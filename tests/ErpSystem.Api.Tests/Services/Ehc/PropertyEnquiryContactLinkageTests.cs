using System.Security.Claims;
using System.Text.Json;
using ErpSystem.Api.Controllers.Crm;
using ErpSystem.Api.Services.Ehc;
using ErpSystem.Core.DTOs.Crm;
using ErpSystem.Core.DTOs.Ehc;
using ErpSystem.Core.Entities.Ehc;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.Sales;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Crm;
using ErpSystem.Core.Interfaces.Ehc;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.Interfaces.Sales;
using ErpSystem.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Ehc;

public sealed class PropertyEnquiryContactLinkageTests
{
    [Fact]
    public async Task Crm_activity_links_the_selected_enquiry_but_only_completed_contacts_advance_it()
    {
        var tenantId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        await using var db = new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);
        var salesUnit = new OrganizationUnit
        {
            TenantId = tenantId,
            OrganizationLevelId = Guid.NewGuid(),
            Name = "Sales",
            Code = "DEPT-SALES",
            Path = "/SALES",
            IsActive = true
        };
        var selectedTicket = Ticket(tenantId, salesUnit, "PE-CRM-001", "LIST-CRM-001");
        db.AddRange(salesUnit, selectedTicket);
        await db.SaveChangesAsync();

        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(user => user.TenantId).Returns(tenantId);
        currentUser.SetupGet(user => user.UserId).Returns(actorId.ToString());
        currentUser.SetupGet(user => user.UserName).Returns("sales.officer");
        var crm = new Mock<ICrmService>();
        var completedActivityId = Guid.NewGuid();
        var plannedActivityId = Guid.NewGuid();
        crm.SetupSequence(service => service.CreateActivityAsync(It.IsAny<CreateCrmActivityDto>()))
            .ReturnsAsync(new CrmActivityDetailDto
            {
                ActivityId = completedActivityId,
                Subject = "Completed sales call",
                ActivityType = "Call",
                ActivityStatus = "Completed"
            })
            .ReturnsAsync(new CrmActivityDetailDto
            {
                ActivityId = plannedActivityId,
                Subject = "Planned follow-up",
                ActivityType = "Call",
                ActivityStatus = "Planned"
            });
        var propertyProspects = new Mock<IPropertyEnquiryProspectService>();
        propertyProspects.Setup(service => service.GetAsync(selectedTicket.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PropertyEnquiryProspectDto?)null);
        propertyProspects.Setup(service => service.RecordContactAsync(
                selectedTicket.Id,
                It.IsAny<RecordPropertyEnquiryContactRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PropertyEnquiryProspectDto(
                selectedTicket.Id,
                Guid.NewGuid(),
                null,
                null,
                null,
                null,
                null,
                EhcPropertyProspectStatuses.Contacted,
                0m,
                "GHS",
                ProspectDepositRequirementTypes.Full,
                0m,
                0m,
                true,
                null,
                null));
        var controller = new CrmController(crm.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        [new Claim(ClaimTypes.Role, "Sales Officer")],
                        "Test"))
                }
            }
        };

        var completedResult = await controller.CreateActivity(
            new CreateCrmActivityDto
            {
                Subject = "Completed sales call",
                ActivityType = "Call",
                ActivityStatus = "Completed",
                ActivityDate = DateTime.UtcNow,
                PropertyEnquiryTicketId = selectedTicket.Id
            },
            db,
            currentUser.Object,
            propertyProspects.Object,
            CancellationToken.None);
        var completed = Assert.IsType<CreatedAtActionResult>(completedResult.Result);
        var completedDto = Assert.IsType<CrmActivityDetailDto>(completed.Value);
        Assert.Equal(selectedTicket.Id, completedDto.PropertyEnquiryTicketId);
        Assert.Equal(selectedTicket.TicketNumber, completedDto.PropertyEnquiryTicketNumber);

        await controller.CreateActivity(
            new CreateCrmActivityDto
            {
                Subject = "Planned follow-up",
                ActivityType = "Call",
                ActivityStatus = "Planned",
                ActivityDate = DateTime.UtcNow.AddDays(1),
                PropertyEnquiryTicketId = selectedTicket.Id
            },
            db,
            currentUser.Object,
            propertyProspects.Object,
            CancellationToken.None);

        propertyProspects.Verify(service => service.RecordContactAsync(
            selectedTicket.Id,
            It.Is<RecordPropertyEnquiryContactRequest>(request =>
                request.Notes != null && request.Notes.Contains("Completed sales call")),
            It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal(2, await db.EhcCrmEngagementLinks.CountAsync(
            link => link.TicketId == selectedTicket.Id));
        Assert.Contains(await db.EhcCrmEngagementLinks.ToListAsync(),
            link => link.CrmActivityId == completedActivityId
                && link.EngagementType == "SalesContact");
        Assert.Contains(await db.EhcCrmEngagementLinks.ToListAsync(),
            link => link.CrmActivityId == plannedActivityId
                && link.EngagementType == "Call");
    }

    [Fact]
    public async Task Contact_is_scoped_to_its_enquiry_and_only_that_enquiry_can_then_be_qualified()
    {
        var tenantId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        await using var db = new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options);

        var salesUnit = new OrganizationUnit
        {
            TenantId = tenantId,
            OrganizationLevelId = Guid.NewGuid(),
            Name = "Sales",
            Code = "DEPT-SALES",
            Path = "/SALES",
            IsActive = true
        };
        var contactedTicket = Ticket(tenantId, salesUnit, "PE-CONTACT-001", "LIST-001");
        var otherTicket = Ticket(tenantId, salesUnit, "PE-CONTACT-002", "LIST-002");
        db.AddRange(
            salesUnit,
            contactedTicket,
            otherTicket,
            new SalesSaleableSource
            {
                TenantId = tenantId,
                Code = "LAND",
                DisplayName = "Land Management",
                SourceType = "LandManagement",
                AdapterKey = "land-management",
                DefaultCurrency = "GHS",
                AllowSalesOrders = true,
                IsActive = true
            });
        await db.SaveChangesAsync();

        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(user => user.TenantId).Returns(tenantId);
        currentUser.SetupGet(user => user.UserId).Returns(actorId.ToString());
        currentUser.SetupGet(user => user.UserName).Returns("sales.officer");
        var service = new PropertyEnquiryProspectService(
            db,
            currentUser.Object,
            Mock.Of<IBusinessPartnerService>(),
            Mock.Of<IOpportunityService>(),
            Mock.Of<ISalesAllocationService>(),
            Mock.Of<IProspectDepositFinancePostingService>(),
            Mock.Of<INotificationService>(),
            Mock.Of<IEhcTicketService>(),
            NullLogger<PropertyEnquiryProspectService>.Instance);

        var contacted = await service.RecordContactAsync(
            contactedTicket.Id,
            new RecordPropertyEnquiryContactRequest
            {
                Notes = "Called the enquirer and confirmed interest."
            });

        Assert.Equal(contactedTicket.Id, contacted.TicketId);
        Assert.Equal(EhcPropertyProspectStatuses.Contacted, contacted.Status);
        var contactAudit = await db.EhcTicketAuditEvents.AsNoTracking()
            .SingleAsync(audit => audit.EventType == "ProspectContacted");
        Assert.Equal(contactedTicket.Id, contactAudit.TicketId);

        var qualified = await service.QualifyAsync(
            contactedTicket.Id,
            new QualifyPropertyEnquiryRequest
            {
                AgreedAmount = 250000m,
                Currency = "GHS",
                QualificationScore = 70
            });

        Assert.Equal(contactedTicket.Id, qualified.TicketId);
        Assert.Equal(EhcPropertyProspectStatuses.Qualified, qualified.Status);

        var otherError = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.QualifyAsync(
                otherTicket.Id,
                new QualifyPropertyEnquiryRequest
                {
                    AgreedAmount = 175000m,
                    Currency = "GHS",
                    QualificationScore = 60
                }));
        Assert.Equal("Record Sales contact before qualifying this prospect.", otherError.Message);
        Assert.DoesNotContain(
            await db.EhcTicketAuditEvents.AsNoTracking().ToListAsync(),
            audit => audit.TicketId == otherTicket.Id
                && (audit.EventType == "ProspectContacted" || audit.EventType == "ProspectQualified"));
    }

    private static EhcTicket Ticket(
        Guid tenantId,
        OrganizationUnit salesUnit,
        string ticketNumber,
        string listingReference) =>
        new()
        {
            TenantId = tenantId,
            TicketNumber = ticketNumber,
            Subject = $"Enquiry {listingReference}",
            Description = "Public property enquiry",
            TicketType = EhcTicketType.Enquiry,
            Status = EhcTicketStatus.Acknowledged,
            AssignedOrganizationUnitId = salesUnit.Id,
            AssignedOrganizationUnit = salesUnit,
            PropertyListingContextJson = JsonSerializer.Serialize(
                new EhcPropertyListingContextDto(
                    "estate-public-listing",
                    Guid.NewGuid(),
                    listingReference,
                    $"Property {listingReference}",
                    "Sale",
                    "GHS",
                    "Accra",
                    250000m,
                    Guid.NewGuid(),
                    null,
                    null,
                    "Ama Mensah",
                    "Ama Mensah",
                    "ama@example.test",
                    "+233245550101"))
        };
}
