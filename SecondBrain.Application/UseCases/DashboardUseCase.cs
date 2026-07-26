using SecondBrain.Application.Ports;
using SecondBrain.Domain.Entities;
using SecondBrain.Domain.ValueObjects;

namespace SecondBrain.Application.UseCases;

public sealed record GetDashboardQuery(
    int RecentLimit = 5,
    IReadOnlyList<string>? EnabledModuleNames = null);

public sealed record GetInboxQuery;

public sealed record QuickCaptureCommand(string Text);

public sealed record DashboardItem(
    SecondBrainItemId Id,
    string Title,
    string Content,
    DateTimeOffset UpdatedAt);

public sealed record DashboardProject(
    ProjectId Id,
    string Name,
    string Outcome);

public sealed record DashboardModuleSlot(
    string Name,
    string EmptyMessage);

public sealed record DashboardSnapshot(
    IReadOnlyList<DashboardItem> Inbox,
    IReadOnlyList<DashboardProject> ActiveProjects,
    IReadOnlyList<DashboardItem> Favorites,
    IReadOnlyList<DashboardItem> RecentItems,
    IReadOnlyList<DashboardModuleSlot> ModuleSlots);

public sealed class DashboardUseCase(ICoreKnowledgeRepository repository)
{
    private static readonly AreaId ReservedInboxAreaId =
        new(new Guid("47f69d0c-a337-4e4b-8291-11c7be0cf143"));

    public async Task<DashboardSnapshot> GetDashboardAsync(
        GetDashboardQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (query.RecentLimit <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(query),
                "Recent item limit must be greater than zero.");
        }

        var state = await repository.LoadStateAsync(cancellationToken);
        var activeItems = state.BrainItems
            .Where(item => !item.IsArchived)
            .ToArray();

        return new DashboardSnapshot(
            FindInboxItems(state, activeItems),
            state.Projects
                .Where(project =>
                    !project.IsArchived &&
                    project.Status == ProjectStatus.Active)
                .OrderBy(project => project.Name.Value)
                .Select(project => new DashboardProject(
                    project.Id,
                    project.Name.Value,
                    project.Outcome))
                .ToArray(),
            activeItems
                .Where(item => item.IsFavorite)
                .OrderByDescending(item => item.UpdatedAt)
                .Select(ToDashboardItem)
                .ToArray(),
            activeItems
                .OrderByDescending(item => item.UpdatedAt)
                .Take(query.RecentLimit)
                .Select(ToDashboardItem)
                .ToArray(),
            (query.EnabledModuleNames ?? [])
                .Select(name => new DashboardModuleSlot(
                    name,
                    $"No {name} data is available yet."))
                .ToArray());
    }

    public async Task<IReadOnlyList<DashboardItem>> GetInboxAsync(
        GetInboxQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var state = await repository.LoadStateAsync(cancellationToken);
        return FindInboxItems(
            state,
            state.BrainItems.Where(item => !item.IsArchived));
    }

    public async Task<DashboardItem> QuickCaptureAsync(
        QuickCaptureCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentException.ThrowIfNullOrWhiteSpace(command.Text);

        var text = command.Text.Trim();
        var state = await repository.LoadStateAsync(cancellationToken);
        var inboxArea = state.Areas.FirstOrDefault(IsActiveInboxArea);

        if (inboxArea is null)
        {
            inboxArea = new Area(
                ReservedInboxAreaId,
                new ParaContextName("Inbox"));
            state = state with
            {
                Areas = state.Areas.Append(inboxArea).ToArray(),
            };
        }

        var now = DateTimeOffset.UtcNow;
        var item = new BrainItem(
            SecondBrainItemId.New(),
            BrainItemKind.Idea,
            CreateTitle(text),
            text,
            PrimaryPlacement.InArea(inboxArea.Id),
            now,
            ideaMaturity: IdeaMaturity.Captured);

        state = state with
        {
            BrainItems = state.BrainItems.Append(item).ToArray(),
        };
        await repository.SaveStateAsync(state, cancellationToken);

        return ToDashboardItem(item);
    }

    private static DashboardItem[] FindInboxItems(
        CoreKnowledgeState state,
        IEnumerable<BrainItem> activeItems)
    {
        var inboxAreaIds = state.Areas
            .Where(IsActiveInboxArea)
            .Select(area => area.Id.Value)
            .ToHashSet();

        return activeItems
            .Where(item =>
                item.Kind == BrainItemKind.Idea &&
                item.IdeaMaturity == IdeaMaturity.Captured &&
                item.PrimaryPlacement.Kind == PrimaryPlacementKind.Area &&
                inboxAreaIds.Contains(item.PrimaryPlacement.ContextId))
            .OrderByDescending(item => item.CreatedAt)
            .Select(ToDashboardItem)
            .ToArray();
    }

    private static bool IsActiveInboxArea(Area area) =>
        !area.IsArchived &&
        string.Equals(
            area.Name.Value,
            "Inbox",
            StringComparison.OrdinalIgnoreCase);

    private static DashboardItem ToDashboardItem(BrainItem item) =>
        new(item.Id, item.Title, item.Content, item.UpdatedAt);

    private static string CreateTitle(string text)
    {
        var firstLine = text
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault()?
            .Trim() ?? text;

        return firstLine.Length <= 80
            ? firstLine
            : $"{firstLine[..77]}...";
    }
}
