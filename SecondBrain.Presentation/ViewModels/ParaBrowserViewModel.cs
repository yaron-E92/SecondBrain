using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SecondBrain.Application.Ports;
using SecondBrain.Application.UseCases;
using SecondBrain.Domain.Entities;
using SecondBrain.Domain.ValueObjects;

namespace SecondBrain.Presentation.ViewModels;

public enum ParaContextKind
{
    Project,
    Area,
    ResourceTopic,
    Inbox,
    Archive,
}

public sealed record ParaContextItem(
    ParaContextKind Kind,
    Guid? Id,
    string Name,
    string Details);

public sealed record ParaDestination(
    PrimaryPlacement Placement,
    string Name);

public sealed record ParaTagOption(TagId Id, string Name);

public sealed record ParaKindFilter(BrainItemKind? Kind, string Name);

public sealed record ParaTagFilter(TagId? TagId, string Name);

public sealed record ParaItemSummary(
    SecondBrainItemId Id,
    BrainItemKind Kind,
    string Title,
    string Content,
    string PrimaryLocation,
    string SecondaryRelationships,
    bool IsFavorite,
    bool IsArchived);

public sealed partial class ParaBrowserViewModel : ObservableObject
{
    private readonly ICoreKnowledgeRepository _repository;
    private readonly CoreKnowledgeUseCases _useCases;
    private readonly Func<DateTimeOffset> _now;
    private CoreKnowledgeState? _state;

    public ParaBrowserViewModel(
        ICoreKnowledgeRepository repository,
        CoreKnowledgeUseCases useCases)
        : this(repository, useCases, () => DateTimeOffset.UtcNow)
    {
    }

    internal ParaBrowserViewModel(
        ICoreKnowledgeRepository repository,
        CoreKnowledgeUseCases useCases,
        Func<DateTimeOffset> now)
    {
        _repository = repository;
        _useCases = useCases;
        _now = now;
        KindFilters =
        [
            new(null, "All kinds"),
            .. Enum.GetValues<BrainItemKind>()
                .Select(kind => new ParaKindFilter(kind, FormatName(kind.ToString()))),
        ];
        SelectedKindFilter = KindFilters[0];
    }

    public IReadOnlyList<ParaKindFilter> KindFilters { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    public partial IReadOnlyList<ParaContextItem> Contexts { get; set; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    public partial IReadOnlyList<ParaItemSummary> Items { get; set; } = [];

    [ObservableProperty]
    public partial IReadOnlyList<ParaDestination> Destinations { get; set; } = [];

    [ObservableProperty]
    public partial IReadOnlyList<ParaTagOption> AvailableTags { get; set; } = [];

    [ObservableProperty]
    public partial IReadOnlyList<ParaTagFilter> TagFilters { get; set; } =
        [new(null, "All tags")];

    [ObservableProperty]
    public partial IReadOnlyList<ParaItemSummary> AvailableLinkTargets { get; set; } = [];

    [ObservableProperty]
    public partial ParaContextItem? SelectedContext { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedItem))]
    [NotifyPropertyChangedFor(nameof(CanArchiveSelected))]
    [NotifyPropertyChangedFor(nameof(CanRestoreSelected))]
    public partial ParaItemSummary? SelectedItem { get; set; }

    [ObservableProperty]
    public partial ParaDestination? SelectedDestination { get; set; }

    [ObservableProperty]
    public partial ParaTagOption? SelectedTagToAdd { get; set; }

    [ObservableProperty]
    public partial ParaItemSummary? SelectedLinkTarget { get; set; }

    [ObservableProperty]
    public partial ParaKindFilter SelectedKindFilter { get; set; }

    [ObservableProperty]
    public partial ParaTagFilter? SelectedTagFilter { get; set; }

