using ErpSystem.Api.Services;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Enums;
using ErpSystem.Data;
using ErpSystem.Shared;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ErpSystem.Api.Tests.Services;

public sealed class OperationalUatBaselineSeederTests
{
    private static readonly ServiceProvider Provider = new ServiceCollection()
        .AddEntityFrameworkInMemoryDatabase()
        .BuildServiceProvider();

    [Fact]
    public async Task FinanceSeederBoundary_DetachesPreviouslyMaterializedGraph()
    {
        await using var db = CreateContext();
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(), Code = "UAT", Name = "Operational UAT",
            ContactEmail = "uat@example.invalid", Status = TenantStatus.Active,
            CreatedAt = DateTime.UtcNow, CreatedBy = "Tests"
        };
        db.Add(tenant);
        await db.SaveChangesAsync();
        db.ChangeTracker.Entries().Should().ContainSingle()
            .Which.State.Should().Be(EntityState.Unchanged);

        var seeder = new OperationalUatBaselineSeeder(
            db, null!, null!, null!, null!, null!, null!, null!,
            NullLogger<OperationalUatBaselineSeeder>.Instance);

        seeder.ResetTrackingAtSeederBoundary();

        db.ChangeTracker.Entries().Should().BeEmpty(
            "Finance reconciliation must not inherit stale entities from access seeding");
    }

    [Fact]
    public void FinanceGovernanceBlocker_IsPreservedWithoutMaskingOtherFailures()
    {
        OperationalUatBaselineSeeder.IsPreservableFinanceGovernanceBlocker(new InvalidOperationException(
                "FINANCE_CLASSIFICATION_ENABLED_MAPPING_LINEAGE_INVALID: user-owned mapping requires review."))
            .Should().BeTrue();
        OperationalUatBaselineSeeder.IsPreservableFinanceGovernanceBlocker(new InvalidOperationException(
                "A database update failed."))
            .Should().BeFalse();
    }

    [Fact]
    public async Task InventoryMasterPass_Twice_CreatesMissingRecordsAndPreservesExistingValues()
    {
        await using var db = CreateContext();
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(), Code = "UAT", Name = "Operational UAT",
            ContactEmail = "uat@example.invalid", Status = TenantStatus.Active,
            CreatedAt = DateTime.UtcNow, CreatedBy = "Tests"
        };
        var existingCategory = new InventoryCategory
        {
            Id = Guid.NewGuid(), TenantId = tenant.Id, Code = "FILT", Name = "Tenant-owned filters",
            DefaultUnitOfMeasure = "PACK", IsActive = true, CreatedBy = "Tests"
        };
        var existingWarehouse = new Warehouse
        {
            Id = Guid.NewGuid(), TenantId = tenant.Id, Code = "DEMO-PM", Name = "Tenant-owned warehouse",
            IsActive = true, CreatedBy = "Tests"
        };
        var existingItem = new InventoryItem
        {
            Id = Guid.NewGuid(), TenantId = tenant.Id, ItemCode = "FILTER-AIR-001",
            Name = "Tenant-owned filter", CategoryId = existingCategory.Id,
            UnitOfMeasure = "PACK", StandardCost = 99m, Status = ItemStatus.Active,
            CreatedBy = "Tests"
        };
        db.AddRange(
            tenant,
            new UnitOfMeasure
            {
                Id = Guid.NewGuid(), TenantId = tenant.Id, Code = "EA", Name = "Tenant-owned each",
                Category = "Quantity", IsBaseUnit = true, IsActive = true, CreatedBy = "Tests"
            },
            existingCategory,
            existingWarehouse,
            existingItem);
        await db.SaveChangesAsync();

        var seeder = new OperationalUatBaselineSeeder(
            db, null!, null!, null!, null!, null!, null!, null!,
            NullLogger<OperationalUatBaselineSeeder>.Instance);

        var first = await seeder.EnsureInventoryMasterDataAsync(tenant.Id, CancellationToken.None);
        var second = await seeder.EnsureInventoryMasterDataAsync(tenant.Id, CancellationToken.None);

        first.Should().Be(new InventoryMasterSeedCounts(4, 5, 1, 3, 8, 2));
        second.Should().Be(new InventoryMasterSeedCounts(0, 0, 0, 0, 0, 0));
        (await db.UnitsOfMeasure.CountAsync(value => value.TenantId == tenant.Id)).Should().Be(5);
        (await db.InventoryCategories.CountAsync(value => value.TenantId == tenant.Id)).Should().Be(6);
        (await db.Warehouses.CountAsync(value => value.TenantId == tenant.Id)).Should().Be(2);
        (await db.WarehouseLocations.CountAsync(value => value.TenantId == tenant.Id)).Should().Be(3);
        (await db.InventoryItems.CountAsync(value => value.TenantId == tenant.Id)).Should().Be(9);
        (await db.Suppliers.CountAsync()).Should().Be(0);
        (await db.BusinessPartners.CountAsync(value => value.TenantId == tenant.Id)).Should().Be(2);
        (await db.BusinessPartnerRoles.CountAsync(value => value.TenantId == tenant.Id &&
            value.RoleType == BusinessPartnerRoleType.Supplier && value.Status == BusinessPartnerRoleStatus.Active)).Should().Be(2);
        (await db.BusinessPartnerApProfileVersions.CountAsync(value => value.TenantId == tenant.Id &&
            value.Status == BusinessPartnerFinanceProfileStatus.Approved)).Should().Be(2);
        var itemUnits = await db.ItemUnitsOfMeasure.Where(value => value.TenantId == tenant.Id).ToListAsync();
        itemUnits.Should().HaveCount(8);
        foreach (var itemUnit in itemUnits)
        {
            var item = await db.InventoryItems.SingleAsync(value => value.Id == itemUnit.InventoryItemId);
            var unit = await db.UnitsOfMeasure.SingleAsync(value => value.Id == itemUnit.UnitOfMeasureId);
            unit.TenantId.Should().Be(item.TenantId);
            unit.Code.Should().Be(item.UnitOfMeasure);
            itemUnit.ConversionToBase.Should().Be(1m);
            itemUnit.IsBaseUnit.Should().BeTrue();
            itemUnit.IsPurchaseUnit.Should().BeTrue();
            itemUnit.IsStockingUnit.Should().BeTrue();
        }
        itemUnits.Should().NotContain(value => value.InventoryItemId == existingItem.Id,
            "existing tenant-owned conversion definitions must not be invented or changed");

        existingCategory.Name.Should().Be("Tenant-owned filters");
        existingCategory.DefaultUnitOfMeasure.Should().Be("PACK");
        existingWarehouse.Name.Should().Be("Tenant-owned warehouse");
        existingItem.Name.Should().Be("Tenant-owned filter");
        existingItem.UnitOfMeasure.Should().Be("PACK");
        existingItem.StandardCost.Should().Be(99m);
    }

    [Fact]
    public async Task ExistingPartnerAndDraftProfile_RemainUnapprovedAndUnchanged()
    {
        await using var db = CreateContext();
        var tenant = NewTenant("UAT");
        var partner = new BusinessPartner
        {
            Id = Guid.NewGuid(), TenantId = tenant.Id, PartnerCode = "SUP260001",
            PartnerName = "Tenant-owned supplier", PartnerType = "Supplier", IsActive = false,
            RegistrationStatus = "Draft", ApprovalStatus = "Pending", Currency = "USD", CreatedBy = "User"
        };
        var role = new BusinessPartnerRole
        {
            Id = Guid.NewGuid(), TenantId = tenant.Id, BusinessPartnerId = partner.Id,
            RoleType = BusinessPartnerRoleType.Supplier, Status = BusinessPartnerRoleStatus.Active,
            ActiveFromUtc = DateTime.UtcNow, CreatedBy = "User"
        };
        var profile = new BusinessPartnerApProfileVersion
        {
            Id = Guid.NewGuid(), TenantId = tenant.Id, BusinessPartnerRoleId = role.Id,
            VersionNumber = 1, Status = BusinessPartnerFinanceProfileStatus.Draft,
            EffectiveFrom = DateTime.UtcNow, ApReferenceNumber = "User draft", CreatedBy = "User"
        };
        db.AddRange(tenant, partner, role, profile);
        await db.SaveChangesAsync();
        var seeder = NewSeeder(db);

        var first = await seeder.EnsureInventoryMasterDataAsync(tenant.Id, CancellationToken.None);
        var second = await seeder.EnsureInventoryMasterDataAsync(tenant.Id, CancellationToken.None);

        first.Suppliers.Should().Be(1);
        second.Suppliers.Should().Be(0);
        partner.PartnerName.Should().Be("Tenant-owned supplier");
        partner.Currency.Should().Be("USD");
        partner.IsActive.Should().BeFalse();
        partner.RegistrationStatus.Should().Be("Draft");
        partner.ApprovalStatus.Should().Be("Pending");
        profile.Status.Should().Be(BusinessPartnerFinanceProfileStatus.Draft);
        profile.ApprovedAtUtc.Should().BeNull();
        profile.ApReferenceNumber.Should().Be("User draft");
        (await db.BusinessPartnerApProfileVersions.CountAsync(value => value.BusinessPartnerRoleId == role.Id)).Should().Be(1);
        (await db.Suppliers.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task SameCodesInAnotherTenant_DoNotSupplyTargetTenantLineage()
    {
        await using var db = CreateContext();
        var source = NewTenant("SOURCE");
        var target = NewTenant("TARGET");
        db.AddRange(source, target);
        await db.SaveChangesAsync();
        var seeder = NewSeeder(db);
        await seeder.EnsureInventoryMasterDataAsync(source.Id, CancellationToken.None);
        var sourcePartner = await db.BusinessPartners.SingleAsync(value => value.TenantId == source.Id && value.PartnerCode == "SUP260001");
        sourcePartner.PartnerName = "Source tenant-owned name";
        await db.SaveChangesAsync();

        var first = await seeder.EnsureInventoryMasterDataAsync(target.Id, CancellationToken.None);
        var second = await seeder.EnsureInventoryMasterDataAsync(target.Id, CancellationToken.None);

        first.Should().Be(new InventoryMasterSeedCounts(5, 6, 2, 3, 9, 2));
        second.Should().Be(new InventoryMasterSeedCounts(0, 0, 0, 0, 0, 0));
        sourcePartner.PartnerName.Should().Be("Source tenant-owned name");
        var targetPartners = await db.BusinessPartners.Where(value => value.TenantId == target.Id).ToListAsync();
        targetPartners.Should().HaveCount(2).And.NotContain(value => value.Id == sourcePartner.Id);
        foreach (var role in await db.BusinessPartnerRoles.Where(value => value.TenantId == target.Id).ToListAsync())
        {
            targetPartners.Should().Contain(value => value.Id == role.BusinessPartnerId);
            var profile = await db.BusinessPartnerApProfileVersions.SingleAsync(value => value.BusinessPartnerRoleId == role.Id);
            profile.TenantId.Should().Be(target.Id);
        }
        foreach (var link in await db.ItemUnitsOfMeasure.Where(value => value.TenantId == target.Id).ToListAsync())
        {
            (await db.InventoryItems.SingleAsync(value => value.Id == link.InventoryItemId)).TenantId.Should().Be(target.Id);
            (await db.UnitsOfMeasure.SingleAsync(value => value.Id == link.UnitOfMeasureId)).TenantId.Should().Be(target.Id);
        }
        (await db.Suppliers.CountAsync()).Should().Be(0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ProjectsSeededAdom_ReusesIdentityButNeverBypassesAnExistingDraft(bool hasDraft)
    {
        await using var db = CreateContext();
        var tenant = NewTenant("UAT");
        var partner = new BusinessPartner
        {
            Id = Guid.NewGuid(), TenantId = tenant.Id, PartnerCode = "CONT-GH-ADOM-BUILD",
            PartnerName = "Preserved Projects contractor", PartnerType = "Contractor", CreatedBy = "System",
            RegistrationStatus = "Approved", ApprovalStatus = "Approved", IsActive = true
        };
        db.AddRange(tenant, partner);
        BusinessPartnerApProfileVersion? draft = null;
        if (hasDraft)
        {
            var contractorRole = new BusinessPartnerRole
            {
                Id = Guid.NewGuid(), TenantId = tenant.Id, BusinessPartnerId = partner.Id,
                RoleType = BusinessPartnerRoleType.Contractor, Status = BusinessPartnerRoleStatus.Active,
                ActiveFromUtc = DateTime.UtcNow, CreatedBy = "User"
            };
            draft = new BusinessPartnerApProfileVersion
            {
                Id = Guid.NewGuid(), TenantId = tenant.Id, BusinessPartnerRoleId = contractorRole.Id,
                VersionNumber = 1, Status = BusinessPartnerFinanceProfileStatus.Draft,
                EffectiveFrom = DateTime.UtcNow, CreatedBy = "User"
            };
            db.AddRange(contractorRole, draft);
        }
        await db.SaveChangesAsync();
        var seeder = NewSeeder(db);

        var first = await seeder.EnsureInventoryMasterDataAsync(tenant.Id, CancellationToken.None);
        var second = await seeder.EnsureInventoryMasterDataAsync(tenant.Id, CancellationToken.None);

        first.Suppliers.Should().Be(1, "the existing Adom identity is reused, not duplicated");
        second.Suppliers.Should().Be(0);
        (await db.BusinessPartners.CountAsync(value => value.PartnerCode == partner.PartnerCode)).Should().Be(1);
        partner.PartnerName.Should().Be("Preserved Projects contractor");
        partner.PartnerType.Should().Be("Contractor");
        var supplierRoles = await db.BusinessPartnerRoles.Where(value => value.BusinessPartnerId == partner.Id &&
            value.RoleType == BusinessPartnerRoleType.Supplier).ToListAsync();
        if (hasDraft)
        {
            supplierRoles.Should().BeEmpty("a second AP capability must not bypass the existing draft");
            draft!.Status.Should().Be(BusinessPartnerFinanceProfileStatus.Draft);
            draft.ApprovedAtUtc.Should().BeNull();
        }
        else
        {
            var role = supplierRoles.Should().ContainSingle().Which;
            var profile = await db.BusinessPartnerApProfileVersions.SingleAsync(value => value.BusinessPartnerRoleId == role.Id);
            profile.Status.Should().Be(BusinessPartnerFinanceProfileStatus.Approved);
            profile.CreatedBy.Should().Be("Operational UAT baseline");
        }
    }

    private static Tenant NewTenant(string code) => new()
    {
        Id = Guid.NewGuid(), Code = code, Name = code, ContactEmail = "uat@example.invalid",
        Status = TenantStatus.Active, CreatedAt = DateTime.UtcNow, CreatedBy = "Tests"
    };

    private static OperationalUatBaselineSeeder NewSeeder(ApplicationDbContext db) => new(
        db, null!, null!, null!, null!, null!, null!, null!, NullLogger<OperationalUatBaselineSeeder>.Instance);

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"operational-uat-{Guid.NewGuid():N}")
            .UseInternalServiceProvider(Provider)
            .Options;
        return new ApplicationDbContext(options);
    }
}
