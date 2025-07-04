using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RolesWarden.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "guild_config",
                columns: table => new
                {
                    guild_id = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    default_action = table.Column<int>(type: "integer", nullable: false),
                    ignore_admin = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_guild_config", x => x.guild_id);
                });

            migrationBuilder.CreateTable(
                name: "role_config",
                columns: table => new
                {
                    role_id = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    guild_id = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    action = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_role_config", x => x.role_id);
                });

            migrationBuilder.CreateTable(
                name: "saved_roles",
                columns: table => new
                {
                    guild_id = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    user_id = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    roles_ids = table.Column<decimal[]>(type: "numeric(20,0)[]", nullable: true),
                    timestamp = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_saved_roles", x => new { x.guild_id, x.user_id });
                });

            migrationBuilder.CreateIndex(
                name: "IX_role_config_guild_id",
                table: "role_config",
                column: "guild_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "guild_config");

            migrationBuilder.DropTable(
                name: "role_config");

            migrationBuilder.DropTable(
                name: "saved_roles");
        }
    }
}
