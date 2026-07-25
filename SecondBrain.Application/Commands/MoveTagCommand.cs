using SecondBrain.Domain.ValueObjects;

namespace SecondBrain.Application.UseCases;

public sealed record MoveTagCommand(TagId Id, TagId? ParentId);
