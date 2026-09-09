using Microsoft.Maui.Layouts;
using SecondBrain.Application.Ports;
using SecondBrain.Application.UseCases;
using SecondBrain.Presentation.ViewModels;

namespace SecondBrain.Presentation;

public sealed class ReviewPage : ContentPage, IQueryAttributable
{
    private readonly ReviewViewModel _viewModel;
    private bool _configuredByNavigation;
    private bool _resumeConfiguredReview;

    public ReviewPage(ReviewViewModel viewModel)
    {
        _viewModel = viewModel;
        BindingContext = viewModel;
        Title = "Review";
        BackgroundColor = SecondBrainVisual.Background;

        var heading = SecondBrainVisual.PageTitle("Review");
        heading.SetBinding(Label.TextProperty, nameof(viewModel.Title));

        var progress = new Label
        {
            TextColor = SecondBrainVisual.Muted,
            FontSize = 13,
        };
        progress.SetBinding(
            Label.TextProperty,
            nameof(viewModel.RemainingCount),
            stringFormat: "{0} remaining");

        var itemTitle = new Label
        {
            FontSize = 23,
            FontAttributes = FontAttributes.Bold,
            TextColor = SecondBrainVisual.Ink,
        };
        itemTitle.SetBinding(
            Label.TextProperty,
            $"{nameof(viewModel.CurrentItem)}.{nameof(ReviewQueueItem.Title)}");
        var details = new Label
        {
            TextColor = SecondBrainVisual.Muted,
            LineBreakMode = LineBreakMode.WordWrap,
        };
        details.SetBinding(
            Label.TextProperty,
            $"{nameof(viewModel.CurrentItem)}.{nameof(ReviewQueueItem.Details)}");

        var open = SecondBrainVisual.SecondaryButton("Open item", "ReviewOpen");
        open.SetBinding(IsEnabledProperty, nameof(viewModel.CanActOnCurrentItem));
        open.Clicked += async (_, _) => await OpenCurrentAsync();
        var move = SecondBrainVisual.SecondaryButton("Move", "ReviewMove");
        move.SetBinding(IsVisibleProperty, nameof(viewModel.CanMoveCurrentItem));
        move.SetBinding(IsEnabledProperty, nameof(viewModel.CanActOnCurrentItem));
        move.Clicked += async (_, _) => await MoveCurrentAsync();
        var reviewed = SecondBrainVisual.PrimaryButton("Mark reviewed", "ReviewMarkReviewed");
        reviewed.SetBinding(Button.CommandProperty, nameof(viewModel.MarkReviewedCommand));
        var defer = SecondBrainVisual.SecondaryButton("Defer one day", "ReviewDefer");
        defer.SetBinding(Button.CommandProperty, nameof(viewModel.DeferCommand));
        var archive = SecondBrainVisual.SecondaryButton("Archive", "ReviewArchive");
        archive.SetBinding(Button.CommandProperty, nameof(viewModel.ArchiveCommand));

        var current = SecondBrainVisual.Card(
            SecondBrainVisual.Eyebrow("Current item"),
            itemTitle,
            details,
            WrapActions(open, move),
            WrapActions(reviewed, defer, archive));
        current.SetBinding(IsVisibleProperty, nameof(viewModel.HasCurrentItem));

        var changed = BoundLabel(
            nameof(viewModel.ChangedCount),
            "{0} item(s) changed. This review is complete.");
        var completion = SecondBrainVisual.Card(
            SecondBrainVisual.Eyebrow("Complete"),
            new Label
            {
                Text = "Nothing needs attention",
                FontSize = 23,
                FontAttributes = FontAttributes.Bold,
                TextColor = SecondBrainVisual.Success,
            },
            changed);
        completion.SetBinding(IsVisibleProperty, nameof(viewModel.IsComplete));

        var errorMessage = BoundLabel(
            nameof(viewModel.ErrorMessage),
            textColor: SecondBrainVisual.Danger);
        var retry = SecondBrainVisual.SecondaryButton("Retry", "ReviewRetry");
        retry.SetBinding(Button.CommandProperty, nameof(viewModel.LoadCommand));
        var error = SecondBrainVisual.Card(
            SecondBrainVisual.SectionTitle("Review could not be loaded"),
            errorMessage,
            retry);
        error.SetBinding(IsVisibleProperty, nameof(viewModel.HasError));

        var status = BoundLabel(nameof(viewModel.StatusMessage));
        var loading = new ActivityIndicator
        {
            Color = SecondBrainVisual.Accent,
            HorizontalOptions = LayoutOptions.Start,
        };
        loading.SetBinding(ActivityIndicator.IsRunningProperty, nameof(viewModel.IsLoading));
        loading.SetBinding(IsVisibleProperty, nameof(viewModel.IsLoading));

        var back = SecondBrainVisual.QuietButton(
            "Back to where I started",
            "ReviewReturn");
        back.HorizontalOptions = LayoutOptions.Start;
        back.Clicked += async (_, _) =>
            await Shell.Current.GoToAsync($"//{_viewModel.ReturnRoute}");

        var body = new VerticalStackLayout
        {
            Padding = new Thickness(24, 20, 24, 36),
            Spacing = 16,
            MaximumWidthRequest = 900,
            HorizontalOptions = LayoutOptions.Fill,
            Children =
            {
                SecondBrainVisual.Eyebrow("Review"),
                heading,
                SecondBrainVisual.Body(
                    "Review is deliberate maintenance, not another permanent work queue."),
                progress,
                loading,
                error,
                current,
                completion,
                status,
                back,
            },
        };

        Content = new ScrollView { Content = Centered(body, 900) };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        // The primary Review destination should always mean the default due
        // PARA review. Only an explicit route configuration or a deliberate
        // return from an item/move flow may reuse an existing review scope.
        if (!_configuredByNavigation && !_resumeConfiguredReview)
        {
            _viewModel.Configure(
                ReviewQueueKind.Para,
                scopeKind: null,
                scopeId: null,
                returnRoute: "home");
        }

        _configuredByNavigation = false;
        _resumeConfiguredReview = false;
        await _viewModel.LoadCommand.ExecuteAsync(null);
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (!query.ContainsKey("kind"))
        {
            return;
        }

        var queueKind = Value(query, "kind")?.Equals(
            "para",
            StringComparison.OrdinalIgnoreCase) == true
            ? ReviewQueueKind.Para
            : ReviewQueueKind.Inbox;
        ReviewScopeKind? scopeKind = Enum.TryParse<ReviewScopeKind>(
            Value(query, "scopeKind"),
            true,
            out var parsedScope)
            ? parsedScope
            : null;
        Guid? scopeId = Guid.TryParse(Value(query, "scopeId"), out var parsedId)
            ? parsedId
            : null;

        _configuredByNavigation = true;
        _resumeConfiguredReview = false;
        _viewModel.Configure(
            queueKind,
            scopeKind,
            scopeId,
            Value(query, "returnRoute"));
    }

    private async Task OpenCurrentAsync()
    {
        var target = ReviewNavigation.Open(_viewModel.CurrentItem);
        if (target is null)
        {
            return;
        }

        _resumeConfiguredReview = true;
        await Shell.Current.GoToAsync(target.Route, target.Parameters);
    }

    private async Task MoveCurrentAsync()
    {
        var target = ReviewNavigation.Move(_viewModel.CurrentItem);
        if (target is null)
        {
            return;
        }

        _resumeConfiguredReview = true;
        await Shell.Current.GoToAsync(target.Route, target.Parameters);
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

    private static Label BoundLabel(
        string property,
        string? format = null,
        Color? textColor = null)
    {
        var label = new Label
        {
            TextColor = textColor ?? SecondBrainVisual.Muted,
            LineBreakMode = LineBreakMode.WordWrap,
        };
        label.SetBinding(Label.TextProperty, property, stringFormat: format);
        return label;
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

    private static string? Value(IDictionary<string, object> query, string key) =>
        query.TryGetValue(key, out var value) ? value?.ToString() : null;
}
