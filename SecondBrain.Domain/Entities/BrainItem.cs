using System.Collections.ObjectModel;
using SecondBrain.Domain.ValueObjects;

namespace SecondBrain.Domain.Entities;

public sealed class BrainItem
{
    private readonly List<SecondBrainItemId> _contextualLinks;
    private readonly ReadOnlyCollection<SecondBrainItemId> _readOnlyContextualLinks;

    public BrainItem(
        SecondBrainItemId id,
        BrainItemKind kind,
        string title,
        string content,
        PrimaryPlacement primaryPlacement,
        DateTimeOffset createdAt,
        NoteKind? noteKind = null,
        IdeaMaturity? ideaMaturity = null,
        DateOnly? entryDate = null,
        IEnumerable<string>? tags = null,
        IEnumerable<SecondBrainItemId>? contextualLinks = null,
        DateTimeOffset? updatedAt = null)
    {
        if (id.Value == Guid.Empty)
        {
            throw new ArgumentException("Brain item ID cannot be empty.", nameof(id));
        }

        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind));
        }

        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException("Brain item title cannot be empty.", nameof(title));
        }

        if (string.IsNullOrWhiteSpace(content))
        {
            throw new ArgumentException("Brain item content cannot be empty.", nameof(content));
        }

        ArgumentNullException.ThrowIfNull(primaryPlacement);

        var effectiveUpdatedAt = updatedAt ?? createdAt;
        if (effectiveUpdatedAt < createdAt)
        {
            throw new ArgumentOutOfRangeException(
                nameof(updatedAt),
                "Updated time cannot precede created time.");
        }

        ValidateKindMetadata(kind, noteKind, ideaMaturity, entryDate);

        Id = id;
        Kind = kind;
        Title = title.Trim();
        Content = content.Trim();
        PrimaryPlacement = primaryPlacement;
        CreatedAt = createdAt;
        UpdatedAt = effectiveUpdatedAt;
        NoteKind = noteKind;
        IdeaMaturity = ideaMaturity;
        EntryDate = entryDate;
        Tags = Array.AsReadOnly(NormalizeTags(tags).ToArray());

        _contextualLinks = [];
        foreach (var link in contextualLinks ?? [])
        {
            AddContextualLinkCore(link);
        }

        _readOnlyContextualLinks = _contextualLinks.AsReadOnly();
    }

    public SecondBrainItemId Id { get; }

    public BrainItemKind Kind { get; }

    public string Title { get; }

    public string Content { get; }

    public PrimaryPlacement PrimaryPlacement { get; }

    public DateTimeOffset CreatedAt { get; }

    public DateTimeOffset UpdatedAt { get; }

    public NoteKind? NoteKind { get; }

    public IdeaMaturity? IdeaMaturity { get; private set; }

    public DateOnly? EntryDate { get; }

    public IReadOnlyCollection<string> Tags { get; }

    public IReadOnlyCollection<SecondBrainItemId> ContextualLinks =>
        _readOnlyContextualLinks;

    public bool IsArchived { get; private set; }

    public void Sharpen()
    {
        EnsureActive();
        EnsureIdeaMaturity(global::SecondBrain.Domain.Entities.IdeaMaturity.Captured);
        IdeaMaturity = global::SecondBrain.Domain.Entities.IdeaMaturity.Sharpened;
    }

    public void MakeActionable()
    {
        EnsureActive();
        EnsureIdeaMaturity(global::SecondBrain.Domain.Entities.IdeaMaturity.Sharpened);
        IdeaMaturity = global::SecondBrain.Domain.Entities.IdeaMaturity.Actionable;
    }

    public void AddContextualLink(SecondBrainItemId linkedItemId)
    {
        EnsureActive();
        AddContextualLinkCore(linkedItemId);
    }

    public void Archive()
    {
        if (IsArchived)
        {
            throw new InvalidOperationException("Brain item is already archived.");
        }

        IsArchived = true;
    }

    public void Restore()
    {
        if (!IsArchived)
        {
            throw new InvalidOperationException("Brain item is not archived.");
        }

        IsArchived = false;
    }

    private static void ValidateKindMetadata(
        BrainItemKind kind,
        NoteKind? noteKind,
        IdeaMaturity? ideaMaturity,
        DateOnly? entryDate)
    {
        switch (kind)
        {
            case BrainItemKind.Note:
                if (noteKind is null || !Enum.IsDefined(noteKind.Value))
                {
                    throw new ArgumentOutOfRangeException(nameof(noteKind));
                }

                if (ideaMaturity is not null || entryDate is not null)
                {
                    throw new ArgumentException(
                        "Notes cannot have idea maturity or an entry date.");
                }

                break;

            case BrainItemKind.Idea:
                if (ideaMaturity is null || !Enum.IsDefined(ideaMaturity.Value))
                {
                    throw new ArgumentOutOfRangeException(nameof(ideaMaturity));
                }

                if (noteKind is not null || entryDate is not null)
                {
                    throw new ArgumentException(
                        "Ideas cannot have a note kind or an entry date.");
                }

                break;

            case BrainItemKind.JournalEntry:
                if (entryDate is null)
                {
                    throw new ArgumentException(
                        "Journal entries require an entry date.",
                        nameof(entryDate));
                }

                if (noteKind is not null || ideaMaturity is not null)
                {
                    throw new ArgumentException(
                        "Journal entries cannot have a note kind or idea maturity.");
                }

                break;
        }
    }

    private static IEnumerable<string> NormalizeTags(IEnumerable<string>? tags)
    {
        var normalizedTags = new List<string>();

        foreach (var tag in tags ?? [])
        {
            if (string.IsNullOrWhiteSpace(tag))
            {
                throw new ArgumentException("Tags cannot be empty.", nameof(tags));
            }

            var normalizedTag = tag.Trim();
            if (!normalizedTags.Contains(normalizedTag, StringComparer.OrdinalIgnoreCase))
            {
                normalizedTags.Add(normalizedTag);
            }
        }

        return normalizedTags;
    }

    private void AddContextualLinkCore(SecondBrainItemId linkedItemId)
    {
        if (linkedItemId.Value == Guid.Empty)
        {
            throw new ArgumentException(
                "Contextual link ID cannot be empty.",
                nameof(linkedItemId));
        }

        if (linkedItemId == Id)
        {
            throw new ArgumentException(
                "A Brain item cannot link to itself.",
                nameof(linkedItemId));
        }

        if (_contextualLinks.Contains(linkedItemId))
        {
            throw new InvalidOperationException("Contextual link already exists.");
        }

        _contextualLinks.Add(linkedItemId);
    }

    private void EnsureIdeaMaturity(IdeaMaturity required)
    {
        if (Kind != BrainItemKind.Idea)
        {
            throw new InvalidOperationException("Only ideas have a maturity lifecycle.");
        }

        if (IdeaMaturity != required)
        {
            throw new InvalidOperationException(
                $"Idea must be {required}, but is {IdeaMaturity}.");
        }
    }

    private void EnsureActive()
    {
        if (IsArchived)
        {
            throw new InvalidOperationException("Archived Brain items cannot be changed.");
        }
    }
}
