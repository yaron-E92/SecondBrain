using NUnit.Framework;
using SecondBrain.Application.Ports;
using SecondBrain.Application.UseCases;
using SecondBrain.Domain.Entities;
using SecondBrain.Domain.ValueObjects;
using SecondBrain.Presentation.ViewModels;

namespace SecondBrain.Application.Tests.ViewModels;

[TestFixture]
public sealed class InboxProcessViewModelTests
{
    private static readonly DateTimeOffset CreatedAt =
        new(2026, 9, 9, 8, 0, 0, TimeSpan.Zero);

    [Test]
    public async Task Process_MovesCapturedIdeaOutOfInboxAndPersistsEdits()
    {
        var inbox = new Area(AreaId.New(), new ParaContextName("Inbox"));
        var writing = new Area(AreaId.New(), new ParaContextName("Writing"));
        var item = new BrainItem(
            SecondBrainItemId.New(),
            BrainItemKind.Idea,
            "Raw thought",
            "Something worth developing",
            PrimaryPlacement.InArea(inbox.Id),
            CreatedAt,
            ideaMaturity: IdeaMaturity.Captured);
        var repository = new FakeRepository(new CoreKnowledgeState(
            [],
            [inbox, writing],
            [],
            [],
            [item],
            []));
        var useCases = new CoreKnowledgeUseCases(repository);
        var viewModel = new InboxProcessViewModel(
            useCases,
            repository,
            () => CreatedAt.AddMinutes(10));

        await viewModel.LoadAsync(item.Id);
        viewModel.Title = "Writing idea";
        viewModel.Content = "Develop this into a longer essay.";
        viewModel.SelectedDestination = viewModel.Destinations.Single(option =>
            option.Label == "Area · Writing");

        await viewModel.ProcessCommand.ExecuteAsync(null);

        var saved = repository.State.BrainItems.Single();
        var inboxAfter = await new DashboardUseCase(repository)
            .GetInboxAsync(new GetInboxQuery());

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.WasProcessed, Is.True);
            Assert.That(viewModel.HasError, Is.False);
            Assert.That(saved.Title, Is.EqualTo("Writing idea"));
            Assert.That(saved.Content, Is.EqualTo("Develop this into a longer essay."));
            Assert.That(saved.PrimaryPlacement, Is.EqualTo(PrimaryPlacement.InArea(writing.Id)));
            Assert.That(inboxAfter, Is.Empty);
            Assert.That(repository.SaveCount, Is.EqualTo(2));
        });
    }

    [Test]
    public async Task Load_WithNoRealHome_KeepsProcessingBlockedAndExplainsNextAction()
    {
        var inbox = new Area(AreaId.New(), new ParaContextName("Inbox"));
        var item = new BrainItem(
            SecondBrainItemId.New(),
            BrainItemKind.Idea,
            "Unsorted",
            "Still needs a home",
            PrimaryPlacement.InArea(inbox.Id),
            CreatedAt,
            ideaMaturity: IdeaMaturity.Captured);
        var repository = new FakeRepository(new CoreKnowledgeState(
            [],
            [inbox],
            [],
            [],
            [item],
            []));
        var viewModel = new InboxProcessViewModel(
            new CoreKnowledgeUseCases(repository),
            repository,
            () => CreatedAt.AddMinutes(10));

        await viewModel.LoadAsync(item.Id);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.HasDestinations, Is.False);
            Assert.That(viewModel.SelectedDestination, Is.Null);
            Assert.That(viewModel.CanProcess, Is.False);
            Assert.That(viewModel.StatusMessage, Does.Contain("Create a Project, Area, or Resource Topic"));
            Assert.That(repository.SaveCount, Is.Zero);
        });
    }

    private sealed class FakeRepository(CoreKnowledgeState state)
        : ICoreKnowledgeRepository
    {
        public CoreKnowledgeState State { get; private set; } = state;

        public int SaveCount { get; private set; }

        public Task<CoreKnowledgeState> LoadStateAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult(State);

        public Task SaveStateAsync(
            CoreKnowledgeState state,
            CancellationToken cancellationToken = default)
        {
            State = state;
            SaveCount++;
            return Task.CompletedTask;
        }
    }
}
