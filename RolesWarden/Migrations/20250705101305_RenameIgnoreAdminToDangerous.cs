using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RolesWarden.Migrations
{
    /// <inheritdoc />
    public partial class RenameIgnoreAdminToDangerous : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "ignore_admin",
                table: "guild_config",
                newName: "ignore_dangerous");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "ignore_dangerous",
                table: "guild_config",
                newName: "ignore_admin");
        }
    }
}
