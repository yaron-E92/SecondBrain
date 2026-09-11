using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Layouts;
using SecondBrain.Application.UseCases;
using SecondBrain.Presentation.ViewModels;

namespace SecondBrain.Presentation;

public sealed class MainPage : ContentPage
{
    private readonly DashboardViewModel _viewModel;
    private readonly Editor _captureEditor;

    public MainPage(DashboardViewModel viewModel)
    {
        _viewModel = viewModel;
        BindingContext = viewModel;
        Title = "Home";
        BackgroundColor = SecondBrainVisual.Background;

        _captureEditor = new Editor
        {
            AutoSize = EditorAutoSizeOption.TextChanges,
            MinimumHeightRequest = 110,
            Placeholder = "What do you want to remember?",
            AutomationId = "HomeCaptureText",
        };
        _captureEditor.SetBinding(Editor.TextProperty, nameof(viewModel.CaptureText));

        var capture = SecondBrainVisual.PrimaryButton("Save to Inbox", "HomeCaptureSave");
        capture.HorizontalOptions = LayoutOptions.End;
        capture.SetBinding(Button.CommandProperty, nameof(viewModel.CaptureCommand));

        var captureStatus = new Label
        {
            FontSize = 13,
            TextColor = SecondBrainVisual.Success,
        };
        captureStatus.SetBinding(Label.TextProperty, nameof(viewModel.CaptureStatus));

        var captureCard = SecondBrainVisual.Card(
            SecondBrainVisual.Eyebrow("Capture"),
            SecondBrainVisual.SectionTitle("Get it out of your head"),
            SecondBrainVisual.Body(
                "Capture first. You can decide what it is and where it belongs when you process Inbox."),
            _captureEditor,
            capture,
            captureStatus);

        var processInbox = SecondBrainVisual.PrimaryButton("Process Inbox", "HomeProcessInbox");
        processInbox.Clicked += async (_, _) => await Shell.Current.GoToAsync("//inbox");

        var review = SecondBrainVisual.SecondaryButton("Start Review", "HomeStartReview");
        review.Clicked += async (_, _) => await Shell.Current.GoToAsync(
            "//review",
            new Dictionary<string, object>
            {
                ["kind"] = "para",
                ["returnRoute"] = "home",
            });

        var attentionCard = SecondBrainVisual.Card(
            SecondBrainVisual.Eyebrow("Attention"),
            SecondBrainVisual.SectionTitle("What needs attention"),
            SecondBrainVisual.Body(
                "Process captured thoughts into useful homes, or enter Review when you want deliberate maintenance."),
            new FlexLayout
            {
                Direction = FlexDirection.Row,
                Wrap = FlexWrap.Wrap,
                AlignItems = FlexAlignItems.Center,
                Children = { processInbox, review },
            });

        var inbox = ItemSection(
            "Inbox",
            "Thoughts waiting for a home",
            nameof(viewModel.InboxItems),
            nameof(viewModel.IsInboxEmpty),
            "Inbox is clear.",
            "inbox-process",
            "Process · choose its home",
            "home");

        var projects = ProjectSection(viewModel);
        var favorites = ItemSection(
            "Favorites",
            "Knowledge you chose to keep close",
            nameof(viewModel.Favorites),
            nameof(viewModel.AreFavoritesEmpty),
            "No favorites yet.",
            "editor",
            null,
            "home");
        var recent = ItemSection(
            "Recent",
            "Recently changed knowledge",
            nameof(viewModel.RecentItems),
            nameof(viewModel.AreRecentItemsEmpty),
            "Your recently updated items will appear here.",
            "editor",
            null,
            "home");

        var failure = FailureState(viewModel);
        var loading = new ActivityIndicator
        {
            Color = SecondBrainVisual.Accent,
            HorizontalOptions = LayoutOptions.Start,
        };
        loading.SetBinding(ActivityIndicator.IsRunningProperty, nameof(viewModel.IsLoading));
        loading.SetBinding(IsVisibleProperty, nameof(viewModel.IsLoading));

        var body = new VerticalStackLayout
        {
            Padding = new Thickness(24, 20, 24, 36),
            Spacing = 18,
            MaximumWidthRequest = 1180,
            HorizontalOptions = LayoutOptions.Fill,
            Children =
            {
                new VerticalStackLayout
                {
                    Spacing = 4,
                    Children =
                    {
                        SecondBrainVisual.Eyebrow("Offline workspace"),
                        SecondBrainVisual.PageTitle("Home"),
                        SecondBrainVisual.Body("Capture quickly. Process deliberately. Find what matters."),
                    },
                },
                failure,
                loading,
                ResponsiveWorkspace(captureCard, attentionCard, inbox, projects, favorites, recent),
            },
        };

        var refresh = new RefreshView
        {
            Content = new ScrollView { Content = Centered(body, 1180) },
        };
        refresh.SetBinding(RefreshView.IsRefreshingProperty, nameof(viewModel.IsRefreshing));
        refresh.SetBinding(RefreshView.CommandProperty, nameof(viewModel.LoadCommand));
        Content = refresh;
    }

