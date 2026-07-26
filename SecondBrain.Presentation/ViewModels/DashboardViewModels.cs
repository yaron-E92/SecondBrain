using System.ComponentModel;
using System.Runtime.CompilerServices;
using SecondBrain.Application.UseCases;

namespace SecondBrain.Presentation.ViewModels;

public sealed class DashboardViewModel(
    DashboardUseCase dashboardUseCase,
    InboxViewModel inboxViewModel) : ObservableViewModel
{
    private IReadOnlyList<DashboardProject> activeProjects = [];
    private IReadOnlyList<DashboardItem> favorites = [];
    private IReadOnlyList<DashboardItem> recentItems = [];
    private IReadOnlyList<DashboardModuleSlot> moduleSlots = [];
    private string captureText = string.Empty;
    private string captureStatus = string.Empty;
    private bool isLoading;
    private string? errorMessage;

    public IReadOnlyList<DashboardItem> InboxItems => inboxViewModel.Items;

    public IReadOnlyList<DashboardProject> ActiveProjects
    {
        get => activeProjects;
        private set => SetProperty(ref activeProjects, value);
    }

    public IReadOnlyList<DashboardItem> Favorites
    {
        get => favorites;
        private set => SetProperty(ref favorites, value);
    }

    public IReadOnlyList<DashboardItem> RecentItems
    {
        get => recentItems;
        private set => SetProperty(ref recentItems, value);
    }

    public IReadOnlyList<DashboardModuleSlot> ModuleSlots
    {
        get => moduleSlots;
        private set => SetProperty(ref moduleSlots, value);
    }

    public string CaptureText
    {
        get => captureText;
        set => SetProperty(ref captureText, value);
    }

    public string CaptureStatus
    {
        get => captureStatus;
        private set => SetProperty(ref captureStatus, value);
    }

    public bool IsLoading
    {
        get => isLoading;
        private set => SetProperty(ref isLoading, value);
    }

    public string? ErrorMessage
    {
        get => errorMessage;
        private set
        {
            if (SetProperty(ref errorMessage, value))
            {
                OnPropertyChanged(nameof(HasError));
            }
        }
    }

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public bool IsInboxEmpty => InboxItems.Count == 0;

    public bool AreProjectsEmpty => ActiveProjects.Count == 0;

    public bool AreFavoritesEmpty => Favorites.Count == 0;

    public bool AreRecentItemsEmpty => RecentItems.Count == 0;

    public bool AreModuleSlotsEmpty => ModuleSlots.Count == 0;

    public async Task LoadAsync(CancellationToken cancellationToken = default)
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

    public async Task CaptureAsync(CancellationToken cancellationToken = default)
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
        OnPropertyChanged(nameof(AreProjectsEmpty));
        OnPropertyChanged(nameof(AreFavoritesEmpty));
        OnPropertyChanged(nameof(AreRecentItemsEmpty));
        OnPropertyChanged(nameof(AreModuleSlotsEmpty));
    }
}

public sealed class InboxViewModel(DashboardUseCase dashboardUseCase)
    : ObservableViewModel
{
    private IReadOnlyList<DashboardItem> items = [];
    private bool isLoading;
    private string? errorMessage;

    public IReadOnlyList<DashboardItem> Items
    {
        get => items;
        private set
        {
            if (SetProperty(ref items, value))
            {
                OnPropertyChanged(nameof(IsEmpty));
            }
        }
    }

    public bool IsEmpty => Items.Count == 0;

    public bool IsLoading
    {
        get => isLoading;
        private set => SetProperty(ref isLoading, value);
    }

    public string? ErrorMessage
    {
        get => errorMessage;
        private set
        {
            if (SetProperty(ref errorMessage, value))
            {
                OnPropertyChanged(nameof(HasError));
            }
        }
    }

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public async Task LoadAsync(CancellationToken cancellationToken = default)
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

public abstract class ObservableViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected bool SetProperty<T>(
        ref T field,
        T value,
        [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    protected void OnPropertyChanged(
        [CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(
            this,
            new PropertyChangedEventArgs(propertyName));
}
