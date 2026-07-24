using System.Collections.ObjectModel;
using SecondBrain.Domain.ValueObjects;

namespace SecondBrain.Domain.Entities;

public sealed class BrainItem
{
    private readonly List<SecondBrainItemId> _contextualLinks;
    private readonly ReadOnlyCollection<SecondBrainItemId> _readOnlyContextualLinks;
    private readonly List<SecondBrainItemId> _derivedItemLinks;
    private readonly ReadOnlyCollection<SecondBrainItemId> _readOnlyDerivedItemLinks;

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
        DateTimeOffset? updatedAt = null,
        CaptureSourceType? captureSourceType = null,
        Uri? sourceUri = null,
        string? sourceCitation = null,
        DateTimeOffset? reminderAt = null,
        CaptureProcessingState? captureProcessingState = null,
        IEnumerable<SecondBrainItemId>? derivedItemLinks = null)
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

        var initialDerivedItemLinks = (derivedItemLinks ?? []).ToArray();
        ValidateKindMetadata(
            kind,
            noteKind,
            ideaMaturity,
            entryDate,
            captureSourceType,
            sourceUri,
            sourceCitation,
            reminderAt,
            captureProcessingState,
            initialDerivedItemLinks,
            createdAt);

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
        CaptureSourceType = captureSourceType;
        SourceUri = sourceUri;
        SourceCitation = sourceCitation?.Trim();
        ReminderAt = reminderAt;
        CaptureProcessingState = captureProcessingState;
        Tags = Array.AsReadOnly(NormalizeTags(tags).ToArray());

        _contextualLinks = [];
        foreach (var link in contextualLinks ?? [])
        {
            AddContextualLinkCore(link);
        }

        _readOnlyContextualLinks = _contextualLinks.AsReadOnly();

        _derivedItemLinks = [];
        foreach (var link in initialDerivedItemLinks)
        {
            AddDerivedItemLinkCore(link);
        }

        _readOnlyDerivedItemLinks = _derivedItemLinks.AsReadOnly();
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

    public CaptureSourceType? CaptureSourceType { get; }

    public Uri? SourceUri { get; }

    public string? SourceCitation { get; }

    public DateTimeOffset? ReminderAt { get; }

    public CaptureProcessingState? CaptureProcessingState { get; private set; }

    public IReadOnlyCollection<string> Tags { get; }

    public IReadOnlyCollection<SecondBrainItemId> ContextualLinks =>
        _readOnlyContextualLinks;

    public IReadOnlyCollection<SecondBrainItemId> DerivedItemLinks =>
        _readOnlyDerivedItemLinks;

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

    public void StartConsuming()
    {
        EnsureActive();
        EnsureCaptureProcessingState(
            global::SecondBrain.Domain.Entities.CaptureProcessingState.Captured);
        CaptureProcessingState =
            global::SecondBrain.Domain.Entities.CaptureProcessingState.Consuming;
    }

    public void MarkDistilled()
    {
        EnsureActive();
        EnsureCaptureProcessingState(
            global::SecondBrain.Domain.Entities.CaptureProcessingState.Consuming);
        CaptureProcessingState =
            global::SecondBrain.Domain.Entities.CaptureProcessingState.Distilled;
    }

    public void MarkReferenced()
    {
        EnsureActive();
        EnsureCaptureProcessingState(
            global::SecondBrain.Domain.Entities.CaptureProcessingState.Consuming);
        CaptureProcessingState =
            global::SecondBrain.Domain.Entities.CaptureProcessingState.Referenced;
    }

    public void AddDerivedItemLink(SecondBrainItemId linkedItemId)
    {
        EnsureActive();
        EnsureKnowledgeCapture();
        AddDerivedItemLinkCore(linkedItemId);
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
        DateOnly? entryDate,
        CaptureSourceType? captureSourceType,
        Uri? sourceUri,
        string? sourceCitation,
        DateTimeOffset? reminderAt,
        CaptureProcessingState? captureProcessingState,
        IReadOnlyCollection<SecondBrainItemId> derivedItemLinks,
        DateTimeOffset createdAt)
    {
        switch (kind)
        {
            case BrainItemKind.Note:
                if (noteKind is null || !Enum.IsDefined(noteKind.Value))
                {
                    throw new ArgumentOutOfRangeException(nameof(noteKind));
                }

                if (ideaMaturity is not null ||
                    entryDate is not null ||
                    HasCaptureMetadata(
                        captureSourceType,
                        sourceUri,
                        sourceCitation,
                        reminderAt,
                        captureProcessingState,
                        derivedItemLinks))
                {
                    throw new ArgumentException(
                        "Notes cannot have idea, journal, or capture metadata.");
                }

                break;

            case BrainItemKind.Idea:
                if (ideaMaturity is null || !Enum.IsDefined(ideaMaturity.Value))
                {
                    throw new ArgumentOutOfRangeException(nameof(ideaMaturity));
                }

                if (noteKind is not null ||
                    entryDate is not null ||
                    HasCaptureMetadata(
                        captureSourceType,
                        sourceUri,
                        sourceCitation,
                        reminderAt,
                        captureProcessingState,
                        derivedItemLinks))
                {
                    throw new ArgumentException(
                        "Ideas cannot have note, journal, or capture metadata.");
                }

                break;

            case BrainItemKind.JournalEntry:
                if (entryDate is null)
                {
                    throw new ArgumentException(
                        "Journal entries require an entry date.",
                        nameof(entryDate));
                }

                if (noteKind is not null ||
                    ideaMaturity is not null ||
                    HasCaptureMetadata(
                        captureSourceType,
                        sourceUri,
                        sourceCitation,
                        reminderAt,
                        captureProcessingState,
                        derivedItemLinks))
                {
                    throw new ArgumentException(
                        "Journal entries cannot have note, idea, or capture metadata.");
                }

                break;

            case BrainItemKind.KnowledgeCapture:
                if (noteKind is not null ||
                    ideaMaturity is not null ||
                    entryDate is not null)
                {
                    throw new ArgumentException(
                        "Knowledge captures cannot have authored item metadata.");
                }

                ValidateCaptureMetadata(
                    captureSourceType,
                    sourceUri,
                    sourceCitation,
                    reminderAt,
                    captureProcessingState,
                    createdAt);
                break;
        }
    }

    private static bool HasCaptureMetadata(
        CaptureSourceType? captureSourceType,
        Uri? sourceUri,
        string? sourceCitation,
        DateTimeOffset? reminderAt,
        CaptureProcessingState? captureProcessingState,
        IReadOnlyCollection<SecondBrainItemId> derivedItemLinks) =>
        captureSourceType is not null ||
        sourceUri is not null ||
        sourceCitation is not null ||
        reminderAt is not null ||
        captureProcessingState is not null ||
        derivedItemLinks.Count > 0;

    private static void ValidateCaptureMetadata(
        CaptureSourceType? captureSourceType,
        Uri? sourceUri,
        string? sourceCitation,
        DateTimeOffset? reminderAt,
        CaptureProcessingState? captureProcessingState,
        DateTimeOffset createdAt)
    {
        if (captureSourceType is null ||
            !Enum.IsDefined(captureSourceType.Value))
        {
            throw new ArgumentOutOfRangeException(nameof(captureSourceType));
        }

        if (sourceUri is null || !sourceUri.IsAbsoluteUri)
        {
            throw new ArgumentException(
                "Capture source URL must be absolute.",
                nameof(sourceUri));
        }

        var hasExpectedScheme =
            captureSourceType ==
            global::SecondBrain.Domain.Entities.CaptureSourceType.File
            ? sourceUri.IsFile
            : sourceUri.Scheme == Uri.UriSchemeHttp ||
                sourceUri.Scheme == Uri.UriSchemeHttps;

        if (!hasExpectedScheme)
        {
            throw new ArgumentException(
                "Capture source URL has an invalid scheme for its source type.",
                nameof(sourceUri));
        }

        if (string.IsNullOrWhiteSpace(sourceCitation))
        {
            throw new ArgumentException(
                "Capture source citation cannot be empty.",
                nameof(sourceCitation));
        }

        if (reminderAt < createdAt)
        {
            throw new ArgumentOutOfRangeException(
                nameof(reminderAt),
                "Capture reminder cannot precede its creation time.");
        }

        if (captureProcessingState is null ||
            !Enum.IsDefined(captureProcessingState.Value))
        {
            throw new ArgumentOutOfRangeException(nameof(captureProcessingState));
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

    private void AddDerivedItemLinkCore(SecondBrainItemId linkedItemId)
    {
        if (linkedItemId.Value == Guid.Empty)
        {
            throw new ArgumentException(
                "Derived item link ID cannot be empty.",
                nameof(linkedItemId));
        }

        if (linkedItemId == Id)
        {
            throw new ArgumentException(
                "A Knowledge Capture cannot derive itself.",
                nameof(linkedItemId));
        }

        if (_derivedItemLinks.Contains(linkedItemId))
        {
            throw new InvalidOperationException("Derived item link already exists.");
        }

        _derivedItemLinks.Add(linkedItemId);
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

    private void EnsureCaptureProcessingState(CaptureProcessingState required)
    {
        EnsureKnowledgeCapture();

        if (CaptureProcessingState != required)
        {
            throw new InvalidOperationException(
                $"Knowledge Capture must be {required}, but is {CaptureProcessingState}.");
        }
    }

    private void EnsureKnowledgeCapture()
    {
        if (Kind != BrainItemKind.KnowledgeCapture)
        {
            throw new InvalidOperationException(
                "Only Knowledge Captures have a processing lifecycle.");
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
