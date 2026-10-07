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
    public async Task DraftUpdateRemovingTaxPersistsZeroTaxInsteadOfReapplyingDefaults()
    {
        var tenant = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedApprovedApInvoiceAsync(db, tenant, invoice =>
        {
            invoice.Status = VendorInvoiceStatus.Draft;
            invoice.ApprovalStatus = "Draft";
            invoice.ApprovedById = null;
            invoice.ApprovedDate = null;
            invoice.TaxAmount = 15m;
            invoice.TotalAmount = 115m;
            invoice.BaseCurrencyAmount = 115m;
            var line = invoice.LineItems.Single();
            line.TaxGroupId = Guid.NewGuid();
            line.TaxTreatment = TaxTreatment.Standard;
            line.TaxRate = 15m;
            line.TaxAmount = 15m;
        });
        var taxEngine = new Mock<ITaxCalculationEngine>();
        var (service, _) = CreateService(db, tenant, taxEngine: taxEngine.Object);
        var line = fixture.Invoice.LineItems.Single();

        var updated = await service.UpdateAsync(new VendorInvoiceUpdateDto
        {
            Id = fixture.Invoice.Id,
            SupplierInvoiceNumber = fixture.Invoice.SupplierInvoiceNumber,
            InvoiceDate = fixture.Invoice.InvoiceDate,
            ReceivedDate = fixture.Invoice.ReceivedDate,
            DueDate = fixture.Invoice.DueDate,
            CurrencyCode = fixture.Invoice.CurrencyCode,
            ExchangeRate = fixture.Invoice.ExchangeRate,
            PaymentTermsDays = fixture.Invoice.PaymentTermsDays,
            ExpenseAccountId = fixture.ExpenseAccount.Id,
            LineItems =
            [
                new VendorInvoiceLineItemCreateDto
                {
                    Id = line.Id,
                    LineItemType = line.LineItemType,
                    GLAccountId = fixture.ExpenseAccount.Id,
                    Description = line.Description,
                    Quantity = line.Quantity,
                    UnitPrice = line.UnitPrice,
                    TaxGroupId = null,
                    TaxTreatment = TaxTreatment.Exempt
                }
            ]
        });

        updated.TaxAmount.Should().Be(0m);
        updated.TotalAmount.Should().Be(100m);
        updated.LineItems.Single().TaxGroupId.Should().BeNull();
        updated.LineItems.Single().TaxTreatment.Should().Be(TaxTreatment.Exempt);
        db.ChangeTracker.Clear();
        var persisted = await db.VendorInvoices.Include(invoice => invoice.LineItems)
            .SingleAsync(invoice => invoice.Id == fixture.Invoice.Id);
        persisted.TaxAmount.Should().Be(0m);
        persisted.TotalAmount.Should().Be(100m);
        persisted.LineItems.Single().TaxAmount.Should().Be(0m);
        persisted.LineItems.Single().TaxGroupId.Should().BeNull();
        persisted.LineItems.Single().TaxTreatment.Should().Be(TaxTreatment.Exempt);
        taxEngine.Verify(engine => engine.CalculateDocumentTaxesAsync(
            It.IsAny<TaxDocumentCalculationRequestDto>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

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
