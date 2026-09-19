using ErpSystem.Data.Migrations;
using FluentAssertions;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Finance;

public sealed class CrossCurrencySettlementMigrationTests
{
    [Fact]
    [Trait("Category", "Architecture")]
    public void Up_ShouldBackfillExistingSameCurrencyAllocationAndRealizedFxEvidence()
    {
        var allocationBackfill = ArchivedMigrationSource.Read("20260806095000_AddApArCrossCurrencySettlement.cs");
        allocationBackfill.Should().Contain("Cannot safely backfill pre-migration AP cross-currency allocations");
        allocationBackfill.Should().Contain("Cannot safely backfill pre-migration AR cross-currency allocations");
        allocationBackfill.Should().Contain("[PaymentCurrencyAmount] = [a].[AllocatedAmount]");
        allocationBackfill.Should().Contain("[SettlementFunctionalAmount] = ROUND");
        allocationBackfill.Should().Contain("INNER JOIN [VendorInvoice]");
        allocationBackfill.Should().Contain("INNER JOIN [Invoices]");

        // TDC-0505's readiness trigger validates operational allocation edits. The migration's
        // currency-only backfill must bypass it briefly without weakening the runtime control or
        // accidentally enabling a trigger that an administrator had already disabled.
        allocationBackfill.Should().Contain("TR_VendorPaymentAllocation_TDC0505PaymentReadiness");
        allocationBackfill.Should().Contain("OBJECTPROPERTY(@apReadinessTriggerId, 'ExecIsTriggerDisabled')");
        allocationBackfill.Should().Contain("DISABLE TRIGGER [dbo].[TR_VendorPaymentAllocation_TDC0505PaymentReadiness]");
        allocationBackfill.Should().Contain("ENABLE TRIGGER [dbo].[TR_VendorPaymentAllocation_TDC0505PaymentReadiness]");
        allocationBackfill.Should().Contain("BEGIN TRY");
        allocationBackfill.Should().Contain("BEGIN CATCH");
        allocationBackfill.Should().Contain("THROW;");

        var triggerDisable = allocationBackfill.IndexOf("DISABLE TRIGGER", StringComparison.Ordinal);
        var apUpdate = allocationBackfill.IndexOf("UPDATE [a]", triggerDisable, StringComparison.Ordinal);
        var triggerRestore = allocationBackfill.IndexOf("ENABLE TRIGGER", apUpdate, StringComparison.Ordinal);
        var arUpdate = allocationBackfill.IndexOf("UPDATE [a]", triggerRestore, StringComparison.Ordinal);
        triggerDisable.Should().BeGreaterThan(-1);
        apUpdate.Should().BeGreaterThan(triggerDisable, "the AP backfill must run only while its readiness trigger is suspended");
        triggerRestore.Should().BeGreaterThan(apUpdate, "the runtime readiness control must be restored after the AP backfill");
        arUpdate.Should().BeGreaterThan(triggerRestore, "the unrelated AR backfill must not run inside the AP trigger exception");

        var realizedFxBackfill = allocationBackfill;
        realizedFxBackfill.Should().Contain("FROM [FxRealizedSettlements]");
        realizedFxBackfill.Should().Contain("INNER JOIN [VendorPaymentAllocation]");
        realizedFxBackfill.Should().Contain("INNER JOIN [PaymentAllocation]");
        realizedFxBackfill.Should().Contain("[PaymentExchangeRate] = [a].[PaymentExchangeRate]");
    }
}
