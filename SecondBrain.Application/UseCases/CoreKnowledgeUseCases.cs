using SecondBrain.Application.Ports;
using SecondBrain.Domain.Entities;
using SecondBrain.Domain.ValueObjects;

namespace SecondBrain.Application.UseCases;

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

    public Task<CoreOperationResult<BrainItem>> DeriveBrainItemAsync(
        DeriveBrainItemCommand command,
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
                        "Derived item is required.");
                }

                if (command.Item.Kind is not (
                    BrainItemKind.Note or BrainItemKind.ResourceArtifact))
                {
                    return Mutation<BrainItem>.Failed(
                        CoreOperationErrorCode.Validation,
                        "Only Notes and Resource Artifacts can be derived from captures.");
                }

                if (command.SourceCaptureIds is null ||
                    command.SourceCaptureIds.Count == 0)
                {
                    return Mutation<BrainItem>.Failed(
                        CoreOperationErrorCode.Validation,
                        "Select at least one source capture.");
                }

                if (command.SourceCaptureIds.Distinct().Count() !=
                    command.SourceCaptureIds.Count)
                {
                    return Mutation<BrainItem>.Failed(
                        CoreOperationErrorCode.Validation,
                        "Source captures cannot contain duplicates.");
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

                var sources = new List<BrainItem>(command.SourceCaptureIds.Count);
                foreach (var sourceId in command.SourceCaptureIds)
                {
                    var source = state.BrainItems.SingleOrDefault(
                        item => item.Id == sourceId);
                    if (source is null)
                    {
                        return Mutation<BrainItem>.NotFound(
                            "Source capture",
                            sourceId.Value);
                    }

                    if (source.Kind != BrainItemKind.KnowledgeCapture)
                    {
                        return Mutation<BrainItem>.Failed(
                            CoreOperationErrorCode.Validation,
                            $"Source '{source.Id.Value}' is not a Knowledge Capture.");
                    }

                    if (source.IsArchived)
                    {
                        return Mutation<BrainItem>.Failed(
                            CoreOperationErrorCode.Conflict,
                            $"Source capture '{source.Id.Value}' is archived.");
                    }

                    if (source.DerivedItemLinks.Contains(command.Item.Id))
                    {
                        return Mutation<BrainItem>.Failed(
                            CoreOperationErrorCode.Conflict,
                            $"Source capture '{source.Id.Value}' already links to the derived item.");
                    }

                    sources.Add(source);
                }

                if (command.Item.Kind == BrainItemKind.ResourceArtifact)
                {
                    foreach (var source in sources)
                    {
                        command.Item.AddProvenanceSource(source);
                    }
                }

                var updatedSources = sources.ToDictionary(
                    source => source.Id,
                    source => CopyCaptureForDerivation(
                        source,
                        command.Item.Id,
                        command.MarkSourcesReferenced));

                return Mutation<BrainItem>.Succeeded(
                    state with
                    {
                        BrainItems = state.BrainItems
                            .Select(item => updatedSources.GetValueOrDefault(item.Id) ?? item)
                            .Append(command.Item)
                            .ToArray(),
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
            state => AddUniqueNamed(
                state,
                command.Project,
                state.Projects,
                project => project.Id == command.Project.Id,
                project => project.Name.Value,
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
            state => AddUniqueNamed(
                state,
                command.Area,
                state.Areas,
                area => area.Id == command.Area.Id,
                area => area.Name.Value,
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
            state => AddUniqueNamed(
                state,
                command.ResourceTopic,
                state.ResourceTopics,
                resourceTopic => resourceTopic.Id == command.ResourceTopic.Id,
                resourceTopic => resourceTopic.Name.Value,
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

        return MutateAsync(
            state => UpdateNamedContext(
                state,
                state.Projects,
                project => project.Id.Value,
                project => project.Name.Value,
                command.Id.Value,
                command.Name.Value,
                "Project",
                project =>
                {
                    project.UpdateMetadata(
                        command.Outcome,
                        command.Priority,
                        command.TargetDate);
                    project.Rename(command.Name);
                }),
            cancellationToken);
    }

    public Task<CoreOperationResult<Area>> UpdateAreaAsync(
        UpdateAreaCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        return MutateAsync(
            state => UpdateNamedContext(
                state,
                state.Areas,
                area => area.Id.Value,
                area => area.Name.Value,
                command.Id.Value,
                command.Name.Value,
                "Area",
                area => area.Rename(command.Name)),
            cancellationToken);
    }

    public Task<CoreOperationResult<ResourceTopic>> UpdateResourceTopicAsync(
        UpdateResourceTopicCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        return MutateAsync(
            state => UpdateNamedContext(
                state,
                state.ResourceTopics,
                resourceTopic => resourceTopic.Id.Value,
                resourceTopic => resourceTopic.Name.Value,
                command.Id.Value,
                command.Name.Value,
                "Resource topic",
                resourceTopic => resourceTopic.Rename(command.Name)),
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

    private static Mutation<T> AddUniqueNamed<T>(
        CoreKnowledgeState state,
        T value,
        IReadOnlyList<T> values,
        Func<T, bool> idExists,
        Func<T, string> getName,
        string entityName,
        Guid id,
        Func<IReadOnlyList<T>, CoreKnowledgeState> updateState)
        where T : class
    {
        var uniqueId = AddUnique(
            state,
            value,
            values,
            idExists,
            entityName,
            id,
            updateState);
        if (!uniqueId.Result.IsSuccess || value is null)
        {
            return uniqueId;
        }

        var name = getName(value);
        if (values.Any(existing => string.Equals(
            getName(existing),
            name,
            StringComparison.OrdinalIgnoreCase)))
        {
            return Mutation<T>.Failed(
                CoreOperationErrorCode.Conflict,
                $"{entityName} named '{name}' already exists.");
        }

        return uniqueId;
    }

    private static Mutation<T> UpdateNamedContext<T>(
        CoreKnowledgeState state,
        IReadOnlyList<T> values,
        Func<T, Guid> getId,
        Func<T, string> getName,
        Guid id,
        string name,
        string entityName,
        Action<T> update)
        where T : class
    {
        if (id == Guid.Empty)
        {
            return Mutation<T>.Failed(
                CoreOperationErrorCode.Validation,
                $"{entityName} ID cannot be empty.");
        }

        var value = values.SingleOrDefault(existing => getId(existing) == id);
        if (value is null)
        {
            return Mutation<T>.NotFound(entityName, id);
        }

        if (values.Any(existing =>
            getId(existing) != id &&
            string.Equals(
                getName(existing),
                name,
                StringComparison.OrdinalIgnoreCase)))
        {
            return Mutation<T>.Failed(
                CoreOperationErrorCode.Conflict,
                $"{entityName} named '{name}' already exists.");
        }

        update(value);
        return Mutation<T>.Succeeded(state, value);
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

    private static BrainItem CopyCaptureForDerivation(
        BrainItem source,
        SecondBrainItemId derivedItemId,
        bool markReferenced)
    {
        var processingState = markReferenced &&
            source.CaptureProcessingState is (
                CaptureProcessingState.Captured or CaptureProcessingState.Consuming)
            ? CaptureProcessingState.Referenced
            : source.CaptureProcessingState;
        var copy = new BrainItem(
            source.Id,
            source.Kind,
            source.Title,
            source.Content,
            source.PrimaryPlacement,
            source.CreatedAt,
            tags: source.Tags,
            contextualLinks: source.ContextualLinks,
            updatedAt: source.UpdatedAt,
            captureSourceType: source.CaptureSourceType,
            sourceUri: source.SourceUri,
            sourceCitation: source.SourceCitation,
            reminderAt: source.ReminderAt,
            captureProcessingState: processingState,
            derivedItemLinks: source.DerivedItemLinks.Append(derivedItemId),
            tagIds: source.TagIds,
            links: source.Links);
        if (source.IsFavorite)
        {
            copy.MarkFavorite();
        }

        return copy;
    }

}
