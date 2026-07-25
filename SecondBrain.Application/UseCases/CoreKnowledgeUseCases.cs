using SecondBrain.Application.Ports;
using SecondBrain.Domain.Entities;
using SecondBrain.Domain.ValueObjects;

namespace SecondBrain.Application.UseCases;

public enum CoreOperationErrorCode
{
    Validation = 1,
    NotFound = 2,
    Conflict = 3,
}

public sealed record CoreOperationError(
    CoreOperationErrorCode Code,
    string Message);

public sealed record CoreOperationResult<T>(
    T? Value,
    CoreOperationError? Error)
{
    public bool IsSuccess => Error is null;

    public static CoreOperationResult<T> Success(T value) =>
        new(value, null);

    public static CoreOperationResult<T> Failure(
        CoreOperationErrorCode code,
        string message) =>
        new(default, new CoreOperationError(code, message));
}

public sealed record CreateBrainItemCommand(BrainItem Item);

public sealed record GetBrainItemQuery(SecondBrainItemId Id);

public sealed record UpdateBrainItemCommand(
    SecondBrainItemId Id,
    string Title,
    string Content,
    DateTimeOffset UpdatedAt);

public sealed record MoveBrainItemCommand(
    SecondBrainItemId Id,
    PrimaryPlacement Placement,
    DateTimeOffset UpdatedAt);

public sealed record ArchiveBrainItemCommand(SecondBrainItemId Id);

public sealed record RestoreBrainItemCommand(SecondBrainItemId Id);

public enum BrainItemLifecycleTransition
{
    SharpenIdea = 1,
    MakeIdeaActionable = 2,
    StartConsumingCapture = 3,
    MarkCaptureDistilled = 4,
    MarkCaptureReferenced = 5,
    MarkResourceCurrent = 6,
    MarkResourceOutdated = 7,
    MarkFavorite = 8,
    UnmarkFavorite = 9,
}

public sealed record TransitionBrainItemCommand(
    SecondBrainItemId Id,
    BrainItemLifecycleTransition Transition);

public sealed record CreateProjectCommand(Project Project);

public sealed record GetProjectQuery(ProjectId Id);

public sealed record UpdateProjectCommand(
    ProjectId Id,
    ParaContextName Name,
    string Outcome,
    ProjectPriority Priority,
    DateOnly? TargetDate);

public sealed record ArchiveProjectCommand(ProjectId Id);

public sealed record RestoreProjectCommand(ProjectId Id);

public enum ProjectLifecycleTransition
{
    Activate = 1,
    Complete = 2,
    Cancel = 3,
}

public sealed record TransitionProjectCommand(
    ProjectId Id,
    ProjectLifecycleTransition Transition);

public sealed record CreateAreaCommand(Area Area);

public sealed record GetAreaQuery(AreaId Id);

public sealed record UpdateAreaCommand(AreaId Id, ParaContextName Name);

public sealed record ArchiveAreaCommand(AreaId Id);

public sealed record RestoreAreaCommand(AreaId Id);

public sealed record CreateResourceTopicCommand(ResourceTopic ResourceTopic);

public sealed record GetResourceTopicQuery(ResourceTopicId Id);

public sealed record UpdateResourceTopicCommand(
    ResourceTopicId Id,
    ParaContextName Name);

public sealed record ArchiveResourceTopicCommand(ResourceTopicId Id);

public sealed record RestoreResourceTopicCommand(ResourceTopicId Id);

public sealed record CreateTagCommand(Tag Tag);

public sealed record GetTagQuery(TagId Id);

public sealed record MoveTagCommand(TagId Id, TagId? ParentId);

public sealed record CreateJournalCommand(Journal Journal);

public sealed record GetJournalQuery(SecondBrainItemId Id);

public sealed record RenameJournalCommand(
    SecondBrainItemId Id,
    string Title);

