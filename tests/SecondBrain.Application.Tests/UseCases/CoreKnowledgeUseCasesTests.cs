using NUnit.Framework;
using SecondBrain.Application.Ports;
using SecondBrain.Application.UseCases;
using SecondBrain.Domain.Entities;
using SecondBrain.Domain.ValueObjects;

namespace SecondBrain.Application.Tests.UseCases;

[TestFixture]
public sealed class CoreKnowledgeUseCasesTests
{
    private static readonly DateTimeOffset CreatedAt =
        new(2026, 7, 25, 8, 0, 0, TimeSpan.Zero);

    [Test]
    public async Task CreateAndRead_CoversEveryCoreContentAndContextType()
    {
        var repository = new FakeCoreKnowledgeRepository(EmptyState());
        var useCases = new CoreKnowledgeUseCases(repository);
        var project = new Project(
            ProjectId.New(),
            new ParaContextName("Delivery"),
            "Ship the milestone");
        var area = new Area(AreaId.New(), new ParaContextName("Engineering"));
        var topic = new ResourceTopic(
            ResourceTopicId.New(),
            new ParaContextName("Architecture"));

        var projectResult = await useCases.CreateProjectAsync(
            new CreateProjectCommand(project));
        var areaResult = await useCases.CreateAreaAsync(new CreateAreaCommand(area));
        var topicResult = await useCases.CreateResourceTopicAsync(
            new CreateResourceTopicCommand(topic));

        var items = CreateEveryBrainItemKind(area.Id);
        var itemResults = new List<CoreOperationResult<BrainItem>>();
        foreach (var item in items)
        {
            itemResults.Add(
                await useCases.CreateBrainItemAsync(new CreateBrainItemCommand(item)));
        }

        var journalEntry = items.Single(
            item => item.Kind == BrainItemKind.JournalEntry);
        var journal = new Journal(SecondBrainItemId.New(), "Daily");
        var journalResult = await useCases.CreateJournalAsync(
            new CreateJournalCommand(journal));
        var entryResult = await useCases.AddJournalEntryAsync(
            new AddJournalEntryCommand(journal.Id, journalEntry.Id));

        var readProject = await useCases.GetProjectAsync(new GetProjectQuery(project.Id));
        var readArea = await useCases.GetAreaAsync(new GetAreaQuery(area.Id));
        var readTopic = await useCases.GetResourceTopicAsync(
            new GetResourceTopicQuery(topic.Id));
        var readJournal = await useCases.GetJournalAsync(
            new GetJournalQuery(journal.Id));

        Assert.Multiple(() =>
        {
            Assert.That(projectResult.IsSuccess, Is.True);
            Assert.That(areaResult.IsSuccess, Is.True);
            Assert.That(topicResult.IsSuccess, Is.True);
            Assert.That(itemResults.All(result => result.IsSuccess), Is.True);
            Assert.That(journalResult.IsSuccess, Is.True);
            Assert.That(entryResult.IsSuccess, Is.True);
            Assert.That(readProject.Value, Is.SameAs(project));
            Assert.That(readArea.Value, Is.SameAs(area));
            Assert.That(readTopic.Value, Is.SameAs(topic));
            Assert.That(readJournal.Value!.Entries.Single(), Is.SameAs(journalEntry));
            Assert.That(
                repository.State.BrainItems.Select(item => item.Kind),
                Is.EquivalentTo(Enum.GetValues<BrainItemKind>()));
        });
    }

