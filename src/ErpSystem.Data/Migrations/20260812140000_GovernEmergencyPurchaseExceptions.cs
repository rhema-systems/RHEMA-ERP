using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260812140000_GovernEmergencyPurchaseExceptions")]
public partial class GovernEmergencyPurchaseExceptions : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_EmergencyProcurementPlans_PlanCode",
            table: "EmergencyProcurementPlans");

        migrationBuilder.AddColumn<string>(name: "ApprovalAuthority", table: "EmergencyProcurementPlans", type: "nvarchar(30)", maxLength: 30, nullable: true);
        migrationBuilder.AddColumn<string>(name: "ApprovalReference", table: "EmergencyProcurementPlans", type: "nvarchar(200)", maxLength: 200, nullable: true);
        migrationBuilder.AddColumn<Guid>(name: "CentralDocumentVersionId", table: "EmergencyProcurementPlans", type: "uniqueidentifier", nullable: true);
        migrationBuilder.AddColumn<string>(name: "EvidenceReference", table: "EmergencyProcurementPlans", type: "nvarchar(500)", maxLength: 500, nullable: true);
        migrationBuilder.AddColumn<Guid>(name: "ExceptionRuleId", table: "EmergencyProcurementPlans", type: "uniqueidentifier", nullable: true);
        migrationBuilder.AddColumn<string>(name: "ExceptionJustification", table: "EmergencyProcurementPlans", type: "nvarchar(2000)", maxLength: 2000, nullable: true);
        migrationBuilder.AddColumn<Guid>(name: "ExceptionalSourcingTenderId", table: "EmergencyProcurementPlans", type: "uniqueidentifier", nullable: true);
        migrationBuilder.AddColumn<Guid>(name: "FileUploadRecordId", table: "EmergencyProcurementPlans", type: "uniqueidentifier", nullable: true);
        migrationBuilder.AddColumn<DateTime>(name: "FiledAtUtc", table: "EmergencyProcurementPlans", type: "datetime2", nullable: true);
        migrationBuilder.AddColumn<Guid>(name: "FiledById", table: "EmergencyProcurementPlans", type: "uniqueidentifier", nullable: true);
        migrationBuilder.AddColumn<string>(name: "IntegrityHash", table: "EmergencyProcurementPlans", type: "nvarchar(64)", maxLength: 64, nullable: false, defaultValue: "");
        migrationBuilder.AddColumn<string>(name: "InternalAuditVouchNote", table: "EmergencyProcurementPlans", type: "nvarchar(1000)", maxLength: 1000, nullable: true);
        migrationBuilder.AddColumn<DateTime>(name: "InternalAuditVouchedAtUtc", table: "EmergencyProcurementPlans", type: "datetime2", nullable: true);
        migrationBuilder.AddColumn<Guid>(name: "InternalAuditVouchedById", table: "EmergencyProcurementPlans", type: "uniqueidentifier", nullable: true);
        migrationBuilder.AddColumn<string>(name: "LifecycleSnapshotJson", table: "EmergencyProcurementPlans", type: "nvarchar(max)", nullable: false, defaultValue: "");
        migrationBuilder.AddColumn<Guid>(name: "PostAwardCentralDocumentVersionId", table: "EmergencyProcurementPlans", type: "uniqueidentifier", nullable: true);
        migrationBuilder.AddColumn<string>(name: "PostAwardEvidenceReference", table: "EmergencyProcurementPlans", type: "nvarchar(500)", maxLength: 500, nullable: true);
        migrationBuilder.AddColumn<Guid>(name: "PostAwardFileUploadRecordId", table: "EmergencyProcurementPlans", type: "uniqueidentifier", nullable: true);
        migrationBuilder.AddColumn<string>(name: "PostAwardJustification", table: "EmergencyProcurementPlans", type: "nvarchar(2000)", maxLength: 2000, nullable: true);
        migrationBuilder.AddColumn<DateTime>(name: "PreparedAtUtc", table: "EmergencyProcurementPlans", type: "datetime2", nullable: true);
        migrationBuilder.AddColumn<Guid>(name: "PreparedById", table: "EmergencyProcurementPlans", type: "uniqueidentifier", nullable: true);
        migrationBuilder.AddColumn<Guid>(name: "PurchaseRequisitionId", table: "EmergencyProcurementPlans", type: "uniqueidentifier", nullable: true);
        migrationBuilder.AddColumn<byte[]>(name: "RowVersion", table: "EmergencyProcurementPlans", type: "rowversion", rowVersion: true, nullable: false);
        migrationBuilder.AddColumn<DateTime>(name: "SubmittedForApprovalAtUtc", table: "EmergencyProcurementPlans", type: "datetime2", nullable: true);
        migrationBuilder.AddColumn<DateTime>(name: "SubmittedForAuditAtUtc", table: "EmergencyProcurementPlans", type: "datetime2", nullable: true);
        migrationBuilder.AddColumn<Guid>(name: "WorkflowDefinitionId", table: "EmergencyProcurementPlans", type: "uniqueidentifier", nullable: true);
        migrationBuilder.AddColumn<Guid>(name: "WorkflowInstanceId", table: "EmergencyProcurementPlans", type: "uniqueidentifier", nullable: true);

        // Legacy Active/Inactive rows were not supported by evidence, audit or
        // workflow lineage and therefore must re-enter through the governed Draft path.
        migrationBuilder.Sql("""
            UPDATE dbo.EmergencyProcurementPlans
               SET [Status] = 'Draft', ApprovedById = NULL, ApprovedDate = NULL
             WHERE [Status] <> 'Draft';
            """);

        migrationBuilder.CreateIndex(name: "IX_EmergencyProcurementPlans_TenantId_PlanCode", table: "EmergencyProcurementPlans", columns: new[] { "TenantId", "PlanCode" }, unique: true);
        migrationBuilder.CreateIndex(name: "IX_EmergencyProcurementPlans_TenantId_PurchaseRequisitionId", table: "EmergencyProcurementPlans", columns: new[] { "TenantId", "PurchaseRequisitionId" }, unique: true, filter: "[PurchaseRequisitionId] IS NOT NULL AND [IsDeleted] = 0");
        migrationBuilder.CreateIndex(name: "IX_EmergencyProcurementPlans_TenantId_ExceptionRuleId", table: "EmergencyProcurementPlans", columns: new[] { "TenantId", "ExceptionRuleId" });
        migrationBuilder.CreateIndex(name: "IX_EmergencyProcurementPlans_TenantId_WorkflowInstanceId", table: "EmergencyProcurementPlans", columns: new[] { "TenantId", "WorkflowInstanceId" });
        migrationBuilder.CreateIndex(name: "IX_EmergencyProcurementPlans_TenantId_ExceptionalSourcingTenderId", table: "EmergencyProcurementPlans", columns: new[] { "TenantId", "ExceptionalSourcingTenderId" });
        migrationBuilder.CreateIndex(name: "IX_EmergencyProcurementPlans_CentralDocumentVersionId", table: "EmergencyProcurementPlans", column: "CentralDocumentVersionId");
        migrationBuilder.CreateIndex(name: "IX_EmergencyProcurementPlans_PostAwardCentralDocumentVersionId", table: "EmergencyProcurementPlans", column: "PostAwardCentralDocumentVersionId");
        migrationBuilder.CreateIndex(name: "IX_EmergencyProcurementPlans_FileUploadRecordId", table: "EmergencyProcurementPlans", column: "FileUploadRecordId");
        migrationBuilder.CreateIndex(name: "IX_EmergencyProcurementPlans_PostAwardFileUploadRecordId", table: "EmergencyProcurementPlans", column: "PostAwardFileUploadRecordId");
        migrationBuilder.CreateIndex(name: "IX_EmergencyProcurementPlans_WorkflowDefinitionId", table: "EmergencyProcurementPlans", column: "WorkflowDefinitionId");
        migrationBuilder.CreateIndex(name: "IX_EmergencyProcurementPlans_PreparedById", table: "EmergencyProcurementPlans", column: "PreparedById");
        migrationBuilder.CreateIndex(name: "IX_EmergencyProcurementPlans_InternalAuditVouchedById", table: "EmergencyProcurementPlans", column: "InternalAuditVouchedById");
        migrationBuilder.CreateIndex(name: "IX_EmergencyProcurementPlans_FiledById", table: "EmergencyProcurementPlans", column: "FiledById");

        AddForeignKey(migrationBuilder, "FK_EmergencyProcurementPlans_PurchaseRequisitions_PurchaseRequisitionId", "PurchaseRequisitionId", "PurchaseRequisitions");
        AddForeignKey(migrationBuilder, "FK_EmergencyProcurementPlans_ProcurementPolicyExceptionRules_ExceptionRuleId", "ExceptionRuleId", "ProcurementPolicyExceptionRules");
        AddForeignKey(migrationBuilder, "FK_EmergencyProcurementPlans_WorkflowDefinitions_WorkflowDefinitionId", "WorkflowDefinitionId", "WorkflowDefinitions");
        AddForeignKey(migrationBuilder, "FK_EmergencyProcurementPlans_WorkflowInstances_WorkflowInstanceId", "WorkflowInstanceId", "WorkflowInstances");
        AddForeignKey(migrationBuilder, "FK_EmergencyProcurementPlans_CentralDocumentVersions_CentralDocumentVersionId", "CentralDocumentVersionId", "CentralDocumentVersions");
        AddForeignKey(migrationBuilder, "FK_EmergencyProcurementPlans_CentralDocumentVersions_PostAwardCentralDocumentVersionId", "PostAwardCentralDocumentVersionId", "CentralDocumentVersions");
        AddForeignKey(migrationBuilder, "FK_EmergencyProcurementPlans_FileUploadRecords_FileUploadRecordId", "FileUploadRecordId", "FileUploadRecords");
        AddForeignKey(migrationBuilder, "FK_EmergencyProcurementPlans_FileUploadRecords_PostAwardFileUploadRecordId", "PostAwardFileUploadRecordId", "FileUploadRecords");
        AddForeignKey(migrationBuilder, "FK_EmergencyProcurementPlans_Tenders_ExceptionalSourcingTenderId", "ExceptionalSourcingTenderId", "Tenders");
        AddForeignKey(migrationBuilder, "FK_EmergencyProcurementPlans_Users_PreparedById", "PreparedById", "Users");
        AddForeignKey(migrationBuilder, "FK_EmergencyProcurementPlans_Users_InternalAuditVouchedById", "InternalAuditVouchedById", "Users");
        AddForeignKey(migrationBuilder, "FK_EmergencyProcurementPlans_Users_FiledById", "FiledById", "Users");

        migrationBuilder.AddCheckConstraint(name: "CK_EmergencyProcurementPlans_GovernedStatus", table: "EmergencyProcurementPlans", sql: "[Status] IN ('Draft','Prepared','PendingAudit','AuditVouched','PendingApproval','Approved','Rejected','Triggered','Filed')");
        migrationBuilder.AddCheckConstraint(name: "CK_EmergencyProcurementPlans_PreparedLineage", table: "EmergencyProcurementPlans", sql: "([Status] = 'Draft' AND [PurchaseRequisitionId] IS NULL AND [ExceptionRuleId] IS NULL AND [WorkflowDefinitionId] IS NULL AND [PreparedById] IS NULL AND [PreparedAtUtc] IS NULL) OR ([Status] <> 'Draft' AND [PurchaseRequisitionId] IS NOT NULL AND [ExceptionRuleId] IS NOT NULL AND [WorkflowDefinitionId] IS NOT NULL AND [CentralDocumentVersionId] IS NOT NULL AND [FileUploadRecordId] IS NOT NULL AND LEN([EvidenceReference]) > 0 AND LEN([ExceptionJustification]) >= 20 AND [PreparedById] IS NOT NULL AND [PreparedAtUtc] IS NOT NULL AND [ApprovalAuthority] IN ('ManagingDirector','Board') AND LEN([IntegrityHash]) = 64)");
        migrationBuilder.AddCheckConstraint(name: "CK_EmergencyProcurementPlans_AuditLineage", table: "EmergencyProcurementPlans", sql: "([Status] IN ('Draft','Prepared','PendingAudit') AND [InternalAuditVouchedById] IS NULL AND [InternalAuditVouchedAtUtc] IS NULL) OR ([Status] IN ('AuditVouched','PendingApproval','Approved','Rejected','Triggered','Filed') AND [InternalAuditVouchedById] IS NOT NULL AND [InternalAuditVouchedAtUtc] IS NOT NULL AND LEN([InternalAuditVouchNote]) >= 10)");
        migrationBuilder.AddCheckConstraint(name: "CK_EmergencyProcurementPlans_ApprovalLineage", table: "EmergencyProcurementPlans", sql: "([Status] IN ('Draft','Prepared','PendingAudit','AuditVouched') AND [WorkflowInstanceId] IS NULL AND [ApprovedById] IS NULL AND [ApprovedDate] IS NULL) OR ([Status] IN ('PendingApproval','Rejected') AND [WorkflowInstanceId] IS NOT NULL AND [ApprovedById] IS NULL AND [ApprovedDate] IS NULL AND [ApprovalReference] IS NULL) OR ([Status] IN ('Approved','Triggered','Filed') AND [WorkflowInstanceId] IS NOT NULL AND [ApprovedById] IS NOT NULL AND [ApprovedDate] IS NOT NULL AND [ApprovalReference] IS NOT NULL)");
        migrationBuilder.AddCheckConstraint(name: "CK_EmergencyProcurementPlans_FilingLineage", table: "EmergencyProcurementPlans", sql: "([Status] <> 'Filed' AND [ExceptionalSourcingTenderId] IS NULL AND [PostAwardJustification] IS NULL AND [PostAwardCentralDocumentVersionId] IS NULL AND [PostAwardFileUploadRecordId] IS NULL AND [PostAwardEvidenceReference] IS NULL AND [FiledAtUtc] IS NULL AND [FiledById] IS NULL) OR ([Status] = 'Filed' AND [ExceptionalSourcingTenderId] IS NOT NULL AND [PostAwardJustification] IS NOT NULL AND [PostAwardCentralDocumentVersionId] IS NOT NULL AND [PostAwardFileUploadRecordId] IS NOT NULL AND [PostAwardEvidenceReference] IS NOT NULL AND [FiledAtUtc] IS NOT NULL AND [FiledById] IS NOT NULL)");

        migrationBuilder.Sql("""
            CREATE OR ALTER TRIGGER dbo.TR_EmergencyProcurementPlans_Governance
            ON dbo.EmergencyProcurementPlans
            AFTER UPDATE, DELETE
            AS
            BEGIN
                SET NOCOUNT ON;
                IF EXISTS (SELECT 1 FROM deleted d LEFT JOIN inserted i ON i.Id = d.Id WHERE i.Id IS NULL)
                    THROW 51871, 'Governed emergency-purchase records cannot be deleted.', 1;

                IF EXISTS (
                    SELECT 1 FROM inserted i JOIN deleted d ON d.Id = i.Id
                    WHERE d.Status IN ('Rejected','Filed'))
                    THROW 51872, 'Terminal emergency-purchase records are immutable.', 1;

                IF EXISTS (
                    SELECT 1 FROM inserted i JOIN deleted d ON d.Id = i.Id
                    WHERE i.Status <> d.Status AND NOT (
                        (d.Status = 'Draft' AND i.Status = 'Prepared') OR
                        (d.Status = 'Prepared' AND i.Status = 'PendingAudit') OR
                        (d.Status = 'PendingAudit' AND i.Status = 'AuditVouched') OR
                        (d.Status = 'AuditVouched' AND i.Status = 'PendingApproval') OR
                        (d.Status = 'PendingApproval' AND i.Status IN ('Approved','Rejected')) OR
                        (d.Status = 'Approved' AND i.Status = 'Triggered') OR
                        (d.Status = 'Triggered' AND i.Status = 'Filed')))
                    THROW 51873, 'Invalid emergency-purchase lifecycle transition.', 1;

                IF EXISTS (
                    SELECT 1 FROM inserted i JOIN deleted d ON d.Id = i.Id
                    WHERE d.Status <> 'Draft' AND (
                        i.TenantId <> d.TenantId OR
                        ISNULL(i.PurchaseRequisitionId, '00000000-0000-0000-0000-000000000000') <> ISNULL(d.PurchaseRequisitionId, '00000000-0000-0000-0000-000000000000') OR
                        ISNULL(i.ExceptionRuleId, '00000000-0000-0000-0000-000000000000') <> ISNULL(d.ExceptionRuleId, '00000000-0000-0000-0000-000000000000') OR
                        ISNULL(i.WorkflowDefinitionId, '00000000-0000-0000-0000-000000000000') <> ISNULL(d.WorkflowDefinitionId, '00000000-0000-0000-0000-000000000000') OR
                        ISNULL(i.CentralDocumentVersionId, '00000000-0000-0000-0000-000000000000') <> ISNULL(d.CentralDocumentVersionId, '00000000-0000-0000-0000-000000000000') OR
                        ISNULL(i.FileUploadRecordId, '00000000-0000-0000-0000-000000000000') <> ISNULL(d.FileUploadRecordId, '00000000-0000-0000-0000-000000000000') OR
                        ISNULL(i.PreparedById, '00000000-0000-0000-0000-000000000000') <> ISNULL(d.PreparedById, '00000000-0000-0000-0000-000000000000') OR
                        ISNULL(i.PreparedAtUtc, '19000101') <> ISNULL(d.PreparedAtUtc, '19000101') OR
                        ISNULL(i.ExceptionJustification, '') <> ISNULL(d.ExceptionJustification, '') OR
                        ISNULL(i.EvidenceReference, '') <> ISNULL(d.EvidenceReference, '') OR
                        ISNULL(i.ApprovalAuthority, '') <> ISNULL(d.ApprovalAuthority, '')))
                    THROW 51874, 'Emergency-purchase subject, policy, evidence, and preparer lineage is immutable.', 1;

                IF EXISTS (
                    SELECT 1 FROM inserted i JOIN deleted d ON d.Id = i.Id
                    WHERE d.Status <> 'Draft' AND (
                        i.PlanCode <> d.PlanCode OR i.Title <> d.Title OR
                        ISNULL(i.Description, '') <> ISNULL(d.Description, '') OR
                        ISNULL(i.DepartmentId, '00000000-0000-0000-0000-000000000000') <> ISNULL(d.DepartmentId, '00000000-0000-0000-0000-000000000000') OR
                        i.EmergencyType <> d.EmergencyType OR i.CriticalityLevel <> d.CriticalityLevel OR
                        i.BudgetReserve <> d.BudgetReserve OR i.UtilizedReserve <> d.UtilizedReserve OR
                        i.Currency <> d.Currency OR i.MaxApprovalLimit <> d.MaxApprovalLimit OR
                        ISNULL(i.RapidProcurementProcess, '') <> ISNULL(d.RapidProcurementProcess, '') OR
                        ISNULL(i.EscalationContacts, '') <> ISNULL(d.EscalationContacts, '') OR
                        ISNULL(i.EffectiveDate, '19000101') <> ISNULL(d.EffectiveDate, '19000101') OR
                        ISNULL(i.ExpiryDate, '19000101') <> ISNULL(d.ExpiryDate, '19000101') OR
                        ISNULL(i.NextReviewDate, '19000101') <> ISNULL(d.NextReviewDate, '19000101') OR
                        ISNULL(i.Notes, '') <> ISNULL(d.Notes, '')))
                    THROW 51875, 'Emergency-purchase configuration is immutable after preparation.', 1;

                IF EXISTS (
                    SELECT 1 FROM inserted i JOIN deleted d ON d.Id = i.Id
                    WHERE d.Status IN ('AuditVouched','PendingApproval','Approved','Triggered','Filed') AND (
                        ISNULL(i.InternalAuditVouchedById, '00000000-0000-0000-0000-000000000000') <> ISNULL(d.InternalAuditVouchedById, '00000000-0000-0000-0000-000000000000') OR
                        ISNULL(i.InternalAuditVouchedAtUtc, '19000101') <> ISNULL(d.InternalAuditVouchedAtUtc, '19000101') OR
                        ISNULL(i.InternalAuditVouchNote, '') <> ISNULL(d.InternalAuditVouchNote, '')))
                    THROW 51876, 'Emergency-purchase Internal Audit lineage is immutable after vouch.', 1;

                IF EXISTS (
                    SELECT 1 FROM inserted i JOIN deleted d ON d.Id = i.Id
                    WHERE d.Status IN ('Approved','Triggered','Filed') AND (
                        ISNULL(i.ApprovedById, '00000000-0000-0000-0000-000000000000') <> ISNULL(d.ApprovedById, '00000000-0000-0000-0000-000000000000') OR
                        ISNULL(i.ApprovedDate, '19000101') <> ISNULL(d.ApprovedDate, '19000101') OR
                        ISNULL(i.ApprovalReference, '') <> ISNULL(d.ApprovalReference, '')))
                    THROW 51877, 'Emergency-purchase authority approval lineage is immutable.', 1;
            END;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP TRIGGER IF EXISTS dbo.TR_EmergencyProcurementPlans_Governance;");
        migrationBuilder.DropCheckConstraint("CK_EmergencyProcurementPlans_FilingLineage", "EmergencyProcurementPlans");
        migrationBuilder.DropCheckConstraint("CK_EmergencyProcurementPlans_ApprovalLineage", "EmergencyProcurementPlans");
        migrationBuilder.DropCheckConstraint("CK_EmergencyProcurementPlans_AuditLineage", "EmergencyProcurementPlans");
        migrationBuilder.DropCheckConstraint("CK_EmergencyProcurementPlans_PreparedLineage", "EmergencyProcurementPlans");
        migrationBuilder.DropCheckConstraint("CK_EmergencyProcurementPlans_GovernedStatus", "EmergencyProcurementPlans");

        foreach (var name in ForeignKeys)
            migrationBuilder.DropForeignKey(name, "EmergencyProcurementPlans");
        foreach (var name in Indexes)
            migrationBuilder.DropIndex(name, "EmergencyProcurementPlans");
        foreach (var name in Columns)
            migrationBuilder.DropColumn(name, "EmergencyProcurementPlans");
        migrationBuilder.CreateIndex(name: "IX_EmergencyProcurementPlans_PlanCode", table: "EmergencyProcurementPlans", column: "PlanCode", unique: true);
    }

    private static void AddForeignKey(MigrationBuilder migrationBuilder, string name, string column, string principalTable) =>
        migrationBuilder.AddForeignKey(name: name, table: "EmergencyProcurementPlans", column: column,
            principalTable: principalTable, principalColumn: "Id", onDelete: ReferentialAction.Restrict);

    private static readonly string[] ForeignKeys =
    {
        "FK_EmergencyProcurementPlans_PurchaseRequisitions_PurchaseRequisitionId",
        "FK_EmergencyProcurementPlans_ProcurementPolicyExceptionRules_ExceptionRuleId",
        "FK_EmergencyProcurementPlans_WorkflowDefinitions_WorkflowDefinitionId",
        "FK_EmergencyProcurementPlans_WorkflowInstances_WorkflowInstanceId",
        "FK_EmergencyProcurementPlans_CentralDocumentVersions_CentralDocumentVersionId",
        "FK_EmergencyProcurementPlans_CentralDocumentVersions_PostAwardCentralDocumentVersionId",
        "FK_EmergencyProcurementPlans_FileUploadRecords_FileUploadRecordId",
        "FK_EmergencyProcurementPlans_FileUploadRecords_PostAwardFileUploadRecordId",
        "FK_EmergencyProcurementPlans_Tenders_ExceptionalSourcingTenderId",
        "FK_EmergencyProcurementPlans_Users_PreparedById",
        "FK_EmergencyProcurementPlans_Users_InternalAuditVouchedById",
        "FK_EmergencyProcurementPlans_Users_FiledById"
    };

    private static readonly string[] Indexes =
    {
        "IX_EmergencyProcurementPlans_TenantId_PlanCode",
        "IX_EmergencyProcurementPlans_TenantId_PurchaseRequisitionId",
        "IX_EmergencyProcurementPlans_TenantId_ExceptionRuleId",
        "IX_EmergencyProcurementPlans_TenantId_WorkflowInstanceId",
        "IX_EmergencyProcurementPlans_TenantId_ExceptionalSourcingTenderId",
        "IX_EmergencyProcurementPlans_CentralDocumentVersionId",
        "IX_EmergencyProcurementPlans_PostAwardCentralDocumentVersionId",
        "IX_EmergencyProcurementPlans_FileUploadRecordId",
        "IX_EmergencyProcurementPlans_PostAwardFileUploadRecordId",
        "IX_EmergencyProcurementPlans_WorkflowDefinitionId",
        "IX_EmergencyProcurementPlans_PreparedById",
        "IX_EmergencyProcurementPlans_InternalAuditVouchedById",
        "IX_EmergencyProcurementPlans_FiledById"
    };

    private static readonly string[] Columns =
    {
        "ApprovalAuthority", "ApprovalReference", "CentralDocumentVersionId", "EvidenceReference",
        "ExceptionRuleId", "ExceptionJustification", "ExceptionalSourcingTenderId", "FileUploadRecordId",
        "FiledAtUtc", "FiledById", "IntegrityHash", "InternalAuditVouchNote", "InternalAuditVouchedAtUtc",
        "InternalAuditVouchedById", "LifecycleSnapshotJson", "PostAwardCentralDocumentVersionId",
        "PostAwardEvidenceReference", "PostAwardFileUploadRecordId", "PostAwardJustification",
        "PreparedAtUtc", "PreparedById", "PurchaseRequisitionId", "RowVersion",
        "SubmittedForApprovalAtUtc", "SubmittedForAuditAtUtc", "WorkflowDefinitionId", "WorkflowInstanceId"
    };
}
