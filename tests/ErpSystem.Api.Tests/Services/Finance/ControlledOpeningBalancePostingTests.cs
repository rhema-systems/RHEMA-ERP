using ErpSystem.Api.Services.Finance.Cash;
using ErpSystem.Api.Services.Finance;
using ErpSystem.Api.Services.Finance.GL;
using ErpSystem.Api.Services.Finance.Migration;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.DTOs.Workflow;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Finance.FixedAssets;
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
        var posted = await service.PostAsync(batch.Id);

        posted.Status.Should().Be("Posted");
        posted.JournalEntryId.Should().NotBeNull();
        posted.PostingEventId.Should().NotBeNull();
        (await db.JournalEntries.CountAsync(j => j.TenantId == tenantId && j.SourceDocumentType == "OpeningBalanceBatch")).Should().Be(1);
        (await db.FinancePostingEvents.CountAsync(e => e.TenantId == tenantId && e.SourceDocumentType == "OpeningBalanceBatch")).Should().Be(1);
        (await db.AccountTransactions.CountAsync(t => t.TenantId == tenantId && t.SourceDocumentType == "OpeningBalanceBatch")).Should().Be(2);

        var cash = await db.Accounts.SingleAsync(a => a.Id == fixture.Cash.Id);
        var equity = await db.Accounts.SingleAsync(a => a.Id == fixture.Equity.Id);
        // Opening balances also maintain Account.Balance as a read-side snapshot through the posting engine.
        cash.Balance.Should().Be(100m);
        equity.Balance.Should().Be(100m);
        (await db.AuditLogs.CountAsync(a => a.TenantId == tenantId && a.Action == FinanceAuditEvents.OpeningBalancePosted)).Should().Be(1);
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

        var batch = await service.CreateBatchAsync(CreateBalancedBatch(fixture.Period.Id, fixture.Cash.Id, otherAccount.Id));
        var result = await service.ValidateBatchAsync(batch.Id);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("belongs to another tenant", StringComparison.OrdinalIgnoreCase)
            || e.Contains("missing", StringComparison.OrdinalIgnoreCase));
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

        var batch = await service.CreateBatchAsync(CreateBalancedBatch(fixture.Period.Id, fixture.Cash.Id, fixture.Equity.Id));
        var result = await service.ValidateBatchAsync(batch.Id);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("does not allow direct posting", StringComparison.OrdinalIgnoreCase));
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

        var post = () => service.PostAsync(batch.Id);
        await post.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*must be approved before posting*");
        (await db.OpeningBalanceBatches.SingleAsync(b => b.Id == batch.Id)).Status.Should().Be("Failed");
        (await db.FinancePostingEvents.CountAsync()).Should().Be(0);
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
        validation.Errors.Should().Contain(error => error.Contains("foreign-currency opening balances are not supported", StringComparison.OrdinalIgnoreCase));
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
            error.Contains("foreign-currency opening balances are not supported", StringComparison.OrdinalIgnoreCase));
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
    public async Task BankNonzeroOpeningBalance_ShouldRemainBlockedUnlessPostedThroughOpeningBalanceFlow()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        SeedTenant(db, tenantId);
        var glAccount = SeedAccount(db, tenantId, "1000", AccountType.Asset);
        await db.SaveChangesAsync();
        var service = CreateBankAccountService(db, tenantId);

        var act = () => service.CreateAsync(new CreateBankAccountDto
        {
            AccountNumber = "BANK-001",
            AccountName = "Tenant Bank",
            BankName = "Bank",
            Currency = "GHS",
            AccountType = BankAccountType.Checking,
            GLAccountId = glAccount.Id,
            OpeningDate = new DateTime(2026, 1, 1),
            OpeningBalance = 100m
        });

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*FIN-LIM-0006 opening-balance migration batch*");
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
        await service.PostAsync(batch.Id);
        return fixture;
    }

    private static OpeningBalanceService CreateService(
        ApplicationDbContext db,
        Guid tenantId,
        bool withAudit = false,
        IWorkflowService? workflow = null)
    {
        var currentUser = CreateCurrentUser(tenantId);
        var audit = withAudit
            ? new FinanceAuditService(db, currentUser.Object, new HttpContextAccessor())
            : null;
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
            workflow);
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
        db.FinanceSettings.Add(new FinanceSettings
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            BaseCurrency = "GHS",
            MigrationClearingAccountId = equity.Id,
            ReferenceNumber = "FIN-SETTINGS",
            Status = "Active"
        });
        return new OpeningBalanceFixture(period, cash, equity);
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
        decimal amount)
    {
        var journal = new JournalEntry
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            JournalEntryNumber = $"JE-{reference}-{Guid.NewGuid():N}"[..30],
            JournalType = "Subledger",
            EntryDate = new DateTime(2026, 1, 5),
            Description = reference,
            FiscalPeriodId = fiscalPeriodId,
            PostingStatus = "Posted",
            BookClassification = "IFRS",
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
            BookClassification = "IFRS",
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
            BookClassification = "IFRS",
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
            BookClassification = "IFRS"
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

    private sealed record OpeningBalanceFixture(FiscalPeriod Period, Account Cash, Account Equity);
    private sealed record FixedAssetOpeningFixture(
        FixedAsset Asset,
        FixedAssetBookValue BookValue,
        Account AssetAccount,
        Account AccumulatedDepreciationAccount,
        Account MigrationClearingAccount);
}