public sealed record AddJournalEntryCommand(
    SecondBrainItemId JournalId,
    SecondBrainItemId EntryId);

public sealed class CoreKnowledgeUseCases(ICoreKnowledgeRepository repository)
{
    public Task<CoreOperationResult<BrainItem>> GetBrainItemAsync(
        GetBrainItemQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        return ReadAsync(
            state => state.BrainItems.SingleOrDefault(item => item.Id == query.Id),
            "Brain item",
            query.Id.Value,
            cancellationToken);
    }

    public Task<CoreOperationResult<Project>> GetProjectAsync(
        GetProjectQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        return ReadAsync(
            state => state.Projects.SingleOrDefault(project => project.Id == query.Id),
            "Project",
            query.Id.Value,
            cancellationToken);
    }

    public Task<CoreOperationResult<Area>> GetAreaAsync(
        GetAreaQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        return ReadAsync(
            state => state.Areas.SingleOrDefault(area => area.Id == query.Id),
            "Area",
            query.Id.Value,
            cancellationToken);
    }

    public Task<CoreOperationResult<ResourceTopic>> GetResourceTopicAsync(
        GetResourceTopicQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        return ReadAsync(
            state => state.ResourceTopics.SingleOrDefault(
                resourceTopic => resourceTopic.Id == query.Id),
            "Resource topic",
            query.Id.Value,
            cancellationToken);
    }

    public Task<CoreOperationResult<Tag>> GetTagAsync(
        GetTagQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        return ReadAsync(
            state => state.Tags.SingleOrDefault(tag => tag.Id == query.Id),
            "Tag",
            query.Id.Value,
            cancellationToken);
    }

    public Task<CoreOperationResult<Journal>> GetJournalAsync(
        GetJournalQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        return ReadAsync(
            state => state.Journals.SingleOrDefault(journal => journal.Id == query.Id),
            "Journal",
            query.Id.Value,
            cancellationToken);
    }

    public Task<CoreOperationResult<BrainItem>> CreateBrainItemAsync(
        CreateBrainItemCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        return MutateAsync(
            state =>
            {
                if (command.Item is null)
                {
                    return Mutation<BrainItem>.Failed(
                        CoreOperationErrorCode.Validation,
                        "Brain item is required.");
                }

                if (state.BrainItems.Any(item => item.Id == command.Item.Id))
                {
                    return Mutation<BrainItem>.Failed(
                        CoreOperationErrorCode.Conflict,
                        $"Brain item '{command.Item.Id.Value}' already exists.");
                }

                var referenceError = ValidateBrainItemReferences(state, command.Item);
                if (referenceError is not null)
                {
                    return Mutation<BrainItem>.Failed(referenceError);
                }

                return Mutation<BrainItem>.Succeeded(
                    state with
                    {
                        BrainItems = state.BrainItems.Append(command.Item).ToArray(),
                    },
                    command.Item);
            },
            cancellationToken);
    }

    public Task<CoreOperationResult<Project>> CreateProjectAsync(
        CreateProjectCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        return MutateAsync(
            state => AddUnique(
                state,
                command.Project,
                state.Projects,
                project => project.Id == command.Project.Id,
                "Project",
                command.Project.Id.Value,
                projects => state with { Projects = projects }),
            cancellationToken);
    }

    public Task<CoreOperationResult<Area>> CreateAreaAsync(
        CreateAreaCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        return MutateAsync(
            state => AddUnique(
                state,
                command.Area,
                state.Areas,
                area => area.Id == command.Area.Id,
                "Area",
                command.Area.Id.Value,
                areas => state with { Areas = areas }),
            cancellationToken);
    }

    public Task<CoreOperationResult<ResourceTopic>> CreateResourceTopicAsync(
        CreateResourceTopicCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        return MutateAsync(
            state => AddUnique(
                state,
                command.ResourceTopic,
                state.ResourceTopics,
                resourceTopic => resourceTopic.Id == command.ResourceTopic.Id,
                "Resource topic",
                command.ResourceTopic.Id.Value,
                resourceTopics => state with { ResourceTopics = resourceTopics }),
            cancellationToken);
    }

