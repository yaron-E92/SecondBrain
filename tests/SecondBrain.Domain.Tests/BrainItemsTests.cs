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
    public void JournalArchiveRestore_PreservesEntriesAndControlsMutation()
    {
        var journal = new Journal(SecondBrainItemId.New(), "Daily");
        var entry = CreateItem(
            BrainItemKind.JournalEntry,
            entryDate: new DateOnly(2026, 7, 24));
        journal.AddEntry(entry);

        journal.Archive();

        Assert.Multiple(() =>
        {
            Assert.That(journal.IsArchived, Is.True);
            Assert.That(journal.Entries, Is.EqualTo(new[] { entry }));
            Assert.Throws<InvalidOperationException>(() => journal.Rename("Changed"));
            Assert.Throws<InvalidOperationException>(() => journal.AddEntry(
                CreateItem(
                    BrainItemKind.JournalEntry,
                    entryDate: new DateOnly(2026, 7, 25))));
            Assert.Throws<InvalidOperationException>(journal.Archive);
        });

        journal.Restore();
        journal.Rename("Changed");

        Assert.Multiple(() =>
        {
            Assert.That(journal.IsArchived, Is.False);
            Assert.That(journal.Title, Is.EqualTo("Changed"));
            Assert.Throws<InvalidOperationException>(journal.Restore);
        });
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

    [TestCase(CaptureSourceType.Article, "https://example.com/article")]
    [TestCase(CaptureSourceType.Video, "https://example.com/video")]
    [TestCase(CaptureSourceType.Course, "https://example.com/course")]
    [TestCase(CaptureSourceType.Podcast, "https://example.com/podcast")]
    [TestCase(CaptureSourceType.Page, "https://example.com/page")]
    [TestCase(CaptureSourceType.File, "file:///C:/captures/source.pdf")]
    public void KnowledgeCapture_RetainsSourceProvenance(
        CaptureSourceType sourceType,
        string sourceUrl)
    {
        var reminderAt = CreatedAt.AddDays(1);
        var capture = CreateCapture(
            sourceType: sourceType,
            sourceUri: new Uri(sourceUrl),
            reminderAt: reminderAt);

        Assert.Multiple(() =>
        {
            Assert.That(capture.Kind, Is.EqualTo(BrainItemKind.KnowledgeCapture));
            Assert.That(capture.CaptureSourceType, Is.EqualTo(sourceType));
            Assert.That(capture.SourceUri, Is.EqualTo(new Uri(sourceUrl)));
            Assert.That(capture.SourceCitation, Is.EqualTo("Example citation"));
            Assert.That(capture.ReminderAt, Is.EqualTo(reminderAt));
            Assert.That(
                capture.CaptureProcessingState,
                Is.EqualTo(CaptureProcessingState.Captured));
            Assert.That(capture.NoteKind, Is.Null);
        });
    }

    [Test]
    public void KnowledgeCapture_AdvancesToDistilledOrReferenced()
    {
        var distilled = CreateCapture();
        var referenced = CreateCapture();

        distilled.StartConsuming();
        distilled.MarkDistilled();
        referenced.StartConsuming();
        referenced.MarkReferenced();

        Assert.Multiple(() =>
        {
            Assert.That(
                distilled.CaptureProcessingState,
                Is.EqualTo(CaptureProcessingState.Distilled));
            Assert.That(
                referenced.CaptureProcessingState,
                Is.EqualTo(CaptureProcessingState.Referenced));
        });
    }

    [Test]
    public void KnowledgeCapture_InvalidTransitionsFailPredictably()
    {
        var capture = CreateCapture();
        var note = CreateItem(BrainItemKind.Note, noteKind: NoteKind.General);

        Assert.Multiple(() =>
        {
            Assert.Throws<InvalidOperationException>(capture.MarkDistilled);
            Assert.Throws<InvalidOperationException>(capture.MarkReferenced);
            Assert.Throws<InvalidOperationException>(note.StartConsuming);
        });

        capture.StartConsuming();

        Assert.Throws<InvalidOperationException>(capture.StartConsuming);

        capture.MarkDistilled();

        Assert.Multiple(() =>
        {
            Assert.Throws<InvalidOperationException>(capture.MarkDistilled);
            Assert.Throws<InvalidOperationException>(capture.MarkReferenced);
        });

        capture.Archive();

        Assert.Throws<InvalidOperationException>(capture.StartConsuming);
    }

    [Test]
    public void KnowledgeCapture_InvalidSourceMetadataFailsPredictably()
    {
        Assert.Multiple(() =>
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => CreateItem(
                    BrainItemKind.KnowledgeCapture,
                    sourceUri: new Uri("https://example.com"),
                    sourceCitation: "Citation",
                    captureProcessingState: CaptureProcessingState.Captured));
            Assert.Throws<ArgumentException>(
                () => CreateItem(
                    BrainItemKind.KnowledgeCapture,
                    captureSourceType: CaptureSourceType.Article,
                    sourceCitation: "Citation",
                    captureProcessingState: CaptureProcessingState.Captured));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => CreateCapture(sourceType: (CaptureSourceType)99));
            Assert.Throws<ArgumentException>(
                () => CreateCapture(sourceUri: new Uri("relative", UriKind.Relative)));
            Assert.Throws<ArgumentException>(
                () => CreateCapture(sourceUri: new Uri("ftp://example.com/source")));
            Assert.Throws<ArgumentException>(
                () => CreateCapture(
                    sourceType: CaptureSourceType.File,
                    sourceUri: new Uri("https://example.com/source.pdf")));
            Assert.Throws<ArgumentException>(
                () => CreateCapture(sourceCitation: " "));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => CreateCapture(reminderAt: CreatedAt.AddTicks(-1)));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => CreateCapture(
                    processingState: (CaptureProcessingState)99));
            Assert.Throws<ArgumentException>(
                () => CreateItem(
                    BrainItemKind.Note,
                    noteKind: NoteKind.General,
                    captureSourceType: CaptureSourceType.Article,
                    sourceUri: new Uri("https://example.com")));
        });
    }

    [Test]
    public void KnowledgeCapture_LinksDerivedItemsAndPreservesThemWhenArchived()
    {
        var derivedItemId = SecondBrainItemId.New();
        var capture = CreateCapture(derivedItemLinks: new[] { derivedItemId });
        var anotherDerivedItemId = SecondBrainItemId.New();

        capture.AddDerivedItemLink(anotherDerivedItemId);
        capture.Archive();

        Assert.That(
            capture.DerivedItemLinks,
            Is.EqualTo(new[] { derivedItemId, anotherDerivedItemId }));
        Assert.Multiple(() =>
        {
            Assert.That(capture.SourceCitation, Is.EqualTo("Example citation"));
            Assert.Throws<ArgumentException>(
                () => CreateCapture(
                    id: derivedItemId,
                    derivedItemLinks: new[] { derivedItemId }));
            Assert.Throws<InvalidOperationException>(
                () => CreateCapture(
                    derivedItemLinks: new[] { derivedItemId, derivedItemId }));
            Assert.Throws<InvalidOperationException>(
                () => capture.AddDerivedItemLink(SecondBrainItemId.New()));
        });
    }

    [Test]
    public void ResourceArtifact_RetainsFormatReviewDateAndOptionalManyToManyProvenance()
    {
        var reviewDate = new DateOnly(2026, 8, 24);
        var capture = CreateCapture();
        var note = CreateItem(BrainItemKind.Note, noteKind: NoteKind.General);
        var sourceResource = CreateResource(ResourceArtifactKind.Guide);
        var fromScratch = CreateResource(ResourceArtifactKind.Checklist);
        var derived = CreateResource(
            ResourceArtifactKind.CheatSheet,
            reviewDate: reviewDate,
            provenanceSources: new[] { capture, note, sourceResource });
        var anotherDerived = CreateResource(
            ResourceArtifactKind.Template,
            provenanceSources: new[] { capture });

        Assert.Multiple(() =>
        {
            Assert.That(
                fromScratch.ProvenanceSourceLinks,
                Is.Empty);
            Assert.That(
                derived.ResourceArtifactKind,
                Is.EqualTo(ResourceArtifactKind.CheatSheet));
            Assert.That(
                derived.ResourceFreshness,
                Is.EqualTo(ResourceFreshness.Draft));
            Assert.That(derived.ReviewDate, Is.EqualTo(reviewDate));
            Assert.That(
                derived.ProvenanceSourceLinks,
                Is.EqualTo(new[] { capture.Id, note.Id, sourceResource.Id }));
            Assert.That(
                anotherDerived.ProvenanceSourceLinks,
                Is.EqualTo(new[] { capture.Id }));
        });
    }

    [Test]
    public void ResourceArtifact_TransitionsFreshnessIndependentlyFromArchive()
    {
        var resource = CreateResource(ResourceArtifactKind.Guide);
        var activeOutdated = CreateResource(ResourceArtifactKind.Template);

        Assert.Throws<InvalidOperationException>(resource.MarkResourceOutdated);

        resource.MarkResourceCurrent();
        resource.MarkResourceOutdated();
        resource.MarkResourceCurrent();
        resource.Archive();

        activeOutdated.MarkResourceCurrent();
        activeOutdated.MarkResourceOutdated();

        Assert.Multiple(() =>
        {
            Assert.That(resource.ResourceFreshness, Is.EqualTo(ResourceFreshness.Current));
            Assert.That(resource.IsArchived, Is.True);
            Assert.That(
                activeOutdated.ResourceFreshness,
                Is.EqualTo(ResourceFreshness.Outdated));
            Assert.That(activeOutdated.IsArchived, Is.False);
            Assert.Throws<InvalidOperationException>(resource.MarkResourceOutdated);
            Assert.Throws<InvalidOperationException>(resource.MarkResourceCurrent);
        });
    }

    [Test]
    public void ResourceArtifact_RejectsDuplicateAndCyclicProvenance()
    {
        var first = CreateResource(ResourceArtifactKind.Guide);
        var second = CreateResource(ResourceArtifactKind.Template);
        var third = CreateResource(ResourceArtifactKind.Checklist);
        var note = CreateItem(BrainItemKind.Note, noteKind: NoteKind.General);

        first.AddProvenanceSource(second);
        second.AddProvenanceSource(third);
        first.AddProvenanceSource(note);

        Assert.Multiple(() =>
        {
            Assert.Throws<ArgumentException>(() => first.AddProvenanceSource(first));
            Assert.Throws<InvalidOperationException>(
                () => first.AddProvenanceSource(note));
            Assert.Throws<InvalidOperationException>(
                () => third.AddProvenanceSource(first));
        });
    }

    [Test]
    public void ResourceArtifact_InvalidMetadataAndProvenanceFailPredictably()
    {
        var idea = CreateItem(
            BrainItemKind.Idea,
            ideaMaturity: IdeaMaturity.Captured);
        var resource = CreateResource(ResourceArtifactKind.Guide);

        Assert.Multiple(() =>
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => CreateItem(
                    BrainItemKind.ResourceArtifact,
                    resourceFreshness: ResourceFreshness.Draft));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => CreateResource((ResourceArtifactKind)99));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => CreateResource(
                    ResourceArtifactKind.Guide,
                    freshness: (ResourceFreshness)99));
            Assert.Throws<ArgumentException>(
                () => CreateResource(
                    ResourceArtifactKind.Guide,
                    noteKind: NoteKind.General));
            Assert.Throws<ArgumentException>(
                () => resource.AddProvenanceSource(idea));
            Assert.Throws<InvalidOperationException>(idea.MarkResourceCurrent);
        });
    }

    private static BrainItem CreateItem(
        BrainItemKind kind,
        SecondBrainItemId? id = null,
        PrimaryPlacement? placement = null,
        NoteKind? noteKind = null,
        IdeaMaturity? ideaMaturity = null,
        DateOnly? entryDate = null,
        IEnumerable<string>? tags = null,
        IEnumerable<SecondBrainItemId>? contextualLinks = null,
        CaptureSourceType? captureSourceType = null,
        Uri? sourceUri = null,
        string? sourceCitation = null,
        DateTimeOffset? reminderAt = null,
        CaptureProcessingState? captureProcessingState = null,
        IEnumerable<SecondBrainItemId>? derivedItemLinks = null,
        ResourceArtifactKind? resourceArtifactKind = null,
        ResourceFreshness? resourceFreshness = null,
        DateOnly? reviewDate = null,
        IEnumerable<BrainItem>? provenanceSources = null) =>
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
            contextualLinks,
            captureSourceType: captureSourceType,
            sourceUri: sourceUri,
            sourceCitation: sourceCitation,
            reminderAt: reminderAt,
            captureProcessingState: captureProcessingState,
            derivedItemLinks: derivedItemLinks,
            resourceArtifactKind: resourceArtifactKind,
            resourceFreshness: resourceFreshness,
            reviewDate: reviewDate,
            provenanceSources: provenanceSources);

    private static BrainItem CreateCapture(
        SecondBrainItemId? id = null,
        CaptureSourceType sourceType = CaptureSourceType.Article,
        Uri? sourceUri = null,
        string sourceCitation = " Example citation ",
        DateTimeOffset? reminderAt = null,
        CaptureProcessingState processingState = CaptureProcessingState.Captured,
        IEnumerable<SecondBrainItemId>? derivedItemLinks = null) =>
        CreateItem(
            BrainItemKind.KnowledgeCapture,
            id: id,
            captureSourceType: sourceType,
            sourceUri: sourceUri ?? new Uri("https://example.com/source"),
            sourceCitation: sourceCitation,
            reminderAt: reminderAt,
            captureProcessingState: processingState,
            derivedItemLinks: derivedItemLinks);

    private static BrainItem CreateResource(
        ResourceArtifactKind kind,
        ResourceFreshness freshness = ResourceFreshness.Draft,
        DateOnly? reviewDate = null,
        IEnumerable<BrainItem>? provenanceSources = null,
        NoteKind? noteKind = null) =>
        CreateItem(
            BrainItemKind.ResourceArtifact,
            noteKind: noteKind,
            resourceArtifactKind: kind,
            resourceFreshness: freshness,
            reviewDate: reviewDate,
            provenanceSources: provenanceSources);
}
