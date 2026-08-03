using SecondBrain.Application.Ports;
using SecondBrain.Domain.Entities;
using SecondBrain.Domain.ValueObjects;
using SecondBrain.Presentation.ViewModels;

namespace SecondBrain.Presentation;

public sealed class CoreEditorPage : ContentPage
{
    private readonly CoreEditorViewModel _viewModel;
    private readonly ICoreKnowledgeRepository _repository;
    private readonly Picker _kindPicker;
    private readonly Picker _placementPicker;
    private readonly Picker _itemPicker;
    private readonly Picker _journalPicker;
    private readonly Entry _reminderEntry;
    private readonly Entry _reviewDateEntry;
    private readonly Entry _occurrenceDateEntry;
    private readonly Label _catalogMessage;
    private CoreKnowledgeState? _state;

    public CoreEditorPage(
        CoreEditorViewModel viewModel,
        ICoreKnowledgeRepository repository)
    {
        _viewModel = viewModel;
        _repository = repository;
        BindingContext = viewModel;
        Title = "Editor";
        BackgroundColor = Colors.White;

        _kindPicker = EnumPicker<BrainItemKind>();
        _kindPicker.SelectedItem = BrainItemKind.Note;
        _placementPicker = new Picker { Title = "Placement" };
        _placementPicker.ItemDisplayBinding = new Binding("Name.Value");
        _itemPicker = new Picker { Title = "Existing item" };
        _itemPicker.ItemDisplayBinding = new Binding(nameof(BrainItem.Title));
        _journalPicker = new Picker { Title = "Journal" };
        _journalPicker.ItemDisplayBinding = new Binding(nameof(Journal.Title));
        _journalPicker.SelectedIndexChanged += (_, _) =>
            _viewModel.JournalEntry.JournalId =
                (_journalPicker.SelectedItem as Journal)?.Id;

        _reminderEntry = DateEntry("Optional reminder (ISO date/time)");
        _reminderEntry.TextChanged += (_, args) =>
            _viewModel.Capture.ReminderAt =
                DateTimeOffset.TryParse(args.NewTextValue, out var value)
                    ? value
                    : null;
        _reviewDateEntry = DateEntry("Optional review date (yyyy-MM-dd)");
        _reviewDateEntry.TextChanged += (_, args) =>
            _viewModel.Resource.ReviewDate =
                DateOnly.TryParse(args.NewTextValue, out var value)
                    ? value
                    : null;
        _occurrenceDateEntry = DateEntry("Occurrence date (yyyy-MM-dd)");
        _occurrenceDateEntry.TextChanged += (_, args) =>
            _viewModel.JournalEntry.OccurrenceDate =
                DateOnly.TryParse(args.NewTextValue, out var value)
                    ? value
                    : null;

        _catalogMessage = new Label
        {
            TextColor = Colors.DarkRed,
            FontSize = 13
        };

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = 20,
                Spacing = 14,
                Children =
                {
                    Header(),
                    _catalogMessage,
                    CreateSelector(),
                    EditSelector(),
                    CommonFields(),
                    NoteFields(),
                    IdeaFields(),
                    CaptureFields(),
                    ResourceFields(),
                    JournalFields(),
                    EditorState(),
                    Actions()
                }
            }
        };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await RefreshChoicesAsync();

        if (_viewModel.ItemId is null &&
            !_viewModel.IsDirty &&
            _placementPicker.SelectedItem is not null)
        {
            BeginCreate();
        }
    }

    private View CreateSelector()
    {
        var createContext = new Button
        {
            Text = "Create or manage placements",
            HorizontalOptions = LayoutOptions.Start
        };
        createContext.Clicked += async (_, _) =>
            await Shell.Current.GoToAsync("//para");

        var button = new Button
        {
            Text = "New item",
            HorizontalOptions = LayoutOptions.End
        };
        button.Clicked += async (_, _) =>
        {
            if (await ConfirmDiscardAsync())
            {
                BeginCreate();
            }
        };

        return Section(
            "Create",
            _kindPicker,
            _placementPicker,
            createContext,
            button);
    }

    private View EditSelector()
    {
        var button = new Button
        {
            Text = "Edit selected",
            HorizontalOptions = LayoutOptions.End
        };
        button.Clicked += async (_, _) =>
        {
            if (_itemPicker.SelectedItem is not BrainItem item ||
                !await ConfirmDiscardAsync())
            {
                return;
            }

            var journalId = _state?.Journals
                .FirstOrDefault(journal =>
                    journal.Entries.Any(entry => entry.Id == item.Id))?
                .Id;
            await _viewModel.LoadAsync(item.Id, journalId);
            SyncDateFields();
            SelectJournal(journalId);
        };

        return Section("Edit", _itemPicker, button);
    }

    private View CommonFields()
    {
        var title = new Entry { Placeholder = "Title" };
        title.SetBinding(Entry.TextProperty, nameof(_viewModel.Title));

        var content = new Editor
        {
            Placeholder = "Content",
            AutoSize = EditorAutoSizeOption.TextChanges,
            MinimumHeightRequest = 140
        };
        content.SetBinding(Editor.TextProperty, nameof(_viewModel.Content));

        return Section("Content", title, content);
    }

    private View NoteFields()
    {
        var kind = EnumPicker<NoteKind>();
        kind.SetBinding(
            Picker.SelectedItemProperty,
            $"{nameof(_viewModel.Note)}.{nameof(_viewModel.Note.Kind)}");
        kind.SetBinding(IsEnabledProperty, nameof(_viewModel.AreTypeFieldsEditable));
        return TypedSection("Note", nameof(_viewModel.IsNote), kind);
    }

    private View IdeaFields()
    {
        var maturity = EnumPicker<IdeaMaturity>();
        maturity.SetBinding(
            Picker.SelectedItemProperty,
            $"{nameof(_viewModel.Idea)}.{nameof(_viewModel.Idea.Maturity)}");
        return TypedSection("Idea lifecycle", nameof(_viewModel.IsIdea), maturity);
    }

    private View CaptureFields()
    {
        var sourceType = EnumPicker<CaptureSourceType>();
        sourceType.SetBinding(
            Picker.SelectedItemProperty,
            $"{nameof(_viewModel.Capture)}.{nameof(_viewModel.Capture.SourceType)}");
        sourceType.SetBinding(
            IsEnabledProperty,
            nameof(_viewModel.AreTypeFieldsEditable));

        var sourceUrl = new Entry { Placeholder = "Source URL" };
        sourceUrl.SetBinding(
            Entry.TextProperty,
            $"{nameof(_viewModel.Capture)}.{nameof(_viewModel.Capture.SourceUrl)}");
        sourceUrl.SetBinding(
            IsEnabledProperty,
            nameof(_viewModel.AreTypeFieldsEditable));

        var citation = new Entry { Placeholder = "Source citation" };
        citation.SetBinding(
            Entry.TextProperty,
            $"{nameof(_viewModel.Capture)}.{nameof(_viewModel.Capture.SourceCitation)}");
        citation.SetBinding(
            IsEnabledProperty,
            nameof(_viewModel.AreTypeFieldsEditable));
        _reminderEntry.SetBinding(
            IsEnabledProperty,
            nameof(_viewModel.AreTypeFieldsEditable));

        var processingState = EnumPicker<CaptureProcessingState>();
        processingState.SetBinding(
            Picker.SelectedItemProperty,
            $"{nameof(_viewModel.Capture)}.{nameof(_viewModel.Capture.ProcessingState)}");

        return TypedSection(
            "Capture",
            nameof(_viewModel.IsCapture),
            sourceType,
            sourceUrl,
            citation,
            _reminderEntry,
            processingState);
    }

    private View ResourceFields()
    {
        var artifactKind = EnumPicker<ResourceArtifactKind>();
        artifactKind.SetBinding(
            Picker.SelectedItemProperty,
            $"{nameof(_viewModel.Resource)}.{nameof(_viewModel.Resource.ArtifactKind)}");
        artifactKind.SetBinding(
            IsEnabledProperty,
            nameof(_viewModel.AreTypeFieldsEditable));

        var freshness = EnumPicker<ResourceFreshness>();
        freshness.SetBinding(
            Picker.SelectedItemProperty,
            $"{nameof(_viewModel.Resource)}.{nameof(_viewModel.Resource.Freshness)}");
        _reviewDateEntry.SetBinding(
            IsEnabledProperty,
            nameof(_viewModel.AreTypeFieldsEditable));

        return TypedSection(
            "Resource",
            nameof(_viewModel.IsResource),
            artifactKind,
            freshness,
            _reviewDateEntry);
    }

    private View JournalFields()
    {
        _journalPicker.SetBinding(
            IsEnabledProperty,
            nameof(_viewModel.AreTypeFieldsEditable));
        _occurrenceDateEntry.SetBinding(
            IsEnabledProperty,
            nameof(_viewModel.AreTypeFieldsEditable));
        return TypedSection(
            "Journal entry",
            nameof(_viewModel.IsJournalEntry),
            _journalPicker,
            _occurrenceDateEntry);
    }

    private View EditorState()
    {
        var dirty = new Label
        {
            Text = "Unsaved changes",
            TextColor = Colors.DarkOrange,
            FontAttributes = FontAttributes.Bold
        };
        dirty.SetBinding(IsVisibleProperty, nameof(_viewModel.IsDirty));

        var error = new Label { TextColor = Colors.DarkRed };
        error.SetBinding(Label.TextProperty, nameof(_viewModel.ErrorMessage));
        error.SetBinding(IsVisibleProperty, nameof(_viewModel.HasError));

        var busy = new ActivityIndicator { Color = Colors.DarkSlateBlue };
        busy.SetBinding(
            ActivityIndicator.IsRunningProperty,
            nameof(_viewModel.IsBusy));
        busy.SetBinding(IsVisibleProperty, nameof(_viewModel.IsBusy));

        return new VerticalStackLayout
        {
            Spacing = 6,
            Children = { dirty, error, busy }
        };
    }

    private View Actions()
    {
        var cancel = new Button { Text = "Cancel" };
        cancel.SetBinding(Button.CommandProperty, nameof(_viewModel.CancelCommand));

        var save = new Button { Text = "Save" };
        save.Clicked += async (_, _) =>
        {
            await _viewModel.SaveCommand.ExecuteAsync(null);
            if (!_viewModel.HasError)
            {
                await RefreshChoicesAsync();
                await DisplayAlertAsync("Saved", "Your changes were saved.", "OK");
            }
        };

        return new HorizontalStackLayout
        {
            Spacing = 10,
            HorizontalOptions = LayoutOptions.End,
            Children = { cancel, save }
        };
    }

    private void BeginCreate()
    {
        if (_kindPicker.SelectedItem is not BrainItemKind kind ||
            TryGetPlacement(_placementPicker.SelectedItem) is not { } placement)
        {
            _catalogMessage.Text =
                "Choose a content kind and an active placement first.";
            return;
        }

        _catalogMessage.Text = string.Empty;
        _viewModel.BeginCreate(kind, placement);
        if (kind == BrainItemKind.JournalEntry)
        {
            _journalPicker.SelectedIndex =
                _journalPicker.ItemsSource?.Count > 0 ? 0 : -1;
            _viewModel.JournalEntry.OccurrenceDate =
                DateOnly.FromDateTime(DateTime.Today);
        }

        SyncDateFields();
    }

    private async Task RefreshChoicesAsync()
    {
        try
        {
            _state = await _repository.LoadStateAsync();
            var placements = _state.Areas
                .Where(area => !area.IsArchived)
                .Cast<object>()
                .Concat(_state.Projects.Where(project => !project.IsArchived))
                .Concat(_state.ResourceTopics.Where(topic => !topic.IsArchived))
                .ToArray();
            _placementPicker.ItemsSource = placements;
            _placementPicker.SelectedIndex = placements.Length > 0 ? 0 : -1;

            var items = _state.BrainItems
                .Where(item => !item.IsArchived)
                .OrderBy(item => item.Title)
                .ToArray();
            _itemPicker.ItemsSource = items;
            _itemPicker.SelectedIndex = items.Length > 0 ? 0 : -1;

            var journals = _state.Journals.OrderBy(journal => journal.Title).ToArray();
            _journalPicker.ItemsSource = journals;
            SelectJournal(_viewModel.JournalEntry.JournalId);

            _catalogMessage.Text = placements.Length == 0
                ? "Create an Area, Project, or Resource Topic before adding content."
                : string.Empty;
        }
        catch (Exception exception)
        {
            _catalogMessage.Text =
                $"Editor choices could not be loaded. {exception.Message}";
        }
    }

    private async Task<bool> ConfirmDiscardAsync() =>
        !_viewModel.IsDirty ||
        await DisplayAlertAsync(
            "Discard changes?",
            "Your unsaved changes will be lost.",
            "Discard",
            "Keep editing");

    private void SyncDateFields()
    {
        _reminderEntry.Text = _viewModel.Capture.ReminderAt?.ToString("O") ?? string.Empty;
        _reviewDateEntry.Text =
            _viewModel.Resource.ReviewDate?.ToString("yyyy-MM-dd") ?? string.Empty;
        _occurrenceDateEntry.Text =
            _viewModel.JournalEntry.OccurrenceDate?.ToString("yyyy-MM-dd") ??
            string.Empty;
    }

    private void SelectJournal(SecondBrainItemId? journalId)
    {
        if (journalId is null)
        {
            _journalPicker.SelectedIndex = -1;
            return;
        }

        var journals = _journalPicker.ItemsSource?.Cast<Journal>().ToArray() ?? [];
        _journalPicker.SelectedItem = journals.FirstOrDefault(
            journal => journal.Id == journalId.Value);
    }

    private static PrimaryPlacement? TryGetPlacement(object? value) =>
        value switch
        {
            Area area => PrimaryPlacement.InArea(area.Id),
            Project project => PrimaryPlacement.InProject(project.Id),
            ResourceTopic topic => PrimaryPlacement.InResourceTopic(topic.Id),
            _ => null,
        };

    private static Picker EnumPicker<T>()
        where T : struct, Enum =>
        new()
        {
            Title = typeof(T).Name,
            ItemsSource = Enum.GetValues<T>()
        };

    private static Entry DateEntry(string placeholder) =>
        new()
        {
            Placeholder = placeholder,
            Keyboard = Keyboard.Text
        };

    private static View Header() =>
        new VerticalStackLayout
        {
            Children =
            {
                new Label
                {
                    Text = "Core content editor",
                    FontSize = 28,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = Colors.Black
                },
                new Label
                {
                    Text = "Create or develop typed knowledge.",
                    TextColor = Colors.DarkSlateGray
                }
            }
        };

    private static View TypedSection(
        string title,
        string visibilityProperty,
        params View[] children)
    {
        var section = Section(title, children);
        section.SetBinding(IsVisibleProperty, visibilityProperty);
        return section;
    }

    private static View Section(string title, params View[] children)
    {
        var content = new VerticalStackLayout { Spacing = 8 };
        content.Children.Add(new Label
        {
            Text = title,
            FontSize = 18,
            FontAttributes = FontAttributes.Bold,
            TextColor = Colors.Black
        });
        foreach (var child in children)
        {
            content.Children.Add(child);
        }

        return new Border
        {
            Stroke = Colors.LightGray,
            Padding = 14,
            Content = content
        };
    }
}
