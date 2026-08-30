using System.Reflection;
using ErpSystem.Data.Migrations;
using FluentAssertions;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Procurement;

public sealed class ProcurementSourceEntryGuardMigrationTests
{
    [Fact]
    public void SourceEntryGuardsRequireApprovedPrAndReleaseButKeepSourcingCaseOptional()
    {
        var sql = Sql();

        sql.Should().Contain("TR_RequestForQuotations_SourcingReleaseGuard");
        sql.Should().Contain("TR_Tenders_SourcingReleaseGuard");
        sql.Should().Contain("pr.[Status] <> 'Approved'");
        sql.Should().Contain("sr.[PurchaseRequisitionId] <> i.[SourcePurchaseRequisitionId]");
        sql.Should().Contain("matching optional sourcing case");
        sql.Should().NotContain("fully lotted");
        sql.Should().NotContain("ProcurementSourcingCaseLotItems");
        sql.Should().NotContain("ProcurementSourcingCaseSourceRequests");
        sql.Should().NotContain("ProcurementPolicySets");
    }

    [Fact]
    public void StatutoryRfqGuardAllowsOnlyMatureReleaseOnlyAwardTransition()
    {
        var migration = new AllowReleaseOnlyRfqAwardTransition();
        var builder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
        migration.GetType()
            .GetMethod("Up", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(migration, [builder]);
        var sql = string.Join(
            Environment.NewLine,
            builder.Operations.OfType<SqlOperation>().Select(item => item.Sql));

        sql.Should().Contain("TR_RequestForQuotations_StatutoryLifecycleGuard");
        sql.Should().Contain("d.Status = 'Sent'");
        sql.Should().Contain("i.[Status] = 'Awarded'");
        sql.Should().Contain("i.[SourcePurchaseRequisitionId] IS NOT NULL");
        sql.Should().Contain("i.[SourcingReleaseId] IS NOT NULL");
        sql.Should().Contain("i.[SourcingCaseId] IS NULL");
        sql.Should().Contain("i.[SubmissionDeadline] <= SYSUTCDATETIME()");
        sql.Should().Contain("RequestForQuotationAwardLines");
        sql.Should().Contain("i.[AwardedAt] IS NOT NULL");
    }

    [Fact]
    public void PurchaseOrderLineageConstraintSupportsOnlyDefinedDirectAndAdvancedRoutes()
    {
        var migration = new AllowReleaseOnlyPurchaseOrderSourceLineage();
        var builder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
        migration.GetType()
            .GetMethod("Up", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(migration, [builder]);

        var operation = builder.Operations
            .OfType<AddCheckConstraintOperation>()
            .Single(item =>
                item.Name == "CK_PurchaseOrders_ApprovedSourceLineage");
        var sql = operation.Sql;

        sql.Should().Contain("[SourceRequisitionId] IS NOT NULL");
        sql.Should().Contain("[SourcingReleaseId] IS NOT NULL");
        sql.Should().Contain("[SourcingCaseId] IS NOT NULL");
        sql.Should().Contain("[AwardReadinessDecisionId] IS NOT NULL");
        sql.Should().Contain("[ProcurementSourceType] = 0");
        sql.Should().Contain("[AwardReadinessDecisionId] IS NULL");
        sql.Should().Contain("[ProcurementSourceType] IN (1, 2)");
        sql.Should().Contain("[ProcurementSourceType] = 5");
        sql.Should().NotContain("[ProcurementSourceType] IN (0, 1, 2, 3, 4)");
    }

    [Fact]
    public void PurchaseOrderTriggerSupportsTheSameDirectAndAdvancedRoutesAsTheConstraint()
    {
        var migration = new AlignPurchaseOrderSourceTriggerWithSupportedRoutes();
        var builder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
        migration.GetType()
            .GetMethod("Up", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(migration, [builder]);

        var sql = builder.Operations.OfType<SqlOperation>().Single().Sql;

        sql.Should().Contain("TR_PurchaseOrders_ApprovedSourceProtected");
        sql.Should().Contain("TDC0406_PO_AMENDMENT_ID",
            "the migration must preserve the governed PO amendment exception");
        sql.Should().Contain("i.SourcingCaseId IS NULL");
        sql.Should().Contain("i.ProcurementSourceType = 0");
        sql.Should().Contain("i.ProcurementSourceType IN (1, 2)");
        sql.Should().Contain("i.AwardReadinessDecisionId IS NULL");
        sql.Should().Contain("i.AwardReadinessDecisionId IS NOT NULL");
        sql.Should().Contain("i.SourcingCaseId IS NOT NULL AND (");
        sql.Should().Contain("OR (i.AwardReadinessDecisionId IS NOT NULL");
        sql.Should().Contain("AND readiness.Id IS NULL)))");
        sql.Should().Contain("THROW 51202");
        sql.Should().Contain("THROW 51205");
    }

    [Fact]
    public void PurchaseOrderCommitmentGuardUsesApprovedRequisitionWhenReleaseSnapshotIsEmpty()
    {
        var migration = new AlignPurchaseOrderCommitmentWithRequisition();
        var builder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
        migration.GetType()
            .GetMethod("Up", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(migration, [builder]);
        var sql = string.Join(
            Environment.NewLine,
            builder.Operations.OfType<SqlOperation>().Select(item => item.Sql));

        sql.Should().Contain("TR_PurchaseOrders_GovernedCommitment");
        sql.Should().Contain("commitment.PurchaseRequisitionId = i.SourceRequisitionId");
        sql.Should().Contain("release.BudgetCommitmentId IS NULL");
        sql.Should().Contain("commitment.Id = release.BudgetCommitmentId");
        sql.Should().Contain("release.BudgetCommitmentReference IS NOT NULL");
        sql.Should().Contain("commitment.Status <> 1");
        sql.Should().Contain(") > commitment.ReservedAmount");
        sql.Should().Contain("AND i.Status IN ('Approved', 'Sent', 'Acknowledged')");
        sql.Should().NotContain("'Submitted', 'Pending Approval'",
            "submission checks availability but firm Finance exposure begins only at final approval");
        sql.Should().NotContain("LEFT JOIN deleted d",
            "all writes in governed statuses are checked while Draft writes are excluded by status");
    }

    private static string Sql()
    {
        var migration = new AllowReleaseOnlyProcurementSourceEntry();
        var builder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
        migration.GetType()
            .GetMethod("Up", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(migration, [builder]);
        return string.Join(
            Environment.NewLine,
            builder.Operations.OfType<SqlOperation>().Select(item => item.Sql));
    }
}
