using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dewiride.Erp.Modules.Identity.Users.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDepartmentsDesignationsAndPositions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Departments",
                schema: "identity_users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ParentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DeletedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Departments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Departments_Departments_ParentId",
                        column: x => x.ParentId,
                        principalSchema: "identity_users",
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Designations",
                schema: "identity_users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DeletedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Designations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Positions",
                schema: "identity_users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    DepartmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DesignationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ManagerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Positions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Positions_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalSchema: "identity_users",
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Positions_Designations_DesignationId",
                        column: x => x.DesignationId,
                        principalSchema: "identity_users",
                        principalTable: "Designations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Positions_Users_ManagerId",
                        column: x => x.ManagerId,
                        principalSchema: "identity_users",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Positions_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "identity_users",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Departments_ParentId_Name",
                schema: "identity_users",
                table: "Departments",
                columns: new[] { "ParentId", "Name" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Designations_Name",
                schema: "identity_users",
                table: "Designations",
                column: "Name",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Positions_DepartmentId",
                schema: "identity_users",
                table: "Positions",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_Positions_DesignationId",
                schema: "identity_users",
                table: "Positions",
                column: "DesignationId");

            migrationBuilder.CreateIndex(
                name: "IX_Positions_ManagerId",
                schema: "identity_users",
                table: "Positions",
                column: "ManagerId");

            migrationBuilder.CreateIndex(
                name: "IX_Positions_UserId_EffectiveFrom",
                schema: "identity_users",
                table: "Positions",
                columns: new[] { "UserId", "EffectiveFrom" },
                unique: true);

            migrationBuilder.Sql(
                @"EXEC(N'INSERT INTO [identity_users].[Designations] ([Id], [Name], [CreatedAt], [CreatedBy], [IsDeleted])
SELECT NEWID(), MIN(LTRIM(RTRIM([u].[Designation])) COLLATE Latin1_General_BIN2), SYSUTCDATETIME(), ''00000000-0000-0000-0000-000000000001'', CAST(0 AS bit)
FROM [identity_users].[Users] AS [u]
WHERE LTRIM(RTRIM([u].[Designation])) <> N''''
GROUP BY LTRIM(RTRIM([u].[Designation]))')");

            migrationBuilder.Sql(
                @"EXEC(N'INSERT INTO [identity_users].[Positions] ([Id], [UserId], [EffectiveFrom], [DesignationId], [CreatedAt], [CreatedBy])
SELECT NEWID(), [u].[Id], CASE WHEN [s].[Start] > [s].[Today] THEN [s].[Today] ELSE [s].[Start] END, [d].[Id], SYSUTCDATETIME(), ''00000000-0000-0000-0000-000000000001''
FROM [identity_users].[Users] AS [u]
CROSS APPLY (SELECT COALESCE([u].[DateOfJoining], CAST(SWITCHOFFSET([u].[CreatedAt], ''+05:30'') AS date)) AS [Start], CAST(SWITCHOFFSET(SYSDATETIMEOFFSET(), ''+05:30'') AS date) AS [Today]) AS [s]
INNER JOIN [identity_users].[Designations] AS [d] ON [d].[Name] = LTRIM(RTRIM([u].[Designation])) AND [d].[IsDeleted] = 0
WHERE LTRIM(RTRIM([u].[Designation])) <> N''''')");

            migrationBuilder.DropColumn(
                name: "Designation",
                schema: "identity_users",
                table: "Users");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Designation",
                schema: "identity_users",
                table: "Users",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.Sql(
                @"EXEC(N'UPDATE [u] SET [Designation] = (
SELECT TOP (1) [d].[Name] FROM [identity_users].[Positions] AS [p]
INNER JOIN [identity_users].[Designations] AS [d] ON [d].[Id] = [p].[DesignationId]
WHERE [p].[UserId] = [u].[Id] AND [p].[EffectiveFrom] <= CAST(SWITCHOFFSET(SYSDATETIMEOFFSET(), ''+05:30'') AS date)
ORDER BY [p].[EffectiveFrom] DESC)
FROM [identity_users].[Users] AS [u]')");

            migrationBuilder.DropTable(
                name: "Positions",
                schema: "identity_users");

            migrationBuilder.DropTable(
                name: "Departments",
                schema: "identity_users");

            migrationBuilder.DropTable(
                name: "Designations",
                schema: "identity_users");
        }
    }
}