    [Test]
    public async Task UpdateMoveArchiveRestoreAndLifecycle_AreExplicitAndPersisted()
    {
        var firstArea = new Area(AreaId.New(), new ParaContextName("First"));
        var secondArea = new Area(AreaId.New(), new ParaContextName("Second"));
        var project = new Project(
            ProjectId.New(),
            new ParaContextName("Initial"),
            "Initial outcome");
        var idea = CreateIdea(firstArea.Id);
        var repository = new FakeCoreKnowledgeRepository(
            EmptyState() with
            {
                Areas = [firstArea, secondArea],
                Projects = [project],
                BrainItems = [idea],
            });
        var useCases = new CoreKnowledgeUseCases(repository);

        var update = await useCases.UpdateBrainItemAsync(
            new UpdateBrainItemCommand(
                idea.Id,
                "Updated title",
                "Updated content",
                CreatedAt.AddMinutes(1)));
        var move = await useCases.MoveBrainItemAsync(
            new MoveBrainItemCommand(
                idea.Id,
                PrimaryPlacement.InArea(secondArea.Id),
                CreatedAt.AddMinutes(2)));
        var sharpen = await useCases.TransitionBrainItemAsync(
            new TransitionBrainItemCommand(
                idea.Id,
                BrainItemLifecycleTransition.SharpenIdea));
        var archive = await useCases.ArchiveBrainItemAsync(
            new ArchiveBrainItemCommand(idea.Id));
        var restore = await useCases.RestoreBrainItemAsync(
            new RestoreBrainItemCommand(idea.Id));
        var updateProject = await useCases.UpdateProjectAsync(
            new UpdateProjectCommand(
                project.Id,
                new ParaContextName("Renamed"),
                "Updated outcome",
                ProjectPriority.High,
                new DateOnly(2026, 8, 1)));
        var activate = await useCases.TransitionProjectAsync(
            new TransitionProjectCommand(
                project.Id,
                ProjectLifecycleTransition.Activate));

        Assert.Multiple(() =>
        {
            Assert.That(update.IsSuccess, Is.True);
            Assert.That(move.IsSuccess, Is.True);
            Assert.That(sharpen.IsSuccess, Is.True);
            Assert.That(archive.IsSuccess, Is.True);
            Assert.That(restore.IsSuccess, Is.True);
            Assert.That(updateProject.IsSuccess, Is.True);
            Assert.That(activate.IsSuccess, Is.True);
            Assert.That(idea.Title, Is.EqualTo("Updated title"));
            Assert.That(idea.Content, Is.EqualTo("Updated content"));
            Assert.That(idea.PrimaryPlacement, Is.EqualTo(
                PrimaryPlacement.InArea(secondArea.Id)));
            Assert.That(idea.IdeaMaturity, Is.EqualTo(IdeaMaturity.Sharpened));
            Assert.That(idea.IsArchived, Is.False);
            Assert.That(project.Name.Value, Is.EqualTo("Renamed"));
            Assert.That(project.Status, Is.EqualTo(ProjectStatus.Active));
            Assert.That(repository.SaveCount, Is.EqualTo(7));
        });
    }

    [Test]
    public async Task ContextArchiveRestoreTagMoveAndJournalRename_ArePersisted()
    {
        var project = new Project(
            ProjectId.New(),
            new ParaContextName("Project"),
            "Outcome");
        var area = new Area(AreaId.New(), new ParaContextName("Area"));
        var topic = new ResourceTopic(
            ResourceTopicId.New(),
            new ParaContextName("Topic"));
        var rootTag = new Tag(TagId.New(), "Root");
        var childTag = new Tag(TagId.New(), "Child");
        var journal = new Journal(SecondBrainItemId.New(), "Before");
        var repository = new FakeCoreKnowledgeRepository(
            EmptyState() with
            {
                Projects = [project],
                Areas = [area],
                ResourceTopics = [topic],
                Tags = [rootTag, childTag],
                Journals = [journal],
            });
        var useCases = new CoreKnowledgeUseCases(repository);

        var results = new[]
        {
            (await useCases.ArchiveProjectAsync(
                new ArchiveProjectCommand(project.Id))).IsSuccess,
            (await useCases.RestoreProjectAsync(
                new RestoreProjectCommand(project.Id))).IsSuccess,
            (await useCases.ArchiveAreaAsync(
                new ArchiveAreaCommand(area.Id))).IsSuccess,
            (await useCases.RestoreAreaAsync(
                new RestoreAreaCommand(area.Id))).IsSuccess,
            (await useCases.ArchiveResourceTopicAsync(
                new ArchiveResourceTopicCommand(topic.Id))).IsSuccess,
            (await useCases.RestoreResourceTopicAsync(
                new RestoreResourceTopicCommand(topic.Id))).IsSuccess,
            (await useCases.MoveTagAsync(
                new MoveTagCommand(childTag.Id, rootTag.Id))).IsSuccess,
            (await useCases.RenameJournalAsync(
                new RenameJournalCommand(journal.Id, "After"))).IsSuccess,
            (await useCases.ArchiveJournalAsync(
                new ArchiveJournalCommand(journal.Id))).IsSuccess,
            (await useCases.RestoreJournalAsync(
                new RestoreJournalCommand(journal.Id))).IsSuccess,
        };

        Assert.Multiple(() =>
        {
            Assert.That(
                results.All(result => result),
                Is.True);
            Assert.That(project.IsArchived, Is.False);
            Assert.That(area.IsArchived, Is.False);
            Assert.That(topic.IsArchived, Is.False);
            Assert.That(childTag.Parent, Is.SameAs(rootTag));
            Assert.That(journal.Title, Is.EqualTo("After"));
            Assert.That(journal.IsArchived, Is.False);
            Assert.That(repository.SaveCount, Is.EqualTo(10));
        });
    }

