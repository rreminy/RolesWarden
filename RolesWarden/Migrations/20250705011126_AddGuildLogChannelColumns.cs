using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RolesWarden.Migrations
{
    /// <inheritdoc />
    public partial class AddGuildLogChannelColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "log_channel_id",
                table: "guild_config",
                type: "numeric(20,0)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "log_types",
                table: "guild_config",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "log_channel_id",
                table: "guild_config");

            migrationBuilder.DropColumn(
                name: "log_types",
                table: "guild_config");
        }
    }
}
