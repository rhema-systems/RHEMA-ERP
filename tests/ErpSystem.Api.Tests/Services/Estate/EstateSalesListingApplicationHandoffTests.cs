using System.Text.Json;
using ErpSystem.Api.Services.Estate;
using ErpSystem.Core.DTOs.Notifications;
using ErpSystem.Core.DTOs.Procedures;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Estate;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Entities.Procedures;
using ErpSystem.Core.Entities.Sales;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Procedures;
using ErpSystem.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Estate;

public sealed class EstateSalesListingApplicationHandoffTests
{
    private readonly Guid tenantId = Guid.NewGuid();

    [Theory]
    [InlineData(EstateManagedAssetType.Property)]
    [InlineData(EstateManagedAssetType.Facility)]
    public async Task SuccessfulHandoffUnpublishesExactManagedAssetWithoutChangingEstateStatus(
        EstateManagedAssetType assetType)
    {
        await using var fixture = await RelationalFixture.CreateAsync();
        var db = fixture.Db;
        var publishedAt = DateTime.UtcNow.AddDays(-2);
        var asset = ManagedAsset(assetType, $"{assetType.ToString().ToUpperInvariant()}-001", publishedAt);
        var untouched = ManagedAsset(assetType, $"{assetType.ToString().ToUpperInvariant()}-002", publishedAt);
        var partner = Customer();
        var opportunity = ClosedWonOpportunity();
        db.AddRange(asset, untouched, partner, opportunity);
        await db.SaveChangesAsync();

        var service = new EstateSalesListingApplicationHandoffService(
            db,
            SuccessfulProcedureService().Object,
            Mock.Of<INotificationService>());
        await service.CreateAsync(tenantId, Request(asset.Id, partner.Id, opportunity.Id));

        var persisted = await db.EstateManagedAssets.AsNoTracking().SingleAsync(item => item.Id == asset.Id);
        var persistedUntouched = await db.EstateManagedAssets.AsNoTracking().SingleAsync(item => item.Id == untouched.Id);
        Assert.False(persisted.IsPublishedToExternalPortal);
        Assert.Equal(EstateManagedAssetStatus.Available, persisted.Status);
        Assert.Equal("Published", persisted.ExternalListingStatus);
        Assert.Equal(publishedAt, persisted.ExternalPublishedAt);
        Assert.True(persistedUntouched.IsPublishedToExternalPortal);
    }

    [Fact]
    public async Task SuccessfulLandHandoffUnpublishesExactDemarcationOnly()
    {
        await using var fixture = await RelationalFixture.CreateAsync(includeDemarcations: true);
        var db = fixture.Db;
        var publishedAt = DateTime.UtcNow.AddDays(-2);
        var land = ManagedAsset(EstateManagedAssetType.Land, "LAND-001", publishedAt);
        land.Status = EstateManagedAssetStatus.LandBank;
        var selected = Demarcation(land, 1, "LAND-001-D001", publishedAt);
        var sibling = Demarcation(land, 2, "LAND-001-D002", publishedAt);
        var partner = Customer();
        var opportunity = ClosedWonOpportunity();
        db.AddRange(land, selected, sibling, partner, opportunity);
        await db.SaveChangesAsync();

        var service = new EstateSalesListingApplicationHandoffService(
            db,
            SuccessfulProcedureService().Object,
            Mock.Of<INotificationService>());
        await service.CreateAsync(tenantId, Request(selected.Id, partner.Id, opportunity.Id));

        var persistedLand = await db.EstateManagedAssets.AsNoTracking().SingleAsync(item => item.Id == land.Id);
        var persistedSelected = await db.EstateLandDemarcations.AsNoTracking().SingleAsync(item => item.Id == selected.Id);
        var persistedSibling = await db.EstateLandDemarcations.AsNoTracking().SingleAsync(item => item.Id == sibling.Id);
        Assert.True(persistedLand.IsPublishedToExternalPortal);
        Assert.False(persistedSelected.IsPublishedToExternalPortal);
        Assert.Equal("Published", persistedSelected.ExternalListingStatus);
        Assert.Equal(publishedAt, persistedSelected.ExternalPublishedAt);
        Assert.True(persistedSibling.IsPublishedToExternalPortal);
    }

