using Avalonia;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.VisualTree;
using AvCharts.Axes;
using AvCharts.Controls;
using AvCharts.Data;
using AvCharts.Demo;
using AvCharts.Demo.Dashboard;
using AvCharts.Demo.Pages;
using AvCharts.Series;
using AvCharts.Themes;
using Xunit;

namespace AvCharts.Tests;

/// <summary>
/// Checks the operations dashboard: the ViewModel that owns its state, and the page that shows it.
/// </summary>
/// <remarks>
/// The feed is handed in rather than waited for, which is what the page's own constructor does not do: here a
/// frame is advanced by a method call, so a claim about 100 millisecond points or a four second alarm is a claim
/// about one frame rather than about the machine the build ran on.
/// </remarks>
[Collection(AvaloniaCollection.Name)]
public class DashboardTests
{
    [Fact]
    public void The_first_frame_is_the_one_the_design_asks_for()
    {
        var view = new DashboardViewModel(new ManualFeed());

        Assert.Equal(DashboardRange.Last7Days, view.SelectedRange.Range);
        Assert.Equal(2, view.Throughput.Count);
        Assert.Equal(2, view.OeePoints.Count);
        Assert.Equal(4, view.LineAPoints.Count);
        Assert.Equal(4, view.LineBPoints.Count);
        Assert.Equal(6, view.Alarms.Count);
        Assert.Equal(6, view.VisibleAlarms.Count);
        Assert.Equal(6, view.ActiveAlarms);

        // Two info, three warnings and one critical: the pie and the KPI read the same six alarms.
        Assert.Equal(2, view.AlarmLevels.Single(level => level.Severity == AlarmSeverity.Info).Count);
        Assert.Equal(3, view.AlarmLevels.Single(level => level.Severity == AlarmSeverity.Warning).Count);
        Assert.Equal(1, view.AlarmLevels.Single(level => level.Severity == AlarmSeverity.Critical).Count);

        Assert.Equal("1,248 u/h", view.ThroughputText);
        Assert.Equal("97.6 %", view.YieldText);
        Assert.Equal("82.4 %", view.OeeText);
        Assert.Equal("6", view.ActiveAlarmText);
        Assert.Equal("Live", view.LiveText);
        Assert.Equal("Pause", view.PauseText);
    }

    [Fact]
    public void New_alarms_arrive_at_the_top_and_the_log_keeps_twenty()
    {
        var feed = new ManualFeed();
        var view = new DashboardViewModel(feed);
        var raised = new List<AlarmEvent>();
        var resets = 0;

        view.VisibleAlarms.CollectionChanged += (_, change) =>
        {
            if (change.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Reset)
            {
                resets++;
            }
        };

        for (var index = 0; index < 25; index++)
        {
            var alarm = new AlarmEvent(
                AlarmAt.AddMinutes(index),
                "Line B",
                AlarmSeverity.Warning,
                $"Frame {index}");

            raised.Add(alarm);
            feed.Raise(alarm);

            Assert.Same(alarm, view.Alarms[0]);
            Assert.InRange(view.Alarms.Count, 1, 20);
        }

        // Twenty kept, newest first: the five oldest frames fell off the bottom.
        Assert.Equal(20, view.Alarms.Count);
        Assert.Same(raised[^1], view.Alarms[0]);
        Assert.Same(raised[^20], view.Alarms[^1]);
        Assert.Equal(20, view.VisibleAlarms.Count);

        // A new alarm is one row added at the top, not the whole view cleared and filled again: a reader who
        // scrolled down the log is not thrown back to the top by the next event.
        Assert.Equal(0, resets);
    }

    [Fact]
    public void Acknowledging_updates_the_alarm_the_kpi_and_the_pie()
    {
        var view = new DashboardViewModel(new ManualFeed());
        var critical = view.Alarms.Single(alarm => alarm.Severity == AlarmSeverity.Critical);

        view.AcknowledgeCommand.Execute(critical);

        Assert.Equal(AlarmState.Acknowledged, critical.State);
        Assert.False(critical.IsActive);
        Assert.Equal("Acknowledged", critical.StateText);
        Assert.Equal(5, view.ActiveAlarms);
        Assert.Equal("5", view.ActiveAlarmText);
        Assert.Equal(0, view.AlarmLevels.Single(level => level.Severity == AlarmSeverity.Critical).Count);

        // The log keeps the row: acknowledging is not deleting.
        Assert.Contains(critical, view.VisibleAlarms);

        // And doing it twice changes nothing, so the KPI cannot be talked into going negative.
        view.AcknowledgeCommand.Execute(critical);

        Assert.Equal(5, view.ActiveAlarms);
    }

