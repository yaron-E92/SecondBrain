using SecondBrain.Presentation.ViewModels;
using SecondBrain.Application.UseCases;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Layouts;

namespace SecondBrain.Presentation;

public sealed class MainPage : ContentPage
{
    private readonly DashboardViewModel viewModel;
    private readonly Editor captureEditor;

    public MainPage(DashboardViewModel viewModel)
    {
        this.viewModel = viewModel;
        BindingContext = viewModel;
        Title = "Home";
        BackgroundColor = Color.FromArgb("#F6F8FB");

        captureEditor = new Editor
        {
            AutoSize = EditorAutoSizeOption.TextChanges,
            MinimumHeightRequest = 88,
            Placeholder = "Capture a thought while it is fresh..."
        };
        captureEditor.SetBinding(Editor.TextProperty, nameof(viewModel.CaptureText));

        var captureButton = new Button
        {
            Text = "Save to Inbox",
            HorizontalOptions = LayoutOptions.End
        };
        captureButton.SetBinding(
            Button.CommandProperty,
            nameof(viewModel.CaptureCommand));

        var captureStatus = new Label
        {
            FontSize = 13,
            TextColor = Colors.DarkGreen
        };
        captureStatus.SetBinding(Label.TextProperty, nameof(viewModel.CaptureStatus));

        var refreshView = new RefreshView
        {
            Content = new ScrollView
            {
                Content = new VerticalStackLayout
                {
                    Padding = new Thickness(20, 16),
                    Spacing = 20,
                    Children =
                    {
                        Header("Home", "OFFLINE WORKSPACE"),
                        FailureState(
                            nameof(viewModel.HasError),
                            nameof(viewModel.ErrorMessage),
                            nameof(viewModel.LoadCommand)),
                        LoadingState(nameof(viewModel.IsLoading)),
                        Section(
                            "Get the thought out of your head.",
                            new Label
                            {
                                Text = "Capture first. Decide where it belongs when you have the attention for it.",
                                TextColor = Color.FromArgb("#65717B")
                            },
                            captureEditor,
                            captureButton,
                            captureStatus),
                        AttentionSection(),
                        ItemSection(
                            "Inbox",
                            nameof(viewModel.InboxItems),
                            nameof(viewModel.IsInboxEmpty),
                            "Inbox is clear. Capture a thought above.",
                            "home",
                            "Open to create a Note or Resource"),
                        ProjectSection(viewModel),
                        ItemSection(
                            "Favorites",
                            nameof(viewModel.Favorites),
                            nameof(viewModel.AreFavoritesEmpty),
                            "Mark an item as a favorite to keep it close.",
                            "home"),
                        ItemSection(
                            "Recent",
                            nameof(viewModel.RecentItems),
                            nameof(viewModel.AreRecentItemsEmpty),
                            "Your recently updated items will appear here.",
                            "home")
                    }
                }
            }
        };
        refreshView.SetBinding(
            RefreshView.IsRefreshingProperty,
            nameof(viewModel.IsRefreshing));
        refreshView.SetBinding(
            RefreshView.CommandProperty,
            nameof(viewModel.LoadCommand));
        Content = refreshView;
    }

    public void FocusCapture()
    {
        Dispatcher.DispatchDelayed(
            TimeSpan.FromMilliseconds(150),
            () => captureEditor.Focus());
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await viewModel.LoadCommand.ExecuteAsync(null);
    }

    private static View ProjectSection(DashboardViewModel viewModel)
    {
        var collection = new CollectionView
        {
            SelectionMode = SelectionMode.Single,
            ItemTemplate = new DataTemplate(() =>
            {
                var name = new Label
                {
                    FontAttributes = FontAttributes.Bold,
                    TextColor = Colors.Black
                };
                name.SetBinding(Label.TextProperty, nameof(DashboardProject.Name));
                var outcome = new Label
                {
                    FontSize = 13,
                    TextColor = Colors.DarkSlateGray
                };
                outcome.SetBinding(
                    Label.TextProperty,
                    nameof(DashboardProject.Outcome));
                return new VerticalStackLayout
                {
                    Padding = new Thickness(0, 6),
                    Children = { name, outcome }
                };
            })
        };
        collection.SetBinding(
            ItemsView.ItemsSourceProperty,
            nameof(viewModel.ActiveProjects));
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

        var manageProjects = new Button
        {
            Text = "Create or manage Projects",
            HorizontalOptions = LayoutOptions.Start
        };
        manageProjects.Clicked += async (_, _) =>
            await Shell.Current.GoToAsync(
                "//para",
                new Dictionary<string, object> { ["mode"] = "browse" });

        return Section(
            "Current Projects",
            EmptyState(
                nameof(viewModel.AreProjectsEmpty),
                "No Projects yet. Create one to give current work a home."),
            manageProjects,
            collection);
    }

