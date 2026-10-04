using System.Reflection;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.DTOs.Sales;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Entities.Sales;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Core.Interfaces.Numbering;
using ErpSystem.Core.Interfaces.Sales;
using ErpSystem.Core.Services.Sales;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Sales;

public class SalesOrderTaxSelectionTests
{
    [Fact]
    public async Task Document_tax_group_calculates_discounted_line_tax_with_finance_engine()
    {
        var taxGroupId = Guid.NewGuid();
        var partnerId = Guid.NewGuid();
        TaxCalculationRequestDto? request = null;
        var engine = new Mock<ITaxCalculationEngine>();
        engine.Setup(item => item.CalculateTaxesAsync(
                It.IsAny<TaxCalculationRequestDto>(),
                It.IsAny<CancellationToken>()))
            .Callback<TaxCalculationRequestDto, CancellationToken>((value, _) => request = value)
            .ReturnsAsync(new TaxCalculationResultDto
            {
                TaxGroupId = taxGroupId,
                BaseAmount = 180m,
                TotalTaxAmount = 18m,
                EffectiveTaxRate = 10m,
                TaxBreakdowns = [new TaxBreakdownDto { TaxCode = "VAT" }]
            });
        var service = CreateService(engine.Object);
        var method = typeof(SalesOrderService).GetMethod(
            "CalculateLineTaxAsync",
            BindingFlags.Instance | BindingFlags.NonPublic);

        method.Should().NotBeNull();
        var pending = (Task)method!.Invoke(service,
        [
            new CreateSalesOrderLineDto
            {
                Description = "Plot A",
                Quantity = 2m,
                UnitPrice = 100m,
                DiscountPercentage = 10m,
                TaxGroupId = Guid.NewGuid()
            },
            taxGroupId,
            partnerId
        ])!;
        await pending;
        var result = pending.GetType().GetProperty("Result")!.GetValue(pending)!;

        request.Should().NotBeNull();
        request!.BaseAmount.Should().Be(180m);
        request.TaxGroupId.Should().Be(taxGroupId);
        request.TransactionType.Should().Be(TaxTransactionType.SaleOfGoods);
        request.BusinessPartnerId.Should().Be(partnerId);
        request.BusinessPartnerRole.Should().Be(BusinessPartnerRoleType.Customer);
        ReadDecimal(result, "DiscountAmount").Should().Be(20m);
        ReadDecimal(result, "TaxAmount").Should().Be(18m);
        ReadDecimal(result, "EffectiveRate").Should().Be(10m);
        result.GetType().GetProperty("TaxGroupId")!.GetValue(result).Should().Be(taxGroupId);
    }

    private static SalesOrderService CreateService(ITaxCalculationEngine engine) => new(
        Mock.Of<IGenericRepository<SalesOrder>>(),
        Mock.Of<IGenericRepository<SalesOrderLine>>(),
        Mock.Of<IGenericRepository<SalesOrderStatusHistory>>(),
        Mock.Of<IGenericRepository<BusinessPartner>>(),
        Mock.Of<IGenericRepository<Quote>>(),
        Mock.Of<IGenericRepository<PaymentTerm>>(),
        Mock.Of<IUnitOfWork>(),
        Mock.Of<ICurrentUserProvider>(),
        Mock.Of<IDocumentNumberingService>(),
        Mock.Of<IWorkflowIntegrationService>(),
        Mock.Of<IWorkflowStatusAdapterRegistry>(),
        NullLogger<SalesOrderService>.Instance,
        null,
        engine);

    private static decimal ReadDecimal(object value, string propertyName) =>
        (decimal)value.GetType().GetProperty(propertyName)!.GetValue(value)!;
}
