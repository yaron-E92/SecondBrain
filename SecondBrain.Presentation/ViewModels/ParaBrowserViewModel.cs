using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Globalization;
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

public sealed record ContextCatalogItem(
    ParaContextKind Kind,
    Guid Id,
    string Name,
    string Details,
    bool IsArchived,
    ProjectStatus? ProjectStatus = null);

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

public sealed record ParaWorkspaceContextTarget(
    ParaContextKind Kind,
    Guid Id,
    string Name);

public sealed record ParaWorkspaceCreateTarget(
    BrainItemKind Kind,
    string Name,
    PrimaryPlacement Placement);

public sealed record ParaWorkspace(
    ParaContextKind Kind,
    Guid Id,
    string Name,
    string Details,
    bool IsArchived,
    IReadOnlyList<ParaItemSummary> Items,
    IReadOnlyList<ParaWorkspaceContextTarget> RelatedContexts,
    IReadOnlyList<ParaWorkspaceCreateTarget> CreateTargets,
    string? UnavailableMessage)
{
    public bool IsAvailable => string.IsNullOrWhiteSpace(UnavailableMessage);

    public bool IsEmpty => IsAvailable && Items.Count == 0;
}

public sealed partial class ParaBrowserViewModel : ObservableObject
{
    private readonly ICoreKnowledgeRepository _repository;
    private readonly CoreKnowledgeUseCases _useCases;
    private readonly Func<DateTimeOffset> _now;
    private CoreKnowledgeState? _state;
    private (ParaContextKind Kind, Guid Id)? _workspaceKey;
    private readonly Stack<(ParaContextKind Kind, Guid Id)> _workspaceHistory = [];

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
    [NotifyPropertyChangedFor(nameof(CatalogProjects))]
    [NotifyPropertyChangedFor(nameof(CatalogAreas))]
    [NotifyPropertyChangedFor(nameof(CatalogResourceTopics))]
    [NotifyPropertyChangedFor(nameof(AreCatalogProjectsEmpty))]
    [NotifyPropertyChangedFor(nameof(AreCatalogAreasEmpty))]
    [NotifyPropertyChangedFor(nameof(AreCatalogResourceTopicsEmpty))]
    public partial IReadOnlyList<ContextCatalogItem> ContextCatalog { get; set; } = [];

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
    [NotifyPropertyChangedFor(nameof(HasSelectedCatalogContext))]
    [NotifyPropertyChangedFor(nameof(CanArchiveSelectedContext))]
    [NotifyPropertyChangedFor(nameof(CanRestoreSelectedContext))]
    [NotifyPropertyChangedFor(nameof(CanActivateSelectedProject))]
    [NotifyPropertyChangedFor(nameof(CanCompleteSelectedProject))]
    [NotifyPropertyChangedFor(nameof(CanCancelSelectedProject))]
    public partial ContextCatalogItem? SelectedCatalogContext { get; set; }

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
    [NotifyPropertyChangedFor(nameof(IsProjectContextEditor))]
    public partial ParaContextKind ContextEditorKind { get; set; } =
        ParaContextKind.Project;

    [ObservableProperty]
    public partial bool IsContextEditorVisible { get; set; }

    [ObservableProperty]
    public partial bool IsCreatingContext { get; set; }

    [ObservableProperty]
    public partial string ContextName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ProjectOutcome { get; set; } = string.Empty;

    [ObservableProperty]
    public partial ProjectPriority ProjectPriority { get; set; } =
        ProjectPriority.Normal;

