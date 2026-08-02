using SecondBrain.Domain.Entities;
using SecondBrain.Domain.ValueObjects;

namespace SecondBrain.Presentation.ViewModels;

internal sealed record CoreEditorSnapshot(
    string Title,
    string Content,
    NoteKind NoteKind,
    IdeaMaturity IdeaMaturity,
    CaptureSourceType CaptureSourceType,
    string SourceUrl,
    string SourceCitation,
    DateTimeOffset? ReminderAt,
    CaptureProcessingState CaptureProcessingState,
    ResourceArtifactKind ResourceArtifactKind,
    ResourceFreshness ResourceFreshness,
    DateOnly? ReviewDate,
    SecondBrainItemId? JournalId,
    DateOnly? OccurrenceDate);
