using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SecondBrain.Application.UseCases;
using SecondBrain.Domain.Entities;
using SecondBrain.Domain.ValueObjects;

namespace SecondBrain.Presentation.ViewModels;

public sealed partial class CoreEditorViewModel : ObservableObject
{
    private readonly CoreKnowledgeUseCases _useCases;
    private readonly Func<DateTimeOffset> _utcNow;
    private CoreEditorSnapshot? _original;
    private PrimaryPlacement? _primaryPlacement;
    private DateTimeOffset _createdAt;
    private bool _suppressDirtyTracking;
    private bool _needsJournalAttachment;

    [ObservableProperty]
    public partial string Title { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Content { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsDirty { get; set; }

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    public partial string? ErrorMessage { get; set; }

    [ObservableProperty]
    public partial BrainItem? LastSavedItem { get; set; }

    public CoreEditorViewModel(
        CoreKnowledgeUseCases useCases,
        Func<DateTimeOffset>? utcNow = null)
    {
        _useCases = useCases;
        _utcNow = utcNow ?? (() => DateTimeOffset.UtcNow);

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

        _suppressDirtyTracking = true;
        try
        {
            ItemId = null;
            Kind = kind;
            IsNew = true;
            _primaryPlacement = placement;
            _createdAt = _utcNow();
            _needsJournalAttachment = kind == BrainItemKind.JournalEntry;
            Title = string.Empty;
            Content = string.Empty;
            ErrorMessage = null;
            LastSavedItem = null;
            ResetSections();
            NotifyEditorShapeChanged();
            _original = CaptureSnapshot();
            IsDirty = false;
        }
        finally
        {
            _suppressDirtyTracking = false;
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
            var result = await _useCases.GetBrainItemAsync(
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
                saveResult = await _useCases.CreateBrainItemAsync(
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
                saveResult = await _useCases.UpdateBrainItemAsync(
                    new UpdateBrainItemCommand(
                        ItemId!.Value,
                        Title,
                        Content,
                        _utcNow()),
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
            if (_needsJournalAttachment)
            {
                var journalResult = await _useCases.AddJournalEntryAsync(
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

                _needsJournalAttachment = false;
            }

            LastSavedItem = savedItem;
            _original = CaptureSnapshot();
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
        if (_original is null)
        {
            return;
        }

        ApplySnapshot(_original);
        ErrorMessage = null;
        IsDirty = false;
    }

    private void LoadItem(BrainItem item, SecondBrainItemId? journalId)
    {
        _suppressDirtyTracking = true;
        try
        {
            ItemId = item.Id;
            Kind = item.Kind;
            IsNew = false;
            _primaryPlacement = item.PrimaryPlacement;
            _createdAt = item.CreatedAt;
            _needsJournalAttachment = false;
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
            _original = CaptureSnapshot();
            IsDirty = false;
        }
        finally
        {
            _suppressDirtyTracking = false;
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
            _primaryPlacement!,
            _createdAt,
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
            current = await _useCases.TransitionBrainItemAsync(
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

            if (target == CaptureProcessingState.Distilled && target != current)
            {
                yield return BrainItemLifecycleTransition.MarkCaptureDistilled;
            }
            else if (target == CaptureProcessingState.Referenced && target != current)
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

        if (_primaryPlacement is null)
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

    private CoreEditorSnapshot CaptureSnapshot() =>
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

    private void ApplySnapshot(CoreEditorSnapshot snapshot)
    {
        _suppressDirtyTracking = true;
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
            _suppressDirtyTracking = false;
        }
    }

    private void TrackShellChange(object? sender, PropertyChangedEventArgs args)
    {
        if (!_suppressDirtyTracking &&
            args.PropertyName is nameof(Title) or nameof(Content))
        {
            IsDirty = true;
        }
    }

    private void TrackSectionChange(object? sender, PropertyChangedEventArgs args)
    {
        if (!_suppressDirtyTracking)
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

}
