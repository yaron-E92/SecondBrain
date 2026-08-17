using Microsoft.EntityFrameworkCore;
using SecondBrain.Abstractions.Items;
using SecondBrain.Abstractions.Modules;
using SecondBrain.Application.Ports;
using SecondBrain.Domain.Entities;
using SecondBrain.Domain.ValueObjects;

namespace SecondBrain.Persistence;

public sealed class SecondBrainDataStore(SecondBrainDbContext context)
    : ICoreKnowledgeRepository
{
    public async Task<CoreKnowledgeState> LoadStateAsync(
        CancellationToken cancellationToken = default)
    {
        var snapshot = await LoadAsync(cancellationToken);
        return new CoreKnowledgeState(
            snapshot.Projects,
            snapshot.Areas,
            snapshot.ResourceTopics,
            snapshot.Tags,
            snapshot.BrainItems,
            snapshot.Journals,
            snapshot.ReviewStates);
    }

    public Task SaveStateAsync(
        CoreKnowledgeState state,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(state);

        return ReplaceAsync(
            new SecondBrainDataSnapshot(
                state.Projects,
                state.Areas,
                state.ResourceTopics,
                state.Tags,
                state.BrainItems,
                state.Journals,
                state.ReviewStates),
            cancellationToken);
    }

    public async Task ReplaceAsync(
        SecondBrainDataSnapshot snapshot,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        await using var transaction =
            await context.Database.BeginTransactionAsync(cancellationToken);

        context.JournalEntries.RemoveRange(context.JournalEntries);
        context.Journals.RemoveRange(context.Journals);
        context.ReviewStates.RemoveRange(context.ReviewStates);
        context.BrainItemRelations.RemoveRange(context.BrainItemRelations);
        context.BrainItemLinks.RemoveRange(context.BrainItemLinks);
        context.BrainItemTags.RemoveRange(context.BrainItemTags);
        context.BrainItemTextTags.RemoveRange(context.BrainItemTextTags);
        context.BrainItems.RemoveRange(context.BrainItems);
        context.Tags.RemoveRange(context.Tags);
        context.ResourceTopics.RemoveRange(context.ResourceTopics);
        context.Areas.RemoveRange(context.Areas);
        context.Projects.RemoveRange(context.Projects);
        await context.SaveChangesAsync(cancellationToken);

        context.Projects.AddRange(snapshot.Projects.Select(ToRow));
        context.Areas.AddRange(snapshot.Areas.Select(ToRow));
        context.ResourceTopics.AddRange(snapshot.ResourceTopics.Select(ToRow));
        context.Tags.AddRange(snapshot.Tags.Select(ToRow));
        context.BrainItems.AddRange(snapshot.BrainItems.Select(ToRow));
        context.BrainItemTextTags.AddRange(
            snapshot.BrainItems.SelectMany(item =>
                item.Tags.Select(tag => new BrainItemTextTagRow
                {
                    BrainItemId = item.Id.Value,
                    Value = tag,
                })));
        context.BrainItemTags.AddRange(
            snapshot.BrainItems.SelectMany(item =>
                item.TagIds.Select(tagId => new BrainItemTagRow
                {
                    BrainItemId = item.Id.Value,
                    TagId = tagId.Value,
                })));
        context.BrainItemLinks.AddRange(
            snapshot.BrainItems.SelectMany(item =>
                item.Links.Select(link => ToRow(item.Id, link))));
        context.BrainItemRelations.AddRange(
            snapshot.BrainItems.SelectMany(ToRelationRows));
        context.Journals.AddRange(
            snapshot.Journals.Select(journal => new JournalRow
            {
                Id = journal.Id.Value,
                Title = journal.Title,
                IsArchived = journal.IsArchived,
            }));
        context.JournalEntries.AddRange(
            snapshot.Journals.SelectMany(journal =>
                journal.Entries.Select(entry => new JournalEntryRow
                {
                    JournalId = journal.Id.Value,
                    BrainItemId = entry.Id.Value,
                })));
        context.ReviewStates.AddRange(
            (snapshot.ReviewStates ?? []).Select(review => new ReviewStateRow
            {
                TargetKind = review.TargetKind,
                TargetId = review.TargetId,
                LastReviewedAt = review.LastReviewedAt,
                DeferredUntil = review.DeferredUntil,
            }));

        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<SecondBrainDataSnapshot> LoadAsync(
        CancellationToken cancellationToken = default)
    {
        var projects = (await context.Projects.AsNoTracking().ToListAsync(cancellationToken))
            .Select(ToDomain)
            .ToArray();
        var areas = (await context.Areas.AsNoTracking().ToListAsync(cancellationToken))
            .Select(ToDomain)
            .ToArray();
        var resourceTopics =
            (await context.ResourceTopics.AsNoTracking().ToListAsync(cancellationToken))
            .Select(ToDomain)
            .ToArray();

        var tagRows = await context.Tags.AsNoTracking().ToListAsync(cancellationToken);
        var tags = MaterializeTags(tagRows);

        var itemRows =
            await context.BrainItems.AsNoTracking().ToListAsync(cancellationToken);
        var textTags = (await context.BrainItemTextTags.AsNoTracking()
                .ToListAsync(cancellationToken))
            .ToLookup(row => row.BrainItemId);
        var itemTags = (await context.BrainItemTags.AsNoTracking()
                .ToListAsync(cancellationToken))
            .ToLookup(row => row.BrainItemId);
        var links = (await context.BrainItemLinks.AsNoTracking()
                .ToListAsync(cancellationToken))
            .ToLookup(row => row.BrainItemId);
        var relations =
            await context.BrainItemRelations.AsNoTracking().ToListAsync(cancellationToken);
        var relationLookup = relations.ToLookup(row => (row.SourceId, row.Kind));

        var brainItems = itemRows.ToDictionary(
            row => row.Id,
            row => ToDomain(
                row,
                textTags[row.Id].Select(tag => tag.Value),
                itemTags[row.Id].Select(tag => new TagId(tag.TagId)),
                links[row.Id].Select(ToDomain),
                relationLookup[(row.Id, BrainItemRelationKind.Contextual)]
                    .Select(relation => new SecondBrainItemId(relation.TargetId)),
                relationLookup[(row.Id, BrainItemRelationKind.Derived)]
                    .Select(relation => new SecondBrainItemId(relation.TargetId))));

        foreach (var relation in relations.Where(
                     row => row.Kind == BrainItemRelationKind.Provenance))
        {
            brainItems[relation.SourceId].AddProvenanceSource(brainItems[relation.TargetId]);
        }

        foreach (var row in itemRows.Where(row => row.IsArchived))
        {
            brainItems[row.Id].Archive();
        }

        var journalRows =
            await context.Journals.AsNoTracking().ToListAsync(cancellationToken);
        var journalEntries = (await context.JournalEntries.AsNoTracking()
                .ToListAsync(cancellationToken))
            .ToLookup(row => row.JournalId);
        var journals = journalRows.Select(row =>
        {
            var journal = new Journal(new SecondBrainItemId(row.Id), row.Title);
            foreach (var entry in journalEntries[row.Id])
            {
                journal.AddEntry(brainItems[entry.BrainItemId]);
            }

            if (row.IsArchived)
            {
                journal.Archive();
            }

            return journal;
        }).ToArray();
        var reviewStates = await context.ReviewStates.AsNoTracking()
            .Select(row => new ReviewState(
                row.TargetKind,
                row.TargetId,
                row.LastReviewedAt,
                row.DeferredUntil))
            .ToArrayAsync(cancellationToken);

        return new SecondBrainDataSnapshot(
            projects,
            areas,
            resourceTopics,
            tags,
            brainItems.Values.OrderBy(item => item.Id.Value).ToArray(),
            journals,
            reviewStates);
    }

    private static ProjectRow ToRow(Project project) =>
        new()
        {
            Id = project.Id.Value,
            Name = project.Name.Value,
            Outcome = project.Outcome,
            Status = project.Status,
            Priority = project.Priority,
            TargetDate = project.TargetDate,
            IsArchived = project.IsArchived,
        };

    private static AreaRow ToRow(Area area) =>
        new()
        {
            Id = area.Id.Value,
            Name = area.Name.Value,
            IsArchived = area.IsArchived,
        };

    private static ResourceTopicRow ToRow(ResourceTopic resourceTopic) =>
        new()
        {
            Id = resourceTopic.Id.Value,
            Name = resourceTopic.Name.Value,
            IsArchived = resourceTopic.IsArchived,
        };

    private static TagRow ToRow(Tag tag) =>
        new()
        {
            Id = tag.Id.Value,
            Name = tag.Name,
            ParentId = tag.Parent?.Id.Value,
        };

    private static BrainItemRow ToRow(BrainItem item)
    {
        var row = new BrainItemRow
        {
            Id = item.Id.Value,
            Kind = item.Kind,
            Title = item.Title,
            Content = item.Content,
            PlacementKind = item.PrimaryPlacement.Kind,
            CreatedAt = item.CreatedAt,
            UpdatedAt = item.UpdatedAt,
            NoteKind = item.NoteKind,
            IdeaMaturity = item.IdeaMaturity,
            EntryDate = item.EntryDate,
            CaptureSourceType = item.CaptureSourceType,
            SourceUri = item.SourceUri?.OriginalString,
            SourceCitation = item.SourceCitation,
            ReminderAt = item.ReminderAt,
            CaptureProcessingState = item.CaptureProcessingState,
            ResourceArtifactKind = item.ResourceArtifactKind,
            ResourceFreshness = item.ResourceFreshness,
            ReviewDate = item.ReviewDate,
            IsArchived = item.IsArchived,
            IsFavorite = item.IsFavorite,
        };

        switch (item.PrimaryPlacement.Kind)
        {
            case PrimaryPlacementKind.Project:
                row.ProjectId = item.PrimaryPlacement.ContextId;
                break;
            case PrimaryPlacementKind.Area:
                row.AreaId = item.PrimaryPlacement.ContextId;
                break;
            case PrimaryPlacementKind.ResourceTopic:
                row.ResourceTopicId = item.PrimaryPlacement.ContextId;
                break;
            default:
                throw new InvalidOperationException("Unknown primary placement kind.");
        }

        return row;
    }

    private static BrainItemLinkRow ToRow(
        SecondBrainItemId brainItemId,
        BrainItemLink link) =>
        new()
        {
            Id = link.Id.Value,
            BrainItemId = brainItemId.Value,
            Type = link.Type,
            TargetModuleId = link.Target.ModuleId.Id,
            TargetModuleName = link.Target.ModuleId.Name,
            TargetExternalId = link.Target.ExternalId,
            TargetItemType = link.Target.ItemType,
            TargetState = link.TargetState,
        };

    private static IEnumerable<BrainItemRelationRow> ToRelationRows(BrainItem item)
    {
        foreach (var target in item.ContextualLinks)
        {
            yield return Relation(item.Id, target, BrainItemRelationKind.Contextual);
        }

        foreach (var target in item.DerivedItemLinks)
        {
            yield return Relation(item.Id, target, BrainItemRelationKind.Derived);
        }

        foreach (var target in item.ProvenanceSourceLinks)
        {
            yield return Relation(item.Id, target, BrainItemRelationKind.Provenance);
        }
    }

    private static BrainItemRelationRow Relation(
        SecondBrainItemId source,
        SecondBrainItemId target,
        BrainItemRelationKind kind) =>
        new()
        {
            SourceId = source.Value,
            TargetId = target.Value,
            Kind = kind,
        };

    private static Project ToDomain(ProjectRow row)
    {
        var project = new Project(
            new ProjectId(row.Id),
            new ParaContextName(row.Name),
            row.Outcome,
            row.Priority,
            row.TargetDate);
        switch (row.Status)
        {
            case ProjectStatus.Active:
                project.Activate();
                break;
            case ProjectStatus.Completed:
                project.Activate();
                project.Complete();
                break;
            case ProjectStatus.Cancelled:
                project.Cancel();
                break;
        }

        if (row.IsArchived)
        {
            project.Archive();
        }

        return project;
    }

    private static Area ToDomain(AreaRow row)
    {
        var area = new Area(new AreaId(row.Id), new ParaContextName(row.Name));
        if (row.IsArchived)
        {
            area.Archive();
        }

        return area;
    }

    private static ResourceTopic ToDomain(ResourceTopicRow row)
    {
        var resourceTopic = new ResourceTopic(
            new ResourceTopicId(row.Id),
            new ParaContextName(row.Name));
        if (row.IsArchived)
        {
            resourceTopic.Archive();
        }

        return resourceTopic;
    }

    private static IReadOnlyList<Tag> MaterializeTags(IReadOnlyCollection<TagRow> rows)
    {
        var rowsById = rows.ToDictionary(row => row.Id);
        var tags = new Dictionary<Guid, Tag>();
        var visiting = new HashSet<Guid>();

        Tag Materialize(Guid id)
        {
            if (tags.TryGetValue(id, out var existing))
            {
                return existing;
            }

            if (!visiting.Add(id))
            {
                throw new InvalidOperationException("Persisted tag hierarchy contains a cycle.");
            }

            var row = rowsById[id];
            var parent = row.ParentId is null ? null : Materialize(row.ParentId.Value);
            var tag = new Tag(new TagId(row.Id), row.Name, parent);
            visiting.Remove(id);
            tags.Add(id, tag);
            return tag;
        }

        return rows.Select(row => Materialize(row.Id)).ToArray();
    }

    private static BrainItem ToDomain(
        BrainItemRow row,
        IEnumerable<string> textTags,
        IEnumerable<TagId> tagIds,
        IEnumerable<BrainItemLink> links,
        IEnumerable<SecondBrainItemId> contextualLinks,
        IEnumerable<SecondBrainItemId> derivedLinks)
    {
        var item = new BrainItem(
            new SecondBrainItemId(row.Id),
            row.Kind,
            row.Title,
            row.Content,
            ToPlacement(row),
            row.CreatedAt,
            row.NoteKind,
            row.IdeaMaturity,
            row.EntryDate,
            textTags,
            contextualLinks,
            row.UpdatedAt,
            row.CaptureSourceType,
            row.SourceUri is null ? null : new Uri(row.SourceUri, UriKind.RelativeOrAbsolute),
            row.SourceCitation,
            row.ReminderAt,
            row.CaptureProcessingState,
            derivedLinks,
            row.ResourceArtifactKind,
            row.ResourceFreshness,
            row.ReviewDate,
            tagIds: tagIds,
            links: links);

        if (row.IsFavorite)
        {
            item.MarkFavorite();
        }

        return item;
    }

    private static PrimaryPlacement ToPlacement(BrainItemRow row) =>
        row.PlacementKind switch
        {
            PrimaryPlacementKind.Project =>
                PrimaryPlacement.InProject(new ProjectId(row.ProjectId!.Value)),
            PrimaryPlacementKind.Area =>
                PrimaryPlacement.InArea(new AreaId(row.AreaId!.Value)),
            PrimaryPlacementKind.ResourceTopic =>
                PrimaryPlacement.InResourceTopic(
                    new ResourceTopicId(row.ResourceTopicId!.Value)),
            _ => throw new InvalidOperationException("Unknown primary placement kind."),
        };

    private static BrainItemLink ToDomain(BrainItemLinkRow row)
    {
        var link = new BrainItemLink(
            new BrainItemLinkId(row.Id),
            row.Type,
            new SecondBrainItemReference(
                new SecondBrainModuleId(row.TargetModuleId, row.TargetModuleName),
                row.TargetExternalId,
                row.TargetItemType));
        if (row.TargetState == BrainItemLinkTargetState.Stale)
        {
            link.MarkTargetStale();
        }
        else if (row.TargetState == BrainItemLinkTargetState.Deleted)
        {
            link.MarkTargetDeleted();
        }

        return link;
    }
}
