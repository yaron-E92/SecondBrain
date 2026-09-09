using SecondBrain.Domain.Entities;
using SecondBrain.Domain.ValueObjects;
using SecondBrain.Presentation.ViewModels;

namespace SecondBrain.Presentation;

internal sealed class CoreKnowledgeForm : VerticalStackLayout
{
    private readonly CoreEditorViewModel _viewModel;
    private readonly Entry _reminderEntry;
    private readonly Entry _reviewDateEntry;
    private readonly Entry _occurrenceDateEntry;

    internal CoreKnowledgeForm(CoreEditorViewModel viewModel)
    {
        _viewModel = viewModel;
        BindingContext = viewModel;
        Spacing = 14;

        var title = new Entry
        {
            Placeholder = "Title",
            MinimumHeightRequest = 44,
            AutomationId = "KnowledgeTitle",
        };
        title.SetBinding(Entry.TextProperty, nameof(viewModel.Title));

        var content = new Editor
        {
            Placeholder = "Write the useful part here…",
            AutoSize = EditorAutoSizeOption.TextChanges,
            MinimumHeightRequest = 180,
            AutomationId = "KnowledgeContent",
        };
        content.SetBinding(Editor.TextProperty, nameof(viewModel.Content));

        Children.Add(SecondBrainVisual.Card(
            SecondBrainVisual.Eyebrow("Content"),
            title,
            content));

        var noteKind = EnumPicker<NoteKind>("Note type");
        noteKind.SetBinding(
            Picker.SelectedItemProperty,
            $"{nameof(viewModel.Note)}.{nameof(viewModel.Note.Kind)}");
        AddTypedCard("Note", nameof(viewModel.IsNote), noteKind);

        var ideaMaturity = EnumPicker<IdeaMaturity>("Maturity");
        ideaMaturity.SetBinding(
            Picker.SelectedItemProperty,
            $"{nameof(viewModel.Idea)}.{nameof(viewModel.Idea.Maturity)}");
        AddTypedCard("Idea", nameof(viewModel.IsIdea), ideaMaturity);

        var sourceType = EnumPicker<CaptureSourceType>("Source type");
        sourceType.SetBinding(
            Picker.SelectedItemProperty,
            $"{nameof(viewModel.Capture)}.{nameof(viewModel.Capture.SourceType)}");
        sourceType.SetBinding(IsEnabledProperty, nameof(viewModel.AreTypeFieldsEditable));

        var sourceUrl = new Entry
        {
            Placeholder = "Source URL",
            MinimumHeightRequest = 44,
        };
        sourceUrl.SetBinding(
            Entry.TextProperty,
            $"{nameof(viewModel.Capture)}.{nameof(viewModel.Capture.SourceUrl)}");
        sourceUrl.SetBinding(IsEnabledProperty, nameof(viewModel.AreTypeFieldsEditable));

        var citation = new Entry
        {
            Placeholder = "Source citation",
            MinimumHeightRequest = 44,
        };
        citation.SetBinding(
            Entry.TextProperty,
            $"{nameof(viewModel.Capture)}.{nameof(viewModel.Capture.SourceCitation)}");
        citation.SetBinding(IsEnabledProperty, nameof(viewModel.AreTypeFieldsEditable));

        _reminderEntry = DateEntry("Optional reminder (ISO date/time)");
        _reminderEntry.SetBinding(IsEnabledProperty, nameof(viewModel.AreTypeFieldsEditable));
        _reminderEntry.TextChanged += (_, args) =>
            viewModel.Capture.ReminderAt =
                DateTimeOffset.TryParse(args.NewTextValue, out var value) ? value : null;

        var processingState = EnumPicker<CaptureProcessingState>("Processing state");
        processingState.SetBinding(
            Picker.SelectedItemProperty,
            $"{nameof(viewModel.Capture)}.{nameof(viewModel.Capture.ProcessingState)}");

        AddTypedCard(
            "Capture details",
            nameof(viewModel.IsCapture),
            sourceType,
            sourceUrl,
            citation,
            _reminderEntry,
            processingState);

        var artifactKind = EnumPicker<ResourceArtifactKind>("Resource type");
        artifactKind.SetBinding(
            Picker.SelectedItemProperty,
            $"{nameof(viewModel.Resource)}.{nameof(viewModel.Resource.ArtifactKind)}");
        artifactKind.SetBinding(IsEnabledProperty, nameof(viewModel.AreTypeFieldsEditable));

        var freshness = EnumPicker<ResourceFreshness>("Freshness");
        freshness.SetBinding(
            Picker.SelectedItemProperty,
            $"{nameof(viewModel.Resource)}.{nameof(viewModel.Resource.Freshness)}");

        _reviewDateEntry = DateEntry("Optional review date (yyyy-MM-dd)");
        _reviewDateEntry.TextChanged += (_, args) =>
            viewModel.Resource.ReviewDate =
                DateOnly.TryParse(args.NewTextValue, out var value) ? value : null;

        AddTypedCard(
            "Resource details",
            nameof(viewModel.IsResource),
            artifactKind,
            freshness,
            _reviewDateEntry);

        JournalPicker = new Picker
        {
            Title = "Journal",
            ItemDisplayBinding = new Binding(nameof(Journal.Title)),
            MinimumHeightRequest = 44,
        };
        JournalPicker.SetBinding(IsEnabledProperty, nameof(viewModel.AreTypeFieldsEditable));
        JournalPicker.SelectedIndexChanged += (_, _) =>
            viewModel.JournalEntry.JournalId =
                (JournalPicker.SelectedItem as Journal)?.Id;

        _occurrenceDateEntry = DateEntry("Occurrence date (yyyy-MM-dd)");
        _occurrenceDateEntry.TextChanged += (_, args) =>
            viewModel.JournalEntry.OccurrenceDate =
                DateOnly.TryParse(args.NewTextValue, out var value) ? value : null;

        AddTypedCard(
            "Journal entry",
            nameof(viewModel.IsJournalEntry),
            JournalPicker,
            _occurrenceDateEntry);

        var derivationSummary = SecondBrainVisual.Body(string.Empty);
        derivationSummary.SetBinding(
            Label.TextProperty,
            nameof(viewModel.DerivationSourceSummary));
        var markReferenced = new Switch();
        markReferenced.SetBinding(
            Switch.IsToggledProperty,
            nameof(viewModel.MarkSourcesReferenced));
        var derivationLifecycle = new HorizontalStackLayout
        {
            Spacing = 10,
            Children =
            {
                markReferenced,
                new Label
                {
                    Text = "Mark the source capture as referenced after saving",
                    VerticalTextAlignment = TextAlignment.Center,
                    TextColor = SecondBrainVisual.Muted,
                },
            },
        };
        AddTypedCard(
            "Derived from",
            nameof(viewModel.IsDeriving),
            derivationSummary,
            derivationLifecycle);
    }

