using ErpSystem.Api.Services.Finance;
using ErpSystem.Api.Services.Finance.FixedAssets;
using ErpSystem.Api.Services.Finance.GL;
using ErpSystem.Core.DTOs.AR;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.DTOs.Workflow;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Finance.FixedAssets;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Core.Interfaces.Numbering;
using ErpSystem.Data;
using ErpSystem.Shared;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Finance;

public sealed class FixedAssetDisposalFoundationTests
{
    [Fact]
    [Trait("Batch", "FinanceGoLive-FixedAssetDisposals")]
    [Trait("Category", "FixedAssets")]
    public async Task DisposalCannotRunBeforeCapitalization()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedDisposalFoundationAsync(db, tenantId, isCapitalized: false, status: FixedAssetStatus.Draft);
        var services = CreateServices(db, tenantId);

        var act = () => services.Disposals.RequestDisposalAsync(RequestWriteOff(fixture.Asset.Id), fixture.RequestedBy.Id);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*capitalized*");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FixedAssetDisposals")]
    [Trait("Category", "FixedAssets")]
    public async Task AlreadyDisposedAssetCannotBeDisposedAgain()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedDisposalFoundationAsync(db, tenantId);
        var services = CreateServices(db, tenantId);

        await RequestApproveAndCompleteAsync(services.Disposals, fixture, RequestWriteOff(fixture.Asset.Id));

        var act = () => services.Disposals.RequestDisposalAsync(RequestWriteOff(fixture.Asset.Id), fixture.RequestedBy.Id);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*again*");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FixedAssetDisposals")]
    [Trait("Category", "FixedAssets")]
    public async Task DisposedAssetCannotBeTransferredDepreciatedOrRevalued()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedDisposalFoundationAsync(db, tenantId);
        var services = CreateServices(db, tenantId);

        await RequestApproveAndCompleteAsync(services.Disposals, fixture, RequestWriteOff(fixture.Asset.Id));

        var transferAct = () => services.Transfers.RequestTransferAsync(new RequestAssetTransferDto
        {
            FixedAssetId = fixture.Asset.Id,
            TransferDate = new DateTime(2026, 7, 15),
            TransferType = AssetTransferType.Custodial,
            ToLocation = "Warehouse B",
            Reason = "Should fail"
        }, fixture.RequestedBy.Id);
        await transferAct.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*transferable*");

        var depreciationAct = () => services.Depreciation.RunDepreciationAsync(new RunDepreciationDto
        {
            FiscalPeriodId = fixture.OpenPeriod.Id,
            FixedAssetId = fixture.Asset.Id
        });
        await depreciationAct.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not eligible*");

        var valuationAct = () => services.Valuations.CreateValuationAsync(new CreateAssetValuationDto
        {
            FixedAssetId = fixture.Asset.Id,
            ValuationDate = new DateTime(2026, 7, 20),
            ValuationType = ValuationType.Impairment,
            FairValue = 800m,
            Reason = "Should fail"
        }, fixture.RequestedBy.Id);
        await valuationAct.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*revalued or impaired*");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FixedAssetDisposals")]
    [Trait("Category", "FixedAssets")]
    public async Task DisposalBeforePlacedInServiceOrCapitalizationDateRejected()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedDisposalFoundationAsync(db, tenantId);
        var services = CreateServices(db, tenantId);

        var dto = RequestWriteOff(fixture.Asset.Id);
        dto.DisposalDate = new DateTime(2026, 6, 15);

        var act = () => services.Disposals.RequestDisposalAsync(dto, fixture.RequestedBy.Id);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*capitalization date*");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FixedAssetDisposals")]
    [Trait("Category", "FixedAssets")]
    public async Task DisposalIntoClosedPeriodRejectedThroughPostingEngine()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedDisposalFoundationAsync(db, tenantId, closeDisposalPeriod: true);
        var services = CreateServices(db, tenantId);
        var requested = await RequestAndApproveAsync(services.Disposals, fixture, RequestWriteOff(fixture.Asset.Id));

        var act = () => services.Disposals.CompleteDisposalAsync(requested.Id);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*period*");
        (await db.AuditLogs.CountAsync(a => a.Action == FinanceAuditEvents.FixedAssetDisposalBlockedClosedPeriod)).Should().Be(1);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FixedAssetDisposals")]
    [Trait("Category", "FixedAssets")]
    public async Task WriteOffNoProceedsPostsCorrectDebitCredit()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedDisposalFoundationAsync(db, tenantId, acquisitionCost: 1200m, accumulatedDepreciation: 200m, netBookValue: 1000m);
        var services = CreateServices(db, tenantId);

        var completed = await RequestApproveAndCompleteAsync(services.Disposals, fixture, RequestWriteOff(fixture.Asset.Id));

        var lines = await PostedLinesAsync(db);
        completed.FinalDepreciationAmount.Should().Be(32.88m);
        lines.Should().ContainEquivalentOf(new { AccountId = fixture.Accounts.DepreciationExpense.Id, DebitAmount = 32.88m, CreditAmount = 0m });
        lines.Should().ContainEquivalentOf(new { AccountId = fixture.Accounts.AccumulatedDepreciation.Id, DebitAmount = 0m, CreditAmount = 32.88m });
        lines.Should().ContainEquivalentOf(new { AccountId = fixture.Accounts.AccumulatedDepreciation.Id, DebitAmount = 232.88m, CreditAmount = 0m });
        lines.Should().ContainEquivalentOf(new { AccountId = fixture.Accounts.LossOnDisposal.Id, DebitAmount = 967.12m, CreditAmount = 0m });
        lines.Should().ContainEquivalentOf(new { AccountId = fixture.Accounts.Asset.Id, DebitAmount = 0m, CreditAmount = 1200m });
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FixedAssetDisposals")]
    [Trait("Category", "FixedAssets")]
    public async Task SaleDisposalWithGainPostsCorrectDebitCredit()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedDisposalFoundationAsync(db, tenantId, acquisitionCost: 1200m, accumulatedDepreciation: 200m, netBookValue: 1000m);
        var services = CreateServices(db, tenantId);

        var completed = await RequestApproveAndCompleteAsync(services.Disposals, fixture, RequestSale(fixture, 1100m));

        var lines = await PostedLinesAsync(db);
        lines.Should().ContainEquivalentOf(new { AccountId = fixture.Accounts.AccumulatedDepreciation.Id, DebitAmount = 232.88m, CreditAmount = 0m });
        lines.Should().ContainEquivalentOf(new { AccountId = fixture.Accounts.ProceedsClearing.Id, DebitAmount = 1100m, CreditAmount = 0m });
        lines.Should().ContainEquivalentOf(new { AccountId = fixture.Accounts.Asset.Id, DebitAmount = 0m, CreditAmount = 1200m });
        lines.Should().ContainEquivalentOf(new { AccountId = fixture.Accounts.GainOnDisposal.Id, DebitAmount = 0m, CreditAmount = 132.88m });
        completed.GainOrLoss.Should().Be(132.88m);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FixedAssetDisposals")]
    [Trait("Category", "FixedAssets")]
    public async Task SaleDisposalWithLossPostsCorrectDebitCredit()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedDisposalFoundationAsync(db, tenantId, acquisitionCost: 1200m, accumulatedDepreciation: 200m, netBookValue: 1000m);
        var services = CreateServices(db, tenantId);

        var completed = await RequestApproveAndCompleteAsync(services.Disposals, fixture, RequestSale(fixture, 900m));

        var lines = await PostedLinesAsync(db);
        lines.Should().ContainEquivalentOf(new { AccountId = fixture.Accounts.AccumulatedDepreciation.Id, DebitAmount = 232.88m, CreditAmount = 0m });
        lines.Should().ContainEquivalentOf(new { AccountId = fixture.Accounts.ProceedsClearing.Id, DebitAmount = 900m, CreditAmount = 0m });
        lines.Should().ContainEquivalentOf(new { AccountId = fixture.Accounts.LossOnDisposal.Id, DebitAmount = 67.12m, CreditAmount = 0m });
        lines.Should().ContainEquivalentOf(new { AccountId = fixture.Accounts.Asset.Id, DebitAmount = 0m, CreditAmount = 1200m });
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FixedAssetDisposals")]
    [Trait("Category", "FixedAssets")]
    public async Task CreditSaleCreatesLinkedArInvoiceAgainstTheDisposalClearingAccount()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedDisposalFoundationAsync(db, tenantId);
        var services = CreateServices(db, tenantId);

        var completed = await RequestApproveAndCompleteAsync(services.Disposals, fixture, RequestSale(fixture, 1100m));

        completed.SettlementStatus.Should().Be(AssetDisposalSettlementStatus.Invoiced);
        completed.CustomerInvoiceId.Should().NotBeNull();
        completed.SettlementInvoiceAmount.Should().Be(1100m);
        services.InvoiceService.Verify(service => service.CreateAsync(
            It.Is<InvoiceCreateDto>(invoice =>
                invoice.BusinessPartnerId == fixture.Buyer.Id &&
                invoice.Reference!.StartsWith("FA-DISPOSAL:") &&
                invoice.LineItems.Count == 1 &&
                invoice.LineItems[0].LineItemType == nameof(LineItemType.FixedAssetDisposal) &&
                invoice.LineItems[0].GLAccountId == fixture.Accounts.ProceedsClearing.Id &&
                invoice.LineItems[0].UnitPrice == 1100m),
            It.IsAny<CancellationToken>()), Times.Once);
        services.PaymentService.Verify(service => service.CreateAsync(
            It.IsAny<PaymentCreateDto>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FixedAssetDisposals")]
    [Trait("Category", "FixedAssets")]
    public async Task ImmediateSaleCreatesAndPostsReceiptAllocatedToLinkedInvoice()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedDisposalFoundationAsync(db, tenantId);
        var method = new ErpSystem.Core.Entities.Finance.PaymentMethod
        {
            Id = Guid.NewGuid(), TenantId = tenantId, Name = "Bank Transfer", Code = "BANK",
            Type = PaymentMethodType.BankTransfer, IsActive = true, RequiresBankAccount = true,
            RequiresReference = true, CreatedAt = DateTime.UtcNow, CreatedBy = "seed"
        };
        var bank = new BankAccount
        {
            Id = Guid.NewGuid(), TenantId = tenantId, AccountNumber = "001234", AccountName = "Disposal Receipts",
            BankName = "Test Bank", Currency = "GHS", AccountType = BankAccountType.Checking,
            GLAccountId = fixture.Accounts.ProceedsClearing.Id, IsActive = true, CreatedAt = DateTime.UtcNow, CreatedBy = "seed"
        };
        db.PaymentMethods.Add(method);
        db.BankAccounts.Add(bank);
        await db.SaveChangesAsync();
        var services = CreateServices(db, tenantId);
        var request = RequestSale(fixture, 1100m);
        request.SettlementMode = AssetDisposalSettlementMode.ImmediateReceipt;
        request.SettlementPaymentMethodId = method.Id;
        request.SettlementBankAccountId = bank.Id;
        request.SettlementReference = "BANK-ADVICE-001";

        var completed = await RequestApproveAndCompleteAsync(services.Disposals, fixture, request);

        completed.SettlementStatus.Should().Be(AssetDisposalSettlementStatus.Settled);
        completed.CustomerInvoiceId.Should().NotBeNull();
        completed.CustomerPaymentId.Should().NotBeNull();
        services.PaymentService.Verify(service => service.CreateAsync(
            It.Is<PaymentCreateDto>(payment =>
                payment.BusinessPartnerId == fixture.Buyer.Id &&
                payment.TotalAmount == 1100m &&
                payment.BankAccountId == bank.Id &&
                payment.TransactionReference == "BANK-ADVICE-001" &&
                payment.Allocations != null &&
                payment.Allocations.Count == 1 &&
                payment.Allocations[0].InvoiceId == completed.CustomerInvoiceId &&
                payment.Allocations[0].AllocatedAmount == 1100m),
            It.IsAny<CancellationToken>()), Times.Once);
        services.PaymentService.Verify(service => service.PostAsync(
            completed.CustomerPaymentId!.Value, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FixedAssetDisposals")]
    [Trait("Category", "FixedAssets")]
    public async Task ImmediateSaleRejectsBankDestinationForCashMethodBeforeApproval()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedDisposalFoundationAsync(db, tenantId);
        var cashMethod = new ErpSystem.Core.Entities.Finance.PaymentMethod
        {
            Id = Guid.NewGuid(), TenantId = tenantId, Name = "Cash", Code = "CASH",
            Type = PaymentMethodType.Cash, IsActive = true, CreatedAt = DateTime.UtcNow, CreatedBy = "seed"
        };
        var bank = new BankAccount
        {
            Id = Guid.NewGuid(), TenantId = tenantId, AccountNumber = "001234", AccountName = "Disposal Receipts",
            BankName = "Test Bank", Currency = "GHS", AccountType = BankAccountType.Checking,
            GLAccountId = fixture.Accounts.ProceedsClearing.Id, IsActive = true, CreatedAt = DateTime.UtcNow, CreatedBy = "seed"
        };
        db.AddRange(cashMethod, bank);
        await db.SaveChangesAsync();
        var request = RequestSale(fixture, 1100m);
        request.SettlementMode = AssetDisposalSettlementMode.ImmediateReceipt;
        request.SettlementPaymentMethodId = cashMethod.Id;
        request.SettlementBankAccountId = bank.Id;

        var action = () => CreateServices(db, tenantId).Disposals.RequestDisposalAsync(request, fixture.RequestedBy.Id);

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*liquidity holding account*");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FixedAssetDisposals")]
    [Trait("Category", "FixedAssets")]
    public async Task ImmediateSaleRejectsReceivingBankInDifferentCurrencyBeforeApproval()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedDisposalFoundationAsync(db, tenantId);
        var method = new ErpSystem.Core.Entities.Finance.PaymentMethod
        {
            Id = Guid.NewGuid(), TenantId = tenantId, Name = "Bank Transfer", Code = "BANK",
            Type = PaymentMethodType.BankTransfer, IsActive = true, RequiresBankAccount = true,
            CreatedAt = DateTime.UtcNow, CreatedBy = "seed"
        };
        var usdBank = new BankAccount
        {
            Id = Guid.NewGuid(), TenantId = tenantId, AccountNumber = "USD-001", AccountName = "USD Receipts",
            BankName = "Test Bank", Currency = "USD", AccountType = BankAccountType.Checking,
            GLAccountId = fixture.Accounts.ProceedsClearing.Id, IsActive = true, CreatedAt = DateTime.UtcNow, CreatedBy = "seed"
        };
        db.AddRange(method, usdBank);
        await db.SaveChangesAsync();
        var request = RequestSale(fixture, 1100m);
        request.SettlementMode = AssetDisposalSettlementMode.ImmediateReceipt;
        request.SettlementPaymentMethodId = method.Id;
        request.SettlementBankAccountId = usdBank.Id;

        var action = () => CreateServices(db, tenantId).Disposals.RequestDisposalAsync(request, fixture.RequestedBy.Id);

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*currencies must match*");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FixedAssetDisposals")]
    [Trait("Category", "FixedAssets")]
    public async Task AccumulatedDepreciationIsClearedAndCostIsNotMutated()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedDisposalFoundationAsync(db, tenantId, acquisitionCost: 1200m, accumulatedDepreciation: 200m, netBookValue: 1000m);
        var services = CreateServices(db, tenantId);

        await RequestApproveAndCompleteAsync(services.Disposals, fixture, RequestWriteOff(fixture.Asset.Id));

        var asset = await db.FixedAssets.Include(a => a.BookValues).SingleAsync(a => a.Id == fixture.Asset.Id);
        asset.AcquisitionCost.Should().Be(1200m);
        asset.NetBookValue.Should().Be(0m);
        asset.Status.Should().Be(FixedAssetStatus.WrittenOff);
        asset.BookValues.Single().AcquisitionCost.Should().Be(1200m);
        asset.BookValues.Single().AccumulatedDepreciation.Should().Be(0m);
        asset.BookValues.Single().NetBookValue.Should().Be(0m);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FixedAssetDisposals")]
    [Trait("Category", "FixedAssets")]
    public async Task PartialDisposalAllocatesPostingLayersAndKeepsRemainderActive()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedDisposalFoundationAsync(
            db, tenantId, acquisitionCost: 1200m, accumulatedDepreciation: 200m, netBookValue: 1000m);
        var services = CreateServices(db, tenantId);
        var request = RequestWriteOff(fixture.Asset.Id);
        request.DisposalScope = AssetDisposalScope.PartialPortion;
        request.DisposedPortionPercent = 25m;
        request.AllocationEvidenceReference = "TDC-ENG-2026-041";
        request.AllocationEvidenceNotes = "Engineering quantity survey identifies one quarter of the installation.";

        var completed = await RequestApproveAndCompleteAsync(services.Disposals, fixture, request);

        completed.DisposalScope.Should().Be(AssetDisposalScope.PartialPortion);
        completed.DisposedPortionPercent.Should().Be(25m);
        completed.AcquisitionCostAllocated.Should().Be(300m);
        completed.AccumulatedDepreciationAtDisposal.Should().Be(58.22m);
        completed.NetBookValueAtDisposal.Should().Be(241.78m);
        completed.RemainingAcquisitionCostAfterDisposal.Should().Be(900m);
        completed.FinalDepreciationAmount.Should().Be(8.22m);
        completed.RemainingAccumulatedDepreciationAfterDisposal.Should().Be(150m);
        completed.RemainingNetBookValueAfterDisposal.Should().Be(750m);

        var asset = await db.FixedAssets.Include(a => a.BookValues).SingleAsync(a => a.Id == fixture.Asset.Id);
        asset.Status.Should().Be(FixedAssetStatus.Active);
        asset.DisposalDate.Should().BeNull();
        asset.AcquisitionCost.Should().Be(900m);
        asset.NetBookValue.Should().Be(750m);
        asset.BookValues.Single().AccumulatedDepreciation.Should().Be(150m);

        var lines = await PostedLinesAsync(db);
        lines.Should().ContainEquivalentOf(new { AccountId = fixture.Accounts.DepreciationExpense.Id, DebitAmount = 8.22m, CreditAmount = 0m });
        lines.Should().ContainEquivalentOf(new { AccountId = fixture.Accounts.AccumulatedDepreciation.Id, DebitAmount = 58.22m, CreditAmount = 0m });
        lines.Should().ContainEquivalentOf(new { AccountId = fixture.Accounts.LossOnDisposal.Id, DebitAmount = 241.78m, CreditAmount = 0m });
        lines.Should().ContainEquivalentOf(new { AccountId = fixture.Accounts.Asset.Id, DebitAmount = 0m, CreditAmount = 300m });
        (await db.AuditLogs.CountAsync(a => a.Action == FinanceAuditEvents.FixedAssetPartialDisposalAllocationCalculated)).Should().Be(1);
        (await db.AuditLogs.CountAsync(a => a.Action == FinanceAuditEvents.FixedAssetPartialDisposalAllocationPosted)).Should().Be(1);

        // The disposal-linked schedule covers only the portion that left service. The ordinary
        // period run must still calculate the retained asset's charge instead of treating the
        // disposal schedule as a duplicate full-asset schedule.
        var retainedSchedules = await services.Depreciation.RunDepreciationAsync(new RunDepreciationDto
        {
            FiscalPeriodId = fixture.OpenPeriod.Id,
            FixedAssetId = fixture.Asset.Id,
            PostToGl = false
        });
        retainedSchedules.Should().ContainSingle();
        retainedSchedules.Single().DepreciationAmount.Should().Be(76.44m);
        retainedSchedules.Single().ConventionEligibleFromDate.Should().Be(new DateTime(2026, 7, 1));
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FixedAssetDisposals")]
    [Trait("Category", "FixedAssets")]
    public async Task ComponentDisposalRequiresAndPersistsComponentAllocationEvidence()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedDisposalFoundationAsync(db, tenantId);
        var services = CreateServices(db, tenantId);
        var request = RequestWriteOff(fixture.Asset.Id);
        request.DisposalScope = AssetDisposalScope.Component;
        request.DisposedPortionPercent = 20m;

        var missingEvidence = () => services.Disposals.RequestDisposalAsync(request, fixture.RequestedBy.Id);
        await missingEvidence.Should().ThrowAsync<InvalidOperationException>().WithMessage("*allocation evidence*");

        request.ComponentReference = "LIFT-MOTOR-01";
        request.ComponentDescription = "East-wing lift traction motor";
        request.AllocationEvidenceReference = "VALUATION-TDC-2026-109";
        var requested = await services.Disposals.RequestDisposalAsync(request, fixture.RequestedBy.Id);

        requested.DisposalScope.Should().Be(AssetDisposalScope.Component);
        requested.ComponentReference.Should().Be("LIFT-MOTOR-01");
        requested.ComponentDescription.Should().Be("East-wing lift traction motor");
        requested.AllocationEvidenceReference.Should().Be("VALUATION-TDC-2026-109");
        requested.DisposedPortionPercent.Should().Be(20m);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(100)]
    [InlineData(120)]
    [Trait("Batch", "FinanceGoLive-FixedAssetDisposals")]
    [Trait("Category", "FixedAssets")]
    public async Task PartialDisposalRejectsInvalidPercentage(decimal percentage)
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedDisposalFoundationAsync(db, tenantId);
        var services = CreateServices(db, tenantId);
        var request = RequestWriteOff(fixture.Asset.Id);
        request.DisposalScope = AssetDisposalScope.PartialPortion;
        request.DisposedPortionPercent = percentage;
        request.AllocationEvidenceReference = "ALLOC-001";

        var act = () => services.Disposals.RequestDisposalAsync(request, fixture.RequestedBy.Id);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*percentage*");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FixedAssetDisposals")]
    [Trait("Category", "FixedAssets")]
    public async Task AccumulatedImpairmentIsClearedThroughDerecognitionLines()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedDisposalFoundationAsync(db, tenantId, acquisitionCost: 1200m, accumulatedDepreciation: 200m, netBookValue: 900m);
        SeedPostedImpairment(db, fixture, 100m);
        await db.SaveChangesAsync();
        var services = CreateServices(db, tenantId);

        var completed = await RequestApproveAndCompleteAsync(services.Disposals, fixture, RequestWriteOff(fixture.Asset.Id));

        completed.AccumulatedImpairmentAtDisposal.Should().Be(100m);
        var lines = await PostedLinesAsync(db);
        lines.Should().ContainEquivalentOf(new { AccountId = fixture.Accounts.AccumulatedImpairment.Id, DebitAmount = 100m, CreditAmount = 0m });
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FixedAssetDisposals")]
    [Trait("Category", "FixedAssets")]
    public async Task PriorDepreciationSchedulesAndValuationsAreNotRewritten()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedDisposalFoundationAsync(db, tenantId);
        var schedule = SeedPostedDepreciationSchedule(db, fixture, 100m);
        var valuation = SeedPostedRevaluation(db, fixture, surplus: 300m);
        fixture.Asset.BookValues.Single().NetBookValue = 1300m;
        fixture.Asset.NetBookValue = 1300m;
        await db.SaveChangesAsync();
        var services = CreateServices(db, tenantId);

        await RequestApproveAndCompleteAsync(services.Disposals, fixture, RequestWriteOff(fixture.Asset.Id));

        var unchangedSchedule = await db.AssetDepreciationSchedules.SingleAsync(s => s.Id == schedule.Id);
        unchangedSchedule.DepreciationAmount.Should().Be(100m);
        unchangedSchedule.NetBookValue.Should().Be(1100m);
        unchangedSchedule.IsPosted.Should().BeTrue();

        var unchangedValuation = await db.AssetValuations.SingleAsync(v => v.Id == valuation.Id);
        unchangedValuation.RevaluationSurplus.Should().Be(300m);
        unchangedValuation.Status.Should().Be("Posted");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FixedAssetDisposals")]
    [Trait("Category", "FixedAssets")]
    public async Task MissingDisposalGainLossOrProceedsMappingBlocksPosting()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedDisposalFoundationAsync(db, tenantId);
        fixture.Category.LossOnDisposalAccountId = null;
        await db.SaveChangesAsync();
        var services = CreateServices(db, tenantId);
        var requested = await RequestAndApproveAsync(services.Disposals, fixture, RequestWriteOff(fixture.Asset.Id));

        var act = () => services.Disposals.CompleteDisposalAsync(requested.Id);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*loss on disposal account*");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FixedAssetDisposals")]
    [Trait("Category", "FixedAssets")]
    public async Task CrossTenantDisposalAccountRejected()
    {
        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedDisposalFoundationAsync(db, tenantId);
        var other = await SeedDisposalFoundationAsync(db, otherTenantId, codePrefix: "OTH");
        fixture.Category.LossOnDisposalAccountId = other.Accounts.LossOnDisposal.Id;
        await db.SaveChangesAsync();
        var services = CreateServices(db, tenantId);
        var requested = await RequestAndApproveAsync(services.Disposals, fixture, RequestWriteOff(fixture.Asset.Id));

        var act = () => services.Disposals.CompleteDisposalAsync(requested.Id);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*was not found for this tenant*");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FixedAssetDisposals")]
    [Trait("Category", "FixedAssets")]
    public async Task DuplicateDisposalIsIdempotentOrSafelyRejected()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedDisposalFoundationAsync(db, tenantId);
        var services = CreateServices(db, tenantId);
        var dto = RequestWriteOff(fixture.Asset.Id);
        dto.IdempotencyKey = "dispose-once";

        var completed = await RequestApproveAndCompleteAsync(services.Disposals, fixture, dto);
        var again = await services.Disposals.CompleteDisposalAsync(completed.Id);

        again.Id.Should().Be(completed.Id);
        (await db.JournalEntries.CountAsync()).Should().Be(1);
        (await db.FinancePostingEvents.CountAsync(e => e.SourceDocumentType == "FixedAssetDisposal")).Should().Be(1);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FixedAssetDisposals")]
    [Trait("Category", "FixedAssets")]
    public async Task DisposalWithMissingRequiredDepreciationIsRejected()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedDisposalFoundationAsync(db, tenantId, lastDepreciationDate: null);
        fixture.Asset.BookValues.Single().LastDepreciationDate = null;
        await db.SaveChangesAsync();
        var services = CreateServices(db, tenantId);
        var act = () => services.Disposals.RequestDisposalAsync(RequestWriteOff(fixture.Asset.Id), fixture.RequestedBy.Id);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*depreciation must be posted*");
        (await db.AuditLogs.CountAsync(a => a.Action == FinanceAuditEvents.FixedAssetDisposalBlockedMissingDepreciation)).Should().Be(1);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FixedAssetDisposals")]
    [Trait("Category", "FixedAssets")]
    public async Task DisposalPostsActualDaysDepreciationAndCreatesReportableScheduleAtomically()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedDisposalFoundationAsync(
            db,
            tenantId,
            acquisitionCost: 1200m,
            accumulatedDepreciation: 200m,
            netBookValue: 1000m);
        var services = CreateServices(db, tenantId);

        var requested = await services.Disposals.RequestDisposalAsync(RequestWriteOff(fixture.Asset.Id), fixture.RequestedBy.Id);

        // Actual-days uses 10 eligible days over the tenant's 365-day fiscal year against the
        // annualised straight-line charge: GHS 100 × 12 × 10/365.
        requested.FinalDepreciationAmount.Should().Be(32.88m);
        requested.FinalDepreciationFromDate.Should().Be(new DateTime(2026, 7, 1));
        requested.FinalDepreciationToDate.Should().Be(new DateTime(2026, 7, 10));
        requested.FinalDepreciationEligibleDays.Should().Be(10);
        requested.FinalDepreciationPeriodDays.Should().Be(31);
        requested.FinalDepreciationProrationBasis.Should().Be("ActualDaysInclusive");
        requested.FinalDepreciationConventionSnapshot.Should().Be(DepreciationConvention.ActualDays);
        requested.FinalDepreciationConventionFactor.Should().Be(0.32876712m);

        var approved = await services.Disposals.ApproveDisposalAsync(
            requested.Id,
            fixture.Approver.Id,
            new ApproveAssetDisposalDto { Comments = "Approved final depreciation and disposal." });
        var completed = await services.Disposals.CompleteDisposalAsync(approved.Id);

        var schedule = await db.AssetDepreciationSchedules.SingleAsync(s => s.AssetDisposalId == completed.Id);
        schedule.Id.Should().Be(completed.FinalDepreciationScheduleId!.Value);
        schedule.DepreciationAmount.Should().Be(32.88m);
        schedule.DepreciationConventionSnapshot.Should().Be(DepreciationConvention.ActualDays);
        schedule.ConventionFactor.Should().Be(0.32876712m);
        schedule.IsPosted.Should().BeTrue();
        schedule.JournalEntryId.Should().Be(completed.JournalEntryId);
        schedule.PostingEventId.Should().Be(completed.PostingEventId);
        (await db.JournalEntries.CountAsync()).Should().Be(1);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FixedAssetDisposals")]
    [Trait("Category", "FixedAssets")]
    public async Task CurrentPeriodFullDepreciationMustBeReversedBeforeMidPeriodDisposal()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedDisposalFoundationAsync(db, tenantId);
        var currentSchedule = SeedPostedDepreciationSchedule(db, fixture, 100m);
        currentSchedule.FiscalPeriodId = fixture.OpenPeriod.Id;
        currentSchedule.PostingDate = new DateTime(2026, 7, 31);
        await db.SaveChangesAsync();
        var services = CreateServices(db, tenantId);

        var act = () => services.Disposals.RequestDisposalAsync(RequestWriteOff(fixture.Asset.Id), fixture.RequestedBy.Id);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*current-period depreciation schedule*Reverse*");
        (await db.AssetDisposals.CountAsync()).Should().Be(0);
        (await db.AuditLogs.CountAsync(a => a.Action == FinanceAuditEvents.FixedAssetDisposalBlockedMissingDepreciation)).Should().Be(1);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FixedAssetDisposals")]
    [Trait("Category", "FixedAssets")]
    public async Task UnitsOfProductionDisposalRequiresAndPersistsVerifiedUsageEvidence()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedDisposalFoundationAsync(
            db,
            tenantId,
            acquisitionCost: 1200m,
            accumulatedDepreciation: 200m,
            netBookValue: 1000m,
            depreciationMethod: DepreciationMethod.UnitsOfProduction,
            lifetimeProductionCapacity: 10000m);
        var services = CreateServices(db, tenantId);
        var missingEvidence = RequestWriteOff(fixture.Asset.Id);

        var missingAct = () => services.Disposals.RequestDisposalAsync(missingEvidence, fixture.RequestedBy.Id);
        await missingAct.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*production usage*");

        var dto = RequestWriteOff(fixture.Asset.Id);
        dto.FinalDepreciationProductionUnits = 500m;
        dto.FinalDepreciationEvidenceReference = "METER-2026-07-10";
        dto.FinalDepreciationEvidenceNotes = "Signed disposal meter reading.";
        var completed = await RequestApproveAndCompleteAsync(services.Disposals, fixture, dto);

        completed.FinalDepreciationAmount.Should().Be(50m);
        completed.FinalDepreciationProrationBasis.Should().Be("ProductionUsage");
        var schedule = await db.AssetDepreciationSchedules.SingleAsync(s => s.AssetDisposalId == completed.Id);
        schedule.PeriodProductionUnits.Should().Be(500m);
        schedule.ProductionEvidenceReference.Should().Be("METER-2026-07-10");
        schedule.ProductionEvidenceNotes.Should().Be("Signed disposal meter reading.");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FixedAssetDisposals")]
    [Trait("Category", "FixedAssets")]
    public async Task RevaluationSurplusTransfersDirectlyToRetainedEarningsWithoutAffectingProfitAndLoss()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedDisposalFoundationAsync(db, tenantId, acquisitionCost: 1200m, accumulatedDepreciation: 200m, netBookValue: 1300m);
        SeedPostedRevaluation(db, fixture, surplus: 300m);
        await db.SaveChangesAsync();
        var services = CreateServices(db, tenantId);

        var completed = await RequestApproveAndCompleteAsync(services.Disposals, fixture, RequestWriteOff(fixture.Asset.Id));

        var lines = await PostedLinesAsync(db);
        lines.Should().ContainEquivalentOf(new { AccountId = fixture.Accounts.RevaluationSurplus.Id, DebitAmount = 300m, CreditAmount = 0m });
        lines.Should().ContainEquivalentOf(new { AccountId = fixture.Accounts.RetainedEarnings.Id, DebitAmount = 0m, CreditAmount = 300m });
        lines.Should().ContainEquivalentOf(new { AccountId = fixture.Accounts.Asset.Id, DebitAmount = 0m, CreditAmount = 1500m });
        completed.FinalDepreciationAmount.Should().Be(42.74m);
        completed.GainOrLoss.Should().Be(-1257.26m);
        completed.RevaluationSurplusTransferAmount.Should().Be(300m);
        (await db.AuditLogs.CountAsync(a => a.Action == FinanceAuditEvents.FixedAssetDisposalRevaluationSurplusTransferred)).Should().Be(1);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FixedAssetDisposals")]
    [Trait("Category", "FixedAssets")]
    public async Task RevaluedAssetDisposalRequiresConfiguredRetainedEarningsAccount()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedDisposalFoundationAsync(db, tenantId, netBookValue: 1300m);
        SeedPostedRevaluation(db, fixture, surplus: 300m);
        (await db.FinanceSettings.SingleAsync()).RetainedEarningsAccountId = null;
        await db.SaveChangesAsync();
        var services = CreateServices(db, tenantId);

        var act = () => services.Disposals.RequestDisposalAsync(RequestWriteOff(fixture.Asset.Id), fixture.RequestedBy.Id);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*retained earnings disposal-transfer account is required*");
        (await db.AssetDisposals.CountAsync()).Should().Be(0);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FixedAssetDisposals")]
    [Trait("Category", "FixedAssets")]
    public async Task ChangedSurplusPolicyAccountCancelsStaleDisposalApproval()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedDisposalFoundationAsync(db, tenantId, netBookValue: 1300m);
        SeedPostedRevaluation(db, fixture, surplus: 300m);
        await db.SaveChangesAsync();
        var services = CreateServices(db, tenantId);
        var approved = await RequestAndApproveAsync(services.Disposals, fixture, RequestWriteOff(fixture.Asset.Id));

        // Finance configuration can change legitimately, but an old checker decision must never
        // be silently rebuilt with the replacement retained-earnings account.
        var replacementRetainedEarnings = SeedAccount(db, tenantId, "3199", AccountType.Equity);
        (await db.FinanceSettings.SingleAsync()).RetainedEarningsAccountId = replacementRetainedEarnings.Id;
        await db.SaveChangesAsync();

        var act = () => services.Disposals.CompleteDisposalAsync(approved.Id);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*policy accounts changed*");
        var cancelled = await db.AssetDisposals.SingleAsync(d => d.Id == approved.Id);
        cancelled.Status.Should().Be(AssetDisposalStatus.Cancelled);
        (await db.JournalEntries.CountAsync()).Should().Be(0);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FixedAssetDisposals")]
    [Trait("Category", "FixedAssets")]
    public async Task ChangedRevaluationSurplusBalanceCancelsStaleDisposalApproval()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedDisposalFoundationAsync(db, tenantId, netBookValue: 1300m);
        SeedPostedRevaluation(db, fixture, surplus: 300m);
        await db.SaveChangesAsync();
        var services = CreateServices(db, tenantId);
        var approved = await RequestAndApproveAsync(services.Disposals, fixture, RequestWriteOff(fixture.Asset.Id));

        // A posted valuation after checker approval changes the equity reserve that the disposal
        // would derecognise. The service must invalidate the stale approval instead of silently
        // transferring an amount that the checker never reviewed.
        SeedPostedRevaluation(db, fixture, surplus: 50m);
        await db.SaveChangesAsync();

        var act = () => services.Disposals.CompleteDisposalAsync(approved.Id);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*revaluation surplus changed*");
        var cancelled = await db.AssetDisposals.SingleAsync(d => d.Id == approved.Id);
        cancelled.Status.Should().Be(AssetDisposalStatus.Cancelled);
        (await db.JournalEntries.CountAsync()).Should().Be(0);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FixedAssetDisposals")]
    [Trait("Category", "FixedAssets")]
    public async Task ForeignCurrencyProceedsPreserveNativeEvidenceAndPostFunctionalValue()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedDisposalFoundationAsync(db, tenantId);
        SeedAccountCurrencyLink(db, fixture.Accounts.ProceedsClearing, "USD");
        var rate = SeedExchangeRate(db, tenantId, "USD", 15m);
        await db.SaveChangesAsync();
        var services = CreateServices(db, tenantId);
        var dto = RequestSale(fixture, 100m);
        dto.ProceedsCurrencyCode = "USD";
        dto.ProceedsExchangeRateId = rate.Id;

        var completed = await RequestApproveAndCompleteAsync(services.Disposals, fixture, dto);
        var proceedsLine = (await PostedLinesAsync(db)).Single(line => line.TransactionTag == "FA-DisposalProceeds");

        completed.NetProceeds.Should().Be(100m);
        completed.ProceedsCurrencyCode.Should().Be("USD");
        completed.ProceedsFunctionalAmount.Should().Be(1500m);
        completed.ProceedsExchangeRateId.Should().Be(rate.Id);
        completed.GainOrLoss.Should().Be(532.88m);
        proceedsLine.DebitAmount.Should().Be(1500m);
        proceedsLine.TransactionCurrency.Should().Be("USD");
        proceedsLine.TransactionDebitAmount.Should().Be(100m);
        proceedsLine.ExchangeRateId.Should().Be(rate.Id);
        proceedsLine.ExchangeRate.Should().Be(15m);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FixedAssetDisposals")]
    [Trait("Category", "FixedAssets")]
    public async Task ForeignCurrencyRateApprovalDriftCancelsDisposalBeforePosting()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedDisposalFoundationAsync(db, tenantId);
        SeedAccountCurrencyLink(db, fixture.Accounts.ProceedsClearing, "USD");
        var rate = SeedExchangeRate(db, tenantId, "USD", 15m);
        await db.SaveChangesAsync();
        var services = CreateServices(db, tenantId);
        var request = RequestSale(fixture, 100m);
        request.ProceedsCurrencyCode = "USD";
        request.ProceedsExchangeRateId = rate.Id;
        var approved = await RequestAndApproveAsync(services.Disposals, fixture, request);

        rate.ApprovalStatus = RateApprovalStatus.Rejected;
        await db.SaveChangesAsync();

        var act = () => services.Disposals.CompleteDisposalAsync(approved.Id);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*selected exchange rate*");
        (await db.AssetDisposals.SingleAsync(item => item.Id == approved.Id)).Status.Should().Be(AssetDisposalStatus.Cancelled);
        (await db.JournalEntries.CountAsync()).Should().Be(0);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FixedAssetDisposals")]
    [Trait("Category", "FixedAssets")]
    public async Task ForeignCurrencyProceedsRequireTenantApprovedEffectiveRate()
    {
        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedDisposalFoundationAsync(db, tenantId);
        SeedAccountCurrencyLink(db, fixture.Accounts.ProceedsClearing, "USD");
        SeedTenant(db, otherTenantId, "OTH");
        var otherTenantRate = SeedExchangeRate(db, otherTenantId, "USD", 15m);
        await db.SaveChangesAsync();
        var services = CreateServices(db, tenantId);
        var request = RequestSale(fixture, 100m);
        request.ProceedsCurrencyCode = "USD";
        request.ProceedsExchangeRateId = otherTenantRate.Id;

        var act = () => services.Disposals.RequestDisposalAsync(request, fixture.RequestedBy.Id);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*selected exchange rate*");
        (await db.AssetDisposals.CountAsync()).Should().Be(0);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FixedAssetDisposals")]
    [Trait("Category", "FixedAssets")]
    public async Task ForeignCurrencyProceedsRequireAuthorizedClearingAccountCurrency()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedDisposalFoundationAsync(db, tenantId);
        var rate = SeedExchangeRate(db, tenantId, "USD", 15m);
        await db.SaveChangesAsync();
        var services = CreateServices(db, tenantId);
        var request = RequestSale(fixture, 100m);
        request.ProceedsCurrencyCode = "USD";
        request.ProceedsExchangeRateId = rate.Id;

        var act = () => services.Disposals.RequestDisposalAsync(request, fixture.RequestedBy.Id);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*clearing account is not authorized for USD*");
        (await db.AssetDisposals.CountAsync()).Should().Be(0);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FixedAssetDisposals")]
    [Trait("Category", "FixedAssets")]
    public async Task DisposalAuditEventsAreEmitted()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedDisposalFoundationAsync(db, tenantId);
        var services = CreateServices(db, tenantId);

        await RequestApproveAndCompleteAsync(services.Disposals, fixture, RequestSale(fixture, 1100m));

        var actions = await db.AuditLogs
            .Where(a => a.TenantId == tenantId)
            .Select(a => a.Action)
            .ToListAsync();

        actions.Should().Contain(FinanceAuditEvents.FixedAssetDisposalRequested);
        actions.Should().Contain(FinanceAuditEvents.FixedAssetDisposalApproved);
        actions.Should().Contain(FinanceAuditEvents.FixedAssetDisposalCalculated);
        actions.Should().Contain(FinanceAuditEvents.FixedAssetDisposalConfigurationUsed);
        actions.Should().Contain(FinanceAuditEvents.FixedAssetDisposalPosted);
        actions.Should().Contain(FinanceAuditEvents.FixedAssetDisposalSaleProceedsRecorded);
    }

    private static RequestAssetDisposalDto RequestWriteOff(Guid assetId) => new()
    {
        FixedAssetId = assetId,
        DisposalDate = new DateTime(2026, 7, 10),
        DisposalType = DisposalType.Scrap,
        Reason = "Asset has no future economic benefit.",
        SaleProceeds = 0m,
        DisposalCost = 0m,
        ProceedsCurrencyCode = "GHS"
    };

    private static RequestAssetDisposalDto RequestSale(DisposalFixture fixture, decimal proceeds) => new()
    {
        FixedAssetId = fixture.Asset.Id,
        DisposalDate = new DateTime(2026, 7, 10),
        DisposalType = DisposalType.Sale,
        Reason = "Approved sale disposal.",
        BuyerName = fixture.Buyer.PartnerName,
        BuyerBusinessPartnerId = fixture.Buyer.Id,
        SettlementMode = AssetDisposalSettlementMode.CreditSale,
        // Existing disposal-basis tests are intentionally non-taxable. Dedicated settlement tests
        // below exercise the statutory treatment contract without changing their GL expectations.
        SaleTaxTreatment = TaxTreatment.OutOfScope,
        SaleProceeds = proceeds,
        DisposalCost = 0m,
        ProceedsCurrencyCode = "GHS"
    };

    private static async Task<AssetDisposalDto> RequestAndApproveAsync(
        AssetDisposalService service,
        DisposalFixture fixture,
        RequestAssetDisposalDto dto)
    {
        var requested = await service.RequestDisposalAsync(dto, fixture.RequestedBy.Id);
        return await service.ApproveDisposalAsync(requested.Id, fixture.Approver.Id, new ApproveAssetDisposalDto { Comments = "Approved" });
    }

    private static async Task<AssetDisposalDto> RequestApproveAndCompleteAsync(
        AssetDisposalService service,
        DisposalFixture fixture,
        RequestAssetDisposalDto dto)
    {
        var approved = await RequestAndApproveAsync(service, fixture, dto);
        return await service.CompleteDisposalAsync(approved.Id);
    }

    private static async Task<List<AccountTransaction>> PostedLinesAsync(ApplicationDbContext db)
    {
        var journal = await db.JournalEntries.Include(j => j.Transactions).SingleAsync();
        return journal.Transactions.OrderBy(t => t.LineNumber).ToList();
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"fixed-asset-disposal-foundation-{Guid.NewGuid()}")
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new ApplicationDbContext(options);
    }

    private static ServiceFixture CreateServices(ApplicationDbContext db, Guid tenantId)
    {
        var currentUser = CreateCurrentUser(tenantId);
        var auditService = new FinanceAuditService(
            db,
            currentUser.Object,
            new HttpContextAccessor
            {
                HttpContext = new DefaultHttpContext { TraceIdentifier = "trace-fa-disposal" }
            });
        var postingEngine = new FinancePostingEngine(
            db,
            currentUser.Object,
            Mock.Of<ILogger<FinancePostingEngine>>(),
            auditService);
        var numbering = new Mock<IDocumentNumberingService>();
        numbering.Setup(x => x.GenerateAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Guid?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<string?>(),
                It.IsAny<Guid?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => $"DSP-{Guid.NewGuid():N}"[..20]);

        var workflow = new Mock<IWorkflowService>();
        workflow.Setup(x => x.StartApprovalWorkflowAsync("AssetDisposal", It.IsAny<Guid>()))
            .ReturnsAsync(new WorkflowExecutionResult
            {
                Success = true,
                Status = WorkflowInstanceStatus.InProgress,
                WorkflowInstanceId = Guid.NewGuid()
            });
        workflow.Setup(x => x.CanUserApproveAsync("AssetDisposal", It.IsAny<Guid>(), It.IsAny<Guid>()))
            .ReturnsAsync(true);
        workflow.Setup(x => x.ProcessApprovalStepAsync("AssetDisposal", It.IsAny<Guid>(), It.IsAny<Guid>(), "Approve", It.IsAny<string?>()))
            .ReturnsAsync(new WorkflowExecutionResult
            {
                Success = true,
                Status = WorkflowInstanceStatus.Completed,
                WorkflowInstanceId = Guid.NewGuid()
            });
        workflow.Setup(x => x.ProcessApprovalStepAsync("AssetDisposal", It.IsAny<Guid>(), It.IsAny<Guid>(), "Reject", It.IsAny<string?>()))
            .ReturnsAsync(new WorkflowExecutionResult
            {
                Success = true,
                Status = WorkflowInstanceStatus.Cancelled,
                WorkflowInstanceId = Guid.NewGuid()
            });

        var invoiceService = new Mock<IInvoiceService>();
        var createdInvoices = new Dictionary<Guid, InvoiceDto>();
        invoiceService.Setup(x => x.CreateAsync(It.IsAny<InvoiceCreateDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((InvoiceCreateDto dto, CancellationToken _) =>
            {
                var subtotal = dto.LineItems.Sum(line => line.Quantity * line.UnitPrice);
                var invoice = new InvoiceDto
                {
                    Id = Guid.NewGuid(),
                    InvoiceNumber = $"INV-{Guid.NewGuid():N}"[..20],
                    BusinessPartnerId = dto.BusinessPartnerId,
                    InvoiceDate = dto.InvoiceDate,
                    SubTotal = subtotal,
                    TotalAmount = subtotal,
                    BalanceAmount = subtotal,
                    CurrencyCode = dto.CurrencyCode,
                    Status = "Draft"
                };
                createdInvoices[invoice.Id] = invoice;
                return invoice;
            });
        invoiceService.Setup(x => x.SendInvoiceAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, CancellationToken _) =>
            {
                var invoice = createdInvoices[id];
                invoice.Status = "Sent";
                return invoice;
            });

        var paymentService = new Mock<IPaymentService>();
        paymentService.Setup(x => x.CreateAsync(It.IsAny<PaymentCreateDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PaymentCreateDto dto, CancellationToken _) => new CustomerPaymentDto
            {
                Id = Guid.NewGuid(),
                PaymentNumber = $"RCP-{Guid.NewGuid():N}"[..20],
                BusinessPartnerId = dto.BusinessPartnerId,
                PaymentDate = dto.PaymentDate,
                TotalAmount = dto.TotalAmount,
                CurrencyCode = dto.CurrencyCode,
                Status = "Pending"
            });
        paymentService.Setup(x => x.PostAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, CancellationToken _) => new CustomerPaymentDto
            {
                Id = id,
                PaymentNumber = $"RCP-{id:N}"[..20],
                Status = "Posted"
            });

        var disposalService = new AssetDisposalService(
            db,
            currentUser.Object,
            numbering.Object,
            workflow.Object,
            postingEngine,
            auditService,
            invoiceService.Object,
            paymentService.Object);

        var transferService = new AssetTransferService(
            db,
            currentUser.Object,
            numbering.Object,
            workflow.Object,
            auditService);

        var depreciationService = new FixedAssetDepreciationService(
            db,
            currentUser.Object,
            Mock.Of<IJournalEntryService>(),
            accountingBookService: null,
            financePostingEngine: postingEngine,
            financeAuditService: auditService);

        var valuationService = new AssetValuationService(
            db,
            currentUser.Object,
            Mock.Of<IJournalEntryService>(),
            accountingBookService: null,
            financePostingEngine: postingEngine,
            financeAuditService: auditService);

        return new ServiceFixture(disposalService, transferService, depreciationService, valuationService, invoiceService, paymentService);
    }

    private static Mock<ICurrentUserService> CreateCurrentUser(Guid tenantId)
    {
        var userId = Guid.NewGuid();
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(x => x.TenantId).Returns(tenantId);
        currentUser.SetupGet(x => x.Claims).Returns(new Dictionary<string, string>());
        currentUser.SetupGet(x => x.UserId).Returns(userId.ToString());
        currentUser.SetupGet(x => x.UserName).Returns("fa.disposal");
        currentUser.SetupGet(x => x.IpAddress).Returns("127.0.0.1");
        currentUser.SetupGet(x => x.UserAgent).Returns("fixed-asset-disposal-tests");
        currentUser.SetupGet(x => x.IsAuthenticated).Returns(true);
        currentUser.SetupGet(x => x.Roles).Returns(Array.Empty<string>());
        currentUser.SetupGet(x => x.EmployeeId).Returns(Guid.NewGuid());
        return currentUser;
    }

    private static async Task<DisposalFixture> SeedDisposalFoundationAsync(
        ApplicationDbContext db,
        Guid tenantId,
        string codePrefix = "TEN",
        bool isCapitalized = true,
        FixedAssetStatus status = FixedAssetStatus.Active,
        decimal acquisitionCost = 1200m,
        decimal accumulatedDepreciation = 200m,
        decimal? netBookValue = null,
        DateTime? lastDepreciationDate = null,
        bool closeDisposalPeriod = false,
        DepreciationMethod depreciationMethod = DepreciationMethod.StraightLine,
        decimal lifetimeProductionCapacity = 0m)
    {
        SeedTenant(db, tenantId, codePrefix);
        var previousPeriod = SeedPeriod(db, tenantId, new DateTime(2026, 6, 1), new DateTime(2026, 6, 30), "2026-06", isOpen: true);
        var openPeriod = SeedPeriod(db, tenantId, new DateTime(2026, 7, 1), new DateTime(2026, 7, 31), "2026-07", isOpen: !closeDisposalPeriod);
        var book = SeedBook(db, tenantId);
        var accounts = SeedAccounts(db, tenantId, codePrefix);
        FinancePostingAuthorityFixture.SeedExactBookPeriod(db, tenantId, previousPeriod, book.Code);
        FinancePostingAuthorityFixture.SeedExactBookPeriod(db, tenantId, openPeriod, book.Code);
        FinancePostingAuthorityFixture.SeedEnabledBookMappings(
            db,
            tenantId,
            book,
            accounts.Asset,
            accounts.AccumulatedDepreciation,
            accounts.DepreciationExpense,
            accounts.GainOnDisposal,
            accounts.LossOnDisposal,
            accounts.ProceedsClearing,
            accounts.RevaluationSurplus,
            accounts.RevaluationLoss,
            accounts.ImpairmentLoss,
            accounts.AccumulatedImpairment,
            accounts.RetainedEarnings);

        db.FinanceSettings.Add(new FinanceSettings
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            BaseCurrency = "GHS",
            RetainedEarningsAccountId = accounts.RetainedEarnings.Id
        });

        var category = new FixedAssetCategory
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Code = $"DSP-{codePrefix}-{tenantId.ToString("N")[..4]}",
            Name = "Disposal Assets",
            AssetAccountId = accounts.Asset.Id,
            AccumulatedDepreciationAccountId = accounts.AccumulatedDepreciation.Id,
            DepreciationExpenseAccountId = accounts.DepreciationExpense.Id,
            GainOnDisposalAccountId = accounts.GainOnDisposal.Id,
            LossOnDisposalAccountId = accounts.LossOnDisposal.Id,
            DisposalProceedsClearingAccountId = accounts.ProceedsClearing.Id,
            RevaluationSurplusAccountId = accounts.RevaluationSurplus.Id,
            RevaluationLossAccountId = accounts.RevaluationLoss.Id,
            ImpairmentLossAccountId = accounts.ImpairmentLoss.Id,
            AccumulatedImpairmentAccountId = accounts.AccumulatedImpairment.Id,
            DefaultMethod = DepreciationMethod.StraightLine,
            DefaultUsefulLifeMonths = 12,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed"
        };

        var requestedBy = SeedEmployee(db, tenantId, $"{codePrefix}-REQ", "Request", "User");
        var approver = SeedEmployee(db, tenantId, $"{codePrefix}-APR", "Approve", "User");
        var buyer = new BusinessPartner
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            PartnerCode = $"BUY-{codePrefix}-{tenantId.ToString("N")[..4]}",
            PartnerName = "Buyer Ltd",
            PartnerType = "Customer",
            CustomerAccountNumber = $"CUS-{tenantId.ToString("N")[..6]}",
            Currency = "GHS",
            RegistrationStatus = "Approved",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed"
        };
        var capitalizedAt = isCapitalized ? new DateTime(2026, 6, 30) : (DateTime?)null;
        var journalEntryId = isCapitalized ? Guid.NewGuid() : (Guid?)null;
        var postingEventId = isCapitalized ? Guid.NewGuid() : (Guid?)null;
        var nbv = netBookValue ?? acquisitionCost - accumulatedDepreciation;
        var asset = new FixedAsset
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            AssetCode = $"FA-DSP-{codePrefix}-{tenantId.ToString("N")[..4]}",
            Name = "Disposal asset",
            Location = "Warehouse A",
            CurrentSegmentString = "OPS",
            FixedAssetCategoryId = category.Id,
            Category = category,
            PurchaseDate = new DateTime(2026, 6, 1),
            PlacedInServiceDate = status == FixedAssetStatus.Active ? new DateTime(2026, 6, 1) : null,
            PurchasePrice = acquisitionCost,
            InstallationCost = 0m,
            TaxAmount = 0m,
            AcquisitionCost = acquisitionCost,
            NetBookValue = nbv,
            UsefulLifeMonths = 12,
            ResidualValue = 0m,
            DepreciationMethod = depreciationMethod,
            DepreciationConvention = DepreciationConvention.ActualDays,
            LifetimeProductionCapacity = lifetimeProductionCapacity,
            Status = status,
            FunctionalCurrencyCode = "GHS",
            TransactionCurrencyCode = "GHS",
            CapitalizationDate = isCapitalized ? new DateTime(2026, 6, 30) : null,
            CapitalizedAt = capitalizedAt,
            JournalEntryId = journalEntryId,
            PostingEventId = postingEventId,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed"
        };

        var bookValue = new FixedAssetBookValue
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            FixedAssetId = asset.Id,
            FixedAsset = asset,
            AccountingBookId = book.Id,
            AccountingBook = book,
            BookClassification = "IFRS",
            AcquisitionCost = acquisitionCost,
            AccumulatedDepreciation = accumulatedDepreciation,
            NetBookValue = nbv,
            ResidualValue = 0m,
            UsefulLifeMonths = 12,
            RemainingUsefulLifeMonths = 10,
            DepreciationMethod = depreciationMethod,
            DepreciationConvention = DepreciationConvention.ActualDays,
            LifetimeProductionCapacity = lifetimeProductionCapacity,
            PlacedInServiceDate = asset.PlacedInServiceDate,
            CapitalizationDate = asset.CapitalizationDate,
            CapitalizationJournalEntryId = journalEntryId,
            CapitalizationPostingEventId = postingEventId,
            LastDepreciationDate = lastDepreciationDate ?? previousPeriod.EndDate,
            OpeningSource = "Capitalization",
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed"
        };

        asset.BookValues.Add(bookValue);
        db.BusinessPartners.Add(buyer);
        db.FixedAssetCategories.Add(category);
        db.FixedAssets.Add(asset);
        await db.SaveChangesAsync();

        return new DisposalFixture(openPeriod, previousPeriod, book, asset, category, accounts, requestedBy, approver, buyer);
    }

    private static void SeedPostedImpairment(ApplicationDbContext db, DisposalFixture fixture, decimal amount)
    {
        db.AssetValuations.Add(new AssetValuation
        {
            Id = Guid.NewGuid(),
            TenantId = fixture.Asset.TenantId,
            FixedAssetId = fixture.Asset.Id,
            AccountingBookId = fixture.Book.Id,
            BookClassification = "IFRS",
            FiscalPeriodId = fixture.PreviousPeriod.Id,
            ValuationDate = new DateTime(2026, 6, 30),
            AccountingDate = new DateTime(2026, 6, 30),
            ValuationType = ValuationType.Impairment,
            CarryingAmountBefore = fixture.Asset.NetBookValue + amount,
            AccumulatedDepreciationBefore = fixture.Asset.BookValues.Single().AccumulatedDepreciation,
            NetBookValueBefore = fixture.Asset.NetBookValue + amount,
            FairValue = fixture.Asset.NetBookValue,
            CarryingAmountAfter = fixture.Asset.NetBookValue,
            ImpairmentLoss = amount,
            AdjustmentAmount = amount,
            IsPostedToGL = true,
            Status = "Posted",
            Reason = "Impairment before disposal",
            PerformedByUserId = fixture.RequestedBy.Id,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed"
        });
    }

    private static AssetValuation SeedPostedRevaluation(ApplicationDbContext db, DisposalFixture fixture, decimal surplus)
    {
        var valuation = new AssetValuation
        {
            Id = Guid.NewGuid(),
            TenantId = fixture.Asset.TenantId,
            FixedAssetId = fixture.Asset.Id,
            AccountingBookId = fixture.Book.Id,
            BookClassification = "IFRS",
            FiscalPeriodId = fixture.PreviousPeriod.Id,
            ValuationDate = new DateTime(2026, 6, 30),
            AccountingDate = new DateTime(2026, 6, 30),
            ValuationType = ValuationType.Revaluation,
            CarryingAmountBefore = 1200m,
            AccumulatedDepreciationBefore = 200m,
            NetBookValueBefore = 1000m,
            FairValue = 1300m,
            CarryingAmountAfter = 1300m,
            RevaluationSurplus = surplus,
            AdjustmentAmount = surplus,
            IsPostedToGL = true,
            Status = "Posted",
            Reason = "Market valuation before disposal",
            PerformedByUserId = fixture.RequestedBy.Id,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed"
        };
        db.AssetValuations.Add(valuation);
        return valuation;
    }

    private static AssetDepreciationSchedule SeedPostedDepreciationSchedule(ApplicationDbContext db, DisposalFixture fixture, decimal amount)
    {
        var schedule = new AssetDepreciationSchedule
        {
            Id = Guid.NewGuid(),
            TenantId = fixture.Asset.TenantId,
            FixedAssetId = fixture.Asset.Id,
            AccountingBookId = fixture.Book.Id,
            BookClassification = "IFRS",
            FiscalPeriodId = fixture.PreviousPeriod.Id,
            DepreciationAmount = amount,
            AccumulatedDepreciationBefore = 0m,
            AccumulatedDepreciation = amount,
            NetBookValueBefore = 1200m,
            NetBookValue = 1100m,
            DepreciableAmount = 1200m,
            ResidualValueSnapshot = 0m,
            UsefulLifeMonthsSnapshot = 12,
            DepreciationMethodSnapshot = DepreciationMethod.StraightLine,
            PlacedInServiceDateSnapshot = fixture.Asset.PlacedInServiceDate,
            IsPosted = true,
            PostedDate = new DateTime(2026, 6, 30),
            PostingDate = new DateTime(2026, 6, 30),
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed"
        };
        db.AssetDepreciationSchedules.Add(schedule);
        return schedule;
    }

    private static void SeedTenant(ApplicationDbContext db, Guid tenantId, string code)
    {
        if (db.Tenants.Any(t => t.Id == tenantId))
        {
            return;
        }

        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = $"Tenant {code}",
            Code = code,
            Status = TenantStatus.Active,
            BaseCurrency = "GHS"
        });
    }

    private static ExchangeRate SeedExchangeRate(ApplicationDbContext db, Guid tenantId, string currency, decimal rate)
    {
        var exchangeRate = new ExchangeRate
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            BaseCurrencyCode = "GHS",
            TargetCurrencyCode = currency,
            Rate = rate,
            InverseRate = 1m / rate,
            EffectiveDate = new DateTime(2026, 7, 10),
            RateType = ExchangeRateType.Daily,
            QuoteSide = ExchangeRateQuoteSide.Mid,
            RateSource = "Bank of Ghana test fixture",
            ApprovalStatus = RateApprovalStatus.Approved,
            IsActive = true,
            CreatedByUserId = Guid.NewGuid(),
            CreatedDate = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed"
        };
        db.ExchangeRates.Add(exchangeRate);
        return exchangeRate;
    }

    private static void SeedAccountCurrencyLink(ApplicationDbContext db, Account account, string currency)
    {
        // Foreign proceeds are held in the existing disposal clearing account, so the fixture
        // models the same explicit account/currency authorization required in production.
        account.IsMultiCurrency = true;
        db.AccountCurrencyLinks.Add(new AccountCurrencyLink
        {
            Id = Guid.NewGuid(),
            TenantId = account.TenantId,
            AccountId = account.Id,
            LinkedCurrencyCode = currency,
            TransactionRateType = "Daily",
            TransactionQuoteSide = ExchangeRateQuoteSide.Mid,
            RevaluationRateType = "Month-End",
            RevaluationQuoteSide = ExchangeRateQuoteSide.Mid,
            IsActive = true,
            EffectiveDate = new DateTime(2026, 7, 1),
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed"
        });
    }

    private static FiscalPeriod SeedPeriod(ApplicationDbContext db, Guid tenantId, DateTime start, DateTime end, string code, bool isOpen)
    {
        var fiscalYear = db.FiscalYears.Local.FirstOrDefault(year => year.TenantId == tenantId)
            ?? new FiscalYear
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                FiscalYearName = "Fiscal Year 2026",
                FiscalYearCode = $"FY26-{tenantId.ToString("N")[..4]}",
                Year = 2026,
                StartDate = new DateTime(2026, 1, 1),
                EndDate = new DateTime(2026, 12, 31),
                TotalDays = 365,
                NumberOfPeriods = 12,
                Status = "Open",
                IsActive = true
            };
        if (db.Entry(fiscalYear).State == EntityState.Detached)
            db.FiscalYears.Add(fiscalYear);
        var period = new FiscalPeriod
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            FiscalYearId = fiscalYear.Id,
            FiscalYear = fiscalYear,
            PeriodName = code,
            PeriodCode = $"{code}-{tenantId.ToString("N")[..4]}",
            PeriodNumber = start.Month,
            PeriodType = PeriodType.Monthly,
            StartDate = start,
            EndDate = end,
            PeriodDays = (end - start).Days + 1,
            PeriodStatus = isOpen ? "Open" : "Closed",
            IsOpen = isOpen,
            IsClosed = !isOpen,
            IsLocked = !isOpen
        };
        db.FiscalPeriods.Add(period);
        return period;
    }

    private static AccountingBook SeedBook(ApplicationDbContext db, Guid tenantId)
    {
        var book = new AccountingBook
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Code = "IFRS",
            Name = "IFRS",
            Purpose = "Primary",
            FunctionalCurrencyCode = "GHS",
            IsActive = true,
            IsDefault = true,
            AllowsPosting = true,
            IsSystemDefined = true,
            SortOrder = 10,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed"
        };
        db.AccountingBooks.Add(book);
        return book;
    }

    private static Employee SeedEmployee(ApplicationDbContext db, Guid tenantId, string number, string firstName, string lastName)
    {
        var employee = new Employee
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            EmployeeNumber = number,
            FirstName = firstName,
            LastName = lastName,
            EmailAddress = $"{number.ToLowerInvariant()}@example.test",
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed"
        };
        db.Employees.Add(employee);
        return employee;
    }

    private static DisposalAccounts SeedAccounts(ApplicationDbContext db, Guid tenantId, string codePrefix)
    {
        return new DisposalAccounts(
            SeedAccount(db, tenantId, $"16{codePrefix[..Math.Min(2, codePrefix.Length)]}0", AccountType.Asset),
            SeedAccount(db, tenantId, $"16{codePrefix[..Math.Min(2, codePrefix.Length)]}9", AccountType.Asset),
            SeedAccount(db, tenantId, $"67{codePrefix[..Math.Min(2, codePrefix.Length)]}0", AccountType.Expense),
            SeedAccount(db, tenantId, $"78{codePrefix[..Math.Min(2, codePrefix.Length)]}1", AccountType.Expense),
            SeedAccount(db, tenantId, $"78{codePrefix[..Math.Min(2, codePrefix.Length)]}2", AccountType.Expense),
            SeedAccount(db, tenantId, $"11{codePrefix[..Math.Min(2, codePrefix.Length)]}5", AccountType.Asset),
            SeedAccount(db, tenantId, $"32{codePrefix[..Math.Min(2, codePrefix.Length)]}0", AccountType.Equity),
            SeedAccount(db, tenantId, $"78{codePrefix[..Math.Min(2, codePrefix.Length)]}3", AccountType.Expense),
            SeedAccount(db, tenantId, $"78{codePrefix[..Math.Min(2, codePrefix.Length)]}4", AccountType.Expense),
            SeedAccount(db, tenantId, $"16{codePrefix[..Math.Min(2, codePrefix.Length)]}8", AccountType.Asset),
            SeedAccount(db, tenantId, $"31{codePrefix[..Math.Min(2, codePrefix.Length)]}0", AccountType.Equity));
    }

    private static Account SeedAccount(ApplicationDbContext db, Guid tenantId, string accountNumber, AccountType accountType)
    {
        var account = new Account
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            AccountCode = $"{accountNumber}-{tenantId.ToString("N")[..4]}",
            AccountNumber = $"{accountNumber}-{tenantId.ToString("N")[..4]}",
            AccountName = $"Account {accountNumber}",
            AccountType = accountType,
            Status = AccountStatus.Active,
            CurrencyCode = "GHS",
            AllowDirectPosting = true
        };

        db.Accounts.Add(account);
        return account;
    }

    private sealed record ServiceFixture(
        AssetDisposalService Disposals,
        AssetTransferService Transfers,
        FixedAssetDepreciationService Depreciation,
        AssetValuationService Valuations,
        Mock<IInvoiceService> InvoiceService,
        Mock<IPaymentService> PaymentService);

    private sealed record DisposalFixture(
        FiscalPeriod OpenPeriod,
        FiscalPeriod PreviousPeriod,
        AccountingBook Book,
        FixedAsset Asset,
        FixedAssetCategory Category,
        DisposalAccounts Accounts,
        Employee RequestedBy,
        Employee Approver,
        BusinessPartner Buyer);

    private sealed record DisposalAccounts(
        Account Asset,
        Account AccumulatedDepreciation,
        Account DepreciationExpense,
        Account GainOnDisposal,
        Account LossOnDisposal,
        Account ProceedsClearing,
        Account RevaluationSurplus,
        Account RevaluationLoss,
        Account ImpairmentLoss,
        Account AccumulatedImpairment,
        Account RetainedEarnings);
}
