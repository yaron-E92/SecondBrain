using SecondBrain.Application.Ports;
using SecondBrain.Domain.Entities;
using SecondBrain.Domain.ValueObjects;

namespace SecondBrain.Application.UseCases;

public enum ReviewQueueKind
{
    Inbox,
    Para,
}

public enum ReviewScopeKind
{
    Project,
    Area,
    ResourceTopic,
}

public sealed record GetReviewQueueQuery(
    ReviewQueueKind Kind,
    DateTimeOffset AsOf,
    ReviewScopeKind? ScopeKind = null,
    Guid? ScopeId = null);

public sealed record ReviewDecisionCommand(
    ReviewTargetKind TargetKind,
    Guid TargetId,
    DateTimeOffset DecidedAt);

public sealed record DeferReviewCommand(
    ReviewTargetKind TargetKind,
    Guid TargetId,
    DateTimeOffset DecidedAt,
    DateTimeOffset DeferredUntil);

public sealed record ReviewQueueItem(
    ReviewTargetKind TargetKind,
    Guid TargetId,
    string Title,
    string Details,
    SecondBrainItemId? BrainItemId = null);

public sealed class ReviewUseCase(ICoreKnowledgeRepository repository)
{
    private static readonly TimeSpan ProjectCadence = TimeSpan.FromDays(7);
    private static readonly TimeSpan AreaCadence = TimeSpan.FromDays(30);
    private static readonly TimeSpan ResourceCadence = TimeSpan.FromDays(30);

    public async Task<IReadOnlyList<ReviewQueueItem>> GetQueueAsync(
        GetReviewQueueQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ValidateScope(query);

        var state = await repository.LoadStateAsync(cancellationToken);
        var reviewStates = (state.ReviewStates ?? []).ToDictionary(
            review => (review.TargetKind, review.TargetId));

        return query.Kind == ReviewQueueKind.Inbox
            ? GetInboxQueue(state, reviewStates, query.AsOf)
            : GetParaQueue(state, reviewStates, query);
    }

    public Task MarkReviewedAsync(
        ReviewDecisionCommand command,
        CancellationToken cancellationToken = default) =>
        ChangeReviewStateAsync(
            command.TargetKind,
            command.TargetId,
            command.DecidedAt,
            null,
            archive: false,
            cancellationToken);

    public Task DeferAsync(
        DeferReviewCommand command,
        CancellationToken cancellationToken = default)
    {
        if (command.DeferredUntil <= command.DecidedAt)
        {
            throw new ArgumentOutOfRangeException(
                nameof(command),
                "A deferred review must resume in the future.");
        }

        return ChangeReviewStateAsync(
            command.TargetKind,
            command.TargetId,
            null,
            command.DeferredUntil,
            archive: false,
            cancellationToken);
    }

    public Task ArchiveAsync(
        ReviewDecisionCommand command,
        CancellationToken cancellationToken = default) =>
        ChangeReviewStateAsync(
            command.TargetKind,
            command.TargetId,
            null,
            null,
            archive: true,
            cancellationToken);

    private async Task ChangeReviewStateAsync(
        ReviewTargetKind targetKind,
        Guid targetId,
        DateTimeOffset? reviewedAt,
        DateTimeOffset? deferredUntil,
        bool archive,
        CancellationToken cancellationToken)
    {
        if (targetId == Guid.Empty || !Enum.IsDefined(targetKind))
        {
            throw new ArgumentException("A valid review target is required.");
        }

        var state = await repository.LoadStateAsync(cancellationToken);
        var target = FindTarget(state, targetKind, targetId);
        if (target is null || IsArchived(target))
        {
            throw new InvalidOperationException(
                "This review item is no longer available. Refresh the review and try again.");
        }

        if (archive)
        {
            Archive(target);
        }

        var reviews = (state.ReviewStates ?? [])
            .Where(review =>
                review.TargetKind != targetKind || review.TargetId != targetId)
            .ToList();
        if (!archive)
        {
            var existing = (state.ReviewStates ?? []).FirstOrDefault(review =>
                review.TargetKind == targetKind && review.TargetId == targetId);
            reviews.Add(new ReviewState(
                targetKind,
                targetId,
                reviewedAt ?? existing?.LastReviewedAt,
                deferredUntil));
        }

        await repository.SaveStateAsync(
            state with { ReviewStates = reviews },
            cancellationToken);
    }

