namespace SecondBrain.Persistence;

internal sealed class AreaRow
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public bool IsArchived { get; set; }
}
