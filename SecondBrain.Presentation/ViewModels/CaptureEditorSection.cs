using CommunityToolkit.Mvvm.ComponentModel;
using SecondBrain.Domain.Entities;

namespace SecondBrain.Presentation.ViewModels;

public sealed partial class CaptureEditorSection : ObservableObject
{
    [ObservableProperty]
    private CaptureSourceType _sourceType = CaptureSourceType.Article;

    [ObservableProperty]
    private string _sourceUrl = string.Empty;

    [ObservableProperty]
    private string _sourceCitation = string.Empty;

    [ObservableProperty]
    private DateTimeOffset? _reminderAt;

    [ObservableProperty]
    private CaptureProcessingState _processingState =
        CaptureProcessingState.Captured;
}
