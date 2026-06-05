using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using TestPlanManager.Data;

#nullable disable

namespace TestPlanManager.Migrations
{
    [DbContext(typeof(TestPlanContext))]
    [Migration("20260601120000_AddDependenciesToTestsAndTemplates")]
    public partial class AddDependenciesToTestsAndTemplates : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Dependencies",
                table: "Tests",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Dependencies",
                table: "TemplateTestCases",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Dependencies",
                table: "Tests");

            migrationBuilder.DropColumn(
                name: "Dependencies",
                table: "TemplateTestCases");
        }
    }
}