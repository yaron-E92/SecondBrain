using SecondBrain.Domain.ValueObjects;

namespace SecondBrain.Application.UseCases;

public sealed record UpdateBrainItemCommand(
    SecondBrainItemId Id,
    string Title,
    string Content,
    DateTimeOffset UpdatedAt);
