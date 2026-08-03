using Microsoft.Maui.Controls.Shapes;
using SecondBrain.Application.UseCases;
using SecondBrain.Domain.Entities;
using SecondBrain.Presentation.ViewModels;

namespace SecondBrain.Presentation;

public sealed class ParaBrowserPage : ContentPage
{
    private readonly ParaBrowserViewModel _viewModel;

    public ParaBrowserPage(ParaBrowserViewModel viewModel)
    {
        _viewModel = viewModel;
        BindingContext = viewModel;
        Title = "PARA";
        BackgroundColor = Colors.White;

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
                        Catalog(),
                        Contexts(),
                        Filters(),
                        Items(),
                        Details(),
                        Organize()
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
            _viewModel.BeginEditContext(
                args.CurrentSelection.FirstOrDefault() as ContextCatalogItem);

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
        save.Clicked += async (_, _) => await _viewModel.SaveContextAsync();
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

        return Section("Contexts", selectedName, selectedDetails, contexts);
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
            _viewModel.BeginCreateContext(ParaContextKind.Area);

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
