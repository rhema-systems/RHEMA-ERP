using System.Text.Json;
using ErpSystem.Api.Controllers.Ehc;
using ErpSystem.Api.Controllers.Estate;
using ErpSystem.Api.Controllers.Legal;
using ErpSystem.Api.Services;
using ErpSystem.Api.Services.DocumentManagement;
using ErpSystem.Api.Services.Estate;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Ehc;
using ErpSystem.Core.Entities.Estate;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.Procedures;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Ehc;
using ErpSystem.Core.Interfaces.Estate;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Core.Interfaces.Legal;
using ErpSystem.Core.Interfaces.Maintenance;
using ErpSystem.Core.Interfaces.Planning;
using ErpSystem.Core.Interfaces.Workflow;
using ErpSystem.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Controllers;

public sealed class GlobalSearchEstateLegalTests
{
    private readonly Guid tenantId = Guid.NewGuid();
    private readonly Guid userId = Guid.NewGuid();
    private ApplicationDbContext Database() => new(new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private ICurrentUserService User()
    {
        var user = new Mock<ICurrentUserService>();
        user.SetupGet(value => value.TenantId).Returns(tenantId);
        user.SetupGet(value => value.UserId).Returns(userId.ToString());
        user.SetupGet(value => value.Roles).Returns(Array.Empty<string>());
        return user.Object;
    }
    private static JsonElement Rows(IActionResult result) => JsonSerializer.SerializeToElement(
        Assert.IsType<OkObjectResult>(result).Value,
        new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }).GetProperty("data");

    [Fact]
    public async Task LegalSearchFiltersTenantModuleAndVisibilityBeforeLimitIncludingLaterBatches()
    {
        await using var db = Database();
        ProcedureCase Case(Guid tenant, Guid owner, DateTime created, bool deleted = false, string module = "Legal") => new()
        {
            TenantId = tenant, OpenedById = owner, Title = "Boundary dispute", ReferenceNumber = "CASE-42",
            Module = module, EntityType = "LegalCourtProcess", CreatedAt = created, IsDeleted = deleted,
        };
        var now = DateTime.UtcNow;
        db.AddRange(Enumerable.Range(0, 101).Select(index => Case(tenantId, Guid.NewGuid(), now.AddMinutes(-index))));
        var visible = Case(tenantId, userId, now.AddDays(-1));
        var older = Case(tenantId, userId, now.AddDays(-2));
        db.AddRange(visible, older, Case(Guid.NewGuid(), userId, now), Case(tenantId, userId, now, true), Case(tenantId, userId, now, module: "Estate"));
        // Preserve historical fixture timestamps; the convenience override stamps new audit dates.
        await db.SaveChangesAsync(acceptAllChangesOnSuccess: true, CancellationToken.None);
        var service = new ProcedureCaseService(db, User(), Mock.Of<ILegalProcedureCatalogService>(),
            Mock.Of<IEstateProcedureCatalogService>(), Mock.Of<IFacilitiesProcedureCatalogService>(),
            Mock.Of<IPropertyManagementProcedureCatalogService>(), Mock.Of<IPlanningProcedureCatalogService>(),
            Mock.Of<IWorkflowEngine>(), Mock.Of<INotificationService>(), Mock.Of<IFileStorageService>(),
            Mock.Of<IInvoiceService>(), Mock.Of<ICentralDocumentPdfSigningService>(), Mock.Of<IJobCardService>(),
            Mock.Of<IEhcTicketService>());

        var results = await service.SearchCasesAsync("Legal", " BOUNDARY ", 1);
        Assert.Equal(visible.Id, Assert.Single(results).Id);
        Assert.Empty(await service.SearchCasesAsync("Legal", "not present", 5));
        Assert.Empty(await service.SearchCasesAsync("Legal", "x", 5));
        Assert.False(db.ChangeTracker.HasChanges());
    }

    [Fact]
    public async Task AcquisitionSearchUsesBoardVisibilityWithoutSynchronizingOrChangingRecords()
    {
        await using var db = Database();
        LandAcquisition Acquisition(Guid tenant, Guid creator, DateTime created, bool deleted = false) => new()
        {
            TenantId = tenant, CreatedById = creator, ProjectReference = "ACQ-42", Location = "Tema",
            CreatedAt = created, StageOrder = 0, IsDeleted = deleted,
        };
        var now = DateTime.UtcNow;
        db.AddRange(Enumerable.Range(0, 101).Select(index => Acquisition(tenantId, Guid.NewGuid(), now.AddMinutes(-index))));
        var visible = Acquisition(tenantId, userId, now.AddDays(-1));
        db.AddRange(visible, Acquisition(Guid.NewGuid(), userId, now), Acquisition(tenantId, userId, now, true));
        // Keep the visible record behind the full first page of inaccessible records.
        await db.SaveChangesAsync(acceptAllChangesOnSuccess: true, CancellationToken.None);
        var invoices = new Mock<IVendorInvoiceService>(MockBehavior.Strict);
        var controller = new LandAcquisitionsController(db, User(), Mock.Of<IWorkflowIntegrationService>(),
            Mock.Of<IWorkflowEngine>(), Mock.Of<IWorkflowService>(), Mock.Of<IEstateManagedAssetService>(),
            invoices.Object, Mock.Of<IFileStorageService>(), Mock.Of<ICentralDocumentRenditionService>(),
            NullLogger<LandAcquisitionsController>.Instance);

        var rows = Rows(await controller.SearchAcquisitions(" TEMA ", 1));
        Assert.Equal(visible.Id, Assert.Single(rows.EnumerateArray()).GetProperty("id").GetGuid());
        Assert.Empty(invoices.Invocations);
        Assert.False(db.ChangeTracker.HasChanges());
        Assert.Empty(Rows(await controller.SearchAcquisitions("x")).EnumerateArray());
    }

