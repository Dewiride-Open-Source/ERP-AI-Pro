using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dewiride.Erp.BuildingBlocks.Attachments.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialAttachments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "files");

            migrationBuilder.CreateTable(
                name: "StoredContents",
                schema: "files",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Sha256 = table.Column<byte[]>(type: "binary(32)", fixedLength: true, maxLength: 32, nullable: false),
                    Length = table.Column<long>(type: "bigint", nullable: false),
                    KeyId = table.Column<string>(type: "varchar(32)", unicode: false, maxLength: 32, nullable: false),
                    StoredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StoredContents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UploadReservations",
                schema: "files",
                columns: table => new
                {
                    ContentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReservedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ReservedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UploadReservations", x => x.ContentId);
                });

            migrationBuilder.CreateTable(
                name: "Attachments",
                schema: "files",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    ContentType = table.Column<string>(type: "varchar(127)", unicode: false, maxLength: 127, nullable: false),
                    ScanStatus = table.Column<byte>(type: "tinyint", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DeletedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Attachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Attachments_StoredContents_ContentId",
                        column: x => x.ContentId,
                        principalSchema: "files",
                        principalTable: "StoredContents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DownloadLinks",
                schema: "files",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AttachmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TokenHash = table.Column<byte[]>(type: "binary(32)", fixedLength: true, maxLength: 32, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DownloadLinks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DownloadLinks_Attachments_AttachmentId",
                        column: x => x.AttachmentId,
                        principalSchema: "files",
                        principalTable: "Attachments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DownloadRedemptions",
                schema: "files",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LinkId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AttachmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RedeemedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DownloadRedemptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DownloadRedemptions_Attachments_AttachmentId",
                        column: x => x.AttachmentId,
                        principalSchema: "files",
                        principalTable: "Attachments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DownloadRedemptions_DownloadLinks_LinkId",
                        column: x => x.LinkId,
                        principalSchema: "files",
                        principalTable: "DownloadLinks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Attachments_ContentId",
                schema: "files",
                table: "Attachments",
                column: "ContentId");

            migrationBuilder.CreateIndex(
                name: "IX_Attachments_CreatedAt",
                schema: "files",
                table: "Attachments",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_DownloadLinks_AttachmentId",
                schema: "files",
                table: "DownloadLinks",
                column: "AttachmentId");

            migrationBuilder.CreateIndex(
                name: "IX_DownloadLinks_TokenHash",
                schema: "files",
                table: "DownloadLinks",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DownloadRedemptions_AttachmentId_RedeemedAt",
                schema: "files",
                table: "DownloadRedemptions",
                columns: new[] { "AttachmentId", "RedeemedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_DownloadRedemptions_LinkId",
                schema: "files",
                table: "DownloadRedemptions",
                column: "LinkId");

            migrationBuilder.CreateIndex(
                name: "IX_StoredContents_KeyId",
                schema: "files",
                table: "StoredContents",
                column: "KeyId");

            migrationBuilder.CreateIndex(
                name: "IX_StoredContents_Sha256_Length",
                schema: "files",
                table: "StoredContents",
                columns: new[] { "Sha256", "Length" });

            migrationBuilder.CreateIndex(
                name: "IX_UploadReservations_ReservedAt",
                schema: "files",
                table: "UploadReservations",
                column: "ReservedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DownloadRedemptions",
                schema: "files");

            migrationBuilder.DropTable(
                name: "UploadReservations",
                schema: "files");

            migrationBuilder.DropTable(
                name: "DownloadLinks",
                schema: "files");

            migrationBuilder.DropTable(
                name: "Attachments",
                schema: "files");

            migrationBuilder.DropTable(
                name: "StoredContents",
                schema: "files");
        }
    }
}
