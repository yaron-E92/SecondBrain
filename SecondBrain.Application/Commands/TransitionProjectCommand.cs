using SecondBrain.Domain.ValueObjects;

namespace SecondBrain.Application.UseCases;

public sealed record TransitionProjectCommand(
    ProjectId Id,
    ProjectLifecycleTransition Transition);
