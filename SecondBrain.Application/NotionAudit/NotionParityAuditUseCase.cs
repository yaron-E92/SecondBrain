using System.Text;
using System.Text.Json;

namespace SecondBrain.Application.NotionAudit;

public sealed class NotionParityAuditUseCase(INotionExportReader reader)
{
    private static readonly HashSet<string> Supported = new(
        ["Projects", "Areas", "Notes", "Ideas", "Journals", "Captures"],
        StringComparer.OrdinalIgnoreCase);

    private static readonly HashSet<string> ShuffleTask = new(
        ["Tasks", "Chores"], StringComparer.OrdinalIgnoreCase);

    private static readonly HashSet<string> Phoodab = new(
        ["PHOODAB", "Pantry", "Shopping", "Household", "Inventory", "Replenishment"],
        StringComparer.OrdinalIgnoreCase);

    public async Task<NotionAuditReport> AuditAsync(
        string sourcePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        var export = await reader.ReadAsync(sourcePath, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        return Analyze(export);
    }

    public NotionAuditReport Analyze(NotionExportMetadata export)
    {
        ArgumentNullException.ThrowIfNull(export);
        var sections = new List<NotionAuditSection>();
        var risks = new List<NotionRelationshipRisk>();
        var diagnostics = new List<string>(export.Diagnostics);
        var conflictingPageIds = FindConflictingPageIds(export, diagnostics);
        var classifiedTables = export.Tables.Select(table =>
        {
            var rows = EffectiveRows(table.Rows);
            var status = Classify(table, table.DatabaseName?.Trim(), rows);
            if (status == NotionAuditStatus.CoreSupported && rows.Any(row =>
                    row.NotionId is not null && conflictingPageIds.Contains(row.NotionId)))
            {
                status = NotionAuditStatus.CoreSupportedWithReview;
            }

            return (Table: table, Rows: rows, Status: status);
        }).ToArray();
        var knownPageIds = export.Tables
            .Where(table => !table.IsDuplicateAllView)
            .SelectMany(table => table.Rows)
            .Where(row => !row.IsTemplate && !string.IsNullOrWhiteSpace(row.NotionId))
            .Select(row => row.NotionId!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var excludedPageIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var ambiguousPageIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var reviewPageIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        reviewPageIds.UnionWith(conflictingPageIds);
        var unsupportedPageIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var classified in classifiedTables)
        {
            if (classified.Status == NotionAuditStatus.ModuleOwnedExcluded)
            {
                AddIds(excludedPageIds, classified.Rows);
            }
            else if (classified.Status == NotionAuditStatus.Ambiguous)
            {
                AddIds(
                    ambiguousPageIds,
                    classified.Table.DatabaseName?.Equals(
                        "Resources",
                        StringComparison.OrdinalIgnoreCase) == true
                        ? classified.Rows.Where(IsAmbiguousResource)
                        : classified.Rows);
            }
            else if (classified.Status == NotionAuditStatus.CoreSupportedWithReview)
            {
                AddIds(reviewPageIds, classified.Rows);
            }
            else if (classified.Status == NotionAuditStatus.Unsupported)
            {
                AddIds(unsupportedPageIds, classified.Rows);
            }
        }

        foreach (var classified in classifiedTables)
        {
            var table = classified.Table;
            var name = table.DatabaseName?.Trim();
            var rows = classified.Rows;
            var status = classified.Status;

            var rowCount = table.IsDuplicateAllView ? 0 : rows.Count;
            sections.Add(new NotionAuditSection(
                name ?? "Unidentified database",
                status,
                rowCount,
                Outcome(status),
                table.Fields.Order(StringComparer.OrdinalIgnoreCase).ToArray())
            {
                FieldMappings = table.Fields
                    .Order(StringComparer.OrdinalIgnoreCase)
                    .Select(field => MapField(field, status))
                    .ToArray()
            });

            if (table.IsDuplicateAllView)
            {
                continue;
            }

            foreach (var relationGroup in rows
                .SelectMany(row => row.Relations)
                .GroupBy(relation => relation.FieldName, StringComparer.OrdinalIgnoreCase))
            {
                var targets = relationGroup.SelectMany(relation => relation.TargetNotionIds).ToArray();
                var unresolved = targets.Count(target => !knownPageIds.Contains(target));
                var excluded = targets.Count(excludedPageIds.Contains);
                var ambiguous = targets.Count(ambiguousPageIds.Contains);
                var review = targets.Count(reviewPageIds.Contains);
                var unsupported = targets.Count(unsupportedPageIds.Contains);
                var hasTargetRisk = unresolved + excluded + ambiguous + review + unsupported > 0;
                var sourceNeedsReview = status is NotionAuditStatus.CoreSupportedWithReview or
                    NotionAuditStatus.Ambiguous;
                var preservation = status switch
                {
                    NotionAuditStatus.ModuleOwnedExcluded => "will be skipped with module-owned source",
                    NotionAuditStatus.Unsupported => "cannot currently be represented",
                    _ when hasTargetRisk || sourceNeedsReview => "needs review",
                    _ => "will preserve by Notion page ID",
                };
                var message = status switch
                {
                    NotionAuditStatus.ModuleOwnedExcluded =>
                        "The source is module-owned or excluded from Core; no relationship will be imported.",
                    NotionAuditStatus.Unsupported =>
                        "The source cannot currently be represented in Core; no relationship will be imported.",
                    _ when !hasTargetRisk && sourceNeedsReview =>
                        "Targets are present, but the source mapping requires review before this relation can be preserved.",
                    _ when !hasTargetRisk =>
                        "Targets are present and eligible; relation type will be retained.",
                    _ =>
                        $"{unresolved} target(s) are missing, {excluded} are module-owned/excluded, {ambiguous} are ambiguous, {review} require review, and {unsupported} are unsupported. No placeholder will be created.",
                };
                risks.Add(new NotionRelationshipRisk(
                    name ?? "Unidentified database",
                    relationGroup.Key,
                    preservation,
                    targets.Length,
                    message));
            }
        }

        var summary = new NotionAuditSummary(
            "1.0",
            sections.Where(section => section.Status == NotionAuditStatus.CoreSupported).Sum(section => section.RowCount),
            sections.Where(section => section.Status is NotionAuditStatus.CoreSupportedWithReview or NotionAuditStatus.Ambiguous).Sum(section => section.RowCount),
            sections.Where(section => section.Status == NotionAuditStatus.ModuleOwnedExcluded).Sum(section => section.RowCount),
            sections.Where(section => section.Status == NotionAuditStatus.Unsupported).Sum(section => section.RowCount),
            export.Tables.Where(table => table.IsDuplicateAllView).Sum(table => EffectiveRows(table.Rows).Count),
            sections,
            risks,
            diagnostics);
        return new NotionAuditReport(
            summary,
            RenderHumanReadable(summary),
            JsonSerializer.Serialize(summary, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static NotionAuditStatus Classify(
        NotionExportTableMetadata table,
        string? name,
        IReadOnlyList<NotionExportRowMetadata> rows)
    {
        if (table.IsDuplicateAllView)
        {
            return NotionAuditStatus.DuplicateView;
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            return NotionAuditStatus.Ambiguous;
        }

        if (ShuffleTask.Contains(name) || Phoodab.Contains(name))
        {
            return NotionAuditStatus.ModuleOwnedExcluded;
        }

        if (string.IsNullOrWhiteSpace(table.DatabaseNotionId))
        {
            return NotionAuditStatus.Ambiguous;
        }

        if (name.Equals("Archive", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("Global Tags", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("Tags", StringComparison.OrdinalIgnoreCase))
        {
            return NotionAuditStatus.CoreSupportedWithReview;
        }

        if (name.Equals("Resources", StringComparison.OrdinalIgnoreCase))
        {
            return rows.Any(IsAmbiguousResource)
                ? NotionAuditStatus.Ambiguous
                : NotionAuditStatus.CoreSupportedWithReview;
        }

        return Supported.Contains(name)
            ? NotionAuditStatus.CoreSupported
            : NotionAuditStatus.Unsupported;
    }

    private static string Outcome(NotionAuditStatus status) => status switch
    {
        NotionAuditStatus.CoreSupported => "will import",
        NotionAuditStatus.CoreSupportedWithReview => "needs review before import",
        NotionAuditStatus.ModuleOwnedExcluded => "will be skipped because module-owned or excluded",
        NotionAuditStatus.Unsupported => "cannot currently be represented",
        NotionAuditStatus.DuplicateView => "duplicate view; visible but not counted",
        _ => "ambiguous; needs review",
    };

    private static NotionAuditField MapField(string field, NotionAuditStatus tableStatus)
    {
        if (tableStatus is NotionAuditStatus.DuplicateView or
            NotionAuditStatus.ModuleOwnedExcluded or
            NotionAuditStatus.Unsupported or
            NotionAuditStatus.Ambiguous)
        {
            return new NotionAuditField(field, tableStatus, Outcome(tableStatus));
        }

        var normalized = field.Replace(" ", string.Empty, StringComparison.Ordinal).ToLowerInvariant();
        if (normalized.Contains("relation", StringComparison.Ordinal) ||
            (normalized != "notionid" && normalized.EndsWith("notionid", StringComparison.Ordinal)) ||
            normalized.EndsWith("notionids", StringComparison.Ordinal) ||
            normalized is "tags" or "links" or "placement" or "primaryplacement" or "primarytype" or
                "project" or "projects" or "area" or "areas" or "resource" or "resources" or
                "resourcetopic" or "resourcetopics")
        {
            return new NotionAuditField(
                field,
                NotionAuditStatus.CoreSupportedWithReview,
                "needs review for relationship preservation");
        }

        var supported = normalized is
            "name" or "content" or "notionid" or "status" or "priority" or
            "start" or "startdate" or "due" or "duedate" or "archived" or
            "istemplate" or "notekind" or "maturity" or "entrydate" or
            "sourcetype" or "sourceurl" or "sourcecitation" or "reminder" or
            "processingstate" or "classification" or "artifactkind" or
            "freshness" or "reviewdate";
        return supported
            ? new NotionAuditField(field, NotionAuditStatus.CoreSupported, "will import")
            : new NotionAuditField(
                field,
                NotionAuditStatus.Unsupported,
                "cannot currently be represented");
    }

    private static void AddIds(HashSet<string> destination, IEnumerable<NotionExportRowMetadata> rows)
    {
        foreach (var id in rows.Select(row => row.NotionId).Where(id => !string.IsNullOrWhiteSpace(id)))
        {
            destination.Add(id!);
        }
    }

    private static bool IsAmbiguousResource(NotionExportRowMetadata row) =>
        string.IsNullOrWhiteSpace(row.Classification) ||
        !(row.Classification.Equals("Topic", StringComparison.OrdinalIgnoreCase) ||
          row.Classification.Equals("Artifact", StringComparison.OrdinalIgnoreCase) ||
          row.Classification.Equals("Note", StringComparison.OrdinalIgnoreCase));

    private static IReadOnlyList<NotionExportRowMetadata> EffectiveRows(
        IReadOnlyList<NotionExportRowMetadata> rows)
    {
        var result = new List<NotionExportRowMetadata>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows.Where(row => !row.IsTemplate))
        {
            if (string.IsNullOrWhiteSpace(row.NotionId) || seen.Add(row.NotionId))
            {
                result.Add(row);
            }
        }

        return result;
    }

    private static HashSet<string> FindConflictingPageIds(
        NotionExportMetadata export,
        List<string> diagnostics)
    {
        var conflicts = export.Tables
            .Where(table => !table.IsDuplicateAllView)
            .SelectMany(table => table.Rows.Select(row => (table.SourceName, Row: row)))
            .Where(item => !string.IsNullOrWhiteSpace(item.Row.NotionId))
            .GroupBy(item => item.Row.NotionId!, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Select(item => RowSignature(item.Row))
                .Distinct(StringComparer.Ordinal).Count() > 1)
            .ToArray();
        foreach (var group in conflicts)
        {
            diagnostics.Add($"A repeated Notion page ID appears in {group.Select(item => item.SourceName).Distinct().Count()} source file(s); conflicting rows require review.");
        }

        return conflicts
            .Select(group => group.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static string RowSignature(NotionExportRowMetadata row) =>
        string.Join(
            "|",
            row.Classification ?? string.Empty,
            row.IsTemplate,
            row.IsArchived,
            row.ContentFingerprint ?? string.Empty,
            string.Join(
                ";",
                row.Relations
                    .OrderBy(relation => relation.FieldName, StringComparer.OrdinalIgnoreCase)
                    .Select(relation => $"{relation.FieldName}:{string.Join(',', relation.TargetNotionIds.Order(StringComparer.OrdinalIgnoreCase))}")));

    private static string RenderHumanReadable(NotionAuditSummary summary)
    {
        var report = new StringBuilder()
            .AppendLine("Notion export parity audit (read-only)")
            .AppendLine($"Will import: {summary.WillImport}")
            .AppendLine($"Needs review: {summary.NeedsReview}")
            .AppendLine($"Module-owned/excluded: {summary.ModuleOwnedOrExcluded}")
            .AppendLine($"Cannot currently be represented: {summary.Unsupported}")
            .AppendLine($"Duplicate-view rows ignored: {summary.DuplicateRowsIgnored}");
        foreach (var section in summary.Sections)
        {
            report.AppendLine($"• {section.Name}: {section.RowCount} — {section.Outcome}");
        }

        foreach (var risk in summary.RelationshipRisks)
        {
            report.AppendLine($"• Relationship {risk.Source}.{risk.Field}: {risk.PreservationStatus}. {risk.Message}");
        }

        foreach (var diagnostic in summary.Diagnostics)
        {
            report.AppendLine($"• Review: {diagnostic}");
        }

        return report.ToString().TrimEnd();
    }
}