    private static ReviewQueueItem[] GetInboxQueue(
        CoreKnowledgeState state,
        IReadOnlyDictionary<(ReviewTargetKind, Guid), ReviewState> reviews,
        DateTimeOffset asOf)
    {
        var inboxIds = state.Areas
            .Where(area =>
                !area.IsArchived &&
                string.Equals(area.Name.Value, "Inbox", StringComparison.OrdinalIgnoreCase))
            .Select(area => area.Id.Value)
            .ToHashSet();

        return state.BrainItems
            .Where(item =>
                !item.IsArchived &&
                item.Kind == BrainItemKind.Idea &&
                item.IdeaMaturity == IdeaMaturity.Captured &&
                item.PrimaryPlacement.Kind == PrimaryPlacementKind.Area &&
                inboxIds.Contains(item.PrimaryPlacement.ContextId) &&
                IsAvailable(reviews, ReviewTargetKind.InboxItem, item.Id.Value, asOf))
            .OrderBy(item => item.CreatedAt)
            .ThenBy(item => item.Id.Value)
            .Select(item => new ReviewQueueItem(
                ReviewTargetKind.InboxItem,
                item.Id.Value,
                item.Title,
                item.Content,
                item.Id))
            .ToArray();
    }

    private static ReviewQueueItem[] GetParaQueue(
        CoreKnowledgeState state,
        IReadOnlyDictionary<(ReviewTargetKind, Guid), ReviewState> reviews,
        GetReviewQueueQuery query)
    {
        var items = new List<(DateTimeOffset DueAt, ReviewQueueItem Item)>();

        if (query.ScopeKind is null or ReviewScopeKind.Project)
        {
            items.AddRange(state.Projects
                .Where(project =>
                    !project.IsArchived &&
                    project.Status is ProjectStatus.Planned or ProjectStatus.Active &&
                    MatchesScope(query, ReviewScopeKind.Project, project.Id.Value))
                .Select(project => CreateDueItem(
                    reviews,
                    ReviewTargetKind.Project,
                    project.Id.Value,
                    query.AsOf,
                    ProjectCadence,
                    project.Name.Value,
                    project.Outcome))
                .Where(item => item.HasValue)
                .Select(item => item!.Value));
        }

        if (query.ScopeKind is null or ReviewScopeKind.Area)
        {
            items.AddRange(state.Areas
                .Where(area =>
                    !area.IsArchived &&
                    !string.Equals(area.Name.Value, "Inbox", StringComparison.OrdinalIgnoreCase) &&
                    MatchesScope(query, ReviewScopeKind.Area, area.Id.Value))
                .Select(area => CreateDueItem(
                    reviews,
                    ReviewTargetKind.Area,
                    area.Id.Value,
                    query.AsOf,
                    AreaCadence,
                    area.Name.Value,
                    "Confirm this Area is still useful and maintained."))
                .Where(item => item.HasValue)
                .Select(item => item!.Value));
        }

        if (query.ScopeKind is null or ReviewScopeKind.ResourceTopic)
        {
            items.AddRange(state.BrainItems
                .Where(item =>
                    !item.IsArchived &&
                    item.Kind == BrainItemKind.ResourceArtifact &&
                    (query.ScopeKind is null ||
                     item.PrimaryPlacement.Kind == PrimaryPlacementKind.ResourceTopic &&
                     item.PrimaryPlacement.ContextId == query.ScopeId))
                .Select(item => CreateResourceItem(reviews, item, query.AsOf))
                .Where(item => item.HasValue)
                .Select(item => item!.Value));
        }

        return items
            .OrderBy(item => item.DueAt)
            .ThenBy(item => item.Item.Title, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Item.TargetId)
            .Select(item => item.Item)
            .ToArray();
    }

