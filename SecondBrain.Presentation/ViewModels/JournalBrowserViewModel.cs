using CommunityToolkit.Mvvm.ComponentModel;
using SecondBrain.Application.Ports;
using SecondBrain.Application.UseCases;
using SecondBrain.Domain.Entities;
using SecondBrain.Domain.ValueObjects;

namespace SecondBrain.Presentation.ViewModels;

public sealed record JournalTimelineEntry(
    SecondBrainItemId Id,
    DateOnly Date,
    string Title,
    string Content,
    string ParaContext,
    bool IsArchived);

public sealed partial class JournalBrowserViewModel : ObservableObject
{
    private readonly ICoreKnowledgeRepository _repository;
    private readonly CoreKnowledgeUseCases _useCases;
    private CoreKnowledgeState? _state;

    public JournalBrowserViewModel(
        ICoreKnowledgeRepository repository,
        CoreKnowledgeUseCases useCases)
    {
        _repository = repository;
        _useCases = useCases;
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    public partial IReadOnlyList<Journal> Journals { get; set; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedJournal))]
    [NotifyPropertyChangedFor(nameof(IsSelectedArchived))]
    [NotifyPropertyChangedFor(nameof(CanAddEntry))]
    [NotifyPropertyChangedFor(nameof(CanArchive))]
    [NotifyPropertyChangedFor(nameof(CanRestore))]
    [NotifyPropertyChangedFor(nameof(Timeline))]
    [NotifyPropertyChangedFor(nameof(IsTimelineEmpty))]
    public partial Journal? SelectedJournal { get; set; }

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    public partial string? ErrorMessage { get; set; }

    [ObservableProperty]
    public partial bool IsJournalEditorVisible { get; set; }

    [ObservableProperty]
    public partial bool IsCreatingJournal { get; set; }

    [ObservableProperty]
    public partial string JournalTitle { get; set; } = string.Empty;

    public bool IsEmpty => Journals.Count == 0;

    public bool HasSelectedJournal => SelectedJournal is not null;

    public bool IsSelectedArchived => SelectedJournal?.IsArchived == true;

    public bool CanAddEntry => SelectedJournal is { IsArchived: false };

    public bool CanArchive => SelectedJournal is { IsArchived: false };

    public bool CanRestore => SelectedJournal is { IsArchived: true };

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public IReadOnlyList<JournalTimelineEntry> Timeline =>
        SelectedJournal?.Entries
            .Select(entry => new JournalTimelineEntry(
                entry.Id,
                entry.EntryDate!.Value,
                entry.Title,
                entry.Content,
                DescribePlacement(entry.PrimaryPlacement),
                entry.IsArchived))
            .ToArray() ?? [];

    public bool IsTimelineEmpty => SelectedJournal is not null && Timeline.Count == 0;

    public Task LoadAsync(CancellationToken cancellationToken = default) =>
        RefreshAsync(SelectedJournal?.Id, cancellationToken);

    public void SelectJournal(Journal? journal)
    {
        SelectedJournal = journal;
        ErrorMessage = null;
    }

    public void BeginCreateJournal()
    {
        IsCreatingJournal = true;
        JournalTitle = string.Empty;
        ErrorMessage = null;
        IsJournalEditorVisible = true;
    }

    public void BeginRenameJournal()
    {
        if (SelectedJournal is null)
        {
            ErrorMessage = "Choose a Journal first.";
            return;
        }

        if (SelectedJournal.IsArchived)
        {
            ErrorMessage = "Restore the Journal before renaming it.";
            return;
        }

        IsCreatingJournal = false;
        JournalTitle = SelectedJournal.Title;
        ErrorMessage = null;
        IsJournalEditorVisible = true;
    }

    public void CancelJournalEdit()
    {
        IsJournalEditorVisible = false;
        IsCreatingJournal = false;
        JournalTitle = string.Empty;
        ErrorMessage = null;
    }

    public async Task<bool> SaveJournalAsync(
        CancellationToken cancellationToken = default)
    {
        var title = JournalTitle.Trim();
        if (title.Length == 0)
        {
            ErrorMessage = "Journal title is required.";
            return false;
        }

        try
        {
            CoreOperationResult<Journal> result;
            if (IsCreatingJournal)
            {
                result = await _useCases.CreateJournalAsync(
                    new CreateJournalCommand(
                        new Journal(SecondBrainItemId.New(), title)),
                    cancellationToken);
            }
            else if (SelectedJournal is not null)
            {
                result = await _useCases.RenameJournalAsync(
                    new RenameJournalCommand(SelectedJournal.Id, title),
                    cancellationToken);
            }
            else
            {
                ErrorMessage = "Choose a Journal first.";
                return false;
            }

            if (!result.IsSuccess)
            {
                ErrorMessage = result.Error!.Message;
                return false;
            }

            IsJournalEditorVisible = false;
            IsCreatingJournal = false;
            JournalTitle = string.Empty;
            await RefreshAsync(result.Value!.Id, cancellationToken);
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return false;
        }
        catch (Exception exception)
        {
            ErrorMessage = $"Journal could not be saved. {exception.Message}";
            return false;
        }
    }

    public async Task<bool> ArchiveSelectedAsync(
        CancellationToken cancellationToken = default) =>
        await ChangeArchiveStateAsync(archive: true, cancellationToken);

    public async Task<bool> RestoreSelectedAsync(
        CancellationToken cancellationToken = default) =>
        await ChangeArchiveStateAsync(archive: false, cancellationToken);

    private async Task<bool> ChangeArchiveStateAsync(
        bool archive,
        CancellationToken cancellationToken)
    {
        if (SelectedJournal is null)
        {
            ErrorMessage = "Choose a Journal first.";
            return false;
        }

        var selectedId = SelectedJournal.Id;
        try
        {
            var result = archive
                ? await _useCases.ArchiveJournalAsync(
                    new ArchiveJournalCommand(selectedId),
                    cancellationToken)
                : await _useCases.RestoreJournalAsync(
                    new RestoreJournalCommand(selectedId),
                    cancellationToken);
            if (!result.IsSuccess)
            {
                ErrorMessage = result.Error!.Message;
                return false;
            }

            await RefreshAsync(selectedId, cancellationToken);
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return false;
        }
        catch (Exception exception)
        {
            ErrorMessage = $"Journal lifecycle could not be changed. {exception.Message}";
            return false;
        }
    }

    private async Task RefreshAsync(
        SecondBrainItemId? preferredJournalId,
        CancellationToken cancellationToken)
    {
        IsLoading = true;
        ErrorMessage = null;
        try
        {
            _state = await _repository.LoadStateAsync(cancellationToken);
            Journals = _state.Journals
                .OrderBy(journal => journal.IsArchived)
                .ThenBy(journal => journal.Title, StringComparer.OrdinalIgnoreCase)
                .ThenBy(journal => journal.Id.Value)
                .ToArray();
            var selectedJournal = preferredJournalId is null
                ? Journals.FirstOrDefault()
                : Journals.FirstOrDefault(journal => journal.Id == preferredJournalId.Value)
                    ?? Journals.FirstOrDefault();
            SelectedJournal = null;
            SelectedJournal = selectedJournal;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            ErrorMessage = $"Journals could not be loaded. {exception.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private string DescribePlacement(PrimaryPlacement placement)
    {
        if (_state is null)
        {
            return "PARA context unavailable";
        }

        return placement.Kind switch
        {
            PrimaryPlacementKind.Project =>
                "Project · " + (_state.Projects.SingleOrDefault(project =>
                    project.Id.Value == placement.ContextId)?.Name.Value ?? "Unavailable"),
            PrimaryPlacementKind.Area =>
                "Area · " + (_state.Areas.SingleOrDefault(area =>
                    area.Id.Value == placement.ContextId)?.Name.Value ?? "Unavailable"),
            PrimaryPlacementKind.ResourceTopic =>
                "Resource · " + (_state.ResourceTopics.SingleOrDefault(topic =>
                    topic.Id.Value == placement.ContextId)?.Name.Value ?? "Unavailable"),
            _ => "PARA context unavailable",
        };
    }
}
