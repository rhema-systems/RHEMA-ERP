using System.Security.Claims;
using ErpSystem.Api.Controllers;
using ErpSystem.Core.DTOs.Inventory;
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
using Microsoft.EntityFrameworkCore.Diagnostics;
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
    private readonly Mock<IInventoryItemIdentifierService> _identifiers = new();

    public InventoryItemIdentifiersControllerAuthorizationTests()
    {
        _db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options);
        _unitOfWork = new UnitOfWork(_db);
        _currentUser.SetupGet(value => value.TenantId).Returns(_tenantId);
        _currentUser.SetupGet(value => value.UserId).Returns(Guid.NewGuid());
        _currentUser.SetupGet(value => value.IsAuthenticated).Returns(true);
        _identifiers.Setup(value => value.Normalize(It.IsAny<string?>()))
            .Returns((string? value) => value);
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

    [Fact]
    public async Task Exported_multiline_csv_record_can_be_imported_again()
    {
        var item = new InventoryItem
        {
            TenantId = _tenantId,
            CategoryId = Guid.NewGuid(),
            ItemCode = "CSV-MULTILINE-001",
            Name = "Sterile gloves\nlarge pack",
            Barcode = "CSV-MULTILINE-BARCODE"
        };
        await _db.InventoryItems.AddAsync(item);
        await _db.SaveChangesAsync();
        AllowInventoryAccess();

        var export = (FileContentResult)await Controller().Export(CancellationToken.None);
        await using var stream = new MemoryStream(export.FileContents);
        var upload = new FormFile(stream, 0, stream.Length, "file", "inventory-identifiers.csv")
        {
            Headers = new HeaderDictionary(),
            ContentType = "text/csv"
        };

        var import = await Controller().Import(upload, CancellationToken.None);

        var result = import.Result.Should().BeOfType<OkObjectResult>().Subject;
        result.Value.Should().BeAssignableTo<InventoryIdentifierImportResultDto>()
            .Which.UpdatedItems.Should().Be(1);
        (await _db.InventoryItems.SingleAsync(value => value.Id == item.Id)).Barcode
            .Should().Be("CSV-MULTILINE-BARCODE");
    }

    [Theory]
    [InlineData("\t=HYPERLINK(\"https://invalid.test\")")]
    [InlineData("  +1+1")]
    [InlineData("\r@SUM(A1:A2)")]
    public async Task Export_neutralizes_formula_prefixes_after_leading_whitespace(string unsafeName)
    {
        await _db.InventoryItems.AddAsync(new InventoryItem
        {
            TenantId = _tenantId,
            CategoryId = Guid.NewGuid(),
            ItemCode = $"CSV-FORMULA-{Guid.NewGuid():N}",
            Name = unsafeName
        });
        await _db.SaveChangesAsync();
        AllowInventoryAccess();

        var export = (FileContentResult)await Controller().Export(CancellationToken.None);
        var csv = System.Text.Encoding.UTF8.GetString(export.FileContents);

        csv.Should().Contain($"\"'{unsafeName.Replace("\"", "\"\"")}\"");
    }

    private void AllowInventoryAccess()
    {
        _access.Setup(value => value.CheckCapabilityAsync(
                It.IsAny<ProcurementAccessCapabilityRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcurementAccessCapabilityDecisionDto { Allowed = true });
        _access.Setup(value => value.EnforceCapabilityAsync(
                It.IsAny<ProcurementAccessCapabilityRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcurementAccessCapabilityDecisionDto { Allowed = true });
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
            _identifiers.Object,
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
