using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dewiride.Erp.BuildingBlocks.Idempotency.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialIdempotencyKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "platform_idempotency");

            migrationBuilder.CreateTable(
                name: "IdempotencyKeys",
                schema: "platform_idempotency",
                columns: table => new
                {
                    ActorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Key = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Fingerprint = table.Column<byte[]>(type: "binary(32)", fixedLength: true, maxLength: 32, nullable: false),
                    Status = table.Column<byte>(type: "tinyint", nullable: false),
                    StatusCode = table.Column<int>(type: "int", nullable: true),
                    ContentType = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Location = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    Body = table.Column<byte[]>(type: "varbinary(max)", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IdempotencyKeys", x => new { x.ActorId, x.Key });
                });

            migrationBuilder.CreateIndex(
                name: "IX_IdempotencyKeys_ExpiresAt",
                schema: "platform_idempotency",
                table: "IdempotencyKeys",
                column: "ExpiresAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IdempotencyKeys",
                schema: "platform_idempotency");
        }
    }
}
