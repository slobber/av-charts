using System.Collections.ObjectModel;
using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Threading;
using AvCharts.Series;

namespace AvCharts.Controls;

/// <summary>
/// A read-only table of series: rows or columns are series, the other direction is their records.
/// </summary>
/// <remarks>
/// <para>
/// The table is a view and not a second data store. A cell asks its series for the record's text when the cell is
/// realised, so a series of a million records does not make a million strings; only the rows in the viewport are
/// built, because the table is an Avalonia <see cref="TableView"/> underneath.
/// </para>
/// <para>
/// <see cref="SetCellText"/> replaces what a cell says without changing the series: a formatter's answer is the
/// default, and an override is the host's. <see cref="InsertRow"/> and <see cref="InsertColumn"/> insert display
/// slots of their own, which is how a blank row or column can be reserved before its series data arrives.
/// </para>
/// </remarks>
public sealed class SeriesTable : TableView
{
    private readonly ObservableCollection<int> _rows = [];
    private readonly Dictionary<(int Row, int Column), string> _overrides = [];
    private readonly Dictionary<(int Row, int Column), SeriesTableCellStyle> _styles = [];
    private readonly Dictionary<int, SeriesTableBorderStyle> _rowBorders = [];
    private readonly Dictionary<int, SeriesTableBorderStyle> _columnBorders = [];
    private readonly HashSet<SeriesBase> _subscribed = new(ReferenceEqualityComparer.Instance);
    private int _rowCount;
    private int _columnCount;
    private int _extraRows;
    private int _extraColumns;
    private int[] _recordCounts = [];

    /// <summary>Creates an empty table.</summary>
    public SeriesTable()
    {
        ItemsSource = _rows;
        CanUserResizeColumns = true;
        Series.CollectionChanged += OnSeriesChanged;
        Refresh(sync: true);
    }

    /// <inheritdoc />
    protected override void OnAttachedToLogicalTree(LogicalTreeAttachmentEventArgs e)
    {
        base.OnAttachedToLogicalTree(e);

        // TableView builds its row containers during its own template pass. Applying it as the table joins the
        // tree means the first layout has something to arrange, which is the frame a host or a golden renders.
        ApplyTemplate();
        InvalidateMeasure();
    }

    /// <summary>The series shown, in their row or column order.</summary>
    public Avalonia.Collections.AvaloniaList<SeriesBase> Series { get; } = [];

    /// <summary>Whether series are the columns or the rows.</summary>
    public SeriesTableOrientation Orientation
    {
        get;
        set
        {
            if (field == value)
            {
                return;
            }

            field = value;
            _extraRows = 0;
            _extraColumns = 0;
            _overrides.Clear();
            Refresh(sync: true);
        }
    } = SeriesTableOrientation.SeriesAsColumns;