    public void FocusCapture()
    {
        Dispatcher.DispatchDelayed(
            TimeSpan.FromMilliseconds(120),
            () => _captureEditor.Focus());
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadCommand.ExecuteAsync(null);
    }

    private static View ResponsiveWorkspace(
        View capture,
        View attention,
        View inbox,
        View projects,
        View favorites,
        View recent)
    {
        if (DeviceInfo.Idiom != DeviceIdiom.Desktop)
        {
            return new VerticalStackLayout
            {
                Spacing = 16,
                Children = { capture, attention, inbox, projects, favorites, recent },
            };
        }

        var left = new VerticalStackLayout
        {
            Spacing = 16,
            Children = { capture, inbox, projects },
        };
        var right = new VerticalStackLayout
        {
            Spacing = 16,
            Children = { attention, favorites, recent },
        };
        var grid = new Grid
        {
            ColumnSpacing = 18,
            ColumnDefinitions =
            {
                new ColumnDefinition(new GridLength(1.35, GridUnitType.Star)),
                new ColumnDefinition(new GridLength(1, GridUnitType.Star)),
            },
            Children = { left, right },
        };
        Grid.SetColumn(right, 1);
        return grid;
    }

    private static View ProjectSection(DashboardViewModel viewModel)
    {
        var collection = new CollectionView
        {
            SelectionMode = SelectionMode.Single,
            MaximumHeightRequest = 260,
            ItemTemplate = new DataTemplate(() =>
            {
                var name = new Label
                {
                    FontAttributes = FontAttributes.Bold,
                    TextColor = SecondBrainVisual.Ink,
                };
                name.SetBinding(Label.TextProperty, nameof(DashboardProject.Name));
                var outcome = new Label
                {
                    FontSize = 13,
                    TextColor = SecondBrainVisual.Muted,
                    MaxLines = 2,
                };
                outcome.SetBinding(Label.TextProperty, nameof(DashboardProject.Outcome));
                return new VerticalStackLayout
                {
                    Padding = new Thickness(4, 8),
                    Spacing = 3,
                    Children = { name, outcome },
                };
            }),
        };
        collection.SetBinding(ItemsView.ItemsSourceProperty, nameof(viewModel.ActiveProjects));
        collection.SelectionChanged += async (_, args) =>
        {
            if (args.CurrentSelection.FirstOrDefault() is not DashboardProject project)
            {
                return;
            }

            collection.SelectedItem = null;
            await Shell.Current.GoToAsync(
                "//para",
                new Dictionary<string, object>
                {
                    ["contextKind"] = ParaContextKind.Project.ToString(),
                    ["contextId"] = project.Id.Value.ToString(),
                    ["returnRoute"] = "home",
                });
        };

        var manage = SecondBrainVisual.QuietButton("Manage Projects", "HomeManageProjects");
        manage.HorizontalOptions = LayoutOptions.Start;
        manage.Clicked += async (_, _) => await Shell.Current.GoToAsync(
            "//para",
            new Dictionary<string, object> { ["mode"] = "browse" });

        var empty = EmptyState(
            nameof(viewModel.AreProjectsEmpty),
            "No Projects yet. Create one when a piece of work needs an outcome and an end.");

        return SecondBrainVisual.Card(
            SecondBrainVisual.Eyebrow("Browse"),
            SecondBrainVisual.SectionTitle("Current Projects"),
            empty,
            collection,
            manage);
    }

