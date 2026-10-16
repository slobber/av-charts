using System.Globalization;


using AvCharts.Data;



namespace AvCharts.Axes;

/// <summary>
/// An axis of names: its unit space is the number of the slot a category sits in.
/// </summary>
/// <remarks>
/// <para>
/// Slots are handed out in the order the categories are first seen, which is the order the data arrives in -
/// the same bargain every category axis makes, and the reason a category axis is not a plain linear one with
/// different labels: the numbers are positions, not measurements.
/// </para>
/// <para>
/// This axis is the one exception to "an axis holds settings, never results": the list of categories is learned
/// from the data as it is described. That is inherent - there is nowhere else for it to come from - and
/// <see cref="ClearCategories"/> is how a caller starts over when the data is replaced wholesale.
/// </para>
/// <para>
/// Grid lines fall on whole slots, since a fraction of a category means nothing. The half slot of room that a
/// band layout wants at either end - so that the first and last column are not cut in half - comes with that
/// layout, not here.
/// </para>
/// </remarks>
public class CategoryAxis : AxisBase
{
    private readonly List<string> _categories = [];
    private readonly Dictionary<string, int> _slots = new(StringComparer.Ordinal);

    /// <summary>The categories, in slot order.</summary>
    public IReadOnlyList<string> Categories => _categories;

    /// <inheritdoc />
    public override ChartValueKind ValueKind => ChartValueKind.Category;

    /// <summary>Gets the slot a category sits in, giving it the next free one the first time it is seen.</summary>
    /// <param name="category">The category.</param>
    /// <returns>The slot number.</returns>
    public int GetSlot(string category)
    {
        ArgumentNullException.ThrowIfNull(category);

        if (_slots.TryGetValue(category, out var slot))
        {
            return slot;
        }

        slot = _categories.Count;
        _categories.Add(category);
        _slots[category] = slot;

        // Telling the transform is what makes the axis cover the new slot. Registering happens while the
        // transform is describing its elements, so the pass that learned about the category is not the last
        // one: the next one sees a settled list and stops.
        Invalidate();

        return slot;
    }

    /// <summary>Forgets every slot, so the next values start again from zero.</summary>
    public void ClearCategories()
    {
        if (_categories.Count == 0)
        {
            return;
        }

        _categories.Clear();
        _slots.Clear();
        Invalidate();
    }

    /// <inheritdoc />
    public override bool TryToUnit(ChartValue value, out double unit)
    {
        unit = 0;

        if (value.Kind != ChartValueKind.Category || value.Category is null)
        {
            return false;
        }

        unit = GetSlot(value.Category);
        return true;
    }

    /// <inheritdoc />
    public override ChartValue FromUnit(double unit)
    {
        var slot = (int)Math.Round(unit);

        return slot >= 0 && slot < _categories.Count ? _categories[slot] : ChartValue.Empty;
    }

    /// <inheritdoc />
    public override string FormatValue(double unit)
    {
        var slot = (int)Math.Round(unit);

        // A label only names a category when it stands on one; between two slots there is nothing to say.
        return Math.Abs(unit - slot) < 1e-9 && slot >= 0 && slot < _categories.Count
            ? _categories[slot]
            : slot.ToString(CultureInfo.InvariantCulture);
    }

    /// <inheritdoc />
    protected override double NiceInterval(double target)
    {
        // A fraction of a slot is not a place, so the grid steps in whole categories.
        var candidate = base.NiceInterval(target);

        return Math.Max(1, Math.Round(candidate));
    }
}
