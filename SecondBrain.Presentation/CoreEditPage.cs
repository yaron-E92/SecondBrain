using SecondBrain.Application.Ports;
using SecondBrain.Domain.Entities;
using SecondBrain.Domain.ValueObjects;
using SecondBrain.Presentation.ViewModels;

namespace SecondBrain.Presentation;

public sealed class CoreEditPage : ContentPage, IQueryAttributable
{
    private readonly CoreEditorViewModel _viewModel;
    private readonly ICoreKnowledgeRepository _repository;
    private readonly CoreKnowledgeForm _form;
    private readonly Label _message;
    private readonly Label _homeLabel;
    private readonly VerticalStackLayout _relationships;
    private readonly VerticalStackLayout _captureActions;
    private SecondBrainItemId? _pendingItemId;
    private string _returnRoute = "para";
    private BrainItem? _currentItem;

    public CoreEditPage(
        CoreEditorViewModel viewModel,
        ICoreKnowledgeRepository repository)
    {
        _viewModel = viewModel;
        _repository = repository;
        BindingContext = viewModel;
        Title = "Edit knowledge";
        BackgroundColor = SecondBrainVisual.Background;

        _message = SecondBrainVisual.Body("Open an item from Browse, Search, Review, or Home to edit it here.");
        _homeLabel = SecondBrainVisual.Body(string.Empty);

        var openHome = SecondBrainVisual.SecondaryButton("Open its home", "EditKnowledgeOpenHome");
        openHome.Clicked += async (_, _) => await OpenHomeAsync();

        _form = new CoreKnowledgeForm(viewModel)
        {
            IsVisible = false,
        };

        _relationships = new VerticalStackLayout { Spacing = 8 };
        var relationshipCard = SecondBrainVisual.Card(
            SecondBrainVisual.Eyebrow("Context"),
            SecondBrainVisual.SectionTitle("Related knowledge"),
            _relationships);
        relationshipCard.SetBinding(IsVisibleProperty, nameof(viewModel.IsNew), converter: new InvertedBooleanConverter());

        _captureActions = new VerticalStackLayout
        {
            Spacing = 10,
            IsVisible = false,
        };
        var deriveNote = SecondBrainVisual.SecondaryButton("Create Note from this capture", "EditKnowledgeDeriveNote");
        deriveNote.Clicked += async (_, _) => await CreateFromCaptureAsync(BrainItemKind.Note);
        var deriveResource = SecondBrainVisual.SecondaryButton("Create Resource from this capture", "EditKnowledgeDeriveResource");
        deriveResource.Clicked += async (_, _) => await CreateFromCaptureAsync(BrainItemKind.ResourceArtifact);
        _captureActions.Children.Add(SecondBrainVisual.Body(
            "Use this capture as source material, but create the authored item in its own focused flow."));
        _captureActions.Children.Add(new HorizontalStackLayout
        {
            Spacing = 10,
            Children = { deriveNote, deriveResource },
        });

        var save = SecondBrainVisual.PrimaryButton("Save changes", "EditKnowledgeSave");
        save.Clicked += async (_, _) => await SaveAsync();
        var cancel = SecondBrainVisual.SecondaryButton("Discard changes", "EditKnowledgeCancel");
        cancel.Clicked += async (_, _) => await CancelAsync();

        var busy = new ActivityIndicator { Color = SecondBrainVisual.Accent };
        busy.SetBinding(ActivityIndicator.IsRunningProperty, nameof(viewModel.IsBusy));
        busy.SetBinding(IsVisibleProperty, nameof(viewModel.IsBusy));
        var error = new Label { TextColor = SecondBrainVisual.Danger };
        error.SetBinding(Label.TextProperty, nameof(viewModel.ErrorMessage));
        error.SetBinding(IsVisibleProperty, nameof(viewModel.HasError));
        var dirty = new Label
        {
            Text = "Unsaved changes",
            TextColor = SecondBrainVisual.Warning,
            FontAttributes = FontAttributes.Bold,
        };
        dirty.SetBinding(IsVisibleProperty, nameof(viewModel.IsDirty));

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                MaximumWidthRequest = 1000,
                HorizontalOptions = LayoutOptions.Fill,
                Padding = new Thickness(24, 26),
                Spacing = 18,
                Children =
                {
                    SecondBrainVisual.Eyebrow("Edit"),
                    SecondBrainVisual.PageTitle("Edit knowledge"),
                    SecondBrainVisual.Body("Keep one established item useful. Creation is a separate flow."),
                    _message,
                    SecondBrainVisual.Card(
                        SecondBrainVisual.Eyebrow("Home"),
                        _homeLabel,
                        openHome),
                    _form,
                    SecondBrainVisual.Card(
                        SecondBrainVisual.Eyebrow("From this capture"),
                        _captureActions),
                    relationshipCard,
                    dirty,
                    busy,
                    error,
                    new HorizontalStackLayout
                    {
                        Spacing = 10,
                        HorizontalOptions = LayoutOptions.End,
                        Children = { cancel, save },
                    },
                },
            },
        };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_pendingItemId is { } itemId)
        {
            await LoadAsync(itemId);
        }
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        _pendingItemId = TryId(query, "itemId");
        _returnRoute = NormalizeReturnRoute(Value(query, "returnRoute"));
    }

    private async Task LoadAsync(SecondBrainItemId itemId)
    {
        try
        {
            var state = await _repository.LoadStateAsync();
            var item = state.BrainItems.SingleOrDefault(candidate => candidate.Id == itemId);
            if (item is null)
            {
                _message.Text = "That item is no longer available. Return to the previous view and refresh.";
                _form.IsVisible = false;
                return;
            }

            var journalId = state.Journals
                .FirstOrDefault(journal => journal.Entries.Any(entry => entry.Id == item.Id))?
                .Id;
            _form.SetJournals(state.Journals, journalId);
            await _viewModel.LoadAsync(item.Id, journalId);
            if (_viewModel.HasError)
            {
                _message.Text = _viewModel.ErrorMessage ?? "The item could not be loaded.";
                return;
            }

            _currentItem = item;
            _pendingItemId = null;
            _message.Text = string.Empty;
            _homeLabel.Text = PlacementLabel(item.PrimaryPlacement);
            _form.SyncDates();
            _form.IsVisible = true;
            _captureActions.IsVisible = item.Kind == BrainItemKind.KnowledgeCapture && !item.IsArchived;
            BuildRelationships(state.BrainItems, item);
        }
        catch (Exception exception)
        {
            _message.Text = $"The editor could not be loaded. {exception.Message}";
        }
    }

    private async Task SaveAsync()
    {
        if (_currentItem is null)
        {
            return;
        }

        await _viewModel.SaveCommand.ExecuteAsync(null);
        if (_viewModel.HasError)
        {
            return;
        }

        if (_viewModel.LastSavedItem is { } saved)
        {
            _currentItem = saved;
            _homeLabel.Text = PlacementLabel(saved.PrimaryPlacement);
        }

        await NavigateBackAsync();
    }

    private async Task CancelAsync()
    {
        if (_viewModel.IsDirty &&
            !await DisplayAlertAsync(
                "Discard changes?",
                "Your unsaved edits will be lost.",
                "Discard",
                "Keep editing"))
        {
            return;
        }

        _viewModel.CancelCommand.Execute(null);
        await NavigateBackAsync();
    }

    private async Task NavigateBackAsync()
    {
        if (_returnRoute == "editor")
        {
            await Shell.Current.GoToAsync("//para");
            return;
        }

        await Shell.Current.GoToAsync($"//{_returnRoute}");
    }

    private async Task OpenHomeAsync()
    {
        if (_currentItem is null)
        {
            return;
        }

        var (kind, id) = ContextFor(_currentItem.PrimaryPlacement);
        await Shell.Current.GoToAsync(
            "//para",
            new Dictionary<string, object>
            {
                ["contextKind"] = kind.ToString(),
                ["contextId"] = id.ToString(),
                ["returnRoute"] = _returnRoute,
            });
    }

    private async Task CreateFromCaptureAsync(BrainItemKind kind)
    {
        if (_currentItem is not { Kind: BrainItemKind.KnowledgeCapture } source)
        {
            return;
        }

        var (contextKind, contextId) = ContextFor(source.PrimaryPlacement);
        await Shell.Current.GoToAsync(
            "//create",
            new Dictionary<string, object>
            {
                ["itemKind"] = kind.ToString(),
                ["contextKind"] = contextKind.ToString(),
                ["contextId"] = contextId.ToString(),
                ["deriveFromId"] = source.Id.Value.ToString(),
                ["returnItemId"] = source.Id.Value.ToString(),
                ["returnRoute"] = _returnRoute,
            });
    }

    private void BuildRelationships(IEnumerable<BrainItem> items, BrainItem current)
    {
        var all = items.ToArray();
        IEnumerable<SecondBrainItemId> linkedIds = current.Kind switch
        {
            BrainItemKind.KnowledgeCapture => current.DerivedItemLinks,
            BrainItemKind.ResourceArtifact => current.ProvenanceSourceLinks,
            _ => all
                .Where(item =>
                    item.Kind == BrainItemKind.KnowledgeCapture &&
                    item.DerivedItemLinks.Contains(current.Id))
                .Select(item => item.Id),
        };
        var links = linkedIds
            .Distinct()
            .Select(id => all.SingleOrDefault(item => item.Id == id))
            .Where(item => item is not null)
            .Cast<BrainItem>()
            .OrderBy(item => item.Title)
            .ToArray();

        _relationships.Children.Clear();
        if (links.Length == 0)
        {
            _relationships.Children.Add(SecondBrainVisual.Body("No source or derived links yet."));
            return;
        }

        foreach (var item in links)
        {
            var open = SecondBrainVisual.QuietButton(item.Title);
            open.Clicked += async (_, _) =>
                await Shell.Current.GoToAsync(
                    "//editor",
                    new Dictionary<string, object>
                    {
                        ["itemId"] = item.Id.Value.ToString(),
                        ["returnRoute"] = _returnRoute,
                    });
            _relationships.Children.Add(open);
        }
    }

    private static (ParaContextKind Kind, Guid Id) ContextFor(PrimaryPlacement placement) =>
        placement.Kind switch
        {
            PrimaryPlacementKind.Project => (ParaContextKind.Project, placement.ContextId),
            PrimaryPlacementKind.Area => (ParaContextKind.Area, placement.ContextId),
            PrimaryPlacementKind.ResourceTopic => (ParaContextKind.ResourceTopic, placement.ContextId),
            _ => throw new ArgumentOutOfRangeException(nameof(placement)),
        };

    private static string PlacementLabel(PrimaryPlacement placement) =>
        placement.Kind switch
        {
            PrimaryPlacementKind.Project => "Project home",
            PrimaryPlacementKind.Area => "Area home",
            PrimaryPlacementKind.ResourceTopic => "Resource home",
            _ => "Knowledge home",
        };

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
            "para" => "para",
            _ => "para",
        };

    private sealed class InvertedBooleanConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture) =>
            value is bool flag && !flag;

        public object ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture) =>
            throw new NotSupportedException();
    }
}
