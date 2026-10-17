using System.Collections.ObjectModel;
using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.LogicalTree;
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
                CellTemplate = CellTemplate(header: true, column: -1),
            });
        }

        for (var column = 0; column < _columnCount; column++)
        {
            Columns.Add(new TableViewColumn
            {
                Header = Header(column),
                Width = new GridLength(140),
                CellTemplate = CellTemplate(header: false, column),
            });
        }
    }

    private IDataTemplate CellTemplate(bool header, int column) =>
        new FuncDataTemplate<int>((row, _) =>
        {
            var text = new SeriesTableCell(this, header, column)
            {
                Text = header ? RowName(row) : string.Empty,
                Padding = new Thickness(6, 2),
                TextTrimming = Avalonia.Media.TextTrimming.CharacterEllipsis,
            };

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
    }

    private sealed class SeriesTableCell : TextBlock
    {
        private readonly SeriesTable _table;
        private readonly bool _header;
        private readonly int _column;

        public SeriesTableCell(SeriesTable table, bool header, int column)
        {
            _table = table;
            _header = header;
            _column = column;
            AttachedToVisualTree += (_, _) => _table.Changed += OnChanged;
            DetachedFromVisualTree += (_, _) => _table.Changed -= OnChanged;
            DataContextChanged += (_, _) => Update();
        }

        private void OnChanged(object? sender, EventArgs e) => Update();

        private void Update()
        {
            if (DataContext is int row)
            {
                Text = _header ? _table.RowName(row) : _table.GetCellText(row, _column);
            }
        }
    }
}
