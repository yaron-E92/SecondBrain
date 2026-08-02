using CommunityToolkit.Mvvm.ComponentModel;
using SecondBrain.Domain.ValueObjects;

namespace SecondBrain.Presentation.ViewModels;

public sealed partial class JournalEntryEditorSection : ObservableObject
{
    [ObservableProperty]
    private SecondBrainItemId? _journalId;

    [ObservableProperty]
    private DateOnly? _occurrenceDate;
}
