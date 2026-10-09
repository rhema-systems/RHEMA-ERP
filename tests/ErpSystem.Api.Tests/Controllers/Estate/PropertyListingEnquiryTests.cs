using System.Text.Json;
using System.ComponentModel.DataAnnotations;
using ErpSystem.Api.Controllers.Estate;
using ErpSystem.Api.Controllers.Ehc;
using ErpSystem.Api.Services.Estate;
using ErpSystem.Api.Services;
using ErpSystem.Api.Services.DocumentManagement;
using ErpSystem.Core.DTOs.Ehc;
using ErpSystem.Core.DTOs.Sales;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Ehc;
using ErpSystem.Core.Entities.Estate;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Entities.Procedures;
using ErpSystem.Core.Entities.Sales;
using ErpSystem.Core.DTOs.Procedures;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Ehc;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Core.Interfaces.Maintenance;
using ErpSystem.Core.Interfaces.Procedures;
using ErpSystem.Core.Interfaces.Sales;
using ErpSystem.Core.Interfaces.Workflow;
using ErpSystem.Core.Services.Estate;
using ErpSystem.Core.Services.Sales;
using ErpSystem.Core.Services.Legal;
using ErpSystem.Core.Services.Planning;
using ErpSystem.Data;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Controllers.Estate;

