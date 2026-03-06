using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TestPlanManager.Migrations
{
    /// <inheritdoc />
    public partial class AddTestExecutedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ExecutedAt",
                table: "Tests",
                type: "TEXT",
                nullable: true);

            migrationBuilder.Sql(@"
                UPDATE Tests
                SET ExecutedAt = (
                    SELECT tc.TestDate
                    FROM TestCategories tc
                    WHERE tc.TestCategoryId = Tests.TestCategoryId
                )
                WHERE ExecutionStatus <> 'NotRun' AND ExecutedAt IS NULL;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExecutedAt",
                table: "Tests");
        }
    }
}
