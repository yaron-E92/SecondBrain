using SecondBrain.Domain.Entities;
using SecondBrain.Domain.ValueObjects;
using NUnit.Framework;

namespace SecondBrain.Domain.Tests;

[TestFixture]
public sealed class BrainItemsTests
{
    private static readonly DateTimeOffset CreatedAt =
        new(2026, 7, 24, 8, 30, 0, TimeSpan.Zero);

    [Test]
    public void AuthoredItemKinds_ShareCommonMetadataAndContent()
    {
        var placement = PrimaryPlacement.InArea(AreaId.New());
        var tags = new[] { " Reflection ", "personal" };
        var note = CreateItem(
            BrainItemKind.Note,
            placement: placement,
            noteKind: NoteKind.General,
            tags: tags);
        var idea = CreateItem(
            BrainItemKind.Idea,
            placement: placement,
            ideaMaturity: IdeaMaturity.Captured,
            tags: tags);
        var entry = CreateItem(
            BrainItemKind.JournalEntry,
            placement: placement,
            entryDate: new DateOnly(2026, 7, 24),
            tags: tags);

        foreach (var item in new[] { note, idea, entry })
        {
            Assert.Multiple(() =>
            {
                Assert.That(item.Title, Is.EqualTo("Title"));
                Assert.That(item.Content, Is.EqualTo("Content"));
                Assert.That(item.PrimaryPlacement, Is.EqualTo(placement));
                Assert.That(item.CreatedAt, Is.EqualTo(CreatedAt));
                Assert.That(item.UpdatedAt, Is.EqualTo(CreatedAt));
                Assert.That(item.Tags, Is.EqualTo(new[] { "Reflection", "personal" }));
                Assert.That(item.IsArchived, Is.False);
            });
        }
    }

    [Test]
    public void Items_ExposeOnlyMetadataForTheirKind()
    {
        var note = CreateItem(BrainItemKind.Note, noteKind: NoteKind.General);
        var idea = CreateItem(
            BrainItemKind.Idea,
            ideaMaturity: IdeaMaturity.Captured);
        var entryDate = new DateOnly(2026, 7, 24);
        var entry = CreateItem(
            BrainItemKind.JournalEntry,
            entryDate: entryDate);

        Assert.Multiple(() =>
        {
            Assert.That(note.NoteKind, Is.EqualTo(NoteKind.General));
            Assert.That(note.IdeaMaturity, Is.Null);
            Assert.That(note.EntryDate, Is.Null);
            Assert.That(idea.NoteKind, Is.Null);
            Assert.That(idea.IdeaMaturity, Is.EqualTo(IdeaMaturity.Captured));
            Assert.That(idea.EntryDate, Is.Null);
            Assert.That(entry.NoteKind, Is.Null);
            Assert.That(entry.IdeaMaturity, Is.Null);
            Assert.That(entry.EntryDate, Is.EqualTo(entryDate));
        });
    }

    [Test]
    public void Idea_AdvancesFromCapturedThroughSharpenedToActionable()
    {
        var idea = CreateItem(
            BrainItemKind.Idea,
            ideaMaturity: IdeaMaturity.Captured);

        idea.Sharpen();
        Assert.That(idea.IdeaMaturity, Is.EqualTo(IdeaMaturity.Sharpened));

        idea.MakeActionable();
        Assert.That(idea.IdeaMaturity, Is.EqualTo(IdeaMaturity.Actionable));
    }

    [Test]
    public void Idea_InvalidLifecycleTransitionsFailPredictably()
    {
        var capturedIdea = CreateItem(
            BrainItemKind.Idea,
            ideaMaturity: IdeaMaturity.Captured);
        var note = CreateItem(BrainItemKind.Note, noteKind: NoteKind.General);

        Assert.Multiple(() =>
        {
            Assert.Throws<InvalidOperationException>(capturedIdea.MakeActionable);
            Assert.Throws<InvalidOperationException>(note.Sharpen);
        });

        capturedIdea.Sharpen();

        Assert.Throws<InvalidOperationException>(capturedIdea.Sharpen);

        capturedIdea.Archive();

        Assert.Throws<InvalidOperationException>(capturedIdea.MakeActionable);
    }

