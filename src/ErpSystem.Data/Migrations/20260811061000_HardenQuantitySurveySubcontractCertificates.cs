using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260811061000_HardenQuantitySurveySubcontractCertificates")]
public partial class HardenQuantitySurveySubcontractCertificates : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE OR ALTER TRIGGER [dbo].[TR_QS0521_SubcontractCertificates_Governance]
            ON [dbo].[ProjectPaymentCertificates]
            AFTER INSERT, UPDATE, DELETE
            AS
            BEGIN
                SET NOCOUNT ON;

                IF EXISTS (
                    SELECT 1 FROM deleted d LEFT JOIN inserted i ON i.Id = d.Id
                    WHERE d.QuantitySurveySubcontractValuationId IS NOT NULL AND i.Id IS NULL)
                    THROW 52031, 'Governed QS subcontract certificates cannot be physically deleted.', 1;

                IF EXISTS (
                    SELECT 1 FROM inserted i
                    LEFT JOIN dbo.QuantitySurveySubcontractValuations v ON v.Id = i.QuantitySurveySubcontractValuationId
                        AND v.TenantId = i.TenantId AND v.IsDeleted = 0
                    LEFT JOIN dbo.QuantitySurveySubcontracts s ON s.Id = v.SubcontractId
                        AND s.TenantId = i.TenantId AND s.IsDeleted = 0
                    WHERE i.QuantitySurveySubcontractValuationId IS NOT NULL AND (
                        v.Id IS NULL OR s.Id IS NULL OR i.ProjectId <> s.ProjectId OR i.ContractId <> s.ContractId
                        OR i.SubcontractorBusinessPartnerId <> s.SubcontractorBusinessPartnerId
                        OR i.ConfigurationProfileId <> v.ConfigurationProfileId OR i.ValuationDecisionId <> v.ValuationDecisionId
                        OR i.ApprovalWorkflowDefinitionId <> v.ApprovalWorkflowDefinitionId OR i.WorkflowInstanceId <> v.WorkflowInstanceId
                        OR i.PaymentTermId <> s.PaymentTermId OR i.PolicyHash <> v.PolicyHash
                        OR i.CertifiedToDateAmount <> v.AssessedToDateAmount OR i.PreviouslyCertifiedAmount <> v.PreviouslyCertifiedAmount
                        OR i.GrossCertifiedAmount <> v.CurrentCertifiedAmount OR i.RetentionHeldAmount <> v.RetentionHeldAmount
                        OR i.RetentionReleasedAmount <> v.RetentionReleasedAmount
                        OR i.OtherDeductionsAmount <> v.ApprovedBackChargeAmount + v.ApprovedContraChargeAmount
                        OR i.TaxAmount <> v.TaxAmount OR i.NetCertifiedAmount <> v.NetCertifiedAmount))
                    THROW 52032, 'QS subcontract certificate source, project, supplier, policy or financial lineage is invalid.', 1;

                IF EXISTS (
                    SELECT 1 FROM inserted i
                    WHERE i.QuantitySurveySubcontractValuationId IS NOT NULL
                      AND ROUND(i.NetCertifiedAmount, 2) <> ROUND(
                        i.GrossCertifiedAmount + i.RetentionReleasedAmount
                        + CASE WHEN i.TaxHandling = 'Inclusive' THEN 0 ELSE i.TaxAmount END
                        - i.RetentionHeldAmount - i.AdvanceRecoveryAmount - i.MaterialDeductionAmount - i.OtherDeductionsAmount, 2))
                    THROW 52033, 'QS subcontract certificate totals do not reconcile.', 1;

                IF EXISTS (
                    SELECT 1 FROM inserted i JOIN deleted d ON d.Id = i.Id
                    WHERE d.QuantitySurveySubcontractValuationId IS NOT NULL AND (
                        i.TenantId <> d.TenantId OR i.ProjectId <> d.ProjectId
                        OR i.QuantitySurveySubcontractValuationId <> d.QuantitySurveySubcontractValuationId
                        OR i.ContractId <> d.ContractId OR i.SubcontractorBusinessPartnerId <> d.SubcontractorBusinessPartnerId
                        OR i.ConfigurationProfileId <> d.ConfigurationProfileId OR i.ValuationDecisionId <> d.ValuationDecisionId
                        OR i.ApprovalWorkflowDefinitionId <> d.ApprovalWorkflowDefinitionId OR i.WorkflowInstanceId <> d.WorkflowInstanceId
                        OR i.ExpenseAccountId <> d.ExpenseAccountId OR i.AccountsPayableAccountId <> d.AccountsPayableAccountId
                        OR i.PaymentTermId <> d.PaymentTermId OR i.TaxGroupId <> d.TaxGroupId
                        OR ISNULL(i.WithholdingTaxId, '00000000-0000-0000-0000-000000000000') <> ISNULL(d.WithholdingTaxId, '00000000-0000-0000-0000-000000000000')
                        OR i.CertificateNumber <> d.CertificateNumber OR i.ClientRequestId <> d.ClientRequestId
                        OR i.RequestHash <> d.RequestHash OR i.PolicyHash <> d.PolicyHash OR i.Currency <> d.Currency
                        OR i.CertifiedToDateAmount <> d.CertifiedToDateAmount OR i.PreviouslyCertifiedAmount <> d.PreviouslyCertifiedAmount
                        OR i.GrossCertifiedAmount <> d.GrossCertifiedAmount OR i.RetentionHeldAmount <> d.RetentionHeldAmount
                        OR i.RetentionReleasedAmount <> d.RetentionReleasedAmount OR i.OtherDeductionsAmount <> d.OtherDeductionsAmount
                        OR i.TaxAmount <> d.TaxAmount OR i.NetCertifiedAmount <> d.NetCertifiedAmount))
                    THROW 52034, 'Approved QS subcontract certificate source, policy and financial lineage is immutable.', 1;

                IF EXISTS (
                    SELECT 1 FROM inserted i
                    WHERE i.QuantitySurveySubcontractValuationId IS NOT NULL AND i.VendorInvoiceId IS NOT NULL
                      AND NOT EXISTS (SELECT 1 FROM dbo.VendorInvoice v WHERE v.Id = i.VendorInvoiceId AND v.TenantId = i.TenantId AND v.IsDeleted = 0))
                    THROW 52035, 'The linked Finance AP invoice must belong to the same tenant.', 1;
            END
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_QS0521_SubcontractCertificates_Governance];");
}
