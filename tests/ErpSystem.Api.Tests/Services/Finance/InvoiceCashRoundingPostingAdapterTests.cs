using ErpSystem.Api.Services.Finance.GL;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Finance;
using ErpSystem.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Finance;

public sealed class InvoiceCashRoundingPostingAdapterTests
{
    public static TheoryData<string, int, decimal, decimal, decimal> CurrencyCases => new()
    {
        { "JPY", 0, 12m, 5m, -2m },
        { "GHS", 2, 10.03m, 0.05m, 0.02m },
        { "KWD", 3, 1.234m, 0.005m, 0.001m },
        { "TST", 4, 1.2346m, 0.0005m, -0.0001m }
    };

    [Theory]
    [MemberData(nameof(CurrencyCases))]
    public async Task Debit_anchor_posts_balanced_gain_or_loss_at_currency_precision(
        string currency, int places, decimal original, decimal increment, decimal delta)
    {
        await using var fixture = await Fixture.CreateAsync(currency, places, increment);
        var request = fixture.Request("AR", "CustomerInvoice", "AR-Control", original, debit: true);

        await fixture.ApplyAsync(request);

        var rounding = request.Lines.Single(x =>
            x.TransactionTag is InvoiceCashRoundingPostingAdapter.GainTag
                or InvoiceCashRoundingPostingAdapter.LossTag);
        var gain = delta > 0m;
        rounding.AccountId.Should().Be(gain ? fixture.Gain.Id : fixture.Loss.Id);
        rounding.TransactionTag.Should().Be(gain
            ? InvoiceCashRoundingPostingAdapter.GainTag
            : InvoiceCashRoundingPostingAdapter.LossTag);
        rounding.DebitAmount.Should().Be(delta < 0m ? decimal.Abs(delta) : 0m);
        rounding.CreditAmount.Should().Be(delta > 0m ? delta : 0m);
        rounding.Notes.Should().Contain($"Original={original}").And.Contain($"Delta={delta}")
            .And.Contain($"Increment={increment}").And.Contain($"Currency={currency}");
        request.Lines.Sum(x => x.DebitAmount).Should().Be(request.Lines.Sum(x => x.CreditAmount));
    }

    [Theory]
    [InlineData("AR", "CustomerPayment", "AR-Bank", true, 10.03, true)]
    [InlineData("AP", "VendorInvoice", "AP-Control", false, 10.03, false)]
    [InlineData("AP", "VendorPayment", "AP-Bank", false, 10.02, true)]
    [InlineData("CASHBANK", "CashReceipt", "CashBankReceipt.Bank", true, 10.02, false)]
    [InlineData("CASHBANK", "CashPayment", "CashBankPayment.Bank", false, 10.03, false)]
    public async Task Canonical_invoice_receipt_payment_and_cash_anchors_choose_account_by_delta_sign(
        string module, string documentType, string tag, bool debit, decimal amount, bool expectGain)
    {
        await using var fixture = await Fixture.CreateAsync("GHS", 2, 0.05m);
        var request = fixture.Request(module, documentType, tag, amount, debit);

        await fixture.ApplyAsync(request);

        var line = request.Lines.Single(x => x.TransactionTag is
            InvoiceCashRoundingPostingAdapter.GainTag or InvoiceCashRoundingPostingAdapter.LossTag);
        line.AccountId.Should().Be(expectGain ? fixture.Gain.Id : fixture.Loss.Id);
    }

    [Fact]
    public async Task Zero_delta_adds_no_line_and_replay_does_not_duplicate_rounding_evidence()
    {
        await using var fixture = await Fixture.CreateAsync("GHS", 2, 0.05m);
        var exact = fixture.Request("AR", "CustomerInvoice", "AR-Control", 10.05m, true);
        await fixture.ApplyAsync(exact);
        exact.Lines.Should().HaveCount(2);

        var rounded = fixture.Request("AR", "CustomerInvoice", "AR-Control", 10.03m, true);
        await fixture.ApplyAsync(rounded);
        await fixture.ApplyAsync(rounded);
        rounded.Lines.Count(x => x.TransactionTag is
            InvoiceCashRoundingPostingAdapter.GainTag or InvoiceCashRoundingPostingAdapter.LossTag)
            .Should().Be(1);
    }

