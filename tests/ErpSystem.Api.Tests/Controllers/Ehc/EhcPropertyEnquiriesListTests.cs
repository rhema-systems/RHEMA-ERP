using System.Text.Json;
using ErpSystem.Api.Controllers.Ehc;
using ErpSystem.Core.Entities.Ehc;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Controllers.Ehc;

public sealed class EhcPropertyEnquiriesListTests
{
    [Fact]
    public async Task List_filters_before_pagination_and_keeps_tenant_and_sales_scope()
    {
        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        await using var db = Database();
        var sales = Unit(tenantId, "DEPT-SALES");
        var helpdesk = Unit(tenantId, "DEPT-HELPDESK");
        var contact = new EhcPublicPropertyEnquiryContact
        {
            TenantId = tenantId,
            Channel = "Email",
            NormalizedContact = "ama@example.test",
            ContactName = "Ama Mensah"
        };
        db.AddRange(sales, helpdesk, contact);
        db.AddRange(
            Ticket(tenantId, sales, "PE-001", "Apartment viewing", EhcTicketStatus.Acknowledged,
                new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc), contact: contact),
            Ticket(tenantId, sales, "PE-002", "Apartment viewing", EhcTicketStatus.Acknowledged,
                new DateTime(2026, 10, 2, 0, 0, 0, DateTimeKind.Utc), crmLinked: true),
            Ticket(tenantId, sales, "PE-003", "Shop viewing", EhcTicketStatus.Closed,
                new DateTime(2026, 10, 3, 0, 0, 0, DateTimeKind.Utc)),
            Ticket(tenantId, helpdesk, "PE-004", "Apartment viewing", EhcTicketStatus.Acknowledged,
                new DateTime(2026, 10, 3, 0, 0, 0, DateTimeKind.Utc)),
            Ticket(otherTenantId, sales, "PE-005", "Apartment viewing", EhcTicketStatus.Acknowledged,
                new DateTime(2026, 10, 3, 0, 0, 0, DateTimeKind.Utc)));
        await db.SaveChangesAsync();
        var controller = Controller(db, tenantId);

        var response = await controller.List(
            search: "Ama", status: EhcTicketStatus.Acknowledged, crmLinked: false,
            cancellationToken: CancellationToken.None);
        using var result = Response(response);
        Assert.Equal(1, result.RootElement.GetProperty("totalCount").GetInt32());
        Assert.Equal("PE-001", result.RootElement.GetProperty("data")[0]
            .GetProperty("ticketNumber").GetString());
        Assert.Equal("Ama Mensah", result.RootElement.GetProperty("data")[0]
            .GetProperty("requesterName").GetString());

        response = await controller.List(
            search: "Apartment", status: EhcTicketStatus.Acknowledged, crmLinked: true,
            createdFrom: new DateTime(2026, 10, 2, 0, 0, 0, DateTimeKind.Utc),
            cancellationToken: CancellationToken.None);
        using var linked = Response(response);
        Assert.Equal(1, linked.RootElement.GetProperty("totalCount").GetInt32());
        Assert.Equal("PE-002", linked.RootElement.GetProperty("data")[0]
            .GetProperty("ticketNumber").GetString());
    }

    [Fact]
    public async Task List_caps_page_size_and_rejects_oversized_search()
    {
        var tenantId = Guid.NewGuid();
        await using var db = Database();
        var sales = Unit(tenantId, "DEPT-SALES");
        db.Add(sales);
        for (var index = 1; index <= 12; index++)
            db.Add(Ticket(tenantId, sales, $"PE-{index:000}", "Plot",
                EhcTicketStatus.Acknowledged,
                new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc).AddMinutes(index)));
        await db.SaveChangesAsync();
        var controller = Controller(db, tenantId);

        var response = await controller.List(page: 2, pageSize: 1,
            cancellationToken: CancellationToken.None);
        using var result = Response(response);
        Assert.Equal(10, result.RootElement.GetProperty("pageSize").GetInt32());
        Assert.Equal(12, result.RootElement.GetProperty("totalCount").GetInt32());
        Assert.Equal(2, result.RootElement.GetProperty("data").GetArrayLength());
        Assert.Equal("PE-002", result.RootElement.GetProperty("data")[0]
            .GetProperty("ticketNumber").GetString());

        Assert.IsType<BadRequestObjectResult>(await controller.List(
            search: new string('x', 101), cancellationToken: CancellationToken.None));
    }

    private static ApplicationDbContext Database() => new(
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static JsonDocument Response(IActionResult action) => JsonDocument.Parse(
        JsonSerializer.Serialize(Assert.IsType<OkObjectResult>(action).Value,
            new JsonSerializerOptions(JsonSerializerDefaults.Web)));

    private static EhcPropertyEnquiriesController Controller(ApplicationDbContext db, Guid tenantId)
    {
        var user = new Mock<ICurrentUserService>();
        user.SetupGet(value => value.TenantId).Returns(tenantId);
        return new EhcPropertyEnquiriesController(db, user.Object, null!, null!, null!);
    }

    private static OrganizationUnit Unit(Guid tenantId, string code)
    {
        var structure = new OrganizationStructure
        {
            TenantId = tenantId, Name = "ERP", Code = "ERP", IsActive = true
        };
        var level = new OrganizationLevel
        {
            TenantId = tenantId, OrganizationStructure = structure,
            StructureId = structure.Id, Name = "Department", Code = "DEPT",
            LevelNumber = 3, IsActive = true
        };
        return new OrganizationUnit
        {
            TenantId = tenantId, OrganizationLevel = level, OrganizationLevelId = level.Id,
            Name = code, Code = code, Path = $"/ERP/{code}", IsActive = true
        };
    }

    private static EhcTicket Ticket(
        Guid tenantId,
        OrganizationUnit unit,
        string number,
        string subject,
        EhcTicketStatus status,
        DateTime createdAt,
        bool crmLinked = false,
        EhcPublicPropertyEnquiryContact? contact = null) => new()
    {
        TenantId = tenantId,
        TicketNumber = number,
        TicketType = EhcTicketType.Enquiry,
        Status = status,
        Subject = subject,
        Description = subject,
        CreatedAt = createdAt,
        AssignedOrganizationUnit = unit,
        AssignedOrganizationUnitId = unit.Id,
        PropertyListingContextJson = "{}",
        CrmLeadId = crmLinked ? Guid.NewGuid() : null,
        PublicPropertyEnquiryContact = contact,
        PublicPropertyEnquiryContactId = contact?.Id
    };
}
