using System.Text.Json;
using ErpSystem.Api.Controllers.Estate;
using ErpSystem.Api.Controllers.Ehc;
using ErpSystem.Api.Services.Estate;
using ErpSystem.Api.Services;
using ErpSystem.Api.Services.DocumentManagement;
using ErpSystem.Core.DTOs.Ehc;
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
using ErpSystem.Core.Interfaces.Workflow;
using ErpSystem.Core.Services.Estate;
using ErpSystem.Core.Services.Sales;
using ErpSystem.Core.Services.Legal;
using ErpSystem.Core.Services.Planning;
using ErpSystem.Data;
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
    private EstateExternalDocumentsController Controller(ApplicationDbContext db, Mock<IEhcTicketService> tickets)
        => new(db, User().Object, null!, null!, null!, null!, null!, tickets.Object, Mock.Of<ICaptchaVerificationService>(),
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
            EstateManagedAssetId = asset.Id, DemarcationNumber = 2, Description = "Serviced plot", BoundaryVerified = true,
            IsPublishedToExternalPortal = true, ExternalListingStatus = "Published", ExternalListingType = "Sale",
            ExternalListingCurrency = "GHS", ExternalSalePrice = 1250000m };
        var partner = new BusinessPartner { TenantId = tenantId, PartnerCode = "SUP-TEST", PartnerName = "Supplier Only Ltd",
            PartnerType = "Supplier", IsActive = true, ApprovalStatus = "Approved", UserId = userId };
        db.AddRange(asset, portion, partner, new EhcTicketCategory { TenantId = tenantId, Code = "PROPERTY-LISTING", Name = "Property enquiry", AppliesToType = EhcTicketType.Enquiry });
        await db.SaveChangesAsync(); return (asset, portion, partner);
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
        Assert.Equal("LAND-002-PORTION-002", captured.ListingReference); Assert.Equal("Parcel Two - Parcel 002", captured.ListingName);
        Assert.Equal("Sale", captured.ListingType); Assert.Equal("GHS", captured.Currency); Assert.Equal(1250000m, captured.Price);
        Assert.Equal(seeded.Partner.Id, captured.BusinessPartnerId);
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
        var service = new Mock<IEhcTicketService>(); var controller = new EhcPropertyEnquiriesController(db, User().Object, service.Object, Mock.Of<IEstateSalesListingApplicationHandoffService>());
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

        var controller = new EhcPropertyEnquiriesController(db, User().Object, Mock.Of<IEhcTicketService>(), Mock.Of<IEstateSalesListingApplicationHandoffService>());
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
        Assert.Equal("LAND-002-PORTION-001", result.PropertyReference);
        Assert.Equal(300000m, result.EstimatedValue);
        Assert.DoesNotContain(results, item => item.SourceItemId == rent.Id.ToString());
        Assert.DoesNotContain(results, item => item.SourceItemId == unpublished.Id.ToString());
    }

    [Fact]
    public async Task PropertyEnquiryLoadsPaymentOnlyFromMatchingCustomerAndLandSalesOrder()
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
            InvoiceId = invoice.Id
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
        db.AddRange(requester, structure, level, sales, partner, otherPartner, ticket, invoice, order, otherOrder, payment, allocation, history);
        await db.SaveChangesAsync();
        var controller = new EhcPropertyEnquiriesController(db, User().Object, Mock.Of<IEhcTicketService>(), Mock.Of<IEstateSalesListingApplicationHandoffService>());

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
    public async Task EstateHandoffRequiresClosedWonOpportunity()
    {
        await using var db = Database();
        var asset = Asset();
        var partner = new BusinessPartner { TenantId = tenantId, PartnerCode = "CUS-ESTATE", PartnerName = "Estate Customer", PartnerType = "Customer", IsActive = true, ApprovalStatus = "Approved" };
        var opportunity = new Opportunity { TenantId = tenantId, Name = "Property enquiry", Stage = "Qualification", Amount = 1250000m, Currency = "GHS" };
        db.AddRange(asset, partner, opportunity); await db.SaveChangesAsync();
        var procedures = new Mock<IProcedureCaseService>();
        var service = new EstateSalesListingApplicationHandoffService(db, procedures.Object);

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
        var tables = new[] { typeof(EstateManagedAsset), typeof(BusinessPartner), typeof(Opportunity),
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
        var partner = new BusinessPartner { TenantId = tenantId, PartnerCode = "CUS-ESTATE", PartnerName = "Estate Customer", PartnerType = "Customer", IsActive = true, ApprovalStatus = "Approved" };
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
        var service = new EstateSalesListingApplicationHandoffService(db, procedures.Object);

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
        Assert.Equal("Reserved", reserved.ExternalListingStatus);
    }

    [Fact]
    public async Task ListingApplicationUsesManualStagesWhenNoPublishedWorkflowExists()
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

        var created = await procedures.CreateCaseAsync(new CreateProcedureCaseRequest(
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
            }));

        Assert.False(created.UsesConfiguredWorkflow);
        Assert.Null(created.WorkflowInstanceId);
        Assert.Equal("Estate intake review", created.CurrentStageName);
        Assert.True(created.CanEditCurrentStage);
        Assert.Contains("customerValidationStatus", created.CurrentStageFieldKeys);
        var manualStages = new PropertyManagementProcedureCatalogService()
            .GetProcedureWorkspace("EstatePropertyManagementListingApplication")!.Stages;
        Assert.Equal(Enumerable.Range(0, manualStages.Count), await db.ProcedureCaseChecklistItems
            .Select(item => item.StageIndex)
            .Distinct()
            .OrderBy(item => item)
            .ToArrayAsync());
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

        var controller = new EhcPropertyEnquiriesController(db, User().Object, tickets.Object, handoffs.Object);
        var result = await controller.CreateEstateHandoff(ticket.Id, new("AGR-001", 1250000m,
            RequestedLeaseTerm: null, SalesAmountPaid: 1250000m, SalesPaymentReference: "RCT-FULL",
            Currency: "GHS", SalesCompletedAt: opportunity.ActualCloseDate, Notes: null), default);

        Assert.IsType<OkObjectResult>(result);
        tickets.Verify(item => item.TransitionTicketAsync(ticket.Id, EhcTicketStatus.InProgress, It.IsAny<string>(), null, null, It.IsAny<CancellationToken>()), Times.Once);
        tickets.Verify(item => item.TransitionTicketAsync(ticket.Id, EhcTicketStatus.Resolved, It.Is<string>(note => note.Contains("ESTATE-001")), null, null, It.IsAny<CancellationToken>()), Times.Once);
        var saved = await db.EhcTickets.SingleAsync(item => item.Id == ticket.Id);
        Assert.Equal(estateCaseId, saved.EstateListingApplicationCaseId);
        Assert.Equal("ESTATE-001", saved.EstateListingApplicationReference);
    }
}