    [Test]
    public void Journal_OrdersEntriesByDateThenIdentity()
    {
        var journal = new Journal(SecondBrainItemId.New(), "Daily");
        var firstId = new SecondBrainItemId(
            Guid.Parse("00000000-0000-0000-0000-000000000001"));
        var secondId = new SecondBrainItemId(
            Guid.Parse("00000000-0000-0000-0000-000000000002"));
        var laterId = new SecondBrainItemId(
            Guid.Parse("00000000-0000-0000-0000-000000000003"));
        var first = CreateItem(
            BrainItemKind.JournalEntry,
            id: firstId,
            entryDate: new DateOnly(2026, 7, 23));
        var second = CreateItem(
            BrainItemKind.JournalEntry,
            id: secondId,
            entryDate: new DateOnly(2026, 7, 23));
        var later = CreateItem(
            BrainItemKind.JournalEntry,
            id: laterId,
            entryDate: new DateOnly(2026, 7, 24));

        journal.AddEntry(later);
        journal.AddEntry(second);
        journal.AddEntry(first);

        Assert.That(
            journal.Entries.Select(entry => entry.Id),
            Is.EqualTo(new[] { firstId, secondId, laterId }));
    }

    [Test]
    public void Journal_RejectsNonEntriesAndDuplicateMembership()
    {
        var journal = new Journal(SecondBrainItemId.New(), "Daily");
        var note = CreateItem(BrainItemKind.Note, noteKind: NoteKind.General);
        var entry = CreateItem(
            BrainItemKind.JournalEntry,
            entryDate: new DateOnly(2026, 7, 24));

        Assert.Throws<ArgumentException>(() => journal.AddEntry(note));

        journal.AddEntry(entry);

        Assert.Throws<InvalidOperationException>(() => journal.AddEntry(entry));
    }

    [Test]
    public void JournalEntry_CarriesValidatedContextualLinks()
    {
        var linkedItemId = SecondBrainItemId.New();
        var entry = CreateItem(
            BrainItemKind.JournalEntry,
            entryDate: new DateOnly(2026, 7, 24),
            contextualLinks: new[] { linkedItemId });
        var anotherLinkedItemId = SecondBrainItemId.New();

        entry.AddContextualLink(anotherLinkedItemId);

        Assert.That(
            entry.ContextualLinks,
            Is.EqualTo(new[] { linkedItemId, anotherLinkedItemId }));
        Assert.Multiple(() =>
        {
            Assert.Throws<ArgumentException>(() => entry.AddContextualLink(entry.Id));
            Assert.Throws<InvalidOperationException>(
                () => entry.AddContextualLink(linkedItemId));
        });
    }

    [Test]
    public void InvalidKindsAndKindMetadataFailPredictably()
    {
        Assert.Multiple(() =>
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => CreateItem((BrainItemKind)99));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => CreateItem(BrainItemKind.Note));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => CreateItem(
                    BrainItemKind.Idea,
                    ideaMaturity: (IdeaMaturity)99));
            Assert.Throws<ArgumentException>(
                () => CreateItem(
                    BrainItemKind.JournalEntry,
                    noteKind: NoteKind.General,
                    entryDate: new DateOnly(2026, 7, 24)));
        });
    }

    [Test]
    public void ArchiveLifecycle_RejectsRepeatedTransitionsAndPreservesKind()
    {
        var entry = CreateItem(
            BrainItemKind.JournalEntry,
            entryDate: new DateOnly(2026, 7, 24));

        Assert.Throws<InvalidOperationException>(entry.Restore);

        entry.Archive();

        Assert.Multiple(() =>
        {
            Assert.That(entry.IsArchived, Is.True);
            Assert.That(entry.Kind, Is.EqualTo(BrainItemKind.JournalEntry));
            Assert.Throws<InvalidOperationException>(entry.Archive);
        });

        entry.Restore();

        Assert.That(entry.IsArchived, Is.False);
    }

    private static BrainItem CreateItem(
        BrainItemKind kind,
        SecondBrainItemId? id = null,
        PrimaryPlacement? placement = null,
        NoteKind? noteKind = null,
        IdeaMaturity? ideaMaturity = null,
        DateOnly? entryDate = null,
        IEnumerable<string>? tags = null,
        IEnumerable<SecondBrainItemId>? contextualLinks = null) =>
        new(
            id ?? SecondBrainItemId.New(),
            kind,
            "  Title  ",
            "  Content  ",
            placement ?? PrimaryPlacement.InProject(ProjectId.New()),
            CreatedAt,
            noteKind,
            ideaMaturity,
            entryDate,
            tags,
            contextualLinks);
}
