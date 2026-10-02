using System.Text.Json;
using ErpSystem.Api.Services.Estate;
using ErpSystem.Core.DTOs.Ehc;
using ErpSystem.Core.DTOs.Sales;
using ErpSystem.Core.Entities.Estate;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Ehc;
using ErpSystem.Core.Interfaces.Sales;
using ErpSystem.Core.Enums;
using ErpSystem.Data;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Controllers.Ehc;

// Sales access is restricted to property enquiries. It grants no access to other helpdesk tickets.
[ApiController]
[Route("api/ehc/internal/property-enquiries")]
[Authorize(Policy = "InternalOnly")]
[Authorize(Roles = "Sales User,Sales Officer,Sales Manager,Marketing User,HelpdeskAgent,HelpdeskSupervisor,HelpdeskManager,Finance Officer,Finance Manager,Accounts Officer,Senior Accountant,Financial Controller,TenantAdmin,SuperAdmin")]
public sealed class EhcPropertyEnquiriesController(ApplicationDbContext db, ICurrentUserService currentUser,
    IEhcTicketService tickets, IEstateSalesListingApplicationHandoffService estateHandoffs,
    IPropertyEnquiryProspectService prospects) : ControllerBase
{
    private static readonly string[] SalesAndMarketingOrganizationUnitCodes = ["DEPT-SALES", "UNIT-MKT"];
    private IPropertyEnquiryProspectService ProspectService => prospects;

    private IQueryable<ErpSystem.Core.Entities.Ehc.EhcTicket> Query() => db.EhcTickets.AsNoTracking()
        .Where(t => t.TenantId == currentUser.TenantId && !t.IsDeleted && t.TicketType == EhcTicketType.Enquiry
            && t.Status != EhcTicketStatus.New
            && t.PropertyListingContextJson != null
            && t.AssignedOrganizationUnitId != null
            && t.AssignedOrganizationUnit != null
            && !t.AssignedOrganizationUnit.IsDeleted
            && t.AssignedOrganizationUnit.IsActive
            && SalesAndMarketingOrganizationUnitCodes.Contains(t.AssignedOrganizationUnit.Code));

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] int page = 1, CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        var total = await Query().CountAsync(cancellationToken);
        var rows = await Query().OrderByDescending(t => t.CreatedAt).Skip((page - 1) * 25).Take(25)
            .Select(t => new { t.Id, t.TicketNumber, t.Subject, t.Status, t.CreatedAt, t.FirstRespondedAt, t.CrmLeadId,
                t.PropertyListingContextJson, FallbackRequesterName = t.RequesterUser == null
                    ? null : t.RequesterUser.FirstName + " " + t.RequesterUser.LastName })
            .ToArrayAsync(cancellationToken);
        var items = rows.Select(t => new { t.Id, t.TicketNumber, t.Subject, t.Status, t.CreatedAt, t.FirstRespondedAt, t.CrmLeadId,
            RequesterName = PublicContactName(t.PropertyListingContextJson) ?? t.FallbackRequesterName }).ToArray();
        return Ok(new { success = true, data = items, totalCount = total, page, pageSize = 25 });
    }

    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery] string? search, [FromQuery] int take = 5,
        CancellationToken cancellationToken = default)
    {
        var term = search?.Trim() ?? string.Empty;
        if (term.Length < 2 || term.Length > 100) return Ok(new { success = true, data = Array.Empty<object>() });
        term = term.ToLowerInvariant();
        var items = await Query()
            .Where(item => item.TicketNumber.ToLower().Contains(term) || item.Subject.ToLower().Contains(term))
            .OrderByDescending(item => item.CreatedAt).ThenBy(item => item.Id)
            .Take(Math.Clamp(take, 1, 10))
            .Select(item => new { item.Id, item.TicketNumber, item.Subject, Status = item.Status.ToString() })
            .ToArrayAsync(cancellationToken);
        return Ok(new { success = true, data = items });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        if (!await Query().AnyAsync(t => t.Id == id, cancellationToken)) return NotFound();
        return Ok(new { success = true, data = await tickets.GetTicketByIdAsync(id, cancellationToken) });
    }

    [HttpGet("{id:guid}/sales-order-source")]
    public async Task<IActionResult> GetSalesOrderSource(
        Guid id,
        [FromServices] ISalesSetupService salesSetup,
        CancellationToken cancellationToken)
    {
        var snapshotJson = await Query()
            .Where(ticket => ticket.Id == id)
            .Select(ticket => ticket.PropertyListingContextJson)
            .SingleOrDefaultAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(snapshotJson)) return NotFound();

        EhcPropertyListingContextDto? property;
        try
        {
            property = JsonSerializer.Deserialize<EhcPropertyListingContextDto>(snapshotJson);
        }
        catch (JsonException)
        {
            return BadRequest(new
            {
                success = false,
                message = "The saved property enquiry snapshot is invalid. Reconcile the enquiry before creating a Sales Order."
            });
        }

        if (property is null)
        {
            return BadRequest(new
            {
                success = false,
                message = "The saved property enquiry snapshot is missing. Reconcile the enquiry before creating a Sales Order."
            });
        }

        var asset = await db.EstateManagedAssets.AsNoTracking()
            .Where(item => item.Id == property.ParentAssetId
                && item.TenantId == currentUser.TenantId
                && !item.IsDeleted)
            .Select(item => new
            {
                item.Id,
                item.AssetCode,
                item.AssetType,
                item.ProjectUnitId
            })
            .SingleOrDefaultAsync(cancellationToken);
        if (asset is null)
        {
            return Conflict(new
            {
                success = false,
                message = "The property from this enquiry no longer resolves to an Estate asset. Reconcile the listing before creating a Sales Order."
            });
        }

        string adapterKey;
        Guid sourceItemId;
        if (asset.AssetType == EstateManagedAssetType.Land)
        {
            if (!property.DemarcationId.HasValue || property.DemarcationId == Guid.Empty)
            {
                return Conflict(new
                {
                    success = false,
                    message = "This land enquiry does not identify a demarcated land record. Reconcile the listing before creating a Sales Order."
                });
            }

            adapterKey = "land-management";
            sourceItemId = property.DemarcationId.Value;
        }
        else if (asset.AssetType is EstateManagedAssetType.Property or EstateManagedAssetType.Facility)
        {
            adapterKey = "property-register";
            sourceItemId = asset.ProjectUnitId ?? asset.Id;
        }
        else
        {
            return Conflict(new
            {
                success = false,
                message = $"Estate asset type '{asset.AssetType}' is not configured as a property Sales Order source."
            });
        }

        var candidates = (await salesSetup.GetSaleableSourcesAsync())
            .Where(source => source.IsActive
                && source.AllowSalesOrders
                && source.AdapterKey.Equals(adapterKey, StringComparison.OrdinalIgnoreCase))
            .OrderBy(source => source.SortOrder)
            .ToArray();

        SalesSaleableSourceDto? matchedSource = null;
        SalesSaleableItemDto? matchedItem = null;
        foreach (var source in candidates)
        {
            var items = await salesSetup.SearchSaleableItemsAsync(source.Id, asset.AssetCode, 100);
            var item = items.FirstOrDefault(candidate =>
                candidate.SourceItemId.Equals(sourceItemId.ToString("D"), StringComparison.OrdinalIgnoreCase));
            if (item is null) continue;

            matchedSource = source;
            matchedItem = item;
            break;
        }

        if (matchedSource is null || matchedItem is null)
        {
            return Conflict(new
            {
                success = false,
                message = $"The {asset.AssetType} listing from this enquiry is no longer available in its configured Sales source. Reconcile the listing before creating a Sales Order."
            });
        }

        var allocationId = await db.Set<ErpSystem.Core.Entities.Ehc.EhcPropertyEnquiryProspect>()
            .AsNoTracking()
            .Where(item => item.TenantId == currentUser.TenantId
                && item.TicketId == id
                && !item.IsDeleted)
            .Select(item => item.SalesAllocationId)
            .SingleOrDefaultAsync(cancellationToken);
        if (allocationId.HasValue)
        {
            var allocation = await db.SalesAllocations.AsNoTracking()
                .SingleOrDefaultAsync(item => item.Id == allocationId.Value
                    && item.TenantId == currentUser.TenantId
                    && !item.IsDeleted, cancellationToken);
            if (allocation is not null)
            {
                matchedItem.ActiveAllocationId = allocation.Id;
                matchedItem.ActiveAllocationStatus = allocation.Status;
                matchedItem.ActiveAllocationReservedUntil = allocation.ReservedUntil;
                matchedItem.ActiveAllocationBusinessPartnerId = allocation.BusinessPartnerId;
                matchedItem.ActiveAllocationOpportunityId = allocation.OpportunityId;
                matchedItem.ActiveAllocationSalesOrderId = allocation.SalesOrderId;
                matchedItem.ActiveAllocationCustomerName = allocation.CustomerName;
                matchedItem.HasActiveAllocation = true;
                matchedItem.CanCreateSalesOrder = false;
                matchedItem.SalesOrderIneligibilityReason = allocation.SalesOrderId.HasValue
                    ? "This reservation is already linked to a Sales Order."
                    : "This item has an active reservation.";
            }
        }

        return Ok(new
        {
            success = true,
            data = new
            {
                propertyEnquiryId = id,
                assetType = asset.AssetType.ToString(),
                source = matchedSource,
                item = matchedItem
            }
        });
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
                item.PropertyListingContextJson,
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

        EhcPropertyListingContextDto? property = null;
        try
        {
            if (!string.IsNullOrWhiteSpace(ticket.PropertyListingContextJson))
            {
                property = JsonSerializer.Deserialize<EhcPropertyListingContextDto>(ticket.PropertyListingContextJson);
            }
        }
        catch (JsonException)
        {
            // The ticket remains readable even if a historical snapshot is malformed.
        }

        var linkedSalesOrder = await FindLinkedSalesOrderAsync(id, property, cancellationToken);

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
                salesOrder = linkedSalesOrder,
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

    private async Task<object?> FindLinkedSalesOrderAsync(
        Guid ticketId,
        EhcPropertyListingContextDto? property,
        CancellationToken cancellationToken)
    {
        var prospectLink = await db.Set<ErpSystem.Core.Entities.Ehc.EhcPropertyEnquiryProspect>()
            .AsNoTracking()
            .Where(item => item.TenantId == currentUser.TenantId && item.TicketId == ticketId && !item.IsDeleted)
            .Select(item => new { item.BusinessPartnerId, item.OpportunityId })
            .SingleOrDefaultAsync(cancellationToken);
        var businessPartnerId = prospectLink?.BusinessPartnerId ?? property?.BusinessPartnerId;
        var opportunityId = prospectLink?.OpportunityId;
        if (businessPartnerId is null || businessPartnerId == Guid.Empty
            || (!opportunityId.HasValue && string.IsNullOrWhiteSpace(property?.ListingReference)))
        {
            return null;
        }

        var orders = await db.SalesOrders.AsNoTracking()
            .Where(order => order.TenantId == currentUser.TenantId
                && !order.IsDeleted
                && order.BusinessPartnerId == businessPartnerId.Value
                && order.OrderStatus != SalesOrderStatus.Cancelled
                && order.OrderStatus != SalesOrderStatus.Rejected)
            .Select(order => new
            {
                order.Id,
                order.DocumentNumber,
                order.PropertyReference,
                order.OpportunityId,
                order.OrderStatus,
                order.TotalAmount,
                order.Currency,
                order.InvoiceId,
                order.CreatedAt,
                order.UpdatedAt
            })
            .ToArrayAsync(cancellationToken);

        // Opportunity is the durable link for public-enquiry Sales Orders. Property reference is
        // retained only as a legacy fallback for orders created before that handoff was introduced.
        var matchingOrders = opportunityId.HasValue
            ? orders.Where(item => item.OpportunityId == opportunityId.Value)
            : orders.Where(item => string.Equals(
                item.PropertyReference?.Trim(),
                property!.ListingReference.Trim(),
                StringComparison.OrdinalIgnoreCase));
        var order = matchingOrders
            .OrderByDescending(item => item.OrderStatus == SalesOrderStatus.Closed)
            .ThenByDescending(item => item.CreatedAt)
            .FirstOrDefault();
        if (order is null)
        {
            return null;
        }

        var invoice = order.InvoiceId is not { } invoiceId
            ? null
            : await db.Invoices.AsNoTracking()
                .Where(item => item.Id == invoiceId
                    && item.TenantId == currentUser.TenantId
                    && !item.IsDeleted
                    && item.BusinessPartnerId == businessPartnerId.Value)
                .Select(item => new
                {
                    item.Id,
                    item.InvoiceNumber,
                    item.PaidAmount,
                    item.CurrencyCode
                })
                .SingleOrDefaultAsync(cancellationToken);

        var payment = invoice is null
            ? null
            : await (from allocation in db.Set<PaymentAllocation>().AsNoTracking()
                     join receipt in db.Set<CustomerPayment>().AsNoTracking()
                         on allocation.CustomerPaymentId equals receipt.Id
                     where allocation.TenantId == currentUser.TenantId
                         && allocation.InvoiceId == invoice.Id
                         && !allocation.IsDeleted
                         && !allocation.IsReversal
                         && receipt.TenantId == currentUser.TenantId
                         && receipt.BusinessPartnerId == businessPartnerId.Value
                         && !receipt.IsDeleted
                         && receipt.ReversedAt == null
                         && receipt.Status != "Cancelled"
                         && receipt.Status != "Bounced"
                     orderby receipt.PaymentDate descending, allocation.AllocationDate descending
                     select new
                     {
                         receipt.PaymentNumber,
                         receipt.TransactionReference,
                         receipt.PaymentDate
                     })
                .FirstOrDefaultAsync(cancellationToken);

        DateTime? completedAt = null;
        if (order.OrderStatus == SalesOrderStatus.Closed)
        {
            completedAt = await db.SalesOrderStatusHistories.AsNoTracking()
                .Where(item => item.TenantId == currentUser.TenantId
                    && item.SalesOrderId == order.Id
                    && !item.IsDeleted
                    && item.ToStatus == SalesOrderStatus.Closed)
                .OrderByDescending(item => item.ChangedAt)
                .Select(item => (DateTime?)item.ChangedAt)
                .FirstOrDefaultAsync(cancellationToken)
                ?? order.UpdatedAt
                ?? order.CreatedAt;
        }

        return new
        {
            id = order.Id,
            reference = order.DocumentNumber,
            status = order.OrderStatus.ToString(),
            agreedAmount = order.TotalAmount,
            amountPaid = invoice?.PaidAmount ?? 0m,
            currency = invoice?.CurrencyCode ?? order.Currency,
            completedAt,
            invoiceReference = invoice?.InvoiceNumber,
            paymentReference = payment?.TransactionReference ?? payment?.PaymentNumber,
            paymentDate = payment?.PaymentDate
        };
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

        var prospectBusinessPartnerId = await db.Set<ErpSystem.Core.Entities.Ehc.EhcPropertyEnquiryProspect>()
            .AsNoTracking()
            .Where(item => item.TenantId == currentUser.TenantId && item.TicketId == id && !item.IsDeleted)
            .Select(item => item.BusinessPartnerId)
            .SingleOrDefaultAsync(cancellationToken);
        var businessPartnerId = prospectBusinessPartnerId ?? property?.BusinessPartnerId;
        if (property is null || !IsEstateListingSource(property.Source) || property.ListingId == Guid.Empty
            || businessPartnerId is null || businessPartnerId == Guid.Empty)
            return BadRequest(new { success = false, message = "This enquiry does not contain a verified Estate listing and business partner context." });

        if (string.IsNullOrWhiteSpace(request.SalesReference) || request.SalesReference.Trim().Length > 200)
            return BadRequest(new { success = false, message = "Enter the completed Sales reference, up to 200 characters." });
        if (request.AgreedAmount is <= 0)
            return BadRequest(new { success = false, message = "Enter a positive agreed amount." });
        if (ListingRequiresSalesDuration(property.ListingType)
            && string.IsNullOrWhiteSpace(request.RequestedLeaseTerm))
            return BadRequest(new { success = false, message = "Enter the Sales-agreed rent or lease duration before handing this enquiry to Estate." });
        if (request.RequestedLeaseTerm?.Trim().Length > 120)
            return BadRequest(new { success = false, message = "Sales-agreed duration must be 120 characters or fewer." });
        if (request.SalesAmountPaid is < 0)
            return BadRequest(new { success = false, message = "Sales amount paid cannot be negative." });
        if (request.SalesAmountPaid.HasValue
            && request.AgreedAmount.HasValue
            && request.SalesAmountPaid.Value > request.AgreedAmount.Value)
            return BadRequest(new { success = false, message = "Sales amount paid cannot be greater than the agreed amount." });
        if (request.SalesPaymentReference?.Trim().Length > 200)
            return BadRequest(new { success = false, message = "Sales payment reference must be 200 characters or fewer." });
        if (!string.IsNullOrWhiteSpace(request.Currency)
            && (request.Currency.Trim().Length != 3 || !request.Currency.Trim().All(char.IsLetter)))
            return BadRequest(new { success = false, message = "Currency must be a three-letter code." });
        if (request.Notes?.Trim().Length > 1000)
            return BadRequest(new { success = false, message = "Notes must be 1,000 characters or fewer." });

        try
        {
            var result = await estateHandoffs.CreateAsync(currentUser.TenantId ?? Guid.Empty, new(
                property.ListingId,
                businessPartnerId.Value,
                property.ListingType,
                ticket.CrmOpportunityId.Value,
                request.SalesReference.Trim(),
                request.AgreedAmount,
                request.RequestedLeaseTerm,
                request.SalesAmountPaid,
                request.SalesPaymentReference,
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
                Body = $"Sales reference: {request.SalesReference.Trim()}. Sales amount paid: {request.SalesAmountPaid ?? 0m:0.00}. Estate application: {result.ReferenceNumber ?? result.ProcedureCaseId.ToString()}.",
                IsInternal = true,
                ActorUserId = actorUserId,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = currentUser.UserName,
                CreatedById = actorUserId
            });
            await db.SaveChangesAsync(cancellationToken);

            await tickets.AddInternalCommentAsync(ticket.Id, new()
            {
                Body = $"Sales completed CRM opportunity {ticket.CrmOpportunityId} and handed this enquiry to Estate. Estate application: {result.ReferenceNumber ?? result.ProcedureCaseId.ToString()}. Sales reference: {request.SalesReference.Trim()}. Sales amount paid: {request.SalesAmountPaid ?? 0m:0.00}."
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

    private static bool ListingRequiresSalesDuration(string? listingType)
        => string.Equals(listingType, "Rent", StringComparison.OrdinalIgnoreCase)
            || string.Equals(listingType, "Lease", StringComparison.OrdinalIgnoreCase)
            || string.Equals(listingType, "SaleAndRent", StringComparison.OrdinalIgnoreCase)
            || string.Equals(listingType, "SaleAndLease", StringComparison.OrdinalIgnoreCase);

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
        var ticket = await Query().Where(t => t.Id == id).Select(t => new { t.TicketNumber, t.Subject }).SingleAsync(cancellationToken);
        var result = await ProspectService.SendEmailAsync(id, new SendPropertyEnquiryEmailRequest
        {
            Subject = $"Re: {ticket.Subject ?? ticket.TicketNumber}",
            Body = request.Body
        }, cancellationToken);
        if (!result.Sent)
            return StatusCode(StatusCodes.Status502BadGateway, new { success = false, data = result, message = "The email was not sent. The failed attempt was retained and can be retried." });
        return Ok(new { success = true, data = result });
    }

    [HttpGet("{id:guid}/prospect")]
    public async Task<IActionResult> GetProspect(Guid id, CancellationToken cancellationToken)
    {
        if (!await Query().AnyAsync(t => t.Id == id, cancellationToken)) return NotFound();
        return Ok(new { success = true, data = await ProspectService.GetAsync(id, cancellationToken) });
    }

    [HttpPost("{id:guid}/prospect/qualify")]
    [Authorize(Roles = "Sales User,Sales Officer,Sales Manager,TenantAdmin,SuperAdmin")]
    public async Task<IActionResult> Qualify(Guid id, [FromBody] QualifyPropertyEnquiryRequest request, CancellationToken cancellationToken)
        => await ExecuteProspectActionAsync(id, () => ProspectService.QualifyAsync(id, request, cancellationToken), cancellationToken);

    [HttpPost("{id:guid}/prospect/contacted")]
    [Authorize(Roles = "Sales User,Sales Officer,Sales Manager,TenantAdmin,SuperAdmin")]
    public async Task<IActionResult> RecordContact(Guid id, [FromBody] RecordPropertyEnquiryContactRequest request, CancellationToken cancellationToken)
        => await ExecuteProspectActionAsync(id, () => ProspectService.RecordContactAsync(id, request, cancellationToken), cancellationToken);

    [HttpPost("{id:guid}/prospect/disqualify")]
    [Authorize(Roles = "Sales User,Sales Officer,Sales Manager,TenantAdmin,SuperAdmin")]
    public async Task<IActionResult> Disqualify(Guid id, [FromBody] DisqualifyPropertyEnquiryRequest request, CancellationToken cancellationToken)
        => await ExecuteProspectActionAsync(id, () => ProspectService.DisqualifyAsync(id, request, cancellationToken), cancellationToken);

    [HttpPost("{id:guid}/prospect/opportunity")]
    [Authorize(Roles = "Sales User,Sales Officer,Sales Manager,TenantAdmin,SuperAdmin")]
    public async Task<IActionResult> CreateOpportunity(Guid id, [FromBody] CreatePropertyEnquiryOpportunityRequest request, CancellationToken cancellationToken)
        => await ExecuteProspectActionAsync(id, () => ProspectService.CreateOpportunityAsync(id, request, cancellationToken), cancellationToken);

    [HttpGet("{id:guid}/prospect/business-partner-matches")]
    public async Task<IActionResult> BusinessPartnerMatches(Guid id, CancellationToken cancellationToken)
        => await ExecuteProspectActionAsync(id, () => ProspectService.FindBusinessPartnerMatchesAsync(id, cancellationToken), cancellationToken);

    [HttpPost("{id:guid}/prospect/link-business-partner")]
    [Authorize(Roles = "Sales Manager,TenantAdmin,SuperAdmin")]
    public async Task<IActionResult> LinkBusinessPartner(Guid id, [FromBody] LinkPropertyEnquiryBusinessPartnerRequest request, CancellationToken cancellationToken)
        => await ExecuteProspectActionAsync(id, () => ProspectService.LinkBusinessPartnerAsync(id, request.BusinessPartnerId, cancellationToken), cancellationToken);

    [HttpPost("{id:guid}/prospect/create-business-partner")]
    [Authorize(Roles = "Sales Manager,TenantAdmin,SuperAdmin")]
    public async Task<IActionResult> CreateBusinessPartner(Guid id, [FromBody] CreatePropertyEnquiryBusinessPartnerRequest request, CancellationToken cancellationToken)
        => await ExecuteProspectActionAsync(id, () => ProspectService.CreateBusinessPartnerAsync(id, request, cancellationToken), cancellationToken);

    [HttpPost("{id:guid}/prospect/finalize-business-partner")]
    [Authorize(Roles = "Sales Manager,TenantAdmin,SuperAdmin")]
    public async Task<IActionResult> FinalizeBusinessPartner(Guid id, CancellationToken cancellationToken)
        => await ExecuteProspectActionAsync(id, () => ProspectService.FinalizeBusinessPartnerAsync(id, cancellationToken), cancellationToken);

    [HttpPost("{id:guid}/prospect/deposits")]
    [Authorize(Roles = "Sales User,Sales Officer,Sales Manager,TenantAdmin,SuperAdmin")]
    public async Task<IActionResult> RecordDeposit(Guid id, [FromBody] RecordProspectDepositRequest request, CancellationToken cancellationToken)
        => await ExecuteProspectActionAsync(id, () => ProspectService.RecordDepositAsync(id, request, cancellationToken), cancellationToken);

    [HttpGet("{id:guid}/prospect/deposits")]
    [Authorize(Roles = "Sales User,Sales Officer,Sales Manager,Finance Officer,Finance Manager,Accounts Officer,Senior Accountant,Financial Controller,TenantAdmin,SuperAdmin")]
    public async Task<IActionResult> GetDeposits(Guid id, CancellationToken cancellationToken)
        => await ExecuteProspectActionAsync(id, () => ProspectService.GetDepositsAsync(id, cancellationToken), cancellationToken);

    [HttpPost("{id:guid}/prospect/deposits/{receiptId:guid}/clear")]
    [Authorize(Policy = FinancePermissions.ReceiveCustomerPayments)]
    public async Task<IActionResult> ClearDeposit(Guid id, Guid receiptId, [FromBody] ClearProspectDepositRequest request, CancellationToken cancellationToken)
        => await ExecuteProspectActionAsync(id, () => ProspectService.ClearDepositAsync(id, receiptId, request, cancellationToken), cancellationToken);

    [HttpPost("{id:guid}/prospect/deposits/{receiptId:guid}/reverse")]
    [Authorize(Policy = FinancePermissions.ReverseArPayments)]
    public async Task<IActionResult> ReverseDeposit(Guid id, Guid receiptId, [FromBody] ReverseProspectDepositRequest request, CancellationToken cancellationToken)
        => await ExecuteProspectActionAsync(id, () => ProspectService.ReverseDepositAsync(id, receiptId, request, cancellationToken), cancellationToken);

    [HttpPost("prospect-deposit-policy")]
    [Authorize(Policy = FinancePermissions.ManageBankingSettings)]
    public async Task<IActionResult> UpsertDepositPolicy([FromBody] UpsertPropertyProspectDepositPolicyRequest request, CancellationToken cancellationToken)
    {
        try
        {
            await ProspectService.UpsertDepositPolicyAsync(request, cancellationToken);
            return Ok(new { success = true });
        }
        catch (KeyNotFoundException ex) { return NotFound(new { success = false, message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { success = false, message = ex.Message }); }
    }

    [HttpGet("prospect-deposit-policy")]
    [Authorize(Policy = FinancePermissions.ManageBankingSettings)]
    public async Task<IActionResult> GetDepositPolicy(
        [FromQuery] Guid salesSaleableSourceId,
        CancellationToken cancellationToken)
    {
        try
        {
            var policy = await ProspectService.GetDepositPolicyAsync(salesSaleableSourceId, cancellationToken);
            return Ok(new { success = true, data = policy });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    [HttpPost("{id:guid}/internal-note")]
    public async Task<IActionResult> AddInternalNote(Guid id, [FromBody] AddEhcTicketMessageRequestDto request, CancellationToken cancellationToken)
    {
        if (!await Query().AnyAsync(t => t.Id == id, cancellationToken)) return NotFound();
        if (string.IsNullOrWhiteSpace(request.Body) || request.Body.Trim().Length > 4000)
            return BadRequest(new { success = false, message = "Enter an internal note of up to 4,000 characters." });
        return Ok(new { success = true, data = await tickets.AddInternalCommentAsync(id, request, cancellationToken) });
    }

    private async Task<IActionResult> ExecuteProspectActionAsync<T>(Guid ticketId, Func<Task<T>> action, CancellationToken cancellationToken)
    {
        if (!await Query().AnyAsync(t => t.Id == ticketId, cancellationToken)) return NotFound();
        try { return Ok(new { success = true, data = await action() }); }
        catch (KeyNotFoundException ex) { return NotFound(new { success = false, message = ex.Message }); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (ArgumentException ex) { return BadRequest(new { success = false, message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { success = false, message = ex.Message }); }
    }

    private static string? PublicContactName(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try { return JsonSerializer.Deserialize<EhcPropertyListingContextDto>(json)?.ContactName; }
        catch (JsonException) { return null; }
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
    string? RequestedLeaseTerm,
    decimal? SalesAmountPaid,
    string? SalesPaymentReference,
    string? Currency,
    DateTime? SalesCompletedAt,
    string? Notes);
