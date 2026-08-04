using SecondBrain.Domain.ValueObjects;

namespace SecondBrain.Application.UseCases;

public sealed record ArchiveJournalCommand(SecondBrainItemId Id);
