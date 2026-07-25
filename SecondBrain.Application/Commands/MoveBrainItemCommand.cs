using SecondBrain.Domain.ValueObjects;

namespace SecondBrain.Application.UseCases;

public sealed record MoveBrainItemCommand(
    SecondBrainItemId Id,
    PrimaryPlacement Placement,
    DateTimeOffset UpdatedAt);