    [ObservableProperty]
    public partial string ProjectTargetDate { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasContextEditorError))]
    public partial string? ContextEditorError { get; set; }

    [ObservableProperty]
    public partial string ContextEditorStatus { get; set; } = string.Empty;

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

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsBrowserVisible))]
    public partial bool IsWorkspaceOpen { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsWorkspaceAvailable))]
    [NotifyPropertyChangedFor(nameof(IsWorkspaceUnavailable))]
    [NotifyPropertyChangedFor(nameof(IsWorkspaceEmpty))]
    [NotifyPropertyChangedFor(nameof(WorkspaceCaptures))]
    [NotifyPropertyChangedFor(nameof(WorkspaceNotes))]
    [NotifyPropertyChangedFor(nameof(WorkspaceIdeas))]
    [NotifyPropertyChangedFor(nameof(WorkspaceResources))]
    [NotifyPropertyChangedFor(nameof(WorkspaceJournalEntries))]
    [NotifyPropertyChangedFor(nameof(AreWorkspaceCapturesEmpty))]
    [NotifyPropertyChangedFor(nameof(AreWorkspaceNotesEmpty))]
    [NotifyPropertyChangedFor(nameof(AreWorkspaceIdeasEmpty))]
    [NotifyPropertyChangedFor(nameof(AreWorkspaceResourcesEmpty))]
    [NotifyPropertyChangedFor(nameof(AreWorkspaceJournalEntriesEmpty))]
    [NotifyPropertyChangedFor(nameof(WorkspaceRelatedContexts))]
    [NotifyPropertyChangedFor(nameof(HasWorkspaceRelatedContexts))]
    [NotifyPropertyChangedFor(nameof(CanCreateWorkspaceJournalEntry))]
    public partial ParaWorkspace? Workspace { get; set; }

    [ObservableProperty]
    public partial string WorkspaceReturnRoute { get; set; } = "para";

    public bool IsEmpty => Items.Count == 0;

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public bool IsBrowserVisible => !IsWorkspaceOpen;

    public bool IsWorkspaceAvailable => Workspace is { IsAvailable: true };

    public bool IsWorkspaceUnavailable => Workspace is { IsAvailable: false };

    public bool IsWorkspaceEmpty => Workspace is { IsEmpty: true };

    public IReadOnlyList<ParaItemSummary> WorkspaceCaptures =>
        WorkspaceItems(BrainItemKind.KnowledgeCapture);

    public IReadOnlyList<ParaItemSummary> WorkspaceNotes =>
        WorkspaceItems(BrainItemKind.Note);

    public IReadOnlyList<ParaItemSummary> WorkspaceIdeas =>
        WorkspaceItems(BrainItemKind.Idea);

    public IReadOnlyList<ParaItemSummary> WorkspaceResources =>
        WorkspaceItems(BrainItemKind.ResourceArtifact);

    public IReadOnlyList<ParaItemSummary> WorkspaceJournalEntries =>
        WorkspaceItems(BrainItemKind.JournalEntry);

    public bool AreWorkspaceCapturesEmpty => WorkspaceCaptures.Count == 0;

    public bool AreWorkspaceNotesEmpty => WorkspaceNotes.Count == 0;

    public bool AreWorkspaceIdeasEmpty => WorkspaceIdeas.Count == 0;

    public bool AreWorkspaceResourcesEmpty => WorkspaceResources.Count == 0;

    public bool AreWorkspaceJournalEntriesEmpty => WorkspaceJournalEntries.Count == 0;

    public IReadOnlyList<ParaWorkspaceContextTarget> WorkspaceRelatedContexts =>
        Workspace?.RelatedContexts ?? [];

    public bool HasWorkspaceRelatedContexts => WorkspaceRelatedContexts.Count > 0;

    public bool CanCreateWorkspaceJournalEntry =>
        Workspace?.CreateTargets.Any(target =>
            target.Kind == BrainItemKind.JournalEntry) == true;

    public bool HasSelectedItem => SelectedItem is not null;

    public bool CanArchiveSelected => SelectedItem is { IsArchived: false };

    public bool CanRestoreSelected => SelectedItem is { IsArchived: true };

    public IReadOnlyList<ContextCatalogItem> CatalogProjects =>
        ContextCatalog.Where(context => context.Kind == ParaContextKind.Project).ToArray();

    public IReadOnlyList<ContextCatalogItem> CatalogAreas =>
        ContextCatalog.Where(context => context.Kind == ParaContextKind.Area).ToArray();

    public IReadOnlyList<ContextCatalogItem> CatalogResourceTopics =>
        ContextCatalog
            .Where(context => context.Kind == ParaContextKind.ResourceTopic)
            .ToArray();

    public bool AreCatalogProjectsEmpty => CatalogProjects.Count == 0;

    public bool AreCatalogAreasEmpty => CatalogAreas.Count == 0;

    public bool AreCatalogResourceTopicsEmpty => CatalogResourceTopics.Count == 0;

    public bool HasSelectedCatalogContext => SelectedCatalogContext is not null;

    public bool IsProjectContextEditor => ContextEditorKind == ParaContextKind.Project;

    public bool HasContextEditorError => !string.IsNullOrWhiteSpace(ContextEditorError);

    public bool CanArchiveSelectedContext =>
        SelectedCatalogContext is { IsArchived: false };

    public bool CanRestoreSelectedContext =>
        SelectedCatalogContext is { IsArchived: true };

    public bool CanActivateSelectedProject =>
        SelectedCatalogContext is
        {
            Kind: ParaContextKind.Project,
            IsArchived: false,
            ProjectStatus: global::SecondBrain.Domain.Entities.ProjectStatus.Planned,
        };

    public bool CanCompleteSelectedProject =>
        SelectedCatalogContext is
        {
            Kind: ParaContextKind.Project,
            IsArchived: false,
            ProjectStatus: global::SecondBrain.Domain.Entities.ProjectStatus.Active,
        };

    public bool CanCancelSelectedProject =>
        SelectedCatalogContext is
        {
            Kind: ParaContextKind.Project,
            IsArchived: false,
            ProjectStatus: global::SecondBrain.Domain.Entities.ProjectStatus.Planned or
                global::SecondBrain.Domain.Entities.ProjectStatus.Active,
        };

    public void OpenWorkspace(
        ContextCatalogItem? context,
        string returnRoute = "para")
    {
        if (context is null)
        {
            return;
        }

        OpenWorkspace(context.Kind, context.Id, returnRoute);
    }

    public void OpenWorkspace(
        ParaContextKind kind,
        Guid id,
        string returnRoute = "para")
    {
        if (!IsWorkspaceKind(kind) || id == Guid.Empty)
        {
            throw new ArgumentException("A Project, Area, or Resource Topic is required.");
        }

        _workspaceHistory.Clear();
        WorkspaceReturnRoute = NormalizeReturnRoute(returnRoute);
        ShowWorkspace(kind, id);
    }

    public void OpenRelatedWorkspace(ParaWorkspaceContextTarget? target)
    {
        if (target is null)
        {
            return;
        }

        if (_workspaceKey is { } current)
        {
            _workspaceHistory.Push(current);
        }

        ShowWorkspace(target.Kind, target.Id);
    }

    public bool TryReturnToPreviousWorkspace()
    {
        if (_workspaceHistory.Count == 0)
        {
            return false;
        }

        var previous = _workspaceHistory.Pop();
        ShowWorkspace(previous.Kind, previous.Id);
        return true;
    }

    public void CloseWorkspace()
    {
        _workspaceKey = null;
        _workspaceHistory.Clear();
        Workspace = null;
        IsWorkspaceOpen = false;
    }

    public ParaWorkspaceCreateTarget? GetWorkspaceCreateTarget(BrainItemKind kind) =>
        Workspace?.CreateTargets.FirstOrDefault(target => target.Kind == kind);

    [RelayCommand]
    private async Task LoadAsync(CancellationToken cancellationToken) =>
        await RefreshAsync(null, cancellationToken);

    public void BeginCreateContext(ParaContextKind kind)
    {
        if (kind is not (
            ParaContextKind.Project or
            ParaContextKind.Area or
            ParaContextKind.ResourceTopic))
        {
            FailContext("Choose Project, Area, or Resource Topic.");
            return;
        }

        SelectedCatalogContext = null;
        ContextEditorKind = kind;
        ContextName = string.Empty;
        ProjectOutcome = string.Empty;
        ProjectPriority = ProjectPriority.Normal;
        ProjectTargetDate = string.Empty;
        ContextEditorError = null;
        ContextEditorStatus = string.Empty;
        IsCreatingContext = true;
        IsContextEditorVisible = true;
    }

    public void BeginEditContext(ContextCatalogItem? context)
    {
        if (context is null || _state is null)
        {
            FailContext("Choose a context to inspect or edit.");
            return;
        }

        SelectedCatalogContext = context;
        PopulateContextForm(context);
        ContextEditorError = null;
        ContextEditorStatus = string.Empty;
        IsCreatingContext = false;
        IsContextEditorVisible = true;
    }

    public void CancelContextEdit()
    {
        IsContextEditorVisible = false;
        ContextEditorError = null;
        ContextEditorStatus = string.Empty;
    }

    public async Task<bool> SaveContextAsync(
        CancellationToken cancellationToken = default)
    {
        if (!IsContextEditorVisible)
        {
            return FailContext("Open a context form first.");
        }

        var name = ContextName.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            return FailContext("Name is required.");
        }

        DateOnly? targetDate = null;
        if (ContextEditorKind == ParaContextKind.Project)
        {
            if (string.IsNullOrWhiteSpace(ProjectOutcome))
            {
                return FailContext("Project outcome is required.");
            }

            if (!string.IsNullOrWhiteSpace(ProjectTargetDate))
            {
                if (!DateOnly.TryParseExact(
                    ProjectTargetDate.Trim(),
                    "yyyy-MM-dd",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out var parsedTargetDate))
                {
                    return FailContext("Target date must use yyyy-MM-dd.");
                }

                targetDate = parsedTargetDate;
            }
        }

        return await RunContextOperationAsync(
            async () =>
            {
                CoreOperationError? error;
                Guid savedId;
                var contextName = new ParaContextName(name);

                switch (ContextEditorKind)
                {
                    case ParaContextKind.Project:
                    {
                        CoreOperationResult<Project> result;
                        if (IsCreatingContext)
                        {
                            var project = new Project(
                                ProjectId.New(),
                                contextName,
                                ProjectOutcome,
                                ProjectPriority,
                                targetDate);
                            result = await _useCases.CreateProjectAsync(
                                new CreateProjectCommand(project),
                                cancellationToken);
                        }
                        else if (SelectedCatalogContext is
                            { Kind: ParaContextKind.Project } selected)
                        {
                            result = await _useCases.UpdateProjectAsync(
                                new UpdateProjectCommand(
                                    new ProjectId(selected.Id),
                                    contextName,
                                    ProjectOutcome,
                                    ProjectPriority,
                                    targetDate),
                                cancellationToken);
                        }
                        else
                        {
                            return FailContext("The Project selection is stale.");
                        }

                        error = result.Error;
                        savedId = result.Value?.Id.Value ?? Guid.Empty;
                        break;
                    }

                    case ParaContextKind.Area:
                    {
                        CoreOperationResult<Area> result;
                        if (IsCreatingContext)
                        {
                            var area = new Area(AreaId.New(), contextName);
                            result = await _useCases.CreateAreaAsync(
                                new CreateAreaCommand(area),
                                cancellationToken);
                        }
                        else if (SelectedCatalogContext is
                            { Kind: ParaContextKind.Area } selected)
                        {
                            result = await _useCases.UpdateAreaAsync(
                                new UpdateAreaCommand(
                                    new AreaId(selected.Id),
                                    contextName),
                                cancellationToken);
                        }
                        else
                        {
                            return FailContext("The Area selection is stale.");
                        }

                        error = result.Error;
                        savedId = result.Value?.Id.Value ?? Guid.Empty;
                        break;
                    }

                    case ParaContextKind.ResourceTopic:
                    {
                        CoreOperationResult<ResourceTopic> result;
                        if (IsCreatingContext)
                        {
                            var topic = new ResourceTopic(
                                ResourceTopicId.New(),
                                contextName);
                            result = await _useCases.CreateResourceTopicAsync(
                                new CreateResourceTopicCommand(topic),
                                cancellationToken);
                        }
                        else if (SelectedCatalogContext is
                            { Kind: ParaContextKind.ResourceTopic } selected)
                        {
                            result = await _useCases.UpdateResourceTopicAsync(
                                new UpdateResourceTopicCommand(
                                    new ResourceTopicId(selected.Id),
                                    contextName),
                                cancellationToken);
                        }
                        else
                        {
                            return FailContext(
                                "The Resource Topic selection is stale.");
                        }

                        error = result.Error;
                        savedId = result.Value?.Id.Value ?? Guid.Empty;
                        break;
                    }

                    default:
                        return FailContext("Choose a supported context type.");
                }

                if (error is not null)
                {
                    return FailContext(error.Message);
                }

                var status = IsCreatingContext
                    ? $"Created {ContextTypeName(ContextEditorKind)}."
                    : $"Updated {ContextTypeName(ContextEditorKind)}.";
                IsCreatingContext = false;
                await RefreshAsync(
                    null,
                    cancellationToken,
                    (ContextEditorKind, savedId));
                if (SelectedCatalogContext is not null)
                {
                    PopulateContextForm(SelectedCatalogContext);
                }

                ContextEditorStatus = status;
                return true;
            });
    }

    public Task<bool> ArchiveSelectedContextAsync(
        CancellationToken cancellationToken = default) =>
        ChangeSelectedContextArchiveStateAsync(archive: true, cancellationToken);

    public Task<bool> RestoreSelectedContextAsync(
        CancellationToken cancellationToken = default) =>
        ChangeSelectedContextArchiveStateAsync(archive: false, cancellationToken);

    public async Task<bool> TransitionSelectedProjectAsync(
        ProjectLifecycleTransition transition,
        CancellationToken cancellationToken = default)
    {
        if (SelectedCatalogContext is not
            { Kind: ParaContextKind.Project } selected)
        {
            return FailContext("Choose a Project first.");
        }

        return await RunContextOperationAsync(
            async () =>
            {
                var result = await _useCases.TransitionProjectAsync(
                    new TransitionProjectCommand(
                        new ProjectId(selected.Id),
                        transition),
                    cancellationToken);
                if (!result.IsSuccess)
                {
                    return FailContext(result.Error!.Message);
                }

                await RefreshAsync(
                    null,
                    cancellationToken,
                    (ParaContextKind.Project, selected.Id));
                if (SelectedCatalogContext is not null)
                {
                    PopulateContextForm(SelectedCatalogContext);
                }

                ContextEditorStatus = $"Project is now {result.Value!.Status}.";
                return true;
            });
    }

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

    private async Task<bool> ChangeSelectedContextArchiveStateAsync(
        bool archive,
        CancellationToken cancellationToken)
    {
        if (SelectedCatalogContext is not { } selected)
        {
            return FailContext("Choose a context first.");
        }

        return await RunContextOperationAsync(
            async () =>
            {
                CoreOperationError? error = selected.Kind switch
                {
                    ParaContextKind.Project => archive
                        ? (await _useCases.ArchiveProjectAsync(
                            new ArchiveProjectCommand(new ProjectId(selected.Id)),
                            cancellationToken)).Error
                        : (await _useCases.RestoreProjectAsync(
                            new RestoreProjectCommand(new ProjectId(selected.Id)),
                            cancellationToken)).Error,
                    ParaContextKind.Area => archive
                        ? (await _useCases.ArchiveAreaAsync(
                            new ArchiveAreaCommand(new AreaId(selected.Id)),
                            cancellationToken)).Error
                        : (await _useCases.RestoreAreaAsync(
                            new RestoreAreaCommand(new AreaId(selected.Id)),
                            cancellationToken)).Error,
                    ParaContextKind.ResourceTopic => archive
                        ? (await _useCases.ArchiveResourceTopicAsync(
                            new ArchiveResourceTopicCommand(
                                new ResourceTopicId(selected.Id)),
                            cancellationToken)).Error
                        : (await _useCases.RestoreResourceTopicAsync(
                            new RestoreResourceTopicCommand(
                                new ResourceTopicId(selected.Id)),
                            cancellationToken)).Error,
                    _ => new CoreOperationError(
                        CoreOperationErrorCode.Validation,
                        "Choose a supported context type."),
                };

                if (error is not null)
                {
                    return FailContext(error.Message);
                }

                await RefreshAsync(
                    null,
                    cancellationToken,
                    (selected.Kind, selected.Id));
                if (SelectedCatalogContext is not null)
                {
                    PopulateContextForm(SelectedCatalogContext);
                }

                ContextEditorStatus = archive
                    ? $"Archived {ContextTypeName(selected.Kind)}."
                    : $"Restored {ContextTypeName(selected.Kind)}.";
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

    partial void OnSelectedCatalogContextChanged(ContextCatalogItem? value)
    {
        OnPropertyChanged(nameof(HasSelectedCatalogContext));
        OnPropertyChanged(nameof(CanArchiveSelectedContext));
        OnPropertyChanged(nameof(CanRestoreSelectedContext));
        OnPropertyChanged(nameof(CanActivateSelectedProject));
        OnPropertyChanged(nameof(CanCompleteSelectedProject));
        OnPropertyChanged(nameof(CanCancelSelectedProject));
    }

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

    private async Task<bool> RunContextOperationAsync(Func<Task<bool>> operation)
    {
        IsLoading = true;
        ContextEditorError = null;
        ContextEditorStatus = string.Empty;
        try
        {
            return await operation();
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch (Exception exception)
        {
            return FailContext(
                $"The context change could not be saved. {exception.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void ShowWorkspace(ParaContextKind kind, Guid id)
    {
        _workspaceKey = (kind, id);
        IsWorkspaceOpen = true;

        if (_state is null)
        {
            Workspace = null;
            return;
        }

        Workspace = BuildWorkspace(_state, _workspaceKey.Value);
        SelectedContext = Contexts.FirstOrDefault(context =>
            context.Kind == kind && context.Id == id);
        SelectedCatalogContext = ContextCatalog.FirstOrDefault(context =>
            context.Kind == kind && context.Id == id);
        SelectedItem = Workspace.Items.FirstOrDefault();
    }

    private IReadOnlyList<ParaItemSummary> WorkspaceItems(BrainItemKind kind) =>
        Workspace?.Items.Where(item => item.Kind == kind).ToArray() ?? [];

    private static bool IsWorkspaceKind(ParaContextKind kind) =>
        kind is ParaContextKind.Project or
            ParaContextKind.Area or
            ParaContextKind.ResourceTopic;

    private static string NormalizeReturnRoute(string? returnRoute) =>
        returnRoute?.Trim().ToLowerInvariant() switch
        {
            "home" => "home",
            "inbox" => "inbox",
            "search" => "search",
            "editor" => "editor",
            _ => "para",
        };

    private async Task RefreshAsync(
        SecondBrainItemId? preferredItemId,
        CancellationToken cancellationToken,
        (ParaContextKind Kind, Guid Id)? preferredCatalogContext = null)
    {
        IsLoading = true;
        ErrorMessage = null;
        var selectedItemId = preferredItemId ?? SelectedItem?.Id;
        (ParaContextKind Kind, Guid? Id)? contextKey = _workspaceKey is { } workspaceKey
            ? (workspaceKey.Kind, workspaceKey.Id)
            : SelectedContext is null
                ? null
                : (SelectedContext.Kind, SelectedContext.Id);
        var catalogKey = preferredCatalogContext ??
            (_workspaceKey is { } selectedWorkspaceKey
                ? selectedWorkspaceKey
                : SelectedCatalogContext is null
                    ? null
                    : (SelectedCatalogContext.Kind, SelectedCatalogContext.Id));

        try
        {
            _state = await _repository.LoadStateAsync(cancellationToken);
            Contexts = BuildContexts(_state);
            ContextCatalog = BuildContextCatalog(_state);
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
            SelectedCatalogContext = catalogKey is null
                ? SelectedCatalogContext is null
                    ? null
                    : ContextCatalog.FirstOrDefault()
                : ContextCatalog.FirstOrDefault(context =>
                    context.Kind == catalogKey.Value.Kind &&
                    context.Id == catalogKey.Value.Id);
            ApplyFilters(preferredItemId);
            if (_workspaceKey is { } activeWorkspaceKey)
            {
                Workspace = BuildWorkspace(_state, activeWorkspaceKey);
                SelectedItem = selectedItemId is null
                    ? Workspace.Items.FirstOrDefault()
                    : Workspace.Items.FirstOrDefault(item =>
                        item.Id == selectedItemId.Value) ??
                        Workspace.Items.FirstOrDefault();
            }
            else
            {
                Workspace = null;
            }
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

    private void PopulateContextForm(ContextCatalogItem context)
    {
        ContextEditorKind = context.Kind;
        ContextName = context.Name;
        ProjectOutcome = string.Empty;
        ProjectPriority = ProjectPriority.Normal;
        ProjectTargetDate = string.Empty;

        if (context.Kind == ParaContextKind.Project && _state is not null)
        {
            var project = _state.Projects.SingleOrDefault(
                candidate => candidate.Id.Value == context.Id);
            if (project is not null)
            {
                ProjectOutcome = project.Outcome;
                ProjectPriority = project.Priority;
                ProjectTargetDate = project.TargetDate?.ToString("yyyy-MM-dd") ??
                    string.Empty;
            }
        }

        IsContextEditorVisible = true;
        IsCreatingContext = false;
    }

    private static ParaWorkspace BuildWorkspace(
        CoreKnowledgeState state,
        (ParaContextKind Kind, Guid Id) key)
    {
        (string Name, string Details, bool IsArchived)? identity = key.Kind switch
        {
            ParaContextKind.Project => state.Projects
                .Where(project => project.Id.Value == key.Id)
                .Select(project => ((string Name, string Details, bool IsArchived)?)(
                    project.Name.Value,
                    $"{project.Status} · {project.Priority} · {project.Outcome}" +
                    (project.TargetDate is null
                        ? string.Empty
                        : $" · Target {project.TargetDate:yyyy-MM-dd}"),
                    project.IsArchived))
                .SingleOrDefault(),
            ParaContextKind.Area => state.Areas
                .Where(area => area.Id.Value == key.Id)
                .Select(area => ((string Name, string Details, bool IsArchived)?)(
                    area.Name.Value,
                    "Area · Ongoing responsibility",
                    area.IsArchived))
                .SingleOrDefault(),
            ParaContextKind.ResourceTopic => state.ResourceTopics
                .Where(topic => topic.Id.Value == key.Id)
                .Select(topic => ((string Name, string Details, bool IsArchived)?)(
                    topic.Name.Value,
                    "Resource Topic · Reference workspace",
                    topic.IsArchived))
                .SingleOrDefault(),
            _ => null,
        };

        if (identity is null)
        {
            return new ParaWorkspace(
                key.Kind,
                key.Id,
                "Workspace unavailable",
                ContextTypeName(key.Kind),
                false,
                [],
                [],
                [],
                $"This {ContextTypeName(key.Kind)} no longer exists. Return to PARA and choose another workspace.");
        }

        var (name, details, isArchived) = identity.Value;
        if (isArchived)
        {
            return new ParaWorkspace(
                key.Kind,
                key.Id,
                name,
                details,
                true,
                [],
                [],
                [],
                $"This {ContextTypeName(key.Kind)} is archived. Return to PARA to restore or reorganize it.");
        }

        var placement = PlacementFor(key);
        var items = state.BrainItems
            .Where(item => !item.IsArchived && item.PrimaryPlacement == placement)
            .OrderBy(item => item.Kind)
            .ThenBy(item => item.Title, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Id.Value)
            .Select(item => ToSummary(state, item))
            .ToArray();
        var relatedContexts = BuildRelatedContexts(state, items, key);
        var createTargets = new List<ParaWorkspaceCreateTarget>
        {
            new(BrainItemKind.Note, "New Note", placement),
            new(BrainItemKind.KnowledgeCapture, "New Capture", placement),
            new(BrainItemKind.ResourceArtifact, "New Resource", placement),
        };
        if (state.Journals.Count > 0)
        {
            createTargets.Add(new ParaWorkspaceCreateTarget(
                BrainItemKind.JournalEntry,
                "New Journal Entry",
                placement));
        }

        return new ParaWorkspace(
            key.Kind,
            key.Id,
            name,
            details,
            false,
            items,
            relatedContexts,
            createTargets,
            null);
    }

    private static IReadOnlyList<ParaWorkspaceContextTarget> BuildRelatedContexts(
        CoreKnowledgeState state,
        IReadOnlyList<ParaItemSummary> workspaceItems,
        (ParaContextKind Kind, Guid Id) workspaceKey)
    {
        var workspaceItemIds = workspaceItems.Select(item => item.Id).ToHashSet();
        var linkedIds = state.BrainItems
            .Where(item => workspaceItemIds.Contains(item.Id))
            .SelectMany(item => item.ContextualLinks)
            .Concat(state.BrainItems
                .Where(item => item.ContextualLinks.Any(workspaceItemIds.Contains))
                .Select(item => item.Id))
            .ToHashSet();

        return state.BrainItems
            .Where(item => !item.IsArchived && linkedIds.Contains(item.Id))
            .Select(item => ToWorkspaceContextTarget(state, item.PrimaryPlacement))
            .Where(target => target is not null)
            .Select(target => target!)
            .Where(target =>
                target.Kind != workspaceKey.Kind || target.Id != workspaceKey.Id)
            .DistinctBy(target => (target.Kind, target.Id))
            .OrderBy(target => target.Kind)
            .ThenBy(target => target.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static ParaWorkspaceContextTarget? ToWorkspaceContextTarget(
        CoreKnowledgeState state,
        PrimaryPlacement placement) =>
        placement.Kind switch
        {
            PrimaryPlacementKind.Project => state.Projects
                .Where(project =>
                    project.Id.Value == placement.ContextId && !project.IsArchived)
                .Select(project => new ParaWorkspaceContextTarget(
                    ParaContextKind.Project,
                    project.Id.Value,
                    project.Name.Value))
                .SingleOrDefault(),
            PrimaryPlacementKind.Area => state.Areas
                .Where(area =>
                    area.Id.Value == placement.ContextId && !area.IsArchived)
                .Select(area => new ParaWorkspaceContextTarget(
                    ParaContextKind.Area,
                    area.Id.Value,
                    area.Name.Value))
                .SingleOrDefault(),
            PrimaryPlacementKind.ResourceTopic => state.ResourceTopics
                .Where(topic =>
                    topic.Id.Value == placement.ContextId && !topic.IsArchived)
                .Select(topic => new ParaWorkspaceContextTarget(
                    ParaContextKind.ResourceTopic,
                    topic.Id.Value,
                    topic.Name.Value))
                .SingleOrDefault(),
            _ => null,
        };

    private static PrimaryPlacement PlacementFor(
        (ParaContextKind Kind, Guid Id) key) =>
        key.Kind switch
        {
            ParaContextKind.Project => PrimaryPlacement.InProject(new ProjectId(key.Id)),
            ParaContextKind.Area => PrimaryPlacement.InArea(new AreaId(key.Id)),
            ParaContextKind.ResourceTopic => PrimaryPlacement.InResourceTopic(
                new ResourceTopicId(key.Id)),
            _ => throw new ArgumentOutOfRangeException(nameof(key)),
        };

    private static IReadOnlyList<ContextCatalogItem> BuildContextCatalog(
        CoreKnowledgeState state) =>
        state.Projects
            .Select(project => new ContextCatalogItem(
                ParaContextKind.Project,
                project.Id.Value,
                project.Name.Value,
                $"{project.Status} · {project.Priority} · {project.Outcome}",
                project.IsArchived,
                project.Status))
            .Concat(state.Areas
                .Where(area => !IsInboxArea(area))
                .Select(area => new ContextCatalogItem(
                    ParaContextKind.Area,
                    area.Id.Value,
                    area.Name.Value,
                    "Area",
                    area.IsArchived)))
            .Concat(state.ResourceTopics
                .Select(topic => new ContextCatalogItem(
                    ParaContextKind.ResourceTopic,
                    topic.Id.Value,
                    topic.Name.Value,
                    "Resource topic",
                    topic.IsArchived)))
            .OrderBy(context => context.Kind)
            .ThenBy(context => context.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

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

    private bool FailContext(string message)
    {
        ContextEditorError = message;
        return false;
    }

    private static string ContextTypeName(ParaContextKind kind) =>
        kind switch
        {
            ParaContextKind.Project => "Project",
            ParaContextKind.Area => "Area",
            ParaContextKind.ResourceTopic => "Resource Topic",
            _ => "context",
        };

    private static bool IsInboxArea(Area area) =>
        string.Equals(area.Name.Value, "Inbox", StringComparison.OrdinalIgnoreCase);

    private static string FormatName(string value) =>
        string.Concat(value.Select((character, index) =>
            index > 0 && char.IsUpper(character)
                ? $" {character}"
                : character.ToString()));
}