    private static (DateTimeOffset DueAt, ReviewQueueItem Item)? CreateDueItem(
        IReadOnlyDictionary<(ReviewTargetKind, Guid), ReviewState> reviews,
        ReviewTargetKind kind,
        Guid id,
        DateTimeOffset asOf,
        TimeSpan cadence,
        string title,
        string details)
    {
        reviews.TryGetValue((kind, id), out var review);
        if (review?.DeferredUntil > asOf)
        {
            return null;
        }

        var dueAt = review?.LastReviewedAt?.Add(cadence) ?? DateTimeOffset.MinValue;
        return dueAt <= asOf
            ? (dueAt, new ReviewQueueItem(kind, id, title, details))
            : null;
    }

    private static (DateTimeOffset DueAt, ReviewQueueItem Item)? CreateResourceItem(
        IReadOnlyDictionary<(ReviewTargetKind, Guid), ReviewState> reviews,
        BrainItem item,
        DateTimeOffset asOf)
    {
        reviews.TryGetValue((ReviewTargetKind.Resource, item.Id.Value), out var review);
        if (review?.DeferredUntil > asOf)
        {
            return null;
        }

        var scheduledDate = item.ReviewDate is null
            ? DateTimeOffset.MinValue
            : new DateTimeOffset(item.ReviewDate.Value.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var dueAt = review?.LastReviewedAt is { } lastReviewed
            ? lastReviewed.Add(ResourceCadence)
            : item.ResourceFreshness == ResourceFreshness.Outdated
                ? DateTimeOffset.MinValue
                : scheduledDate;

        return dueAt <= asOf
            ? (dueAt, new ReviewQueueItem(
                ReviewTargetKind.Resource,
                item.Id.Value,
                item.Title,
                item.ResourceFreshness == ResourceFreshness.Outdated
                    ? "Outdated resource — update, archive, or confirm it is still useful."
                    : item.Content,
                item.Id))
            : null;
    }

    private static bool IsAvailable(
        IReadOnlyDictionary<(ReviewTargetKind, Guid), ReviewState> reviews,
        ReviewTargetKind kind,
        Guid id,
        DateTimeOffset asOf) =>
        !reviews.TryGetValue((kind, id), out var review) ||
        review.LastReviewedAt is null && review.DeferredUntil <= asOf;

    private static bool MatchesScope(
        GetReviewQueueQuery query,
        ReviewScopeKind kind,
        Guid id) =>
        query.ScopeKind is null ||
        query.ScopeKind == kind && query.ScopeId == id;

    private static void ValidateScope(GetReviewQueueQuery query)
    {
        if ((query.ScopeKind is null) != (query.ScopeId is null) ||
            query.ScopeId == Guid.Empty ||
            query.Kind == ReviewQueueKind.Inbox && query.ScopeKind is not null)
        {
            throw new ArgumentException("The selected review scope is invalid.");
        }
    }

    private static object? FindTarget(
        CoreKnowledgeState state,
        ReviewTargetKind kind,
        Guid id) => kind switch
        {
            ReviewTargetKind.InboxItem or ReviewTargetKind.Resource =>
                state.BrainItems.SingleOrDefault(item => item.Id.Value == id),
            ReviewTargetKind.Project =>
                state.Projects.SingleOrDefault(project => project.Id.Value == id),
            ReviewTargetKind.Area =>
                state.Areas.SingleOrDefault(area => area.Id.Value == id),
            _ => null,
        };

    private static bool IsArchived(object target) => target switch
    {
        BrainItem item => item.IsArchived,
        Project project => project.IsArchived,
        Area area => area.IsArchived,
        _ => true,
    };

    private static void Archive(object target)
    {
        switch (target)
        {
            case BrainItem item:
                item.Archive();
                break;
            case Project project:
                project.Archive();
                break;
            case Area area:
                area.Archive();
                break;
            default:
                throw new InvalidOperationException("This review item cannot be archived.");
        }
    }
}
