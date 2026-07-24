using Microsoft.EntityFrameworkCore;

namespace SecondBrain.Persistence;

public sealed class SecondBrainDbContext(DbContextOptions<SecondBrainDbContext> options)
    : DbContext(options)
{
    internal DbSet<ProjectRow> Projects => Set<ProjectRow>();

    internal DbSet<AreaRow> Areas => Set<AreaRow>();

    internal DbSet<ResourceTopicRow> ResourceTopics => Set<ResourceTopicRow>();

    internal DbSet<TagRow> Tags => Set<TagRow>();

    internal DbSet<BrainItemRow> BrainItems => Set<BrainItemRow>();

    internal DbSet<BrainItemTextTagRow> BrainItemTextTags => Set<BrainItemTextTagRow>();

    internal DbSet<BrainItemTagRow> BrainItemTags => Set<BrainItemTagRow>();

    internal DbSet<BrainItemLinkRow> BrainItemLinks => Set<BrainItemLinkRow>();

    internal DbSet<BrainItemRelationRow> BrainItemRelations => Set<BrainItemRelationRow>();

    internal DbSet<JournalRow> Journals => Set<JournalRow>();

    internal DbSet<JournalEntryRow> JournalEntries => Set<JournalEntryRow>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ConfigureProjects(modelBuilder);
        ConfigureAreas(modelBuilder);
        ConfigureResourceTopics(modelBuilder);
        ConfigureTags(modelBuilder);
        ConfigureBrainItems(modelBuilder);
        ConfigureBrainItemCollections(modelBuilder);
        ConfigureJournals(modelBuilder);
    }

    private static void ConfigureProjects(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<ProjectRow>();
        entity.ToTable("Projects", table =>
        {
            table.HasCheckConstraint("CK_Projects_Id", "Id <> '00000000-0000-0000-0000-000000000000'");
            table.HasCheckConstraint("CK_Projects_Name", "length(trim(Name)) > 0");
            table.HasCheckConstraint("CK_Projects_Outcome", "length(trim(Outcome)) > 0");
            table.HasCheckConstraint("CK_Projects_Status", "Status BETWEEN 0 AND 3");
            table.HasCheckConstraint("CK_Projects_Priority", "Priority BETWEEN 0 AND 2");
        });
        entity.HasKey(row => row.Id);
        entity.Property(row => row.Id).ValueGeneratedNever();
        entity.Property(row => row.Name).HasMaxLength(200).IsRequired();
        entity.Property(row => row.Outcome).HasMaxLength(2000).IsRequired();
    }

    private static void ConfigureAreas(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<AreaRow>();
        entity.ToTable("Areas", table =>
        {
            table.HasCheckConstraint("CK_Areas_Id", "Id <> '00000000-0000-0000-0000-000000000000'");
            table.HasCheckConstraint("CK_Areas_Name", "length(trim(Name)) > 0");
        });
        entity.HasKey(row => row.Id);
        entity.Property(row => row.Id).ValueGeneratedNever();
        entity.Property(row => row.Name).HasMaxLength(200).IsRequired();
    }

    private static void ConfigureResourceTopics(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<ResourceTopicRow>();
        entity.ToTable("ResourceTopics", table =>
        {
            table.HasCheckConstraint("CK_ResourceTopics_Id", "Id <> '00000000-0000-0000-0000-000000000000'");
            table.HasCheckConstraint("CK_ResourceTopics_Name", "length(trim(Name)) > 0");
        });
        entity.HasKey(row => row.Id);
        entity.Property(row => row.Id).ValueGeneratedNever();
        entity.Property(row => row.Name).HasMaxLength(200).IsRequired();
    }

    private static void ConfigureTags(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<TagRow>();
        entity.ToTable("Tags", table =>
        {
            table.HasCheckConstraint("CK_Tags_Id", "Id <> '00000000-0000-0000-0000-000000000000'");
            table.HasCheckConstraint("CK_Tags_Name", "length(trim(Name)) > 0");
            table.HasCheckConstraint("CK_Tags_NotSelfParent", "ParentId IS NULL OR ParentId <> Id");
        });
        entity.HasKey(row => row.Id);
        entity.Property(row => row.Id).ValueGeneratedNever();
        entity.Property(row => row.Name).HasMaxLength(200).IsRequired();
        entity.HasOne<TagRow>()
            .WithMany()
            .HasForeignKey(row => row.ParentId)
            .OnDelete(DeleteBehavior.Restrict);
    }

    private static void ConfigureBrainItems(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<BrainItemRow>();
        entity.ToTable("BrainItems", table =>
        {
            table.HasCheckConstraint("CK_BrainItems_Id", "Id <> '00000000-0000-0000-0000-000000000000'");
            table.HasCheckConstraint("CK_BrainItems_Kind", "Kind BETWEEN 1 AND 5");
            table.HasCheckConstraint("CK_BrainItems_Title", "length(trim(Title)) > 0");
            table.HasCheckConstraint("CK_BrainItems_Content", "length(trim(Content)) > 0");
            table.HasCheckConstraint(
                "CK_BrainItems_Placement",
                "(PlacementKind = 0 AND ProjectId IS NOT NULL AND AreaId IS NULL AND ResourceTopicId IS NULL) OR " +
                "(PlacementKind = 1 AND ProjectId IS NULL AND AreaId IS NOT NULL AND ResourceTopicId IS NULL) OR " +
                "(PlacementKind = 2 AND ProjectId IS NULL AND AreaId IS NULL AND ResourceTopicId IS NOT NULL)");
            table.HasCheckConstraint("CK_BrainItems_Timestamps", "UpdatedAt >= CreatedAt");
            table.HasCheckConstraint(
                "CK_BrainItems_Lifecycle",
                "(Kind = 1 AND NoteKind = 1 AND IdeaMaturity IS NULL AND EntryDate IS NULL AND CaptureSourceType IS NULL AND SourceUri IS NULL AND SourceCitation IS NULL AND ReminderAt IS NULL AND CaptureProcessingState IS NULL AND ResourceArtifactKind IS NULL AND ResourceFreshness IS NULL AND ReviewDate IS NULL) OR " +
                "(Kind = 2 AND NoteKind IS NULL AND IdeaMaturity BETWEEN 1 AND 3 AND EntryDate IS NULL AND CaptureSourceType IS NULL AND SourceUri IS NULL AND SourceCitation IS NULL AND ReminderAt IS NULL AND CaptureProcessingState IS NULL AND ResourceArtifactKind IS NULL AND ResourceFreshness IS NULL AND ReviewDate IS NULL) OR " +
                "(Kind = 3 AND NoteKind IS NULL AND IdeaMaturity IS NULL AND EntryDate IS NOT NULL AND CaptureSourceType IS NULL AND SourceUri IS NULL AND SourceCitation IS NULL AND ReminderAt IS NULL AND CaptureProcessingState IS NULL AND ResourceArtifactKind IS NULL AND ResourceFreshness IS NULL AND ReviewDate IS NULL) OR " +
                "(Kind = 4 AND NoteKind IS NULL AND IdeaMaturity IS NULL AND EntryDate IS NULL AND CaptureSourceType BETWEEN 1 AND 6 AND length(trim(SourceUri)) > 0 AND length(trim(SourceCitation)) > 0 AND (ReminderAt IS NULL OR ReminderAt >= CreatedAt) AND CaptureProcessingState BETWEEN 1 AND 4 AND ResourceArtifactKind IS NULL AND ResourceFreshness IS NULL AND ReviewDate IS NULL) OR " +
                "(Kind = 5 AND NoteKind IS NULL AND IdeaMaturity IS NULL AND EntryDate IS NULL AND CaptureSourceType IS NULL AND SourceUri IS NULL AND SourceCitation IS NULL AND ReminderAt IS NULL AND CaptureProcessingState IS NULL AND ResourceArtifactKind BETWEEN 1 AND 4 AND ResourceFreshness BETWEEN 1 AND 3)");
        });
        entity.HasKey(row => row.Id);
        entity.Property(row => row.Id).ValueGeneratedNever();
        entity.Property(row => row.Title).HasMaxLength(500).IsRequired();
        entity.Property(row => row.Content).IsRequired();
        entity.Property(row => row.SourceUri).HasMaxLength(2048);
        entity.Property(row => row.SourceCitation).HasMaxLength(2000);
        entity.HasOne<ProjectRow>()
            .WithMany()
            .HasForeignKey(row => row.ProjectId)
            .OnDelete(DeleteBehavior.Restrict);
        entity.HasOne<AreaRow>()
            .WithMany()
            .HasForeignKey(row => row.AreaId)
            .OnDelete(DeleteBehavior.Restrict);
        entity.HasOne<ResourceTopicRow>()
            .WithMany()
            .HasForeignKey(row => row.ResourceTopicId)
            .OnDelete(DeleteBehavior.Restrict);
    }

    private static void ConfigureBrainItemCollections(ModelBuilder modelBuilder)
    {
        var textTags = modelBuilder.Entity<BrainItemTextTagRow>();
        textTags.ToTable("BrainItemTextTags", table =>
            table.HasCheckConstraint("CK_BrainItemTextTags_Value", "length(trim(Value)) > 0"));
        textTags.HasKey(row => new { row.BrainItemId, row.Value });
        textTags.Property(row => row.Value).HasMaxLength(200);
        textTags.HasOne<BrainItemRow>()
            .WithMany()
            .HasForeignKey(row => row.BrainItemId)
            .OnDelete(DeleteBehavior.Cascade);

        var tags = modelBuilder.Entity<BrainItemTagRow>();
        tags.ToTable("BrainItemTags");
        tags.HasKey(row => new { row.BrainItemId, row.TagId });
        tags.HasOne<BrainItemRow>()
            .WithMany()
            .HasForeignKey(row => row.BrainItemId)
            .OnDelete(DeleteBehavior.Cascade);
        tags.HasOne<TagRow>()
            .WithMany()
            .HasForeignKey(row => row.TagId)
            .OnDelete(DeleteBehavior.Restrict);

        var links = modelBuilder.Entity<BrainItemLinkRow>();
        links.ToTable("BrainItemLinks", table =>
        {
            table.HasCheckConstraint("CK_BrainItemLinks_Id", "Id <> '00000000-0000-0000-0000-000000000000'");
            table.HasCheckConstraint("CK_BrainItemLinks_Type", "Type BETWEEN 0 AND 4");
            table.HasCheckConstraint("CK_BrainItemLinks_TargetState", "TargetState BETWEEN 0 AND 2");
            table.HasCheckConstraint("CK_BrainItemLinks_Target", "TargetModuleId <> '00000000-0000-0000-0000-000000000000' AND length(trim(TargetModuleName)) > 0 AND length(trim(TargetExternalId)) > 0 AND length(trim(TargetItemType)) > 0");
        });
        links.HasKey(row => row.Id);
        links.Property(row => row.Id).ValueGeneratedNever();
        links.HasIndex(row => new { row.BrainItemId, row.Id }).IsUnique();
        links.Property(row => row.TargetModuleName).HasMaxLength(200).IsRequired();
        links.Property(row => row.TargetExternalId).HasMaxLength(500).IsRequired();
        links.Property(row => row.TargetItemType).HasMaxLength(200).IsRequired();
        links.HasOne<BrainItemRow>()
            .WithMany()
            .HasForeignKey(row => row.BrainItemId)
            .OnDelete(DeleteBehavior.Cascade);

        var relations = modelBuilder.Entity<BrainItemRelationRow>();
        relations.ToTable("BrainItemRelations", table =>
        {
            table.HasCheckConstraint("CK_BrainItemRelations_Kind", "Kind BETWEEN 0 AND 2");
            table.HasCheckConstraint("CK_BrainItemRelations_NotSelf", "SourceId <> TargetId");
        });
        relations.HasKey(row => new { row.SourceId, row.TargetId, row.Kind });
        relations.HasOne<BrainItemRow>()
            .WithMany()
            .HasForeignKey(row => row.SourceId)
            .OnDelete(DeleteBehavior.Cascade);
        relations.HasOne<BrainItemRow>()
            .WithMany()
            .HasForeignKey(row => row.TargetId)
            .OnDelete(DeleteBehavior.Restrict);
    }

    private static void ConfigureJournals(ModelBuilder modelBuilder)
    {
        var journals = modelBuilder.Entity<JournalRow>();
        journals.ToTable("Journals", table =>
        {
            table.HasCheckConstraint("CK_Journals_Id", "Id <> '00000000-0000-0000-0000-000000000000'");
            table.HasCheckConstraint("CK_Journals_Title", "length(trim(Title)) > 0");
        });
        journals.HasKey(row => row.Id);
        journals.Property(row => row.Id).ValueGeneratedNever();
        journals.Property(row => row.Title).HasMaxLength(500).IsRequired();

        var entries = modelBuilder.Entity<JournalEntryRow>();
        entries.ToTable("JournalEntries");
        entries.HasKey(row => new { row.JournalId, row.BrainItemId });
        entries.HasIndex(row => row.BrainItemId).IsUnique();
        entries.HasOne<JournalRow>()
            .WithMany()
            .HasForeignKey(row => row.JournalId)
            .OnDelete(DeleteBehavior.Cascade);
        entries.HasOne<BrainItemRow>()
            .WithMany()
            .HasForeignKey(row => row.BrainItemId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
