using ErpSystem.Api.Services.Estate;
using ErpSystem.Core.DTOs.Ehc;
using ErpSystem.Core.DTOs.Procedures;
using ErpSystem.Core.Entities.Ehc;
using ErpSystem.Core.Entities.Procedures;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces.Ehc;
using ErpSystem.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Estate;

public sealed class FacilitiesComplaintHandoffServiceTests
{
    [Fact]
    public async Task HandoffRequiresComplaintCategoryBeforeCreatingTicket()
    {
        var tenantId = Guid.NewGuid();
        await using var db = Database(tenantId);
        var procedureCase = ComplaintCase(tenantId, "Complaint Resolution Review");
        db.ProcedureCases.Add(procedureCase);
        await db.SaveChangesAsync();
        var tickets = new Mock<IEhcTicketService>();
        var service = new FacilitiesComplaintHandoffService(db, tickets.Object);

        var action = () => service.EnsureTicketForHandoffAsync(procedureCase, Guid.NewGuid(), DateTime.UtcNow);

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Configure a Helpdesk complaint category*");
        tickets.Verify(item => item.CreateInternalTicketAsync(
            It.IsAny<CreateEhcTicketRequestDto>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandoffCreatesOneLinkedTicketAndReusesItOnRetry()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var ticketId = Guid.NewGuid();
        await using var db = Database(tenantId);
        var procedureCase = ComplaintCase(tenantId, "Complaint Resolution Review");
        var category = new EhcTicketCategory
        {
            Id = Guid.NewGuid(), TenantId = tenantId, Code = "COMPLAINTS",
            Name = "Complaints", AppliesToType = EhcTicketType.Complaint
        };
        db.ProcedureCases.Add(procedureCase);
        db.EhcTicketCategories.Add(category);
        await db.SaveChangesAsync();
        var tickets = new Mock<IEhcTicketService>();
        tickets.Setup(item => item.CreateInternalTicketAsync(
                It.IsAny<CreateEhcTicketRequestDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EhcTicketDetailDto { Id = ticketId, TicketNumber = "EHC-001" });
        var service = new FacilitiesComplaintHandoffService(db, tickets.Object);

        await service.EnsureTicketForHandoffAsync(procedureCase, userId, DateTime.UtcNow);

        var fields = await db.ProcedureCaseFields.Where(item => item.ProcedureCaseId == procedureCase.Id).ToListAsync();
        fields.Should().ContainSingle(item => item.Key == "helpdeskTicketId" && item.Value == ticketId.ToString());
        fields.Should().ContainSingle(item => item.Key == "helpdeskTicketReference" && item.Value == "EHC-001");
        db.EhcTickets.Add(new EhcTicket
        {
            Id = ticketId, TenantId = tenantId, TicketNumber = "EHC-001", TicketType = EhcTicketType.Complaint,
            CategoryId = category.Id, RequesterUserId = userId, Description = "Complaint",
            RelatedEntityType = "ProcedureCase", RelatedEntityReference = procedureCase.Id.ToString()
        });
        await db.SaveChangesAsync();
        var reloaded = await db.ProcedureCases.Include(item => item.Fields).Include(item => item.Activities)
            .SingleAsync(item => item.Id == procedureCase.Id);

        await service.EnsureTicketForHandoffAsync(reloaded, userId, DateTime.UtcNow);

        tickets.Verify(item => item.CreateInternalTicketAsync(
            It.Is<CreateEhcTicketRequestDto>(request =>
                request.TicketType == EhcTicketType.Complaint
                && request.Priority == EhcTicketPriority.Critical
                && request.CategoryId == category.Id
                && request.RelatedEntityReference == procedureCase.Id.ToString()),
            It.IsAny<CancellationToken>()), Times.Once);
        (await db.ProcedureCaseActivities.CountAsync(item => item.ProcedureCaseId == procedureCase.Id
            && item.Action == "Helpdesk ticket linked")).Should().Be(1);
    }

    [Fact]
    public async Task CloseoutRequiresResolvedOrClosedHelpdeskTicket()
    {
        var tenantId = Guid.NewGuid();
        await using var db = Database(tenantId);
        var procedureCase = ComplaintCase(tenantId, "Complaint Closeout");
        var ticket = new EhcTicket
        {
            Id = Guid.NewGuid(), TenantId = tenantId, TicketNumber = "EHC-002", TicketType = EhcTicketType.Complaint,
            RequesterUserId = Guid.NewGuid(), Description = "Complaint", Status = EhcTicketStatus.InProgress,
            RelatedEntityType = "ProcedureCase", RelatedEntityReference = procedureCase.Id.ToString()
        };
        db.ProcedureCases.Add(procedureCase);
        db.EhcTickets.Add(ticket);
        await db.SaveChangesAsync();
        var service = new FacilitiesComplaintHandoffService(db, Mock.Of<IEhcTicketService>());

        var action = () => service.EnsureCloseoutReadyAsync(procedureCase);
        await action.Should().ThrowAsync<InvalidOperationException>().WithMessage("*still InProgress*");

        ticket.Status = EhcTicketStatus.Resolved;
        await db.SaveChangesAsync();
        await action.Should().ThrowAsync<InvalidOperationException>().WithMessage("*requester feedback outcome*");

        db.ProcedureCaseFields.Add(new ProcedureCaseField
        {
            Id = Guid.NewGuid(), TenantId = tenantId, ProcedureCaseId = procedureCase.Id,
            Key = "requesterFeedbackStatus", Label = "Requester feedback status", Value = "Satisfied"
        });
        await db.SaveChangesAsync();
        await action.Should().ThrowAsync<InvalidOperationException>().WithMessage("*closeout notes*");

        db.ProcedureCaseFields.Add(new ProcedureCaseField
        {
            Id = Guid.NewGuid(), TenantId = tenantId, ProcedureCaseId = procedureCase.Id,
            Key = "closureNotes", Label = "Closeout notes", Value = "Confirmed with requester"
        });
        await db.SaveChangesAsync();
        await service.EnsureCloseoutReadyAsync(procedureCase);

        var displayFields = new List<ProcedureCaseFieldDto>();
        await service.AddTicketStatusFieldsAsync(procedureCase, displayFields);
        displayFields.Should().Contain(item => item.Key == "helpdeskResolutionComplete" && item.Value == "true");
        displayFields.Should().Contain(item => item.Key == "helpdeskTicketStatus" && item.Value == "Resolved");
    }

    [Fact]
    public async Task CloseoutDoesNotAcceptAnotherCasesTicketReference()
    {
        var tenantId = Guid.NewGuid();
        await using var db = Database(tenantId);
        var procedureCase = ComplaintCase(tenantId, "Complaint Closeout");
        procedureCase.Fields.Add(new ProcedureCaseField
        {
            TenantId = tenantId, Key = "helpdeskTicketReference", Label = "Helpdesk ticket reference", Value = "EHC-OTHER"
        });
        db.ProcedureCases.Add(procedureCase);
        db.EhcTickets.Add(new EhcTicket
        {
            Id = Guid.NewGuid(), TenantId = tenantId, TicketNumber = "EHC-OTHER", TicketType = EhcTicketType.Complaint,
            RequesterUserId = Guid.NewGuid(), Description = "Different complaint", Status = EhcTicketStatus.Resolved,
            RelatedEntityType = "ProcedureCase", RelatedEntityReference = Guid.NewGuid().ToString()
        });
        await db.SaveChangesAsync();
        var service = new FacilitiesComplaintHandoffService(db, Mock.Of<IEhcTicketService>());

        var action = () => service.EnsureCloseoutReadyAsync(procedureCase);

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*requires a linked Helpdesk ticket*");
    }

    private static ApplicationDbContext Database(Guid tenantId) => new(
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options,
        tenantId);

    private static ProcedureCase ComplaintCase(Guid tenantId, string stage) => new()
    {
        Id = Guid.NewGuid(), TenantId = tenantId, Module = "Facilities",
        EntityType = "EstateFacilityComplaint", Title = "Water service complaint",
        ReferenceNumber = "FAC-C-001", CurrentStageName = stage,
        Fields =
        [
            new ProcedureCaseField { TenantId = tenantId, Key = "priority", Label = "Priority", Value = "Urgent" },
            new ProcedureCaseField { TenantId = tenantId, Key = "propertyUnit", Label = "Property / unit", Value = "Block A" },
            new ProcedureCaseField { TenantId = tenantId, Key = "complaintDescription", Label = "Complaint", Value = "No water" }
        ]
    };
}