    [Fact]
    public void A_wedge_filters_the_log_and_clicking_it_again_clears_the_filter()
    {
        var view = new DashboardViewModel(new ManualFeed());
        var warnings = view.AlarmLevels.Single(level => level.Severity == AlarmSeverity.Warning);
        var series = new PieSeries<AlarmLevelCount>
        {
            ItemsSource = view.AlarmLevels,
            ValueSelector = level => level.Count,
        };
        var clicked = new ChartItemEventArgs(
            [],
            new HitData
            {
                Distance = 0,
                ChartItem = series.Items.First(item => ReferenceEquals(item.Item, warnings)),
                DisplayText = warnings.LabelText,
            });

        view.FilterLevelCommand.Execute(clicked);

        Assert.Equal(AlarmSeverity.Warning, view.SelectedSeverity);
        Assert.Equal(3, view.VisibleAlarms.Count);
        Assert.All(view.VisibleAlarms, alarm => Assert.Equal(AlarmSeverity.Warning, alarm.Severity));

        view.FilterLevelCommand.Execute(clicked);

        Assert.Null(view.SelectedSeverity);
        Assert.Equal(6, view.VisibleAlarms.Count);
    }

    [Fact]
    public void Refresh_returns_to_the_first_frame_and_a_paused_feed_stays_paused()
    {
        var feed = new ManualFeed();
        var view = new DashboardViewModel(feed);

        view.StartFeed();
        Assert.Equal(1, feed.Starts);

        view.TogglePauseCommand.Execute(null);
        Assert.True(view.IsPaused);
        Assert.Equal("Paused", view.LiveText);
        Assert.Equal("Resume", view.PauseText);

        // One stop when the constructor loaded its first frame, and one when the reader pressed Pause.
        Assert.Equal(2, feed.Stops);

        feed.Raise(new AlarmEvent(AlarmAt, "Line A", AlarmSeverity.Critical, "After the pause"));
        view.AcknowledgeCommand.Execute(view.Alarms[0]);
        view.SelectedRange = view.Ranges.Single(option => option.Range == DashboardRange.Last30Days);

        Assert.Equal(DashboardRange.Last30Days, feed.ResetRange);

        view.RefreshCommand.Execute(null);

        Assert.Equal(6, view.Alarms.Count);
        Assert.All(view.Alarms, alarm => Assert.True(alarm.IsActive));
        Assert.Equal(6, view.ActiveAlarms);
        Assert.Equal(2, view.Throughput.Count);
        Assert.True(view.IsPaused);

        // A refresh reloads and holds the frame it reloaded; it does not quietly resume what the reader paused.
        Assert.Equal(1, feed.Starts);

        view.TogglePauseCommand.Execute(null);

        Assert.False(view.IsPaused);
        Assert.Equal(2, feed.Starts);
    }

    [Fact]
    public void A_point_frame_changes_values_and_statuses_but_raises_no_alarm()
    {
        var feed = new ManualFeed();
        var view = new DashboardViewModel(feed);
        var temperature = view.LineAPoints[0];
        var vibration = view.LineAPoints[3];

        feed.Tick(new DashboardTick(
            [
                new PointSample("Temperature", 81.5, PointStatus.Alarm),
                new PointSample("Pressure", 1.9, PointStatus.Normal),
                new PointSample("Flow", 41, PointStatus.Normal),
                new PointSample("Vibration", 0.72, PointStatus.Warning),
                new PointSample("Temperature", 69, PointStatus.Normal),
                new PointSample("Pressure", 2.0, PointStatus.Normal),
                new PointSample("Motor current", 12, PointStatus.Normal),
                new PointSample("Conveyor speed", 1_230, PointStatus.Normal),
            ],
            1_301,
            97.1,
            81.9));

        Assert.Equal(81.5, temperature.Value);
        Assert.Equal(PointStatus.Alarm, temperature.Status);
        Assert.Equal("Alarm", temperature.StatusText);
        Assert.Equal("81.5 °C", temperature.ValueText);
        Assert.Equal(PointStatus.Warning, vibration.Status);

        Assert.Equal("1,301 u/h", view.ThroughputText);
        Assert.Equal("97.1 %", view.YieldText);
        Assert.Equal("81.9 %", view.OeeText);

        // A point crossing its limit is a status, not an event: the alarm log is what the feed raises.
        Assert.Equal(6, view.Alarms.Count);
        Assert.Equal(6, view.ActiveAlarms);
    }