public sealed class PropertyListingEnquiryTests
{
    private readonly Guid tenantId = Guid.NewGuid();
    private readonly Guid userId = Guid.NewGuid();
    private ApplicationDbContext Database() => new(new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString())
        .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
        .Options);
    private Mock<ICurrentUserService> User()
    {
        var user = new Mock<ICurrentUserService>();
        user.SetupGet(u => u.TenantId).Returns(tenantId); user.SetupGet(u => u.UserId).Returns(userId.ToString());
        user.SetupGet(u => u.FullName).Returns("Supplier Contact"); user.SetupGet(u => u.Email).Returns("contact@example.test");
        return user;
    }
    private EstateExternalDocumentsController Controller(
        ApplicationDbContext db,
        Mock<IEhcTicketService> tickets,
        ICurrentUserService? currentUser = null)
        => new(db, currentUser ?? User().Object, null!, null!, null!, null!, null!, tickets.Object, Mock.Of<ICaptchaVerificationService>(),
            Mock.Of<ErpSystem.Api.Services.Otp.IOtpService>(), Mock.Of<ErpSystem.Api.Services.Sms.ITenantSmsSender>(),
            Mock.Of<ITenantEmailSender>(),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<EstateExternalDocumentsController>.Instance)
        { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() } };
    private EstateManagedAsset Asset() => new()
    {
        TenantId = tenantId, AssetCode = "LAND-002", Name = "Parcel Two", AssetType = EstateManagedAssetType.Land,
        Status = EstateManagedAssetStatus.LandBank, Location = "Accra", IsAvailableForSale = true,
        BoundaryVerified = true, IsPublishedToExternalPortal = false, ExternalListingCurrency = "GHS",
    };
    private async Task<(EstateManagedAsset Asset, EstateLandDemarcation Portion, BusinessPartner Partner)> Seed(ApplicationDbContext db)
    {
        var asset = Asset();
        var portion = new EstateLandDemarcation { TenantId = tenantId, EstateManagedAsset = asset,
            EstateManagedAssetId = asset.Id, DemarcationNumber = 2, ChildFixedAssetReference = "LAND-002-D002",
            Description = "Serviced plot", BoundaryVerified = true,
            IsPublishedToExternalPortal = true, ExternalListingStatus = "Published", ExternalListingType = "Sale",
            ExternalListingCurrency = "GHS", ExternalSalePrice = 1250000m };
        var partner = new BusinessPartner { TenantId = tenantId, PartnerCode = "SUP-TEST", PartnerName = "Supplier Only Ltd",
            PartnerType = "Supplier", IsActive = true, ApprovalStatus = "Approved", UserId = userId };
        db.AddRange(asset, portion, partner, new EhcTicketCategory { TenantId = tenantId, Code = "PROPERTY-LISTING", Name = "Property enquiry", AppliesToType = EhcTicketType.Enquiry });
        await db.SaveChangesAsync(); return (asset, portion, partner);
    }

    [Theory]
    [InlineData(EstateManagedAssetType.Land, "land-management", true)]
    [InlineData(EstateManagedAssetType.Property, "property-register", false)]
    [InlineData(EstateManagedAssetType.Facility, "property-register", false)]
    public async Task PropertyEnquirySalesOrderSourceResolvesTheExactEstateListing(
        EstateManagedAssetType assetType,
        string adapterKey,
        bool usesDemarcation)
    {
        await using var db = Database();
        var structure = new OrganizationStructure
        {
            TenantId = tenantId,
            Name = "TDC structure",
            Code = "TDC",
            IsActive = true
        };
        var level = new OrganizationLevel
        {
            TenantId = tenantId,
            StructureId = structure.Id,
            OrganizationStructure = structure,
            Name = "Department",
            Code = "DEPT",
            LevelNumber = 3,
            IsActive = true
        };
        var salesUnit = new OrganizationUnit
        {
            TenantId = tenantId,
            OrganizationLevelId = level.Id,
            OrganizationLevel = level,
            Name = "Sales",
            Code = "DEPT-SALES",
            Path = "/TDC/SALES",
            IsActive = true
        };
        var asset = new EstateManagedAsset
        {
            TenantId = tenantId,
            AssetCode = $"{assetType.ToString().ToUpperInvariant()}-001",
            Name = $"Test {assetType}",
            AssetType = assetType,
            Status = usesDemarcation ? EstateManagedAssetStatus.LandBank : EstateManagedAssetStatus.Available,
            Location = "Accra",
            IsAvailableForSale = true,
            IsPublishedToExternalPortal = !usesDemarcation,
            ExternalListingStatus = "Published",
            ExternalListingType = "Sale",
            ExternalListingCurrency = "GHS"
        };
        var demarcationId = usesDemarcation ? Guid.NewGuid() : (Guid?)null;
        var expectedSourceItemId = demarcationId ?? asset.Id;
        var ticket = new EhcTicket
        {
            TenantId = tenantId,
            TicketNumber = $"EHC-{assetType}-001",
            TicketType = EhcTicketType.Enquiry,
            Status = EhcTicketStatus.Acknowledged,
            Description = "Property enquiry",
            AssignedOrganizationUnitId = salesUnit.Id,
            AssignedOrganizationUnit = salesUnit,
            PropertyListingContextJson = JsonSerializer.Serialize(new EhcPropertyListingContextDto(
                "estate-public-listing",
                expectedSourceItemId,
                asset.AssetCode,
                asset.Name,
                "Sale",
                "GHS",
                asset.Location,
                250000m,
                asset.Id,
                demarcationId,
                null,
                "Prospect",
                "Prospect",
                "prospect@example.test",
                null))
        };
        db.AddRange(structure, level, salesUnit, asset, ticket);
        await db.SaveChangesAsync();

        var source = new SalesSaleableSourceDto
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Code = usesDemarcation ? "LAND_MANAGEMENT" : $"{assetType.ToString().ToUpperInvariant()}_REGISTER",
            DisplayName = assetType.ToString(),
            SourceType = $"{assetType}Register",
            AdapterKey = adapterKey,
            IsActive = true,
            AllowSalesOrders = true,
            SortOrder = 1
        };
        var item = new SalesSaleableItemDto
        {
            SourceId = source.Id,
            SourceCode = source.Code,
            SourceType = source.SourceType,
            AdapterKey = adapterKey,
            SourceItemId = expectedSourceItemId.ToString("D"),
            ItemCode = asset.AssetCode,
            ItemName = asset.Name,
            ItemType = assetType.ToString(),
            CanCreateSalesOrder = true
        };
        var setup = new Mock<ISalesSetupService>();
        setup.Setup(service => service.GetSaleableSourcesAsync(false))
            .ReturnsAsync([source]);
        setup.Setup(service => service.SearchSaleableItemsAsync(source.Id, asset.AssetCode, 100))
            .ReturnsAsync([item]);

        var controller = new EhcPropertyEnquiriesController(
            db,
            User().Object,
            Mock.Of<IEhcTicketService>(),
            Mock.Of<IEstateSalesListingApplicationHandoffService>(),
            Mock.Of<IPropertyEnquiryProspectService>());

        var response = Assert.IsType<OkObjectResult>(
            await controller.GetSalesOrderSource(ticket.Id, setup.Object, default));
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(response.Value));

        Assert.Equal(assetType.ToString(), json.RootElement.GetProperty("data").GetProperty("assetType").GetString());
        Assert.Equal(source.Id, json.RootElement.GetProperty("data").GetProperty("source").GetProperty("Id").GetGuid());
        Assert.Equal(expectedSourceItemId.ToString("D"),
            json.RootElement.GetProperty("data").GetProperty("item").GetProperty("SourceItemId").GetString());
    }

    [Fact]
    public void PublicEnquiryContractValidatesContactDetailsAndExcludesInternalFields()
    {
        var invalid = new PublicPropertyListingEnquiryRequestDto
        {
            SubmissionId = Guid.Empty,
            Message = " ",
            ContactName = " ",
            ContactEmail = "not-an-email",
            ContactPhone = "abc",
            AlternativePhoneNumber = "123",
            PreferredContactMethod = "InternalWorkflow"
        };
        var validationResults = new List<ValidationResult>();

        Assert.False(Validator.TryValidateObject(
            invalid,
            new ValidationContext(invalid),
            validationResults,
            validateAllProperties: true));
        Assert.Contains(validationResults, result => result.MemberNames.Contains(nameof(invalid.ContactEmail)));
        Assert.Contains(validationResults, result => result.MemberNames.Contains(nameof(invalid.ContactPhone)));
        Assert.Contains(validationResults, result => result.MemberNames.Contains(nameof(invalid.AlternativePhoneNumber)));
        // DataAnnotations invokes IValidatableObject only after property-level validation succeeds.
        // Validate the class-level submission and contact-method rules independently so those
        // assertions are not masked by the deliberately invalid contact properties above.
        var invalidSubmission = new PublicPropertyListingEnquiryRequestDto
        {
            SubmissionId = Guid.Empty,
            Message = "Please contact me about this property.",
            ContactName = "Ama Mensah",
            ContactEmail = "ama@example.com",
            ContactPhone = "+233 20 555 0101",
            PreferredContactMethod = "InternalWorkflow"
        };
        var submissionValidationResults = new List<ValidationResult>();
        Assert.False(Validator.TryValidateObject(
            invalidSubmission,
            new ValidationContext(invalidSubmission),
            submissionValidationResults,
            validateAllProperties: true));
        Assert.Contains(
            submissionValidationResults,
            result => result.MemberNames.Contains(nameof(invalidSubmission.SubmissionId)));
        Assert.Contains(
            submissionValidationResults,
            result => result.MemberNames.Contains(nameof(invalidSubmission.PreferredContactMethod)));

        var publicProperties = typeof(PublicPropertyListingEnquiryRequestDto)
            .GetProperties()
            .Select(property => property.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var internalField in new[]
                 {
                     "BusinessPartnerId", "AssignedEmployeeId", "AssignedSalesPersonId", "SalesTeamId",
                     "Status", "OpportunityId", "CreatedByUserId", "ApprovalStatus", "WorkflowId"
                 })
        {
            Assert.DoesNotContain(internalField, publicProperties);
        }
    }

    [Fact]
    public async Task Anonymous_public_listing_resolution_does_not_fall_back_to_an_arbitrary_active_tenant()
    {
        await using var db = Database();
        var unrelatedTenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant
        {
            Id = unrelatedTenantId,
            Code = "UNRELATED",
            Name = "Unrelated active tenant",
            Status = TenantStatus.Active,
            Domain = "unrelated.example.test",
            IsDefaultForPublicUsers = false
        });
        db.EstateManagedAssets.Add(new EstateManagedAsset
        {
            TenantId = unrelatedTenantId,
            AssetCode = "PROP-UNRELATED-001",
            Name = "Unrelated tenant property",
            AssetType = EstateManagedAssetType.Property,
            SourceType = EstateManagedAssetSourceType.Imported,
            Status = EstateManagedAssetStatus.Available,
            Location = "Accra",
            IsPublishedToExternalPortal = true,
            ExternalListingStatus = "Published",
            ExternalListingType = "Sale",
            IsAvailableForSale = true,
            ExternalSalePrice = 125000m,
            ExternalListingCurrency = "GHS"
        });
        await db.SaveChangesAsync();
        var anonymousUser = new Mock<ICurrentUserService>();
        anonymousUser.SetupGet(user => user.TenantId).Returns((Guid?)null);
        var controller = Controller(db, new Mock<IEhcTicketService>(), anonymousUser.Object);
        controller.HttpContext.Request.Host = new HostString("unknown.example.test");

        var result = Assert.IsType<OkObjectResult>(await controller.GetPublicListings(cancellationToken: default));
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(result.Value));

        Assert.Empty(document.RootElement.GetProperty("data").EnumerateArray());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Anonymous_public_listing_resolution_preserves_host_and_public_default_mappings(bool useHostMapping)
    {
        await using var db = Database();
        var mappedTenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant
        {
            Id = mappedTenantId,
            Code = useHostMapping ? "HOST-MAPPED" : "PUBLIC-DEFAULT",
            Name = "Mapped public tenant",
            Status = TenantStatus.Active,
            Domain = useHostMapping ? "mapped.example.test" : "other.example.test",
            IsDefaultForPublicUsers = !useHostMapping
        });
        db.EstateManagedAssets.Add(new EstateManagedAsset
        {
            TenantId = mappedTenantId,
            AssetCode = "PROP-MAPPED-001",
            Name = "Mapped tenant property",
            AssetType = EstateManagedAssetType.Property,
            SourceType = EstateManagedAssetSourceType.Imported,
            Status = EstateManagedAssetStatus.Available,
            Location = "Accra",
            IsPublishedToExternalPortal = true,
            ExternalListingStatus = "Published",
            ExternalListingType = "Sale",
            IsAvailableForSale = true,
            ExternalSalePrice = 125000m,
            ExternalListingCurrency = "GHS"
        });
        await db.SaveChangesAsync();
        var anonymousUser = new Mock<ICurrentUserService>();
        anonymousUser.SetupGet(user => user.TenantId).Returns((Guid?)null);
        var controller = Controller(db, new Mock<IEhcTicketService>(), anonymousUser.Object);
        controller.HttpContext.Request.Host = new HostString(
            useHostMapping ? "mapped.example.test" : "unknown.example.test");

        var result = Assert.IsType<OkObjectResult>(await controller.GetPublicListings(cancellationToken: default));
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(result.Value));

        Assert.Single(document.RootElement.GetProperty("data").EnumerateArray());
    }

    [Fact]
    public async Task AnonymousPublicEnquiryUsesServerControlledFieldsAndPreservesListingAndContactSnapshot()
    {
        await using var db = Database();
        var seeded = await Seed(db);
        var externalUserId = Guid.NewGuid();
        db.Users.Add(new ApplicationUser
        {
            Id = externalUserId,
            TenantId = tenantId,
            UserName = "external",
            Email = "external@default.com",
            FirstName = "Public",
            LastName = "Enquiry",
            IsActive = true
        });
        await db.SaveChangesAsync();

        CreateEhcTicketRequestDto? capturedTicket = null;
        EhcPropertyListingContextDto? capturedProperty = null;
        Guid capturedTenantId = Guid.Empty;
        Guid capturedRequesterId = Guid.Empty;
        var tickets = new Mock<IEhcTicketService>();
        tickets.Setup(service => service.CreatePublicPropertyEnquiryAsync(
                It.IsAny<CreateEhcTicketRequestDto>(),
                It.IsAny<EhcPropertyListingContextDto>(),
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Callback<CreateEhcTicketRequestDto, EhcPropertyListingContextDto, Guid, Guid, Guid, string, CancellationToken>(
                (ticket, property, _, submittedTenantId, requesterId, _, _) =>
                {
                    capturedTicket = ticket;
                    capturedProperty = property;
                    capturedTenantId = submittedTenantId;
                    capturedRequesterId = requesterId;
                })
            .ReturnsAsync(new EhcTicketDetailDto { TicketNumber = "EHC-PUBLIC-001" });

        var result = await Controller(db, tickets).CreatePublicListingEnquiry(
            seeded.Portion.Id,
            new PublicPropertyListingEnquiryRequestDto
            {
                SubmissionId = Guid.NewGuid(),
                Message = "  Please send the deposit and viewing details.  ",
                ContactName = "  Ama Mensah  ",
                ContactEmail = "ama@example.test",
                ContactPhone = "  +233 24 555 0101  ",
                PreferredContactMethod = "Email",
                AlternativePhoneNumber = "+233 50 100 2000",
                ContactReference = "  GH-REF-100  "
            },
            default);

        Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(capturedTicket);
        Assert.NotNull(capturedProperty);
        Assert.Equal(EhcTicketType.Enquiry, capturedTicket.TicketType);
        Assert.Equal(EhcTicketSource.Web, capturedTicket.Source);
        Assert.Equal("Please send the deposit and viewing details.", capturedTicket.Description);
        Assert.Equal("EstateListing", capturedTicket.RelatedEntityType);
        Assert.Equal(seeded.Portion.Id, capturedProperty.ListingId);
        Assert.Equal("LAND-002-D002", capturedProperty.ListingReference);
        Assert.Null(capturedProperty.BusinessPartnerId);
        Assert.Equal("Ama Mensah", capturedProperty.ContactName);
        Assert.Equal("ama@example.test", capturedProperty.ContactEmail);
        Assert.Equal("+233 24 555 0101", capturedProperty.ContactPhone);
        Assert.Equal("+233 50 100 2000", capturedProperty.AlternativePhoneNumber);
        Assert.Equal("Email", capturedProperty.PreferredContactMethod);
        Assert.Equal("GH-REF-100", capturedProperty.ContactReference);
        Assert.Equal(tenantId, capturedTenantId);
        Assert.Equal(externalUserId, capturedRequesterId);
    }

    [Fact]
    public async Task PublicEnquiryNeverFallsBackToAnUnrelatedActiveTenantUser()
    {
        await using var db = Database();
        var seeded = await Seed(db);
        db.Users.Add(new ApplicationUser
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            UserName = "finance.approver",
            Email = "finance@example.test",
            FirstName = "Finance",
            LastName = "Approver",
            IsActive = true
        });
        await db.SaveChangesAsync();

        var tickets = new Mock<IEhcTicketService>();
        var result = await Controller(db, tickets).CreatePublicListingEnquiry(
            seeded.Portion.Id,
            new PublicPropertyListingEnquiryRequestDto
            {
                SubmissionId = Guid.NewGuid(),
                Message = "Please contact me about this listing.",
                ContactName = "Ama Mensah",
                ContactEmail = "ama@example.test",
                ContactPhone = "+233245550101"
            },
            default);

        var unavailable = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, unavailable.StatusCode);
        tickets.Verify(service => service.CreatePublicPropertyEnquiryAsync(
            It.IsAny<CreateEhcTicketRequestDto>(),
            It.IsAny<EhcPropertyListingContextDto>(),
            It.IsAny<Guid>(),
            It.IsAny<Guid>(),
            It.IsAny<Guid>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SupplierOnlyAccountCreatesEnquiryWithServerVerifiedPortionSnapshot()
    {
        await using var db = Database(); var seeded = await Seed(db);
        EhcPropertyListingContextDto? captured = null;
        var tickets = new Mock<IEhcTicketService>();
        tickets.Setup(t => t.CreateExternalPropertyEnquiryAsync(It.IsAny<CreateEhcTicketRequestDto>(), It.IsAny<EhcPropertyListingContextDto>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Callback<CreateEhcTicketRequestDto, EhcPropertyListingContextDto, Guid, CancellationToken>((_, p, _, _) => captured = p)
            .ReturnsAsync(new EhcTicketDetailDto { TicketNumber = "EHC-26-000001" });
        var result = await Controller(db, tickets).CreateListingEnquiry(seeded.Portion.Id,
            new(Guid.NewGuid(), "Can we arrange a visit?", seeded.Partner.Id), default);
        Assert.IsType<OkObjectResult>(result); Assert.NotNull(captured);
        Assert.Equal("estate-public-listing", captured.Source); Assert.Equal(seeded.Portion.Id, captured.ListingId);
        Assert.Equal("LAND-002-D002", captured.ListingReference); Assert.Equal("LAND-002-D002", captured.ListingName);
        Assert.Equal("Sale", captured.ListingType); Assert.Equal("GHS", captured.Currency); Assert.Equal(1250000m, captured.Price);
        Assert.Equal(seeded.Partner.Id, captured.BusinessPartnerId);
    }

    [Fact]
    public async Task ParcelListingsUseUniqueChildReferencesInPortalAndPropertyManagement()
    {
        await using var db = Database();
        var seeded = await Seed(db);
        var legacyPortion = new EstateLandDemarcation
        {
            TenantId = tenantId,
            EstateManagedAssetId = seeded.Asset.Id,
            DemarcationNumber = 3,
            Description = "Legacy plot",
            BoundaryVerified = true,
            IsPublishedToExternalPortal = true,
            ExternalListingStatus = "Published",
            ExternalListingType = "Sale",
            ExternalListingCurrency = "GHS"
        };
        db.Add(legacyPortion);
        await db.SaveChangesAsync();

        var external = Assert.IsType<OkObjectResult>(await Controller(db, new Mock<IEhcTicketService>())
            .GetListings(cancellationToken: default));
        using var externalJson = JsonDocument.Parse(JsonSerializer.Serialize(external.Value));
        var externalListings = externalJson.RootElement.GetProperty("data").EnumerateArray().ToList();
        Assert.Equal("LAND-002-D002", externalListings.Single(item => item.GetProperty("Id").GetGuid() == seeded.Portion.Id)
            .GetProperty("Name").GetString());
        Assert.Equal("LAND-002-D003", externalListings.Single(item => item.GetProperty("Id").GetGuid() == legacyPortion.Id)
            .GetProperty("AssetCode").GetString());
        Assert.All(externalListings, item => Assert.DoesNotContain("PORTION", item.GetProperty("AssetCode").GetString()!));

        var managed = new EstateManagedAssetsController(null!, null!, null!, null!, db, User().Object, null!);
        var propertyManagement = Assert.IsType<OkObjectResult>(await managed.GetPortalListingDemarcations());
        using var managementJson = JsonDocument.Parse(JsonSerializer.Serialize(propertyManagement.Value));
        var managedListings = managementJson.RootElement.GetProperty("data").EnumerateArray().ToList();
        Assert.Equal("LAND-002-D002", managedListings.Single(item => item.GetProperty("Id").GetGuid() == seeded.Portion.Id)
            .GetProperty("Name").GetString());
        Assert.Equal("LAND-002-D003", managedListings.Single(item => item.GetProperty("Id").GetGuid() == legacyPortion.Id)
            .GetProperty("Name").GetString());
        Assert.Equal("Serviced plot", managedListings.Single(item => item.GetProperty("Id").GetGuid() == seeded.Portion.Id)
            .GetProperty("Description").GetString());
    }

    [Fact]
    public void ChildFixedAssetReferenceHasTenantScopedUniqueIndex()
    {
        using var db = Database();
        var index = Assert.Single(db.Model.FindEntityType(typeof(EstateLandDemarcation))!.GetIndexes(), item =>
            item.Properties.Select(property => property.Name).SequenceEqual(new[]
            {
                nameof(EstateLandDemarcation.TenantId),
                nameof(EstateLandDemarcation.ChildFixedAssetReference)
            }));

        Assert.True(index.IsUnique);
        Assert.Equal("[ChildFixedAssetReference] IS NOT NULL", index.GetFilter());
    }

    [Fact]
    public void LandReferenceParserStillAcceptsHistoricalPortionReferences()
    {
        Assert.True(EstateLandDemarcationReference.TryParse("LAND-002-D002", out var childAssetCode, out var childNumber));
        Assert.Equal("LAND-002", childAssetCode);
        Assert.Equal(2, childNumber);
        Assert.True(EstateLandDemarcationReference.TryParse("LAND-002-PORTION-002", out var legacyAssetCode, out var legacyNumber));
        Assert.Equal(childAssetCode, legacyAssetCode);
        Assert.Equal(childNumber, legacyNumber);
    }

    [Fact]
    public async Task UnlinkedPartnerAndUnpublishedListingCannotCreateTickets()
    {
        await using var db = Database(); var seeded = await Seed(db); var tickets = new Mock<IEhcTicketService>();
        var controller = Controller(db, tickets);
        Assert.IsType<BadRequestObjectResult>(await controller.CreateListingEnquiry(seeded.Portion.Id, new(Guid.NewGuid(), "Question", Guid.NewGuid()), default));
        seeded.Portion.IsPublishedToExternalPortal = false; await db.SaveChangesAsync();
        Assert.IsType<NotFoundObjectResult>(await controller.CreateListingEnquiry(seeded.Portion.Id, new(Guid.NewGuid(), "Question", seeded.Partner.Id), default));
        tickets.Verify(t => t.CreateExternalPropertyEnquiryAsync(It.IsAny<CreateEhcTicketRequestDto>(), It.IsAny<EhcPropertyListingContextDto>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SalesFollowUpCannotReadOrReplyToOtherTenantsOrOrdinaryHelpdeskTickets()
    {
        await using var db = Database();
        var ordinary = new EhcTicket { TenantId = tenantId, TicketNumber = "H-1", RequesterUserId = userId, Description = "Private helpdesk", TicketType = EhcTicketType.Helpdesk };
        var foreign = new EhcTicket { TenantId = Guid.NewGuid(), TicketNumber = "H-2", RequesterUserId = userId, Description = "Other tenant", TicketType = EhcTicketType.Enquiry, PropertyListingContextJson = "{}" };
        db.AddRange(ordinary, foreign); await db.SaveChangesAsync();
        var service = new Mock<IEhcTicketService>(); var controller = new EhcPropertyEnquiriesController(db, User().Object, service.Object, Mock.Of<IEstateSalesListingApplicationHandoffService>(), Mock.Of<IPropertyEnquiryProspectService>());
        Assert.IsType<NotFoundResult>(await controller.Get(ordinary.Id, default));
        Assert.IsType<NotFoundResult>(await controller.Get(foreign.Id, default));
        Assert.IsType<NotFoundResult>(await controller.Reply(ordinary.Id, new() { Body = "Reply" }, default));
        service.Verify(t => t.AddAgentMessageAsync(It.IsAny<Guid>(), It.IsAny<AddEhcTicketMessageRequestDto>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SalesQueueRequiresSalesAndMarketingOrganizationUnitAndStatusBeyondNew()
    {
        await using var db = Database();
        var requester = new ApplicationUser
        {
            Id = userId,
            TenantId = tenantId,
            UserName = "supplier.contact",
            Email = "supplier.contact@example.test",
            FirstName = "Supplier",
            LastName = "Contact"
        };
        var structure = new OrganizationStructure { TenantId = tenantId, Name = "TDC structure", Code = "TDC", IsActive = true };
        var level = new OrganizationLevel
        {
            TenantId = tenantId,
            StructureId = structure.Id,
            OrganizationStructure = structure,
            Name = "Department",
            Code = "DEPT",
            LevelNumber = 3,
            IsActive = true
        };
        var salesAndMarketing = new OrganizationUnit
        {
            TenantId = tenantId,
            OrganizationLevelId = level.Id,
            OrganizationLevel = level,
            Name = "Marketing Unit",
            Code = "UNIT-MKT",
            Path = "/TDC/MKT",
            IsActive = true
        };
        var otherUnit = new OrganizationUnit
        {
            TenantId = tenantId,
            OrganizationLevelId = level.Id,
            OrganizationLevel = level,
            Name = "Operations Unit",
            Code = "UNIT-OPS",
            Path = "/TDC/OPS",
            IsActive = true
        };
        var legacySalesDepartment = new Department
        {
            TenantId = tenantId,
            Name = "Sales",
            Code = "SALES",
            AccountCode = "SALES",
            DepartmentType = DepartmentType.Sales,
            IsActive = true
        };
        EhcTicket Ticket(string number, EhcTicketStatus status, Guid? organizationUnitId) => new()
        {
            TenantId = tenantId,
            TicketNumber = number,
            RequesterUserId = requester.Id,
            TicketType = EhcTicketType.Enquiry,
            Description = "Property enquiry",
            PropertyListingContextJson = "{}",
            Status = status,
            AssignedOrganizationUnitId = organizationUnitId
        };

        var ready = Ticket("EHC-READY", EhcTicketStatus.Acknowledged, salesAndMarketing.Id);
        var newAtSales = Ticket("EHC-NEW", EhcTicketStatus.New, salesAndMarketing.Id);
        var routedToOtherUnit = Ticket("EHC-OPS", EhcTicketStatus.Acknowledged, otherUnit.Id);
        var unassigned = Ticket("EHC-NONE", EhcTicketStatus.InProgress, null);
        var legacyDepartmentOnly = Ticket("EHC-LEGACY", EhcTicketStatus.Acknowledged, null);
        legacyDepartmentOnly.AssignedDepartmentId = legacySalesDepartment.Id;
        db.AddRange(requester, structure, level, salesAndMarketing, otherUnit, legacySalesDepartment, ready, newAtSales, routedToOtherUnit, unassigned, legacyDepartmentOnly);
        await db.SaveChangesAsync();

        var controller = new EhcPropertyEnquiriesController(db, User().Object, Mock.Of<IEhcTicketService>(), Mock.Of<IEstateSalesListingApplicationHandoffService>(), Mock.Of<IPropertyEnquiryProspectService>());
        var result = Assert.IsType<OkObjectResult>(await controller.List(cancellationToken: default));
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(result.Value));
        var ids = document.RootElement.GetProperty("data").EnumerateArray()
            .Select(item => item.GetProperty("Id").GetGuid()).ToList();

        Assert.Equal(1, document.RootElement.GetProperty("totalCount").GetInt32());
        Assert.Equal(new[] { ready.Id }, ids);
    }

    [Fact]
    public async Task LandSalesSourceReturnsOnlyPublishedDemarcationsThatMatchConfiguredFilters()
    {
        await using var db = Database();
        var asset = Asset();
        asset.IsPublishedToExternalPortal = false;
        var sale = new EstateLandDemarcation
        {
            TenantId = tenantId,
            EstateManagedAsset = asset,
            EstateManagedAssetId = asset.Id,
            DemarcationNumber = 1,
            ChildFixedAssetReference = "LAND-002-D001",
            Description = "Published sale plot",
            BoundaryCoordinates = "[]",
            BoundaryVerified = true,
            IsPublishedToExternalPortal = true,
            ExternalListingStatus = "Published",
            ExternalListingType = "Sale",
            ExternalListingCurrency = "GHS",
            ExternalSalePrice = 300000m
        };
        var rent = new EstateLandDemarcation
        {
            TenantId = tenantId,
            EstateManagedAsset = asset,
            EstateManagedAssetId = asset.Id,
            DemarcationNumber = 2,
            Description = "Published rental plot",
            BoundaryCoordinates = "[]",
            BoundaryVerified = true,
            IsPublishedToExternalPortal = true,
            ExternalListingStatus = "Published",
            ExternalListingType = "Rent",
            ExternalListingCurrency = "GHS",
            ExternalMonthlyRent = 2000m
        };
        var unpublished = new EstateLandDemarcation
        {
            TenantId = tenantId,
            EstateManagedAsset = asset,
            EstateManagedAssetId = asset.Id,
            DemarcationNumber = 3,
            Description = "Draft sale plot",
            BoundaryCoordinates = "[]",
            BoundaryVerified = true,
            IsPublishedToExternalPortal = false,
            ExternalListingStatus = "Draft",
            ExternalListingType = "Sale",
            ExternalListingCurrency = "GHS"
        };
        db.AddRange(asset, sale, rent, unpublished);
        await db.SaveChangesAsync();
        using var unit = new UnitOfWork(db);
        var user = new Mock<ICurrentUserProvider>();
        user.SetupGet(item => item.TenantId).Returns(tenantId);
        var adapter = new LandManagementSaleableSourceAdapter(unit, user.Object);
        var source = new SalesSaleableSource
        {
            TenantId = tenantId,
            Code = "LAND_MANAGEMENT",
            DisplayName = "Land Management",
            SourceType = "LandManagement",
            AdapterKey = "land-management",
            IsActive = true,
            AllowSalesOrders = true,
            AllowSalesAgreements = true,
            SettingsJson = "{\"filters\":[{\"field\":\"externalListingType\",\"value\":\"Sale\"}]}"
        };

        var results = await adapter.SearchItemsAsync(source);

        var result = Assert.Single(results);
        Assert.Equal(sale.Id.ToString(), result.SourceItemId);
        Assert.Equal("LAND-002-D001", result.PropertyReference);
        Assert.Equal("LAND-002-D001", result.ItemName);
        Assert.Equal(300000m, result.EstimatedValue);
        Assert.DoesNotContain(results, item => item.SourceItemId == rent.Id.ToString());
        Assert.DoesNotContain(results, item => item.SourceItemId == unpublished.Id.ToString());
    }

    [Fact]
    public async Task PropertyEnquiryLoadsPaymentOnlyFromMatchingCustomerAndOpportunitySalesOrder()
    {
        await using var db = Database();
        var requester = new ApplicationUser
        {
            Id = userId,
            TenantId = tenantId,
            UserName = "buyer.contact",
            FirstName = "Buyer",
            LastName = "Contact"
        };
        var structure = new OrganizationStructure { TenantId = tenantId, Name = "TDC structure", Code = "TDC", IsActive = true };
        var level = new OrganizationLevel { TenantId = tenantId, StructureId = structure.Id, OrganizationStructure = structure, Name = "Department", Code = "DEPT", LevelNumber = 3, IsActive = true };
        var sales = new OrganizationUnit { TenantId = tenantId, OrganizationLevelId = level.Id, OrganizationLevel = level, Name = "Marketing", Code = "UNIT-MKT", Path = "/TDC/MKT", IsActive = true };
        var partner = new BusinessPartner { TenantId = tenantId, PartnerCode = "CUS-001", PartnerName = "Buyer One", PartnerType = "Customer", IsActive = true, ApprovalStatus = "Approved" };
        var otherPartner = new BusinessPartner { TenantId = tenantId, PartnerCode = "CUS-002", PartnerName = "Buyer Two", PartnerType = "Customer", IsActive = true, ApprovalStatus = "Approved" };
        var listingId = Guid.NewGuid();
        var opportunityId = Guid.NewGuid();
        var ticket = new EhcTicket
        {
            TenantId = tenantId,
            TicketNumber = "EHC-SALES-001",
            RequesterUserId = requester.Id,
            TicketType = EhcTicketType.Enquiry,
            Status = EhcTicketStatus.Acknowledged,
            Description = "I want this plot",
            AssignedOrganizationUnitId = sales.Id,
            PropertyListingContextJson = JsonSerializer.Serialize(new EhcPropertyListingContextDto(
                "estate-public-listing", listingId, "LAND-002-PORTION-002", "Parcel Two", "Sale", "GHS", "Accra", 1250000m,
                Guid.NewGuid(), listingId, partner.Id, partner.PartnerName, null, null, null))
        };
        var invoice = new Invoice
        {
            TenantId = tenantId,
            InvoiceNumber = "INV-001",
            BusinessPartnerId = partner.Id,
            BusinessPartnerRoleId = Guid.NewGuid(),
            BusinessPartnerArProfileVersionId = Guid.NewGuid(),
            BusinessPartnerCode = partner.PartnerCode,
            CustomerName = partner.PartnerName,
            InvoiceDate = DateTime.UtcNow.Date,
            TotalAmount = 1250000m,
            PaidAmount = 400000m,
            CurrencyCode = "GHS"
        };
        var order = new SalesOrder
        {
            TenantId = tenantId,
            DocumentNumber = "SO-001",
            BusinessPartnerId = partner.Id,
            CustomerName = partner.PartnerName,
            PropertyReference = "LAND-002-PORTION-002",
            OrderStatus = SalesOrderStatus.Closed,
            TotalAmount = 1250000m,
            Currency = "GHS",
            OpportunityId = opportunityId,
            InvoiceId = invoice.Id
        };
        var sameCustomerWrongOpportunity = new SalesOrder
        {
            TenantId = tenantId,
            DocumentNumber = "SO-WRONG-OPPORTUNITY",
            BusinessPartnerId = partner.Id,
            CustomerName = partner.PartnerName,
            PropertyReference = "LAND-002-PORTION-002",
            OpportunityId = Guid.NewGuid(),
            OrderStatus = SalesOrderStatus.Closed,
            TotalAmount = 1250000m,
            Currency = "GHS",
            CreatedAt = DateTime.UtcNow.AddMinutes(1)
        };
        var otherOrder = new SalesOrder
        {
            TenantId = tenantId,
            DocumentNumber = "SO-OTHER",
            BusinessPartnerId = otherPartner.Id,
            CustomerName = otherPartner.PartnerName,
            PropertyReference = "LAND-002-PORTION-002",
            OrderStatus = SalesOrderStatus.Closed,
            TotalAmount = 999999m,
            Currency = "GHS"
        };
        var payment = new CustomerPayment
        {
            TenantId = tenantId,
            PaymentNumber = "RCT-001",
            BusinessPartnerId = partner.Id,
            BusinessPartnerRoleId = Guid.NewGuid(),
            BusinessPartnerArProfileVersionId = Guid.NewGuid(),
            BusinessPartnerCode = partner.PartnerCode,
            BusinessPartnerName = partner.PartnerName,
            PaymentDate = DateTime.UtcNow.Date,
            TotalAmount = 400000m,
            AllocatedAmount = 400000m,
            CurrencyCode = "GHS",
            TransactionReference = "BANK-REF-001",
            Status = "Approved"
        };
        var allocation = new PaymentAllocation
        {
            TenantId = tenantId,
            CustomerPaymentId = payment.Id,
            InvoiceId = invoice.Id,
            AllocatedAmount = 400000m,
            PaymentCurrencyAmount = 400000m,
            InvoiceCurrencyCode = "GHS",
            PaymentCurrencyCode = "GHS"
        };
        var closedAt = DateTime.UtcNow.AddDays(-1);
        var history = new SalesOrderStatusHistory
        {
            TenantId = tenantId,
            SalesOrderId = order.Id,
            ToStatus = SalesOrderStatus.Closed,
            ChangedAt = closedAt
        };
        var prospect = new EhcPropertyEnquiryProspect
        {
            TenantId = tenantId,
            TicketId = ticket.Id,
            LeadId = Guid.NewGuid(),
            OpportunityId = opportunityId,
            BusinessPartnerId = partner.Id,
            Status = EhcPropertyProspectStatuses.Converted,
            AgreedAmount = order.TotalAmount,
            Currency = order.Currency
        };
        db.AddRange(requester, structure, level, sales, partner, otherPartner, ticket, invoice, order,
            sameCustomerWrongOpportunity, otherOrder, prospect, payment, allocation, history);
        await db.SaveChangesAsync();
        var controller = new EhcPropertyEnquiriesController(db, User().Object, Mock.Of<IEhcTicketService>(), Mock.Of<IEstateSalesListingApplicationHandoffService>(), Mock.Of<IPropertyEnquiryProspectService>());

        var result = Assert.IsType<OkObjectResult>(await controller.GetEstateHandoff(ticket.Id, default));
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(result.Value));
        var salesOrder = document.RootElement.GetProperty("data").GetProperty("salesOrder");

        Assert.Equal(order.Id, salesOrder.GetProperty("id").GetGuid());
        Assert.Equal("SO-001", salesOrder.GetProperty("reference").GetString());
        Assert.Equal(400000m, salesOrder.GetProperty("amountPaid").GetDecimal());
        Assert.Equal("BANK-REF-001", salesOrder.GetProperty("paymentReference").GetString());
        Assert.Equal(closedAt.Date, salesOrder.GetProperty("completedAt").GetDateTime().Date);
    }

    [Fact]
    public async Task MyPropertiesIncludesPriorSalesBillAndReceiptForSameParcel()
    {
        await using var db = Database();
        var customer = new BusinessPartner
        {
            TenantId = tenantId,
            PartnerCode = "CUS-BILL-076",
            PartnerName = "Ikea Customer",
            PartnerType = "Customer",
            CustomerAccountNumber = "CUS-BILL-076",
            UserId = userId,
            IsActive = true,
            ApprovalStatus = "Approved"
        };
        var parcel = new EstateManagedAsset
        {
            TenantId = tenantId,
            AssetCode = "LAND-076",
            ProjectUnitCode = "PLOT-076",
            Name = "Bill 76",
            AssetType = EstateManagedAssetType.Land,
            Status = EstateManagedAssetStatus.Sold,
            Location = "Accra",
            CustomerBusinessPartnerId = customer.Id,
            ExternalListingType = "Sale",
            ExternalListingCurrency = "GHS"
        };
        var salesInvoice = new Invoice
        {
            TenantId = tenantId,
            InvoiceNumber = "INV-SALES-076",
            BusinessPartnerId = customer.Id,
            BusinessPartnerRoleId = Guid.NewGuid(),
            BusinessPartnerArProfileVersionId = Guid.NewGuid(),
            BusinessPartnerCode = customer.PartnerCode,
            CustomerName = customer.PartnerName,
            InvoiceDate = DateTime.UtcNow.Date.AddDays(-10),
            TotalAmount = 500000m,
            PaidAmount = 500000m,
            CurrencyCode = "GHS",
            Status = InvoiceStatus.Paid,
            Reference = "SO-BILL-076",
            Notes = "Source: Sales"
        };
        var estateInvoice = new Invoice
        {
            TenantId = tenantId,
            InvoiceNumber = "INV-ESTATE-076",
            BusinessPartnerId = customer.Id,
            BusinessPartnerRoleId = Guid.NewGuid(),
            BusinessPartnerArProfileVersionId = Guid.NewGuid(),
            BusinessPartnerCode = customer.PartnerCode,
            CustomerName = customer.PartnerName,
            InvoiceDate = DateTime.UtcNow.Date,
            TotalAmount = 120000m,
            PaidAmount = 120000m,
            CurrencyCode = "GHS",
            Status = InvoiceStatus.Paid,
            Reference = "ESTATE-BILL-076",
            Notes = "Source: Estate / Property Management -> Finance AR"
        };
        var unrelatedSalesInvoice = new Invoice
        {
            TenantId = tenantId,
            InvoiceNumber = "INV-SALES-OTHER",
            BusinessPartnerId = customer.Id,
            BusinessPartnerRoleId = Guid.NewGuid(),
            BusinessPartnerArProfileVersionId = Guid.NewGuid(),
            BusinessPartnerCode = customer.PartnerCode,
            CustomerName = customer.PartnerName,
            InvoiceDate = DateTime.UtcNow.Date.AddDays(-5),
            TotalAmount = 300000m,
            PaidAmount = 300000m,
            CurrencyCode = "GHS",
            Status = InvoiceStatus.Paid,
            Reference = "SO-OTHER",
            Notes = "Source: Sales"
        };
        var salesOrder = new SalesOrder
        {
            TenantId = tenantId,
            DocumentNumber = "SO-BILL-076",
            BusinessPartnerId = customer.Id,
            CustomerName = customer.PartnerName,
            PropertyReference = parcel.AssetCode,
            OrderStatus = SalesOrderStatus.Closed,
            TotalAmount = salesInvoice.TotalAmount,
            Currency = "GHS",
            InvoiceId = salesInvoice.Id
        };
        var unrelatedOrder = new SalesOrder
        {
            TenantId = tenantId,
            DocumentNumber = "SO-OTHER",
            BusinessPartnerId = customer.Id,
            CustomerName = customer.PartnerName,
            PropertyReference = "LAND-OTHER",
            OrderStatus = SalesOrderStatus.Closed,
            TotalAmount = unrelatedSalesInvoice.TotalAmount,
            Currency = "GHS",
            InvoiceId = unrelatedSalesInvoice.Id
        };
        var salesLine = new InvoiceLineItem
        {
            TenantId = tenantId,
            InvoiceId = salesInvoice.Id,
            Description = "Sales bill for LAND-076",
            Quantity = 1,
            UnitPrice = salesInvoice.TotalAmount
        };
        var payment = new CustomerPayment
        {
            TenantId = tenantId,
            PaymentNumber = "RCT-SALES-076",
            BusinessPartnerId = customer.Id,
            BusinessPartnerRoleId = Guid.NewGuid(),
            BusinessPartnerArProfileVersionId = Guid.NewGuid(),
            BusinessPartnerCode = customer.PartnerCode,
            BusinessPartnerName = customer.PartnerName,
            PaymentDate = DateTime.UtcNow.Date.AddDays(-9),
            TotalAmount = salesInvoice.PaidAmount,
            AllocatedAmount = salesInvoice.PaidAmount,
            CurrencyCode = "GHS",
            PaymentMethod = "BankTransfer",
            TransactionReference = "BANK-SALES-076",
            Status = "Approved"
        };
        var allocation = new PaymentAllocation
        {
            TenantId = tenantId,
            CustomerPaymentId = payment.Id,
            CustomerPayment = payment,
            InvoiceId = salesInvoice.Id,
            Invoice = salesInvoice,
            AllocatedAmount = salesInvoice.PaidAmount,
            PaymentCurrencyAmount = salesInvoice.PaidAmount,
            InvoiceCurrencyCode = "GHS",
            PaymentCurrencyCode = "GHS"
        };
        db.AddRange(customer, parcel, salesInvoice, estateInvoice, unrelatedSalesInvoice, salesOrder,
            unrelatedOrder, salesLine, payment, allocation);
        await db.SaveChangesAsync();

        var result = Assert.IsType<OkObjectResult>(await Controller(db, new Mock<IEhcTicketService>()).GetMyProperties(default));
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(result.Value));
        var invoices = document.RootElement.GetProperty("data").GetProperty("Invoices").EnumerateArray().ToList();

        Assert.Contains(invoices, item => item.GetProperty("InvoiceNumber").GetString() == "INV-ESTATE-076");
        var matchedSalesInvoice = Assert.Single(invoices, item => item.GetProperty("InvoiceNumber").GetString() == "INV-SALES-076");
        Assert.DoesNotContain(invoices, item => item.GetProperty("InvoiceNumber").GetString() == "INV-SALES-OTHER");
        Assert.Equal(620000m, invoices.Sum(item => item.GetProperty("PaidAmount").GetDecimal()));
        Assert.Equal("Sales bill for LAND-076", matchedSalesInvoice.GetProperty("Description").GetString());

        var receipt = Assert.Single(matchedSalesInvoice.GetProperty("Receipts").EnumerateArray());
        Assert.Equal("RCT-SALES-076", receipt.GetProperty("PaymentNumber").GetString());
        Assert.Equal("BANK-SALES-076", receipt.GetProperty("TransactionReference").GetString());
    }

    [Fact]
    public async Task EstateHandoffRequiresClosedWonOpportunity()
    {
        await using var db = Database();
        var asset = Asset();
        var partner = new BusinessPartner { TenantId = tenantId, PartnerCode = "CUS-ESTATE", PartnerName = "Estate Customer", PartnerType = "Customer", IsActive = true, ApprovalStatus = "Approved" };
        var opportunity = new Opportunity { TenantId = tenantId, Name = "Property enquiry", Stage = "Qualification", Amount = 1250000m, Currency = "GHS" };
        db.AddRange(asset, partner, opportunity); await db.SaveChangesAsync();
        var procedures = new Mock<IProcedureCaseService>();
        var service = new EstateSalesListingApplicationHandoffService(
            db,
            procedures.Object,
            Mock.Of<INotificationService>());

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(tenantId,
            new(asset.Id, partner.Id, "Sale", opportunity.Id, "AGR-001", 1250000m,
                RequestedLeaseTerm: null, SalesAmountPaid: 0m, SalesPaymentReference: null,
                Currency: "GHS", SalesCompletedAt: DateTime.UtcNow, Notes: null)));

        Assert.Equal("Close the linked Sales opportunity as Won before handing the enquiry to Estate.", error.Message);
        procedures.Verify(item => item.CreateCaseAsync(It.IsAny<CreateProcedureCaseRequest>()), Times.Never);
    }

    [Fact]
    public async Task EstateHandoffCreatesOneTraceableEstateCaseForClosedWonOpportunity()
    {
        // ExecuteUpdate requires a relational provider. Keep this fixture isolated from UAT.
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        connection.CreateFunction<string?, int>("ISJSON", value =>
        {
            if (string.IsNullOrWhiteSpace(value)) return 0;
            try
            {
                using var json = JsonDocument.Parse(value);
                return json.RootElement.ValueKind is JsonValueKind.Object or JsonValueKind.Array ? 1 : 0;
            }
            catch (JsonException) { return 0; }
        });
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection).Options);
        // This focused fixture exercises reservation persistence, not unrelated master-data FKs.
        await db.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys = OFF;");
        var tables = new[] { typeof(EstateManagedAsset), typeof(BusinessPartner), typeof(BusinessPartnerUser), typeof(Opportunity),
            typeof(ProcedureCase), typeof(ProcedureCaseField) }
            .Select(type => db.Model.FindEntityType(type)!.GetTableName()!).ToArray();
        foreach (var statement in db.Database.GenerateCreateScript().Split(';', StringSplitOptions.RemoveEmptyEntries)
            .Where(statement => tables.Any(table => statement.Contains($"CREATE TABLE \"{table}\"", StringComparison.Ordinal))))
        {
            await db.Database.ExecuteSqlRawAsync(statement
                .Replace("nvarchar(max)", "TEXT", StringComparison.OrdinalIgnoreCase)
                .Replace("varchar(max)", "TEXT", StringComparison.OrdinalIgnoreCase)
                .Replace("varbinary(max)", "BLOB", StringComparison.OrdinalIgnoreCase)
                .Replace("GETUTCDATE()", "CURRENT_TIMESTAMP", StringComparison.OrdinalIgnoreCase)
                .Replace("NEWID()", "lower(hex(randomblob(16)))", StringComparison.OrdinalIgnoreCase)
                .Replace("\"RowVersion\" BLOB NOT NULL", "\"RowVersion\" BLOB NOT NULL DEFAULT X''", StringComparison.Ordinal));
        }
        var asset = Asset();
        asset.AssetType = EstateManagedAssetType.Property;
        asset.Status = EstateManagedAssetStatus.Available;
        asset.IsPublishedToExternalPortal = true;
        asset.ExternalListingStatus = "Published";
        var partner = new BusinessPartner
        {
            TenantId = tenantId,
            PartnerCode = "CUS-ESTATE",
            PartnerName = "Estate Customer",
            PartnerType = "Customer",
            CustomerAccountNumber = "CUS-ESTATE",
            UserId = Guid.NewGuid(),
            IsActive = true,
            ApprovalStatus = "Approved"
        };
        var opportunity = new Opportunity { TenantId = tenantId, Name = "Property enquiry", Stage = "Closed Won", Amount = 1250000m, Currency = "GHS", ActualCloseDate = DateTime.UtcNow };
        db.AddRange(asset, partner, opportunity); await db.SaveChangesAsync();

        CreateProcedureCaseRequest? captured = null;
        var caseId = Guid.NewGuid();
        var procedures = new Mock<IProcedureCaseService>();
        procedures.Setup(item => item.CreateCaseAsync(It.IsAny<CreateProcedureCaseRequest>()))
            .Callback<CreateProcedureCaseRequest>(request => captured = request)
            .ReturnsAsync(new ProcedureCaseDetailDto(caseId, "PropertyManagement", "EstatePropertyManagementListingApplication",
                "Purchase enquiry - Parcel Two", "ESTATE-001", partner.PartnerName, "Sales - Estate Enquiry", DateTime.UtcNow,
                "Accepted", "Open", 0, "Estate review", "Estate Manager", "Estate Manager", true, null, true,
                Array.Empty<string>(), Array.Empty<ProcedureCaseFieldDto>(), Array.Empty<ProcedureCaseChecklistItemDto>(),
                Array.Empty<ProcedureCaseDocumentDto>(), Array.Empty<ProcedureCaseActivityDto>()));
        var service = new EstateSalesListingApplicationHandoffService(
            db,
            procedures.Object,
            Mock.Of<INotificationService>());

        var result = await service.CreateAsync(tenantId,
            new(asset.Id, partner.Id, "Sale", opportunity.Id, "AGR-001", 1200000m,
                RequestedLeaseTerm: null, SalesAmountPaid: 250000m, SalesPaymentReference: "RCT-001",
                Currency: "GHS", SalesCompletedAt: opportunity.ActualCloseDate,
                Notes: "Accepted offer", EhcTicketId: Guid.NewGuid(), EhcTicketNumber: "EHC-26-000001"));

        Assert.False(result.AlreadyExists); Assert.Equal(caseId, result.ProcedureCaseId);
        Assert.NotNull(captured); Assert.Equal("AGR-001", captured!.FieldValues!["salesReference"]);
        Assert.Equal("250000.00", captured.FieldValues["salesAmountPaid"]);
        Assert.Equal("RCT-001", captured.FieldValues["salesPaymentReference"]);
        Assert.Equal("950000.00", captured.FieldValues["estateRemainingAmount"]);
        Assert.Equal(opportunity.Id.ToString(), captured.FieldValues["salesOpportunityId"]);
        Assert.Equal("EHC-26-000001", captured.FieldValues["ehcTicketNumber"]);
        procedures.Verify(item => item.CreateCaseAsync(It.IsAny<CreateProcedureCaseRequest>()), Times.Once);
        var reserved = await db.EstateManagedAssets.AsNoTracking().SingleAsync(item => item.Id == asset.Id);
        Assert.False(reserved.IsPublishedToExternalPortal);
        Assert.Equal("Published", reserved.ExternalListingStatus);
        Assert.Equal(EstateManagedAssetStatus.Available, reserved.Status);
    }

    [Fact]
    public async Task ListingApplicationRequiresPublishedWorkflow()
    {
        await using var db = Database();
        var currentUser = User();
        currentUser.SetupGet(item => item.UserName).Returns("estate.manager");
        currentUser.SetupGet(item => item.Roles).Returns(["Estate Manager"]);
        var workflow = new Mock<IWorkflowEngine>();
        var procedures = new ProcedureCaseService(
            db,
            currentUser.Object,
            new LegalProcedureCatalogService(),
            new EstateProcedureCatalogService(),
            new FacilitiesProcedureCatalogService(),
            new PropertyManagementProcedureCatalogService(),
            new PlanningProcedureCatalogService(),
            workflow.Object,
            Mock.Of<INotificationService>(),
            Mock.Of<IFileStorageService>(),
            Mock.Of<IInvoiceService>(),
            Mock.Of<ICentralDocumentPdfSigningService>(),
            Mock.Of<IJobCardService>(),
            Mock.Of<IEhcTicketService>());

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => procedures.CreateCaseAsync(new CreateProcedureCaseRequest(
            "PropertyManagement",
            "EstatePropertyManagementListingApplication",
            "Purchase enquiry - Parcel Two",
            "ESTATE-MANUAL-001",
            "Estate Customer",
            "Sales - Estate Enquiry",
            DateTime.UtcNow,
            "Completed Sales transaction handed to Estate.",
            new Dictionary<string, string?>
            {
                ["applicationReference"] = "ESTATE-MANUAL-001",
                ["listingReference"] = "LAND-002-PORTION-002",
                ["requestType"] = "Sale",
                ["currency"] = "GHS"
            })));

        Assert.Contains("Publish a workflow", error.Message);
        Assert.False(await db.ProcedureCases.AnyAsync());
        Assert.DoesNotContain(workflow.Invocations, item => item.Method.Name == nameof(IWorkflowEngine.StartWorkflowAsync));
    }

    [Fact]
    public async Task EstateHandoffResolvesAnAcknowledgedPropertyEnquiryAfterEstateAcceptsIt()
    {
        await using var db = Database();
        var structure = new OrganizationStructure { TenantId = tenantId, Name = "TDC structure", Code = "TDC", IsActive = true };
        var level = new OrganizationLevel { TenantId = tenantId, OrganizationStructure = structure,
            StructureId = structure.Id, Name = "Department", Code = "DEPT", LevelNumber = 3, IsActive = true };
        var sales = new OrganizationUnit { TenantId = tenantId, OrganizationLevel = level,
            OrganizationLevelId = level.Id, Name = "Marketing Unit", Code = "UNIT-MKT", Path = "/TDC/MKT", IsActive = true };
        var partner = new BusinessPartner { TenantId = tenantId, PartnerCode = "CUS-HANDOFF", PartnerName = "Estate Customer", PartnerType = "Customer", IsActive = true, ApprovalStatus = "Approved" };
        var opportunity = new Opportunity { TenantId = tenantId, Name = "Property enquiry", Stage = "Closed Won", Amount = 1250000m, Currency = "GHS", ActualCloseDate = DateTime.UtcNow };
        var listingId = Guid.NewGuid();
        var ticket = new EhcTicket
        {
            TenantId = tenantId,
            TicketNumber = "EHC-HANDOFF",
            RequesterUserId = userId,
            TicketType = EhcTicketType.Enquiry,
            Status = EhcTicketStatus.Acknowledged,
            Description = "Property enquiry",
            AssignedOrganizationUnitId = sales.Id,
            CrmOpportunityId = opportunity.Id,
            PropertyListingContextJson = JsonSerializer.Serialize(new EhcPropertyListingContextDto(
                "estate-public-listing", listingId, "LAND-002-PORTION-002", "Parcel Two", "Sale", "GHS", "Accra", 1250000m,
                Guid.NewGuid(), listingId, partner.Id, partner.PartnerName, null, null, null))
        };
        db.AddRange(sales, partner, opportunity, ticket);
        await db.SaveChangesAsync();

        var tickets = new Mock<IEhcTicketService>();
        tickets.Setup(item => item.AddInternalCommentAsync(ticket.Id, It.IsAny<AddEhcTicketMessageRequestDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EhcTicketMessageDto { Id = Guid.NewGuid(), Body = "Estate handoff", IsInternal = true });
        var handoffs = new Mock<IEstateSalesListingApplicationHandoffService>();
        var estateCaseId = Guid.NewGuid();
        handoffs.Setup(item => item.CreateAsync(tenantId, It.IsAny<EstateSalesListingApplicationHandoffRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EstateSalesListingApplicationHandoffResult(estateCaseId, "ESTATE-001", "Purchase enquiry", "Open", "Estate review", DateTime.UtcNow, false));

        var controller = new EhcPropertyEnquiriesController(db, User().Object, tickets.Object, handoffs.Object, Mock.Of<IPropertyEnquiryProspectService>());
        var result = await controller.CreateEstateHandoff(ticket.Id, new("AGR-001", 1250000m,
            RequestedLeaseTerm: null, SalesAmountPaid: 1250000m, SalesPaymentReference: "RCT-FULL",
            Currency: "GHS", SalesCompletedAt: opportunity.ActualCloseDate, Notes: null), default);

        Assert.IsType<OkObjectResult>(result);
        tickets.Verify(item => item.TransitionTicketAsync(ticket.Id, EhcTicketStatus.InProgress, It.IsAny<string>(), null, null, It.IsAny<CancellationToken>()), Times.Once);
        tickets.Verify(item => item.TransitionTicketAsync(ticket.Id, EhcTicketStatus.Resolved, It.Is<string>(note => note.Contains("ESTATE-001")), null, null, It.IsAny<CancellationToken>()), Times.Once);
        var saved = await db.EhcTickets.SingleAsync(item => item.Id == ticket.Id);
        Assert.Equal(estateCaseId, saved.EstateListingApplicationCaseId);
        Assert.Equal("ESTATE-001", saved.EstateListingApplicationReference);
        handoffs.Verify(item => item.CreateAsync(
            tenantId,
            It.Is<EstateSalesListingApplicationHandoffRequest>(request =>
                request.ListingId == listingId
                && request.EhcTicketId == ticket.Id
                && request.SalesOpportunityId == opportunity.Id),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
