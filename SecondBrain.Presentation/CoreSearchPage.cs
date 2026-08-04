using SecondBrain.Application.Ports;
using SecondBrain.Presentation.ViewModels;

namespace SecondBrain.Presentation;

public sealed class CoreSearchPage : ContentPage
{
    private readonly CoreSearchViewModel _viewModel;

    public CoreSearchPage(CoreSearchViewModel viewModel)
    {
        _viewModel = viewModel;
        BindingContext = viewModel;
        Title = "Search";
        BackgroundColor = Colors.White;

        var query = new SearchBar
        {
            Placeholder = "Search titles, body, tags, kind, or placement"
        };
        query.SetBinding(SearchBar.TextProperty, nameof(viewModel.QueryText));
        query.SearchButtonPressed += async (_, _) =>
            await viewModel.SearchCommand.ExecuteAsync(null);

        var search = new Button { Text = "Search" };
        search.SetBinding(Button.CommandProperty, nameof(viewModel.SearchCommand));
        var clear = new Button { Text = "Clear filters" };
        clear.SetBinding(Button.CommandProperty, nameof(viewModel.ClearFiltersCommand));

        var status = new Label { TextColor = Colors.DarkSlateGray };
        status.SetBinding(Label.TextProperty, nameof(viewModel.ResultStatus));
        var stale = new Label
        {
            Text = "Showing stale results. Retry when storage is available.",
            TextColor = Colors.DarkOrange
        };
        stale.SetBinding(IsVisibleProperty, nameof(viewModel.AreResultsStale));

        var results = ItemCollection();
        results.SetBinding(ItemsView.ItemsSourceProperty, nameof(viewModel.Results));
        results.SetBinding(
            SelectableItemsView.SelectedItemProperty,
            nameof(viewModel.SelectedResult),
            mode: BindingMode.TwoWay);

        var open = new Button { Text = "Open selected item" };
        open.SetBinding(IsEnabledProperty, nameof(viewModel.HasResults));
        open.Clicked += async (_, _) => await OpenItemAsync(viewModel.SelectedResult);
        var placement = new Button { Text = "Open placement" };
        placement.SetBinding(IsEnabledProperty, nameof(viewModel.HasResults));
        placement.Clicked += async (_, _) =>
            await OpenPlacementAsync(viewModel.SelectedResult);

        var detail = new Label { TextColor = Colors.DarkSlateGray };
        detail.SetBinding(
            Label.TextProperty,
            $"{nameof(viewModel.SelectedResult)}.{nameof(CoreSearchItem.BacklinksText)}");
        var backlinks = new CollectionView
        {
            SelectionMode = SelectionMode.Single,
            MaximumHeightRequest = 160,
            ItemTemplate = new DataTemplate(() =>
            {
                var label = new Label { Padding = new Thickness(4) };
                label.SetBinding(
                    Label.TextProperty,
                    nameof(CoreSearchBacklink.DisplayText));
                return label;
            })
        };
        backlinks.SetBinding(
            ItemsView.ItemsSourceProperty,
            $"{nameof(viewModel.SelectedResult)}.{nameof(CoreSearchItem.Backlinks)}");
        backlinks.SelectionChanged += async (_, args) =>
        {
            if (args.CurrentSelection.FirstOrDefault() is CoreSearchBacklink backlink)
            {
                backlinks.SelectedItem = null;
                await OpenItemAsync(backlink.SourceId.Value);
            }
        };

        var loadMore = new Button { Text = "Load more" };
        loadMore.SetBinding(Button.CommandProperty, nameof(viewModel.LoadMoreCommand));
        loadMore.SetBinding(IsVisibleProperty, nameof(viewModel.HasMore));

        var emptyAction = new Button { Text = "Capture your first item" };
        emptyAction.SetBinding(IsVisibleProperty, nameof(viewModel.IsEmpty));
        emptyAction.Clicked += async (_, _) => await Shell.Current.GoToAsync("//home");
        var loading = new ActivityIndicator { Color = Colors.DarkSlateBlue };
        loading.SetBinding(
            ActivityIndicator.IsRunningProperty,
            nameof(viewModel.IsLoading));
        loading.SetBinding(IsVisibleProperty, nameof(viewModel.IsLoading));

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = 20,
                Spacing = 14,
                Children =
                {
                    Header(),
                    FailureState(viewModel),
                    loading,
                    query,
                    FilterRow(viewModel),
                    new HorizontalStackLayout
                    {
                        Spacing = 10,
                        Children = { search, clear }
                    },
                    status,
                    stale,
                    emptyAction,
                    Section("Results", results, loadMore),
                    Section(
                        "Selected result",
                        detail,
                        new HorizontalStackLayout
                        {
                            Spacing = 10,
                            Children = { open, placement }
                        },
                        backlinks),
                    RetrievalSection(
                        "Favorites",
                        nameof(viewModel.Favorites),
                        "No active favorites yet."),
                    RetrievalSection(
                        "Recent",
                        nameof(viewModel.RecentItems),
                        "No recently updated items yet.")
                }
            }
        };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadCommand.ExecuteAsync(null);
    }

    private static View Header() => new VerticalStackLayout
    {
        Children =
        {
            new Label
            {
                Text = "Search Core",
                FontSize = 28,
                FontAttributes = FontAttributes.Bold,
                TextColor = Colors.Black
            },
            new Label
            {
                Text = "Find persisted knowledge and follow its context.",
                TextColor = Colors.DarkSlateGray
            }
        }
    };

    private static View FilterRow(CoreSearchViewModel viewModel)
    {
        var kind = FilterPicker(
            "Kind",
            nameof(viewModel.KindOptions),
            nameof(viewModel.SelectedKind));
        var tag = FilterPicker(
            "Tag",
            nameof(viewModel.TagOptions),
            nameof(viewModel.SelectedTag));
        var placement = FilterPicker(
            "Placement",
            nameof(viewModel.PlacementOptions),
            nameof(viewModel.SelectedPlacement));
        var archive = FilterPicker(
            "Archive state",
            nameof(viewModel.ArchiveOptions),
            nameof(viewModel.SelectedArchive));
        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star)
            },
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto)
            }
        };
        AddToGrid(grid, kind, 0, 0);
        AddToGrid(grid, tag, 1, 0);
        AddToGrid(grid, placement, 0, 1);
        AddToGrid(grid, archive, 1, 1);
        return grid;
    }

    private static void AddToGrid(Grid grid, View view, int column, int row)
    {
        Grid.SetColumn(view, column);
        Grid.SetRow(view, row);
        grid.Children.Add(view);
    }

    private static Picker FilterPicker(
        string title,
        string itemsProperty,
        string selectedProperty)
    {
        var picker = new Picker
        {
            Title = title,
            ItemDisplayBinding = new Binding("Label")
        };
        picker.SetBinding(Picker.ItemsSourceProperty, itemsProperty);
        picker.SetBinding(
            Picker.SelectedItemProperty,
            selectedProperty,
            mode: BindingMode.TwoWay);
        return picker;
    }

    private static View FailureState(CoreSearchViewModel viewModel)
    {
        var message = new Label { TextColor = Colors.DarkRed };
        message.SetBinding(Label.TextProperty, nameof(viewModel.ErrorMessage));
        var retry = new Button { Text = "Retry" };
        retry.SetBinding(Button.CommandProperty, nameof(viewModel.LoadCommand));
        var layout = new VerticalStackLayout
        {
            Spacing = 6,
            Children = { message, retry }
        };
        layout.SetBinding(IsVisibleProperty, nameof(viewModel.HasError));
        return layout;
    }

    private static CollectionView ItemCollection() => new()
    {
        SelectionMode = SelectionMode.Single,
        MaximumHeightRequest = 420,
        EmptyView = new Label
        {
            Text = "No results. Broaden the query or clear filters.",
            TextColor = Colors.DarkSlateGray
        },
        ItemTemplate = new DataTemplate(() =>
        {
            var title = new Label
            {
                FontAttributes = FontAttributes.Bold,
                TextColor = Colors.Black
            };
            title.SetBinding(Label.TextProperty, nameof(CoreSearchItem.Title));
            var context = new Label { FontSize = 12, TextColor = Colors.DarkSlateGray };
            context.SetBinding(
                Label.TextProperty,
                nameof(CoreSearchItem.KindAndPlacement));
            var preview = new Label
            {
                FontSize = 13,
                MaxLines = 2,
                LineBreakMode = LineBreakMode.TailTruncation
            };
            preview.SetBinding(Label.TextProperty, nameof(CoreSearchItem.Preview));
            var state = new Label { FontSize = 12, TextColor = Colors.DarkSlateBlue };
            state.SetBinding(Label.TextProperty, nameof(CoreSearchItem.State));
            return new VerticalStackLayout
            {
                Padding = new Thickness(4, 8),
                Children = { title, context, preview, state }
            };
        })
    };

    private static View RetrievalSection(
        string title,
        string itemsProperty,
        string emptyMessage)
    {
        var collection = ItemCollection();
        collection.MaximumHeightRequest = 220;
        collection.EmptyView = new Label
        {
            Text = emptyMessage,
            TextColor = Colors.DarkSlateGray
        };
        collection.SetBinding(ItemsView.ItemsSourceProperty, itemsProperty);
        collection.SelectionChanged += async (_, args) =>
        {
            if (args.CurrentSelection.FirstOrDefault() is CoreSearchItem item)
            {
                collection.SelectedItem = null;
                await OpenItemAsync(item);
            }
        };
        return Section(title, collection);
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

    private static Task OpenItemAsync(CoreSearchItem? item) =>
        item is null ? Task.CompletedTask : OpenItemAsync(item.Id.Value);

    private static Task OpenItemAsync(Guid itemId) => Shell.Current.GoToAsync(
        "//editor",
        new Dictionary<string, object>
        {
            ["itemId"] = itemId.ToString(),
            ["returnRoute"] = "search",
        });

    private static Task OpenPlacementAsync(CoreSearchItem? item) => item is null
        ? Task.CompletedTask
        : Shell.Current.GoToAsync(
            "//para",
            new Dictionary<string, object>
            {
                ["contextKind"] = item.PlacementKind.ToString(),
                ["contextId"] = item.PlacementId.ToString(),
                ["returnRoute"] = "search",
            });
}
