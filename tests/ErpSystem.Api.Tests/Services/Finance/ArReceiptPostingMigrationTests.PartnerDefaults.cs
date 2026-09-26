using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Enums;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Finance;

public sealed partial class ArReceiptPostingMigrationTests
{
    [Fact]
    public async Task SavedCustomerDiscountAccount_ShouldReloadAndDriveReceiptLedger()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedApprovedArReceiptAsync(db, tenantId, allocationAmount: 90m);
        var discount = SeedAccount(db, tenantId, "SAVED-CUSTOMER-DISCOUNT", AccountType.Expense);
        fixture.Allocation.DiscountAmount = 10m;
        await db.SaveChangesAsync();

        var saved = await CustomerPostingSettingsFixture.SaveAndReloadAsync(db, tenantId, fixture.Customer.Id,
            new() { TermsDiscountsTakenAccountId = discount.Id });
        saved.TermsDiscountsTakenAccountId.Should().Be(discount.Id);
        saved.DefaultArAccountId.Should().Be(fixture.ArAccount.Id);
        var (service, _) = CreateService(db, tenantId);

        var posted = await service.PostAsync(fixture.Payment.Id);
        var replay = await service.PostAsync(fixture.Payment.Id);

        replay.JournalEntryId.Should().Be(posted.JournalEntryId);
        var lines = await db.AccountTransactions.Where(x => x.JournalEntryId == posted.JournalEntryId).ToListAsync();
        lines.Single(x => x.AccountId == discount.Id).DebitAmount.Should().Be(10m);
        lines.Single(x => x.AccountId == fixture.ArAccount.Id).CreditAmount.Should().Be(100m);
        lines.Single(x => x.AccountId == fixture.BankGlAccount.Id).DebitAmount.Should().Be(90m);
        lines.Sum(x => x.DebitAmount).Should().Be(lines.Sum(x => x.CreditAmount));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CustomerDiscountDefault_ShouldPostIndependentlyOfSupplierDiscountDefault(bool deactivateNewDefault)
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedApprovedArReceiptAsync(db, tenantId, allocationAmount: 90m);
        var account = SeedAccount(db, tenantId, "CUSTOMER-DISCOUNT", AccountType.Expense);
        var replacementAccount = SeedAccount(db, tenantId, "UPDATED-CUSTOMER-DISCOUNT", AccountType.Expense);
        fixture.Customer.PartnerType = "CustomerAndSupplier";
        fixture.Customer.CustomerTermsDiscountsTakenAccountId = account.Id;
        fixture.Customer.DefaultTermsDiscountsTakenAccountId = Guid.NewGuid();
        fixture.Allocation.DiscountAmount = 10m;
        await db.SaveChangesAsync();
        var (service, _) = CreateService(db, tenantId);

        var result = await service.PostAsync(fixture.Payment.Id);
        fixture.Customer.CustomerTermsDiscountsTakenAccountId = replacementAccount.Id;
        if (deactivateNewDefault) replacementAccount.Status = AccountStatus.Inactive;
        await db.SaveChangesAsync();
        var replay = await service.PostAsync(fixture.Payment.Id);

        replay.JournalEntryId.Should().Be(result.JournalEntryId);
        var lines = await db.AccountTransactions.Where(line => line.JournalEntryId == result.JournalEntryId).ToListAsync();
        lines.Single(line => line.TransactionTag == "AR-Discount").AccountId.Should().Be(account.Id);
        lines.Single(line => line.TransactionTag == "AR-Discount").DebitAmount.Should().Be(10m);
        lines.Single(line => line.AccountId == fixture.ArAccount.Id).CreditAmount.Should().Be(100m);
        lines.Single(line => line.AccountId == fixture.BankGlAccount.Id).DebitAmount.Should().Be(90m);
        lines.Sum(line => line.DebitAmount).Should().Be(lines.Sum(line => line.CreditAmount));
    }
}
