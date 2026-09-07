namespace SecondBrain.Persistence;

internal sealed class NotionImportProvenanceRow
{
    public string DatabaseNotionId { get; set; } = "";
    public string PageNotionId { get; set; } = "";
    public string SpecificationVersion { get; set; } = "";
    public string ContentFingerprint { get; set; } = "";
    public string TargetKind { get; set; } = "";
    public Guid TargetId { get; set; }
}
