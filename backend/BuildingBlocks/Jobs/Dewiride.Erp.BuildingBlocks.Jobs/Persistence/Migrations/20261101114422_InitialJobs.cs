using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dewiride.Erp.BuildingBlocks.Jobs.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialJobs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "platform_jobs");

            migrationBuilder.CreateTable(
                name: "JobRuns",
                schema: "platform_jobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    JobName = table.Column<string>(type: "varchar(128)", unicode: false, maxLength: 128, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "varchar(200)", unicode: false, maxLength: 200, nullable: false),
                    Payload = table.Column<string>(type: "json", nullable: true),
                    Status = table.Column<byte>(type: "tinyint", nullable: false),
                    DueAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RequestedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AttemptCount = table.Column<int>(type: "int", nullable: false),
                    FailureCount = table.Column<int>(type: "int", nullable: false),
                    LeaseToken = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LeaseHolder = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: true),
                    LeaseExpiresAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CancelRequestedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CancelRequestedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    StartedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    FinishedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LastErrorCode = table.Column<string>(type: "varchar(128)", unicode: false, maxLength: 128, nullable: true),
                    LastErrorMessage = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JobRuns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "JobSchedules",
                schema: "platform_jobs",
                columns: table => new
                {
                    JobName = table.Column<string>(type: "varchar(128)", unicode: false, maxLength: 128, nullable: false),
                    Expression = table.Column<string>(type: "varchar(200)", unicode: false, maxLength: 200, nullable: false),
                    NextOccurrenceAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastOccurrenceAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LastRunId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JobSchedules", x => x.JobName);
                });

            migrationBuilder.CreateTable(
                name: "JobRunEvents",
                schema: "platform_jobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Kind = table.Column<byte>(type: "tinyint", nullable: false),
                    ActorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Attempt = table.Column<int>(type: "int", nullable: true),
                    Runner = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: true),
                    TraceId = table.Column<string>(type: "varchar(32)", unicode: false, maxLength: 32, nullable: true),
                    ErrorCode = table.Column<string>(type: "varchar(128)", unicode: false, maxLength: 128, nullable: true),
                    ErrorMessage = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    RetryAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JobRunEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_JobRunEvents_JobRuns_RunId",
                        column: x => x.RunId,
                        principalSchema: "platform_jobs",
                        principalTable: "JobRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_JobRunEvents_RunId_OccurredAt",
                schema: "platform_jobs",
                table: "JobRunEvents",
                columns: new[] { "RunId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_JobRuns_CreatedAt",
                schema: "platform_jobs",
                table: "JobRuns",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_JobRuns_FinishedAt",
                schema: "platform_jobs",
                table: "JobRuns",
                column: "FinishedAt",
                filter: "[FinishedAt] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_JobRuns_JobName_IdempotencyKey",
                schema: "platform_jobs",
                table: "JobRuns",
                columns: new[] { "JobName", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_JobRuns_LeaseExpiresAt",
                schema: "platform_jobs",
                table: "JobRuns",
                column: "LeaseExpiresAt",
                filter: "[LeaseExpiresAt] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_JobRuns_Status_DueAt",
                schema: "platform_jobs",
                table: "JobRuns",
                columns: new[] { "Status", "DueAt" })
                .Annotation("SqlServer:Include", new[] { "JobName" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "JobRunEvents",
                schema: "platform_jobs");

            migrationBuilder.DropTable(
                name: "JobSchedules",
                schema: "platform_jobs");

            migrationBuilder.DropTable(
                name: "JobRuns",
                schema: "platform_jobs");
        }
    }
}
