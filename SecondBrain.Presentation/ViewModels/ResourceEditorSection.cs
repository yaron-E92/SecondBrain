using CommunityToolkit.Mvvm.ComponentModel;
using SecondBrain.Domain.Entities;

namespace SecondBrain.Presentation.ViewModels;

public sealed partial class ResourceEditorSection : ObservableObject
{
    [ObservableProperty]
    public partial ResourceArtifactKind ArtifactKind { get; set; } =
        ResourceArtifactKind.Guide;

    [ObservableProperty]
    public partial ResourceFreshness Freshness { get; set; } =
        ResourceFreshness.Draft;

    [ObservableProperty]
    public partial DateOnly? ReviewDate { get; set; }
}
