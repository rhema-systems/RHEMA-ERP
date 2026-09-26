using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Enums;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Finance;

public sealed partial class ApPaymentPostingMigrationTests
{
    [Theory]
    [InlineData("Chequebook", false)]
    [InlineData("BusinessPartner", false)]
    [InlineData("Chequebook", true)]
    public async Task SupplierCashDefault_ShouldFreezeBankAndPostToItsGlAccount(string source, bool explicitBank)
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedApprovedApPaymentAsync(db, tenantId);
        var cash = SeedAccount(db, tenantId, "SUPPLIER-CASH", AccountType.Asset);
        var bank = SeedBankAccount(db, tenantId, cash.Id);
        bank.AccountNumber = "PARTNER-BANK";
        var advance = SeedAccount(db, tenantId, "SUPPLIER-ADVANCE", AccountType.Asset);
        (await db.FinanceSettings.SingleAsync()).SupplierAdvanceAccountId = advance.Id;
        var partner = new BusinessPartner
        {
            Id = Guid.NewGuid(), TenantId = tenantId, PartnerCode = fixture.Supplier.SupplierCode,
            PartnerName = "Supplier cash defaults", PartnerType = "Supplier", IsActive = true,
            ApprovalStatus = "Approved", RegistrationStatus = "Active", CashAccountSource = source,
            DefaultBankAccountId = bank.Id, DefaultCashAccountId = cash.Id
        };
        db.BusinessPartners.Add(partner);
        await db.SaveChangesAsync();
        var (service, _) = CreateService(db, tenantId);

        var created = await service.CreateAsync(new VendorPaymentCreateDto
        {
            SupplierId = fixture.Supplier.Id, PaymentDate = fixture.Payment.PaymentDate,
            TotalAmount = 25m, CurrencyCode = "GHS", TransactionReference = "PARTNER-CASH-TEST",
            BankAccountId = explicitBank ? fixture.BankAccount.Id : null
        });
        created.BankAccountId.Should().Be(explicitBank ? fixture.BankAccount.Id : bank.Id);
        var payment = await db.Set<VendorPayment>().SingleAsync(item => item.Id == created.Id);
        payment.Status = VendorPaymentStatus.Authorized;
        payment.AuthorizedById = Guid.NewGuid();
        payment.AuthorizedDate = DateTime.UtcNow;
        partner.DefaultBankAccountId = fixture.BankAccount.Id;
        partner.DefaultCashAccountId = fixture.BankGlAccount.Id;
        await db.SaveChangesAsync();

        var posted = await service.PostAsync(payment.Id);
        var lines = await db.AccountTransactions.Where(line => line.JournalEntryId == posted.JournalEntryId).ToListAsync();
        lines.Single(line => line.TransactionTag == "AP-Bank").AccountId.Should().Be(explicitBank ? fixture.BankGlAccount.Id : cash.Id);
        lines.Single(line => line.TransactionTag == "AP-Bank").CreditAmount.Should().Be(25m);
        lines.Single(line => line.AccountId == advance.Id).DebitAmount.Should().Be(25m);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task SupplierDiscountAccount_ShouldPostThroughPartnerIdentityAndKeepOriginalOnRetry(bool linked, bool invalidChangedMapping)
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedApprovedApPaymentAsync(db, tenantId, allocationAmount: 90m);
        fixture.Allocation.DiscountAmount = 10m;
        var account = SeedAccount(db, tenantId, "PARTNER-DISCOUNT", AccountType.Revenue);
        var changedDefault = SeedAccount(db, tenantId, "PARTNER-DISCOUNT-CHANGED", AccountType.Revenue);
        var partner = new BusinessPartner
        {
            Id = Guid.NewGuid(), TenantId = tenantId,
            PartnerCode = linked ? "LINKED-PARTNER" : fixture.Supplier.SupplierCode,
            PartnerName = "Dual-role supplier", PartnerType = "CustomerAndSupplier",
            IsActive = true, ApprovalStatus = "Approved", RegistrationStatus = "Active",
            DefaultTermsDiscountsTakenAccountId = account.Id,
            DefaultArAccountId = Guid.NewGuid()
        };
        db.BusinessPartners.Add(partner);
        if (linked)
            db.ApSupplierIdentityLinks.Add(new ApSupplierIdentityLink
            {
                TenantId = tenantId, BusinessPartnerId = partner.Id,
                SupplierId = fixture.Supplier.Id, MappingSource = "Manual", IsVerified = true
            });
        await db.SaveChangesAsync();
        var (service, _) = CreateService(db, tenantId);

        var result = await service.PostAsync(fixture.Payment.Id);
        partner.DefaultTermsDiscountsTakenAccountId = invalidChangedMapping ? Guid.NewGuid() : changedDefault.Id;
        await db.SaveChangesAsync();
        var replay = await service.PostAsync(fixture.Payment.Id);

        replay.JournalEntryId.Should().Be(result.JournalEntryId);
        var lines = await db.AccountTransactions.Where(line => line.JournalEntryId == result.JournalEntryId).ToListAsync();
        lines.Single(line => line.TransactionTag == "AP-Discount").AccountId.Should().Be(account.Id);
        lines.Single(line => line.TransactionTag == "AP-Discount").CreditAmount.Should().Be(10m);
        lines.Single(line => line.TransactionTag == "AP-Control").DebitAmount.Should().Be(100m);
        lines.Single(line => line.TransactionTag == "AP-Bank").CreditAmount.Should().Be(90m);
        lines.Sum(line => line.DebitAmount).Should().Be(lines.Sum(line => line.CreditAmount));
        (await db.FinancePostingEvents.CountAsync(entry => entry.SourceDocumentId == fixture.Payment.Id && entry.PostingAction == "Post"))
            .Should().Be(1);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SupplierDiscountAccount_ShouldRejectInactiveOrCrossTenantMapping(bool crossTenant)
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedApprovedApPaymentAsync(db, tenantId, allocationAmount: 90m);
        fixture.Allocation.DiscountAmount = 10m;
        var accountTenantId = crossTenant ? Guid.NewGuid() : tenantId;
        if (crossTenant) SeedTenant(db, accountTenantId);
        var account = SeedAccount(db, accountTenantId, "INVALID-DISCOUNT", AccountType.Revenue);
        if (!crossTenant) account.Status = AccountStatus.Inactive;
        db.BusinessPartners.Add(new BusinessPartner
        {
            Id = Guid.NewGuid(), TenantId = tenantId, PartnerCode = fixture.Supplier.SupplierCode,
            PartnerName = "Supplier", PartnerType = "Supplier", IsActive = true,
            ApprovalStatus = "Approved", RegistrationStatus = "Active",
            DefaultTermsDiscountsTakenAccountId = account.Id
        });
        await db.SaveChangesAsync();
        var (service, _) = CreateService(db, tenantId);

        var post = () => service.PostAsync(fixture.Payment.Id);
        await post.Should().ThrowAsync<InvalidOperationException>();
        (await db.AccountTransactions.AnyAsync(line => line.SourceDocumentId == fixture.Payment.Id)).Should().BeFalse();
        fixture.Payment.JournalEntryId.Should().BeNull();
    }
}