    private static View AttentionSection()
    {
        var inbox = new Button
        {
            Text = "Process Inbox",
            HorizontalOptions = LayoutOptions.Start,
            AutomationId = "HomeProcessInbox",
        };
        inbox.Clicked += async (_, _) => await Shell.Current.GoToAsync("//inbox");

        return Section(
            "What needs attention",
            new Label
            {
                Text = "Give captured thoughts a home, or enter a deliberate maintenance review.",
                TextColor = Color.FromArgb("#65717B"),
            },
            new FlexLayout
            {
                Direction = FlexDirection.Row,
                Wrap = FlexWrap.Wrap,
                AlignItems = FlexAlignItems.Start,
                Children =
                {
                    inbox,
                    ReviewButton("Start Review", "para", "home"),
                },
            });
    }

    private static View ItemSection(
        string title,
        string itemsProperty,
        string emptyProperty,
        string emptyMessage,
        string returnRoute,
        string? rowHint = null)
    {
        var collection = new CollectionView
        {
            SelectionMode = SelectionMode.Single,
            ItemTemplate = new DataTemplate(() =>
            {
                var titleLabel = new Label
                {
                    FontAttributes = FontAttributes.Bold,
                    TextColor = Colors.Black,
                    LineBreakMode = LineBreakMode.TailTruncation
                };
                titleLabel.SetBinding(
                    Label.TextProperty,
                    nameof(DashboardItem.Title));
                var content = new Label
                {
                    FontSize = 13,
                    MaxLines = 2,
                    TextColor = Colors.DarkSlateGray,
                    LineBreakMode = LineBreakMode.TailTruncation
                };
                content.SetBinding(
                    Label.TextProperty,
                    nameof(DashboardItem.Content));
                var layout = new VerticalStackLayout
                {
                    Padding = new Thickness(0, 6),
                    Children = { titleLabel, content }
                };
                if (rowHint is not null)
                {
                    layout.Children.Add(new Label
                    {
                        Text = rowHint,
                        FontSize = 12,
                        TextColor = Colors.DarkSlateBlue
                    });
                }

                return layout;
            })
        };
        collection.SetBinding(ItemsView.ItemsSourceProperty, itemsProperty);
        collection.SelectionChanged += async (_, args) =>
        {
            if (args.CurrentSelection.FirstOrDefault() is not DashboardItem item)
            {
                return;
            }

            collection.SelectedItem = null;
            await OpenItemAsync(item, returnRoute);
        };

        return Section(
            title,
            EmptyState(emptyProperty, emptyMessage),
            collection);
    }

    private static async Task OpenItemAsync(
        DashboardItem item,
        string returnRoute) =>
        await Shell.Current.GoToAsync(
            "//editor",
            new Dictionary<string, object>
            {
                ["itemId"] = item.Id.Value.ToString(),
                ["returnRoute"] = returnRoute,
            });

    private static Button ReviewButton(
        string text,
        string kind,
        string returnRoute)
    {
        var button = new Button
        {
            Text = text,
            HorizontalOptions = LayoutOptions.Start,
        };
        button.Clicked += async (_, _) =>
            await Shell.Current.GoToAsync(
                "//review",
                new Dictionary<string, object>
                {
                    ["kind"] = kind,
                    ["returnRoute"] = returnRoute,
                });
        return button;
    }

    private static View Header(string title, string subtitle) =>
        new VerticalStackLayout
        {
            Children =
            {
                new Label
                {
                    Text = title,
                    FontSize = 30,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = Colors.Black
                },
                new Label
                {
                    Text = subtitle,
                    FontSize = 15,
                    TextColor = Colors.DarkSlateGray
                }
            }
        };

    private static View Section(string title, params View[] content)
    {
        var children = new List<IView>
        {
            new Label
            {
                Text = title,
                FontSize = 20,
                FontAttributes = FontAttributes.Bold,
                TextColor = Colors.Black
            }
        };
        children.AddRange(content);

        var sectionContent = new VerticalStackLayout { Spacing = 8 };
        foreach (var child in children)
        {
            sectionContent.Children.Add(child);
        }

        return new Border
        {
            Stroke = Colors.LightGray,
            StrokeShape = new RoundRectangle { CornerRadius = 12 },
            Padding = 16,
            Content = sectionContent
        };
    }

    private static View EmptyState(string visibilityProperty, string text)
    {
        var label = new Label
        {
            Text = text,
            FontSize = 13,
            TextColor = Colors.DarkSlateGray
        };
        label.SetBinding(IsVisibleProperty, visibilityProperty);
        return label;
    }

    private static View LoadingState(string loadingProperty)
    {
        var indicator = new ActivityIndicator
        {
            Color = Colors.DarkSlateBlue,
            HorizontalOptions = LayoutOptions.Center
        };
        indicator.SetBinding(
            ActivityIndicator.IsRunningProperty,
            loadingProperty);
        indicator.SetBinding(IsVisibleProperty, loadingProperty);
        return indicator;
    }

    private static View FailureState(
        string visibilityProperty,
        string messageProperty,
        string retryCommandProperty)
    {
        var message = new Label { TextColor = Colors.DarkRed };
        message.SetBinding(Label.TextProperty, messageProperty);
        var retryButton = new Button
        {
            Text = "Retry",
            HorizontalOptions = LayoutOptions.Start
        };
        retryButton.SetBinding(
            Button.CommandProperty,
            retryCommandProperty);
        var layout = new VerticalStackLayout
        {
            Spacing = 8,
            Children = { message, retryButton }
        };
        layout.SetBinding(IsVisibleProperty, visibilityProperty);
        return layout;
    }
}

