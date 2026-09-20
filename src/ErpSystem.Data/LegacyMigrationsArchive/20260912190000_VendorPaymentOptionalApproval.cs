using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260912190000_VendorPaymentOptionalApproval")]
public sealed class VendorPaymentOptionalApproval : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>("ApprovalRequired", "VendorPayment", "bit", nullable: false, defaultValue: true);
        migrationBuilder.AddColumn<bool>("ApprovalRequired", "PaymentBatch", "bit", nullable: false, defaultValue: true);
        Patch(migrationBuilder, "TR_VendorPayment_TDC0506InvoiceProcessorSod",
            "WHERE payment.IsDeleted = 0", "WHERE payment.IsDeleted = 0 AND payment.ApprovalRequired = 1");
        Patch(migrationBuilder, "TR_PaymentBatch_TDC0506InvoiceProcessorSod",
            "WHERE batch.IsDeleted = 0", "WHERE batch.IsDeleted = 0 AND batch.ApprovalRequired = 1");
        Patch(migrationBuilder, "TR_PaymentBatch_TDC0505Readiness",
            "OR (batch.Status = 3 AND NOT EXISTS (", "OR (batch.ApprovalRequired = 1 AND batch.Status = 3 AND NOT EXISTS (");
        Patch(migrationBuilder, "TR_PaymentBatch_TDC0505Readiness",
            "CASE WHEN batch.Status = 3 THEN 'PaymentBatchInvoiceApproved' ELSE 'PaymentBatchInvoiceProcessed' END",
            "CASE WHEN batch.Status = 3 AND batch.ApprovalRequired = 0 THEN 'PaymentBatchInvoiceSelected' WHEN batch.Status = 3 THEN 'PaymentBatchInvoiceApproved' ELSE 'PaymentBatchInvoiceProcessed' END");

        migrationBuilder.Sql("""
            CREATE OR ALTER TRIGGER dbo.TR_PaymentBatch_OptionalApproval
            ON dbo.PaymentBatch AFTER INSERT, UPDATE AS
            BEGIN
                SET NOCOUNT ON;
                IF EXISTS (SELECT 1 FROM inserted i LEFT JOIN deleted d ON d.Id=i.Id
                    WHERE (d.Id IS NOT NULL AND (i.ApprovalRequired<>d.ApprovalRequired OR i.TenantId<>d.TenantId))
                       OR (d.Id IS NULL AND i.ApprovalRequired=0 AND
                           (i.Status<>3 OR i.CreatedById IS NULL OR i.CreatedById='00000000-0000-0000-0000-000000000000'
                            OR dbo.WorkflowApprovalRequiredAtSubmission(i.TenantId,N'PaymentBatch',i.Id)<>0)))
                    THROW 51890, 'The batch approval requirement is captured only when the batch is created.', 1;
                IF EXISTS (SELECT 1 FROM inserted WHERE ApprovalRequired=0 AND
                    (Status IN (1,2) OR ApprovedById IS NOT NULL OR ApprovedDate IS NOT NULL OR InvoicePaymentSodControlEventId IS NOT NULL))
                    THROW 51891, 'A directly completed payment batch cannot claim human approval.', 1;
                IF EXISTS (SELECT 1 FROM inserted i JOIN deleted d ON d.Id=i.Id WHERE d.ApprovalRequired=0 AND
                    (i.TotalAmount<>d.TotalAmount OR i.PaymentCount<>d.PaymentCount OR
                     ISNULL(i.CreatedById,'00000000-0000-0000-0000-000000000000')<>ISNULL(d.CreatedById,'00000000-0000-0000-0000-000000000000') OR
                     ISNULL(i.BankAccountId,'00000000-0000-0000-0000-000000000000')<>ISNULL(d.BankAccountId,'00000000-0000-0000-0000-000000000000')))
                    THROW 51892, 'A directly completed batch must retain its exact payment source.', 1;
            END
            """);
        migrationBuilder.Sql("""
            CREATE OR ALTER TRIGGER dbo.TR_VendorPayment_OptionalApproval
            ON dbo.VendorPayment AFTER INSERT, UPDATE AS
            BEGIN
                SET NOCOUNT ON;
                IF EXISTS (SELECT 1 FROM inserted i LEFT JOIN deleted d ON d.Id=i.Id
                    WHERE (d.Id IS NOT NULL AND i.TenantId<>d.TenantId)
                       OR (d.Id IS NOT NULL AND i.ApprovalRequired<>d.ApprovalRequired AND NOT
                           (d.ApprovalRequired=1 AND i.ApprovalRequired=0 AND d.Status=1 AND i.Status=3
                            AND d.PaymentBatchId IS NULL AND i.PaymentBatchId IS NULL
                            AND d.WorkflowInstanceId IS NULL AND d.AuthorizedById IS NULL AND d.AuthorizedDate IS NULL
                            AND d.InvoicePaymentSodControlEventId IS NULL AND d.JournalEntryId IS NULL
                            AND dbo.WorkflowApprovalRequiredAtSubmission(i.TenantId,N'VendorPayment',i.Id)=0))
                       OR (d.Id IS NULL AND i.ApprovalRequired=0 AND
                           (i.Status<>3 OR i.PaymentBatchId IS NULL OR NOT EXISTS
                             (SELECT 1 FROM dbo.PaymentBatch b WHERE b.Id=i.PaymentBatchId AND b.TenantId=i.TenantId
                              AND b.IsDeleted=0 AND b.ApprovalRequired=0 AND b.Status=3)
                            OR dbo.WorkflowApprovalRequiredAtSubmission(i.TenantId,N'PaymentBatch',i.PaymentBatchId)<>0)))
                    THROW 51893, 'The payment approval requirement must be captured by its initial submission or owning batch.', 1;
                IF EXISTS (SELECT 1 FROM inserted WHERE ApprovalRequired=0 AND
                    (Status IN (1,2) OR AuthorizedById IS NOT NULL OR AuthorizedDate IS NOT NULL OR WorkflowInstanceId IS NOT NULL
                     OR InvoicePaymentSodControlEventId IS NOT NULL OR SubmittedById IS NULL OR SubmittedAt IS NULL
                     OR SubmittedById='00000000-0000-0000-0000-000000000000'
                     OR IsExceptionalPayment=1 OR RequiresManagingDirectorApproval=1 OR EvidenceExceptionRequested=1
                     OR ManagingDirectorApprovedById IS NOT NULL OR ManagingDirectorApprovedAt IS NOT NULL
                     OR EvidenceExceptionApprovedById IS NOT NULL OR EvidenceExceptionApprovedAt IS NOT NULL
                     OR ISJSON(ApprovalControlSnapshotJson)<>1 OR ApprovalControlSnapshotJson IS NULL
                     OR ApprovalControlSnapshotHash IS NULL OR LEN(ApprovalControlSnapshotHash)<>64))
                    THROW 51894, 'A directly completed payment must retain policy evidence and cannot claim human or exceptional authority.', 1;
                IF EXISTS (SELECT 1 FROM inserted i WHERE i.ApprovalRequired=0 AND
                    (ISNULL(JSON_VALUE(i.ApprovalControlSnapshotJson,'$.requiresManagingDirectorApproval'),'true')<>'false'
                     OR JSON_VALUE(i.ApprovalControlSnapshotJson,'$.signaturePolicy.isRequired')='true'
                     OR EXISTS (SELECT 1 FROM OPENJSON(i.ApprovalControlSnapshotJson,'$.evidenceRequirements') e
                                WHERE NULLIF(LTRIM(RTRIM(JSON_VALUE(e.value,'$.requirementKey'))),'') IS NOT NULL)))
                    THROW 51895, 'Separate payment authority, evidence and signature requirements cannot be bypassed.', 1;
                IF EXISTS (SELECT 1 FROM inserted i JOIN deleted d ON d.Id=i.Id WHERE d.ApprovalRequired=0 AND
                    (i.TotalAmount<>d.TotalAmount OR i.ExchangeRate<>d.ExchangeRate OR i.CurrencyCode<>d.CurrencyCode
                     OR i.SupplierId<>d.SupplierId OR i.PaymentDate<>d.PaymentDate
                     OR ISNULL(i.PaymentBatchId,'00000000-0000-0000-0000-000000000000')<>ISNULL(d.PaymentBatchId,'00000000-0000-0000-0000-000000000000')
                     OR ISNULL(i.BankAccountId,'00000000-0000-0000-0000-000000000000')<>ISNULL(d.BankAccountId,'00000000-0000-0000-0000-000000000000')
                     OR i.SubmittedById<>d.SubmittedById OR i.SubmittedAt<>d.SubmittedAt
                     OR ISNULL(i.AppliedApprovalPolicySetId,'00000000-0000-0000-0000-000000000000')<>ISNULL(d.AppliedApprovalPolicySetId,'00000000-0000-0000-0000-000000000000')
                     OR ISNULL(i.AppliedApprovalPolicyCode,'')<>ISNULL(d.AppliedApprovalPolicyCode,'')
                     OR i.ApprovalControlSnapshotJson<>d.ApprovalControlSnapshotJson
                     OR i.ApprovalControlSnapshotHash<>d.ApprovalControlSnapshotHash))
                    THROW 51896, 'The payment no-approval source and policy snapshot are immutable.', 1;
                IF EXISTS (SELECT 1 FROM inserted i WHERE i.ApprovalRequired=0 AND i.PaymentBatchId IS NOT NULL AND NOT EXISTS
                    (SELECT 1 FROM dbo.PaymentBatch b WHERE b.Id=i.PaymentBatchId AND b.TenantId=i.TenantId
                     AND b.IsDeleted=0 AND b.ApprovalRequired=0))
                    THROW 51897, 'The payment must retain its exact no-approval batch source.', 1;
                IF EXISTS (SELECT 1 FROM inserted i JOIN deleted d ON d.Id=i.Id
                    WHERE i.ApprovalRequired=0 AND i.PaymentBatchId IS NOT NULL
                      AND d.JournalEntryId IS NULL AND i.JournalEntryId IS NOT NULL AND
                      (NOT EXISTS (SELECT 1 FROM dbo.PaymentBatch b WHERE b.Id=i.PaymentBatchId
                                   AND b.TenantId=i.TenantId AND b.IsDeleted=0 AND b.Status=4)
                       OR NOT EXISTS (SELECT 1 FROM dbo.PaymentBatchInvoice s WHERE s.VendorPaymentId=i.Id
                                      AND s.PaymentBatchId=i.PaymentBatchId AND s.TenantId=i.TenantId AND s.IsDeleted=0)
                       OR EXISTS (SELECT 1 FROM dbo.PaymentBatchInvoice s WHERE s.VendorPaymentId=i.Id
                                  AND s.PaymentBatchId=i.PaymentBatchId AND s.TenantId=i.TenantId AND s.IsDeleted=0
                                  AND s.Amount<>(SELECT ISNULL(SUM(a.AllocatedAmount+a.DiscountAmount+a.WithholdingTaxAmount),0)
                                    FROM dbo.VendorPaymentAllocation a WHERE a.VendorPaymentId=i.Id
                                      AND a.VendorInvoiceId=s.VendorInvoiceId AND a.TenantId=i.TenantId
                                      AND a.IsDeleted=0 AND a.IsReversal=0 AND NOT EXISTS
                                        (SELECT 1 FROM dbo.VendorPaymentAllocation r WHERE r.OriginalAllocationId=a.Id
                                         AND r.TenantId=a.TenantId AND r.VendorPaymentId=a.VendorPaymentId
                                         AND r.IsDeleted=0 AND r.IsReversal=1)))))
                    THROW 51897, 'A batch payment must post from its processing batch and exact invoice allocations.', 1;
            END
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            IF EXISTS (SELECT 1 FROM dbo.VendorPayment WHERE ApprovalRequired=0)
                OR EXISTS (SELECT 1 FROM dbo.PaymentBatch WHERE ApprovalRequired=0)
                THROW 51898, 'No-approval payment history must be retained; this rollback is unsafe.', 1;
            DROP TRIGGER IF EXISTS dbo.TR_VendorPayment_OptionalApproval;
            DROP TRIGGER IF EXISTS dbo.TR_PaymentBatch_OptionalApproval;
            """);
        Patch(migrationBuilder, "TR_VendorPayment_TDC0506InvoiceProcessorSod",
            "WHERE payment.IsDeleted = 0 AND payment.ApprovalRequired = 1", "WHERE payment.IsDeleted = 0");
        Patch(migrationBuilder, "TR_PaymentBatch_TDC0506InvoiceProcessorSod",
            "WHERE batch.IsDeleted = 0 AND batch.ApprovalRequired = 1", "WHERE batch.IsDeleted = 0");
        Patch(migrationBuilder, "TR_PaymentBatch_TDC0505Readiness",
            "OR (batch.ApprovalRequired = 1 AND batch.Status = 3 AND NOT EXISTS (", "OR (batch.Status = 3 AND NOT EXISTS (");
        Patch(migrationBuilder, "TR_PaymentBatch_TDC0505Readiness",
            "CASE WHEN batch.Status = 3 AND batch.ApprovalRequired = 0 THEN 'PaymentBatchInvoiceSelected' WHEN batch.Status = 3 THEN 'PaymentBatchInvoiceApproved' ELSE 'PaymentBatchInvoiceProcessed' END",
            "CASE WHEN batch.Status = 3 THEN 'PaymentBatchInvoiceApproved' ELSE 'PaymentBatchInvoiceProcessed' END");
        migrationBuilder.DropColumn("ApprovalRequired", "VendorPayment");
        migrationBuilder.DropColumn("ApprovalRequired", "PaymentBatch");
    }

    private static void Patch(MigrationBuilder builder, string name, string before, string after) =>
        builder.Sql($"""
            DECLARE @definition nvarchar(max)=OBJECT_DEFINITION(OBJECT_ID(N'dbo.{name}'));
            DECLARE @before nvarchar(max)=N'{before.Replace("'", "''")}';
            DECLARE @after nvarchar(max)=N'{after.Replace("'", "''")}';
            IF @definition IS NULL OR (LEN(@definition)-LEN(REPLACE(@definition,@before,N'')))/LEN(@before)<>1
                THROW 51899, 'Payment guard differs from the reviewed definition; manual review is required.', 1;
            SET @definition=REPLACE(@definition,@before,@after);
            DECLARE @trigger int=CHARINDEX(N'TRIGGER',@definition);
            IF @trigger=0 THROW 51899, 'Payment guard declaration is missing.', 1;
            SET @definition=N'ALTER '+SUBSTRING(@definition,@trigger,LEN(@definition));
            EXEC sys.sp_executesql @definition;
            """);
}
