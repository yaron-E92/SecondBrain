using SecondBrain.Domain.Entities;
using SecondBrain.Presentation.ViewModels;

namespace SecondBrain.Presentation;

public sealed class JournalBrowserPage : ContentPage
{
    private readonly JournalBrowserViewModel _viewModel;
    private readonly Picker _journalPicker;
    private readonly CollectionView _timeline;

    public JournalBrowserPage(JournalBrowserViewModel viewModel)
    {
        _viewModel = viewModel;
        BindingContext = viewModel;
        Title = "Journals";
        BackgroundColor = Colors.White;

        _journalPicker = new Picker
        {
            Title = "Choose a Journal",
            ItemDisplayBinding = new Binding(nameof(Journal.Title)),
        };
        _journalPicker.SetBinding(
            Picker.ItemsSourceProperty,
            nameof(_viewModel.Journals));
        _journalPicker.SetBinding(
            Picker.SelectedItemProperty,
            nameof(_viewModel.SelectedJournal),
            BindingMode.TwoWay);

        _timeline = new CollectionView
        {
            SelectionMode = SelectionMode.Single,
            ItemTemplate = new DataTemplate(() =>
            {
                var date = new Label
                {
                    FontAttributes = FontAttributes.Bold,
                    TextColor = Colors.DarkSlateBlue,
                };
                date.SetBinding(Label.TextProperty, new Binding(
                    nameof(JournalTimelineEntry.Date),
                    stringFormat: "{0:yyyy-MM-dd}"));

                var title = new Label
                {
                    FontAttributes = FontAttributes.Bold,
                    FontSize = 17,
                };
                title.SetBinding(Label.TextProperty, nameof(JournalTimelineEntry.Title));

                var content = new Label
                {
                    LineBreakMode = LineBreakMode.TailTruncation,
                    MaxLines = 3,
                    TextColor = Colors.DarkSlateGray,
                };
                content.SetBinding(Label.TextProperty, nameof(JournalTimelineEntry.Content));

                var context = new Label
                {
                    FontSize = 12,
                    TextColor = Colors.Gray,
                };
                context.SetBinding(
                    Label.TextProperty,
                    nameof(JournalTimelineEntry.ParaContext));

                return new Border
                {
                    Stroke = Colors.LightGray,
                    StrokeThickness = 1,
                    Padding = 12,
                    Margin = new Thickness(0, 4),
                    Content = new VerticalStackLayout
                    {
                        Spacing = 4,
                        Children = { date, title, content, context },
                    },
                };
            }),
        };
        _timeline.SetBinding(
            ItemsView.ItemsSourceProperty,
            nameof(_viewModel.Timeline));
        _timeline.SelectionChanged += async (_, args) =>
        {
            if (args.CurrentSelection.FirstOrDefault() is not JournalTimelineEntry entry)
            {
                return;
            }

            _timeline.SelectedItem = null;
            if (_viewModel.IsSelectedArchived)
            {
                await DisplayAlertAsync(
                    "Archived Journal",
                    "Restore this Journal before editing its entries.",
                    "OK");
                return;
            }

            await Shell.Current.GoToAsync(
                "//editor",
                new Dictionary<string, object>
                {
                    ["itemId"] = entry.Id.Value.ToString(),
                    ["returnRoute"] = "journals",
                });
        };

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = 20,
                Spacing = 12,
                Children =
                {
                    Header(),
                    LoadingAndErrors(),
                    JournalSelector(),
                    JournalEditor(),
                    TimelineSection(),
                },
            },
        };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadAsync();
    }

    private static View Header() => new VerticalStackLayout
    {
        Spacing = 4,
        Children =
        {
            new Label
            {
                Text = "Journals",
                FontSize = 30,
                FontAttributes = FontAttributes.Bold,
            },
            new Label
            {
                Text = "Create a Journal, then return to its persisted timeline.",
                TextColor = Colors.DarkSlateGray,
            },
        },
    };

    private View LoadingAndErrors()
    {
        var loading = new ActivityIndicator { Color = Colors.DarkSlateBlue };
        loading.SetBinding(
            ActivityIndicator.IsRunningProperty,
            nameof(_viewModel.IsLoading));
        loading.SetBinding(IsVisibleProperty, nameof(_viewModel.IsLoading));

        var error = new Label { TextColor = Colors.DarkRed };
        error.SetBinding(Label.TextProperty, nameof(_viewModel.ErrorMessage));
        error.SetBinding(IsVisibleProperty, nameof(_viewModel.HasError));

        return new VerticalStackLayout { Children = { loading, error } };
    }

    private View JournalSelector()
    {
        var empty = new Label
        {
            Text = "No Journals yet. Create one to open an empty timeline.",
            TextColor = Colors.DarkSlateGray,
        };
        empty.SetBinding(IsVisibleProperty, nameof(_viewModel.IsEmpty));

        var create = new Button { Text = "New Journal" };
        create.Clicked += (_, _) => _viewModel.BeginCreateJournal();

        return new VerticalStackLayout
        {
            Spacing = 8,
            Children = { empty, _journalPicker, create },
        };
    }

    private View JournalEditor()
    {
        var title = new Entry { Placeholder = "Journal title" };
        title.SetBinding(
            Entry.TextProperty,
            nameof(_viewModel.JournalTitle),
            BindingMode.TwoWay);

        var save = new Button { Text = "Save Journal" };
        save.Clicked += async (_, _) => await _viewModel.SaveJournalAsync();

        var cancel = new Button { Text = "Cancel" };
        cancel.Clicked += (_, _) => _viewModel.CancelJournalEdit();

        var editor = new VerticalStackLayout
        {
            Spacing = 8,
            Children =
            {
                title,
                new HorizontalStackLayout
                {
                    Spacing = 8,
                    Children = { cancel, save },
                },
            },
        };
        editor.SetBinding(
            IsVisibleProperty,
            nameof(_viewModel.IsJournalEditorVisible));
        return editor;
    }

    private View TimelineSection()
    {
        var selectedTitle = new Label
        {
            FontSize = 22,
            FontAttributes = FontAttributes.Bold,
        };
        selectedTitle.SetBinding(
            Label.TextProperty,
            $"{nameof(_viewModel.SelectedJournal)}.{nameof(Journal.Title)}");

        var archived = new Label
        {
            Text = "Archived — restore this Journal to add or edit entries.",
            TextColor = Colors.DarkOrange,
            FontAttributes = FontAttributes.Bold,
        };
        archived.SetBinding(
            IsVisibleProperty,
            nameof(_viewModel.IsSelectedArchived));

        var emptyTimeline = new Label
        {
            Text = "This timeline is empty. Add the first dated entry.",
            TextColor = Colors.DarkSlateGray,
        };
        emptyTimeline.SetBinding(
            IsVisibleProperty,
            nameof(_viewModel.IsTimelineEmpty));

        var addEntry = new Button { Text = "Add entry" };
        addEntry.SetBinding(IsEnabledProperty, nameof(_viewModel.CanAddEntry));
        addEntry.Clicked += async (_, _) => await AddEntryAsync();

        var rename = new Button { Text = "Rename" };
        rename.SetBinding(IsEnabledProperty, nameof(_viewModel.CanArchive));
        rename.Clicked += (_, _) => _viewModel.BeginRenameJournal();

        var archive = new Button { Text = "Archive" };
        archive.SetBinding(IsVisibleProperty, nameof(_viewModel.CanArchive));
        archive.Clicked += async (_, _) =>
        {
            var confirmed = await DisplayAlertAsync(
                "Archive Journal?",
                "The Journal becomes read-only. Its entries and PARA links are preserved. Permanent delete is not available.",
                "Archive",
                "Cancel");
            if (confirmed)
            {
                await _viewModel.ArchiveSelectedAsync();
            }
        };

        var restore = new Button { Text = "Restore" };
        restore.SetBinding(IsVisibleProperty, nameof(_viewModel.CanRestore));
        restore.Clicked += async (_, _) => await _viewModel.RestoreSelectedAsync();

        var lifecycle = new Label
        {
            Text = "Archiving preserves every entry and context link; Journals are never hard-deleted.",
            FontSize = 12,
            TextColor = Colors.Gray,
        };

        var panel = new VerticalStackLayout
        {
            Spacing = 8,
            Children =
            {
                selectedTitle,
                archived,
                new HorizontalStackLayout
                {
                    Spacing = 8,
                    Children = { addEntry, rename, archive, restore },
                },
                lifecycle,
                emptyTimeline,
                _timeline,
            },
        };
        panel.SetBinding(IsVisibleProperty, nameof(_viewModel.HasSelectedJournal));
        return panel;
    }

    private async Task AddEntryAsync()
    {
        if (_viewModel.SelectedJournal is not { IsArchived: false } journal)
        {
            return;
        }

        await Shell.Current.GoToAsync(
            "//editor",
            new Dictionary<string, object>
            {
                ["mode"] = "create",
                ["itemKind"] = BrainItemKind.JournalEntry.ToString(),
                ["journalId"] = journal.Id.Value.ToString(),
                ["returnRoute"] = "journals",
            });
    }
}
