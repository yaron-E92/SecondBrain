using CommunityToolkit.Mvvm.ComponentModel;
using SecondBrain.Domain.Entities;

namespace SecondBrain.Presentation.ViewModels;

public sealed partial class NoteEditorSection : ObservableObject
{
    [ObservableProperty]
    private NoteKind _kind = NoteKind.General;
}
