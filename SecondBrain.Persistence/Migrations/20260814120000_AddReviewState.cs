using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace SecondBrain.Persistence.Migrations;

[DbContext(typeof(SecondBrainDbContext))]
[Migration("20260814120000_AddReviewState")]
public sealed class AddReviewState : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "ReviewStates",
            columns: table => new
            {
                TargetKind = table.Column<int>(type: "INTEGER", nullable: false),
                TargetId = table.Column<Guid>(type: "TEXT", nullable: false),
                LastReviewedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                DeferredUntil = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ReviewStates", row => new
                {
                    row.TargetKind,
                    row.TargetId,
                });
                table.CheckConstraint(
                    "CK_ReviewStates_TargetKind",
                    "TargetKind BETWEEN 0 AND 3");
                table.CheckConstraint(
                    "CK_ReviewStates_TargetId",
                    "TargetId <> '00000000-0000-0000-0000-000000000000'");
            });
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropTable(name: "ReviewStates");
}
