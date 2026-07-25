namespace SecondBrain.Application.UseCases;

public enum BrainItemLifecycleTransition
{
    SharpenIdea = 1,
    MakeIdeaActionable = 2,
    StartConsumingCapture = 3,
    MarkCaptureDistilled = 4,
    MarkCaptureReferenced = 5,
    MarkResourceCurrent = 6,
    MarkResourceOutdated = 7,
    MarkFavorite = 8,
    UnmarkFavorite = 9,
}
