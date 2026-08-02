using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SecondBrain.Application.UseCases;

namespace SecondBrain.Presentation.ViewModels;

public sealed partial class DashboardViewModel(
    DashboardUseCase dashboardUseCase,
    InboxViewModel inboxViewModel) : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AreProjectsEmpty))]
    private IReadOnlyList<DashboardProject> activeProjects = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AreFavoritesEmpty))]
    private IReadOnlyList<DashboardItem> favorites = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AreRecentItemsEmpty))]
    private IReadOnlyList<DashboardItem> recentItems = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AreModuleSlotsEmpty))]
    private IReadOnlyList<DashboardModuleSlot> moduleSlots = [];

    [ObservableProperty]
    private string captureText = string.Empty;

    [ObservableProperty]
    private string captureStatus = string.Empty;

    [ObservableProperty]
    private bool isLoading;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string? errorMessage;

    public IReadOnlyList<DashboardItem> InboxItems => inboxViewModel.Items;

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public bool IsInboxEmpty => InboxItems.Count == 0;

    public bool AreProjectsEmpty => ActiveProjects.Count == 0;

    public bool AreFavoritesEmpty => Favorites.Count == 0;

    public bool AreRecentItemsEmpty => RecentItems.Count == 0;

    public bool AreModuleSlotsEmpty => ModuleSlots.Count == 0;

    [RelayCommand]
    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        IsLoading = true;
        ErrorMessage = null;

        try
        {
            var snapshot = await dashboardUseCase.GetDashboardAsync(
                new GetDashboardQuery(),
                cancellationToken);
            Apply(snapshot);
        }
        catch (Exception exception)
        {
            ErrorMessage =
                $"Dashboard could not be loaded. {exception.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task CaptureAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(CaptureText))
        {
            CaptureStatus = "Type something to capture.";
            return;
        }

        IsLoading = true;
        ErrorMessage = null;
        CaptureStatus = "Saving locally...";

        try
        {
            await dashboardUseCase.QuickCaptureAsync(
                new QuickCaptureCommand(CaptureText),
                cancellationToken);
            CaptureText = string.Empty;
            CaptureStatus = "Captured to Inbox.";
            var snapshot = await dashboardUseCase.GetDashboardAsync(
                new GetDashboardQuery(),
                cancellationToken);
            Apply(snapshot);
        }
        catch (Exception exception)
        {
            CaptureStatus = string.Empty;
            ErrorMessage =
                $"Capture could not be saved. {exception.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void Apply(DashboardSnapshot snapshot)
    {
        ActiveProjects = snapshot.ActiveProjects;
        Favorites = snapshot.Favorites;
        RecentItems = snapshot.RecentItems;
        ModuleSlots = snapshot.ModuleSlots;
        inboxViewModel.Replace(snapshot.Inbox);
        OnPropertyChanged(nameof(InboxItems));
        OnPropertyChanged(nameof(IsInboxEmpty));
    }
}

public sealed partial class InboxViewModel(DashboardUseCase dashboardUseCase)
    : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    private IReadOnlyList<DashboardItem> items = [];

    [ObservableProperty]
    private bool isLoading;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string? errorMessage;

    public bool IsEmpty => Items.Count == 0;

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    [RelayCommand]
    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        IsLoading = true;
        ErrorMessage = null;

        try
        {
            Replace(await dashboardUseCase.GetInboxAsync(
                new GetInboxQuery(),
                cancellationToken));
        }
        catch (Exception exception)
        {
            ErrorMessage = $"Inbox could not be loaded. {exception.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    internal void Replace(IReadOnlyList<DashboardItem> newItems) =>
        Items = newItems;
}
