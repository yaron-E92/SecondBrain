using Microsoft.EntityFrameworkCore;
using SecondBrain.Application.NotionAudit;
using SecondBrain.Domain.Entities;
using SecondBrain.Domain.ValueObjects;

namespace SecondBrain.Persistence;

public sealed class NotionImportExecutor : INotionImportExecutor
{
    private readonly IDbContextFactory<SecondBrainDbContext>? _contextFactory;
    private readonly SecondBrainDbContext? _suppliedContext;

    public NotionImportExecutor(IDbContextFactory<SecondBrainDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    // Kept for focused tests and callers that explicitly own a context.
    public NotionImportExecutor(SecondBrainDbContext context)
    {
        _suppliedContext = context;
    }

    public async Task<NotionImportResult> ExecuteAsync(
        NotionImportPlan plan,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plan);
        if (_suppliedContext is not null)
        {
            return await ExecuteCoreAsync(_suppliedContext, plan, cancellationToken);
        }

        await using var context = await _contextFactory!.CreateDbContextAsync(cancellationToken);
        return await ExecuteCoreAsync(context, plan, cancellationToken);
    }

    private static async Task<NotionImportResult> ExecuteCoreAsync(
        SecondBrainDbContext context,
        NotionImportPlan plan,
        CancellationToken cancellationToken)
    {
        var diagnostics = new List<NotionImportDiagnostic>(plan.UnresolvedLinks);
        var sourceIds = plan.Records.Select(record => record.PageNotionId).ToArray();
        var existing = await context.NotionImportProvenance
            .Where(row => sourceIds.Contains(row.PageNotionId))
            .ToDictionaryAsync(row => row.PageNotionId, StringComparer.OrdinalIgnoreCase, cancellationToken);
        var conflicts = plan.Records.Where(record => existing.TryGetValue(record.PageNotionId, out var prior) &&
                (!string.Equals(prior.DatabaseNotionId, record.DatabaseNotionId, StringComparison.OrdinalIgnoreCase) ||
                 !string.Equals(prior.ContentFingerprint, record.ContentFingerprint, StringComparison.Ordinal)))
            .ToArray();
        if (conflicts.Length > 0)
        {
            diagnostics.AddRange(conflicts.Select(record => new NotionImportDiagnostic(
                NotionImportDiagnosticCodes.ChangedSourceConflict, record.PageNotionId, null,
                "This Notion page was imported previously with different content; existing Core data was preserved.")));
            diagnostics.Add(new NotionImportDiagnostic(
                NotionImportDiagnosticCodes.ImportBlockedByConflicts, null, null,
                $"No changes were applied because {conflicts.Length} previously imported source record(s) changed. Resolve the conflicts and preview again."));
            var conflictIds = conflicts.Select(record => record.PageNotionId)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            return new NotionImportResult(
                0,
                0,
                plan.Records.Count - conflicts.Length,
                conflicts.Length,
                diagnostics)
            {
                BlockedByConflicts = true,
                Targets = ResultTargets(
                    plan.Records.Where(record => !conflictIds.Contains(record.PageNotionId)),
                    existing)
            };
        }

        var pending = plan.Records.Where(record => !existing.ContainsKey(record.PageNotionId)).ToArray();
        if (pending.Length == 0)
        {
            return new NotionImportResult(0, 0, plan.Records.Count, 0, diagnostics)
            {
                Targets = ResultTargets(plan.Records, existing)
            };
        }

        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var targets = plan.Records.ToDictionary(
                record => record.PageNotionId,
                record => existing.TryGetValue(record.PageNotionId, out var prior)
                    ? prior.TargetId
                    : ParseId(record.PageNotionId),
                StringComparer.OrdinalIgnoreCase);
            foreach (var record in pending)
            {
                AddTarget(context, record, targets);
                context.NotionImportProvenance.Add(new NotionImportProvenanceRow
                {
                    DatabaseNotionId = record.DatabaseNotionId,
                    PageNotionId = record.PageNotionId,
                    SpecificationVersion = plan.SpecificationVersion,
                    ContentFingerprint = record.ContentFingerprint,
                    TargetKind = record.Target.ToString(),
                    TargetId = targets[record.PageNotionId],
                });
            }

            // Establish every new Core identity before relation rows reference them.
            // Both phases remain inside this transaction, so a later relation failure
            // still rolls back the target/provenance writes atomically.
            await context.SaveChangesAsync(cancellationToken);
            await AddRelationsAsync(context, pending, targets, diagnostics, cancellationToken);
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new NotionImportResult(pending.Length, 0, existing.Count, 0, diagnostics)
            {
                Targets = plan.Records.Select(record => new NotionImportedTarget(
                    targets[record.PageNotionId], record.Target, Value(record, "name") ?? record.Target.ToString())).ToArray()
            };
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            diagnostics.Add(new NotionImportDiagnostic(
                NotionImportDiagnosticCodes.TransactionRolledBack, null, null,
                $"Nothing was imported. Correct the source and retry. {RootMessage(exception)}"));
            return new NotionImportResult(0, 0, existing.Count, 0, diagnostics, true);
        }
    }

    private static string RootMessage(Exception exception)
    {
        var current = exception;
        while (current.InnerException is not null)
        {
            current = current.InnerException;
        }

        return current.Message;
    }

    private static void AddTarget(
        SecondBrainDbContext context,
        NotionImportRecord record,
        IReadOnlyDictionary<string, Guid> targets)
    {
        var id = targets[record.PageNotionId];
        var name = Required(record, "name");
        switch (record.Target)
        {
            case NotionImportTarget.Project:
                context.Projects.Add(new ProjectRow
                {
                    Id = id,
                    Name = name,
                    Outcome = Value(record, "outcome") ?? name,
                    Status = Parse(Value(record, "status"), ProjectStatus.Planned),
                    Priority = Parse(Value(record, "priority"), ProjectPriority.Normal),
                    TargetDate = Date(Value(record, "dueDate") ?? Value(record, "due")),
                    IsArchived = Boolean(record, "archived"),
                });
                return;
            case NotionImportTarget.Area:
                context.Areas.Add(new AreaRow { Id = id, Name = name, IsArchived = Boolean(record, "archived") });
                return;
            case NotionImportTarget.ResourceTopic:
                context.ResourceTopics.Add(new ResourceTopicRow { Id = id, Name = name, IsArchived = Boolean(record, "archived") });
                return;
        }

        var placement = Placement(record, targets);
        var row = new BrainItemRow
        {
            Id = id,
            Kind = Kind(record.Target),
            Title = name,
            Content = Required(record, "content"),
            PlacementKind = placement.Kind,
            ProjectId = placement.Kind == PrimaryPlacementKind.Project ? placement.Id : null,
            AreaId = placement.Kind == PrimaryPlacementKind.Area ? placement.Id : null,
            ResourceTopicId = placement.Kind == PrimaryPlacementKind.ResourceTopic ? placement.Id : null,
            CreatedAt = Timestamp(Value(record, "createdAt")),
            UpdatedAt = Timestamp(Value(record, "updatedAt") ?? Value(record, "createdAt")),
            IsArchived = Boolean(record, "archived"),
        };
        ApplyKindFields(row, record);
        context.BrainItems.Add(row);
        foreach (var tag in Split(Value(record, "tags")).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            context.BrainItemTextTags.Add(new BrainItemTextTagRow { BrainItemId = id, Value = tag });
        }
    }

    private static async Task AddRelationsAsync(
        SecondBrainDbContext context,
        IEnumerable<NotionImportRecord> records,
        IReadOnlyDictionary<string, Guid> targets,
        List<NotionImportDiagnostic> diagnostics,
        CancellationToken cancellationToken)
    {
        var targetIds = targets.Values.ToArray();
        var brainItemIds = (await context.BrainItems
            .Where(item => targetIds.Contains(item.Id))
            .Select(item => item.Id)
            .ToListAsync(cancellationToken))
            .ToHashSet();
        brainItemIds.UnionWith(context.BrainItems.Local.Select(item => item.Id));
        foreach (var record in records.Where(record => IsBrainItem(record.Target)))
        {
            var source = targets[record.PageNotionId];
            var added = new HashSet<(Guid TargetId, BrainItemRelationKind Kind)>();
            foreach (var relation in record.Relations)
            {
                var kind = RelationKind(relation.Kind);
                foreach (var target in relation.TargetNotionIds
                             .Where(targets.ContainsKey)
                             .Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    var targetId = targets[target];
                    if (source == targetId)
                    {
                        continue;
                    }

                    if (!brainItemIds.Contains(targetId))
                    {
                        if (!diagnostics.Any(diagnostic =>
                                diagnostic.Code == NotionImportDiagnosticCodes.RelationNotRepresentable &&
                                string.Equals(diagnostic.SourceNotionId, record.PageNotionId, StringComparison.OrdinalIgnoreCase) &&
                                string.Equals(diagnostic.TargetNotionId, target, StringComparison.OrdinalIgnoreCase)))
                        {
                            diagnostics.Add(new NotionImportDiagnostic(
                                NotionImportDiagnosticCodes.RelationNotRepresentable,
                                record.PageNotionId,
                                target,
                                $"{relation.FieldName} targets a Core context rather than a BrainItem; the relation was reported instead of silently discarded."));
                        }

                        continue;
                    }

                    if (!added.Add((targetId, kind)))
                    {
                        continue;
                    }

                    context.BrainItemRelations.Add(new BrainItemRelationRow
                    {
                        SourceId = source,
                        TargetId = targetId,
                        Kind = kind,
                    });
                }
            }
        }
    }

    private static BrainItemRelationKind RelationKind(NotionImportRelationKind kind) => kind switch
    {
        NotionImportRelationKind.Contextual => BrainItemRelationKind.Contextual,
        NotionImportRelationKind.Derived => BrainItemRelationKind.Derived,
        NotionImportRelationKind.Provenance => BrainItemRelationKind.Provenance,
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    private static void ApplyKindFields(BrainItemRow row, NotionImportRecord record)
    {
        switch (row.Kind)
        {
            case BrainItemKind.Note:
                row.NoteKind = Parse(Value(record, "noteKind"), NoteKind.General);
                break;
            case BrainItemKind.Idea:
                row.IdeaMaturity = Parse(Value(record, "maturity"), IdeaMaturity.Captured);
                break;
            case BrainItemKind.JournalEntry:
                row.EntryDate = Date(Value(record, "entryDate")) ?? throw new InvalidDataException("Journal entry date is required.");
                break;
            case BrainItemKind.KnowledgeCapture:
                row.CaptureSourceType = Parse(Value(record, "sourceType"), CaptureSourceType.Page);
                row.SourceUri = Required(record, "sourceUrl");
                row.SourceCitation = Required(record, "sourceCitation");
                row.ReminderAt = OptionalTimestamp(Value(record, "reminder"));
                row.CaptureProcessingState = Parse(Value(record, "processingState"), CaptureProcessingState.Captured);
                break;
            case BrainItemKind.ResourceArtifact:
                row.ResourceArtifactKind = Parse(Value(record, "artifactKind"), ResourceArtifactKind.Guide);
                row.ResourceFreshness = Parse(Value(record, "freshness"), ResourceFreshness.Draft);
                row.ReviewDate = Date(Value(record, "reviewDate"));
                break;
        }
    }

    private static (PrimaryPlacementKind Kind, Guid Id) Placement(
        NotionImportRecord record,
        IReadOnlyDictionary<string, Guid> targets)
    {
        var sourceId = Value(record, "primaryNotionId") ?? Value(record, "placementNotionId");
        if (sourceId is null || !targets.TryGetValue(sourceId.Replace("-", string.Empty, StringComparison.Ordinal), out var id))
        {
            throw new InvalidDataException($"{record.PageNotionId} has no resolvable primary placement.");
        }

        return (Value(record, "primaryType")?.ToLowerInvariant() switch
        {
            "project" => PrimaryPlacementKind.Project,
            "area" => PrimaryPlacementKind.Area,
            "resourcetopic" => PrimaryPlacementKind.ResourceTopic,
            _ => throw new InvalidDataException($"{record.PageNotionId} has an unsupported primary placement type."),
        }, id);
    }

    private static Guid ParseId(string value) => Guid.ParseExact(value, "N");
    private static IReadOnlyList<NotionImportedTarget> ResultTargets(
        IEnumerable<NotionImportRecord> records,
        IReadOnlyDictionary<string, NotionImportProvenanceRow> provenance) => records
        .Where(record => provenance.ContainsKey(record.PageNotionId))
        .Select(record => new NotionImportedTarget(
            provenance[record.PageNotionId].TargetId,
            record.Target,
            Value(record, "name") ?? record.Target.ToString()))
        .ToArray();
    private static string Required(NotionImportRecord record, string name) =>
        Value(record, name) is { } value && !string.IsNullOrWhiteSpace(value)
            ? value.Trim()
            : throw new InvalidDataException($"{record.PageNotionId} is missing required field {name}.");
    private static string? Value(NotionImportRecord record, string name) => record.Values
        .FirstOrDefault(pair => Normalize(pair.Key) == Normalize(name)).Value;
    private static string Normalize(string value) => value.Replace(" ", string.Empty, StringComparison.Ordinal).ToLowerInvariant();
    private static bool Boolean(NotionImportRecord record, string name) => bool.TryParse(Value(record, name), out var value) && value;
    private static T Parse<T>(string? value, T fallback) where T : struct, Enum =>
        Enum.TryParse<T>(value, true, out var parsed) && Enum.IsDefined(parsed) ? parsed : fallback;
    private static DateOnly? Date(string? value) => DateOnly.TryParse(value, out var parsed) ? parsed : null;
    private static DateTimeOffset Timestamp(string? value) => OptionalTimestamp(value) ?? DateTimeOffset.UnixEpoch;
    private static DateTimeOffset? OptionalTimestamp(string? value) => DateTimeOffset.TryParse(value, out var parsed) ? parsed : null;
    private static IEnumerable<string> Split(string? value) => (value ?? string.Empty)
        .Split([',', ';'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
    private static bool IsBrainItem(NotionImportTarget target) => target is not (NotionImportTarget.Project or NotionImportTarget.Area or NotionImportTarget.ResourceTopic);
    private static BrainItemKind Kind(NotionImportTarget target) => target switch
    {
        NotionImportTarget.Note => BrainItemKind.Note,
        NotionImportTarget.Idea => BrainItemKind.Idea,
        NotionImportTarget.JournalEntry => BrainItemKind.JournalEntry,
        NotionImportTarget.KnowledgeCapture => BrainItemKind.KnowledgeCapture,
        NotionImportTarget.ResourceArtifact => BrainItemKind.ResourceArtifact,
        _ => throw new ArgumentOutOfRangeException(nameof(target)),
    };
}