    public Task<CoreOperationResult<Tag>> CreateTagAsync(
        CreateTagCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        return MutateAsync(
            state =>
            {
                if (command.Tag is null)
                {
                    return Mutation<Tag>.Failed(
                        CoreOperationErrorCode.Validation,
                        "Tag is required.");
                }

                if (state.Tags.Any(tag => tag.Id == command.Tag.Id))
                {
                    return Mutation<Tag>.Failed(
                        CoreOperationErrorCode.Conflict,
                        $"Tag '{command.Tag.Id.Value}' already exists.");
                }

                if (command.Tag.Parent is not null &&
                    state.Tags.All(tag => tag.Id != command.Tag.Parent.Id))
                {
                    return Mutation<Tag>.Failed(
                        CoreOperationErrorCode.NotFound,
                        $"Parent tag '{command.Tag.Parent.Id.Value}' was not found.");
                }

                return Mutation<Tag>.Succeeded(
                    state with { Tags = state.Tags.Append(command.Tag).ToArray() },
                    command.Tag);
            },
            cancellationToken);
    }

    public Task<CoreOperationResult<Journal>> CreateJournalAsync(
        CreateJournalCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        return MutateAsync(
            state =>
            {
                if (command.Journal is null)
                {
                    return Mutation<Journal>.Failed(
                        CoreOperationErrorCode.Validation,
                        "Journal is required.");
                }

                if (state.Journals.Any(journal => journal.Id == command.Journal.Id))
                {
                    return Mutation<Journal>.Failed(
                        CoreOperationErrorCode.Conflict,
                        $"Journal '{command.Journal.Id.Value}' already exists.");
                }

                var missingEntry = command.Journal.Entries.FirstOrDefault(
                    entry => state.BrainItems.All(item => item.Id != entry.Id));
                if (missingEntry is not null)
                {
                    return Mutation<Journal>.Failed(
                        CoreOperationErrorCode.NotFound,
                        $"Journal entry '{missingEntry.Id.Value}' was not found.");
                }

                return Mutation<Journal>.Succeeded(
                    state with
                    {
                        Journals = state.Journals.Append(command.Journal).ToArray(),
                    },
                    command.Journal);
            },
            cancellationToken);
    }

    public Task<CoreOperationResult<BrainItem>> UpdateBrainItemAsync(
        UpdateBrainItemCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        return MutateExistingAsync(
            state => state.BrainItems.SingleOrDefault(item => item.Id == command.Id),
            "Brain item",
            command.Id.Value,
            item => item.UpdateContent(command.Title, command.Content, command.UpdatedAt),
            cancellationToken);
    }

    public Task<CoreOperationResult<BrainItem>> MoveBrainItemAsync(
        MoveBrainItemCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        return MutateAsync(
            state =>
            {
                var item = state.BrainItems.SingleOrDefault(
                    candidate => candidate.Id == command.Id);
                if (item is null)
                {
                    return Mutation<BrainItem>.NotFound(
                        "Brain item",
                        command.Id.Value);
                }

                var placementError = ValidatePlacement(state, command.Placement);
                if (placementError is not null)
                {
                    return Mutation<BrainItem>.Failed(placementError);
                }

                item.MoveTo(command.Placement, command.UpdatedAt);
                return Mutation<BrainItem>.Succeeded(state, item);
            },
            cancellationToken);
    }

    public Task<CoreOperationResult<Project>> UpdateProjectAsync(
        UpdateProjectCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        return MutateExistingAsync(
            state => state.Projects.SingleOrDefault(project => project.Id == command.Id),
            "Project",
            command.Id.Value,
            project =>
            {
                project.Rename(command.Name);
                project.UpdateMetadata(
                    command.Outcome,
                    command.Priority,
                    command.TargetDate);
            },
            cancellationToken);
    }

