using System.Reflection;
using ErpSystem.Api.Services.Finance.AP;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Core.Interfaces.Inventory;
using ErpSystem.Core.Interfaces.Numbering;
using ErpSystem.Core.Services.Procurement;
using ErpSystem.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Finance;

public sealed class ApInvoicePartnerDefaultsTests
{
    [Fact]
    public async Task RequiredSupplierAsksForTransactionDecisionWithoutCreatingAnInvoice()
    {
        await using var fixture = new Fixture();
        await fixture.SeedAsync();
        var request = fixture.WhtRequest();
        request.ApplySupplierWithholdingDefaults = null;
        var act = () => fixture.Service.CreateAsync(request);
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("AP_WHT_CONFIRMATION_REQUIRED:*");
        fixture.Context.VendorInvoices.Should().BeEmpty();
    }

    [Fact]
    public async Task DeclinePersistsAndWinsOverStaleSelectedTaxWithoutChangingSupplier()
    {
        await using var fixture = new Fixture();
        await fixture.SeedAsync();
        var request = fixture.WhtRequest();
        request.ApplySupplierWithholdingDefaults = false;
        request.WithholdingTaxId = fixture.WithholdingTax.Id;
        request.WithholdingTaxRateOverride = 9m;
        var invoice = await fixture.Service.CreateAsync(request);
        invoice.ApplySupplierWithholdingDefaults.Should().BeFalse();
        invoice.WithholdingTaxId.Should().BeNull();
        invoice.WithholdingTaxAmount.Should().Be(0m);
        invoice.WithholdingTaxRateOverride.Should().BeNull();
        fixture.Partner.SubjectToWithholdingDeduction.Should().BeTrue();
        fixture.Partner.WithholdingTaxRate.Should().Be(7.5m);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    [InlineData(7.5678)]
    [InlineData(100)]
    public async Task ConfirmedInvoiceKeepsChosenRateAndUsesNetOfDiscountBase(decimal chosenRate)
    {
        await using var fixture = new Fixture();
        await fixture.SeedAsync();
        var request = fixture.WhtRequest();
        request.WithholdingTaxRateOverride = chosenRate;
        request.LineItems[0].DiscountPercentage = 20m;
        var invoice = await fixture.Service.CreateAsync(request);
        invoice.ApplySupplierWithholdingDefaults.Should().BeTrue();
        invoice.WithholdingTaxRateOverride.Should().Be(chosenRate);
        invoice.WithholdingTaxRate.Should().Be(chosenRate);
        invoice.WithholdingTaxAmount.Should().Be(decimal.Round(80m * chosenRate / 100m, 2, MidpointRounding.AwayFromZero));
        invoice.TotalAmount.Should().Be(80m, "invoice AP remains gross; withholding is recognized only at payment");
        fixture.WithholdingTax.Rate.Should().Be(7.5m);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public async Task InvalidTransactionRateIsRejected(decimal rate)
    {
        await using var fixture = new Fixture();
        await fixture.SeedAsync();
        var request = fixture.WhtRequest(); request.WithholdingTaxRateOverride = rate;
        var act = () => fixture.Service.CreateAsync(request);
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*between zero and 100*");
    }

    [Fact]
    public async Task TransactionRateCannotSilentlyLosePrecisionWhenSaved()
    {
        await using var fixture = new Fixture(); await fixture.SeedAsync();
        var request = fixture.WhtRequest(); request.WithholdingTaxRateOverride = 7.12345m;
        var act = () => fixture.Service.CreateAsync(request);
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*four decimal places*");
    }

    [Fact]
    public async Task ExplicitConfiguredSelectionCountsAsYesAndIsNotReplacedBySupplierRule()
    {
        await using var fixture = new Fixture();
        await fixture.SeedAsync();
        var selected = new Tax { TenantId = fixture.TenantId, Code = "WHT-EXPLICIT", Name = "Explicit rule", Rate = 3m,
            Category = TaxCategory.Withholding, Applicability = TaxApplicability.Purchases,
            EffectiveFrom = new DateTime(2025, 1, 1), TaxPayableAccountId = fixture.WithholdingTax.TaxPayableAccountId };
        fixture.Context.Taxes.Add(selected); await fixture.Context.SaveChangesAsync();
        var request = fixture.WhtRequest(); request.ApplySupplierWithholdingDefaults = null; request.WithholdingTaxId = selected.Id;
        var invoice = await fixture.Service.CreateAsync(request);
        invoice.ApplySupplierWithholdingDefaults.Should().BeTrue();
        invoice.WithholdingTaxId.Should().Be(selected.Id);
        invoice.WithholdingTaxRate.Should().Be(3m);
    }

    [Theory]
    [InlineData(false, 7.5)]
    [InlineData(true, 0)]
    public async Task DisabledOrZeroRateSupplierDoesNotRequestOrApplyDefault(bool enabled, decimal rate)
    {
        await using var fixture = new Fixture();
        fixture.Partner.SubjectToWithholdingDeduction = enabled; fixture.Partner.WithholdingTaxRate = rate;
        await fixture.SeedAsync();
        var request = fixture.WhtRequest(); request.ApplySupplierWithholdingDefaults = null;
        var invoice = await fixture.Service.CreateAsync(request);
        invoice.WithholdingTaxId.Should().BeNull(); invoice.WithholdingTaxAmount.Should().Be(0m);
    }

    [Fact]
    public async Task LegacyAmbiguousRateRequiresRuleMappingButExplicitRuleResolvesIt()
    {
        await using var fixture = new Fixture();
        await fixture.SeedAsync();
        fixture.Context.Taxes.Add(new Tax { TenantId = fixture.TenantId, Code = "SECOND-WHT", Name = "Another WHT rule",
            Rate = 7.5m, EffectiveFrom = new DateTime(2025, 1, 1), Category = TaxCategory.Withholding,
            Applicability = TaxApplicability.Purchases, TaxPayableAccountId = fixture.WithholdingTax.TaxPayableAccountId });
        await fixture.Context.SaveChangesAsync();
        var defaults = await fixture.Service.GetSupplierDefaultsAsync(fixture.Supplier.Id, invoiceDate: new DateTime(2026, 9, 13));
        defaults!.WithholdingDefault!.Required.Should().BeTrue();
        defaults.WithholdingDefault.TaxId.Should().BeNull();
        defaults.WithholdingDefault.Message.Should().Contain("More than one");
        var act = () => fixture.Service.CreateAsync(fixture.WhtRequest());
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*no rule has been guessed*");
        fixture.Partner.DefaultWithholdingTaxId = fixture.WithholdingTax.Id;
        await fixture.Context.SaveChangesAsync();
        (await fixture.Service.CreateAsync(fixture.WhtRequest())).WithholdingTaxId.Should().Be(fixture.WithholdingTax.Id);
    }

    [Fact]
    public async Task DefaultResolutionValidatesEffectiveRuleButUsesSupplierEnteredRate()
    {
        await using var fixture = new Fixture();
        fixture.Partner.DefaultWithholdingTaxId = fixture.WithholdingTax.Id;
        fixture.WithholdingTax.Rate = 8m; fixture.WithholdingTax.EffectiveFrom = new DateTime(2027, 1, 1);
        fixture.Context.Set<TaxRateHistory>().Add(new TaxRateHistory { TenantId = fixture.TenantId,
            TaxId = fixture.WithholdingTax.Id, Rate = 7.5m, EffectiveFrom = new DateTime(2025, 1, 1), EffectiveTo = new DateTime(2026, 12, 31) });
        await fixture.SeedAsync();
        var earlier = await fixture.Service.GetSupplierDefaultsAsync(fixture.Supplier.Id, invoiceDate: new DateTime(2026, 9, 13));
        var later = await fixture.Service.GetSupplierDefaultsAsync(fixture.Supplier.Id, invoiceDate: new DateTime(2027, 1, 2));
        var notEffective = await fixture.Service.GetSupplierDefaultsAsync(fixture.Supplier.Id, invoiceDate: new DateTime(2024, 1, 1));
        earlier!.WithholdingDefault!.Rate.Should().Be(7.5m);
        later!.WithholdingDefault!.Rate.Should().Be(7.5m, "the supplier-entered rate is the transaction default");
        later.WithholdingDefault.TaxId.Should().Be(fixture.WithholdingTax.Id);
        notEffective!.WithholdingDefault!.TaxId.Should().BeNull();
        notEffective.WithholdingDefault.Message.Should().Contain("not effective");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ConfirmedSupplierRateIsCapturedEvenWhenDifferentFromCatalogue(bool prefilledTaxId)
    {
        await using var fixture = new Fixture();
        fixture.Partner.DefaultWithholdingTaxId = fixture.WithholdingTax.Id;
        fixture.Partner.WithholdingTaxRate = 4.5m;
        await fixture.SeedAsync();
        var request = fixture.WhtRequest();
        if (prefilledTaxId) request.WithholdingTaxId = fixture.WithholdingTax.Id;
        var invoice = await fixture.Service.CreateAsync(request);
        invoice.WithholdingTaxId.Should().Be(fixture.WithholdingTax.Id);
        invoice.WithholdingTaxRate.Should().Be(4.5m);
        invoice.WithholdingTaxRateOverride.Should().Be(4.5m);
        invoice.WithholdingTaxAmount.Should().Be(4.5m);
        fixture.WithholdingTax.Rate.Should().Be(7.5m);
    }

    [Theory]
    [InlineData("foreign")]
    [InlineData("inactive")]
    [InlineData("missing")]
    public async Task InvalidSupplierRuleIsVisibleAndNotSilentlyApplied(string state)
    {
        await using var fixture = new Fixture();
        fixture.Partner.DefaultWithholdingTaxId = fixture.WithholdingTax.Id;
        if (state == "foreign") fixture.WithholdingTax.TenantId = Guid.NewGuid();
        if (state == "inactive") fixture.WithholdingTax.IsActive = false;
        if (state == "missing") fixture.WithholdingTax.TaxPayableAccountId = null;
        await fixture.SeedAsync();
        var defaults = await fixture.Service.GetSupplierDefaultsAsync(fixture.Supplier.Id);
        defaults!.WithholdingDefault!.Message.Should().NotBeNullOrWhiteSpace();
        defaults.WithholdingDefault.TaxId.Should().BeNull();
        var act = () => fixture.Service.CreateAsync(fixture.WhtRequest());
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task GeneratedDraftDefersDecisionWithoutApplyingWhtAndCannotSubmitYet()
    {
        await using var fixture = new Fixture();
        await fixture.SeedAsync();
        var request = fixture.WhtRequest(); request.ApplySupplierWithholdingDefaults = null;
        var invoice = await fixture.CreateDeferredAsync(request);
        invoice.WithholdingDecisionPending.Should().BeTrue();
        invoice.ApplySupplierWithholdingDefaults.Should().BeNull();
        invoice.WithholdingTaxId.Should().BeNull();
        var submit = () => fixture.Service.SubmitForApprovalAsync(invoice.Id);
        await submit.Should().ThrowAsync<InvalidOperationException>().WithMessage("AP_WHT_CONFIRMATION_REQUIRED:*");
    }

    [Fact]
    public async Task EditingGeneratedDraftCanDeclineAndClearsPendingDecision()
    {
        await using var fixture = new Fixture(); await fixture.SeedAsync();
        var request = fixture.WhtRequest(); request.ApplySupplierWithholdingDefaults = null;
        var invoice = await fixture.CreateDeferredAsync(request);
        var update = fixture.UpdateRequest(invoice); update.ApplySupplierWithholdingDefaults = false;
        var saved = await fixture.Service.UpdateAsync(update);
        saved.WithholdingDecisionPending.Should().BeFalse();
        saved.ApplySupplierWithholdingDefaults.Should().BeFalse();
        saved.WithholdingTaxId.Should().BeNull();
    }

    [Fact]
    public async Task EditingPreservesDeclineAndChosenZeroOverrideWhenFieldsAreOmitted()
    {
        await using var fixture = new Fixture(); await fixture.SeedAsync();
        var request = fixture.WhtRequest(); request.WithholdingTaxRateOverride = 0m;
        var invoice = await fixture.Service.CreateAsync(request);
        fixture.WithholdingTax.Rate = 9m; await fixture.Context.SaveChangesAsync();
        var update = fixture.UpdateRequest(invoice);
        var retained = await fixture.Service.UpdateAsync(update);
        retained.WithholdingTaxRateOverride.Should().Be(0m); retained.WithholdingTaxRate.Should().Be(0m);
        update = fixture.UpdateRequest(retained); update.ApplySupplierWithholdingDefaults = false;
        var declined = await fixture.Service.UpdateAsync(update);
        update = fixture.UpdateRequest(declined); update.WithholdingTaxId = fixture.WithholdingTax.Id;
        var stillDeclined = await fixture.Service.UpdateAsync(update);
        stillDeclined.ApplySupplierWithholdingDefaults.Should().BeFalse();
        stillDeclined.WithholdingTaxId.Should().BeNull();
        stillDeclined.WithholdingTaxAmount.Should().Be(0m);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UnrelatedDraftEditRetainsAcceptedRateAfterCatalogueChanges(bool omitTaxId)
    {
        await using var fixture = new Fixture(); await fixture.SeedAsync();
        var request = fixture.WhtRequest(); request.WithholdingTaxId = fixture.WithholdingTax.Id;
        request.ApplySupplierWithholdingDefaults = null;
        var invoice = await fixture.Service.CreateAsync(request);
        invoice.WithholdingTaxRateOverride.Should().BeNull();
        fixture.WithholdingTax.Rate = 9m; await fixture.Context.SaveChangesAsync();
        var update = fixture.UpdateRequest(invoice);
        if (omitTaxId) update.WithholdingTaxId = null;
        update.Notes = "Corrected reference only";
        var retained = await fixture.Service.UpdateAsync(update);
        retained.WithholdingTaxRate.Should().Be(7.5m);
        retained.WithholdingTaxRateOverride.Should().Be(7.5m);
        retained.WithholdingTaxAmount.Should().Be(7.5m);
    }

    [Fact]
    public async Task ExplicitDraftRateEditStillReplacesPreviouslyAcceptedRate()
    {
        await using var fixture = new Fixture(); await fixture.SeedAsync();
        var request = fixture.WhtRequest(); request.WithholdingTaxId = fixture.WithholdingTax.Id;
        var invoice = await fixture.Service.CreateAsync(request);
        var update = fixture.UpdateRequest(invoice); update.WithholdingTaxRateOverride = 4.25m;
        var saved = await fixture.Service.UpdateAsync(update);
        saved.WithholdingTaxRate.Should().Be(4.25m);
        saved.WithholdingTaxAmount.Should().Be(4.25m);
    }

    [Fact]
    public async Task OpeningBalancesNeverRequestOrInheritSupplierWithholding()
    {
        await using var fixture = new Fixture(); await fixture.SeedAsync();
        var request = fixture.WhtRequest(); request.IsOpeningBalance = true;
        request.ApplySupplierWithholdingDefaults = null;
        (await fixture.ApplyWithholdingAsync(request)).Should().BeFalse();
        request.WithholdingTaxId.Should().BeNull();
        request.ApplySupplierWithholdingDefaults.Should().BeNull();
    }

    [Fact]
    public async Task DefaultsReadUsesCanonicalCodeIdentityAndDoesNotCreateSuppliers()
    {
        await using var fixture = new Fixture();
        await fixture.SeedAsync();
        fixture.Context.ChangeTracker.Clear();
        var before = await fixture.Context.Suppliers.CountAsync();
        var defaults = await fixture.Service.GetSupplierDefaultsAsync(fixture.Supplier.Id);
        defaults!.BusinessPartnerId.Should().Be(fixture.Partner.Id);
        defaults.PostingDefaults.DefaultApAccountId.Should().Be(fixture.Partner.DefaultApAccountId);
        defaults.PostingDefaults.WithholdingTaxRate.Should().Be(7.5m);
        fixture.Context.ChangeTracker.HasChanges().Should().BeFalse();
        (await fixture.Context.Suppliers.CountAsync()).Should().Be(before);
    }

    [Fact]
    public async Task DefaultsReadNeverMatchesByDisplayName()
    {
        await using var fixture = new Fixture();
        fixture.Supplier.SupplierCode = "OTHER-CODE";
        fixture.Supplier.Name = fixture.Partner.PartnerName;
        await fixture.SeedAsync();
        (await fixture.Service.GetSupplierDefaultsAsync(fixture.Supplier.Id)).Should().BeNull();
    }

    [Fact]
    public async Task PurchaseOrderSnapshotWinsOverLaterPartnerEdits()
    {
        await using var fixture = new Fixture();
        await fixture.SeedAsync();
        var snapshot = BusinessPartnerPostingDefaults.SerializeSnapshot(fixture.Partner);
        var originalAccount = fixture.Partner.DefaultApAccountId;
        var order = new PurchaseOrder
        {
            Id = Guid.NewGuid(), TenantId = fixture.TenantId, BusinessPartnerId = fixture.Partner.Id,
            OrderNumber = "PO-SNAPSHOT", SupplierDefaultsSnapshotJson = snapshot
        };
        fixture.Context.PurchaseOrders.Add(order);
        fixture.Partner.DefaultApAccountId = Guid.NewGuid();
        await fixture.Context.SaveChangesAsync();
        var defaults = await fixture.Service.GetSupplierDefaultsAsync(fixture.Supplier.Id, order.Id);
        defaults!.PostingDefaults.DefaultApAccountId.Should().Be(originalAccount);
    }

    [Fact]
    public async Task NewInvoiceRequiresDecisionWhenSupplierActivatedAfterPurchaseOrderSnapshot()
    {
        await using var fixture = new Fixture();
        fixture.Partner.SubjectToWithholdingDeduction = false; fixture.Partner.WithholdingTaxRate = 0m;
        var snapshot = BusinessPartnerPostingDefaults.SerializeSnapshot(fixture.Partner);
        var capturedApAccount = fixture.Partner.DefaultApAccountId;
        var order = new PurchaseOrder
        {
            TenantId = fixture.TenantId, BusinessPartnerId = fixture.Partner.Id,
            OrderNumber = "PO-BEFORE-WHT", SupplierDefaultsSnapshotJson = snapshot
        };
        fixture.Context.PurchaseOrders.Add(order);
        fixture.Partner.SubjectToWithholdingDeduction = true; fixture.Partner.WithholdingTaxRate = 7.5m;
        fixture.Partner.DefaultApAccountId = Guid.NewGuid();
        await fixture.SeedAsync();
        var defaults = await fixture.Service.GetSupplierDefaultsAsync(fixture.Supplier.Id, order.Id);
        defaults!.PostingDefaults.DefaultApAccountId.Should().Be(capturedApAccount);
        defaults.PostingDefaults.SubjectToWithholdingDeduction.Should().BeFalse("the PO snapshot remains immutable");
        defaults.WithholdingDefault!.Required.Should().BeTrue("a new invoice uses current supplier WHT eligibility");
        defaults.WithholdingDefault.TaxId.Should().Be(fixture.WithholdingTax.Id);
        var request = fixture.WhtRequest(); request.PurchaseOrderId = order.Id; request.ApplySupplierWithholdingDefaults = null;
        var apply = () => fixture.ApplyWithholdingAsync(request);
        await apply.Should().ThrowAsync<InvalidOperationException>().WithMessage("AP_WHT_CONFIRMATION_REQUIRED:*");
        fixture.Context.VendorInvoices.Should().BeEmpty();
    }

    [Fact]
    public async Task CurrentSupplierDisableDoesNotInheritOldPurchaseOrderWithholding()
    {
        await using var fixture = new Fixture();
        var order = new PurchaseOrder
        {
            TenantId = fixture.TenantId, BusinessPartnerId = fixture.Partner.Id,
            OrderNumber = "PO-OLD-WHT", SupplierDefaultsSnapshotJson = BusinessPartnerPostingDefaults.SerializeSnapshot(fixture.Partner)
        };
        fixture.Context.PurchaseOrders.Add(order);
        fixture.Partner.SubjectToWithholdingDeduction = false; fixture.Partner.WithholdingTaxRate = 0m;
        await fixture.SeedAsync();
        var defaults = await fixture.Service.GetSupplierDefaultsAsync(fixture.Supplier.Id, order.Id);
        defaults!.WithholdingDefault!.Required.Should().BeFalse();
        var request = fixture.WhtRequest(); request.PurchaseOrderId = order.Id; request.ApplySupplierWithholdingDefaults = null;
        (await fixture.ApplyWithholdingAsync(request)).Should().BeFalse();
        request.WithholdingTaxId.Should().BeNull();
    }

    [Fact]
    public async Task ForeignTenantAndDifferentSupplierPurchaseOrderAreRejected()
    {
        await using var fixture = new Fixture();
        await fixture.SeedAsync();
        var foreign = new BusinessPartner
        {
            Id = Guid.NewGuid(), TenantId = Guid.NewGuid(), PartnerName = "Foreign", PartnerCode = "FOREIGN"
        };
        fixture.Context.BusinessPartners.Add(foreign);
        var order = new PurchaseOrder
        {
            Id = Guid.NewGuid(), TenantId = fixture.TenantId, BusinessPartnerId = Guid.NewGuid(), OrderNumber = "PO-OTHER"
        };
        fixture.Context.PurchaseOrders.Add(order);
        await fixture.Context.SaveChangesAsync();
        var foreignRead = () => fixture.Service.GetSupplierDefaultsAsync(foreign.Id);
        await foreignRead.Should().ThrowAsync<KeyNotFoundException>();
        var wrongOrder = () => fixture.Service.GetSupplierDefaultsAsync(fixture.Supplier.Id, order.Id);
        await wrongOrder.Should().ThrowAsync<InvalidOperationException>().WithMessage("*different supplier*");
    }

    [Theory]
    [InlineData(null)]
    [InlineData(false)]
    public async Task OldClientsAndExplicitNoDefaultsKeepNoTax(bool? apply)
    {
        await using var fixture = new Fixture();
        await fixture.SeedAsync();
        var request = fixture.Request();
        request.ApplyBusinessPartnerDefaults = apply;
        await fixture.ApplyAsync(request);
        request.ApAccountId.Should().BeNull();
        request.LineItems[0].TaxGroupId.Should().BeNull();
        request.WithholdingTaxRate.Should().Be(0m);
    }

    [Fact]
    public async Task ExplicitAccountsTaxAndExemptLinesAreNeverReplaced()
    {
        await using var fixture = new Fixture();
        await fixture.SeedAsync();
        var request = fixture.Request();
        var account = Guid.NewGuid();
        var selectedTax = Guid.NewGuid();
        request.ApAccountId = account;
        request.PaymentTermsDays = 45;
        request.LineItems[0].TaxGroupId = selectedTax;
        request.LineItems.Add(new() { Description = "Exempt", Quantity = 1, UnitPrice = 10, TaxTreatment = TaxTreatment.Exempt });
        request.LineItems.Add(new() { Description = "Explicit rate", Quantity = 1, UnitPrice = 10, TaxRate = 2.5m });
        await fixture.ApplyAsync(request);
        request.ApAccountId.Should().BeNull("partner and invoice defaults cannot override the Finance AP control account");
        request.PaymentTermsDays.Should().Be(45);
        request.LineItems[0].TaxGroupId.Should().Be(selectedTax);
        request.LineItems[1].TaxGroupId.Should().BeNull();
        request.LineItems[2].TaxGroupId.Should().BeNull();
        request.WithholdingTaxId.Should().BeNull();
        request.WithholdingTaxRate.Should().Be(0m);
    }

    [Fact]
    public async Task NewDraftUsesTheTaxEngineAfterApplyingVisibleOptInDefaults()
    {
        await using var fixture = new Fixture();
        await fixture.SeedAsync();
        fixture.TaxEngine.Setup(engine => engine.CalculateTaxesAsync(It.IsAny<TaxCalculationRequestDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TaxCalculationResultDto { BaseAmount = 100m, TotalTaxAmount = 15m, GrandTotal = 115m });
        var invoice = await fixture.Service.CreateAsync(fixture.Request());
        invoice.Status.Should().Be(VendorInvoiceStatus.Draft);
        invoice.ApAccountId.Should().BeNull("new invoices resolve AP control exclusively from Finance settings at posting");
        invoice.ExpenseAccountId.Should().Be(fixture.Partner.DefaultExpenseAccountId);
        invoice.TaxAmount.Should().Be(15m);
        invoice.TotalAmount.Should().Be(115m);
        invoice.WithholdingTaxId.Should().Be(fixture.WithholdingTax.Id);
        invoice.WithholdingTaxRate.Should().Be(7.5m);
        fixture.Supplier.DefaultApAccountId.Should().BeNull("creating a draft must not rewrite an existing Finance supplier's master defaults");
        fixture.Supplier.DefaultExpenseAccountId.Should().BeNull();
        fixture.TaxEngine.Verify(engine => engine.CalculateTaxesAsync(It.Is<TaxCalculationRequestDto>(request =>
            request.TaxGroupId == fixture.Partner.DefaultTaxGroupId && request.BaseAmount == 100m), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExplicitTimingSurvivesSupplierPaymentTermFallback(bool explicitDueDate)
    {
        await using var fixture = new Fixture();
        var term = new PaymentTerm
        {
            Id = Guid.NewGuid(), TenantId = fixture.TenantId, Code = "NET60", Name = "Net 60",
            DueDays = 60, IsActive = true, ApplicableTo = "Supplier"
        };
        fixture.Context.Set<PaymentTerm>().Add(term);
        fixture.Supplier.PaymentTermId = term.Id;
        fixture.Partner.DefaultTaxGroupId = null;
        await fixture.SeedAsync();
        var request = fixture.Request();
        request.PaymentTermsDays = explicitDueDate ? 30 : 45;
        if (explicitDueDate) request.DueDate = request.InvoiceDate.AddDays(17);
        var invoice = await fixture.Service.CreateAsync(request);
        invoice.PaymentTermsDays.Should().Be(explicitDueDate ? 30 : 45);
        invoice.DueDate.Should().Be(request.InvoiceDate.AddDays(explicitDueDate ? 17 : 45));
    }

    [Fact]
    public async Task NewFinanceSupplierIdentityCopiesPartnerAccounts()
    {
        await using var fixture = new Fixture();
        fixture.Partner.DefaultTaxGroupId = null;
        fixture.Context.BusinessPartners.Add(fixture.Partner);
        await fixture.Context.SaveChangesAsync();
        var request = fixture.Request();
        request.SupplierId = fixture.Partner.Id;
        var invoice = await fixture.Service.CreateAsync(request);
        var supplier = await fixture.Context.Suppliers.SingleAsync();
        supplier.Id.Should().Be(invoice.SupplierId);
        supplier.SupplierCode.Should().Be(fixture.Partner.PartnerCode);
        supplier.DefaultApAccountId.Should().Be(fixture.Partner.DefaultApAccountId);
        supplier.DefaultExpenseAccountId.Should().Be(fixture.Partner.DefaultExpenseAccountId);
    }

    [Fact]
    public async Task PrefilledPurchaseOrderTermRetainsCapturedDaysAfterCatalogueChanges()
    {
        await using var fixture = new Fixture();
        var term = new PaymentTerm
        {
            Id = Guid.NewGuid(), TenantId = fixture.TenantId, Code = "NET30", Name = "Net 30",
            DueDays = 30, IsActive = true, ApplicableTo = "Supplier"
        };
        fixture.Context.Set<PaymentTerm>().Add(term);
        fixture.Partner.PaymentTermId = term.Id;
        fixture.Partner.PaymentTerm = term;
        var order = new PurchaseOrder
        {
            Id = Guid.NewGuid(), TenantId = fixture.TenantId, BusinessPartnerId = fixture.Partner.Id,
            OrderNumber = "PO-TERM-SNAPSHOT", SupplierDefaultsSnapshotJson = BusinessPartnerPostingDefaults.SerializeSnapshot(fixture.Partner)
        };
        fixture.Context.PurchaseOrders.Add(order);
        term.DueDays = 60;
        await fixture.SeedAsync();
        var request = fixture.Request();
        request.PurchaseOrderId = order.Id;
        request.PaymentTermId = term.Id;
        request.PaymentTermsDays = 60;
        (await fixture.ApplyAsync(request)).Should().Be(30);
        request.PaymentTermsDays.Should().Be(30);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        public Guid TenantId { get; } = Guid.NewGuid();
        public ApplicationDbContext Context { get; }
        public BusinessPartner Partner { get; }
        public Supplier Supplier { get; }
        public Tax WithholdingTax { get; }
        public Mock<ITaxCalculationEngine> TaxEngine { get; } = new();
        public VendorInvoiceService Service { get; }
        public Fixture()
        {
            Context = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
                .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning)).Options);
            Partner = new BusinessPartner
            {
                Id = Guid.NewGuid(), TenantId = TenantId, PartnerName = "Selected supplier", PartnerCode = "SUP-SELECTED",
                PartnerType = "Supplier", IsActive = true, RegistrationStatus = "Approved",
                DefaultApAccountId = Guid.NewGuid(), DefaultExpenseAccountId = Guid.NewGuid(), DefaultTaxGroupId = Guid.NewGuid(),
                SubjectToWithholdingDeduction = true, WithholdingTaxRate = 7.5m, Currency = "GHS"
            };
            Supplier = new Supplier
            {
                Id = Guid.NewGuid(), TenantId = TenantId, SupplierCode = Partner.PartnerCode,
                Name = Partner.PartnerName, IsActive = true, Status = "Active"
            };
            var payable = new Account { TenantId = TenantId, AccountCode = "WHT", AccountNumber = "WHT",
                AccountName = "WHT payable", AccountType = AccountType.Liability, Status = AccountStatus.Active, IsControlAccount = true };
            WithholdingTax = new Tax { TenantId = TenantId, Code = "WHT-TEST", Name = "Purchase withholding", Rate = 7.5m,
                Category = TaxCategory.Withholding, Applicability = TaxApplicability.Purchases,
                EffectiveFrom = new DateTime(2025, 1, 1), TaxPayableAccountId = payable.Id };
            Context.Accounts.Add(payable); Context.Taxes.Add(WithholdingTax);
            var current = new Mock<ICurrentUserService>();
            current.SetupGet(user => user.TenantId).Returns(TenantId);
            current.SetupGet(user => user.UserName).Returns("test-maker");
            var numbering = new Mock<IDocumentNumberingService>();
            numbering.Setup(service => service.GenerateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid?>(),
                It.IsAny<DateTime?>(), It.IsAny<string?>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync("VI-DEFAULTS-TEST");
            Service = new VendorInvoiceService(new UnitOfWork(Context), current.Object, Mock.Of<IInventoryValuationService>(),
                NullLogger<VendorInvoiceService>.Instance, numbering.Object, Mock.Of<IWorkflowService>(), taxEngine: TaxEngine.Object);
        }
        public async Task SeedAsync()
        {
            Context.BusinessPartners.Add(Partner);
            Context.Suppliers.Add(Supplier);
            await Context.SaveChangesAsync();
        }
        public VendorInvoiceCreateDto Request() => new()
        {
            SupplierId = Supplier.Id, ApplyBusinessPartnerDefaults = true, ApplySupplierWithholdingDefaults = true,
            InvoiceDate = new DateTime(2026, 9, 13),
            CurrencyCode = "GHS", LineItems = [new() { Description = "Goods", Quantity = 1, UnitPrice = 100 }]
        };
        public Task<int?> ApplyAsync(VendorInvoiceCreateDto request) =>
            (Task<int?>)typeof(VendorInvoiceService).GetMethod("ApplyBusinessPartnerCreateDefaultsAsync",
                BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(Service, [request, Supplier, CancellationToken.None])!;
        public VendorInvoiceCreateDto WhtRequest()
        {
            var request = Request(); request.ApplyBusinessPartnerDefaults = false; return request;
        }
        public Task<VendorInvoiceDto> CreateDeferredAsync(VendorInvoiceCreateDto request) =>
            (Task<VendorInvoiceDto>)typeof(VendorInvoiceService).GetMethod("CreateCoreAsync",
                BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(Service, [request, null, CancellationToken.None, true])!;
        public Task<bool> ApplyWithholdingAsync(VendorInvoiceCreateDto request) =>
            (Task<bool>)typeof(VendorInvoiceService).GetMethod("ApplySupplierWithholdingCreateDefaultAsync",
                BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(Service, [request, false, CancellationToken.None])!;
        public VendorInvoiceUpdateDto UpdateRequest(VendorInvoiceDto invoice) => new()
        {
            Id = invoice.Id, InvoiceDate = invoice.InvoiceDate, CurrencyCode = invoice.CurrencyCode, ExchangeRate = invoice.ExchangeRate,
            WithholdingTaxId = invoice.WithholdingTaxId, WithholdingTaxRate = invoice.WithholdingTaxRate,
            LineItems = invoice.LineItems.Select(line => new VendorInvoiceLineItemCreateDto
            { Id = line.Id, Description = line.Description, Quantity = line.Quantity, UnitPrice = line.UnitPrice,
                TaxTreatment = line.TaxTreatment, TaxGroupId = line.TaxGroupId, TaxRate = line.TaxRate }).ToList()
        };
        public ValueTask DisposeAsync() => Context.DisposeAsync();
    }
}
