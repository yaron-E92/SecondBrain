namespace SecondBrain.Application.NotionAudit;

public enum NotionImportTarget
{
    Project,
    Area,
    ResourceTopic,
    Note,
    Idea,
    JournalEntry,
    KnowledgeCapture,
    ResourceArtifact,
}

public enum NotionResourceResolution
{
    Exclude,
    Topic,
    Note,
    Artifact,
}

public sealed record NotionImportRecord(
    string DatabaseNotionId,
    string PageNotionId,
    string SourceName,
    NotionImportTarget Target,
    string ContentFingerprint,
    IReadOnlyDictionary<string, string> Values,
    IReadOnlyList<NotionImportRelation> Relations);

public sealed record NotionImportDiagnostic(
    string Code,
    string? SourceNotionId,
    string? TargetNotionId,
    string Message);

public sealed record NotionImportPlan(
    string SpecificationVersion,
    IReadOnlyList<NotionImportRecord> Records,
    IReadOnlyList<NotionImportDiagnostic> Skips,
    IReadOnlyList<NotionImportDiagnostic> Deferred,
    IReadOnlyList<NotionImportDiagnostic> UnresolvedLinks)
{
    public bool RequiresReview => Deferred.Count > 0;
}

public sealed record NotionImportResult(
    int Created,
    int Updated,
    int Skipped,
    int Conflicted,
    IReadOnlyList<NotionImportDiagnostic> Diagnostics,
    bool RolledBack = false)
{
    public IReadOnlyList<NotionImportedTarget> Targets { get; init; } = [];

    public bool BlockedByConflicts { get; init; }
}

public sealed record NotionImportedTarget(
    Guid TargetId,
    NotionImportTarget Kind,
    string Title);

public interface INotionImportExecutor
{
    Task<NotionImportResult> ExecuteAsync(
        NotionImportPlan plan,
        CancellationToken cancellationToken = default);
}

