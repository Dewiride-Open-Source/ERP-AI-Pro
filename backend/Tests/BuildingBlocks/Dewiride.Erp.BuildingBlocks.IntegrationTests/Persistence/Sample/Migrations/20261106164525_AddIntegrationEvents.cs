using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dewiride.Erp.BuildingBlocks.IntegrationTests.Persistence.Sample.Migrations
{
    /// <inheritdoc />
    public partial class AddIntegrationEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OutboxMessages",
                schema: "test_sample",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EventName = table.Column<string>(type: "varchar(128)", unicode: false, maxLength: 128, nullable: false),
                    Payload = table.Column<string>(type: "json", nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CorrelationId = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: true),
                    Depth = table.Column<int>(type: "int", nullable: false),
                    RelayedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutboxMessages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProcessedMessages",
                schema: "test_sample",
                columns: table => new
                {
                    EventId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Subscriber = table.Column<string>(type: "varchar(128)", unicode: false, maxLength: 128, nullable: false),
                    ProcessedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcessedMessages", x => new { x.EventId, x.Subscriber });
                });

            migrationBuilder.CreateTable(
                name: "Receipts",
                schema: "test_sample",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ShipmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EventId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Parcels = table.Column<int>(type: "int", nullable: false),
                    RecordedBy = table.Column<string>(type: "varchar(128)", unicode: false, maxLength: 128, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Receipts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Shipments",
                schema: "test_sample",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Parcels = table.Column<int>(type: "int", nullable: false),
                    DispatchedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Shipments", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_OccurredAt_Id",
                schema: "test_sample",
                table: "OutboxMessages",
                columns: new[] { "OccurredAt", "Id" },
                filter: "[RelayedAt] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_RelayedAt",
                schema: "test_sample",
                table: "OutboxMessages",
                column: "RelayedAt",
                filter: "[RelayedAt] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ProcessedMessages_ProcessedAt",
                schema: "test_sample",
                table: "ProcessedMessages",
                column: "ProcessedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Receipts_ShipmentId",
                schema: "test_sample",
                table: "Receipts",
                column: "ShipmentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OutboxMessages",
                schema: "test_sample");

            migrationBuilder.DropTable(
                name: "ProcessedMessages",
                schema: "test_sample");

            migrationBuilder.DropTable(
                name: "Receipts",
                schema: "test_sample");

            migrationBuilder.DropTable(
                name: "Shipments",
                schema: "test_sample");
        }
    }
}
