using ErpSystem.Api.Services.Maintenance;
using ErpSystem.Core.Entities.Estate;
using ErpSystem.Core.Entities.Finance.FixedAssets;
using ErpSystem.Core.Entities.Maintenance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Data;
using ErpSystem.Data.Repositories.Maintenance;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Maintenance;

public sealed class MaintenanceAssetSourceIntegrationTests
{
    [Fact]
    public async Task FinanceAsset_InMaintenanceCategory_AppearsWithCanonicalDetails()
    {
        await using var fixture = CreateFixture();
        var category = AddFixedAssetCategory(fixture, requiresMaintenance: true);
        var asset = AddFixedAsset(fixture, category, "FA-MAINT", "Generator", "Plant Room");
        await fixture.Db.SaveChangesAsync();

        var result = await fixture.Service.GetSelectionOptionsAsync();

        result.Should().ContainSingle(item => item.AssetSource == JobCardAssetSource.FixedAsset
            && item.SourceAssetId == asset.Id
            && item.AssetCode == "FA-MAINT"
            && item.AssetName == "Generator"
            && item.Location == "Plant Room");
    }

    [Fact]
    public async Task FinanceAsset_OutsideMaintenanceCategory_IsExcluded()
    {
        await using var fixture = CreateFixture();
        var category = AddFixedAssetCategory(fixture, requiresMaintenance: false);
        var asset = AddFixedAsset(fixture, category, "FA-OFF", "Office Artwork", "Reception");
        await fixture.Db.SaveChangesAsync();

        var result = await fixture.Service.GetSelectionOptionsAsync();

        result.Should().NotContain(item => item.SourceAssetId == asset.Id);
    }

    [Theory]
    [InlineData(EstateManagedAssetStatus.Sold)]
    [InlineData(EstateManagedAssetStatus.Leased)]
    [InlineData(EstateManagedAssetStatus.Occupied)]
    public async Task EstateAsset_OperationalOwnershipStatuses_RemainSelectable(EstateManagedAssetStatus status)
    {
        await using var fixture = CreateFixture();
        var asset = AddEstateAsset(fixture, status, isExternallyPublished: true);
        await fixture.Db.SaveChangesAsync();

        var result = await fixture.Service.GetSelectionOptionsAsync();

        result.Should().ContainSingle(item => item.AssetSource == JobCardAssetSource.EstateManagedAsset
            && item.SourceAssetId == asset.Id
            && item.Status == status.ToString());
    }

    [Fact]
    public async Task EstateAsset_Retired_IsExcluded()
    {
        await using var fixture = CreateFixture();
        var asset = AddEstateAsset(fixture, EstateManagedAssetStatus.Retired, isExternallyPublished: false);
        await fixture.Db.SaveChangesAsync();

        var result = await fixture.Service.GetSelectionOptionsAsync();

        result.Should().NotContain(item => item.SourceAssetId == asset.Id);
    }

    [Fact]
    public async Task ReleasedProjectUnit_IsSelectableWithoutFinanceCategory()
    {
        await using var fixture = CreateFixture();
        var asset = AddEstateAsset(fixture, EstateManagedAssetStatus.Available, isExternallyPublished: false);
        asset.SourceType = EstateManagedAssetSourceType.ProjectUnit;
        asset.ProjectUnitId = Guid.NewGuid();
        asset.ProjectUnitCode = "UNIT-A-01";
        asset.IsPublishedFromProject = true;
        await fixture.Db.SaveChangesAsync();

        var result = await fixture.Service.GetSelectionOptionsAsync();

        result.Should().ContainSingle(item => item.AssetSource == JobCardAssetSource.EstateManagedAsset
            && item.SourceAssetId == asset.Id);
    }

