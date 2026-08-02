using SecondBrain.Domain.ValueObjects;

namespace SecondBrain.Application.UseCases;

public sealed record GetBrainItemQuery(SecondBrainItemId Id);
