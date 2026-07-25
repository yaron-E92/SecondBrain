using SecondBrain.Domain.Entities;
using SecondBrain.Domain.ValueObjects;

namespace SecondBrain.Application.UseCases;

public sealed record UpdateProjectCommand(
    ProjectId Id,
    ParaContextName Name,
    string Outcome,
    ProjectPriority Priority,
    DateOnly? TargetDate);
