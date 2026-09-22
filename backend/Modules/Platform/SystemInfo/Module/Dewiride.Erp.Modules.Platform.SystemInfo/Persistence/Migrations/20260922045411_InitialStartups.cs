using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dewiride.Erp.Modules.Platform.SystemInfo.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialStartups : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "platform_system_info");

            migrationBuilder.CreateTable(
                name: "Startups",
                schema: "platform_system_info",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApplicationName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    EnvironmentName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ConfigurationLabel = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    MachineName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RecordedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Build_Framework = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Build_Version = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Startups", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Startups_StartedAt",
                schema: "platform_system_info",
                table: "Startups",
                column: "StartedAt",
                descending: new bool[0]);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Startups",
                schema: "platform_system_info");
        }
    }
}
