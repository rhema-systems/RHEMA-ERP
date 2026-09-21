using ErpSystem.Api.Services.Finance.Cash;
using ErpSystem.Api.Services.Finance;
using ErpSystem.Api.Services.Finance.GL;
using ErpSystem.Api.Services.Finance.Migration;
using ErpSystem.Api.Services.Finance.Taxation;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.DTOs.Workflow;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Finance.FixedAssets;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Entities.Sales;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
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

public sealed class ControlledOpeningBalancePostingTests
{
    [Fact]
    [Trait("Batch", "FinanceGoLive-OpeningBalances")]
    [Trait("Category", "CanonicalMasterData")]
    public async Task SpecializedOptions_ShouldUseCanonicalArCustomersAndActiveApSuppliers()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        db.Suppliers.Add(new Supplier
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            SupplierCode = "SUP-001",
            Name = "Opening Supplier",
            IsActive = true,
            Status = "Active"
        });
        db.BusinessPartners.Add(new BusinessPartner
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            PartnerCode = "CUS-001",
            CustomerAccountNumber = "AR-CUS-001",
            PartnerName = "Opening Customer",
            PartnerType = "Customer",
            IsActive = true
        });
        await db.SaveChangesAsync();

        var options = await CreateService(db, tenantId).GetSpecializedOptionsAsync();

        options.Suppliers.Should().ContainSingle(item =>
            item.Code == "SUP-001" && item.Name == "Opening Supplier");
        options.Customers.Should().ContainSingle(item =>
            item.Code == "AR-CUS-001" && item.Name == "Opening Customer");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-OpeningBalances")]
    [Trait("Category", "ProjectionContract")]
    public async Task ApprovalProjection_ShouldLabelFunctionalAmountWithFunctionalCurrency_NotFiscalPeriod()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var period = SeedPeriod(db, tenantId, isOpen: true, isClosed: false);
        period.PeriodCode = "2025-01";
        var batch = new OpeningBalanceBatch
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            BatchNumber = "FINDEMO-OB-001",
            OpeningDate = new DateTime(2025, 1, 1),
            FiscalPeriodId = period.Id,
            FiscalPeriod = period,
            BookClassification = "IFRS",
            Status = "PendingApproval",
            TotalDebit = 1_500m,
            TotalCredit = 1_500m
        };
        batch.Lines.Add(new OpeningBalanceLine
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            OpeningBalanceBatchId = batch.Id,
            AccountId = Guid.NewGuid(),
            LineNumber = 1,
            DebitAmount = 1_500m,
            TransactionDebitAmount = 100m,
            TransactionCurrencyCode = "USD",
            FunctionalCurrencyCode = "GHS"
        });
        db.OpeningBalanceBatches.Add(batch);
        await db.SaveChangesAsync();

        var controller = new ErpSystem.Api.Controllers.Finance.FinanceApprovalsController(
            db,
            Mock.Of<ICurrentUserService>(),
            Mock.Of<Microsoft.AspNetCore.Authorization.IAuthorizationService>(),
            Mock.Of<IWorkflowService>(),
            Mock.Of<ErpSystem.Core.Interfaces.Workflow.IWorkflowEntityDisplayService>(),
            Mock.Of<IJournalEntryService>(),
            Mock.Of<IInvoiceService>(),
            Mock.Of<ErpSystem.Core.Interfaces.Inventory.IInventoryValuationService>(),
            null!,
            Mock.Of<ILogger<ErpSystem.Api.Controllers.Finance.FinanceApprovalsController>>());
        var resolver = typeof(ErpSystem.Api.Controllers.Finance.FinanceApprovalsController)
            .GetMethod("ResolveFactsAsync", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;

        var projectionTask = (Task)resolver.Invoke(
            controller,
            new object[] { tenantId, "OpeningBalanceBatch", batch.Id, CancellationToken.None })!;
        await projectionTask;
        var facts = projectionTask.GetType().GetProperty("Result")!.GetValue(projectionTask)!;
        var currencyCode = facts.GetType().GetProperty("CurrencyCode")!.GetValue(facts);

        currencyCode.Should().Be("GHS");
        currencyCode.Should().NotBe(period.PeriodCode);
        currencyCode.Should().NotBe(batch.BookClassification);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-GovernedOpeningSources")]
    [Trait("Category", "CashBank")]
    public async Task GovernedBankOpening_ShouldDeriveAccountsPostOnceAndUpdateSnapshotAtomically()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = SeedOpeningBalanceFixture(db, tenantId);
        var bankGl = SeedAccount(db, tenantId, "1010", AccountType.Asset);
        bankGl.AllowDirectPosting = false;
        bankGl.IsControlAccount = true;
        var bank = new BankAccount
        {
            Id = Guid.NewGuid(), TenantId = tenantId, AccountNumber = "TDC-DEMO-GHS-001",
            AccountName = "Main Operating Bank", BankName = "TDC Test Bank", Currency = "GHS",
            GLAccountId = bankGl.Id, OpeningBalance = 0m, CurrentBalance = 0m, AvailableBalance = 0m,
            IsActive = true
        };
        db.BankAccounts.Add(bank);
        await db.SaveChangesAsync();
        var service = CreateService(db, tenantId, workflow: CreatePendingOpeningWorkflow());
        var request = new CreateBankAccountOpeningBalanceDto
        {
            BatchNumber = "FINDEMO-BANK-OB-001",
            SourceReference = "FINDEMO-BANK-SCHEDULE-2025",
            OpeningDate = new DateTime(2026, 1, 1),
            FiscalPeriodId = fixture.Period.Id,
            BookClassification = "IFRS",
            IdempotencyKey = $"bank-opening-{bank.Id:N}",
            BankAccountId = bank.Id,
            Amount = 750_000m
        };

        var created = await service.CreateBankAccountOpeningBatchAsync(request);
        var retriedCreate = await service.CreateBankAccountOpeningBatchAsync(request);

        created.SourceKind.Should().Be("BankAccountOpening");
        created.IsSystemGenerated.Should().BeTrue();
        created.IsEditable.Should().BeFalse();
        retriedCreate.Id.Should().Be(created.Id);
        request.IdempotencyKey = $"different-bank-opening-{bank.Id:N}";
        await FluentActions.Awaiting(() => service.CreateBankAccountOpeningBatchAsync(request))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already exists for this bank account*");
        (await db.OpeningBalanceBatches.CountAsync(item => item.TenantId == tenantId)).Should().Be(1);
        created.Lines.Should().ContainSingle(line =>
            line.CounterpartyType == "BankAccountOpening" && line.BankAccountId == bank.Id &&
            line.CounterpartyId == bank.Id && line.AccountId == bankGl.Id && line.DebitAmount == 750_000m);
        created.Lines.Should().ContainSingle(line =>
            line.CounterpartyType == "BankOpeningClearing" && line.AccountId == fixture.Equity.Id &&
            line.CreditAmount == 750_000m);
        await FluentActions.Awaiting(() => service.UpdateBatchAsync(created.Id, new UpdateOpeningBalanceBatchDto
        {
            OpeningDate = created.OpeningDate,
            FiscalPeriodId = created.FiscalPeriodId,
            BookClassification = created.BookClassification,
            Lines = created.Lines.Select(line => new CreateOpeningBalanceLineDto
            {
                AccountId = line.AccountId, DebitAmount = line.DebitAmount, CreditAmount = line.CreditAmount
            }).ToArray()
        })).Should().ThrowAsync<InvalidOperationException>().WithMessage("Generated opening batches cannot be edited manually*");

        (await service.SubmitForApprovalAsync(created.Id)).Status.Should().Be("PendingApproval");
        await ApproveBatchAsync(db, created.Id);
        var posted = await service.PostAsync(created.Id);
        var retriedPost = await service.PostAsync(created.Id);
        var reloadedBank = await db.BankAccounts.SingleAsync(item => item.Id == bank.Id);

        posted.Status.Should().Be("Posted");
        posted.JournalEntryId.Should().NotBeNull();
        posted.PostingEventId.Should().NotBeNull();
        retriedPost.JournalEntryId.Should().Be(posted.JournalEntryId);
        reloadedBank.OpeningBalance.Should().Be(0m);
        reloadedBank.CurrentBalance.Should().Be(750_000m);
        reloadedBank.AvailableBalance.Should().Be(750_000m);
        (await db.FinancePostingEvents.CountAsync(item => item.SourceDocumentId == created.Id)).Should().Be(1);
        (await db.AccountTransactions.Where(item => item.JournalEntryId == posted.JournalEntryId).SumAsync(item => item.DebitAmount))
            .Should().Be(750_000m);
        (await db.AccountTransactions.Where(item => item.JournalEntryId == posted.JournalEntryId).SumAsync(item => item.CreditAmount))
            .Should().Be(750_000m);
        request.BatchNumber = "FINDEMO-BANK-OB-POSTED-DUP";
        request.IdempotencyKey = $"posted-bank-opening-{bank.Id:N}";
        await FluentActions.Awaiting(() => service.CreateBankAccountOpeningBatchAsync(request))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already exists for this bank account*");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-GovernedOpeningSources")]
    [Trait("Category", "MultiCurrency")]
    public async Task ForeignCurrencyBankOpening_ShouldFreezeApprovedRateAndKeepNativeSnapshotSeparateFromFunctionalGl()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = SeedOpeningBalanceFixture(db, tenantId);
        var bankGl = SeedAccount(db, tenantId, "1015", AccountType.Asset);
        bankGl.AllowDirectPosting = false;
        bankGl.IsControlAccount = true;
        bankGl.CurrencyCode = "USD";
        var bank = new BankAccount
        {
            Id = Guid.NewGuid(), TenantId = tenantId, AccountNumber = "FINDEMO-USD-001",
            AccountName = "USD Operating Bank", BankName = "TDC Test Bank", Currency = "USD",
            GLAccountId = bankGl.Id, OpeningBalance = 0m, CurrentBalance = 0m, AvailableBalance = 0m,
            IsActive = true
        };
        // The governed quote stores functional GHS per one USD; 12.5 therefore converts
        // USD 50,000 to GHS 625,000, while the inverse remains USD 0.08 per GHS.
        var rate = new ExchangeRate
        {
            Id = Guid.NewGuid(), TenantId = tenantId,
            BaseCurrencyCode = "GHS", TargetCurrencyCode = "USD",
            Rate = 12.5m, InverseRate = 0.08m,
            EffectiveDate = new DateTime(2026, 1, 1),
            RateType = ExchangeRateType.Daily,
            QuoteSide = ExchangeRateQuoteSide.Mid,
            RateSource = "Bank of Ghana opening schedule",
            IsActive = true,
            ApprovalStatus = RateApprovalStatus.Approved,
            CreatedByUserId = Guid.NewGuid(),
            CreatedDate = DateTime.UtcNow
        };
        db.BankAccounts.Add(bank);
        db.ExchangeRates.Add(rate);
        await db.SaveChangesAsync();
        var service = CreateService(db, tenantId, workflow: CreatePendingOpeningWorkflow());

        var options = await service.GetGovernedOptionsAsync(new GovernedOpeningBalanceOptionsRequestDto
        {
            OpeningDate = new DateTime(2026, 1, 1),
            FiscalPeriodId = fixture.Period.Id,
            BookClassification = "IFRS"
        });
        var bankOption = options.BankAccounts.Single(option => option.Id == bank.Id);
        bankOption.IsEligible.Should().BeTrue();
        bankOption.ExchangeRateId.Should().Be(rate.Id);
        bankOption.ExchangeRate.Should().Be(12.5m);
        bankOption.CurrencyCode.Should().Be("USD");

        var request = new CreateBankAccountOpeningBalanceDto
        {
            BatchNumber = "FINDEMO-USD-BANK-OB-001",
            SourceReference = "FINDEMO-USD-BANK-SCHEDULE-2025",
            OpeningDate = new DateTime(2026, 1, 1),
            FiscalPeriodId = fixture.Period.Id,
            BookClassification = "IFRS",
            IdempotencyKey = $"usd-bank-opening-{bank.Id:N}",
            BankAccountId = bank.Id,
            Amount = 50_000m
        };
        await FluentActions.Awaiting(() => service.CreateBankAccountOpeningBatchAsync(request))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*require the approved exchange rate selected by the dated options contract*");

        request.ExchangeRateId = rate.Id;
        var created = await service.CreateBankAccountOpeningBatchAsync(request);
        var primary = created.Lines.Single(line => line.CounterpartyType == "BankAccountOpening");
        var clearing = created.Lines.Single(line => line.CounterpartyType == "BankOpeningClearing");
        primary.TransactionCurrencyCode.Should().Be("USD");
        primary.TransactionDebitAmount.Should().Be(50_000m);
        primary.DebitAmount.Should().Be(625_000m);
        primary.ExchangeRateId.Should().Be(rate.Id);
        primary.ExchangeRateDate.Should().Be(new DateTime(2026, 1, 1));
        clearing.TransactionCurrencyCode.Should().Be("GHS");
        clearing.TransactionCreditAmount.Should().Be(625_000m);
        clearing.CreditAmount.Should().Be(625_000m);
        clearing.ExchangeRateId.Should().BeNull();

        (await service.SubmitForApprovalAsync(created.Id)).Status.Should().Be("PendingApproval");
        await ApproveBatchAsync(db, created.Id);
        var posted = await service.PostAsync(created.Id);
        var reloadedBank = await db.BankAccounts.SingleAsync(item => item.Id == bank.Id);
        reloadedBank.CurrentBalance.Should().Be(50_000m);
        reloadedBank.AvailableBalance.Should().Be(50_000m);
        var bankPosting = await db.AccountTransactions.SingleAsync(item =>
            item.JournalEntryId == posted.JournalEntryId && item.AccountId == bankGl.Id);
        bankPosting.DebitAmount.Should().Be(625_000m);
        bankPosting.TransactionCurrency.Should().Be("USD");
        bankPosting.TransactionDebitAmount.Should().Be(50_000m);
        bankPosting.ExchangeRateId.Should().Be(rate.Id);

        var snapshotDiagnostic = await CreateMigrationSignOffService(db, tenantId)
            .DiagnoseBankSnapshotsAsync(new BankSnapshotRebuildRequestDto { BankAccountId = bank.Id });
        var bankDiagnostic = snapshotDiagnostic.Items.Single();
        bankDiagnostic.Currency.Should().Be("USD");
        bankDiagnostic.PostedGlBalance.Should().Be(50_000m);
        bankDiagnostic.Variance.Should().Be(0m);
        bankDiagnostic.Status.Should().Be("Current");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-GovernedOpeningSources")]
    [Trait("Category", "ApprovalControl")]
    public async Task RejectedGovernedBankOpening_ShouldPreserveHistoryAndChainCorrectedReplacements()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = SeedOpeningBalanceFixture(db, tenantId);
        var bankGl = SeedAccount(db, tenantId, "1011", AccountType.Asset);
        var bank = new BankAccount
        {
            Id = Guid.NewGuid(), TenantId = tenantId, AccountNumber = "REJECTED-BANK",
            AccountName = "Rejected replacement bank", BankName = "Bank", Currency = "GHS",
            GLAccountId = bankGl.Id, IsActive = true
        };
        db.BankAccounts.Add(bank);
        await db.SaveChangesAsync();
        var service = CreateService(db, tenantId, workflow: CreateCompletedOpeningWorkflow());
        const string clientKey = "governed-bank-rejected-chain";
        var firstRequest = new CreateBankAccountOpeningBalanceDto
        {
            BatchNumber = "BANK-REJECTED-001",
            SourceReference = "BANK-SCHEDULE-V1",
            OpeningDate = new DateTime(2026, 1, 1),
            FiscalPeriodId = fixture.Period.Id,
            BookClassification = "IFRS",
            IdempotencyKey = clientKey,
            BankAccountId = bank.Id,
            Amount = 100m
        };
        var first = await service.CreateBankAccountOpeningBatchAsync(firstRequest);
        var firstEntity = await db.OpeningBalanceBatches.SingleAsync(item => item.Id == first.Id);
        firstEntity.Status = "Rejected";
        await db.SaveChangesAsync();

        var duplicateNumberRequest = new CreateBankAccountOpeningBalanceDto
        {
            BatchNumber = first.BatchNumber,
            SourceReference = "BANK-SCHEDULE-CORRECTED",
            OpeningDate = first.OpeningDate,
            FiscalPeriodId = first.FiscalPeriodId,
            BookClassification = first.BookClassification,
            IdempotencyKey = clientKey,
            BankAccountId = bank.Id,
            Amount = 150m
        };
        await FluentActions.Awaiting(() => service.CreateBankAccountOpeningBatchAsync(duplicateNumberRequest))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*batch 'BANK-REJECTED-001' already exists*");

        var correctedRequest = new CreateBankAccountOpeningBalanceDto
        {
            BatchNumber = "BANK-REPLACEMENT-002",
            SourceReference = "BANK-SCHEDULE-CORRECTED",
            OpeningDate = first.OpeningDate,
            FiscalPeriodId = first.FiscalPeriodId,
            BookClassification = first.BookClassification,
            IdempotencyKey = clientKey,
            BankAccountId = bank.Id,
            Amount = 150m
        };
        var replacement = await service.CreateBankAccountOpeningBatchAsync(correctedRequest);
        var retriedThroughOriginal = await service.CreateBankAccountOpeningBatchAsync(correctedRequest);
        correctedRequest.IdempotencyKey = replacement.IdempotencyKey;
        var retriedThroughPhysical = await service.CreateBankAccountOpeningBatchAsync(correctedRequest);

        replacement.Id.Should().NotBe(first.Id);
        replacement.IdempotencyKey.Should().NotBe(clientKey);
        replacement.IdempotencyKey.Length.Should().BeLessThanOrEqualTo(120);
        retriedThroughOriginal.Id.Should().Be(replacement.Id);
        retriedThroughPhysical.Id.Should().Be(replacement.Id);
        firstEntity.Status.Should().Be("Rejected");
        firstEntity.IdempotencyKey.Should().Be(clientKey);
        firstEntity.JournalEntryId.Should().BeNull();
        firstEntity.PostingEventId.Should().BeNull();

        var activeDuplicate = new CreateBankAccountOpeningBalanceDto
        {
            BatchNumber = "BANK-ACTIVE-DUP-003",
            SourceReference = "BANK-ACTIVE-DUP",
            OpeningDate = first.OpeningDate,
            FiscalPeriodId = first.FiscalPeriodId,
            BookClassification = first.BookClassification,
            IdempotencyKey = "different-active-bank-key",
            BankAccountId = bank.Id,
            Amount = 200m
        };
        await FluentActions.Awaiting(() => service.CreateBankAccountOpeningBatchAsync(activeDuplicate))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already exists for this bank account*");

        var replacementEntity = await db.OpeningBalanceBatches.SingleAsync(item => item.Id == replacement.Id);
        replacementEntity.Status = "Rejected";
        await db.SaveChangesAsync();
        correctedRequest.BatchNumber = "BANK-REPLACEMENT-003";
        correctedRequest.IdempotencyKey = clientKey;
        correctedRequest.Amount = 175m;
        var secondReplacement = await service.CreateBankAccountOpeningBatchAsync(correctedRequest);
        var retriedSecondReplacement = await service.CreateBankAccountOpeningBatchAsync(correctedRequest);

        secondReplacement.Id.Should().NotBe(replacement.Id);
        secondReplacement.IdempotencyKey.Should().NotBe(replacement.IdempotencyKey);
        retriedSecondReplacement.Id.Should().Be(secondReplacement.Id);

        var evidence = SeedPostedGl(
            db,
            tenantId,
            fixture.Period.Id,
            bankGl.Id,
            fixture.Equity.Id,
            "MIGRATION",
            "OpeningBalanceBatch",
            secondReplacement.Id,
            "CORRUPT-REJECTED-EVIDENCE",
            175m);
        var secondEntity = await db.OpeningBalanceBatches.SingleAsync(item => item.Id == secondReplacement.Id);
        secondEntity.Status = "Rejected";
        secondEntity.JournalEntryId = evidence.Journal.Id;
        secondEntity.PostingEventId = evidence.PostingEvent.Id;
        secondEntity.PostedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        correctedRequest.BatchNumber = "BANK-REPLACEMENT-004";
        correctedRequest.Amount = 200m;

        await FluentActions.Awaiting(() => service.CreateBankAccountOpeningBatchAsync(correctedRequest))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*retains posting evidence and cannot be replaced*");
        (await db.OpeningBalanceBatches.CountAsync(item => item.TenantId == tenantId)).Should().Be(3);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-GovernedOpeningSources")]
    [Trait("Category", "ApprovalControl")]
    public async Task RejectedGovernedOpeningWithEventOnlyPostingEvidence_ShouldBlockReplacementAndLogicalDuplicate()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = SeedOpeningBalanceFixture(db, tenantId);
        var bankGl = SeedAccount(db, tenantId, "1012", AccountType.Asset);
        var bank = new BankAccount
        {
            Id = Guid.NewGuid(), TenantId = tenantId, AccountNumber = "EVENT-ONLY-BANK",
            AccountName = "Event-only evidence bank", BankName = "Bank", Currency = "GHS",
            GLAccountId = bankGl.Id, IsActive = true
        };
        db.BankAccounts.Add(bank);
        await db.SaveChangesAsync();
        var service = CreateService(db, tenantId, workflow: CreateCompletedOpeningWorkflow());
        const string clientKey = "governed-bank-event-only-evidence";
        var originalRequest = new CreateBankAccountOpeningBalanceDto
        {
            BatchNumber = "BANK-EVENT-EVIDENCE-001",
            SourceReference = "BANK-EVENT-EVIDENCE-V1",
            OpeningDate = new DateTime(2026, 1, 1),
            FiscalPeriodId = fixture.Period.Id,
            BookClassification = "IFRS",
            IdempotencyKey = clientKey,
            BankAccountId = bank.Id,
            Amount = 100m
        };
        var original = await service.CreateBankAccountOpeningBatchAsync(originalRequest);
        var originalEntity = await db.OpeningBalanceBatches.SingleAsync(item => item.Id == original.Id);
        originalEntity.Status = "Rejected";
        db.FinancePostingEvents.Add(new FinancePostingEvent
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            SourceModule = "MIGRATION",
            SourceDocumentType = "OpeningBalanceBatch",
            SourceDocumentId = original.Id,
            SourceDocumentReference = original.BatchNumber,
            PostingAction = "Post",
            PostingStatus = "Posted",
            PostingDate = original.OpeningDate,
            PostedAt = DateTime.UtcNow,
            FunctionalCurrencyCode = "GHS",
            TotalDebitAmount = original.TotalDebit,
            TotalCreditAmount = original.TotalCredit,
            BookClassification = original.BookClassification
        });
        await db.SaveChangesAsync();

        originalEntity.JournalEntryId.Should().BeNull();
        originalEntity.PostingEventId.Should().BeNull();
        originalEntity.PostedAt.Should().BeNull();

        var replacementThroughOriginalKey = new CreateBankAccountOpeningBalanceDto
        {
            BatchNumber = "BANK-EVENT-EVIDENCE-002",
            SourceReference = "BANK-EVENT-EVIDENCE-CORRECTED",
            OpeningDate = original.OpeningDate,
            FiscalPeriodId = original.FiscalPeriodId,
            BookClassification = original.BookClassification,
            IdempotencyKey = clientKey,
            BankAccountId = bank.Id,
            Amount = 125m
        };
        await FluentActions.Awaiting(() => service.CreateBankAccountOpeningBatchAsync(replacementThroughOriginalKey))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*retains posting evidence and cannot be replaced*");

        replacementThroughOriginalKey.BatchNumber = "BANK-EVENT-EVIDENCE-003";
        replacementThroughOriginalKey.IdempotencyKey = "distinct-event-only-bank-key";
        await FluentActions.Awaiting(() => service.CreateBankAccountOpeningBatchAsync(replacementThroughOriginalKey))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already exists for this bank account*");
        (await db.OpeningBalanceBatches.CountAsync(item => item.TenantId == tenantId)).Should().Be(1);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-GovernedOpeningSources")]
    [Trait("Category", "ApprovalControl")]
    public async Task RejectedGovernedResidualOpening_ShouldAllowCorrectedReplacementButActiveDuplicateBlocks()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = SeedOpeningBalanceFixture(db, tenantId);
        var accrued = SeedAccount(db, tenantId, "2110", AccountType.Liability);
        var shareCapital = SeedAccount(db, tenantId, "3010", AccountType.Equity);
        var retained = SeedAccount(db, tenantId, "3110", AccountType.Equity);
        retained.AllowDirectPosting = false;
        db.FinanceSettings.Local.Single(item => item.TenantId == tenantId).RetainedEarningsAccountId = retained.Id;
        SeedPostedGl(db, tenantId, fixture.Period.Id, fixture.Cash.Id, fixture.Equity.Id,
            "AP", "VendorInvoice", Guid.NewGuid(), "UPSTREAM-RESIDUAL-V1", 200m, new DateTime(2026, 1, 1));
        await db.SaveChangesAsync();
        var service = CreateService(db, tenantId, workflow: CreateCompletedOpeningWorkflow());
        const string clientKey = "governed-residual-rejected";
        var request = new CreateResidualGlEquityOpeningBalanceDto
        {
            BatchNumber = "RESIDUAL-REJECTED-001",
            SourceReference = "RESIDUAL-SCHEDULE-V1",
            OpeningDate = new DateTime(2026, 1, 1),
            FiscalPeriodId = fixture.Period.Id,
            BookClassification = "IFRS",
            IdempotencyKey = clientKey,
            AccruedExpensesAccountId = accrued.Id,
            AccruedExpensesAmount = 20m,
            ShareCapitalAccountId = shareCapital.Id,
            ShareCapitalAmount = 80m,
            RetainedEarningsAmount = 100m
        };
        var first = await service.CreateResidualGlEquityOpeningBatchAsync(request);
        var firstEntity = await db.OpeningBalanceBatches.SingleAsync(item => item.Id == first.Id);
        firstEntity.Status = "Rejected";
        SeedPostedGl(db, tenantId, fixture.Period.Id, fixture.Cash.Id, fixture.Equity.Id,
            "INVENTORY", "StockAdjustment", Guid.NewGuid(), "UPSTREAM-RESIDUAL-CORRECTION", 20m, new DateTime(2026, 1, 1));
        await db.SaveChangesAsync();

        request.BatchNumber = "RESIDUAL-REPLACEMENT-002";
        request.SourceReference = "RESIDUAL-SCHEDULE-CORRECTED";
        request.AccruedExpensesAmount = 25m;
        request.ShareCapitalAmount = 85m;
        request.RetainedEarningsAmount = 110m;
        var replacement = await service.CreateResidualGlEquityOpeningBatchAsync(request);
        var retriedOriginal = await service.CreateResidualGlEquityOpeningBatchAsync(request);
        request.IdempotencyKey = replacement.IdempotencyKey;
        var retriedPhysical = await service.CreateResidualGlEquityOpeningBatchAsync(request);

        replacement.Id.Should().NotBe(first.Id);
        replacement.IdempotencyKey.Should().NotBe(clientKey);
        retriedOriginal.Id.Should().Be(replacement.Id);
        retriedPhysical.Id.Should().Be(replacement.Id);
        firstEntity.Status.Should().Be("Rejected");
        firstEntity.IdempotencyKey.Should().Be(clientKey);

        request.BatchNumber = "RESIDUAL-ACTIVE-DUP-003";
        request.IdempotencyKey = "different-active-residual-key";
        await FluentActions.Awaiting(() => service.CreateResidualGlEquityOpeningBatchAsync(request))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already exists for this date and book*");
        (await db.OpeningBalanceBatches.CountAsync(item => item.TenantId == tenantId)).Should().Be(2);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-GovernedOpeningSources")]
    [Trait("Category", "ApprovalControl")]
    public async Task RejectedGovernedResidualOpeningWithEventOnlyPostingEvidence_ShouldBlockLogicalDuplicate()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = SeedOpeningBalanceFixture(db, tenantId);
        var accrued = SeedAccount(db, tenantId, "2111", AccountType.Liability);
        var shareCapital = SeedAccount(db, tenantId, "3011", AccountType.Equity);
        var retained = SeedAccount(db, tenantId, "3111", AccountType.Equity);
        retained.AllowDirectPosting = false;
        db.FinanceSettings.Local.Single(item => item.TenantId == tenantId).RetainedEarningsAccountId = retained.Id;
        SeedPostedGl(db, tenantId, fixture.Period.Id, fixture.Cash.Id, fixture.Equity.Id,
            "AP", "VendorInvoice", Guid.NewGuid(), "UPSTREAM-RESIDUAL-EVENT-ONLY", 200m, new DateTime(2026, 1, 1));
        await db.SaveChangesAsync();
        var service = CreateService(db, tenantId, workflow: CreateCompletedOpeningWorkflow());
        var request = new CreateResidualGlEquityOpeningBalanceDto
        {
            BatchNumber = "RESIDUAL-EVENT-EVIDENCE-001",
            SourceReference = "RESIDUAL-EVENT-EVIDENCE-V1",
            OpeningDate = new DateTime(2026, 1, 1),
            FiscalPeriodId = fixture.Period.Id,
            BookClassification = "IFRS",
            IdempotencyKey = "governed-residual-event-only-evidence",
            AccruedExpensesAccountId = accrued.Id,
            AccruedExpensesAmount = 20m,
            ShareCapitalAccountId = shareCapital.Id,
            ShareCapitalAmount = 80m,
            RetainedEarningsAmount = 100m
        };
        var original = await service.CreateResidualGlEquityOpeningBatchAsync(request);
        var originalEntity = await db.OpeningBalanceBatches.SingleAsync(item => item.Id == original.Id);
        originalEntity.Status = "Rejected";
        db.FinancePostingEvents.Add(new FinancePostingEvent
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            SourceModule = "MIGRATION",
            SourceDocumentType = "OpeningBalanceBatch",
            SourceDocumentId = original.Id,
            SourceDocumentReference = original.BatchNumber,
            PostingAction = "Post",
            PostingStatus = "Posted",
            PostingDate = original.OpeningDate,
            PostedAt = DateTime.UtcNow,
            FunctionalCurrencyCode = "GHS",
            TotalDebitAmount = original.TotalDebit,
            TotalCreditAmount = original.TotalCredit,
            BookClassification = original.BookClassification
        });
        await db.SaveChangesAsync();

        originalEntity.JournalEntryId.Should().BeNull();
        originalEntity.PostingEventId.Should().BeNull();
        originalEntity.PostedAt.Should().BeNull();
        request.BatchNumber = "RESIDUAL-EVENT-EVIDENCE-002";
        request.SourceReference = "RESIDUAL-EVENT-EVIDENCE-CORRECTED";
        request.IdempotencyKey = "distinct-residual-event-only-key";

        await FluentActions.Awaiting(() => service.CreateResidualGlEquityOpeningBatchAsync(request))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already exists for this date and book*");
        (await db.OpeningBalanceBatches.CountAsync(item => item.TenantId == tenantId)).Should().Be(1);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-GovernedOpeningSources")]
    [Trait("Category", "AuditIntegrity")]
    public async Task GovernedBankPosting_ShouldRemainDurablyPostedWhenSuccessAuditFails()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = SeedOpeningBalanceFixture(db, tenantId);
        var bankGl = SeedAccount(db, tenantId, "1012", AccountType.Asset);
        bankGl.AllowDirectPosting = false;
        bankGl.IsControlAccount = true;
        var bank = new BankAccount
        {
            Id = Guid.NewGuid(), TenantId = tenantId, AccountNumber = "AUDIT-FAIL-BANK",
            AccountName = "Audit failure bank", BankName = "Bank", Currency = "GHS",
            GLAccountId = bankGl.Id, IsActive = true
        };
        db.BankAccounts.Add(bank);
        await db.SaveChangesAsync();
        var audit = new Mock<IFinanceAuditService>();
        audit.Setup(service => service.RecordAsync(It.IsAny<FinanceAuditEventDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AuditLog());
        audit.Setup(service => service.RecordAsync(
                It.Is<FinanceAuditEventDto>(item => item.EventType == FinanceAuditEvents.OpeningBalancePosted),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("audit sink unavailable"));
        var logger = new Mock<ILogger<OpeningBalanceService>>();
        var service = CreateService(
            db,
            tenantId,
            workflow: CreatePendingOpeningWorkflow(),
            auditOverride: audit.Object,
            logger: logger.Object);
        var created = await service.CreateBankAccountOpeningBatchAsync(new CreateBankAccountOpeningBalanceDto
        {
            BatchNumber = "BANK-AUDIT-FAIL-001",
            SourceReference = "BANK-AUDIT-EVIDENCE",
            OpeningDate = new DateTime(2026, 1, 1),
            FiscalPeriodId = fixture.Period.Id,
            BookClassification = "IFRS",
            IdempotencyKey = "bank-audit-failure",
            BankAccountId = bank.Id,
            Amount = 325m
        });
        await service.SubmitForApprovalAsync(created.Id);
        await ApproveBatchAsync(db, created.Id);

        var posted = await service.PostAsync(created.Id);
        var retried = await service.PostAsync(created.Id);
        var persisted = await db.OpeningBalanceBatches.AsNoTracking().SingleAsync(item => item.Id == created.Id);
        var reloadedBank = await db.BankAccounts.AsNoTracking().SingleAsync(item => item.Id == bank.Id);

        posted.Status.Should().Be("Posted");
        retried.Status.Should().Be("Posted");
        retried.JournalEntryId.Should().Be(posted.JournalEntryId);
        persisted.Status.Should().Be("Posted");
        persisted.FailureReason.Should().BeNull();
        persisted.JournalEntryId.Should().NotBeNull();
        persisted.PostingEventId.Should().NotBeNull();
        reloadedBank.CurrentBalance.Should().Be(325m);
        reloadedBank.AvailableBalance.Should().Be(325m);
        (await db.JournalEntries.CountAsync(item => item.Id == persisted.JournalEntryId && item.PostingStatus == "Posted"))
            .Should().Be(1);
        (await db.FinancePostingEvents.CountAsync(item =>
            item.Id == persisted.PostingEventId && item.PostingStatus == "Posted" && item.SourceDocumentId == created.Id))
            .Should().Be(1);
        audit.Verify(service => service.RecordAsync(
            It.Is<FinanceAuditEventDto>(item => item.EventType == FinanceAuditEvents.OpeningBalancePosted),
            It.IsAny<CancellationToken>()), Times.Once);
        audit.Verify(service => service.RecordAsync(
            It.Is<FinanceAuditEventDto>(item => item.EventType == FinanceAuditEvents.OpeningBalancePostingFailed),
            It.IsAny<CancellationToken>()), Times.Never);
        logger.Verify(loggerInstance => loggerInstance.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((state, _) =>
                    state.ToString()!.Contains(created.Id.ToString(), StringComparison.OrdinalIgnoreCase) &&
                    state.ToString()!.Contains(persisted.PostingEventId!.Value.ToString(), StringComparison.OrdinalIgnoreCase)),
                It.Is<Exception>(exception => exception.Message == "audit sink unavailable"),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-GovernedOpeningSources")]
    [Trait("Category", "AccountingControl")]
    public async Task GovernedResidualOpening_ShouldDeriveClearingAndRetainedEarningsWithoutInventoryLine()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = SeedOpeningBalanceFixture(db, tenantId);
        var accrued = SeedAccount(db, tenantId, "2100", AccountType.Liability);
        var shareCapital = SeedAccount(db, tenantId, "3000-SHARE", AccountType.Equity);
        var retained = SeedAccount(db, tenantId, "3100", AccountType.Equity);
        retained.AllowDirectPosting = false;
        db.FinanceSettings.Local.Single(item => item.TenantId == tenantId).RetainedEarningsAccountId = retained.Id;
        var cutoverDate = new DateTime(2026, 1, 1);
        SeedPostedGl(db, tenantId, fixture.Period.Id, fixture.Cash.Id, fixture.Equity.Id,
            "AP", "VendorInvoice", Guid.NewGuid(), "AP-OPENING", 150_000m, cutoverDate);
        SeedPostedGl(db, tenantId, fixture.Period.Id, fixture.Cash.Id, fixture.Equity.Id,
            "AR", "Invoice", Guid.NewGuid(), "AR-OPENING", 70_000m, cutoverDate);
        SeedPostedGl(db, tenantId, fixture.Period.Id, fixture.Cash.Id, fixture.Equity.Id,
            "FA", "FixedAssetOpening", Guid.NewGuid(), "FA-OPENING", 180_000m, cutoverDate);
        SeedPostedGl(db, tenantId, fixture.Period.Id, fixture.Cash.Id, fixture.Equity.Id,
            "MIGRATION", "OpeningBalanceBatch", Guid.NewGuid(), "BANK-OPENING", 500_000m, cutoverDate);
        SeedPostedGl(db, tenantId, fixture.Period.Id, fixture.Cash.Id, fixture.Equity.Id,
            "INVENTORY", "StockAdjustment", Guid.NewGuid(), "INVENTORY-OPENING", 300_000m, cutoverDate);
        await db.SaveChangesAsync();
        var service = CreateService(db, tenantId, workflow: CreatePendingOpeningWorkflow());
        var request = new CreateResidualGlEquityOpeningBalanceDto
        {
            BatchNumber = "FINDEMO-GL-OB-2025",
            SourceReference = "FINDEMO-OPEN-GL-2025",
            OpeningDate = new DateTime(2026, 1, 1),
            FiscalPeriodId = fixture.Period.Id,
            BookClassification = "IFRS",
            AccruedExpensesAccountId = accrued.Id,
            AccruedExpensesAmount = 20_000m,
            ShareCapitalAccountId = shareCapital.Id,
            ShareCapitalAmount = 800_000m,
            RetainedEarningsAmount = 380_000m
        };

        var created = await service.CreateResidualGlEquityOpeningBatchAsync(request);
        var retriedCreate = await service.CreateResidualGlEquityOpeningBatchAsync(request);

        created.SourceKind.Should().Be("ResidualGlEquityOpening");
        created.IsSystemGenerated.Should().BeTrue();
        created.IsEditable.Should().BeFalse();
        retriedCreate.Id.Should().Be(created.Id);
        created.TotalDebit.Should().Be(1_200_000m);
        created.TotalCredit.Should().Be(1_200_000m);
        created.Lines.Should().HaveCount(4);
        created.Lines.Should().ContainSingle(line =>
            line.CounterpartyType == "ResidualMigrationClearing" && line.AccountId == fixture.Equity.Id &&
            line.DebitAmount == 1_200_000m && line.CounterpartyId == fixture.Equity.Id);
        created.Lines.Should().ContainSingle(line =>
            line.CounterpartyType == "ResidualRetainedEarnings" && line.AccountId == retained.Id &&
            line.CreditAmount == 380_000m && line.CounterpartyId == retained.Id);
        created.Lines.Select(line => line.AccountId).Should().BeEquivalentTo(new[]
        {
            fixture.Equity.Id, accrued.Id, shareCapital.Id, retained.Id
        });
        created.Lines.Should().NotContain(line =>
            (line.CounterpartyType ?? string.Empty).Contains("Inventory", StringComparison.OrdinalIgnoreCase) ||
            (line.Notes ?? string.Empty).Contains("Inventory", StringComparison.OrdinalIgnoreCase));

        (await service.SubmitForApprovalAsync(created.Id)).Status.Should().Be("PendingApproval");
        await ApproveBatchAsync(db, created.Id);
        var posted = await service.PostAsync(created.Id);
        var postedEntity = await db.OpeningBalanceBatches.SingleAsync(item => item.Id == created.Id);
        postedEntity.Status = "PostingFailed";
        await db.SaveChangesAsync();
        var recoveredRetry = await service.PostAsync(created.Id);

        posted.Status.Should().Be("Posted");
        recoveredRetry.Status.Should().Be("Posted");
        recoveredRetry.JournalEntryId.Should().Be(posted.JournalEntryId);
        (await db.AccountTransactions.SingleAsync(item =>
            item.JournalEntryId == posted.JournalEntryId && item.AccountId == retained.Id)).CreditAmount.Should().Be(380_000m);
        (await db.AccountTransactions.Where(item =>
                item.AccountId == fixture.Equity.Id &&
                item.TransactionDate < cutoverDate.AddDays(1) &&
                item.BookClassification == "IFRS" &&
                item.PostingStatus == "Posted")
            .SumAsync(item => item.CreditAmount - item.DebitAmount)).Should().Be(0m);
        (await db.FinancePostingEvents.CountAsync(item => item.SourceDocumentId == created.Id)).Should().Be(1);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-GovernedOpeningSources")]
    [Trait("Category", "AccountingControl")]
    public async Task GovernedResidualOpening_ShouldTreatPostedOriginalAndReversalAsNetZero()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = SeedOpeningBalanceFixture(db, tenantId);
        var accrued = SeedAccount(db, tenantId, "2120", AccountType.Liability);
        var shareCapital = SeedAccount(db, tenantId, "3020", AccountType.Equity);
        var retained = SeedAccount(db, tenantId, "3120", AccountType.Equity);
        retained.AllowDirectPosting = false;
        db.FinanceSettings.Local.Single(item => item.TenantId == tenantId).RetainedEarningsAccountId = retained.Id;
        var cutoverDate = new DateTime(2026, 1, 1);
        var original = SeedPostedGl(
            db, tenantId, fixture.Period.Id, fixture.Cash.Id, fixture.Equity.Id,
            "AP", "VendorInvoice", Guid.NewGuid(), "REVERSED-UPSTREAM", 100m, cutoverDate);
        db.AccountTransactions.Local.Single(item =>
            item.JournalEntryId == original.Journal.Id && item.AccountId == fixture.Equity.Id).IsReversed = true;
        SeedPostedGl(
            db, tenantId, fixture.Period.Id, fixture.Equity.Id, fixture.Cash.Id,
            "GL", "JournalReversal", Guid.NewGuid(), "REVERSED-UPSTREAM-R", 100m, cutoverDate);
        await db.SaveChangesAsync();
        var service = CreateService(db, tenantId, workflow: CreatePendingOpeningWorkflow());

        var action = () => service.CreateResidualGlEquityOpeningBatchAsync(new CreateResidualGlEquityOpeningBalanceDto
        {
            BatchNumber = "RESIDUAL-REVERSED-ZERO",
            SourceReference = "RESIDUAL-REVERSED-SCHEDULE",
            OpeningDate = cutoverDate,
            FiscalPeriodId = fixture.Period.Id,
            BookClassification = "IFRS",
            AccruedExpensesAccountId = accrued.Id,
            AccruedExpensesAmount = 20m,
            ShareCapitalAccountId = shareCapital.Id,
            ShareCapitalAmount = 30m,
            RetainedEarningsAmount = 50m
        });

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*blocked until upstream opening sources leave a positive posted Migration Clearing credit balance*");
        (await db.OpeningBalanceBatches.CountAsync(item => item.TenantId == tenantId)).Should().Be(0);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-GovernedOpeningSources")]
    [Trait("Category", "TenantIsolation")]
    public async Task GovernedBankOpening_ShouldRejectCrossTenantAndPreviouslyUsedBanksBeforePersistence()
    {
        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = SeedOpeningBalanceFixture(db, tenantId);
        SeedTenant(db, otherTenantId, "OTH");
        var otherGl = SeedAccount(db, otherTenantId, "OTH-BANK", AccountType.Asset);
        var otherBank = new BankAccount
        {
            Id = Guid.NewGuid(), TenantId = otherTenantId, AccountNumber = "OTHER-BANK", AccountName = "Other bank",
            BankName = "Other", Currency = "GHS", GLAccountId = otherGl.Id, IsActive = true
        };
        db.BankAccounts.Add(otherBank);
        await db.SaveChangesAsync();
        var service = CreateService(db, tenantId);

        var act = () => service.CreateBankAccountOpeningBatchAsync(new CreateBankAccountOpeningBalanceDto
        {
            SourceReference = "CROSS-TENANT-ATTEMPT",
            OpeningDate = new DateTime(2026, 1, 1),
            FiscalPeriodId = fixture.Period.Id,
            BankAccountId = otherBank.Id,
            Amount = 100m
        });

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*not found for the current tenant*");
        (await db.OpeningBalanceBatches.CountAsync(item => item.TenantId == tenantId)).Should().Be(0);

        var usedBankGl = SeedAccount(db, tenantId, "USED-BANK-GL", AccountType.Asset);
        var usedBank = new BankAccount
        {
            Id = Guid.NewGuid(), TenantId = tenantId, AccountNumber = "USED-BANK", AccountName = "Used bank",
            BankName = "Bank", Currency = "GHS", GLAccountId = usedBankGl.Id, IsActive = true,
            CurrentBalance = 25m, AvailableBalance = 25m
        };
        db.BankAccounts.Add(usedBank);
        await db.SaveChangesAsync();

        var usedAct = () => service.CreateBankAccountOpeningBatchAsync(new CreateBankAccountOpeningBalanceDto
        {
            SourceReference = "USED-BANK-ATTEMPT",
            OpeningDate = new DateTime(2026, 1, 1),
            FiscalPeriodId = fixture.Period.Id,
            BankAccountId = usedBank.Id,
            Amount = 100m
        });

        await usedAct.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*current and available balances must both be zero*");
        (await db.OpeningBalanceBatches.CountAsync(item => item.TenantId == tenantId)).Should().Be(0);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-GovernedOpeningSources")]
    [Trait("Category", "AccountingControl")]
    public async Task GovernedSources_ShouldFailClosedWhenMappingsDriftOrWorkflowIsUnavailable()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = SeedOpeningBalanceFixture(db, tenantId);
        var bankGl = SeedAccount(db, tenantId, "1015", AccountType.Asset);
        var replacementGl = SeedAccount(db, tenantId, "1016", AccountType.Asset);
        var bank = new BankAccount
        {
            Id = Guid.NewGuid(), TenantId = tenantId, AccountNumber = "DRIFT-BANK", AccountName = "Drift bank",
            BankName = "Bank", Currency = "GHS", GLAccountId = bankGl.Id, IsActive = true
        };
        db.BankAccounts.Add(bank);
        await db.SaveChangesAsync();
        var service = CreateService(db, tenantId, withoutWorkflow: true);
        var created = await service.CreateBankAccountOpeningBatchAsync(new CreateBankAccountOpeningBalanceDto
        {
            SourceReference = "BANK-MAPPING-SNAPSHOT",
            OpeningDate = new DateTime(2026, 1, 1),
            FiscalPeriodId = fixture.Period.Id,
            BankAccountId = bank.Id,
            Amount = 500m
        });

        bank.GLAccountId = replacementGl.Id;
        await db.SaveChangesAsync();
        var validation = await service.ValidateBatchAsync(created.Id);

        validation.IsValid.Should().BeFalse();
        validation.Errors.Should().Contain(error => error.Contains("GL mapping changed", StringComparison.OrdinalIgnoreCase));
        await FluentActions.Awaiting(() => service.SubmitForApprovalAsync(created.Id))
            .Should().ThrowAsync<InvalidOperationException>();
        (await db.FinancePostingEvents.CountAsync(item => item.SourceDocumentId == created.Id)).Should().Be(0);

        bank.GLAccountId = bankGl.Id;
        await db.SaveChangesAsync();
        await FluentActions.Awaiting(() => service.SubmitForApprovalAsync(created.Id))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Controlled opening balances require the Finance approval workflow*");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-GovernedOpeningSources")]
    [Trait("Category", "LookupContainment")]
    public async Task GovernedOptions_ShouldReturnOnlyTenantSafeResidualAccountsAndBankBlockers()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = SeedOpeningBalanceFixture(db, tenantId);
        var safeAccrued = SeedAccount(db, tenantId, "2190", AccountType.Liability);
        var futureAccrued = SeedAccount(db, tenantId, "2191", AccountType.Liability);
        futureAccrued.EffectiveDate = new DateTime(2026, 6, 1);
        var safeShare = SeedAccount(db, tenantId, "3090", AccountType.Equity);
        var expiredShare = SeedAccount(db, tenantId, "3091", AccountType.Equity);
        expiredShare.ExpirationDate = new DateTime(2025, 12, 31);
        var retained = SeedAccount(db, tenantId, "3190", AccountType.Equity);
        retained.AllowDirectPosting = false;
        var protectedAp = SeedAccount(db, tenantId, "2201", AccountType.Liability);
        // Keep it superficially selectable; the server must exclude it because Finance Settings
        // owns the AP mapping, not merely because of chart flags.
        protectedAp.IsControlAccount = false;
        protectedAp.AllowDirectPosting = true;
        var bankGl = SeedAccount(db, tenantId, "1090", AccountType.Asset);
        var blockedBankGl = SeedAccount(db, tenantId, "1091", AccountType.Asset);
        var futureBankGl = SeedAccount(db, tenantId, "1092", AccountType.Asset);
        futureBankGl.EffectiveDate = new DateTime(2026, 6, 1);
        var eligibleBank = new BankAccount
        {
            Id = Guid.NewGuid(), TenantId = tenantId, AccountNumber = "ELIGIBLE", AccountName = "Eligible bank",
            BankName = "Bank", Currency = "GHS", GLAccountId = bankGl.Id, IsActive = true
        };
        var blockedBank = new BankAccount
        {
            Id = Guid.NewGuid(), TenantId = tenantId, AccountNumber = "BLOCKED", AccountName = "Blocked bank",
            BankName = "Bank", Currency = "GHS", GLAccountId = blockedBankGl.Id, IsActive = true, CurrentBalance = 5m
        };
        var futureDatedBank = new BankAccount
        {
            Id = Guid.NewGuid(), TenantId = tenantId, AccountNumber = "FUTURE", AccountName = "Future-dated bank",
            BankName = "Bank", Currency = "GHS", GLAccountId = futureBankGl.Id, IsActive = true
        };
        db.BankAccounts.AddRange(eligibleBank, blockedBank, futureDatedBank);
        var settings = db.FinanceSettings.Local.Single(item => item.TenantId == tenantId);
        settings.RetainedEarningsAccountId = retained.Id;
        settings.ControlAccountApId = protectedAp.Id;
        await db.SaveChangesAsync();
        var service = CreateService(db, tenantId);

        var options = await service.GetGovernedOptionsAsync(new GovernedOpeningBalanceOptionsRequestDto
        {
            OpeningDate = new DateTime(2026, 1, 1),
            FiscalPeriodId = fixture.Period.Id,
            BookClassification = "IFRS"
        });

        options.FunctionalCurrencyCode.Should().Be("GHS");
        options.AccruedExpensesAccounts.Should().ContainSingle(item => item.Id == safeAccrued.Id);
        options.AccruedExpensesAccounts.Should().NotContain(item => item.Id == futureAccrued.Id);
        options.AccruedExpensesAccounts.Should().NotContain(item => item.Id == protectedAp.Id);
        options.ShareCapitalAccounts.Should().ContainSingle(item => item.Id == safeShare.Id);
        options.ShareCapitalAccounts.Should().NotContain(item => item.Id == expiredShare.Id);
        options.ShareCapitalAccounts.Should().NotContain(item => item.Id == retained.Id || item.Id == fixture.Equity.Id);
        options.MigrationClearingAccount!.AccountId.Should().Be(fixture.Equity.Id);
        options.MigrationClearingAccount.PostingDirection.Should().Be("Debit");
        options.RetainedEarningsAccount!.AccountId.Should().Be(retained.Id);
        options.RetainedEarningsAccount.PostingDirection.Should().Be("Credit");
        options.BankAccounts.Single(item => item.Id == eligibleBank.Id).IsEligible.Should().BeTrue();
        options.BankAccounts.Single(item => item.Id == eligibleBank.Id).Blockers.Should().BeEmpty();
        options.BankAccounts.Single(item => item.Id == blockedBank.Id).IsEligible.Should().BeFalse();
        options.BankAccounts.Single(item => item.Id == blockedBank.Id).Blockers.Should().NotBeEmpty();
        options.BankAccounts.Single(item => item.Id == futureDatedBank.Id).IsEligible.Should().BeFalse();
        options.BankAccounts.Single(item => item.Id == futureDatedBank.Id).Blockers.Should()
            .Contain(error => error.Contains("2026-01-01", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-GovernedOpeningSources")]
    [Trait("Category", "LookupContainment")]
    public async Task GovernedOptions_ShouldFailClosedForInvalidFiscalContext()
    {
        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = SeedOpeningBalanceFixture(db, tenantId);
        var otherFixture = SeedOpeningBalanceFixture(db, otherTenantId);
        await db.SaveChangesAsync();
        var service = CreateService(db, tenantId);
        var request = new GovernedOpeningBalanceOptionsRequestDto
        {
            OpeningDate = new DateTime(2026, 1, 1),
            FiscalPeriodId = otherFixture.Period.Id,
            BookClassification = "IFRS"
        };

        await FluentActions.Awaiting(() => service.GetGovernedOptionsAsync(request))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not found for the current tenant*");

        request.FiscalPeriodId = fixture.Period.Id;
        request.OpeningDate = new DateTime(2026, 2, 1);
        await FluentActions.Awaiting(() => service.GetGovernedOptionsAsync(request))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*within the selected fiscal period*");

        request.OpeningDate = new DateTime(2026, 1, 1);
        fixture.Period.IsLocked = true;
        await db.SaveChangesAsync();
        await FluentActions.Awaiting(() => service.GetGovernedOptionsAsync(request))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*open and unlocked fiscal period*");
    }

    [Theory]
    [InlineData("BankAccountOpening")]
    [InlineData("BankOpeningClearing")]
    [InlineData("ResidualAccruedOpening")]
    [InlineData("ResidualShareCapitalOpening")]
    [InlineData("ResidualRetainedEarnings")]
    [InlineData("ResidualMigrationClearing")]
    [Trait("Batch", "FinanceGoLive-GovernedOpeningSources")]
    [Trait("Category", "Security")]
    public async Task FreeFormOpening_ShouldRejectGovernedSourceMarkersBeforePersistence(string forgedMarker)
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = SeedOpeningBalanceFixture(db, tenantId);
        await db.SaveChangesAsync();
        var service = CreateService(db, tenantId);
        var request = CreateBalancedBatch(fixture.Period.Id, fixture.Cash.Id, fixture.Equity.Id);
        request.Lines.First().CounterpartyType = forgedMarker;
        request.Lines.First().CounterpartyId = Guid.NewGuid();

        await FluentActions.Awaiting(() => service.CreateBatchAsync(request))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*reserved server-derived opening source*");
        (await db.OpeningBalanceBatches.CountAsync()).Should().Be(0);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-SpecializedOpeningBalances")]
    [Trait("Requirement", "FIN-LIM-0048")]
    public async Task SupplierAndCustomerAdvanceOpenings_ShouldPostAndRemainVisibleAsUnappliedAdvances()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = SeedOpeningBalanceFixture(db, tenantId);
        await db.SaveChangesAsync();
        var service = CreateService(db, tenantId);

        var supplierBatch = await service.CreateSupplierAdvanceBatchAsync(new CreateSupplierAdvanceOpeningBalanceDto
        {
            SupplierId = fixture.Supplier.Id,
            SourceReference = "TDC-AP-ADV-001",
            OpeningDate = new DateTime(2026, 1, 1),
            FiscalPeriodId = fixture.Period.Id,
            CurrencyCode = "GHS",
            Amount = 1_500m
        });
        var customerBatch = await service.CreateCustomerAdvanceBatchAsync(new CreateCustomerAdvanceOpeningBalanceDto
        {
            CustomerId = fixture.Customer.Id,
            SourceReference = "TDC-AR-ADV-001",
            OpeningDate = new DateTime(2026, 1, 1),
            FiscalPeriodId = fixture.Period.Id,
            CurrencyCode = "GHS",
            Amount = 900m
        });

        await service.SubmitForApprovalAsync(supplierBatch.Id);
        await ApproveBatchAsync(db, supplierBatch.Id);
        await service.PostAsync(supplierBatch.Id);
        await service.SubmitForApprovalAsync(customerBatch.Id);
        await ApproveBatchAsync(db, customerBatch.Id);
        await service.PostAsync(customerBatch.Id);

        var supplierAdvance = await db.Set<VendorPayment>().SingleAsync(payment => payment.OpeningBalanceBatchId == supplierBatch.Id);
        supplierAdvance.IsSupplierAdvance.Should().BeTrue();
        supplierAdvance.TotalAmount.Should().Be(1_500m);
        supplierAdvance.Status.Should().Be(VendorPaymentStatus.Cleared);
        supplierAdvance.JournalEntryId.Should().NotBeNull();
        var customerAdvance = await db.Set<CustomerPayment>().SingleAsync(payment => payment.OpeningBalanceBatchId == customerBatch.Id);
        customerAdvance.IsCustomerAdvance.Should().BeTrue();
        customerAdvance.TotalAmount.Should().Be(900m);
        customerAdvance.Status.Should().Be("Cleared");
        customerAdvance.JournalEntryId.Should().NotBeNull();

        // A single opening-batch posting event is the immutable GL source. The rebuild deliberately
        // follows the source back-link so the canonical advance lot is still available for matching.
        var currentUser = CreateCurrentUser(tenantId);
        var readModel = new SubledgerSettlementReadModelService(
            db, currentUser.Object, Mock.Of<ILogger<SubledgerSettlementReadModelService>>());
        await readModel.RebuildAsync(new SubledgerSettlementRebuildRequestDto
        {
            SourceModule = "Both",
            AsOfDate = new DateTime(2026, 1, 31)
        });

        (await db.SubledgerUnappliedSettlementBalances.SingleAsync(item => item.SettlementSourceId == supplierAdvance.Id))
            .Classification.Should().Be(SubledgerUnappliedSettlementClassifications.SupplierAdvance);
        (await db.SubledgerUnappliedSettlementBalances.SingleAsync(item => item.SettlementSourceId == customerAdvance.Id))
            .Classification.Should().Be(SubledgerUnappliedSettlementClassifications.CustomerAdvance);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-SpecializedOpeningBalances")]
    [Trait("Requirement", "FIN-LIM-0048")]
    public async Task ForeignSupplierAdvanceOpening_ShouldPreserveNativeAndFunctionalEvidence()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = SeedOpeningBalanceFixture(db, tenantId);
        var rate = new ExchangeRate
        {
            Id = Guid.NewGuid(), TenantId = tenantId, BaseCurrencyCode = "GHS", TargetCurrencyCode = "USD",
            Rate = 15m, InverseRate = 0.066667m, EffectiveDate = new DateTime(2026, 1, 1),
            RateType = ExchangeRateType.Daily, RateSource = "TDC cutover evidence", IsActive = true,
            ApprovalStatus = RateApprovalStatus.Approved, CreatedByUserId = Guid.NewGuid(), CreatedDate = DateTime.UtcNow
        };
        db.ExchangeRates.Add(rate);
        fixture.SupplierAdvance.IsMultiCurrency = true;
        db.AccountCurrencyLinks.Add(new AccountCurrencyLink
        {
            Id = Guid.NewGuid(), TenantId = tenantId, AccountId = fixture.SupplierAdvance.Id,
            LinkedCurrencyCode = "USD", IsActive = true, EffectiveDate = new DateTime(2026, 1, 1),
            TransactionRateType = "Daily", RevaluationRateType = "Month-End"
        });
        await db.SaveChangesAsync();
        var service = CreateService(db, tenantId);

        var batch = await service.CreateSupplierAdvanceBatchAsync(new CreateSupplierAdvanceOpeningBalanceDto
        {
            SupplierId = fixture.Supplier.Id,
            SourceReference = "TDC-USD-ADV-001",
            OpeningDate = new DateTime(2026, 1, 1),
            FiscalPeriodId = fixture.Period.Id,
            CurrencyCode = "USD",
            Amount = 100m,
            ExchangeRateId = rate.Id,
            ExchangeRate = 15m
        });

        // Native USD is the future allocation quantity; GHS is the immutable GL value. Keeping
        // both prevents later settlement code from treating GHS 1,500 as USD 1,500.
        var sourceLine = await db.OpeningBalanceLines.SingleAsync(line =>
            line.OpeningBalanceBatchId == batch.Id && line.CounterpartyType == "SupplierAdvanceOpening");
        sourceLine.TransactionCurrencyCode.Should().Be("USD");
        sourceLine.FunctionalCurrencyCode.Should().Be("GHS");
        sourceLine.TransactionDebitAmount.Should().Be(100m);
        sourceLine.DebitAmount.Should().Be(1_500m);
        sourceLine.ExchangeRateId.Should().Be(rate.Id);

        var payment = await db.Set<VendorPayment>().SingleAsync(item => item.OpeningBalanceBatchId == batch.Id);
        payment.TotalAmount.Should().Be(100m);
        payment.ExchangeRate.Should().Be(15m);
        payment.ExchangeRateId.Should().Be(rate.Id);

        await service.SubmitForApprovalAsync(batch.Id);
        await ApproveBatchAsync(db, batch.Id);
        var posted = await service.PostAsync(batch.Id);
        posted.Status.Should().Be("Posted");
        var postedLine = await db.AccountTransactions.SingleAsync(transaction =>
            transaction.JournalEntryId == posted.JournalEntryId && transaction.AccountId == fixture.SupplierAdvance.Id);
        postedLine.TransactionCurrency.Should().Be("USD");
        postedLine.TransactionDebitAmount.Should().Be(100m);
        postedLine.DebitAmount.Should().Be(1_500m);
        postedLine.ExchangeRateId.Should().Be(rate.Id);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-SpecializedOpeningBalances")]
    [Trait("Requirement", "FIN-LIM-0048")]
    public async Task WithholdingOpenings_ShouldFeedExistingCertificateAndStatutoryEvidenceWorkflows()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = SeedOpeningBalanceFixture(db, tenantId);
        await db.SaveChangesAsync();
        var service = CreateService(db, tenantId);

        var apBatch = await service.CreateApWithholdingBatchAsync(new CreateApWithholdingOpeningBalanceDto
        {
            SupplierId = fixture.Supplier.Id,
            TaxId = fixture.WithholdingTax.Id,
            WithholdingTaxAccountId = fixture.WhtPayable.Id,
            SourceReference = "TDC-WHT-PAY-001",
            OpeningDate = new DateTime(2026, 1, 1),
            FiscalPeriodId = fixture.Period.Id,
            CurrencyCode = "GHS",
            TaxableBase = 1_000m,
            NetPaidAmount = 925m,
            Amount = 75m
        });
        var arBatch = await service.CreateArWithholdingBatchAsync(new CreateArWithholdingOpeningBalanceDto
        {
            CustomerId = fixture.Customer.Id,
            TaxId = fixture.WithholdingTax.Id,
            WithholdingTaxAccountId = fixture.WhtReceivable.Id,
            SourceReference = "TDC-WHT-CERT-001",
            CertificateNumber = "GRA-CERT-OPEN-001",
            CertificateDate = new DateTime(2025, 12, 20),
            OpeningDate = new DateTime(2026, 1, 1),
            FiscalPeriodId = fixture.Period.Id,
            CurrencyCode = "GHS",
            Amount = 45m
        });

        await service.SubmitForApprovalAsync(apBatch.Id);
        await ApproveBatchAsync(db, apBatch.Id);
        await service.PostAsync(apBatch.Id);
        await service.SubmitForApprovalAsync(arBatch.Id);
        await ApproveBatchAsync(db, arBatch.Id);
        await service.PostAsync(arBatch.Id);

        var compliance = new WithholdingTaxCertificateService(db, CreateCurrentUser(tenantId).Object);
        var register = await compliance.GetApCertificatesAsync(new WhtCertificateQueryDto { Page = 1, PageSize = 20 });
        register.Items.Should().ContainSingle(item =>
            item.WithholdingAmount == 75m && item.TaxableBase == 1_000m && item.TaxRate == 7.5m);
        (await compliance.GetUnremittedLiabilitiesAsync(null, null, "GHS"))
            .Should().ContainSingle(item => item.WithholdingAmount == 75m);

        var arEvidence = await db.Set<CustomerPayment>().SingleAsync(payment => payment.OpeningBalanceBatchId == arBatch.Id);
        arEvidence.WithholdingTaxAmount.Should().Be(45m);
        arEvidence.WithholdingCertificateNumber.Should().Be("GRA-CERT-OPEN-001");
        arEvidence.JournalEntryId.Should().NotBeNull();
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-SpecializedOpeningBalances")]
    [Trait("Category", "AccountingControl")]
    public async Task WithholdingOpening_ShouldRejectAnAccountNotMappedOnTheTaxMaster()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = SeedOpeningBalanceFixture(db, tenantId);
        await db.SaveChangesAsync();
        var service = CreateService(db, tenantId);

        var act = () => service.CreateApWithholdingBatchAsync(new CreateApWithholdingOpeningBalanceDto
        {
            SupplierId = fixture.Supplier.Id,
            TaxId = fixture.WithholdingTax.Id,
            WithholdingTaxAccountId = fixture.Equity.Id,
            OpeningDate = new DateTime(2026, 1, 1),
            FiscalPeriodId = fixture.Period.Id,
            CurrencyCode = "GHS",
            TaxableBase = 1_000m,
            NetPaidAmount = 925m,
            Amount = 75m
        });

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not the payable account configured*");
        (await db.OpeningBalanceBatches.CountAsync()).Should().Be(0);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-SubledgerOpeningBalances")]
    [Trait("Requirement", "FIN-LIM-0048")]
    public async Task FixedAssetOpeningBatch_ShouldDerivePostingAndLinkImportedRegisterEvidence()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = SeedOpeningBalanceFixture(db, tenantId);
        var assetFixture = SeedFixedAssetOpeningFixture(db, tenantId, fixture.Cash, fixture.Equity);
        await db.SaveChangesAsync();
        var service = CreateService(db, tenantId, withAudit: true);

        var batch = await service.CreateFixedAssetBatchAsync(new CreateFixedAssetOpeningBalanceBatchDto
        {
            SourceReference = "TDC-FA-CUTOVER",
            OpeningDate = new DateTime(2026, 1, 1),
            FiscalPeriodId = fixture.Period.Id,
            BookClassification = "IFRS",
            FixedAssetBookValueIds = new[] { assetFixture.BookValue.Id }
        });

        // Accounts and amounts are server-derived from the approved asset category and imported
        // book values; the migration operator never supplies editable GL lines for this workflow.
        batch.Lines.Should().ContainSingle(line =>
            line.CounterpartyType == "FixedAssetOpeningCost" &&
            line.AccountId == assetFixture.AssetAccount.Id &&
            line.DebitAmount == 1_000m);
        batch.Lines.Should().ContainSingle(line =>
            line.CounterpartyType == "FixedAssetOpeningDep" &&
            line.AccountId == assetFixture.AccumulatedDepreciationAccount.Id &&
            line.CreditAmount == 200m);
        batch.Lines.Should().ContainSingle(line =>
            line.CounterpartyType == null &&
            line.AccountId == fixture.Equity.Id &&
            line.CreditAmount == 800m);

        await service.SubmitForApprovalAsync(batch.Id);
        await ApproveBatchAsync(db, batch.Id);
        var posted = await service.PostAsync(batch.Id);

        posted.Status.Should().Be("Posted");
        var bookValue = await db.FixedAssetBookValues.SingleAsync(value => value.Id == assetFixture.BookValue.Id);
        bookValue.OpeningPostedToGl.Should().BeTrue();
        bookValue.OpeningJournalEntryId.Should().Be(posted.JournalEntryId);
        bookValue.SourceDocumentType.Should().Be("OpeningBalanceBatch");
        bookValue.SourceDocumentId.Should().Be(batch.Id);
        var asset = await db.FixedAssets.SingleAsync(item => item.Id == assetFixture.Asset.Id);
        asset.JournalEntryId.Should().Be(posted.JournalEntryId);
        asset.PostingEventId.Should().Be(posted.PostingEventId);
        (await db.AssetTransactions
            .Where(transaction => transaction.FixedAssetId == asset.Id && transaction.TransactionType.StartsWith("Opening"))
            .AllAsync(transaction => transaction.RelatedEntityId == posted.JournalEntryId)).Should().BeTrue();
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-SubledgerOpeningBalances")]
    [Trait("Requirement", "FIN-LIM-0048")]
    public async Task FixedAssetOpeningBatch_ShouldRejectRegisterDriftAfterPreparation()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = SeedOpeningBalanceFixture(db, tenantId);
        var assetFixture = SeedFixedAssetOpeningFixture(db, tenantId, fixture.Cash, fixture.Equity);
        await db.SaveChangesAsync();
        var service = CreateService(db, tenantId);
        var batch = await service.CreateFixedAssetBatchAsync(new CreateFixedAssetOpeningBalanceBatchDto
        {
            OpeningDate = new DateTime(2026, 1, 1),
            FiscalPeriodId = fixture.Period.Id,
            FixedAssetBookValueIds = new[] { assetFixture.BookValue.Id }
        });

        var value = await db.FixedAssetBookValues.SingleAsync(item => item.Id == assetFixture.BookValue.Id);
        value.AcquisitionCost = 1_100m;
        value.NetBookValue = 900m;
        await db.SaveChangesAsync();

        var validation = await service.ValidateBatchAsync(batch.Id);

        validation.IsValid.Should().BeFalse();
        validation.Errors.Should().Contain(error => error.Contains("changed after batch preparation", StringComparison.OrdinalIgnoreCase));
        (await db.FinancePostingEvents.CountAsync()).Should().Be(0);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-SubledgerOpeningBalances")]
    [Trait("Category", "TenantIsolation")]
    public async Task FixedAssetOpeningBatch_ShouldRejectCrossTenantAssetSelection()
    {
        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = SeedOpeningBalanceFixture(db, tenantId);
        var otherFixture = SeedOpeningBalanceFixture(db, otherTenantId);
        var otherAsset = SeedFixedAssetOpeningFixture(db, otherTenantId, otherFixture.Cash, otherFixture.Equity);
        await db.SaveChangesAsync();
        var service = CreateService(db, tenantId);

        var act = () => service.CreateFixedAssetBatchAsync(new CreateFixedAssetOpeningBalanceBatchDto
        {
            OpeningDate = new DateTime(2026, 1, 1),
            FiscalPeriodId = fixture.Period.Id,
            FixedAssetBookValueIds = new[] { otherAsset.BookValue.Id }
        });

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*current tenant*");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-SubledgerOpeningBalances")]
    [Trait("Requirement", "FIN-LIM-0048")]
    public async Task FixedAssetOpeningBatch_ShouldNotReinterpretSelectedEvidenceAcrossBooks()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = SeedOpeningBalanceFixture(db, tenantId);
        var assetFixture = SeedFixedAssetOpeningFixture(db, tenantId, fixture.Cash, fixture.Equity);
        var taxBook = new AccountingBook
        {
            Id = Guid.NewGuid(), TenantId = tenantId, Code = "TAX", Name = "Tax", AllowsPosting = true
        };
        var taxBookValue = new FixedAssetBookValue
        {
            Id = Guid.NewGuid(), TenantId = tenantId,
            FixedAssetId = assetFixture.Asset.Id, FixedAsset = assetFixture.Asset,
            AccountingBookId = taxBook.Id, AccountingBook = taxBook, BookClassification = "TAX",
            AcquisitionCost = 900m, AccumulatedDepreciation = 300m, NetBookValue = 600m,
            UsefulLifeMonths = 120, RemainingUsefulLifeMonths = 80,
            OpeningAsOfDate = new DateTime(2026, 1, 1), OpeningSource = "OpeningImport"
        };
        assetFixture.Asset.BookValues.Add(taxBookValue);
        db.AccountingBooks.Add(taxBook);
        await db.SaveChangesAsync();
        var service = CreateService(db, tenantId);

        // This reproduces the reviewed UI race: a TAX row was selected and the header later
        // changed to IFRS. The exact book-value key must be rejected, never translated to the
        // IFRS row belonging to the same fixed asset.
        var act = () => service.CreateFixedAssetBatchAsync(new CreateFixedAssetOpeningBalanceBatchDto
        {
            OpeningDate = new DateTime(2026, 1, 1),
            FiscalPeriodId = fixture.Period.Id,
            BookClassification = "IFRS",
            FixedAssetBookValueIds = new[] { taxBookValue.Id }
        });

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*do not belong to the IFRS book*");
        (await db.OpeningBalanceBatches.CountAsync()).Should().Be(0);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-SubledgerOpeningBalances")]
    [Trait("Requirement", "FIN-LIM-0048")]
    public async Task SubledgerReadiness_ShouldExposePostedAndUnpostedSourceEvidence()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = SeedOpeningBalanceFixture(db, tenantId);
        SeedFixedAssetOpeningFixture(db, tenantId, fixture.Cash, fixture.Equity);
        db.VendorInvoices.Add(new VendorInvoice
        {
            Id = Guid.NewGuid(), TenantId = tenantId, InvoiceNumber = "AP-OPEN-1",
            InvoiceDate = new DateTime(2026, 1, 1), DueDate = new DateTime(2026, 2, 1),
            IsOpeningBalance = true, TotalAmount = 500m, BaseCurrencyAmount = 500m
        });
        db.Invoices.Add(new Invoice
        {
            Id = Guid.NewGuid(), TenantId = tenantId, InvoiceNumber = "AR-OPEN-1",
            InvoiceDate = new DateTime(2026, 1, 1), DueDate = new DateTime(2026, 2, 1),
            IsOpeningBalance = true, TotalAmount = 700m, BaseCurrencyAmount = 700m
        });
        await db.SaveChangesAsync();
        var service = CreateService(db, tenantId);

        var readiness = await service.GetSubledgerReadinessAsync();

        readiness.ApOpeningInvoiceCount.Should().Be(1);
        readiness.ArOpeningInvoiceCount.Should().Be(1);
        readiness.FixedAssetOpeningBookValueCount.Should().Be(1);
        readiness.FixedAssetOpeningNetBookValue.Should().Be(800m);
        readiness.Warnings.Should().HaveCount(3);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-OpeningBalances")]
    [Trait("Category", "Migration")]
    public async Task BalancedGlOpeningBalanceBatch_ShouldPostThroughFinancePostingEngine()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = SeedOpeningBalanceFixture(db, tenantId);
        await db.SaveChangesAsync();
        var service = CreateService(db, tenantId, withAudit: true);

        var batch = await service.CreateBatchAsync(CreateBalancedBatch(fixture.Period.Id, fixture.Cash.Id, fixture.Equity.Id));
        await service.SubmitForApprovalAsync(batch.Id);
        await ApproveBatchAsync(db, batch.Id);
        var posted = await service.PostAsync(batch.Id);

        posted.Status.Should().Be("Posted");
        posted.JournalEntryId.Should().NotBeNull();
        posted.PostingEventId.Should().NotBeNull();
        (await db.JournalEntries.CountAsync(j => j.TenantId == tenantId && j.SourceDocumentType == "OpeningBalanceBatch")).Should().Be(1);
        (await db.FinancePostingEvents.CountAsync(e => e.TenantId == tenantId && e.SourceDocumentType == "OpeningBalanceBatch")).Should().Be(1);
        (await db.AccountTransactions.CountAsync(t => t.TenantId == tenantId && t.SourceDocumentType == "OpeningBalanceBatch")).Should().Be(2);

        (await db.AccountBalances.SingleAsync(a => a.AccountId == fixture.Cash.Id)).ClosingBalance.Should().Be(100m);
        (await db.AccountBalances.SingleAsync(a => a.AccountId == fixture.Equity.Id)).ClosingBalance.Should().Be(-100m);
        (await db.AuditLogs.CountAsync(a => a.TenantId == tenantId && a.Action == FinanceAuditEvents.OpeningBalancePosted)).Should().Be(1);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-OpeningBalances")]
    [Trait("Category", "Reversal")]
    public async Task PostedOpeningBalanceReversal_ShouldRequireIndependentReviewAndPostCompensatingEvidence()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = SeedOpeningBalanceFixture(db, tenantId);
        await db.SaveChangesAsync();
        var maker = CreateService(db, tenantId, withAudit: true);
        var batch = await maker.CreateBatchAsync(CreateBalancedBatch(fixture.Period.Id, fixture.Cash.Id, fixture.Equity.Id));
        await maker.SubmitForApprovalAsync(batch.Id);
        await ApproveBatchAsync(db, batch.Id);
        var posted = await maker.PostAsync(batch.Id);

        var request = await maker.RequestReversalAsync(batch.Id, new RequestOpeningBalanceBatchReversalDto
        {
            ReversalDate = new DateTime(2026, 1, 1),
            Reason = "Incorrect cutover values require controlled correction",
            ImpactAssessment = "The original GL opening will be neutralized before a corrected controlled batch is prepared."
        });
        await FluentActions.Awaiting(() => maker.ReviewReversalAsync(batch.Id, request.Id, new ReviewOpeningBalanceBatchReversalDto
            {
                Approved = true,
                ReviewComment = "Independent review confirms the correction is necessary."
            }))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("The reversal requester cannot review the same request.");

        var checker = CreateService(db, tenantId, withAudit: true);
        var approved = await checker.ReviewReversalAsync(batch.Id, request.Id, new ReviewOpeningBalanceBatchReversalDto
        {
            Approved = true,
            ReviewComment = "Independent review confirms the correction is necessary."
        });
        approved.Status.Should().Be(OpeningBalanceBatchReversalStatuses.Approved);

        var reversed = await checker.PostReversalAsync(batch.Id, request.Id);
        reversed.Status.Should().Be(OpeningBalanceBatchReversalStatuses.Posted);
        reversed.OriginalJournalEntryId.Should().Be(posted.JournalEntryId!.Value);
        reversed.ReversalJournalEntryId.Should().NotBeNull();
        reversed.ReversalPostingEventId.Should().NotBeNull();
        (await db.OpeningBalanceBatches.SingleAsync(item => item.Id == batch.Id)).Status.Should().Be("Reversed");
        (await db.AccountBalances.SingleAsync(item => item.AccountId == fixture.Cash.Id)).ClosingBalance.Should().Be(0m);
        (await db.AccountBalances.SingleAsync(item => item.AccountId == fixture.Equity.Id)).ClosingBalance.Should().Be(0m);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-OpeningBalances")]
    [Trait("Category", "AccountingControl")]
    public async Task FreeFormOpeningBalance_ShouldRejectEveryProtectedAccountSourceBeforePersistence()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = SeedOpeningBalanceFixture(db, tenantId);
        var apControl = SeedAccount(db, tenantId, "2100", AccountType.Liability);
        var retainedEarnings = SeedAccount(db, tenantId, "3100", AccountType.Equity);
        var bankGl = SeedAccount(db, tenantId, "1010", AccountType.Asset);
        var liquidityGl = SeedAccount(db, tenantId, "1020", AccountType.Asset);
        var fixedAssetGl = SeedAccount(db, tenantId, "1510", AccountType.Asset);
        var accumulatedDepreciation = SeedAccount(db, tenantId, "1590", AccountType.Asset);
        var depreciationExpense = SeedAccount(db, tenantId, "6100", AccountType.Expense);
        var explicitControl = SeedAccount(db, tenantId, "2199", AccountType.Liability);
        explicitControl.IsControlAccount = true;

        var settings = db.FinanceSettings.Local.Single(item => item.TenantId == tenantId);
        settings.ControlAccountApId = apControl.Id;
        settings.RetainedEarningsAccountId = retainedEarnings.Id;
        db.BankAccounts.Add(new BankAccount
        {
            Id = Guid.NewGuid(), TenantId = tenantId, AccountNumber = "FINDEMO-BANK", AccountName = "Demo bank",
            BankName = "TDC Test Bank", Currency = "GHS", GLAccountId = bankGl.Id, IsActive = true
        });
        db.LiquidityAccounts.Add(new LiquidityAccount
        {
            Id = Guid.NewGuid(), TenantId = tenantId, Code = "FINDEMO-LIQ", Name = "Demo undeposited cash",
            Currency = "GHS", AccountType = LiquidityAccountType.UndepositedCash,
            GLAccountId = liquidityGl.Id, IsActive = true
        });
        db.FixedAssetCategories.Add(new FixedAssetCategory
        {
            Id = Guid.NewGuid(), TenantId = tenantId, Code = "FA-CONTROL", Name = "Protected fixed assets",
            AssetAccountId = fixedAssetGl.Id,
            AccumulatedDepreciationAccountId = accumulatedDepreciation.Id,
            DepreciationExpenseAccountId = depreciationExpense.Id
        });
        await db.SaveChangesAsync();
        var service = CreateService(db, tenantId);

        var protectedAccounts = new[]
        {
            apControl,
            retainedEarnings,
            bankGl,
            liquidityGl,
            fixedAssetGl,
            fixture.WhtPayable,
            explicitControl
        };
        var request = new CreateOpeningBalanceBatchDto
        {
            BatchNumber = "OB-PROTECTED-ACCOUNTS",
            OpeningDate = new DateTime(2026, 1, 1),
            FiscalPeriodId = fixture.Period.Id,
            BookClassification = "IFRS",
            Lines = protectedAccounts.Select(account => new CreateOpeningBalanceLineDto
            {
                AccountId = account.Id,
                DebitAmount = 100m
            }).Append(new CreateOpeningBalanceLineDto
            {
                // Migration clearing is the intentional free-form cutover bridge and is not
                // rejected merely because it is configured in Finance Settings.
                AccountId = fixture.Equity.Id,
                CreditAmount = protectedAccounts.Length * 100m
            }).ToArray()
        };

        Func<Task> act = () => service.CreateBatchAsync(request);

        var error = await act.Should().ThrowAsync<InvalidOperationException>();
        error.Which.Message.Should().Contain("protected-account validation");
        foreach (var account in protectedAccounts)
            error.Which.Message.Should().Contain($"account '{account.AccountCode}' is protected");
        error.Which.Message.Should().Contain("ControlAccountApId");
        error.Which.Message.Should().Contain("RetainedEarningsAccountId");
        error.Which.Message.Should().Contain("bank master");
        error.Which.Message.Should().Contain("liquidity account");
        error.Which.Message.Should().Contain("tax master");
        error.Which.Message.Should().Contain("fixed-asset category");
        (await db.OpeningBalanceBatches.CountAsync()).Should().Be(0);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-OpeningBalances")]
    [Trait("Category", "AccountingControl")]
    public async Task FreeFormOpeningBalance_ShouldFailClosedWhenFinanceSettingsAreMissing()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        SeedTenant(db, tenantId);
        var period = SeedPeriod(db, tenantId, isOpen: true, isClosed: false);
        var debitAccount = SeedAccount(db, tenantId, "1000", AccountType.Asset);
        var creditAccount = SeedAccount(db, tenantId, "3000", AccountType.Equity);
        await db.SaveChangesAsync();
        var service = CreateService(db, tenantId);

        Func<Task> act = () => service.CreateBatchAsync(
            CreateBalancedBatch(period.Id, debitAccount.Id, creditAccount.Id));

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Finance Settings are required*classified safely*");
        (await db.OpeningBalanceBatches.CountAsync()).Should().Be(0);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-OpeningBalances")]
    [Trait("Category", "AccountingControl")]
    public async Task ExistingFreeFormDraft_ShouldFailValidationWhenItsAccountBecomesProtected()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = SeedOpeningBalanceFixture(db, tenantId);
        await db.SaveChangesAsync();
        var service = CreateService(db, tenantId);
        var batch = await service.CreateBatchAsync(
            CreateBalancedBatch(fixture.Period.Id, fixture.Cash.Id, fixture.Equity.Id));

        db.BankAccounts.Add(new BankAccount
        {
            Id = Guid.NewGuid(), TenantId = tenantId, AccountNumber = "LATE-BANK", AccountName = "Late mapped bank",
            BankName = "TDC Test Bank", Currency = "GHS", GLAccountId = fixture.Cash.Id, IsActive = true
        });
        await db.SaveChangesAsync();

        var validation = await service.ValidateBatchAsync(batch.Id);

        validation.IsValid.Should().BeFalse();
        validation.Errors.Should().Contain(error =>
            error.Contains($"account '{fixture.Cash.AccountCode}' is protected", StringComparison.OrdinalIgnoreCase) &&
            error.Contains("bank master", StringComparison.OrdinalIgnoreCase));
        (await db.OpeningBalanceBatches.SingleAsync(item => item.Id == batch.Id)).Status.Should().Be("Draft");
        (await db.FinancePostingEvents.CountAsync()).Should().Be(0);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-OpeningBalances")]
    [Trait("Category", "AccountingControl")]
    public async Task FreeFormOpeningBalance_ShouldRejectReservedServerDerivedEvidenceMarkers()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = SeedOpeningBalanceFixture(db, tenantId);
        await db.SaveChangesAsync();
        var service = CreateService(db, tenantId);
        var request = CreateBalancedBatch(fixture.Period.Id, fixture.Cash.Id, fixture.Equity.Id);
        request.Lines.First().CounterpartyType = "FixedAssetOpeningCost";
        request.Lines.First().CounterpartyId = Guid.NewGuid();

        Func<Task> act = () => service.CreateBatchAsync(request);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*reserved server-derived opening source*");
        (await db.OpeningBalanceBatches.CountAsync()).Should().Be(0);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-OpeningBalances")]
    [Trait("Category", "Migration")]
    public async Task UnbalancedOpeningBalanceBatch_ShouldBeRejectedWithoutPosting()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = SeedOpeningBalanceFixture(db, tenantId);
        await db.SaveChangesAsync();
        var service = CreateService(db, tenantId);

        var batch = await service.CreateBatchAsync(new CreateOpeningBalanceBatchDto
        {
            BatchNumber = "OB-UNBALANCED",
            OpeningDate = new DateTime(2026, 1, 1),
            FiscalPeriodId = fixture.Period.Id,
            Lines = new[]
            {
                new CreateOpeningBalanceLineDto { AccountId = fixture.Cash.Id, DebitAmount = 100m },
                new CreateOpeningBalanceLineDto { AccountId = fixture.Equity.Id, CreditAmount = 90m }
            }
        });

        var result = await service.ValidateBatchAsync(batch.Id);
        var act = () => service.SubmitForApprovalAsync(batch.Id);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("must be balanced", StringComparison.OrdinalIgnoreCase));
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*must be balanced*");
        (await db.FinancePostingEvents.CountAsync()).Should().Be(0);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-OpeningBalances")]
    [Trait("Category", "Migration")]
    public async Task ClosedPeriodOpeningBalancePosting_ShouldBeRejectedByPostingEngine()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = SeedOpeningBalanceFixture(db, tenantId, isOpen: false, isClosed: true);
        await db.SaveChangesAsync();
        var service = CreateService(db, tenantId);

        var batch = await service.CreateBatchAsync(CreateBalancedBatch(fixture.Period.Id, fixture.Cash.Id, fixture.Equity.Id));
        await service.SubmitForApprovalAsync(batch.Id);
        await ApproveBatchAsync(db, batch.Id);
        var act = () => service.PostAsync(batch.Id);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Posting period is not open.");
        (await db.FinancePostingEvents.CountAsync()).Should().Be(0);
        (await db.OpeningBalanceBatches.SingleAsync(b => b.Id == batch.Id)).Status.Should().Be("PostingFailed");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-OpeningBalances")]
    [Trait("Category", "TenantIsolation")]
    public async Task CrossTenantAccount_ShouldBeRejected()
    {
        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = SeedOpeningBalanceFixture(db, tenantId);
        SeedTenant(db, otherTenantId, "OTH");
        var otherAccount = SeedAccount(db, otherTenantId, "3999", AccountType.Equity);
        await db.SaveChangesAsync();
        var service = CreateService(db, tenantId);

        Func<Task> act = () => service.CreateBatchAsync(
            CreateBalancedBatch(fixture.Period.Id, fixture.Cash.Id, otherAccount.Id));

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*account selection is unavailable for the current tenant*");
        (await db.OpeningBalanceBatches.CountAsync()).Should().Be(0);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-OpeningBalances")]
    [Trait("Category", "Migration")]
    public async Task InactiveOrNonPostingAccount_ShouldBeRejected()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = SeedOpeningBalanceFixture(db, tenantId);
        fixture.Equity.AllowDirectPosting = false;
        await db.SaveChangesAsync();
        var service = CreateService(db, tenantId);

        Func<Task> act = () => service.CreateBatchAsync(
            CreateBalancedBatch(fixture.Period.Id, fixture.Cash.Id, fixture.Equity.Id));

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*chart disallows direct posting*");
        (await db.OpeningBalanceBatches.CountAsync()).Should().Be(0);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-OpeningBalances")]
    [Trait("Category", "Migration")]
    public async Task DuplicateOpeningBalancePosting_ShouldReturnExistingJournal()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = SeedOpeningBalanceFixture(db, tenantId);
        await db.SaveChangesAsync();
        var service = CreateService(db, tenantId);

        var batch = await service.CreateBatchAsync(CreateBalancedBatch(fixture.Period.Id, fixture.Cash.Id, fixture.Equity.Id));
        await service.SubmitForApprovalAsync(batch.Id);
        await ApproveBatchAsync(db, batch.Id);
        var first = await service.PostAsync(batch.Id);
        var second = await service.PostAsync(batch.Id);

        second.JournalEntryId.Should().Be(first.JournalEntryId);
        second.PostingEventId.Should().Be(first.PostingEventId);
        (await db.JournalEntries.CountAsync(j => j.SourceDocumentType == "OpeningBalanceBatch")).Should().Be(1);
        (await db.FinancePostingEvents.CountAsync(e => e.SourceDocumentType == "OpeningBalanceBatch")).Should().Be(1);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-OpeningBalances")]
    [Trait("Category", "Workflow")]
    public async Task OpeningBalancePosting_ShouldRequireWorkflowApproval_WhenWorkflowIsConfigured()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = SeedOpeningBalanceFixture(db, tenantId);
        await db.SaveChangesAsync();
        var workflow = new Mock<IWorkflowService>();
        workflow.Setup(x => x.StartApprovalWorkflowAsync("OpeningBalanceBatch", It.IsAny<Guid>()))
            .ReturnsAsync(new WorkflowExecutionResult
            {
                Success = true,
                Status = WorkflowInstanceStatus.InProgress,
                WorkflowInstanceId = Guid.NewGuid()
            });
        var service = CreateService(db, tenantId, workflow: workflow.Object);

        var batch = await service.CreateBatchAsync(CreateBalancedBatch(fixture.Period.Id, fixture.Cash.Id, fixture.Equity.Id));
        var submitted = await service.SubmitForApprovalAsync(batch.Id);
        var act = () => service.PostAsync(batch.Id);

        submitted.Status.Should().Be("PendingApproval");
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Opening balance batch must be approved before posting.");
        workflow.Verify(x => x.StartApprovalWorkflowAsync("OpeningBalanceBatch", batch.Id), Times.Once);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-OpeningBalances")]
    [Trait("Category", "Workflow")]
    public async Task StandardOpening_ShouldFailClosedWhenWorkflowIsUnavailable()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = SeedOpeningBalanceFixture(db, tenantId);
        await db.SaveChangesAsync();
        var service = CreateService(db, tenantId, withoutWorkflow: true);
        var batch = await service.CreateBatchAsync(
            CreateBalancedBatch(fixture.Period.Id, fixture.Cash.Id, fixture.Equity.Id));

        await FluentActions.Awaiting(() => service.SubmitForApprovalAsync(batch.Id))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Controlled opening balances require the Finance approval workflow*");

        (await db.OpeningBalanceBatches.SingleAsync(item => item.Id == batch.Id)).Status.Should().Be("Validated");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-GovernedOpeningSources")]
    [Trait("Category", "Workflow")]
    public async Task SubmitForApprovalRetries_ShouldPreserveInFlightAndTerminalStatusesWithoutDuplicateWorkflow()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = SeedOpeningBalanceFixture(db, tenantId);
        await db.SaveChangesAsync();
        var workflow = new Mock<IWorkflowService>();
        workflow.Setup(x => x.StartApprovalWorkflowAsync("OpeningBalanceBatch", It.IsAny<Guid>()))
            .ReturnsAsync(new WorkflowExecutionResult
            {
                Success = true,
                Status = WorkflowInstanceStatus.InProgress,
                WorkflowInstanceId = Guid.NewGuid()
            });
        var service = CreateService(db, tenantId, workflow: workflow.Object);
        var created = await service.CreateBatchAsync(
            CreateBalancedBatch(fixture.Period.Id, fixture.Cash.Id, fixture.Equity.Id));

        (await service.SubmitForApprovalAsync(created.Id)).Status.Should().Be("PendingApproval");
        (await service.SubmitForApprovalAsync(created.Id)).Status.Should().Be("PendingApproval");
        var entity = await db.OpeningBalanceBatches.SingleAsync(item => item.Id == created.Id);
        entity.Status = "Approved";
        await db.SaveChangesAsync();
        (await service.SubmitForApprovalAsync(created.Id)).Status.Should().Be("Approved");
        entity.Status = "PostingFailed";
        await db.SaveChangesAsync();
        (await service.SubmitForApprovalAsync(created.Id)).Status.Should().Be("PostingFailed");
        entity.Status = "Posted";
        await db.SaveChangesAsync();
        (await service.SubmitForApprovalAsync(created.Id)).Status.Should().Be("Posted");

        workflow.Verify(x => x.StartApprovalWorkflowAsync("OpeningBalanceBatch", created.Id), Times.Once);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-GovernedOpeningSources")]
    [Trait("Category", "Workflow")]
    public async Task GovernedOpening_ShouldRejectCompletedAtStartWorkflowAndNotRestartIt()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = SeedOpeningBalanceFixture(db, tenantId);
        var bankGl = SeedAccount(db, tenantId, "1018", AccountType.Asset);
        var bank = new BankAccount
        {
            Id = Guid.NewGuid(), TenantId = tenantId, AccountNumber = "COMPLETED-WORKFLOW-BANK",
            AccountName = "Completed workflow bank", BankName = "Bank", Currency = "GHS",
            GLAccountId = bankGl.Id, IsActive = true
        };
        db.BankAccounts.Add(bank);
        await db.SaveChangesAsync();
        var workflow = new Mock<IWorkflowService>();
        workflow.Setup(x => x.StartApprovalWorkflowAsync("OpeningBalanceBatch", It.IsAny<Guid>()))
            .ReturnsAsync(new WorkflowExecutionResult
            {
                Success = true,
                Status = WorkflowInstanceStatus.Completed,
                WorkflowInstanceId = Guid.NewGuid()
            });
        var service = CreateService(db, tenantId, workflow: workflow.Object);
        var created = await service.CreateBankAccountOpeningBatchAsync(new CreateBankAccountOpeningBalanceDto
        {
            BatchNumber = "BANK-COMPLETED-AT-START",
            SourceReference = "BANK-WORKFLOW-EVIDENCE",
            OpeningDate = new DateTime(2026, 1, 1),
            FiscalPeriodId = fixture.Period.Id,
            BookClassification = "IFRS",
            BankAccountId = bank.Id,
            Amount = 100m
        });

        await FluentActions.Awaiting(() => service.SubmitForApprovalAsync(created.Id))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*must start as a real pending workflow*");
        (await db.OpeningBalanceBatches.SingleAsync(item => item.Id == created.Id)).Status.Should().Be("Failed");
        await FluentActions.Awaiting(() => service.SubmitForApprovalAsync(created.Id))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*only Draft or Validated batches may start approval*");
        workflow.Verify(x => x.StartApprovalWorkflowAsync("OpeningBalanceBatch", created.Id), Times.Once);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-OpeningBalances")]
    [Trait("Category", "Workflow")]
    public async Task WorkflowStartupFailure_ShouldNotAllowOpeningBalancePosting()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = SeedOpeningBalanceFixture(db, tenantId);
        await db.SaveChangesAsync();
        var workflow = new Mock<IWorkflowService>();
        workflow.Setup(x => x.StartApprovalWorkflowAsync("OpeningBalanceBatch", It.IsAny<Guid>()))
            .ReturnsAsync(new WorkflowExecutionResult
            {
                Success = false,
                Message = "Workflow routing is unavailable."
            });
        var service = CreateService(db, tenantId, workflow: workflow.Object);

        var batch = await service.CreateBatchAsync(CreateBalancedBatch(fixture.Period.Id, fixture.Cash.Id, fixture.Equity.Id));
        Func<Task> submit = () => service.SubmitForApprovalAsync(batch.Id);
        await submit
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Workflow routing is unavailable*");
        await submit
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*only Draft or Validated batches may start approval*");

        var post = () => service.PostAsync(batch.Id);
        await post.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*must be approved before posting*");
        (await db.OpeningBalanceBatches.SingleAsync(b => b.Id == batch.Id)).Status.Should().Be("Failed");
        (await db.FinancePostingEvents.CountAsync()).Should().Be(0);
        workflow.Verify(x => x.StartApprovalWorkflowAsync("OpeningBalanceBatch", batch.Id), Times.Once);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-OpeningBalances")]
    [Trait("Category", "Migration")]
    public async Task SavedOpeningBalanceBatch_ShouldBeListedReloadedAndEditable()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = SeedOpeningBalanceFixture(db, tenantId);
        await db.SaveChangesAsync();
        var service = CreateService(db, tenantId, withAudit: true);

        var created = await service.CreateBatchAsync(CreateBalancedBatch(fixture.Period.Id, fixture.Cash.Id, fixture.Equity.Id));
        (await service.ValidateBatchAsync(created.Id)).IsValid.Should().BeTrue();

        var listed = await service.GetBatchesAsync();
        listed.Should().ContainSingle(batch => batch.Id == created.Id && batch.Status == "Validated");

        var updated = await service.UpdateBatchAsync(created.Id, new UpdateOpeningBalanceBatchDto
        {
            SourceReference = "TB-REVISED",
            Description = "Revised opening trial balance",
            OpeningDate = new DateTime(2026, 1, 2),
            FiscalPeriodId = fixture.Period.Id,
            BookClassification = "IFRS",
            Lines = new[]
            {
                new CreateOpeningBalanceLineDto { AccountId = fixture.Cash.Id, DebitAmount = 250m, Notes = "Revised debit" },
                new CreateOpeningBalanceLineDto { AccountId = fixture.Equity.Id, CreditAmount = 250m, Notes = "Revised credit" }
            }
        });

        updated.Status.Should().Be("Draft", "editing a validated batch must require validation again");
        updated.SourceReference.Should().Be("TB-REVISED");
        updated.Description.Should().Be("Revised opening trial balance");
        updated.OpeningDate.Should().Be(new DateTime(2026, 1, 2));
        updated.TotalDebit.Should().Be(250m);
        updated.TotalCredit.Should().Be(250m);
        updated.Lines.Should().HaveCount(2);
        (await service.GetBatchAsync(created.Id))!.Description.Should().Be("Revised opening trial balance");
        (await db.AuditLogs.CountAsync(log => log.TenantId == tenantId && log.Action == FinanceAuditEvents.OpeningBalanceBatchUpdated)).Should().Be(1);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-OpeningBalances")]
    [Trait("Category", "AccountingControl")]
    public async Task UpdateFreeFormOpeningBalance_ShouldRejectProtectedAccountWithoutMutatingDraft()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = SeedOpeningBalanceFixture(db, tenantId);
        var arControl = SeedAccount(db, tenantId, "1100", AccountType.Asset);
        db.FinanceSettings.Local.Single(item => item.TenantId == tenantId).ControlAccountArId = arControl.Id;
        await db.SaveChangesAsync();
        var service = CreateService(db, tenantId);
        var created = await service.CreateBatchAsync(
            CreateBalancedBatch(fixture.Period.Id, fixture.Cash.Id, fixture.Equity.Id));

        Func<Task> act = () => service.UpdateBatchAsync(created.Id, new UpdateOpeningBalanceBatchDto
        {
            SourceReference = "MUST-NOT-PERSIST",
            Description = "Unsafe AR control opening",
            OpeningDate = created.OpeningDate,
            FiscalPeriodId = created.FiscalPeriodId,
            BookClassification = created.BookClassification,
            Lines = new[]
            {
                new CreateOpeningBalanceLineDto { AccountId = arControl.Id, DebitAmount = 100m },
                new CreateOpeningBalanceLineDto { AccountId = fixture.Equity.Id, CreditAmount = 100m }
            }
        });

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*ControlAccountArId*");
        var unchanged = await service.GetBatchAsync(created.Id);
        unchanged!.SourceReference.Should().Be(created.SourceReference);
        unchanged.Description.Should().Be(created.Description);
        unchanged.Lines.Should().Contain(line => line.AccountId == fixture.Cash.Id);
        unchanged.Lines.Should().NotContain(line => line.AccountId == arControl.Id);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-OpeningBalances")]
    [Trait("Category", "Migration")]
    public async Task ApprovedOpeningBalanceBatch_ShouldNotBeEditable()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = SeedOpeningBalanceFixture(db, tenantId);
        await db.SaveChangesAsync();
        var service = CreateService(db, tenantId);

        var created = await service.CreateBatchAsync(CreateBalancedBatch(fixture.Period.Id, fixture.Cash.Id, fixture.Equity.Id));
        await service.SubmitForApprovalAsync(created.Id);

        var act = () => service.UpdateBatchAsync(created.Id, new UpdateOpeningBalanceBatchDto
        {
            OpeningDate = created.OpeningDate,
            FiscalPeriodId = created.FiscalPeriodId,
            BookClassification = created.BookClassification,
            Lines = created.Lines.Select(line => new CreateOpeningBalanceLineDto
            {
                AccountId = line.AccountId,
                DebitAmount = line.DebitAmount,
                CreditAmount = line.CreditAmount
            }).ToArray()
        });

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*cannot be edited*");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-OpeningBalances")]
    [Trait("Category", "Migration")]
    public async Task OpeningDateOutsideSelectedPeriod_ShouldFailValidation()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = SeedOpeningBalanceFixture(db, tenantId);
        await db.SaveChangesAsync();
        var service = CreateService(db, tenantId);
        var request = CreateBalancedBatch(fixture.Period.Id, fixture.Cash.Id, fixture.Equity.Id);
        request.OpeningDate = fixture.Period.EndDate.AddDays(1);

        var batch = await service.CreateBatchAsync(request);
        var validation = await service.ValidateBatchAsync(batch.Id);

        validation.IsValid.Should().BeFalse();
        validation.Errors.Should().Contain(error => error.Contains("must fall within the selected fiscal period", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-OpeningBalances")]
    [Trait("Category", "Migration")]
    public async Task ForeignCurrencyOpeningLine_ShouldFailValidationBeforePosting()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = SeedOpeningBalanceFixture(db, tenantId);
        await db.SaveChangesAsync();
        var service = CreateService(db, tenantId);
        var request = CreateBalancedBatch(fixture.Period.Id, fixture.Cash.Id, fixture.Equity.Id);
        request.Lines = new[]
        {
            new CreateOpeningBalanceLineDto
            {
                AccountId = fixture.Cash.Id,
                DebitAmount = 100m,
                TransactionCurrencyCode = "USD",
                FunctionalCurrencyCode = "GHS"
            },
            new CreateOpeningBalanceLineDto
            {
                AccountId = fixture.Equity.Id,
                CreditAmount = 100m,
                TransactionCurrencyCode = "USD",
                FunctionalCurrencyCode = "GHS"
            }
        };

        var batch = await service.CreateBatchAsync(request);
        var validation = await service.ValidateBatchAsync(batch.Id);

        validation.IsValid.Should().BeFalse();
        validation.Errors.Should().Contain(error => error.Contains("foreign-currency opening balances require a controlled source and approved FX evidence", StringComparison.OrdinalIgnoreCase));
        (await db.FinancePostingEvents.CountAsync()).Should().Be(0);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-OpeningBalances")]
    [Trait("Category", "Migration")]
    public async Task MatchingNonTenantCurrencies_ShouldFailValidationBeforeApproval()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = SeedOpeningBalanceFixture(db, tenantId);
        await db.SaveChangesAsync();
        var service = CreateService(db, tenantId);
        var request = CreateBalancedBatch(fixture.Period.Id, fixture.Cash.Id, fixture.Equity.Id);
        request.Lines = new[]
        {
            new CreateOpeningBalanceLineDto
            {
                AccountId = fixture.Cash.Id,
                DebitAmount = 100m,
                TransactionCurrencyCode = "USD",
                FunctionalCurrencyCode = "USD"
            },
            new CreateOpeningBalanceLineDto
            {
                AccountId = fixture.Equity.Id,
                CreditAmount = 100m,
                TransactionCurrencyCode = "USD",
                FunctionalCurrencyCode = "USD"
            }
        };

        var batch = await service.CreateBatchAsync(request);
        var validation = await service.ValidateBatchAsync(batch.Id);

        validation.IsValid.Should().BeFalse();
        validation.Errors.Should().Contain(error =>
            error.Contains("must match the tenant functional currency 'GHS'", StringComparison.OrdinalIgnoreCase));
        validation.Errors.Should().Contain(error =>
            error.Contains("foreign-currency opening balances require a controlled source and approved FX evidence", StringComparison.OrdinalIgnoreCase));
        Func<Task> submit = () => service.SubmitForApprovalAsync(batch.Id);
        await submit.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*tenant functional currency*");
        (await db.OpeningBalanceBatches.FindAsync(batch.Id))!.Status.Should().Be("Draft");
        (await db.FinancePostingEvents.CountAsync()).Should().Be(0);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-OpeningBalances")]
    [Trait("Category", "Workflow")]
    public async Task RejectedOpeningBalanceBatch_ShouldNotPost()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = SeedOpeningBalanceFixture(db, tenantId);
        await db.SaveChangesAsync();
        var service = CreateService(db, tenantId);

        var batch = await service.CreateBatchAsync(CreateBalancedBatch(fixture.Period.Id, fixture.Cash.Id, fixture.Equity.Id));
        var entity = await db.OpeningBalanceBatches.SingleAsync(b => b.Id == batch.Id);
        entity.Status = "Rejected";
        await db.SaveChangesAsync();
        var act = () => service.PostAsync(batch.Id);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Rejected opening-balance batches cannot be posted.");
        (await db.FinancePostingEvents.CountAsync()).Should().Be(0);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-OpeningBalances")]
    [Trait("Category", "Migration")]
    public async Task AllActiveBooksOpeningBalance_ShouldRemainRejected()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = SeedOpeningBalanceFixture(db, tenantId);
        await db.SaveChangesAsync();
        var service = CreateService(db, tenantId);
        var dto = CreateBalancedBatch(fixture.Period.Id, fixture.Cash.Id, fixture.Equity.Id);
        dto.BookClassification = "ALL_ACTIVE_BOOKS";

        var act = () => service.CreateBatchAsync(dto);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*ALL_ACTIVE_BOOKS*disabled*");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-OpeningBalances")]
    [Trait("Category", "CashBank")]
    public async Task BankCreationContract_ShouldNotAcceptOrCreateUngovernedOpeningValues()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        SeedTenant(db, tenantId);
        var glAccount = SeedAccount(db, tenantId, "1000", AccountType.Asset);
        await db.SaveChangesAsync();
        var service = CreateBankAccountService(db, tenantId);

        var created = await service.CreateAsync(new CreateBankAccountDto
        {
            AccountNumber = "BANK-001",
            AccountName = "Tenant Bank",
            BankName = "Bank",
            Currency = "GHS",
            AccountType = BankAccountType.Checking,
            GLAccountId = glAccount.Id
        });

        var stored = await db.BankAccounts.SingleAsync(item => item.Id == created.Id);
        stored.OpeningBalance.Should().Be(0m);
        stored.CurrentBalance.Should().Be(0m);
        stored.AvailableBalance.Should().Be(0m);
        typeof(CreateBankAccountDto).GetProperty("OpeningBalance").Should().BeNull();
        typeof(CreateBankAccountDto).GetProperty("OpeningBalanceExchangeRate").Should().BeNull();
        typeof(CreateBankAccountDto).GetProperty("OpeningDate").Should().BeNull();
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-OpeningBalances")]
    [Trait("Category", "Architecture")]
    public void OpeningBalancePosting_ShouldNotUseLegacySubledgerPostingService()
    {
        var root = FindRepositoryRoot();
        var service = File.ReadAllText(Path.Combine(root, "src", "ErpSystem.Api", "Services", "Finance", "Migration", "OpeningBalanceService.cs"));

        service.Should().Contain("IFinancePostingEngine");
        service.Should().Contain("_postingEngine.PostAsync");
        service.Should().NotContain("ISubledgerPostingService");
        service.Should().NotContain("PostJournalEntryAsync");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-GovernedOpeningSources")]
    [Trait("Category", "Architecture")]
    public void GovernedOpeningCreation_ShouldSerializeLogicalSourceAndRecoverOnlyExactIdempotentCollision()
    {
        var root = FindRepositoryRoot();
        var service = File.ReadAllText(Path.Combine(
            root,
            "src",
            "ErpSystem.Api",
            "Services",
            "Finance",
            "Migration",
            "OpeningBalanceService.cs"));

        service.Should().Contain("IsolationLevel.Serializable");
        service.Should().Contain("AcquireTransactionLockAsync");
        service.Should().Contain("BuildGovernedCreationLockResource");
        service.Should().Contain("ReloadBankCreationCollisionAsync");
        service.Should().Contain("ReloadResidualCreationCollisionAsync");
        service.Should().Contain("EnsureBankOpeningRetryMatches");
        service.Should().Contain("EnsureResidualOpeningRetryMatches");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-OpeningBalances")]
    [Trait("Category", "Migration")]
    public async Task PostingBackReferenceRepairDryRun_ShouldDetectMissingLinksWithoutMutation()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = SeedOpeningBalanceFixture(db, tenantId);
        var invoice = new VendorInvoice
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            InvoiceNumber = "AP-MISS-JE",
            InvoiceDate = new DateTime(2026, 1, 5),
            DueDate = new DateTime(2026, 1, 31),
            TotalAmount = 100m,
            Status = VendorInvoiceStatus.Approved
        };
        db.VendorInvoices.Add(invoice);
        var (journal, postingEvent) = SeedPostedGl(db, tenantId, fixture.Period.Id, fixture.Cash.Id, fixture.Equity.Id, "AP", "VendorInvoice", invoice.Id, invoice.InvoiceNumber, 100m);
        await db.SaveChangesAsync();
        var service = CreateMigrationSignOffService(db, tenantId, withAudit: true);

        var result = await service.DiagnosePostingBackReferencesAsync(new PostingBackReferenceRepairRequestDto
        {
            SourceDocumentTypes = new[] { "VendorInvoice" }
        });

        result.RepairMode.Should().BeFalse();
        result.RepairableCount.Should().Be(1);
        result.RepairedCount.Should().Be(0);
        result.Items.Single().CandidateJournalEntryId.Should().Be(journal.Id);
        result.Items.Single().CandidatePostingEventId.Should().Be(postingEvent.Id);
        (await db.VendorInvoices.SingleAsync(i => i.Id == invoice.Id)).JournalEntryId.Should().BeNull();
        (await db.AuditLogs.CountAsync(a => a.TenantId == tenantId && a.Action == FinanceAuditEvents.PostingBackReferenceRepairDiagnosticRun)).Should().Be(1);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-OpeningBalances")]
    [Trait("Category", "Migration")]
    public async Task PostingBackReferenceRepair_ShouldRepairOnlyUnambiguousLinks()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = SeedOpeningBalanceFixture(db, tenantId);
        var invoice = new VendorInvoice
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            InvoiceNumber = "AP-REPAIR-JE",
            InvoiceDate = new DateTime(2026, 1, 5),
            DueDate = new DateTime(2026, 1, 31),
            TotalAmount = 100m,
            Status = VendorInvoiceStatus.Approved
        };
        db.VendorInvoices.Add(invoice);
        var (journal, _) = SeedPostedGl(db, tenantId, fixture.Period.Id, fixture.Cash.Id, fixture.Equity.Id, "AP", "VendorInvoice", invoice.Id, invoice.InvoiceNumber, 100m);
        await db.SaveChangesAsync();
        var service = CreateMigrationSignOffService(db, tenantId, withAudit: true);

        var result = await service.RepairPostingBackReferencesAsync(new PostingBackReferenceRepairRequestDto
        {
            Repair = true,
            SourceDocumentTypes = new[] { "VendorInvoice" }
        });

        result.RepairedCount.Should().Be(1);
        (await db.VendorInvoices.SingleAsync(i => i.Id == invoice.Id)).JournalEntryId.Should().Be(journal.Id);
        (await db.JournalEntries.CountAsync()).Should().Be(1);
        (await db.AccountTransactions.CountAsync()).Should().Be(2);
        (await db.AuditLogs.CountAsync(a => a.TenantId == tenantId && a.Action == FinanceAuditEvents.PostingBackReferenceRepairApplied)).Should().Be(1);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-OpeningBalances")]
    [Trait("Category", "Migration")]
    public async Task PostingBackReferenceRepair_ShouldRefuseAmbiguousMatches()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = SeedOpeningBalanceFixture(db, tenantId);
        var invoice = new VendorInvoice
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            InvoiceNumber = "AP-AMBIGUOUS",
            InvoiceDate = new DateTime(2026, 1, 5),
            DueDate = new DateTime(2026, 1, 31),
            TotalAmount = 100m,
            Status = VendorInvoiceStatus.Approved
        };
        db.VendorInvoices.Add(invoice);
        SeedPostedGl(db, tenantId, fixture.Period.Id, fixture.Cash.Id, fixture.Equity.Id, "AP", "VendorInvoice", invoice.Id, "AP-AMBIGUOUS-A", 100m);
        SeedPostedGl(db, tenantId, fixture.Period.Id, fixture.Cash.Id, fixture.Equity.Id, "AP", "VendorInvoice", invoice.Id, "AP-AMBIGUOUS-B", 100m);
        await db.SaveChangesAsync();
        var service = CreateMigrationSignOffService(db, tenantId);

        var result = await service.RepairPostingBackReferencesAsync(new PostingBackReferenceRepairRequestDto
        {
            Repair = true,
            SourceDocumentTypes = new[] { "VendorInvoice" }
        });

        result.AmbiguousCount.Should().Be(1);
        result.RepairedCount.Should().Be(0);
        result.Items.Single().Status.Should().Be("Ambiguous");
        (await db.VendorInvoices.SingleAsync(i => i.Id == invoice.Id)).JournalEntryId.Should().BeNull();
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-OpeningBalances")]
    [Trait("Category", "CashBank")]
    public async Task BankSnapshotDiagnosticAndRepair_ShouldUsePostedGlOnly()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = SeedOpeningBalanceFixture(db, tenantId);
        var bank = new BankAccount
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            AccountNumber = "BANK-REBUILD",
            AccountName = "Migration Bank",
            BankName = "Bank",
            Currency = "GHS",
            GLAccountId = fixture.Cash.Id,
            OpeningBalance = 999m,
            CurrentBalance = 10m,
            AvailableBalance = 10m,
            IsActive = true
        };
        db.BankAccounts.Add(bank);
        SeedPostedGl(db, tenantId, fixture.Period.Id, fixture.Cash.Id, fixture.Equity.Id, "MIGRATION", "OpeningBalanceBatch", Guid.NewGuid(), "BANK-OB", 250m);
        await db.SaveChangesAsync();
        var service = CreateMigrationSignOffService(db, tenantId, withAudit: true);

        var diagnostic = await service.DiagnoseBankSnapshotsAsync(new BankSnapshotRebuildRequestDto { BankAccountId = bank.Id });
        var repaired = await service.RebuildBankSnapshotsAsync(new BankSnapshotRebuildRequestDto { Repair = true, BankAccountId = bank.Id });
        var reloaded = await db.BankAccounts.SingleAsync(b => b.Id == bank.Id);

        diagnostic.Items.Single().PostedGlBalance.Should().Be(250m);
        diagnostic.Items.Single().StoredOpeningBalance.Should().Be(999m);
        diagnostic.Items.Single().Status.Should().Be("Variance");
        repaired.RepairedCount.Should().Be(1);
        reloaded.CurrentBalance.Should().Be(250m);
        reloaded.AvailableBalance.Should().Be(250m);
        reloaded.OpeningBalance.Should().Be(999m);
        (await db.JournalEntries.CountAsync()).Should().Be(1);
        (await db.AuditLogs.CountAsync(a => a.TenantId == tenantId && a.Action == FinanceAuditEvents.BankSnapshotRebuildApplied)).Should().Be(1);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-OpeningBalances")]
    [Trait("Category", "Migration")]
    public async Task SubledgerOpeningMigrationDecision_ShouldRejectGlOnlySubledgerOpenings()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        SeedTenant(db, tenantId);
        await db.SaveChangesAsync();
        var service = CreateMigrationSignOffService(db, tenantId);

        var decision = await service.GetSubledgerOpeningMigrationDecisionAsync();

        decision.GlTrialBalanceOpeningSupported.Should().BeTrue();
        decision.ApOpeningInvoicesSupportedByGlBatch.Should().BeFalse();
        decision.ArOpeningInvoicesSupportedByGlBatch.Should().BeFalse();
        decision.FixedAssetOpeningRegisterSupportedByGlBatch.Should().BeFalse();
        decision.Scope.Should().Contain(s => s.Area == "AP" && s.BlocksFinalSignOffIfRequired);
        decision.Scope.Should().Contain(s => s.Area == "AR" && s.BlocksFinalSignOffIfRequired);
        decision.Scope.Should().Contain(s => s.Area == "FixedAssets" && s.BlocksFinalSignOffIfRequired);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FinalSignOff")]
    [Trait("Category", "Migration")]
    public async Task FinalSignOffCleanFixture_ShouldPassWithAcceptedLimitations()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        await SeedPostedOpeningBalanceAsync(db, tenantId);
        var service = CreateMigrationSignOffService(db, tenantId, withAudit: true);

        var result = await service.RunFinalMigrationSignOffAsync(CreatePassingSignOffRequest());

        result.Status.Should().Be("PassedWithAcceptedLimitations");
        result.BlockingFindingsCount.Should().Be(0);
        result.Checks.Should().Contain(c => c.Code == "TRIAL-BALANCE-BALANCED" && c.Status == "Passed");
        result.Checks.Should().Contain(c => c.Code == "OPENING-BALANCE-CHECKS" && c.Status == "Passed");
        result.CutoverDataShapeEvaluation.FinLim0048BlocksSignOff.Should().BeFalse();
        result.LimitationAcceptanceMatrix.Should().Contain(i => i.LimitationId == "FIN-LIM-0048" && i.Classification == "NotApplicable");
        (await db.AuditLogs.CountAsync(a => a.TenantId == tenantId && a.Action == FinanceAuditEvents.MigrationSignOffRunCompleted)).Should().Be(1);
        (await db.AuditLogs.CountAsync(a => a.TenantId == tenantId && a.Action == FinanceAuditEvents.MigrationSignOffEvidenceGenerated)).Should().Be(1);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FinalSignOff")]
    [Trait("Category", "Schema")]
    public void CustomerPaymentModel_ShouldNotCreateLegacyCustomerIdShadowColumn()
    {
        using var db = CreateContext();

        var customerPaymentEntity = db.Model.FindEntityType(typeof(CustomerPayment));
        var customerEntity = db.Model.FindEntityType(typeof(ErpSystem.Core.Entities.Sales.Customer));

        customerPaymentEntity.Should().NotBeNull();
        customerPaymentEntity!.FindProperty("CustomerId").Should().NotBeNull();
        customerPaymentEntity.FindProperty("CustomerId1").Should().BeNull();
        customerPaymentEntity.FindNavigation(nameof(CustomerPayment.Customer)).Should().BeNull();
        customerEntity?.FindNavigation("Payments").Should().BeNull();
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FinalSignOff")]
    [Trait("Category", "Schema")]
    public void CurrentArFinanceModel_ShouldUseInvoiceSourceTables()
    {
        using var db = CreateContext();

        db.Model.FindEntityType(typeof(Invoice))?.GetTableName().Should().Be("Invoices");
        db.Model.FindEntityType(typeof(InvoiceLineItem))?.GetTableName().Should().Be("InvoiceLineItem");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FinalSignOff")]
    [Trait("Category", "Migration")]
    public async Task FinalSignOffUnbalancedTrialBalance_ShouldFail()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedPostedOpeningBalanceAsync(db, tenantId);
        db.AccountTransactions.Add(new AccountTransaction
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            JournalEntryId = Guid.NewGuid(),
            AccountId = fixture.Cash.Id,
            FiscalPeriodId = fixture.Period.Id,
            TransactionDate = new DateTime(2026, 1, 10),
            DebitAmount = 1m,
            CreditAmount = 0m,
            PostingStatus = "Posted",
            BookClassification = "IFRS",
            LineNumber = 99,
            FunctionalCurrencyCode = "GHS",
            TransactionCurrency = "GHS",
            SourceModule = "TEST",
            SourceDocumentType = "UnbalancedFixture",
            SourceDocumentId = Guid.NewGuid()
        });
        await db.SaveChangesAsync();
        var service = CreateMigrationSignOffService(db, tenantId);

        var result = await service.RunFinalMigrationSignOffAsync(CreatePassingSignOffRequest());

        result.Status.Should().Be("Failed");
        result.Checks.Should().Contain(c => c.Code == "TRIAL-BALANCE-BALANCED" && c.Severity == "Critical");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FinalSignOff")]
    [Trait("Category", "Migration")]
    public async Task FinalSignOffMissingOpeningBalanceReferences_ShouldFail()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = SeedOpeningBalanceFixture(db, tenantId);
        db.OpeningBalanceBatches.Add(new OpeningBalanceBatch
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            BatchNumber = "OB-NO-REF",
            OpeningDate = new DateTime(2026, 1, 1),
            FiscalPeriodId = fixture.Period.Id,
            BookClassification = "IFRS",
            Status = "Posted",
            TotalDebit = 100m,
            TotalCredit = 100m,
            Difference = 0m,
            IdempotencyKey = Guid.NewGuid().ToString("N")
        });
        await db.SaveChangesAsync();
        var service = CreateMigrationSignOffService(db, tenantId);

        var result = await service.RunFinalMigrationSignOffAsync(CreatePassingSignOffRequest());

        result.Status.Should().Be("Failed");
        result.Checks.Should().Contain(c => c.Code == "OPENING-BALANCE-MISSING-REFERENCES" && c.Severity == "Critical");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FinalSignOff")]
    [Trait("Category", "CashBank")]
    public async Task FinalSignOffBankSnapshotVariance_ShouldFailUnlessAccepted()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedPostedOpeningBalanceAsync(db, tenantId);
        db.BankAccounts.Add(new BankAccount
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            AccountNumber = "BANK-SIGNOFF",
            AccountName = "Sign-off Bank",
            BankName = "Bank",
            Currency = "GHS",
            GLAccountId = fixture.Cash.Id,
            CurrentBalance = 0m,
            AvailableBalance = 0m,
            IsActive = true
        });
        await db.SaveChangesAsync();
        var service = CreateMigrationSignOffService(db, tenantId);

        var failed = await service.RunFinalMigrationSignOffAsync(CreatePassingSignOffRequest());
        var accepted = await service.RunFinalMigrationSignOffAsync(CreatePassingSignOffRequest(acceptBankVariance: true));

        failed.Status.Should().Be("Failed");
        failed.Checks.Should().Contain(c => c.Code == "BANK-SNAPSHOT-VARIANCE" && c.Severity == "Critical");
        accepted.Checks.Should().Contain(c => c.Code == "BANK-SNAPSHOT-VARIANCE" && c.Status == "Warning");
        accepted.BlockingFindingsCount.Should().Be(0);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FinalSignOff")]
    [Trait("Category", "Migration")]
    public async Task FinalSignOffFinLim0048_ShouldBlockWhenSubledgerOpeningsAreRequired()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        await SeedPostedOpeningBalanceAsync(db, tenantId);
        var service = CreateMigrationSignOffService(db, tenantId);
        var request = CreatePassingSignOffRequest();
        request.CutoverDataShape.HasOpenApSupplierInvoices = true;

        var result = await service.RunFinalMigrationSignOffAsync(request);

        result.Status.Should().Be("Failed");
        result.CutoverDataShapeEvaluation.FinLim0048BlocksSignOff.Should().BeTrue();
        result.LimitationAcceptanceMatrix.Should().Contain(i => i.LimitationId == "FIN-LIM-0048" && i.BlocksSignOff);
        result.Checks.Should().Contain(c => c.Code == "FIN-LIM-0048-BLOCKS-CUTOVER");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FinalSignOff")]
    [Trait("Category", "Migration")]
    public async Task FinalSignOffUnacceptedGoLiveLimitation_ShouldFail()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        await SeedPostedOpeningBalanceAsync(db, tenantId);
        var service = CreateMigrationSignOffService(db, tenantId);

        var result = await service.RunFinalMigrationSignOffAsync(new FinalMigrationSignOffRunRequestDto
        {
            AsOfDate = new DateTime(2026, 1, 31),
            NotApplicableLimitationIds = new[] { "FIN-LIM-0048" }
        });

        result.Status.Should().Be("Failed");
        result.LimitationAcceptanceMatrix.Should().Contain(i => i.LimitationId == "FIN-LIM-0009" && i.BlocksSignOff);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FinalSignOff")]
    [Trait("Category", "Migration")]
    public async Task FinalSignOffBackReferenceCandidate_ShouldAppearInDiagnostics()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedPostedOpeningBalanceAsync(db, tenantId);
        var invoice = new VendorInvoice
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            InvoiceNumber = "AP-SIGNOFF-MISSING-JE",
            InvoiceDate = new DateTime(2026, 1, 5),
            DueDate = new DateTime(2026, 1, 31),
            TotalAmount = 100m,
            Status = VendorInvoiceStatus.Approved
        };
        db.VendorInvoices.Add(invoice);
        SeedPostedGl(db, tenantId, fixture.Period.Id, fixture.Cash.Id, fixture.Equity.Id, "AP", "VendorInvoice", invoice.Id, invoice.InvoiceNumber, 100m);
        await db.SaveChangesAsync();
        var service = CreateMigrationSignOffService(db, tenantId);

        var result = await service.RunFinalMigrationSignOffAsync(CreatePassingSignOffRequest());

        result.Status.Should().Be("Failed");
        result.Checks.Should().Contain(c => c.Area == "Posting Back-References" && c.SourceId == invoice.Id);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FinalSignOff")]
    [Trait("Category", "Tax")]
    public async Task FinalSignOffActiveCurrentCovidLevy_ShouldFail()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        await SeedPostedOpeningBalanceAsync(db, tenantId);
        db.Taxes.Add(new Tax
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Code = "COVID",
            Name = "COVID-19 Health Recovery Levy",
            Rate = 1m,
            EffectiveFrom = new DateTime(2026, 1, 1),
            IsActive = true
        });
        await db.SaveChangesAsync();
        var service = CreateMigrationSignOffService(db, tenantId);

        var result = await service.RunFinalMigrationSignOffAsync(CreatePassingSignOffRequest());

        result.Status.Should().Be("Failed");
        result.Checks.Should().Contain(c => c.Code == "TAX-CURRENT-COVID-LEVY-ACTIVE");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FinalSignOff")]
    [Trait("Category", "TenantIsolation")]
    public async Task FinalSignOff_ShouldIgnoreCrossTenantData()
    {
        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        await using var db = CreateContext();
        await SeedPostedOpeningBalanceAsync(db, tenantId);
        var otherFixture = SeedOpeningBalanceFixture(db, otherTenantId);
        db.AccountTransactions.Add(new AccountTransaction
        {
            Id = Guid.NewGuid(),
            TenantId = otherTenantId,
            JournalEntryId = Guid.NewGuid(),
            AccountId = otherFixture.Cash.Id,
            FiscalPeriodId = otherFixture.Period.Id,
            TransactionDate = new DateTime(2026, 1, 10),
            DebitAmount = 999m,
            PostingStatus = "Posted",
            BookClassification = "IFRS",
            LineNumber = 1,
            FunctionalCurrencyCode = "GHS"
        });
        await db.SaveChangesAsync();
        var service = CreateMigrationSignOffService(db, tenantId);

        var result = await service.RunFinalMigrationSignOffAsync(CreatePassingSignOffRequest());

        result.Status.Should().Be("PassedWithAcceptedLimitations");
        result.Checks.Single(c => c.Code == "TRIAL-BALANCE-BALANCED").Amount.Should().Be(0m);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FinalSignOff")]
    [Trait("Category", "Audit")]
    public async Task FinalSignOffReview_ShouldEmitAuditEvent()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        SeedTenant(db, tenantId);
        await db.SaveChangesAsync();
        var service = CreateMigrationSignOffService(db, tenantId, withAudit: true);
        var runId = Guid.NewGuid();

        var result = await service.ReviewSignOffRunAsync(new SignOffReviewRequestDto
        {
            RunId = runId,
            Decision = "ReviewedForDryRun",
            ReviewerComments = "Evidence reviewed for UAT dry-run."
        });

        result.RunId.Should().Be(runId);
        (await db.AuditLogs.CountAsync(a => a.TenantId == tenantId && a.Action == FinanceAuditEvents.MigrationSignOffReviewed)).Should().Be(1);
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"opening-balance-foundation-{Guid.NewGuid()}")
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new ApplicationDbContext(options);
    }

    private static readonly string[] AcceptedSignOffLimitations =
    {
        "FIN-LIM-0009",
        "FIN-LIM-0010",
        "FIN-LIM-0011",
        "FIN-LIM-0013",
        "FIN-LIM-0014",
        "FIN-LIM-0021",
        "FIN-LIM-0022",
        "FIN-LIM-0028",
        "FIN-LIM-0030",
        "FIN-LIM-0031",
        "FIN-LIM-0033",
        "FIN-LIM-0034",
        "FIN-LIM-0035",
        "FIN-LIM-0037",
        "FIN-LIM-0038",
        "FIN-LIM-0039",
        "FIN-LIM-0040",
        "FIN-LIM-0041",
        "FIN-LIM-0042",
        "FIN-LIM-0043",
        "FIN-LIM-0046",
        "FIN-LIM-0047"
    };

    private static FinalMigrationSignOffRunRequestDto CreatePassingSignOffRequest(bool acceptBankVariance = false)
        => new()
        {
            RunType = "DryRun",
            AsOfDate = new DateTime(2026, 1, 31),
            BookClassification = "IFRS",
            AcceptBankSnapshotVariance = acceptBankVariance,
            AcceptedLimitationIds = AcceptedSignOffLimitations,
            NotApplicableLimitationIds = new[] { "FIN-LIM-0048" }
        };

    private static async Task<OpeningBalanceFixture> SeedPostedOpeningBalanceAsync(
        ApplicationDbContext db,
        Guid tenantId)
    {
        var fixture = SeedOpeningBalanceFixture(db, tenantId);
        await db.SaveChangesAsync();
        var service = CreateService(db, tenantId);
        var batch = await service.CreateBatchAsync(CreateBalancedBatch(fixture.Period.Id, fixture.Cash.Id, fixture.Equity.Id));
        await service.SubmitForApprovalAsync(batch.Id);
        await ApproveBatchAsync(db, batch.Id);
        await service.PostAsync(batch.Id);
        return fixture;
    }

    private static OpeningBalanceService CreateService(
        ApplicationDbContext db,
        Guid tenantId,
        bool withAudit = false,
        IWorkflowService? workflow = null,
        IFinanceAuditService? auditOverride = null,
        ILogger<OpeningBalanceService>? logger = null,
        bool withoutWorkflow = false)
    {
        var currentUser = CreateCurrentUser(tenantId);
        var audit = auditOverride ?? (withAudit
            ? new FinanceAuditService(db, currentUser.Object, new HttpContextAccessor())
            : null);
        var postingEngine = new FinancePostingEngine(
            db,
            currentUser.Object,
            Mock.Of<ILogger<FinancePostingEngine>>(),
            audit);

        return new OpeningBalanceService(
            db,
            currentUser.Object,
            postingEngine,
            audit,
            withoutWorkflow ? null : workflow ?? CreatePendingOpeningWorkflow(),
            logger,
            reversalPolicyService: new FinanceReversalPolicyService(db, currentUser.Object));
    }

    private static IWorkflowService CreateCompletedOpeningWorkflow()
    {
        var workflow = new Mock<IWorkflowService>();
        workflow.Setup(service => service.StartApprovalWorkflowAsync("OpeningBalanceBatch", It.IsAny<Guid>()))
            .ReturnsAsync(new WorkflowExecutionResult
            {
                Success = true,
                Status = WorkflowInstanceStatus.Completed,
                WorkflowInstanceId = Guid.NewGuid()
            });
        return workflow.Object;
    }

    private static IWorkflowService CreatePendingOpeningWorkflow()
    {
        var workflow = new Mock<IWorkflowService>();
        workflow.Setup(service => service.StartApprovalWorkflowAsync("OpeningBalanceBatch", It.IsAny<Guid>()))
            .ReturnsAsync(new WorkflowExecutionResult
            {
                Success = true,
                Status = WorkflowInstanceStatus.InProgress,
                WorkflowInstanceId = Guid.NewGuid()
            });
        return workflow.Object;
    }

    private static async Task ApproveBatchAsync(ApplicationDbContext db, Guid batchId)
    {
        var batch = await db.OpeningBalanceBatches.SingleAsync(item => item.Id == batchId);
        batch.Status = "Approved";
        batch.ApprovedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }

    private static MigrationSignOffService CreateMigrationSignOffService(
        ApplicationDbContext db,
        Guid tenantId,
        bool withAudit = false)
    {
        var currentUser = CreateCurrentUser(tenantId);
        var audit = withAudit
            ? new FinanceAuditService(db, currentUser.Object, new HttpContextAccessor())
            : null;

        return new MigrationSignOffService(db, currentUser.Object, audit);
    }

    private static BankAccountService CreateBankAccountService(ApplicationDbContext db, Guid tenantId)
    {
        var currentUser = CreateCurrentUser(tenantId);
        var tenantSettings = new Mock<ITenantSettingsService>();
        tenantSettings.Setup(x => x.GetBaseCurrencyAsync()).ReturnsAsync("GHS");
        return new BankAccountService(db, tenantSettings.Object, currentUser.Object);
    }

    private static Mock<ICurrentUserService> CreateCurrentUser(Guid tenantId)
    {
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(x => x.TenantId).Returns(tenantId);
        currentUser.SetupGet(x => x.Claims).Returns(new Dictionary<string, string>());
        currentUser.SetupGet(x => x.UserId).Returns(Guid.NewGuid().ToString());
        currentUser.SetupGet(x => x.UserName).Returns("migration.controller");
        currentUser.SetupGet(x => x.IpAddress).Returns("127.0.0.1");
        currentUser.SetupGet(x => x.UserAgent).Returns("opening-balance-tests");
        return currentUser;
    }

    private static OpeningBalanceFixture SeedOpeningBalanceFixture(
        ApplicationDbContext db,
        Guid tenantId,
        bool isOpen = true,
        bool isClosed = false)
    {
        SeedTenant(db, tenantId);
        var period = SeedPeriod(db, tenantId, isOpen, isClosed);
        var cash = SeedAccount(db, tenantId, "1000", AccountType.Asset);
        var equity = SeedAccount(db, tenantId, "3000", AccountType.Equity);
        var supplierAdvance = SeedAccount(db, tenantId, "1200", AccountType.Asset);
        var customerAdvance = SeedAccount(db, tenantId, "2200", AccountType.Liability);
        var whtPayable = SeedAccount(db, tenantId, "2300", AccountType.Liability);
        var whtReceivable = SeedAccount(db, tenantId, "1300", AccountType.Asset);
        var supplier = new Supplier
        {
            Id = Guid.NewGuid(), TenantId = tenantId, SupplierCode = $"SUP-{tenantId:N}"[..12],
            Name = "TDC cutover supplier", IsActive = true, Status = "Active", TaxId = "TDC-SUP-TIN"
        };
        var customer = new BusinessPartner
        {
            Id = Guid.NewGuid(), TenantId = tenantId, PartnerCode = $"CUS-{tenantId:N}"[..12],
            CustomerAccountNumber = $"AR-{tenantId:N}"[..12], PartnerName = "TDC cutover customer",
            PartnerType = "Customer", RegistrationStatus = "Active", ApprovalStatus = "Approved",
            IsActive = true, Currency = "GHS"
        };
        var withholdingTax = new Tax
        {
            Id = Guid.NewGuid(), TenantId = tenantId, Code = "WHT-SVC", Name = "Services WHT",
            Rate = 7.5m, EffectiveFrom = new DateTime(2025, 1, 1), Category = TaxCategory.Withholding,
            Applicability = TaxApplicability.Both, IsActive = true,
            TaxPayableAccountId = whtPayable.Id, TaxReceivableAccountId = whtReceivable.Id
        };
        db.Suppliers.Add(supplier);
        db.BusinessPartners.Add(customer);
        db.Taxes.Add(withholdingTax);
        db.FinanceSettings.Add(new FinanceSettings
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            BaseCurrency = "GHS",
            MigrationClearingAccountId = equity.Id,
            SupplierAdvanceAccountId = supplierAdvance.Id,
            CustomerAdvanceAccountId = customerAdvance.Id,
            ReferenceNumber = "FIN-SETTINGS",
            Status = "Active"
        });
        return new OpeningBalanceFixture(
            period, cash, equity, supplierAdvance, customerAdvance, whtPayable, whtReceivable,
            supplier, customer, withholdingTax);
    }

    private static FixedAssetOpeningFixture SeedFixedAssetOpeningFixture(
        ApplicationDbContext db,
        Guid tenantId,
        Account assetAccount,
        Account migrationClearingAccount)
    {
        var accumulatedDepreciationAccount = SeedAccount(db, tenantId, $"19{Guid.NewGuid():N}"[..4], AccountType.Asset);
        var depreciationExpenseAccount = SeedAccount(db, tenantId, $"61{Guid.NewGuid():N}"[..4], AccountType.Expense);
        var book = new AccountingBook
        {
            Id = Guid.NewGuid(), TenantId = tenantId, Code = "IFRS", Name = "IFRS", IsDefault = true, AllowsPosting = true
        };
        var category = new FixedAssetCategory
        {
            Id = Guid.NewGuid(), TenantId = tenantId, Code = $"FA-{Guid.NewGuid():N}"[..8], Name = "Opening assets",
            AssetAccountId = assetAccount.Id,
            AccumulatedDepreciationAccountId = accumulatedDepreciationAccount.Id,
            DepreciationExpenseAccountId = depreciationExpenseAccount.Id
        };
        var asset = new FixedAsset
        {
            Id = Guid.NewGuid(), TenantId = tenantId, AssetCode = $"FA-{Guid.NewGuid():N}"[..12], Name = "Imported opening asset",
            FixedAssetCategoryId = category.Id, Category = category,
            PurchaseDate = new DateTime(2024, 1, 1), PlacedInServiceDate = new DateTime(2024, 1, 1),
            PurchasePrice = 1_000m, AcquisitionCost = 1_000m, NetBookValue = 800m,
            UsefulLifeMonths = 120, Status = FixedAssetStatus.Active
        };
        var bookValue = new FixedAssetBookValue
        {
            Id = Guid.NewGuid(), TenantId = tenantId, FixedAssetId = asset.Id, FixedAsset = asset,
            AccountingBookId = book.Id, AccountingBook = book, BookClassification = "IFRS",
            AcquisitionCost = 1_000m, AccumulatedDepreciation = 200m, NetBookValue = 800m,
            UsefulLifeMonths = 120, RemainingUsefulLifeMonths = 96,
            OpeningAsOfDate = new DateTime(2026, 1, 1), OpeningSource = "OpeningImport"
        };
        asset.BookValues.Add(bookValue);
        db.AccountingBooks.Add(book);
        db.FixedAssetCategories.Add(category);
        db.FixedAssets.Add(asset);
        var actorId = Guid.NewGuid();
        db.AssetTransactions.AddRange(
            new AssetTransaction
            {
                Id = Guid.NewGuid(), TenantId = tenantId, FixedAssetId = asset.Id, AccountingBookId = book.Id,
                BookClassification = "IFRS", TransactionDate = new DateTime(2026, 1, 1),
                TransactionType = "Opening Acquisition", Amount = 1_000m, ResultingBookValue = 1_000m,
                PerformedByUserId = actorId
            },
            new AssetTransaction
            {
                Id = Guid.NewGuid(), TenantId = tenantId, FixedAssetId = asset.Id, AccountingBookId = book.Id,
                BookClassification = "IFRS", TransactionDate = new DateTime(2026, 1, 1),
                TransactionType = "Opening Accumulated Depreciation", Amount = 200m, ResultingBookValue = 800m,
                PerformedByUserId = actorId
            });
        return new FixedAssetOpeningFixture(asset, bookValue, assetAccount, accumulatedDepreciationAccount, migrationClearingAccount);
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
    }

    private static FiscalPeriod SeedPeriod(ApplicationDbContext db, Guid tenantId, bool isOpen, bool isClosed)
    {
        var period = new FiscalPeriod
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            FiscalYearId = Guid.NewGuid(),
            PeriodName = "Opening 2026",
            PeriodCode = "2026-OPEN",
            PeriodNumber = 1,
            PeriodType = PeriodType.Monthly,
            StartDate = new DateTime(2026, 1, 1),
            EndDate = new DateTime(2026, 1, 31),
            PeriodDays = 31,
            PeriodStatus = isClosed ? "Closed" : isOpen ? "Open" : "Future",
            IsOpen = isOpen,
            IsClosed = isClosed,
            IsLocked = false
        };
        db.FiscalPeriods.Add(period);
        return period;
    }

    private static Account SeedAccount(
        ApplicationDbContext db,
        Guid tenantId,
        string accountNumber,
        AccountType type,
        AccountStatus status = AccountStatus.Active)
    {
        var account = new Account
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            AccountCode = accountNumber,
            AccountNumber = accountNumber,
            AccountName = $"Account {accountNumber}",
            AccountType = type,
            CurrencyCode = "GHS",
            Status = status,
            AllowDirectPosting = true
        };
        db.Accounts.Add(account);
        return account;
    }

    private static (JournalEntry Journal, FinancePostingEvent PostingEvent) SeedPostedGl(
        ApplicationDbContext db,
        Guid tenantId,
        Guid fiscalPeriodId,
        Guid debitAccountId,
        Guid creditAccountId,
        string sourceModule,
        string sourceDocumentType,
        Guid sourceDocumentId,
        string reference,
        decimal amount,
        DateTime? transactionDate = null,
        string bookClassification = "IFRS")
    {
        var entryDate = (transactionDate ?? new DateTime(2026, 1, 5)).Date;
        var journal = new JournalEntry
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            JournalEntryNumber = $"JE-{reference}-{Guid.NewGuid():N}"[..30],
            JournalType = "Subledger",
            EntryDate = entryDate,
            Description = reference,
            FiscalPeriodId = fiscalPeriodId,
            PostingStatus = "Posted",
            BookClassification = bookClassification,
            SourceModule = sourceModule,
            SourceDocumentType = sourceDocumentType,
            SourceDocumentId = sourceDocumentId,
            TotalDebitAmount = amount,
            TotalCreditAmount = amount,
            IsBalanced = true
        };
        db.JournalEntries.Add(journal);

        db.AccountTransactions.Add(new AccountTransaction
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            JournalEntryId = journal.Id,
            AccountId = debitAccountId,
            FiscalPeriodId = fiscalPeriodId,
            TransactionDate = journal.EntryDate,
            DebitAmount = amount,
            CreditAmount = 0m,
            PostingStatus = "Posted",
            BookClassification = bookClassification,
            LineNumber = 1,
            FunctionalCurrencyCode = "GHS",
            TransactionCurrency = "GHS",
            SourceModule = sourceModule,
            SourceDocumentType = sourceDocumentType,
            SourceDocumentId = sourceDocumentId,
            SourceReferenceNumber = reference
        });
        db.AccountTransactions.Add(new AccountTransaction
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            JournalEntryId = journal.Id,
            AccountId = creditAccountId,
            FiscalPeriodId = fiscalPeriodId,
            TransactionDate = journal.EntryDate,
            DebitAmount = 0m,
            CreditAmount = amount,
            PostingStatus = "Posted",
            BookClassification = bookClassification,
            LineNumber = 2,
            FunctionalCurrencyCode = "GHS",
            TransactionCurrency = "GHS",
            SourceModule = sourceModule,
            SourceDocumentType = sourceDocumentType,
            SourceDocumentId = sourceDocumentId,
            SourceReferenceNumber = reference
        });

        var postingEvent = new FinancePostingEvent
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            SourceModule = sourceModule,
            SourceDocumentType = sourceDocumentType,
            SourceDocumentId = sourceDocumentId,
            SourceDocumentReference = reference,
            PostingAction = "Post",
            PostingStatus = "Posted",
            PostingDate = journal.EntryDate,
            PostedAt = DateTime.UtcNow,
            JournalEntryId = journal.Id,
            FunctionalCurrencyCode = "GHS",
            TotalDebitAmount = amount,
            TotalCreditAmount = amount,
            BookClassification = bookClassification
        };
        db.FinancePostingEvents.Add(postingEvent);
        return (journal, postingEvent);
    }

    private static CreateOpeningBalanceBatchDto CreateBalancedBatch(Guid periodId, Guid debitAccountId, Guid creditAccountId)
        => new()
        {
            BatchNumber = $"OB-{Guid.NewGuid():N}"[..18],
            OpeningDate = new DateTime(2026, 1, 1),
            FiscalPeriodId = periodId,
            BookClassification = "IFRS",
            Lines = new[]
            {
                new CreateOpeningBalanceLineDto { AccountId = debitAccountId, DebitAmount = 100m },
                new CreateOpeningBalanceLineDto { AccountId = creditAccountId, CreditAmount = 100m }
            }
        };

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "src")) &&
                Directory.Exists(Path.Combine(directory.FullName, "tests")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Repository root could not be located.");
    }

    private sealed record OpeningBalanceFixture(
        FiscalPeriod Period,
        Account Cash,
        Account Equity,
        Account SupplierAdvance,
        Account CustomerAdvance,
        Account WhtPayable,
        Account WhtReceivable,
        Supplier Supplier,
        BusinessPartner Customer,
        Tax WithholdingTax);
    private sealed record FixedAssetOpeningFixture(
        FixedAsset Asset,
        FixedAssetBookValue BookValue,
        Account AssetAccount,
        Account AccumulatedDepreciationAccount,
        Account MigrationClearingAccount);
}
