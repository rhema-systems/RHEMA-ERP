using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260809003309_AddFinanceReportAutomation")]
    public sealed class AddFinanceReportAutomation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // FR-RP-010 extends the shared reporting tables rather than creating
            // a Finance-only duplicate. Existing interactive exports therefore
            // remain queryable beside scheduled artifacts.
            migrationBuilder.DropIndex(
                name: "IX_ReportExports_TenantId",
                table: "ReportExports");

            migrationBuilder.DropIndex(
                name: "IX_ReportExecutions_TenantId",
                table: "ReportExecutions");

            migrationBuilder.AddColumn<int>(
                name: "ConsecutiveFailureCount",
                table: "ReportSchedules",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "LastError",
                table: "ReportSchedules",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaximumRetryAttempts",
                table: "ReportSchedules",
                type: "int",
                nullable: false,
                defaultValue: 3);

            migrationBuilder.AddColumn<DateTime>(
                name: "PausedAt",
                table: "ReportSchedules",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PausedById",
                table: "ReportSchedules",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RecipientUserIds",
                table: "ReportSchedules",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReportTemplateId",
                table: "ReportSchedules",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "ReportSchedules",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<Guid>(
                name: "RunAsUserId",
                table: "ReportSchedules",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContentType",
                table: "ReportExports",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RetainUntil",
                table: "ReportExports",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Sha256Checksum",
                table: "ReportExports",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StoragePath",
                table: "ReportExports",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AttemptNumber",
                table: "ReportExecutions",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "CompletedAt",
                table: "ReportExecutions",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReportExportId",
                table: "ReportExecutions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReportScheduleId",
                table: "ReportExecutions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReportTemplateId",
                table: "ReportExecutions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "ReportExecutions",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<DateTime>(
                name: "ScheduledFor",
                table: "ReportExecutions",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "StartedAt",
                table: "ReportExecutions",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TemplateVersion",
                table: "ReportExecutions",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Trigger",
                table: "ReportExecutions",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Manual");

            migrationBuilder.CreateIndex(
                name: "IX_ReportSchedules_IsActive_Status_NextExecutionDate",
                table: "ReportSchedules",
                columns: new[] { "IsActive", "Status", "NextExecutionDate" });

            migrationBuilder.CreateIndex(
                name: "IX_ReportSchedules_PausedById",
                table: "ReportSchedules",
                column: "PausedById");

            migrationBuilder.CreateIndex(
                name: "IX_ReportSchedules_ReportTemplateId",
                table: "ReportSchedules",
                column: "ReportTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_ReportSchedules_RunAsUserId",
                table: "ReportSchedules",
                column: "RunAsUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ReportExports_TenantId_Status_ExportedAt",
                table: "ReportExports",
                columns: new[] { "TenantId", "Status", "ExportedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ReportExecutions_ReportExportId",
                table: "ReportExecutions",
                column: "ReportExportId");

            migrationBuilder.CreateIndex(
                name: "IX_ReportExecutions_ReportScheduleId",
                table: "ReportExecutions",
                column: "ReportScheduleId");

            migrationBuilder.CreateIndex(
                name: "IX_ReportExecutions_ReportTemplateId",
                table: "ReportExecutions",
                column: "ReportTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_ReportExecutions_TenantId_ReportScheduleId_ScheduledFor",
                table: "ReportExecutions",
                columns: new[] { "TenantId", "ReportScheduleId", "ScheduledFor" },
                unique: true,
                filter: "[ReportScheduleId] IS NOT NULL AND [ScheduledFor] IS NOT NULL AND [IsDeleted] = 0");

            // This unique occurrence key is the database-level guard against two
            // horizontally scaled API nodes generating and distributing the same
            // scheduled report slot.

            migrationBuilder.AddForeignKey(
                name: "FK_ReportExecutions_ReportExports_ReportExportId",
                table: "ReportExecutions",
                column: "ReportExportId",
                principalTable: "ReportExports",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ReportExecutions_ReportSchedules_ReportScheduleId",
                table: "ReportExecutions",
                column: "ReportScheduleId",
                principalTable: "ReportSchedules",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ReportExecutions_ReportTemplates_ReportTemplateId",
                table: "ReportExecutions",
                column: "ReportTemplateId",
                principalTable: "ReportTemplates",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ReportSchedules_ReportTemplates_ReportTemplateId",
                table: "ReportSchedules",
                column: "ReportTemplateId",
                principalTable: "ReportTemplates",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ReportSchedules_Users_PausedById",
                table: "ReportSchedules",
                column: "PausedById",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ReportSchedules_Users_RunAsUserId",
                table: "ReportSchedules",
                column: "RunAsUserId",
                principalTable: "Users",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ReportExecutions_ReportExports_ReportExportId",
                table: "ReportExecutions");

            migrationBuilder.DropForeignKey(
                name: "FK_ReportExecutions_ReportSchedules_ReportScheduleId",
                table: "ReportExecutions");

            migrationBuilder.DropForeignKey(
                name: "FK_ReportExecutions_ReportTemplates_ReportTemplateId",
                table: "ReportExecutions");

            migrationBuilder.DropForeignKey(
                name: "FK_ReportSchedules_ReportTemplates_ReportTemplateId",
                table: "ReportSchedules");

            migrationBuilder.DropForeignKey(
                name: "FK_ReportSchedules_Users_PausedById",
                table: "ReportSchedules");

            migrationBuilder.DropForeignKey(
                name: "FK_ReportSchedules_Users_RunAsUserId",
                table: "ReportSchedules");

            migrationBuilder.DropIndex(
                name: "IX_ReportSchedules_IsActive_Status_NextExecutionDate",
                table: "ReportSchedules");

            migrationBuilder.DropIndex(
                name: "IX_ReportSchedules_PausedById",
                table: "ReportSchedules");

            migrationBuilder.DropIndex(
                name: "IX_ReportSchedules_ReportTemplateId",
                table: "ReportSchedules");

            migrationBuilder.DropIndex(
                name: "IX_ReportSchedules_RunAsUserId",
                table: "ReportSchedules");

            migrationBuilder.DropIndex(
                name: "IX_ReportExports_TenantId_Status_ExportedAt",
                table: "ReportExports");

            migrationBuilder.DropIndex(
                name: "IX_ReportExecutions_ReportExportId",
                table: "ReportExecutions");

            migrationBuilder.DropIndex(
                name: "IX_ReportExecutions_ReportScheduleId",
                table: "ReportExecutions");

            migrationBuilder.DropIndex(
                name: "IX_ReportExecutions_ReportTemplateId",
                table: "ReportExecutions");

            migrationBuilder.DropIndex(
                name: "IX_ReportExecutions_TenantId_ReportScheduleId_ScheduledFor",
                table: "ReportExecutions");

            migrationBuilder.DropColumn(
                name: "ConsecutiveFailureCount",
                table: "ReportSchedules");

            migrationBuilder.DropColumn(
                name: "LastError",
                table: "ReportSchedules");

            migrationBuilder.DropColumn(
                name: "MaximumRetryAttempts",
                table: "ReportSchedules");

            migrationBuilder.DropColumn(
                name: "PausedAt",
                table: "ReportSchedules");

            migrationBuilder.DropColumn(
                name: "PausedById",
                table: "ReportSchedules");

            migrationBuilder.DropColumn(
                name: "RecipientUserIds",
                table: "ReportSchedules");

            migrationBuilder.DropColumn(
                name: "ReportTemplateId",
                table: "ReportSchedules");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "ReportSchedules");

            migrationBuilder.DropColumn(
                name: "RunAsUserId",
                table: "ReportSchedules");

            migrationBuilder.DropColumn(
                name: "ContentType",
                table: "ReportExports");

            migrationBuilder.DropColumn(
                name: "RetainUntil",
                table: "ReportExports");

            migrationBuilder.DropColumn(
                name: "Sha256Checksum",
                table: "ReportExports");

            migrationBuilder.DropColumn(
                name: "StoragePath",
                table: "ReportExports");

            migrationBuilder.DropColumn(
                name: "AttemptNumber",
                table: "ReportExecutions");

            migrationBuilder.DropColumn(
                name: "CompletedAt",
                table: "ReportExecutions");

            migrationBuilder.DropColumn(
                name: "ReportExportId",
                table: "ReportExecutions");

            migrationBuilder.DropColumn(
                name: "ReportScheduleId",
                table: "ReportExecutions");

            migrationBuilder.DropColumn(
                name: "ReportTemplateId",
                table: "ReportExecutions");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "ReportExecutions");

            migrationBuilder.DropColumn(
                name: "ScheduledFor",
                table: "ReportExecutions");

            migrationBuilder.DropColumn(
                name: "StartedAt",
                table: "ReportExecutions");

            migrationBuilder.DropColumn(
                name: "TemplateVersion",
                table: "ReportExecutions");

            migrationBuilder.DropColumn(
                name: "Trigger",
                table: "ReportExecutions");

            migrationBuilder.CreateIndex(
                name: "IX_ReportExports_TenantId",
                table: "ReportExports",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ReportExecutions_TenantId",
                table: "ReportExecutions",
                column: "TenantId");
        }
    }
}
