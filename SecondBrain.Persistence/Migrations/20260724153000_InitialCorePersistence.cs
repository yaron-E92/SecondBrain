using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace SecondBrain.Persistence.Migrations;

[DbContext(typeof(SecondBrainDbContext))]
[Migration("20260724153000_InitialCorePersistence")]
public sealed class InitialCorePersistence : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            CREATE TABLE "Projects" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_Projects" PRIMARY KEY,
                "Name" TEXT NOT NULL,
                "Outcome" TEXT NOT NULL,
                "Status" INTEGER NOT NULL,
                "Priority" INTEGER NOT NULL,
                "TargetDate" TEXT NULL,
                "IsArchived" INTEGER NOT NULL,
                CONSTRAINT "CK_Projects_Id" CHECK ("Id" <> '00000000-0000-0000-0000-000000000000'),
                CONSTRAINT "CK_Projects_Name" CHECK (length(trim("Name")) > 0),
                CONSTRAINT "CK_Projects_Outcome" CHECK (length(trim("Outcome")) > 0),
                CONSTRAINT "CK_Projects_Status" CHECK ("Status" BETWEEN 0 AND 3),
                CONSTRAINT "CK_Projects_Priority" CHECK ("Priority" BETWEEN 0 AND 2)
            );

            CREATE TABLE "Areas" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_Areas" PRIMARY KEY,
                "Name" TEXT NOT NULL,
                "IsArchived" INTEGER NOT NULL,
                CONSTRAINT "CK_Areas_Id" CHECK ("Id" <> '00000000-0000-0000-0000-000000000000'),
                CONSTRAINT "CK_Areas_Name" CHECK (length(trim("Name")) > 0)
            );

            CREATE TABLE "ResourceTopics" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_ResourceTopics" PRIMARY KEY,
                "Name" TEXT NOT NULL,
                "IsArchived" INTEGER NOT NULL,
                CONSTRAINT "CK_ResourceTopics_Id" CHECK ("Id" <> '00000000-0000-0000-0000-000000000000'),
                CONSTRAINT "CK_ResourceTopics_Name" CHECK (length(trim("Name")) > 0)
            );

            CREATE TABLE "Tags" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_Tags" PRIMARY KEY,
                "Name" TEXT NOT NULL,
                "ParentId" TEXT NULL,
                CONSTRAINT "FK_Tags_Tags_ParentId" FOREIGN KEY ("ParentId") REFERENCES "Tags" ("Id") ON DELETE RESTRICT,
                CONSTRAINT "CK_Tags_Id" CHECK ("Id" <> '00000000-0000-0000-0000-000000000000'),
                CONSTRAINT "CK_Tags_Name" CHECK (length(trim("Name")) > 0),
                CONSTRAINT "CK_Tags_NotSelfParent" CHECK ("ParentId" IS NULL OR "ParentId" <> "Id")
            );
            CREATE INDEX "IX_Tags_ParentId" ON "Tags" ("ParentId");

            CREATE TABLE "BrainItems" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_BrainItems" PRIMARY KEY,
                "Kind" INTEGER NOT NULL,
                "Title" TEXT NOT NULL,
                "Content" TEXT NOT NULL,
                "PlacementKind" INTEGER NOT NULL,
                "ProjectId" TEXT NULL,
                "AreaId" TEXT NULL,
                "ResourceTopicId" TEXT NULL,
                "CreatedAt" TEXT NOT NULL,
                "UpdatedAt" TEXT NOT NULL,
                "NoteKind" INTEGER NULL,
                "IdeaMaturity" INTEGER NULL,
                "EntryDate" TEXT NULL,
                "CaptureSourceType" INTEGER NULL,
                "SourceUri" TEXT NULL,
                "SourceCitation" TEXT NULL,
                "ReminderAt" TEXT NULL,
                "CaptureProcessingState" INTEGER NULL,
                "ResourceArtifactKind" INTEGER NULL,
                "ResourceFreshness" INTEGER NULL,
                "ReviewDate" TEXT NULL,
                "IsArchived" INTEGER NOT NULL,
                "IsFavorite" INTEGER NOT NULL,
                CONSTRAINT "FK_BrainItems_Projects_ProjectId" FOREIGN KEY ("ProjectId") REFERENCES "Projects" ("Id") ON DELETE RESTRICT,
                CONSTRAINT "FK_BrainItems_Areas_AreaId" FOREIGN KEY ("AreaId") REFERENCES "Areas" ("Id") ON DELETE RESTRICT,
                CONSTRAINT "FK_BrainItems_ResourceTopics_ResourceTopicId" FOREIGN KEY ("ResourceTopicId") REFERENCES "ResourceTopics" ("Id") ON DELETE RESTRICT,
                CONSTRAINT "CK_BrainItems_Id" CHECK ("Id" <> '00000000-0000-0000-0000-000000000000'),
                CONSTRAINT "CK_BrainItems_Kind" CHECK ("Kind" BETWEEN 1 AND 5),
                CONSTRAINT "CK_BrainItems_Title" CHECK (length(trim("Title")) > 0),
                CONSTRAINT "CK_BrainItems_Content" CHECK (length(trim("Content")) > 0),
                CONSTRAINT "CK_BrainItems_Placement" CHECK (
                    ("PlacementKind" = 0 AND "ProjectId" IS NOT NULL AND "AreaId" IS NULL AND "ResourceTopicId" IS NULL) OR
                    ("PlacementKind" = 1 AND "ProjectId" IS NULL AND "AreaId" IS NOT NULL AND "ResourceTopicId" IS NULL) OR
                    ("PlacementKind" = 2 AND "ProjectId" IS NULL AND "AreaId" IS NULL AND "ResourceTopicId" IS NOT NULL)),
                CONSTRAINT "CK_BrainItems_Timestamps" CHECK ("UpdatedAt" >= "CreatedAt"),
                CONSTRAINT "CK_BrainItems_Lifecycle" CHECK (COALESCE(
                    ("Kind" = 1 AND "NoteKind" = 1 AND "IdeaMaturity" IS NULL AND "EntryDate" IS NULL AND "CaptureSourceType" IS NULL AND "SourceUri" IS NULL AND "SourceCitation" IS NULL AND "ReminderAt" IS NULL AND "CaptureProcessingState" IS NULL AND "ResourceArtifactKind" IS NULL AND "ResourceFreshness" IS NULL AND "ReviewDate" IS NULL) OR
                    ("Kind" = 2 AND "NoteKind" IS NULL AND "IdeaMaturity" BETWEEN 1 AND 3 AND "EntryDate" IS NULL AND "CaptureSourceType" IS NULL AND "SourceUri" IS NULL AND "SourceCitation" IS NULL AND "ReminderAt" IS NULL AND "CaptureProcessingState" IS NULL AND "ResourceArtifactKind" IS NULL AND "ResourceFreshness" IS NULL AND "ReviewDate" IS NULL) OR
                    ("Kind" = 3 AND "NoteKind" IS NULL AND "IdeaMaturity" IS NULL AND "EntryDate" IS NOT NULL AND "CaptureSourceType" IS NULL AND "SourceUri" IS NULL AND "SourceCitation" IS NULL AND "ReminderAt" IS NULL AND "CaptureProcessingState" IS NULL AND "ResourceArtifactKind" IS NULL AND "ResourceFreshness" IS NULL AND "ReviewDate" IS NULL) OR
                    ("Kind" = 4 AND "NoteKind" IS NULL AND "IdeaMaturity" IS NULL AND "EntryDate" IS NULL AND "CaptureSourceType" BETWEEN 1 AND 6 AND length(trim("SourceUri")) > 0 AND length(trim("SourceCitation")) > 0 AND ("ReminderAt" IS NULL OR "ReminderAt" >= "CreatedAt") AND "CaptureProcessingState" BETWEEN 1 AND 4 AND "ResourceArtifactKind" IS NULL AND "ResourceFreshness" IS NULL AND "ReviewDate" IS NULL) OR
                    ("Kind" = 5 AND "NoteKind" IS NULL AND "IdeaMaturity" IS NULL AND "EntryDate" IS NULL AND "CaptureSourceType" IS NULL AND "SourceUri" IS NULL AND "SourceCitation" IS NULL AND "ReminderAt" IS NULL AND "CaptureProcessingState" IS NULL AND "ResourceArtifactKind" BETWEEN 1 AND 4 AND "ResourceFreshness" BETWEEN 1 AND 3),
                    0))
            );
            CREATE INDEX "IX_BrainItems_ProjectId" ON "BrainItems" ("ProjectId");
            CREATE INDEX "IX_BrainItems_AreaId" ON "BrainItems" ("AreaId");
            CREATE INDEX "IX_BrainItems_ResourceTopicId" ON "BrainItems" ("ResourceTopicId");

            CREATE TABLE "BrainItemTextTags" (
                "BrainItemId" TEXT NOT NULL,
                "Value" TEXT NOT NULL,
                CONSTRAINT "PK_BrainItemTextTags" PRIMARY KEY ("BrainItemId", "Value"),
                CONSTRAINT "FK_BrainItemTextTags_BrainItems_BrainItemId" FOREIGN KEY ("BrainItemId") REFERENCES "BrainItems" ("Id") ON DELETE CASCADE,
                CONSTRAINT "CK_BrainItemTextTags_Value" CHECK (length(trim("Value")) > 0)
            );

            CREATE TABLE "BrainItemTags" (
                "BrainItemId" TEXT NOT NULL,
                "TagId" TEXT NOT NULL,
                CONSTRAINT "PK_BrainItemTags" PRIMARY KEY ("BrainItemId", "TagId"),
                CONSTRAINT "FK_BrainItemTags_BrainItems_BrainItemId" FOREIGN KEY ("BrainItemId") REFERENCES "BrainItems" ("Id") ON DELETE CASCADE,
                CONSTRAINT "FK_BrainItemTags_Tags_TagId" FOREIGN KEY ("TagId") REFERENCES "Tags" ("Id") ON DELETE RESTRICT
            );
            CREATE INDEX "IX_BrainItemTags_TagId" ON "BrainItemTags" ("TagId");

            CREATE TABLE "BrainItemLinks" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_BrainItemLinks" PRIMARY KEY,
                "BrainItemId" TEXT NOT NULL,
                "Type" INTEGER NOT NULL,
                "TargetModuleId" TEXT NOT NULL,
                "TargetModuleName" TEXT NOT NULL,
                "TargetExternalId" TEXT NOT NULL,
                "TargetItemType" TEXT NOT NULL,
                "TargetState" INTEGER NOT NULL,
                CONSTRAINT "FK_BrainItemLinks_BrainItems_BrainItemId" FOREIGN KEY ("BrainItemId") REFERENCES "BrainItems" ("Id") ON DELETE CASCADE,
                CONSTRAINT "CK_BrainItemLinks_Id" CHECK ("Id" <> '00000000-0000-0000-0000-000000000000'),
                CONSTRAINT "CK_BrainItemLinks_Type" CHECK ("Type" BETWEEN 0 AND 4),
                CONSTRAINT "CK_BrainItemLinks_TargetState" CHECK ("TargetState" BETWEEN 0 AND 2),
                CONSTRAINT "CK_BrainItemLinks_Target" CHECK ("TargetModuleId" <> '00000000-0000-0000-0000-000000000000' AND length(trim("TargetModuleName")) > 0 AND length(trim("TargetExternalId")) > 0 AND length(trim("TargetItemType")) > 0)
            );
            CREATE UNIQUE INDEX "IX_BrainItemLinks_BrainItemId_Id" ON "BrainItemLinks" ("BrainItemId", "Id");

            CREATE TABLE "BrainItemRelations" (
                "SourceId" TEXT NOT NULL,
                "TargetId" TEXT NOT NULL,
                "Kind" INTEGER NOT NULL,
                CONSTRAINT "PK_BrainItemRelations" PRIMARY KEY ("SourceId", "TargetId", "Kind"),
                CONSTRAINT "FK_BrainItemRelations_BrainItems_SourceId" FOREIGN KEY ("SourceId") REFERENCES "BrainItems" ("Id") ON DELETE CASCADE,
                CONSTRAINT "FK_BrainItemRelations_BrainItems_TargetId" FOREIGN KEY ("TargetId") REFERENCES "BrainItems" ("Id") ON DELETE RESTRICT,
                CONSTRAINT "CK_BrainItemRelations_Kind" CHECK ("Kind" BETWEEN 0 AND 2),
                CONSTRAINT "CK_BrainItemRelations_NotSelf" CHECK ("SourceId" <> "TargetId")
            );
            CREATE INDEX "IX_BrainItemRelations_TargetId" ON "BrainItemRelations" ("TargetId");

            CREATE TABLE "Journals" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_Journals" PRIMARY KEY,
                "Title" TEXT NOT NULL,
                CONSTRAINT "CK_Journals_Id" CHECK ("Id" <> '00000000-0000-0000-0000-000000000000'),
                CONSTRAINT "CK_Journals_Title" CHECK (length(trim("Title")) > 0)
            );

            CREATE TABLE "JournalEntries" (
                "JournalId" TEXT NOT NULL,
                "BrainItemId" TEXT NOT NULL,
                CONSTRAINT "PK_JournalEntries" PRIMARY KEY ("JournalId", "BrainItemId"),
                CONSTRAINT "FK_JournalEntries_Journals_JournalId" FOREIGN KEY ("JournalId") REFERENCES "Journals" ("Id") ON DELETE CASCADE,
                CONSTRAINT "FK_JournalEntries_BrainItems_BrainItemId" FOREIGN KEY ("BrainItemId") REFERENCES "BrainItems" ("Id") ON DELETE RESTRICT
            );
            CREATE UNIQUE INDEX "IX_JournalEntries_BrainItemId" ON "JournalEntries" ("BrainItemId");

            CREATE TRIGGER "TRG_Tags_PreventCycle_Update"
            BEFORE UPDATE OF "ParentId" ON "Tags"
            WHEN NEW."ParentId" IS NOT NULL
            BEGIN
                WITH RECURSIVE "Ancestors"("Id", "ParentId") AS (
                    SELECT "Id", "ParentId" FROM "Tags" WHERE "Id" = NEW."ParentId"
                    UNION ALL
                    SELECT "Tags"."Id", "Tags"."ParentId"
                    FROM "Tags" JOIN "Ancestors" ON "Tags"."Id" = "Ancestors"."ParentId"
                )
                SELECT RAISE(ABORT, 'Tag hierarchy cannot contain a cycle')
                WHERE EXISTS (SELECT 1 FROM "Ancestors" WHERE "Id" = NEW."Id");
            END;

            CREATE TRIGGER "TRG_JournalEntries_RequireJournalKind"
            BEFORE INSERT ON "JournalEntries"
            BEGIN
                SELECT RAISE(ABORT, 'Journals can contain only Journal Entry items')
                WHERE (SELECT "Kind" FROM "BrainItems" WHERE "Id" = NEW."BrainItemId") <> 3;
            END;

            CREATE TRIGGER "TRG_BrainItems_PreserveJournalKind"
            BEFORE UPDATE OF "Kind" ON "BrainItems"
            WHEN OLD."Kind" = 3 AND NEW."Kind" <> 3
            BEGIN
                SELECT RAISE(ABORT, 'Journal Entry kind is required while assigned to a journal')
                WHERE EXISTS (SELECT 1 FROM "JournalEntries" WHERE "BrainItemId" = OLD."Id");
            END;

            CREATE TRIGGER "TRG_BrainItemRelations_RequireLifecycle"
            BEFORE INSERT ON "BrainItemRelations"
            BEGIN
                SELECT RAISE(ABORT, 'Derived links require a Knowledge Capture source')
                WHERE NEW."Kind" = 1 AND (SELECT "Kind" FROM "BrainItems" WHERE "Id" = NEW."SourceId") <> 4;
                SELECT RAISE(ABORT, 'Provenance links require a Resource Artifact source')
                WHERE NEW."Kind" = 2 AND (SELECT "Kind" FROM "BrainItems" WHERE "Id" = NEW."SourceId") <> 5;
            END;

            CREATE TRIGGER "TRG_BrainItemRelations_PreventProvenanceCycle"
            BEFORE INSERT ON "BrainItemRelations"
            WHEN NEW."Kind" = 2
            BEGIN
                WITH RECURSIVE "Sources"("Id") AS (
                    SELECT "TargetId" FROM "BrainItemRelations"
                    WHERE "SourceId" = NEW."TargetId" AND "Kind" = 2
                    UNION
                    SELECT "Relations"."TargetId"
                    FROM "BrainItemRelations" AS "Relations"
                    JOIN "Sources" ON "Relations"."SourceId" = "Sources"."Id"
                    WHERE "Relations"."Kind" = 2
                )
                SELECT RAISE(ABORT, 'Resource provenance cannot contain a cycle')
                WHERE EXISTS (SELECT 1 FROM "Sources" WHERE "Id" = NEW."SourceId");
            END;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DROP TRIGGER IF EXISTS "TRG_BrainItemRelations_PreventProvenanceCycle";
            DROP TRIGGER IF EXISTS "TRG_BrainItemRelations_RequireLifecycle";
            DROP TRIGGER IF EXISTS "TRG_BrainItems_PreserveJournalKind";
            DROP TRIGGER IF EXISTS "TRG_JournalEntries_RequireJournalKind";
            DROP TRIGGER IF EXISTS "TRG_Tags_PreventCycle_Update";
            DROP TABLE "JournalEntries";
            DROP TABLE "Journals";
            DROP TABLE "BrainItemRelations";
            DROP TABLE "BrainItemLinks";
            DROP TABLE "BrainItemTags";
            DROP TABLE "BrainItemTextTags";
            DROP TABLE "BrainItems";
            DROP TABLE "Tags";
            DROP TABLE "ResourceTopics";
            DROP TABLE "Areas";
            DROP TABLE "Projects";
            """);
    }
}
