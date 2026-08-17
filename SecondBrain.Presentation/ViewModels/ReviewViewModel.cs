using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SecondBrain.Application.Ports;
using SecondBrain.Application.UseCases;

namespace SecondBrain.Presentation.ViewModels;

public sealed partial class ReviewViewModel : ObservableObject
{
    private readonly ReviewUseCase _useCase;
    private readonly Func<DateTimeOffset> _now;
    private ReviewQueueKind _queueKind = ReviewQueueKind.Inbox;
    private ReviewScopeKind? _scopeKind;
    private Guid? _scopeId;

    public ReviewViewModel(ReviewUseCase useCase)
        : this(useCase, () => DateTimeOffset.UtcNow)
    {
    }

    internal ReviewViewModel(ReviewUseCase useCase, Func<DateTimeOffset> now)
    {
        _useCase = useCase;
        _now = now;
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CurrentItem))]
    [NotifyPropertyChangedFor(nameof(RemainingCount))]
    [NotifyPropertyChangedFor(nameof(IsComplete))]
    [NotifyPropertyChangedFor(nameof(HasCurrentItem))]
    [NotifyPropertyChangedFor(nameof(CanMoveCurrentItem))]
    public partial IReadOnlyList<ReviewQueueItem> Items { get; set; } = [];

    [ObservableProperty]
    public partial string Title { get; set; } = "Inbox review";

    [ObservableProperty]
    public partial string ReturnRoute { get; set; } = "home";

    [ObservableProperty]
    public partial int ChangedCount { get; set; }

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    public partial string? ErrorMessage { get; set; }

    [ObservableProperty]
    public partial string StatusMessage { get; set; } = string.Empty;

    public ReviewQueueItem? CurrentItem => Items.FirstOrDefault();

    public int RemainingCount => Items.Count;

    public bool IsComplete => !IsLoading && Items.Count == 0 && !HasError;

    public bool HasCurrentItem => CurrentItem is not null;

    public bool CanMoveCurrentItem => CurrentItem?.BrainItemId is not null;

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public void Configure(
        ReviewQueueKind queueKind,
        ReviewScopeKind? scopeKind,
        Guid? scopeId,
        string? returnRoute)
    {
        _queueKind = queueKind;
        _scopeKind = scopeKind;
        _scopeId = scopeId;
        Title = queueKind == ReviewQueueKind.Inbox
            ? "Inbox review"
            : scopeKind is null ? "Due PARA review" : "Workspace review";
        ReturnRoute = NormalizeRoute(returnRoute);
        ChangedCount = 0;
        StatusMessage = string.Empty;
        ErrorMessage = null;
    }

    [RelayCommand]
    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        IsLoading = true;
        ErrorMessage = null;
        try
        {
            Items = await _useCase.GetQueueAsync(
                new GetReviewQueueQuery(
                    _queueKind,
                    _now(),
                    _scopeKind,
                    _scopeId),
                cancellationToken);
            StatusMessage = Items.Count == 0
                ? "Review complete."
                : $"{Items.Count} item{(Items.Count == 1 ? "" : "s")} remaining.";
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            ErrorMessage = $"Review could not be loaded. {exception.Message}";
        }
        finally
        {
            IsLoading = false;
            OnPropertyChanged(nameof(IsComplete));
        }
    }

    [RelayCommand]
    private Task MarkReviewedAsync(CancellationToken cancellationToken) =>
        DecideAsync(
            (item, now) => _useCase.MarkReviewedAsync(
                new ReviewDecisionCommand(item.TargetKind, item.TargetId, now),
                cancellationToken),
            "Marked reviewed.",
            cancellationToken);

    [RelayCommand]
    private Task DeferAsync(CancellationToken cancellationToken) =>
        DecideAsync(
            (item, now) => _useCase.DeferAsync(
                new DeferReviewCommand(
                    item.TargetKind,
                    item.TargetId,
                    now,
                    now.AddDays(1)),
                cancellationToken),
            "Deferred until tomorrow.",
            cancellationToken);

    [RelayCommand]
    private Task ArchiveAsync(CancellationToken cancellationToken) =>
        DecideAsync(
            (item, now) => _useCase.ArchiveAsync(
                new ReviewDecisionCommand(item.TargetKind, item.TargetId, now),
                cancellationToken),
            "Archived.",
            cancellationToken);

    private async Task DecideAsync(
        Func<ReviewQueueItem, DateTimeOffset, Task> decide,
        string successMessage,
        CancellationToken cancellationToken)
    {
        var current = CurrentItem;
        if (current is null)
        {
            return;
        }

        IsLoading = true;
        ErrorMessage = null;
        try
        {
            await decide(current, _now());
            ChangedCount++;
            StatusMessage = successMessage;
            Items = await _useCase.GetQueueAsync(
                new GetReviewQueueQuery(
                    _queueKind,
                    _now(),
                    _scopeKind,
                    _scopeId),
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            ErrorMessage =
                $"The decision was not saved. Your place is unchanged. {exception.Message}";
        }
        finally
        {
            IsLoading = false;
            OnPropertyChanged(nameof(IsComplete));
        }
    }

    private static string NormalizeRoute(string? route) =>
        route?.Trim().ToLowerInvariant() switch
        {
            "inbox" => "inbox",
            "para" => "para",
            _ => "home",
        };
}
