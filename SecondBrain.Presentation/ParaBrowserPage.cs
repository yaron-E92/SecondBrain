using Microsoft.Maui.Controls.Shapes;
using SecondBrain.Application.UseCases;
using SecondBrain.Domain.Entities;
using SecondBrain.Presentation.ViewModels;

namespace SecondBrain.Presentation;

public sealed class ParaBrowserPage : ContentPage, IQueryAttributable
{
    private readonly ParaBrowserViewModel _viewModel;
    private string _browserReturnRoute = "para";
    private string? _returnEditorItemKind;
    private string? _returnEditorJournalId;
    private string _returnEditorFinalRoute = "editor";

    public ParaBrowserPage(ParaBrowserViewModel viewModel)
    {
        _viewModel = viewModel;
        BindingContext = viewModel;
        Title = "PARA";
        BackgroundColor = Colors.White;

        var browser = new VerticalStackLayout
        {
            Spacing = 16,
            Children =
            {
                Catalog(),
                Contexts(),
                Filters(),
                Items(),
                Details(),
                Organize()
            }
        };
        browser.SetBinding(IsVisibleProperty, nameof(viewModel.IsBrowserVisible));

        Content = new RefreshView
        {
            Content = new ScrollView
            {
                Content = new VerticalStackLayout
                {
                    Padding = 20,
                    Spacing = 16,
                    Children =
                    {
                        Header(),
                        Feedback(),
                        Workspace(),
                        browser
                    }
                }
            }
        };
        ((RefreshView)Content).SetBinding(
            RefreshView.IsRefreshingProperty,
            nameof(viewModel.IsRefreshing));
        ((RefreshView)Content).SetBinding(
            RefreshView.CommandProperty,
            nameof(viewModel.LoadCommand));
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadCommand.ExecuteAsync(null);
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (TryGetQueryValue(query, "mode", out var mode) &&
            string.Equals(mode, "browse", StringComparison.OrdinalIgnoreCase))
        {
            _viewModel.CloseWorkspace();
            TryGetQueryValue(query, "returnRoute", out var returnRoute);
            _browserReturnRoute = returnRoute == "editor" ? "editor" : "para";
            TryGetQueryValue(query, "itemKind", out _returnEditorItemKind);
            TryGetQueryValue(query, "journalId", out _returnEditorJournalId);
            TryGetQueryValue(query, "editorReturnRoute", out var editorReturnRoute);
            _returnEditorFinalRoute = editorReturnRoute is "journals" ? "journals" : "editor";
            return;
        }

        if (!TryGetQueryValue(query, "contextKind", out var kindValue) ||
            !Enum.TryParse<ParaContextKind>(kindValue, true, out var kind) ||
            !TryGetQueryValue(query, "contextId", out var idValue) ||
            !Guid.TryParse(idValue, out var id))
        {
            return;
        }

        TryGetQueryValue(query, "returnRoute", out var returnRoute);
        _viewModel.OpenWorkspace(kind, id, returnRoute ?? "para");
    }

    private View Header() =>
        new VerticalStackLayout
        {
            Children =
            {
                new Label
                {
                    Text = "PARA workspace",
                    FontSize = 28,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = Colors.Black
                },
                new Label
                {
                    Text = "Browse one primary home and keep relationships visible.",
                    TextColor = Colors.DarkSlateGray
                }
            }
        };

