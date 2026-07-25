using SecondBrain.Domain.ValueObjects;

namespace SecondBrain.Application.UseCases;

public sealed record RestoreBrainItemCommand(SecondBrainItemId Id);