    public Task<CoreOperationResult<Area>> UpdateAreaAsync(
        UpdateAreaCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        return MutateExistingAsync(
            state => state.Areas.SingleOrDefault(area => area.Id == command.Id),
            "Area",
            command.Id.Value,
            area => area.Rename(command.Name),
            cancellationToken);
    }

    public Task<CoreOperationResult<ResourceTopic>> UpdateResourceTopicAsync(
        UpdateResourceTopicCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        return MutateExistingAsync(
            state => state.ResourceTopics.SingleOrDefault(
                resourceTopic => resourceTopic.Id == command.Id),
            "Resource topic",
            command.Id.Value,
            resourceTopic => resourceTopic.Rename(command.Name),
            cancellationToken);
    }

    public Task<CoreOperationResult<Journal>> RenameJournalAsync(
        RenameJournalCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        return MutateExistingAsync(
            state => state.Journals.SingleOrDefault(
                journal => journal.Id == command.Id),
            "Journal",
            command.Id.Value,
            journal => journal.Rename(command.Title),
            cancellationToken);
    }

    public Task<CoreOperationResult<BrainItem>> ArchiveBrainItemAsync(
        ArchiveBrainItemCommand command,
        CancellationToken cancellationToken = default) =>
        ChangeBrainItemArchiveStateAsync(command.Id, archive: true, cancellationToken);

    public Task<CoreOperationResult<BrainItem>> RestoreBrainItemAsync(
        RestoreBrainItemCommand command,
        CancellationToken cancellationToken = default) =>
        ChangeBrainItemArchiveStateAsync(command.Id, archive: false, cancellationToken);

    public Task<CoreOperationResult<Project>> ArchiveProjectAsync(
        ArchiveProjectCommand command,
        CancellationToken cancellationToken = default) =>
        ChangeArchiveStateAsync(
            state => state.Projects.SingleOrDefault(project => project.Id == command.Id),
            "Project",
            command.Id.Value,
            project => project.Archive(),
            cancellationToken);

    public Task<CoreOperationResult<Project>> RestoreProjectAsync(
        RestoreProjectCommand command,
        CancellationToken cancellationToken = default) =>
        ChangeArchiveStateAsync(
            state => state.Projects.SingleOrDefault(project => project.Id == command.Id),
            "Project",
            command.Id.Value,
            project => project.Restore(),
            cancellationToken);

    public Task<CoreOperationResult<Area>> ArchiveAreaAsync(
        ArchiveAreaCommand command,
        CancellationToken cancellationToken = default) =>
        ChangeArchiveStateAsync(
            state => state.Areas.SingleOrDefault(area => area.Id == command.Id),
            "Area",
            command.Id.Value,
            area => area.Archive(),
            cancellationToken);

    public Task<CoreOperationResult<Area>> RestoreAreaAsync(
        RestoreAreaCommand command,
        CancellationToken cancellationToken = default) =>
        ChangeArchiveStateAsync(
            state => state.Areas.SingleOrDefault(area => area.Id == command.Id),
            "Area",
            command.Id.Value,
            area => area.Restore(),
            cancellationToken);

    public Task<CoreOperationResult<ResourceTopic>> ArchiveResourceTopicAsync(
        ArchiveResourceTopicCommand command,
        CancellationToken cancellationToken = default) =>
        ChangeArchiveStateAsync(
            state => state.ResourceTopics.SingleOrDefault(
                resourceTopic => resourceTopic.Id == command.Id),
            "Resource topic",
            command.Id.Value,
            resourceTopic => resourceTopic.Archive(),
            cancellationToken);

