using NUnit.Framework;
using SecondBrain.Application.Ports;
using SecondBrain.Application.UseCases;
using SecondBrain.Domain.Entities;
using SecondBrain.Domain.ValueObjects;
using SecondBrain.Presentation.ViewModels;

namespace SecondBrain.Application.Tests.ViewModels;

[TestFixture]
public sealed class JournalBrowserViewModelTests
{
    private static readonly DateTimeOffset CreatedAt =
        new(2026, 8, 4, 8, 0, 0, TimeSpan.Zero);

    [Test]
    public async Task FreshDatabase_CreateOpensEmptyTimelineAndPersistsSelection()
    {
        var repository = new FakeRepository(EmptyState());
        var viewModel = CreateViewModel(repository);

        await viewModel.LoadAsync();
        viewModel.BeginCreateJournal();
        viewModel.JournalTitle = "Daily";
        var saved = await viewModel.SaveJournalAsync();

        Assert.Multiple(() =>
        {
            Assert.That(saved, Is.True);
            Assert.That(viewModel.IsEmpty, Is.False);
            Assert.That(viewModel.SelectedJournal!.Title, Is.EqualTo("Daily"));
            Assert.That(viewModel.IsTimelineEmpty, Is.True);
            Assert.That(viewModel.CanAddEntry, Is.True);
            Assert.That(repository.SaveCount, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task Timeline_UsesStableDateThenIdentityOrderAndParaContextNames()
    {
        var area = new Area(AreaId.New(), new ParaContextName("Writing"));
        var journal = new Journal(SecondBrainItemId.New(), "Daily");
        var first = Entry(
            Guid.Parse("00000000-0000-0000-0000-000000000001"),
            "First",
            new DateOnly(2026, 8, 2),
            area.Id);
        var second = Entry(
            Guid.Parse("00000000-0000-0000-0000-000000000002"),
            "Second",
            new DateOnly(2026, 8, 2),
            area.Id);
        var later = Entry(
            Guid.Parse("00000000-0000-0000-0000-000000000003"),
            "Later",
            new DateOnly(2026, 8, 3),
            area.Id);
        journal.AddEntry(later);
        journal.AddEntry(second);
        journal.AddEntry(first);
        var repository = new FakeRepository(
            EmptyState() with
            {
                Areas = [area],
                BrainItems = [later, second, first],
                Journals = [journal],
            });
        var viewModel = CreateViewModel(repository);

        await viewModel.LoadAsync();

        Assert.Multiple(() =>
        {
            Assert.That(
                viewModel.Timeline.Select(entry => entry.Title),
                Is.EqualTo(new[] { "First", "Second", "Later" }));
            Assert.That(
                viewModel.Timeline.All(entry => entry.ParaContext == "Area · Writing"),
                Is.True);
        });
    }

    [Test]
    public async Task ArchiveRestore_PreservesTimelineAndMakesLifecycleExplicit()
    {
        var area = new Area(AreaId.New(), new ParaContextName("Writing"));
        var journal = new Journal(SecondBrainItemId.New(), "Daily");
        var entry = Entry(Guid.NewGuid(), "Entry", new DateOnly(2026, 8, 4), area.Id);
        journal.AddEntry(entry);
        var repository = new FakeRepository(
            EmptyState() with
            {
                Areas = [area],
                BrainItems = [entry],
                Journals = [journal],
            });
        var viewModel = CreateViewModel(repository);
        await viewModel.LoadAsync();

        var archived = await viewModel.ArchiveSelectedAsync();
        var retainedTimeline = viewModel.Timeline.ToArray();
        var restored = await viewModel.RestoreSelectedAsync();

        Assert.Multiple(() =>
        {
            Assert.That(archived, Is.True);
            Assert.That(restored, Is.True);
            Assert.That(retainedTimeline.Select(item => item.Title), Is.EqualTo(new[] { "Entry" }));
            Assert.That(viewModel.SelectedJournal!.IsArchived, Is.False);
            Assert.That(viewModel.CanAddEntry, Is.True);
            Assert.That(repository.SaveCount, Is.EqualTo(2));
        });
    }

    [Test]
    public async Task InvalidFailureAndCancel_RetainDraftWithoutMutation()
    {
        var repository = new FakeRepository(
            EmptyState(),
            new InvalidOperationException("Disk unavailable"));
        var viewModel = CreateViewModel(repository);
        await viewModel.LoadAsync();
        viewModel.BeginCreateJournal();
        viewModel.JournalTitle = " ";
        var invalid = await viewModel.SaveJournalAsync();
        viewModel.JournalTitle = "Keep this draft";
        var failed = await viewModel.SaveJournalAsync();

        Assert.Multiple(() =>
        {
            Assert.That(invalid, Is.False);
            Assert.That(failed, Is.False);
            Assert.That(viewModel.JournalTitle, Is.EqualTo("Keep this draft"));
            Assert.That(viewModel.ErrorMessage, Does.Contain("Disk unavailable"));
            Assert.That(repository.State.Journals, Is.Empty);
        });

        viewModel.CancelJournalEdit();

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.IsJournalEditorVisible, Is.False);
            Assert.That(repository.State.Journals, Is.Empty);
        });
    }

    private static JournalBrowserViewModel CreateViewModel(FakeRepository repository) =>
        new(repository, new CoreKnowledgeUseCases(repository));

    private static BrainItem Entry(
        Guid id,
        string title,
        DateOnly date,
        AreaId areaId) =>
        new(
            new SecondBrainItemId(id),
            BrainItemKind.JournalEntry,
            title,
            $"{title} content",
            PrimaryPlacement.InArea(areaId),
            CreatedAt,
            entryDate: date);

    private static CoreKnowledgeState EmptyState() =>
        new([], [], [], [], [], []);

    private sealed class FakeRepository(
        CoreKnowledgeState state,
        Exception? saveException = null) : ICoreKnowledgeRepository
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