    [Fact]
    public async Task ExistingMaintenanceAsset_RemainsSelectableAsLegacySource()
    {
        await using var fixture = CreateFixture();
        var category = AddMaintenanceCategory(fixture, "LEGACY");
        var legacy = new MaintenanceAsset
        {
            TenantId = fixture.TenantId,
            AssetCategoryId = category.Id,
            AssetNumber = "MA-OLD-001",
            Name = "Existing Pump",
            SourceType = JobCardAssetSource.LegacyMaintenanceAsset
        };
        fixture.Db.MaintenanceAssets.Add(legacy);
        await fixture.Db.SaveChangesAsync();

        var result = await fixture.Service.GetSelectionOptionsAsync();

        result.Should().ContainSingle(item => item.AssetSource == JobCardAssetSource.LegacyMaintenanceAsset
            && item.SourceAssetId == legacy.Id
            && item.MaintenanceAssetId == legacy.Id);
    }

    [Fact]
    public async Task FinanceMapping_IsIdempotent_AndLinksLegacyFinanceNavigation()
    {
        await using var fixture = CreateFixture();
        var category = AddFixedAssetCategory(fixture, requiresMaintenance: true);
        var source = AddFixedAsset(fixture, category, "FA-UNIQUE", "Unique Asset", "Workshop");
        await fixture.Db.SaveChangesAsync();

        var first = await fixture.Service.ResolveOrCreateProfileAsync(
            JobCardAssetSource.FixedAsset, source.Id, fixture.TenantId);
        var second = await fixture.Service.ResolveOrCreateProfileAsync(
            JobCardAssetSource.FixedAsset, source.Id, fixture.TenantId);

        second.Id.Should().Be(first.Id);
        first.SourceType.Should().Be(JobCardAssetSource.FixedAsset);
        first.FixedAssetId.Should().Be(source.Id);
        (await fixture.Db.MaintenanceAssets.CountAsync(item => item.FixedAssetId == source.Id)).Should().Be(1);
        source.MaintenanceAssetId.Should().Be(first.Id);
    }

    [Fact]
    public async Task EstateMapping_PreservesTypedSourceReference()
    {
        await using var fixture = CreateFixture();
        var source = AddEstateAsset(fixture, EstateManagedAssetStatus.Sold, isExternallyPublished: true);
        await fixture.Db.SaveChangesAsync();

        var profile = await fixture.Service.ResolveOrCreateProfileAsync(
            JobCardAssetSource.EstateManagedAsset, source.Id, fixture.TenantId);

        profile.SourceType.Should().Be(JobCardAssetSource.EstateManagedAsset);
        profile.EstateManagedAssetId.Should().Be(source.Id);
        profile.FixedAssetId.Should().BeNull();
    }

    [Fact]
    public async Task IneligibleFinanceAsset_CannotCreateMaintenanceProfile()
    {
        await using var fixture = CreateFixture();
        var category = AddFixedAssetCategory(fixture, requiresMaintenance: false);
        var source = AddFixedAsset(fixture, category, "FA-NO-PROFILE", "Non-maintained", null);
        await fixture.Db.SaveChangesAsync();

        var action = () => fixture.Service.ResolveOrCreateProfileAsync(
            JobCardAssetSource.FixedAsset, source.Id, fixture.TenantId);

        await action.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*not eligible for Maintenance*");
    }

    [Fact]
    public async Task ExistingLegacyJobCard_ContinuesLoadingFromMaintenanceAsset()
    {
        await using var fixture = CreateFixture();
        var category = AddMaintenanceCategory(fixture, "LEGACY-JC");
        var asset = new MaintenanceAsset
        {
            TenantId = fixture.TenantId,
            AssetCategoryId = category.Id,
            AssetNumber = "MA-JC-001",
            Name = "Legacy Chiller"
        };
        var jobCard = new JobCard
        {
            TenantId = fixture.TenantId,
            JobCardNumber = "JC-LEGACY-001",
            AssetId = asset.Id,
            AssetSource = JobCardAssetSource.LegacyMaintenanceAsset,
            MaintenanceTypeId = Guid.NewGuid(),
            PriorityLevelId = Guid.NewGuid(),
            RequestedById = Guid.NewGuid(),
            Title = "Inspect legacy chiller"
        };
        fixture.Db.AddRange(asset, jobCard);
        await fixture.Db.SaveChangesAsync();

        var loaded = await new JobCardRepository(fixture.Db).GetByIdWithDetailsAsync(jobCard.Id);

        loaded.Should().NotBeNull();
        loaded!.AssetSource.Should().Be(JobCardAssetSource.LegacyMaintenanceAsset);
        loaded.SourceAssetId.Should().Be(asset.Id);
        loaded.AssetCode.Should().Be("MA-JC-001");
        loaded.AssetName.Should().Be("Legacy Chiller");
    }

