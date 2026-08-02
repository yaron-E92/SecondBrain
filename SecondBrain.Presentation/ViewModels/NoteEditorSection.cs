using CommunityToolkit.Mvvm.ComponentModel;
using SecondBrain.Domain.Entities;

namespace SecondBrain.Presentation.ViewModels;

public sealed partial class NoteEditorSection : ObservableObject
{
    [ObservableProperty]
    public partial NoteKind Kind { get; set; } = NoteKind.General;
}
