using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Enums;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Finance;

public sealed partial class ApPaymentPostingMigrationTests
{
    [Fact]
    public async Task SelectedBankOwnsCashAccountRegardlessOfLegacyPartnerDefaults()
    {
        var tenant=Guid.NewGuid();await using var db=CreateContext();
        var fixture=await SeedApprovedApPaymentAsync(db,tenant);
        var cash=SeedAccount(db,tenant,"CHOSEN-CASH",AccountType.Asset);
        var bank=SeedBankAccount(db,tenant,cash.Id);bank.AccountNumber="SELECTED-BANK";
        var advance=SeedAccount(db,tenant,"ADVANCE",AccountType.Asset);
        (await db.FinanceSettings.SingleAsync()).SupplierAdvanceAccountId=advance.Id;
        fixture.Supplier.DefaultCashAccountId=Guid.NewGuid();fixture.Supplier.DefaultBankAccountId=Guid.NewGuid();
        await db.SaveChangesAsync();var (service,_)=CreateService(db,tenant);
        var created=await service.CreateAsync(new VendorPaymentCreateDto
        {
            BusinessPartnerId=fixture.Supplier.Id,PaymentDate=fixture.Payment.PaymentDate,TotalAmount=25m,
            CurrencyCode="GHS",TransactionReference="SELECTED-BANK",BankAccountId=bank.Id
        });
        created.BankAccountId.Should().Be(bank.Id);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task FinanceDiscountAccountPostsOnceAndRetainsPostedAccountOnRetry(bool invalidLegacyAccount)
    {
        var tenant=Guid.NewGuid();await using var db=CreateContext();
        var fixture=await SeedApprovedApPaymentAsync(db,tenant,allocationAmount:90m);fixture.Allocation.DiscountAmount=10m;
        var settings=await db.FinanceSettings.SingleAsync();
        var account=SeedAccount(db,tenant,"FINANCE-DISCOUNT",AccountType.Revenue);settings.DiscountReceivedAccountId=account.Id;
        fixture.Supplier.DefaultTermsDiscountsTakenAccountId=invalidLegacyAccount?Guid.NewGuid():fixture.ApAccount.Id;
        await db.SaveChangesAsync();var (service,_)=CreateService(db,tenant);
        var result=await service.PostAsync(fixture.Payment.Id);
        fixture.Supplier.DefaultTermsDiscountsTakenAccountId=Guid.NewGuid();await db.SaveChangesAsync();
        var retry=await service.PostAsync(fixture.Payment.Id);retry.JournalEntryId.Should().Be(result.JournalEntryId);
        var lines=await db.AccountTransactions.Where(x=>x.JournalEntryId==result.JournalEntryId).ToListAsync();
        lines.Single(x=>x.TransactionTag=="AP-Discount").AccountId.Should().Be(account.Id);
        lines.Single(x=>x.TransactionTag=="AP-Discount").CreditAmount.Should().Be(10m);
        lines.Sum(x=>x.DebitAmount).Should().Be(lines.Sum(x=>x.CreditAmount));
    }
}
