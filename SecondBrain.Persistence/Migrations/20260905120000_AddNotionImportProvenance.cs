using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SecondBrain.Persistence.Migrations;

[DbContext(typeof(SecondBrainDbContext))]
[Migration("20260905120000_AddNotionImportProvenance")]
public sealed class AddNotionImportProvenance : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "NotionImportProvenance",
            columns: table => new
            {
                DatabaseNotionId = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                PageNotionId = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                SpecificationVersion = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                ContentFingerprint = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                TargetKind = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                TargetId = table.Column<Guid>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_NotionImportProvenance", x => new { x.DatabaseNotionId, x.PageNotionId });
                table.CheckConstraint("CK_NotionImportProvenance_DatabaseId", "length(DatabaseNotionId) = 32");
                table.CheckConstraint("CK_NotionImportProvenance_PageId", "length(PageNotionId) = 32");
                table.CheckConstraint("CK_NotionImportProvenance_TargetId", "TargetId <> '00000000-0000-0000-0000-000000000000'");
            });
        migrationBuilder.CreateIndex(
            name: "IX_NotionImportProvenance_TargetId",
            table: "NotionImportProvenance",
            column: "TargetId",
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropTable(name: "NotionImportProvenance");
}
