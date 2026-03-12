using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TestPlanManager.Migrations
{
    /// <inheritdoc />
    public partial class AddSprintTemplateFlag : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsTemplate",
                table: "Sprints",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsTemplate",
                table: "Sprints");
        }
    }
}