    private View Workspace()
    {
        var back = new Button
        {
            Text = "← Back",
            HorizontalOptions = LayoutOptions.Start
        };
        back.Clicked += async (_, _) =>
        {
            if (_viewModel.TryReturnToPreviousWorkspace())
            {
                return;
            }

            var returnRoute = _viewModel.WorkspaceReturnRoute;
            _viewModel.CloseWorkspace();
            if (returnRoute != "para")
            {
                await Shell.Current.GoToAsync($"//{returnRoute}");
            }
        };

        var breadcrumb = new Label
        {
            FontSize = 13,
            TextColor = Colors.DarkSlateBlue
        };
        breadcrumb.SetBinding(
            Label.TextProperty,
            $"{nameof(_viewModel.Workspace)}.{nameof(ParaWorkspace.Name)}",
            stringFormat: "PARA / {0}");

        var name = new Label
        {
            FontSize = 26,
            FontAttributes = FontAttributes.Bold,
            TextColor = Colors.Black
        };
        name.SetBinding(
            Label.TextProperty,
            $"{nameof(_viewModel.Workspace)}.{nameof(ParaWorkspace.Name)}");

        var details = new Label { TextColor = Colors.DarkSlateGray };
        details.SetBinding(
            Label.TextProperty,
            $"{nameof(_viewModel.Workspace)}.{nameof(ParaWorkspace.Details)}");

        var archived = new Label
        {
            Text = "Archived",
            FontAttributes = FontAttributes.Bold,
            TextColor = Colors.DarkOrange
        };
        archived.SetBinding(
            IsVisibleProperty,
            $"{nameof(_viewModel.Workspace)}.{nameof(ParaWorkspace.IsArchived)}");

        var unavailableMessage = new Label { TextColor = Colors.DarkRed };
        unavailableMessage.SetBinding(
            Label.TextProperty,
            $"{nameof(_viewModel.Workspace)}.{nameof(ParaWorkspace.UnavailableMessage)}");
        var retry = new Button
        {
            Text = "Retry",
            HorizontalOptions = LayoutOptions.Start
        };
        retry.SetBinding(Button.CommandProperty, nameof(_viewModel.LoadCommand));
        var manageUnavailable = new Button
        {
            Text = "Return to PARA management",
            HorizontalOptions = LayoutOptions.Start
        };
        manageUnavailable.Clicked += (_, _) =>
        {
            var selected = _viewModel.SelectedCatalogContext;
            _viewModel.CloseWorkspace();
            if (selected is not null)
            {
                _viewModel.BeginEditContext(selected);
            }
        };
        var unavailable = new VerticalStackLayout
        {
            Spacing = 8,
            Children = { unavailableMessage, retry, manageUnavailable }
        };
        unavailable.SetBinding(
            IsVisibleProperty,
            nameof(_viewModel.IsWorkspaceUnavailable));

        var createNote = WorkspaceCreateButton("New Note", BrainItemKind.Note);
        var createCapture = WorkspaceCreateButton(
            "New Capture",
            BrainItemKind.KnowledgeCapture);
        var createResource = WorkspaceCreateButton(
            "New Resource",
            BrainItemKind.ResourceArtifact);
        var createJournal = WorkspaceCreateButton(
            "New Journal Entry",
            BrainItemKind.JournalEntry);
        createJournal.SetBinding(
            IsVisibleProperty,
            nameof(_viewModel.CanCreateWorkspaceJournalEntry));

        var empty = new Label
        {
            Text = "Nothing belongs here yet. Create knowledge here or move an existing item into this workspace.",
            TextColor = Colors.DarkSlateGray
        };
        empty.SetBinding(IsVisibleProperty, nameof(_viewModel.IsWorkspaceEmpty));

        var openItem = new Button
        {
            Text = "Open selected item",
            HorizontalOptions = LayoutOptions.Start
        };
        openItem.SetBinding(IsVisibleProperty, nameof(_viewModel.HasSelectedItem));
        openItem.Clicked += async (_, _) => await OpenSelectedWorkspaceItemAsync();

        var related = new CollectionView
        {
            HeightRequest = 120,
            SelectionMode = SelectionMode.Single,
            ItemTemplate = new DataTemplate(() =>
            {
                var label = new Label
                {
                    Padding = new Thickness(8, 6),
                    FontAttributes = FontAttributes.Bold,
                    TextColor = Colors.DarkSlateBlue
                };
                label.SetBinding(
                    Label.TextProperty,
                    nameof(ParaWorkspaceContextTarget.Name));
                return label;
            })
        };
        related.SetBinding(
            ItemsView.ItemsSourceProperty,
            nameof(_viewModel.WorkspaceRelatedContexts));
        related.SelectionChanged += (_, args) =>
        {
            _viewModel.OpenRelatedWorkspace(
                args.CurrentSelection.FirstOrDefault() as ParaWorkspaceContextTarget);
            related.SelectedItem = null;
        };
        var relatedSection = Section("Related workspaces", related);
        relatedSection.SetBinding(
            IsVisibleProperty,
            nameof(_viewModel.HasWorkspaceRelatedContexts));

        var manage = new Button
        {
            Text = "Edit or organize this workspace",
            HorizontalOptions = LayoutOptions.Start
        };
        manage.Clicked += (_, _) =>
        {
            var selected = _viewModel.SelectedCatalogContext;
            _viewModel.CloseWorkspace();
            if (selected is not null)
            {
                _viewModel.BeginEditContext(selected);
            }
        };

        var available = new VerticalStackLayout
        {
            Spacing = 12,
            Children =
            {
                Section(
                    "What can I do next?",
                    new HorizontalStackLayout
                    {
                        Spacing = 8,
                        Children =
                        {
                            createNote,
                            createCapture,
                            createResource,
                            createJournal
                        }
                    },
                    manage),
                empty,
                WorkspaceItemSection(
                    "Captures",
                    nameof(_viewModel.WorkspaceCaptures),
                    nameof(_viewModel.AreWorkspaceCapturesEmpty)),
                WorkspaceItemSection(
                    "Notes",
                    nameof(_viewModel.WorkspaceNotes),
                    nameof(_viewModel.AreWorkspaceNotesEmpty)),
                WorkspaceItemSection(
                    "Ideas",
                    nameof(_viewModel.WorkspaceIdeas),
                    nameof(_viewModel.AreWorkspaceIdeasEmpty)),
                WorkspaceItemSection(
                    "Resources",
                    nameof(_viewModel.WorkspaceResources),
                    nameof(_viewModel.AreWorkspaceResourcesEmpty)),
                WorkspaceItemSection(
                    "Journal Entries",
                    nameof(_viewModel.WorkspaceJournalEntries),
                    nameof(_viewModel.AreWorkspaceJournalEntriesEmpty)),
                openItem,
                Details(),
                relatedSection,
                Organize()
            }
        };
        available.SetBinding(
            IsVisibleProperty,
            nameof(_viewModel.IsWorkspaceAvailable));

        var content = Section(
            "Workspace",
            back,
            breadcrumb,
            name,
            details,
            archived,
            unavailable,
            available);
        content.SetBinding(IsVisibleProperty, nameof(_viewModel.IsWorkspaceOpen));
        return content;
    }

