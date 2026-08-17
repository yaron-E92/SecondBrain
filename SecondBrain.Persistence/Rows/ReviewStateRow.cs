using SecondBrain.Application.Ports;

namespace SecondBrain.Persistence;

internal sealed class ReviewStateRow
{
    public ReviewTargetKind TargetKind { get; set; }
    public Guid TargetId { get; set; }
    public DateTimeOffset? LastReviewedAt { get; set; }
    public DateTimeOffset? DeferredUntil { get; set; }
}
