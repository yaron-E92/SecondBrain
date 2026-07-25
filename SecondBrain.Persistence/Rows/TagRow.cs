namespace SecondBrain.Persistence;

internal sealed class TagRow
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public Guid? ParentId { get; set; }
}