    private View WorkspaceItemSection(
        string title,
        string itemsProperty,
        string emptyProperty)
    {
        var empty = new Label
        {
            Text = $"No {title.ToLowerInvariant()} yet.",
            FontSize = 13,
            TextColor = Colors.DarkSlateGray
        };
        empty.SetBinding(IsVisibleProperty, emptyProperty);

        var items = new CollectionView
        {
            HeightRequest = 130,
            SelectionMode = SelectionMode.Single,
            ItemTemplate = new DataTemplate(() =>
            {
                var titleLabel = new Label
                {
                    Padding = new Thickness(8, 6),
                    FontAttributes = FontAttributes.Bold,
                    TextColor = Colors.Black
                };
                titleLabel.SetBinding(
                    Label.TextProperty,
                    nameof(ParaItemSummary.Title));
                return titleLabel;
            })
        };
        items.SetBinding(ItemsView.ItemsSourceProperty, itemsProperty);
        items.SetBinding(
            SelectableItemsView.SelectedItemProperty,
            nameof(_viewModel.SelectedItem),
            mode: BindingMode.TwoWay);
        return Section(title, empty, items);
    }

    private Button WorkspaceCreateButton(string text, BrainItemKind kind)
    {
        var button = new Button { Text = text };
        button.Clicked += async (_, _) => await OpenWorkspaceCreateAsync(kind);
        return button;
    }

    private View Feedback()
    {
        var loading = new ActivityIndicator
        {
            Color = Colors.DarkSlateBlue,
            HorizontalOptions = LayoutOptions.Center
        };
        loading.SetBinding(
            ActivityIndicator.IsRunningProperty,
            nameof(_viewModel.IsLoading));
        loading.SetBinding(IsVisibleProperty, nameof(_viewModel.IsLoading));

        var error = new Label { TextColor = Colors.DarkRed };
        error.SetBinding(Label.TextProperty, nameof(_viewModel.ErrorMessage));
        error.SetBinding(IsVisibleProperty, nameof(_viewModel.HasError));

        var status = new Label { TextColor = Colors.DarkGreen };
        status.SetBinding(Label.TextProperty, nameof(_viewModel.StatusMessage));

        return new VerticalStackLayout
        {
            Spacing = 6,
            Children = { loading, error, status }
        };
    }

