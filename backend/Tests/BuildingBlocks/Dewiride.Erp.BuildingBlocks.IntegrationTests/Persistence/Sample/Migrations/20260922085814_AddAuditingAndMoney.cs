using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dewiride.Erp.BuildingBlocks.IntegrationTests.Persistence.Sample.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditingAndMoney : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Price",
                schema: "test_sample",
                table: "Samples",
                newName: "Price_Amount");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CreatedAt",
                schema: "test_sample",
                table: "Samples",
                type: "datetimeoffset",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedBy",
                schema: "test_sample",
                table: "Samples",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeletedAt",
                schema: "test_sample",
                table: "Samples",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DeletedBy",
                schema: "test_sample",
                table: "Samples",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                schema: "test_sample",
                table: "Samples",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ModifiedAt",
                schema: "test_sample",
                table: "Samples",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModifiedBy",
                schema: "test_sample",
                table: "Samples",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Price_Currency",
                schema: "test_sample",
                table: "Samples",
                type: "char(3)",
                unicode: false,
                fixedLength: true,
                maxLength: 3,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                schema: "test_sample",
                table: "Samples",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CreatedAt",
                schema: "test_sample",
                table: "Samples");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                schema: "test_sample",
                table: "Samples");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "test_sample",
                table: "Samples");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                schema: "test_sample",
                table: "Samples");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                schema: "test_sample",
                table: "Samples");

            migrationBuilder.DropColumn(
                name: "ModifiedAt",
                schema: "test_sample",
                table: "Samples");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                schema: "test_sample",
                table: "Samples");

            migrationBuilder.DropColumn(
                name: "Price_Currency",
                schema: "test_sample",
                table: "Samples");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                schema: "test_sample",
                table: "Samples");

            migrationBuilder.RenameColumn(
                name: "Price_Amount",
                schema: "test_sample",
                table: "Samples",
                newName: "Price");
        }
    }
}
