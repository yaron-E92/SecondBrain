using Microsoft.Maui.Controls.Shapes;
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
            nameof(viewModel.IsLoading));
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
}
