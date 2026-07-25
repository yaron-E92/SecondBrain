using SecondBrain.Domain.ValueObjects;

namespace SecondBrain.Domain.Entities;

public sealed class Journal
{
    private readonly List<BrainItem> _entries = [];

    public Journal(SecondBrainItemId id, string title)
    {
        if (id.Value == Guid.Empty)
        {
            throw new ArgumentException("Journal ID cannot be empty.", nameof(id));
        }

        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException("Journal title cannot be empty.", nameof(title));
        }

        Id = id;
        Title = title.Trim();
    }

    public SecondBrainItemId Id { get; }

    public string Title { get; private set; }

    public IReadOnlyList<BrainItem> Entries =>
        _entries
            .OrderBy(entry => entry.EntryDate)
            .ThenBy(entry => entry.Id.Value)
            .ToArray();

    public void Rename(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException("Journal title cannot be empty.", nameof(title));
        }

        Title = title.Trim();
    }

    public void AddEntry(BrainItem entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        if (entry.Kind != BrainItemKind.JournalEntry)
        {
            throw new ArgumentException(
                "Journals can contain only Journal Entry items.",
                nameof(entry));
        }

        if (_entries.Any(existing => existing.Id == entry.Id))
        {
            throw new InvalidOperationException("Journal entry already belongs to this journal.");
        }

        _entries.Add(entry);
    }
}