    private static View ItemSection(
        string title,
        string subtitle,
        string itemsProperty,
        string emptyProperty,
        string emptyMessage,
        string openRoute,
        string? rowHint,
        string returnRoute)
    {
        var collection = new CollectionView
        {
            SelectionMode = SelectionMode.Single,
            MaximumHeightRequest = 260,
            ItemTemplate = new DataTemplate(() =>
            {
                var titleLabel = new Label
                {
                    FontAttributes = FontAttributes.Bold,
                    TextColor = SecondBrainVisual.Ink,
                    LineBreakMode = LineBreakMode.TailTruncation,
                };
                titleLabel.SetBinding(Label.TextProperty, nameof(DashboardItem.Title));
                var preview = new Label
                {
                    FontSize = 13,
                    MaxLines = 2,
                    TextColor = SecondBrainVisual.Muted,
                    LineBreakMode = LineBreakMode.TailTruncation,
                };
                preview.SetBinding(Label.TextProperty, nameof(DashboardItem.Content));
                var stack = new VerticalStackLayout
                {
                    Padding = new Thickness(4, 8),
                    Spacing = 3,
                    Children = { titleLabel, preview },
                };
                if (!string.IsNullOrWhiteSpace(rowHint))
                {
                    stack.Children.Add(new Label
                    {
                        Text = rowHint,
                        FontSize = 12,
                        FontAttributes = FontAttributes.Bold,
                        TextColor = SecondBrainVisual.Accent,
                    });
                }

                return stack;
            }),
        };
        collection.SetBinding(ItemsView.ItemsSourceProperty, itemsProperty);
        collection.SelectionChanged += async (_, args) =>
        {
            if (args.CurrentSelection.FirstOrDefault() is not DashboardItem item)
            {
                return;
            }

            collection.SelectedItem = null;
            await OpenItemAsync(item, openRoute, returnRoute);
        };

        return SecondBrainVisual.Card(
            SecondBrainVisual.SectionTitle(title),
            SecondBrainVisual.Body(subtitle),
            EmptyState(emptyProperty, emptyMessage),
            collection);
    }

    private static Task OpenItemAsync(
        DashboardItem item,
        string route,
        string returnRoute)
    {
        var parameters = new Dictionary<string, object>
        {
            ["itemId"] = item.Id.Value.ToString(),
        };
        if (route == "editor")
        {
            parameters["returnRoute"] = returnRoute;
        }

        return Shell.Current.GoToAsync($"//{route}", parameters);
    }

    private static View EmptyState(string visibilityProperty, string text)
    {
        var label = SecondBrainVisual.Body(text);
        label.SetBinding(IsVisibleProperty, visibilityProperty);
        return label;
    }

    private static View FailureState(DashboardViewModel viewModel)
    {
        var message = new Label { TextColor = SecondBrainVisual.Danger };
        message.SetBinding(Label.TextProperty, nameof(viewModel.ErrorMessage));
        var retry = SecondBrainVisual.SecondaryButton("Retry", "HomeRetry");
        retry.HorizontalOptions = LayoutOptions.Start;
        retry.SetBinding(Button.CommandProperty, nameof(viewModel.LoadCommand));
        var card = SecondBrainVisual.Card(
            SecondBrainVisual.SectionTitle("Home could not be loaded"),
            message,
            retry);
        card.SetBinding(IsVisibleProperty, nameof(viewModel.HasError));
        return card;
    }

    private static Grid Centered(View view, double maxWidth)
    {
        view.MaximumWidthRequest = maxWidth;
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
            if (grid.Width < maxWidth + 40)
            {
                grid.ColumnDefinitions[0].Width = 0;
                grid.ColumnDefinitions[1].Width = GridLength.Star;
                grid.ColumnDefinitions[2].Width = 0;
            }
        };
        return grid;
    }
}

public sealed class InboxPage : ContentPage
{
    private readonly InboxViewModel _viewModel;

