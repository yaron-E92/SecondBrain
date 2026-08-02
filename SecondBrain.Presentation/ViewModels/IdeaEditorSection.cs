using CommunityToolkit.Mvvm.ComponentModel;
using SecondBrain.Domain.Entities;

namespace SecondBrain.Presentation.ViewModels;

public sealed partial class IdeaEditorSection : ObservableObject
{
    [ObservableProperty]
    public partial IdeaMaturity Maturity { get; set; } = IdeaMaturity.Captured;
}