    [Fact]
    public async Task PropertyEnquirySearchKeepsTenantTypeAndSalesOrganizationScope()
    {
        await using var db = Database();
        var structure = new OrganizationStructure { TenantId = tenantId, Name = "Structure", Code = "ORG", IsActive = true };
        var level = new OrganizationLevel { TenantId = tenantId, OrganizationStructure = structure, Name = "Department", Code = "DEPT", LevelNumber = 1, IsActive = true };
        var sales = new OrganizationUnit { TenantId = tenantId, OrganizationLevel = level, Name = "Sales", Code = "DEPT-SALES", Path = "/SALES", IsActive = true };
        var other = new OrganizationUnit { TenantId = tenantId, OrganizationLevel = level, Name = "Other", Code = "OTHER", Path = "/OTHER", IsActive = true };
        EhcTicket Ticket(Guid tenant, OrganizationUnit unit, EhcTicketType type = EhcTicketType.Enquiry, bool deleted = false) => new()
        {
            TenantId = tenant, TicketNumber = "PE-42", Subject = "Tema plot", Description = "Property enquiry",
            TicketType = type, PropertyListingContextJson = "{}", AssignedOrganizationUnit = unit,
            AssignedOrganizationUnitId = unit.Id, RequesterUserId = userId, IsDeleted = deleted,
        };
        var visible = Ticket(tenantId, sales);
        db.AddRange(structure, level, sales, other, visible, Ticket(Guid.NewGuid(), sales),
            Ticket(tenantId, other), Ticket(tenantId, sales, EhcTicketType.Helpdesk), Ticket(tenantId, sales, deleted: true));
        await db.SaveChangesAsync();
        var controller = new EhcPropertyEnquiriesController(db, User(), Mock.Of<IEhcTicketService>(), Mock.Of<IEstateSalesListingApplicationHandoffService>());

        var rows = Rows(await controller.Search("TEMA", 5));
        Assert.Equal(visible.Id, Assert.Single(rows.EnumerateArray()).GetProperty("id").GetGuid());
        Assert.Empty(Rows(await controller.Search("unknown", 5)).EnumerateArray());
        Assert.False(db.ChangeTracker.HasChanges());
    }

    [Fact]
    public void LegalProcedureSearchFiltersAndCapsCatalogResults()
    {
        using var db = Database();
        var catalog = new Mock<ILegalProcedureCatalogService>();
        catalog.Setup(value => value.GetProcedures()).Returns(Enumerable.Range(0, 20)
            .Select(index => new LegalProcedureCatalogItem($"Court {index}", $"LegalCourt{index}", "Legal", 1, "")).ToList());
        var controller = new LegalProceduresController(catalog.Object, db, User());
        Assert.Equal(10, Rows(controller.SearchProcedures("COURT", 500)).GetArrayLength());
        Assert.Empty(Rows(controller.SearchProcedures("x")).EnumerateArray());
        Assert.Empty(Rows(controller.SearchProcedures("mortgage")).EnumerateArray());
    }

    [Theory]
    [InlineData("enquiry-internal", EhcTicketSource.Internal)]
    [InlineData("enquiry-external", EhcTicketSource.Web)]
    public async Task EnquirySearchFiltersTypeSourceAndTenantBeforeLimit(string scope, EhcTicketSource source)
    {
        await using var db = Database();
        EhcTicket Ticket(Guid tenant, EhcTicketType type, EhcTicketSource channel, DateTime created, bool deleted = false) => new()
        {
            TenantId = tenant, TicketNumber = "ENQ-42", Subject = "Boundary enquiry", Description = "Boundary",
            TicketType = type, Source = channel, CreatedAt = created, RequesterUserId = userId, IsDeleted = deleted,
        };
        var now = DateTime.UtcNow;
        var visible = Ticket(tenantId, EhcTicketType.Enquiry, source, now.AddDays(-1));
        var otherSource = source == EhcTicketSource.Internal ? EhcTicketSource.Web : EhcTicketSource.Internal;
        db.AddRange(visible, Ticket(tenantId, EhcTicketType.Enquiry, otherSource, now),
            Ticket(tenantId, EhcTicketType.Helpdesk, source, now),
            Ticket(Guid.NewGuid(), EhcTicketType.Enquiry, source, now),
            Ticket(tenantId, EhcTicketType.Enquiry, source, now, deleted: true));
        await db.SaveChangesAsync();
        // This lookup does not use the user-manager dependency.
        var controller = new EhcInternalLookupsController(db, null!, User());

        var rows = Rows(await controller.SearchTickets("BOUNDARY", 1, scope: scope));
        Assert.Equal(visible.Id, Assert.Single(rows.EnumerateArray()).GetProperty("id").GetGuid());
        Assert.Empty(Rows(await controller.SearchTickets("unmatched", 5, scope: scope)).EnumerateArray());
        Assert.IsType<BadRequestObjectResult>(await controller.SearchTickets("Boundary", 5, scope: "unknown"));
        Assert.False(db.ChangeTracker.HasChanges());
    }
}