    private View Catalog()
    {
        var layout = new VerticalStackLayout
        {
            Spacing = 14,
            Children =
            {
                CatalogTypeSection(
                    "Projects",
                    nameof(_viewModel.CatalogProjects),
                    nameof(_viewModel.AreCatalogProjectsEmpty),
                    "No Projects yet. Create the first Project to give work a home.",
                    ParaContextKind.Project),
                CatalogTypeSection(
                    "Areas",
                    nameof(_viewModel.CatalogAreas),
                    nameof(_viewModel.AreCatalogAreasEmpty),
                    "No Areas yet. Create the first ongoing responsibility.",
                    ParaContextKind.Area),
                CatalogTypeSection(
                    "Resource Topics",
                    nameof(_viewModel.CatalogResourceTopics),
                    nameof(_viewModel.AreCatalogResourceTopicsEmpty),
                    "No Resource Topics yet. Create the first reference shelf.",
                    ParaContextKind.ResourceTopic),
                ContextEditor()
            }
        };

        return Section("Build your PARA structure", layout);
    }

    private View CatalogTypeSection(
        string title,
        string itemsProperty,
        string emptyProperty,
        string emptyMessage,
        ParaContextKind kind)
    {
        var empty = new Label
        {
            Text = emptyMessage,
            FontSize = 13,
            TextColor = Colors.DarkSlateGray
        };
        empty.SetBinding(IsVisibleProperty, emptyProperty);

        var items = new CollectionView
        {
            HeightRequest = 120,
            SelectionMode = SelectionMode.Single,
            ItemTemplate = new DataTemplate(() =>
            {
                var name = new Label
                {
                    FontAttributes = FontAttributes.Bold,
                    TextColor = Colors.Black
                };
                name.SetBinding(Label.TextProperty, nameof(ContextCatalogItem.Name));
                var details = new Label
                {
                    FontSize = 12,
                    TextColor = Colors.DarkSlateGray,
                    LineBreakMode = LineBreakMode.TailTruncation
                };
                details.SetBinding(
                    Label.TextProperty,
                    nameof(ContextCatalogItem.Details));
                var archived = new Label
                {
                    Text = "Archived",
                    FontSize = 12,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = Colors.DarkOrange
                };
                archived.SetBinding(
                    IsVisibleProperty,
                    nameof(ContextCatalogItem.IsArchived));
                return new VerticalStackLayout
                {
                    Padding = new Thickness(8, 5),
                    Children = { name, details, archived }
                };
            })
        };
        items.SetBinding(ItemsView.ItemsSourceProperty, itemsProperty);
        items.SelectionChanged += (_, args) =>
        {
            _viewModel.OpenWorkspace(
                args.CurrentSelection.FirstOrDefault() as ContextCatalogItem);
            items.SelectedItem = null;
        };

        var create = new Button
        {
            Text = $"New {ContextTypeName(kind)}",
            HorizontalOptions = LayoutOptions.Start
        };
        create.Clicked += (_, _) => _viewModel.BeginCreateContext(kind);

        return new VerticalStackLayout
        {
            Spacing = 6,
            Children =
            {
                new Label
                {
                    Text = title,
                    FontSize = 16,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = Colors.Black
                },
                empty,
                items,
                create
            }
        };
    }

