using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AvCharts.Controls;
using AvCharts.Series;
using AvCharts.Themes;
using Xunit;
using ResizeGlyph = Avalonia.Controls.Shapes.Path;

namespace AvCharts.Tests;

/// <summary>
/// Checks the table as a view of series: either the series are columns and the records are rows, or the other
/// way round; a cell's words can be replaced without changing the series, and slots can be inserted or appended.
/// </summary>
[Collection(AvaloniaCollection.Name)]
public class SeriesTableTests
{
    [Fact]
    public void Series_are_columns_by_default_and_their_values_are_cells()
    {
        var first = Line(1, 2, 3);
        var second = Line(10, 20, 30);
        var table = new SeriesTable();

        table.Series.Add(first);
        table.Series.Add(second);

        Assert.Equal(3, first.RecordCount);
        Assert.Equal(3, table.RowCount);
        Assert.Equal(2, table.ColumnCount);
        Assert.Equal("2", table.GetCellText(1, 0));
        Assert.Equal("30", table.GetCellText(2, 1));
    }

    [Fact]
    public void Series_can_be_rows_instead()
    {
        var first = Line(1, 2, 3);
        var second = Line(10, 20, 30);
        var table = new SeriesTable { Orientation = SeriesTableOrientation.SeriesAsRows };

        table.Series.Add(first);
        table.Series.Add(second);

        Assert.Equal(2, table.RowCount);
        Assert.Equal(3, table.ColumnCount);
        Assert.Equal("20", table.GetCellText(1, 1));
        Assert.Equal("30", table.GetCellText(1, 2));
    }

    [Fact]
    public void A_cell_can_say_something_else_without_changing_the_series()
    {
        var series = Line(1, 2, 3);
        var table = new SeriesTable();
        table.Series.Add(series);

        table.SetCellText(1, 0, "two");

        Assert.Equal("two", table.GetCellText(1, 0));
        Assert.Equal("2", series.ValueTextAt(1));
    }

    [Fact]
    public void A_formatter_changes_only_what_the_table_says()
    {
        var series = Line(1, 2, 3);
        var table = new SeriesTable { CellFormatter = (_, index) => $"row {index}" };
        table.Series.Add(series);

        Assert.Equal("row 1", table.GetCellText(1, 0));
        Assert.Equal("2", series.ValueTextAt(1));
    }

    [Fact]
    public void A_cell_can_set_its_own_background_text_and_border()
    {
        var table = new SeriesTable
        {
            CellStyleSelector = (_, index) => index == 1
                ? new SeriesTableCellStyle(
                    Background: Brushes.LightYellow,
                    Foreground: Brushes.DarkRed,
                    BorderBrush: Brushes.DarkBlue,
                    BorderThickness: new Thickness(2))
                : null,
        };

        table.Series.Add(Line(1, 2, 3));

        var style = table.GetCellStyle(1, 0);

        Assert.NotNull(style);
        Assert.Same(Brushes.LightYellow, style.Background);
        Assert.Same(Brushes.DarkRed, style.Foreground);
        Assert.Same(Brushes.DarkBlue, style.BorderBrush);
        Assert.Equal(new Thickness(2), style.BorderThickness);
        Assert.Null(table.GetCellStyle(0, 0));
    }

    [Fact]
    public void A_cell_style_can_be_overridden_and_follows_its_row_when_one_is_inserted()
    {
        var table = new SeriesTable();
        table.Series.Add(Line(1, 2, 3));
        table.SetCellStyle(
            1,
            0,
            new SeriesTableCellStyle(
                Background: Brushes.LightYellow,
                Foreground: Brushes.DarkRed,
                BorderBrush: Brushes.DarkBlue,
                BorderThickness: new Thickness(1)));
        table.SetCellStyle(
            1,
            0,
            new SeriesTableCellStyle(Background: Brushes.Gold, Foreground: Brushes.Black));

        table.InsertRow(0);

        Assert.Equal(Brushes.Gold, table.GetCellStyle(2, 0)?.Background);
        Assert.Null(table.GetCellStyle(1, 0));
    }

