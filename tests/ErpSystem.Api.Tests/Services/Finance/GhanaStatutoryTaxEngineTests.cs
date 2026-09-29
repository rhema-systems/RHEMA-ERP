using ErpSystem.Api.Services.Finance;
using ErpSystem.Api.Services.Finance.AP;
using ErpSystem.Api.Services.Finance.AR;
using ErpSystem.Api.Services.Finance.GL;
using ErpSystem.Api.Services.Finance.Taxation;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Core.Interfaces.Inventory;
using ErpSystem.Core.Interfaces.Numbering;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Data;
using ErpSystem.Shared;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

#pragma warning disable CS0618

namespace ErpSystem.Api.Tests.Services.Finance;

public sealed class GhanaStatutoryTaxEngineTests
{
    [Fact]
    [Trait("Batch", "FinanceGoLive-GhanaTax")]
    [Trait("Category", "Tax")]
    public async Task ActiveTaxSelector_ShouldFilterByApplicabilityAndCategory()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        SeedTenant(db, tenantId);
        var payable = SeedAccount(db, tenantId, "2205", AccountType.Liability, isControlAccount: true, allowDirectPosting: false);
        var receivable = SeedAccount(db, tenantId, "1405", AccountType.Asset, isControlAccount: true, allowDirectPosting: false);
        var salesWht = SeedTax(db, tenantId, "WHT-SALES", "Sales WHT", 5m, TaxCategory.Withholding, receivable.Id, payable.Id, new DateTime(2026, 1, 1));
        salesWht.Applicability = TaxApplicability.Sales;
        var purchaseWht = SeedTax(db, tenantId, "WHT-PURCH", "Purchase WHT", 5m, TaxCategory.Withholding, receivable.Id, payable.Id, new DateTime(2026, 1, 1));
        purchaseWht.Applicability = TaxApplicability.Purchases;
        var salesVat = SeedTax(db, tenantId, "VAT-SALES", "Sales VAT", 15m, TaxCategory.Standard, receivable.Id, payable.Id, new DateTime(2026, 1, 1));
        salesVat.Applicability = TaxApplicability.Sales;
        SeedTax(db, tenantId, "WHT-INACTIVE", "Inactive WHT", 5m, TaxCategory.Withholding, receivable.Id, payable.Id, new DateTime(2026, 1, 1), isActive: false);
        await db.SaveChangesAsync();

        var service = new TaxConfigurationService(db, CreateCurrentUser(tenantId).Object, Mock.Of<ILogger<TaxConfigurationService>>());

        var result = await service.GetActiveTaxesAsync(TaxApplicability.Sales, TaxCategory.Withholding);

        result.Should().ContainSingle();
        result.Single().Code.Should().Be("WHT-SALES");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-GhanaTax")]
    [Trait("Category", "Tax")]
    public async Task CurrentGhanaVatNhiltGetfund_ShouldCalculateFromEffectiveDatedTenantConfigWithoutCovidLevy()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        SeedTenant(db, tenantId);
        var currentUser = CreateCurrentUser(tenantId);
        var config = new TaxConfigurationService(db, currentUser.Object, Mock.Of<ILogger<TaxConfigurationService>>());
        await config.SeedGhanaTaxesAsync();

        var engine = new TaxCalculationEngine(db, currentUser.Object, Mock.Of<ILogger<TaxCalculationEngine>>());

        var result = await engine.CalculateTaxesAsync(new TaxCalculationRequestDto
        {
            BaseAmount = 100m,
            TransactionDate = new DateTime(2026, 7, 6),
            TransactionType = TaxTransactionType.SaleOfGoods
        });

        result.TotalTaxAmount.Should().Be(20m);
        result.EffectiveTaxRate.Should().Be(20m);
        result.TaxBreakdowns.Select(t => t.TaxCode).Should().BeEquivalentTo("VAT", "NHIL", "GETFL");
        result.TaxBreakdowns.Should().NotContain(t => t.TaxCode.Contains("COVID", StringComparison.OrdinalIgnoreCase));

