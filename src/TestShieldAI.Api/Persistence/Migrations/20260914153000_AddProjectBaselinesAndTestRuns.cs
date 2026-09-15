using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TestShieldAI.Api.Persistence.Migrations;

/// <inheritdoc />
public partial class AddProjectBaselinesAndTestRuns : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "ProjectBaselines",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                ProjectId = table.Column<Guid>(type: "TEXT", nullable: false),
                EstablishedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                SpecificationId = table.Column<Guid>(type: "TEXT", nullable: true),
                SnapshotJson = table.Column<string>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ProjectBaselines", x => x.Id);
                table.ForeignKey(
                    name: "FK_ProjectBaselines_Projects_ProjectId",
                    column: x => x.ProjectId,
                    principalTable: "Projects",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "ProjectTestRuns",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                ProjectId = table.Column<Guid>(type: "TEXT", nullable: false),
                CompletedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                Decision = table.Column<string>(type: "TEXT", nullable: false),
                PassedCount = table.Column<int>(type: "INTEGER", nullable: false),
                FailedCount = table.Column<int>(type: "INTEGER", nullable: false),
                ErrorCount = table.Column<int>(type: "INTEGER", nullable: false),
                FindingCount = table.Column<int>(type: "INTEGER", nullable: false),
                ResultsJson = table.Column<string>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ProjectTestRuns", x => x.Id);
                table.ForeignKey(
                    name: "FK_ProjectTestRuns_Projects_ProjectId",
                    column: x => x.ProjectId,
                    principalTable: "Projects",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_ProjectBaselines_ProjectId",
            table: "ProjectBaselines",
            column: "ProjectId",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_ProjectTestRuns_ProjectId",
            table: "ProjectTestRuns",
            column: "ProjectId",
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "ProjectBaselines");
        migrationBuilder.DropTable(name: "ProjectTestRuns");
    }
}
