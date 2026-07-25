using SecondBrain.Domain.ValueObjects;

namespace SecondBrain.Application.UseCases;

public sealed record ArchiveBrainItemCommand(SecondBrainItemId Id);