    [Test]
    public async Task DuplicateContextNames_ReturnConflictsWithoutSavingOrMutating()
    {
        var firstProject = new Project(
            ProjectId.New(),
            new ParaContextName("Launch"),
            "First outcome");
        var firstArea = new Area(AreaId.New(), new ParaContextName("Writing"));
        var secondArea = new Area(AreaId.New(), new ParaContextName("Planning"));
        var firstTopic = new ResourceTopic(
            ResourceTopicId.New(),
            new ParaContextName("Architecture"));
        var repository = new FakeCoreKnowledgeRepository(
            EmptyState() with
            {
                Projects = [firstProject],
                Areas = [firstArea, secondArea],
                ResourceTopics = [firstTopic],
            });
        var useCases = new CoreKnowledgeUseCases(repository);

        var project = await useCases.CreateProjectAsync(
            new CreateProjectCommand(new Project(
                ProjectId.New(),
                new ParaContextName("launch"),
                "Duplicate")));
        var area = await useCases.CreateAreaAsync(
            new CreateAreaCommand(new Area(
                AreaId.New(),
                new ParaContextName("WRITING"))));
        var topic = await useCases.CreateResourceTopicAsync(
            new CreateResourceTopicCommand(new ResourceTopic(
                ResourceTopicId.New(),
                new ParaContextName("architecture"))));
        var update = await useCases.UpdateAreaAsync(
            new UpdateAreaCommand(
                secondArea.Id,
                new ParaContextName("writing")));

        Assert.Multiple(() =>
        {
            Assert.That(project.Error!.Code, Is.EqualTo(CoreOperationErrorCode.Conflict));
            Assert.That(area.Error!.Code, Is.EqualTo(CoreOperationErrorCode.Conflict));
            Assert.That(topic.Error!.Code, Is.EqualTo(CoreOperationErrorCode.Conflict));
            Assert.That(update.Error!.Code, Is.EqualTo(CoreOperationErrorCode.Conflict));
            Assert.That(project.Error.Message, Does.Contain("named 'launch'"));
            Assert.That(secondArea.Name.Value, Is.EqualTo("Planning"));
            Assert.That(repository.SaveCount, Is.Zero);
        });
    }

    [Test]
    public async Task MissingReferencesAndInvalidTransitions_ReturnTypedFailuresWithoutSaving()
    {
        var note = CreateNote(AreaId.New());
        var repository = new FakeCoreKnowledgeRepository(
            EmptyState() with { BrainItems = [note] });
        var useCases = new CoreKnowledgeUseCases(repository);

        var create = await useCases.CreateBrainItemAsync(
            new CreateBrainItemCommand(CreateNote(AreaId.New())));
        var read = await useCases.GetAreaAsync(new GetAreaQuery(AreaId.New()));
        var transition = await useCases.TransitionBrainItemAsync(
            new TransitionBrainItemCommand(
                note.Id,
                BrainItemLifecycleTransition.SharpenIdea));

        Assert.Multiple(() =>
        {
            Assert.That(create.Error!.Code, Is.EqualTo(CoreOperationErrorCode.NotFound));
            Assert.That(create.Error.Message, Does.Contain("placement"));
            Assert.That(read.Error!.Code, Is.EqualTo(CoreOperationErrorCode.NotFound));
            Assert.That(transition.Error!.Code, Is.EqualTo(
                CoreOperationErrorCode.Conflict));
            Assert.That(transition.Error.Message, Does.Contain("ideas"));
            Assert.That(repository.SaveCount, Is.Zero);
        });
    }

