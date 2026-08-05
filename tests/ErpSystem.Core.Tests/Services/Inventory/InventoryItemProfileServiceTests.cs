using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces.Inventory;
using ErpSystem.Core.Services.Inventory;
using ErpSystem.Data;
using ErpSystem.Data.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Inventory;

public sealed class InventoryItemProfileServiceTests : IAsyncLifetime
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly ApplicationDbContext _context;
    private readonly UnitOfWork _unitOfWork;
    private readonly InventoryItemProfileService _service;
    private InventoryCategory _category = null!;

    public InventoryItemProfileServiceTests()
    {
        _context = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options);
        _unitOfWork = new UnitOfWork(_context);
        var identifiers = new InventoryItemIdentifierService(_unitOfWork);
        _service = new InventoryItemProfileService(_unitOfWork, identifiers);
    }

    public async Task InitializeAsync()
    {
        _category = new InventoryCategory
        {
            TenantId = _tenantId,
            Code = "GEN",
            Name = "General",
            IsActive = true
        };
        await _context.AddRangeAsync(
            _category,
            new UnitOfMeasure { TenantId = _tenantId, Code = "EA", Name = "Each", IsActive = true });
        await _context.SaveChangesAsync();
    }

    [Fact]
    [Trait("Batch", "TDC-0616")]
    public async Task Normalizes_and_accepts_the_complete_default_profile()
    {
        var item = ValidItem();
        item.ItemCode = " stock-001 ";
        item.Name = "  Test stock item  ";
        item.UnitOfMeasure = " ea ";
        item.IsProjectApplicable = true;
        item.IsCostCentreApplicable = true;

        await _service.NormalizeAndValidateAsync(item, null);

        item.ItemCode.Should().Be("STOCK-001");
        item.Name.Should().Be("Test stock item");
        item.UnitOfMeasure.Should().Be("EA");
        item.ValuationMethod.Should().Be(ValuationMethod.WeightedAverage);
        item.IsProjectApplicable.Should().BeTrue();
        item.IsCostCentreApplicable.Should().BeTrue();
    }

    [Theory]
    [Trait("Batch", "TDC-0616")]
    [InlineData(-1, 10, 5, "ITEM_MINIMUM_LEVEL_INVALID")]
    [InlineData(11, 10, 5, "ITEM_STOCK_LEVEL_RANGE_INVALID")]
    [InlineData(1, 10, 11, "ITEM_REORDER_LEVEL_RANGE_INVALID")]
    public async Task Rejects_invalid_replenishment_ranges(decimal minimum, decimal maximum, decimal reorder, string code)
    {
        var item = ValidItem();
        item.MinimumLevel = minimum;
        item.MaximumLevel = maximum;
        item.ReorderLevel = reorder;

        var action = () => _service.NormalizeAndValidateAsync(item, null);

        (await action.Should().ThrowAsync<InventoryItemProfileValidationException>())
            .Which.Code.Should().Be(code);
    }

    [Fact]
    [Trait("Batch", "TDC-0616")]
    public async Task Allows_combined_traceability_and_rejects_missing_shelf_life()
    {
        var serialBatch = ValidItem();
        serialBatch.IsSerialTracked = true;
        serialBatch.IsBatchTracked = true;
        serialBatch.IsLotTracked = true;
        await _service.NormalizeAndValidateAsync(serialBatch, null);

        var expiring = ValidItem();
        expiring.IsExpirationTracked = true;
        expiring.ShelfLifeDays = null;
        var expiryAction = () => _service.NormalizeAndValidateAsync(expiring, null);
        (await expiryAction.Should().ThrowAsync<InventoryItemProfileValidationException>())
            .Which.Code.Should().Be("ITEM_SHELF_LIFE_REQUIRED");
    }

    [Fact]
    [Trait("Batch", "TDC-0616")]
    public async Task Rejects_foreign_tenant_category_and_uom_without_disclosing_rows()
    {
        var item = ValidItem();
        item.CategoryId = Guid.NewGuid();

        var categoryAction = () => _service.NormalizeAndValidateAsync(item, null);
        (await categoryAction.Should().ThrowAsync<InventoryItemProfileValidationException>())
            .Which.Code.Should().Be("ITEM_CATEGORY_NOT_FOUND");

        item.CategoryId = _category.Id;
        item.UnitOfMeasure = "FOREIGN";
        await _context.AddAsync(new UnitOfMeasure
        {
            TenantId = Guid.NewGuid(),
            Code = "FOREIGN",
            Name = "Foreign tenant unit",
            IsActive = true
        });
        await _context.SaveChangesAsync();

        var uomAction = () => _service.NormalizeAndValidateAsync(item, null);
        (await uomAction.Should().ThrowAsync<InventoryItemProfileValidationException>())
            .Which.Code.Should().Be("ITEM_UOM_NOT_FOUND");
    }

    [Fact]
    [Trait("Batch", "TDC-0616")]
    public async Task Rejects_valuation_method_change_after_transaction_lock()
    {
        var persisted = ValidItem();
        persisted.IsValuationLocked = true;
        await _context.AddAsync(persisted);
        await _context.SaveChangesAsync();

        var proposal = ValidItem();
        proposal.Id = persisted.Id;
        proposal.ValuationMethod = ValuationMethod.FIFO;
        var action = () => _service.NormalizeAndValidateAsync(proposal, persisted.Id);

        (await action.Should().ThrowAsync<InventoryItemProfileValidationException>())
            .Which.Code.Should().Be("ITEM_VALUATION_LOCKED");
    }

    [Fact]
    [Trait("Batch", "TDC-0616")]
    public void Ef_model_has_profile_concurrency_applicability_and_sql_guards()
    {
        var sqlContext = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=Tdc0616ModelOnly;Trusted_Connection=True")
            .Options);
        var entity = sqlContext.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(InventoryItem))!;

        entity.FindProperty(nameof(InventoryItem.RowVersion))!.IsConcurrencyToken.Should().BeTrue();
        entity.FindProperty(nameof(InventoryItem.IsProjectApplicable)).Should().NotBeNull();
        entity.FindProperty(nameof(InventoryItem.IsCostCentreApplicable)).Should().NotBeNull();
        entity.GetCheckConstraints().Select(value => value.Name).Should().Contain(new[]
        {
            "CK_InventoryItems_ProfileRequired",
            "CK_InventoryItems_ProfileLevels",
            "CK_InventoryItems_ProfileEnums",
            "CK_InventoryItems_ProfileTracking"
        });
    }

    private InventoryItem ValidItem() => new()
    {
        TenantId = _tenantId,
        ItemCode = "STOCK-001",
        Name = "Test stock item",
        CategoryId = _category.Id,
        UnitOfMeasure = "EA",
        ItemType = ItemType.StockItem,
        Status = ItemStatus.Active,
        ValuationMethod = ValuationMethod.WeightedAverage,
        MinimumLevel = 1,
        MaximumLevel = 20,
        ReorderLevel = 5,
        ReorderQuantity = 10,
        LeadTimeDays = 7
    };

    public async Task DisposeAsync()
    {
        await _context.DisposeAsync();
        _unitOfWork.Dispose();
    }
}
