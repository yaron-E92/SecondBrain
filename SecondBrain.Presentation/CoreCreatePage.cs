using SecondBrain.Application.Ports;
using SecondBrain.Domain.Entities;
using SecondBrain.Domain.ValueObjects;
using SecondBrain.Presentation.ViewModels;

namespace SecondBrain.Presentation;

public sealed class CoreCreatePage : ContentPage, IQueryAttributable
{
    private readonly CoreEditorViewModel _viewModel;
    private readonly ICoreKnowledgeRepository _repository;
    private readonly Picker _kindPicker;
    private readonly Picker _placementPicker;
    private readonly CoreKnowledgeForm _form;
    private readonly Label _message;
    private readonly Button _startButton;
    private BrainItemKind? _pendingKind;
    private PrimaryPlacement? _pendingPlacement;
    private SecondBrainItemId? _pendingJournalId;
    private SecondBrainItemId? _deriveFromId;
    private SecondBrainItemId? _returnItemId;
    private string _returnRoute = "para";

    public CoreCreatePage(
        CoreEditorViewModel viewModel,
        ICoreKnowledgeRepository repository)
    {
        _viewModel = viewModel;
        _repository = repository;
        BindingContext = viewModel;
        Title = "Create knowledge";
        BackgroundColor = SecondBrainVisual.Background;

        _kindPicker = new Picker
        {
            Title = "What are you creating?",
            ItemsSource = Enum.GetValues<BrainItemKind>(),
            SelectedItem = BrainItemKind.Note,
            MinimumHeightRequest = 44,
            AutomationId = "CreateKnowledgeKind",
        };
        _placementPicker = new Picker
        {
            Title = "Choose its home",
            ItemDisplayBinding = new Binding("Name.Value"),
            MinimumHeightRequest = 44,
            AutomationId = "CreateKnowledgePlacement",
        };

        _startButton = SecondBrainVisual.PrimaryButton("Start creating", "CreateKnowledgeStart");
        _startButton.Clicked += async (_, _) => await BeginSelectedAsync();

        _message = SecondBrainVisual.Body(string.Empty);
        _message.TextColor = SecondBrainVisual.Danger;

        _form = new CoreKnowledgeForm(viewModel)
        {
            IsVisible = false,
        };

        var save = SecondBrainVisual.PrimaryButton("Create item", "CreateKnowledgeSave");
        save.Clicked += async (_, _) => await SaveAsync();
        var cancel = SecondBrainVisual.SecondaryButton("Cancel", "CreateKnowledgeCancel");
        cancel.Clicked += async (_, _) => await CancelAsync();
        var actions = new HorizontalStackLayout
        {
            Spacing = 10,
            HorizontalOptions = LayoutOptions.End,
            Children = { cancel, save },
        };
        actions.SetBinding(IsVisibleProperty, nameof(viewModel.IsNew));

        var busy = new ActivityIndicator { Color = SecondBrainVisual.Accent };
        busy.SetBinding(ActivityIndicator.IsRunningProperty, nameof(viewModel.IsBusy));
        busy.SetBinding(IsVisibleProperty, nameof(viewModel.IsBusy));
        var error = new Label { TextColor = SecondBrainVisual.Danger };
        error.SetBinding(Label.TextProperty, nameof(viewModel.ErrorMessage));
        error.SetBinding(IsVisibleProperty, nameof(viewModel.HasError));

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                MaximumWidthRequest = 900,
                HorizontalOptions = LayoutOptions.Fill,
                Padding = new Thickness(24, 26),
                Spacing = 18,
                Children =
                {
                    SecondBrainVisual.Eyebrow("Create"),
                    SecondBrainVisual.PageTitle("Create knowledge"),
                    SecondBrainVisual.Body("Choose what this is and where it belongs, then focus only on creating it."),
                    SecondBrainVisual.Card(
                        SecondBrainVisual.SectionTitle("Type and home"),
                        _kindPicker,
                        _placementPicker,
                        _startButton,
                        _message),
                    _form,
                    busy,
                    error,
                    actions,
                },
            },
        };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadChoicesAndApplyNavigationAsync();
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        ResetSurface();
        _pendingKind = TryEnum<BrainItemKind>(query, "itemKind");
        _pendingPlacement = TryPlacement(query);
        _pendingJournalId = TryId(query, "journalId");
        _deriveFromId = TryId(query, "deriveFromId");
        _returnItemId = TryId(query, "returnItemId");
        _returnRoute = NormalizeReturnRoute(Value(query, "returnRoute"));
    }

    private void ResetSurface()
    {
        _viewModel.CancelCommand.Execute(null);
        _form.IsVisible = false;
        _kindPicker.IsEnabled = true;
        _placementPicker.IsEnabled = true;
        _startButton.IsVisible = true;
        _message.Text = string.Empty;
        _pendingKind = null;
        _pendingPlacement = null;
        _pendingJournalId = null;
        _deriveFromId = null;
        _returnItemId = null;
        _returnRoute = "para";
    }

    private async Task LoadChoicesAndApplyNavigationAsync()
    {
        try
        {
            var state = await _repository.LoadStateAsync();
            var placements = state.Areas
                .Where(area => !area.IsArchived)
                .Cast<object>()
                .Concat(state.Projects.Where(project => !project.IsArchived))
                .Concat(state.ResourceTopics.Where(topic => !topic.IsArchived))
                .ToArray();
            _placementPicker.ItemsSource = placements;
            _placementPicker.SelectedIndex = placements.Length > 0 ? 0 : -1;
            _form.SetJournals(state.Journals, _pendingJournalId);

            if (placements.Length == 0)
            {
                _message.Text = "Create a Project, Area, or Resource Topic in Browse before adding established knowledge.";
                _startButton.IsEnabled = false;
                return;
            }

            _startButton.IsEnabled = true;
            _message.Text = string.Empty;

            if (_pendingKind is { } kind)
            {
                _kindPicker.SelectedItem = kind;
            }
            if (_pendingPlacement is { } placement)
            {
                SelectPlacement(placement);
            }

            if (_pendingKind is not null || _pendingPlacement is not null || _deriveFromId is not null)
            {
                await BeginSelectedAsync();
            }
        }
        catch (Exception exception)
        {
            _message.Text = $"Creation choices could not be loaded. {exception.Message}";
        }
    }

    private async Task BeginSelectedAsync()
    {
        if (_kindPicker.SelectedItem is not BrainItemKind kind ||
            TryGetPlacement(_placementPicker.SelectedItem) is not { } placement)
        {
            _message.Text = "Choose a content type and an active home first.";
            return;
        }

        if (_viewModel.IsDirty &&
            !await DisplayAlertAsync(
                "Start over?",
                "Your unsaved creation draft will be replaced.",
                "Start over",
                "Keep editing"))
        {
            return;
        }

        _message.Text = string.Empty;
        if (_deriveFromId is { } sourceId)
        {
            await _viewModel.LoadAsync(sourceId);
            if (_viewModel.HasError)
            {
                _message.Text = _viewModel.ErrorMessage ?? "The source capture could not be loaded.";
                return;
            }

            try
            {
                _viewModel.BeginDerivation(kind, [], placement);
            }
            catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
            {
                _message.Text = exception.Message;
                return;
            }
        }
        else
        {
            _viewModel.BeginCreate(kind, placement);
        }

        if (kind == BrainItemKind.JournalEntry)
        {
            _viewModel.JournalEntry.JournalId =
                (_form.JournalPicker.SelectedItem as Journal)?.Id;
            _viewModel.JournalEntry.OccurrenceDate ??=
                DateOnly.FromDateTime(DateTime.Today);
        }

        _form.SyncDates();
        _form.IsVisible = true;
        _kindPicker.IsEnabled = false;
        _placementPicker.IsEnabled = false;
        _startButton.IsVisible = false;
    }

    private async Task SaveAsync()
    {
        await _viewModel.SaveCommand.ExecuteAsync(null);
        if (_viewModel.HasError)
        {
            return;
        }

        if (_returnItemId is { } itemId)
        {
            await Shell.Current.GoToAsync(
                "//editor",
                new Dictionary<string, object>
                {
                    ["itemId"] = itemId.Value.ToString(),
                    ["returnRoute"] = _returnRoute,
                });
            return;
        }

        await Shell.Current.GoToAsync($"//{_returnRoute}");
    }

    private async Task CancelAsync()
    {
        if (_viewModel.IsDirty &&
            !await DisplayAlertAsync(
                "Discard draft?",
                "This creation draft has not been saved.",
                "Discard",
                "Keep editing"))
        {
            return;
        }

        _viewModel.CancelCommand.Execute(null);
        if (_returnItemId is { } itemId)
        {
            await Shell.Current.GoToAsync(
                "//editor",
                new Dictionary<string, object>
                {
                    ["itemId"] = itemId.Value.ToString(),
                    ["returnRoute"] = _returnRoute,
                });
            return;
        }

        await Shell.Current.GoToAsync($"//{_returnRoute}");
    }

    private bool SelectPlacement(PrimaryPlacement placement)
    {
        var choices = _placementPicker.ItemsSource?.Cast<object>().ToArray() ?? [];
        var match = choices.FirstOrDefault(candidate => TryGetPlacement(candidate) == placement);
        _placementPicker.SelectedItem = match;
        return match is not null;
    }

    private static PrimaryPlacement? TryPlacement(IDictionary<string, object> query)
    {
        var kindValue = Value(query, "contextKind");
        var idValue = Value(query, "contextId");
        if (!Enum.TryParse<ParaContextKind>(kindValue, true, out var kind) ||
            !Guid.TryParse(idValue, out var id) || id == Guid.Empty)
        {
            return null;
        }

        return kind switch
        {
            ParaContextKind.Project => PrimaryPlacement.InProject(new ProjectId(id)),
            ParaContextKind.Area => PrimaryPlacement.InArea(new AreaId(id)),
            ParaContextKind.ResourceTopic => PrimaryPlacement.InResourceTopic(new ResourceTopicId(id)),
            _ => null,
        };
    }

    private static PrimaryPlacement? TryGetPlacement(object? value) =>
        value switch
        {
            Area area => PrimaryPlacement.InArea(area.Id),
            Project project => PrimaryPlacement.InProject(project.Id),
            ResourceTopic topic => PrimaryPlacement.InResourceTopic(topic.Id),
            _ => null,
        };

    private static T? TryEnum<T>(IDictionary<string, object> query, string key)
        where T : struct, Enum =>
        Enum.TryParse<T>(Value(query, key), true, out var value) ? value : null;

    private static SecondBrainItemId? TryId(IDictionary<string, object> query, string key) =>
        Guid.TryParse(Value(query, key), out var id) && id != Guid.Empty
            ? new SecondBrainItemId(id)
            : null;

    private static string? Value(IDictionary<string, object> query, string key) =>
        query.TryGetValue(key, out var value) && value is not null
            ? Uri.UnescapeDataString(value.ToString() ?? string.Empty)
            : null;

    private static string NormalizeReturnRoute(string? route) =>
        route?.Trim().ToLowerInvariant() switch
        {
            "home" => "home",
            "inbox" => "inbox",
            "search" => "search",
            "journals" => "journals",
            "review" => "review",
            "editor" => "editor",
            _ => "para",
        };
}
