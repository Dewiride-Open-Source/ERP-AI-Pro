using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dewiride.Erp.Modules.Identity.Users.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CreateUsers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "identity_users");

            migrationBuilder.CreateTable(
                name: "Users",
                schema: "identity_users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EntraObjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DisplayName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    WorkEmail = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: false),
                    EmployeeCode = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    PhoneNumber = table.Column<string>(type: "varchar(16)", unicode: false, maxLength: 16, nullable: true),
                    Designation = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    DateOfJoining = table.Column<DateOnly>(type: "date", nullable: true),
                    Status = table.Column<byte>(type: "tinyint", nullable: false),
                    LastSignedInAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DeletedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Users_DisplayName",
                schema: "identity_users",
                table: "Users",
                column: "DisplayName");

            migrationBuilder.CreateIndex(
                name: "IX_Users_EmployeeCode",
                schema: "identity_users",
                table: "Users",
                column: "EmployeeCode",
                unique: true,
                filter: "[EmployeeCode] IS NOT NULL AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Users_EntraObjectId",
                schema: "identity_users",
                table: "Users",
                column: "EntraObjectId",
                unique: true,
                filter: "[EntraObjectId] IS NOT NULL AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Users_WorkEmail",
                schema: "identity_users",
                table: "Users",
                column: "WorkEmail",
                unique: true,
                filter: "[EntraObjectId] IS NULL AND [IsDeleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Users",
                schema: "identity_users");
        }
    }
}
