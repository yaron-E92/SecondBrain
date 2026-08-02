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
    public partial IReadOnlyList<DashboardProject> ActiveProjects { get; set; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AreFavoritesEmpty))]
    public partial IReadOnlyList<DashboardItem> Favorites { get; set; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AreRecentItemsEmpty))]
    public partial IReadOnlyList<DashboardItem> RecentItems { get; set; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AreModuleSlotsEmpty))]
    public partial IReadOnlyList<DashboardModuleSlot> ModuleSlots { get; set; } = [];

    [ObservableProperty]
    public partial string CaptureText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string CaptureStatus { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    public partial string? ErrorMessage { get; set; }

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
    public partial IReadOnlyList<DashboardItem> Items { get; set; } = [];

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    public partial string? ErrorMessage { get; set; }

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
