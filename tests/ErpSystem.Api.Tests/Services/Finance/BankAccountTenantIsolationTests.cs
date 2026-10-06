using ErpSystem.Api.Services.Finance.Cash;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Finance;

public sealed class BankAccountTenantIsolationTests
{
    [Fact]
    [Trait("Batch", "FinanceGoLive-3")]
    [Trait("Category", "TenantIsolation")]
    public async Task GetAllAsync_ShouldExcludeOtherTenantBankAccounts()
    {
        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        await using var db = CreateContext();
        db.BankAccounts.Add(CreateBankAccount(tenantId, "BANK-001", "Current Account"));
        db.BankAccounts.Add(CreateBankAccount(otherTenantId, "BANK-002", "Other Tenant Account"));
        await db.SaveChangesAsync();

        var service = CreateService(db, tenantId);

        var accounts = (await service.GetAllAsync()).ToList();

        accounts.Should().ContainSingle();
        accounts[0].AccountNumber.Should().Be("BANK-001");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-BankReconciliation")]
    [Trait("Category", "CashBank")]
    public async Task GetActiveAccountsAsync_ShouldIncludeLinkedGlAccountForReconciliationPosting()
    {
        var tenantId = Guid.NewGuid();
        var glAccountId = Guid.NewGuid();
        var bankAccount = CreateBankAccount(tenantId, "BANK-001", "Current Account");
        bankAccount.GLAccountId = glAccountId;
        await using var db = CreateContext();
        db.BankAccounts.Add(bankAccount);
        await db.SaveChangesAsync();

        var accounts = (await CreateService(db, tenantId).GetActiveAccountsAsync()).ToList();

        accounts.Should().ContainSingle();
        accounts[0].GLAccountId.Should().Be(glAccountId);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-3")]
    [Trait("Category", "TenantIsolation")]
    public async Task GetByIdAsync_ShouldReturnNull_ForOtherTenantBankAccount()
    {
        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        var otherTenantAccount = CreateBankAccount(otherTenantId, "BANK-002", "Other Tenant Account");
        await using var db = CreateContext();
        db.BankAccounts.Add(otherTenantAccount);
        await db.SaveChangesAsync();

        var service = CreateService(db, tenantId);

        var account = await service.GetByIdAsync(otherTenantAccount.Id);

        account.Should().BeNull();
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-CashScope")]
    [Trait("Category", "CashBank")]
    public async Task GetAllAsync_ShouldOnlyReturnBankAccountsPermittedByFinanceScope()
    {
        var tenantId = Guid.NewGuid();
        var permitted = CreateBankAccount(tenantId, "BANK-001", "Permitted Account");
        var restricted = CreateBankAccount(tenantId, "BANK-002", "Restricted Account");
        await using var db = CreateContext();
        db.BankAccounts.AddRange(permitted, restricted);
        await db.SaveChangesAsync();

        var scope = new Mock<IFinanceAccessScopeService>();
        scope.Setup(service => service.GetPermittedBankAccountIdsAsync(
                FinanceAccessLevel.Read,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { permitted.Id });

        var accounts = (await CreateService(db, tenantId, scope.Object).GetAllAsync()).ToList();

        accounts.Should().ContainSingle().Which.Id.Should().Be(permitted.Id);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-CashScope")]
    [Trait("Category", "CashBank")]
    public async Task UpdateBalanceAsync_ShouldRejectDirectSnapshotMutation()
    {
        var tenantId = Guid.NewGuid();
        var bank = CreateBankAccount(tenantId, "BANK-001", "Operating Account");
        await using var db = CreateContext();
        db.BankAccounts.Add(bank);
        await db.SaveChangesAsync();

        var action = () => CreateService(db, tenantId).UpdateBalanceAsync(bank.Id, 50m, isDebit: false);

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Direct bank-balance mutation is disabled*");
        (await db.BankAccounts.SingleAsync()).CurrentBalance.Should().Be(0m);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-3")]
    [Trait("Category", "TenantIsolation")]
    public async Task CreateAsync_ShouldRejectOtherTenantGlAccount()
    {
        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        var otherTenantGlAccountId = Guid.NewGuid();
        await using var db = CreateContext();
        db.Accounts.Add(new Account
        {
            Id = otherTenantGlAccountId,
            TenantId = otherTenantId,
            AccountCode = "CASH-OTHER",
            AccountNumber = "1000",
            AccountName = "Other Tenant Cash",
            AccountType = AccountType.Asset,
            Status = AccountStatus.Active
        });
        await db.SaveChangesAsync();

        var service = CreateService(db, tenantId);
        var dto = new CreateBankAccountDto
        {
            AccountNumber = "BANK-001",
            AccountName = "Tenant Bank",
            BankName = "Bank",
            Currency = "GHS",
            AccountType = BankAccountType.Checking,
            GLAccountId = otherTenantGlAccountId
        };

        var act = () => service.CreateAsync(dto);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("The bank GL account was not found for this tenant.");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-3")]
    [Trait("Category", "CashBank")]
    public async Task CreateAsync_ShouldRejectSingleCurrencyGlMismatch()
    {
        var tenantId = Guid.NewGuid();
        var usdGl = new Account
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            AccountCode = "BANK-USD",
            AccountNumber = "1001-USD",
            AccountName = "USD Bank GL",
            AccountType = AccountType.Asset,
            CurrencyCode = "USD",
            IsMultiCurrency = false,
            Status = AccountStatus.Active
        };
        await using var db = CreateContext();
        db.Accounts.Add(usdGl);
        await db.SaveChangesAsync();
        var service = CreateService(db, tenantId);

        Func<Task> act = () => service.CreateAsync(new CreateBankAccountDto
        {
            AccountNumber = "BANK-GHS-001",
            AccountName = "GHS Operating Bank",
            BankName = "Test Bank",
            Currency = "GHS",
            AccountType = BankAccountType.Checking,
            GLAccountId = usdGl.Id
        });

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("The bank account currency (GHS) must match the GL account currency (USD).");
        (await db.BankAccounts.CountAsync()).Should().Be(0);
        (await db.LiquidityAccounts.CountAsync()).Should().Be(0);
    }

    [Theory]
    [InlineData("liability", "*must be an Asset account*")]
    [InlineData("inactive", "*currently effective and active*")]
    [InlineData("future", "*currently effective and active*")]
    [InlineData("expired", "*currently effective and active*")]
    [InlineData("non-posting", "*protected control account or allow direct posting*")]
    [Trait("Batch", "FinanceGoLive-3")]
    [Trait("Category", "CashBank")]
    public async Task CreateAsync_ShouldRejectGlThatCannotRepresentEffectiveBankLiquidity(
        string scenario,
        string expectedMessage)
    {
        var tenantId = Guid.NewGuid();
        var glAccount = new Account
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            AccountCode = $"BANK-{scenario}",
            AccountNumber = $"1100-{scenario}",
            AccountName = $"Bank {scenario}",
            AccountType = scenario == "liability" ? AccountType.Liability : AccountType.Asset,
            CurrencyCode = "GHS",
            Status = scenario == "inactive" ? AccountStatus.Inactive : AccountStatus.Active,
            EffectiveDate = scenario == "future" ? DateTime.UtcNow.AddDays(1) : null,
            ExpirationDate = scenario == "expired" ? DateTime.UtcNow.AddMinutes(-1) : null,
            AllowDirectPosting = scenario != "non-posting",
            IsControlAccount = false
        };
        await using var db = CreateContext();
        db.Accounts.Add(glAccount);
        await db.SaveChangesAsync();

        var action = () => CreateService(db, tenantId).CreateAsync(new CreateBankAccountDto
        {
            AccountNumber = "BANK-GHS-001",
            AccountName = "GHS Operating Bank",
            BankName = "Test Bank",
            Currency = "GHS",
            AccountType = BankAccountType.Checking,
            GLAccountId = glAccount.Id
        });

        await action.Should().ThrowAsync<InvalidOperationException>().WithMessage(expectedMessage);
        (await db.BankAccounts.CountAsync()).Should().Be(0);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-3")]
    [Trait("Category", "CashBank")]
    public async Task DeleteAsync_ShouldFailClosedWhileLinkedLiquidityAccountIsActive()
    {
        var tenantId = Guid.NewGuid();
        var bank = CreateBankAccount(tenantId, "BANK-001", "Operating Bank");
        var glAccountId = Guid.NewGuid();
        bank.GLAccountId = glAccountId;
        await using var db = CreateContext();
        db.BankAccounts.Add(bank);
        db.LiquidityAccounts.Add(new LiquidityAccount
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Code = "BANK-001",
            Name = "Operating Bank",
            AccountType = LiquidityAccountType.Bank,
            Currency = "GHS",
            GLAccountId = glAccountId,
            BankAccountId = bank.Id,
            IsActive = true,
            IsSystemAccount = true
        });
        await db.SaveChangesAsync();

        var action = () => CreateService(db, tenantId).DeleteAsync(bank.Id);

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Deactivate this bank account before deleting it; Finance will also deactivate its linked Bank liquidity account.");
        (await db.BankAccounts.SingleAsync()).IsDeleted.Should().BeFalse();
        (await db.LiquidityAccounts.SingleAsync()).IsActive.Should().BeTrue();
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-GovernedOpeningSources")]
    [Trait("Category", "CashBank")]
    public async Task GovernedBankOpeningEvidence_ShouldLockGlRemapDeactivationAndDeletion()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var originalGl = CreateGlAccount(tenantId, "BANK-GL-1");
        var replacementGl = CreateGlAccount(tenantId, "BANK-GL-2");
        var bank = CreateBankAccount(tenantId, "BANK-GOV-001", "Governed Bank");
        bank.GLAccountId = originalGl.Id;
        db.Accounts.AddRange(originalGl, replacementGl);
        db.BankAccounts.Add(bank);
        AddGovernedOpeningEvidence(db, tenantId, bank, originalGl, "Draft");
        await db.SaveChangesAsync();
        var service = CreateService(db, tenantId);

        await FluentActions.Awaiting(() => service.UpdateAsync(bank.Id, new UpdateBankAccountDto
            {
                AccountName = bank.AccountName,
                BankName = bank.BankName,
                GLAccountId = replacementGl.Id,
                IsActive = true
            }))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*GL mapping and active state are locked*");
        await FluentActions.Awaiting(() => service.UpdateAsync(bank.Id, new UpdateBankAccountDto
            {
                AccountName = bank.AccountName,
                BankName = bank.BankName,
                GLAccountId = originalGl.Id,
                IsActive = false
            }))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*GL mapping and active state are locked*");
        await FluentActions.Awaiting(() => service.DeleteAsync(bank.Id))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*active or posted governed opening evidence and cannot be deleted*");

        var persisted = await db.BankAccounts.SingleAsync(item => item.Id == bank.Id);
        persisted.GLAccountId.Should().Be(originalGl.Id);
        persisted.IsActive.Should().BeTrue();
        persisted.IsDeleted.Should().BeFalse();
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-GovernedOpeningSources")]
    [Trait("Category", "CashBank")]
    public async Task TerminalUnpostedRejectedOpening_ShouldReleaseBankMasterForCorrectionAndDeletion()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var originalGl = CreateGlAccount(tenantId, "BANK-GL-R1");
        var replacementGl = CreateGlAccount(tenantId, "BANK-GL-R2");
        var bank = CreateBankAccount(tenantId, "BANK-REJECTED-001", "Rejected Bank");
        bank.GLAccountId = originalGl.Id;
        db.Accounts.AddRange(originalGl, replacementGl);
        db.BankAccounts.Add(bank);
        AddGovernedOpeningEvidence(db, tenantId, bank, originalGl, "Rejected");
        await db.SaveChangesAsync();
        var service = CreateService(db, tenantId);

        var updated = await service.UpdateAsync(bank.Id, new UpdateBankAccountDto
        {
            AccountName = "Corrected rejected bank",
            BankName = bank.BankName,
            GLAccountId = replacementGl.Id,
            IsActive = false
        });
        await service.DeleteAsync(bank.Id);

        updated.GLAccountId.Should().Be(replacementGl.Id);
        updated.IsActive.Should().BeFalse();
        (await db.BankAccounts.IgnoreQueryFilters().SingleAsync(item => item.Id == bank.Id)).IsDeleted.Should().BeTrue();
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-GovernedOpeningSources")]
    [Trait("Category", "CashBank")]
    public async Task RejectedOpeningWithPostingBacklink_ShouldRemainBankMasterBlockingEvidence()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var gl = CreateGlAccount(tenantId, "BANK-GL-CORRUPT");
        var bank = CreateBankAccount(tenantId, "BANK-CORRUPT-001", "Corrupt Rejected Bank");
        bank.GLAccountId = gl.Id;
        db.Accounts.Add(gl);
        db.BankAccounts.Add(bank);
        var batch = AddGovernedOpeningEvidence(db, tenantId, bank, gl, "Rejected");
        batch.JournalEntryId = Guid.NewGuid();
        await db.SaveChangesAsync();

        await FluentActions.Awaiting(() => CreateService(db, tenantId).UpdateAsync(bank.Id, new UpdateBankAccountDto
            {
                AccountName = bank.AccountName,
                BankName = bank.BankName,
                GLAccountId = gl.Id,
                IsActive = false
            }))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*GL mapping and active state are locked*");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-GovernedOpeningSources")]
    [Trait("Category", "CashBank")]
    public async Task RejectedOpeningWithEventOnlyPostingEvidence_ShouldKeepBankMasterLocked()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var gl = CreateGlAccount(tenantId, "BANK-GL-EVENT-ONLY");
        var bank = CreateBankAccount(tenantId, "BANK-EVENT-ONLY-001", "Event-only Rejected Bank");
        bank.GLAccountId = gl.Id;
        db.Accounts.Add(gl);
        db.BankAccounts.Add(bank);
        var batch = AddGovernedOpeningEvidence(db, tenantId, bank, gl, "Rejected");
        db.FinancePostingEvents.Add(new FinancePostingEvent
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            SourceModule = "MIGRATION",
            SourceDocumentType = "OpeningBalanceBatch",
            SourceDocumentId = batch.Id,
            SourceDocumentReference = batch.BatchNumber,
            PostingAction = "Post",
            PostingStatus = "Posted",
            PostingDate = batch.OpeningDate,
            PostedAt = DateTime.UtcNow,
            FunctionalCurrencyCode = "GHS",
            TotalDebitAmount = 100m,
            TotalCreditAmount = 100m,
            BookClassification = batch.BookClassification
        });
        await db.SaveChangesAsync();

        batch.JournalEntryId.Should().BeNull();
        batch.PostingEventId.Should().BeNull();
        batch.PostedAt.Should().BeNull();
        var service = CreateService(db, tenantId);

        await FluentActions.Awaiting(() => service.UpdateAsync(bank.Id, new UpdateBankAccountDto
            {
                AccountName = bank.AccountName,
                BankName = bank.BankName,
                GLAccountId = gl.Id,
                IsActive = false
            }))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*GL mapping and active state are locked*");
        await FluentActions.Awaiting(() => service.DeleteAsync(bank.Id))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*cannot be deleted*");

        var persisted = await db.BankAccounts.IgnoreQueryFilters().SingleAsync(item => item.Id == bank.Id);
        persisted.IsActive.Should().BeTrue();
        persisted.IsDeleted.Should().BeFalse();
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"bank-account-tenant-isolation-{Guid.NewGuid()}")
            .Options;

        return new ApplicationDbContext(options);
    }

    private static BankAccountService CreateService(
        ApplicationDbContext db,
        Guid tenantId,
        IFinanceAccessScopeService? financeAccessScopeService = null)
    {
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(x => x.TenantId).Returns(tenantId);
        currentUser.SetupGet(x => x.Claims).Returns(new Dictionary<string, string>());
        currentUser.SetupGet(x => x.UserName).Returns("finance.test");

        var tenantSettings = new Mock<ITenantSettingsService>();
        tenantSettings.Setup(x => x.GetBaseCurrencyAsync()).ReturnsAsync("GHS");

        return new BankAccountService(
            db,
            tenantSettings.Object,
            currentUser.Object,
            financeAccessScopeService);
    }

    private static BankAccount CreateBankAccount(Guid tenantId, string number, string name)
        => new()
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            AccountNumber = number,
            AccountName = name,
            BankName = "Test Bank",
            Currency = "GHS",
            AccountType = BankAccountType.Checking,
            IsActive = true,
            OpeningDate = DateTime.UtcNow
        };