    private View ContextEditor()
    {
        var name = new Entry { Placeholder = "Name" };
        name.SetBinding(
            Entry.TextProperty,
            nameof(_viewModel.ContextName),
            mode: BindingMode.TwoWay);

        var outcome = new Editor
        {
            Placeholder = "Project outcome",
            AutoSize = EditorAutoSizeOption.TextChanges,
            MinimumHeightRequest = 72
        };
        outcome.SetBinding(
            Editor.TextProperty,
            nameof(_viewModel.ProjectOutcome),
            mode: BindingMode.TwoWay);

        var priority = new Picker
        {
            Title = "Priority",
            ItemsSource = Enum.GetValues<ProjectPriority>()
        };
        priority.SetBinding(
            Picker.SelectedItemProperty,
            nameof(_viewModel.ProjectPriority),
            mode: BindingMode.TwoWay);

        var targetDate = new Entry
        {
            Placeholder = "Optional target date (yyyy-MM-dd)",
            Keyboard = Keyboard.Text
        };
        targetDate.SetBinding(
            Entry.TextProperty,
            nameof(_viewModel.ProjectTargetDate),
            mode: BindingMode.TwoWay);

        var projectFields = new VerticalStackLayout
        {
            Spacing = 8,
            Children = { outcome, priority, targetDate }
        };
        projectFields.SetBinding(
            IsVisibleProperty,
            nameof(_viewModel.IsProjectContextEditor));

        var details = new Label { TextColor = Colors.DarkSlateGray };
        details.SetBinding(
            Label.TextProperty,
            $"{nameof(_viewModel.SelectedCatalogContext)}.{nameof(ContextCatalogItem.Details)}");

        var error = new Label { TextColor = Colors.DarkRed };
        error.SetBinding(
            Label.TextProperty,
            nameof(_viewModel.ContextEditorError));
        error.SetBinding(
            IsVisibleProperty,
            nameof(_viewModel.HasContextEditorError));

        var status = new Label { TextColor = Colors.DarkGreen };
        status.SetBinding(
            Label.TextProperty,
            nameof(_viewModel.ContextEditorStatus));

        var save = new Button { Text = "Save" };
        save.Clicked += async (_, _) =>
        {
            if (!await _viewModel.SaveContextAsync() ||
                _browserReturnRoute != "editor")
            {
                return;
            }

            var itemKind = _returnEditorItemKind;
            var journalId = _returnEditorJournalId;
            var finalRoute = _returnEditorFinalRoute;
            _browserReturnRoute = "para";
            _returnEditorItemKind = null;
            _returnEditorJournalId = null;
            _returnEditorFinalRoute = "editor";
            await Shell.Current.GoToAsync(
                "//editor",
                new Dictionary<string, object>
                {
                    ["mode"] = "create",
                    ["itemKind"] = itemKind ?? BrainItemKind.Note.ToString(),
                    ["journalId"] = journalId ?? string.Empty,
                    ["returnRoute"] = finalRoute,
                });
        };
        var cancel = new Button { Text = "Cancel" };
        cancel.Clicked += (_, _) => _viewModel.CancelContextEdit();

        var archive = new Button { Text = "Archive" };
        archive.SetBinding(
            IsVisibleProperty,
            nameof(_viewModel.CanArchiveSelectedContext));
        archive.Clicked += async (_, _) =>
        {
            if (_viewModel.SelectedCatalogContext is { } selected &&
                await DisplayAlertAsync(
                    $"Archive {ContextTypeName(selected.Kind)}?",
                    $"Archive '{selected.Name}' while preserving its item placements?",
                    "Archive",
                    "Cancel"))
            {
                await _viewModel.ArchiveSelectedContextAsync();
            }
        };

        var restore = new Button { Text = "Restore" };
        restore.SetBinding(
            IsVisibleProperty,
            nameof(_viewModel.CanRestoreSelectedContext));
        restore.Clicked += async (_, _) =>
        {
            if (_viewModel.SelectedCatalogContext is { } selected &&
                await DisplayAlertAsync(
                    $"Restore {ContextTypeName(selected.Kind)}?",
                    $"Restore '{selected.Name}' to active PARA lists and selectors?",
                    "Restore",
                    "Cancel"))
            {
                await _viewModel.RestoreSelectedContextAsync();
            }
        };

        var activate = LifecycleButton(
            "Activate Project",
            nameof(_viewModel.CanActivateSelectedProject),
            ProjectLifecycleTransition.Activate);
        var complete = LifecycleButton(
            "Complete Project",
            nameof(_viewModel.CanCompleteSelectedProject),
            ProjectLifecycleTransition.Complete);
        var cancelProject = LifecycleButton(
            "Cancel Project",
            nameof(_viewModel.CanCancelSelectedProject),
            ProjectLifecycleTransition.Cancel);

        var editor = new VerticalStackLayout
        {
            Spacing = 8,
            Children =
            {
                new Label
                {
                    Text = "Context details",
                    FontSize = 16,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = Colors.Black
                },
                details,
                name,
                projectFields,
                error,
                status,
                new HorizontalStackLayout
                {
                    Spacing = 8,
                    Children = { save, cancel, archive, restore }
                },
                new HorizontalStackLayout
                {
                    Spacing = 8,
                    Children = { activate, complete, cancelProject }
                }
            }
        };
        editor.SetBinding(
            IsVisibleProperty,
            nameof(_viewModel.IsContextEditorVisible));
        return editor;
    }

