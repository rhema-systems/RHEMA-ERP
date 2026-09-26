using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces.Finance;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Finance;

public sealed partial class ApInvoicePostingMigrationTests
{
    [Theory]
    [InlineData("Freight")]
    [InlineData("FinanceCharge")]
    public async Task SupplierServiceCharge_ShouldUseServiceTaxRules(string type)
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedApprovedApInvoiceAsync(db, tenantId);
        TaxCalculationRequestDto? capturedRequest = null;
        var taxEngine = new Mock<ITaxCalculationEngine>();
        taxEngine.Setup(engine => engine.CalculateTaxesAsync(It.IsAny<TaxCalculationRequestDto>(), It.IsAny<CancellationToken>()))
            .Callback<TaxCalculationRequestDto, CancellationToken>((request, _) => capturedRequest = request)
            .ReturnsAsync(new TaxCalculationResultDto());
        var (service, _) = CreateService(db, tenantId, taxEngine: taxEngine.Object);
        await service.CreateAsync(new VendorInvoiceCreateDto
        {
            SupplierId = fixture.Supplier.Id, InvoiceDate = fixture.Invoice.InvoiceDate,
            SupplierInvoiceNumber = "SERVICE-CHARGE", CurrencyCode = "GHS", ExchangeRate = 1m,
            LineItems = [new() { LineItemType = type, Description = "Service charge", Quantity = 1m,
                UnitPrice = 100m, GLAccountId = fixture.ExpenseAccount.Id, TaxGroupId = Guid.NewGuid() }]
        });
        capturedRequest.Should().NotBeNull();
        capturedRequest!.TransactionType.Should().Be(TaxTransactionType.PurchaseOfServices);
    }

    [Fact]
    public async Task SupplierDefaults_ShouldUseCapturedPurchaseOrderChargeAccounts()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedApprovedApInvoiceAsync(db, tenantId);
        var captured = SeedAccount(db, tenantId, "PO-FREIGHT", AccountType.Expense);
        var partner = new BusinessPartner
        {
            TenantId = tenantId, PartnerCode = fixture.Supplier.SupplierCode,
            PartnerName = "Charge supplier", PartnerType = "Supplier", IsActive = true,
            ApprovalStatus = "Approved", RegistrationStatus = "Active", DefaultFreightAccountId = captured.Id
        };
        var order = new PurchaseOrder
        {
            TenantId = tenantId, BusinessPartnerId = partner.Id, OrderNumber = "PO-CHARGE",
            SupplierDefaultsSnapshotJson = ErpSystem.Core.Services.Procurement.BusinessPartnerPostingDefaults.SerializeSnapshot(partner)
        };
        db.BusinessPartners.Add(partner); db.PurchaseOrders.Add(order);
        partner.DefaultFreightAccountId = Guid.NewGuid();
        await db.SaveChangesAsync();
        var (service, _) = CreateService(db, tenantId);
        var source = await service.GetSupplierDefaultsAsync(fixture.Supplier.Id, order.Id);
        source!.PostingDefaults.DefaultFreightAccountId.Should().Be(captured.Id);
    }

    [Fact]
    public async Task DurableSupplierLink_ShouldNotBypassPartnerEligibility()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedApprovedApInvoiceAsync(db, tenantId);
        var partner = new BusinessPartner
        {
            TenantId = tenantId, PartnerCode = "OTHER-CODE", PartnerName = "Inactive supplier",
            PartnerType = "Supplier", IsActive = false, ApprovalStatus = "Approved", RegistrationStatus = "Active"
        };
        db.BusinessPartners.Add(partner);
        db.ApSupplierIdentityLinks.Add(new ApSupplierIdentityLink
        {
            TenantId = tenantId, BusinessPartnerId = partner.Id, SupplierId = fixture.Supplier.Id,
            MappingSource = "Manual", IsVerified = true
        });
        await db.SaveChangesAsync();
        var (service, _) = CreateService(db, tenantId);
        var create = () => service.CreateAsync(new VendorInvoiceCreateDto
        {
            SupplierId = fixture.Supplier.Id, ApplyBusinessPartnerDefaults = true,
            InvoiceDate = fixture.Invoice.InvoiceDate, SupplierInvoiceNumber = "UNAVAILABLE-SUPPLIER",
            LineItems = [new() { LineItemType = "Freight", Description = "Charge", Quantity = 1m, UnitPrice = 10m }]
        });
        await create.Should().ThrowAsync<InvalidOperationException>().WithMessage("*active, approved*");
        (await db.VendorInvoices.CountAsync()).Should().Be(1, "only the original fixture exists");
    }

    [Theory]
    [InlineData("Freight", false, true, false)]
    [InlineData("Miscellaneous", false, true, false)]
    [InlineData("FinanceCharge", false, true, false)]
    [InlineData("Freight", true, true, false)]
    [InlineData("Freight", false, false, false)]
    [InlineData("Freight", false, true, true)]
    public async Task TypedSupplierCharge_ShouldCaptureDraftAccountAndPostItOnce(
        string type, bool explicitAccount, bool applyDefaults, bool durableLink)
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedApprovedApInvoiceAsync(db, tenantId);
        var chargeAccount = SeedAccount(db, tenantId, "CHARGE", AccountType.Expense);
        var partner = new BusinessPartner
        {
            TenantId = tenantId, PartnerCode = durableLink ? "RENAMED-SUPPLIER" : fixture.Supplier.SupplierCode,
            PartnerName = "Charge supplier", PartnerType = "CustomerAndSupplier", IsActive = true,
            ApprovalStatus = "Approved", RegistrationStatus = "Active",
            DefaultExpenseAccountId = fixture.ExpenseAccount.Id,
            DefaultFreightAccountId = chargeAccount.Id, DefaultMiscellaneousAccountId = chargeAccount.Id,
            DefaultFinanceChargesAccountId = chargeAccount.Id
        };
        db.BusinessPartners.Add(partner);
        if (durableLink) db.ApSupplierIdentityLinks.Add(new ApSupplierIdentityLink
        {
            TenantId = tenantId, SupplierId = fixture.Supplier.Id, BusinessPartnerId = partner.Id,
            MappingSource = "Manual", IsVerified = true
        });
        await db.SaveChangesAsync();
        var (service, _) = CreateService(db, tenantId);
        var created = await service.CreateAsync(new VendorInvoiceCreateDto
        {
            SupplierId = fixture.Supplier.Id, SupplierInvoiceNumber = "TYPED-CHARGE-TEST",
            InvoiceDate = fixture.Invoice.InvoiceDate, CurrencyCode = "GHS", ExchangeRate = 1m,
            ApplyBusinessPartnerDefaults = applyDefaults,
            LineItems = [new() { LineItemType = type, Description = "Supplier charge", Quantity = 1m,
                UnitPrice = 100m, DiscountPercentage = 10m, TaxTreatment = TaxTreatment.OutOfScope,
                GLAccountId = explicitAccount ? fixture.ExpenseAccount.Id : null }]
        });
        var expected = applyDefaults && !explicitAccount ? chargeAccount.Id : fixture.ExpenseAccount.Id;
        if (applyDefaults || explicitAccount) created.LineItems.Single().GLAccountId.Should().Be(expected);
        var draftPost = () => service.PostAsync(created.Id);
        await draftPost.Should().ThrowAsync<InvalidOperationException>();
        (await db.FinancePostingEvents.AnyAsync(entry => entry.SourceDocumentId == created.Id)).Should().BeFalse();

        // Approval is fixture state; the service above must reject an unapproved draft.
        var invoice = await db.VendorInvoices.SingleAsync(item => item.Id == created.Id);
        invoice.Status = VendorInvoiceStatus.Approved; invoice.ApprovalStatus = "Approved";
        invoice.ApprovedById = Guid.NewGuid(); invoice.ApprovedDate = DateTime.UtcNow;
        partner.DefaultFreightAccountId = partner.DefaultMiscellaneousAccountId = partner.DefaultFinanceChargesAccountId = Guid.NewGuid();
        await db.SaveChangesAsync();
        var posted = await service.PostAsync(created.Id);
        var retry = await service.PostAsync(created.Id);
        retry.JournalEntryId.Should().Be(posted.JournalEntryId);
        var lines = await db.AccountTransactions.Where(line => line.JournalEntryId == posted.JournalEntryId).ToListAsync();
        lines.Single(line => line.AccountId == expected).DebitAmount.Should().Be(90m);
        lines.Single(line => line.AccountId == fixture.ApAccount.Id).CreditAmount.Should().Be(90m);
        lines.Should().HaveCount(2, "trade discounts remain net, without a separate discount entry");
        (await db.FinancePostingEvents.CountAsync(entry => entry.SourceDocumentId == created.Id && entry.PostingAction == "Post"))
            .Should().Be(1);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SupplierCharge_ShouldRejectIneligibleCapturedAccount(bool otherTenant)
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedApprovedApInvoiceAsync(db, tenantId);
        var account = SeedAccount(db, tenantId, "INVALID-CHARGE", AccountType.Expense);
        if (otherTenant) account.TenantId = Guid.NewGuid(); else account.Status = AccountStatus.Inactive;
        fixture.Invoice.LineItems.Single().LineItemType = "FinanceCharge";
        fixture.Invoice.LineItems.Single().GLAccountId = account.Id;
        await db.SaveChangesAsync();
        var (service, _) = CreateService(db, tenantId);
        var post = () => service.PostAsync(fixture.Invoice.Id);
        await post.Should().ThrowAsync<InvalidOperationException>();
        fixture.Invoice.JournalEntryId.Should().BeNull();
        (await db.AccountTransactions.AnyAsync()).Should().BeFalse();
    }
}
