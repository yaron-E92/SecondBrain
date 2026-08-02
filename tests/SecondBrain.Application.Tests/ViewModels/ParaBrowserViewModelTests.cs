using NUnit.Framework;
using SecondBrain.Application.Ports;
using SecondBrain.Application.UseCases;
using SecondBrain.Domain.Entities;
using SecondBrain.Domain.ValueObjects;
using SecondBrain.Presentation.ViewModels;

namespace SecondBrain.Application.Tests.ViewModels;

[TestFixture]
public sealed class ParaBrowserViewModelTests
{
    private static readonly DateTimeOffset CreatedAt =
        new(2026, 8, 2, 12, 0, 0, TimeSpan.Zero);

    [Test]
    public async Task Load_ExposesActiveItemsForEveryParaContextAndArchive()
    {
        var project = new Project(
            ProjectId.New(),
            new ParaContextName("Launch"),
            "Ship the app");
        var area = new Area(AreaId.New(), new ParaContextName("Writing"));
        var inbox = new Area(AreaId.New(), new ParaContextName("Inbox"));
        var topic = new ResourceTopic(
            ResourceTopicId.New(),
            new ParaContextName("Architecture"));
        var archived = CreateNote("Archived", PrimaryPlacement.InArea(area.Id));
        archived.Archive();
        var repository = new FakeRepository(
            EmptyState() with
            {
                Projects = [project],
                Areas = [area, inbox],
                ResourceTopics = [topic],
                BrainItems =
                [
                    CreateNote("Project item", PrimaryPlacement.InProject(project.Id)),
                    CreateNote("Area item", PrimaryPlacement.InArea(area.Id)),
                    CreateNote("Inbox item", PrimaryPlacement.InArea(inbox.Id)),
                    CreateNote(
                        "Resource item",
                        PrimaryPlacement.InResourceTopic(topic.Id)),
                    archived,
                ],
            });
        var viewModel = CreateViewModel(repository);

        await viewModel.LoadCommand.ExecuteAsync(null);

        AssertContextItems(viewModel, "Launch", "Project item");
        AssertContextItems(viewModel, "Writing", "Area item");
        AssertContextItems(viewModel, "Architecture", "Resource item");
        AssertContextItems(viewModel, "Inbox", "Inbox item");
        AssertContextItems(viewModel, "Archive", "Archived");
        Assert.That(
            viewModel.Contexts.Select(context => context.Name),
            Is.EqualTo(new[]
            {
                "Launch",
                "Writing",
                "Architecture",
                "Inbox",
                "Archive",
            }));
    }

    [Test]
    public async Task Filters_CombineAsStableAndPredicates()
    {
        var area = new Area(AreaId.New(), new ParaContextName("Writing"));
        var focus = new Tag(TagId.New(), "Focus");
        var other = new Tag(TagId.New(), "Other");
        var alpha = CreateNote("Alpha", PrimaryPlacement.InArea(area.Id));
        alpha.AddTag(focus.Id);
        alpha.MarkFavorite();
        var zebra = CreateNote("zebra", PrimaryPlacement.InArea(area.Id));
        zebra.AddTag(focus.Id);
        zebra.MarkFavorite();
        var wrongKind = CreateIdea("Idea", PrimaryPlacement.InArea(area.Id));
        wrongKind.AddTag(focus.Id);
        wrongKind.MarkFavorite();
        var wrongTag = CreateNote("Other tag", PrimaryPlacement.InArea(area.Id));
        wrongTag.AddTag(other.Id);
        wrongTag.MarkFavorite();
        var notFavorite = CreateNote("Not favorite", PrimaryPlacement.InArea(area.Id));
        notFavorite.AddTag(focus.Id);
        var repository = new FakeRepository(
            EmptyState() with
            {
                Areas = [area],
                Tags = [focus, other],
                BrainItems = [zebra, wrongKind, wrongTag, notFavorite, alpha],
            });
        var viewModel = CreateViewModel(repository);

        await viewModel.LoadCommand.ExecuteAsync(null);
        viewModel.SelectedContext = viewModel.Contexts.Single(
            context => context.Name == "Writing");
        viewModel.SelectedKindFilter = viewModel.KindFilters.Single(
            filter => filter.Kind == BrainItemKind.Note);
        viewModel.SelectedTagFilter = viewModel.TagFilters.Single(
            filter => filter.TagId == focus.Id);
        viewModel.FavoritesOnly = true;

        Assert.That(
            viewModel.Items.Select(item => item.Title),
            Is.EqualTo(new[] { "Alpha", "zebra" }));
    }

