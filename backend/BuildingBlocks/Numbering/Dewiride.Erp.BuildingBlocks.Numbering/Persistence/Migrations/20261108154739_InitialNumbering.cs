using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dewiride.Erp.BuildingBlocks.Numbering.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialNumbering : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "platform_numbering");

            migrationBuilder.CreateTable(
                name: "NumberSeries",
                schema: "platform_numbering",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentType = table.Column<string>(type: "varchar(128)", unicode: false, maxLength: 128, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    Format_Digits = table.Column<int>(type: "int", nullable: false),
                    Format_Prefix = table.Column<string>(type: "varchar(32)", unicode: false, maxLength: 32, nullable: false),
                    Format_Separator = table.Column<string>(type: "varchar(1)", unicode: false, maxLength: 1, nullable: false),
                    Format_Year = table.Column<byte>(type: "tinyint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NumberSeries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NumberSeriesYears",
                schema: "platform_numbering",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SeriesId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Year = table.Column<string>(type: "char(7)", unicode: false, fixedLength: true, maxLength: 7, nullable: false),
                    MaxLength = table.Column<int>(type: "int", nullable: false),
                    FirstNumber = table.Column<long>(type: "bigint", nullable: false),
                    NextNumber = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    Format_Digits = table.Column<int>(type: "int", nullable: false),
                    Format_Prefix = table.Column<string>(type: "varchar(32)", unicode: false, maxLength: 32, nullable: false),
                    Format_Separator = table.Column<string>(type: "varchar(1)", unicode: false, maxLength: 1, nullable: false),
                    Format_Year = table.Column<byte>(type: "tinyint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NumberSeriesYears", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NumberSeriesYears_NumberSeries_SeriesId",
                        column: x => x.SeriesId,
                        principalSchema: "platform_numbering",
                        principalTable: "NumberSeries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_NumberSeries_DocumentType",
                schema: "platform_numbering",
                table: "NumberSeries",
                column: "DocumentType",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NumberSeriesYears_SeriesId_Year",
                schema: "platform_numbering",
                table: "NumberSeriesYears",
                columns: new[] { "SeriesId", "Year" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NumberSeriesYears",
                schema: "platform_numbering");

            migrationBuilder.DropTable(
                name: "NumberSeries",
                schema: "platform_numbering");
        }
    }
}
