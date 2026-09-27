using ErpSystem.Api.Controllers.HR;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;
using System.Text.Json;

namespace ErpSystem.Api.Tests.Controllers;

public class GlobalSearchSafetyTests
{
    [Fact]
    public async Task PermitSearchUsesScopedOwnerAndReturnsOnlyBoundedSummaryFields()
    {
        var records = Enumerable.Range(1, 25).Select(i => new SheEnvironmentalPermitSummaryDto
        { Id = Guid.NewGuid(), RegisterNumber = $"PER-{i}", PermitName = "Operating licence", ResponsibleOfficerName = "Not in search projection" }).ToList();
        var owner = new Mock<ISheEnvironmentalPermitService>(MockBehavior.Strict);
        owner.Setup(service => service.GetAllAsync(null, null, "licence", null, default)).ReturnsAsync(records);
        var controller = new SheEnvironmentalComplianceController(owner.Object,
            Mock.Of<ISheEnvironmentalGovernanceService>(), Mock.Of<ICurrentUserService>());

        var response = Assert.IsType<OkObjectResult>(await controller.SearchPermits(" licence ", 100));
        var data = JsonSerializer.SerializeToElement(response.Value, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        Assert.Equal(20, data.GetArrayLength());
        Assert.Equal(records[0].Id, data[0].GetProperty("id").GetGuid());
        Assert.Equal(new[] { "id", "permitName", "registerNumber", "status" }, data[0].EnumerateObject().Select(property => property.Name).OrderBy(name => name));
        var minimum = Assert.IsType<OkObjectResult>(await controller.SearchPermits("licence", 0));
        Assert.Single(JsonSerializer.SerializeToElement(minimum.Value).EnumerateArray());
        owner.Verify(service => service.GetAllAsync(null, null, "licence", null, default), Times.Exactly(2));
        await controller.SearchPermits("x");
        await controller.SearchPermits(new string('x', 101));
        owner.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(null, 25)]
    [InlineData(5, 5)]
    [InlineData(100, 20)]
    [InlineData(0, 1)]
    public async Task Documents_PreserveOwnerSearch_AndOnlyBoundWhenRequested(int? take, int expected)
    {
        var records = Enumerable.Range(1, 25).Select(i => new SheControlledDocumentSummaryDto
        { Id = Guid.NewGuid(), DocumentNumber = $"DOC-{i}", Title = "Safety plan" }).ToList();
        var owner = new Mock<ISheControlledDocumentService>(MockBehavior.Strict);
        owner.Setup(service => service.GetAllAsync(null, null, "Safety plan", null, default))
            .ReturnsAsync(records);
        var controller = new SheControlledDocumentController(owner.Object, Mock.Of<ICurrentUserService>());

        var response = await controller.GetAll(null, null, "Safety plan", null, take);
        var result = Assert.IsType<OkObjectResult>(response.Result);
        var returned = Assert.IsAssignableFrom<IEnumerable<SheControlledDocumentSummaryDto>>(result.Value).ToList();
        Assert.Equal(records.Take(expected).Select(item => item.Id), returned.Select(item => item.Id));
        owner.VerifyAll();
        owner.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(null, 25)]
    [InlineData(5, 5)]
    [InlineData(100, 20)]
    [InlineData(-1, 1)]
    public async Task Reviews_PreserveOwnerSearch_AndOnlyBoundWhenRequested(int? take, int expected)
    {
        var records = Enumerable.Range(1, 25).Select(i => new SheEnvironmentalReviewSummaryDto
        { Id = Guid.NewGuid(), ReviewNumber = $"ENV-{i}", ProjectName = "Tema" }).ToList();
        var owner = new Mock<ISheEnvironmentalReviewService>(MockBehavior.Strict);
        owner.Setup(service => service.GetAllAsync(null, "Tema", default)).ReturnsAsync(records);
        var controller = new SheEnvironmentalReviewController(owner.Object,
            Mock.Of<ISheMonthlyEnvironmentalReportService>(), Mock.Of<ICurrentUserService>());

        var response = await controller.GetReviews(null, "Tema", take);
        var result = Assert.IsType<OkObjectResult>(response.Result);
        var returned = Assert.IsAssignableFrom<IEnumerable<SheEnvironmentalReviewSummaryDto>>(result.Value).ToList();
        Assert.Equal(records.Take(expected).Select(item => item.Id), returned.Select(item => item.Id));
        owner.VerifyAll();
        owner.VerifyNoOtherCalls();
    }
}
