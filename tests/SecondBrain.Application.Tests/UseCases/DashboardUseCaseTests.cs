using NUnit.Framework;
using SecondBrain.Presentation.ViewModels;
using SecondBrain.Application.Ports;
using SecondBrain.Application.UseCases;
using SecondBrain.Domain.Entities;
using SecondBrain.Domain.ValueObjects;

namespace SecondBrain.Application.Tests.UseCases;

[TestFixture]
public sealed class DashboardUseCaseTests
{
    private static readonly DateTimeOffset CreatedAt =
        new(2026, 7, 26, 8, 0, 0, TimeSpan.Zero);

    [Test]
    public async Task DashboardQuery_ProjectsFavoritesRecentAndInbox_AreProjected()
    {
        var inboxArea = new Area(
            AreaId.New(),
            new ParaContextName("Inbox"));
        var otherArea = new Area(
            AreaId.New(),
            new ParaContextName("Writing"));
        var activeProject = new Project(
            ProjectId.New(),
            new ParaContextName("Launch"),
            "Ship the dashboard");
        activeProject.Activate();
        var plannedProject = new Project(
            ProjectId.New(),
            new ParaContextName("Later"),
            "Wait");

        var captured = CreateIdea(
            inboxArea.Id,
            "Captured",
            CreatedAt);
        var favorite = CreateIdea(
            otherArea.Id,
            "Favorite",
            CreatedAt.AddMinutes(1));
        favorite.MarkFavorite();
        var recent = CreateIdea(
            otherArea.Id,
            "Recent",
            CreatedAt.AddMinutes(2));
        var repository = new FakeRepository(
            EmptyState() with
            {
                Areas = [inboxArea, otherArea],
                Projects = [activeProject, plannedProject],
                BrainItems = [captured, favorite, recent],
            });

        var snapshot = await new DashboardUseCase(repository)
            .GetDashboardAsync(new GetDashboardQuery());

        Assert.Multiple(() =>
        {
            Assert.That(
                snapshot.Inbox.Select(item => item.Title),
                Is.EqualTo(new[] { "Captured" }));
            Assert.That(
                snapshot.ActiveProjects.Select(project => project.Name),
                Is.EqualTo(new[] { "Launch" }));
            Assert.That(
                snapshot.Favorites.Select(item => item.Title),
                Is.EqualTo(new[] { "Favorite" }));
            Assert.That(
                snapshot.RecentItems.Select(item => item.Title),
                Is.EqualTo(new[] { "Recent", "Favorite", "Captured" }));
        });
    }

    [Test]
    public async Task QuickCapture_PersistsOfflineAndRefreshesBothViewModelsImmediately()
    {
        var repository = new FakeRepository(EmptyState());
        var useCase = new DashboardUseCase(repository);
        var inbox = new InboxViewModel(useCase);
        var dashboard = new DashboardViewModel(useCase, inbox)
        {
            CaptureText = "Remember the architecture review"
        };

        await dashboard.CaptureCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(repository.SaveCount, Is.EqualTo(1));
            Assert.That(repository.State.Areas.Single().Name.Value, Is.EqualTo("Inbox"));
            Assert.That(repository.State.BrainItems.Single().Kind, Is.EqualTo(
                BrainItemKind.Idea));
            Assert.That(dashboard.InboxItems.Single().Title, Is.EqualTo(
                "Remember the architecture review"));
            Assert.That(inbox.Items.Single().Title, Is.EqualTo(
                "Remember the architecture review"));
            Assert.That(dashboard.CaptureText, Is.Empty);
            Assert.That(dashboard.CaptureStatus, Is.EqualTo("Captured to Inbox."));
        });
    }

    [Test]
    public async Task EmptyAndFailureStates_RemainActionable()
    {
        var emptyUseCase = new DashboardUseCase(
            new FakeRepository(EmptyState()));
        var emptyInbox = new InboxViewModel(emptyUseCase);
        var emptyDashboard = new DashboardViewModel(
            emptyUseCase,
            emptyInbox);

        await emptyDashboard.LoadCommand.ExecuteAsync(null);
        emptyDashboard.CaptureText = " ";
        await emptyDashboard.CaptureCommand.ExecuteAsync(null);

        var failingUseCase = new DashboardUseCase(
            new FakeRepository(
                EmptyState(),
                new InvalidOperationException("Database unavailable")));
        var failingInbox = new InboxViewModel(failingUseCase);
        var failingDashboard = new DashboardViewModel(
            failingUseCase,
            failingInbox);

        await failingDashboard.LoadCommand.ExecuteAsync(null);
        await failingInbox.LoadCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(emptyDashboard.IsInboxEmpty, Is.True);
            Assert.That(emptyDashboard.AreProjectsEmpty, Is.True);
            Assert.That(emptyDashboard.AreFavoritesEmpty, Is.True);
            Assert.That(emptyDashboard.AreRecentItemsEmpty, Is.True);
            Assert.That(
                emptyDashboard.CaptureStatus,
                Is.EqualTo("Type something to capture."));
            Assert.That(failingDashboard.HasError, Is.True);
            Assert.That(
                failingDashboard.ErrorMessage,
                Does.Contain("Database unavailable"));
            Assert.That(failingInbox.HasError, Is.True);
            Assert.That(failingDashboard.IsLoading, Is.False);
            Assert.That(failingInbox.IsLoading, Is.False);
        });
    }

    [Test]
    public async Task CanceledLoad_DoesNotTreatLoadingAsRefreshOrSurfaceAnError()
    {
        var repository = new CancellationAwareRepository();
        var useCase = new DashboardUseCase(repository);
        var dashboard = new DashboardViewModel(
            useCase,
            new InboxViewModel(useCase));

        var load = dashboard.LoadCommand.ExecuteAsync(null);
        await repository.LoadStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Multiple(() =>
        {
            Assert.That(dashboard.IsLoading, Is.True);
            Assert.That(dashboard.IsRefreshing, Is.False);
        });

        dashboard.LoadCommand.Cancel();
        await load.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Multiple(() =>
        {
            Assert.That(dashboard.IsLoading, Is.False);
            Assert.That(dashboard.IsRefreshing, Is.False);
            Assert.That(dashboard.HasError, Is.False);
        });
    }

    private static BrainItem CreateIdea(
        AreaId areaId,
        string title,
        DateTimeOffset createdAt) =>
        new(
            SecondBrainItemId.New(),
            BrainItemKind.Idea,
            title,
            $"{title} content",
            PrimaryPlacement.InArea(areaId),
            createdAt,
            ideaMaturity: IdeaMaturity.Captured);

    private static CoreKnowledgeState EmptyState() =>
        new([], [], [], [], [], []);

    private sealed class FakeRepository(
        CoreKnowledgeState state,
        Exception? loadException = null) : ICoreKnowledgeRepository
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
            State = newState;
            SaveCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class CancellationAwareRepository : ICoreKnowledgeRepository
    {
        public TaskCompletionSource LoadStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<CoreKnowledgeState> LoadStateAsync(
            CancellationToken cancellationToken = default)
        {
            LoadStarted.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return EmptyState();
        }

        public Task SaveStateAsync(
            CoreKnowledgeState state,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