    [Test]
    public void Cancellation_IsHonoredBeforeLoadOrSave()
    {
        var repository = new FakeCoreKnowledgeRepository(EmptyState());
        var useCases = new CoreKnowledgeUseCases(repository);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Assert.ThrowsAsync<OperationCanceledException>(
            async () => await useCases.GetAreaAsync(
                new GetAreaQuery(AreaId.New()),
                cancellation.Token));
        Assert.ThrowsAsync<OperationCanceledException>(
            async () => await useCases.CreateAreaAsync(
                new CreateAreaCommand(
                    new Area(AreaId.New(), new ParaContextName("Area"))),
                cancellation.Token));

        Assert.Multiple(() =>
        {
            Assert.That(repository.LoadCount, Is.Zero);
            Assert.That(repository.SaveCount, Is.Zero);
        });
    }

    [Test]
    public async Task ArchivedOrMissingMoveTarget_ReturnsTypedFailureWithoutSaving()
    {
        var currentArea = new Area(AreaId.New(), new ParaContextName("Current"));
        var archivedArea = new Area(AreaId.New(), new ParaContextName("Archived"));
        archivedArea.Archive();
        var note = CreateNote(currentArea.Id);
        var repository = new FakeCoreKnowledgeRepository(
            EmptyState() with
            {
                Areas = [currentArea, archivedArea],
                BrainItems = [note],
            });
        var useCases = new CoreKnowledgeUseCases(repository);

        var archived = await useCases.MoveBrainItemAsync(
            new MoveBrainItemCommand(
                note.Id,
                PrimaryPlacement.InArea(archivedArea.Id),
                CreatedAt.AddMinutes(1)));
        var missing = await useCases.MoveBrainItemAsync(
            new MoveBrainItemCommand(
                note.Id,
                PrimaryPlacement.InArea(AreaId.New()),
                CreatedAt.AddMinutes(1)));

        Assert.Multiple(() =>
        {
            Assert.That(archived.Error!.Code, Is.EqualTo(
                CoreOperationErrorCode.Conflict));
            Assert.That(missing.Error!.Code, Is.EqualTo(
                CoreOperationErrorCode.NotFound));
            Assert.That(repository.SaveCount, Is.Zero);
        });
    }