    internal Picker JournalPicker { get; }

    internal void SetJournals(IEnumerable<Journal> journals, SecondBrainItemId? selectedId = null)
    {
        var choices = journals
            .Where(journal => !journal.IsArchived)
            .OrderBy(journal => journal.Title)
            .ToArray();
        JournalPicker.ItemsSource = choices;
        JournalPicker.SelectedItem = selectedId is null
            ? choices.FirstOrDefault()
            : choices.FirstOrDefault(journal => journal.Id == selectedId.Value);
    }

    internal void SyncDates()
    {
        _reminderEntry.Text = _viewModel.Capture.ReminderAt?.ToString("O") ?? string.Empty;
        _reviewDateEntry.Text = _viewModel.Resource.ReviewDate?.ToString("yyyy-MM-dd") ?? string.Empty;
        _occurrenceDateEntry.Text =
            _viewModel.JournalEntry.OccurrenceDate?.ToString("yyyy-MM-dd") ?? string.Empty;
    }

    private void AddTypedCard(string title, string visibilityProperty, params View[] children)
    {
        var card = SecondBrainVisual.Card(
            SecondBrainVisual.Eyebrow(title),
            children);
        card.SetBinding(IsVisibleProperty, visibilityProperty);
        Children.Add(card);
    }

    private static Picker EnumPicker<T>(string title)
        where T : struct, Enum =>
        new()
        {
            Title = title,
            ItemsSource = Enum.GetValues<T>(),
            MinimumHeightRequest = 44,
        };

    private static Entry DateEntry(string placeholder) =>
        new()
        {
            Placeholder = placeholder,
            Keyboard = Keyboard.Text,
            MinimumHeightRequest = 44,
        };
}
