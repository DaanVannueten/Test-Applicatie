using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TestPlanManager.Migrations
{
    /// <inheritdoc />
    public partial class AddSourceTemplateSprintReference : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SourceTemplateSprintId",
                table: "Sprints",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SourceTemplateSprintId",
                table: "Sprints");
        }
    }
}