    public Task<CoreOperationResult<ResourceTopic>> RestoreResourceTopicAsync(
        RestoreResourceTopicCommand command,
        CancellationToken cancellationToken = default) =>
        ChangeArchiveStateAsync(
            state => state.ResourceTopics.SingleOrDefault(
                resourceTopic => resourceTopic.Id == command.Id),
            "Resource topic",
            command.Id.Value,
            resourceTopic => resourceTopic.Restore(),
            cancellationToken);

    public Task<CoreOperationResult<BrainItem>> TransitionBrainItemAsync(
        TransitionBrainItemCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        return MutateExistingAsync(
            state => state.BrainItems.SingleOrDefault(item => item.Id == command.Id),
            "Brain item",
            command.Id.Value,
            item => ApplyTransition(item, command.Transition),
            cancellationToken);
    }

    public Task<CoreOperationResult<Project>> TransitionProjectAsync(
        TransitionProjectCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        return MutateExistingAsync(
            state => state.Projects.SingleOrDefault(project => project.Id == command.Id),
            "Project",
            command.Id.Value,
            project => ApplyTransition(project, command.Transition),
            cancellationToken);
    }

    public Task<CoreOperationResult<Tag>> MoveTagAsync(
        MoveTagCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        return MutateAsync(
            state =>
            {
                var tag = state.Tags.SingleOrDefault(candidate => candidate.Id == command.Id);
                if (tag is null)
                {
                    return Mutation<Tag>.NotFound("Tag", command.Id.Value);
                }

                Tag? parent = null;
                if (command.ParentId is not null)
                {
                    parent = state.Tags.SingleOrDefault(
                        candidate => candidate.Id == command.ParentId.Value);
                    if (parent is null)
                    {
                        return Mutation<Tag>.NotFound(
                            "Parent tag",
                            command.ParentId.Value.Value);
                    }
                }

                tag.MoveUnder(parent);
                return Mutation<Tag>.Succeeded(state, tag);
            },
            cancellationToken);
    }

    public Task<CoreOperationResult<Journal>> AddJournalEntryAsync(
        AddJournalEntryCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        return MutateAsync(
            state =>
            {
                var journal = state.Journals.SingleOrDefault(
                    candidate => candidate.Id == command.JournalId);
                if (journal is null)
                {
                    return Mutation<Journal>.NotFound(
                        "Journal",
                        command.JournalId.Value);
                }

                var entry = state.BrainItems.SingleOrDefault(
                    item => item.Id == command.EntryId);
                if (entry is null)
                {
                    return Mutation<Journal>.NotFound(
                        "Journal entry",
                        command.EntryId.Value);
                }

                journal.AddEntry(entry);
                return Mutation<Journal>.Succeeded(state, journal);
            },
            cancellationToken);
    }

    private Task<CoreOperationResult<BrainItem>> ChangeBrainItemArchiveStateAsync(
        SecondBrainItemId id,
        bool archive,
        CancellationToken cancellationToken) =>
        ChangeArchiveStateAsync(
            state => state.BrainItems.SingleOrDefault(item => item.Id == id),
            "Brain item",
            id.Value,
            item =>
            {
                if (archive)
                {
                    item.Archive();
                }
                else
                {
                    item.Restore();
                }
            },
            cancellationToken);

    private Task<CoreOperationResult<T>> ChangeArchiveStateAsync<T>(
        Func<CoreKnowledgeState, T?> find,
        string entityName,
        Guid id,
        Action<T> change,
        CancellationToken cancellationToken)
        where T : class =>
        MutateExistingAsync(
            find,
            entityName,
            id,
            change,
            cancellationToken);

    private async Task<CoreOperationResult<T>> ReadAsync<T>(
        Func<CoreKnowledgeState, T?> find,
        string entityName,
        Guid id,
        CancellationToken cancellationToken)
        where T : class
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (id == Guid.Empty)
        {
            return CoreOperationResult<T>.Failure(
                CoreOperationErrorCode.Validation,
                $"{entityName} ID cannot be empty.");
        }

