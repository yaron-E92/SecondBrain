using CommunityToolkit.Mvvm.ComponentModel;
using SecondBrain.Domain.Entities;

namespace SecondBrain.Presentation.ViewModels;

public sealed partial class IdeaEditorSection : ObservableObject
{
    [ObservableProperty]
    private IdeaMaturity _maturity = IdeaMaturity.Captured;
}