    [ObservableProperty]
    public partial bool FavoritesOnly { get; set; }

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    // RefreshView executes its command when this becomes true, so it must not
    // share state with the general-purpose loading indicator.
    [ObservableProperty]
    public partial bool IsRefreshing { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    public partial string? ErrorMessage { get; set; }

    [ObservableProperty]
    public partial string StatusMessage { get; set; } = string.Empty;

    public bool IsEmpty => Items.Count == 0;

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public bool HasSelectedItem => SelectedItem is not null;

    public bool CanArchiveSelected => SelectedItem is { IsArchived: false };

    public bool CanRestoreSelected => SelectedItem is { IsArchived: true };

    [RelayCommand]
    private async Task LoadAsync(CancellationToken cancellationToken) =>
        await RefreshAsync(null, cancellationToken);

    public async Task<bool> MoveSelectedAsync(
        ParaDestination? destination,
        CancellationToken cancellationToken = default)
    {
        if (SelectedItem is null || destination is null)
        {
            return Fail("Choose an item and an active destination first.");
        }

        var selectedId = SelectedItem.Id;
        return await RunOperationAsync(
            async () =>
            {
                var state = await _repository.LoadStateAsync(cancellationToken);
                var item = state.BrainItems.SingleOrDefault(
                    candidate => candidate.Id == selectedId);
                if (item is null)
                {
                    return Fail("The item no longer exists. Refresh and try again.");
                }

                if (!IsActiveDestination(state, destination.Placement))
                {
                    return Fail(
                        "The destination is no longer available. Choose another location.");
                }

                var updatedAt = _now();
                if (updatedAt <= item.UpdatedAt)
                {
                    updatedAt = item.UpdatedAt.AddTicks(1);
                }

                var result = await _useCases.MoveBrainItemAsync(
                    new MoveBrainItemCommand(selectedId, destination.Placement, updatedAt),
                    cancellationToken);
                if (!result.IsSuccess)
                {
                    return Fail(result.Error!.Message);
                }

                StatusMessage = $"Moved to {destination.Name}.";
                await RefreshAsync(selectedId, cancellationToken);
                return true;
            });
    }

    public Task<bool> ArchiveSelectedAsync(
        CancellationToken cancellationToken = default) =>
        ChangeArchiveStateAsync(archive: true, cancellationToken);

    public Task<bool> RestoreSelectedAsync(
        CancellationToken cancellationToken = default) =>
        ChangeArchiveStateAsync(archive: false, cancellationToken);

    public async Task<bool> AddTagToSelectedAsync(
        ParaTagOption? tag,
        CancellationToken cancellationToken = default)
    {
        if (SelectedItem is null || tag is null)
        {
            return Fail("Choose an item and a tag first.");
        }

        var selectedId = SelectedItem.Id;
        return await RunOperationAsync(
            async () =>
            {
                var state = await _repository.LoadStateAsync(cancellationToken);
                var item = state.BrainItems.SingleOrDefault(
                    candidate => candidate.Id == selectedId);
                var currentTag = state.Tags.SingleOrDefault(
                    candidate => candidate.Id == tag.Id);
                if (item is null || currentTag is null)
                {
                    return Fail(
                        "The item or tag is no longer available. Refresh and try again.");
                }

                if (item.IsArchived)
                {
                    return Fail("Restore the item before changing its tags.");
                }

                item.AddTag(currentTag.Id);
                await _repository.SaveStateAsync(state, cancellationToken);
                StatusMessage = $"Added tag {currentTag.Name}.";
                await RefreshAsync(selectedId, cancellationToken);
                return true;
            });
    }

    public async Task<bool> AddLinkToSelectedAsync(
        ParaItemSummary? target,
        CancellationToken cancellationToken = default)
    {
        if (SelectedItem is null || target is null)
        {
            return Fail("Choose an item and a related item first.");
        }

        var selectedId = SelectedItem.Id;
        return await RunOperationAsync(
            async () =>
            {
                var state = await _repository.LoadStateAsync(cancellationToken);
                var item = state.BrainItems.SingleOrDefault(
                    candidate => candidate.Id == selectedId);
                var linkedItem = state.BrainItems.SingleOrDefault(
                    candidate => candidate.Id == target.Id && !candidate.IsArchived);
                if (item is null || linkedItem is null)
                {
                    return Fail(
                        "The item or link target is no longer available. Refresh and try again.");
                }

                if (item.IsArchived)
                {
                    return Fail("Restore the item before changing its links.");
                }

                item.AddContextualLink(linkedItem.Id);
                await _repository.SaveStateAsync(state, cancellationToken);
                StatusMessage = $"Linked {linkedItem.Title}.";
                await RefreshAsync(selectedId, cancellationToken);
                return true;
            });
    }

    partial void OnSelectedContextChanged(ParaContextItem? value) =>
        ApplyFilters();

    partial void OnSelectedItemChanged(ParaItemSummary? value)
    {
        UpdateOrganizationChoices(value);
        OnPropertyChanged(nameof(HasSelectedItem));
        OnPropertyChanged(nameof(CanArchiveSelected));
        OnPropertyChanged(nameof(CanRestoreSelected));
    }

    partial void OnSelectedKindFilterChanged(ParaKindFilter value) =>
        ApplyFilters();

    partial void OnSelectedTagFilterChanged(ParaTagFilter? value) =>
        ApplyFilters();

    partial void OnFavoritesOnlyChanged(bool value) =>
        ApplyFilters();

    private async Task<bool> ChangeArchiveStateAsync(
        bool archive,
        CancellationToken cancellationToken)
    {
        if (SelectedItem is null)
        {
            return Fail("Choose an item first.");
        }

        var selectedId = SelectedItem.Id;
        return await RunOperationAsync(
            async () =>
            {
                var result = archive
                    ? await _useCases.ArchiveBrainItemAsync(
                        new ArchiveBrainItemCommand(selectedId),
                        cancellationToken)
                    : await _useCases.RestoreBrainItemAsync(
                        new RestoreBrainItemCommand(selectedId),
                        cancellationToken);
                if (!result.IsSuccess)
                {
                    return Fail(result.Error!.Message);
                }

                StatusMessage = archive ? "Item archived." : "Item restored.";
                await RefreshAsync(selectedId, cancellationToken);
                return true;
            });
    }

    private async Task<bool> RunOperationAsync(Func<Task<bool>> operation)
    {
        IsLoading = true;
        ErrorMessage = null;
        StatusMessage = string.Empty;
        try
        {
            return await operation();
        }
        catch (Exception exception)
        {
            return Fail($"The change could not be saved. {exception.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task RefreshAsync(
        SecondBrainItemId? preferredItemId,
        CancellationToken cancellationToken)
    {
        IsLoading = true;
        ErrorMessage = null;
        (ParaContextKind Kind, Guid? Id)? contextKey = SelectedContext is null
            ? null
            : (SelectedContext.Kind, SelectedContext.Id);

        try
        {
            _state = await _repository.LoadStateAsync(cancellationToken);
            Contexts = BuildContexts(_state);
            Destinations = BuildDestinations(_state);
            TagFilters =
            [
                new(null, "All tags"),
                .. _state.Tags
                    .OrderBy(tag => tag.Name, StringComparer.OrdinalIgnoreCase)
                    .Select(tag => new ParaTagFilter(tag.Id, tag.Name)),
            ];

            SelectedContext = contextKey is null
                ? Contexts.FirstOrDefault()
                : Contexts.FirstOrDefault(context =>
                    context.Kind == contextKey.Value.Kind &&
                    context.Id == contextKey.Value.Id) ?? Contexts.FirstOrDefault();
            ApplyFilters(preferredItemId);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // A superseded or explicitly canceled load is not a user-facing failure.
        }
        catch (Exception exception)
        {
            ErrorMessage = $"PARA content could not be loaded. {exception.Message}";
        }
        finally
        {
            IsLoading = false;
            IsRefreshing = false;
        }
    }

    private void ApplyFilters(SecondBrainItemId? preferredItemId = null)
    {
        if (_state is null || SelectedContext is null)
        {
            Items = [];
            SelectedItem = null;
            return;
        }

        var selectedId = preferredItemId ?? SelectedItem?.Id;
        var selectedTagId = SelectedTagFilter?.TagId;
        Items = _state.BrainItems
            .Where(item => IsInContext(_state, item, SelectedContext))
            .Where(item =>
                SelectedKindFilter.Kind is null ||
                item.Kind == SelectedKindFilter.Kind)
            .Where(item =>
                selectedTagId is null ||
                item.TagIds.Contains(selectedTagId.Value))
            .Where(item => !FavoritesOnly || item.IsFavorite)
            .OrderBy(item => item.Title, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Id.Value)
            .Select(item => ToSummary(_state, item))
            .ToArray();
        SelectedItem = selectedId is null
            ? Items.FirstOrDefault()
            : Items.FirstOrDefault(item => item.Id == selectedId.Value);
        OnPropertyChanged(nameof(IsEmpty));
    }

    private void UpdateOrganizationChoices(ParaItemSummary? selected)
    {
        if (_state is null || selected is null)
        {
            AvailableTags = [];
            AvailableLinkTargets = [];
            SelectedDestination = null;
            SelectedTagToAdd = null;
            SelectedLinkTarget = null;
            return;
        }

        var item = _state.BrainItems.SingleOrDefault(candidate => candidate.Id == selected.Id);
        if (item is null)
        {
            AvailableTags = [];
            AvailableLinkTargets = [];
            return;
        }

        AvailableTags = _state.Tags
            .Where(tag => !item.TagIds.Contains(tag.Id))
            .OrderBy(tag => tag.Name, StringComparer.OrdinalIgnoreCase)
            .Select(tag => new ParaTagOption(tag.Id, tag.Name))
            .ToArray();
        AvailableLinkTargets = _state.BrainItems
            .Where(candidate =>
                candidate.Id != item.Id &&
                !candidate.IsArchived &&
                !item.ContextualLinks.Contains(candidate.Id))
            .OrderBy(candidate => candidate.Title, StringComparer.OrdinalIgnoreCase)
            .ThenBy(candidate => candidate.Id.Value)
            .Select(candidate => ToSummary(_state, candidate))
            .ToArray();
        SelectedDestination = Destinations.FirstOrDefault(destination =>
            destination.Placement != item.PrimaryPlacement);
        SelectedTagToAdd = AvailableTags.FirstOrDefault();
        SelectedLinkTarget = AvailableLinkTargets.FirstOrDefault();
    }

    private static IReadOnlyList<ParaContextItem> BuildContexts(
        CoreKnowledgeState state)
    {
        var contexts = state.Projects
            .Where(project => !project.IsArchived)
            .OrderBy(project => project.Name.Value, StringComparer.OrdinalIgnoreCase)
            .Select(project => new ParaContextItem(
                ParaContextKind.Project,
                project.Id.Value,
                project.Name.Value,
                $"Project · {project.Status} · {project.Outcome}"))
            .Concat(state.Areas
                .Where(area => !area.IsArchived && !IsInboxArea(area))
                .OrderBy(area => area.Name.Value, StringComparer.OrdinalIgnoreCase)
                .Select(area => new ParaContextItem(
                    ParaContextKind.Area,
                    area.Id.Value,
                    area.Name.Value,
                    "Area")))
            .Concat(state.ResourceTopics
                .Where(topic => !topic.IsArchived)
                .OrderBy(topic => topic.Name.Value, StringComparer.OrdinalIgnoreCase)
                .Select(topic => new ParaContextItem(
                    ParaContextKind.ResourceTopic,
                    topic.Id.Value,
                    topic.Name.Value,
                    "Resource topic")))
            .ToList();
        contexts.Add(new ParaContextItem(
            ParaContextKind.Inbox,
            null,
            "Inbox",
            "Unprocessed captured ideas"));
        contexts.Add(new ParaContextItem(
            ParaContextKind.Archive,
            null,
            "Archive",
            "Archived items"));
        return contexts;
    }

    private static IReadOnlyList<ParaDestination> BuildDestinations(
        CoreKnowledgeState state) =>
        state.Projects
            .Where(project => !project.IsArchived)
            .Select(project => new ParaDestination(
                PrimaryPlacement.InProject(project.Id),
                $"Project · {project.Name.Value}"))
            .Concat(state.Areas
                .Where(area => !area.IsArchived)
                .Select(area => new ParaDestination(
                    PrimaryPlacement.InArea(area.Id),
                    IsInboxArea(area) ? "Inbox" : $"Area · {area.Name.Value}")))
            .Concat(state.ResourceTopics
                .Where(topic => !topic.IsArchived)
                .Select(topic => new ParaDestination(
                    PrimaryPlacement.InResourceTopic(topic.Id),
                    $"Resource · {topic.Name.Value}")))
            .OrderBy(destination => destination.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private static ParaItemSummary ToSummary(
        CoreKnowledgeState state,
        BrainItem item)
    {
        var tagNames = state.Tags
            .Where(tag => item.TagIds.Contains(tag.Id))
            .Select(tag => tag.Name);
        var linkNames = state.BrainItems
            .Where(candidate => item.ContextualLinks.Contains(candidate.Id))
            .Select(candidate => candidate.Title);
        var relationships = tagNames
            .Select(name => $"#{name}")
            .Concat(linkNames.Select(name => $"↔ {name}"))
            .ToArray();

        return new ParaItemSummary(
            item.Id,
            item.Kind,
            item.Title,
            item.Content,
            PlacementName(state, item.PrimaryPlacement),
            relationships.Length == 0
                ? "No tags or related items"
                : string.Join(" · ", relationships),
            item.IsFavorite,
            item.IsArchived);
    }

    private static bool IsInContext(
        CoreKnowledgeState state,
        BrainItem item,
        ParaContextItem context) =>
        context.Kind switch
        {
            ParaContextKind.Archive => item.IsArchived,
            ParaContextKind.Inbox =>
                !item.IsArchived &&
                item.PrimaryPlacement.Kind == PrimaryPlacementKind.Area &&
                state.Areas.Any(area =>
                    !area.IsArchived &&
                    IsInboxArea(area) &&
                    area.Id.Value == item.PrimaryPlacement.ContextId),
            ParaContextKind.Project =>
                !item.IsArchived &&
                item.PrimaryPlacement.Kind == PrimaryPlacementKind.Project &&
                item.PrimaryPlacement.ContextId == context.Id,
            ParaContextKind.Area =>
                !item.IsArchived &&
                item.PrimaryPlacement.Kind == PrimaryPlacementKind.Area &&
                item.PrimaryPlacement.ContextId == context.Id,
            ParaContextKind.ResourceTopic =>
                !item.IsArchived &&
                item.PrimaryPlacement.Kind == PrimaryPlacementKind.ResourceTopic &&
                item.PrimaryPlacement.ContextId == context.Id,
            _ => false,
        };

    private static bool IsActiveDestination(
        CoreKnowledgeState state,
        PrimaryPlacement placement) =>
        placement.Kind switch
        {
            PrimaryPlacementKind.Project => state.Projects.Any(project =>
                project.Id.Value == placement.ContextId && !project.IsArchived),
            PrimaryPlacementKind.Area => state.Areas.Any(area =>
                area.Id.Value == placement.ContextId && !area.IsArchived),
            PrimaryPlacementKind.ResourceTopic => state.ResourceTopics.Any(topic =>
                topic.Id.Value == placement.ContextId && !topic.IsArchived),
            _ => false,
        };

    private static string PlacementName(
        CoreKnowledgeState state,
        PrimaryPlacement placement) =>
        placement.Kind switch
        {
            PrimaryPlacementKind.Project => state.Projects
                .FirstOrDefault(project => project.Id.Value == placement.ContextId)?
                .Name.Value ?? "Unavailable project",
            PrimaryPlacementKind.Area => state.Areas
                .FirstOrDefault(area => area.Id.Value == placement.ContextId)?
                .Name.Value ?? "Unavailable area",
            PrimaryPlacementKind.ResourceTopic => state.ResourceTopics
                .FirstOrDefault(topic => topic.Id.Value == placement.ContextId)?
                .Name.Value ?? "Unavailable resource topic",
            _ => "Unavailable location",
        };

    private bool Fail(string message)
    {
        ErrorMessage = message;
        return false;
    }

    private static bool IsInboxArea(Area area) =>
        string.Equals(area.Name.Value, "Inbox", StringComparison.OrdinalIgnoreCase);

    private static string FormatName(string value) =>
        string.Concat(value.Select((character, index) =>
            index > 0 && char.IsUpper(character)
                ? $" {character}"
                : character.ToString()));
}
