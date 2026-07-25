namespace SecondBrain.Persistence;

internal sealed class BrainItemRelationRow
{
    public Guid SourceId { get; set; }
    public Guid TargetId { get; set; }
    public BrainItemRelationKind Kind { get; set; }
}
