using System.Reflection;
using ErpSystem.Api.Controllers.Procurement;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Interfaces.Finance;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Controllers.Procurement;

public sealed class ProcurementReferenceDataControllerTests
{
    [Fact]
    public void ControllerRequiresProcurementReadPermission()
    {
        var authorization = typeof(ProcurementReferenceDataController)
            .GetCustomAttribute<AuthorizeAttribute>();

        authorization.Should().NotBeNull();
        authorization!.Policy.Should().Be("procurement.records.read");
    }

    [Fact]
    public async Task ActiveCurrenciesUseTheFinanceOwnedCurrencyService()
    {
        var cancellationToken = new CancellationTokenSource().Token;
        IReadOnlyList<CurrencyDto> currencies =
        [
            new()
            {
                Id = Guid.NewGuid(),
                CurrencyCode = "GHS",
                CurrencyName = "Ghana Cedi",
                CurrencySymbol = "GH₵",
                IsBaseCurrency = true,
                IsActive = true
            },
            new()
            {
                Id = Guid.NewGuid(),
                CurrencyCode = "USD",
                CurrencyName = "US Dollar",
                CurrencySymbol = "$",
                IsActive = true
            }
        ];
        var currencyService = new Mock<ICurrencyService>();
        currencyService
            .Setup(service => service.GetActiveAsync(cancellationToken))
            .ReturnsAsync(currencies);

        var controller = new ProcurementReferenceDataController(currencyService.Object);

        var result = await controller.GetActiveCurrencies(cancellationToken);

        result.Result.Should().BeOfType<OkObjectResult>()
            .Which.Value.Should().BeSameAs(currencies);
        currencyService.Verify(
            service => service.GetActiveAsync(cancellationToken),
            Times.Once);
    }
}
