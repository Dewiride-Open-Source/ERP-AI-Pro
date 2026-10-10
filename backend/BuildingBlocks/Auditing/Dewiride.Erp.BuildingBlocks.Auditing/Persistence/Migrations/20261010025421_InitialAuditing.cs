using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dewiride.Erp.BuildingBlocks.Auditing.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialAuditing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "audit");

            migrationBuilder.CreateTable(
                name: "SecurityEvents",
                schema: "audit",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Kind = table.Column<byte>(type: "tinyint", nullable: false),
                    Detail = table.Column<string>(type: "varchar(128)", unicode: false, maxLength: 128, nullable: true),
                    ActorObjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ClientApplicationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ClientAddress = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: true),
                    CorrelationId = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SecurityEvents", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SecurityEvents_ActorObjectId_OccurredAt",
                schema: "audit",
                table: "SecurityEvents",
                columns: new[] { "ActorObjectId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SecurityEvents_OccurredAt",
                schema: "audit",
                table: "SecurityEvents",
                column: "OccurredAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SecurityEvents",
                schema: "audit");
        }
    }
}
