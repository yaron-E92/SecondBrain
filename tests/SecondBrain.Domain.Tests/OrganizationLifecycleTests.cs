using SecondBrain.Abstractions.Items;
using SecondBrain.Abstractions.Modules;
using SecondBrain.Domain.Entities;
using SecondBrain.Domain.ValueObjects;
using NUnit.Framework;

namespace SecondBrain.Domain.Tests;

[TestFixture]
public sealed class OrganizationLifecycleTests
{
    private static readonly DateTimeOffset CreatedAt =
        new(2026, 7, 24, 9, 0, 0, TimeSpan.Zero);

    [Test]
    public void TagHierarchy_RejectsSelfAndIndirectCycles()
    {
        var root = new Tag(TagId.New(), "Knowledge");
        var child = new Tag(TagId.New(), "Architecture", root);
        var grandchild = new Tag(TagId.New(), "ADRs", child);

        Assert.Multiple(() =>
        {
            Assert.Throws<InvalidOperationException>(() => child.MoveUnder(child));
            Assert.Throws<InvalidOperationException>(() => root.MoveUnder(grandchild));
        });

        grandchild.MoveUnder(root);

        Assert.That(grandchild.Parent, Is.SameAs(root));
    }

    [Test]
    public void TypedLinks_KeepStableIdentityAndOpaqueTarget()
    {
        var tagId = TagId.New();
        var linkId = BrainItemLinkId.New();
        var target = new SecondBrainItemReference(
            new SecondBrainModuleId(Guid.NewGuid(), " ShuffleTask "),
            " task-42 ",
            " Task ");
        var link = new BrainItemLink(linkId, BrainItemLinkType.Supports, target);
        var item = CreateItem(tagIds: new[] { tagId }, links: new[] { link });

        Assert.Multiple(() =>
        {
            Assert.That(item.TagIds, Is.EqualTo(new[] { tagId }));
            Assert.That(item.Links, Is.EqualTo(new[] { link }));
            Assert.That(link.Id, Is.EqualTo(linkId));
            Assert.That(link.Type, Is.EqualTo(BrainItemLinkType.Supports));
            Assert.That(link.Target.ModuleId.Name, Is.EqualTo("ShuffleTask"));
            Assert.That(link.Target.ExternalId, Is.EqualTo("task-42"));
            Assert.That(link.Target.ItemType, Is.EqualTo("Task"));
            Assert.Throws<InvalidOperationException>(() => item.AddTag(tagId));
            Assert.Throws<InvalidOperationException>(() => item.AddLink(link));
        });
    }

    [Test]
    public void TypedLinks_RetainStaleAndDeletedReferences()
    {
        var target = new SecondBrainItemReference(
            new SecondBrainModuleId(Guid.NewGuid(), "PHOODAB"),
            "resource-7",
            "Resource");
        var recoverable = new BrainItemLink(
            BrainItemLinkId.New(),
            BrainItemLinkType.References,
            target);
        var deleted = new BrainItemLink(
            BrainItemLinkId.New(),
            BrainItemLinkType.Related,
            target);

        recoverable.MarkTargetStale();
        recoverable.MarkTargetAvailable();
        deleted.MarkTargetStale();
        deleted.MarkTargetDeleted();

        Assert.Multiple(() =>
        {
            Assert.That(
                recoverable.TargetState,
                Is.EqualTo(BrainItemLinkTargetState.Available));
            Assert.That(deleted.TargetState, Is.EqualTo(BrainItemLinkTargetState.Deleted));
            Assert.That(deleted.Target, Is.EqualTo(target));
            Assert.Throws<InvalidOperationException>(deleted.MarkTargetAvailable);
            Assert.Throws<InvalidOperationException>(deleted.MarkTargetStale);
            Assert.Throws<InvalidOperationException>(deleted.MarkTargetDeleted);
        });
    }

    [Test]
    public void ArchiveRestore_PreservesTypePlacementFavoriteTagsAndLinks()
    {
        var placement = PrimaryPlacement.InArea(AreaId.New());
        var tagId = TagId.New();
        var link = CreateLink();
        var item = CreateItem(
            kind: BrainItemKind.JournalEntry,
            placement: placement,
            entryDate: new DateOnly(2026, 7, 24),
            tagIds: new[] { tagId },
            links: new[] { link });

        item.MarkFavorite();
        item.Archive();

        Assert.Multiple(() =>
        {
            Assert.That(item.Kind, Is.EqualTo(BrainItemKind.JournalEntry));
            Assert.That(item.PrimaryPlacement, Is.EqualTo(placement));
            Assert.That(item.IsFavorite, Is.True);
            Assert.That(item.TagIds, Is.EqualTo(new[] { tagId }));
            Assert.That(item.Links, Is.EqualTo(new[] { link }));
            Assert.Throws<InvalidOperationException>(item.UnmarkFavorite);
        });

        item.Restore();

        Assert.Multiple(() =>
        {
            Assert.That(item.IsArchived, Is.False);
            Assert.That(item.PrimaryPlacement, Is.EqualTo(placement));
            Assert.That(item.IsFavorite, Is.True);
        });
    }

    [Test]
    public void FavoriteAndArchiveFilters_AreDeterministic()
    {
        var first = CreateItem(id: Id("00000000-0000-0000-0000-000000000001"));
        var second = CreateItem(id: Id("00000000-0000-0000-0000-000000000002"));
        var third = CreateItem(id: Id("00000000-0000-0000-0000-000000000003"));

        third.MarkFavorite();
        first.MarkFavorite();
        second.MarkFavorite();
        second.Archive();

        var activeFavorites = BrainItemFilters.Apply(
            new[] { third, second, first },
            isFavorite: true,
            isArchived: false);
        var archivedFavorites = BrainItemFilters.Apply(
            new[] { third, second, first },
            isFavorite: true,
            isArchived: true);

        Assert.Multiple(() =>
        {
            Assert.That(
                activeFavorites.Select(item => item.Id),
                Is.EqualTo(new[] { first.Id, third.Id }));
            Assert.That(
                archivedFavorites.Select(item => item.Id),
                Is.EqualTo(new[] { second.Id }));
        });
    }

    private static BrainItem CreateItem(
        SecondBrainItemId? id = null,
        BrainItemKind kind = BrainItemKind.Note,
        PrimaryPlacement? placement = null,
        DateOnly? entryDate = null,
        IEnumerable<TagId>? tagIds = null,
        IEnumerable<BrainItemLink>? links = null) =>
        new(
            id ?? SecondBrainItemId.New(),
            kind,
            "Title",
            "Content",
            placement ?? PrimaryPlacement.InProject(ProjectId.New()),
            CreatedAt,
            noteKind: kind == BrainItemKind.Note ? NoteKind.General : null,
            entryDate: entryDate,
            tagIds: tagIds,
            links: links);

    private static BrainItemLink CreateLink() =>
        new(
            BrainItemLinkId.New(),
            BrainItemLinkType.Related,
            new SecondBrainItemReference(
                new SecondBrainModuleId(Guid.NewGuid(), "ShuffleTask"),
                "task-1",
                "Task"));

    private static SecondBrainItemId Id(string value) =>
        new(Guid.Parse(value));
}
