using System.Text.Json;
using ErpSystem.Api.Services.Estate;
using ErpSystem.Core.DTOs.Ehc;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Ehc;
using ErpSystem.Core.Enums;
using ErpSystem.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Controllers.Ehc;

// Sales access is restricted to property enquiries. It grants no access to other helpdesk tickets.
[ApiController]
[Route("api/ehc/internal/property-enquiries")]
[Authorize(Policy = "InternalOnly")]
[Authorize(Roles = "Sales User,Marketing User,HelpdeskAgent,HelpdeskSupervisor,HelpdeskManager,TenantAdmin,SuperAdmin")]
public sealed class EhcPropertyEnquiriesController(ApplicationDbContext db, ICurrentUserService currentUser,
    IEhcTicketService tickets, IEstateSalesListingApplicationHandoffService estateHandoffs) : ControllerBase
{
    private IQueryable<ErpSystem.Core.Entities.Ehc.EhcTicket> Query() => db.EhcTickets.AsNoTracking()
        .Where(t => t.TenantId == currentUser.TenantId && !t.IsDeleted && t.TicketType == EhcTicketType.Enquiry
            && t.PropertyListingContextJson != null
            && t.AssignedDepartmentId != null
            && t.AssignedDepartment != null
            && !t.AssignedDepartment.IsDeleted
            && t.AssignedDepartment.IsActive
            && t.AssignedDepartment.DepartmentType == DepartmentType.Sales
            && t.Status != EhcTicketStatus.New);

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] int page = 1, CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        var total = await Query().CountAsync(cancellationToken);
        var items = await Query().OrderByDescending(t => t.CreatedAt).Skip((page - 1) * 25).Take(25)
            .Select(t => new { t.Id, t.TicketNumber, t.Subject, t.Status, t.CreatedAt, t.FirstRespondedAt,
                RequesterName = t.RequesterUser.FirstName + " " + t.RequesterUser.LastName }).ToArrayAsync(cancellationToken);
        return Ok(new { success = true, data = items, totalCount = total, page, pageSize = 25 });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        if (!await Query().AnyAsync(t => t.Id == id, cancellationToken)) return NotFound();
        return Ok(new { success = true, data = await tickets.GetTicketByIdAsync(id, cancellationToken) });
    }

    [HttpGet("{id:guid}/estate-handoff")]
    public async Task<IActionResult> GetEstateHandoff(Guid id, CancellationToken cancellationToken)
    {
        var ticket = await Query()
            .Where(item => item.Id == id)
            .Select(item => new
            {
                item.CrmOpportunityId,
                item.EstateListingApplicationCaseId,
                item.EstateListingApplicationReference,
                item.EstateListingApplicationHandedOffAt,
                Opportunity = item.CrmOpportunity == null ? null : new
                {
                    item.CrmOpportunity.Id,
                    item.CrmOpportunity.ReferenceNumber,
                    item.CrmOpportunity.Stage,
                    item.CrmOpportunity.Amount,
                    item.CrmOpportunity.Currency,
                    item.CrmOpportunity.ActualCloseDate
                }
            })
            .SingleOrDefaultAsync(cancellationToken);
        if (ticket is null) return NotFound();

        var estateCase = ticket.EstateListingApplicationCaseId is not { } estateCaseId || estateCaseId == Guid.Empty
            ? null
            : await db.ProcedureCases.AsNoTracking()
                .Where(item => item.Id == estateCaseId && item.TenantId == currentUser.TenantId && !item.IsDeleted)
                .Select(item => new { item.Id, item.ReferenceNumber, item.Title, item.Status, item.CurrentStageName, item.CreatedAt })
                .SingleOrDefaultAsync(cancellationToken);

        return Ok(new
        {
            success = true,
            data = new
            {
                ticket.CrmOpportunityId,
                opportunity = ticket.Opportunity,
                estateCase,
                estateHandoffReference = ticket.EstateListingApplicationReference,
                ticket.EstateListingApplicationHandedOffAt,
                canHandoff = ticket.CrmOpportunityId.HasValue
                    && ticket.Opportunity is not null
                    && string.Equals(ticket.Opportunity.Stage, "Closed Won", StringComparison.OrdinalIgnoreCase)
                    && estateCase is null
            }
        });
    }

    [HttpPost("{id:guid}/estate-handoff")]
    [Authorize(Roles = "Sales User,Sales Officer,Sales Manager,TenantAdmin,SuperAdmin")]
    public async Task<IActionResult> CreateEstateHandoff(
        Guid id,
        [FromBody] CreatePropertyEnquiryEstateHandoff request,
        CancellationToken cancellationToken)
    {
        if (!await Query().AnyAsync(item => item.Id == id, cancellationToken)) return NotFound();

        var ticket = await db.EhcTickets.FirstOrDefaultAsync(item => item.Id == id
            && item.TenantId == currentUser.TenantId
            && !item.IsDeleted, cancellationToken);
        if (ticket is null) return NotFound();

        if (ticket.EstateListingApplicationCaseId is { } existingCaseId && existingCaseId != Guid.Empty)
        {
            var existing = await db.ProcedureCases.AsNoTracking().FirstOrDefaultAsync(item => item.Id == existingCaseId
                && item.TenantId == currentUser.TenantId && !item.IsDeleted, cancellationToken);
            if (existing is not null)
            {
                await ResolveAfterEstateHandoffAsync(ticket, existing.ReferenceNumber ?? existing.Id.ToString(), cancellationToken);
                return Ok(new
                {
                    success = true,
                    data = new EstateSalesListingApplicationHandoffResult(existing.Id, existing.ReferenceNumber, existing.Title,
                        existing.Status, existing.CurrentStageName, existing.CreatedAt, AlreadyExists: true),
                    message = "This enquiry has already been handed to Estate."
                });
            }
        }

        if (!ticket.CrmOpportunityId.HasValue)
            return BadRequest(new { success = false, message = "This enquiry has not yet created a Sales CRM opportunity." });

        EhcPropertyListingContextDto? property;
        try
        {
            property = string.IsNullOrWhiteSpace(ticket.PropertyListingContextJson)
                ? null
                : JsonSerializer.Deserialize<EhcPropertyListingContextDto>(ticket.PropertyListingContextJson);
        }
        catch (JsonException)
        {
            return BadRequest(new { success = false, message = "The saved property listing context is invalid." });
        }

        if (property is null || !IsEstateListingSource(property.Source) || property.ListingId == Guid.Empty
            || property.BusinessPartnerId is not { } businessPartnerId || businessPartnerId == Guid.Empty)
            return BadRequest(new { success = false, message = "This enquiry does not contain a verified Estate listing and business partner context." });

        if (string.IsNullOrWhiteSpace(request.SalesReference) || request.SalesReference.Trim().Length > 200)
            return BadRequest(new { success = false, message = "Enter the completed Sales reference, up to 200 characters." });
        if (request.AgreedAmount is <= 0)
            return BadRequest(new { success = false, message = "Enter a positive agreed amount." });
        if (!string.IsNullOrWhiteSpace(request.Currency)
            && (request.Currency.Trim().Length != 3 || !request.Currency.Trim().All(char.IsLetter)))
            return BadRequest(new { success = false, message = "Currency must be a three-letter code." });
        if (request.Notes?.Trim().Length > 1000)
            return BadRequest(new { success = false, message = "Notes must be 1,000 characters or fewer." });

        try
        {
            var result = await estateHandoffs.CreateAsync(currentUser.TenantId ?? Guid.Empty, new(
                property.ListingId,
                businessPartnerId,
                property.ListingType,
                ticket.CrmOpportunityId.Value,
                request.SalesReference.Trim(),
                request.AgreedAmount,
                request.Currency,
                request.SalesCompletedAt,
                request.Notes,
                ticket.Id,
                ticket.TicketNumber), cancellationToken);

            ticket.EstateListingApplicationCaseId = result.ProcedureCaseId;
            ticket.EstateListingApplicationReference = result.ReferenceNumber;
            ticket.EstateListingApplicationHandedOffAt = DateTime.UtcNow;
            ticket.UpdatedAt = DateTime.UtcNow;
            var actorUserId = Guid.TryParse(currentUser.UserId, out var parsedActorUserId) ? parsedActorUserId : (Guid?)null;
            db.EhcTicketAuditEvents.Add(new()
            {
                TenantId = ticket.TenantId,
                TicketId = ticket.Id,
                EventType = "EstateHandoff",
                Title = "Sales handed the property enquiry to Estate",
                Body = $"Sales reference: {request.SalesReference.Trim()}. Estate application: {result.ReferenceNumber ?? result.ProcedureCaseId.ToString()}.",
                IsInternal = true,
                ActorUserId = actorUserId,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = currentUser.UserName,
                CreatedById = actorUserId
            });
            await db.SaveChangesAsync(cancellationToken);

            await tickets.AddInternalCommentAsync(ticket.Id, new()
            {
                Body = $"Sales completed CRM opportunity {ticket.CrmOpportunityId} and handed this enquiry to Estate. Estate application: {result.ReferenceNumber ?? result.ProcedureCaseId.ToString()}. Sales reference: {request.SalesReference.Trim()}."
            }, cancellationToken);

            await ResolveAfterEstateHandoffAsync(ticket, result.ReferenceNumber ?? result.ProcedureCaseId.ToString(), cancellationToken);

            return Ok(new
            {
                success = true,
                data = result,
                message = result.AlreadyExists
                    ? "The existing Estate listing application has been linked to this enquiry."
                    : "The completed Sales enquiry has been handed to Estate."
            });
        }
        catch (KeyNotFoundException ex) { return NotFound(new { success = false, message = ex.Message }); }
        catch (ArgumentException ex) { return BadRequest(new { success = false, message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { success = false, message = ex.Message }); }
    }

    private static bool IsEstateListingSource(string? source)
        => string.Equals(source, "estate-public-listing", StringComparison.OrdinalIgnoreCase)
            || string.Equals(source, "state-public-listing", StringComparison.OrdinalIgnoreCase);

    private async Task ResolveAfterEstateHandoffAsync(
        ErpSystem.Core.Entities.Ehc.EhcTicket ticket,
        string estateApplicationReference,
        CancellationToken cancellationToken)
    {
        if (ticket.Status is EhcTicketStatus.Resolved or EhcTicketStatus.Closed)
        {
            return;
        }

        const string handoffNotePrefix = "Estate listing application ";
        var handoffNote = $"{handoffNotePrefix}{estateApplicationReference} was created and accepted by Estate.";

        if (ticket.Status is EhcTicketStatus.Acknowledged or EhcTicketStatus.PendingUser or EhcTicketStatus.PendingThirdParty)
        {
            await tickets.TransitionTicketAsync(
                ticket.Id,
                EhcTicketStatus.InProgress,
                "Sales is completing the Estate handoff.",
                cancellationToken: cancellationToken);
        }

        if (ticket.Status is EhcTicketStatus.Acknowledged or EhcTicketStatus.InProgress
            or EhcTicketStatus.PendingUser or EhcTicketStatus.PendingThirdParty)
        {
            await tickets.TransitionTicketAsync(
                ticket.Id,
                EhcTicketStatus.Resolved,
                handoffNote,
                cancellationToken: cancellationToken);
            return;
        }

        throw new InvalidOperationException($"Ticket status '{ticket.Status}' cannot be resolved after the Estate handoff.");
    }

    [HttpPost("{id:guid}/reply")]
    public async Task<IActionResult> Reply(Guid id, [FromBody] AddEhcTicketMessageRequestDto request, CancellationToken cancellationToken)
    {
        if (!await Query().AnyAsync(t => t.Id == id, cancellationToken)) return NotFound();
        if (string.IsNullOrWhiteSpace(request.Body) || request.Body.Trim().Length > 4000)
            return BadRequest(new { success = false, message = "Enter a reply of up to 4,000 characters." });
        return Ok(new { success = true, data = await tickets.AddAgentMessageAsync(id, request, cancellationToken) });
    }

    [HttpGet("{id:guid}/transitions")]
    public async Task<IActionResult> Transitions(Guid id, CancellationToken cancellationToken)
    {
        if (!await Query().AnyAsync(t => t.Id == id, cancellationToken)) return NotFound();
        return Ok(new { success = true, data = await tickets.GetAllowedTransitionsAsync(id, cancellationToken) });
    }

    [HttpPost("{id:guid}/transition")]
    public async Task<IActionResult> Transition(Guid id, [FromBody] PropertyEnquiryTransition request, CancellationToken cancellationToken)
    {
        if (!await Query().AnyAsync(t => t.Id == id, cancellationToken)) return NotFound();
        try
        {
            await tickets.TransitionTicketAsync(id, request.Status, request.Notes, request.TransitionId, cancellationToken: cancellationToken);
            return Ok(new { success = true });
        }
        catch (InvalidOperationException ex) { return BadRequest(new { success = false, message = ex.Message }); }
        catch (ArgumentException ex) { return BadRequest(new { success = false, message = ex.Message }); }
    }
}
public sealed record PropertyEnquiryTransition(EhcTicketStatus Status, Guid? TransitionId, string? Notes);
public sealed record CreatePropertyEnquiryEstateHandoff(
    string? SalesReference,
    decimal? AgreedAmount,
    string? Currency,
    DateTime? SalesCompletedAt,
    string? Notes);