public sealed class NotionImportUseCase(
    INotionExportReader reader,
    INotionImportExecutor executor)
{
    private static readonly HashSet<string> Excluded = new(
        ["Tasks", "Chores", "PHOODAB", "Pantry", "Shopping", "Household", "Inventory", "Replenishment"],
        StringComparer.OrdinalIgnoreCase);

    public async Task<NotionImportPlan> PreviewAsync(
        string sourcePath,
        IReadOnlyDictionary<string, NotionResourceResolution>? resolutions = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        return Plan(await reader.ReadAsync(sourcePath, cancellationToken), resolutions);
    }

    public NotionImportPlan Plan(
        NotionExportMetadata export,
        IReadOnlyDictionary<string, NotionResourceResolution>? resolutions = null)
    {
        ArgumentNullException.ThrowIfNull(export);
        resolutions ??= new Dictionary<string, NotionResourceResolution>(StringComparer.OrdinalIgnoreCase);
        var records = new List<NotionImportRecord>();
        var skips = new List<NotionImportDiagnostic>();
        var deferred = new List<NotionImportDiagnostic>();
        var candidates = new Dictionary<string, NotionImportRecord>(StringComparer.OrdinalIgnoreCase);
        var conflicting = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var table in export.Tables)
        {
            var database = table.DatabaseName?.Trim();
            if (table.IsDuplicateAllView)
            {
                skips.Add(Diagnostic("duplicate-all-view", null, null, $"{table.SourceName} is a duplicate _all view."));
                continue;
            }

            if (Excluded.Contains(database ?? string.Empty))
            {
                var isShuffleTask = database is not null &&
                    (database.Equals("Tasks", StringComparison.OrdinalIgnoreCase) ||
                     database.Equals("Chores", StringComparison.OrdinalIgnoreCase));
                var code = isShuffleTask ? "module-owned-shuffletask" : "module-owned-phoodab";
                skips.Add(Diagnostic(code, null, null, $"{table.SourceName} is owned outside Core."));
                continue;
            }

            if (string.IsNullOrWhiteSpace(table.DatabaseNotionId) || string.IsNullOrWhiteSpace(database))
            {
                deferred.Add(Diagnostic("authoritative-database-identity-required", null, null, $"{table.SourceName} cannot be imported from its filename alone."));
                continue;
            }

            foreach (var row in table.Rows)
            {
                if (row.IsTemplate)
                {
                    skips.Add(Diagnostic("template", row.NotionId, null, "Template rows are not imported."));
                    continue;
                }

                if (string.IsNullOrWhiteSpace(row.NotionId))
                {
                    deferred.Add(Diagnostic("page-identity-required", null, null, "A row has no authoritative Notion page ID."));
                    continue;
                }

                var target = ResolveTarget(database, row, resolutions, deferred, skips);
                if (target is null)
                {
                    continue;
                }

                var relations = ResolveRelations(row, deferred);
                if (relations is null)
                {
                    continue;
                }

                var record = new NotionImportRecord(
                    table.DatabaseNotionId,
                    row.NotionId,
                    table.SourceName,
                    target.Value,
                    row.ContentFingerprint ?? string.Empty,
                    row.Values,
                    relations);
                if (candidates.TryGetValue(row.NotionId, out var existing))
                {
                    if (!string.Equals(existing.ContentFingerprint, record.ContentFingerprint, StringComparison.Ordinal))
                    {
                        conflicting.Add(row.NotionId);
                    }
                }
                else
                {
                    candidates.Add(row.NotionId, record);
                }
            }
        }

        foreach (var conflict in conflicting)
        {
            candidates.Remove(conflict);
            deferred.Add(Diagnostic("conflicting-page-id", conflict, null, "Repeated Notion page ID has conflicting content."));
        }

        records.AddRange(candidates.Values);
        Preflight(records, deferred);
        var recordsById = records.ToDictionary(record => record.PageNotionId, StringComparer.OrdinalIgnoreCase);
        var unresolved = new List<NotionImportDiagnostic>();
        foreach (var record in records)
        {
            foreach (var relation in record.Relations)
            {
                foreach (var targetId in relation.TargetNotionIds.Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    if (!recordsById.TryGetValue(targetId, out var targetRecord))
                    {
                        unresolved.Add(Diagnostic("unresolved-link", record.PageNotionId, targetId,
                            $"{relation.FieldName} target is not eligible for import; no placeholder was created."));
                        continue;
                    }

                    if (!IsBrainItem(record.Target) || !IsBrainItem(targetRecord.Target))
                    {
                        unresolved.Add(Diagnostic("relation-not-representable", record.PageNotionId, targetId,
                            $"{relation.FieldName} links records whose relationship cannot be represented by the current Core relation model; no relation was silently created."));
                    }
                }
            }
        }

        return new NotionImportPlan("1.0", records, skips, deferred, unresolved);
    }

    public Task<NotionImportResult> ConfirmAsync(
        NotionImportPlan plan,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plan);
        if (plan.RequiresReview)
        {
            throw new InvalidOperationException("Resolve or exclude every blocking import decision before confirmation.");
        }

        return executor.ExecuteAsync(plan, cancellationToken);
    }

    private static IReadOnlyList<NotionImportRelation>? ResolveRelations(
        NotionExportRowMetadata row,
        List<NotionImportDiagnostic> deferred)
    {
        var result = new List<NotionImportRelation>();
        foreach (var relation in row.Relations)
        {
            if (IsNonImportedRelationField(relation.FieldName))
            {
                continue;
            }

            if (!TryResolveRelationKind(relation.DeclaredType, out var kind))
            {
                deferred.Add(Diagnostic(
                    "unsupported-relation-type",
                    row.NotionId,
                    null,
                    $"{relation.FieldName} declares relation type '{relation.DeclaredType}', which mapping v1 cannot preserve safely."));
                return null;
            }

            result.Add(new NotionImportRelation(relation.FieldName, kind, relation.TargetNotionIds));
        }

        return result;
    }

    private static bool TryResolveRelationKind(
        string? declaredType,
        out NotionImportRelationKind kind)
    {
        if (string.IsNullOrWhiteSpace(declaredType))
        {
            kind = NotionImportRelationKind.Contextual;
            return true;
        }

        switch (Normalize(declaredType).Replace("-", string.Empty, StringComparison.Ordinal))
        {
            case "related":
            case "contextual":
                kind = NotionImportRelationKind.Contextual;
                return true;
            case "derived":
                kind = NotionImportRelationKind.Derived;
                return true;
            case "provenance":
                kind = NotionImportRelationKind.Provenance;
                return true;
            default:
                kind = default;
                return false;
        }
    }

    private static bool IsNonImportedRelationField(string fieldName)
    {
        var normalized = Normalize(fieldName);
        return normalized is "primarynotionid" or "placementnotionid" or "primaryplacement" or "placement" or "tags";
    }

    private static NotionImportTarget? ResolveTarget(
        string database,
        NotionExportRowMetadata row,
        IReadOnlyDictionary<string, NotionResourceResolution> resolutions,
        List<NotionImportDiagnostic> deferred,
        List<NotionImportDiagnostic> skips)
    {
        if (!database.Equals("Resources", StringComparison.OrdinalIgnoreCase))
        {
            if (database.Equals("Archive", StringComparison.OrdinalIgnoreCase) ||
                database.Equals("Global Tags", StringComparison.OrdinalIgnoreCase) ||
                database.Equals("Tags", StringComparison.OrdinalIgnoreCase))
            {
                skips.Add(Diagnostic("not-imported-by-v1-mapping", row.NotionId, null,
                    "This auxiliary database has no direct Core target in mapping v1."));
                return null;
            }

            return database.ToLowerInvariant() switch
            {
                "projects" => NotionImportTarget.Project,
                "areas" => NotionImportTarget.Area,
                "notes" => NotionImportTarget.Note,
                "ideas" => NotionImportTarget.Idea,
                "journals" => NotionImportTarget.JournalEntry,
                "captures" => NotionImportTarget.KnowledgeCapture,
                _ => Defer("unsupported-database", row.NotionId, deferred),
            };
        }

        var resolution = row.Classification?.ToLowerInvariant() switch
        {
            "topic" => NotionResourceResolution.Topic,
            "note" => NotionResourceResolution.Note,
            "artifact" => NotionResourceResolution.Artifact,
            _ when resolutions.TryGetValue(row.NotionId!, out var selected) => selected,
            _ => (NotionResourceResolution?)null,
        };
        if (resolution is null)
        {
            deferred.Add(Diagnostic("ambiguous-resource-classification-required", row.NotionId, null,
                "Choose Topic, Note, Artifact, or Exclude."));
            return null;
        }

        if (resolution == NotionResourceResolution.Exclude)
        {
            skips.Add(Diagnostic("user-excluded", row.NotionId, null, "The Resource was explicitly excluded during review."));
            return null;
        }

        return resolution switch
        {
            NotionResourceResolution.Topic => NotionImportTarget.ResourceTopic,
            NotionResourceResolution.Note => NotionImportTarget.Note,
            _ => NotionImportTarget.ResourceArtifact,
        };
    }

    private static NotionImportTarget? Defer(
        string code,
        string? sourceId,
        List<NotionImportDiagnostic> deferred)
    {
        deferred.Add(Diagnostic(code, sourceId, null, "This database is not part of the Core v1 import mapping."));
        return null;
    }

    private static NotionImportDiagnostic Diagnostic(string code, string? source, string? target, string message) =>
        new(code, source, target, message);

    private static void Preflight(
        List<NotionImportRecord> records,
        List<NotionImportDiagnostic> deferred)
    {
        bool removed;
        do
        {
            removed = false;
            var targets = records.Select(record => record.PageNotionId)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var record in records.ToArray())
            {
                var reason = ValidationFailure(record, targets);
                if (reason is null)
                {
                    continue;
                }

                records.Remove(record);
                deferred.Add(Diagnostic("invalid-target-data", record.PageNotionId, null, reason));
                removed = true;
            }
        }
        while (removed);
    }

    private static string? ValidationFailure(
        NotionImportRecord record,
        IReadOnlySet<string> targets)
    {
        if (!Guid.TryParseExact(record.PageNotionId, "N", out _))
        {
            return "The authoritative page ID is invalid. Correct the export metadata and retry.";
        }

        if (string.IsNullOrWhiteSpace(Value(record, "name")))
        {
            return "A required name is missing. Correct the source and retry.";
        }

        if (!IsBrainItem(record.Target))
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(Value(record, "content")))
        {
            return "Required content is missing. Correct the source and retry.";
        }

        var placementId = Value(record, "primaryNotionId") ?? Value(record, "placementNotionId");
        var placementType = Value(record, "primaryType")?.Replace(" ", string.Empty, StringComparison.Ordinal);
        if (string.IsNullOrWhiteSpace(placementId) || !targets.Contains(placementId.Replace("-", string.Empty, StringComparison.Ordinal)) ||
            placementType is null || !(placementType.Equals("Project", StringComparison.OrdinalIgnoreCase) ||
              placementType.Equals("Area", StringComparison.OrdinalIgnoreCase) ||
              placementType.Equals("ResourceTopic", StringComparison.OrdinalIgnoreCase)))
        {
            return "The primary placement cannot be resolved by Notion page ID. Correct the placement and retry.";
        }

        if (record.Target == NotionImportTarget.JournalEntry &&
            !DateOnly.TryParse(Value(record, "entryDate"), out _))
        {
            return "A valid journal entry date is required. Correct the source and retry.";
        }

        if (record.Target == NotionImportTarget.KnowledgeCapture &&
            (string.IsNullOrWhiteSpace(Value(record, "sourceUrl")) ||
             string.IsNullOrWhiteSpace(Value(record, "sourceCitation"))))
        {
            return "Capture source URL and citation are required. Correct the source and retry.";
        }

        return null;
    }

    private static string? Value(NotionImportRecord record, string name) => record.Values
        .FirstOrDefault(pair => Normalize(pair.Key) == Normalize(name)).Value;

    private static string Normalize(string value) =>
        value.Replace(" ", string.Empty, StringComparison.Ordinal).ToLowerInvariant();

    private static bool IsBrainItem(NotionImportTarget target) => target is not
        (NotionImportTarget.Project or NotionImportTarget.Area or NotionImportTarget.ResourceTopic);
}
