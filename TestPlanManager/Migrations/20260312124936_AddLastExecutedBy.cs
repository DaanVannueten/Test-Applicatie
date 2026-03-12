using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TestPlanManager.Migrations
{
    /// <inheritdoc />
    public partial class AddLastExecutedBy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LastExecutedBy",
                table: "Tests",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastExecutedBy",
                table: "Tests");
        }
    }
}
