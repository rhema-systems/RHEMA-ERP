using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

/// <summary>
/// Adds the shared audit-governance lifecycle and semantic operation index used by every module.
/// Procurement and Inventory contribute their control events through the shared provider seam.
/// </summary>
public partial class TDC0706SharedAuditGovernance : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "Operation",
            table: "ProcurementControlEvents",
            type: "int",
            nullable: false,
            defaultValue: 0);

        // The table is normally append-only. The migration owns this bounded backfill, then
        // restores the exact append-only guard before accepting application traffic.
        migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_ProcurementControlEvents_AppendOnly];");
        migrationBuilder.Sql(
            """
            UPDATE [dbo].[ProcurementControlEvents]
            SET [Operation] = CASE
                WHEN LOWER([Action]) LIKE '%reverse%' OR LOWER([Action]) LIKE '%reversal%'
                    OR LOWER([Action]) LIKE '%void%' THEN 7
                WHEN LOWER([Action]) LIKE '%override%' OR LOWER([Action]) LIKE '%emergency%'
                    OR LOWER([Action]) LIKE '%bypass%' OR LOWER([Action]) LIKE '%waiver%' THEN 5
                WHEN LOWER([Action]) LIKE '%reject%' OR LOWER([Action]) LIKE '%decline%' THEN 4
                WHEN LOWER([Action]) LIKE '%dispatch%' OR LOWER([Action]) LIKE '%issue%'
                    OR LOWER([Action]) LIKE '%send%' OR LOWER([Action]) LIKE '%sent%' THEN 8
                WHEN LOWER([Action]) LIKE '%receive%' OR LOWER([Action]) LIKE '%receipt%'
                    OR LOWER([Action]) LIKE '%acknowledge%' THEN 9
                WHEN LOWER([Action]) LIKE '%post%' OR LOWER([Action]) LIKE '%capitaliz%' THEN 6
                WHEN LOWER([Action]) LIKE '%approve%' OR LOWER([Action]) LIKE '%authoriz%'
                    OR LOWER([Action]) LIKE '%accept%' THEN 3
                WHEN LOWER([Action]) LIKE '%update%' OR LOWER([Action]) LIKE '%amend%'
                    OR LOWER([Action]) LIKE '%edit%' OR LOWER([Action]) LIKE '%change%'
                    OR LOWER([Action]) LIKE '%revis%' OR LOWER([Action]) LIKE '%correct%' THEN 2
                WHEN LOWER([Action]) LIKE '%create%' OR LOWER([Action]) LIKE '%register%'
                    OR LOWER([Action]) LIKE '%generat%' OR LOWER([Action]) LIKE '%submit%' THEN 1
                ELSE 0
            END;
            """);
        migrationBuilder.Sql(
            """
            CREATE TRIGGER [dbo].[TR_ProcurementControlEvents_AppendOnly]
            ON [dbo].[ProcurementControlEvents]
            INSTEAD OF UPDATE, DELETE
            AS
            BEGIN
                SET NOCOUNT ON;
                THROW 51000, 'Procurement control events are append-only and cannot be updated or deleted.', 1;
            END;
            """);

        migrationBuilder.CreateTable(
            name: "AuditRecordLifecycleEvents",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                StoreKey = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false),
                RecordId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                SequenceNumber = table.Column<int>(type: "int", nullable: false),
                Action = table.Column<int>(type: "int", nullable: false),
                Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                RequestKey = table.Column<string>(type: "varchar(200)", unicode: false, maxLength: 200, nullable: false),
                CorrelationId = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false),
                ArchiveReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                SourceOccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                RetainUntilUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ActorName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                ActorRolesJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                RecordSnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                PreviousIntegrityHash = table.Column<string>(type: "char(64)", unicode: false, fixedLength: true, maxLength: 64, nullable: true),
                IntegrityHash = table.Column<string>(type: "char(64)", unicode: false, fixedLength: true, maxLength: 64, nullable: false),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AuditRecordLifecycleEvents", x => x.Id);
                table.CheckConstraint("CK_AuditRecordLifecycleEvents_Action", "[Action] BETWEEN 0 AND 3");
                table.CheckConstraint("CK_AuditRecordLifecycleEvents_Retention", "[RetainUntilUtc] >= DATEADD(day, 2555, [SourceOccurredAtUtc])");
                table.CheckConstraint("CK_AuditRecordLifecycleEvents_Sequence", "[SequenceNumber] >= 1");
                table.ForeignKey(
                    name: "FK_AuditRecordLifecycleEvents_Tenants_TenantId",
                    column: x => x.TenantId,
                    principalTable: "Tenants",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_AuditRecordLifecycleEvents_Users_ActorUserId",
                    column: x => x.ActorUserId,
                    principalTable: "Users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_ProcurementControlEvents_TenantId_Operation_OccurredAtUtc",
            table: "ProcurementControlEvents",
            columns: new[] { "TenantId", "Operation", "OccurredAtUtc" });
        migrationBuilder.AddCheckConstraint(
            name: "CK_ProcurementControlEvents_Operation",
            table: "ProcurementControlEvents",
            sql: "[Operation] BETWEEN 0 AND 9");

        migrationBuilder.Sql(
            """
            UPDATE [dbo].[DataRetentionPolicies]
            SET [AuditLogRetentionDays] = CASE WHEN [AuditLogRetentionDays] < 2555 THEN 2555 ELSE [AuditLogRetentionDays] END,
                [WorkflowAuditRetentionDays] = CASE WHEN [WorkflowAuditRetentionDays] < 2555 THEN 2555 ELSE [WorkflowAuditRetentionDays] END;
            """);
        migrationBuilder.AddCheckConstraint(
            name: "CK_DataRetentionPolicies_AuditMinimum",
            table: "DataRetentionPolicies",
            sql: "[AuditLogRetentionDays] >= 2555");
        migrationBuilder.AddCheckConstraint(
            name: "CK_DataRetentionPolicies_WorkflowAuditMinimum",
            table: "DataRetentionPolicies",
            sql: "[WorkflowAuditRetentionDays] >= 2555");

        migrationBuilder.CreateIndex(
            name: "IX_AuditRecordLifecycleEvents_ActorUserId",
            table: "AuditRecordLifecycleEvents",
            column: "ActorUserId");
        migrationBuilder.CreateIndex(
            name: "IX_AuditRecordLifecycleEvents_TenantId_RequestKey",
            table: "AuditRecordLifecycleEvents",
            columns: new[] { "TenantId", "RequestKey" },
            unique: true);
        migrationBuilder.CreateIndex(
            name: "IX_AuditRecordLifecycleEvents_TenantId_StoreKey_RecordId_CreatedAt",
            table: "AuditRecordLifecycleEvents",
            columns: new[] { "TenantId", "StoreKey", "RecordId", "CreatedAt" });
        migrationBuilder.CreateIndex(
            name: "IX_AuditRecordLifecycleEvents_TenantId_StoreKey_RecordId_SequenceNumber",
            table: "AuditRecordLifecycleEvents",
            columns: new[] { "TenantId", "StoreKey", "RecordId", "SequenceNumber" },
            unique: true);

        migrationBuilder.Sql(
            """
            CREATE TRIGGER [dbo].[TR_AuditLogs_AppendOnly]
            ON [dbo].[AuditLogs]
            AFTER UPDATE, DELETE
            AS
            BEGIN
                SET NOCOUNT ON;
                THROW 51470, 'Platform audit logs are append-only and cannot be updated or deleted.', 1;
            END;
            """);
        migrationBuilder.Sql(
            """
            CREATE TRIGGER [dbo].[TR_AuditRecordLifecycleEvents_AppendOnly]
            ON [dbo].[AuditRecordLifecycleEvents]
            INSTEAD OF UPDATE, DELETE
            AS
            BEGIN
                SET NOCOUNT ON;
                THROW 51471, 'Audit lifecycle events are append-only and cannot be updated or deleted.', 1;
            END;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_AuditRecordLifecycleEvents_AppendOnly];");
        migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_AuditLogs_AppendOnly];");

        migrationBuilder.DropTable(name: "AuditRecordLifecycleEvents");
        migrationBuilder.DropIndex(
            name: "IX_ProcurementControlEvents_TenantId_Operation_OccurredAtUtc",
            table: "ProcurementControlEvents");
        migrationBuilder.DropCheckConstraint(
            name: "CK_ProcurementControlEvents_Operation",
            table: "ProcurementControlEvents");
        migrationBuilder.DropCheckConstraint(
            name: "CK_DataRetentionPolicies_AuditMinimum",
            table: "DataRetentionPolicies");
        migrationBuilder.DropCheckConstraint(
            name: "CK_DataRetentionPolicies_WorkflowAuditMinimum",
            table: "DataRetentionPolicies");
        migrationBuilder.DropColumn(name: "Operation", table: "ProcurementControlEvents");
    }
}
