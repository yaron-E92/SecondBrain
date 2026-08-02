using SecondBrain.Presentation.ViewModels;
using SecondBrain.Application.UseCases;
using Microsoft.Maui.Controls.Shapes;

namespace SecondBrain.Presentation;

public sealed class MainPage : ContentPage
{
    private readonly DashboardViewModel viewModel;

    public MainPage(DashboardViewModel viewModel)
    {
        this.viewModel = viewModel;
        BindingContext = viewModel;
        Title = "Home";
        BackgroundColor = Colors.White;

        var captureEditor = new Editor
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
                        Header("SecondBrain", "Your offline workspace"),
                        FailureState(
                            nameof(viewModel.HasError),
                            nameof(viewModel.ErrorMessage),
                            nameof(viewModel.LoadCommand)),
                        LoadingState(nameof(viewModel.IsLoading)),
                        Section(
                            "Quick capture",
                            captureEditor,
                            captureButton,
                            captureStatus),
                        ItemSection(
                            "Inbox",
                            nameof(viewModel.InboxItems),
                            nameof(viewModel.IsInboxEmpty),
                            "Inbox is clear. Capture a thought above."),
                        ProjectSection(viewModel),
                        ItemSection(
                            "Favorites",
                            nameof(viewModel.Favorites),
                            nameof(viewModel.AreFavoritesEmpty),
                            "Mark an item as a favorite to keep it close."),
                        ItemSection(
                            "Recent",
                            nameof(viewModel.RecentItems),
                            nameof(viewModel.AreRecentItemsEmpty),
                            "Your recently updated items will appear here."),
                        ModuleSection(viewModel)
                    }
                }
            }
        };
        refreshView.SetBinding(
            RefreshView.IsRefreshingProperty,
            nameof(viewModel.IsLoading));
        refreshView.SetBinding(
            RefreshView.CommandProperty,
            nameof(viewModel.LoadCommand));
        Content = refreshView;
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

        return Section(
            "Current Projects",
            EmptyState(
                nameof(viewModel.AreProjectsEmpty),
                "Activate a Project to make it visible here."),
            collection);
    }

    private static View ItemSection(
        string title,
        string itemsProperty,
        string emptyProperty,
        string emptyMessage)
    {
        var collection = new CollectionView
        {
            SelectionMode = SelectionMode.None,
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
                return new VerticalStackLayout
                {
                    Padding = new Thickness(0, 6),
                    Children = { titleLabel, content }
                };
            })
        };
        collection.SetBinding(ItemsView.ItemsSourceProperty, itemsProperty);

        return Section(
            title,
            EmptyState(emptyProperty, emptyMessage),
            collection);
    }

    private static View ModuleSection(DashboardViewModel viewModel)
    {
        var collection = new CollectionView
        {
            ItemTemplate = new DataTemplate(() =>
            {
                var name = new Label
                {
                    FontAttributes = FontAttributes.Bold,
                    TextColor = Colors.Black
                };
                name.SetBinding(Label.TextProperty, nameof(DashboardModuleSlot.Name));
                var emptyMessage = new Label
                {
                    FontSize = 13,
                    TextColor = Colors.DarkSlateGray
                };
                emptyMessage.SetBinding(
                    Label.TextProperty,
                    nameof(DashboardModuleSlot.EmptyMessage));
                return new Border
                {
                    Stroke = Colors.LightGray,
                    StrokeShape = new RoundRectangle { CornerRadius = 8 },
                    Padding = 12,
                    Content = new VerticalStackLayout
                    {
                        Children = { name, emptyMessage }
                    }
                };
            })
        };
        collection.SetBinding(
            ItemsView.ItemsSourceProperty,
            nameof(viewModel.ModuleSlots));

        return Section(
            "Module extensions",
            EmptyState(
                nameof(viewModel.AreModuleSlotsEmpty),
                "No optional modules are enabled."),
            collection);
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
        BackgroundColor = Colors.White;

        var items = new CollectionView
        {
            SelectionMode = SelectionMode.None,
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
                return new Border
                {
                    Stroke = Colors.LightGray,
                    StrokeShape = new RoundRectangle { CornerRadius = 10 },
                    Margin = new Thickness(0, 0, 0, 10),
                    Padding = 14,
                    Content = new VerticalStackLayout
                    {
                        Spacing = 4,
                        Children = { title, content }
                    }
                };
            })
        };
        items.SetBinding(ItemsView.ItemsSourceProperty, nameof(viewModel.Items));

        var emptyMessage = new Label
        {
            Text = "Inbox is clear. Use Home quick capture to add an item.",
            HorizontalTextAlignment = TextAlignment.Center,
            TextColor = Colors.DarkSlateGray
        };
        emptyMessage.SetBinding(IsVisibleProperty, nameof(viewModel.IsEmpty));

        var captureButton = new Button
        {
            Text = "Go to quick capture",
            HorizontalOptions = LayoutOptions.Center
        };
        captureButton.SetBinding(IsVisibleProperty, nameof(viewModel.IsEmpty));
        captureButton.Clicked += async (_, _) =>
            await Shell.Current.GoToAsync("//home");

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
            Text = "Captured thoughts",
            FontSize = 28,
            FontAttributes = FontAttributes.Bold,
            TextColor = Colors.Black
        };
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
        Grid.SetRow(loading, 1);
        Grid.SetRow(statePanel, 2);
        Grid.SetRow(items, 3);

        Content = new Grid
        {
            Padding = 20,
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Star)
            },
            Children =
            {
                heading,
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
