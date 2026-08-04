using SecondBrain.Domain.ValueObjects;

namespace SecondBrain.Application.UseCases;

public sealed record RestoreJournalCommand(SecondBrainItemId Id);
