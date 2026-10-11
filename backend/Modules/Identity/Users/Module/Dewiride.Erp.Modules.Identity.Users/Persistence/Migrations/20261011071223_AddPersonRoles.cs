using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dewiride.Erp.Modules.Identity.Users.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPersonRoles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Roles",
                schema: "identity_users",
                table: "Users",
                type: "json",
                nullable: false,
                defaultValue: "[]");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Roles",
                schema: "identity_users",
                table: "Users");
        }
    }
}
