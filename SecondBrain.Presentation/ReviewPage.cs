using Microsoft.Maui.Controls.Shapes;
using SecondBrain.Application.Ports;
using SecondBrain.Application.UseCases;
using SecondBrain.Presentation.ViewModels;

namespace SecondBrain.Presentation;

public sealed class ReviewPage : ContentPage, IQueryAttributable
{
    private readonly ReviewViewModel _viewModel;

    public ReviewPage(ReviewViewModel viewModel)
    {
        _viewModel = viewModel;
        BindingContext = viewModel;
        BackgroundColor = Colors.White;

        var heading = new Label
        {
            FontSize = 28,
            FontAttributes = FontAttributes.Bold,
            TextColor = Colors.Black,
        };
        heading.SetBinding(Label.TextProperty, nameof(viewModel.Title));

        var progress = new Label { TextColor = Colors.DarkSlateGray };
        progress.SetBinding(
            Label.TextProperty,
            nameof(viewModel.RemainingCount),
            stringFormat: "{0} remaining");

        var itemTitle = new Label
        {
            FontSize = 22,
            FontAttributes = FontAttributes.Bold,
            TextColor = Colors.Black,
        };
        itemTitle.SetBinding(
            Label.TextProperty,
            $"{nameof(viewModel.CurrentItem)}.{nameof(ReviewQueueItem.Title)}");
        var details = new Label { TextColor = Colors.DarkSlateGray };
        details.SetBinding(
            Label.TextProperty,
            $"{nameof(viewModel.CurrentItem)}.{nameof(ReviewQueueItem.Details)}");

        var open = new Button { Text = "Open item" };
        open.Clicked += async (_, _) => await OpenCurrentAsync();
        var move = new Button { Text = "Move" };
        move.SetBinding(
            IsVisibleProperty,
            nameof(viewModel.CanMoveCurrentItem));
        move.Clicked += async (_, _) => await MoveCurrentAsync();
        var reviewed = new Button { Text = "Mark reviewed" };
        reviewed.SetBinding(
            Button.CommandProperty,
            nameof(viewModel.MarkReviewedCommand));
        var defer = new Button { Text = "Defer one day" };
        defer.SetBinding(Button.CommandProperty, nameof(viewModel.DeferCommand));
        var archive = new Button { Text = "Archive" };
        archive.SetBinding(Button.CommandProperty, nameof(viewModel.ArchiveCommand));

        var current = new Border
        {
            Stroke = Colors.LightGray,
            StrokeShape = new RoundRectangle { CornerRadius = 12 },
            Padding = 16,
            Content = new VerticalStackLayout
            {
                Spacing = 12,
                Children =
                {
                    itemTitle,
                    details,
                    new HorizontalStackLayout
                    {
                        Spacing = 8,
                        Children = { open, move },
                    },
                    new HorizontalStackLayout
                    {
                        Spacing = 8,
                        Children = { reviewed, defer, archive },
                    },
                },
            },
        };
        current.SetBinding(IsVisibleProperty, nameof(viewModel.HasCurrentItem));

        var completion = new VerticalStackLayout
        {
            Spacing = 8,
            Children =
            {
                new Label
                {
                    Text = "Review complete",
                    FontSize = 22,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = Colors.DarkGreen,
                },
                BoundLabel(
                    nameof(viewModel.ChangedCount),
                    "{0} item(s) changed. Nothing remains due in this review."),
            },
        };
        completion.SetBinding(IsVisibleProperty, nameof(viewModel.IsComplete));

        var error = new VerticalStackLayout
        {
            Spacing = 8,
            Children =
            {
                BoundLabel(nameof(viewModel.ErrorMessage), textColor: Colors.DarkRed),
                CommandButton("Retry", nameof(viewModel.LoadCommand)),
            },
        };
        error.SetBinding(IsVisibleProperty, nameof(viewModel.HasError));

        var status = BoundLabel(nameof(viewModel.StatusMessage));
        var loading = new ActivityIndicator { Color = Colors.DarkSlateBlue };
        loading.SetBinding(
            ActivityIndicator.IsRunningProperty,
            nameof(viewModel.IsLoading));
        loading.SetBinding(IsVisibleProperty, nameof(viewModel.IsLoading));

        var back = new Button { Text = "Back to where I started" };
        back.Clicked += async (_, _) =>
            await Shell.Current.GoToAsync($"//{_viewModel.ReturnRoute}");

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = 20,
                Spacing = 16,
                Children =
                {
                    heading,
                    progress,
                    loading,
                    error,
                    current,
                    completion,
                    status,
                    back,
                },
            },
        };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
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

        _viewModel.Configure(
            queueKind,
            scopeKind,
            scopeId,
            Value(query, "returnRoute"));
    }

    private async Task OpenCurrentAsync()
    {
        var item = _viewModel.CurrentItem;
        if (item is null)
        {
            return;
        }

        if (item.BrainItemId is { } brainItemId)
        {
            await Shell.Current.GoToAsync(
                "//editor",
                new Dictionary<string, object>
                {
                    ["itemId"] = brainItemId.Value.ToString(),
                    ["returnRoute"] = "review",
                });
            return;
        }

        var contextKind = item.TargetKind switch
        {
            ReviewTargetKind.Project => ParaContextKind.Project,
            ReviewTargetKind.Area => ParaContextKind.Area,
            _ => throw new InvalidOperationException("Unknown review target."),
        };
        await Shell.Current.GoToAsync(
            "//para",
            new Dictionary<string, object>
            {
                ["contextKind"] = contextKind.ToString(),
                ["contextId"] = item.TargetId.ToString(),
                ["returnRoute"] = "review",
            });
    }

    private async Task MoveCurrentAsync()
    {
        if (_viewModel.CurrentItem?.BrainItemId is not { } brainItemId)
        {
            return;
        }

        await Shell.Current.GoToAsync(
            "//para",
            new Dictionary<string, object>
            {
                ["mode"] = "move",
                ["itemId"] = brainItemId.Value.ToString(),
                ["returnRoute"] = "review",
            });
    }

    private static Label BoundLabel(
        string property,
        string? format = null,
        Color? textColor = null)
    {
        var label = new Label { TextColor = textColor ?? Colors.DarkSlateGray };
        label.SetBinding(Label.TextProperty, property, stringFormat: format);
        return label;
    }

    private static Button CommandButton(string text, string command)
    {
        var button = new Button { Text = text };
        button.SetBinding(Button.CommandProperty, command);
        return button;
    }

    private static string? Value(IDictionary<string, object> query, string key) =>
        query.TryGetValue(key, out var value) ? value?.ToString() : null;
}
