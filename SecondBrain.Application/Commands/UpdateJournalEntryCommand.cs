using SecondBrain.Domain.ValueObjects;

namespace SecondBrain.Application.UseCases;

public sealed record UpdateJournalEntryCommand(
    SecondBrainItemId Id,
    string Title,
    string Content,
    DateOnly EntryDate,
    DateTimeOffset UpdatedAt);
