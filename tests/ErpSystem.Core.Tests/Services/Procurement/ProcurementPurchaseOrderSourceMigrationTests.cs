using System.Reflection;
using ErpSystem.Data.Migrations;
using FluentAssertions;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Procurement;

public sealed class ProcurementPurchaseOrderSourceMigrationTests
{
    [Fact]
    public void RfqSourceHardStopRequiresAwardLineOwnedByPurchaseOrderSupplier()
    {
        var initialSql = Sql(new TDC0403MandatoryPurchaseOrderSources());

        initialSql.Should().Contain(
            "FROM RequestForQuotationAwardLines awardLine");
        initialSql.Should().Contain(
            "awardLine.TenantId = i.TenantId");
        initialSql.Should().Contain(
            "awardLine.RfqId = rfq.Id");
        initialSql.Should().Contain(
            "awardLine.BusinessPartnerId = i.BusinessPartnerId");
        initialSql.Should().Contain(
            "awardLine.IsDeleted = 0");
        initialSql.Should().Contain("THROW 51207");
    }

    [Fact]
    public void OrdinarySourceHardStopUsesTenderReadinessAndContractEffectivePeriod()
    {
        var initialSql = Sql(new TDC0403MandatoryPurchaseOrderSources());

        initialSql.Should().Contain(
            "readiness.SourceId <> exceptional.TenderId");
        initialSql.Should().NotContain(
            "readiness.SourceId <> exceptional.Id");
        initialSql.Should().Contain(
            "contract.StartDate > SYSUTCDATETIME()");
        initialSql.Should().Contain(
            "contract.EndDate < SYSUTCDATETIME()");
    }

    [Fact]
    public void CorrectiveMigrationUpgradesAlreadyAppliedSourceTriggerFailClosed()
    {
        var correctiveSql = Sql(
            new TDC0403RfqAwardSupplierHardStop());

        correctiveSql.Should().Contain(
            "OBJECT_DEFINITION(OBJECT_ID(");
        correctiveSql.Should().Contain(
            "awardLine.BusinessPartnerId = i.BusinessPartnerId");
        correctiveSql.Should().Contain(
            "N'CREATE OR ALTER '");
        correctiveSql.Should().Contain(
            "CHARINDEX(N'TRIGGER', UPPER(@definition))");
        correctiveSql.Should().Contain(
            "EXEC sys.sp_executesql @definition");
        correctiveSql.Should().Contain("THROW 51216");
        correctiveSql.Should().NotContain("DISABLE TRIGGER");
    }

    [Fact]
    public void FrameworkTriggerUsesExceptionalTenderAndRejectsSupersededReadiness()
    {
        var initialSql = Sql(
            new TDC0403FrameworkLineageFailClosed());

        initialSql.Should().Contain(
            "exceptional.TenderId = readiness.SourceId");
        initialSql.Should().NotContain(
            "exceptional.Id = readiness.SourceId");
        initialSql.Should().Contain(
            "newer.SourceType = readiness.SourceType");
        initialSql.Should().Contain(
            "newer.SourceId = readiness.SourceId");
        initialSql.Should().Contain(
            "newer.DecisionSequence >");
        initialSql.Should().Contain(
            "readiness.DecisionSequence");
    }

    [Fact]
    public void CorrectiveFrameworkMigrationUpgradesAppliedTriggerFailClosed()
    {
        var correctiveSql = Sql(
            new TDC0403ExceptionalFrameworkLineage());

        correctiveSql.Should().Contain(
            "OBJECT_DEFINITION(");
        correctiveSql.Should().Contain(
            "exceptional.TenderId = readiness.SourceId");
        correctiveSql.Should().Contain(
            "newer.DecisionSequence >");
        correctiveSql.Should().Contain(
            "N'CREATE OR ALTER '");
        correctiveSql.Should().Contain(
            "EXEC sys.sp_executesql @definition");
        correctiveSql.Should().Contain("THROW 51217");
        correctiveSql.Should().NotContain("DISABLE TRIGGER");
    }

    [Fact]
    public void CorrectiveOrdinarySourceMigrationUpgradesAppliedTriggerFailClosed()
    {
        var correctiveSql = Sql(
            new TDC0403OrdinarySourceTriggerCorrections());

        correctiveSql.Should().Contain(
            "OBJECT_DEFINITION(");
        correctiveSql.Should().Contain(
            "readiness.SourceId <> exceptional.TenderId");
        correctiveSql.Should().Contain(
            "contract.StartDate > SYSUTCDATETIME()");
        correctiveSql.Should().Contain(
            "contract.EndDate < SYSUTCDATETIME()");
        correctiveSql.Should().Contain(
            "N'CREATE OR ALTER '");
        correctiveSql.Should().Contain(
            "EXEC sys.sp_executesql @definition");
        correctiveSql.Should().Contain("THROW 51218");
        correctiveSql.Should().NotContain("DISABLE TRIGGER");
    }

