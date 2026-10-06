using ErpSystem.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261005103000_AddSecurityOperationsTenantIndexes")]
public partial class AddSecurityOperationsTenantIndexes : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateIndex(
            name: "IX_AuditLogs_TenantId_Timestamp",
            table: "AuditLogs",
            columns: new[] { "TenantId", "Timestamp" });

        migrationBuilder.CreateIndex(
            name: "IX_SecurityLogs_TenantId_Timestamp",
            table: "SecurityLogs",
            columns: new[] { "TenantId", "Timestamp" });

        migrationBuilder.CreateIndex(
            name: "IX_SecurityLogs_TenantId_Action_Timestamp",
            table: "SecurityLogs",
            columns: new[] { "TenantId", "Action", "Timestamp" });

        migrationBuilder.CreateIndex(
            name: "IX_SecurityLogs_TenantId_UserId_Timestamp",
            table: "SecurityLogs",
            columns: new[] { "TenantId", "UserId", "Timestamp" });

        migrationBuilder.CreateIndex(
            name: "IX_UserSessions_TenantId_IsActive_LastActivityTime",
            table: "UserSessions",
            columns: new[] { "TenantId", "IsActive", "LastActivityTime" });

        migrationBuilder.CreateIndex(
            name: "IX_UserSessions_TenantId_UserId_LoginTime",
            table: "UserSessions",
            columns: new[] { "TenantId", "UserId", "LoginTime" });

        migrationBuilder.CreateIndex(
            name: "IX_SecurityAlerts_TenantId_Dismissed_Timestamp",
            table: "SecurityAlerts",
            columns: new[] { "TenantId", "Dismissed", "Timestamp" });

        migrationBuilder.CreateIndex(
            name: "IX_ThreatDetections_TenantId_Status_Severity_DetectedAt",
            table: "ThreatDetections",
            columns: new[] { "TenantId", "Status", "Severity", "DetectedAt" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(name: "IX_AuditLogs_TenantId_Timestamp", table: "AuditLogs");
        migrationBuilder.DropIndex(name: "IX_SecurityLogs_TenantId_Timestamp", table: "SecurityLogs");
        migrationBuilder.DropIndex(name: "IX_SecurityLogs_TenantId_Action_Timestamp", table: "SecurityLogs");
        migrationBuilder.DropIndex(name: "IX_SecurityLogs_TenantId_UserId_Timestamp", table: "SecurityLogs");
        migrationBuilder.DropIndex(name: "IX_UserSessions_TenantId_IsActive_LastActivityTime", table: "UserSessions");
        migrationBuilder.DropIndex(name: "IX_UserSessions_TenantId_UserId_LoginTime", table: "UserSessions");
        migrationBuilder.DropIndex(name: "IX_SecurityAlerts_TenantId_Dismissed_Timestamp", table: "SecurityAlerts");
        migrationBuilder.DropIndex(name: "IX_ThreatDetections_TenantId_Status_Severity_DetectedAt", table: "ThreatDetections");
    }
}
