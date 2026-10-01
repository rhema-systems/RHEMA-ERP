using System.Reflection;
using ErpSystem.Api.Controllers.Sales;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Interfaces.Finance;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Controllers.Sales;

public sealed class SalesReferenceControllerTests
{
    [Fact]
    public void Currency_reference_route_is_internal_read_only_and_separate_from_finance_authority()
    {
        var controllerType = typeof(SalesReferenceController);
        var method = controllerType.GetMethod(nameof(SalesReferenceController.GetCurrencies));

        controllerType.GetCustomAttribute<RouteAttribute>()?.Template.Should().Be("api/sales/reference");
        controllerType.GetCustomAttribute<AuthorizeAttribute>()?.Policy.Should().Be("InternalOnly");
        method.Should().NotBeNull();
        method!.GetCustomAttribute<HttpGetAttribute>()?.Template.Should().Be("currencies");
        method.GetCustomAttributes<AuthorizeAttribute>().Should().BeEmpty();
    }

    [Fact]
    public async Task GetCurrencies_returns_only_active_sales_reference_fields()
    {
        var currencyService = new Mock<ICurrencyService>();
        currencyService.Setup(service => service.GetActiveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new CurrencyDto
                {
                    CurrencyCode = "USD",
                    CurrencyName = "US Dollar",
                    CurrencySymbol = "$",
                    DecimalPlaces = 2,
                    IsActive = true
                },
                new CurrencyDto
                {
                    CurrencyCode = "GHS",
                    CurrencyName = "Ghana Cedi",
                    CurrencySymbol = "GH₵",
                    DecimalPlaces = 2,
                    IsActive = true,
                    IsBaseCurrency = true
                }
            });
        var controller = new SalesReferenceController(currencyService.Object);

        var response = await controller.GetCurrencies();

        var ok = response.Result.Should().BeOfType<OkObjectResult>().Subject;
        var currencies = ok.Value.Should().BeAssignableTo<IReadOnlyList<SalesCurrencyReferenceDto>>().Subject;
        currencies.Should().HaveCount(2);
        currencies[0].Should().Be(new SalesCurrencyReferenceDto("GHS", "Ghana Cedi", "GH₵", 2, true));
        currencies[1].Should().Be(new SalesCurrencyReferenceDto("USD", "US Dollar", "$", 2, false));
        currencyService.Verify(service => service.GetActiveAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