    [Fact]
    public async Task FailedEstateCaseCreationLeavesListingPublished()
    {
        await using var fixture = await RelationalFixture.CreateAsync();
        var db = fixture.Db;
        var asset = ManagedAsset(EstateManagedAssetType.Property, "PROPERTY-FAIL", DateTime.UtcNow.AddDays(-1));
        var partner = Customer();
        var opportunity = ClosedWonOpportunity();
        db.AddRange(asset, partner, opportunity);
        await db.SaveChangesAsync();
        var procedures = new Mock<IProcedureCaseService>();
        procedures.Setup(item => item.CreateCaseAsync(It.IsAny<CreateProcedureCaseRequest>()))
            .ThrowsAsync(new InvalidOperationException("Estate workflow failed."));

        var service = new EstateSalesListingApplicationHandoffService(
            db,
            procedures.Object,
            Mock.Of<INotificationService>());
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateAsync(tenantId, Request(asset.Id, partner.Id, opportunity.Id)));

        var persisted = await db.EstateManagedAssets.AsNoTracking().SingleAsync(item => item.Id == asset.Id);
        Assert.True(persisted.IsPublishedToExternalPortal);
        Assert.Equal("Published", persisted.ExternalListingStatus);
    }

    [Fact]
    public async Task SuccessfulHandoffNotifiesLinkedPortalCustomerWithRequestLink()
    {
        await using var fixture = await RelationalFixture.CreateAsync();
        var db = fixture.Db;
        var asset = ManagedAsset(EstateManagedAssetType.Property, "PROPERTY-PORTAL", DateTime.UtcNow.AddDays(-1));
        var partner = Customer();
        var portalUserId = Guid.NewGuid();
        var linkedPortalUserId = Guid.NewGuid();
        partner.UserId = portalUserId;
        var opportunity = ClosedWonOpportunity();
        var portalLink = new BusinessPartnerUser
        {
            TenantId = tenantId,
            BusinessPartnerId = partner.Id,
            UserId = linkedPortalUserId,
            IsActive = true
        };
        db.AddRange(asset, partner, opportunity, portalLink);
        await db.SaveChangesAsync();

        var notifications = new Mock<INotificationService>();
        notifications.Setup(item => item.CreateNotificationAsync(
                It.IsAny<CreateNotificationDto>(),
                It.IsAny<Guid>(),
                tenantId))
            .ReturnsAsync(new NotificationDto());
        var service = new EstateSalesListingApplicationHandoffService(
            db,
            SuccessfulProcedureService().Object,
            notifications.Object);

        var result = await service.CreateAsync(
            tenantId,
            Request(asset.Id, partner.Id, opportunity.Id) with { ActorUserId = Guid.NewGuid() });

        notifications.Verify(item => item.CreateNotificationAsync(
            It.Is<CreateNotificationDto>(notification =>
                notification.RecipientId == portalUserId
                && notification.Type == "estate.property.sales-handoff"
                && notification.EntityId == result.ProcedureCaseId
                && notification.ActionUrl == $"/external-portal/my-property-requests/{result.ProcedureCaseId}"),
            It.IsAny<Guid>(),
            tenantId), Times.Once);
        notifications.Verify(item => item.CreateNotificationAsync(
            It.Is<CreateNotificationDto>(notification =>
                notification.RecipientId == linkedPortalUserId
                && notification.EntityId == result.ProcedureCaseId),
            It.IsAny<Guid>(),
            tenantId), Times.Once);
    }

    private Mock<IProcedureCaseService> SuccessfulProcedureService()
    {
        var service = new Mock<IProcedureCaseService>();
        service.Setup(item => item.CreateCaseAsync(It.IsAny<CreateProcedureCaseRequest>()))
            .ReturnsAsync(new ProcedureCaseDetailDto(
                Guid.NewGuid(),
                "PropertyManagement",
                "EstatePropertyManagementListingApplication",
                "Estate listing application",
                "ESTATE-001",
                "Customer",
                "Sales - Estate Enquiry",
                DateTime.UtcNow,
                null,
                "Open",
                0,
                "Estate review",
                "Estate Manager",
                "Estate Manager",
                true,
                null,
                true,
                Array.Empty<string>(),
                Array.Empty<ProcedureCaseFieldDto>(),
                Array.Empty<ProcedureCaseChecklistItemDto>(),
                Array.Empty<ProcedureCaseDocumentDto>(),
                Array.Empty<ProcedureCaseActivityDto>()));
        return service;
    }

    private EstateManagedAsset ManagedAsset(
        EstateManagedAssetType assetType,
        string code,
        DateTime publishedAt) => new()
    {
        TenantId = tenantId,
        AssetCode = code,
        Name = code,
        AssetType = assetType,
        Status = EstateManagedAssetStatus.Available,
        Location = "Accra",
        IsAvailableForSale = true,
        IsPublishedToExternalPortal = true,
        ExternalListingType = "Sale",
        ExternalListingStatus = "Published",
        ExternalListingCurrency = "GHS",
        ExternalPublishedAt = publishedAt
    };

    private EstateLandDemarcation Demarcation(
        EstateManagedAsset land,
        int number,
        string reference,
        DateTime publishedAt) => new()
    {
        TenantId = tenantId,
        EstateManagedAsset = land,
        EstateManagedAssetId = land.Id,
        DemarcationNumber = number,
        Description = reference,
        BoundaryCoordinates = "[]",
        ChildFixedAssetReference = reference,
        IsPublishedToExternalPortal = true,
        ExternalListingType = "Sale",
        ExternalListingStatus = "Published",
        ExternalListingCurrency = "GHS",
        ExternalPublishedAt = publishedAt
    };

    private BusinessPartner Customer() => new()
    {
        TenantId = tenantId,
        PartnerCode = $"CUS-{Guid.NewGuid():N}",
        PartnerName = "Estate Customer",
        PartnerType = "Customer",
        CustomerAccountNumber = "CUS-001",
        Currency = "GHS",
        IsActive = true,
        ApprovalStatus = "Approved"
    };

    private Opportunity ClosedWonOpportunity() => new()
    {
        TenantId = tenantId,
        Name = "Property enquiry",
        Stage = "Closed Won",
        Amount = 1_250_000m,
        Currency = "GHS",
        ActualCloseDate = DateTime.UtcNow
    };

    private static EstateSalesListingApplicationHandoffRequest Request(
        Guid listingId,
        Guid partnerId,
        Guid opportunityId) => new(
            listingId,
            partnerId,
            "Sale",
            opportunityId,
            "SO-001",
            1_250_000m,
            RequestedLeaseTerm: null,
            SalesAmountPaid: 250_000m,
            SalesPaymentReference: "RCT-001",
            Currency: "GHS",
            SalesCompletedAt: DateTime.UtcNow,
            Notes: null);

    private sealed class RelationalFixture : IAsyncDisposable
    {
        private RelationalFixture(SqliteConnection connection, ApplicationDbContext db)
        {
            Connection = connection;
            Db = db;
        }

        private SqliteConnection Connection { get; }
        public ApplicationDbContext Db { get; }

        public static async Task<RelationalFixture> CreateAsync(bool includeDemarcations = false)
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            connection.CreateFunction<string?, int>("ISJSON", value =>
            {
                if (string.IsNullOrWhiteSpace(value)) return 0;
                try
                {
                    using var json = JsonDocument.Parse(value);
                    return json.RootElement.ValueKind is JsonValueKind.Object or JsonValueKind.Array ? 1 : 0;
                }
                catch (JsonException)
                {
                    return 0;
                }
            });
            var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlite(connection)
                .Options);
            await db.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys = OFF;");
            var types = new List<Type>
            {
                typeof(EstateManagedAsset),
                typeof(BusinessPartner),
                typeof(Opportunity),
                typeof(ProcedureCase),
                typeof(ProcedureCaseField),
                typeof(BusinessPartnerUser)
            };
            if (includeDemarcations) types.Add(typeof(EstateLandDemarcation));
            var tables = types.Select(type => db.Model.FindEntityType(type)!.GetTableName()!).ToArray();
            foreach (var statement in db.Database.GenerateCreateScript()
                         .Split(';', StringSplitOptions.RemoveEmptyEntries)
                         .Where(statement => tables.Any(table =>
                             statement.Contains($"CREATE TABLE \"{table}\"", StringComparison.Ordinal))))
            {
                await db.Database.ExecuteSqlRawAsync(statement
                    .Replace("nvarchar(max)", "TEXT", StringComparison.OrdinalIgnoreCase)
                    .Replace("varchar(max)", "TEXT", StringComparison.OrdinalIgnoreCase)
                    .Replace("varbinary(max)", "BLOB", StringComparison.OrdinalIgnoreCase)
                    .Replace("GETUTCDATE()", "CURRENT_TIMESTAMP", StringComparison.OrdinalIgnoreCase)
                    .Replace("NEWID()", "lower(hex(randomblob(16)))", StringComparison.OrdinalIgnoreCase)
                    .Replace("\"RowVersion\" BLOB NOT NULL", "\"RowVersion\" BLOB NOT NULL DEFAULT X''", StringComparison.Ordinal));
            }

            return new RelationalFixture(connection, db);
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await Connection.DisposeAsync();
        }
    }
}
