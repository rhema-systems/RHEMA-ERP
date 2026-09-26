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
            BusinessPartnerId = fixture.Supplier.Id, InvoiceDate = fixture.Invoice.InvoiceDate,
            SupplierInvoiceNumber = "SERVICE-CHARGE", CurrencyCode = "GHS", ExchangeRate = 1m,
            LineItems = [new() { LineItemType = type, Description = "Service charge", Quantity = 1m,
                UnitPrice = 100m, GLAccountId = fixture.ExpenseAccount.Id, TaxGroupId = Guid.NewGuid() }]
        });
        capturedRequest.Should().NotBeNull();
        capturedRequest!.TransactionType.Should().Be(TaxTransactionType.PurchaseOfServices);
    }

    [Theory]
    [InlineData("Freight")]
    [InlineData("Miscellaneous")]
    [InlineData("FinanceCharge")]
    public async Task ExplicitChargeAccountIsRetainedWithoutLegacyPartnerOverride(string type)
    {
        var tenant=Guid.NewGuid(); await using var db=CreateContext();
        var fixture=await SeedApprovedApInvoiceAsync(db,tenant);
        var expense=SeedAccount(db,tenant,"EXPLICIT-CHARGE",AccountType.Expense);
        fixture.Supplier.DefaultFreightAccountId=Guid.NewGuid();
        fixture.Supplier.DefaultMiscellaneousAccountId=Guid.NewGuid();
        fixture.Supplier.DefaultFinanceChargesAccountId=Guid.NewGuid();
        await db.SaveChangesAsync(); var (service,_)=CreateService(db,tenant);
        var result=await service.CreateAsync(new VendorInvoiceCreateDto
        {
            BusinessPartnerId=fixture.Supplier.Id,InvoiceDate=fixture.Invoice.InvoiceDate,
            SupplierInvoiceNumber="EXPLICIT-CHARGE",CurrencyCode="GHS",ExchangeRate=1m,ApplyBusinessPartnerDefaults=true,
            LineItems=[new() {LineItemType=type,Description="Reviewed charge",Quantity=1m,UnitPrice=100m,
                DiscountPercentage=10m,TaxTreatment=TaxTreatment.OutOfScope,GLAccountId=expense.Id}]
        });
        result.LineItems.Single().GLAccountId.Should().Be(expense.Id);
        result.SubTotal.Should().Be(90m); result.ApAccountId.Should().BeNull();
        (await db.Suppliers.AnyAsync()).Should().BeFalse();
        var defaults=await service.GetSupplierDefaultsAsync(fixture.Supplier.Id,invoiceDate:fixture.Invoice.InvoiceDate);
        defaults!.PostingDefaults.DefaultFreightAccountId.Should().BeNull();
        defaults.PostingDefaults.DefaultMiscellaneousAccountId.Should().BeNull();
        defaults.PostingDefaults.DefaultFinanceChargesAccountId.Should().BeNull();
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
