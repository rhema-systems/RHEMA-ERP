using System.Linq.Expressions;
using ErpSystem.Core.DTOs.Estate;
using ErpSystem.Core.Entities.Sales;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Estate;
using ErpSystem.Core.Interfaces.Sales;
using ErpSystem.Core.Services.Sales;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Sales;

public class SalesPropertyRegisterSourceTests
{
    [Theory]
    [InlineData("Property", EstateManagedAssetType.Property)]
    [InlineData("Facility", EstateManagedAssetType.Facility)]
    public async Task Property_register_adapter_selects_the_configured_published_asset_type(
        string configuredAssetType,
        EstateManagedAssetType expectedAssetType)
    {
        var projectUnitId = Guid.NewGuid();
        var expected = Asset("MATCH", expectedAssetType, true, "Draft");
        expected.ProjectUnitId = projectUnitId;
        var wrongType = Asset(
            "WRONG-TYPE",
            expectedAssetType == EstateManagedAssetType.Property
                ? EstateManagedAssetType.Facility
                : EstateManagedAssetType.Property,
            true,
            "Published");
        var unpublished = Asset("NOT-PUBLISHED", expectedAssetType, false, "Published");

        EstateManagedAssetQuery? capturedQuery = null;
        var estate = new Mock<IEstateManagedAssetService>();
        estate.Setup(service => service.GetManagedAssetsAsync(It.IsAny<EstateManagedAssetQuery>()))
            .Callback<EstateManagedAssetQuery>(query => capturedQuery = query)
            .ReturnsAsync([expected, wrongType, unpublished]);

        var source = new SalesSaleableSource
        {
            Id = Guid.NewGuid(),
            Code = $"{configuredAssetType.ToUpperInvariant()}_REGISTER",
            SourceType = $"{configuredAssetType}Register",
            AdapterKey = "property-register",
            IsActive = true,
            AllowSalesOrders = true,
            AllowSalesAgreements = true,
            SettingsJson = $$"""
                {"source":"property-register","filters":[{"field":"isPublishedToExternalPortal","value":"true"},{"field":"assetType","value":"{{configuredAssetType}}"}]}
                """
        };

        var result = await new PropertyRegisterSaleableSourceAdapter(estate.Object)
            .SearchItemsAsync(source, "match", 50);

        capturedQuery.Should().NotBeNull();
        capturedQuery!.AssetType.Should().Be(expectedAssetType);
        capturedQuery.PublishedToExternalPortal.Should().BeTrue();
        capturedQuery.AvailableForSale.Should().BeNull();
        capturedQuery.AvailableForSaleOrLease.Should().BeNull();
        capturedQuery.ExcludedStatuses.Should().BeEmpty();
        result.Should().ContainSingle();
        result.Single().ItemCode.Should().Be("MATCH");
        result.Single().SourceItemId.Should().Be(projectUnitId.ToString());
    }

    [Fact]
    public async Task Default_sources_activate_property_and_facility_and_retire_legacy_project_units()
    {
        var tenantId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var rows = new List<SalesSaleableSource>
        {
            new()
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Code = "PROJECT_UNITS",
                DisplayName = "Project Units",
                SourceType = "ProjectUnits",
                AdapterKey = "project-units",
                IsActive = true,
                IsSystemSource = true
            },
            new()
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Code = "PROPERTY_REGISTER",
                DisplayName = "Property Register",
                SourceType = "PropertyRegister",
                AdapterKey = "property-register",
                IsActive = false,
                IsSystemSource = true,
                RequiresExternalModule = true,
                SettingsJson = "{\"source\":\"property-register\",\"status\":\"pending-module\"}"
            }
        };

        var repository = new Mock<IGenericRepository<SalesSaleableSource>>();
        repository.Setup(item => item.FindAsync(It.IsAny<Expression<Func<SalesSaleableSource, bool>>>()))
            .ReturnsAsync((Expression<Func<SalesSaleableSource, bool>> predicate) =>
                rows.Where(predicate.Compile()).ToList());
        repository.Setup(item => item.AddAsync(It.IsAny<SalesSaleableSource>()))
            .ReturnsAsync((SalesSaleableSource source) =>
            {
                source.Id = source.Id == Guid.Empty ? Guid.NewGuid() : source.Id;
                rows.Add(source);
                return source;
            });

        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.Setup(item => item.Repository<SalesSaleableSource>()).Returns(repository.Object);
        unitOfWork.Setup(item => item.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var currentUser = new Mock<ICurrentUserProvider>();
        currentUser.SetupGet(item => item.TenantId).Returns(tenantId);
        currentUser.SetupGet(item => item.UserId).Returns(actorId);
        currentUser.SetupGet(item => item.Username).Returns("sales.admin");

        var service = new SalesSetupService(
            unitOfWork.Object,
            currentUser.Object,
            Array.Empty<ISalesSaleableSourceAdapter>(),
            NullLogger<SalesSetupService>.Instance);

        var sources = await service.GetSaleableSourcesAsync(includeInactive: true);

        sources.Single(item => item.Code == "PROJECT_UNITS").IsActive.Should().BeFalse();

        var property = sources.Single(item => item.Code == "PROPERTY_REGISTER");
        property.IsActive.Should().BeTrue();
        property.RequiresExternalModule.Should().BeFalse();
        property.SettingsJson.Should().Contain("\"assetType\",\"value\":\"Property\"");
        property.SettingsJson.Should().Contain("\"isPublishedToExternalPortal\",\"value\":\"true\"");

        var facility = sources.Single(item => item.Code == "FACILITY_REGISTER");
        facility.IsActive.Should().BeTrue();
        facility.AdapterKey.Should().Be("property-register");
        facility.SettingsJson.Should().Contain("\"assetType\",\"value\":\"Facility\"");
        facility.SettingsJson.Should().Contain("\"isPublishedToExternalPortal\",\"value\":\"true\"");
    }

    private static EstateManagedAssetDto Asset(
        string code,
        EstateManagedAssetType assetType,
        bool isPublishedToExternalPortal,
        string externalListingStatus)
        => new()
        {
            Id = Guid.NewGuid(),
            AssetCode = code,
            Name = code,
            AssetType = assetType,
            Status = EstateManagedAssetStatus.Available,
            IsAvailableForSale = true,
            IsPublishedToExternalPortal = isPublishedToExternalPortal,
            ExternalListingStatus = externalListingStatus,
            Currency = "GHS"
        };
}
