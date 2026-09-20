using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260907161000_AllowUnpublishedFirstBindingScheduleApproval")]
public sealed class AllowUnpublishedFirstBindingScheduleApproval : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql(UpgradeGuard);

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql(
        "THROW 51206, 'First-binding schedule history is retained; rollback requires a reviewed recovery migration.', 1;");

    // The API atomically retains the expired source schedule and starts its pending
    // reschedule workflow. No source dates are rewritten on binding. Publication,
    // issuance and the independent schedule-approval triggers remain unchanged.
    internal const string UpgradeGuard = """
        DECLARE @definition nvarchar(max) = OBJECT_DEFINITION(OBJECT_ID(N'[dbo].[TR_ProcurementTenderDocumentRegisters_Immutable]'));
        DECLARE @old nvarchar(max) = N'OR i.OriginalSubmissionDeadlineUtc <= i.BoundAtUtc';
        IF @definition IS NULL OR CHARINDEX(@old, @definition) = 0
            THROW 51206, 'Unrecognized tender-document register deadline guard; no change applied.', 1;
        DECLARE @replacement nvarchar(max) = N'OR (i.OriginalSubmissionDeadlineUtc <= i.BoundAtUtc AND
            (i.SourceType <> 0 OR i.Method <> 1 OR t.Id IS NULL OR t.Status <> ''Approved''
             OR t.PublishDate IS NOT NULL OR t.PublishedById IS NOT NULL
             OR t.RequiresPrequalification = 1 OR t.UseQCBSEvaluation = 1
             OR i.OriginalBidValidityUntilUtc <= i.BoundAtUtc
             OR EXISTS (SELECT 1 FROM TenderBids b WHERE b.TenderId = t.Id AND b.TenantId = i.TenantId AND b.IsDeleted = 0)
             OR EXISTS (SELECT 1 FROM ProcurementTenderControls tc WHERE tc.TenderId = t.Id AND tc.TenantId = i.TenantId AND tc.IsDeleted = 0)))';
        SET @definition = REPLACE(@definition, @old, @replacement);
        SET @definition = LTRIM(@definition);
        WHILE UNICODE(LEFT(@definition, 1)) IN (9, 10, 13, 32) SET @definition = SUBSTRING(@definition, 2, LEN(@definition));
        DECLARE @triggerPosition int = CHARINDEX(N'trigger', LOWER(@definition));
        DECLARE @header nvarchar(100) = LOWER(REPLACE(REPLACE(REPLACE(REPLACE(
            LEFT(@definition, CASE WHEN @triggerPosition > 0 THEN @triggerPosition - 1 ELSE 0 END),
            CHAR(9), N''), CHAR(10), N''), CHAR(13), N''), N' ', N''));
        IF @triggerPosition = 0 OR @header NOT IN (N'create', N'createoralter', N'alter')
            THROW 51206, 'Unsupported register trigger header; no change applied.', 1;
        SET @definition = N'ALTER ' + SUBSTRING(@definition, @triggerPosition, LEN(@definition));
        EXEC sys.sp_executesql @definition;
        """;
}
