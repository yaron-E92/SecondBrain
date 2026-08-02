using SecondBrain.Domain.Entities;
using SecondBrain.Domain.ValueObjects;

namespace SecondBrain.Persistence;

internal sealed class BrainItemRow
{
    public Guid Id { get; set; }
    public BrainItemKind Kind { get; set; }
    public string Title { get; set; } = "";
    public string Content { get; set; } = "";
    public PrimaryPlacementKind PlacementKind { get; set; }
    public Guid? ProjectId { get; set; }
    public Guid? AreaId { get; set; }
    public Guid? ResourceTopicId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public NoteKind? NoteKind { get; set; }
    public IdeaMaturity? IdeaMaturity { get; set; }
    public DateOnly? EntryDate { get; set; }
    public CaptureSourceType? CaptureSourceType { get; set; }
    public string? SourceUri { get; set; }
    public string? SourceCitation { get; set; }
    public DateTimeOffset? ReminderAt { get; set; }
    public CaptureProcessingState? CaptureProcessingState { get; set; }
    public ResourceArtifactKind? ResourceArtifactKind { get; set; }
    public ResourceFreshness? ResourceFreshness { get; set; }
    public DateOnly? ReviewDate { get; set; }
    public bool IsArchived { get; set; }
    public bool IsFavorite { get; set; }
}