    [Test]
    public async Task Move_ChangesOnlyPrimaryPlacement_AndRejectsStaleDestination()
    {
        var source = new Area(AreaId.New(), new ParaContextName("Source"));
        var destination = new Area(
            AreaId.New(),
            new ParaContextName("Destination"));
        var tag = new Tag(TagId.New(), "Keep");
        var linked = CreateNote("Linked", PrimaryPlacement.InArea(source.Id));
        var item = CreateNote("Move me", PrimaryPlacement.InArea(source.Id));
        item.AddTag(tag.Id);
        item.AddContextualLink(linked.Id);
        var repository = new FakeRepository(
            EmptyState() with
            {
                Areas = [source, destination],
                Tags = [tag],
                BrainItems = [item, linked],
            });
        var viewModel = CreateViewModel(repository);

        await viewModel.LoadCommand.ExecuteAsync(null);
        SelectItem(viewModel, "Source", "Move me");
        var target = viewModel.Destinations.Single(
            candidate => candidate.Placement == PrimaryPlacement.InArea(destination.Id));

        var moved = await viewModel.MoveSelectedAsync(target);

        Assert.Multiple(() =>
        {
            Assert.That(moved, Is.True);
            Assert.That(
                item.PrimaryPlacement,
                Is.EqualTo(PrimaryPlacement.InArea(destination.Id)));
            Assert.That(item.TagIds, Is.EqualTo(new[] { tag.Id }));
            Assert.That(item.ContextualLinks, Is.EqualTo(new[] { linked.Id }));
            Assert.That(repository.SaveCount, Is.EqualTo(1));
        });

        SelectItem(viewModel, "Destination", "Move me");
        var staleTarget = viewModel.Destinations.Single(
            candidate => candidate.Placement == PrimaryPlacement.InArea(source.Id));
        source.Archive();

        var staleMove = await viewModel.MoveSelectedAsync(staleTarget);

        Assert.Multiple(() =>
        {
            Assert.That(staleMove, Is.False);
            Assert.That(viewModel.ErrorMessage, Does.Contain("no longer available"));
            Assert.That(
                item.PrimaryPlacement,
                Is.EqualTo(PrimaryPlacement.InArea(destination.Id)));
            Assert.That(repository.SaveCount, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task ArchiveRestoreAndOrganization_PreserveTypeTagsAndLinks()
    {
        var area = new Area(AreaId.New(), new ParaContextName("Writing"));
        var tag = new Tag(TagId.New(), "Reference");
        var linked = CreateNote("Related", PrimaryPlacement.InArea(area.Id));
        var item = CreateIdea("Idea", PrimaryPlacement.InArea(area.Id));
        var repository = new FakeRepository(
            EmptyState() with
            {
                Areas = [area],
                Tags = [tag],
                BrainItems = [item, linked],
            });
        var viewModel = CreateViewModel(repository);

        await viewModel.LoadCommand.ExecuteAsync(null);
        SelectItem(viewModel, "Writing", "Idea");
        await viewModel.AddTagToSelectedAsync(
            viewModel.AvailableTags.Single(candidate => candidate.Id == tag.Id));
        await viewModel.AddLinkToSelectedAsync(
            viewModel.AvailableLinkTargets.Single(candidate => candidate.Id == linked.Id));
        await viewModel.ArchiveSelectedAsync();
        SelectItem(viewModel, "Archive", "Idea");

        Assert.Multiple(() =>
        {
            Assert.That(item.IsArchived, Is.True);
            Assert.That(item.Kind, Is.EqualTo(BrainItemKind.Idea));
            Assert.That(item.TagIds, Is.EqualTo(new[] { tag.Id }));
            Assert.That(item.ContextualLinks, Is.EqualTo(new[] { linked.Id }));
            Assert.That(
                viewModel.SelectedItem!.SecondaryRelationships,
                Does.Contain("#Reference"));
            Assert.That(
                viewModel.SelectedItem.SecondaryRelationships,
                Does.Contain("Related"));
        });

        var restored = await viewModel.RestoreSelectedAsync();

        Assert.Multiple(() =>
        {
            Assert.That(restored, Is.True);
            Assert.That(item.IsArchived, Is.False);
            Assert.That(item.Kind, Is.EqualTo(BrainItemKind.Idea));
            Assert.That(item.TagIds, Is.EqualTo(new[] { tag.Id }));
            Assert.That(item.ContextualLinks, Is.EqualTo(new[] { linked.Id }));
        });
    }

    private static ParaBrowserViewModel CreateViewModel(FakeRepository repository) =>
        new(
            repository,
            new CoreKnowledgeUseCases(repository),
            () => CreatedAt.AddHours(1));

    private static void AssertContextItems(
        ParaBrowserViewModel viewModel,
        string contextName,
        params string[] expectedTitles)
    {
        viewModel.SelectedContext = viewModel.Contexts.Single(
            context => context.Name == contextName);
        Assert.That(
            viewModel.Items.Select(item => item.Title),
            Is.EqualTo(expectedTitles));
    }

    private static void SelectItem(
        ParaBrowserViewModel viewModel,
        string contextName,
        string itemTitle)
    {
        viewModel.SelectedContext = viewModel.Contexts.Single(
            context => context.Name == contextName);
        viewModel.SelectedItem = viewModel.Items.Single(item => item.Title == itemTitle);
    }

    private static BrainItem CreateNote(string title, PrimaryPlacement placement) =>
        new(
            SecondBrainItemId.New(),
            BrainItemKind.Note,
            title,
            $"{title} content",
            placement,
            CreatedAt,
            noteKind: NoteKind.General);

    private static BrainItem CreateIdea(string title, PrimaryPlacement placement) =>
        new(
            SecondBrainItemId.New(),
            BrainItemKind.Idea,
            title,
            $"{title} content",
            placement,
            CreatedAt,
            ideaMaturity: IdeaMaturity.Captured);

    private static CoreKnowledgeState EmptyState() =>
        new([], [], [], [], [], []);

    private sealed class FakeRepository(CoreKnowledgeState state)
        : ICoreKnowledgeRepository
    {
        public CoreKnowledgeState State { get; private set; } = state;

        public int SaveCount { get; private set; }

        public Task<CoreKnowledgeState> LoadStateAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult(State);

        public Task SaveStateAsync(
            CoreKnowledgeState newState,
            CancellationToken cancellationToken = default)
        {
            State = newState;
            SaveCount++;
            return Task.CompletedTask;
        }
    }
}