    private static Account CreateGlAccount(Guid tenantId, string code)
        => new()
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            AccountCode = code,
            AccountNumber = code,
            AccountName = code,
            AccountType = AccountType.Asset,
            CurrencyCode = "GHS",
            Status = AccountStatus.Active,
            AllowDirectPosting = true
        };

    private static OpeningBalanceBatch AddGovernedOpeningEvidence(
        ApplicationDbContext db,
        Guid tenantId,
        BankAccount bank,
        Account gl,
        string status)
    {
        var batch = new OpeningBalanceBatch
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            BatchNumber = $"OB-{Guid.NewGuid():N}"[..20],
            SourceReference = "BANK-OPENING-EVIDENCE",
            OpeningDate = new DateTime(2026, 1, 1),
            FiscalPeriodId = Guid.NewGuid(),
            BookClassification = "IFRS",
            IdempotencyKey = $"bank-opening-{Guid.NewGuid():N}",
            Status = status
        };
        batch.Lines.Add(new OpeningBalanceLine
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            LineNumber = 1,
            AccountId = gl.Id,
            BankAccountId = bank.Id,
            CounterpartyType = "BankAccountOpening",
            CounterpartyId = bank.Id,
            DebitAmount = 100m,
            TransactionDebitAmount = 100m,
            TransactionCreditAmount = 0m,
            SourceReference = batch.SourceReference
        });
        db.OpeningBalanceBatches.Add(batch);
        return batch;
    }
}
