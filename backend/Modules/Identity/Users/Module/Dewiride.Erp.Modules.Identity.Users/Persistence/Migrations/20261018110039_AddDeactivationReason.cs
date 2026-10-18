using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dewiride.Erp.Modules.Identity.Users.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDeactivationReason : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte>(
                name: "DeactivatedBecause",
                schema: "identity_users",
                table: "Users",
                type: "tinyint",
                nullable: true);

            migrationBuilder.Sql(
                @"EXEC(N'UPDATE [identity_users].[Users] SET [DeactivatedBecause] = CAST(1 AS tinyint) WHERE [Status] = CAST(2 AS tinyint)')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeactivatedBecause",
                schema: "identity_users",
                table: "Users");
        }
    }
}