        var covid = await db.Taxes.SingleAsync(t => t.TenantId == tenantId && t.Code == "COVID");
        covid.IsActive.Should().BeFalse();
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-GhanaTax")]
    [Trait("Category", "Tax")]
    public async Task TaxCalculation_ShouldRejectCrossTenantTaxGroup()
    {
        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        await using var db = CreateContext();
        SeedTenant(db, tenantId);
        SeedTenant(db, otherTenantId, "OTH");
        var otherGroup = SeedEmptyTaxGroup(db, otherTenantId, TaxApplicability.Sales);
        await db.SaveChangesAsync();

        var engine = new TaxCalculationEngine(db, CreateCurrentUser(tenantId).Object, Mock.Of<ILogger<TaxCalculationEngine>>());

        var act = () => engine.CalculateTaxesAsync(new TaxCalculationRequestDto
        {
            BaseAmount = 100m,
            TaxGroupId = otherGroup.Id,
            TransactionDate = new DateTime(2026, 7, 6),
            TransactionType = TaxTransactionType.SaleOfGoods
        });

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Tax group was not found for this tenant.");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-GhanaTax")]
    [Trait("Category", "Tax")]
    public async Task TaxConfiguration_ShouldRejectCurrentActiveCovidLevyAndCrossTenantTaxAccounts()
    {
        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        await using var db = CreateContext();
        SeedTenant(db, tenantId);
        SeedTenant(db, otherTenantId, "OTH");
        var otherAccount = SeedAccount(db, otherTenantId, "2299", AccountType.Liability, isControlAccount: true, allowDirectPosting: false);
        await db.SaveChangesAsync();

        var service = new TaxConfigurationService(db, CreateCurrentUser(tenantId).Object, Mock.Of<ILogger<TaxConfigurationService>>());

        var covid = () => service.CreateTaxAsync(new CreateTaxDto
        {
            Code = "COVID19",
            Name = "COVID-19 Health Recovery Levy",
            Rate = 1m,
            EffectiveFrom = new DateTime(2026, 7, 1),
            IsActive = true
        });
        await covid.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("COVID-19 Health Recovery Levy must not be active for current Ghana postings.");

        var crossTenantAccount = () => service.CreateTaxAsync(new CreateTaxDto
        {
            Code = "VAT",
            Name = "Value Added Tax",
            Rate = 15m,
            EffectiveFrom = new DateTime(2026, 7, 1),
            TaxPayableAccountId = otherAccount.Id
        });
        await crossTenantAccount.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Configured tax payable account was not found for this tenant.");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-GhanaTax")]
    [Trait("Category", "Tax")]
    public async Task ApInvoicePosting_ShouldUseConfiguredInputTaxAccountsAndCreateTaxSnapshots()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedApprovedApInvoiceAsync(db, tenantId, withTax: true);
        SeedGhanaTaxConfiguration(db, tenantId, fixture.TaxReceivableAccount.Id, fixture.TaxPayableAccount.Id);
        await db.SaveChangesAsync();

        var service = CreateApService(db, tenantId);

        await service.PostAsync(fixture.Invoice.Id);

        var journal = await db.JournalEntries
            .Include(j => j.Transactions)
            .SingleAsync(j => j.SourceDocumentType == "VendorInvoice" && j.SourceDocumentId == fixture.Invoice.Id);
        journal.Transactions.Where(t => t.TransactionTag != null && t.TransactionTag.StartsWith("AP-Tax-"))
            .Should().HaveCount(3);
        journal.Transactions.Where(t => t.AccountId == fixture.TaxReceivableAccount.Id).Sum(t => t.DebitAmount).Should().Be(20m);
        journal.TotalCreditAmount.Should().Be(fixture.Invoice.SubTotal + fixture.Invoice.TaxAmount);
        fixture.Invoice.TotalAmount.Should().Be(fixture.Invoice.SubTotal + fixture.Invoice.TaxAmount);
        (await db.Set<TaxCalculation>().CountAsync(t => t.TenantId == tenantId && t.DocumentType == "VendorInvoice" && t.DocumentId == fixture.Invoice.Id)).Should().Be(3);
        (await db.AuditLogs.CountAsync(a => a.TenantId == tenantId && a.Action == FinanceAuditEvents.TaxCalculatedOnApInvoice)).Should().Be(1);
        (await db.AuditLogs.CountAsync(a => a.TenantId == tenantId && a.Action == FinanceAuditEvents.TaxPosted)).Should().Be(1);
        (await db.AuditLogs.CountAsync(a => a.TenantId == tenantId && a.Action == FinanceAuditEvents.TaxConfigurationUsedInPosting)).Should().Be(1);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-GhanaTax")]
    [Trait("Category", "Tax")]
    public async Task ArInvoicePosting_ShouldUseConfiguredOutputTaxAccountsAndCreateTaxSnapshots()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedSentArInvoiceAsync(db, tenantId, withTax: true);
        var taxGroups = SeedGhanaTaxConfiguration(db, tenantId, fixture.TaxReceivableAccount.Id, fixture.TaxPayableAccount.Id);
        fixture.Invoice.LineItems.Single().TaxGroupId = taxGroups.SalesGroupId;
        await db.SaveChangesAsync();

        var service = CreateArService(db, tenantId);

        await service.PostAsync(fixture.Invoice.Id);

        var journal = await db.JournalEntries
            .Include(j => j.Transactions)
            .SingleAsync(j => j.SourceDocumentType == "CustomerInvoice" && j.SourceDocumentId == fixture.Invoice.Id);
        journal.Transactions.Where(t => t.TransactionTag != null && t.TransactionTag.StartsWith("AR-Tax-"))
            .Should().HaveCount(3);
        journal.Transactions.Where(t => t.AccountId == fixture.TaxPayableAccount.Id).Sum(t => t.CreditAmount).Should().Be(20m);
        journal.TotalDebitAmount.Should().Be(fixture.Invoice.SubTotal + fixture.Invoice.TaxAmount);
        fixture.Invoice.TotalAmount.Should().Be(fixture.Invoice.SubTotal + fixture.Invoice.TaxAmount);
        (await db.Set<TaxCalculation>().CountAsync(t => t.TenantId == tenantId && t.DocumentType == "CustomerInvoice" && t.DocumentId == fixture.Invoice.Id)).Should().Be(3);
        (await db.AuditLogs.CountAsync(a => a.TenantId == tenantId && a.Action == FinanceAuditEvents.TaxCalculatedOnArInvoice)).Should().Be(1);
        (await db.AuditLogs.CountAsync(a => a.TenantId == tenantId && a.Action == FinanceAuditEvents.TaxPosted)).Should().Be(1);
        (await db.AuditLogs.CountAsync(a => a.TenantId == tenantId && a.Action == FinanceAuditEvents.TaxConfigurationUsedInPosting)).Should().Be(1);
    }

    [Theory]
    [InlineData(TaxTreatment.Exempt)]
    [InlineData(TaxTreatment.ZeroRated)]
    [InlineData(TaxTreatment.OutOfScope)]
    [Trait("Batch", "FinanceGoLive-GhanaTax")]
    [Trait("Category", "Tax")]
    public async Task ExplicitNoTaxTreatments_ShouldPostWithoutTaxLinesOrSnapshots(TaxTreatment treatment)
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var apFixture = await SeedApprovedApInvoiceAsync(db, tenantId, withTax: false);
        apFixture.Invoice.LineItems.Single().TaxTreatment = treatment;
        apFixture.Invoice.LineItems.Single().TaxRate = 0m;
        apFixture.Invoice.LineItems.Single().TaxAmount = 0m;
        await db.SaveChangesAsync();

        await CreateApService(db, tenantId).PostAsync(apFixture.Invoice.Id);

        var apJournal = await db.JournalEntries
            .Include(j => j.Transactions)
            .SingleAsync(j => j.SourceDocumentType == "VendorInvoice" && j.SourceDocumentId == apFixture.Invoice.Id);
        apJournal.Transactions.Should().NotContain(t => t.TransactionTag != null && t.TransactionTag.StartsWith("AP-Tax-"));
        (await db.Set<TaxCalculation>().CountAsync(t => t.TenantId == tenantId && t.DocumentType == "VendorInvoice" && t.DocumentId == apFixture.Invoice.Id)).Should().Be(0);

        var arTenantId = Guid.NewGuid();
        var arFixture = await SeedSentArInvoiceAsync(db, arTenantId, withTax: false);
        arFixture.Invoice.LineItems.Single().TaxTreatment = treatment;
        arFixture.Invoice.LineItems.Single().TaxRate = 0m;
        arFixture.Invoice.LineItems.Single().TaxAmount = 0m;
        await db.SaveChangesAsync();

        await CreateArService(db, arTenantId).PostAsync(arFixture.Invoice.Id);

        var arJournal = await db.JournalEntries
            .Include(j => j.Transactions)
            .SingleAsync(j => j.SourceDocumentType == "CustomerInvoice" && j.SourceDocumentId == arFixture.Invoice.Id);
        arJournal.Transactions.Should().NotContain(t => t.TransactionTag != null && t.TransactionTag.StartsWith("AR-Tax-"));
        (await db.Set<TaxCalculation>().CountAsync(t => t.TenantId == arTenantId && t.DocumentType == "CustomerInvoice" && t.DocumentId == arFixture.Invoice.Id)).Should().Be(0);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-GhanaTax")]
    [Trait("Category", "Tax")]
    public async Task TaxCalculation_ShouldUseHistoricalEffectiveRateAndDeterministicRounding()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        SeedTenant(db, tenantId);
        var payableAccount = SeedAccount(db, tenantId, "2201", AccountType.Liability, isControlAccount: true, allowDirectPosting: false);
        var receivableAccount = SeedAccount(db, tenantId, "1401", AccountType.Asset, isControlAccount: true, allowDirectPosting: false);
        await db.SaveChangesAsync();

        var currentUser = CreateCurrentUser(tenantId);
        var service = new TaxConfigurationService(db, currentUser.Object, Mock.Of<ILogger<TaxConfigurationService>>(), CreateAuditService(db, currentUser));
        var created = await service.CreateTaxAsync(new CreateTaxDto
        {
            Code = "ROUND",
            Name = "Rounding Tax",
            Rate = 10m,
            EffectiveFrom = new DateTime(2026, 1, 1),
            TaxPayableAccountId = payableAccount.Id,
            TaxReceivableAccountId = receivableAccount.Id
        });
        var group = SeedTaxGroup(db, tenantId, "ROUND-GRP", TaxApplicability.Sales);
        AddComponent(db, tenantId, group.Id, created.Id, 1);
        await db.SaveChangesAsync();

        await service.UpdateTaxAsync(created.Id, new UpdateTaxDto
        {
            Rate = 15m,
            EffectiveFrom = new DateTime(2026, 8, 1)
        });

        var engine = new TaxCalculationEngine(db, currentUser.Object, Mock.Of<ILogger<TaxCalculationEngine>>());
        var historical = await engine.CalculateTaxesAsync(new TaxCalculationRequestDto
        {
            BaseAmount = 100m,
            TaxGroupId = group.Id,
            TransactionDate = new DateTime(2026, 7, 6),
            TransactionType = TaxTransactionType.SaleOfServices
        });
        var current = await engine.CalculateTaxesAsync(new TaxCalculationRequestDto
        {
            BaseAmount = 100m,
            TaxGroupId = group.Id,
            TransactionDate = new DateTime(2026, 8, 1),
            TransactionType = TaxTransactionType.SaleOfServices
        });
        var rounded = await engine.CalculateTaxesAsync(new TaxCalculationRequestDto
        {
            BaseAmount = 0.25m,
            TaxGroupId = group.Id,
            TransactionDate = new DateTime(2026, 7, 6),
            TransactionType = TaxTransactionType.SaleOfServices
        });

        historical.TotalTaxAmount.Should().Be(10m);
        current.TotalTaxAmount.Should().Be(15m);
        rounded.TotalTaxAmount.Should().Be(0.03m);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-GhanaTax")]
    [Trait("Category", "Tax")]
    public async Task TaxConfiguration_ShouldRejectBackdatedRateChangesAndAuditConfigurationEvents()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        SeedTenant(db, tenantId);
        var payableAccount = SeedAccount(db, tenantId, "2202", AccountType.Liability, isControlAccount: true, allowDirectPosting: false);
        var receivableAccount = SeedAccount(db, tenantId, "1402", AccountType.Asset, isControlAccount: true, allowDirectPosting: false);
        var replacementReceivable = SeedAccount(db, tenantId, "1403", AccountType.Asset, isControlAccount: true, allowDirectPosting: false);
        await db.SaveChangesAsync();

        var currentUser = CreateCurrentUser(tenantId);
        var service = new TaxConfigurationService(db, currentUser.Object, Mock.Of<ILogger<TaxConfigurationService>>(), CreateAuditService(db, currentUser));
        var created = await service.CreateTaxAsync(new CreateTaxDto
        {
            Code = "AUDIT",
            Name = "Audited Tax",
            Rate = 10m,
            EffectiveFrom = new DateTime(2026, 1, 1),
            TaxPayableAccountId = payableAccount.Id,
            TaxReceivableAccountId = receivableAccount.Id
        });

        await service.UpdateTaxAsync(created.Id, new UpdateTaxDto
        {
            Rate = 12m,
            EffectiveFrom = new DateTime(2026, 8, 1)
        });

        var backdated = () => service.UpdateTaxAsync(created.Id, new UpdateTaxDto
        {
            Rate = 13m,
            EffectiveFrom = new DateTime(2026, 7, 1)
        });
        await backdated.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("New tax rate effective date must be after the current effective date. Use a new effective-dated version instead of overwriting historical rates.");

        await service.UpdateTaxAsync(created.Id, new UpdateTaxDto
        {
            TaxReceivableAccountId = replacementReceivable.Id
        });

        await service.UpdateTaxAsync(created.Id, new UpdateTaxDto
        {
            ClearTaxReceivableAccount = true
        });

        var cleared = await db.Set<Tax>()
            .AsNoTracking()
            .FirstAsync(t => t.Id == created.Id);
        cleared.TaxReceivableAccountId.Should().BeNull();

        await service.DeleteTaxAsync(created.Id);

        var actions = await db.AuditLogs
            .Where(a => a.TenantId == tenantId && a.ResourceId == created.Id.ToString())
            .Select(a => a.Action)
            .ToListAsync();

        actions.Should().Contain(FinanceAuditEvents.TaxRuleCreated);
        actions.Should().Contain(FinanceAuditEvents.TaxRuleUpdated);
        actions.Should().Contain(FinanceAuditEvents.TaxAccountMappingChanged);
        actions.Should().Contain(FinanceAuditEvents.TaxRuleDeactivated);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-GhanaTax")]
    [Trait("Category", "Tax")]
    public async Task InactiveModifiedTax_ShouldRemainVisibleLockedAndExposeCompleteConfigurationHistory()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        SeedTenant(db, tenantId);
        var payableAccount = SeedAccount(db, tenantId, "2210", AccountType.Liability, isControlAccount: true, allowDirectPosting: false);
        var receivableAccount = SeedAccount(db, tenantId, "1410", AccountType.Asset, isControlAccount: true, allowDirectPosting: false);
        await db.SaveChangesAsync();

        var currentUser = CreateCurrentUser(tenantId);
        var service = new TaxConfigurationService(
            db,
            currentUser.Object,
            Mock.Of<ILogger<TaxConfigurationService>>(),
            CreateAuditService(db, currentUser));
        var created = await service.CreateTaxAsync(new CreateTaxDto
        {
            Code = "LOCKED",
            Name = "Locked Historical Tax",
            Description = "Original configuration",
            Rate = 10m,
            EffectiveFrom = new DateTime(2026, 1, 1),
            TaxPayableAccountId = payableAccount.Id,
            TaxReceivableAccountId = receivableAccount.Id
        });

        await service.UpdateTaxAsync(created.Id, new UpdateTaxDto
        {
            Rate = 12.5m,
            EffectiveFrom = new DateTime(2026, 8, 1),
            ChangeReason = "Approved statutory rate change"
        });
        await service.UpdateTaxAsync(created.Id, new UpdateTaxDto
        {
            IsActive = false,
            ChangeReason = "Tax retired by statutory authority"
        });

        var inactive = await service.GetTaxByIdAsync(created.Id);
        inactive.Should().NotBeNull();
        inactive!.IsActive.Should().BeFalse();
        inactive.IsLocked.Should().BeTrue();

        var versions = await service.GetTaxConfigurationVersionsAsync(created.Id);
        versions.Should().HaveCount(3);
        versions.Single(v => v.VersionNumber == 1).Should().Match<TaxConfigurationVersionDto>(v =>
            v.Rate == 10m
            && v.Description == "Original configuration"
            && v.TaxPayableAccountId == payableAccount.Id
            && v.TaxReceivableAccountId == receivableAccount.Id
            && v.ChangeReason == "Approved statutory rate change"
            && !v.IsCurrent);
        versions.Single(v => v.IsCurrent).IsActive.Should().BeFalse();

        await service.Invoking(s => s.UpdateTaxAsync(created.Id, new UpdateTaxDto { Name = "Forbidden edit" }))
            .Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*locked*");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-GhanaTax")]
    [Trait("Category", "Tax")]
    public async Task TaxCalculation_ShouldRejectCrossTenantManualTaxSelection()
    {
        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        await using var db = CreateContext();
        SeedTenant(db, tenantId);
        SeedTenant(db, otherTenantId, "OTH");
        var otherPayable = SeedAccount(db, otherTenantId, "2290", AccountType.Liability, isControlAccount: true, allowDirectPosting: false);
        var otherReceivable = SeedAccount(db, otherTenantId, "1490", AccountType.Asset, isControlAccount: true, allowDirectPosting: false);
        var otherTax = SeedTax(db, otherTenantId, "OTH-WHT", "Other Withholding", 7m, TaxCategory.Withholding, otherReceivable.Id, otherPayable.Id, new DateTime(2026, 1, 1));
        await db.SaveChangesAsync();

        var engine = new TaxCalculationEngine(db, CreateCurrentUser(tenantId).Object, Mock.Of<ILogger<TaxCalculationEngine>>());
        var act = () => engine.CalculateTaxesAsync(new TaxCalculationRequestDto
        {
            BaseAmount = 100m,
            ManualTaxIds = new List<Guid> { otherTax.Id },
            TransactionDate = new DateTime(2026, 7, 6),
            TransactionType = TaxTransactionType.SaleOfServices
        });

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("One or more selected taxes were not found for this tenant.");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-GhanaTax")]
    [Trait("Category", "Tax")]
    public async Task TaxCalculation_ShouldNotApplyCrossTenantTaxRule()
    {
        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        await using var db = CreateContext();
        SeedTenant(db, tenantId);
        SeedTenant(db, otherTenantId, "OTH");
        var otherPayable = SeedAccount(db, otherTenantId, "2291", AccountType.Liability, isControlAccount: true, allowDirectPosting: false);
        var otherReceivable = SeedAccount(db, otherTenantId, "1491", AccountType.Asset, isControlAccount: true, allowDirectPosting: false);
        var otherTax = SeedTax(db, otherTenantId, "OTH-VAT", "Other VAT", 20m, TaxCategory.Standard, otherReceivable.Id, otherPayable.Id, new DateTime(2026, 1, 1));
        var otherGroup = SeedTaxGroup(db, otherTenantId, "OTH-RULE", TaxApplicability.Sales);
        AddComponent(db, otherTenantId, otherGroup.Id, otherTax.Id, 1);
        db.TaxRules.Add(new TaxRule
        {
            Id = Guid.NewGuid(),
            TenantId = otherTenantId,
            Name = "Cross-tenant sale rule",
            Priority = 1,
            TaxGroupId = otherGroup.Id,
            TransactionType = TaxTransactionType.SaleOfServices.ToString(),
            IsActive = true
        });
        await db.SaveChangesAsync();

        var engine = new TaxCalculationEngine(db, CreateCurrentUser(tenantId).Object, Mock.Of<ILogger<TaxCalculationEngine>>());
        var result = await engine.CalculateTaxesAsync(new TaxCalculationRequestDto
        {
            BaseAmount = 100m,
            TransactionDate = new DateTime(2026, 7, 6),
            TransactionType = TaxTransactionType.SaleOfServices
        });

        result.TotalTaxAmount.Should().Be(0m);
        result.TaxBreakdowns.Should().BeEmpty();
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-GhanaTax")]
    [Trait("Category", "Tax")]
    public async Task ApAndArInvoicePosting_ShouldRejectInvalidTaxConfigurationInsteadOfFallingBack()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var apFixture = await SeedApprovedApInvoiceAsync(db, tenantId, withTax: true);

        var apAct = () => CreateApService(db, tenantId).PostAsync(apFixture.Invoice.Id);
        await apAct.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("AP invoice configured tax calculation does not reconcile to the invoice tax total.");

        var arTenantId = Guid.NewGuid();
        var arFixture = await SeedSentArInvoiceAsync(db, arTenantId, withTax: true);

        var arAct = () => CreateArService(db, arTenantId).PostAsync(arFixture.Invoice.Id);
        await arAct.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("AR invoice configured tax calculation does not reconcile to the invoice tax total.");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-GhanaTax")]
    [Trait("Category", "Tax")]
    public async Task ClosedPeriodTaxPosting_ShouldBeRejectedThroughFinancePostingEngine()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedApprovedApInvoiceAsync(db, tenantId, withTax: true);
        var taxGroups = SeedGhanaTaxConfiguration(db, tenantId, fixture.TaxReceivableAccount.Id, fixture.TaxPayableAccount.Id);
        fixture.Invoice.LineItems.Single().TaxGroupId = taxGroups.PurchaseGroupId;
        var period = await db.FiscalPeriods.SingleAsync(p => p.TenantId == tenantId);
        period.IsOpen = false;
        period.IsClosed = true;
        period.PeriodStatus = "Closed";
        await db.SaveChangesAsync();

        var act = () => CreateApService(db, tenantId).PostAsync(fixture.Invoice.Id);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Posting period is not open.");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-GhanaTax")]
    [Trait("Category", "Tax")]
    public async Task PostedTaxSnapshots_ShouldRemainUnchangedAfterLaterTaxConfigurationEdits()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedApprovedApInvoiceAsync(db, tenantId, withTax: true);
        var taxGroups = SeedGhanaTaxConfiguration(db, tenantId, fixture.TaxReceivableAccount.Id, fixture.TaxPayableAccount.Id);
        fixture.Invoice.LineItems.Single().TaxGroupId = taxGroups.PurchaseGroupId;
        await db.SaveChangesAsync();

        await CreateApService(db, tenantId).PostAsync(fixture.Invoice.Id);
        var originalSnapshots = await db.Set<TaxCalculation>()
            .Where(t => t.TenantId == tenantId && t.DocumentType == "VendorInvoice" && t.DocumentId == fixture.Invoice.Id)
            .OrderBy(t => t.TaxId)
            .Select(t => new { t.TaxId, t.TaxRate, t.TaxAmount })
            .ToListAsync();
        var originalTaxLineTotal = await db.AccountTransactions
            .Where(t => t.TenantId == tenantId && t.TransactionTag != null && t.TransactionTag.StartsWith("AP-Tax-"))
            .SumAsync(t => t.DebitAmount);

        var currentUser = CreateCurrentUser(tenantId);
        var service = new TaxConfigurationService(db, currentUser.Object, Mock.Of<ILogger<TaxConfigurationService>>(), CreateAuditService(db, currentUser));
        var vat = await db.Taxes.SingleAsync(t => t.TenantId == tenantId && t.Code == "VAT");
        await service.UpdateTaxAsync(vat.Id, new UpdateTaxDto
        {
            Rate = 18m,
            EffectiveFrom = new DateTime(2026, 8, 1)
        });

        var currentSnapshots = await db.Set<TaxCalculation>()
            .Where(t => t.TenantId == tenantId && t.DocumentType == "VendorInvoice" && t.DocumentId == fixture.Invoice.Id)
            .OrderBy(t => t.TaxId)
            .Select(t => new { t.TaxId, t.TaxRate, t.TaxAmount })
            .ToListAsync();
        var currentTaxLineTotal = await db.AccountTransactions
            .Where(t => t.TenantId == tenantId && t.TransactionTag != null && t.TransactionTag.StartsWith("AP-Tax-"))
            .SumAsync(t => t.DebitAmount);

        currentSnapshots.Should().BeEquivalentTo(originalSnapshots);
        currentTaxLineTotal.Should().Be(originalTaxLineTotal);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-GhanaTax")]
    [Trait("Category", "Tax")]
    public async Task ApPaymentPosting_ShouldUseConfiguredWithholdingPayableAccount()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedApprovedApPaymentWithWithholdingAsync(db, tenantId);

        await CreateApPaymentService(db, tenantId).PostAsync(fixture.Payment.Id);

        var journal = await db.JournalEntries
            .Include(j => j.Transactions)
            .SingleAsync(j => j.SourceDocumentType == "VendorPayment" && j.SourceDocumentId == fixture.Payment.Id);

        journal.Transactions.Single(t => t.TransactionTag == "AP-Control").DebitAmount.Should().Be(100m);
        journal.Transactions.Single(t => t.TransactionTag == "AP-Bank").CreditAmount.Should().Be(95m);
        journal.Transactions.Single(t => t.TransactionTag == "AP-WHT").CreditAmount.Should().Be(5m);
        journal.Transactions.Single(t => t.TransactionTag == "AP-WHT").AccountId.Should().Be(fixture.WithholdingPayableAccount.Id);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-GhanaTax")]
    [Trait("Category", "Tax")]
    public async Task ArReceiptPosting_ShouldUseConfiguredVatWithholdingAndWhtReceivableAccounts()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedApprovedArReceiptWithWithholdingAsync(db, tenantId);

        await CreateArPaymentService(db, tenantId).PostAsync(fixture.Payment.Id);

        var journal = await db.JournalEntries
            .Include(j => j.Transactions)
            .SingleAsync(j => j.SourceDocumentType == "CustomerPayment" && j.SourceDocumentId == fixture.Payment.Id);

        journal.Transactions.Single(t => t.TransactionTag == "AR-Bank").DebitAmount.Should().Be(88m);
        journal.Transactions.Single(t => t.TransactionTag == "AR-WHT").DebitAmount.Should().Be(5m);
        journal.Transactions.Single(t => t.TransactionTag == "AR-WHT").AccountId.Should().Be(fixture.WithholdingReceivableAccount.Id);
        journal.Transactions.Single(t => t.TransactionTag == "AR-VAT-WHT").DebitAmount.Should().Be(7m);
        journal.Transactions.Single(t => t.TransactionTag == "AR-VAT-WHT").AccountId.Should().Be(fixture.VatWithholdingReceivableAccount.Id);
        journal.Transactions.Single(t => t.TransactionTag == "AR-Control").CreditAmount.Should().Be(100m);
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"ghana-tax-{Guid.NewGuid()}")
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new ApplicationDbContext(options);
    }

    private static VendorInvoiceService CreateApService(ApplicationDbContext db, Guid tenantId)
    {
        var currentUser = CreateCurrentUser(tenantId);
        var auditService = CreateAuditService(db, currentUser);
        var taxEngine = new TaxCalculationEngine(db, currentUser.Object, Mock.Of<ILogger<TaxCalculationEngine>>());
        var postingEngine = new FinancePostingEngine(db, currentUser.Object, Mock.Of<ILogger<FinancePostingEngine>>(), auditService);

        return new VendorInvoiceService(
            new UnitOfWork(db),
            currentUser.Object,
            Mock.Of<IInventoryValuationService>(),
            Mock.Of<ILogger<VendorInvoiceService>>(),
            CreateDocumentNumberingService(),
            Mock.Of<IWorkflowService>(),
            postingEngine,
            auditService,
            taxEngine);
    }

    private static InvoiceService CreateArService(ApplicationDbContext db, Guid tenantId)
    {
        var currentUser = CreateCurrentUser(tenantId);
        var auditService = CreateAuditService(db, currentUser);
        var taxEngine = new TaxCalculationEngine(db, currentUser.Object, Mock.Of<ILogger<TaxCalculationEngine>>());
        var postingEngine = new FinancePostingEngine(db, currentUser.Object, Mock.Of<ILogger<FinancePostingEngine>>(), auditService);

        return new InvoiceService(
            new UnitOfWork(db),
            currentUser.Object,
            taxEngine,
            Mock.Of<IInventoryValuationService>(),
            Mock.Of<ILogger<InvoiceService>>(),
            CreateDocumentNumberingService(),
            postingEngine,
            auditService);
    }

    private static VendorPaymentService CreateApPaymentService(ApplicationDbContext db, Guid tenantId)
    {
        var currentUser = CreateCurrentUser(tenantId);
        var auditService = CreateAuditService(db, currentUser);
        var postingEngine = new FinancePostingEngine(db, currentUser.Object, Mock.Of<ILogger<FinancePostingEngine>>(), auditService);
        var tenantSettings = new Mock<ITenantSettingsService>();
        tenantSettings.Setup(x => x.GetBaseCurrencyAsync()).ReturnsAsync("GHS");
        var payment = db.Set<VendorPayment>().Single(item => item.TenantId == tenantId);
        var configuredTax = db.Taxes.Single(item => item.Id == payment.WithholdingTaxId);
        var withholdingService = new Mock<IWithholdingTaxCertificateService>();
        withholdingService
            .Setup(service => service.CalculateApWithholdingAsync(
                It.IsAny<WhtCalculationRequestDto>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((WhtCalculationRequestDto request, CancellationToken _) =>
                new WhtCalculationResultDto
                {
                    TaxId = configuredTax.Id,
                    TaxCode = configuredTax.Code,
                    TaxName = configuredTax.Name,
                    TaxRate = configuredTax.Rate,
                    TaxableBase = request.TaxableBase,
                    WithholdingAmount = 5m,
                    TaxPayableAccountId = configuredTax.TaxPayableAccountId,
                    CalculationNote = "Statutory posting regression fixture"
                });
        var invoicePaymentSod = new Mock<IProcurementInvoicePaymentSodService>();
        invoicePaymentSod
            .Setup(service => service.RevalidatePaymentAuthorizationAsync(
                payment.Id,
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var procurementControlEvents = new Mock<IProcurementControlEventService>();
        procurementControlEvents
            .Setup(service => service.RecordAsync(
                It.IsAny<ProcurementControlEventWriteRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProcurementControlEventWriteRequest request, CancellationToken _) =>
                new ProcurementControlEventDto
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    EventKey = request.EventKey,
                    EventType = request.EventType,
                    Action = request.Action,
                    Result = request.Result,
                    SourceType = request.SourceType,
                    SourceId = request.SourceId,
                    SourceReference = request.SourceReference ?? string.Empty,
                    CorrelationId = request.CorrelationId,
                    OccurredAtUtc = request.OccurredAtUtc == default ? DateTime.UtcNow : request.OccurredAtUtc,
                    RecordedAtUtc = DateTime.UtcNow,
                    IntegrityValid = true
                });

        return new VendorPaymentService(
            new UnitOfWork(db),
            currentUser.Object,
            tenantSettings.Object,
            Mock.Of<ILogger<VendorPaymentService>>(),
            CreateDocumentNumberingService(),
            Mock.Of<IWorkflowService>(),
            // Tax tests are concerned with statutory calculation and posting, not data-scope
            // enforcement. Dedicated Finance access-scope tests cover that control boundary.
            Mock.Of<IFinanceAccessScopeService>(),
            new FinanceReversalPolicyService(db, currentUser.Object),
            postingEngine,
            auditService,
            withholdingTaxService: withholdingService.Object,
            procurementControlEvents: procurementControlEvents.Object,
            invoicePaymentSod: invoicePaymentSod.Object);
    }

    private static PaymentService CreateArPaymentService(ApplicationDbContext db, Guid tenantId)
    {
        var currentUser = CreateCurrentUser(tenantId);
        var auditService = CreateAuditService(db, currentUser);
        var postingEngine = new FinancePostingEngine(db, currentUser.Object, Mock.Of<ILogger<FinancePostingEngine>>(), auditService);
        var tenantSettings = new Mock<ITenantSettingsService>();
        tenantSettings.Setup(x => x.GetBaseCurrencyAsync()).ReturnsAsync("GHS");

        return new PaymentService(
            new UnitOfWork(db),
            currentUser.Object,
            tenantSettings.Object,
            Mock.Of<ILogger<PaymentService>>(),
            CreateDocumentNumberingService(),
            Mock.Of<IFinanceAccessScopeService>(),
            new FinanceReversalPolicyService(db, currentUser.Object),
            postingEngine,
            auditService);
    }

    private static IDocumentNumberingService CreateDocumentNumberingService()
    {
        var numbering = new Mock<IDocumentNumberingService>();
        numbering
            .Setup(service => service.GenerateAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Guid?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<string?>(),
                It.IsAny<Guid?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => $"TEST-{Guid.NewGuid():N}");
        return numbering.Object;
    }

    private static FinanceAuditService CreateAuditService(ApplicationDbContext db, Mock<ICurrentUserService> currentUser)
    {
        return new FinanceAuditService(
            db,
            currentUser.Object,
            new HttpContextAccessor
            {
                HttpContext = new DefaultHttpContext { TraceIdentifier = "trace-ghana-tax" }
            });
    }

    private static Mock<ICurrentUserService> CreateCurrentUser(Guid tenantId)
    {
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(x => x.TenantId).Returns(tenantId);
        currentUser.SetupGet(x => x.Claims).Returns(new Dictionary<string, string>());
        currentUser.SetupGet(x => x.UserId).Returns(Guid.NewGuid().ToString());
        currentUser.SetupGet(x => x.UserName).Returns("ghana.tax.tester");
        currentUser.SetupGet(x => x.IpAddress).Returns("127.0.0.1");
        currentUser.SetupGet(x => x.UserAgent).Returns("ghana-tax-tests");
        currentUser.SetupGet(x => x.IsAuthenticated).Returns(true);
        currentUser.SetupGet(x => x.Roles).Returns(Array.Empty<string>());
        return currentUser;
    }

    private static async Task<ApInvoiceFixture> SeedApprovedApInvoiceAsync(ApplicationDbContext db, Guid tenantId, bool withTax)
    {
        SeedTenant(db, tenantId);
        SeedOpenPeriod(db, tenantId);
        var expenseAccount = SeedAccount(db, tenantId, "6000", AccountType.Expense);
        var apAccount = SeedAccount(db, tenantId, "2000", AccountType.Liability, isControlAccount: true, allowDirectPosting: false);
        var taxReceivable = SeedAccount(db, tenantId, "1400", AccountType.Asset, isControlAccount: true, allowDirectPosting: false);
        var taxPayable = SeedAccount(db, tenantId, "2200", AccountType.Liability, isControlAccount: true, allowDirectPosting: false);
        var supplier = SeedSupplier(db, tenantId, apAccount.Id, expenseAccount.Id);
        var supplierRole = db.BusinessPartnerRoles.Local.Single(role =>
            role.BusinessPartnerId == supplier.Id && role.RoleType == BusinessPartnerRoleType.Supplier);
        var supplierProfile = db.BusinessPartnerApProfileVersions.Local.Single(profile =>
            profile.BusinessPartnerRoleId == supplierRole.Id);

        db.Set<FinanceSettings>().Add(new FinanceSettings
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            BaseCurrency = "GHS",
            ControlAccountApId = apAccount.Id,
            ControlAccountTaxId = taxReceivable.Id
        });

        var invoice = new VendorInvoice
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            InvoiceNumber = "VI-TAX-001",
            SupplierInvoiceNumber = "SUP-TAX-001",
            BusinessPartnerId = supplier.Id,
            BusinessPartnerRoleId = supplierRole.Id,
            BusinessPartnerApProfileVersionId = supplierProfile.Id,
            BusinessPartnerCode = supplier.SupplierCode,
            SupplierName = supplier.Name,
            InvoiceDate = new DateTime(2026, 7, 6),
            ReceivedDate = new DateTime(2026, 7, 6),
            DueDate = new DateTime(2026, 8, 5),
            SubTotal = 100m,
            TaxAmount = withTax ? 20m : 0m,
            TotalAmount = withTax ? 120m : 100m,
            CurrencyCode = "GHS",
            ExchangeRate = 1m,
            BaseCurrencyAmount = withTax ? 120m : 100m,
            Status = VendorInvoiceStatus.Approved,
            ApprovalStatus = "Approved",
            ApprovedById = Guid.NewGuid(),
            ApprovedDate = DateTime.UtcNow,
            ApAccountId = apAccount.Id,
            WithholdingContractReference = "CONTRACT-WHT-001",
            WithholdingSupplyCategory = WhtSupplyCategory.Services,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed"
        };

        invoice.LineItems.Add(new VendorInvoiceLineItem
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            VendorInvoiceId = invoice.Id,
            LineItemType = "Expense",
            GLAccountId = expenseAccount.Id,
            Description = "Professional services",
            Quantity = 1m,
            UnitPrice = 100m,
            TaxRate = withTax ? 20m : 0m,
            TaxAmount = withTax ? 20m : 0m,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed"
        });

        db.VendorInvoices.Add(invoice);
        await db.SaveChangesAsync();
        return new ApInvoiceFixture(invoice, taxReceivable, taxPayable);
    }

    private static async Task<ArInvoiceFixture> SeedSentArInvoiceAsync(ApplicationDbContext db, Guid tenantId, bool withTax)
    {
        SeedTenant(db, tenantId);
        SeedOpenPeriod(db, tenantId);
        var arAccount = SeedAccount(db, tenantId, "1200", AccountType.Asset, isControlAccount: true, allowDirectPosting: false);
        var revenueAccount = SeedAccount(db, tenantId, "4000", AccountType.Revenue);
        var taxReceivable = SeedAccount(db, tenantId, "1400", AccountType.Asset, isControlAccount: true, allowDirectPosting: false);
        var taxPayable = SeedAccount(db, tenantId, "2200", AccountType.Liability, isControlAccount: true, allowDirectPosting: false);
        var customer = SeedCustomer(db, tenantId, arAccount.Id);
        var customerRole = db.BusinessPartnerRoles.Local.Single(role =>
            role.BusinessPartnerId == customer.Id && role.RoleType == BusinessPartnerRoleType.Customer);
        var customerProfile = db.BusinessPartnerArProfileVersions.Local.Single(profile =>
            profile.BusinessPartnerRoleId == customerRole.Id);

        db.Set<FinanceSettings>().Add(new FinanceSettings
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            BaseCurrency = "GHS",
            ControlAccountArId = arAccount.Id,
            ControlAccountTaxId = taxPayable.Id
        });

        var invoice = new Invoice
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            InvoiceNumber = "INV-TAX-001",
            BusinessPartnerId = customer.Id,
            BusinessPartnerRoleId = customerRole.Id,
            BusinessPartnerArProfileVersionId = customerProfile.Id,
            CustomerName = customer.PartnerName,
            CustomerAddress = customer.PhysicalAddress,
            InvoiceDate = new DateTime(2026, 7, 6),
            DueDate = new DateTime(2026, 8, 5),
            SubTotal = 100m,
            TaxAmount = withTax ? 20m : 0m,
            TotalAmount = withTax ? 120m : 100m,
            CurrencyCode = "GHS",
            ExchangeRate = 1m,
            BaseCurrencyAmount = withTax ? 120m : 100m,
            PaymentTermsDays = 30,
            Status = InvoiceStatus.Sent,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed"
        };

        invoice.LineItems.Add(new InvoiceLineItem
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            InvoiceId = invoice.Id,
            LineItemType = LineItemType.GLAccount,
            GLAccountId = revenueAccount.Id,
            Description = "Consulting services",
            Quantity = 1m,
            UnitPrice = 100m,
            TaxRate = withTax ? 20m : 0m,
            TaxAmount = withTax ? 20m : 0m,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed"
        });

        db.Invoices.Add(invoice);
        await db.SaveChangesAsync();
        return new ArInvoiceFixture(invoice, taxReceivable, taxPayable);
    }

    private static async Task<ApPaymentTaxFixture> SeedApprovedApPaymentWithWithholdingAsync(ApplicationDbContext db, Guid tenantId)
    {
        SeedTenant(db, tenantId);
        var period = SeedOpenPeriod(db, tenantId);
        var apAccount = SeedAccount(db, tenantId, "2005", AccountType.Liability, isControlAccount: true, allowDirectPosting: false);
        var bankGlAccount = SeedAccount(db, tenantId, "1105", AccountType.Asset);
        var expenseAccount = SeedAccount(db, tenantId, "6005", AccountType.Expense);
        var withholdingPayable = SeedAccount(db, tenantId, "2305", AccountType.Liability, isControlAccount: true, allowDirectPosting: false);
        var withholdingReceivable = SeedAccount(db, tenantId, "1455", AccountType.Asset, isControlAccount: true, allowDirectPosting: false);
        var supplier = SeedSupplier(db, tenantId, apAccount.Id, expenseAccount.Id);
        var supplierRole = db.BusinessPartnerRoles.Local.Single(role =>
            role.BusinessPartnerId == supplier.Id && role.RoleType == BusinessPartnerRoleType.Supplier);
        var supplierProfile = db.BusinessPartnerApProfileVersions.Local.Single(profile =>
            profile.BusinessPartnerRoleId == supplierRole.Id);
        var bankAccount = SeedBankAccount(db, tenantId, bankGlAccount.Id);
        var withholdingTax = SeedTax(db, tenantId, "AP-WHT", "AP Withholding Tax", 5m, TaxCategory.Withholding, withholdingReceivable.Id, withholdingPayable.Id, new DateTime(2026, 1, 1));

        db.Set<FinanceSettings>().Add(new FinanceSettings
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            BaseCurrency = "GHS",
            ControlAccountApId = apAccount.Id,
            DefaultBankAccountId = bankAccount.Id
        });

        var invoice = SeedPostedVendorInvoiceForPayment(db, tenantId, supplier, apAccount, "VI-WHT-001", new DateTime(2026, 7, 6), 100m, period.Id);
        var payment = new VendorPayment
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            PaymentNumber = "VP-WHT-001",
            BusinessPartnerId = supplier.Id,
            BusinessPartnerRoleId = supplierRole.Id,
            BusinessPartnerApProfileVersionId = supplierProfile.Id,
            BusinessPartnerCode = supplier.SupplierCode,
            BusinessPartnerName = supplier.Name,
            PaymentDate = new DateTime(2026, 7, 6),
            TotalAmount = 95m,
            AllocatedAmount = 95m,
            PaymentMethod = VendorPaymentMethod.BankTransfer,
            CurrencyCode = "GHS",
            ExchangeRate = 1m,
            BankAccountId = bankAccount.Id,
            WithholdingTaxId = withholdingTax.Id,
            WithholdingTaxAmount = 5m,
            Status = VendorPaymentStatus.Authorized,
            AuthorizedById = Guid.NewGuid(),
            AuthorizedDate = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed"
        };

        var allocation = new VendorPaymentAllocation
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            VendorPaymentId = payment.Id,
            VendorInvoiceId = invoice.Id,
            AllocatedAmount = 95m,
            WithholdingTaxAmount = 5m,
            AllocationDate = payment.PaymentDate,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed"
        };

        // Posting owns the invoice settlement transition; seeding it as already paid would model
        // a duplicate payment and correctly trip the over-settlement control.
        invoice.PaidAmount = 0m;
        invoice.Status = VendorInvoiceStatus.Approved;
        db.Set<VendorPayment>().Add(payment);
        db.Set<VendorPaymentAllocation>().Add(allocation);
        await db.SaveChangesAsync();

        return new ApPaymentTaxFixture(payment, withholdingPayable);
    }

    private static async Task<ArReceiptTaxFixture> SeedApprovedArReceiptWithWithholdingAsync(ApplicationDbContext db, Guid tenantId)
    {
        SeedTenant(db, tenantId);
        var period = SeedOpenPeriod(db, tenantId);
        var arAccount = SeedAccount(db, tenantId, "1205", AccountType.Asset, isControlAccount: true, allowDirectPosting: false);
        var bankGlAccount = SeedAccount(db, tenantId, "1115", AccountType.Asset);
        var revenueAccount = SeedAccount(db, tenantId, "4005", AccountType.Revenue);
        var withholdingReceivable = SeedAccount(db, tenantId, "1465", AccountType.Asset, isControlAccount: true, allowDirectPosting: false);
        var vatWithholdingReceivable = SeedAccount(db, tenantId, "1475", AccountType.Asset, isControlAccount: true, allowDirectPosting: false);
        var withholdingPayable = SeedAccount(db, tenantId, "2355", AccountType.Liability, isControlAccount: true, allowDirectPosting: false);
        var customer = SeedCustomer(db, tenantId, arAccount.Id);
        var customerRole = db.BusinessPartnerRoles.Local.Single(role =>
            role.BusinessPartnerId == customer.Id && role.RoleType == BusinessPartnerRoleType.Customer);
        var customerProfile = db.BusinessPartnerArProfileVersions.Local.Single(profile =>
            profile.BusinessPartnerRoleId == customerRole.Id);
        var bankAccount = SeedBankAccount(db, tenantId, bankGlAccount.Id);
        var withholdingTax = SeedTax(db, tenantId, "AR-WHT", "AR Withholding Tax", 5m, TaxCategory.Withholding, withholdingReceivable.Id, withholdingPayable.Id, new DateTime(2026, 1, 1));
        var vatWithholdingTax = SeedTax(db, tenantId, "VAT-WHT", "VAT Withholding", 7m, TaxCategory.VatWithholding, vatWithholdingReceivable.Id, withholdingPayable.Id, new DateTime(2026, 1, 1));

        db.Set<FinanceSettings>().Add(new FinanceSettings
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            BaseCurrency = "GHS",
            ControlAccountArId = arAccount.Id,
            DefaultBankAccountId = bankAccount.Id
        });

        var invoice = SeedPostedCustomerInvoiceForReceipt(db, tenantId, customer, arAccount, revenueAccount, "INV-WHT-001", new DateTime(2026, 7, 6), 100m, period.Id);
        var payment = new CustomerPayment
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            PaymentNumber = "CP-WHT-001",
            BusinessPartnerId = customer.Id,
            BusinessPartnerRoleId = customerRole.Id,
            BusinessPartnerArProfileVersionId = customerProfile.Id,
            BusinessPartnerCode = customer.PartnerCode,
            BusinessPartnerName = customer.PartnerName,
            PaymentDate = new DateTime(2026, 7, 6),
            TotalAmount = 88m,
            AllocatedAmount = 100m,
            PaymentMethod = "BankTransfer",
            CurrencyCode = "GHS",
            ExchangeRate = 1m,
            BankAccountId = bankAccount.Id,
            WithholdingTaxId = withholdingTax.Id,
            WithholdingTaxAmount = 5m,
            VatWithholdingTaxId = vatWithholdingTax.Id,
            VatWithholdingAmount = 7m,
            Status = "Approved",
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed"
        };

        var allocation = new PaymentAllocation
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CustomerPaymentId = payment.Id,
            InvoiceId = invoice.Id,
            // Deductions are invoice-scoped native amounts. For this GHS fixture the functional
            // values are identical, but keeping both snapshots exercises the production model.
            AllocatedAmount = 88m,
            PaymentCurrencyAmount = 88m,
            InvoiceCurrencyCode = "GHS",
            PaymentCurrencyCode = "GHS",
            PaymentExchangeRate = 1m,
            InvoiceSettlementExchangeRate = 1m,
            PaymentFunctionalAmount = 88m,
            WithholdingTaxAmount = 5m,
            WithholdingTaxFunctionalAmount = 5m,
            VatWithholdingAmount = 7m,
            VatWithholdingFunctionalAmount = 7m,
            SettlementFunctionalAmount = 100m,
            AllocationDate = payment.PaymentDate,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed"
        };

        invoice.PaidAmount = 100m;
        invoice.Status = InvoiceStatus.Paid;
        db.Set<CustomerPayment>().Add(payment);
        db.Set<PaymentAllocation>().Add(allocation);
        await db.SaveChangesAsync();

        return new ArReceiptTaxFixture(payment, withholdingReceivable, vatWithholdingReceivable);
    }

    private static TaxGroupIds SeedGhanaTaxConfiguration(ApplicationDbContext db, Guid tenantId, Guid receivableAccountId, Guid payableAccountId)
    {
        var effectiveFrom = new DateTime(2026, 1, 1);
        var vat = SeedTax(db, tenantId, "VAT", "Value Added Tax", 15m, TaxCategory.Standard, receivableAccountId, payableAccountId, effectiveFrom);
        var nhil = SeedTax(db, tenantId, "NHIL", "National Health Insurance Levy", 2.5m, TaxCategory.Levy, receivableAccountId, payableAccountId, effectiveFrom);
        var getfund = SeedTax(db, tenantId, "GETFL", "GETFund Levy", 2.5m, TaxCategory.Levy, receivableAccountId, payableAccountId, effectiveFrom);
        SeedTax(db, tenantId, "COVID", "COVID-19 Health Recovery Levy", 1m, TaxCategory.Levy, receivableAccountId, payableAccountId, effectiveFrom, isActive: false);

        var salesGroup = SeedTaxGroup(db, tenantId, "GH-SALES-STD", TaxApplicability.Sales);
        var purchaseGroup = SeedTaxGroup(db, tenantId, "GH-PURCH-STD", TaxApplicability.Purchases);
        AddComponent(db, tenantId, salesGroup.Id, nhil.Id, 1);
        AddComponent(db, tenantId, salesGroup.Id, getfund.Id, 2);
        AddComponent(db, tenantId, salesGroup.Id, vat.Id, 3);
        AddComponent(db, tenantId, purchaseGroup.Id, nhil.Id, 1);
        AddComponent(db, tenantId, purchaseGroup.Id, getfund.Id, 2);
        AddComponent(db, tenantId, purchaseGroup.Id, vat.Id, 3);

        return new TaxGroupIds(salesGroup.Id, purchaseGroup.Id);
    }

    private static Tax SeedTax(
        ApplicationDbContext db,
        Guid tenantId,
        string code,
        string name,
        decimal rate,
        TaxCategory category,
        Guid receivableAccountId,
        Guid payableAccountId,
        DateTime effectiveFrom,
        bool isActive = true)
    {
        var tax = new Tax
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Code = code,
            Name = name,
            Rate = rate,
            EffectiveFrom = effectiveFrom,
            Applicability = TaxApplicability.Both,
            Category = category,
            IsActive = isActive,
            IsInputTaxDeductible = true,
            TaxReceivableAccountId = receivableAccountId,
            TaxPayableAccountId = payableAccountId,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed"
        };
        db.Taxes.Add(tax);
        return tax;
    }

    private static TaxGroup SeedTaxGroup(ApplicationDbContext db, Guid tenantId, string code, TaxApplicability applicability)
    {
        var group = new TaxGroup
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Code = code,
            Name = code,
            Applicability = applicability,
            IsDefault = true,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed"
        };
        db.TaxGroups.Add(group);
        return group;
    }

    private static TaxGroup SeedEmptyTaxGroup(ApplicationDbContext db, Guid tenantId, TaxApplicability applicability)
        => SeedTaxGroup(db, tenantId, $"GROUP-{tenantId:N}"[..20], applicability);

    private static void AddComponent(ApplicationDbContext db, Guid tenantId, Guid groupId, Guid taxId, int order)
    {
        db.TaxGroupComponents.Add(new TaxGroupComponent
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            TaxGroupId = groupId,
            TaxId = taxId,
            CalculationOrder = order,
            CompoundBasis = CompoundBasis.BaseOnly,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed"
        });
    }

    private static void SeedTenant(ApplicationDbContext db, Guid tenantId, string code = "TEN")
    {
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = $"Tenant {code}",
            Code = code,
            Status = TenantStatus.Active,
            BaseCurrency = "GHS"
        });
        db.AccountingBooks.Add(new AccountingBook
        {
            Id = Guid.NewGuid(), TenantId = tenantId, Code = "IFRS", Name = "IFRS Primary",
            Purpose = "Primary", BookType = AccountingBookType.PrimaryFull,
            LifecycleStatus = AccountingBookLifecycleStatus.Active, FunctionalCurrencyCode = "GHS",
            IsDefault = true, IsActive = true, AllowsPosting = true
        });
    }

    private static FiscalPeriod SeedOpenPeriod(ApplicationDbContext db, Guid tenantId)
    {
        var period = new FiscalPeriod
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            FiscalYearId = Guid.NewGuid(),
            PeriodName = "July 2026",
            PeriodCode = "2026-07",
            PeriodNumber = 7,
            PeriodType = PeriodType.Monthly,
            StartDate = new DateTime(2026, 7, 1),
            EndDate = new DateTime(2026, 7, 31),
            PeriodDays = 31,
            PeriodStatus = "Open",
            IsOpen = true,
            IsClosed = false,
            IsLocked = false
        };
        db.FiscalPeriods.Add(period);
        FinancePostingAuthorityFixture.SeedExactBookPeriod(db, tenantId, period);
        return period;
    }

    private static Account SeedAccount(
        ApplicationDbContext db,
        Guid tenantId,
        string accountNumber,
        AccountType accountType,
        bool isControlAccount = false,
        bool allowDirectPosting = true)
    {
        var account = new Account
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            AccountCode = accountNumber,
            AccountNumber = accountNumber,
            AccountName = $"Account {accountNumber}",
            AccountType = accountType,
            Status = AccountStatus.Active,
            CurrencyCode = "GHS",
            IsControlAccount = isControlAccount,
            AllowDirectPosting = allowDirectPosting
        };
        db.Accounts.Add(account);
        var book = db.AccountingBooks.Local.Single(item => item.TenantId == tenantId && item.Code == "IFRS");
        FinancePostingAuthorityFixture.SeedEnabledBookMappings(db, tenantId, book, account);
        return account;
    }

    private static Supplier SeedSupplier(ApplicationDbContext db, Guid tenantId, Guid apAccountId, Guid expenseAccountId)
    {
        var supplier = new Supplier
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            SupplierCode = $"SUP-{tenantId:N}"[..10],
            Name = "Tax Supplier",
            SupplierType = "Vendor",
            IsActive = true,
            Status = "Active",
            DefaultApAccountId = apAccountId,
            DefaultExpenseAccountId = expenseAccountId,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed"
        };
        db.Suppliers.Add(supplier);
        var partner = new BusinessPartner
        {
            Id = supplier.Id,
            TenantId = tenantId,
            PartnerCode = supplier.SupplierCode,
            PartnerName = supplier.Name,
            LegalName = supplier.Name,
            PartnerType = "Supplier",
            RegistrationStatus = "Approved",
            ApprovalStatus = "Approved",
            IsActive = true,
            Currency = "GHS",
            DefaultApAccountId = apAccountId,
            DefaultExpenseAccountId = expenseAccountId,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed"
        };
        var role = new BusinessPartnerRole
        {
            Id = Guid.NewGuid(), TenantId = tenantId, BusinessPartnerId = partner.Id,
            RoleType = BusinessPartnerRoleType.Supplier, Status = BusinessPartnerRoleStatus.Active,
            ActiveFromUtc = new DateTime(2025, 1, 1), CreatedAt = DateTime.UtcNow, CreatedBy = "seed"
        };
        var profile = new BusinessPartnerApProfileVersion
        {
            Id = Guid.NewGuid(), TenantId = tenantId, BusinessPartnerRoleId = role.Id,
            VersionNumber = 1, Status = BusinessPartnerFinanceProfileStatus.Approved,
            EffectiveFrom = new DateTime(2025, 1, 1), SubjectToWithholding = false,
            ApprovedAtUtc = new DateTime(2025, 1, 1), ApprovedById = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow, CreatedBy = "seed"
        };
        db.BusinessPartners.Add(partner);
        db.BusinessPartnerRoles.Add(role);
        db.BusinessPartnerApProfileVersions.Add(profile);
        return supplier;
    }

    private static BusinessPartner SeedCustomer(ApplicationDbContext db, Guid tenantId, Guid arAccountId)
    {
        var customer = new BusinessPartner
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            PartnerCode = $"CUS-{tenantId:N}"[..10],
            PartnerName = "Tax Customer",
            PartnerType = "Customer",
            RegistrationStatus = "Approved",
            ApprovalStatus = "Approved",
            IsActive = true,
            IsBlacklisted = false,
            DefaultArAccountId = arAccountId,
            CreditLimit = 10000m,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed"
        };
        db.Set<BusinessPartner>().Add(customer);
        var role = new BusinessPartnerRole
        {
            Id = Guid.NewGuid(), TenantId = tenantId, BusinessPartnerId = customer.Id,
            RoleType = BusinessPartnerRoleType.Customer, Status = BusinessPartnerRoleStatus.Active,
            ActiveFromUtc = new DateTime(2025, 1, 1), CreatedAt = DateTime.UtcNow, CreatedBy = "seed"
        };
        var profile = new BusinessPartnerArProfileVersion
        {
            Id = Guid.NewGuid(), TenantId = tenantId, BusinessPartnerRoleId = role.Id,
            VersionNumber = 1, Status = BusinessPartnerFinanceProfileStatus.Approved,
            EffectiveFrom = new DateTime(2025, 1, 1),
            ApprovedAtUtc = new DateTime(2025, 1, 1), ApprovedById = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow, CreatedBy = "seed"
        };
        db.BusinessPartnerRoles.Add(role);
        db.BusinessPartnerArProfileVersions.Add(profile);
        return customer;
    }

    private static BankAccount SeedBankAccount(ApplicationDbContext db, Guid tenantId, Guid glAccountId)
    {
        var bankAccount = new BankAccount
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            AccountNumber = $"BANK-{tenantId:N}"[..16],
            AccountName = "Operating Bank",
            BankName = "Test Bank",
            Currency = "GHS",
            AccountType = BankAccountType.Checking,
            GLAccountId = glAccountId,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed"
        };
        db.BankAccounts.Add(bankAccount);
        return bankAccount;
    }

    private static VendorInvoice SeedPostedVendorInvoiceForPayment(
        ApplicationDbContext db,
        Guid tenantId,
        Supplier supplier,
        Account apAccount,
        string invoiceNumber,
        DateTime invoiceDate,
        decimal amount,
        Guid fiscalPeriodId)
    {
        var supplierRole = db.BusinessPartnerRoles.Local.Single(role =>
            role.BusinessPartnerId == supplier.Id && role.RoleType == BusinessPartnerRoleType.Supplier);
        var supplierProfile = db.BusinessPartnerApProfileVersions.Local.Single(profile =>
            profile.BusinessPartnerRoleId == supplierRole.Id);
        var invoice = new VendorInvoice
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            InvoiceNumber = invoiceNumber,
            SupplierInvoiceNumber = invoiceNumber,
            BusinessPartnerId = supplier.Id,
            BusinessPartnerRoleId = supplierRole.Id,
            BusinessPartnerApProfileVersionId = supplierProfile.Id,
            BusinessPartnerCode = supplier.SupplierCode,
            SupplierName = supplier.Name,
            InvoiceDate = invoiceDate,
            ReceivedDate = invoiceDate,
            DueDate = invoiceDate.AddDays(30),
            SubTotal = amount,
            TotalAmount = amount,
            CurrencyCode = "GHS",
            ExchangeRate = 1m,
            BaseCurrencyAmount = amount,
            Status = VendorInvoiceStatus.Approved,
            ApprovalStatus = "Approved",
            ApprovedById = Guid.NewGuid(),
            ApprovedDate = DateTime.UtcNow,
            ApAccountId = apAccount.Id,
            WithholdingContractReference = "CONTRACT-WHT-001",
            WithholdingSupplyCategory = WhtSupplyCategory.Services,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed"
        };

        invoice.JournalEntryId = SeedPostedSourceJournalAndEvent(
            db,
            tenantId,
            "AP",
            "VendorInvoice",
            invoice.Id,
            invoiceNumber,
            invoiceDate,
            amount,
            fiscalPeriodId);
        db.VendorInvoices.Add(invoice);
        return invoice;
    }

    private static Invoice SeedPostedCustomerInvoiceForReceipt(
        ApplicationDbContext db,
        Guid tenantId,
        BusinessPartner customer,
        Account arAccount,
        Account revenueAccount,
        string invoiceNumber,
        DateTime invoiceDate,
        decimal amount,
        Guid fiscalPeriodId)
    {
        var customerRole = db.BusinessPartnerRoles.Local.Single(role =>
            role.BusinessPartnerId == customer.Id && role.RoleType == BusinessPartnerRoleType.Customer);
        var customerProfile = db.BusinessPartnerArProfileVersions.Local.Single(profile =>
            profile.BusinessPartnerRoleId == customerRole.Id);
        var invoice = new Invoice
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            InvoiceNumber = invoiceNumber,
            BusinessPartnerId = customer.Id,
            BusinessPartnerRoleId = customerRole.Id,
            BusinessPartnerArProfileVersionId = customerProfile.Id,
            CustomerName = customer.PartnerName,
            CustomerAddress = customer.PhysicalAddress,
            InvoiceDate = invoiceDate,
            DueDate = invoiceDate.AddDays(30),
            SubTotal = amount,
            TotalAmount = amount,
            CurrencyCode = "GHS",
            ExchangeRate = 1m,
            BaseCurrencyAmount = amount,
            PaymentTermsDays = 30,
            Status = InvoiceStatus.Sent,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed"
        };

        invoice.LineItems.Add(new InvoiceLineItem
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            InvoiceId = invoice.Id,
            LineItemType = LineItemType.GLAccount,
            GLAccountId = revenueAccount.Id,
            Description = "Posted sale",
            Quantity = 1m,
            UnitPrice = amount,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed"
        });

        invoice.JournalEntryId = SeedPostedSourceJournalAndEvent(
            db,
            tenantId,
            "AR",
            "CustomerInvoice",
            invoice.Id,
            invoiceNumber,
            invoiceDate,
            amount,
            fiscalPeriodId);
        db.Invoices.Add(invoice);
        return invoice;
    }

    private static Guid SeedPostedSourceJournalAndEvent(
        ApplicationDbContext db,
        Guid tenantId,
        string sourceModule,
        string sourceDocumentType,
        Guid sourceDocumentId,
        string reference,
        DateTime postingDate,
        decimal amount,
        Guid fiscalPeriodId)
    {
        var book = db.AccountingBooks.Local.Single(item => item.TenantId == tenantId && item.Code == "IFRS");
        var journalId = Guid.NewGuid();
        db.JournalEntries.Add(new JournalEntry
        {
            Id = journalId,
            TenantId = tenantId,
            JournalEntryNumber = $"JE-{reference}",
            JournalType = sourceModule == "AP" ? "AP Invoice" : "AR Invoice",
            EntryDate = postingDate,
            Description = $"Posted source {reference}",
            ReferenceNumber = reference,
            SourceModule = sourceModule,
            SourceDocumentType = sourceDocumentType,
            SourceDocumentId = sourceDocumentId,
            TotalDebitAmount = amount,
            TotalCreditAmount = amount,
            IsBalanced = true,
            FiscalPeriodId = fiscalPeriodId,
            AccountingBookId = book.Id,
            BookClassification = book.Code,
            PostingStatus = "Posted",
            ApprovalStatus = "Approved",
            PostingDate = postingDate,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed"
        });

        db.FinancePostingEvents.Add(new FinancePostingEvent
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            SourceModule = sourceModule,
            SourceDocumentType = sourceDocumentType,
            SourceDocumentId = sourceDocumentId,
            PostingAction = "Post",
            SourceDocumentReference = reference,
            JournalEntryId = journalId,
            AccountingBookId = book.Id,
            PostingStatus = "Posted",
            PostingDate = postingDate,
            PostedAt = DateTime.UtcNow,
            TotalDebitAmount = amount,
            TotalCreditAmount = amount,
            FunctionalCurrencyCode = "GHS",
            BookClassification = "IFRS",
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed"
        });

        return journalId;
    }

    private sealed record TaxGroupIds(Guid SalesGroupId, Guid PurchaseGroupId);
    private sealed record ApInvoiceFixture(VendorInvoice Invoice, Account TaxReceivableAccount, Account TaxPayableAccount);
    private sealed record ArInvoiceFixture(Invoice Invoice, Account TaxReceivableAccount, Account TaxPayableAccount);
    private sealed record ApPaymentTaxFixture(VendorPayment Payment, Account WithholdingPayableAccount);
    private sealed record ArReceiptTaxFixture(CustomerPayment Payment, Account WithholdingReceivableAccount, Account VatWithholdingReceivableAccount);
}
