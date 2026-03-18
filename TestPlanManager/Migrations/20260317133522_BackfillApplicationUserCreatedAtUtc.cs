using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TestPlanManager.Migrations
{
    /// <inheritdoc />
    public partial class BackfillApplicationUserCreatedAtUtc : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                WITH ordered AS (
                    SELECT Id, ROW_NUMBER() OVER (ORDER BY rowid) AS rn
                    FROM AspNetUsers
                ),
                totals AS (
                    SELECT COUNT(*) AS total_count FROM AspNetUsers
                )
                UPDATE AspNetUsers
                SET CreatedAtUtc = datetime(
                    'now',
                    '-' || (
                        SELECT (totals.total_count - ordered.rn)
                        FROM ordered, totals
                        WHERE ordered.Id = AspNetUsers.Id
                    ) || ' seconds'
                )
                WHERE CreatedAtUtc = '0001-01-01 00:00:00';
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                UPDATE AspNetUsers
                SET CreatedAtUtc = '0001-01-01 00:00:00';
            ");
        }
    }
}
