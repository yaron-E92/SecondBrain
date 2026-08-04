using NUnit.Framework;
using SecondBrain.Application.Ports;
using SecondBrain.Domain.Entities;
using SecondBrain.Domain.ValueObjects;
using SecondBrain.Presentation.ViewModels;

namespace SecondBrain.Application.Tests.ViewModels;

[TestFixture]
public sealed class CoreSearchViewModelTests
{
    [Test]
    public async Task Search_PreservesFiltersAcrossPagingAndReload()
    {
        var service = new SearchService();
        var viewModel = new CoreSearchViewModel(service);
        await viewModel.LoadCommand.ExecuteAsync(null);

        viewModel.QueryText = "alpha";
        viewModel.SelectedKind = viewModel.KindOptions.Single(option =>
            option.Kind == BrainItemKind.Note);
        viewModel.SelectedTag = viewModel.TagOptions.Single(option =>
            option.Tag == "focus");
        viewModel.SelectedPlacement = viewModel.PlacementOptions.Single(option =>
            option.Placement is not null);
        await viewModel.SearchCommand.ExecuteAsync(null);
        await viewModel.LoadMoreCommand.ExecuteAsync(null);
        await viewModel.LoadCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.QueryText, Is.EqualTo("alpha"));
            Assert.That(viewModel.SelectedKind.Kind, Is.EqualTo(BrainItemKind.Note));
            Assert.That(viewModel.SelectedTag.Tag, Is.EqualTo("focus"));
            Assert.That(viewModel.SelectedPlacement.Placement?.Name, Is.EqualTo("Writing"));
            Assert.That(service.Searches.Any(query => query.Offset == 1), Is.True);
            Assert.That(viewModel.HasError, Is.False);
        });
    }

    [Test]
    public async Task Failure_KeepsEditableQueryAndExistingResults_ForRetry()
    {
        var service = new SearchService();
        var viewModel = new CoreSearchViewModel(service);
        await viewModel.LoadCommand.ExecuteAsync(null);
        viewModel.QueryText = "keep me";
        service.FailSearch = true;

        await viewModel.SearchCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.QueryText, Is.EqualTo("keep me"));
            Assert.That(viewModel.Results, Is.Not.Empty);
            Assert.That(viewModel.AreResultsStale, Is.True);
            Assert.That(viewModel.ErrorMessage, Does.Contain("Temporary query failure"));
        });

        service.FailSearch = false;
        await viewModel.SearchCommand.ExecuteAsync(null);
        Assert.That(viewModel.HasError, Is.False);
    }

    private sealed class SearchService : ICoreSearchQueryService
    {
        private static readonly CoreSearchItem Item = new(
            new SecondBrainItemId(new Guid("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa")),
            BrainItemKind.Note,
            "Alpha",
            "Preview",
            PrimaryPlacementKind.Area,
            new Guid("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
            "Writing",
            false,
            true,
            new DateTimeOffset(2026, 8, 1, 8, 0, 0, TimeSpan.Zero),
            ["focus"],
            []);

        public List<CoreSearchQuery> Searches { get; } = [];

        public bool FailSearch { get; set; }

        public Task<CoreSearchPage> SearchAsync(
            CoreSearchQuery query,
            CancellationToken cancellationToken = default)
        {
            Searches.Add(query);
            if (FailSearch)
            {
                throw new InvalidOperationException("Temporary query failure");
            }

            return Task.FromResult(new CoreSearchPage([Item], 21));
        }

        public Task<CoreSearchFilterOptions> GetFilterOptionsAsync(
            CancellationToken cancellationToken = default) => Task.FromResult(
            new CoreSearchFilterOptions(
                ["focus"],
                [new CoreSearchPlacement(
                    PrimaryPlacementKind.Area,
                    Item.PlacementId,
                    "Writing")]));

        public Task<IReadOnlyList<CoreSearchItem>> GetFavoritesAsync(
            int limit = 5,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<CoreSearchItem>>([Item]);

        public Task<IReadOnlyList<CoreSearchItem>> GetRecentAsync(
            int limit = 5,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<CoreSearchItem>>([Item]);
    }
}
