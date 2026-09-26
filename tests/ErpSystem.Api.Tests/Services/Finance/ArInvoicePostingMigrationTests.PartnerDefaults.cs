using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Enums;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Finance;

public sealed partial class ArInvoicePostingMigrationTests
{
    [Fact]
    public async Task LegacyCustomerAccounts_ShouldNotOverrideFinanceAndInvoiceAccounts()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedSentArInvoiceAsync(db, tenantId);
        var ar = SeedAccount(db, tenantId, "SAVED-CUSTOMER-AR", AccountType.Asset,
            isControlAccount: true, allowDirectPosting: false);
        var sales = SeedAccount(db, tenantId, "SAVED-CUSTOMER-SALES", AccountType.Revenue);
        var cost = SeedAccount(db, tenantId, "SAVED-CUSTOMER-COGS", AccountType.Expense);
        var inventory = SeedAccount(db, tenantId, "SAVED-CUSTOMER-INVENTORY", AccountType.Asset);
        var discounts = SeedAccount(db, tenantId, "SAVED-CUSTOMER-DISCOUNTS", AccountType.Expense);
        var returns = SeedAccount(db, tenantId, "SAVED-CUSTOMER-RETURNS", AccountType.Expense);
        fixture.Customer.PartnerType = "CustomerAndSupplier";
        var supplierAccount = Guid.NewGuid();
        fixture.Customer.DefaultApAccountId = supplierAccount;
        var sourceLine = fixture.Invoice.LineItems.Single();
        sourceLine.GLAccountId = sales.Id;
        sourceLine.LineItemType = LineItemType.Inventory;
        sourceLine.CostTotal = 40m;
        await db.SaveChangesAsync();

        var saved = await CustomerPostingSettingsFixture.SaveAndReloadAsync(db, tenantId, fixture.Customer.Id,
            new()
            {
                DefaultArAccountId = ar.Id, SalesAccountId = sales.Id,
                CostOfSalesAccountId = cost.Id, InventoryAccountId = inventory.Id,
                TermsDiscountsTakenAccountId = discounts.Id, SalesReturnsAccountId = returns.Id
            });
        saved.DefaultArAccountId.Should().Be(ar.Id);
        saved.SalesAccountId.Should().Be(sales.Id);
        saved.CostOfSalesAccountId.Should().Be(cost.Id);
        saved.InventoryAccountId.Should().Be(inventory.Id);
        saved.TermsDiscountsTakenAccountId.Should().Be(discounts.Id);
        saved.SalesReturnsAccountId.Should().Be(returns.Id);
        (await db.Set<ErpSystem.Core.Entities.Procurement.BusinessPartner>().SingleAsync(x => x.Id == fixture.Customer.Id))
            .DefaultApAccountId.Should().Be(supplierAccount);
        var settings = await db.FinanceSettings.SingleAsync(x => x.TenantId == tenantId);
        settings.ControlAccountCOGSId = cost.Id;
        settings.ControlAccountInventoryId = inventory.Id;
        await db.SaveChangesAsync();
        var (service, _) = CreateService(db, tenantId);

        var posted = await service.PostAsync(fixture.Invoice.Id);
        var customer = await db.Set<ErpSystem.Core.Entities.Procurement.BusinessPartner>()
            .SingleAsync(x => x.Id == fixture.Customer.Id);
        customer.DefaultArAccountId = fixture.ArAccount.Id;
        customer.CustomerSalesAccountId = fixture.RevenueAccount.Id;
        customer.CustomerCostOfSalesAccountId = fixture.DiscountAccount.Id;
        customer.CustomerInventoryAccountId = fixture.ArAccount.Id;
        await db.SaveChangesAsync();
        var replay = await service.PostAsync(fixture.Invoice.Id);

