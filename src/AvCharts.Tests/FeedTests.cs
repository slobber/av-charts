using AvCharts.Demo;
using Xunit;

namespace AvCharts.Tests;

/// <summary>
/// Checks the demo's live feed: the readings it hands over when it is told how much time has gone by.
/// </summary>
/// <remarks>
/// The feed is driven rather than self-starting, which is the whole reason these can exist - and they exist
/// because of what went wrong without them: the feed kept two clocks, the readings were stamped on one and the
/// owed readings worked out on the other, so nothing arrived at all until the switch was flipped twice and the
/// first reading that did arrive was drawn as a straight line across the chart to reach it.
/// </remarks>
public class FeedTests
{
    [Fact]
    public void A_feed_hands_over_the_readings_that_are_due()
    {
        var feed = new Feed(seconds => seconds * 2) { Rate = 1_000, Window = 5 };

        // Copied, because the feed hands back the same list every time rather than a new one per beat.
        var first = feed.Advance(TimeSpan.FromSeconds(5)).ToArray();

        Assert.Equal(5_000, first.Length);
        Assert.Equal(1, first[0].At, 3);
        Assert.Equal(5_000, first[^1].At, 3);
        // The signal is given the time in seconds, so the reading a millisecond in is two thousandths.
        Assert.Equal(0.002, first[0].Value, 6);

        // And the next beat carries on from the last reading rather than from the clock: the reading after it is
        // one step along, not a jump to wherever the wall clock happens to be. That jump was the bug.
        var next = feed.Advance(TimeSpan.FromMilliseconds(4)).ToArray();

        Assert.Equal(4, next.Length);
        Assert.Equal(first[^1].At + 1, next[0].At, 3);
        Assert.Equal(5_004, next[^1].At, 3);
    }

    [Fact]
    public void A_feed_that_was_away_for_a_while_leaves_a_gap_rather_than_a_line_across_it()
    {
        var feed = new Feed(seconds => seconds) { Rate = 1_000, Window = 5 };

        feed.Advance(TimeSpan.FromSeconds(5));

        // A minute goes by while nobody is looking. A window of readings is handed over - the rest are ones the
        // window would have thrown away - and the gap is marked, because a gap drawn as a straight line reads as
        // a reading that was never taken.
        var after = feed.Advance(TimeSpan.FromSeconds(60)).ToArray();

        Assert.Contains(after, reading => double.IsNaN(reading.Value));
        Assert.True(after.Length <= 5_001, $"expected a window of readings at most, got {after.Length}");
    }

    [Fact]
    public void A_stopped_feed_owes_nothing_when_it_starts_again()
    {
        var feed = new Feed(seconds => seconds) { Rate = 1_000, Window = 5 };

        var before = feed.Advance(TimeSpan.FromSeconds(5)).ToArray();

        feed.Running = false;

        Assert.Empty(feed.Advance(TimeSpan.FromSeconds(30)));

        feed.Running = true;

        var resumed = feed.Advance(TimeSpan.FromMilliseconds(5)).ToArray();

        // Five milliseconds gone by, five readings: the half-minute the feed was stopped is time nobody was
        // recording, so it is not owed back in a burst.
        Assert.Equal(5, resumed.Length);
        Assert.Equal(before[^1].At + 1, resumed[0].At, 3);
    }

    [Fact]
    public void Changing_the_rate_does_not_move_the_clock()
    {
        // The rate is a setting a reader changes while the recording runs. It is not the clock: a feed that worked
        // its clock out from the number of readings divided by the rate moved every reading in the window the
        // moment the setting changed - which is a chart torn in half, with a line straight across the tear.
        var feed = new Feed(seconds => seconds) { Rate = 1_000, Window = 5 };

        feed.Advance(TimeSpan.FromSeconds(1));

        Assert.Equal(1_000, feed.At, 3);

        feed.Rate = 50;

        var slower = feed.Advance(TimeSpan.FromSeconds(1));

        // A second of wall time is a second of recording, whatever the rate: fifty readings of it, and the clock
        // is where it would have been at a thousand.
        Assert.Equal(50, slower.Count);
        Assert.Equal(2_000, feed.At, 3);
        Assert.Equal(2_000, slower[^1].At, 3);

        // And every reading is where the recording says it is: the second one is twenty milliseconds along, and
        // the fifty readings of that second cover nine hundred and eighty of them.
        Assert.Equal(20, slower[1].At - slower[0].At, 3);
        Assert.Equal(980, slower[^1].At - slower[0].At, 3);
    }

    [Fact]
    public void The_window_is_what_is_worth_keeping()
    {
        var feed = new Feed(seconds => seconds) { Rate = 100, Window = 2 };

        feed.Advance(TimeSpan.FromSeconds(2));

        Assert.Equal(0, feed.Oldest);

        feed.Advance(TimeSpan.FromSeconds(1));

        // A second on, the oldest reading worth keeping is a window behind the newest one.
        Assert.Equal(feed.At - 2_000, feed.Oldest, 3);
    }
}
