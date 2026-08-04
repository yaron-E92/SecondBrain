using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SecondBrain.Application.Ports;
using SecondBrain.Domain.Entities;

namespace SecondBrain.Presentation.ViewModels;

public sealed record CoreSearchKindOption(string Label, BrainItemKind? Kind);

public sealed record CoreSearchTagOption(string Label, string? Tag);

public sealed record CoreSearchPlacementOption(
    string Label,
    CoreSearchPlacement? Placement);

public sealed record CoreSearchArchiveOption(string Label, bool? IsArchived);

public sealed partial class CoreSearchViewModel(ICoreSearchQueryService queries)
    : ObservableObject
{
    private const int PageSize = 20;
    private int _nextOffset;

    [ObservableProperty]
    public partial string QueryText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial IReadOnlyList<CoreSearchKindOption> KindOptions { get; set; } =
        [new("All kinds", null), .. Enum.GetValues<BrainItemKind>()
            .Select(kind => new CoreSearchKindOption(kind.ToString(), kind))];

    [ObservableProperty]
    public partial CoreSearchKindOption SelectedKind { get; set; } =
        new("All kinds", null);

    [ObservableProperty]
    public partial IReadOnlyList<CoreSearchTagOption> TagOptions { get; set; } =
        [new("All tags", null)];

    [ObservableProperty]
    public partial CoreSearchTagOption SelectedTag { get; set; } =
        new("All tags", null);

    [ObservableProperty]
    public partial IReadOnlyList<CoreSearchPlacementOption> PlacementOptions { get; set; } =
        [new("All placements", null)];

    [ObservableProperty]
    public partial CoreSearchPlacementOption SelectedPlacement { get; set; } =
        new("All placements", null);

    [ObservableProperty]
    public partial IReadOnlyList<CoreSearchArchiveOption> ArchiveOptions { get; set; } =
    [
        new("Active", false),
        new("Archived", true),
        new("Active and archived", null),
    ];

    [ObservableProperty]
    public partial CoreSearchArchiveOption SelectedArchive { get; set; } =
        new("Active", false);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasResults))]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    public partial IReadOnlyList<CoreSearchItem> Results { get; set; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AreFavoritesEmpty))]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    public partial IReadOnlyList<CoreSearchItem> Favorites { get; set; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AreRecentItemsEmpty))]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    public partial IReadOnlyList<CoreSearchItem> RecentItems { get; set; } = [];

    [ObservableProperty]
    public partial CoreSearchItem? SelectedResult { get; set; }

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    [ObservableProperty]
    public partial bool IsLoadingMore { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    public partial string? ErrorMessage { get; set; }

    [ObservableProperty]
    public partial bool AreResultsStale { get; set; }

    [ObservableProperty]
    public partial bool HasMore { get; set; }

    [ObservableProperty]
    public partial string ResultStatus { get; set; } = "Search your Core knowledge.";

    public bool HasResults => Results.Count > 0;

    public bool IsEmpty =>
        Results.Count == 0 && Favorites.Count == 0 && RecentItems.Count == 0;

    public bool AreFavoritesEmpty => Favorites.Count == 0;

    public bool AreRecentItemsEmpty => RecentItems.Count == 0;

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    [RelayCommand]
    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        IsLoading = true;
        ErrorMessage = null;
        try
        {
            var options = await queries.GetFilterOptionsAsync(cancellationToken);
            ApplyOptions(options);
            Favorites = await queries.GetFavoritesAsync(5, cancellationToken);
            RecentItems = await queries.GetRecentAsync(5, cancellationToken);
            await SearchCoreAsync(false, cancellationToken);
            AreResultsStale = false;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // A superseded load is not a user-facing failure.
        }
        catch (Exception exception)
        {
            AreResultsStale = Results.Count > 0;
            ErrorMessage = $"Search could not be loaded. {exception.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task SearchAsync(CancellationToken cancellationToken)
    {
        IsLoading = true;
        ErrorMessage = null;
        try
        {
            await SearchCoreAsync(false, cancellationToken);
            AreResultsStale = false;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // A superseded search is not a user-facing failure.
        }
        catch (Exception exception)
        {
            AreResultsStale = Results.Count > 0;
            ErrorMessage = $"Search failed. {exception.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand(CanExecute = nameof(HasMore))]
    private async Task LoadMoreAsync(CancellationToken cancellationToken)
    {
        if (!HasMore || IsLoadingMore)
        {
            return;
        }

        IsLoadingMore = true;
        ErrorMessage = null;
        try
        {
            await SearchCoreAsync(true, cancellationToken);
            AreResultsStale = false;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // A superseded page request is not a user-facing failure.
        }
        catch (Exception exception)
        {
            AreResultsStale = Results.Count > 0;
            ErrorMessage = $"More results could not be loaded. {exception.Message}";
        }
        finally
        {
            IsLoadingMore = false;
        }
    }

    [RelayCommand]
    private async Task ClearFiltersAsync(CancellationToken cancellationToken)
    {
        QueryText = string.Empty;
        SelectedKind = KindOptions[0];
        SelectedTag = TagOptions[0];
        SelectedPlacement = PlacementOptions[0];
        SelectedArchive = ArchiveOptions[0];
        await SearchAsync(cancellationToken);
    }

    private async Task SearchCoreAsync(
        bool append,
        CancellationToken cancellationToken)
    {
        var offset = append ? _nextOffset : 0;
        var placement = SelectedPlacement.Placement;
        var page = await queries.SearchAsync(
            new CoreSearchQuery(
                QueryText,
                SelectedKind.Kind,
                SelectedTag.Tag,
                placement?.Kind,
                placement?.Id,
                SelectedArchive.IsArchived,
                Offset: offset,
                PageSize: PageSize),
            cancellationToken);

        Results = append ? Results.Concat(page.Items).ToArray() : page.Items;
        _nextOffset = offset + page.Items.Count;
        HasMore = _nextOffset < page.TotalCount;
        LoadMoreCommand.NotifyCanExecuteChanged();
        SelectedResult = Results.FirstOrDefault(item => item.Id == SelectedResult?.Id)
            ?? Results.FirstOrDefault();
        ResultStatus = page.TotalCount switch
        {
            0 when string.IsNullOrWhiteSpace(QueryText) =>
                "No Core items yet. Capture a thought to start your brain.",
            0 => "No results. Broaden your query or clear filters.",
            1 => "1 result",
            _ => $"{page.TotalCount} results",
        };
    }

    private void ApplyOptions(CoreSearchFilterOptions options)
    {
        var selectedTag = SelectedTag.Tag;
        TagOptions = [new("All tags", null), .. options.Tags.Select(tag =>
            new CoreSearchTagOption(tag, tag))];
        SelectedTag = TagOptions.FirstOrDefault(option => string.Equals(
            option.Tag,
            selectedTag,
            StringComparison.OrdinalIgnoreCase)) ?? TagOptions[0];

        var selectedPlacementId = SelectedPlacement.Placement?.Id;
        PlacementOptions = [new("All placements", null), .. options.Placements.Select(
            placement => new CoreSearchPlacementOption(
                $"{placement.Kind} · {placement.Name}",
                placement))];
        SelectedPlacement = PlacementOptions.FirstOrDefault(option =>
            option.Placement?.Id == selectedPlacementId) ?? PlacementOptions[0];
    }
}
