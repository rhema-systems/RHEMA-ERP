using System.Security.Claims;
using ErpSystem.Api.Controllers;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Inventory;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Data;
using ErpSystem.Data.Repositories;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Controllers.Inventory;

public sealed class InventoryItemIdentifiersControllerAuthorizationTests : IAsyncLifetime
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly ApplicationDbContext _db;
    private readonly UnitOfWork _unitOfWork;
    private readonly Mock<ICurrentUserProvider> _currentUser = new();
    private readonly Mock<IProcurementAccessControlService> _access = new();

    public InventoryItemIdentifiersControllerAuthorizationTests()
    {
        _db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options);
        _unitOfWork = new UnitOfWork(_db);
        _currentUser.SetupGet(value => value.TenantId).Returns(_tenantId);
        _currentUser.SetupGet(value => value.UserId).Returns(Guid.NewGuid());
        _currentUser.SetupGet(value => value.IsAuthenticated).Returns(true);
    }

    [Fact]
    public async Task Export_denies_an_internal_actor_without_inventory_read_or_master_data_capability()
    {
        _access.Setup(value => value.CheckCapabilityAsync(
                It.IsAny<ProcurementAccessCapabilityRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcurementAccessCapabilityDecisionDto { Allowed = false });
        _access.Setup(value => value.EnforceCapabilityAsync(
                It.IsAny<ProcurementAccessCapabilityRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcurementAccessCapabilityDecisionDto { Allowed = false });

        var result = await Controller().Export(CancellationToken.None);

        result.Should().BeOfType<ForbidResult>();
        _access.Verify(value => value.CheckCapabilityAsync(
            It.Is<ProcurementAccessCapabilityRequest>(request =>
                request.PermissionCode == "procurement.inventory.read" ||
                request.PermissionCode == "procurement.inventory.master-data.manage"),
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task Export_allows_the_shared_master_data_manager_capability()
    {
        await _db.InventoryItems.AddAsync(new InventoryItem
        {
            TenantId = _tenantId, CategoryId = Guid.NewGuid(), ItemCode = "EXPORT-001", Name = "Export item"
        });
        await _db.SaveChangesAsync();
        _access.Setup(value => value.CheckCapabilityAsync(
                It.IsAny<ProcurementAccessCapabilityRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProcurementAccessCapabilityRequest request, string _, CancellationToken __) =>
                new ProcurementAccessCapabilityDecisionDto
                {
                    Allowed = request.PermissionCode == "procurement.inventory.master-data.manage"
                });

        var result = await Controller().Export(CancellationToken.None);

        var file = result.Should().BeOfType<FileContentResult>().Subject;
        file.ContentType.Should().Be("text/csv");
        file.FileContents.Should().NotBeEmpty();
        _access.Verify(value => value.EnforceCapabilityAsync(
            It.IsAny<ProcurementAccessCapabilityRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private InventoryItemIdentifiersController Controller()
    {
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim("tenant_id", _tenantId.ToString())], "Test"))
        };
        return new InventoryItemIdentifiersController(
            _unitOfWork,
            Mock.Of<IInventoryItemIdentifierService>(),
            _currentUser.Object,
            _access.Object,
            NullLogger<InventoryItemIdentifiersController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext }
        };
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        _unitOfWork.Dispose();
        await _db.DisposeAsync();
    }
}