    [Fact]
    public void A_newest_record_can_be_inserted_at_the_top_without_growing_the_table()
    {
        var source = new List<double> { 3, 2, 1 };
        var series = new LineSeries<double> { ItemsSource = source, YSelector = value => value };
        var table = new SeriesTable();
        table.Series.Add(series);

        source.Insert(0, 4);
        source.RemoveAt(3);
        series.InvalidateData();

        Assert.Equal(3, table.RowCount);
        Assert.Equal("4", table.GetCellText(0, 0));
        Assert.Equal("3", table.GetCellText(1, 0));
        Assert.Equal("2", table.GetCellText(2, 0));
    }

    [Fact]
    public void A_record_changed_in_place_updates_its_cell()
    {
        var source = new List<double> { 10, 20, 30 };
        var series = new LineSeries<double> { ItemsSource = source, YSelector = value => value };
        var table = new SeriesTable();
        table.Series.Add(series);

        source[1] = 25;
        series.InvalidateData();

        Assert.Equal(3, table.RowCount);
        Assert.Equal("10", table.GetCellText(0, 0));
        Assert.Equal("25", table.GetCellText(1, 0));
        Assert.Equal("30", table.GetCellText(2, 0));
    }

    [Fact]
    public void Rows_and_columns_can_have_borders_of_their_own()
    {
        var table = new SeriesTable
        {
            RowBorderSelector = row => row == 1
                ? new SeriesTableBorderStyle(Brushes.DarkOrange, new Thickness(0, 0, 0, 2))
                : null,
            ColumnBorderSelector = column => column == 0
                ? new SeriesTableBorderStyle(Brushes.DarkBlue, new Thickness(0, 0, 2, 0))
                : null,
        };

        table.Series.Add(Line(1, 2, 3));

        Assert.Same(Brushes.DarkOrange, table.GetRowBorder(1)?.Brush);
        Assert.Equal(new Thickness(0, 0, 0, 2), table.GetRowBorder(1)?.Thickness);
        Assert.Same(Brushes.DarkBlue, table.GetColumnBorder(0)?.Brush);
        Assert.Null(table.GetRowBorder(0));

        table.SetRowBorder(1, new SeriesTableBorderStyle(Brushes.Red, new Thickness(0, 0, 0, 3)));
        table.InsertRow(0);

        Assert.Equal(3, table.GetRowBorder(2)?.Thickness.Bottom);
    }

    [Fact]
    public void A_whole_cell_takes_its_background_not_only_the_words()
    {
        var table = new SeriesTable();
        table.Series.Add(Line(1, 2, 3));
        table.SetCellStyle(
            1,
            0,
            new SeriesTableCellStyle(
                Background: Brushes.Tomato,
                BorderBrush: Brushes.DarkBlue,
                BorderThickness: new Thickness(1)));

        var window = new Window
        {
            Width = 400,
            Height = 200,
            Resources = ChartTheme.Create(),
            Content = table,
        };

        window.Show();
        window.Measure(new Size(400, 200));
        window.Arrange(new Rect(0, 0, 400, 200));
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();

        var cell = table.GetVisualDescendants()
            .OfType<TableViewCell>()
            .First(cell => Equals(cell.DataContext, 1));
        var content = table.GetVisualDescendants()
            .OfType<Control>()
            .Single(control => control.GetType().Name == "SeriesTableCell" && Equals(control.DataContext, 1));
        Assert.Equal(cell.Bounds.Size, content.Bounds.Size);
        var at = cell.TranslatePoint(new Point(), table);
        Assert.NotNull(at);

        var picture = new Avalonia.Media.Imaging.RenderTargetBitmap(new PixelSize(400, 200));
        picture.Render(table);

        var area = (int)(cell.Bounds.Width * cell.Bounds.Height);
        var painted = Ink.Count(picture, Brushes.Tomato.Color, new Rect(at!.Value, cell.Bounds.Size));

        Assert.True(
            painted > area * 0.7,
            $"the cell background covers {painted} of its {area} pixels");
        Assert.True(
            Ink.Count(picture, Brushes.DarkBlue.Color, new Rect(at.Value, cell.Bounds.Size)) > 0,
            "the cell border was not painted");
        window.Close();
    }

