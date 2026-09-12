using System.Security.Claims;
using ErpSystem.Api.Controllers.Inventory;
using ErpSystem.Core.DTOs.Inventory;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.DocumentManagement;
using ErpSystem.Core.Interfaces.Inventory;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.Services.Inventory;
using ErpSystem.Core.Services.Procurement;
using ErpSystem.Data;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Controllers.Inventory;

public sealed class PhysicalCountDecisionOptionsSecurityTests : IDisposable
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _actorId = Guid.NewGuid();
    private readonly Guid _warehouseId = Guid.NewGuid();
    private readonly Guid _locationId = Guid.NewGuid();
    private readonly ApplicationDbContext _db = new(new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options);
    private readonly Mock<ICurrentUserService> _currentUser = new();
    private readonly Mock<IProcurementAccessControlService> _access = new();

    public PhysicalCountDecisionOptionsSecurityTests()
    {
        _currentUser.SetupGet(x => x.TenantId).Returns(_tenantId);
        _currentUser.SetupGet(x => x.UserId).Returns(_actorId.ToString());
        _access.Setup(x => x.EnforceCapabilityAsync(It.IsAny<ProcurementAccessCapabilityRequest>(),
            It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcurementAccessCapabilityDecisionDto { Allowed = true });
    }

    [Theory]
    [InlineData("TDC_STORES_MANAGER", "PendingStoresApproval")]
    [InlineData("TDC_FINANCE_REVIEWER", "PendingFinanceApproval")]
    [InlineData("TDC_INTERNAL_AUDIT", "PendingAuditAttestation")]
    public async Task Decision_readers_use_the_saved_count_scope_and_tenant_settings(string role, string status)
    {
        _currentUser.SetupGet(x => x.Roles).Returns(new[] { role });
        var count = await AddCount(status: status);
        await _db.SystemSettings.AddAsync(new SystemSettings {
            TenantId = Guid.NewGuid(), Key = PhysicalCountDecisionPolicy.SettingKey,
            Value = PhysicalCountDecisionPolicy.Serialize(new List<PhysicalCountDecisionOption> {
                new PhysicalCountDecisionOption("APPROVE", "Foreign tenant approval", "ApproveAdjustment"),
                new PhysicalCountDecisionOption("CHECK", "Foreign investigation", "Investigate")
            })
        });
        await _db.SaveChangesAsync();
        using var cts = new CancellationTokenSource();
        var controller = Controller();
        controller.Request.QueryString = new QueryString("?warehouseId=" + Guid.NewGuid());

        var result = await controller.GetDecisionOptions(count.Id, cts.Token);

        var setup = result.Result.Should().BeOfType<OkObjectResult>().Subject.Value
            .Should().BeOfType<PhysicalCountDecisionSetup>().Subject;
        setup.Decisions.Should().NotContain(x => x.Label.Contains("Foreign"));
        setup.Decisions.Select(x => x.Effect).Should().BeEquivalentTo("ApproveAdjustment", "Investigate");
        _access.Verify(x => x.EnforceCapabilityAsync(It.Is<ProcurementAccessCapabilityRequest>(request =>
            request.PermissionCode == "procurement.inventory.read" && request.WarehouseId == _warehouseId &&
            request.LocationId == _locationId && request.RequireLocationScope &&
            request.SourceType == "PhysicalCount" && request.SourceReference == count.CountNumber),
            "count-decision-options-test", cts.Token), Times.Once);
    }

    [Fact]
    public async Task Warehouse_wide_count_requires_all_location_scope_without_inventing_a_bin()
    {
        var count = await AddCount(locationScoped: false);
        var result = await Controller().GetDecisionOptions(count.Id, CancellationToken.None);
        result.Result.Should().BeOfType<OkObjectResult>();
        _access.Verify(x => x.EnforceCapabilityAsync(It.Is<ProcurementAccessCapabilityRequest>(request =>
            request.WarehouseId == _warehouseId && request.LocationId == null && request.RequireLocationScope),
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("foreign-tenant")]
    [InlineData("deleted")]
    [InlineData("missing")]
    public async Task Inaccessible_count_identity_returns_not_found_without_reading_decisions(string target)
    {
        var count = await AddCount(tenantId: target == "foreign-tenant" ? Guid.NewGuid() : _tenantId,
            deleted: target == "deleted");
        var result = await Controller().GetDecisionOptions(target == "missing" ? Guid.NewGuid() : count.Id, CancellationToken.None);
        result.Result.Should().BeOfType<NotFoundObjectResult>();
        _access.Verify(x => x.EnforceCapabilityAsync(It.IsAny<ProcurementAccessCapabilityRequest>(),
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Denied_warehouse_or_location_scope_never_returns_decisions(bool locationScoped)
    {
        var count = await AddCount(locationScoped: locationScoped);
        _access.Setup(x => x.EnforceCapabilityAsync(It.IsAny<ProcurementAccessCapabilityRequest>(),
            It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcurementAccessCapabilityDecisionDto { Allowed = false, Message = "No responsibility for this count scope." });
        var result = await Controller().GetDecisionOptions(count.Id, CancellationToken.None);
        result.Result.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(403);
    }

    [Fact]
    public async Task Thrown_central_scope_denial_is_preserved_as_forbidden()
    {
        var count = await AddCount();
        _access.Setup(x => x.EnforceCapabilityAsync(It.IsAny<ProcurementAccessCapabilityRequest>(),
            It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ProcurementAccessAuthorizationException("No assigned warehouse."));
        var result = await Controller().GetDecisionOptions(count.Id, CancellationToken.None);
        result.Result.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(403);
    }

    [Fact]
    public async Task Missing_count_id_is_a_clear_bad_request_not_an_unscoped_capability_check()
    {
        (await Controller().GetDecisionOptions(null, CancellationToken.None)).Result.Should().BeOfType<BadRequestObjectResult>();
        (await Controller().GetDecisionOptions(Guid.Empty, CancellationToken.None)).Result.Should().BeOfType<BadRequestObjectResult>();
        _access.Verify(x => x.EnforceCapabilityAsync(It.IsAny<ProcurementAccessCapabilityRequest>(),
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Missing_tenant_is_forbidden_before_any_scope_or_setting_lookup()
    {
        _currentUser.SetupGet(x => x.TenantId).Returns((Guid?)null);
        var result = await Controller().GetDecisionOptions(Guid.NewGuid(), CancellationToken.None);
        result.Result.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(403);
    }

    [Fact]
    public async Task Admin_setup_retains_its_nonwarehouse_master_data_permission()
    {
        (await Controller().GetDecisionSetup(CancellationToken.None)).Result.Should().BeOfType<OkObjectResult>();
        _access.Verify(x => x.EnforceCapabilityAsync(It.Is<ProcurementAccessCapabilityRequest>(request =>
            request.PermissionCode == "procurement.inventory.master-data.manage" && request.WarehouseId == null &&
            request.LocationId == null && !request.RequireLocationScope && request.SourceType == "PhysicalCountDecisionSetup"),
            "count-decision-options-test", It.IsAny<CancellationToken>()), Times.Once);
    }

    private async Task<PhysicalCount> AddCount(Guid? tenantId = null, bool deleted = false,
        bool locationScoped = true, string status = "PendingStoresApproval")
    {
        var count = new PhysicalCount { Id = Guid.NewGuid(), TenantId = tenantId ?? _tenantId,
            CountNumber = "PC-OPTIONS-001", WarehouseId = _warehouseId,
            LocationId = locationScoped ? _locationId : null, Status = status, IsDeleted = deleted };
        _db.PhysicalCounts.Add(count);
        await _db.SaveChangesAsync();
        return count;
    }

    private PhysicalCountsController Controller()
    {
        var controller = new PhysicalCountsController(Mock.Of<IPhysicalCountService>(), _currentUser.Object, _access.Object,
            Mock.Of<IControlledFileUploadService>(), Mock.Of<ICentralDocumentRepositoryFileService>(), _db,
            NullLogger<PhysicalCountsController>.Instance);
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext {
            TraceIdentifier = "count-decision-options-test",
            User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, _actorId.ToString()) }, "test"))
        } };
        return controller;
    }

    public void Dispose() => _db.Dispose();
}
