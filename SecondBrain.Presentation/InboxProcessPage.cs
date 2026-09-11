using Microsoft.Maui.Layouts;
using SecondBrain.Domain.ValueObjects;
using SecondBrain.Presentation.ViewModels;

namespace SecondBrain.Presentation;

public sealed class InboxProcessPage : ContentPage, IQueryAttributable
{
    private readonly InboxProcessViewModel _viewModel;
    private SecondBrainItemId? _pendingItemId;

    public InboxProcessPage(InboxProcessViewModel viewModel)
    {
        _viewModel = viewModel;
        BindingContext = viewModel;
        Title = "Process";
        BackgroundColor = SecondBrainVisual.Background;

        var title = new Entry
        {
            Placeholder = "What is this?",
            AutomationId = "ProcessTitle",
        };
        title.SetBinding(Entry.TextProperty, nameof(viewModel.Title));

        var content = new Editor
        {
            Placeholder = "Keep, clarify, or rewrite the thought before giving it a home.",
            AutoSize = EditorAutoSizeOption.TextChanges,
            MinimumHeightRequest = 150,
            AutomationId = "ProcessContent",
        };
        content.SetBinding(Editor.TextProperty, nameof(viewModel.Content));

        var destination = new Picker
        {
            Title = "Choose a Project, Area, or Resource",
            ItemDisplayBinding = new Binding(nameof(InboxPlacementOption.Label)),
            MinimumHeightRequest = 44,
            AutomationId = "ProcessDestination",
        };
        destination.SetBinding(Picker.ItemsSourceProperty, nameof(viewModel.Destinations));
        destination.SetBinding(
            Picker.SelectedItemProperty,
            nameof(viewModel.SelectedDestination),
            mode: BindingMode.TwoWay);

        var manageHomes = SecondBrainVisual.SecondaryButton(
            "Manage Projects, Areas & Resources",
            "ProcessManageHomes");
        manageHomes.Clicked += async (_, _) =>
        {
            var itemId = _viewModel.ItemId ?? _pendingItemId;
            if (itemId is null)
            {
                return;
            }

            await Shell.Current.GoToAsync(
                "//para",
                new Dictionary<string, object>
                {
                    ["mode"] = "browse",
                    ["returnRoute"] = "inbox-process",
                    ["itemId"] = itemId.Value.Value.ToString(),
                });
        };

        var process = SecondBrainVisual.PrimaryButton(
            "Process item",
            "ProcessConfirm");
        process.SetBinding(IsEnabledProperty, nameof(viewModel.CanProcess));
        process.Clicked += async (_, _) =>
        {
            await _viewModel.ProcessCommand.ExecuteAsync(null);
            if (_viewModel.WasProcessed)
            {
                await Shell.Current.GoToAsync("//inbox");
            }
        };

        var cancel = SecondBrainVisual.QuietButton("Cancel", "ProcessCancel");
        cancel.Clicked += async (_, _) => await Shell.Current.GoToAsync("//inbox");

        var loading = new ActivityIndicator
        {
            Color = SecondBrainVisual.Accent,
            HorizontalOptions = LayoutOptions.Start,
        };
        loading.SetBinding(ActivityIndicator.IsRunningProperty, nameof(viewModel.IsLoading));
        loading.SetBinding(IsVisibleProperty, nameof(viewModel.IsLoading));

        var status = new Label
        {
            FontSize = 13,
            TextColor = SecondBrainVisual.Muted,
        };
        status.SetBinding(Label.TextProperty, nameof(viewModel.StatusMessage));

        var error = new Label
        {
            FontSize = 13,
            TextColor = SecondBrainVisual.Danger,
            AutomationId = "ProcessError",
        };
        error.SetBinding(Label.TextProperty, nameof(viewModel.ErrorMessage));
        error.SetBinding(IsVisibleProperty, nameof(viewModel.HasError));

        var noDestinations = SecondBrainVisual.Card(
            SecondBrainVisual.SectionTitle("Create a home first"),
            SecondBrainVisual.Body(
                "Processing means moving this thought out of Inbox. Create at least one Project, Area, or Resource Topic, then come back here."),
            manageHomes);
        noDestinations.SetBinding(
            IsVisibleProperty,
            nameof(viewModel.HasDestinations),
            converter: new InvertedBooleanConverter());

        var form = SecondBrainVisual.Card(
            SecondBrainVisual.SectionTitle("Clarify the thought"),
            SecondBrainVisual.Body(
                "Edit only what helps you recognize and use it later. Classification is still optional; its home is not."),
            title,
            content,
            SecondBrainVisual.SectionTitle("Give it a home"),
            destination);
        form.SetBinding(IsVisibleProperty, nameof(viewModel.HasDestinations));

        var actions = new FlexLayout
        {
            Direction = FlexDirection.Row,
            Wrap = FlexWrap.Wrap,
            JustifyContent = FlexJustify.End,
            AlignItems = FlexAlignItems.Center,
            Children = { cancel, process },
        };

        var body = new VerticalStackLayout
        {
            Padding = new Thickness(24, 22, 24, 32),
            Spacing = 16,
            MaximumWidthRequest = 820,
            HorizontalOptions = LayoutOptions.Fill,
            Children =
            {
                SecondBrainVisual.Eyebrow("Process"),
                SecondBrainVisual.PageTitle("Give this thought a home"),
                SecondBrainVisual.Body(
                    "Inbox is temporary. Clarify what matters, choose where it belongs, and move on."),
                loading,
                error,
                form,
                noDestinations,
                status,
                actions,
            },
        };

        Content = new ScrollView
        {
            Content = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition(GridLength.Star),
                    new ColumnDefinition(new GridLength(820)),
                    new ColumnDefinition(GridLength.Star),
                },
                Children = { body },
            },
        };
        if (Content is ScrollView { Content: Grid host })
        {
            Grid.SetColumn(body, 1);
            host.SizeChanged += (_, _) =>
            {
                var wide = host.Width >= 900;
                host.ColumnDefinitions[0].Width = wide ? GridLength.Star : 0;
                host.ColumnDefinitions[1].Width = wide ? new GridLength(820) : GridLength.Star;
                host.ColumnDefinitions[2].Width = wide ? GridLength.Star : 0;
            };
        }
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        _pendingItemId = query.TryGetValue("itemId", out var value) &&
            Guid.TryParse(value?.ToString(), out var parsed) &&
            parsed != Guid.Empty
            ? new SecondBrainItemId(parsed)
            : null;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_pendingItemId is { } itemId)
        {
            await _viewModel.LoadAsync(itemId);
        }
    }

    private sealed class InvertedBooleanConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture) =>
            value is bool flag && !flag;

        public object ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture) =>
            throw new NotSupportedException();
    }
}
