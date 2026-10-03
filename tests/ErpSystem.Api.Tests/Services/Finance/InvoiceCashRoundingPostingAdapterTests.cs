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
    [InlineData("AR", "CustomerPayment", "AR-Bank", true, 10.03, 0.02)]
    [InlineData("AR", "CustomerPayment", "AR-Bank", true, 10.02, -0.02)]
    [InlineData("AP", "VendorPayment", "AP-Bank", false, 10.03, 0.02)]
    [InlineData("AP", "VendorPayment", "AP-Bank", false, 10.02, -0.02)]
    public async Task Cash_tender_rounding_preserves_allocation_without_creating_false_advance(
        string module, string documentType, string tag, bool debit, decimal amount, decimal delta)
    {
        await using var fixture = await Fixture.CreateAsync("GHS", 2, 0.05m);
        var request = fixture.Request(module, documentType, tag, amount, debit);
        if (module == "AR")
            (await fixture.Context.Set<CustomerPayment>().SingleAsync(x => x.Id == request.SourceDocumentId))
                .AllocatedAmount = amount;
        else
            (await fixture.Context.Set<VendorPayment>().SingleAsync(x => x.Id == request.SourceDocumentId))
                .AllocatedAmount = amount;

        await fixture.ApplyAsync(request);

        if (module == "AR")
        {
            var source = await fixture.Context.Set<CustomerPayment>().SingleAsync(x => x.Id == request.SourceDocumentId);
            source.RoundingAdjustmentAmount.Should().Be(delta);
            source.UnallocatedAmount.Should().Be(0m);
            source.FinanceRoundingEvidenceId.Should().NotBeNull();
        }
        else
        {
            var source = await fixture.Context.Set<VendorPayment>().SingleAsync(x => x.Id == request.SourceDocumentId);
            source.RoundingAdjustmentAmount.Should().Be(delta);
            source.UnallocatedAmount.Should().Be(0m);
            source.FinanceRoundingEvidenceId.Should().NotBeNull();
        }
    }

    [Fact]
    public async Task Bank_receipt_is_not_cash_rounding_eligible()
    {
        await using var fixture = await Fixture.CreateAsync("GHS", 2, 0.05m);
        var request = fixture.Request("AR", "CustomerPayment", "AR-Bank", 10.03m, true);
        var source = await fixture.Context.Set<CustomerPayment>()
            .Include(x => x.ConfiguredPaymentMethod).SingleAsync(x => x.Id == request.SourceDocumentId);
        source.ConfiguredPaymentMethod!.Type = PaymentMethodType.BankTransfer;
        await fixture.Context.SaveChangesAsync();

        await fixture.ApplyAsync(request);

        request.FinanceRoundingEvidenceId.Should().BeNull();
        request.Lines.Should().NotContain(x =>
            x.TransactionTag == InvoiceCashRoundingPostingAdapter.GainTag
            || x.TransactionTag == InvoiceCashRoundingPostingAdapter.LossTag);
        source.TotalAmount.Should().Be(10.03m);
    }

    [Fact]
    public async Task Replay_reuses_frozen_decision_after_settings_change()
    {
        await using var fixture = await Fixture.CreateAsync("GHS", 2, 0.05m);
        var first = fixture.Request("AR", "CustomerInvoice", "AR-Control", 10.03m, true);
        await fixture.ApplyAsync(first);
        await fixture.Context.SaveChangesAsync();
        var evidenceId = first.FinanceRoundingEvidenceId;
        fixture.Settings.InvoiceRoundingIncrement = 1m;
        fixture.Settings.InvoiceRoundingMethod = GovernedRoundingMethod.Down;
        await fixture.Context.SaveChangesAsync();
        var replay = fixture.Replay(first, 10.03m, true, "AR-Control");

        await fixture.ApplyAsync(replay);

        replay.FinanceRoundingEvidenceId.Should().Be(evidenceId);
        replay.Lines.Single(x => x.TransactionTag is
            InvoiceCashRoundingPostingAdapter.GainTag or InvoiceCashRoundingPostingAdapter.LossTag)
            .TransactionCreditAmount.Should().Be(0.02m);
        (await fixture.Context.FinanceRoundingEvidence.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Foreign_invoice_freezes_fx_and_reconciles_source_base_total_to_gl_control()
    {
        await using var fixture = await Fixture.CreateAsync("USD", 2, 0.05m);
        fixture.Context.Add(new Currency { TenantId = fixture.TenantId, CurrencyCode = "GHS",
            NumericCode = "936", CurrencyName = "Ghana Cedi", DecimalPlaces = 2,
            RoundingPrecision = 0.01m, IsActive = true });
        var request = fixture.Request("AR", "CustomerInvoice", "AR-Control", 10.03m, true);
        var invoice = await fixture.Context.Invoices.SingleAsync(x => x.Id == request.SourceDocumentId);
        invoice.ExchangeRate = 12m;
        invoice.BaseCurrencyAmount = 120.36m;
        foreach (var line in request.Lines)
        {
            line.DebitAmount *= 12m;
            line.CreditAmount *= 12m;
            line.ExchangeRate = 12m;
            line.ExchangeRateSource = "TEST-FROZEN";
            line.ExchangeRateDate = new DateTime(2026, 10, 2);
        }
        await fixture.Context.SaveChangesAsync();

        await fixture.ApplyAsync(request, "GHS", 2);
        await fixture.Context.SaveChangesAsync();

        invoice.TotalAmount.Should().Be(10.05m);
        invoice.BaseCurrencyAmount.Should().Be(120.60m);
        invoice.RoundingAdjustmentAmount.Should().Be(0.02m);
        request.Lines.Single(x => x.TransactionTag == "AR-Control").DebitAmount.Should().Be(120.60m);
        var evidence = await fixture.Context.FinanceRoundingEvidence.SingleAsync();
        evidence.ExchangeRate.Should().Be(12m);
        evidence.FunctionalDeltaAmount.Should().Be(0.24m);
        evidence.ExchangeRateSource.Should().Be("TEST-FROZEN");
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
            var sourceId = Guid.NewGuid();
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
            AddSource(module, documentType, sourceId, amount);
            Context.SaveChanges();
            return new FinancePostingRequestV2Dto
            {
                SourceModule = module,
                SourceDocumentType = documentType,
                SourceDocumentId = sourceId,
                SourceDocumentReference = "ROUND-1",
                PostingAction = "Post",
                IdempotencyKey = $"{module}:{documentType}:{TenantId:N}:{sourceId:N}:Post",
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

        private void AddSource(string module, string documentType, Guid sourceId, decimal amount)
        {
            if (module == "AR" && documentType == "CustomerInvoice")
                Context.Add(new Invoice { Id = sourceId, TenantId = TenantId, InvoiceNumber = "ROUND-1",
                    TotalAmount = amount, BaseCurrencyAmount = amount, CurrencyCode = CurrencyCode,
                    ExchangeRate = 1m, BusinessPartnerId = Guid.NewGuid() });
            else if (module == "AP" && documentType == "VendorInvoice")
                Context.Add(new VendorInvoice { Id = sourceId, TenantId = TenantId, InvoiceNumber = "ROUND-1",
                    TotalAmount = amount, BaseCurrencyAmount = amount, CurrencyCode = CurrencyCode,
                    ExchangeRate = 1m, BusinessPartnerId = Guid.NewGuid() });
            else if (module == "AR" && documentType == "CustomerPayment")
            {
                var method = new ErpSystem.Core.Entities.Finance.PaymentMethod { Id = Guid.NewGuid(), TenantId = TenantId,
                    Name = "Cash", Type = PaymentMethodType.Cash };
                var till = new LiquidityAccount { Id = Guid.NewGuid(), TenantId = TenantId,
                    Code = "TILL", Name = "Till", AccountType = LiquidityAccountType.CashTill,
                    Currency = CurrencyCode, GLAccountId = Guid.NewGuid() };
                Context.AddRange(method, till, new CustomerPayment { Id = sourceId, TenantId = TenantId,
                    PaymentNumber = "ROUND-1", TotalAmount = amount, CurrencyCode = CurrencyCode,
                    ExchangeRate = 1m, BusinessPartnerId = Guid.NewGuid(), PaymentMethodId = method.Id,
                    LiquidityAccountId = till.Id });
            }
            else if (module == "AP" && documentType == "VendorPayment")
                Context.Add(new VendorPayment { Id = sourceId, TenantId = TenantId, PaymentNumber = "ROUND-1",
                    TotalAmount = amount, CurrencyCode = CurrencyCode, ExchangeRate = 1m,
                    BusinessPartnerId = Guid.NewGuid(), PaymentMethod = VendorPaymentMethod.Cash });
            else if (module == "CASHBANK")
            {
                var method = new ErpSystem.Core.Entities.Finance.PaymentMethod { Id = Guid.NewGuid(), TenantId = TenantId,
                    Name = "Cash", Type = PaymentMethodType.Cash };
                Context.AddRange(method, new CashTransaction { Id = sourceId, TenantId = TenantId,
                    TransactionNumber = "ROUND-1", TransactionType = documentType.Contains("Receipt")
                        ? CashTransactionType.Receipt : CashTransactionType.Payment,
                    Amount = amount, BaseAmount = amount, Currency = CurrencyCode,
                    BankAccountId = Guid.NewGuid(), PaymentMethodId = method.Id });
            }
        }

        public Task ApplyAsync(FinancePostingRequestV2Dto request) =>
            InvoiceCashRoundingPostingAdapter.ApplyAsync(
                Context, TenantId, request, CurrencyCode, DecimalPlaces, CancellationToken.None);

        public Task ApplyAsync(FinancePostingRequestV2Dto request,
            string functionalCurrency, int functionalPlaces) =>
            InvoiceCashRoundingPostingAdapter.ApplyAsync(
                Context, TenantId, request, functionalCurrency, functionalPlaces, CancellationToken.None);

        public FinancePostingRequestV2Dto Replay(FinancePostingRequestV2Dto original,
            decimal amount, bool debit, string anchorTag) => new()
        {
            SourceModule = original.SourceModule,
            SourceDocumentType = original.SourceDocumentType,
            SourceDocumentId = original.SourceDocumentId,
            SourceDocumentReference = original.SourceDocumentReference,
            PostingAction = original.PostingAction,
            IdempotencyKey = original.IdempotencyKey,
            FunctionalCurrencyCode = original.FunctionalCurrencyCode,
            Lines = new[]
            {
                new FinancePostingLineDto
                {
                    AccountId = Guid.NewGuid(), DebitAmount = debit ? amount : 0m,
                    CreditAmount = debit ? 0m : amount, TransactionCurrency = CurrencyCode,
                    TransactionDebitAmount = debit ? amount : 0m,
                    TransactionCreditAmount = debit ? 0m : amount,
                    TransactionTag = anchorTag, LineNumber = 1
                },
                new FinancePostingLineDto
                {
                    AccountId = Guid.NewGuid(), DebitAmount = debit ? 0m : amount,
                    CreditAmount = debit ? amount : 0m, TransactionCurrency = CurrencyCode,
                    TransactionDebitAmount = debit ? 0m : amount,
                    TransactionCreditAmount = debit ? amount : 0m,
                    TransactionTag = "Offset", LineNumber = 2
                }
            }
        };

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
