using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SecondBrain.Application.Ports;
using SecondBrain.Application.UseCases;
using SecondBrain.Domain.Entities;
using SecondBrain.Domain.ValueObjects;

namespace SecondBrain.Presentation.ViewModels;

public sealed record InboxPlacementOption(
    string Label,
    PrimaryPlacement Placement);

public sealed partial class InboxProcessViewModel : ObservableObject
{
    private readonly CoreKnowledgeUseCases _useCases;
    private readonly ICoreKnowledgeRepository _repository;
    private readonly Func<DateTimeOffset> _utcNow;

    public InboxProcessViewModel(
        CoreKnowledgeUseCases useCases,
        ICoreKnowledgeRepository repository,
        Func<DateTimeOffset>? utcNow = null)
    {
        _useCases = useCases;
        _repository = repository;
        _utcNow = utcNow ?? (() => DateTimeOffset.UtcNow);
    }

    public SecondBrainItemId? ItemId { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanProcess))]
    public partial string Title { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanProcess))]
    public partial string Content { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasDestinations))]
    [NotifyPropertyChangedFor(nameof(CanProcess))]
    public partial IReadOnlyList<InboxPlacementOption> Destinations { get; set; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanProcess))]
    public partial InboxPlacementOption? SelectedDestination { get; set; }

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanProcess))]
    public partial bool IsSaving { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    public partial string? ErrorMessage { get; set; }

    [ObservableProperty]
    public partial string StatusMessage { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool WasProcessed { get; set; }

    public bool HasDestinations => Destinations.Count > 0;

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public bool CanProcess =>
        ItemId is not null &&
        !IsSaving &&
        !string.IsNullOrWhiteSpace(Title) &&
        !string.IsNullOrWhiteSpace(Content) &&
        SelectedDestination is not null;

    public async Task LoadAsync(
        SecondBrainItemId itemId,
        CancellationToken cancellationToken = default)
    {
        IsLoading = true;
        ErrorMessage = null;
        StatusMessage = string.Empty;
        WasProcessed = false;

        try
        {
            var itemResult = await _useCases.GetBrainItemAsync(
                new GetBrainItemQuery(itemId),
                cancellationToken);
            if (!itemResult.IsSuccess || itemResult.Value is null)
            {
                ItemId = null;
                ErrorMessage = itemResult.Error?.Message ?? "That Inbox item is no longer available.";
                return;
            }

            var item = itemResult.Value;
            ItemId = item.Id;
            Title = item.Title;
            Content = item.Content;

            var state = await _repository.LoadStateAsync(cancellationToken);
            Destinations = BuildDestinations(state);
            SelectedDestination = Destinations.FirstOrDefault(option =>
                option.Placement == item.PrimaryPlacement)
                ?? Destinations.FirstOrDefault();

            StatusMessage = HasDestinations
                ? "Choose the home this thought belongs in. Processing moves it out of Inbox."
                : "Create a Project, Area, or Resource Topic before processing this item.";
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            ItemId = null;
            ErrorMessage = $"This Inbox item could not be loaded. {exception.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task ProcessAsync(CancellationToken cancellationToken)
    {
        if (!CanProcess || ItemId is not { } itemId || SelectedDestination is not { } destination)
        {
            ErrorMessage = HasDestinations
                ? "Add a title and content, then choose where this thought belongs."
                : "Create a Project, Area, or Resource Topic before processing this item.";
            return;
        }

        IsSaving = true;
        ErrorMessage = null;
        StatusMessage = "Processing locally…";
        WasProcessed = false;

        try
        {
            var updated = await _useCases.UpdateBrainItemAsync(
                new UpdateBrainItemCommand(
                    itemId,
                    Title,
                    Content,
                    _utcNow()),
                cancellationToken);
            if (!updated.IsSuccess || updated.Value is null)
            {
                ErrorMessage = updated.Error?.Message ?? "The item could not be updated.";
                StatusMessage = "Nothing was moved. Correct the problem and retry.";
                return;
            }

            var saved = updated.Value;
            if (saved.PrimaryPlacement != destination.Placement)
            {
                var moved = await _useCases.MoveBrainItemAsync(
                    new MoveBrainItemCommand(
                        itemId,
                        destination.Placement,
                        _utcNow()),
                    cancellationToken);
                if (!moved.IsSuccess || moved.Value is null)
                {
                    ErrorMessage = moved.Error?.Message ?? "The item could not be moved.";
                    StatusMessage = "The content was saved, but the item is still in its previous home.";
                    return;
                }
            }

            WasProcessed = true;
            StatusMessage = $"Processed into {destination.Label}.";
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            ErrorMessage = $"The Inbox item could not be processed. {exception.Message}";
            StatusMessage = "No navigation occurred. Review the item and retry.";
        }
        finally
        {
            IsSaving = false;
        }
    }

    private static IReadOnlyList<InboxPlacementOption> BuildDestinations(CoreKnowledgeState state)
    {
        var projects = state.Projects
            .Where(project => !project.IsArchived)
            .OrderBy(project => project.Name.Value, StringComparer.OrdinalIgnoreCase)
            .Select(project => new InboxPlacementOption(
                $"Project · {project.Name.Value}",
                PrimaryPlacement.InProject(project.Id)));

        var areas = state.Areas
            .Where(area =>
                !area.IsArchived &&
                !string.Equals(area.Name.Value, "Inbox", StringComparison.OrdinalIgnoreCase))
            .OrderBy(area => area.Name.Value, StringComparer.OrdinalIgnoreCase)
            .Select(area => new InboxPlacementOption(
                $"Area · {area.Name.Value}",
                PrimaryPlacement.InArea(area.Id)));

        var resources = state.ResourceTopics
            .Where(topic => !topic.IsArchived)
            .OrderBy(topic => topic.Name.Value, StringComparer.OrdinalIgnoreCase)
            .Select(topic => new InboxPlacementOption(
                $"Resource · {topic.Name.Value}",
                PrimaryPlacement.InResourceTopic(topic.Id)));

        return [.. projects, .. areas, .. resources];
    }
}
