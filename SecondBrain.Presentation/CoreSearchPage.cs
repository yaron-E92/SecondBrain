using Microsoft.Maui.Layouts;
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
        BackgroundColor = SecondBrainVisual.Background;

        var query = new SearchBar
        {
            Placeholder = "Search titles, body, tags, kind, or placement",
            AutomationId = "SearchQuery",
        };
        query.SetBinding(SearchBar.TextProperty, nameof(viewModel.QueryText));
        query.SearchButtonPressed += async (_, _) =>
            await viewModel.SearchCommand.ExecuteAsync(null);

        var search = SecondBrainVisual.PrimaryButton("Search", "SearchSubmit");
        search.SetBinding(Button.CommandProperty, nameof(viewModel.SearchCommand));
        var clear = SecondBrainVisual.QuietButton("Clear filters", "SearchClearFilters");
        clear.SetBinding(Button.CommandProperty, nameof(viewModel.ClearFiltersCommand));

        var searchCard = SecondBrainVisual.Card(
            query,
            FilterLayout(viewModel),
            WrapActions(search, clear));

        var status = new Label
        {
            FontSize = 13,
            TextColor = SecondBrainVisual.Muted,
        };
        status.SetBinding(Label.TextProperty, nameof(viewModel.ResultStatus));
        var stale = new Label
        {
            Text = "Showing the last useful results. Retry when storage is available.",
            FontSize = 13,
            TextColor = SecondBrainVisual.Warning,
        };
        stale.SetBinding(IsVisibleProperty, nameof(viewModel.AreResultsStale));

        var providerFailures = ProviderFailures(viewModel);
        var providerResults = ProviderResults();
        providerResults.SetBinding(ItemsView.ItemsSourceProperty, nameof(viewModel.FederatedResults));

        var results = ItemCollection();
        results.SetBinding(ItemsView.ItemsSourceProperty, nameof(viewModel.Results));
        results.SetBinding(
            SelectableItemsView.SelectedItemProperty,
            nameof(viewModel.SelectedResult),
            mode: BindingMode.TwoWay);

        var open = SecondBrainVisual.PrimaryButton("Open item", "SearchOpenItem");
        open.SetBinding(IsEnabledProperty, nameof(viewModel.HasResults));
        open.Clicked += async (_, _) => await OpenItemAsync(viewModel.SelectedResult);
        var placement = SecondBrainVisual.SecondaryButton("Open its home", "SearchOpenPlacement");
        placement.SetBinding(IsEnabledProperty, nameof(viewModel.HasResults));
        placement.Clicked += async (_, _) => await OpenPlacementAsync(viewModel.SelectedResult);

        var detail = new Label
        {
            TextColor = SecondBrainVisual.Muted,
            LineBreakMode = LineBreakMode.WordWrap,
        };
        detail.SetBinding(
            Label.TextProperty,
            $"{nameof(viewModel.SelectedResult)}.{nameof(CoreSearchItem.BacklinksText)}");
        var backlinks = Backlinks();

        var detailCard = SecondBrainVisual.Card(
            SecondBrainVisual.Eyebrow("Selected"),
            SecondBrainVisual.SectionTitle("Inspect result"),
            detail,
            WrapActions(open, placement),
            SecondBrainVisual.SectionTitle("Linked from"),
            backlinks);

        var loadMore = SecondBrainVisual.SecondaryButton("Load more", "SearchLoadMore");
        loadMore.SetBinding(Button.CommandProperty, nameof(viewModel.LoadMoreCommand));
        loadMore.SetBinding(IsVisibleProperty, nameof(viewModel.HasMore));
        var resultsCard = SecondBrainVisual.Card(
            SecondBrainVisual.Eyebrow("Core"),
            SecondBrainVisual.SectionTitle("Results"),
            results,
            loadMore);

        var emptyAction = SecondBrainVisual.PrimaryButton("Capture your first item", "SearchCapture");
        emptyAction.HorizontalOptions = LayoutOptions.Start;
        emptyAction.SetBinding(IsVisibleProperty, nameof(viewModel.IsEmpty));
        emptyAction.Clicked += async (_, _) => await Shell.Current.GoToAsync("//home");

        var loading = new ActivityIndicator
        {
            Color = SecondBrainVisual.Accent,
            HorizontalOptions = LayoutOptions.Start,
        };
        loading.SetBinding(ActivityIndicator.IsRunningProperty, nameof(viewModel.IsLoading));
        loading.SetBinding(IsVisibleProperty, nameof(viewModel.IsLoading));

        var providerCard = SecondBrainVisual.Card(
            SecondBrainVisual.Eyebrow("Enabled sources"),
            SecondBrainVisual.SectionTitle("Other sources"),
            SecondBrainVisual.Body(
                "Results stay source-owned. If one source fails, useful Core results remain available."),
            providerResults);

        var body = new VerticalStackLayout
        {
            Padding = new Thickness(24, 20, 24, 36),
            Spacing = 16,
            MaximumWidthRequest = 1180,
            HorizontalOptions = LayoutOptions.Fill,
            Children =
            {
                SecondBrainVisual.Eyebrow("Find"),
                SecondBrainVisual.PageTitle("Search"),
                SecondBrainVisual.Body(
                    "Search everything you enabled. Every result keeps its owner and useful context."),
                FailureState(viewModel),
                loading,
                searchCard,
                status,
                stale,
                providerFailures,
                providerCard,
                emptyAction,
                SearchWorkspace(resultsCard, detailCard),
                RetrievalSection(
                    "Favorites",
                    nameof(viewModel.Favorites),
                    "No active favorites yet."),
                RetrievalSection(
                    "Recent",
                    nameof(viewModel.RecentItems),
                    "No recently updated items yet."),
            },
        };

        Content = new ScrollView { Content = Centered(body, 1180) };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadCommand.ExecuteAsync(null);
    }

    private CollectionView Backlinks()
    {
        var backlinks = new CollectionView
        {
            SelectionMode = SelectionMode.Single,
            MaximumHeightRequest = 170,
            EmptyView = SecondBrainVisual.Body("No backlinks for this result."),
            ItemTemplate = new DataTemplate(() =>
            {
                var label = new Label
                {
                    Padding = new Thickness(4, 7),
                    TextColor = SecondBrainVisual.Ink,
                };
                label.SetBinding(Label.TextProperty, nameof(CoreSearchBacklink.DisplayText));
                return label;
            }),
        };
        backlinks.SetBinding(
            ItemsView.ItemsSourceProperty,
            $"{nameof(_viewModel.SelectedResult)}.{nameof(CoreSearchItem.Backlinks)}");
        backlinks.SelectionChanged += async (_, args) =>
        {
            if (args.CurrentSelection.FirstOrDefault() is CoreSearchBacklink backlink)
            {
                backlinks.SelectedItem = null;
                await OpenItemAsync(backlink.SourceId.Value);
            }
        };
        return backlinks;
    }

    private static View SearchWorkspace(View results, View detail)
    {
        if (DeviceInfo.Idiom != DeviceIdiom.Desktop)
        {
            return new VerticalStackLayout
            {
                Spacing = 14,
                Children = { results, detail },
            };
        }

        var workspace = new Grid
        {
            ColumnSpacing = 16,
            ColumnDefinitions =
            {
                new ColumnDefinition(new GridLength(1.45, GridUnitType.Star)),
                new ColumnDefinition(new GridLength(1, GridUnitType.Star)),
            },
            Children = { results, detail },
        };
        Grid.SetColumn(detail, 1);
        return workspace;
    }

    private static View FilterLayout(CoreSearchViewModel viewModel)
    {
        var filters = new[]
        {
            FilterPicker("Kind", nameof(viewModel.KindOptions), nameof(viewModel.SelectedKind)),
            FilterPicker("Tag", nameof(viewModel.TagOptions), nameof(viewModel.SelectedTag)),
            FilterPicker("Placement", nameof(viewModel.PlacementOptions), nameof(viewModel.SelectedPlacement)),
            FilterPicker("Archive state", nameof(viewModel.ArchiveOptions), nameof(viewModel.SelectedArchive)),
        };

        if (DeviceInfo.Idiom != DeviceIdiom.Desktop)
        {
            var stack = new VerticalStackLayout { Spacing = 8 };
            foreach (var filter in filters)
            {
                stack.Children.Add(filter);
            }
            return stack;
        }

        var grid = new Grid
        {
            ColumnSpacing = 10,
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star),
            },
        };
        for (var index = 0; index < filters.Length; index++)
        {
            Grid.SetColumn(filters[index], index);
            grid.Children.Add(filters[index]);
        }
        return grid;
    }

    private static Picker FilterPicker(
        string title,
        string itemsProperty,
        string selectedProperty)
    {
        var picker = new Picker
        {
            Title = title,
            ItemDisplayBinding = new Binding("Label"),
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
        var message = new Label { TextColor = SecondBrainVisual.Danger };
        message.SetBinding(Label.TextProperty, nameof(viewModel.ErrorMessage));
        var retry = SecondBrainVisual.SecondaryButton("Retry", "SearchRetry");
        retry.HorizontalOptions = LayoutOptions.Start;
        retry.SetBinding(Button.CommandProperty, nameof(viewModel.LoadCommand));
        var card = SecondBrainVisual.Card(
            SecondBrainVisual.SectionTitle("Search could not refresh"),
            message,
            retry);
        card.SetBinding(IsVisibleProperty, nameof(viewModel.HasError));
        return card;
    }

    private static CollectionView ItemCollection() => new()
    {
        SelectionMode = SelectionMode.Single,
        MaximumHeightRequest = 430,
        EmptyView = SecondBrainVisual.Body("No results. Broaden the query or clear filters."),
        ItemTemplate = new DataTemplate(() =>
        {
            var title = new Label
            {
                FontAttributes = FontAttributes.Bold,
                TextColor = SecondBrainVisual.Ink,
            };
            title.SetBinding(Label.TextProperty, nameof(CoreSearchItem.Title));
            var context = new Label
            {
                FontSize = 12,
                TextColor = SecondBrainVisual.Muted,
            };
            context.SetBinding(
                Label.TextProperty,
                nameof(CoreSearchItem.KindAndPlacement),
                stringFormat: "Core · {0}");
            var preview = new Label
            {
                FontSize = 13,
                MaxLines = 2,
                LineBreakMode = LineBreakMode.TailTruncation,
                TextColor = SecondBrainVisual.Ink,
            };
            preview.SetBinding(Label.TextProperty, nameof(CoreSearchItem.Preview));
            var state = new Label
            {
                FontSize = 12,
                TextColor = SecondBrainVisual.Accent,
            };
            state.SetBinding(Label.TextProperty, nameof(CoreSearchItem.State));
            return new VerticalStackLayout
            {
                Padding = new Thickness(4, 9),
                Spacing = 3,
                Children = { title, context, preview, state },
            };
        }),
    };

    private static View ProviderFailures(CoreSearchViewModel viewModel)
    {
        var failures = new CollectionView
        {
            SelectionMode = SelectionMode.None,
            ItemTemplate = new DataTemplate(() =>
            {
                var message = new Label
                {
                    TextColor = SecondBrainVisual.Danger,
                    LineBreakMode = LineBreakMode.WordWrap,
                };
                message.SetBinding(Label.TextProperty, nameof(SearchProviderFailure.Message));
                var retry = SecondBrainVisual.SecondaryButton("Retry source");
                retry.SetBinding(
                    Button.CommandProperty,
                    new Binding(nameof(viewModel.RetryProviderCommand), source: viewModel));
                retry.SetBinding(Button.CommandParameterProperty, ".");
                return SecondBrainVisual.Card(
                    SecondBrainVisual.Eyebrow("Source unavailable"),
                    message,
                    retry);
            }),
        };
        failures.SetBinding(ItemsView.ItemsSourceProperty, nameof(viewModel.ProviderFailures));
        failures.SetBinding(IsVisibleProperty, nameof(viewModel.HasProviderFailures));
        return failures;
    }

    private static CollectionView ProviderResults() => new()
    {
        SelectionMode = SelectionMode.None,
        EmptyView = SecondBrainVisual.Body(
            "No additional provider results. Core results remain available below."),
        ItemTemplate = new DataTemplate(() =>
        {
            var source = new Label
            {
                FontSize = 12,
                FontAttributes = FontAttributes.Bold,
                TextColor = SecondBrainVisual.Accent,
            };
            source.SetBinding(
                Label.TextProperty,
                nameof(FederatedSearchItem.SourceName),
                stringFormat: "Owned by {0}");
            var title = new Label
            {
                FontAttributes = FontAttributes.Bold,
                TextColor = SecondBrainVisual.Ink,
            };
            title.SetBinding(Label.TextProperty, nameof(FederatedSearchItem.Title));
            var preview = new Label
            {
                MaxLines = 2,
                TextColor = SecondBrainVisual.Muted,
            };
            preview.SetBinding(Label.TextProperty, nameof(FederatedSearchItem.Preview));
            var open = SecondBrainVisual.QuietButton("Open in source");
            open.HorizontalOptions = LayoutOptions.Start;
            open.Clicked += async (sender, _) =>
            {
                if (sender is Button { BindingContext: FederatedSearchItem item })
                {
                    try
                    {
                        await Shell.Current.GoToAsync(item.OpenRoute);
                    }
                    catch (Exception exception)
                    {
                        await Shell.Current.CurrentPage.DisplayAlertAsync(
                            $"Could not open {item.SourceName}",
                            exception.Message,
                            "OK");
                    }
                }
            };
            return new VerticalStackLayout
            {
                Padding = new Thickness(4, 8),
                Spacing = 3,
                Children = { source, title, preview, open },
            };
        }),
    };

    private static View RetrievalSection(
        string title,
        string itemsProperty,
        string emptyMessage)
    {
        var collection = ItemCollection();
        collection.MaximumHeightRequest = 220;
        collection.EmptyView = SecondBrainVisual.Body(emptyMessage);
        collection.SetBinding(ItemsView.ItemsSourceProperty, itemsProperty);
        collection.SelectionChanged += async (_, args) =>
        {
            if (args.CurrentSelection.FirstOrDefault() is CoreSearchItem item)
            {
                collection.SelectedItem = null;
                await OpenItemAsync(item);
            }
        };
        return SecondBrainVisual.Card(
            SecondBrainVisual.SectionTitle(title),
            collection);
    }

    private static FlexLayout WrapActions(params View[] views)
    {
        var layout = new FlexLayout
        {
            Direction = FlexDirection.Row,
            Wrap = FlexWrap.Wrap,
            AlignItems = FlexAlignItems.Center,
        };
        foreach (var view in views)
        {
            layout.Children.Add(view);
        }
        return layout;
    }

    private static Grid Centered(View view, double maxWidth)
    {
        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(new GridLength(maxWidth)),
                new ColumnDefinition(GridLength.Star),
            },
            Children = { view },
        };
        Grid.SetColumn(view, 1);
        grid.SizeChanged += (_, _) =>
        {
            var wide = grid.Width >= maxWidth + 40;
            grid.ColumnDefinitions[0].Width = wide ? GridLength.Star : 0;
            grid.ColumnDefinitions[1].Width = wide ? new GridLength(maxWidth) : GridLength.Star;
            grid.ColumnDefinitions[2].Width = wide ? GridLength.Star : 0;
        };
        return grid;
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
