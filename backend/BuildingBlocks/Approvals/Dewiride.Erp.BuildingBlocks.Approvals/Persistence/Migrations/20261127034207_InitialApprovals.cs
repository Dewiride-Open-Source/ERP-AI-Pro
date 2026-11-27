using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dewiride.Erp.BuildingBlocks.Approvals.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialApprovals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "platform_approvals");

            migrationBuilder.CreateTable(
                name: "ApprovalKindSettings",
                schema: "platform_approvals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kind = table.Column<string>(type: "varchar(128)", unicode: false, maxLength: 128, nullable: false),
                    Required = table.Column<bool>(type: "bit", nullable: false),
                    WorkingDaysToDecide = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApprovalKindSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ApprovalRequests",
                schema: "platform_approvals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kind = table.Column<string>(type: "varchar(128)", unicode: false, maxLength: 128, nullable: false),
                    SubjectType = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                    SubjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestedFor = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    State = table.Column<byte>(type: "tinyint", nullable: false),
                    CurrentStep = table.Column<int>(type: "int", nullable: true),
                    SubmittedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ClosedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApprovalRequests", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ApprovalSteps",
                schema: "platform_approvals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Order = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Rule = table.Column<byte>(type: "tinyint", nullable: false),
                    ApproverKind = table.Column<byte>(type: "tinyint", nullable: false),
                    ApproverId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ApproverRole = table.Column<string>(type: "varchar(16)", unicode: false, maxLength: 16, nullable: true),
                    WorkingDays = table.Column<int>(type: "int", nullable: false),
                    State = table.Column<byte>(type: "tinyint", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DueAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DecidedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DecidedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    InPlaceOfApprover = table.Column<bool>(type: "bit", nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApprovalSteps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ApprovalSteps_ApprovalRequests_RequestId",
                        column: x => x.RequestId,
                        principalSchema: "platform_approvals",
                        principalTable: "ApprovalRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalKindSettings_Kind",
                schema: "platform_approvals",
                table: "ApprovalKindSettings",
                column: "Kind",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalRequests_CreatedBy_SubmittedAt",
                schema: "platform_approvals",
                table: "ApprovalRequests",
                columns: new[] { "CreatedBy", "SubmittedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalRequests_Kind_SubjectId",
                schema: "platform_approvals",
                table: "ApprovalRequests",
                columns: new[] { "Kind", "SubjectId" },
                unique: true,
                filter: "[State] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalRequests_Kind_SubjectId_SubmittedAt",
                schema: "platform_approvals",
                table: "ApprovalRequests",
                columns: new[] { "Kind", "SubjectId", "SubmittedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalRequests_RequestedFor_SubmittedAt",
                schema: "platform_approvals",
                table: "ApprovalRequests",
                columns: new[] { "RequestedFor", "SubmittedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalRequests_State_SubmittedAt",
                schema: "platform_approvals",
                table: "ApprovalRequests",
                columns: new[] { "State", "SubmittedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalSteps_ApproverId_State",
                schema: "platform_approvals",
                table: "ApprovalSteps",
                columns: new[] { "ApproverId", "State" },
                filter: "[State] = 2");

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalSteps_ApproverRole_State",
                schema: "platform_approvals",
                table: "ApprovalSteps",
                columns: new[] { "ApproverRole", "State" },
                filter: "[State] = 2");

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalSteps_DecidedBy_DecidedAt",
                schema: "platform_approvals",
                table: "ApprovalSteps",
                columns: new[] { "DecidedBy", "DecidedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalSteps_RequestId_Order",
                schema: "platform_approvals",
                table: "ApprovalSteps",
                columns: new[] { "RequestId", "Order" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ApprovalKindSettings",
                schema: "platform_approvals");

            migrationBuilder.DropTable(
                name: "ApprovalSteps",
                schema: "platform_approvals");

            migrationBuilder.DropTable(
                name: "ApprovalRequests",
                schema: "platform_approvals");
        }
    }
}
