using ErpSystem.Api.Services.Estate;
using ErpSystem.Core.DTOs.Estate;
using ErpSystem.Core.Entities.Estate;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Estate;

public sealed class GroundRentAssessmentTests
{
    [Fact]
    public async Task PublishedApartmentCanSetAndReviseFixedAnnualGroundRent()
    {
        var tenantId = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var db = new ApplicationDbContext(options, tenantId);
        var apartment = new EstateManagedAsset
        {
            TenantId = tenantId,
            AssetCode = "APT-001",
            Name = "Apartment 1",
            AssetType = EstateManagedAssetType.Property,
            Status = EstateManagedAssetStatus.Available,
            IsPublishedToExternalPortal = true
        };
        db.EstateManagedAssets.Add(apartment);
        await db.SaveChangesAsync();
        var service = CreateService(db, tenantId);

        var first = await service.AssessAssetAsync(new AssessEstateGroundRentDto
        {
            EstateManagedAssetId = apartment.Id,
            AnnualAmount = 1200m,
            CurrencyCode = "GHS"
        }, CancellationToken.None);
        var revised = await service.AssessAssetAsync(new AssessEstateGroundRentDto
        {
            EstateManagedAssetId = apartment.Id,
            AnnualAmount = 1440m,
            CurrencyCode = "GHS"
        }, CancellationToken.None);

        first.ApprovedAnnualGroundRent.Should().Be(1200m);
        revised.ApprovedAnnualGroundRent.Should().Be(1440m);
        revised.AssetType.Should().Be("Property");
        apartment.ExternalGroundRentRequired.Should().BeTrue();
        apartment.GroundRentRatePerAcre.Should().BeNull();
    }

    [Fact]
    public async Task LandStillRequiresAcreageAndRatePerAcre()
    {
        var tenantId = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var db = new ApplicationDbContext(options, tenantId);
        var land = new EstateManagedAsset
        {
            TenantId = tenantId,
            AssetCode = "LAND-001",
            Name = "Land 1",
            AssetType = EstateManagedAssetType.Land,
            AreaValue = 2m,
            AreaUnit = "Acres"
        };
        db.EstateManagedAssets.Add(land);
        await db.SaveChangesAsync();
        var service = CreateService(db, tenantId);

        var action = () => service.AssessAssetAsync(new AssessEstateGroundRentDto
        {
            EstateManagedAssetId = land.Id,
            AnnualAmount = 1000m
        }, CancellationToken.None);
        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*rate per acre*");

        var assessed = await service.AssessAssetAsync(new AssessEstateGroundRentDto
        {
            EstateManagedAssetId = land.Id,
            RatePerAcre = 500m
        }, CancellationToken.None);
        assessed.ApprovedAnnualGroundRent.Should().Be(1000m);
    }

    private static GroundRentAdministrationService CreateService(ApplicationDbContext db, Guid tenantId)
    {
        var user = new Mock<ICurrentUserService>();
        user.SetupGet(item => item.TenantId).Returns(tenantId);
        return new GroundRentAdministrationService(db, Mock.Of<IInvoiceService>(),
            Mock.Of<IPaymentService>(), user.Object, Mock.Of<IDistributedLockService>());
    }
}