    [Fact]
    public void A_record_arriving_extends_the_rows()
    {
        var values = new ObservableCollection<double> { 1, 2, 3 };
        var series = new LineSeries<double> { ItemsSource = values, YSelector = value => value };
        var table = new SeriesTable();
        table.Series.Add(series);

        values.Add(4);

        Assert.Equal(4, table.RowCount);
        Assert.Equal("4", table.GetCellText(3, 0));
    }

    [Fact]
    public void Inserting_and_appending_slots_keeps_a_cell_with_its_row()
    {
        var table = new SeriesTable();
        table.Series.Add(Line(1, 2, 3));
        table.SetCellText(1, 0, "kept");

        table.InsertRow(0);
        table.AppendColumn();
        table.AppendRow();

        Assert.Equal(5, table.RowCount);
        Assert.Equal(2, table.ColumnCount);
        Assert.Equal("kept", table.GetCellText(2, 0));
        Assert.Equal(string.Empty, table.GetCellText(4, 0));
        Assert.Equal(string.Empty, table.GetCellText(0, 1));
    }

    [Fact]
    public void The_table_realises_rows_and_headers()
    {
        var table = new SeriesTable();
        table.Series.Add(Line(1, 2, 3));
        table.SetCellStyle(
            1,
            0,
            new SeriesTableCellStyle(
                Background: Brushes.LightYellow,
                Foreground: Brushes.DarkRed,
                BorderBrush: Brushes.DarkBlue,
                BorderThickness: new Thickness(1)));

        var window = new Window
        {
            Width = 400,
            Height = 200,
            Resources = ChartTheme.Create(),
            Content = table,
        };

        window.Show();
        window.Measure(new Size(400, 200));
        window.Arrange(new Rect(0, 0, 400, 200));
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();

        Assert.NotNull(table.Template);
        var row = table.GetVisualDescendants().OfType<TableViewRow>().First();
        Assert.True(row.Bounds.Height > 0, $"the row has no height: {row.Bounds}");

        var cell = table.GetVisualDescendants().OfType<TableViewCell>().First();
        Assert.True(cell.Bounds.Width > 0 && cell.Bounds.Height > 0, $"the cell has no room: {cell.Bounds}");

        var text = Assert.Single(table.GetVisualDescendants().OfType<TextBlock>(), text => text.Text == "2");
        Assert.True(text.Bounds.Width > 0 && text.Bounds.Height > 0, $"the text has no room: {text.Bounds}");
        Assert.Same(Brushes.DarkRed, text.Foreground);
        Assert.NotEmpty(table.GetVisualDescendants().OfType<TableViewColumnHeader>());

        var picture = new Avalonia.Media.Imaging.RenderTargetBitmap(new PixelSize(400, 200));
        picture.Render(table);
        Assert.True(Ink.Count(picture) > 0, "the table has rows but painted nothing");
        window.Close();
    }

    [Fact]
    public void The_table_draws_on_its_first_layout()
    {
        var table = new SeriesTable();
        table.Series.Add(Line(1, 2, 3));

        var window = new Window
        {
            Width = 400,
            Height = 200,
            Resources = ChartTheme.Create(),
            Content = table,
        };

        window.Show();
        window.Measure(new Size(400, 200));
        window.Arrange(new Rect(0, 0, 400, 200));

        var picture = new Avalonia.Media.Imaging.RenderTargetBitmap(new PixelSize(400, 200));
        picture.Render(table);

        Assert.True(Ink.Count(picture) > 0, "a table that has just been laid out painted nothing");
        window.Close();
    }