        replay.JournalEntryId.Should().Be(posted.JournalEntryId);
        var lines = await db.AccountTransactions.Where(x => x.JournalEntryId == posted.JournalEntryId).ToListAsync();
        lines.Should().HaveCount(4);
        lines.Single(x => x.AccountId == fixture.ArAccount.Id).DebitAmount.Should().Be(100m);
        lines.Single(x => x.AccountId == sales.Id).CreditAmount.Should().Be(100m);
        lines.Single(x => x.AccountId == cost.Id).DebitAmount.Should().Be(40m);
        lines.Single(x => x.AccountId == inventory.Id).CreditAmount.Should().Be(40m);
        lines.Sum(x => x.DebitAmount).Should().Be(lines.Sum(x => x.CreditAmount));
    }

    [Fact]
    public async Task FinanceInventoryAccounts_ShouldPostBothSidesIgnoringCustomerDefaults()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedSentArInvoiceAsync(db, tenantId);
        var cost = SeedAccount(db, tenantId, "CUSTOMER-COGS", AccountType.Expense);
        var inventory = SeedAccount(db, tenantId, "CUSTOMER-INVENTORY", AccountType.Asset);
        fixture.Customer.CustomerCostOfSalesAccountId = Guid.NewGuid();
        fixture.Customer.CustomerInventoryAccountId = Guid.NewGuid();
        var settings = await db.FinanceSettings.SingleAsync(x => x.TenantId == tenantId);
        settings.ControlAccountCOGSId = cost.Id;
        settings.ControlAccountInventoryId = inventory.Id;
        var line = fixture.Invoice.LineItems.Single();
        line.LineItemType = LineItemType.Inventory;
        line.CostTotal = 40m;
        await db.SaveChangesAsync();
        var (service, _) = CreateService(db, tenantId);

        var result = await service.PostAsync(fixture.Invoice.Id);

        var lines = await db.AccountTransactions.Where(item => item.JournalEntryId == result.JournalEntryId).ToListAsync();
        lines.Single(item => item.AccountId == cost.Id).DebitAmount.Should().Be(40m);
        lines.Single(item => item.AccountId == inventory.Id).CreditAmount.Should().Be(40m);
        lines.Sum(item => item.DebitAmount).Should().Be(lines.Sum(item => item.CreditAmount));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task InvoiceRevenueAccount_IsRequiredDespiteLegacyCustomerDefault(bool explicitAccount, bool deactivateNewDefault)
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedSentArInvoiceAsync(db, tenantId);
        var sales = SeedAccount(db, tenantId, "PARTNER-SALES", AccountType.Revenue);
        var replacementSales = SeedAccount(db, tenantId, "UPDATED-PARTNER-SALES", AccountType.Revenue);
        fixture.Customer.CustomerSalesAccountId = sales.Id;
        fixture.Customer.PartnerType = "CustomerAndSupplier";
        fixture.Customer.DefaultExpenseAccountId = Guid.NewGuid();
        if (!explicitAccount) fixture.Invoice.LineItems.Single().GLAccountId = null;
        await db.SaveChangesAsync();
        var (service, _) = CreateService(db, tenantId);

        if (!explicitAccount)
        {
            var post = () => service.PostAsync(fixture.Invoice.Id);
            await post.Should().ThrowAsync<InvalidOperationException>().WithMessage("*No revenue account*");
            fixture.Invoice.JournalEntryId.Should().BeNull();
            return;
        }
        var result = await service.PostAsync(fixture.Invoice.Id);
        fixture.Customer.CustomerSalesAccountId = replacementSales.Id;
        if (deactivateNewDefault) replacementSales.Status = AccountStatus.Inactive;
        await db.SaveChangesAsync();
        var replay = await service.PostAsync(fixture.Invoice.Id);

        replay.JournalEntryId.Should().Be(result.JournalEntryId);
        var lines = await db.AccountTransactions.Where(line => line.JournalEntryId == result.JournalEntryId).ToListAsync();
        lines.Single(line => line.CreditAmount > 0m).AccountId.Should().Be(explicitAccount ? fixture.RevenueAccount.Id : sales.Id);
        lines.Single(line => line.CreditAmount > 0m).CreditAmount.Should().Be(100m);
        lines.Single(line => line.DebitAmount > 0m).AccountId.Should().Be(fixture.ArAccount.Id);
    }

    [Fact]
    public async Task CustomerSalesDefault_ShouldRejectAnAccountFromAnotherTenant()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedSentArInvoiceAsync(db, tenantId);
        var otherTenantId = Guid.NewGuid();
        SeedTenant(db, otherTenantId, "OTH");
        var sales = SeedAccount(db, otherTenantId, "FOREIGN-SALES", AccountType.Revenue);
        fixture.Customer.CustomerSalesAccountId = sales.Id;
        fixture.Invoice.LineItems.Single().GLAccountId = null;
        await db.SaveChangesAsync();
        var (service, _) = CreateService(db, tenantId);

        var post = () => service.PostAsync(fixture.Invoice.Id);
        await post.Should().ThrowAsync<InvalidOperationException>();
        fixture.Invoice.JournalEntryId.Should().BeNull();
    }

    [Theory]
    [InlineData("source")]
    [InlineData("tenant")]
    [InlineData("status")]
    [InlineData("missing-role")]
    public async Task CustomerAccountReplay_ShouldRejectInvalidOriginalJournalEvidence(string invalid)
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedSentArInvoiceAsync(db, tenantId);
        fixture.Customer.CustomerSalesAccountId = fixture.RevenueAccount.Id;
        await db.SaveChangesAsync();
        var (service, _) = CreateService(db, tenantId);
        var posted = await service.PostAsync(fixture.Invoice.Id);
        var journal = await db.JournalEntries.SingleAsync(x => x.Id == posted.JournalEntryId);
        if (invalid == "source") journal.SourceDocumentId = Guid.NewGuid();
        if (invalid == "tenant")
        {
            var otherTenantId = Guid.NewGuid();
            var other = await SeedSentArInvoiceAsync(db, otherTenantId);
            (await db.Tenants.SingleAsync(x => x.Id == otherTenantId)).Code = "OTH";
            await db.SaveChangesAsync();
            var (otherService, _) = CreateService(db, otherTenantId);
            var otherPosted = await otherService.PostAsync(other.Invoice.Id);
            // Point the original document/event at a genuinely posted foreign journal;
            // tenant is part of the journal's immutable key and must not be rewritten.
            fixture.Invoice.JournalEntryId = otherPosted.JournalEntryId;
            (await db.FinancePostingEvents.SingleAsync(x => x.TenantId == tenantId &&
                x.SourceDocumentId == fixture.Invoice.Id && x.PostingAction == "Post"))
                .JournalEntryId = otherPosted.JournalEntryId;
        }
        if (invalid == "status") journal.PostingStatus = "Draft";
        if (invalid == "missing-role")
            (await db.AccountTransactions.SingleAsync(x => x.JournalEntryId == posted.JournalEntryId && x.TransactionTag == "AR-Revenue"))
                .TransactionTag = "UNKNOWN";
        await db.SaveChangesAsync();

        var retry = () => service.PostAsync(fixture.Invoice.Id);
        await retry.Should().ThrowAsync<InvalidOperationException>().WithMessage("AR_POSTED_ACCOUNT_EVIDENCE_INVALID*");
        (await db.JournalEntries.CountAsync()).Should().Be(invalid == "tenant" ? 2 : 1);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CustomerAccountReplay_ShouldStillRejectChangedDocumentEvidence(bool changeExplicitAccount)
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedSentArInvoiceAsync(db, tenantId);
        var alternate = SeedAccount(db, tenantId, "ALTERED-LINE-ACCOUNT", AccountType.Revenue);
        fixture.Customer.CustomerSalesAccountId = fixture.RevenueAccount.Id;
        var line = fixture.Invoice.LineItems.Single();
        line.GLAccountId = fixture.RevenueAccount.Id;
        await db.SaveChangesAsync();
        var (service, _) = CreateService(db, tenantId);
        var posted = await service.PostAsync(fixture.Invoice.Id);
        if (changeExplicitAccount) line.GLAccountId = alternate.Id;
        else
        {
            line.UnitPrice = 200m;
            fixture.Invoice.SubTotal = 200m;
            fixture.Invoice.TotalAmount = 200m;
            fixture.Invoice.BaseCurrencyAmount = 200m;
        }
        await db.SaveChangesAsync();

        var retry = () => service.PostAsync(fixture.Invoice.Id);
        await retry.Should().ThrowAsync<InvalidOperationException>().WithMessage("*conflicting canonical request evidence*");
        var lines = await db.AccountTransactions.Where(x => x.JournalEntryId == posted.JournalEntryId).ToListAsync();
        lines.Should().HaveCount(2);
        lines.Sum(x => x.CreditAmount).Should().Be(100m);
    }
}
