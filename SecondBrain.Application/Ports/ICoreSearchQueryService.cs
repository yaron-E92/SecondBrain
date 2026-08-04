using SecondBrain.Domain.Entities;
using SecondBrain.Domain.ValueObjects;

namespace SecondBrain.Application.Ports;

public sealed record CoreSearchQuery(
    string Text = "",
    BrainItemKind? Kind = null,
    string? Tag = null,
    PrimaryPlacementKind? PlacementKind = null,
    Guid? PlacementId = null,
    bool? IsArchived = false,
    bool FavoritesOnly = false,
    int Offset = 0,
    int PageSize = 20);

public enum CoreBacklinkKind
{
    Contextual,
    Derived,
    Provenance,
}

public sealed record CoreSearchBacklink(
    SecondBrainItemId SourceId,
    string SourceTitle,
    CoreBacklinkKind Kind)
{
    public string DisplayText => $"{Kind} · {SourceTitle}";
}

public sealed record CoreSearchItem(
    SecondBrainItemId Id,
    BrainItemKind Kind,
    string Title,
    string Preview,
    PrimaryPlacementKind PlacementKind,
    Guid PlacementId,
    string PlacementName,
    bool IsArchived,
    bool IsFavorite,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<string> Tags,
    IReadOnlyList<CoreSearchBacklink> Backlinks)
{
    public string KindAndPlacement => $"{Kind} · {PlacementName}";

    public string State => IsArchived
        ? "Archived"
        : IsFavorite ? "Favorite" : "Active";

    public string BacklinksText => Backlinks.Count == 0
        ? "No backlinks"
        : string.Join(
            " · ",
            Backlinks.Select(link => $"{link.Kind}: {link.SourceTitle}"));
}

public sealed record CoreSearchPage(
    IReadOnlyList<CoreSearchItem> Items,
    int TotalCount)
{
    public bool HasMore(int offset) => offset + Items.Count < TotalCount;
}

public sealed record CoreSearchPlacement(
    PrimaryPlacementKind Kind,
    Guid Id,
    string Name);

public sealed record CoreSearchFilterOptions(
    IReadOnlyList<string> Tags,
    IReadOnlyList<CoreSearchPlacement> Placements);

public interface ICoreSearchQueryService
{
    Task<CoreSearchPage> SearchAsync(
        CoreSearchQuery query,
        CancellationToken cancellationToken = default);

    Task<CoreSearchFilterOptions> GetFilterOptionsAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CoreSearchItem>> GetFavoritesAsync(
        int limit = 5,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CoreSearchItem>> GetRecentAsync(
        int limit = 5,
        CancellationToken cancellationToken = default);
}
