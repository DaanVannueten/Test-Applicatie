using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TestPlanManager.Migrations
{
    /// <inheritdoc />
    public partial class AddTemplateDrivenCycles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsTemplateDerived",
                table: "Tests",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "TemplateTestCaseId",
                table: "Tests",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsTemplateCategory",
                table: "TestCategories",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "TestTemplateId",
                table: "TestCategories",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TestTemplateId",
                table: "Sprints",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "TestTemplates",
                columns: table => new
                {
                    TestTemplateId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    ProductName = table.Column<string>(type: "TEXT", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TestTemplates", x => x.TestTemplateId);
                });

            migrationBuilder.CreateTable(
                name: "TemplateTestCases",
                columns: table => new
                {
                    TemplateTestCaseId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TestTemplateId = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: false),
                    MediaUrl = table.Column<string>(type: "TEXT", nullable: true),
                    ScopeStatus = table.Column<string>(type: "TEXT", nullable: false),
                    DefaultExecutionStatus = table.Column<string>(type: "TEXT", nullable: false),
                    Production = table.Column<string>(type: "TEXT", nullable: false),
                    Sequence = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TemplateTestCases", x => x.TemplateTestCaseId);
                    table.ForeignKey(
                        name: "FK_TemplateTestCases_TestTemplates_TestTemplateId",
                        column: x => x.TestTemplateId,
                        principalTable: "TestTemplates",
                        principalColumn: "TestTemplateId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TestCategories_TestTemplateId",
                table: "TestCategories",
                column: "TestTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_Sprints_TestTemplateId",
                table: "Sprints",
                column: "TestTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_TemplateTestCases_TestTemplateId_Sequence",
                table: "TemplateTestCases",
                columns: new[] { "TestTemplateId", "Sequence" });

            migrationBuilder.AddForeignKey(
                name: "FK_Sprints_TestTemplates_TestTemplateId",
                table: "Sprints",
                column: "TestTemplateId",
                principalTable: "TestTemplates",
                principalColumn: "TestTemplateId",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_TestCategories_TestTemplates_TestTemplateId",
                table: "TestCategories",
                column: "TestTemplateId",
                principalTable: "TestTemplates",
                principalColumn: "TestTemplateId",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Sprints_TestTemplates_TestTemplateId",
                table: "Sprints");

            migrationBuilder.DropForeignKey(
                name: "FK_TestCategories_TestTemplates_TestTemplateId",
                table: "TestCategories");

            migrationBuilder.DropTable(
                name: "TemplateTestCases");

            migrationBuilder.DropTable(
                name: "TestTemplates");

            migrationBuilder.DropIndex(
                name: "IX_TestCategories_TestTemplateId",
                table: "TestCategories");

            migrationBuilder.DropIndex(
                name: "IX_Sprints_TestTemplateId",
                table: "Sprints");

            migrationBuilder.DropColumn(
                name: "IsTemplateDerived",
                table: "Tests");

            migrationBuilder.DropColumn(
                name: "TemplateTestCaseId",
                table: "Tests");

            migrationBuilder.DropColumn(
                name: "IsTemplateCategory",
                table: "TestCategories");

            migrationBuilder.DropColumn(
                name: "TestTemplateId",
                table: "TestCategories");

            migrationBuilder.DropColumn(
                name: "TestTemplateId",
                table: "Sprints");
        }
    }
}
