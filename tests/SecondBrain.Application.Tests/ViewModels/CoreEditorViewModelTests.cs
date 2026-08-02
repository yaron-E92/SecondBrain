using NUnit.Framework;
using SecondBrain.Application.Ports;
using SecondBrain.Application.UseCases;
using SecondBrain.Domain.Entities;
using SecondBrain.Domain.ValueObjects;
using SecondBrain.Presentation.ViewModels;

namespace SecondBrain.Application.Tests.ViewModels;

[TestFixture]
public sealed class CoreEditorViewModelTests
{
    private static readonly DateTimeOffset _now =
        new(2026, 8, 2, 12, 0, 0, TimeSpan.Zero);

    [TestCase(BrainItemKind.Note)]
    [TestCase(BrainItemKind.Idea)]
    [TestCase(BrainItemKind.KnowledgeCapture)]
    [TestCase(BrainItemKind.ResourceArtifact)]
    [TestCase(BrainItemKind.JournalEntry)]
    public async Task CreateAndLoad_RoundTripsEveryKind(BrainItemKind kind)
    {
        var area = new Area(AreaId.New(), new ParaContextName("Writing"));
        var journal = new Journal(SecondBrainItemId.New(), "Daily");
        var repository = new FakeRepository(
            EmptyState() with { Areas = [area], Journals = [journal] });
        var useCases = new CoreKnowledgeUseCases(repository);
        var editor = new CoreEditorViewModel(useCases, () => _now);
        editor.BeginCreate(kind, PrimaryPlacement.InArea(area.Id));
        editor.Title = $"{kind} title";
        editor.Content = $"{kind} content";
        ConfigureTypedFields(editor, kind, journal.Id);

        await editor.SaveCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(editor.HasError, Is.False);
            Assert.That(editor.IsDirty, Is.False);
            Assert.That(editor.IsNew, Is.False);
            Assert.That(repository.State.BrainItems, Has.Count.EqualTo(1));
        });

        var saved = repository.State.BrainItems.Single();
        AssertSavedTypedFields(saved, kind);
        if (kind == BrainItemKind.JournalEntry)
        {
            Assert.That(journal.Entries.Single().Id, Is.EqualTo(saved.Id));
        }

        var loaded = new CoreEditorViewModel(useCases, () => _now.AddHours(1));
        await loaded.LoadAsync(
            saved.Id,
            kind == BrainItemKind.JournalEntry ? journal.Id : null);

        Assert.Multiple(() =>
        {
            Assert.That(loaded.HasError, Is.False);
            Assert.That(loaded.IsDirty, Is.False);
            Assert.That(loaded.Kind, Is.EqualTo(kind));
            Assert.That(loaded.Title, Is.EqualTo(editor.Title));
            Assert.That(loaded.Content, Is.EqualTo(editor.Content));
            Assert.That(loaded.AreTypeFieldsEditable, Is.False);
        });
        AssertLoadedTypedFields(loaded, kind, journal.Id);

        loaded.Title = $"Edited {kind}";
        loaded.Content = $"Edited {kind} content";
        await loaded.SaveCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(loaded.HasError, Is.False);
            Assert.That(loaded.IsDirty, Is.False);
            Assert.That(saved.Title, Is.EqualTo($"Edited {kind}"));
            Assert.That(saved.Content, Is.EqualTo($"Edited {kind} content"));
        });
    }

    [Test]
    public async Task Save_UsesLegalLifecycleTransitions()
    {
        var area = new Area(AreaId.New(), new ParaContextName("Writing"));
        var idea = CreateIdea(area.Id);
        var capture = CreateCapture(area.Id);
        var resource = CreateResource(area.Id);
        var repository = new FakeRepository(
            EmptyState() with
            {
                Areas = [area],
                BrainItems = [idea, capture, resource],
            });
        var useCases = new CoreKnowledgeUseCases(repository);

        var ideaEditor = new CoreEditorViewModel(useCases, () => _now.AddHours(1));
        await ideaEditor.LoadAsync(idea.Id);
        ideaEditor.Idea.Maturity = IdeaMaturity.Actionable;
        await ideaEditor.SaveCommand.ExecuteAsync(null);

        var captureEditor = new CoreEditorViewModel(
            useCases,
            () => _now.AddHours(1));
        await captureEditor.LoadAsync(capture.Id);
        captureEditor.Capture.ProcessingState = CaptureProcessingState.Referenced;
        await captureEditor.SaveCommand.ExecuteAsync(null);

        var resourceEditor = new CoreEditorViewModel(
            useCases,
            () => _now.AddHours(1));
        await resourceEditor.LoadAsync(resource.Id);
        resourceEditor.Resource.Freshness = ResourceFreshness.Outdated;
        await resourceEditor.SaveCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(ideaEditor.HasError, Is.False);
            Assert.That(idea.IdeaMaturity, Is.EqualTo(IdeaMaturity.Actionable));
            Assert.That(captureEditor.HasError, Is.False);
            Assert.That(
                capture.CaptureProcessingState,
                Is.EqualTo(CaptureProcessingState.Referenced));
            Assert.That(resourceEditor.HasError, Is.False);
            Assert.That(
                resource.ResourceFreshness,
                Is.EqualTo(ResourceFreshness.Outdated));
        });
    }

    [Test]
    public async Task JournalEntry_RequiresJournalAndOccurrenceDate()
    {
        var area = new Area(AreaId.New(), new ParaContextName("Writing"));
        var journal = new Journal(SecondBrainItemId.New(), "Daily");
        var repository = new FakeRepository(
            EmptyState() with { Areas = [area], Journals = [journal] });
        var editor = new CoreEditorViewModel(
            new CoreKnowledgeUseCases(repository),
            () => _now);
        editor.BeginCreate(
            BrainItemKind.JournalEntry,
            PrimaryPlacement.InArea(area.Id));
        editor.Title = "Unfinished entry";
        editor.Content = "Editable draft";

        await editor.SaveCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(editor.ErrorMessage, Is.EqualTo("Journal is required."));
            Assert.That(editor.Title, Is.EqualTo("Unfinished entry"));
            Assert.That(editor.Content, Is.EqualTo("Editable draft"));
            Assert.That(editor.IsDirty, Is.True);
        });

        editor.JournalEntry.JournalId = journal.Id;
        await editor.SaveCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(
                editor.ErrorMessage,
                Is.EqualTo("Occurrence date is required."));
            Assert.That(repository.State.BrainItems, Is.Empty);
        });
    }

    [Test]
    public async Task InvalidInput_RemainsEditableWithFeedback()
    {
        var area = new Area(AreaId.New(), new ParaContextName("Writing"));
        var repository = new FakeRepository(
            EmptyState() with { Areas = [area] });
        var editor = new CoreEditorViewModel(
            new CoreKnowledgeUseCases(repository),
            () => _now);
        editor.BeginCreate(BrainItemKind.Note, PrimaryPlacement.InArea(area.Id));
        editor.Title = " ";
        editor.Content = "Keep this draft";

        await editor.SaveCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(editor.ErrorMessage, Is.EqualTo("Title is required."));
            Assert.That(editor.Title, Is.EqualTo(" "));
            Assert.That(editor.Content, Is.EqualTo("Keep this draft"));
            Assert.That(editor.IsDirty, Is.True);
            Assert.That(repository.SaveCount, Is.Zero);
        });
    }

    [Test]
    public void Cancel_RestoresTheOriginalValuesAndClearsDirtyState()
    {
        var area = new Area(AreaId.New(), new ParaContextName("Writing"));
        var editor = new CoreEditorViewModel(
            new CoreKnowledgeUseCases(new FakeRepository(EmptyState())),
            () => _now);
        editor.BeginCreate(BrainItemKind.Note, PrimaryPlacement.InArea(area.Id));
        editor.Title = "Changed";
        editor.Content = "Changed content";

        editor.CancelCommand.Execute(null);

        Assert.Multiple(() =>
        {
            Assert.That(editor.Title, Is.Empty);
            Assert.That(editor.Content, Is.Empty);
            Assert.That(editor.IsDirty, Is.False);
            Assert.That(editor.HasError, Is.False);
        });
    }

    [Test]
    public async Task SaveFailure_IsShownWithoutDiscardingInput()
    {
        var area = new Area(AreaId.New(), new ParaContextName("Writing"));
        var repository = new FakeRepository(
            EmptyState() with { Areas = [area] },
            saveException: new InvalidOperationException("Disk unavailable"));
        var editor = new CoreEditorViewModel(
            new CoreKnowledgeUseCases(repository),
            () => _now);
        editor.BeginCreate(BrainItemKind.Note, PrimaryPlacement.InArea(area.Id));
        editor.Title = "Keep me";
        editor.Content = "Unsaved content";

        await editor.SaveCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(editor.ErrorMessage, Does.Contain("Disk unavailable"));
            Assert.That(editor.Title, Is.EqualTo("Keep me"));
            Assert.That(editor.Content, Is.EqualTo("Unsaved content"));
            Assert.That(editor.IsDirty, Is.True);
            Assert.That(editor.IsBusy, Is.False);
        });
    }

    private static void ConfigureTypedFields(
        CoreEditorViewModel editor,
        BrainItemKind kind,
        SecondBrainItemId journalId)
    {
        switch (kind)
        {
            case BrainItemKind.Note:
                editor.Note.Kind = NoteKind.General;
                break;
            case BrainItemKind.Idea:
                editor.Idea.Maturity = IdeaMaturity.Actionable;
                break;
            case BrainItemKind.KnowledgeCapture:
                editor.Capture.SourceType = CaptureSourceType.Article;
                editor.Capture.SourceUrl = "https://example.com/source";
                editor.Capture.SourceCitation = "Example source";
                editor.Capture.ReminderAt = _now.AddDays(1);
                editor.Capture.ProcessingState = CaptureProcessingState.Referenced;
                break;
            case BrainItemKind.ResourceArtifact:
                editor.Resource.ArtifactKind = ResourceArtifactKind.Template;
                editor.Resource.Freshness = ResourceFreshness.Outdated;
                editor.Resource.ReviewDate = new DateOnly(2026, 9, 1);
                break;
            case BrainItemKind.JournalEntry:
                editor.JournalEntry.JournalId = journalId;
                editor.JournalEntry.OccurrenceDate = new DateOnly(2026, 8, 2);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(kind));
        }
    }

    private static void AssertSavedTypedFields(
        BrainItem item,
        BrainItemKind kind)
    {
        switch (kind)
        {
            case BrainItemKind.Note:
                Assert.That(item.NoteKind, Is.EqualTo(NoteKind.General));
                break;
            case BrainItemKind.Idea:
                Assert.That(item.IdeaMaturity, Is.EqualTo(IdeaMaturity.Actionable));
                break;
            case BrainItemKind.KnowledgeCapture:
                Assert.Multiple(() =>
                {
                    Assert.That(
                        item.CaptureSourceType,
                        Is.EqualTo(CaptureSourceType.Article));
                    Assert.That(
                        item.CaptureProcessingState,
                        Is.EqualTo(CaptureProcessingState.Referenced));
                    Assert.That(item.SourceCitation, Is.EqualTo("Example source"));
                });
                break;
            case BrainItemKind.ResourceArtifact:
                Assert.Multiple(() =>
                {
                    Assert.That(
                        item.ResourceArtifactKind,
                        Is.EqualTo(ResourceArtifactKind.Template));
                    Assert.That(
                        item.ResourceFreshness,
                        Is.EqualTo(ResourceFreshness.Outdated));
                    Assert.That(item.ReviewDate, Is.EqualTo(new DateOnly(2026, 9, 1)));
                });
                break;
            case BrainItemKind.JournalEntry:
                Assert.That(item.EntryDate, Is.EqualTo(new DateOnly(2026, 8, 2)));
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(kind));
        }
    }

    private static void AssertLoadedTypedFields(
        CoreEditorViewModel editor,
        BrainItemKind kind,
        SecondBrainItemId journalId)
    {
        switch (kind)
        {
            case BrainItemKind.Note:
                Assert.That(editor.Note.Kind, Is.EqualTo(NoteKind.General));
                break;
            case BrainItemKind.Idea:
                Assert.That(editor.Idea.Maturity, Is.EqualTo(IdeaMaturity.Actionable));
                break;
            case BrainItemKind.KnowledgeCapture:
                Assert.Multiple(() =>
                {
                    Assert.That(
                        editor.Capture.SourceType,
                        Is.EqualTo(CaptureSourceType.Article));
                    Assert.That(
                        editor.Capture.ProcessingState,
                        Is.EqualTo(CaptureProcessingState.Referenced));
                    Assert.That(
                        editor.Capture.SourceUrl,
                        Is.EqualTo("https://example.com/source"));
                });
                break;
            case BrainItemKind.ResourceArtifact:
                Assert.Multiple(() =>
                {
                    Assert.That(
                        editor.Resource.ArtifactKind,
                        Is.EqualTo(ResourceArtifactKind.Template));
                    Assert.That(
                        editor.Resource.Freshness,
                        Is.EqualTo(ResourceFreshness.Outdated));
                });
                break;
            case BrainItemKind.JournalEntry:
                Assert.Multiple(() =>
                {
                    Assert.That(editor.JournalEntry.JournalId, Is.EqualTo(journalId));
                    Assert.That(
                        editor.JournalEntry.OccurrenceDate,
                        Is.EqualTo(new DateOnly(2026, 8, 2)));
                });
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(kind));
        }
    }

    private static BrainItem CreateIdea(AreaId areaId) =>
        new(
            SecondBrainItemId.New(),
            BrainItemKind.Idea,
            "Idea",
            "Idea content",
            PrimaryPlacement.InArea(areaId),
            _now,
            ideaMaturity: IdeaMaturity.Captured);

    private static BrainItem CreateCapture(AreaId areaId) =>
        new(
            SecondBrainItemId.New(),
            BrainItemKind.KnowledgeCapture,
            "Capture",
            "Capture content",
            PrimaryPlacement.InArea(areaId),
            _now,
            captureSourceType: CaptureSourceType.Article,
            sourceUri: new Uri("https://example.com/capture"),
            sourceCitation: "Capture source",
            captureProcessingState: CaptureProcessingState.Captured);

    private static BrainItem CreateResource(AreaId areaId) =>
        new(
            SecondBrainItemId.New(),
            BrainItemKind.ResourceArtifact,
            "Resource",
            "Resource content",
            PrimaryPlacement.InArea(areaId),
            _now,
            resourceArtifactKind: ResourceArtifactKind.Guide,
            resourceFreshness: ResourceFreshness.Draft);

    private static CoreKnowledgeState EmptyState() =>
        new([], [], [], [], [], []);

    private sealed class FakeRepository(
        CoreKnowledgeState state,
        Exception? loadException = null,
        Exception? saveException = null) : ICoreKnowledgeRepository
    {
        public CoreKnowledgeState State { get; private set; } = state;

        public int SaveCount { get; private set; }

        public Task<CoreKnowledgeState> LoadStateAsync(
            CancellationToken cancellationToken = default) =>
            loadException is null
                ? Task.FromResult(State)
                : Task.FromException<CoreKnowledgeState>(loadException);

        public Task SaveStateAsync(
            CoreKnowledgeState newState,
            CancellationToken cancellationToken = default)
        {
            if (saveException is not null)
            {
                return Task.FromException(saveException);
            }

            State = newState;
            SaveCount++;
            return Task.CompletedTask;
        }
    }
}