    /// <summary>How a cell is written; unset, the series writes its own value.</summary>
    /// <remarks>The formatter's answer is only displayed. It never changes the series, and an explicit
    /// <see cref="SetCellText"/> wins over it.</remarks>
    public Func<SeriesBase, int, string>? CellFormatter
    {
        get;
        set
        {
            field = value;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>How a cell is painted; unset, cells have no background or border and inherit the table's text colour.</summary>
    /// <remarks>A style returned for a record changes with that record, so a value turning negative can turn the
    /// cell red without a new row or column. <see cref="SetCellStyle"/> wins over this selector for one cell.</remarks>
    public Func<SeriesBase, int, SeriesTableCellStyle?>? CellStyleSelector
    {
        get;
        set
        {
            field = value;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>How a row draws its top and bottom border; unset, the row has none of its own.</summary>
    public Func<int, SeriesTableBorderStyle?>? RowBorderSelector
    {
        get;
        set
        {
            field = value;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>How a column draws its left and right border; unset, the column has none of its own.</summary>
    public Func<int, SeriesTableBorderStyle?>? ColumnBorderSelector
    {
        get;
        set
        {
            field = value;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Whether appending a row scrolls the newest one into view.</summary>
    public bool AutoScrollToEnd { get; set; } = true;

    /// <summary>How many rows the table shows.</summary>
    public int RowCount => _rowCount;

    /// <summary>How many data columns the table shows, not counting the series-name column.</summary>
    public int ColumnCount => _columnCount;

    /// <summary>Raised when a realised cell should ask for its text again.</summary>
    internal event EventHandler? Changed;

    /// <summary>The words a cell shows.</summary>
    /// <param name="row">The row.</param>
    /// <param name="column">The data column.</param>
    /// <returns>The override, the formatter's answer, or the series' value; empty outside the series.</returns>
    public string GetCellText(int row, int column)
    {
        if (row < 0 || row >= _rowCount || column < 0 || column >= _columnCount)
        {
            return string.Empty;
        }

        if (_overrides.TryGetValue((row, column), out var replaced))
        {
            return replaced;
        }

        var (series, record) = Resolve(row, column);

        if (series is null || record < 0 || record >= series.RecordCount)
        {
            return string.Empty;
        }

        return CellFormatter?.Invoke(series, record) ?? series.ValueTextAt(record);
    }

    /// <summary>Replaces the words one cell shows without changing the series.</summary>
    /// <param name="row">The row.</param>
    /// <param name="column">The data column.</param>
    /// <param name="text">The words, or null to show the formatter or series value again.</param>
    public void SetCellText(int row, int column, string? text)
    {
        if (row < 0 || row >= _rowCount || column < 0 || column >= _columnCount)
        {
            throw new ArgumentOutOfRangeException(nameof(row), "The cell is outside the table.");
        }

        if (text is null)
        {
            _overrides.Remove((row, column));
        }
        else
        {
            _overrides[(row, column)] = text;
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>The style a cell wears, after its own override and then the selector.</summary>
    /// <param name="row">The row.</param>
    /// <param name="column">The data column.</param>
    /// <returns>The style, or null when the cell keeps the table's default appearance.</returns>
    public SeriesTableCellStyle? GetCellStyle(int row, int column)
    {
        if (row < 0 || row >= _rowCount || column < 0 || column >= _columnCount)
        {
            return null;
        }

        if (_styles.TryGetValue((row, column), out var style))
        {
            return style;
        }

        var (series, record) = Resolve(row, column);
        return series is null || record < 0 || record >= series.RecordCount
            ? null
            : CellStyleSelector?.Invoke(series, record);
    }

    /// <summary>Replaces the appearance of one cell without changing its series or the table's shape.</summary>
    /// <param name="row">The row.</param>
    /// <param name="column">The data column.</param>
    /// <param name="style">The style, or null to return to the selector or the table's default.</param>
    public void SetCellStyle(int row, int column, SeriesTableCellStyle? style)
    {
        if (row < 0 || row >= _rowCount || column < 0 || column >= _columnCount)
        {
            throw new ArgumentOutOfRangeException(nameof(row), "The cell is outside the table.");
        }

        if (style is { } wanted)
        {
            _styles[(row, column)] = wanted;
        }
        else
        {
            _styles.Remove((row, column));
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>The border a row draws, after its own override and then the selector.</summary>
    /// <param name="row">The row.</param>
    /// <returns>The border, or null when the row has none.</returns>
    public SeriesTableBorderStyle? GetRowBorder(int row)
    {
        if (row < 0 || row >= _rowCount)
        {
            return null;
        }

        return _rowBorders.TryGetValue(row, out var style) ? style : RowBorderSelector?.Invoke(row);
    }

    /// <summary>The border a column draws, after its own override and then the selector.</summary>
    /// <param name="column">The data column.</param>
    /// <returns>The border, or null when the column has none.</returns>
    public SeriesTableBorderStyle? GetColumnBorder(int column)
    {
        if (column < 0 || column >= _columnCount)
        {
            return null;
        }

        return _columnBorders.TryGetValue(column, out var style) ? style : ColumnBorderSelector?.Invoke(column);
    }

    /// <summary>Replaces the border of one row.</summary>
    /// <param name="row">The row.</param>
    /// <param name="style">The border, or null to return to the selector.</param>
    public void SetRowBorder(int row, SeriesTableBorderStyle? style)
    {
        if (row < 0 || row >= _rowCount)
        {
            throw new ArgumentOutOfRangeException(nameof(row));
        }

        if (style is null)
        {
            _rowBorders.Remove(row);
        }
        else
        {
            _rowBorders[row] = style;
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Replaces the border of one column.</summary>
    /// <param name="column">The data column.</param>
    /// <param name="style">The border, or null to return to the selector.</param>
    public void SetColumnBorder(int column, SeriesTableBorderStyle? style)
    {
        if (column < 0 || column >= _columnCount)
        {
            throw new ArgumentOutOfRangeException(nameof(column));
        }

        if (style is null)
        {
            _columnBorders.Remove(column);
        }
        else
        {
            _columnBorders[column] = style;
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Inserts a display row before an existing one.</summary>
    /// <param name="index">Where it goes; the row count itself appends it.</param>
    public void InsertRow(int index)
    {
        if (index < 0 || index > _rowCount)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        _extraRows++;
        ShiftOverrides(row: index, rowDelta: 1, column: null, columnDelta: 0);
        Refresh(sync: true);
    }

    /// <summary>Appends a display row.</summary>
    public void AppendRow() => InsertRow(_rowCount);

    /// <summary>Inserts a display column before an existing one.</summary>
    /// <param name="index">Where it goes; the column count itself appends it.</param>
    public void InsertColumn(int index)
    {
        if (index < 0 || index > _columnCount)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        _extraColumns++;
        ShiftOverrides(row: null, rowDelta: 0, column: index, columnDelta: 1);
        Refresh(sync: true);
    }

    /// <summary>Appends a display column.</summary>
    public void AppendColumn() => InsertColumn(_columnCount);

    private (SeriesBase? Series, int Record) Resolve(int row, int column) =>
        Orientation == SeriesTableOrientation.SeriesAsColumns
            ? (column < Series.Count ? Series[column] : null, row)
            : (row < Series.Count ? Series[row] : null, column);

    private void OnSeriesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Reset)
        {
            foreach (var old in _subscribed)
            {
                old.Invalidated -= OnSeriesInvalidated;
            }

            _subscribed.Clear();
        }
        else if (e.OldItems is not null)
        {
            foreach (SeriesBase series in e.OldItems)
            {
                series.Invalidated -= OnSeriesInvalidated;
                _subscribed.Remove(series);
            }
        }

        foreach (var series in Series)
        {
            if (_subscribed.Add(series))
            {
                series.Invalidated += OnSeriesInvalidated;
            }
        }

        Refresh(sync: true);
    }

    private void OnSeriesInvalidated(object? sender, EventArgs e) => Refresh(sync: true);

    private void Refresh(bool sync)
    {
        var counts = Series.Select(series => series.RecordCount).ToArray();
        var rowsMoved = !counts.SequenceEqual(_recordCounts);
        _recordCounts = counts;

        var records = counts.Length == 0 ? 0 : counts.Max();
        var rows = Orientation == SeriesTableOrientation.SeriesAsColumns
            ? records + _extraRows
            : Series.Count + _extraRows;
        var columns = Orientation == SeriesTableOrientation.SeriesAsColumns
            ? Series.Count + _extraColumns
            : records + _extraColumns;
        var shapeMoved = rows != _rowCount || columns != _columnCount;

        _rowCount = rows;
        _columnCount = columns;

        if (sync && (shapeMoved || rowsMoved))
        {
            while (_rows.Count > _rowCount)
            {
                _rows.RemoveAt(_rows.Count - 1);
            }

            while (_rows.Count < _rowCount)
            {
                _rows.Add(_rows.Count);
            }

            RebuildColumns();
        }

        Changed?.Invoke(this, EventArgs.Empty);

        if (AutoScrollToEnd && VisualRoot is not null && _rowCount > 0)
        {
            Dispatcher.UIThread.Post(() => ScrollIntoView(_rowCount - 1));
        }
    }

    private void RebuildColumns()
    {
        Columns.Clear();

        if (Orientation == SeriesTableOrientation.SeriesAsRows)
        {
            Columns.Add(new TableViewColumn
            {
                Header = "Series",
                Width = new GridLength(140),
                HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
                CellTemplate = CellTemplate(header: true, column: -1),
            });
        }

        for (var column = 0; column < _columnCount; column++)
        {
            Columns.Add(new TableViewColumn
            {
                Header = Header(column),
                Width = new GridLength(140),
                HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
                CellTemplate = CellTemplate(header: false, column),
            });
        }
    }

    private IDataTemplate CellTemplate(bool header, int column) =>
        new FuncDataTemplate<int>((row, _) =>
        {
            var text = new SeriesTableCell(this, header, column);

            text.DataContext = row;
            return text;
        });

    private string Header(int column)
    {
        if (Orientation == SeriesTableOrientation.SeriesAsColumns)
        {
            return column < Series.Count
                ? Series[column].DisplayName is { Length: > 0 } name ? name : $"Series {column + 1}"
                : $"Column {column + 1}";
        }

        return column.ToString();
    }

    private string RowName(int row) =>
        Orientation == SeriesTableOrientation.SeriesAsRows && row < Series.Count
            ? Series[row].DisplayName is { Length: > 0 } name ? name : $"Series {row + 1}"
            : string.Empty;

    private void ShiftOverrides(int? row, int rowDelta, int? column, int columnDelta)
    {
        var shifted = new Dictionary<(int Row, int Column), string>();

        foreach (var (key, text) in _overrides)
        {
            var nextRow = row is { } rowAt && key.Row >= rowAt ? key.Row + rowDelta : key.Row;
            var nextColumn = column is { } columnAt && key.Column >= columnAt
                ? key.Column + columnDelta
                : key.Column;

            shifted[(nextRow, nextColumn)] = text;
        }

        _overrides.Clear();

        foreach (var (key, text) in shifted)
        {
            _overrides[key] = text;
        }

        var shiftedStyles = new Dictionary<(int Row, int Column), SeriesTableCellStyle>();

        foreach (var (key, style) in _styles)
        {
            var nextRow = row is { } rowAt && key.Row >= rowAt ? key.Row + rowDelta : key.Row;
            var nextColumn = column is { } columnAt && key.Column >= columnAt
                ? key.Column + columnDelta
                : key.Column;

            shiftedStyles[(nextRow, nextColumn)] = style;
        }

        _styles.Clear();

        foreach (var (key, style) in shiftedStyles)
        {
            _styles[key] = style;
        }

        if (row is { } rowShift)
        {
            var shiftedRows = new Dictionary<int, SeriesTableBorderStyle>();

            foreach (var (key, style) in _rowBorders)
            {
                shiftedRows[key >= rowShift ? key + rowDelta : key] = style;
            }

            _rowBorders.Clear();

            foreach (var (key, style) in shiftedRows)
            {
                _rowBorders[key] = style;
            }
        }

        if (column is { } columnShift)
        {
            var shiftedColumns = new Dictionary<int, SeriesTableBorderStyle>();

            foreach (var (key, style) in _columnBorders)
            {
                shiftedColumns[key >= columnShift ? key + columnDelta : key] = style;
            }

            _columnBorders.Clear();

            foreach (var (key, style) in shiftedColumns)
            {
                _columnBorders[key] = style;
            }
        }
    }

    private sealed class SeriesTableCell : Control
    {
        private readonly SeriesTable _table;
        private readonly bool _header;
        private readonly int _column;
        private readonly TextBlock _text;
        private static readonly Thickness Inset = new(6, 2);
        private IBrush? _background;
        private SeriesTableCellStyle? _style;

        public SeriesTableCell(SeriesTable table, bool header, int column)
        {
            _table = table;
            _header = header;
            _column = column;
            _text = new TextBlock { TextTrimming = Avalonia.Media.TextTrimming.CharacterEllipsis };
            VisualChildren.Add(_text);
            LogicalChildren.Add(_text);
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch;
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch;
            AttachedToVisualTree += (_, _) => _table.Changed += OnChanged;
            DetachedFromVisualTree += (_, _) => _table.Changed -= OnChanged;
            DataContextChanged += (_, _) => Update();
        }

        private void OnChanged(object? sender, EventArgs e) => Update();

        private void Update()
        {
            if (DataContext is int row)
            {
                _text.Text = _header ? _table.RowName(row) : _table.GetCellText(row, _column);
                _style = _header ? null : _table.GetCellStyle(row, _column);
                _background = _style?.Background;
                _text.Foreground = _style?.Foreground ?? _table.Foreground;
                InvalidateVisual();
            }
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            _text.Measure(availableSize.Deflate(Inset));
            return _text.DesiredSize.Inflate(Inset);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            _text.Arrange(new Rect(finalSize).Deflate(Inset));
            return finalSize;
        }

        public override void Render(DrawingContext context)
        {
            if (_background is not null)
            {
                context.FillRectangle(_background, new Rect(Bounds.Size));
            }

            if (DataContext is not int row)
            {
                return;
            }

            if (!_header && _style is { BorderBrush: { } cellBrush } cell)
            {
                DrawEdges(context, new Rect(Bounds.Size), cellBrush, cell.BorderThickness);
            }

            if (_table.GetRowBorder(row) is { Brush: { } rowBrush } rowBorder)
            {
                DrawEdges(
                    context,
                    new Rect(Bounds.Size),
                    rowBrush,
                    new Thickness(rowBorder.Thickness.Left, rowBorder.Thickness.Top, 0, rowBorder.Thickness.Bottom));
            }

            if (!_header && _table.GetColumnBorder(_column) is { Brush: { } columnBrush } columnBorder)
            {
                DrawEdges(
                    context,
                    new Rect(Bounds.Size),
                    columnBrush,
                    new Thickness(columnBorder.Thickness.Left, 0, columnBorder.Thickness.Right, 0));
            }
        }

        private static void DrawEdges(DrawingContext context, Rect bounds, IBrush brush, Thickness thickness)
        {
            if (thickness.Top > 0)
            {
                context.DrawLine(
                    new Pen(brush, thickness.Top),
                    new Avalonia.Point(bounds.Left, thickness.Top / 2),
                    new Avalonia.Point(bounds.Right, thickness.Top / 2));
            }

            if (thickness.Bottom > 0)
            {
                context.DrawLine(
                    new Pen(brush, thickness.Bottom),
                    new Avalonia.Point(bounds.Left, bounds.Bottom - (thickness.Bottom / 2)),
                    new Avalonia.Point(bounds.Right, bounds.Bottom - (thickness.Bottom / 2)));
            }

            if (thickness.Left > 0)
            {
                context.DrawLine(
                    new Pen(brush, thickness.Left),
                    new Avalonia.Point(thickness.Left / 2, bounds.Top),
                    new Avalonia.Point(thickness.Left / 2, bounds.Bottom));
            }

            if (thickness.Right > 0)
            {
                context.DrawLine(
                    new Pen(brush, thickness.Right),
                    new Avalonia.Point(bounds.Right - (thickness.Right / 2), bounds.Top),
                    new Avalonia.Point(bounds.Right - (thickness.Right / 2), bounds.Bottom));
            }
        }
    }
}
