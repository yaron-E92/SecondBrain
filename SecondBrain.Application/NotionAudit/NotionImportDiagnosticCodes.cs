namespace SecondBrain.Application.NotionAudit;

public static class NotionImportDiagnosticCodes
{
    public const string DuplicateAllView = "duplicate-all-view";
    public const string ModuleOwnedShuffleTask = "module-owned-shuffletask";
    public const string ModuleOwnedPhoodab = "module-owned-phoodab";
    public const string AuthoritativeDatabaseIdentityRequired = "authoritative-database-identity-required";
    public const string Template = "template";
    public const string PageIdentityRequired = "page-identity-required";
    public const string ConflictingPageId = "conflicting-page-id";
    public const string UnresolvedLink = "unresolved-link";
    public const string RelationNotRepresentable = "relation-not-representable";
    public const string UnsupportedRelationType = "unsupported-relation-type";
    public const string NotImportedByV1Mapping = "not-imported-by-v1-mapping";
    public const string UnsupportedDatabase = "unsupported-database";
    public const string AmbiguousResourceClassificationRequired = "ambiguous-resource-classification-required";
    public const string UserExcluded = "user-excluded";
    public const string InvalidTargetData = "invalid-target-data";
    public const string ChangedSourceConflict = "changed-source-conflict";
    public const string ImportBlockedByConflicts = "import-blocked-by-conflicts";
    public const string TransactionRolledBack = "transaction-rolled-back";
}