    public InboxPage(InboxViewModel viewModel)
    {
        _viewModel = viewModel;
        BindingContext = viewModel;
        Title = "Process";
        BackgroundColor = SecondBrainVisual.Background;

        var items = new CollectionView
        {
            SelectionMode = SelectionMode.Single,
            AutomationId = "ProcessInboxList",
            ItemTemplate = new DataTemplate(() =>
            {
                var title = new Label
                {
                    FontSize = 17,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = SecondBrainVisual.Ink,
                };
                title.SetBinding(Label.TextProperty, nameof(DashboardItem.Title));
                var preview = new Label
                {
                    FontSize = 13,
                    MaxLines = 3,
                    TextColor = SecondBrainVisual.Muted,
                    LineBreakMode = LineBreakMode.TailTruncation,
                };
                preview.SetBinding(Label.TextProperty, nameof(DashboardItem.Content));
                return new Border
                {
                    BackgroundColor = SecondBrainVisual.Surface,
                    Stroke = SecondBrainVisual.Line,
                    StrokeShape = new RoundRectangle { CornerRadius = 12 },
                    Margin = new Thickness(0, 0, 0, 10),
                    Padding = 16,
                    Content = new VerticalStackLayout
                    {
                        Spacing = 5,
                        Children =
                        {
                            title,
                            preview,
                            new Label
                            {
                                Text = "Process · choose its home",
                                FontSize = 12,
                                FontAttributes = FontAttributes.Bold,
                                TextColor = SecondBrainVisual.Accent,
                            },
                        },
                    },
                };
            }),
        };
        items.SetBinding(ItemsView.ItemsSourceProperty, nameof(viewModel.Items));
        items.SelectionChanged += async (_, args) =>
        {
            if (args.CurrentSelection.FirstOrDefault() is not DashboardItem item)
            {
                return;
            }

            items.SelectedItem = null;
            await Shell.Current.GoToAsync(
                "//inbox-process",
                new Dictionary<string, object>
                {
                    ["itemId"] = item.Id.Value.ToString(),
                });
        };

        var emptyTitle = new Label
        {
            Text = "Inbox is clear",
            FontSize = 22,
            FontAttributes = FontAttributes.Bold,
            TextColor = SecondBrainVisual.Success,
            HorizontalTextAlignment = TextAlignment.Center,
        };
        emptyTitle.SetBinding(IsVisibleProperty, nameof(viewModel.IsEmpty));
        var emptyBody = SecondBrainVisual.Body(
            "Nothing is waiting for a home. Capture when something new comes up.");
        emptyBody.HorizontalTextAlignment = TextAlignment.Center;
        emptyBody.SetBinding(IsVisibleProperty, nameof(viewModel.IsEmpty));

        var capture = SecondBrainVisual.PrimaryButton("+ Capture", "ProcessCapture");
        capture.HorizontalOptions = LayoutOptions.Center;
        capture.SetBinding(IsVisibleProperty, nameof(viewModel.IsEmpty));
        capture.Clicked += async (_, _) =>
        {
            await Shell.Current.GoToAsync("//home");
            if (Shell.Current.CurrentPage is MainPage home)
            {
                home.FocusCapture();
            }
        };

        var error = new Label { TextColor = SecondBrainVisual.Danger };
        error.SetBinding(Label.TextProperty, nameof(viewModel.ErrorMessage));
        error.SetBinding(IsVisibleProperty, nameof(viewModel.HasError));
        var retry = SecondBrainVisual.SecondaryButton("Retry", "ProcessRetry");
        retry.SetBinding(IsVisibleProperty, nameof(viewModel.HasError));
        retry.SetBinding(Button.CommandProperty, nameof(viewModel.LoadCommand));

        var loading = new ActivityIndicator
        {
            Color = SecondBrainVisual.Accent,
            HorizontalOptions = LayoutOptions.Start,
        };
        loading.SetBinding(ActivityIndicator.IsRunningProperty, nameof(viewModel.IsLoading));
        loading.SetBinding(IsVisibleProperty, nameof(viewModel.IsLoading));

        var review = SecondBrainVisual.QuietButton("Review Inbox deliberately", "ProcessReview");
        review.HorizontalOptions = LayoutOptions.Start;
        review.Clicked += async (_, _) => await Shell.Current.GoToAsync(
            "//review",
            new Dictionary<string, object>
            {
                ["kind"] = "inbox",
                ["returnRoute"] = "inbox",
            });

        var body = new Grid
        {
            Padding = new Thickness(24, 20, 24, 32),
            MaximumWidthRequest = 940,
            RowSpacing = 12,
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Star),
            },
        };

        var eyebrow = SecondBrainVisual.Eyebrow("Process");
        var heading = SecondBrainVisual.PageTitle("Inbox");
        var guidance = SecondBrainVisual.Body(
            "Captured thoughts wait here only until you choose the right Project, Area, or Resource home.");
        var empty = new VerticalStackLayout
        {
            Spacing = 8,
            Children = { emptyTitle, emptyBody, capture },
        };
        var failure = new VerticalStackLayout
        {
            Spacing = 8,
            Children = { error, retry },
        };

        AddRow(body, eyebrow, 0);
        AddRow(body, heading, 1);
        AddRow(body, guidance, 2);
        AddRow(body, review, 3);
        AddRow(body, loading, 4);
        AddRow(body, failure, 5);
        AddRow(body, empty, 5);
        AddRow(body, items, 6);

        Content = Centered(body, 940);
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadCommand.ExecuteAsync(null);
    }

    private static void AddRow(Grid grid, View view, int row)
    {
        Grid.SetRow(view, row);
        grid.Children.Add(view);
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
            if (grid.Width < maxWidth + 40)
            {
                grid.ColumnDefinitions[0].Width = 0;
                grid.ColumnDefinitions[1].Width = GridLength.Star;
                grid.ColumnDefinitions[2].Width = 0;
            }
        };
        return grid;
    }
}
