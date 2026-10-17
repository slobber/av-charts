using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AvCharts.Controls;
using AvCharts.Series;
using AvCharts.Themes;
using Xunit;

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

    private static LineSeries<double> Line(params double[] values) =>
        new()
        {
            DisplayName = string.Join("-", values),
            ItemsSource = values,
            YSelector = value => value,
        };
}
