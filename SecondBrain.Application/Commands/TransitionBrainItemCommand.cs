using SecondBrain.Domain.ValueObjects;

namespace SecondBrain.Application.UseCases;

public sealed record TransitionBrainItemCommand(
    SecondBrainItemId Id,
    BrainItemLifecycleTransition Transition);
