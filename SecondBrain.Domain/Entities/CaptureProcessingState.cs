namespace SecondBrain.Domain.Entities;

public enum CaptureProcessingState
{
    Captured = 1,
    Consuming = 2,
    Distilled = 3,
    Referenced = 4,
}