    [Fact]
    public async Task NewFinanceJobCard_PersistsTypedSourceAndLoadsCanonicalAsset()
    {
        await using var fixture = CreateFixture();
        var category = AddFixedAssetCategory(fixture, requiresMaintenance: true);
        var source = AddFixedAsset(fixture, category, "FA-JC-001", "Finance Generator", "Main Plant");
        await fixture.Db.SaveChangesAsync();
        var profile = await fixture.Service.ResolveOrCreateProfileAsync(
            JobCardAssetSource.FixedAsset, source.Id, fixture.TenantId);
        var jobCard = CreateJobCard(fixture, profile.Id, JobCardAssetSource.FixedAsset);
        jobCard.FixedAssetId = source.Id;
        fixture.Db.JobCards.Add(jobCard);
        await fixture.Db.SaveChangesAsync();
        fixture.Db.ChangeTracker.Clear();

        var loaded = await new JobCardRepository(fixture.Db).GetByIdWithDetailsAsync(jobCard.Id);

        loaded.Should().NotBeNull();
        loaded!.AssetSource.Should().Be(JobCardAssetSource.FixedAsset);
        loaded.SourceAssetId.Should().Be(source.Id);
        loaded.AssetCode.Should().Be("FA-JC-001");
        loaded.AssetName.Should().Be("Finance Generator");
    }

    [Fact]
    public async Task NewEstateJobCard_PersistsTypedSourceAndLoadsCanonicalAsset()
    {
        await using var fixture = CreateFixture();
        var source = AddEstateAsset(fixture, EstateManagedAssetStatus.Occupied, isExternallyPublished: true);
        source.AssetCode = "EST-JC-001";
        source.Name = "Occupied Villa 1";
        await fixture.Db.SaveChangesAsync();
        var profile = await fixture.Service.ResolveOrCreateProfileAsync(
            JobCardAssetSource.EstateManagedAsset, source.Id, fixture.TenantId);
        var jobCard = CreateJobCard(fixture, profile.Id, JobCardAssetSource.EstateManagedAsset);
        jobCard.EstateManagedAssetId = source.Id;
        fixture.Db.JobCards.Add(jobCard);
        await fixture.Db.SaveChangesAsync();
        fixture.Db.ChangeTracker.Clear();

        var loaded = await new JobCardRepository(fixture.Db).GetByIdWithDetailsAsync(jobCard.Id);

        loaded.Should().NotBeNull();
        loaded!.AssetSource.Should().Be(JobCardAssetSource.EstateManagedAsset);
        loaded.SourceAssetId.Should().Be(source.Id);
        loaded.AssetCode.Should().Be("EST-JC-001");
        loaded.AssetName.Should().Be("Occupied Villa 1");
    }

    [Fact]
    public void Model_EnforcesUniqueFinanceAndEstateMappings()
    {
        using var fixture = CreateFixture();
        var entity = fixture.Db.Model.FindEntityType(typeof(MaintenanceAsset));

        entity.Should().NotBeNull();
        entity!.GetIndexes().Should().Contain(index => index.IsUnique
            && index.Properties.Select(property => property.Name)
                .SequenceEqual(new[] { "TenantId", "FixedAssetId" }));
        entity.GetIndexes().Should().Contain(index => index.IsUnique
            && index.Properties.Select(property => property.Name)
                .SequenceEqual(new[] { "TenantId", "EstateManagedAssetId" }));
    }

