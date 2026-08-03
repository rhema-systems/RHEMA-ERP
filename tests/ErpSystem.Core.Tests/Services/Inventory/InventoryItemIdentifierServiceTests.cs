using System.Reflection;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Services.Inventory;
using ErpSystem.Data;
using ErpSystem.Data.Migrations;
using ErpSystem.Data.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Inventory;

public sealed class InventoryItemIdentifierServiceTests : IAsyncLifetime
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly ApplicationDbContext _context;
    private readonly UnitOfWork _unitOfWork;
    private readonly InventoryItemIdentifierService _service;

    public InventoryItemIdentifierServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        // The service owns explicit tenant predicates; keep the test context unscoped so
        // model caching cannot freeze a different test instance's tenant constant.
        _context = new ApplicationDbContext(options);
        _unitOfWork = new UnitOfWork(_context);
        _service = new InventoryItemIdentifierService(_unitOfWork);
    }

    [Theory]
    [InlineData("  abc-123  ", "ABC-123")]
    [InlineData("", null)]
    [InlineData("   ", null)]
    public void Normalize_produces_the_authoritative_identifier(string input, string? expected)
    {
        _service.Normalize(input).Should().Be(expected);
    }

    [Fact]
    public async Task Resolve_returns_primary_alternate_qr_and_unit_matches()
    {
        var item = await AddItemAsync("ITEM-001", "PRIMARY", "ALTERNATE", "QR-CODE");
        var unit = new UnitOfMeasure { TenantId = _tenantId, Code = "BOX", Name = "Box", IsActive = true };
        await _context.Set<UnitOfMeasure>().AddAsync(unit);
        await _context.Set<ItemUnitOfMeasure>().AddAsync(new ItemUnitOfMeasure
        {
            TenantId = _tenantId,
            InventoryItemId = item.Id,
            UnitOfMeasureId = unit.Id,
            Barcode = "BOX-CODE",
            ConversionToBase = 12m,
            IsActive = true
        });
        await _context.SaveChangesAsync();

        (await _service.ResolveAsync(_tenantId, " primary "))!.IdentifierKind.Should().Be("PrimaryBarcode");
        (await _service.ResolveAsync(_tenantId, "alternate"))!.IdentifierKind.Should().Be("AlternateBarcode");
        (await _service.ResolveAsync(_tenantId, "qr-code"))!.IdentifierKind.Should().Be("QRCode");
        var unitMatch = await _service.ResolveAsync(_tenantId, "box-code");
        unitMatch!.IdentifierKind.Should().Be("UnitBarcode");
        unitMatch.UnitCode.Should().Be("BOX");
        unitMatch.ConversionToBase.Should().Be(12m);
    }

    [Fact]
    public async Task Item_validation_rejects_an_identifier_already_used_by_a_unit()
    {
        var item = await AddItemAsync("ITEM-001");
        var unit = new UnitOfMeasure { TenantId = _tenantId, Code = "EA", Name = "Each", IsActive = true };
        await _context.Set<UnitOfMeasure>().AddAsync(unit);
        await _context.Set<ItemUnitOfMeasure>().AddAsync(new ItemUnitOfMeasure
        {
            TenantId = _tenantId,
            InventoryItemId = item.Id,
            UnitOfMeasureId = unit.Id,
            Barcode = "SHARED",
            IsActive = true
        });
        await _context.SaveChangesAsync();

        var act = () => _service.ValidateItemIdentifiersAsync(_tenantId, Guid.NewGuid(), "shared", null, null);

        await act.Should().ThrowAsync<ErpSystem.Core.Interfaces.Inventory.InventoryIdentifierConflictException>()
            .WithMessage("*unique across the complete item master*");
    }

    [Fact]
    public async Task Unit_validation_rejects_an_identifier_used_in_any_item_slot()
    {
        var item = await AddItemAsync("ITEM-001", alternate: "ALT-CODE");

        var act = () => _service.ValidateUnitIdentifierAsync(_tenantId, item.Id, null, "alt-code");

        await act.Should().ThrowAsync<ErpSystem.Core.Interfaces.Inventory.InventoryIdentifierConflictException>();
    }

    [Fact]
    public async Task Same_identifier_in_another_tenant_does_not_leak_or_conflict()
    {
        await _context.Set<InventoryItem>().AddAsync(new InventoryItem
        {
            TenantId = Guid.NewGuid(),
            ItemCode = "OTHER-001",
            Name = "Other tenant item",
            CategoryId = Guid.NewGuid(),
            Barcode = "TENANT-SCOPED"
        });
        await _context.SaveChangesAsync();

        await _service.ValidateItemIdentifiersAsync(_tenantId, null, "tenant-scoped", null, null);
        (await _service.ResolveAsync(_tenantId, "tenant-scoped")).Should().BeNull();
    }

    [Fact]
    public void Ef_model_contains_all_filtered_tenant_unique_identifier_indexes()
    {
        var itemType = _context.Model.FindEntityType(typeof(InventoryItem))!;
        var itemIndexes = itemType.GetIndexes();
        itemIndexes.Should().Contain(index => index.Properties.Select(property => property.Name)
            .SequenceEqual(new[] { "TenantId", "Barcode" }) && index.IsUnique && index.GetFilter()!.Contains("IsDeleted"));
        itemIndexes.Should().Contain(index => index.Properties.Select(property => property.Name)
            .SequenceEqual(new[] { "TenantId", "AlternateBarcode" }) && index.IsUnique);
        itemIndexes.Should().Contain(index => index.Properties.Select(property => property.Name)
            .SequenceEqual(new[] { "TenantId", "QRCode" }) && index.IsUnique);

        var unitType = _context.Model.FindEntityType(typeof(ItemUnitOfMeasure))!;
        var unitIndexes = unitType.GetIndexes();
        unitIndexes.Should().Contain(index => index.Properties.Select(property => property.Name)
            .SequenceEqual(new[] { "TenantId", "Barcode" }) && index.IsUnique);

        var sqlServerOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=Tdc0601ModelOnly;Trusted_Connection=True")
            .Options;
        using var sqlServerContext = new ApplicationDbContext(sqlServerOptions);
        sqlServerContext.Model.FindEntityType(typeof(InventoryItem))!
            .FindAnnotation("SqlServer:UseSqlOutputClause")!.Value.Should().Be(false);
        sqlServerContext.Model.FindEntityType(typeof(ItemUnitOfMeasure))!
            .FindAnnotation("SqlServer:UseSqlOutputClause")!.Value.Should().Be(false);
    }

    [Fact]
    public void Migration_rollback_keeps_item_codes_tenant_scoped()
    {
        var migration = new TDC0601InventoryItemIdentifiers();
        var builder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
        migration.GetType().GetMethod("Down", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(migration, new object[] { builder });

        var rollbackIndex = builder.Operations.OfType<CreateIndexOperation>().Should().ContainSingle(operation =>
            operation.Name == "IX_InventoryItems_ItemCode").Which;
        rollbackIndex.Columns.Should().Equal("TenantId", "ItemCode");
        rollbackIndex.IsUnique.Should().BeTrue();
        rollbackIndex.Filter.Should().Be("[IsDeleted] = 0");
        builder.Operations.OfType<CreateIndexOperation>().Should().NotContain(operation =>
            operation.Columns.SequenceEqual(new[] { "ItemCode" }));
    }

    private async Task<InventoryItem> AddItemAsync(
        string code,
        string? barcode = null,
        string? alternate = null,
        string? qrCode = null)
    {
        var item = new InventoryItem
        {
            TenantId = _tenantId,
            ItemCode = code,
            Name = code,
            CategoryId = Guid.NewGuid(),
            Barcode = barcode,
            AlternateBarcode = alternate,
            QRCode = qrCode
        };
        await _context.Set<InventoryItem>().AddAsync(item);
        await _context.SaveChangesAsync();
        return item;
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        await _context.DisposeAsync();
        _unitOfWork.Dispose();
    }
}
