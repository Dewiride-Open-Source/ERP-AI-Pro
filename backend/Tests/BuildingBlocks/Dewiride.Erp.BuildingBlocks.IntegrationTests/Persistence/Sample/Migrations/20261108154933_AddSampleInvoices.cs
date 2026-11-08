using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dewiride.Erp.BuildingBlocks.IntegrationTests.Persistence.Sample.Migrations
{
    /// <inheritdoc />
    public partial class AddSampleInvoices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Invoices",
                schema: "test_sample",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentType = table.Column<string>(type: "varchar(128)", unicode: false, maxLength: 128, nullable: false),
                    Number = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                    Year = table.Column<string>(type: "char(7)", unicode: false, fixedLength: true, maxLength: 7, nullable: false),
                    Sequence = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Invoices", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_DocumentType_Year_Number",
                schema: "test_sample",
                table: "Invoices",
                columns: new[] { "DocumentType", "Year", "Number" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Invoices",
                schema: "test_sample");
        }
    }
}
