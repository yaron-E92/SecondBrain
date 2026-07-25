using SecondBrain.Domain.ValueObjects;

namespace SecondBrain.Application.UseCases;

public sealed record AddJournalEntryCommand(
    SecondBrainItemId JournalId,
    SecondBrainItemId EntryId);
