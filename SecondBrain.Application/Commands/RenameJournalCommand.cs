using SecondBrain.Domain.ValueObjects;

namespace SecondBrain.Application.UseCases;

public sealed record RenameJournalCommand(
    SecondBrainItemId Id,
    string Title);
