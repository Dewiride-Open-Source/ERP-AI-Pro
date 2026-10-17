using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dewiride.Erp.BuildingBlocks.Auditing.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddChangeRecords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ChangeRecords",
                schema: "audit",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Operation = table.Column<byte>(type: "tinyint", nullable: false),
                    EntityType = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                    EntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubjectType = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                    SubjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientAddress = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: true),
                    CorrelationId = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: true),
                    Changes = table.Column<string>(type: "json", nullable: true),
                    ValueCount = table.Column<short>(type: "smallint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChangeRecords", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChangeRecords_ActorId_OccurredAt",
                schema: "audit",
                table: "ChangeRecords",
                columns: new[] { "ActorId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ChangeRecords_CorrelationId",
                schema: "audit",
                table: "ChangeRecords",
                column: "CorrelationId",
                filter: "[CorrelationId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ChangeRecords_EntityId_OccurredAt",
                schema: "audit",
                table: "ChangeRecords",
                columns: new[] { "EntityId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ChangeRecords_EntityType_OccurredAt",
                schema: "audit",
                table: "ChangeRecords",
                columns: new[] { "EntityType", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ChangeRecords_OccurredAt",
                schema: "audit",
                table: "ChangeRecords",
                column: "OccurredAt");

            migrationBuilder.CreateIndex(
                name: "IX_ChangeRecords_SubjectId_OccurredAt",
                schema: "audit",
                table: "ChangeRecords",
                columns: new[] { "SubjectId", "OccurredAt" });

            migrationBuilder.Sql(
                @"EXEC(N'IF DATABASE_PRINCIPAL_ID(N''erp_audit_append_only'') IS NULL CREATE ROLE [erp_audit_append_only];')");

            migrationBuilder.Sql(
                @"EXEC(N'DENY UPDATE, DELETE, ALTER ON SCHEMA::[audit] TO [erp_audit_append_only];')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                @"EXEC(N'IF DATABASE_PRINCIPAL_ID(N''erp_audit_append_only'') IS NOT NULL
BEGIN
REVOKE UPDATE, DELETE, ALTER ON SCHEMA::[audit] FROM [erp_audit_append_only];
DECLARE @members nvarchar(max) = N'''';
SELECT @members += N''ALTER ROLE [erp_audit_append_only] DROP MEMBER '' + QUOTENAME([m].[name]) + N'';''
FROM [sys].[database_role_members] AS [rm]
INNER JOIN [sys].[database_principals] AS [m] ON [m].[principal_id] = [rm].[member_principal_id]
WHERE [rm].[role_principal_id] = DATABASE_PRINCIPAL_ID(N''erp_audit_append_only'');
EXEC sp_executesql @members;
DROP ROLE [erp_audit_append_only];
END')");

            migrationBuilder.DropTable(
                name: "ChangeRecords",
                schema: "audit");
        }
    }
}