    private Button LifecycleButton(
        string text,
        string visibilityProperty,
        ProjectLifecycleTransition transition)
    {
        var button = new Button { Text = text };
        button.SetBinding(IsVisibleProperty, visibilityProperty);
        button.Clicked += async (_, _) =>
            await _viewModel.TransitionSelectedProjectAsync(transition);
        return button;
    }

    private View Contexts()
    {
        var selectedName = new Label
        {
            FontSize = 18,
            FontAttributes = FontAttributes.Bold,
            TextColor = Colors.Black
        };
        selectedName.SetBinding(
            Label.TextProperty,
            $"{nameof(_viewModel.SelectedContext)}.{nameof(ParaContextItem.Name)}");

        var selectedDetails = new Label { TextColor = Colors.DarkSlateGray };
        selectedDetails.SetBinding(
            Label.TextProperty,
            $"{nameof(_viewModel.SelectedContext)}.{nameof(ParaContextItem.Details)}");

        var contexts = new CollectionView
        {
            HeightRequest = 180,
            SelectionMode = SelectionMode.Single,
            ItemTemplate = new DataTemplate(() =>
            {
                var name = new Label
                {
                    FontAttributes = FontAttributes.Bold,
                    TextColor = Colors.Black
                };
                name.SetBinding(Label.TextProperty, nameof(ParaContextItem.Name));
                var details = new Label
                {
                    FontSize = 12,
                    TextColor = Colors.DarkSlateGray,
                    LineBreakMode = LineBreakMode.TailTruncation
                };
                details.SetBinding(Label.TextProperty, nameof(ParaContextItem.Details));
                return new VerticalStackLayout
                {
                    Padding = new Thickness(8, 6),
                    Children = { name, details }
                };
            })
        };
        contexts.SetBinding(
            ItemsView.ItemsSourceProperty,
            nameof(_viewModel.Contexts));
        contexts.SetBinding(
            SelectableItemsView.SelectedItemProperty,
            nameof(_viewModel.SelectedContext),
            mode: BindingMode.TwoWay);

        var open = new Button
        {
            Text = "Open selected workspace",
            HorizontalOptions = LayoutOptions.Start
        };
        open.Clicked += (_, _) =>
        {
            if (_viewModel.SelectedContext is
                {
                    Kind: ParaContextKind.Project or
                        ParaContextKind.Area or
                        ParaContextKind.ResourceTopic,
                    Id: { } id,
                } selected)
            {
                _viewModel.OpenWorkspace(selected.Kind, id);
            }
        };

        return Section("Contexts", selectedName, selectedDetails, contexts, open);
    }

    private View Filters()
    {
        var kind = new Picker
        {
            Title = "Kind",
            ItemDisplayBinding = new Binding(nameof(ParaKindFilter.Name))
        };
        kind.SetBinding(Picker.ItemsSourceProperty, nameof(_viewModel.KindFilters));
        kind.SetBinding(
            Picker.SelectedItemProperty,
            nameof(_viewModel.SelectedKindFilter),
            mode: BindingMode.TwoWay);

        var tag = new Picker
        {
            Title = "Tag",
            ItemDisplayBinding = new Binding(nameof(ParaTagFilter.Name))
        };
        tag.SetBinding(Picker.ItemsSourceProperty, nameof(_viewModel.TagFilters));
        tag.SetBinding(
            Picker.SelectedItemProperty,
            nameof(_viewModel.SelectedTagFilter),
            mode: BindingMode.TwoWay);

        var favorite = new Switch();
        favorite.SetBinding(
            Switch.IsToggledProperty,
            nameof(_viewModel.FavoritesOnly),
            mode: BindingMode.TwoWay);

        return Section(
            "Filters",
            new HorizontalStackLayout
            {
                Spacing = 8,
                Children =
                {
                    kind,
                    tag
                }
            },
            new HorizontalStackLayout
            {
                Spacing = 8,
                Children =
                {
                    new Label
                    {
                        Text = "Favorites only",
                        VerticalTextAlignment = TextAlignment.Center,
                        TextColor = Colors.Black
                    },
                    favorite
                }
            });
    }