    [Fact]
    public void A_chart_frame_replaces_the_two_windows()
    {
        var feed = new ManualFeed();
        var view = new DashboardViewModel(feed);
        var when = AlarmAt.AddHours(1);
        var throughput = new[]
        {
            new ThroughputPoint(when, 130, 8),
            new ThroughputPoint(when.AddDays(1), 140, 9),
            new ThroughputPoint(when.AddDays(2), 150, 10),
        };
        var oee = new[]
        {
            new OeePoint(when, 83),
            new OeePoint(when.AddDays(1), 84),
            new OeePoint(when.AddDays(2), 85),
        };

        feed.Chart(new DashboardChartTick(throughput, oee));

        Assert.Equal(3, view.Throughput.Count);
        Assert.Equal(3, view.OeePoints.Count);
        Assert.Equal(throughput, view.Throughput);
        Assert.Equal(oee, view.OeePoints);
    }

    [Fact]
    public void The_page_builds_the_charts_the_ViewModel_feeds()
    {
        var page = Show(out var window);

        try
        {
            var view = (DashboardViewModel)page.DataContext!;
            var throughput = page.GetVisualDescendants().OfType<CartesianChart>()
                .Single(chart => chart.Name == "ThroughputChart");
            var oee = page.GetVisualDescendants().OfType<CartesianChart>()
                .Single(chart => chart.Name == "OeeChart");
            var pie = page.GetVisualDescendants().OfType<PieChart>().Single();
            var legend = page.GetVisualDescendants().OfType<Legend>().Single();

            Assert.Equal(2, throughput.Series.Count);
            Assert.All(throughput.Series, series => Assert.IsType<ColumnSeries<ThroughputPoint>>(series));
            Assert.IsType<DateTimeAxis>(throughput.HorizontalAxis);
            Assert.Equal(SeriesLayout.Stacked, throughput.SeriesLayout);
            Assert.True(throughput.ShowCrosshair);
            Assert.Equal(ChartSelectionMode.Single, throughput.SelectionMode);
            Assert.Equal(DataTipMode.Item, throughput.DataTipMode);

            // The dashboard is read at a glance rather than navigated: the pointer may ask a question, not move
            // the window every chart in the row is drawn against.
            Assert.False(throughput.ZoomOnWheel);
            Assert.False(throughput.PanOnDrag);

            Assert.Single(oee.Series);
            Assert.IsType<LineSeries<OeePoint>>(oee.Series[0]);
            Assert.IsType<DateTimeAxis>(oee.HorizontalAxis);
            Assert.False(oee.ZoomOnWheel);
            Assert.False(oee.PanOnDrag);

            Assert.Single(pie.Series);
            Assert.IsType<PieSeries<AlarmLevelCount>>(pie.Series[0]);
            Assert.Same(view.FilterLevelCommand, pie.ItemClickCommand);

            Assert.Same(throughput, legend.Chart);
            Assert.Equal(2, legend.Items.OfType<LegendItem>().Count());
        }
        finally
        {
            window.Close();
        }
    }

