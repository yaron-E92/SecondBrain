namespace SecondBrain.Application.Ports;

public enum ReviewTargetKind
{
    InboxItem = 0,
    Project = 1,
    Area = 2,
    Resource = 3,
}

public sealed record ReviewState(
    ReviewTargetKind TargetKind,
    Guid TargetId,
    DateTimeOffset? LastReviewedAt = null,
    DateTimeOffset? DeferredUntil = null);