    [Fact]
    public async Task Reversal_preserves_exact_historical_lines_without_reapplying_current_policy()
    {
        await using var fixture = await Fixture.CreateAsync("GHS", 2, 0.05m);
        var request = fixture.Request("AR", "CustomerInvoice", "AR-Control", 10.03m, true);
        request.PostingAction = "Reverse";
        request.ReversalOfJournalEntryId = Guid.NewGuid();
        var original = request.Lines.ToArray();

        await fixture.ApplyAsync(request);

        request.Lines.Should().Equal(original);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("inactive")]
    [InlineData("indirect")]
    [InlineData("control")]
    [InlineData("wrong-type")]
    public async Task Missing_or_invalid_configured_accounts_fail_closed(string defect)
    {
        await using var fixture = await Fixture.CreateAsync("GHS", 2, 0.05m);
        if (defect == "missing") fixture.Settings.InvoiceRoundingGainAccountId = Guid.NewGuid();
        if (defect == "inactive") fixture.Gain.Status = AccountStatus.Inactive;
        if (defect == "indirect") fixture.Gain.AllowDirectPosting = false;
        if (defect == "control") fixture.Gain.IsControlAccount = true;
        if (defect == "wrong-type") fixture.Gain.AccountType = AccountType.Asset;
        await fixture.Context.SaveChangesAsync();

        var action = () => fixture.ApplyAsync(
            fixture.Request("AR", "CustomerInvoice", "AR-Control", 10.03m, true));

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*gain account*active*Revenue*direct-posting*");
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private Fixture(string currency, int places)
        {
            CurrencyCode = currency;
            DecimalPlaces = places;
            Context = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options);
        }

        public Guid TenantId { get; } = Guid.NewGuid();
        public string CurrencyCode { get; }
        public int DecimalPlaces { get; }
        public ApplicationDbContext Context { get; }
        public FinanceSettings Settings { get; private set; } = null!;
        public Account Gain { get; private set; } = null!;
        public Account Loss { get; private set; } = null!;

        public static async Task<Fixture> CreateAsync(string currency, int places, decimal increment)
        {
            var fixture = new Fixture(currency, places);
            fixture.Gain = fixture.Account("ROUND-GAIN", AccountType.Revenue);
            fixture.Loss = fixture.Account("ROUND-LOSS", AccountType.Expense);
            fixture.Settings = new FinanceSettings
            {
                TenantId = fixture.TenantId,
                BaseCurrency = currency,
                InvoiceRoundingEnabled = true,
                InvoiceRoundingIncrement = increment,
                InvoiceRoundingMethod = GovernedRoundingMethod.Nearest,
                InvoiceRoundingGainAccountId = fixture.Gain.Id,
                InvoiceRoundingLossAccountId = fixture.Loss.Id
            };
            fixture.Context.AddRange(fixture.Settings, fixture.Gain, fixture.Loss, new Currency
            {
                TenantId = fixture.TenantId,
                CurrencyCode = currency,
                NumericCode = "999",
                CurrencyName = currency,
                DecimalPlaces = places,
                RoundingPrecision = CurrencyMinorUnitPolicy.MinorUnit(places),
                IsActive = true
            });
            await fixture.Context.SaveChangesAsync();
            return fixture;
        }

        public FinancePostingRequestV2Dto Request(string module, string documentType,
            string anchorTag, decimal amount, bool debit)
        {
            var anchor = new FinancePostingLineDto
            {
                AccountId = Guid.NewGuid(),
                DebitAmount = debit ? amount : 0m,
                CreditAmount = debit ? 0m : amount,
                TransactionCurrency = CurrencyCode,
                TransactionDebitAmount = debit ? amount : 0m,
                TransactionCreditAmount = debit ? 0m : amount,
                TransactionTag = anchorTag,
                LineNumber = 1
            };
            return new FinancePostingRequestV2Dto
            {
                SourceModule = module,
                SourceDocumentType = documentType,
                SourceDocumentId = Guid.NewGuid(),
                SourceDocumentReference = "ROUND-1",
                PostingAction = "Post",
                FunctionalCurrencyCode = CurrencyCode,
                Lines = new[]
                {
                    anchor,
                    new FinancePostingLineDto
                    {
                        AccountId = Guid.NewGuid(),
                        DebitAmount = debit ? 0m : amount,
                        CreditAmount = debit ? amount : 0m,
                        TransactionCurrency = CurrencyCode,
                        TransactionDebitAmount = debit ? 0m : amount,
                        TransactionCreditAmount = debit ? amount : 0m,
                        TransactionTag = "Offset",
                        LineNumber = 2
                    }
                }
            };
        }

        public Task ApplyAsync(FinancePostingRequestV2Dto request) =>
            InvoiceCashRoundingPostingAdapter.ApplyAsync(
                Context, TenantId, request, CurrencyCode, DecimalPlaces, CancellationToken.None);

        private Account Account(string code, AccountType type) => new()
        {
            TenantId = TenantId,
            AccountCode = code,
            AccountNumber = code,
            AccountName = code,
            CurrencyCode = CurrencyCode,
            AccountType = type,
            Status = AccountStatus.Active,
            AllowDirectPosting = true
        };

        public ValueTask DisposeAsync() => Context.DisposeAsync();
    }
}
