using SecondBrain.Application.Ports;
using SecondBrain.Domain.Entities;
using SecondBrain.Domain.ValueObjects;
using SecondBrain.Presentation.ViewModels;

namespace SecondBrain.Presentation;

public sealed class CoreEditorPage : ContentPage, IQueryAttributable
{
    private readonly CoreEditorViewModel _viewModel;
    private readonly ICoreKnowledgeRepository _repository;
    private readonly Picker _kindPicker;
    private readonly Picker _placementPicker;
    private readonly Picker _itemPicker;
    private readonly Picker _journalPicker;
    private readonly CollectionView _sourceCaptures;
    private readonly CollectionView _relationships;
    private readonly Entry _reminderEntry;
    private readonly Entry _reviewDateEntry;
    private readonly Entry _occurrenceDateEntry;
    private readonly Label _catalogMessage;
    private readonly Label _relationshipMessage;
    private readonly Button _backToWorkspaceButton;
    private CoreKnowledgeState? _state;
    private BrainItemKind? _pendingCreateKind;
    private SecondBrainItemId? _pendingItemId;
    private PrimaryPlacement? _pendingPlacement;
    private (ParaContextKind Kind, Guid Id)? _returnWorkspace;
    private string _workspaceReturnRoute = "para";
    private string _directReturnRoute = "editor";
    private SecondBrainItemId? _derivationReturnItemId;

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
        _sourceCaptures = BrainItemCollection(SelectionMode.Multiple);
        _relationships = BrainItemCollection(SelectionMode.Single);
        _relationships.SelectionChanged += async (_, args) =>
        {
            if (args.CurrentSelection.FirstOrDefault() is not BrainItem item)
            {
                return;
            }

            _relationships.SelectedItem = null;
            await OpenRelatedItemAsync(item);
        };
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
        _relationshipMessage = new Label
        {
            Text = "No source or derived links yet.",
            TextColor = Colors.DarkSlateGray,
            FontSize = 13
        };