        var state = await repository.LoadStateAsync(cancellationToken);
        var value = find(state);
        return value is null
            ? CoreOperationResult<T>.Failure(
                CoreOperationErrorCode.NotFound,
                $"{entityName} '{id}' was not found.")
            : CoreOperationResult<T>.Success(value);
    }

    private Task<CoreOperationResult<T>> MutateExistingAsync<T>(
        Func<CoreKnowledgeState, T?> find,
        string entityName,
        Guid id,
        Action<T> mutate,
        CancellationToken cancellationToken)
        where T : class =>
        MutateAsync(
            state =>
            {
                if (id == Guid.Empty)
                {
                    return Mutation<T>.Failed(
                        CoreOperationErrorCode.Validation,
                        $"{entityName} ID cannot be empty.");
                }

                var value = find(state);
                if (value is null)
                {
                    return Mutation<T>.NotFound(entityName, id);
                }

                mutate(value);
                return Mutation<T>.Succeeded(state, value);
            },
            cancellationToken);

    private async Task<CoreOperationResult<T>> MutateAsync<T>(
        Func<CoreKnowledgeState, Mutation<T>> mutate,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var state = await repository.LoadStateAsync(cancellationToken);

        Mutation<T> mutation;
        try
        {
            mutation = mutate(state);
        }
        catch (ArgumentException exception)
        {
            return CoreOperationResult<T>.Failure(
                CoreOperationErrorCode.Validation,
                exception.Message);
        }
        catch (InvalidOperationException exception)
        {
            return CoreOperationResult<T>.Failure(
                CoreOperationErrorCode.Conflict,
                exception.Message);
        }

        if (!mutation.Result.IsSuccess)
        {
            return mutation.Result;
        }

        cancellationToken.ThrowIfCancellationRequested();
        await repository.SaveStateAsync(mutation.State!, cancellationToken);
        return mutation.Result;
    }

    private static Mutation<T> AddUnique<T>(
        CoreKnowledgeState state,
        T value,
        IReadOnlyList<T> values,
        Func<T, bool> exists,
        string entityName,
        Guid id,
        Func<IReadOnlyList<T>, CoreKnowledgeState> updateState)
        where T : class
    {
        if (value is null)
        {
            return Mutation<T>.Failed(
                CoreOperationErrorCode.Validation,
                $"{entityName} is required.");
        }

        if (values.Any(exists))
        {
            return Mutation<T>.Failed(
                CoreOperationErrorCode.Conflict,
                $"{entityName} '{id}' already exists.");
        }

        return Mutation<T>.Succeeded(
            updateState(values.Append(value).ToArray()),
            value);
    }

    private static CoreOperationError? ValidateBrainItemReferences(
        CoreKnowledgeState state,
        BrainItem item)
    {
        var placementError = ValidatePlacement(state, item.PrimaryPlacement);
        if (placementError is not null)
        {
            return placementError;
        }

        var missingTag = item.TagIds.FirstOrDefault(
            tagId => state.Tags.All(tag => tag.Id != tagId));
        if (missingTag.Value != Guid.Empty)
        {
            return new CoreOperationError(
                CoreOperationErrorCode.NotFound,
                $"Tag '{missingTag.Value}' was not found.");
        }

        var referencedIds = item.ContextualLinks
            .Concat(item.DerivedItemLinks)
            .Concat(item.ProvenanceSourceLinks);
        var missingItemId = referencedIds.FirstOrDefault(
            itemId => state.BrainItems.All(existing => existing.Id != itemId));
        if (missingItemId.Value != Guid.Empty)
        {
            return new CoreOperationError(
                CoreOperationErrorCode.NotFound,
                $"Referenced Brain item '{missingItemId.Value}' was not found.");
        }

        return null;
    }

    private static CoreOperationError? ValidatePlacement(
        CoreKnowledgeState state,
        PrimaryPlacement placement)
    {
        if (placement is null)
        {
            return new CoreOperationError(
                CoreOperationErrorCode.Validation,
                "Primary placement is required.");
        }

        var isMissing = placement.Kind switch
        {
            PrimaryPlacementKind.Project =>
                state.Projects.All(project => project.Id.Value != placement.ContextId),
            PrimaryPlacementKind.Area =>
                state.Areas.All(area => area.Id.Value != placement.ContextId),
            PrimaryPlacementKind.ResourceTopic =>
                state.ResourceTopics.All(
                    resourceTopic => resourceTopic.Id.Value != placement.ContextId),
            _ => true,
        };
        if (isMissing)
        {
            return new CoreOperationError(
                CoreOperationErrorCode.NotFound,
                $"{placement.Kind} placement '{placement.ContextId}' was not found.");
        }

        var isArchived = placement.Kind switch
        {
            PrimaryPlacementKind.Project =>
                state.Projects.Single(project => project.Id.Value == placement.ContextId)
                    .IsArchived,
            PrimaryPlacementKind.Area =>
                state.Areas.Single(area => area.Id.Value == placement.ContextId)
                    .IsArchived,
            PrimaryPlacementKind.ResourceTopic =>
                state.ResourceTopics.Single(
                    resourceTopic => resourceTopic.Id.Value == placement.ContextId)
                    .IsArchived,
            _ => false,
        };

        return isArchived
            ? new CoreOperationError(
                CoreOperationErrorCode.Conflict,
                $"Cannot place a Brain item in archived {placement.Kind} " +
                $"'{placement.ContextId}'.")
            : null;
    }

    private static void ApplyTransition(
        BrainItem item,
        BrainItemLifecycleTransition transition)
    {
        switch (transition)
        {
            case BrainItemLifecycleTransition.SharpenIdea:
                item.Sharpen();
                break;
            case BrainItemLifecycleTransition.MakeIdeaActionable:
                item.MakeActionable();
                break;
            case BrainItemLifecycleTransition.StartConsumingCapture:
                item.StartConsuming();
                break;
            case BrainItemLifecycleTransition.MarkCaptureDistilled:
                item.MarkDistilled();
                break;
            case BrainItemLifecycleTransition.MarkCaptureReferenced:
                item.MarkReferenced();
                break;
            case BrainItemLifecycleTransition.MarkResourceCurrent:
                item.MarkResourceCurrent();
                break;
            case BrainItemLifecycleTransition.MarkResourceOutdated:
                item.MarkResourceOutdated();
                break;
            case BrainItemLifecycleTransition.MarkFavorite:
                item.MarkFavorite();
                break;
            case BrainItemLifecycleTransition.UnmarkFavorite:
                item.UnmarkFavorite();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(transition));
        }
    }

    private static void ApplyTransition(
        Project project,
        ProjectLifecycleTransition transition)
    {
        switch (transition)
        {
            case ProjectLifecycleTransition.Activate:
                project.Activate();
                break;
            case ProjectLifecycleTransition.Complete:
                project.Complete();
                break;
            case ProjectLifecycleTransition.Cancel:
                project.Cancel();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(transition));
        }
    }

    private sealed record Mutation<T>(
        CoreKnowledgeState? State,
        CoreOperationResult<T> Result)
    {
        public static Mutation<T> Succeeded(
            CoreKnowledgeState state,
            T value) =>
            new(state, CoreOperationResult<T>.Success(value));

        public static Mutation<T> Failed(
            CoreOperationErrorCode code,
            string message) =>
            new(null, CoreOperationResult<T>.Failure(code, message));

        public static Mutation<T> Failed(CoreOperationError error) =>
            Failed(error.Code, error.Message);

        public static Mutation<T> NotFound(string entityName, Guid id) =>
            Failed(
                CoreOperationErrorCode.NotFound,
                $"{entityName} '{id}' was not found.");
    }
}