    private static Fixture CreateFixture()
    {
        var tenantId = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"maintenance-source-{Guid.NewGuid():N}")
            .Options;
        var db = new ApplicationDbContext(options, tenantId);
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(item => item.TenantId).Returns(tenantId);
        return new Fixture(tenantId, db, new MaintenanceAssetMappingService(db, currentUser.Object));
    }

    private static FixedAssetCategory AddFixedAssetCategory(Fixture fixture, bool requiresMaintenance)
    {
        var category = new FixedAssetCategory
        {
            TenantId = fixture.TenantId,
            Code = requiresMaintenance ? "FA-MAINT" : "FA-GENERAL",
            Name = requiresMaintenance ? "Maintainable Plant" : "General Assets",
            RequiresMaintenance = requiresMaintenance,
            AssetAccountId = Guid.NewGuid(),
            AccumulatedDepreciationAccountId = Guid.NewGuid(),
            DepreciationExpenseAccountId = Guid.NewGuid()
        };
        fixture.Db.FixedAssetCategories.Add(category);
        return category;
    }

    private static FixedAsset AddFixedAsset(
        Fixture fixture,
        FixedAssetCategory category,
        string code,
        string name,
        string? location)
    {
        var asset = new FixedAsset
        {
            TenantId = fixture.TenantId,
            FixedAssetCategoryId = category.Id,
            Category = category,
            AssetCode = code,
            Name = name,
            Location = location,
            PurchaseDate = new DateTime(2026, 1, 1),
            PurchasePrice = 1000m,
            AcquisitionCost = 1000m,
            NetBookValue = 800m,
            Status = FixedAssetStatus.Active
        };
        fixture.Db.FixedAssets.Add(asset);
        return asset;
    }

    private static EstateManagedAsset AddEstateAsset(
        Fixture fixture,
        EstateManagedAssetStatus status,
        bool isExternallyPublished)
    {
        var asset = new EstateManagedAsset
        {
            TenantId = fixture.TenantId,
            AssetCode = $"EST-{status}-{Guid.NewGuid():N}"[..24],
            Name = $"{status} Property",
            UnitType = "Residential Unit",
            Location = "North Estate",
            AssetType = EstateManagedAssetType.Property,
            Status = status,
            IsPublishedToExternalPortal = isExternallyPublished,
            ExternalListingStatus = isExternallyPublished ? "Published" : "Draft"
        };
        fixture.Db.EstateManagedAssets.Add(asset);
        return asset;
    }

    private static MaintenanceAssetCategory AddMaintenanceCategory(Fixture fixture, string code)
    {
        var category = new MaintenanceAssetCategory
        {
            TenantId = fixture.TenantId,
            Code = code,
            Name = code,
            AutoGenerateSchedules = false
        };
        fixture.Db.MaintenanceAssetCategories.Add(category);
        return category;
    }

    private static JobCard CreateJobCard(
        Fixture fixture,
        Guid maintenanceAssetId,
        JobCardAssetSource source)
    {
        return new JobCard
        {
            TenantId = fixture.TenantId,
            JobCardNumber = $"JC-{Guid.NewGuid():N}"[..20],
            AssetId = maintenanceAssetId,
            AssetSource = source,
            MaintenanceTypeId = Guid.NewGuid(),
            PriorityLevelId = Guid.NewGuid(),
            RequestedById = Guid.NewGuid(),
            Title = "Source-aware maintenance request"
        };
    }

    private sealed record Fixture(
        Guid TenantId,
        ApplicationDbContext Db,
        MaintenanceAssetMappingService Service) : IDisposable, IAsyncDisposable
    {
        public void Dispose() => Db.Dispose();
        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }
}
