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
    private int _configurationVersion;
    private int _loadVersion;

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
    [NotifyPropertyChangedFor(nameof(CanActOnCurrentItem))]
    [NotifyCanExecuteChangedFor(nameof(MarkReviewedCommand))]
    [NotifyCanExecuteChangedFor(nameof(DeferCommand))]
    [NotifyCanExecuteChangedFor(nameof(ArchiveCommand))]
    public partial IReadOnlyList<ReviewQueueItem> Items { get; set; } = [];

    [ObservableProperty]
    public partial string Title { get; set; } = "Inbox review";

    [ObservableProperty]
    public partial string ReturnRoute { get; set; } = "home";

    [ObservableProperty]
    public partial int ChangedCount { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsComplete))]
    [NotifyPropertyChangedFor(nameof(CanActOnCurrentItem))]
    [NotifyCanExecuteChangedFor(nameof(MarkReviewedCommand))]
    [NotifyCanExecuteChangedFor(nameof(DeferCommand))]
    [NotifyCanExecuteChangedFor(nameof(ArchiveCommand))]
    public partial bool IsLoading { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    [NotifyPropertyChangedFor(nameof(IsComplete))]
    [NotifyPropertyChangedFor(nameof(CanActOnCurrentItem))]
    [NotifyCanExecuteChangedFor(nameof(MarkReviewedCommand))]
    [NotifyCanExecuteChangedFor(nameof(DeferCommand))]
    [NotifyCanExecuteChangedFor(nameof(ArchiveCommand))]
    public partial string? ErrorMessage { get; set; }

    [ObservableProperty]
    public partial string StatusMessage { get; set; } = string.Empty;

    public ReviewQueueItem? CurrentItem => Items.FirstOrDefault();

    public int RemainingCount => Items.Count;

    public bool IsComplete => !IsLoading && Items.Count == 0 && !HasError;

    public bool HasCurrentItem => CurrentItem is not null;

    public bool CanMoveCurrentItem => CurrentItem?.BrainItemId is not null;

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public bool CanActOnCurrentItem => HasCurrentItem && !IsLoading && !HasError;

    public void Configure(
        ReviewQueueKind queueKind,
        ReviewScopeKind? scopeKind,
        Guid? scopeId,
        string? returnRoute)
    {
        _configurationVersion++;
        _queueKind = queueKind;
        _scopeKind = scopeKind;
        _scopeId = scopeId;
        Title = queueKind == ReviewQueueKind.Inbox
            ? "Inbox review"
            : scopeKind is null ? "Due PARA review" : "Workspace review";
        ReturnRoute = NormalizeRoute(returnRoute);
        Items = [];
        ChangedCount = 0;
        IsLoading = false;
        StatusMessage = string.Empty;
        ErrorMessage = null;
    }

    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        var configurationVersion = _configurationVersion;
        var loadVersion = ++_loadVersion;
        var query = CurrentQuery();
        IsLoading = true;
        ErrorMessage = null;
        Items = [];
        try
        {
            var items = await _useCase.GetQueueAsync(query, cancellationToken);
            if (configurationVersion != _configurationVersion ||
                loadVersion != _loadVersion)
            {
                return;
            }

            Items = items;
            StatusMessage = Items.Count == 0
                ? "Review complete."
                : $"{Items.Count} item{(Items.Count == 1 ? "" : "s")} remaining.";
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            if (configurationVersion == _configurationVersion &&
                loadVersion == _loadVersion)
            {
                ErrorMessage = $"Review could not be loaded. {exception.Message}";
            }
        }
        finally
        {
            if (configurationVersion == _configurationVersion &&
                loadVersion == _loadVersion)
            {
                IsLoading = false;
            }
        }
    }

    [RelayCommand(CanExecute = nameof(CanMakeDecision))]
    private Task MarkReviewedAsync(CancellationToken cancellationToken) =>
        DecideAsync(
            (item, now) => _useCase.MarkReviewedAsync(
                new ReviewDecisionCommand(item.TargetKind, item.TargetId, now),
                cancellationToken),
            "Marked reviewed.",
            cancellationToken);

    [RelayCommand(CanExecute = nameof(CanMakeDecision))]
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

    [RelayCommand(CanExecute = nameof(CanMakeDecision))]
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

        var configurationVersion = _configurationVersion;
        var query = CurrentQuery();
        var decisionSaved = false;
        IsLoading = true;
        ErrorMessage = null;
        try
        {
            await decide(current, _now());
            decisionSaved = true;
            if (configurationVersion != _configurationVersion)
            {
                return;
            }

            ChangedCount++;
            StatusMessage = successMessage;
            Items = [];
            Items = await _useCase.GetQueueAsync(query, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            if (decisionSaved && configurationVersion == _configurationVersion)
            {
                ErrorMessage =
                    "The decision was saved, but the review queue refresh was canceled. Retry to refresh the queue.";
            }
        }
        catch (Exception exception)
        {
            if (configurationVersion == _configurationVersion)
            {
                ErrorMessage = decisionSaved
                    ? $"The decision was saved, but the review queue could not be refreshed. Retry to refresh the queue. {exception.Message}"
                    : $"The decision was not saved. Your place is unchanged. {exception.Message}";
            }
        }
        finally
        {
            if (configurationVersion == _configurationVersion)
            {
                IsLoading = false;
            }
        }
    }

    private bool CanMakeDecision() => CanActOnCurrentItem;

    private GetReviewQueueQuery CurrentQuery() =>
        new(_queueKind, _now(), _scopeKind, _scopeId);

    private static string NormalizeRoute(string? route) =>
        route?.Trim().ToLowerInvariant() switch
        {
            "inbox" => "inbox",
            "para" => "para",
            _ => "home",
        };
}
