using SecondBrain.Domain.ValueObjects;

namespace SecondBrain.Application.UseCases;

public sealed record UpdateResourceTopicCommand(
    ResourceTopicId Id,
    ParaContextName Name);