public sealed class InboxPage : ContentPage
{
    private readonly InboxViewModel viewModel;

    public InboxPage(InboxViewModel viewModel)
    {
        this.viewModel = viewModel;
        BindingContext = viewModel;
        Title = "Inbox";
        BackgroundColor = Color.FromArgb("#F6F8FB");

        var items = new CollectionView
        {
            SelectionMode = SelectionMode.Single,
            ItemTemplate = new DataTemplate(() =>
            {
                var title = new Label
                {
                    FontAttributes = FontAttributes.Bold,
                    TextColor = Colors.Black
                };
                title.SetBinding(Label.TextProperty, nameof(DashboardItem.Title));
                var content = new Label
                {
                    FontSize = 14,
                    MaxLines = 3,
                    TextColor = Colors.DarkSlateGray,
                    LineBreakMode = LineBreakMode.TailTruncation
                };
                content.SetBinding(
                    Label.TextProperty,
                    nameof(DashboardItem.Content));
                var hint = new Label
                {
                    Text = "Open to create a Note or Resource",
                    FontSize = 12,
                    TextColor = Colors.DarkSlateBlue
                };
                return new Border
                {
                    Stroke = Colors.LightGray,
                    StrokeShape = new RoundRectangle { CornerRadius = 10 },
                    Margin = new Thickness(0, 0, 0, 10),
                    Padding = 14,
                    Content = new VerticalStackLayout
                    {
                        Spacing = 4,
                        Children = { title, content, hint }
                    }
                };
            })
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
                "//editor",
                new Dictionary<string, object>
                {
                    ["itemId"] = item.Id.Value.ToString(),
                    ["returnRoute"] = "inbox",
                });
        };

        var emptyMessage = new Label
        {
            Text = "Inbox is clear. Use Home quick capture to add an item.",
            HorizontalTextAlignment = TextAlignment.Center,
            TextColor = Colors.DarkSlateGray
        };
        emptyMessage.SetBinding(IsVisibleProperty, nameof(viewModel.IsEmpty));

        var captureButton = new Button
        {
            Text = "+ Capture",
            HorizontalOptions = LayoutOptions.Center
        };
        captureButton.SetBinding(IsVisibleProperty, nameof(viewModel.IsEmpty));
        captureButton.Clicked += async (_, _) =>
        {
            await Shell.Current.GoToAsync("//home");
            if (Shell.Current.CurrentPage is MainPage home)
            {
                home.FocusCapture();
            }
        };

        var errorMessage = new Label { TextColor = Colors.DarkRed };
        errorMessage.SetBinding(
            Label.TextProperty,
            nameof(viewModel.ErrorMessage));
        errorMessage.SetBinding(
            IsVisibleProperty,
            nameof(viewModel.HasError));

        var retryButton = new Button
        {
            Text = "Retry",
            HorizontalOptions = LayoutOptions.Center
        };
        retryButton.SetBinding(
            IsVisibleProperty,
            nameof(viewModel.HasError));
        retryButton.SetBinding(
            Button.CommandProperty,
            nameof(viewModel.LoadCommand));

        var loading = new ActivityIndicator
        {
            Color = Colors.DarkSlateBlue,
            HorizontalOptions = LayoutOptions.Center
        };
        loading.SetBinding(
            ActivityIndicator.IsRunningProperty,
            nameof(viewModel.IsLoading));
        loading.SetBinding(
            IsVisibleProperty,
            nameof(viewModel.IsLoading));

        var heading = new Label
        {
            Text = "Inbox",
            FontSize = 28,
            FontAttributes = FontAttributes.Bold,
            TextColor = Colors.Black
        };
        var guidance = new Label
        {
            Text = "Captured thoughts wait here until you choose the right home. Processing one successfully removes it from this queue.",
            TextColor = Color.FromArgb("#65717B"),
        };
        var reviewButton = new Button
        {
            Text = "Review Inbox deliberately",
            HorizontalOptions = LayoutOptions.Start,
        };
        reviewButton.Clicked += async (_, _) =>
            await Shell.Current.GoToAsync(
                "//review",
                new Dictionary<string, object>
                {
                    ["kind"] = "inbox",
                    ["returnRoute"] = "inbox",
                });
        var statePanel = new VerticalStackLayout
        {
            Spacing = 8,
            Children =
            {
                errorMessage,
                retryButton,
                emptyMessage,
                captureButton
            }
        };
        Grid.SetRow(heading, 0);
        Grid.SetRow(guidance, 1);
        Grid.SetRow(reviewButton, 2);
        Grid.SetRow(loading, 3);
        Grid.SetRow(statePanel, 4);
        Grid.SetRow(items, 5);

        Content = new Grid
        {
            Padding = 20,
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Star)
            },
            Children =
            {
                heading,
                guidance,
                reviewButton,
                loading,
                statePanel,
                items
            }
        };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await viewModel.LoadCommand.ExecuteAsync(null);
    }
}