        _backToWorkspaceButton = new Button
        {
            Text = "← Back to workspace",
            HorizontalOptions = LayoutOptions.Start,
            IsVisible = false
        };
        _backToWorkspaceButton.Clicked += async (_, _) =>
            await NavigateBackAsync();

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = 20,
                Spacing = 14,
                Children =
                {
                    Header(),
                    _backToWorkspaceButton,
                    _catalogMessage,
                    CreateSelector(),
                    EditSelector(),
                    CommonFields(),
                    NoteFields(),
                    IdeaFields(),
                    CaptureFields(),
                    CaptureDerivationActions(),
                    DerivationDraftFields(),
                    ResourceFields(),
                    JournalFields(),
                    RelationshipFields(),
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

        if (await ApplyPendingNavigationAsync())
        {
            return;
        }

        if (_viewModel.ItemId is null &&
            !_viewModel.IsDirty &&
            _placementPicker.SelectedItem is not null)
        {
            BeginCreate();
        }
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        _pendingCreateKind = null;
        _pendingItemId = null;
        _pendingPlacement = null;
        _returnWorkspace = null;
        _workspaceReturnRoute = "para";
        _directReturnRoute = "editor";
        _derivationReturnItemId = null;
        _backToWorkspaceButton.Text = "← Back to workspace";
        _backToWorkspaceButton.IsVisible = false;

        var contextKind = default(ParaContextKind);
        var contextId = Guid.Empty;
        var hasContext =
            TryGetQueryValue(query, "contextKind", out var contextKindValue) &&
            Enum.TryParse<ParaContextKind>(
                contextKindValue,
                true,
                out contextKind) &&
            TryGetQueryValue(query, "contextId", out var contextIdValue) &&
            Guid.TryParse(contextIdValue, out contextId) &&
            contextId != Guid.Empty;
        if (hasContext)
        {
            _pendingPlacement = PlacementFor(contextKind, contextId);
            if (_pendingPlacement is not null)
            {
                _returnWorkspace = (contextKind, contextId);
                TryGetQueryValue(query, "returnRoute", out var returnRoute);
                _workspaceReturnRoute = NormalizeReturnRoute(returnRoute);
                _backToWorkspaceButton.IsVisible = true;
            }
        }

        if (TryGetQueryValue(query, "mode", out var mode) &&
            string.Equals(mode, "create", StringComparison.OrdinalIgnoreCase) &&
            TryGetQueryValue(query, "itemKind", out var itemKindValue) &&
            Enum.TryParse<BrainItemKind>(itemKindValue, true, out var itemKind))
        {
            _pendingCreateKind = itemKind;
        }
        else if (TryGetQueryValue(query, "itemId", out var itemIdValue) &&
            Guid.TryParse(itemIdValue, out var itemId) &&
            itemId != Guid.Empty)
        {
            _pendingItemId = new SecondBrainItemId(itemId);
            TryGetQueryValue(query, "returnRoute", out var returnRoute);
            _directReturnRoute = NormalizeReturnRoute(returnRoute);
            if (_directReturnRoute is "home" or "inbox" or "search")
            {
                _backToWorkspaceButton.Text = $"← Back to {_directReturnRoute}";
                _backToWorkspaceButton.IsVisible = true;
            }
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
            await Shell.Current.GoToAsync(
                "//para",
                new Dictionary<string, object> { ["mode"] = "browse" });

        var openPlacement = new Button
        {
            Text = "Open selected workspace",
            HorizontalOptions = LayoutOptions.Start
        };
        openPlacement.Clicked += async (_, _) =>
        {
            if (TryGetPlacement(_placementPicker.SelectedItem) is { } placement)
            {
                await OpenPlacementWorkspaceAsync(placement);
            }
        };

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
            openPlacement,
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
            SelectPlacement(item.PrimaryPlacement);
            SyncDateFields();
            SelectJournal(journalId);
            SelectSourceCapture(item);
            RefreshRelationships();
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

    private View CaptureDerivationActions()
    {
        var guidance = new Label
        {
            Text = "Select this capture and any additional captures to copy into a new authored item.",
            TextColor = Colors.DarkSlateGray,
            FontSize = 13
        };
        var createNote = new Button { Text = "Create Note from capture" };
        createNote.Clicked += (_, _) => BeginDerivation(BrainItemKind.Note);
        var createResource = new Button { Text = "Create Resource from capture" };
        createResource.Clicked += (_, _) =>
            BeginDerivation(BrainItemKind.ResourceArtifact);

        return TypedSection(
            "Create from captures",
            nameof(_viewModel.IsCapture),
            guidance,
            _sourceCaptures,
            new HorizontalStackLayout
            {
                Spacing = 10,
                Children = { createNote, createResource }
            });
    }

    private View DerivationDraftFields()
    {
        var sources = new Label { TextColor = Colors.DarkSlateGray };
        sources.SetBinding(
            Label.TextProperty,
            nameof(_viewModel.DerivationSourceSummary));
        var lifecycle = new Switch();
        lifecycle.SetBinding(
            Switch.IsToggledProperty,
            nameof(_viewModel.MarkSourcesReferenced));

        return TypedSection(
            "Derived item sources",
            nameof(_viewModel.IsDeriving),
            sources,
            new HorizontalStackLayout
            {
                Spacing = 10,
                Children =
                {
                    lifecycle,
                    new Label
                    {
                        Text = "Mark eligible source captures as Referenced when saved",
                        VerticalTextAlignment = TextAlignment.Center
                    }
                }
            });
    }

    private View RelationshipFields() =>
        Section(
            "Sources and derived items",
            _relationshipMessage,
            _relationships);

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
        cancel.Clicked += async (_, _) =>
        {
            var wasDeriving = _viewModel.IsDeriving;
            _viewModel.CancelCommand.Execute(null);
            if (wasDeriving)
            {
                if (_viewModel.LastSavedItem is { } source)
                {
                    SelectPlacement(source.PrimaryPlacement);
                    SelectSourceCapture(source);
                }

                RefreshRelationships();
                return;
            }

            if (_returnWorkspace is not null || _directReturnRoute != "editor")
            {
                await NavigateBackAsync();
            }
        };

        var save = new Button { Text = "Save" };
        save.Clicked += async (_, _) =>
        {
            var derivationOriginId = _viewModel.DerivationOriginId;
            await _viewModel.SaveCommand.ExecuteAsync(null);
            if (!_viewModel.HasError)
            {
                await RefreshChoicesAsync();
                if (_viewModel.LastSavedItem is { } saved)
                {
                    SelectPlacement(saved.PrimaryPlacement);
                    RefreshRelationships();
                }

                if (derivationOriginId is not null)
                {
                    _derivationReturnItemId = derivationOriginId;
                    _backToWorkspaceButton.Text = "← Back to source capture";
                    _backToWorkspaceButton.IsVisible = true;
                }
                else if (_returnWorkspace is not null || _directReturnRoute != "editor")
                {
                    await NavigateBackAsync();
                }
                else
                {
                    await DisplayAlertAsync("Saved", "Your changes were saved.", "OK");
                }
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

    private void BeginDerivation(BrainItemKind kind)
    {
        if (TryGetPlacement(_placementPicker.SelectedItem) is not { } placement)
        {
            _catalogMessage.Text = "Choose an active placement for the derived item.";
            return;
        }

        try
        {
            var sources = _sourceCaptures.SelectedItems?.Cast<BrainItem>() ?? [];
            _viewModel.BeginDerivation(kind, sources, placement);
            _kindPicker.SelectedItem = kind;
            _catalogMessage.Text = string.Empty;
            SyncDateFields();
        }
        catch (ArgumentException exception)
        {
            _catalogMessage.Text = exception.Message;
        }
        catch (InvalidOperationException exception)
        {
            _catalogMessage.Text = exception.Message;
        }
    }

    private async Task<bool> ApplyPendingNavigationAsync()
    {
        if (_pendingItemId is { } itemId)
        {
            _pendingItemId = null;
            var item = _state?.BrainItems.SingleOrDefault(candidate =>
                candidate.Id == itemId);
            if (item is null)
            {
                _catalogMessage.Text =
                    "The requested item is no longer available. Return to the workspace and refresh.";
                return true;
            }

            var journalId = _state?.Journals
                .FirstOrDefault(journal =>
                    journal.Entries.Any(entry => entry.Id == item.Id))?
                .Id;
            await _viewModel.LoadAsync(item.Id, journalId);
            SelectPlacement(item.PrimaryPlacement);
            SelectJournal(journalId);
            SelectSourceCapture(item);
            RefreshRelationships();
            SyncDateFields();
            return true;
        }

        if (_pendingCreateKind is not { } kind ||
            _pendingPlacement is not { } placement)
        {
            return false;
        }

        _pendingCreateKind = null;
        _pendingPlacement = null;
        _kindPicker.SelectedItem = kind;
        if (!SelectPlacement(placement))
        {
            _catalogMessage.Text =
                "The requested workspace is archived or no longer available. Return to PARA and choose another placement.";
            return true;
        }

        _catalogMessage.Text = string.Empty;
        _viewModel.BeginCreate(kind, placement);
        if (kind == BrainItemKind.JournalEntry)
        {
            _journalPicker.SelectedIndex =
                _journalPicker.ItemsSource?.Count > 0 ? 0 : -1;
            _viewModel.JournalEntry.JournalId =
                (_journalPicker.SelectedItem as Journal)?.Id;
            _viewModel.JournalEntry.OccurrenceDate =
                DateOnly.FromDateTime(DateTime.Today);
        }

        SyncDateFields();
        return true;
    }

    private bool SelectPlacement(PrimaryPlacement placement)
    {
        var placements = _placementPicker.ItemsSource?.Cast<object>().ToArray() ?? [];
        var selected = placements.FirstOrDefault(candidate =>
            TryGetPlacement(candidate) == placement);
        _placementPicker.SelectedItem = selected;
        return selected is not null;
    }

    private void SelectSourceCapture(BrainItem item)
    {
        _sourceCaptures.SelectedItems?.Clear();
        if (item.Kind == BrainItemKind.KnowledgeCapture && !item.IsArchived)
        {
            _sourceCaptures.SelectedItems?.Add(item);
        }
    }

    private void RefreshRelationships()
    {
        if (_state is null || _viewModel.ItemId is not { } itemId)
        {
            _relationships.ItemsSource = Array.Empty<BrainItem>();
            _relationshipMessage.Text = "No source or derived links yet.";
            return;
        }

        var current = _state.BrainItems.SingleOrDefault(item => item.Id == itemId);
        if (current is null)
        {
            _relationships.ItemsSource = Array.Empty<BrainItem>();
            _relationshipMessage.Text = "Linked items are unavailable until refresh.";
            return;
        }

        IEnumerable<SecondBrainItemId> linkedIds = current.Kind switch
        {
            BrainItemKind.KnowledgeCapture => current.DerivedItemLinks,
            BrainItemKind.ResourceArtifact => current.ProvenanceSourceLinks,
            _ => _state.BrainItems
                .Where(item =>
                    item.Kind == BrainItemKind.KnowledgeCapture &&
                    item.DerivedItemLinks.Contains(current.Id))
                .Select(item => item.Id),
        };
        var links = linkedIds
            .Distinct()
            .Select(id => _state.BrainItems.SingleOrDefault(item => item.Id == id))
            .Where(item => item is not null)
            .Cast<BrainItem>()
            .OrderBy(item => item.Title)
            .ToArray();
        _relationships.ItemsSource = links;
        _relationshipMessage.Text = links.Length == 0
            ? "No source or derived links yet."
            : "Select a linked item to open it.";
    }

    private async Task OpenRelatedItemAsync(BrainItem item)
    {
        await _viewModel.LoadAsync(item.Id);
        SelectPlacement(item.PrimaryPlacement);
        SelectSourceCapture(item);
        RefreshRelationships();
        SyncDateFields();
    }

    private async Task OpenPlacementWorkspaceAsync(PrimaryPlacement placement)
    {
        var kind = placement.Kind switch
        {
            PrimaryPlacementKind.Project => ParaContextKind.Project,
            PrimaryPlacementKind.Area => ParaContextKind.Area,
            PrimaryPlacementKind.ResourceTopic => ParaContextKind.ResourceTopic,
            _ => throw new ArgumentOutOfRangeException(nameof(placement)),
        };
        await Shell.Current.GoToAsync(
            "//para",
            new Dictionary<string, object>
            {
                ["contextKind"] = kind.ToString(),
                ["contextId"] = placement.ContextId.ToString(),
                ["returnRoute"] = "editor",
            });
    }

    private async Task NavigateBackAsync()
    {
        if (_derivationReturnItemId is { } sourceId)
        {
            _derivationReturnItemId = null;
            var source = _state?.BrainItems.SingleOrDefault(item => item.Id == sourceId);
            if (source is null)
            {
                _catalogMessage.Text =
                    "The source capture is no longer available. Return to Inbox and refresh.";
                return;
            }

            await _viewModel.LoadAsync(source.Id);
            SelectPlacement(source.PrimaryPlacement);
            SelectSourceCapture(source);
            RefreshRelationships();
            _backToWorkspaceButton.Text = $"← Back to {_directReturnRoute}";
            _backToWorkspaceButton.IsVisible = _directReturnRoute != "editor";
            return;
        }

        if (_returnWorkspace is not { } workspace)
        {
            if (_directReturnRoute != "editor")
            {
                await Shell.Current.GoToAsync($"//{_directReturnRoute}");
            }

            return;
        }

        await Shell.Current.GoToAsync(
            "//para",
            new Dictionary<string, object>
            {
                ["contextKind"] = workspace.Kind.ToString(),
                ["contextId"] = workspace.Id.ToString(),
                ["returnRoute"] = _workspaceReturnRoute,
            });
    }

    private static PrimaryPlacement? PlacementFor(
        ParaContextKind kind,
        Guid id) =>
        kind switch
        {
            ParaContextKind.Project => PrimaryPlacement.InProject(new ProjectId(id)),
            ParaContextKind.Area => PrimaryPlacement.InArea(new AreaId(id)),
            ParaContextKind.ResourceTopic => PrimaryPlacement.InResourceTopic(
                new ResourceTopicId(id)),
            _ => null,
        };

    private static string NormalizeReturnRoute(string? returnRoute) =>
        returnRoute?.Trim().ToLowerInvariant() switch
        {
            "home" => "home",
            "inbox" => "inbox",
            "search" => "search",
            "editor" => "editor",
            _ => "para",
        };

    private static bool TryGetQueryValue(
        IDictionary<string, object> query,
        string key,
        out string? value)
    {
        if (query.TryGetValue(key, out var rawValue) && rawValue is not null)
        {
            value = Uri.UnescapeDataString(rawValue.ToString() ?? string.Empty);
            return true;
        }

        value = null;
        return false;
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

            _sourceCaptures.ItemsSource = items
                .Where(item => item.Kind == BrainItemKind.KnowledgeCapture)
                .ToArray();

            var journals = _state.Journals.OrderBy(journal => journal.Title).ToArray();
            _journalPicker.ItemsSource = journals;
            SelectJournal(_viewModel.JournalEntry.JournalId);

            _catalogMessage.Text = placements.Length == 0
                ? "Create an Area, Project, or Resource Topic before adding content."
                : string.Empty;
            RefreshRelationships();
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

    private static CollectionView BrainItemCollection(SelectionMode selectionMode) =>
        new()
        {
            SelectionMode = selectionMode,
            MaximumHeightRequest = 180,
            ItemTemplate = new DataTemplate(() =>
            {
                var title = new Label
                {
                    FontAttributes = FontAttributes.Bold,
                    TextColor = Colors.Black
                };
                title.SetBinding(Label.TextProperty, nameof(BrainItem.Title));
                var kind = new Label
                {
                    FontSize = 12,
                    TextColor = Colors.DarkSlateGray
                };
                kind.SetBinding(Label.TextProperty, nameof(BrainItem.Kind));
                return new VerticalStackLayout
                {
                    Padding = new Thickness(4),
                    Children = { title, kind }
                };
            })
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
