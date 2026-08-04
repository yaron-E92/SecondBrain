using Microsoft.EntityFrameworkCore;
using SecondBrain.Application.Ports;
using SecondBrain.Domain.Entities;
using SecondBrain.Domain.ValueObjects;

namespace SecondBrain.Persistence;

public sealed class CoreSearchQueryService(SecondBrainDbContext context)
    : ICoreSearchQueryService
{
    public async Task<CoreSearchPage> SearchAsync(
        CoreSearchQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (query.Offset < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(query), "Offset cannot be negative.");
        }

        if (query.PageSize is < 1 or > 100)
        {
            throw new ArgumentOutOfRangeException(
                nameof(query),
                "Page size must be between 1 and 100.");
        }

        if (query.PlacementId == Guid.Empty)
        {
            throw new ArgumentException("Placement ID cannot be empty.", nameof(query));
        }

        var rows = context.BrainItems.AsNoTracking().AsQueryable();
        if (query.Kind is { } kind)
        {
            rows = rows.Where(row => row.Kind == kind);
        }

        if (query.IsArchived is { } isArchived)
        {
            rows = rows.Where(row => row.IsArchived == isArchived);
        }

        if (query.FavoritesOnly)
        {
            rows = rows.Where(row => row.IsFavorite);
        }

        if (query.PlacementKind is { } placementKind)
        {
            rows = rows.Where(row => row.PlacementKind == placementKind);
        }

        if (query.PlacementId is { } placementId)
        {
            rows = rows.Where(row =>
                row.ProjectId == placementId ||
                row.AreaId == placementId ||
                row.ResourceTopicId == placementId);
        }

        var candidates = await rows.ToListAsync(cancellationToken);
        if (candidates.Count == 0)
        {
            return new CoreSearchPage([], 0);
        }

        var candidateIds = candidates.Select(row => row.Id).ToArray();
        var tagRows = await context.BrainItemTags.AsNoTracking()
            .Where(row => candidateIds.Contains(row.BrainItemId))
            .ToListAsync(cancellationToken);
        var textTagRows = await context.BrainItemTextTags.AsNoTracking()
            .Where(row => candidateIds.Contains(row.BrainItemId))
            .ToListAsync(cancellationToken);
        var namedTags = await context.Tags.AsNoTracking().ToListAsync(cancellationToken);
        var tagNames = namedTags.ToDictionary(row => row.Id, row => row.Name);
        var namedTagLookup = tagRows
            .Where(row => tagNames.ContainsKey(row.TagId))
            .ToLookup(row => row.BrainItemId, row => tagNames[row.TagId]);
        var textTagLookup = textTagRows.ToLookup(row => row.BrainItemId, row => row.Value);

        var requestedTag = Normalize(query.Tag);
        if (requestedTag.Length > 0)
        {
            candidates = candidates
                .Where(row => namedTagLookup[row.Id]
                    .Concat(textTagLookup[row.Id])
                    .Any(tag => string.Equals(
                        tag,
                        requestedTag,
                        StringComparison.OrdinalIgnoreCase)))
                .ToList();
        }

        var projects = await context.Projects.AsNoTracking()
            .ToDictionaryAsync(row => row.Id, row => row.Name, cancellationToken);
        var areas = await context.Areas.AsNoTracking()
            .ToDictionaryAsync(row => row.Id, row => row.Name, cancellationToken);
        var topics = await context.ResourceTopics.AsNoTracking()
            .ToDictionaryAsync(row => row.Id, row => row.Name, cancellationToken);
        var term = Normalize(query.Text);

        var ranked = candidates
            .Select(row => new RankedRow(
                row,
                PlacementName(row, projects, areas, topics),
                namedTagLookup[row.Id].Concat(textTagLookup[row.Id]).Distinct(
                    StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase)
                    .ToArray(),
                Rank(row, term, PlacementName(row, projects, areas, topics),
                    namedTagLookup[row.Id].Concat(textTagLookup[row.Id]))))
            .Where(result => term.Length == 0 || result.Rank < int.MaxValue)
            .OrderBy(result => result.Rank)
            .ThenByDescending(result => result.Row.UpdatedAt)
            .ThenBy(result => result.Row.Title, StringComparer.OrdinalIgnoreCase)
            .ThenBy(result => result.Row.Id)
            .ToArray();

        var pageRows = ranked.Skip(query.Offset).Take(query.PageSize).ToArray();
        var pageIds = pageRows.Select(result => result.Row.Id).ToArray();
        var relationRows = await context.BrainItemRelations.AsNoTracking()
            .Where(row => pageIds.Contains(row.TargetId))
            .ToListAsync(cancellationToken);
        var sourceIds = relationRows.Select(row => row.SourceId).Distinct().ToArray();
        var sources = await context.BrainItems.AsNoTracking()
            .Where(row => sourceIds.Contains(row.Id))
            .ToDictionaryAsync(row => row.Id, cancellationToken);
        var backlinks = relationRows
            .Where(row => sources.ContainsKey(row.SourceId))
            .ToLookup(
                row => row.TargetId,
                row => new CoreSearchBacklink(
                    new SecondBrainItemId(row.SourceId),
                    sources[row.SourceId].Title,
                    ToBacklinkKind(row.Kind)));

        return new CoreSearchPage(
            pageRows.Select(result => ToItem(result, backlinks[result.Row.Id])).ToArray(),
            ranked.Length);
    }

    public async Task<CoreSearchFilterOptions> GetFilterOptionsAsync(
        CancellationToken cancellationToken = default)
    {
        var namedTags = await context.Tags.AsNoTracking()
            .Select(row => row.Name)
            .ToListAsync(cancellationToken);
        var textTags = await context.BrainItemTextTags.AsNoTracking()
            .Select(row => row.Value)
            .ToListAsync(cancellationToken);
        var projects = await context.Projects.AsNoTracking()
            .Select(row => new CoreSearchPlacement(
                PrimaryPlacementKind.Project,
                row.Id,
                row.Name))
            .ToListAsync(cancellationToken);
        var areas = await context.Areas.AsNoTracking()
            .Select(row => new CoreSearchPlacement(
                PrimaryPlacementKind.Area,
                row.Id,
                row.Name))
            .ToListAsync(cancellationToken);
        var topics = await context.ResourceTopics.AsNoTracking()
            .Select(row => new CoreSearchPlacement(
                PrimaryPlacementKind.ResourceTopic,
                row.Id,
                row.Name))
            .ToListAsync(cancellationToken);

        return new CoreSearchFilterOptions(
            namedTags.Concat(textTags).Distinct(StringComparer.OrdinalIgnoreCase)
                .Order(StringComparer.OrdinalIgnoreCase).ToArray(),
            projects.Concat(areas).Concat(topics)
                .OrderBy(placement => placement.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(placement => placement.Kind)
                .ThenBy(placement => placement.Id)
                .ToArray());
    }

    public async Task<IReadOnlyList<CoreSearchItem>> GetFavoritesAsync(
        int limit = 5,
        CancellationToken cancellationToken = default) =>
        (await SearchAsync(
            new CoreSearchQuery(FavoritesOnly: true, PageSize: ValidateLimit(limit)),
            cancellationToken)).Items;

    public async Task<IReadOnlyList<CoreSearchItem>> GetRecentAsync(
        int limit = 5,
        CancellationToken cancellationToken = default) =>
        (await SearchAsync(
            new CoreSearchQuery(PageSize: ValidateLimit(limit)),
            cancellationToken)).Items;

    private static int ValidateLimit(int limit) => limit is >= 1 and <= 100
        ? limit
        : throw new ArgumentOutOfRangeException(nameof(limit));

    private static string Normalize(string? value) => value?.Trim() ?? string.Empty;

    private static int Rank(
        BrainItemRow row,
        string term,
        string placementName,
        IEnumerable<string> tags)
    {
        if (term.Length == 0)
        {
            return 0;
        }

        if (string.Equals(row.Title, term, StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        if (row.Title.StartsWith(term, StringComparison.OrdinalIgnoreCase))
        {
            return 1;
        }

        if (row.Title.Contains(term, StringComparison.OrdinalIgnoreCase))
        {
            return 2;
        }

        if (row.Content.Contains(term, StringComparison.OrdinalIgnoreCase))
        {
            return 3;
        }

        return Metadata(row, placementName, tags).Any(value =>
            value.Contains(term, StringComparison.OrdinalIgnoreCase))
            ? 4
            : int.MaxValue;
    }

    private static IEnumerable<string> Metadata(
        BrainItemRow row,
        string placementName,
        IEnumerable<string> tags)
    {
        yield return row.Kind.ToString();
        yield return row.PlacementKind.ToString();
        yield return placementName;
        yield return row.IsArchived ? "archived" : "active";
        yield return row.IsFavorite ? "favorite" : string.Empty;
        yield return row.NoteKind?.ToString() ?? string.Empty;
        yield return row.IdeaMaturity?.ToString() ?? string.Empty;
        yield return row.EntryDate?.ToString("O") ?? string.Empty;
        yield return row.CaptureSourceType?.ToString() ?? string.Empty;
        yield return row.SourceUri ?? string.Empty;
        yield return row.SourceCitation ?? string.Empty;
        yield return row.ReminderAt?.ToString("O") ?? string.Empty;
        yield return row.CaptureProcessingState?.ToString() ?? string.Empty;
        yield return row.ResourceArtifactKind?.ToString() ?? string.Empty;
        yield return row.ResourceFreshness?.ToString() ?? string.Empty;
        yield return row.ReviewDate?.ToString("O") ?? string.Empty;
        foreach (var tag in tags)
        {
            yield return tag;
        }
    }

    private static string PlacementName(
        BrainItemRow row,
        IReadOnlyDictionary<Guid, string> projects,
        IReadOnlyDictionary<Guid, string> areas,
        IReadOnlyDictionary<Guid, string> topics) => row.PlacementKind switch
        {
            PrimaryPlacementKind.Project when row.ProjectId is { } id =>
                projects.GetValueOrDefault(id, "Unavailable project"),
            PrimaryPlacementKind.Area when row.AreaId is { } id =>
                areas.GetValueOrDefault(id, "Unavailable area"),
            PrimaryPlacementKind.ResourceTopic when row.ResourceTopicId is { } id =>
                topics.GetValueOrDefault(id, "Unavailable resource topic"),
            _ => "Unavailable placement",
        };

    private static CoreSearchItem ToItem(
        RankedRow result,
        IEnumerable<CoreSearchBacklink> backlinks)
    {
        var row = result.Row;
        return new CoreSearchItem(
            new SecondBrainItemId(row.Id),
            row.Kind,
            row.Title,
            row.Content.Length <= 180 ? row.Content : $"{row.Content[..177]}...",
            row.PlacementKind,
            row.ProjectId ?? row.AreaId ?? row.ResourceTopicId ?? Guid.Empty,
            result.PlacementName,
            row.IsArchived,
            row.IsFavorite,
            row.UpdatedAt,
            result.Tags,
            backlinks.OrderBy(link => link.Kind)
                .ThenBy(link => link.SourceTitle, StringComparer.OrdinalIgnoreCase)
                .ThenBy(link => link.SourceId.Value)
                .ToArray());
    }

    private static CoreBacklinkKind ToBacklinkKind(BrainItemRelationKind kind) =>
        kind switch
        {
            BrainItemRelationKind.Contextual => CoreBacklinkKind.Contextual,
            BrainItemRelationKind.Derived => CoreBacklinkKind.Derived,
            BrainItemRelationKind.Provenance => CoreBacklinkKind.Provenance,
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };

    private sealed record RankedRow(
        BrainItemRow Row,
        string PlacementName,
        IReadOnlyList<string> Tags,
        int Rank);
}
