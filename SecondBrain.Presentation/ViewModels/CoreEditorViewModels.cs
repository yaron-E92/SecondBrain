using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SecondBrain.Application.UseCases;
using SecondBrain.Domain.Entities;
using SecondBrain.Domain.ValueObjects;

namespace SecondBrain.Presentation.ViewModels;

public sealed partial class CoreEditorViewModel : ObservableObject
{
    private readonly CoreKnowledgeUseCases useCases;
    private readonly Func<DateTimeOffset> utcNow;
    private EditorSnapshot? original;
    private PrimaryPlacement? primaryPlacement;
    private DateTimeOffset createdAt;
    private bool suppressDirtyTracking;
    private bool needsJournalAttachment;

    [ObservableProperty]
    private string title = string.Empty;

    [ObservableProperty]
    private string content = string.Empty;

    [ObservableProperty]
    private bool isDirty;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string? errorMessage;

    [ObservableProperty]
    private BrainItem? lastSavedItem;

    public CoreEditorViewModel(
        CoreKnowledgeUseCases useCases,
        Func<DateTimeOffset>? utcNow = null)
    {
        this.useCases = useCases;
        this.utcNow = utcNow ?? (() => DateTimeOffset.UtcNow);

        Note = new NoteEditorSection();
        Idea = new IdeaEditorSection();
        Capture = new CaptureEditorSection();
        Resource = new ResourceEditorSection();
        JournalEntry = new JournalEntryEditorSection();

        PropertyChanged += TrackShellChange;
        Note.PropertyChanged += TrackSectionChange;
        Idea.PropertyChanged += TrackSectionChange;
        Capture.PropertyChanged += TrackSectionChange;
        Resource.PropertyChanged += TrackSectionChange;
        JournalEntry.PropertyChanged += TrackSectionChange;
    }

    public NoteEditorSection Note { get; }

    public IdeaEditorSection Idea { get; }

    public CaptureEditorSection Capture { get; }

    public ResourceEditorSection Resource { get; }

    public JournalEntryEditorSection JournalEntry { get; }

    public SecondBrainItemId? ItemId { get; private set; }

    public BrainItemKind Kind { get; private set; }

    public bool IsNew { get; private set; }

    public bool AreTypeFieldsEditable => IsNew;

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public bool IsNote => Kind == BrainItemKind.Note;

    public bool IsIdea => Kind == BrainItemKind.Idea;

    public bool IsCapture => Kind == BrainItemKind.KnowledgeCapture;

    public bool IsResource => Kind == BrainItemKind.ResourceArtifact;

    public bool IsJournalEntry => Kind == BrainItemKind.JournalEntry;

