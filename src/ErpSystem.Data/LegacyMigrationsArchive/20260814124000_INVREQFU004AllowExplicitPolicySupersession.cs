using ErpSystem.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

/// <summary>
/// Permits the explicit, audited policy-supersession terminal state without inventing a
/// successor review reference. Normal replacement supersession remains linked to its successor.
/// </summary>
[DbContext(typeof(ApplicationDbContext))]
[Migration("20260814124000_INVREQFU004AllowExplicitPolicySupersession")]
public partial class INVREQFU004AllowExplicitPolicySupersession : Migration
{
    private const string ConstraintName = "CK_ProcurementSupplierDueDiligenceReviews_State";
    private const string Common = "[CycleNumber] >= 1 AND [ReviewType] BETWEEN 0 AND 1 AND [Status] BETWEEN 0 AND 5 AND [Outcome] BETWEEN 0 AND 2 AND [ReviewPeriodEndUtc] > [ReviewPeriodStartUtc] AND [ReviewFrequencyMonths] BETWEEN 1 AND 120 AND [PolicyProfileVersion] >= 1 AND LEN([PolicyValueHash]) = 64 AND LEN([CreationCorrelationId]) > 0 AND LEN([LastOperationCorrelationId]) > 0 AND LEN([IntegrityHash]) = 64 AND ISJSON([PolicySnapshotJson]) = 1 AND ISJSON([SnapshotJson]) = 1 AND (([Status] = 0 AND [Outcome] = 0) OR [Status] <> 0) AND (([Status] = 1 AND [SubmittedById] IS NOT NULL AND [SubmittedAtUtc] IS NOT NULL) OR [Status] <> 1) AND (([Status] = 2 AND [ApprovedById] IS NOT NULL AND [ApprovedAtUtc] IS NOT NULL) OR [Status] <> 2) AND (([Status] = 3 AND [RejectedById] IS NOT NULL AND [RejectedAtUtc] IS NOT NULL) OR [Status] <> 3) AND (([Status] = 4 AND [ExpiredAtUtc] IS NOT NULL) OR [Status] <> 4) AND ";

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint(ConstraintName, "ProcurementSupplierDueDiligenceReviews");
        migrationBuilder.AddCheckConstraint(
            ConstraintName,
            "ProcurementSupplierDueDiligenceReviews",
            Common + "(([Status] = 5 AND ([SupersededByReviewId] IS NOT NULL OR [LastOperation] = N'PolicySuperseded')) OR [Status] <> 5)");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint(ConstraintName, "ProcurementSupplierDueDiligenceReviews");
        migrationBuilder.AddCheckConstraint(
            ConstraintName,
            "ProcurementSupplierDueDiligenceReviews",
            Common + "(([Status] = 5 AND [SupersededByReviewId] IS NOT NULL) OR [Status] <> 5)");
    }
}
