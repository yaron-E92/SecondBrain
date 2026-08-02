namespace SecondBrain.Domain.Entities;

public static class BrainItemFilters
{
    public static IReadOnlyList<BrainItem> Apply(
        IEnumerable<BrainItem> items,
        bool? isFavorite = null,
        bool? isArchived = null)
    {
        ArgumentNullException.ThrowIfNull(items);

        return items
            .Where(item =>
                (isFavorite is null || item.IsFavorite == isFavorite) &&
                (isArchived is null || item.IsArchived == isArchived))
            .OrderBy(item => item.Id.Value)
            .ToArray();
    }
}
