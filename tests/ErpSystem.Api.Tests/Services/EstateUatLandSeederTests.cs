using ErpSystem.Api.Services;
using ErpSystem.Core.DTOs.Estate;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Estate;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Estate;
using ErpSystem.Core.Services.Estate;
using ErpSystem.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Services;

public sealed class EstateUatLandSeederTests
{
    [Theory]
    [InlineData("Test")]
    [InlineData("Testing")]
    [InlineData("Development")]
    public void Write_target_requires_explicit_nonproduction_opt_in_and_exact_database(string environment)
        => EstateUatLandSeeder.ValidateWriteTarget(environment, "true", "UAT_EXACT", "UAT_EXACT");

    [Theory]
    [InlineData("Production", "true", "UAT_EXACT", "UAT_EXACT")]
    [InlineData("Staging", "true", "UAT_EXACT", "UAT_EXACT")]
    [InlineData("Test", null, "UAT_EXACT", "UAT_EXACT")]
    [InlineData("Test", "false", "UAT_EXACT", "UAT_EXACT")]
    [InlineData("Test", "true", null, "UAT_EXACT")]
    [InlineData("Test", "true", "OTHER", "UAT_EXACT")]
    [InlineData("Test", "true", "uat_exact", "UAT_EXACT")]
    public void Unsafe_write_target_is_rejected(string environment, string? enabled, string? expected, string actual)
    {
        var action = () => EstateUatLandSeeder.ValidateWriteTarget(environment, enabled, expected, actual);
        action.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public async Task Fictional_request_passes_real_Estate_creation_demarcation_cost_and_project_selector_contracts()
    {
        await using var db = Context();
        var tenant = await SeedTenantAsync(db);
        var user = User(tenant.Id);
        using var unit = new UnitOfWork(db);
        var estate = new EstateManagedAssetService(unit, user);
        var request = EstateUatLandSeeder.CreateRequest();
        request.Description.Should().Contain("FICTIONAL");
        request.Notes.Should().Contain(EstateUatLandSeeder.SeedMarker);
        var asset = await estate.CreateManualExistingLandAsync(request);
        asset.Name.Should().Be(EstateUatLandSeeder.SeedName);
        asset.AssetCode.Should().StartWith("LAND-");
        asset.IsReadyForProjectManagement.Should().BeFalse();
        var demarcation = await estate.CreateLandDemarcationAsync(asset.Id, new SaveEstateLandDemarcationDto
        {
            Description = "Synthetic test parcel", BoundaryCoordinates = request.BoundaryCoordinates,
            BeaconCount = request.BeaconCount, BoundaryVerified = true
        });
        demarcation.AreaSquareFeet.Should().Be(10000m);
        demarcation.AllocatedCost.Should().Be(100000m);
        await estate.MarkReadyForProjectManagementAsync(asset.Id);
        var ready = await estate.GetProjectReadyLandDemarcationsAsync();
        ready.Should().ContainSingle(value => value.AssetId == asset.Id && value.DemarcationId == demarcation.Id);
        ready.Single().LandReference.Should().Be(demarcation.ChildFixedAssetReference);
        (await db.EstateManagedAssets.SingleAsync()).IsPublishedToExternalPortal.Should().BeFalse();
        (await db.EstateLandDemarcations.SingleAsync()).IsPublishedToExternalPortal.Should().BeFalse();
    }

    [Fact]
    public async Task Verify_is_read_only_and_preserves_existing_assigned_land()
    {
        await using var db = Context();
        var tenant = await SeedTenantAsync(db);
        var asset = Asset(tenant.Id);
        asset.Description = "User-maintained description";
        db.EstateManagedAssets.Add(asset);
        await db.SaveChangesAsync();
        var estate = new Mock<IEstateManagedAssetService>(MockBehavior.Strict);
        var demarcationId = Guid.NewGuid();
        estate.Setup(value => value.GetLandDemarcationsAsync(asset.Id)).ReturnsAsync([
            new EstateLandDemarcationDto { Id = demarcationId, EstateManagedAssetId = asset.Id,
                LandReference = "LAND-SAVED-01", IsAssignedToProject = true }]);
        estate.Setup(value => value.GetProjectReadyLandDemarcationsAsync(null))
            .ReturnsAsync(Array.Empty<ProjectReadyLandDemarcationDto>());
        var service = Seeder(db, User(tenant.Id), estate.Object);
        var first = await service.VerifyAsync();
        var second = await service.VerifyAsync();
        second.Should().Be(first);
        first!.Created.Should().BeFalse();
        first.AssignedToProject.Should().BeTrue();
        first.ReadyForProjectSelection.Should().BeFalse();
        first.DemarcationId.Should().Be(demarcationId);
        db.ChangeTracker.HasChanges().Should().BeFalse();
        (await db.EstateManagedAssets.SingleAsync()).Description.Should().Be("User-maintained description");
        estate.Verify(value => value.CreateManualExistingLandAsync(It.IsAny<CreateManualExistingLandDto>()), Times.Never);
        estate.Verify(value => value.MarkReadyForProjectManagementAsync(It.IsAny<Guid>()), Times.Never);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Existing_unmarked_or_deleted_identifier_is_never_reused(bool deleted)
    {
        await using var db = Context();
        var tenant = await SeedTenantAsync(db);
        var asset = Asset(tenant.Id);
        asset.IsDeleted = deleted;
        if (!deleted) asset.Notes = "An existing user-owned parcel";
        db.EstateManagedAssets.Add(asset);
        await db.SaveChangesAsync();
        var service = Seeder(db, User(tenant.Id), Mock.Of<IEstateManagedAssetService>());
        await FluentActions.Awaiting(() => service.VerifyAsync()).Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*no data was changed*");
        (await db.EstateManagedAssets.IgnoreQueryFilters().CountAsync()).Should().Be(1);
        db.ChangeTracker.HasChanges().Should().BeFalse();
    }

    [Fact]
    public async Task Verify_cannot_select_another_tenants_DEFAULT_record()
    {
        await using var db = Context();
        await SeedTenantAsync(db);
        var other = new Tenant { Code = "OTHER", Name = "Other" };
        db.Tenants.Add(other);
        await db.SaveChangesAsync();
        var service = Seeder(db, User(other.Id), Mock.Of<IEstateManagedAssetService>());
        await FluentActions.Awaiting(() => service.VerifyAsync()).Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*DEFAULT tenant*");
    }

    private static EstateUatLandSeeder Seeder(ApplicationDbContext db, ICurrentUserProvider user, IEstateManagedAssetService estate)
        => new(db, estate, Mock.Of<IUnitOfWork>(), user, new ConfigurationBuilder().Build(), Mock.Of<IHostEnvironment>());

    private static EstateManagedAsset Asset(Guid tenant) => new()
    {
        TenantId = tenant, AssetCode = "LAND-SAVED", Name = EstateUatLandSeeder.SeedName,
        SurveyPlanNumber = EstateUatLandSeeder.SeedName, Notes = EstateUatLandSeeder.SeedMarker,
        AssetType = EstateManagedAssetType.Land, Status = EstateManagedAssetStatus.LandBank
    };

    private static async Task<Tenant> SeedTenantAsync(ApplicationDbContext db)
    {
        var tenant = new Tenant { Code = "DEFAULT", Name = "UAT Default" };
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();
        return tenant;
    }

    private static ICurrentUserProvider User(Guid tenant)
        => Mock.Of<ICurrentUserProvider>(value => value.TenantId == tenant && value.UserId == Guid.Parse("7573a7d7-1bf7-4350-8d96-f5bef9623df5") &&
            value.Username == "qs.uat.bootstrap");

    private static ApplicationDbContext Context() => new(new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString())
        .ConfigureWarnings(value => value.Ignore(InMemoryEventId.TransactionIgnoredWarning)).Options);
}
