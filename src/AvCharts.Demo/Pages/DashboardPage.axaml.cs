using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using AvCharts.Controls;
using AvCharts.Demo.Dashboard;
using AvCharts.Series;

namespace AvCharts.Demo.Pages;

/// <summary>The operations dashboard: the gallery's first page and its MVVM example.</summary>
public partial class DashboardPage : DemoPage
{
    private readonly DashboardViewModel _viewModel;

    /// <summary>Creates the page and its ViewModel.</summary>
    public DashboardPage()
    {
        AvaloniaXamlLoader.Load(this);

        _viewModel = new DashboardViewModel(new DashboardFeed());
        DataContext = _viewModel;

        BuildCharts();

        AttachedToVisualTree += (_, _) => _viewModel.StartFeed();
        DetachedFromVisualTree += (_, _) => _viewModel.StopFeed();
    }

    /// <summary>Makes the page, for the catalog.</summary>
    /// <returns>The page.</returns>
    public static DemoPage Create() => new DashboardPage();

    /// <summary>
    /// Builds the series structure, which is the view's business rather than the ViewModel's.
    /// </summary>
    /// <remarks>
    /// A series' data source and name are plain properties rather than styled ones, so the markup compiler
    /// cannot bind them. The ViewModel owns the collections and the labels; this puts one series per collection
    /// on each chart, and connects the legend and the pie's click command to the charts they belong to.
    /// </remarks>
    private void BuildCharts()
    {
        var throughput = this.FindControl<CartesianChart>("ThroughputChart")!;

        throughput.Series.Add(new ColumnSeries<ThroughputPoint>
        {
            DisplayName = _viewModel.ReceivedLabel,
            ItemsSource = _viewModel.Throughput,
            XSelector = point => point.When,
            YSelector = point => point.Received,
        });

        throughput.Series.Add(new ColumnSeries<ThroughputPoint>
        {
            DisplayName = _viewModel.RejectedLabel,
            ItemsSource = _viewModel.Throughput,
            XSelector = point => point.When,
            YSelector = point => point.Rejected,
        });

        var oee = this.FindControl<CartesianChart>("OeeChart")!;

        oee.Series.Add(new LineSeries<OeePoint>
        {
            DisplayName = _viewModel.OeeLabel,
            ItemsSource = _viewModel.OeePoints,
            XSelector = point => point.When,
            YSelector = point => point.Oee,
            StrokeThickness = 2,
        });

        var pie = this.FindControl<PieChart>("AlarmPie")!;

        pie.Series.Add(new PieSeries<AlarmLevelCount>
        {
            DisplayName = Text.Say("Active alarms"),
            ItemsSource = _viewModel.AlarmLevels,
            ValueSelector = level => level.Count,
            NameSelector = level => level.Label,
            LabelSelector = level => level.LabelText,
            LabelPosition = PieLabelPosition.Inside,
            LabelFontSize = 9,
        });

        pie.ItemClickCommand = _viewModel.FilterLevelCommand;

        this.FindControl<Legend>("MainLegend")!.Chart = throughput;
    }
}
