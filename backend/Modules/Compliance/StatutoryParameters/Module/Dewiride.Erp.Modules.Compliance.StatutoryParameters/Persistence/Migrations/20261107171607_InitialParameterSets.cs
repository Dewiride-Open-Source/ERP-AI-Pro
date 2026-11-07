using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialParameterSets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "compliance_statutory_parameters");

            migrationBuilder.CreateTable(
                name: "ParameterSets",
                schema: "compliance_statutory_parameters",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Key = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Basis = table.Column<byte>(type: "tinyint", nullable: false),
                    AsOf = table.Column<byte>(type: "tinyint", nullable: false),
                    Columns = table.Column<string>(type: "json", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParameterSets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ParameterRows",
                schema: "compliance_statutory_parameters",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowKey = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    From = table.Column<DateOnly>(type: "date", nullable: false),
                    Until = table.Column<DateOnly>(type: "date", nullable: true),
                    Cells = table.Column<string>(type: "json", nullable: false),
                    Source = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParameterRows", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ParameterRows_ParameterSets_SetId",
                        column: x => x.SetId,
                        principalSchema: "compliance_statutory_parameters",
                        principalTable: "ParameterSets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ParameterVersions",
                schema: "compliance_statutory_parameters",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    From = table.Column<DateOnly>(type: "date", nullable: false),
                    Instruments = table.Column<string>(type: "json", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParameterVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ParameterVersions_ParameterSets_SetId",
                        column: x => x.SetId,
                        principalSchema: "compliance_statutory_parameters",
                        principalTable: "ParameterSets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ParameterRows_SetId_From_Until",
                schema: "compliance_statutory_parameters",
                table: "ParameterRows",
                columns: new[] { "SetId", "From", "Until" });

            migrationBuilder.CreateIndex(
                name: "IX_ParameterRows_SetId_RowKey_From",
                schema: "compliance_statutory_parameters",
                table: "ParameterRows",
                columns: new[] { "SetId", "RowKey", "From" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ParameterSets_Key",
                schema: "compliance_statutory_parameters",
                table: "ParameterSets",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ParameterVersions_SetId_From",
                schema: "compliance_statutory_parameters",
                table: "ParameterVersions",
                columns: new[] { "SetId", "From" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ParameterRows",
                schema: "compliance_statutory_parameters");

            migrationBuilder.DropTable(
                name: "ParameterVersions",
                schema: "compliance_statutory_parameters");

            migrationBuilder.DropTable(
                name: "ParameterSets",
                schema: "compliance_statutory_parameters");
        }
    }
}
