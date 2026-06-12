namespace SecondBrain.Application.Services;

public sealed record ApplicationStatus(
    string Name,
    bool IsReady);
