using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

namespace SecondBrain.Persistence.Migrations;

[DbContext(typeof(SecondBrainDbContext))]
public sealed class SecondBrainDbContextModelSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder modelBuilder)
    {
        modelBuilder.HasAnnotation("ProductVersion", "10.0.0");

        ConfigureContexts(modelBuilder);
        ConfigureTags(modelBuilder);
        ConfigureBrainItems(modelBuilder);
        ConfigureCollections(modelBuilder);
        ConfigureJournals(modelBuilder);
        ConfigureReviewStates(modelBuilder);
    }

    private static void ConfigureContexts(ModelBuilder modelBuilder)
    {
        var projects = modelBuilder.Entity<ProjectRow>();
        projects.ToTable("Projects", table =>
        {
            table.HasCheckConstraint("CK_Projects_Id", "Id <> '00000000-0000-0000-0000-000000000000'");
            table.HasCheckConstraint("CK_Projects_Name", "length(trim(Name)) > 0");
            table.HasCheckConstraint("CK_Projects_Outcome", "length(trim(Outcome)) > 0");
            table.HasCheckConstraint("CK_Projects_Status", "Status BETWEEN 0 AND 3");
            table.HasCheckConstraint("CK_Projects_Priority", "Priority BETWEEN 0 AND 2");
        });
        projects.HasKey(row => row.Id);
        projects.Property(row => row.Id).ValueGeneratedNever();
        projects.Property(row => row.Name).HasMaxLength(200).IsRequired();
        projects.Property(row => row.Outcome).HasMaxLength(2000).IsRequired();

        var areas = modelBuilder.Entity<AreaRow>();
        areas.ToTable("Areas", table =>
        {
            table.HasCheckConstraint("CK_Areas_Id", "Id <> '00000000-0000-0000-0000-000000000000'");
            table.HasCheckConstraint("CK_Areas_Name", "length(trim(Name)) > 0");
        });
        areas.HasKey(row => row.Id);
        areas.Property(row => row.Id).ValueGeneratedNever();
        areas.Property(row => row.Name).HasMaxLength(200).IsRequired();

        var topics = modelBuilder.Entity<ResourceTopicRow>();
        topics.ToTable("ResourceTopics", table =>
        {
            table.HasCheckConstraint("CK_ResourceTopics_Id", "Id <> '00000000-0000-0000-0000-000000000000'");
            table.HasCheckConstraint("CK_ResourceTopics_Name", "length(trim(Name)) > 0");
        });
        topics.HasKey(row => row.Id);
        topics.Property(row => row.Id).ValueGeneratedNever();
        topics.Property(row => row.Name).HasMaxLength(200).IsRequired();
    }

    private static void ConfigureTags(ModelBuilder modelBuilder)
    {
        var tags = modelBuilder.Entity<TagRow>();
        tags.ToTable("Tags", table =>
        {
            table.HasCheckConstraint("CK_Tags_Id", "Id <> '00000000-0000-0000-0000-000000000000'");
            table.HasCheckConstraint("CK_Tags_Name", "length(trim(Name)) > 0");
            table.HasCheckConstraint("CK_Tags_NotSelfParent", "ParentId IS NULL OR ParentId <> Id");
        });
        tags.HasKey(row => row.Id);
        tags.Property(row => row.Id).ValueGeneratedNever();
        tags.Property(row => row.Name).HasMaxLength(200).IsRequired();
        tags.HasOne<TagRow>().WithMany().HasForeignKey(row => row.ParentId)
            .OnDelete(DeleteBehavior.Restrict);
    }

    private static void ConfigureBrainItems(ModelBuilder modelBuilder)
    {
        var items = modelBuilder.Entity<BrainItemRow>();
        items.ToTable("BrainItems", table =>
        {
            table.HasCheckConstraint("CK_BrainItems_Id", "Id <> '00000000-0000-0000-0000-000000000000'");
            table.HasCheckConstraint("CK_BrainItems_Kind", "Kind BETWEEN 1 AND 5");
            table.HasCheckConstraint("CK_BrainItems_Title", "length(trim(Title)) > 0");
            table.HasCheckConstraint("CK_BrainItems_Content", "length(trim(Content)) > 0");
            table.HasCheckConstraint("CK_BrainItems_Placement", "(PlacementKind = 0 AND ProjectId IS NOT NULL AND AreaId IS NULL AND ResourceTopicId IS NULL) OR (PlacementKind = 1 AND ProjectId IS NULL AND AreaId IS NOT NULL AND ResourceTopicId IS NULL) OR (PlacementKind = 2 AND ProjectId IS NULL AND AreaId IS NULL AND ResourceTopicId IS NOT NULL)");
            table.HasCheckConstraint("CK_BrainItems_Timestamps", "UpdatedAt >= CreatedAt");
            table.HasCheckConstraint("CK_BrainItems_Lifecycle", "COALESCE((Kind = 1 AND NoteKind = 1 AND IdeaMaturity IS NULL AND EntryDate IS NULL AND CaptureSourceType IS NULL AND SourceUri IS NULL AND SourceCitation IS NULL AND ReminderAt IS NULL AND CaptureProcessingState IS NULL AND ResourceArtifactKind IS NULL AND ResourceFreshness IS NULL AND ReviewDate IS NULL) OR (Kind = 2 AND NoteKind IS NULL AND IdeaMaturity BETWEEN 1 AND 3 AND EntryDate IS NULL AND CaptureSourceType IS NULL AND SourceUri IS NULL AND SourceCitation IS NULL AND ReminderAt IS NULL AND CaptureProcessingState IS NULL AND ResourceArtifactKind IS NULL AND ResourceFreshness IS NULL AND ReviewDate IS NULL) OR (Kind = 3 AND NoteKind IS NULL AND IdeaMaturity IS NULL AND EntryDate IS NOT NULL AND CaptureSourceType IS NULL AND SourceUri IS NULL AND SourceCitation IS NULL AND ReminderAt IS NULL AND CaptureProcessingState IS NULL AND ResourceArtifactKind IS NULL AND ResourceFreshness IS NULL AND ReviewDate IS NULL) OR (Kind = 4 AND NoteKind IS NULL AND IdeaMaturity IS NULL AND EntryDate IS NULL AND CaptureSourceType BETWEEN 1 AND 6 AND length(trim(SourceUri)) > 0 AND length(trim(SourceCitation)) > 0 AND (ReminderAt IS NULL OR ReminderAt >= CreatedAt) AND CaptureProcessingState BETWEEN 1 AND 4 AND ResourceArtifactKind IS NULL AND ResourceFreshness IS NULL AND ReviewDate IS NULL) OR (Kind = 5 AND NoteKind IS NULL AND IdeaMaturity IS NULL AND EntryDate IS NULL AND CaptureSourceType IS NULL AND SourceUri IS NULL AND SourceCitation IS NULL AND ReminderAt IS NULL AND CaptureProcessingState IS NULL AND ResourceArtifactKind BETWEEN 1 AND 4 AND ResourceFreshness BETWEEN 1 AND 3), 0)");
        });
        items.HasKey(row => row.Id);
        items.Property(row => row.Id).ValueGeneratedNever();
        items.Property(row => row.Title).HasMaxLength(500).IsRequired();
        items.Property(row => row.Content).IsRequired();
        items.Property(row => row.SourceUri).HasMaxLength(2048);
        items.Property(row => row.SourceCitation).HasMaxLength(2000);
        items.HasOne<ProjectRow>().WithMany().HasForeignKey(row => row.ProjectId)
            .OnDelete(DeleteBehavior.Restrict);
        items.HasOne<AreaRow>().WithMany().HasForeignKey(row => row.AreaId)
            .OnDelete(DeleteBehavior.Restrict);
        items.HasOne<ResourceTopicRow>().WithMany()
            .HasForeignKey(row => row.ResourceTopicId).OnDelete(DeleteBehavior.Restrict);
    }

    private static void ConfigureCollections(ModelBuilder modelBuilder)
    {
        var textTags = modelBuilder.Entity<BrainItemTextTagRow>();
        textTags.ToTable("BrainItemTextTags", table =>
            table.HasCheckConstraint("CK_BrainItemTextTags_Value", "length(trim(Value)) > 0"));
        textTags.HasKey(row => new { row.BrainItemId, row.Value });
        textTags.Property(row => row.Value).HasMaxLength(200);
        textTags.HasOne<BrainItemRow>().WithMany().HasForeignKey(row => row.BrainItemId)
            .OnDelete(DeleteBehavior.Cascade);

        var itemTags = modelBuilder.Entity<BrainItemTagRow>();
        itemTags.ToTable("BrainItemTags");
        itemTags.HasKey(row => new { row.BrainItemId, row.TagId });
        itemTags.HasOne<BrainItemRow>().WithMany().HasForeignKey(row => row.BrainItemId)
            .OnDelete(DeleteBehavior.Cascade);
        itemTags.HasOne<TagRow>().WithMany().HasForeignKey(row => row.TagId)
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
        links.HasOne<BrainItemRow>().WithMany().HasForeignKey(row => row.BrainItemId)
            .OnDelete(DeleteBehavior.Cascade);

        var relations = modelBuilder.Entity<BrainItemRelationRow>();
        relations.ToTable("BrainItemRelations", table =>
        {
            table.HasCheckConstraint("CK_BrainItemRelations_Kind", "Kind BETWEEN 0 AND 2");
            table.HasCheckConstraint("CK_BrainItemRelations_NotSelf", "SourceId <> TargetId");
        });
        relations.HasKey(row => new { row.SourceId, row.TargetId, row.Kind });
        relations.HasOne<BrainItemRow>().WithMany().HasForeignKey(row => row.SourceId)
            .OnDelete(DeleteBehavior.Cascade);
        relations.HasOne<BrainItemRow>().WithMany().HasForeignKey(row => row.TargetId)
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
        journals.Property(row => row.IsArchived).IsRequired();

        var entries = modelBuilder.Entity<JournalEntryRow>();
        entries.ToTable("JournalEntries");
        entries.HasKey(row => new { row.JournalId, row.BrainItemId });
        entries.HasIndex(row => row.BrainItemId).IsUnique();
        entries.HasOne<JournalRow>().WithMany().HasForeignKey(row => row.JournalId)
            .OnDelete(DeleteBehavior.Cascade);
        entries.HasOne<BrainItemRow>().WithMany().HasForeignKey(row => row.BrainItemId)
            .OnDelete(DeleteBehavior.Restrict);
    }

    private static void ConfigureReviewStates(ModelBuilder modelBuilder)
    {
        var reviews = modelBuilder.Entity<ReviewStateRow>();
        reviews.ToTable("ReviewStates", table =>
        {
            table.HasCheckConstraint(
                "CK_ReviewStates_TargetKind",
                "TargetKind BETWEEN 0 AND 3");
            table.HasCheckConstraint(
                "CK_ReviewStates_TargetId",
                "TargetId <> '00000000-0000-0000-0000-000000000000'");
        });
        reviews.HasKey(row => new { row.TargetKind, row.TargetId });
    }
}
