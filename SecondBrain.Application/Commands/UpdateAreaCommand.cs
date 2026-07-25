using SecondBrain.Domain.ValueObjects;

namespace SecondBrain.Application.UseCases;

public sealed record UpdateAreaCommand(AreaId Id, ParaContextName Name);
