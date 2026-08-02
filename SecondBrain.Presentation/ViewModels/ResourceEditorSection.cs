using CommunityToolkit.Mvvm.ComponentModel;
using SecondBrain.Domain.Entities;

namespace SecondBrain.Presentation.ViewModels;

public sealed partial class ResourceEditorSection : ObservableObject
{
    [ObservableProperty]
    private ResourceArtifactKind _artifactKind = ResourceArtifactKind.Guide;

    [ObservableProperty]
    private ResourceFreshness _freshness = ResourceFreshness.Draft;

    [ObservableProperty]
    private DateOnly? _reviewDate;
}