    public void BeginCreate(
        BrainItemKind kind,
        PrimaryPlacement placement)
    {
        ArgumentNullException.ThrowIfNull(placement);
        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind));
        }

        suppressDirtyTracking = true;
        try
        {
            ItemId = null;
            Kind = kind;
            IsNew = true;
            primaryPlacement = placement;
            createdAt = utcNow();
            needsJournalAttachment = kind == BrainItemKind.JournalEntry;
            Title = string.Empty;
            Content = string.Empty;
            ErrorMessage = null;
            LastSavedItem = null;
            ResetSections();
            NotifyEditorShapeChanged();
            original = CaptureSnapshot();
            IsDirty = false;
        }
        finally
        {
            suppressDirtyTracking = false;
        }
    }

    public async Task LoadAsync(
        SecondBrainItemId itemId,
        SecondBrainItemId? journalId = null,
        CancellationToken cancellationToken = default)
    {
        IsBusy = true;
        ErrorMessage = null;

        try
        {
            var result = await useCases.GetBrainItemAsync(
                new GetBrainItemQuery(itemId),
                cancellationToken);
            if (!result.IsSuccess)
            {
                ErrorMessage = result.Error!.Message;
                return;
            }

            LoadItem(result.Value!, journalId);
        }
        catch (Exception exception)
        {
            ErrorMessage = $"Editor could not be loaded. {exception.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        ErrorMessage = Validate();
        if (ErrorMessage is not null)
        {
            return;
        }

        IsBusy = true;
        try
        {
            CoreOperationResult<BrainItem> saveResult;
            if (IsNew)
            {
                var item = CreateItem();
                saveResult = await useCases.CreateBrainItemAsync(
                    new CreateBrainItemCommand(item),
                    cancellationToken);
                if (!saveResult.IsSuccess)
                {
                    ErrorMessage = saveResult.Error!.Message;
                    return;
                }

                ItemId = item.Id;
                IsNew = false;
                NotifyEditorShapeChanged();
            }
            else
            {
                saveResult = await useCases.UpdateBrainItemAsync(
                    new UpdateBrainItemCommand(
                        ItemId!.Value,
                        Title,
                        Content,
                        utcNow()),
                    cancellationToken);
                if (!saveResult.IsSuccess)
                {
                    ErrorMessage = saveResult.Error!.Message;
                    return;
                }
            }

            var savedItem = saveResult.Value!;
            var lifecycleResult = await ApplyLifecycleAsync(
                savedItem,
                cancellationToken);
            if (!lifecycleResult.IsSuccess)
            {
                LastSavedItem = lifecycleResult.Value ?? savedItem;
                ErrorMessage = lifecycleResult.Error!.Message;
                return;
            }

            savedItem = lifecycleResult.Value!;
            if (needsJournalAttachment)
            {
                var journalResult = await useCases.AddJournalEntryAsync(
                    new AddJournalEntryCommand(
                        JournalEntry.JournalId!.Value,
                        savedItem.Id),
                    cancellationToken);
                if (!journalResult.IsSuccess)
                {
                    LastSavedItem = savedItem;
                    ErrorMessage = journalResult.Error!.Message;
                    return;
                }

                needsJournalAttachment = false;
            }

            LastSavedItem = savedItem;
            original = CaptureSnapshot();
            IsDirty = false;
        }
        catch (ArgumentException exception)
        {
            ErrorMessage = exception.Message;
        }
        catch (InvalidOperationException exception)
        {
            ErrorMessage = exception.Message;
        }
        catch (Exception exception)
        {
            ErrorMessage = $"Changes could not be saved. {exception.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void Cancel()
    {
        if (original is null)
        {
            return;
        }

        ApplySnapshot(original);
        ErrorMessage = null;
        IsDirty = false;
    }

    private void LoadItem(BrainItem item, SecondBrainItemId? journalId)
    {
        suppressDirtyTracking = true;
        try
        {
            ItemId = item.Id;
            Kind = item.Kind;
            IsNew = false;
            primaryPlacement = item.PrimaryPlacement;
            createdAt = item.CreatedAt;
            needsJournalAttachment = false;
            Title = item.Title;
            Content = item.Content;
            ErrorMessage = null;
            LastSavedItem = item;
            ResetSections();

            Note.Kind = item.NoteKind ?? NoteKind.General;
            Idea.Maturity = item.IdeaMaturity ?? IdeaMaturity.Captured;
            Capture.SourceType = item.CaptureSourceType ?? CaptureSourceType.Article;
            Capture.SourceUrl = item.SourceUri?.AbsoluteUri ?? string.Empty;
            Capture.SourceCitation = item.SourceCitation ?? string.Empty;
            Capture.ReminderAt = item.ReminderAt;
            Capture.ProcessingState =
                item.CaptureProcessingState ?? CaptureProcessingState.Captured;
            Resource.ArtifactKind =
                item.ResourceArtifactKind ?? ResourceArtifactKind.Guide;
            Resource.Freshness =
                item.ResourceFreshness ?? ResourceFreshness.Draft;
            Resource.ReviewDate = item.ReviewDate;
            JournalEntry.JournalId = journalId;
            JournalEntry.OccurrenceDate = item.EntryDate;

            NotifyEditorShapeChanged();
            original = CaptureSnapshot();
            IsDirty = false;
        }
        finally
        {
            suppressDirtyTracking = false;
        }
    }

    private BrainItem CreateItem()
    {
        Uri? sourceUri = null;
        if (Kind == BrainItemKind.KnowledgeCapture)
        {
            sourceUri = new Uri(Capture.SourceUrl, UriKind.Absolute);
        }

        return new BrainItem(
            SecondBrainItemId.New(),
            Kind,
            Title,
            Content,
            primaryPlacement!,
            createdAt,
            noteKind: Kind == BrainItemKind.Note ? Note.Kind : null,
            ideaMaturity: Kind == BrainItemKind.Idea ? Idea.Maturity : null,
            entryDate: Kind == BrainItemKind.JournalEntry
                ? JournalEntry.OccurrenceDate
                : null,
            captureSourceType: Kind == BrainItemKind.KnowledgeCapture
                ? Capture.SourceType
                : null,
            sourceUri: sourceUri,
            sourceCitation: Kind == BrainItemKind.KnowledgeCapture
                ? Capture.SourceCitation
                : null,
            reminderAt: Kind == BrainItemKind.KnowledgeCapture
                ? Capture.ReminderAt
                : null,
            captureProcessingState: Kind == BrainItemKind.KnowledgeCapture
                ? Capture.ProcessingState
                : null,
            resourceArtifactKind: Kind == BrainItemKind.ResourceArtifact
                ? Resource.ArtifactKind
                : null,
            resourceFreshness: Kind == BrainItemKind.ResourceArtifact
                ? Resource.Freshness
                : null,
            reviewDate: Kind == BrainItemKind.ResourceArtifact
                ? Resource.ReviewDate
                : null);
    }

    private async Task<CoreOperationResult<BrainItem>> ApplyLifecycleAsync(
        BrainItem item,
        CancellationToken cancellationToken)
    {
        var transitions = RequiredTransitions(item).ToArray();
        var current = CoreOperationResult<BrainItem>.Success(item);
        foreach (var transition in transitions)
        {
            current = await useCases.TransitionBrainItemAsync(
                new TransitionBrainItemCommand(item.Id, transition),
                cancellationToken);
            if (!current.IsSuccess)
            {
                return current;
            }

            item = current.Value!;
        }

        return current;
    }

    private IEnumerable<BrainItemLifecycleTransition> RequiredTransitions(
        BrainItem item)
    {
        if (item.Kind == BrainItemKind.Idea)
        {
            var current = item.IdeaMaturity!.Value;
            var target = Idea.Maturity;
            if (target < current)
            {
                throw new InvalidOperationException(
                    "Idea maturity cannot move backward.");
            }

            if (current == IdeaMaturity.Captured && target >= IdeaMaturity.Sharpened)
            {
                yield return BrainItemLifecycleTransition.SharpenIdea;
            }

            if (target == IdeaMaturity.Actionable && current < IdeaMaturity.Actionable)
            {
                yield return BrainItemLifecycleTransition.MakeIdeaActionable;
            }
        }
        else if (item.Kind == BrainItemKind.KnowledgeCapture)
        {
            var current = item.CaptureProcessingState!.Value;
            var target = Capture.ProcessingState;
            if (target < current ||
                ((current == CaptureProcessingState.Distilled ||
                    current == CaptureProcessingState.Referenced) &&
                    target != current))
            {
                throw new InvalidOperationException(
                    "Capture processing state cannot use that transition.");
            }

            if (current == CaptureProcessingState.Captured &&
                target >= CaptureProcessingState.Consuming)
            {
                yield return BrainItemLifecycleTransition.StartConsumingCapture;
            }

            if (target == CaptureProcessingState.Distilled)
            {
                yield return BrainItemLifecycleTransition.MarkCaptureDistilled;
            }
            else if (target == CaptureProcessingState.Referenced)
            {
                yield return BrainItemLifecycleTransition.MarkCaptureReferenced;
            }
        }
        else if (item.Kind == BrainItemKind.ResourceArtifact)
        {
            var current = item.ResourceFreshness!.Value;
            var target = Resource.Freshness;
            if (target < current)
            {
                throw new InvalidOperationException(
                    "Resource freshness cannot move backward.");
            }

            if (current == ResourceFreshness.Draft && target >= ResourceFreshness.Current)
            {
                yield return BrainItemLifecycleTransition.MarkResourceCurrent;
            }

            if (target == ResourceFreshness.Outdated && current < ResourceFreshness.Outdated)
            {
                yield return BrainItemLifecycleTransition.MarkResourceOutdated;
            }
        }
    }

    private string? Validate()
    {
        if (string.IsNullOrWhiteSpace(Title))
        {
            return "Title is required.";
        }

        if (string.IsNullOrWhiteSpace(Content))
        {
            return "Content is required.";
        }

        if (primaryPlacement is null)
        {
            return "Primary placement is required.";
        }

        if (Kind == BrainItemKind.JournalEntry)
        {
            if (JournalEntry.JournalId is null ||
                JournalEntry.JournalId.Value.Value == Guid.Empty)
            {
                return "Journal is required.";
            }

            if (JournalEntry.OccurrenceDate is null)
            {
                return "Occurrence date is required.";
            }
        }

        if (Kind == BrainItemKind.KnowledgeCapture)
        {
            if (!Uri.TryCreate(Capture.SourceUrl, UriKind.Absolute, out _))
            {
                return "Capture source URL must be absolute.";
            }

            if (string.IsNullOrWhiteSpace(Capture.SourceCitation))
            {
                return "Capture source citation is required.";
            }
        }

        return null;
    }

    private void ResetSections()
    {
        Note.Kind = NoteKind.General;
        Idea.Maturity = IdeaMaturity.Captured;
        Capture.SourceType = CaptureSourceType.Article;
        Capture.SourceUrl = string.Empty;
        Capture.SourceCitation = string.Empty;
        Capture.ReminderAt = null;
        Capture.ProcessingState = CaptureProcessingState.Captured;
        Resource.ArtifactKind = ResourceArtifactKind.Guide;
        Resource.Freshness = ResourceFreshness.Draft;
        Resource.ReviewDate = null;
        JournalEntry.JournalId = null;
        JournalEntry.OccurrenceDate = null;
    }

    private EditorSnapshot CaptureSnapshot() =>
        new(
            Title,
            Content,
            Note.Kind,
            Idea.Maturity,
            Capture.SourceType,
            Capture.SourceUrl,
            Capture.SourceCitation,
            Capture.ReminderAt,
            Capture.ProcessingState,
            Resource.ArtifactKind,
            Resource.Freshness,
            Resource.ReviewDate,
            JournalEntry.JournalId,
            JournalEntry.OccurrenceDate);

    private void ApplySnapshot(EditorSnapshot snapshot)
    {
        suppressDirtyTracking = true;
        try
        {
            Title = snapshot.Title;
            Content = snapshot.Content;
            Note.Kind = snapshot.NoteKind;
            Idea.Maturity = snapshot.IdeaMaturity;
            Capture.SourceType = snapshot.CaptureSourceType;
            Capture.SourceUrl = snapshot.SourceUrl;
            Capture.SourceCitation = snapshot.SourceCitation;
            Capture.ReminderAt = snapshot.ReminderAt;
            Capture.ProcessingState = snapshot.CaptureProcessingState;
            Resource.ArtifactKind = snapshot.ResourceArtifactKind;
            Resource.Freshness = snapshot.ResourceFreshness;
            Resource.ReviewDate = snapshot.ReviewDate;
            JournalEntry.JournalId = snapshot.JournalId;
            JournalEntry.OccurrenceDate = snapshot.OccurrenceDate;
        }
        finally
        {
            suppressDirtyTracking = false;
        }
    }

    private void TrackShellChange(object? sender, PropertyChangedEventArgs args)
    {
        if (!suppressDirtyTracking &&
            args.PropertyName is nameof(Title) or nameof(Content))
        {
            IsDirty = true;
        }
    }

    private void TrackSectionChange(object? sender, PropertyChangedEventArgs args)
    {
        if (!suppressDirtyTracking)
        {
            IsDirty = true;
        }
    }

    private void NotifyEditorShapeChanged()
    {
        OnPropertyChanged(nameof(ItemId));
        OnPropertyChanged(nameof(Kind));
        OnPropertyChanged(nameof(IsNew));
        OnPropertyChanged(nameof(AreTypeFieldsEditable));
        OnPropertyChanged(nameof(IsNote));
        OnPropertyChanged(nameof(IsIdea));
        OnPropertyChanged(nameof(IsCapture));
        OnPropertyChanged(nameof(IsResource));
        OnPropertyChanged(nameof(IsJournalEntry));
    }

    private sealed record EditorSnapshot(
        string Title,
        string Content,
        NoteKind NoteKind,
        IdeaMaturity IdeaMaturity,
        CaptureSourceType CaptureSourceType,
        string SourceUrl,
        string SourceCitation,
        DateTimeOffset? ReminderAt,
        CaptureProcessingState CaptureProcessingState,
        ResourceArtifactKind ResourceArtifactKind,
        ResourceFreshness ResourceFreshness,
        DateOnly? ReviewDate,
        SecondBrainItemId? JournalId,
        DateOnly? OccurrenceDate);
}

public sealed partial class NoteEditorSection : ObservableObject
{
    [ObservableProperty]
    private NoteKind kind = NoteKind.General;
}

public sealed partial class IdeaEditorSection : ObservableObject
{
    [ObservableProperty]
    private IdeaMaturity maturity = IdeaMaturity.Captured;
}

public sealed partial class CaptureEditorSection : ObservableObject
{
    [ObservableProperty]
    private CaptureSourceType sourceType = CaptureSourceType.Article;

    [ObservableProperty]
    private string sourceUrl = string.Empty;

    [ObservableProperty]
    private string sourceCitation = string.Empty;

    [ObservableProperty]
    private DateTimeOffset? reminderAt;

    [ObservableProperty]
    private CaptureProcessingState processingState =
        CaptureProcessingState.Captured;
}

public sealed partial class ResourceEditorSection : ObservableObject
{
    [ObservableProperty]
    private ResourceArtifactKind artifactKind = ResourceArtifactKind.Guide;

    [ObservableProperty]
    private ResourceFreshness freshness = ResourceFreshness.Draft;

    [ObservableProperty]
    private DateOnly? reviewDate;
}

public sealed partial class JournalEntryEditorSection : ObservableObject
{
    [ObservableProperty]
    private SecondBrainItemId? journalId;

    [ObservableProperty]
    private DateOnly? occurrenceDate;
}