    [Fact]
    public void The_headers_follow_the_horizontal_scroll_and_keep_a_resize_grip()
    {
        var table = new SeriesTable
        {
            Orientation = SeriesTableOrientation.SeriesAsRows,
            Width = 420,
            Height = 220,
        };

        for (var series = 0; series < 4; series++)
        {
            table.Series.Add(Line([.. Enumerable.Range(0, 20).Select(record => (double)(series * 20) + record)]));
        }

        var window = new Window
        {
            Width = 420,
            Height = 220,
            Resources = ChartTheme.Create(),
            Content = table,
        };

        window.Show();
        window.Measure(new Size(420, 220));
        window.Arrange(new Rect(0, 0, 420, 220));
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();

        var header = table.GetVisualDescendants().OfType<TableViewColumnHeader>().ElementAt(1);
        var grip = header.GetVisualDescendants().OfType<Thumb>().FirstOrDefault(thumb => thumb.Name == "PART_Resizer");
        var glyph = header.GetVisualDescendants().OfType<ResizeGlyph>().FirstOrDefault(path => path.Name == "PART_ResizeGlyph");
        var scroll = table.Scroll;
        var before = header.TranslatePoint(new Point(), table);

        Assert.NotNull(grip);
        Assert.NotNull(glyph);
        Assert.NotNull(before);
        Assert.True(header.Bounds.Height > 0, $"the header has no height: {header.Bounds}");
        Assert.Equal(0, glyph!.Opacity, 1);

        var overHeader = header.TranslatePoint(new Point(header.Bounds.Width / 2, header.Bounds.Height / 2), window);

        Assert.NotNull(overHeader);
        window.MouseMove(overHeader!.Value, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
        Assert.True(header.IsPointerOver, "the header is not pointer-over");
        Assert.Equal(1, glyph.Opacity, 1);

        window.MouseMove(new Point(5, 5), RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(0, glyph.Opacity, 1);

        Assert.NotNull(scroll);
        scroll!.Offset = new Vector(200, 0);
        Dispatcher.UIThread.RunJobs();
        table.Measure(new Size(420, 220));
        table.Arrange(new Rect(0, 0, 420, 220));
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();

        var after = header.TranslatePoint(new Point(), table);

        Assert.NotNull(after);
        Assert.Equal(before!.Value.X - 200, after!.Value.X, 1);

        var column = header.Column;

        Assert.NotNull(column);
        Assert.True(column!.CanUserEffectivelyResize, "the column says it cannot be resized");
        Assert.Equal(6, grip!.Bounds.Width, 1);

        IReadOnlyList<SeriesTableColumnWidth>? reported = null;
        table.ColumnWidthsChanged = widths => reported = widths;
        var beforeWidth = column.Width.Value;
        grip.RaiseEvent(new VectorEventArgs
        {
            RoutedEvent = Thumb.DragDeltaEvent,
            Vector = new Vector(30, 0),
        });

        Assert.Equal(beforeWidth + 30, column.Width.Value, 1);
        Assert.NotNull(reported);
        Assert.Equal(beforeWidth + 30, reported![1].Width, 1);
        window.Close();
    }

    [Fact]
    public void Column_widths_can_be_saved_restored_and_reported()
    {
        var table = new SeriesTable();

        table.Series.Add(Line(1, 2, 3));
        table.Series.Add(Line(4, 5, 6));
        table.SetColumnWidths(table.ColumnWidths.Select(width => new SeriesTableColumnWidth(width.Key, 200)));

        Assert.All(table.ColumnWidths, width => Assert.Equal(200, width.Width));

        IReadOnlyList<SeriesTableColumnWidth>? reported = null;
        table.ColumnWidthsChanged = widths => reported = widths;
        table.Columns[0].Width = new GridLength(180);

        Assert.NotNull(reported);
        Assert.Equal(180, reported![0].Width);

        // A new series rebuilds the columns; the widths that were already named keep their size, and the
        // column that has not been seen before starts at the table's default.
        table.Series.Add(Line(7, 8, 9));

        Assert.Equal(180, table.ColumnWidths[0].Width);
        Assert.Equal(200, table.ColumnWidths[1].Width);
        Assert.Equal(140, table.ColumnWidths[2].Width);
    }

    private static LineSeries<double> Line(params double[] values) =>
        new()
        {
            DisplayName = string.Join("-", values),
            ItemsSource = values,
            YSelector = value => value,
        };
}
