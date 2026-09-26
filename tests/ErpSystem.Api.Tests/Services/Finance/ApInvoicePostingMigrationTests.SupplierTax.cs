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
    [Fact]
    public async Task NewInvoiceDoesNotCaptureLegacyPartnerTaxAccount()
    {
        var tenant=Guid.NewGuid();await using var db=CreateContext();
        var fixture=await SeedApprovedApInvoiceAsync(db,tenant);
        fixture.Supplier.DefaultTaxAccountId=Guid.NewGuid();await db.SaveChangesAsync();
        var (service,_)=CreateService(db,tenant);
        var created=await service.CreateAsync(new VendorInvoiceCreateDto
        {
            BusinessPartnerId=fixture.Supplier.Id,InvoiceDate=fixture.Invoice.InvoiceDate,
            SupplierInvoiceNumber="PROFILE-TAX",CurrencyCode="GHS",ExchangeRate=1m,ApplyBusinessPartnerDefaults=true,
            LineItems=[new() {LineItemType="Expense",Description="Explicit untaxed charge",Quantity=1m,UnitPrice=100m,
                GLAccountId=fixture.ExpenseAccount.Id,TaxTreatment=TaxTreatment.OutOfScope}]
        });
        (await db.VendorInvoices.SingleAsync(x=>x.Id==created.Id)).SupplierTaxFallbackAccountId.Should().BeNull();
    }

    private static ITaxCalculationEngine SupplierInputTaxEngine(Guid? ruleAccountId, bool recoverable)
    {
        var engine = new Mock<ITaxCalculationEngine>();
        engine.Setup(value => value.CalculateTaxesAsync(It.IsAny<TaxCalculationRequestDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TaxCalculationResultDto
            {
                BaseAmount = 100m, TotalTaxAmount = 15m, GrandTotal = 115m,
                TaxBreakdowns = [new() { TaxId = Guid.NewGuid(), TaxCode = "VAT", TaxName = "Purchase VAT",
                    TaxRate = 15m, TaxAmount = 15m, TaxableAmount = 100m, IsInputTaxDeductible = recoverable,
                    TaxReceivableAccountId = ruleAccountId }]
            });
        return engine.Object;
    }
}
