using NUnit.Framework;
using SecondBrain.Application.Ports;
using SecondBrain.Application.UseCases;
using SecondBrain.Domain.Entities;
using SecondBrain.Domain.ValueObjects;

namespace SecondBrain.Application.Tests.UseCases;

[TestFixture]
public sealed class ReviewUseCaseTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 8, 14, 12, 0, 0, TimeSpan.Zero);

    [Test]
    public async Task InboxQueue_IsOldestFirst_AndDeferredPositionResumesFromPersistence()
    {
        var inbox = new Area(AreaId.New(), new ParaContextName("Inbox"));
        var first = CreateInboxItem(inbox.Id, "First", Now.AddHours(-2));
        var second = CreateInboxItem(inbox.Id, "Second", Now.AddHours(-1));
        var repository = new FakeRepository(
            EmptyState() with
            {
                Areas = [inbox],
                BrainItems = [second, first],
            });
        var useCase = new ReviewUseCase(repository);

        var initial = await useCase.GetQueueAsync(
            new GetReviewQueueQuery(ReviewQueueKind.Inbox, Now));
        await useCase.DeferAsync(new DeferReviewCommand(
            ReviewTargetKind.InboxItem,
            first.Id.Value,
            Now,
            Now.AddDays(1)));
        var resumed = await new ReviewUseCase(repository).GetQueueAsync(
            new GetReviewQueueQuery(ReviewQueueKind.Inbox, Now.AddHours(1)));
        var dueAgain = await new ReviewUseCase(repository).GetQueueAsync(
            new GetReviewQueueQuery(ReviewQueueKind.Inbox, Now.AddDays(1)));

        Assert.Multiple(() =>
        {
            Assert.That(initial.Select(item => item.Title), Is.EqualTo(new[] { "First", "Second" }));
            Assert.That(resumed.Select(item => item.Title), Is.EqualTo(new[] { "Second" }));
            Assert.That(dueAgain.Select(item => item.Title), Is.EqualTo(new[] { "First", "Second" }));
            Assert.That(repository.SaveCount, Is.EqualTo(1));
        });
    }


    [Test]
    public async Task MarkReviewed_AdvancesInboxQueueImmediately()
    {
        var inbox = new Area(AreaId.New(), new ParaContextName("Inbox"));
        var first = CreateInboxItem(inbox.Id, "First", Now.AddHours(-2));
        var second = CreateInboxItem(inbox.Id, "Second", Now.AddHours(-1));
        var repository = new FakeRepository(
            EmptyState() with
            {
                Areas = [inbox],
                BrainItems = [first, second],
            });
        var useCase = new ReviewUseCase(repository);

        await useCase.MarkReviewedAsync(new ReviewDecisionCommand(
            ReviewTargetKind.InboxItem,
            first.Id.Value,
            Now));
        var remaining = await useCase.GetQueueAsync(
            new GetReviewQueueQuery(ReviewQueueKind.Inbox, Now));

        Assert.That(remaining.Select(item => item.Title), Is.EqualTo(new[] { "Second" }));
    }

    [Test]
    public async Task ParaQueue_UsesTransparentCadences_AndKeepsOutdatedResourcesActive()
    {
        var project = new Project(
            ProjectId.New(),
            new ParaContextName("Zeta project"),
            "Ship it");
        project.Activate();
        var area = new Area(AreaId.New(), new ParaContextName("Alpha area"));
        var topic = new ResourceTopic(
            ResourceTopicId.New(),
            new ParaContextName("References"));
        var resource = CreateResource(topic.Id, "Beta resource");
        var repository = new FakeRepository(
            EmptyState() with
            {
                Projects = [project],
                Areas = [area],
                ResourceTopics = [topic],
                BrainItems = [resource],
            });
        var useCase = new ReviewUseCase(repository);

        var initial = await useCase.GetQueueAsync(
            new GetReviewQueueQuery(ReviewQueueKind.Para, Now));
        await useCase.MarkReviewedAsync(new ReviewDecisionCommand(
            ReviewTargetKind.Project,
            project.Id.Value,
            Now));
        await useCase.MarkReviewedAsync(new ReviewDecisionCommand(
            ReviewTargetKind.Resource,
            resource.Id.Value,
            Now));
        var nextDay = await useCase.GetQueueAsync(
            new GetReviewQueueQuery(ReviewQueueKind.Para, Now.AddDays(1)));
        var afterCadences = await useCase.GetQueueAsync(
            new GetReviewQueueQuery(ReviewQueueKind.Para, Now.AddDays(31)));

        Assert.Multiple(() =>
        {
            Assert.That(
                initial.Select(item => item.Title),
                Is.EqualTo(new[] { "Alpha area", "Beta resource", "Zeta project" }));
            Assert.That(nextDay.Select(item => item.Title), Is.EqualTo(new[] { "Alpha area" }));
            Assert.That(afterCadences.Select(item => item.Title), Does.Contain("Zeta project"));
            Assert.That(afterCadences.Select(item => item.Title), Does.Contain("Beta resource"));
            Assert.That(resource.IsArchived, Is.False);
        });
    }

    private static BrainItem CreateInboxItem(
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

    private static BrainItem CreateResource(ResourceTopicId topicId, string title) =>
        new(
            SecondBrainItemId.New(),
            BrainItemKind.ResourceArtifact,
            title,
            $"{title} content",
            PrimaryPlacement.InResourceTopic(topicId),
            Now.AddDays(-60),
            resourceArtifactKind: ResourceArtifactKind.Guide,
            resourceFreshness: ResourceFreshness.Outdated,
            reviewDate: DateOnly.FromDateTime(Now.UtcDateTime.AddDays(-30)));

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
