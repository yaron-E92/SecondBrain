using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace SecondBrain.Persistence.Migrations;

[DbContext(typeof(SecondBrainDbContext))]
[Migration("20260804090000_AddJournalArchiveState")]
public sealed class AddJournalArchiveState : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.AddColumn<bool>(
            name: "IsArchived",
            table: "Journals",
            type: "INTEGER",
            nullable: false,
            defaultValue: false);

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropColumn(
            name: "IsArchived",
            table: "Journals");
}