    [Fact]
    public void CommercialHardStopEnforcesCompleteRfqAwardTerms()
    {
        var sql = Sql(new TDC0403CommercialCapacityHardStops());

        sql.Should().Contain(
            "TR_PurchaseOrders_ApprovedCommercialCapacity");
        sql.Should().Contain(
            "TR_PurchaseOrderItems_ApprovedCommercialCapacity");
        sql.Should().Contain(
            "RequestForQuotationAwardLines");
        sql.Should().Contain(
            "RequestForQuotationItems");
        sql.Should().Contain(
            "UPPER(LTRIM(RTRIM(ISNULL(i.Currency, ''))))");
        sql.Should().Contain(
            "ROUND(i.TotalAmount, 2)");
        sql.Should().Contain(
            "FULL OUTER JOIN #AffectedRfqActual");
        sql.Should().Contain("THROW 51222");
        sql.Should().Contain("THROW 51223");
        sql.Should().Contain("THROW 51227");
    }

    [Fact]
    public void CommercialHardStopAllowsExistingRfqOrdersToBecomeTerminal()
    {
        var sql = Sql(new TDC0403CommercialCapacityHardStops());

        sql.Should().Contain(
            "LEFT JOIN deleted prior");
        sql.Should().Contain(
            "prior.Id = i.Id");
        sql.Should().Contain(
            "prior.Id IS NULL OR");
        sql.Should().Contain(
            "i.Status NOT IN ('Cancelled', 'Rejected')");
        sql.Should().Contain("THROW 51222");
    }

    [Fact]
    public void CommercialHardStopSerializesAndEnforcesContractCapacity()
    {
        var sql = Sql(new TDC0403CommercialCapacityHardStops());

        sql.Should().Contain(
            "Contracts contract WITH (UPDLOCK, HOLDLOCK)");
        sql.Should().Contain(
            "existing.Status NOT IN");
        sql.Should().Contain(
            "SUM(existing.TotalAmount)");
        sql.Should().Contain(
            "TenderBidItems bidItem");
        sql.Should().Contain(
            "TenderNegotiationItems negotiationItem");
        sql.Should().Contain(
            "actual.Quantity > approved.Quantity");
        sql.Should().Contain(
            "actual.LineTotal > approved.LineTotal");
        Count(
                sql,
                "purchaseOrder.ProcurementSourceId AS ContractId,")
            .Should().Be(
                2,
                "both contract header and item triggers must use the same description identity as tender award lines");
        sql.Should().Contain("THROW 51224");
        sql.Should().Contain("THROW 51225");
        sql.Should().Contain("THROW 51228");
        sql.Should().NotContain("DISABLE TRIGGER");
    }

    [Fact]
    public void RfqItemMasterLineageMigrationMatchesCommercialTriggerBaselineCounts()
    {
        var sql = Sql(new AlignRfqCommercialIdentityWithReceiptItemMaster());

        sql.Should().Contain(
            "TR_PurchaseOrders_ApprovedCommercialCapacity",
            "the purchase-order header trigger has four item identity branches in the baseline definition");
        sql.Should().Contain(
            "IF @identityCount <> 4",
            "the corrective migration must match the deployed purchase-order header trigger baseline");
        sql.Should().Contain(
            "TR_PurchaseOrderItems_ApprovedCommercialCapacity",
            "the purchase-order item trigger has two item identity branches in the baseline definition");
        sql.Should().Contain(
            "IF @identityCount <> 2",
            "the corrective migration must match the deployed purchase-order item trigger baseline");
    }

    [Fact]
    public void ContractIdentityRepairTargetsBothInstalledTriggersAndOnlyActualContractKeys()
    {
        var sql = Sql(new AlignContractPurchaseOrderCommercialIdentity());
        sql.Should().Contain("TR_PurchaseOrders_ApprovedCommercialCapacity")
            .And.Contain("TR_PurchaseOrderItems_ApprovedCommercialCapacity")
            .And.Contain("purchaseOrder.ProcurementSourceId AS ContractId,")
            .And.Contain("CHARINDEX(N') actual'")
            .And.Contain("STUFF(@definition, @start, @finish - @start, @repaired)")
            .And.Contain("@count NOT IN (0, 2)");
        sql.Should().NotContain("DISABLE TRIGGER").And.NotContain("UPDATE PurchaseOrderItems");
    }

    [Fact]
    public void ContractIdentityRepairRejectsMissingOrDriftedGuardsAndPreservesCapacityChecks()
    {
        var sql = Sql(new AlignContractPurchaseOrderCommercialIdentity());
        sql.Should().Contain("THROW 51983").And.Contain("THROW 51984")
            .And.Contain("COLLATE Latin1_General_100_BIN2")
            .And.Contain("actual.Quantity > approved.Quantity")
            .And.Contain("actual.LineTotal > approved.LineTotal")
            .And.Contain("Contracts contract WITH (UPDLOCK, HOLDLOCK)")
            .And.Contain("IF @count = 2");
    }

    private static int Count(string value, string fragment) =>
        (value.Length - value.Replace(fragment, string.Empty).Length) /
        fragment.Length;

    private static string Sql(Migration migration)
    {
        var builder = new MigrationBuilder(
            "Microsoft.EntityFrameworkCore.SqlServer");
        migration.GetType()
            .GetMethod(
                "Up",
                BindingFlags.Instance |
                BindingFlags.NonPublic)!
            .Invoke(migration, [builder]);
        return string.Join(
            Environment.NewLine,
            builder.Operations.OfType<SqlOperation>()
                .Select(item => item.Sql));
    }
}
