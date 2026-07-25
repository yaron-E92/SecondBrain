using SecondBrain.Domain.Entities;

namespace SecondBrain.Persistence;

internal sealed class ProjectRow
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string Outcome { get; set; } = "";
    public ProjectStatus Status { get; set; }
    public ProjectPriority Priority { get; set; }
    public DateOnly? TargetDate { get; set; }
    public bool IsArchived { get; set; }
}
