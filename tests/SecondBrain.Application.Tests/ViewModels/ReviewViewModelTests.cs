using NUnit.Framework;
using SecondBrain.Application.Ports;
using SecondBrain.Application.UseCases;
using SecondBrain.Domain.Entities;
using SecondBrain.Domain.ValueObjects;
using SecondBrain.Presentation.ViewModels;

namespace SecondBrain.Application.Tests.ViewModels;

[TestFixture]
public sealed class ReviewViewModelTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 8, 14, 12, 0, 0, TimeSpan.Zero);

    [Test]
    public async Task FailedDecision_RetainsCurrentItemAndOffersRetryableFeedback()
    {
        var inbox = new Area(AreaId.New(), new ParaContextName("Inbox"));
        var item = new BrainItem(
            SecondBrainItemId.New(),
            BrainItemKind.Idea,
            "Keep my place",
            "Content",
            PrimaryPlacement.InArea(inbox.Id),
            Now.AddHours(-1),
            ideaMaturity: IdeaMaturity.Captured);
        var repository = new FailingSaveRepository(
            new CoreKnowledgeState([], [inbox], [], [], [item], []));
        var viewModel = new ReviewViewModel(
            new ReviewUseCase(repository),
            () => Now);
        viewModel.Configure(ReviewQueueKind.Inbox, null, null, "home");
        await viewModel.LoadCommand.ExecuteAsync(null);

        await viewModel.MarkReviewedCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.CurrentItem?.TargetId, Is.EqualTo(item.Id.Value));
            Assert.That(viewModel.RemainingCount, Is.EqualTo(1));
            Assert.That(viewModel.ChangedCount, Is.Zero);
            Assert.That(viewModel.HasError, Is.True);
            Assert.That(viewModel.ErrorMessage, Does.Contain("place is unchanged"));
            Assert.That(viewModel.IsLoading, Is.False);
        });
    }

    [Test]
    public async Task SavedDecision_WhenRefreshFails_DoesNotOfferTheDecidedItemAgain()
    {
        var inbox = new Area(AreaId.New(), new ParaContextName("Inbox"));
        var item = new BrainItem(
            SecondBrainItemId.New(),
            BrainItemKind.Idea,
            "Save once",
            "Content",
            PrimaryPlacement.InArea(inbox.Id),
            Now.AddHours(-1),
            ideaMaturity: IdeaMaturity.Captured);
        var repository = new ControllableRepository(
            new CoreKnowledgeState([], [inbox], [], [], [item], []));
        var viewModel = new ReviewViewModel(
            new ReviewUseCase(repository),
            () => Now);
        viewModel.Configure(ReviewQueueKind.Inbox, null, null, "home");
        await viewModel.LoadCommand.ExecuteAsync(null);
        repository.FailNextLoadAfterSave = true;

        await viewModel.MarkReviewedCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.CurrentItem, Is.Null);
            Assert.That(viewModel.RemainingCount, Is.Zero);
            Assert.That(viewModel.ChangedCount, Is.EqualTo(1));
            Assert.That(viewModel.HasError, Is.True);
            Assert.That(viewModel.ErrorMessage, Does.Contain("decision was saved"));
            Assert.That(viewModel.ErrorMessage, Does.Not.Contain("not saved"));
            Assert.That(viewModel.CanActOnCurrentItem, Is.False);
            Assert.That(repository.SaveCount, Is.EqualTo(1));
        });

        await viewModel.LoadCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.CurrentItem, Is.Null);
            Assert.That(viewModel.HasError, Is.False);
            Assert.That(repository.SaveCount, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task ChangedScope_WhenLoadFails_DoesNotExposeItemsFromPreviousQueue()
    {
        var inbox = new Area(AreaId.New(), new ParaContextName("Inbox"));
        var item = new BrainItem(
            SecondBrainItemId.New(),
            BrainItemKind.Idea,
            "Inbox item",
            "Content",
            PrimaryPlacement.InArea(inbox.Id),
            Now.AddHours(-1),
            ideaMaturity: IdeaMaturity.Captured);
        var repository = new ControllableRepository(
            new CoreKnowledgeState([], [inbox], [], [], [item], []));
        var viewModel = new ReviewViewModel(
            new ReviewUseCase(repository),
            () => Now);
        viewModel.Configure(ReviewQueueKind.Inbox, null, null, "home");
        await viewModel.LoadCommand.ExecuteAsync(null);
        Assert.That(viewModel.CurrentItem?.TargetId, Is.EqualTo(item.Id.Value));

        repository.FailNextLoad = true;
        viewModel.Configure(ReviewQueueKind.Para, null, null, "para");

        Assert.That(viewModel.CurrentItem, Is.Null);

        await viewModel.LoadCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.CurrentItem, Is.Null);
            Assert.That(viewModel.RemainingCount, Is.Zero);
            Assert.That(viewModel.HasError, Is.True);
            Assert.That(viewModel.CanActOnCurrentItem, Is.False);
        });
    }

    private sealed class FailingSaveRepository(CoreKnowledgeState state)
        : ICoreKnowledgeRepository
    {
        public Task<CoreKnowledgeState> LoadStateAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult(state);

        public Task SaveStateAsync(
            CoreKnowledgeState newState,
            CancellationToken cancellationToken = default) =>
            Task.FromException(new InvalidOperationException("Database unavailable"));
    }

    private sealed class ControllableRepository(CoreKnowledgeState state)
        : ICoreKnowledgeRepository
    {
        private CoreKnowledgeState _state = state;

        public bool FailNextLoad { get; set; }

        public bool FailNextLoadAfterSave { get; set; }

        public int SaveCount { get; private set; }

        public Task<CoreKnowledgeState> LoadStateAsync(
            CancellationToken cancellationToken = default)
        {
            if (FailNextLoad)
            {
                FailNextLoad = false;
                return Task.FromException<CoreKnowledgeState>(
                    new InvalidOperationException("Database unavailable"));
            }

            return Task.FromResult(_state);
        }

        public Task SaveStateAsync(
            CoreKnowledgeState newState,
            CancellationToken cancellationToken = default)
        {
            _state = newState;
            SaveCount++;
            if (FailNextLoadAfterSave)
            {
                FailNextLoadAfterSave = false;
                FailNextLoad = true;
            }

            return Task.CompletedTask;
        }
    }
}
