namespace SecondBrain.Persistence;

internal sealed class JournalEntryRow
{
    public Guid JournalId { get; set; }
    public Guid BrainItemId { get; set; }
}