    private View Items()
    {
        var empty = new Label
        {
            Text = "No items match this context and filter combination.",
            TextColor = Colors.DarkSlateGray
        };
        empty.SetBinding(IsVisibleProperty, nameof(_viewModel.IsEmpty));

        var items = new CollectionView
        {
            HeightRequest = 260,
            SelectionMode = SelectionMode.Single,
            ItemTemplate = new DataTemplate(() =>
            {
                var title = new Label
                {
                    FontAttributes = FontAttributes.Bold,
                    TextColor = Colors.Black
                };
                title.SetBinding(Label.TextProperty, nameof(ParaItemSummary.Title));
                var primary = new Label
                {
                    FontSize = 12,
                    TextColor = Colors.DarkSlateBlue
                };
                primary.SetBinding(
                    Label.TextProperty,
                    nameof(ParaItemSummary.PrimaryLocation),
                    stringFormat: "Primary: {0}");
                return new VerticalStackLayout
                {
                    Padding = new Thickness(8, 6),
                    Children = { title, primary }
                };
            })
        };
        items.SetBinding(ItemsView.ItemsSourceProperty, nameof(_viewModel.Items));
        items.SetBinding(
            SelectableItemsView.SelectedItemProperty,
            nameof(_viewModel.SelectedItem),
            mode: BindingMode.TwoWay);

        return Section("Items", empty, items);
    }

    private View Details()
    {
        var title = BoundLabel(nameof(ParaItemSummary.Title), 20, FontAttributes.Bold);
        var content = BoundLabel(nameof(ParaItemSummary.Content), 14);
        var kind = BoundLabel(
            nameof(ParaItemSummary.Kind),
            13,
            stringFormat: "Type: {0}");
        var primary = BoundLabel(
            nameof(ParaItemSummary.PrimaryLocation),
            15,
            FontAttributes.Bold,
            "Primary location: {0}");
        var relationships = BoundLabel(
            nameof(ParaItemSummary.SecondaryRelationships),
            13,
            stringFormat: "Tags and related items: {0}");

        var details = Section(
            "Selected item",
            title,
            kind,
            content,
            primary,
            relationships);
        details.SetBinding(IsVisibleProperty, nameof(_viewModel.HasSelectedItem));
        return details;
    }

