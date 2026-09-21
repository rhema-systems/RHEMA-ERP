using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260920124000_AllowQuantitySurveyCertificateReviewProgress")]
public sealed class AllowQuantitySurveyCertificateReviewProgress : Migration
{
    public const string ReconciliationSql = """
        DECLARE @guard nvarchar(max) = OBJECT_DEFINITION(OBJECT_ID(N'dbo.TR_ProjectPaymentCertificates_QsLifecycle'));
        IF @guard IS NULL
            THROW 52036, 'The existing QS certificate lifecycle guard is required before review alignment.', 1;
        DECLARE @anchor nvarchar(400) = N'(d.Status = ''Issued'' AND d.ApprovalStatus = ''Pending'' AND i.Status = ''Approved'' AND i.ApprovalStatus = ''Approved'')';
        DECLARE @review nvarchar(2000) = N'(d.Status = ''Issued'' AND d.ApprovalStatus = ''Pending'' AND i.Status = ''Issued'' AND i.ApprovalStatus = ''Pending''
            AND i.WorkflowInstanceId = d.WorkflowInstanceId AND i.NetCertifiedAmount = d.NetCertifiedAmount
            AND i.TaxAmount = d.TaxAmount AND i.AdvanceRecoveryAmount = d.AdvanceRecoveryAmount
            AND i.MaterialDeductionAmount = d.MaterialDeductionAmount AND i.OtherDeductionsAmount = d.OtherDeductionsAmount)';
        IF CHARINDEX(@review, @guard) = 0
        BEGIN
            IF CHARINDEX(@anchor, @guard) = 0
                THROW 52037, 'The QS certificate guard differs from the expected version; review before applying.', 1;
            SET @guard = REPLACE(@guard, @anchor, @review + N' OR ' + @anchor);
            SET @guard = REPLACE(@guard, N'CREATE OR ALTER TRIGGER', N'ALTER TRIGGER');
            SET @guard = REPLACE(@guard, N'CREATE TRIGGER', N'ALTER TRIGGER');
            SET @guard = REPLACE(@guard, N'CREATE   TRIGGER', N'ALTER TRIGGER');
            EXEC sys.sp_executesql @guard;
        END;
        """;

    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql(ReconciliationSql);

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Retain compatibility with certificates already progressing through multiple approvals.
    }
}