    [Fact]
    public void An_ack_button_in_the_log_runs_the_ViewModels_command()
    {
        var page = Show(out var window);

        try
        {
            var view = (DashboardViewModel)page.DataContext!;
            var alarm = view.Alarms[0];
            var log = page.GetVisualDescendants().OfType<ItemsControl>()
                .First(control => control.ItemsSource == view.VisibleAlarms);
            var button = log.GetVisualDescendants().OfType<Button>()
                .First(control => ReferenceEquals(control.CommandParameter, alarm));

            Assert.NotNull(button.Command);
            Assert.True(button.Command.CanExecute(button.CommandParameter));

            button.Command.Execute(button.CommandParameter);

            Assert.Equal(AlarmState.Acknowledged, alarm.State);
            Assert.Equal(5, view.ActiveAlarms);
        }
        finally
        {
            window.Close();
        }
    }

    [Fact]
    public void The_page_is_one_column_when_narrow_and_two_when_wide()
    {
        var page = Show(out var window);

        try
        {
            var panels = page.GetVisualDescendants().OfType<ResponsivePanel>().ToArray();
            var legendPanel = page.GetVisualDescendants().OfType<LegendPanel>().Single();
            var legend = page.GetVisualDescendants().OfType<Legend>().Single();
            var throughput = page.GetVisualDescendants().OfType<CartesianChart>()
                .Single(chart => chart.Name == "ThroughputChart");

            Assert.Single(panels);
            Assert.Equal(LegendPlacement.Top, legend.Placement);

            // The legend stands above the plot when the page is narrow, and the two cards are one column.
            Assert.True(legend.Bounds.Bottom <= throughput.Bounds.Top + 0.5);
            Assert.All(panels, panel =>
            {
                Assert.Equal(panel.Children[0].Bounds.X, panel.Children[1].Bounds.X, 1);
                Assert.True(panel.Children[1].Bounds.Y > panel.Children[0].Bounds.Y);
            });

            // A hand-measure does not cascade into a subtree that is still valid, so each responsive panel is
            // measured and arranged at the wide width itself. A thousand-pixel page leaves about 950 for the
            // content, which is where the two-column layout starts.
            const double content = 952;

            legendPanel.Measure(new Size(content, 400));
            legendPanel.Arrange(new Rect(0, 0, content, legendPanel.DesiredSize.Height));

            Assert.Equal(LegendPlacement.Right, legend.Placement);
            Assert.True(legend.Bounds.Left > throughput.Bounds.Right);

            foreach (var panel in panels)
            {
                panel.Measure(new Size(content, Catalog.PageHeight));
                panel.Arrange(new Rect(0, 0, content, panel.DesiredSize.Height));
            }

            Assert.All(panels, panel =>
            {
                Assert.True(panel.Children[1].Bounds.X > panel.Children[0].Bounds.X);
                Assert.Equal(panel.Children[0].Bounds.Y, panel.Children[1].Bounds.Y, 1);
            });
        }
        finally
        {
            window.Close();
        }
    }

    [Fact]
    public void The_dashboard_speaks_Chinese_when_the_gallery_does()
    {
        var was = Text.Language;

        try
        {
            Text.Speak(DemoLanguage.Chinese);

            var page = DashboardPage.Create();
            Text.Apply(page);

            var view = (DashboardViewModel)page.DataContext!;
            var labels = page.GetLogicalDescendants().OfType<TextBlock>()
                .Select(text => text.Text)
                .ToArray();

            Assert.Equal("仪表盘", page.Title);
            Assert.Equal("暂停", view.PauseText);
            Assert.Equal("温度", view.LineAPoints[0].DisplayName);
            Assert.Contains(view.Alarms, alarm => alarm.MessageText == "温度高于 80 °C");
            Assert.Contains("吞吐量", labels);
            Assert.Contains("活动告警", labels);
            Assert.Contains("实时点位", labels);
            Assert.Contains("确认", labels);
        }
        finally
        {
            Text.Speak(was);
        }
    }

    [Fact]
    public void Only_the_alarm_actions_use_xs_density()
    {
        var page = Show(out var window);

        try
        {
            var buttons = page.GetVisualDescendants().OfType<Button>().ToArray();
            var acknowledge = buttons.First(button => Equals(button.Content, "Ack") && button.IsVisible);
            var refresh = buttons.First(button => Equals(button.Content, "Refresh"));

            Assert.Equal(24, acknowledge.Height, 1);
            Assert.Equal(24, acknowledge.MinWidth, 1);
            Assert.Equal(new Thickness(8, 0), acknowledge.Padding);
            Assert.Equal(12, acknowledge.FontSize, 1);

            // The rest of the dashboard keeps the house's default medium density.
            Assert.Equal(36, refresh.Height, 1);
        }
        finally
        {
            window.Close();
        }
    }

