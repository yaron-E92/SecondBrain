using CommunityToolkit.Mvvm.ComponentModel;
using SecondBrain.Domain.ValueObjects;

namespace SecondBrain.Presentation.ViewModels;

public sealed partial class JournalEntryEditorSection : ObservableObject
{
    [ObservableProperty]
    public partial SecondBrainItemId? JournalId { get; set; }

    [ObservableProperty]
    public partial DateOnly? OccurrenceDate { get; set; }
}