    private View Organize()
    {
        var destination = new Picker
        {
            Title = "Move destination",
            ItemDisplayBinding = new Binding(nameof(ParaDestination.Name))
        };
        destination.SetBinding(
            Picker.ItemsSourceProperty,
            nameof(_viewModel.Destinations));
        destination.SetBinding(
            Picker.SelectedItemProperty,
            nameof(_viewModel.SelectedDestination),
            mode: BindingMode.TwoWay);

        var createDestination = new Button
        {
            Text = "Create a destination",
            HorizontalOptions = LayoutOptions.Start
        };
        createDestination.Clicked += (_, _) =>
        {
            if (_viewModel.IsWorkspaceOpen)
            {
                _viewModel.CloseWorkspace();
            }

            _viewModel.BeginCreateContext(ParaContextKind.Area);
        };

        var move = new Button { Text = "Move" };
        move.Clicked += async (_, _) =>
        {
            var selected = _viewModel.SelectedItem;
            var selectedDestination = _viewModel.SelectedDestination;
            if (selected is null || selectedDestination is null)
            {
                return;
            }

            if (await DisplayAlertAsync(
                "Move item?",
                $"Move '{selected.Title}' to {selectedDestination.Name}? Only its primary location will change.",
                "Move",
                "Cancel"))
            {
                await _viewModel.MoveSelectedAsync(selectedDestination);
            }
        };

        var archive = new Button { Text = "Archive" };
        archive.SetBinding(IsVisibleProperty, nameof(_viewModel.CanArchiveSelected));
        archive.Clicked += async (_, _) =>
        {
            if (_viewModel.SelectedItem is { } selected &&
                await DisplayAlertAsync(
                    "Archive item?",
                    $"Archive '{selected.Title}' while preserving its type and relationships?",
                    "Archive",
                    "Cancel"))
            {
                await _viewModel.ArchiveSelectedAsync();
            }
        };

        var restore = new Button { Text = "Restore" };
        restore.SetBinding(IsVisibleProperty, nameof(_viewModel.CanRestoreSelected));
        restore.Clicked += async (_, _) =>
        {
            if (_viewModel.SelectedItem is { } selected &&
                await DisplayAlertAsync(
                    "Restore item?",
                    $"Restore '{selected.Title}' to {selected.PrimaryLocation}?",
                    "Restore",
                    "Cancel"))
            {
                await _viewModel.RestoreSelectedAsync();
            }
        };

        var tag = new Picker
        {
            Title = "Existing tag",
            ItemDisplayBinding = new Binding(nameof(ParaTagOption.Name))
        };
        tag.SetBinding(Picker.ItemsSourceProperty, nameof(_viewModel.AvailableTags));
        tag.SetBinding(
            Picker.SelectedItemProperty,
            nameof(_viewModel.SelectedTagToAdd),
            mode: BindingMode.TwoWay);
        var addTag = new Button { Text = "Add tag" };
        addTag.Clicked += async (_, _) =>
            await _viewModel.AddTagToSelectedAsync(_viewModel.SelectedTagToAdd);

        var link = new Picker
        {
            Title = "Related item",
            ItemDisplayBinding = new Binding(nameof(ParaItemSummary.Title))
        };
        link.SetBinding(
            Picker.ItemsSourceProperty,
            nameof(_viewModel.AvailableLinkTargets));
        link.SetBinding(
            Picker.SelectedItemProperty,
            nameof(_viewModel.SelectedLinkTarget),
            mode: BindingMode.TwoWay);
        var addLink = new Button { Text = "Add relationship" };
        addLink.Clicked += async (_, _) =>
            await _viewModel.AddLinkToSelectedAsync(_viewModel.SelectedLinkTarget);

        var actions = Section(
            "Organize safely",
            destination,
            createDestination,
            new HorizontalStackLayout
            {
                Spacing = 8,
                Children = { move, archive, restore }
            },
            tag,
            addTag,
            link,
            addLink);
        actions.SetBinding(IsVisibleProperty, nameof(_viewModel.HasSelectedItem));
        return actions;
    }

    private async Task OpenWorkspaceCreateAsync(BrainItemKind kind)
    {
        var target = _viewModel.GetWorkspaceCreateTarget(kind);
        var workspace = _viewModel.Workspace;
        if (target is null || workspace is null)
        {
            return;
        }

        await Shell.Current.GoToAsync(
            "//editor",
            new Dictionary<string, object>
            {
                ["mode"] = "create",
                ["itemKind"] = target.Kind.ToString(),
                ["contextKind"] = workspace.Kind.ToString(),
                ["contextId"] = workspace.Id.ToString(),
                ["returnRoute"] = _viewModel.WorkspaceReturnRoute,
            });
    }

    private async Task OpenSelectedWorkspaceItemAsync()
    {
        var item = _viewModel.SelectedItem;
        var workspace = _viewModel.Workspace;
        if (item is null || workspace is null)
        {
            return;
        }

        await Shell.Current.GoToAsync(
            "//editor",
            new Dictionary<string, object>
            {
                ["mode"] = "edit",
                ["itemId"] = item.Id.Value.ToString(),
                ["contextKind"] = workspace.Kind.ToString(),
                ["contextId"] = workspace.Id.ToString(),
                ["returnRoute"] = _viewModel.WorkspaceReturnRoute,
            });
    }

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

    private Label BoundLabel(
        string property,
        double fontSize,
        FontAttributes fontAttributes = FontAttributes.None,
        string? stringFormat = null)
    {
        var label = new Label
        {
            FontSize = fontSize,
            FontAttributes = fontAttributes,
            TextColor = Colors.Black
        };
        label.SetBinding(
            Label.TextProperty,
            $"{nameof(_viewModel.SelectedItem)}.{property}",
            stringFormat: stringFormat);
        return label;
    }

    private static Border Section(string title, params View[] children)
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
            StrokeShape = new RoundRectangle { CornerRadius = 10 },
            Padding = 14,
            Content = content
        };
    }

    private static string ContextTypeName(ParaContextKind kind) =>
        kind switch
        {
            ParaContextKind.Project => "Project",
            ParaContextKind.Area => "Area",
            ParaContextKind.ResourceTopic => "Resource Topic",
            _ => "Context",
        };
}
