using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TestPlanManager.Migrations
{
    /// <inheritdoc />
    public partial class SetAdminUsersActive : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Set all admin users to active
            migrationBuilder.Sql(@"
                UPDATE AspNetUsers 
                SET IsActive = 1 
                WHERE Id IN (
                    SELECT UserId FROM AspNetUserRoles 
                    WHERE RoleId = (SELECT Id FROM AspNetRoles WHERE Name = 'Administrator')
                )
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Revert: set admin users back to inactive (if needed)
            migrationBuilder.Sql(@"
                UPDATE AspNetUsers 
                SET IsActive = 0 
                WHERE Id IN (
                    SELECT UserId FROM AspNetUserRoles 
                    WHERE RoleId = (SELECT Id FROM AspNetRoles WHERE Name = 'Administrator')
                )
            ");
        }
    }
}