    [TestCase(BrainItemKind.Note)]
    [TestCase(BrainItemKind.ResourceArtifact)]
    public async Task DeriveBrainItem_LinksEveryCaptureInOneSave(
        BrainItemKind derivedKind)
    {
        var area = new Area(AreaId.New(), new ParaContextName("Writing"));
        var first = CreateCapture(area.Id, "First", "First excerpt");
        var second = CreateCapture(area.Id, "Second", "Second excerpt");
        var derived = derivedKind == BrainItemKind.Note
            ? new BrainItem(
                SecondBrainItemId.New(),
                BrainItemKind.Note,
                "Derived note",
                "Source: First citation\nFirst excerpt\n\n" +
                "Source: Second citation\nSecond excerpt",
                PrimaryPlacement.InArea(area.Id),
                CreatedAt,
                noteKind: NoteKind.General)
            : new BrainItem(
                SecondBrainItemId.New(),
                BrainItemKind.ResourceArtifact,
                "Derived resource",
                "Source: First citation\nFirst excerpt\n\n" +
                "Source: Second citation\nSecond excerpt",
                PrimaryPlacement.InArea(area.Id),
                CreatedAt,
                resourceArtifactKind: ResourceArtifactKind.Guide,
                resourceFreshness: ResourceFreshness.Draft);
        var repository = new FakeCoreKnowledgeRepository(
            EmptyState() with
            {
                Areas = [area],
                BrainItems = [first, second],
            });

        var result = await new CoreKnowledgeUseCases(repository)
            .DeriveBrainItemAsync(
                new DeriveBrainItemCommand(
                    derived,
                    [first.Id, second.Id],
                    MarkSourcesReferenced: true));

        var savedSources = repository.State.BrainItems
            .Where(item => item.Kind == BrainItemKind.KnowledgeCapture)
            .ToArray();
        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(repository.SaveCount, Is.EqualTo(1));
            Assert.That(repository.State.BrainItems, Has.Count.EqualTo(3));
            Assert.That(
                savedSources.All(source => source.DerivedItemLinks.Contains(derived.Id)),
                Is.True);
            Assert.That(
                savedSources.All(source =>
                    source.CaptureProcessingState == CaptureProcessingState.Referenced),
                Is.True);
            Assert.That(derived.Content, Does.Contain("First citation"));
            Assert.That(derived.Content, Does.Contain("Second citation"));
            Assert.That(
                derived.ProvenanceSourceLinks,
                derivedKind == BrainItemKind.ResourceArtifact
                    ? Is.EquivalentTo(new[] { first.Id, second.Id })
                    : Is.Empty);
        });
    }

    [Test]
    public async Task DeriveBrainItem_InvalidSourceCreatesNoItemOrLinks()
    {
        var area = new Area(AreaId.New(), new ParaContextName("Writing"));
        var source = CreateCapture(area.Id, "Source", "Excerpt");
        var derived = CreateNote(area.Id);
        var repository = new FakeCoreKnowledgeRepository(
            EmptyState() with { Areas = [area], BrainItems = [source] });

        var result = await new CoreKnowledgeUseCases(repository)
            .DeriveBrainItemAsync(
                new DeriveBrainItemCommand(
                    derived,
                    [source.Id, SecondBrainItemId.New()],
                    MarkSourcesReferenced: true));

        Assert.Multiple(() =>
        {
            Assert.That(result.Error!.Code, Is.EqualTo(CoreOperationErrorCode.NotFound));
            Assert.That(repository.SaveCount, Is.Zero);
            Assert.That(repository.State.BrainItems, Has.Count.EqualTo(1));
            Assert.That(source.DerivedItemLinks, Is.Empty);
            Assert.That(
                source.CaptureProcessingState,
                Is.EqualTo(CaptureProcessingState.Captured));
        });
    }

    private static CoreKnowledgeState EmptyState() =>
        new([], [], [], [], [], []);

    private static IReadOnlyList<BrainItem> CreateEveryBrainItemKind(AreaId areaId) =>
        [
            CreateNote(areaId),
            CreateIdea(areaId),
            new BrainItem(
                SecondBrainItemId.New(),
                BrainItemKind.JournalEntry,
                "Journal entry",
                "Content",
                PrimaryPlacement.InArea(areaId),
                CreatedAt,
                entryDate: new DateOnly(2026, 7, 25)),
            new BrainItem(
                SecondBrainItemId.New(),
                BrainItemKind.KnowledgeCapture,
                "Capture",
                "Content",
                PrimaryPlacement.InArea(areaId),
                CreatedAt,
                captureSourceType: CaptureSourceType.Article,
                sourceUri: new Uri("https://example.com/source"),
                sourceCitation: "Example source",
                captureProcessingState: CaptureProcessingState.Captured),
            new BrainItem(
                SecondBrainItemId.New(),
                BrainItemKind.ResourceArtifact,
                "Resource",
                "Content",
                PrimaryPlacement.InArea(areaId),
                CreatedAt,
                resourceArtifactKind: ResourceArtifactKind.Guide,
                resourceFreshness: ResourceFreshness.Draft),
        ];

    private static BrainItem CreateNote(AreaId areaId) =>
        new(
            SecondBrainItemId.New(),
            BrainItemKind.Note,
            "Note",
            "Content",
            PrimaryPlacement.InArea(areaId),
            CreatedAt,
            noteKind: NoteKind.General);

    private static BrainItem CreateIdea(AreaId areaId) =>
        new(
            SecondBrainItemId.New(),
            BrainItemKind.Idea,
            "Idea",
            "Content",
            PrimaryPlacement.InArea(areaId),
            CreatedAt,
            ideaMaturity: IdeaMaturity.Captured);

    private static BrainItem CreateCapture(
        AreaId areaId,
        string title,
        string content) =>
        new(
            SecondBrainItemId.New(),
            BrainItemKind.KnowledgeCapture,
            title,
            content,
            PrimaryPlacement.InArea(areaId),
            CreatedAt,
            captureSourceType: CaptureSourceType.Article,
            sourceUri: new Uri($"https://example.com/{title.ToLowerInvariant()}"),
            sourceCitation: $"{title} citation",
            captureProcessingState: CaptureProcessingState.Captured);

    private sealed class FakeCoreKnowledgeRepository(CoreKnowledgeState state)
        : ICoreKnowledgeRepository
    {
        public CoreKnowledgeState State { get; private set; } = state;

        public int LoadCount { get; private set; }

        public int SaveCount { get; private set; }

        public Task<CoreKnowledgeState> LoadStateAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LoadCount++;
            return Task.FromResult(State);
        }

        public Task SaveStateAsync(
            CoreKnowledgeState state,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            State = state;
            SaveCount++;
            return Task.CompletedTask;
        }
    }
}
