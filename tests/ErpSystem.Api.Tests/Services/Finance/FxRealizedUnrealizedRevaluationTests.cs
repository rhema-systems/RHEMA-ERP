using ErpSystem.Api.Services;
using ErpSystem.Api.Services.Finance;
using ErpSystem.Api.Services.Finance.GL;
using ErpSystem.Api.Services.Finance.MultiCurrency;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Finance.Integration;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Data;
using ErpSystem.Data.Seeders;
using ErpSystem.Shared;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Finance;

public sealed class FxRealizedUnrealizedRevaluationTests
{
    [Fact]
    [Trait("Batch", "FinanceGoLive-FXSettlementRevaluation")]
    [Trait("Category", "FX")]
    public async Task ApSettlementRateIncreasePostsRealizedLoss()
    {
        await using var fixture = await FxFixture.CreateAsync();
        var scenario = await fixture.PostApSettlementAsync(invoiceRate: 10m, paymentRate: 12m, settledForeignAmount: 100m);

        var results = await fixture.Service.PostRealizedFxForApPaymentAsync(scenario.ApPayment!.Id);

        results.Should().ContainSingle();
        results[0].GainLossType.Should().Be("Loss");
        results[0].GainLossAmount.Should().Be(200m);
        var lines = await fixture.Db.AccountTransactions
            .Where(t => t.JournalEntryId == results[0].JournalEntryId)
            .ToListAsync();
        lines.Should().Contain(t => t.AccountId == fixture.RealizedLoss.Id && t.DebitAmount == 200m);
        lines.Should().Contain(t => t.AccountId == fixture.ApControl.Id && t.CreditAmount == 200m);
        fixture.Audit.Events.Should().Contain(e => e.EventType == FinanceAuditEvents.RealizedFxCalculated);
        fixture.Audit.Events.Should().Contain(e => e.EventType == FinanceAuditEvents.RealizedFxPosted);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FXSettlementRevaluation")]
    [Trait("Category", "FX")]
    public async Task ApSettlementRateDecreasePostsRealizedGain()
    {
        await using var fixture = await FxFixture.CreateAsync();
        var scenario = await fixture.PostApSettlementAsync(invoiceRate: 10m, paymentRate: 8m, settledForeignAmount: 100m);

        var results = await fixture.Service.PostRealizedFxForApPaymentAsync(scenario.ApPayment!.Id);

        results.Should().ContainSingle();
        results[0].GainLossType.Should().Be("Gain");
        results[0].GainLossAmount.Should().Be(200m);
        var lines = await fixture.Db.AccountTransactions
            .Where(t => t.JournalEntryId == results[0].JournalEntryId)
            .ToListAsync();
        lines.Should().Contain(t => t.AccountId == fixture.ApControl.Id && t.DebitAmount == 200m);
        lines.Should().Contain(t => t.AccountId == fixture.RealizedGain.Id && t.CreditAmount == 200m);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FXSettlementRevaluation")]
    [Trait("Category", "FX")]
    public async Task ArSettlementRateIncreasePostsRealizedGain()
    {
        await using var fixture = await FxFixture.CreateAsync();
        var scenario = await fixture.PostArSettlementAsync(invoiceRate: 10m, receiptRate: 12m, settledForeignAmount: 100m);

        var results = await fixture.Service.PostRealizedFxForArReceiptAsync(scenario.ArReceipt!.Id);

        results.Should().ContainSingle();
        results[0].GainLossType.Should().Be("Gain");
        results[0].GainLossAmount.Should().Be(200m);
        var lines = await fixture.Db.AccountTransactions
            .Where(t => t.JournalEntryId == results[0].JournalEntryId)
            .ToListAsync();
        lines.Should().Contain(t => t.AccountId == fixture.ArControl.Id && t.DebitAmount == 200m);
        lines.Should().Contain(t => t.AccountId == fixture.RealizedGain.Id && t.CreditAmount == 200m);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FXSettlementRevaluation")]
    [Trait("Category", "FX")]
    public async Task ArSettlementRateDecreasePostsRealizedLoss()
    {
        await using var fixture = await FxFixture.CreateAsync();
        var scenario = await fixture.PostArSettlementAsync(invoiceRate: 10m, receiptRate: 8m, settledForeignAmount: 100m);

        var results = await fixture.Service.PostRealizedFxForArReceiptAsync(scenario.ArReceipt!.Id);

        results.Should().ContainSingle();
        results[0].GainLossType.Should().Be("Loss");
        results[0].GainLossAmount.Should().Be(200m);
        var lines = await fixture.Db.AccountTransactions
            .Where(t => t.JournalEntryId == results[0].JournalEntryId)
            .ToListAsync();
        lines.Should().Contain(t => t.AccountId == fixture.RealizedLoss.Id && t.DebitAmount == 200m);
        lines.Should().Contain(t => t.AccountId == fixture.ArControl.Id && t.CreditAmount == 200m);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FXSettlementRevaluation")]
    [Trait("Category", "FX")]
    public async Task PartialApSettlementCalculatesProportionalFx()
    {
        await using var fixture = await FxFixture.CreateAsync();
        var scenario = await fixture.PostApSettlementAsync(invoiceRate: 10m, paymentRate: 12m, settledForeignAmount: 40m, invoiceForeignAmount: 100m);

        var results = await fixture.Service.PostRealizedFxForApPaymentAsync(scenario.ApPayment!.Id);

        results.Should().ContainSingle();
        results[0].SettledForeignAmount.Should().Be(40m);
        results[0].GainLossAmount.Should().Be(80m);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FXSettlementRevaluation")]
    [Trait("Category", "FX")]
    public async Task PartialArReceiptCalculatesProportionalFx()
    {
        await using var fixture = await FxFixture.CreateAsync();
        var scenario = await fixture.PostArSettlementAsync(invoiceRate: 10m, receiptRate: 12m, settledForeignAmount: 40m, invoiceForeignAmount: 100m);

        var results = await fixture.Service.PostRealizedFxForArReceiptAsync(scenario.ArReceipt!.Id);

        results.Should().ContainSingle();
        results[0].SettledForeignAmount.Should().Be(40m);
        results[0].GainLossAmount.Should().Be(80m);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FXSettlementRevaluation")]
    [Trait("Category", "FX")]
    public async Task ApPaymentAllocatedToMultipleInvoicesCalculatesFxPerAllocation()
    {
        await using var fixture = await FxFixture.CreateAsync();
        var firstInvoice = await fixture.PostOpenApInvoiceAsync(rate: 10m, foreignAmount: 100m);
        var secondInvoice = await fixture.PostOpenApInvoiceAsync(rate: 11m, foreignAmount: 100m);
        var payment = await fixture.PostApPaymentAsync(12m, (firstInvoice, 100m), (secondInvoice, 100m));

        var results = await fixture.Service.PostRealizedFxForApPaymentAsync(payment.Id);

        results.Should().HaveCount(2);
        results.Should().Contain(r => r.InvoiceDocumentId == firstInvoice.Id && r.GainLossType == "Loss" && r.GainLossAmount == 200m);
        results.Should().Contain(r => r.InvoiceDocumentId == secondInvoice.Id && r.GainLossType == "Loss" && r.GainLossAmount == 100m);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FXSettlementRevaluation")]
    [Trait("Category", "FX")]
    public async Task ArReceiptAllocatedToMultipleInvoicesCalculatesFxPerAllocation()
    {
        await using var fixture = await FxFixture.CreateAsync();
        var firstInvoice = await fixture.PostOpenArInvoiceAsync(rate: 10m, foreignAmount: 100m);
        var secondInvoice = await fixture.PostOpenArInvoiceAsync(rate: 11m, foreignAmount: 100m);
        var receipt = await fixture.PostArReceiptAsync(12m, (firstInvoice, 100m), (secondInvoice, 100m));

        var results = await fixture.Service.PostRealizedFxForArReceiptAsync(receipt.Id);

        results.Should().HaveCount(2);
        results.Should().Contain(r => r.InvoiceDocumentId == firstInvoice.Id && r.GainLossType == "Gain" && r.GainLossAmount == 200m);
        results.Should().Contain(r => r.InvoiceDocumentId == secondInvoice.Id && r.GainLossType == "Gain" && r.GainLossAmount == 100m);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FXSettlementRevaluation")]
    [Trait("Category", "FX")]
    public async Task SameRateSettlementProducesNoRealizedFxJournal()
    {
        await using var fixture = await FxFixture.CreateAsync();
        var scenario = await fixture.PostApSettlementAsync(invoiceRate: 10m, paymentRate: 10m, settledForeignAmount: 100m);

        var results = await fixture.Service.PostRealizedFxForApPaymentAsync(scenario.ApPayment!.Id);

        results.Should().BeEmpty();
        (await fixture.Db.FxRealizedSettlements.CountAsync()).Should().Be(0);
        (await fixture.Db.FinancePostingEvents.CountAsync(e => e.SourceModule == "FX" && e.PostingAction == "RealizedFx")).Should().Be(0);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FXSettlementRevaluation")]
    [Trait("Category", "FX")]
    public async Task DuplicateRealizedFxPostingReturnsExistingSettlement()
    {
        await using var fixture = await FxFixture.CreateAsync();
        var scenario = await fixture.PostApSettlementAsync(invoiceRate: 10m, paymentRate: 12m, settledForeignAmount: 100m);

        var first = await fixture.Service.PostRealizedFxForApPaymentAsync(scenario.ApPayment!.Id);
        var second = await fixture.Service.PostRealizedFxForApPaymentAsync(scenario.ApPayment.Id);

        second.Should().ContainSingle();
        second[0].Id.Should().Be(first[0].Id);
        (await fixture.Db.FxRealizedSettlements.CountAsync()).Should().Be(1);
        (await fixture.Db.FinancePostingEvents.CountAsync(e => e.SourceModule == "FX" && e.PostingAction == "RealizedFx")).Should().Be(1);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FXSettlementRevaluation")]
    [Trait("Category", "FX")]
    public async Task MissingRealizedFxMappingRejectsSettlementPosting()
    {
        await using var fixture = await FxFixture.CreateAsync();
        fixture.Settings.RealizedFxLossAccountId = null;
        await fixture.Db.SaveChangesAsync();
        var scenario = await fixture.PostApSettlementAsync(invoiceRate: 10m, paymentRate: 12m, settledForeignAmount: 100m);

        await fixture.Service.Invoking(s => s.PostRealizedFxForApPaymentAsync(scenario.ApPayment!.Id))
            .Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*realized FX loss account*");
        fixture.Audit.Events.Should().Contain(e => e.EventType == FinanceAuditEvents.RealizedFxPostingFailed);
        fixture.Audit.Events.Should().Contain(e => e.EventType == FinanceAuditEvents.FxPostingBlockedInvalidConfiguration);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FXSettlementRevaluation")]
    [Trait("Category", "FX")]
    public async Task MissingUnrealizedFxMappingRejectsRevaluationPosting()
    {
        await using var fixture = await FxFixture.CreateAsync();
        fixture.Settings.UnrealizedFxLossAccountId = null;
        await fixture.PostOpenApInvoiceAsync(rate: 10m, foreignAmount: 100m);
        fixture.SeedExchangeRate(12m, new DateTime(2026, 7, 31), ExchangeRateType.MonthEnd);
        await fixture.Db.SaveChangesAsync();

        await fixture.Service.Invoking(s => s.RunUnrealizedRevaluationAsync(new RevaluationRequestDto
            {
                RevaluationDate = new DateTime(2026, 7, 31),
                RevaluationType = "Month-End",
                AccountingBookCode = "IFRS"
            }))
            .Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*unrealized FX loss account*");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FXSettlementRevaluation")]
    [Trait("Category", "FX")]
    public async Task CrossTenantRealizedFxAccountMappingIsRejected()
    {
        await using var fixture = await FxFixture.CreateAsync();
        fixture.Settings.RealizedFxLossAccountId = fixture.SeedOtherTenantAccount(AccountType.Expense).Id;
        await fixture.Db.SaveChangesAsync();
        var scenario = await fixture.PostApSettlementAsync(invoiceRate: 10m, paymentRate: 12m, settledForeignAmount: 100m);

        await fixture.Service.Invoking(s => s.PostRealizedFxForApPaymentAsync(scenario.ApPayment!.Id))
            .Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*realized FX loss account*");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FXSettlementRevaluation")]
    [Trait("Category", "FX")]
    public async Task CrossTenantUnrealizedFxAccountMappingIsRejected()
    {
        await using var fixture = await FxFixture.CreateAsync();
        fixture.Settings.UnrealizedFxGainAccountId = fixture.SeedOtherTenantAccount(AccountType.Revenue).Id;
        await fixture.PostOpenArInvoiceAsync(rate: 10m, foreignAmount: 100m);
        fixture.SeedExchangeRate(12m, new DateTime(2026, 7, 31), ExchangeRateType.MonthEnd);
        await fixture.Db.SaveChangesAsync();

        await fixture.Service.Invoking(s => s.RunUnrealizedRevaluationAsync(new RevaluationRequestDto
            {
                RevaluationDate = new DateTime(2026, 7, 31),
                RevaluationType = "Month-End",
                AccountingBookCode = "IFRS"
            }))
            .Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*unrealized FX gain account*");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FXSettlementRevaluation")]
    [Trait("Category", "FX")]
    public async Task CrossTenantExchangeRateSnapshotIsRejectedForSettlement()
    {
        await using var fixture = await FxFixture.CreateAsync();
        var scenario = await fixture.PostApSettlementAsync(invoiceRate: 10m, paymentRate: 12m, settledForeignAmount: 100m);
        var foreignRate = fixture.SeedOtherTenantExchangeRate(10m, new DateTime(2026, 7, 1), ExchangeRateType.Daily);
        await fixture.Db.SaveChangesAsync();
        var invoiceControlLine = await fixture.Db.AccountTransactions
            .FirstAsync(t => t.TenantId == fixture.TenantId
                && t.SourceDocumentType == "VendorInvoice"
                && t.SourceDocumentId == scenario.ApInvoice!.Id
                && t.AccountId == fixture.ApControl.Id);
        invoiceControlLine.ExchangeRateId = foreignRate.Id;
        await fixture.Db.SaveChangesAsync();

        await fixture.Service.Invoking(s => s.PostRealizedFxForApPaymentAsync(scenario.ApPayment!.Id))
            .Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*exchange-rate snapshot does not belong to this tenant*");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FXSettlementRevaluation")]
    [Trait("Category", "FX")]
    public async Task CrossTenantClosingRateIsRejectedForRevaluation()
    {
        await using var fixture = await FxFixture.CreateAsync();
        await fixture.PostOpenArInvoiceAsync(rate: 10m, foreignAmount: 100m);
        fixture.SeedOtherTenantExchangeRate(12m, new DateTime(2026, 7, 31), ExchangeRateType.MonthEnd);
        await fixture.Db.SaveChangesAsync();

        await fixture.Service.Invoking(s => s.RunUnrealizedRevaluationAsync(new RevaluationRequestDto
            {
                RevaluationDate = new DateTime(2026, 7, 31),
                RevaluationType = "Month-End",
                AccountingBookCode = "IFRS"
            }))
            .Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*No approved*MonthEnd exchange rate*");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FXSettlementRevaluation")]
    [Trait("Category", "FX")]
    public async Task ClosedPeriodRealizedFxSettlementIsRejectedThroughPostingEngine()
    {
        await using var fixture = await FxFixture.CreateAsync();
        var scenario = await fixture.PostApSettlementAsync(invoiceRate: 10m, paymentRate: 12m, settledForeignAmount: 100m);
        fixture.CloseAllPeriods();
        await fixture.Db.SaveChangesAsync();

        await fixture.Service.Invoking(s => s.PostRealizedFxForApPaymentAsync(scenario.ApPayment!.Id))
            .Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*period is not open*");
        fixture.Audit.Events.Should().Contain(e => e.EventType == FinanceAuditEvents.RealizedFxPostingFailed);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FXSettlementRevaluation")]
    [Trait("Category", "FX")]
    public async Task FunctionalOrThirdCurrencySettlementUsesFrozenPaymentSideEvidence()
    {
        await using var fixture = await FxFixture.CreateAsync();
        var apScenario = await fixture.PostApSettlementAsync(invoiceRate: 10m, paymentRate: 12m, settledForeignAmount: 100m);
        apScenario.ApPayment!.CurrencyCode = "GHS";
        var apAllocation = apScenario.ApPayment.Allocations.Single();
        apAllocation.PaymentCurrencyCode = "GHS";
        apAllocation.PaymentCurrencyAmount = 1_200m;
        apAllocation.PaymentExchangeRate = 1m;
        apAllocation.SettlementFunctionalAmount = 1_200m;
        apAllocation.IsCrossCurrency = true;

        var arScenario = await fixture.PostArSettlementAsync(invoiceRate: 10m, receiptRate: 12m, settledForeignAmount: 100m);
        arScenario.ArReceipt!.CurrencyCode = "EUR";
        var arAllocation = arScenario.ArReceipt.Allocations.Single();
        arAllocation.PaymentCurrencyCode = "EUR";
        arAllocation.PaymentCurrencyAmount = 80m;
        arAllocation.PaymentExchangeRate = 15m;
        arAllocation.SettlementFunctionalAmount = 1_200m;
        arAllocation.IsCrossCurrency = true;
        await fixture.Db.SaveChangesAsync();

        var apSettlements = await fixture.Service.PostRealizedFxForApPaymentAsync(apScenario.ApPayment.Id);
        var arSettlements = await fixture.Service.PostRealizedFxForArReceiptAsync(arScenario.ArReceipt.Id);

        apSettlements.Should().ContainSingle();
        apSettlements[0].PaymentCurrencyCode.Should().Be("GHS");
        apSettlements[0].PaymentCurrencyAmount.Should().Be(1_200m);
        apSettlements[0].GainLossAmount.Should().Be(200m);
        arSettlements.Should().ContainSingle();
        arSettlements[0].PaymentCurrencyCode.Should().Be("EUR");
        arSettlements[0].PaymentCurrencyAmount.Should().Be(80m);
        arSettlements[0].GainLossAmount.Should().Be(200m);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FXSettlementRevaluation")]
    [Trait("Category", "FX")]
    public async Task ForeignApPaymentSettlingFunctionalInvoiceUsesRateOneHistoricalBasis()
    {
        await using var fixture = await FxFixture.CreateAsync();
        var scenario = await fixture.PostApSettlementAsync(
            invoiceRate: 1m,
            paymentRate: 11m,
            settledForeignAmount: 100m,
            invoiceForeignAmount: 1_200m);
        var invoice = scenario.ApInvoice!;
        var payment = scenario.ApPayment!;
        var allocation = payment.Allocations.Single();

        // Recast the helper's invoice/control lines as a genuine functional GHS exposure. A
        // functional line correctly has no ExchangeRate metadata; the regression is that the FX
        // service must use historical rate one rather than reject that missing foreign snapshot.
        invoice.CurrencyCode = "GHS";
        invoice.ExchangeRate = 1m;
        allocation.AllocatedAmount = 1_200m;
        allocation.InvoiceCurrencyCode = "GHS";
        allocation.PaymentCurrencyCode = "USD";
        allocation.PaymentCurrencyAmount = 100m;
        allocation.PaymentExchangeRate = 11m;
        allocation.PaymentFunctionalAmount = 1_100m;
        allocation.SettlementFunctionalAmount = 1_100m;
        allocation.InvoiceSettlementExchangeRate = 1m;
        allocation.IsCrossCurrency = true;
        await fixture.MakeControlLinesFunctionalAsync(invoice.JournalEntryId!.Value, payment.JournalEntryId!.Value, fixture.ApControl.Id);

        var result = (await fixture.Service.PostRealizedFxForApPaymentAsync(payment.Id)).Single();

        result.TransactionCurrency.Should().Be("GHS");
        result.HistoricalExchangeRate.Should().Be(1m);
        result.HistoricalExchangeRateId.Should().BeNull();
        result.HistoricalFunctionalAmount.Should().Be(1_200m);
        result.SettlementFunctionalAmount.Should().Be(1_100m);
        result.GainLossType.Should().Be("Gain");
        result.GainLossAmount.Should().Be(100m);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FXSettlementRevaluation")]
    [Trait("Category", "FX")]
    public async Task ForeignArReceiptSettlingFunctionalInvoiceUsesRateOneHistoricalBasis()
    {
        await using var fixture = await FxFixture.CreateAsync();
        var scenario = await fixture.PostArSettlementAsync(
            invoiceRate: 1m,
            receiptRate: 11m,
            settledForeignAmount: 100m,
            invoiceForeignAmount: 1_200m);
        var invoice = scenario.ArInvoice!;
        var receipt = scenario.ArReceipt!;
        var allocation = receipt.Allocations.Single();

        invoice.CurrencyCode = "GHS";
        invoice.ExchangeRate = 1m;
        allocation.AllocatedAmount = 1_200m;
        allocation.InvoiceCurrencyCode = "GHS";
        allocation.PaymentCurrencyCode = "USD";
        allocation.PaymentCurrencyAmount = 100m;
        allocation.PaymentExchangeRate = 11m;
        allocation.PaymentFunctionalAmount = 1_100m;
        allocation.SettlementFunctionalAmount = 1_100m;
        allocation.InvoiceSettlementExchangeRate = 1m;
        allocation.IsCrossCurrency = true;
        await fixture.MakeControlLinesFunctionalAsync(invoice.JournalEntryId!.Value, receipt.JournalEntryId!.Value, fixture.ArControl.Id);

        var result = (await fixture.Service.PostRealizedFxForArReceiptAsync(receipt.Id)).Single();

        result.TransactionCurrency.Should().Be("GHS");
        result.HistoricalExchangeRate.Should().Be(1m);
        result.HistoricalExchangeRateId.Should().BeNull();
        result.HistoricalFunctionalAmount.Should().Be(1_200m);
        result.SettlementFunctionalAmount.Should().Be(1_100m);
        result.GainLossType.Should().Be("Loss");
        result.GainLossAmount.Should().Be(100m);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FXSettlementRevaluation")]
    [Trait("Category", "FX")]
    public async Task WithholdingSettlementDoesNotOverstateRealizedFxBasis()
    {
        await using var fixture = await FxFixture.CreateAsync();
        var apPayment = await fixture.PostApSettlementWithWithholdingAsync(invoiceRate: 10m, paymentRate: 12m, cashForeignAmount: 100m, withholdingForeignAmount: 10m);
        var arReceipt = await fixture.PostArSettlementWithWithholdingAsync(invoiceRate: 10m, receiptRate: 12m, cashForeignAmount: 90m, withholdingForeignAmount: 10m);

        var ap = await fixture.Service.PostRealizedFxForApPaymentAsync(apPayment.Id);
        var ar = await fixture.Service.PostRealizedFxForArReceiptAsync(arReceipt.Id);

        ap.Should().ContainSingle();
        ap[0].SettledForeignAmount.Should().Be(110m);
        ap[0].GainLossAmount.Should().Be(220m);
        ar.Should().ContainSingle();
        ar[0].SettledForeignAmount.Should().Be(100m);
        ar[0].GainLossAmount.Should().Be(200m);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FXSettlementRevaluation")]
    [Trait("Category", "FX")]
    public async Task UnrealizedRevaluationPostsApArAndForeignBankSignsThroughPostingEngine()
    {
        await using var fixture = await FxFixture.CreateAsync();
        await fixture.PostOpenArInvoiceAsync(rate: 10m, foreignAmount: 100m);
        await fixture.PostOpenApInvoiceAsync(rate: 10m, foreignAmount: 100m);
        await fixture.PostForeignBankBalanceAsync(rate: 10m, foreignAmount: 100m);
        fixture.SeedExchangeRate(12m, new DateTime(2026, 7, 31), ExchangeRateType.MonthEnd);
        await fixture.Db.SaveChangesAsync();

        var batch = await fixture.RunRevaluationAsync(new RevaluationRequestDto
        {
            RevaluationDate = new DateTime(2026, 7, 31),
            RevaluationType = "Month-End",
                AccountingBookCode = "IFRS"
        });

        batch.Status.Should().Be("Posted");
        batch.Lines.Should().Contain(l => l.SourceModule == "AR" && l.GainLossType == "Gain" && l.GainLossAmount == 200m);
        batch.Lines.Should().Contain(l => l.SourceModule == "AP" && l.GainLossType == "Loss" && l.GainLossAmount == -200m);
        batch.Lines.Should().Contain(l => l.SourceModule == "BankCash" && l.GainLossType == "Gain" && l.GainLossAmount == 200m);
        var journalLines = await fixture.Db.AccountTransactions
            .Where(t => t.JournalEntryId == batch.JournalEntryId)
            .ToListAsync();
        journalLines.Sum(l => l.DebitAmount).Should().Be(journalLines.Sum(l => l.CreditAmount));
        journalLines.Should().Contain(t => t.AccountId == fixture.ArControl.Id && t.DebitAmount == 200m);
        journalLines.Should().Contain(t => t.AccountId == fixture.ApControl.Id && t.CreditAmount == 200m);
        journalLines.Should().Contain(t => t.AccountId == fixture.BankGl.Id && t.DebitAmount == 200m);
        journalLines.Where(t => t.AccountId == fixture.UnrealizedGain.Id).Sum(t => t.CreditAmount).Should().Be(400m);
        journalLines.Should().Contain(t => t.AccountId == fixture.UnrealizedLoss.Id && t.DebitAmount == 200m);
        fixture.Audit.Events.Should().Contain(e => e.EventType == FinanceAuditEvents.UnrealizedRevaluationPosted);
        fixture.Audit.Events.Should().Contain(e => e.EventType == FinanceAuditEvents.ExchangeRateUsedForRevaluation);
        fixture.Audit.Events.Should().Contain(e => e.EventType == FinanceAuditEvents.ForeignBankRevaluationPosted);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FXSettlementRevaluation")]
    [Trait("Category", "FX")]
    public async Task ForeignBankRateIncreasePostsBankDebitAndUnrealizedGain()
    {
        await using var fixture = await FxFixture.CreateAsync();
        await fixture.PostForeignBankBalanceAsync(rate: 10m, foreignAmount: 100m);
        fixture.SeedExchangeRate(12m, new DateTime(2026, 7, 31), ExchangeRateType.MonthEnd);
        await fixture.Db.SaveChangesAsync();

        var batch = await fixture.RunRevaluationAsync(new RevaluationRequestDto
        {
            RevaluationDate = new DateTime(2026, 7, 31),
            RevaluationType = "Month-End",
                AccountingBookCode = "IFRS"
        });

        batch.Lines.Should().ContainSingle(l => l.SourceModule == "BankCash" && l.GainLossType == "Gain" && l.GainLossAmount == 200m);
        var journalLines = await fixture.Db.AccountTransactions.Where(t => t.JournalEntryId == batch.JournalEntryId).ToListAsync();
        journalLines.Should().Contain(t => t.AccountId == fixture.BankGl.Id && t.DebitAmount == 200m);
        journalLines.Should().Contain(t => t.AccountId == fixture.UnrealizedGain.Id && t.CreditAmount == 200m);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FXSettlementRevaluation")]
    [Trait("Category", "FX")]
    public async Task ForeignBankRateDecreasePostsUnrealizedLossAndBankCredit()
    {
        await using var fixture = await FxFixture.CreateAsync();
        await fixture.PostForeignBankBalanceAsync(rate: 10m, foreignAmount: 100m);
        fixture.SeedExchangeRate(8m, new DateTime(2026, 7, 31), ExchangeRateType.MonthEnd);
        await fixture.Db.SaveChangesAsync();

        var batch = await fixture.RunRevaluationAsync(new RevaluationRequestDto
        {
            RevaluationDate = new DateTime(2026, 7, 31),
            RevaluationType = "Month-End",
                AccountingBookCode = "IFRS"
        });

        batch.Lines.Should().ContainSingle(l => l.SourceModule == "BankCash" && l.GainLossType == "Loss" && l.GainLossAmount == -200m);
        var journalLines = await fixture.Db.AccountTransactions.Where(t => t.JournalEntryId == batch.JournalEntryId).ToListAsync();
        journalLines.Should().Contain(t => t.AccountId == fixture.UnrealizedLoss.Id && t.DebitAmount == 200m);
        journalLines.Should().Contain(t => t.AccountId == fixture.BankGl.Id && t.CreditAmount == 200m);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FXSettlementRevaluation")]
    [Trait("Category", "FX")]
    public async Task ForeignBankRevaluationUsesPostedGlSnapshotsNotBankCurrentBalance()
    {
        await using var fixture = await FxFixture.CreateAsync();
        await fixture.PostForeignBankBalanceAsync(rate: 10m, foreignAmount: 100m);
        var bank = await fixture.Db.BankAccounts.SingleAsync(b => b.TenantId == fixture.TenantId);
        bank.CurrentBalance = 999999m;
        fixture.SeedExchangeRate(12m, new DateTime(2026, 7, 31), ExchangeRateType.MonthEnd);
        await fixture.Db.SaveChangesAsync();

        var batch = await fixture.RunRevaluationAsync(new RevaluationRequestDto
        {
            RevaluationDate = new DateTime(2026, 7, 31),
            RevaluationType = "Month-End",
                AccountingBookCode = "IFRS"
        });

        batch.Lines.Should().ContainSingle(l => l.SourceModule == "BankCash" && l.ForeignCurrencyBalance == 100m && l.GainLossAmount == 200m);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FXSettlementRevaluation")]
    [Trait("Category", "FX")]
    public async Task MissingClosingRateBlocksUnrealizedRevaluation()
    {
        await using var fixture = await FxFixture.CreateAsync();
        await fixture.PostOpenArInvoiceAsync(rate: 10m, foreignAmount: 100m);

        await fixture.Service.Invoking(s => s.RunUnrealizedRevaluationAsync(new RevaluationRequestDto
            {
                RevaluationDate = new DateTime(2026, 7, 31),
                RevaluationType = "Month-End",
                AccountingBookCode = "IFRS"
            }))
            .Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*No approved*MonthEnd exchange rate*");
        fixture.Audit.Events.Should().Contain(e => e.EventType == FinanceAuditEvents.FxPostingBlockedInvalidConfiguration);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FXSettlementRevaluation")]
    [Trait("Category", "FX")]
    public async Task DuplicateRevaluationIsIdempotentAndReversalPostsInOpenPeriod()
    {
        await using var fixture = await FxFixture.CreateAsync();
        await fixture.PostOpenArInvoiceAsync(rate: 10m, foreignAmount: 100m);
        fixture.SeedExchangeRate(12m, new DateTime(2026, 7, 31), ExchangeRateType.MonthEnd);
        await fixture.Db.SaveChangesAsync();

        var first = await fixture.RunRevaluationAsync(new RevaluationRequestDto
        {
            RevaluationDate = new DateTime(2026, 7, 31),
            RevaluationType = "Month-End",
                AccountingBookCode = "IFRS"
        });
        var second = await fixture.Service.RunUnrealizedRevaluationAsync(new RevaluationRequestDto
        {
            RevaluationDate = new DateTime(2026, 7, 31),
            RevaluationType = "Month-End",
            AccountingBookCode = "IFRS",
            ExpectedPreviewFingerprint = first.PreviewFingerprint
        });

        second.Id.Should().Be(first.Id);
        (await fixture.Db.FxRevaluationBatches.CountAsync()).Should().Be(1);

        var reversed = await fixture.Service.ReverseRevaluationBatchAsync(first.Id, new DateTime(2026, 8, 1), "Auto reversal", CancellationToken.None);

        reversed.Status.Should().Be("Reversed");
        reversed.ReversalJournalEntryId.Should().NotBeNull();
        reversed.ReversalPostingEventId.Should().NotBeNull();
        var duplicate = await fixture.Service.ReverseRevaluationBatchAsync(first.Id, new DateTime(2026, 8, 1), "Auto reversal", CancellationToken.None);
        duplicate.ReversalPostingEventId.Should().Be(reversed.ReversalPostingEventId);
        (await fixture.Db.FinancePostingEvents.CountAsync(e => e.SourceModule == "FX" && e.PostingAction == "ReverseUnrealizedRevaluation")).Should().Be(1);
        var linkState = await fixture.Db.AccountCurrencyLinks.AsNoTracking().SingleAsync(link => link.AccountId == fixture.ArControl.Id);
        linkState.LastRevaluationDate.Should().BeNull();
        linkState.CumulativeRevaluationAdjustment.Should().Be(0m);
        var history = await fixture.Service.GetRevaluationBatchesAsync(new DateTime(2026, 1, 1), new DateTime(2026, 12, 31));
        history.Should().ContainSingle(item => item.Id == first.Id && item.Status == "Reversed" && item.ReversalJournalEntryId.HasValue);
        fixture.Audit.Events.Should().Contain(e => e.EventType == FinanceAuditEvents.UnrealizedRevaluationReversed);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FXSettlementRevaluation")]
    [Trait("Category", "FX")]
    public async Task LaterExchangeRateEditDoesNotMutatePostedRealizedFxSnapshot()
    {
        await using var fixture = await FxFixture.CreateAsync();
        var scenario = await fixture.PostApSettlementAsync(invoiceRate: 10m, paymentRate: 12m, settledForeignAmount: 100m);
        var realized = (await fixture.Service.PostRealizedFxForApPaymentAsync(scenario.ApPayment!.Id)).Single();
        var originalJournalLines = await fixture.Db.AccountTransactions
            .Where(t => t.JournalEntryId == realized.JournalEntryId)
            .Select(t => new { t.AccountId, t.DebitAmount, t.CreditAmount })
            .ToListAsync();

        foreach (var rate in fixture.Db.ExchangeRates.Where(r => r.TenantId == fixture.TenantId))
        {
            rate.Rate = 99m;
            rate.InverseRate = 1m / 99m;
        }
        await fixture.Db.SaveChangesAsync();

        var persisted = await fixture.Db.FxRealizedSettlements.AsNoTracking().SingleAsync(s => s.Id == realized.Id);
        persisted.HistoricalExchangeRate.Should().Be(10m);
        persisted.SettlementExchangeRate.Should().Be(12m);
        persisted.GainLossAmount.Should().Be(200m);
        var currentJournalLines = await fixture.Db.AccountTransactions
            .Where(t => t.JournalEntryId == realized.JournalEntryId)
            .Select(t => new { t.AccountId, t.DebitAmount, t.CreditAmount })
            .ToListAsync();
        currentJournalLines.Should().BeEquivalentTo(originalJournalLines);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FXSettlementRevaluation")]
    [Trait("Category", "FX")]
    public async Task LaterExchangeRateEditDoesNotMutatePostedUnrealizedRevaluationSnapshot()
    {
        await using var fixture = await FxFixture.CreateAsync();
        await fixture.PostOpenArInvoiceAsync(rate: 10m, foreignAmount: 100m);
        var closingRate = fixture.SeedExchangeRate(12m, new DateTime(2026, 7, 31), ExchangeRateType.MonthEnd);
        await fixture.Db.SaveChangesAsync();
        var batch = await fixture.RunRevaluationAsync(new RevaluationRequestDto
        {
            RevaluationDate = new DateTime(2026, 7, 31),
            RevaluationType = "Month-End",
                AccountingBookCode = "IFRS"
        });

        closingRate.Rate = 99m;
        closingRate.InverseRate = 1m / 99m;
        await fixture.Db.SaveChangesAsync();

        var line = await fixture.Db.FxRevaluationLines.AsNoTracking().SingleAsync(l => l.FxRevaluationBatchId == batch.Id);
        line.ClosingExchangeRate.Should().Be(12m);
        line.GainLossAmount.Should().Be(200m);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FXSettlementRevaluation")]
    [Trait("Category", "FX")]
    public async Task RevaluationUsesRequestedQuarterEndRateType()
    {
        await using var fixture = await FxFixture.CreateAsync();
        await fixture.PostOpenArInvoiceAsync(rate: 10m, foreignAmount: 100m, postingDate: new DateTime(2026, 6, 1));
        var arPolicy = await fixture.Db.AccountCurrencyLinks.SingleAsync(link => link.AccountId == fixture.ArControl.Id);
        arPolicy.RevaluationRateType = "Quarter-End";
        fixture.SeedExchangeRate(12m, new DateTime(2026, 6, 30), ExchangeRateType.MonthEnd);
        fixture.SeedExchangeRate(13m, new DateTime(2026, 6, 30), ExchangeRateType.QuarterEnd);
        await fixture.Db.SaveChangesAsync();

        var batch = await fixture.RunRevaluationAsync(new RevaluationRequestDto
        {
            RevaluationDate = new DateTime(2026, 6, 30),
            RevaluationType = "Quarter-End",
                AccountingBookCode = "IFRS"
        });

        batch.Lines.Should().ContainSingle(l => l.ClosingExchangeRate == 13m && l.GainLossAmount == 300m);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FXSettlementRevaluation")]
    [Trait("Category", "FX")]
    public async Task RevaluationUsesConfiguredClosingQuoteSide()
    {
        await using var fixture = await FxFixture.CreateAsync();
        await fixture.PostOpenArInvoiceAsync(rate: 10m, foreignAmount: 100m);
        var arPolicy = await fixture.Db.AccountCurrencyLinks.SingleAsync(link => link.AccountId == fixture.ArControl.Id);
        arPolicy.RevaluationQuoteSide = ExchangeRateQuoteSide.Buying;
        fixture.SeedExchangeRate(12m, new DateTime(2026, 7, 31), ExchangeRateType.MonthEnd, ExchangeRateQuoteSide.Mid);
        var buying = fixture.SeedExchangeRate(13m, new DateTime(2026, 7, 31), ExchangeRateType.MonthEnd, ExchangeRateQuoteSide.Buying);
        await fixture.Db.SaveChangesAsync();

        var batch = await fixture.RunRevaluationAsync(new RevaluationRequestDto
        {
            RevaluationDate = new DateTime(2026, 7, 31),
            RevaluationType = "Month-End",
                AccountingBookCode = "IFRS"
        });

        batch.Lines.Should().OnlyContain(line => line.ClosingExchangeRateId == buying.Id);
        batch.Lines.Should().OnlyContain(line => line.ClosingExchangeRate == 13m);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FXSettlementRevaluation")]
    [Trait("Category", "FX")]
    public async Task MarkedGeneralLedgerMonetaryAccountIsRevaluedAndUnmarkedAccountIsExcluded()
    {
        await using var fixture = await FxFixture.CreateAsync();
        var marked = await fixture.PostGeneralFxExposureAsync("1150", AccountType.Asset, revaluationRequired: true, 10m, 100m);
        var unmarked = await fixture.PostGeneralFxExposureAsync("1160", AccountType.Asset, revaluationRequired: false, 10m, 100m);
        fixture.SeedExchangeRate(12m, new DateTime(2026, 7, 31), ExchangeRateType.MonthEnd);
        await fixture.Db.SaveChangesAsync();

        var batch = await fixture.RunRevaluationAsync(new RevaluationRequestDto
        {
            RevaluationDate = new DateTime(2026, 7, 31),
            RevaluationType = "Month-End",
                AccountingBookCode = "IFRS"
        });

        batch.Lines.Should().ContainSingle(line => line.AccountId == marked.Id && line.SourceModule == "GL" && line.GainLossAmount == 200m);
        batch.Lines.Should().NotContain(line => line.AccountId == unmarked.Id);
    }

    [Theory]
    [InlineData(AccountType.Asset, 200, "Gain")]
    [InlineData(AccountType.Liability, -200, "Loss")]
    [InlineData(AccountType.Equity, -200, "Loss")]
    [InlineData(AccountType.Revenue, -200, "Loss")]
    [InlineData(AccountType.Expense, 200, "Gain")]
    [Trait("Batch", "FinanceGoLive-FXSettlementRevaluation")]
    [Trait("Category", "FX")]
    public async Task ExplicitPolicyCanRevalueEveryCoreAccountTypeUsingSignedBalances(
        AccountType accountType, decimal expectedAdjustment, string expectedGainLossType)
    {
        await using var fixture = await FxFixture.CreateAsync();
        var account = await fixture.PostGeneralFxExposureAsync(
            $"CORE-{accountType}", accountType, revaluationRequired: true, 10m, 100m);
        fixture.SeedExchangeRate(12m, new DateTime(2026, 7, 31), ExchangeRateType.MonthEnd);
        await fixture.Db.SaveChangesAsync();

        var preview = await fixture.Service.PreviewCurrencyRevaluationAsync(new RevaluationRequestDto
        {
            RevaluationDate = new DateTime(2026, 7, 31),
            RevaluationType = "Month-End",
            AccountingBookCode = "IFRS"
        });
        var shouldWarn = accountType == AccountType.Equity
            || accountType == AccountType.Revenue
            || accountType == AccountType.Expense;

        preview.Lines.Should().ContainSingle(line => line.AccountId == account.Id
            && line.GainLossAmount == expectedAdjustment
            && line.GainLossType == expectedGainLossType
            && line.HasGovernanceWarning == shouldWarn);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FXSettlementRevaluation")]
    [Trait("Category", "FX")]
    public async Task PostedHistoryRetainsNonstandardPolicyWarningCount()
    {
        await using var fixture = await FxFixture.CreateAsync();
        await fixture.PostGeneralFxExposureAsync("CORE-EQUITY-HISTORY", AccountType.Equity, true, 10m, 100m);
        fixture.SeedExchangeRate(12m, new DateTime(2026, 7, 31), ExchangeRateType.MonthEnd);
        await fixture.Db.SaveChangesAsync();

        await fixture.RunRevaluationAsync(new RevaluationRequestDto
        {
            RevaluationDate = new DateTime(2026, 7, 31),
            RevaluationType = "Month-End",
            AccountingBookCode = "IFRS"
        });
        var history = await fixture.Service.GetRevaluationBatchesAsync(
            new DateTime(2026, 7, 1), new DateTime(2026, 7, 31));

        history.Should().ContainSingle(item => item.AccountingBookCode == "IFRS"
            && item.ExposureCount == 1 && item.NonstandardPolicyCount == 1);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FXSettlementRevaluation")]
    [Trait("Category", "FX")]
    public async Task PostingRejectsWhenPreviewEvidenceHasChanged()
    {
        await using var fixture = await FxFixture.CreateAsync();
        await fixture.PostOpenArInvoiceAsync(rate: 10m, foreignAmount: 100m);
        var closingRate = fixture.SeedExchangeRate(12m, new DateTime(2026, 7, 31), ExchangeRateType.MonthEnd);
        await fixture.Db.SaveChangesAsync();
        var preview = await fixture.Service.PreviewCurrencyRevaluationAsync(new RevaluationRequestDto
        {
            RevaluationDate = new DateTime(2026, 7, 31),
            RevaluationType = "Month-End",
                AccountingBookCode = "IFRS"
        });

        closingRate.Rate = 13m;
        await fixture.Db.SaveChangesAsync();

        await fixture.Service.Invoking(service => service.RunUnrealizedRevaluationAsync(new RevaluationRequestDto
            {
                RevaluationDate = new DateTime(2026, 7, 31),
                RevaluationType = "Month-End",
                AccountingBookCode = "IFRS",
                ExpectedPreviewFingerprint = preview.PreviewFingerprint
            }))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*changed after preview*");
        (await fixture.Db.FxRevaluationBatches.CountAsync()).Should().Be(0);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FXSettlementRevaluation")]
    [Trait("Category", "FX")]
    public async Task PostingRequiresCanonicalPreviewFingerprint()
    {
        await using var fixture = await FxFixture.CreateAsync();
        await fixture.PostOpenArInvoiceAsync(rate: 10m, foreignAmount: 100m);
        fixture.SeedExchangeRate(12m, new DateTime(2026, 7, 31), ExchangeRateType.MonthEnd);
        await fixture.Db.SaveChangesAsync();

        await fixture.Service.Invoking(service => service.RunUnrealizedRevaluationAsync(new RevaluationRequestDto
            {
                RevaluationDate = new DateTime(2026, 7, 31),
                RevaluationType = "Month-End",
                AccountingBookCode = "IFRS"
            }))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*preview fingerprint is required*");

        await fixture.Service.Invoking(service => service.RunUnrealizedRevaluationAsync(new RevaluationRequestDto
            {
                RevaluationDate = new DateTime(2026, 7, 31),
                RevaluationType = "Month-End",
                AccountingBookCode = "IFRS",
                ExpectedPreviewFingerprint = new string('z', 64)
            }))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*fingerprint is malformed*");
        (await fixture.Db.FxRevaluationBatches.CountAsync()).Should().Be(0);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FXSettlementRevaluation")]
    [Trait("Category", "FX")]
    public async Task GovernanceWarningDriftInvalidatesPreviewFingerprint()
    {
        await using var fixture = await FxFixture.CreateAsync();
        var account = await fixture.PostGeneralFxExposureAsync(
            "CORE-EQUITY-TAMPER", AccountType.Equity, revaluationRequired: true, 10m, 100m);
        fixture.SeedExchangeRate(12m, new DateTime(2026, 7, 31), ExchangeRateType.MonthEnd);
        await fixture.Db.SaveChangesAsync();
        var preview = await fixture.Service.PreviewCurrencyRevaluationAsync(new RevaluationRequestDto
        {
            RevaluationDate = new DateTime(2026, 7, 31),
            RevaluationType = "Month-End",
            AccountingBookCode = "IFRS"
        });

        account.AccountType = AccountType.Asset;
        var mapping = await fixture.Db.AccountAccountingBooks
            .Include(item => item.AccountClassification)
            .SingleAsync(item => item.AccountId == account.Id && item.AccountingBook.Code == "IFRS");
        mapping.AccountClassification!.CoreAccountType = AccountType.Asset;
        await fixture.Db.SaveChangesAsync();

        await fixture.Service.Invoking(service => service.RunUnrealizedRevaluationAsync(new RevaluationRequestDto
            {
                RevaluationDate = new DateTime(2026, 7, 31),
                RevaluationType = "Month-End",
                AccountingBookCode = "IFRS",
                ExpectedPreviewFingerprint = preview.PreviewFingerprint
            }))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*changed after preview*");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FXSettlementRevaluation")]
    [Trait("Category", "FX")]
    public async Task GainLossPostingAccountDriftInvalidatesPreviewFingerprint()
    {
        await using var fixture = await FxFixture.CreateAsync();
        await fixture.PostOpenArInvoiceAsync(rate: 10m, foreignAmount: 100m);
        fixture.SeedExchangeRate(12m, new DateTime(2026, 7, 31), ExchangeRateType.MonthEnd);
        await fixture.Db.SaveChangesAsync();
        var preview = await fixture.Service.PreviewCurrencyRevaluationAsync(new RevaluationRequestDto
        {
            RevaluationDate = new DateTime(2026, 7, 31),
            RevaluationType = "Month-End",
            AccountingBookCode = "IFRS"
        });

        fixture.Settings.UnrealizedFxGainAccountId = fixture.RealizedGain.Id;
        await fixture.Db.SaveChangesAsync();

        await fixture.Service.Invoking(service => service.RunUnrealizedRevaluationAsync(new RevaluationRequestDto
            {
                RevaluationDate = new DateTime(2026, 7, 31),
                RevaluationType = "Month-End",
                AccountingBookCode = "IFRS",
                ExpectedPreviewFingerprint = preview.PreviewFingerprint
            }))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*changed after preview*");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FXSettlementRevaluation")]
    [Trait("Category", "FX")]
    public async Task RetryAfterPostCommitFailureReconcilesSingleJournalAndAuditEvidence()
    {
        await using var fixture = await FxFixture.CreateAsync();
        await fixture.PostOpenArInvoiceAsync(rate: 10m, foreignAmount: 100m);
        fixture.SeedExchangeRate(12m, new DateTime(2026, 7, 31), ExchangeRateType.MonthEnd);
        await fixture.Db.SaveChangesAsync();
        var preview = await fixture.Service.PreviewCurrencyRevaluationAsync(new RevaluationRequestDto
        {
            RevaluationDate = new DateTime(2026, 7, 31),
            RevaluationType = "Month-End",
            AccountingBookCode = "IFRS"
        });
        var request = new RevaluationRequestDto
        {
            RevaluationDate = new DateTime(2026, 7, 31),
            RevaluationType = "Month-End",
            AccountingBookCode = "IFRS",
            ExpectedPreviewFingerprint = preview.PreviewFingerprint
        };

        var faultingService = fixture.CreatePostCommitFaultService();
        await faultingService.Invoking(service => service.RunUnrealizedRevaluationAsync(request))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*simulated post-commit interruption*");

        var interrupted = await fixture.Db.FxRevaluationBatches.SingleAsync();
        interrupted.Status.Should().Be("PostingRecoveryRequired");
        interrupted.JournalEntryId.Should().NotBeNull();
        var recovered = await fixture.Service.RunUnrealizedRevaluationAsync(request);

        recovered.Status.Should().Be("Posted");
        recovered.JournalEntryId.Should().Be(interrupted.JournalEntryId);
        (await fixture.Db.FinancePostingEvents.CountAsync(item =>
            item.SourceModule == "FX" && item.PostingAction == "UnrealizedRevaluation")).Should().Be(1);
        (await fixture.Db.JournalEntries.CountAsync(item => item.Id == recovered.JournalEntryId)).Should().Be(1);
        fixture.Audit.Events.Count(item =>
            item.EventType == FinanceAuditEvents.UnrealizedRevaluationPosted).Should().Be(1);
    }

    [Theory]
    [InlineData(AncillaryFaultPoint.AfterRateUsage)]
    [InlineData(AncillaryFaultPoint.AfterPostedAudit)]
    [InlineData(AncillaryFaultPoint.AfterRateAudit)]
    [InlineData(AncillaryFaultPoint.AfterBankAudit)]
    [Trait("Batch", "FinanceGoLive-FXSettlementRevaluation")]
    [Trait("Category", "FX")]
    public async Task RetryAfterPartialAncillaryFinalizationRecordsEverySideEffectExactlyOnce(
        AncillaryFaultPoint faultPoint)
    {
        await using var fixture = await FxFixture.CreateAsync();
        await fixture.PostForeignBankBalanceAsync(rate: 10m, foreignAmount: 100m);
        var closingRate = fixture.SeedExchangeRate(
            12m,
            new DateTime(2026, 7, 31),
            ExchangeRateType.MonthEnd);
        await fixture.Db.SaveChangesAsync();
        var preview = await fixture.Service.PreviewCurrencyRevaluationAsync(new RevaluationRequestDto
        {
            RevaluationDate = new DateTime(2026, 7, 31),
            RevaluationType = "Month-End",
            AccountingBookCode = "IFRS"
        });
        var request = new RevaluationRequestDto
        {
            RevaluationDate = new DateTime(2026, 7, 31),
            RevaluationType = "Month-End",
            AccountingBookCode = "IFRS",
            ExpectedPreviewFingerprint = preview.PreviewFingerprint
        };

        var faultingService = fixture.CreateAncillaryFaultService(faultPoint);
        await faultingService.Invoking(service => service.RunUnrealizedRevaluationAsync(request))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*simulated ancillary finalization interruption*");

        var interrupted = await fixture.Db.FxRevaluationBatches.SingleAsync();
        interrupted.Status.Should().Be("PostingRecoveryRequired");
        var recovered = await fixture.CreateDurableAuditService().RunUnrealizedRevaluationAsync(request);

        recovered.Status.Should().Be("Posted");
        recovered.JournalEntryId.Should().NotBeNull();
        recovered.PostingEventId.Should().NotBeNull();
        recovered.Lines.Should().OnlyContain(line =>
            line.JournalEntryId == recovered.JournalEntryId
            && line.PostingEventId == recovered.PostingEventId);
        (await fixture.Db.JournalEntries.CountAsync(item => item.Id == recovered.JournalEntryId)).Should().Be(1);
        (await fixture.Db.FinancePostingEvents.CountAsync(item =>
            item.SourceModule == "FX" && item.PostingAction == "UnrealizedRevaluation")).Should().Be(1);

        var usage = await fixture.Db.FxRevaluationRateUsages.AsNoTracking().SingleAsync();
        usage.FxRevaluationBatchId.Should().Be(recovered.Id);
        usage.PostingEventId.Should().Be(recovered.PostingEventId!.Value);
        usage.ExchangeRateId.Should().Be(closingRate.Id);
        usage.UsageCount.Should().Be(1);
        var storedRate = await fixture.Db.ExchangeRates.AsNoTracking().SingleAsync(item => item.Id == closingRate.Id);
        storedRate.TransactionCount.Should().Be(1);

        (await fixture.Db.AuditLogs.CountAsync(item =>
            item.Action == FinanceAuditEvents.UnrealizedRevaluationPosted)).Should().Be(1);
        (await fixture.Db.AuditLogs.CountAsync(item =>
            item.Action == FinanceAuditEvents.ExchangeRateUsedForRevaluation)).Should().Be(1);
        (await fixture.Db.AuditLogs.CountAsync(item =>
            item.Action == FinanceAuditEvents.ForeignBankRevaluationPosted)).Should().Be(1);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FXSettlementRevaluation")]
    [Trait("Category", "FX")]
    public async Task RevaluationFrequencyControlsWhichRunIncludesTheAccount()
    {
        await using var fixture = await FxFixture.CreateAsync();
        await fixture.PostOpenArInvoiceAsync(rate: 10m, foreignAmount: 100m, postingDate: new DateTime(2026, 6, 1));
        var policy = await fixture.Db.AccountCurrencyLinks.SingleAsync(link => link.AccountId == fixture.ArControl.Id);
        policy.RevaluationFrequency = RevaluationFrequency.Quarterly;
        fixture.SeedExchangeRate(12m, new DateTime(2026, 6, 30), ExchangeRateType.MonthEnd);
        await fixture.Db.SaveChangesAsync();

        var monthEnd = await fixture.Service.PreviewCurrencyRevaluationAsync(new RevaluationRequestDto
        {
            RevaluationDate = new DateTime(2026, 6, 30),
            RevaluationType = "Month-End",
                AccountingBookCode = "IFRS"
        });
        var quarterEnd = await fixture.Service.PreviewCurrencyRevaluationAsync(new RevaluationRequestDto
        {
            RevaluationDate = new DateTime(2026, 6, 30),
            RevaluationType = "Quarter-End",
                AccountingBookCode = "IFRS"
        });

        monthEnd.Lines.Should().BeEmpty();
        quarterEnd.Lines.Should().ContainSingle(line => line.AccountId == fixture.ArControl.Id && line.RevaluationFrequency == "Quarterly");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FXSettlementRevaluation")]
    [Trait("Category", "FX")]
    public async Task NextPeriodRevaluationAfterUnreversedPriorBatchUsesPriorCarryingAdjustment()
    {
        await using var fixture = await FxFixture.CreateAsync();
        await fixture.PostOpenArInvoiceAsync(rate: 10m, foreignAmount: 100m);
        fixture.SeedExchangeRate(12m, new DateTime(2026, 7, 31), ExchangeRateType.MonthEnd);
        fixture.SeedExchangeRate(13m, new DateTime(2026, 8, 31), ExchangeRateType.MonthEnd);
        await fixture.Db.SaveChangesAsync();
        await fixture.RunRevaluationAsync(new RevaluationRequestDto
        {
            RevaluationDate = new DateTime(2026, 7, 31),
            RevaluationType = "Month-End",
                AccountingBookCode = "IFRS"
        });

        var second = await fixture.RunRevaluationAsync(new RevaluationRequestDto
        {
            RevaluationDate = new DateTime(2026, 8, 31),
            RevaluationType = "Month-End",
                AccountingBookCode = "IFRS"
        });

        second.Lines.Should().ContainSingle(l => l.SourceModule == "AR" && l.GainLossAmount == 100m);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FXSettlementRevaluation")]
    [Trait("Category", "FX")]
    public async Task RevaluationAfterReversalDoesNotDoubleCountPriorAdjustment()
    {
        await using var fixture = await FxFixture.CreateAsync();
        await fixture.PostOpenArInvoiceAsync(rate: 10m, foreignAmount: 100m);
        fixture.SeedExchangeRate(12m, new DateTime(2026, 7, 31), ExchangeRateType.MonthEnd);
        fixture.SeedExchangeRate(13m, new DateTime(2026, 8, 31), ExchangeRateType.MonthEnd);
        await fixture.Db.SaveChangesAsync();
        var first = await fixture.RunRevaluationAsync(new RevaluationRequestDto
        {
            RevaluationDate = new DateTime(2026, 7, 31),
            RevaluationType = "Month-End",
                AccountingBookCode = "IFRS"
        });
        await fixture.Service.ReverseRevaluationBatchAsync(first.Id, new DateTime(2026, 8, 1), "Auto reversal", CancellationToken.None);

        var second = await fixture.RunRevaluationAsync(new RevaluationRequestDto
        {
            RevaluationDate = new DateTime(2026, 8, 31),
            RevaluationType = "Month-End",
                AccountingBookCode = "IFRS"
        });

        second.Lines.Should().ContainSingle(l => l.SourceModule == "AR" && l.GainLossAmount == 300m);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FXSettlementRevaluation")]
    [Trait("Category", "FX")]
    public async Task ClosedPeriodRevaluationIsRejected()
    {
        await using var fixture = await FxFixture.CreateAsync();
        await fixture.PostOpenArInvoiceAsync(rate: 10m, foreignAmount: 100m, postingDate: new DateTime(2026, 6, 30));
        fixture.SeedExchangeRate(12m, new DateTime(2026, 7, 31), ExchangeRateType.MonthEnd);
        foreach (var period in fixture.Db.FiscalPeriods.Where(p => p.TenantId == fixture.TenantId))
        {
            period.IsOpen = false;
            period.IsClosed = true;
            period.PeriodStatus = "Closed";
        }
        await fixture.Db.SaveChangesAsync();

        await fixture.Service.Invoking(s => s.RunUnrealizedRevaluationAsync(new RevaluationRequestDto
            {
                RevaluationDate = new DateTime(2026, 7, 31),
                RevaluationType = "Month-End",
                AccountingBookCode = "IFRS"
            }))
            .Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*period is not open*");
    }

    private sealed class FxFixture : IAsyncDisposable
    {
        private readonly IFinancePostingEngine _postingEngine;

        private FxFixture(
            ApplicationDbContext db,
            Guid tenantId,
            FinanceSettings settings,
            IFinancePostingEngine postingEngine,
            CurrencyRevaluationService service,
            CapturingFinanceAuditService audit,
            Account apControl,
            Account arControl,
            Account bankGl,
            Account expense,
            Account revenue,
            Account realizedGain,
            Account realizedLoss,
            Account unrealizedGain,
            Account unrealizedLoss)
        {
            Db = db;
            TenantId = tenantId;
            Settings = settings;
            _postingEngine = postingEngine;
            Service = service;
            Audit = audit;
            ApControl = apControl;
            ArControl = arControl;
            BankGl = bankGl;
            Expense = expense;
            Revenue = revenue;
            RealizedGain = realizedGain;
            RealizedLoss = realizedLoss;
            UnrealizedGain = unrealizedGain;
            UnrealizedLoss = unrealizedLoss;
        }

        public ApplicationDbContext Db { get; }
        public Guid TenantId { get; }
        public FinanceSettings Settings { get; }
        public CurrencyRevaluationService Service { get; }
        public CapturingFinanceAuditService Audit { get; }
        public Account ApControl { get; }
        public Account ArControl { get; }
        public Account BankGl { get; }
        public Account Expense { get; }
        public Account Revenue { get; }
        public Account RealizedGain { get; }
        public Account RealizedLoss { get; }
        public Account UnrealizedGain { get; }
        public Account UnrealizedLoss { get; }

        public static async Task<FxFixture> CreateAsync(bool periodOpen = true, bool periodClosed = false)
        {
            var tenantId = Guid.NewGuid();
            var db = CreateContext();
            SeedTenant(db, tenantId, "GHS");
            SeedOpenPeriod(db, tenantId, periodOpen, periodClosed);
            var ap = SeedAccount(db, tenantId, "2100", AccountType.Liability, true);
            var ar = SeedAccount(db, tenantId, "1100", AccountType.Asset, true);
            var bank = SeedAccount(db, tenantId, "1010", AccountType.Asset, true);
            var expense = SeedAccount(db, tenantId, "5000", AccountType.Expense, true);
            var revenue = SeedAccount(db, tenantId, "4000", AccountType.Revenue, true);
            var realizedGain = SeedAccount(db, tenantId, "7200", AccountType.Revenue);
            var realizedLoss = SeedAccount(db, tenantId, "7210", AccountType.Expense);
            var unrealizedGain = SeedAccount(db, tenantId, "7100", AccountType.Revenue);
            var unrealizedLoss = SeedAccount(db, tenantId, "7110", AccountType.Expense);
            var settings = SeedFinanceSettings(db, tenantId, ap.Id, ar.Id, realizedGain.Id, realizedLoss.Id, unrealizedGain.Id, unrealizedLoss.Id);
            foreach (var account in new[] { ap, ar, bank, expense, revenue })
            {
                // The posting engine now enforces account/currency authorization before accepting
                // a foreign line. Seed the fixture's intended USD capability explicitly so these
                // FX tests exercise settlement accounting rather than fail at setup validation.
                db.AccountCurrencyLinks.Add(new AccountCurrencyLink
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    AccountId = account.Id,
                    LinkedCurrencyCode = "USD",
                    IsActive = true,
                    EffectiveDate = new DateTime(2026, 1, 1),
                    TransactionRateType = "Daily",
                    RevaluationRateType = "Month-End",
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = "Tests"
                });
            }
            await new FinanceClassificationManifestSeeder(
                    db,
                    NullLogger<FinanceClassificationManifestSeeder>.Instance)
                .SeedAsync(tenantId, new DateTime(2026, 1, 1));
            db.BankAccounts.Add(new BankAccount
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                AccountNumber = "USD-BANK",
                AccountName = "USD Bank",
                BankName = "Test Bank",
                Currency = "USD",
                GLAccountId = bank.Id,
                IsActive = true
            });
            await db.SaveChangesAsync();

            var audit = new CapturingFinanceAuditService();
            var currentUser = CreateCurrentUser(tenantId).Object;
            var postingEngine = new FinancePostingEngine(db, currentUser, Mock.Of<ILogger<FinancePostingEngine>>(), audit);
            var service = new CurrencyRevaluationService(
                db,
                currentUser,
                new TenantSettingsService(db, currentUser),
                postingEngine,
                Mock.Of<ILogger<CurrencyRevaluationService>>(),
                audit);

            return new FxFixture(db, tenantId, settings, postingEngine, service, audit, ap, ar, bank, expense, revenue, realizedGain, realizedLoss, unrealizedGain, unrealizedLoss);
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
        }

        public ExchangeRate SeedExchangeRate(
            decimal rate,
            DateTime effectiveDate,
            ExchangeRateType rateType = ExchangeRateType.Daily,
            ExchangeRateQuoteSide quoteSide = ExchangeRateQuoteSide.Mid)
        {
            var exchangeRate = new ExchangeRate
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                BaseCurrencyCode = "GHS",
                TargetCurrencyCode = "USD",
                Rate = rate,
                InverseRate = 1m / rate,
                EffectiveDate = effectiveDate.Date,
                RateType = rateType,
                QuoteSide = quoteSide,
                RateSource = "Bank of Ghana",
                ApprovalStatus = RateApprovalStatus.Approved,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "Tests"
            };
            Db.ExchangeRates.Add(exchangeRate);
            return exchangeRate;
        }

        public async Task<FxRevaluationBatch> RunRevaluationAsync(RevaluationRequestDto request)
        {
            var preview = await Service.PreviewCurrencyRevaluationAsync(request);
            request.PreviewOnly = false;
            request.ExpectedPreviewFingerprint = preview.PreviewFingerprint;
            return await Service.RunUnrealizedRevaluationAsync(request);
        }

        public CurrencyRevaluationService CreatePostCommitFaultService()
        {
            var currentUser = CreateCurrentUser(TenantId).Object;
            return new CurrencyRevaluationService(
                Db,
                currentUser,
                new TenantSettingsService(Db, currentUser),
                new ThrowAfterSuccessfulRevaluationPostingEngine(_postingEngine),
                Mock.Of<ILogger<CurrencyRevaluationService>>(),
                Audit);
        }

        public CurrencyRevaluationService CreateAncillaryFaultService(AncillaryFaultPoint faultPoint)
        {
            var currentUser = CreateCurrentUser(TenantId).Object;
            var durableAudit = new FinanceAuditService(Db, currentUser, new HttpContextAccessor());
            return new CurrencyRevaluationService(
                Db,
                currentUser,
                new TenantSettingsService(Db, currentUser),
                _postingEngine,
                Mock.Of<ILogger<CurrencyRevaluationService>>(),
                new ThrowAtAncillaryAuditBoundary(durableAudit, faultPoint));
        }

        public CurrencyRevaluationService CreateDurableAuditService()
        {
            var currentUser = CreateCurrentUser(TenantId).Object;
            return new CurrencyRevaluationService(
                Db,
                currentUser,
                new TenantSettingsService(Db, currentUser),
                _postingEngine,
                Mock.Of<ILogger<CurrencyRevaluationService>>(),
                new FinanceAuditService(Db, currentUser, new HttpContextAccessor()));
        }

        public async Task MakeControlLinesFunctionalAsync(
            Guid invoiceJournalEntryId,
            Guid settlementJournalEntryId,
            Guid controlAccountId)
        {
            var journalIds = new[] { invoiceJournalEntryId, settlementJournalEntryId };
            var controlLines = await Db.AccountTransactions
                .Where(line => journalIds.Contains(line.JournalEntryId) && line.AccountId == controlAccountId)
                .ToListAsync();
            foreach (var line in controlLines)
            {
                // Functional lines carry functional transaction amounts and deliberately omit
                // foreign-rate evidence. This matches the production posting-engine contract.
                line.TransactionCurrency = "GHS";
                line.TransactionDebitAmount = line.DebitAmount;
                line.TransactionCreditAmount = line.CreditAmount;
                line.ForeignCurrencyAmount = null;
                line.ExchangeRate = null;
                line.ExchangeRateId = null;
                line.ExchangeRateSource = null;
                line.ExchangeRateDate = null;
            }
            await Db.SaveChangesAsync();
        }

        public async Task<FxScenario> PostApSettlementAsync(
            decimal invoiceRate,
            decimal paymentRate,
            decimal settledForeignAmount,
            decimal? invoiceForeignAmount = null)
        {
            var invoiceAmount = invoiceForeignAmount ?? settledForeignAmount;
            var invoice = await PostOpenApInvoiceAsync(invoiceRate, invoiceAmount);
            var payment = new VendorPayment
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                PaymentNumber = $"VP-{Guid.NewGuid():N}"[..12],
                SupplierId = Guid.NewGuid(),
                PaymentDate = new DateTime(2026, 7, 10),
                TotalAmount = settledForeignAmount,
                AllocatedAmount = settledForeignAmount,
                CurrencyCode = "USD",
                ExchangeRate = paymentRate,
                Status = VendorPaymentStatus.Processed,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "Tests"
            };
            var allocation = new VendorPaymentAllocation
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                VendorPaymentId = payment.Id,
                VendorPayment = payment,
                VendorInvoiceId = invoice.Id,
                VendorInvoice = invoice,
                AllocatedAmount = settledForeignAmount,
                AllocationDate = payment.PaymentDate
            };
            Db.Set<VendorPayment>().Add(payment);
            Db.Set<VendorPaymentAllocation>().Add(allocation);
            SeedExchangeRate(paymentRate, payment.PaymentDate);
            await Db.SaveChangesAsync();

            var result = await _postingEngine.PostAsync(CreateForeignPostingRequest(
                "AP",
                "VendorPayment",
                payment.Id,
                payment.PaymentNumber,
                payment.PaymentDate,
                debitAccountId: ApControl.Id,
                creditAccountId: BankGl.Id,
                foreignAmount: settledForeignAmount,
                rate: paymentRate));
            payment.JournalEntryId = result.JournalEntryId;
            await Db.SaveChangesAsync();

            return new FxScenario { ApInvoice = invoice, ApPayment = payment };
        }

        public async Task<VendorPayment> PostApPaymentAsync(
            decimal paymentRate,
            params (VendorInvoice Invoice, decimal AllocatedAmount)[] allocations)
        {
            var totalAmount = allocations.Sum(a => a.AllocatedAmount);
            var payment = new VendorPayment
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                PaymentNumber = $"VP-{Guid.NewGuid():N}"[..12],
                SupplierId = Guid.NewGuid(),
                PaymentDate = new DateTime(2026, 7, 10),
                TotalAmount = totalAmount,
                AllocatedAmount = totalAmount,
                CurrencyCode = "USD",
                ExchangeRate = paymentRate,
                Status = VendorPaymentStatus.Processed,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "Tests"
            };

            Db.Set<VendorPayment>().Add(payment);
            foreach (var (invoice, amount) in allocations)
            {
                Db.Set<VendorPaymentAllocation>().Add(new VendorPaymentAllocation
                {
                    Id = Guid.NewGuid(),
                    TenantId = TenantId,
                    VendorPaymentId = payment.Id,
                    VendorPayment = payment,
                    VendorInvoiceId = invoice.Id,
                    VendorInvoice = invoice,
                    AllocatedAmount = amount,
                    AllocationDate = payment.PaymentDate
                });
            }

            SeedExchangeRate(paymentRate, payment.PaymentDate);
            await Db.SaveChangesAsync();

            var result = await _postingEngine.PostAsync(CreateForeignPostingRequest(
                "AP",
                "VendorPayment",
                payment.Id,
                payment.PaymentNumber,
                payment.PaymentDate,
                debitAccountId: ApControl.Id,
                creditAccountId: BankGl.Id,
                foreignAmount: totalAmount,
                rate: paymentRate));
            payment.JournalEntryId = result.JournalEntryId;
            await Db.SaveChangesAsync();
            return payment;
        }

        public async Task<VendorPayment> PostApSettlementWithWithholdingAsync(
            decimal invoiceRate,
            decimal paymentRate,
            decimal cashForeignAmount,
            decimal withholdingForeignAmount)
        {
            var invoice = await PostOpenApInvoiceAsync(invoiceRate, cashForeignAmount + withholdingForeignAmount);
            var payment = new VendorPayment
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                PaymentNumber = $"VP-{Guid.NewGuid():N}"[..12],
                SupplierId = Guid.NewGuid(),
                PaymentDate = new DateTime(2026, 7, 10),
                TotalAmount = cashForeignAmount,
                AllocatedAmount = cashForeignAmount,
                WithholdingTaxAmount = withholdingForeignAmount,
                CurrencyCode = "USD",
                ExchangeRate = paymentRate,
                Status = VendorPaymentStatus.Processed,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "Tests"
            };
            var allocation = new VendorPaymentAllocation
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                VendorPaymentId = payment.Id,
                VendorPayment = payment,
                VendorInvoiceId = invoice.Id,
                VendorInvoice = invoice,
                AllocatedAmount = cashForeignAmount,
                WithholdingTaxAmount = withholdingForeignAmount,
                AllocationDate = payment.PaymentDate
            };
            Db.Set<VendorPayment>().Add(payment);
            Db.Set<VendorPaymentAllocation>().Add(allocation);
            SeedExchangeRate(paymentRate, payment.PaymentDate);
            await Db.SaveChangesAsync();

            var result = await _postingEngine.PostAsync(CreateForeignPostingRequest(
                "AP",
                "VendorPayment",
                payment.Id,
                payment.PaymentNumber,
                payment.PaymentDate,
                debitAccountId: ApControl.Id,
                creditAccountId: BankGl.Id,
                foreignAmount: cashForeignAmount + withholdingForeignAmount,
                rate: paymentRate,
                extraCreditAccountId: Expense.Id,
                extraCreditForeignAmount: withholdingForeignAmount,
                primaryCreditForeignAmount: cashForeignAmount));
            payment.JournalEntryId = result.JournalEntryId;
            await Db.SaveChangesAsync();
            return payment;
        }

        public async Task<VendorInvoice> PostOpenApInvoiceAsync(
            decimal rate,
            decimal foreignAmount,
            DateTime? postingDate = null)
        {
            var date = postingDate ?? new DateTime(2026, 7, 1);
            var invoice = new VendorInvoice
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                InvoiceNumber = $"VI-{Guid.NewGuid():N}"[..12],
                SupplierId = Guid.NewGuid(),
                InvoiceDate = date,
                TotalAmount = foreignAmount,
                CurrencyCode = "USD",
                ExchangeRate = rate,
                Status = VendorInvoiceStatus.Approved,
                ApprovalStatus = "Approved",
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "Tests"
            };
            Db.VendorInvoices.Add(invoice);
            SeedExchangeRate(rate, date);
            await Db.SaveChangesAsync();

            var result = await _postingEngine.PostAsync(CreateForeignPostingRequest(
                "AP",
                "VendorInvoice",
                invoice.Id,
                invoice.InvoiceNumber,
                date,
                debitAccountId: Expense.Id,
                creditAccountId: ApControl.Id,
                foreignAmount: foreignAmount,
                rate: rate));
            invoice.JournalEntryId = result.JournalEntryId;
            await Db.SaveChangesAsync();
            return invoice;
        }

        public async Task<FxScenario> PostArSettlementAsync(
            decimal invoiceRate,
            decimal receiptRate,
            decimal settledForeignAmount,
            decimal? invoiceForeignAmount = null)
        {
            var invoice = await PostOpenArInvoiceAsync(invoiceRate, invoiceForeignAmount ?? settledForeignAmount);
            var receipt = new CustomerPayment
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                PaymentNumber = $"CR-{Guid.NewGuid():N}"[..12],
                CustomerId = invoice.BusinessPartnerId,
                PaymentDate = new DateTime(2026, 7, 10),
                TotalAmount = settledForeignAmount,
                AllocatedAmount = settledForeignAmount,
                CurrencyCode = "USD",
                ExchangeRate = receiptRate,
                PaymentMethod = "BankTransfer",
                Status = "Posted",
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "Tests"
            };
            var allocation = new PaymentAllocation
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                CustomerPaymentId = receipt.Id,
                CustomerPayment = receipt,
                InvoiceId = invoice.Id,
                Invoice = invoice,
                AllocatedAmount = settledForeignAmount,
                AllocationDate = receipt.PaymentDate
            };
            Db.Set<CustomerPayment>().Add(receipt);
            Db.Set<PaymentAllocation>().Add(allocation);
            SeedExchangeRate(receiptRate, receipt.PaymentDate);
            await Db.SaveChangesAsync();

            var result = await _postingEngine.PostAsync(CreateForeignPostingRequest(
                "AR",
                "CustomerPayment",
                receipt.Id,
                receipt.PaymentNumber,
                receipt.PaymentDate,
                debitAccountId: BankGl.Id,
                creditAccountId: ArControl.Id,
                foreignAmount: settledForeignAmount,
                rate: receiptRate));
            receipt.JournalEntryId = result.JournalEntryId;
            await Db.SaveChangesAsync();

            return new FxScenario { ArInvoice = invoice, ArReceipt = receipt };
        }

        public async Task<CustomerPayment> PostArReceiptAsync(
            decimal receiptRate,
            params (Invoice Invoice, decimal AllocatedAmount)[] allocations)
        {
            var totalAmount = allocations.Sum(a => a.AllocatedAmount);
            var receipt = new CustomerPayment
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                PaymentNumber = $"CR-{Guid.NewGuid():N}"[..12],
                CustomerId = allocations.First().Invoice.BusinessPartnerId,
                PaymentDate = new DateTime(2026, 7, 10),
                TotalAmount = totalAmount,
                AllocatedAmount = totalAmount,
                CurrencyCode = "USD",
                ExchangeRate = receiptRate,
                PaymentMethod = "BankTransfer",
                Status = "Posted",
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "Tests"
            };

            Db.Set<CustomerPayment>().Add(receipt);
            foreach (var (invoice, amount) in allocations)
            {
                Db.Set<PaymentAllocation>().Add(new PaymentAllocation
                {
                    Id = Guid.NewGuid(),
                    TenantId = TenantId,
                    CustomerPaymentId = receipt.Id,
                    CustomerPayment = receipt,
                    InvoiceId = invoice.Id,
                    Invoice = invoice,
                    AllocatedAmount = amount,
                    AllocationDate = receipt.PaymentDate
                });
            }

            SeedExchangeRate(receiptRate, receipt.PaymentDate);
            await Db.SaveChangesAsync();

            var result = await _postingEngine.PostAsync(CreateForeignPostingRequest(
                "AR",
                "CustomerPayment",
                receipt.Id,
                receipt.PaymentNumber,
                receipt.PaymentDate,
                debitAccountId: BankGl.Id,
                creditAccountId: ArControl.Id,
                foreignAmount: totalAmount,
                rate: receiptRate));
            receipt.JournalEntryId = result.JournalEntryId;
            await Db.SaveChangesAsync();
            return receipt;
        }

        public async Task<CustomerPayment> PostArSettlementWithWithholdingAsync(
            decimal invoiceRate,
            decimal receiptRate,
            decimal cashForeignAmount,
            decimal withholdingForeignAmount)
        {
            var invoice = await PostOpenArInvoiceAsync(invoiceRate, cashForeignAmount + withholdingForeignAmount);
            var receipt = new CustomerPayment
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                PaymentNumber = $"CR-{Guid.NewGuid():N}"[..12],
                CustomerId = invoice.BusinessPartnerId,
                PaymentDate = new DateTime(2026, 7, 10),
                TotalAmount = cashForeignAmount,
                AllocatedAmount = cashForeignAmount + withholdingForeignAmount,
                WithholdingTaxAmount = withholdingForeignAmount,
                CurrencyCode = "USD",
                ExchangeRate = receiptRate,
                PaymentMethod = "BankTransfer",
                Status = "Posted",
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "Tests"
            };
            var allocation = new PaymentAllocation
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                CustomerPaymentId = receipt.Id,
                CustomerPayment = receipt,
                InvoiceId = invoice.Id,
                Invoice = invoice,
                AllocatedAmount = cashForeignAmount + withholdingForeignAmount,
                AllocationDate = receipt.PaymentDate
            };
            Db.Set<CustomerPayment>().Add(receipt);
            Db.Set<PaymentAllocation>().Add(allocation);
            SeedExchangeRate(receiptRate, receipt.PaymentDate);
            await Db.SaveChangesAsync();

            var result = await _postingEngine.PostAsync(CreateForeignPostingRequest(
                "AR",
                "CustomerPayment",
                receipt.Id,
                receipt.PaymentNumber,
                receipt.PaymentDate,
                debitAccountId: BankGl.Id,
                creditAccountId: ArControl.Id,
                foreignAmount: cashForeignAmount + withholdingForeignAmount,
                rate: receiptRate,
                extraDebitAccountId: Expense.Id,
                extraDebitForeignAmount: withholdingForeignAmount,
                primaryDebitForeignAmount: cashForeignAmount));
            receipt.JournalEntryId = result.JournalEntryId;
            await Db.SaveChangesAsync();
            return receipt;
        }

        public async Task<Invoice> PostOpenArInvoiceAsync(
            decimal rate,
            decimal foreignAmount,
            DateTime? postingDate = null)
        {
            var date = postingDate ?? new DateTime(2026, 7, 1);
            var invoice = new Invoice
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                InvoiceNumber = $"CI-{Guid.NewGuid():N}"[..12],
                BusinessPartnerId = Guid.NewGuid(),
                CustomerName = "FX Customer",
                InvoiceDate = date,
                SubTotal = foreignAmount,
                TotalAmount = foreignAmount,
                CurrencyCode = "USD",
                ExchangeRate = rate,
                Status = InvoiceStatus.Sent,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "Tests"
            };
            Db.Invoices.Add(invoice);
            SeedExchangeRate(rate, date);
            await Db.SaveChangesAsync();

            var result = await _postingEngine.PostAsync(CreateForeignPostingRequest(
                "AR",
                "CustomerInvoice",
                invoice.Id,
                invoice.InvoiceNumber,
                date,
                debitAccountId: ArControl.Id,
                creditAccountId: Revenue.Id,
                foreignAmount: foreignAmount,
                rate: rate));
            invoice.JournalEntryId = result.JournalEntryId;
            await Db.SaveChangesAsync();
            return invoice;
        }

        public async Task PostForeignBankBalanceAsync(decimal rate, decimal foreignAmount)
        {
            var date = new DateTime(2026, 7, 1);
            SeedExchangeRate(rate, date);
            await Db.SaveChangesAsync();

            await _postingEngine.PostAsync(CreateForeignPostingRequest(
                "CashBank",
                "BankTransaction",
                Guid.NewGuid(),
                "USD-BANK-BAL",
                date,
                debitAccountId: BankGl.Id,
                creditAccountId: Revenue.Id,
                foreignAmount: foreignAmount,
                rate: rate));
        }

        public async Task<Account> PostGeneralFxExposureAsync(
            string accountNumber,
            AccountType accountType,
            bool revaluationRequired,
            decimal rate,
            decimal foreignAmount)
        {
            var account = SeedAccount(Db, TenantId, accountNumber, accountType, true);
            var book = await Db.AccountingBooks.SingleAsync(item => item.TenantId == TenantId && item.Code == "IFRS");
            var classificationCode = accountType switch
            {
                AccountType.Asset => "ASSET_OTHER",
                AccountType.Liability => "LIABILITY_OTHER",
                AccountType.Equity => "EQUITY",
                AccountType.Revenue => "REVENUE",
                AccountType.Expense => "EXPENSE",
                _ => throw new ArgumentOutOfRangeException(nameof(accountType))
            };
            var classification = await Db.AccountClassifications.SingleAsync(item =>
                item.TenantId == TenantId && item.AccountingBookId == book.Id && item.Code == classificationCode);
            var mapping = new AccountAccountingBook
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                AccountId = account.Id,
                AccountingBookId = book.Id,
                AccountClassificationId = classification.Id,
                IsEnabled = true,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "Tests"
            };
            var link = new AccountCurrencyLink
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                AccountId = account.Id,
                LinkedCurrencyCode = "USD",
                IsActive = true,
                EffectiveDate = new DateTime(2026, 1, 1),
                RevaluationFrequency = RevaluationFrequency.Monthly,
                TransactionRateType = "Daily",
                RevaluationRateType = "Month-End",
                RevaluationQuoteSide = ExchangeRateQuoteSide.Mid,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "Tests"
            };
            Db.AccountAccountingBooks.Add(mapping);
            Db.AccountCurrencyLinks.Add(link);
            if (revaluationRequired)
            {
                Db.AccountBookCurrencyPolicies.Add(new AccountBookCurrencyPolicy
                {
                    Id = Guid.NewGuid(),
                    TenantId = TenantId,
                    AccountAccountingBookId = mapping.Id,
                    AccountCurrencyLinkId = link.Id,
                    RevaluationOverride = true,
                    OverrideReason = "Approved test policy",
                    LifecycleStatus = "Active",
                    RequestedByUserId = Guid.NewGuid(),
                    RequestedAtUtc = DateTime.UtcNow,
                    DecidedByUserId = Guid.NewGuid(),
                    DecidedAtUtc = DateTime.UtcNow,
                    DecisionReason = "Approved test policy",
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = "Tests"
                });
            }
            SeedExchangeRate(rate, new DateTime(2026, 7, 1));
            await Db.SaveChangesAsync();

            await _postingEngine.PostAsync(CreateForeignPostingRequest(
                "GL",
                "ManualJournal",
                Guid.NewGuid(),
                $"{accountNumber}-FX-BAL",
                new DateTime(2026, 7, 1),
                debitAccountId: accountType is AccountType.Asset or AccountType.Expense ? account.Id : Expense.Id,
                creditAccountId: accountType is AccountType.Liability or AccountType.Equity or AccountType.Revenue ? account.Id : Revenue.Id,
                foreignAmount: foreignAmount,
                rate: rate));
            return account;
        }

        private FinancePostingRequestDto CreateForeignPostingRequest(
            string sourceModule,
            string sourceDocumentType,
            Guid sourceDocumentId,
            string sourceReference,
            DateTime postingDate,
            Guid debitAccountId,
            Guid creditAccountId,
            decimal foreignAmount,
            decimal rate,
            Guid? extraDebitAccountId = null,
            decimal extraDebitForeignAmount = 0m,
            decimal? primaryDebitForeignAmount = null,
            Guid? extraCreditAccountId = null,
            decimal extraCreditForeignAmount = 0m,
            decimal? primaryCreditForeignAmount = null)
        {
            var functionalAmount = decimal.Round(foreignAmount * rate, 2, MidpointRounding.AwayFromZero);
            var debitForeignAmount = primaryDebitForeignAmount ?? foreignAmount;
            var creditForeignAmount = primaryCreditForeignAmount ?? foreignAmount;
            var debitFunctionalAmount = decimal.Round(debitForeignAmount * rate, 2, MidpointRounding.AwayFromZero);
            var creditFunctionalAmount = decimal.Round(creditForeignAmount * rate, 2, MidpointRounding.AwayFromZero);
            var lines = new List<FinancePostingLineDto>
            {
                new()
                {
                    AccountId = debitAccountId,
                    Description = $"{sourceReference} debit",
                    DebitAmount = debitFunctionalAmount,
                    TransactionCurrency = "USD",
                    ForeignCurrencyAmount = debitForeignAmount,
                    ExchangeRate = rate,
                    LineNumber = 1
                }
            };

            var lineNumber = 2;
            if (extraDebitAccountId.HasValue && extraDebitForeignAmount > 0m)
            {
                lines.Add(new FinancePostingLineDto
                {
                    AccountId = extraDebitAccountId.Value,
                    Description = $"{sourceReference} debit adjustment",
                    DebitAmount = decimal.Round(extraDebitForeignAmount * rate, 2, MidpointRounding.AwayFromZero),
                    TransactionCurrency = "USD",
                    ForeignCurrencyAmount = extraDebitForeignAmount,
                    ExchangeRate = rate,
                    LineNumber = lineNumber++
                });
            }

            lines.Add(new FinancePostingLineDto
            {
                AccountId = creditAccountId,
                Description = $"{sourceReference} credit",
                CreditAmount = creditFunctionalAmount,
                TransactionCurrency = "USD",
                ForeignCurrencyAmount = creditForeignAmount,
                ExchangeRate = rate,
                LineNumber = lineNumber++
            });

            if (extraCreditAccountId.HasValue && extraCreditForeignAmount > 0m)
            {
                lines.Add(new FinancePostingLineDto
                {
                    AccountId = extraCreditAccountId.Value,
                    Description = $"{sourceReference} credit adjustment",
                    CreditAmount = decimal.Round(extraCreditForeignAmount * rate, 2, MidpointRounding.AwayFromZero),
                    TransactionCurrency = "USD",
                    ForeignCurrencyAmount = extraCreditForeignAmount,
                    ExchangeRate = rate,
                    LineNumber = lineNumber
                });
            }

            lines.Sum(l => l.DebitAmount).Should().Be(functionalAmount);
            lines.Sum(l => l.CreditAmount).Should().Be(functionalAmount);

            return new FinancePostingRequestDto
            {
                SourceModule = sourceModule,
                SourceDocumentType = sourceDocumentType,
                SourceDocumentId = sourceDocumentId,
                SourceDocumentTenantId = TenantId,
                PostingAction = "Post",
                SourceDocumentReference = sourceReference,
                Description = $"{sourceModule} {sourceReference}",
                PostingDate = postingDate,
                JournalType = "System Generated",
                BookClassification = "IFRS",
                FunctionalCurrencyCode = "GHS",
                IdempotencyKey = $"{sourceModule}:{sourceDocumentType}:{sourceDocumentId:N}:Post",
                Lines = lines
            };
        }

        public Account SeedOtherTenantAccount(AccountType accountType)
        {
            var otherTenantId = Guid.NewGuid();
            SeedTenant(Db, otherTenantId, "GHS");
            return SeedAccount(Db, otherTenantId, $"X{Guid.NewGuid():N}"[..8], accountType);
        }

        public ExchangeRate SeedOtherTenantExchangeRate(decimal rate, DateTime effectiveDate, ExchangeRateType rateType)
        {
            var otherTenantId = Guid.NewGuid();
            SeedTenant(Db, otherTenantId, "GHS");
            var exchangeRate = new ExchangeRate
            {
                Id = Guid.NewGuid(),
                TenantId = otherTenantId,
                BaseCurrencyCode = "GHS",
                TargetCurrencyCode = "USD",
                Rate = rate,
                InverseRate = 1m / rate,
                EffectiveDate = effectiveDate.Date,
                RateType = rateType,
                RateSource = "Other Tenant",
                ApprovalStatus = RateApprovalStatus.Approved,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "Tests"
            };
            Db.ExchangeRates.Add(exchangeRate);
            return exchangeRate;
        }

        public void CloseAllPeriods()
        {
            foreach (var period in Db.FiscalPeriods.Where(p => p.TenantId == TenantId))
            {
                period.IsOpen = false;
                period.IsClosed = true;
                period.PeriodStatus = "Closed";
            }
        }
    }

    private sealed class ThrowAfterSuccessfulRevaluationPostingEngine(IFinancePostingEngine inner)
        : IFinancePostingEngine
    {
        private bool _hasThrown;

        public Task<FinancePostingResultDto> PostAsync(
            FinancePostingRequestDto request,
            CancellationToken cancellationToken = default) => inner.PostAsync(request, cancellationToken);

        public Task<FinancePostingResultDto> PostAsync(
            FinancePostingRequestDto request,
            FinancePostingProducerContext producerContext,
            CancellationToken cancellationToken = default) => inner.PostAsync(request, producerContext, cancellationToken);

        public async Task<FinancePostingResultDto> PostAsync(
            FinancePostingRequestV2Dto request,
            CancellationToken cancellationToken = default)
        {
            var result = await inner.PostAsync(request, cancellationToken);
            if (!_hasThrown && request.PostingAction == "UnrealizedRevaluation")
            {
                _hasThrown = true;
                throw new InvalidOperationException("simulated post-commit interruption");
            }

            return result;
        }

        public Task<FinancePostingResultDto> PostAsync(
            FinancePostingRequestV2Dto request,
            FinancePostingProducerContext producerContext,
            CancellationToken cancellationToken = default) => inner.PostAsync(request, producerContext, cancellationToken);

        public Task<FinanceReversalPlanDto> GetReversalPlanAsync(
            Guid postingEventId,
            string reason,
            DateTime? reversalDate = null,
            CancellationToken cancellationToken = default) =>
            inner.GetReversalPlanAsync(postingEventId, reason, reversalDate, cancellationToken);

        public Task<FinancePostingResultDto> ReverseAsync(
            Guid postingEventId,
            string reason,
            DateTime? reversalDate = null,
            CancellationToken cancellationToken = default) =>
            inner.ReverseAsync(postingEventId, reason, reversalDate, cancellationToken);
    }

    public enum AncillaryFaultPoint
    {
        AfterRateUsage,
        AfterPostedAudit,
        AfterRateAudit,
        AfterBankAudit
    }

    private sealed class ThrowAtAncillaryAuditBoundary(
        IFinanceAuditService inner,
        AncillaryFaultPoint faultPoint) : IFinanceAuditService
    {
        private bool _hasThrown;

        public async Task<AuditLog> RecordAsync(
            FinanceAuditEventDto auditEvent,
            CancellationToken cancellationToken = default)
        {
            var targetEvent = faultPoint switch
            {
                AncillaryFaultPoint.AfterRateUsage => FinanceAuditEvents.UnrealizedRevaluationPosted,
                AncillaryFaultPoint.AfterPostedAudit => FinanceAuditEvents.UnrealizedRevaluationPosted,
                AncillaryFaultPoint.AfterRateAudit => FinanceAuditEvents.ExchangeRateUsedForRevaluation,
                AncillaryFaultPoint.AfterBankAudit => FinanceAuditEvents.ForeignBankRevaluationPosted,
                _ => throw new ArgumentOutOfRangeException(nameof(faultPoint))
            };

            if (!_hasThrown
                && faultPoint == AncillaryFaultPoint.AfterRateUsage
                && auditEvent.EventType == targetEvent)
            {
                _hasThrown = true;
                throw new InvalidOperationException("simulated ancillary finalization interruption after rate usage");
            }

            var result = await inner.RecordAsync(auditEvent, cancellationToken);
            if (!_hasThrown && auditEvent.EventType == targetEvent)
            {
                _hasThrown = true;
                throw new InvalidOperationException("simulated ancillary finalization interruption after audit stage");
            }

            return result;
        }

        public Task<IReadOnlyList<AuditLog>> GetAuditTrailAsync(
            Guid tenantId,
            string resource,
            string resourceId,
            int limit = 100,
            CancellationToken cancellationToken = default) =>
            inner.GetAuditTrailAsync(tenantId, resource, resourceId, limit, cancellationToken);
    }

    private sealed class FxScenario
    {
        public VendorInvoice? ApInvoice { get; init; }
        public VendorPayment? ApPayment { get; init; }
        public Invoice? ArInvoice { get; init; }
        public CustomerPayment? ArReceipt { get; init; }
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"fx-batch-18-{Guid.NewGuid():N}")
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new ApplicationDbContext(options);
    }

    private static Mock<ICurrentUserService> CreateCurrentUser(Guid tenantId)
    {
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(x => x.TenantId).Returns(tenantId);
        currentUser.SetupGet(x => x.UserId).Returns(Guid.NewGuid().ToString());
        currentUser.SetupGet(x => x.UserName).Returns("fx.batch18");
        currentUser.SetupGet(x => x.Claims).Returns(new Dictionary<string, string>());
        currentUser.SetupGet(x => x.IpAddress).Returns("127.0.0.1");
        currentUser.SetupGet(x => x.UserAgent).Returns("fx-batch-18-tests");
        return currentUser;
    }

    private static void SeedTenant(ApplicationDbContext db, Guid tenantId, string baseCurrency)
    {
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = $"Tenant {tenantId:N}"[..20],
            Code = tenantId.ToString("N")[..6].ToUpperInvariant(),
            Status = TenantStatus.Active,
            BaseCurrency = baseCurrency,
            BaseCurrencyName = baseCurrency,
            CurrencySymbol = baseCurrency,
            CurrencyDecimalPlaces = 2,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "Tests"
        });
    }

    private static FiscalPeriod SeedOpenPeriod(ApplicationDbContext db, Guid tenantId, bool isOpen, bool isClosed)
    {
        var period = new FiscalPeriod
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            FiscalYearId = Guid.NewGuid(),
            PeriodName = "FY2026",
            PeriodCode = "2026",
            PeriodNumber = 1,
            PeriodType = PeriodType.Monthly,
            StartDate = new DateTime(2026, 1, 1),
            EndDate = new DateTime(2026, 12, 31),
            PeriodDays = 365,
            PeriodStatus = isClosed ? "Closed" : "Open",
            IsOpen = isOpen,
            IsClosed = isClosed,
            IsLocked = false
        };
        db.FiscalPeriods.Add(period);
        return period;
    }

    private static Account SeedAccount(ApplicationDbContext db, Guid tenantId, string accountNumber, AccountType accountType, bool isMultiCurrency = false)
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
            IsMultiCurrency = isMultiCurrency,
            AllowDirectPosting = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "Tests"
        };
        db.Accounts.Add(account);
        return account;
    }

    private static FinanceSettings SeedFinanceSettings(
        ApplicationDbContext db,
        Guid tenantId,
        Guid apControlId,
        Guid arControlId,
        Guid realizedGainId,
        Guid realizedLossId,
        Guid unrealizedGainId,
        Guid unrealizedLossId)
    {
        var settings = new FinanceSettings
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CoaType = "Segmented",
            BaseCurrency = "GHS",
            ControlAccountApId = apControlId,
            ControlAccountArId = arControlId,
            RealizedFxGainAccountId = realizedGainId,
            RealizedFxLossAccountId = realizedLossId,
            UnrealizedFxGainAccountId = unrealizedGainId,
            UnrealizedFxLossAccountId = unrealizedLossId,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "Tests"
        };
        db.FinanceSettings.Add(settings);
        return settings;
    }

    public sealed class CapturingFinanceAuditService : IFinanceAuditService
    {
        public List<FinanceAuditEventDto> Events { get; } = new();

        public Task<AuditLog> RecordAsync(FinanceAuditEventDto auditEvent, CancellationToken cancellationToken = default)
        {
            if (!string.IsNullOrWhiteSpace(auditEvent.IdempotencyKey))
            {
                var existing = Events.FirstOrDefault(item =>
                    string.Equals(item.IdempotencyKey, auditEvent.IdempotencyKey, StringComparison.Ordinal));
                if (existing != null)
                {
                    return Task.FromResult(new AuditLog
                    {
                        Id = Guid.NewGuid(),
                        TenantId = existing.TenantId,
                        Action = existing.EventType,
                        Resource = existing.Resource ?? "Finance",
                        ResourceId = existing.ResourceId,
                        IdempotencyKey = existing.IdempotencyKey,
                        Timestamp = DateTime.UtcNow
                    });
                }
            }

            Events.Add(auditEvent);
            return Task.FromResult(new AuditLog
            {
                Id = Guid.NewGuid(),
                TenantId = auditEvent.TenantId,
                Action = auditEvent.EventType,
                Resource = auditEvent.Resource ?? "Finance",
                ResourceId = auditEvent.ResourceId,
                IdempotencyKey = auditEvent.IdempotencyKey,
                Timestamp = DateTime.UtcNow
            });
        }

        public Task<IReadOnlyList<AuditLog>> GetAuditTrailAsync(
            Guid tenantId,
            string resource,
            string resourceId,
            int limit = 100,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<AuditLog>>(Array.Empty<AuditLog>());
        }
    }
}
