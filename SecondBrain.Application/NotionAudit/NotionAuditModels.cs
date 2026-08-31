namespace SecondBrain.Application.NotionAudit;

public enum NotionAuditStatus
{
    CoreSupported,
    CoreSupportedWithReview,
    ModuleOwnedExcluded,
    Unsupported,
    DuplicateView,
    Ambiguous,
}

public sealed record NotionExportRelation(
    string FieldName,
    IReadOnlyList<string> TargetNotionIds);

public sealed record NotionExportRowMetadata(
    string? NotionId,
    string? Classification,
    bool IsTemplate,
    bool IsArchived,
    IReadOnlyList<NotionExportRelation> Relations)
{
    public string? ContentFingerprint { get; init; }
}

public sealed record NotionExportTableMetadata(
    string SourceName,
    string? DatabaseName,
    string? DatabaseNotionId,
    bool IsDuplicateAllView,
    IReadOnlyList<string> Fields,
    IReadOnlyList<NotionExportRowMetadata> Rows);

public sealed record NotionExportMetadata(
    IReadOnlyList<NotionExportTableMetadata> Tables,
    IReadOnlyList<string> Diagnostics);

public interface INotionExportReader
{
    Task<NotionExportMetadata> ReadAsync(
        string sourcePath,
        CancellationToken cancellationToken = default);
}

public interface INotionExportSourcePicker
{
    Task<string?> PickFolderAsync(CancellationToken cancellationToken = default);

    Task<string?> PickArchiveAsync(CancellationToken cancellationToken = default);
}

public sealed record NotionAuditSection(
    string Name,
    NotionAuditStatus Status,
    int RowCount,
    string Outcome,
    IReadOnlyList<string> Fields)
{
    public IReadOnlyList<NotionAuditField> FieldMappings { get; init; } = [];
}

public sealed record NotionAuditField(
    string Name,
    NotionAuditStatus Status,
    string Outcome);

public sealed record NotionRelationshipRisk(
    string Source,
    string Field,
    string PreservationStatus,
    int RelationshipCount,
    string Message);

public sealed record NotionAuditSummary(
    string SpecificationVersion,
    int WillImport,
    int NeedsReview,
    int ModuleOwnedOrExcluded,
    int Unsupported,
    int DuplicateRowsIgnored,
    IReadOnlyList<NotionAuditSection> Sections,
    IReadOnlyList<NotionRelationshipRisk> RelationshipRisks,
    IReadOnlyList<string> Diagnostics);

public sealed record NotionAuditReport(
    NotionAuditSummary Summary,
    string HumanReadableReport,
    string MachineReadableSummary);
