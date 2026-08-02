using CommunityToolkit.Mvvm.ComponentModel;
using SecondBrain.Domain.Entities;

namespace SecondBrain.Presentation.ViewModels;

public sealed partial class CaptureEditorSection : ObservableObject
{
    [ObservableProperty]
    public partial CaptureSourceType SourceType { get; set; } =
        CaptureSourceType.Article;

    [ObservableProperty]
    public partial string SourceUrl { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string SourceCitation { get; set; } = string.Empty;

    [ObservableProperty]
    public partial DateTimeOffset? ReminderAt { get; set; }

    [ObservableProperty]
    public partial CaptureProcessingState ProcessingState { get; set; } =
        CaptureProcessingState.Captured;
}