    private static readonly DateTimeOffset AlarmAt =
        new(2026, 3, 14, 9, 26, 53, TimeSpan.FromHours(8));

    /// <summary>Shows the dashboard the way the gallery shows a page, and stops its clock on the first frame.</summary>
    /// <param name="window">The window the page joined, so that the test can let it go.</param>
    /// <returns>The page.</returns>
    private static DemoPage Show(out Window window)
    {
        var page = DashboardPage.Create();

        window = new Window
        {
            Resources = ChartTheme.Create(),
            Styles = { new StyleInclude((Uri?)null) { Source = new Uri("avares://AvChartsDemo/Styles/Demo.axaml") } },
            Content = page,
        };

        // A scroll viewer builds its content through the visual root; the alarm log is one, so the page is shown.
        window.Show();

        ((DashboardViewModel)page.DataContext!).StopFeed();

        page.Measure(new Size(Catalog.PageWidth, Catalog.PageHeight));
        page.Arrange(new Rect(0, 0, Catalog.PageWidth, Catalog.PageHeight));

        return page;
    }

    /// <summary>A feed a test advances by hand.</summary>
    private sealed class ManualFeed : IDashboardFeed
    {
        private readonly DashboardSnapshot _snapshot = Snapshot();

        public event EventHandler<DashboardTick>? Ticked;

        public event EventHandler<DashboardChartTick>? ChartTicked;

        public event EventHandler<AlarmEvent>? AlarmRaised;

        public int Starts { get; private set; }

        public int Stops { get; private set; }

        public DashboardRange? ResetRange { get; private set; }

        public DashboardSnapshot Reset(DashboardRange range)
        {
            ResetRange = range;
            return _snapshot;
        }

        public void Start() => Starts++;

        public void Stop() => Stops++;

        public void Tick(DashboardTick tick) => Ticked?.Invoke(this, tick);

        public void Chart(DashboardChartTick tick) => ChartTicked?.Invoke(this, tick);

        public void Raise(AlarmEvent alarm) => AlarmRaised?.Invoke(this, alarm);

        private static DashboardSnapshot Snapshot()
        {
            var when = AlarmAt.AddDays(-35);
            var points = new (string Name, double Value)[]
            {
                ("Temperature", 73.5),
                ("Pressure", 1.81),
                ("Flow", 42.2),
                ("Vibration", 0.41),
                ("Temperature", 68.8),
                ("Pressure", 2.07),
                ("Motor current", 12.5),
                ("Conveyor speed", 1_245),
            };

            var alarms = new[]
            {
                new AlarmEvent(when.AddMinutes(3), "Line A", AlarmSeverity.Critical, "Temperature above 80 °C"),
                new AlarmEvent(when.AddMinutes(7), "Line B", AlarmSeverity.Warning, "Pressure below 1.4 bar"),
                new AlarmEvent(when.AddMinutes(12), "Line A", AlarmSeverity.Warning, "Conveyor speed out of range"),
                new AlarmEvent(when.AddMinutes(18), "Line B", AlarmSeverity.Warning, "Flow below 35 L/min"),
                new AlarmEvent(when.AddMinutes(25), "Line A", AlarmSeverity.Info, "Operator acknowledged a warning"),
                new AlarmEvent(when.AddMinutes(34), "Line B", AlarmSeverity.Info, "Shift started"),
            };

            return new DashboardSnapshot(
                [new ThroughputPoint(when, 120, 8), new ThroughputPoint(when.AddDays(1), 130, 9)],
                [new OeePoint(when, 82), new OeePoint(when.AddDays(1), 83)],
                [.. points.Select(point => new PointSample(point.Name, point.Value, PointStatus.Normal))],
                alarms,
                1_248,
                97.6,
                82.4);
        }
    }
}
