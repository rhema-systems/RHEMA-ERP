using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ErpSystem.Core.DTOs.Ehc;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.DTOs.Sales;
using ErpSystem.Core.Entities.Ehc;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Entities.Sales;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Ehc;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.Interfaces.Sales;
using ErpSystem.Data;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.Ehc;

public sealed class PropertyEnquiryProspectService(
    ApplicationDbContext db,
    ICurrentUserService currentUser,
    IBusinessPartnerService businessPartners,
    IOpportunityService opportunities,
    ISalesAllocationService allocations,
    IProspectDepositFinancePostingService depositPosting,
    INotificationService notifications,
    IEhcTicketService tickets,
    ILogger<PropertyEnquiryProspectService> logger) : IPropertyEnquiryProspectService
{
    private static readonly string[] SalesOrganizationUnitCodes = ["DEPT-SALES", "UNIT-MKT"];

    public async Task<PropertyEnquiryProspectDto?> GetAsync(Guid ticketId, CancellationToken cancellationToken = default)
    {
        var prospect = await ProspectQuery().SingleOrDefaultAsync(x => x.TicketId == ticketId, cancellationToken);
        return prospect is null ? null : await ToDtoAsync(prospect, cancellationToken);
    }

    public async Task<PropertyEnquiryProspectDto> RecordContactAsync(
        Guid ticketId,
        RecordPropertyEnquiryContactRequest request,
        CancellationToken cancellationToken = default)
    {
        var (ticket, property) = await LoadTicketAsync(ticketId, cancellationToken);
        var prospect = await GetOrCreateProspectAsync(ticket, property, cancellationToken);
        if (prospect.Status == EhcPropertyProspectStatuses.Disqualified)
            throw new InvalidOperationException("A disqualified prospect cannot record further qualification activity.");
        if (prospect.Status is not (EhcPropertyProspectStatuses.New or EhcPropertyProspectStatuses.Contacted))
            throw new InvalidOperationException("Contact activity cannot move a qualified or converted prospect back to Contacted.");
        var lead = prospect.Lead ?? await db.Leads.SingleAsync(
            x => x.Id == prospect.LeadId && x.TenantId == TenantId && !x.IsDeleted,
            cancellationToken);
        lead.LeadStatus = "Contacted";
        lead.LastContactDate = DateTime.UtcNow;
        if (!string.IsNullOrWhiteSpace(request.Notes)) lead.Notes = AppendNote(lead.Notes, request.Notes);
        prospect.Status = EhcPropertyProspectStatuses.Contacted;
        prospect.UpdatedAt = DateTime.UtcNow;
        await AddAuditAsync(ticket, "ProspectContacted", "Sales contacted the public property prospect",
            string.IsNullOrWhiteSpace(request.Notes) ? "Sales recorded a prospect contact." : request.Notes.Trim(), cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return await ToDtoAsync(prospect, cancellationToken);
    }

    public async Task<PropertyEnquiryProspectDto> QualifyAsync(
        Guid ticketId,
        QualifyPropertyEnquiryRequest request,
        CancellationToken cancellationToken = default)
    {
        var (ticket, property) = await LoadTicketAsync(ticketId, cancellationToken);
        if (request.AgreedAmount <= 0) throw new InvalidOperationException("Enter the agreed property amount before qualification.");

        var source = await ResolveSaleableSourceAsync(property, cancellationToken);
        var policy = await db.Set<EhcPropertyProspectDepositPolicy>().AsNoTracking()
            .SingleOrDefaultAsync(x => x.TenantId == TenantId && !x.IsDeleted && x.IsActive
                && x.SalesSaleableSourceId == source.Id, cancellationToken);

        var prospect = await GetOrCreateProspectAsync(ticket, property, cancellationToken);
        if (prospect.Status == EhcPropertyProspectStatuses.Disqualified)
            throw new InvalidOperationException("This prospect is disqualified. Reopen it through a separately governed action before qualification.");
        if (prospect.Status is not (EhcPropertyProspectStatuses.Contacted or EhcPropertyProspectStatuses.Qualified))
            throw new InvalidOperationException(prospect.Status == EhcPropertyProspectStatuses.New
                ? "Record Sales contact before qualifying this prospect."
                : "Qualification cannot move an opportunity or converted prospect back to Qualified.");
        var lead = await db.Leads.SingleAsync(x => x.Id == prospect.LeadId && x.TenantId == TenantId && !x.IsDeleted, cancellationToken);
        var now = DateTime.UtcNow;
        lead.LeadStatus = "Qualified";
        lead.QualificationScore = request.QualificationScore;
        lead.EstimatedValue = request.AgreedAmount;
        lead.LastContactDate ??= now;
        if (!string.IsNullOrWhiteSpace(request.Notes)) lead.Notes = AppendNote(lead.Notes, request.Notes);
        lead.UpdatedAt = now;
        lead.UpdatedBy = currentUser.UserName;
        lead.LastModifiedById = ActorId;

        prospect.Status = EhcPropertyProspectStatuses.Qualified;
        prospect.QualifiedAt = now;
        prospect.QualifiedById = ActorId;
        prospect.AgreedAmount = request.AgreedAmount;
        prospect.Currency = Currency(request.Currency);
        // Qualification and opportunity creation remain available before Finance setup. If no
        // source policy exists yet, the conservative captured rule is full payment. Money cannot
        // be recorded or cleared until RecordDepositAsync finds an active policy and posting accounts.
        prospect.DepositRequirementType = policy?.RequirementType ?? ProspectDepositRequirementTypes.Full;
        prospect.FixedDepositAmount = policy?.FixedAmount;
        prospect.DepositPercentage = policy?.Percentage;
        prospect.UpdatedAt = now;
        prospect.UpdatedBy = currentUser.UserName;
        prospect.LastModifiedById = ActorId;

        await AddAuditAsync(ticket, "ProspectQualified", "Sales qualified the public property prospect",
            $"Qualification score: {request.QualificationScore}. Agreed amount: {request.AgreedAmount:0.00} {prospect.Currency}.", cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return await ToDtoAsync(prospect, cancellationToken);
    }

    public async Task<PropertyEnquiryProspectDto> DisqualifyAsync(
        Guid ticketId,
        DisqualifyPropertyEnquiryRequest request,
        CancellationToken cancellationToken = default)
    {
        var reason = request.Reason?.Trim();
        if (string.IsNullOrWhiteSpace(reason)) throw new InvalidOperationException("A disqualification reason is required.");
        var (ticket, property) = await LoadTicketAsync(ticketId, cancellationToken);
        var prospect = await GetOrCreateProspectAsync(ticket, property, cancellationToken);
        if (ticket.CrmOpportunityId.HasValue || prospect.OpportunityId.HasValue)
            throw new InvalidOperationException("A prospect with an opportunity cannot be disqualified. Close the opportunity through the Sales pipeline instead.");
        var lead = await db.Leads.SingleAsync(x => x.Id == prospect.LeadId && x.TenantId == TenantId && !x.IsDeleted, cancellationToken);
        lead.LeadStatus = "Unqualified";
        lead.Notes = AppendNote(lead.Notes, $"Disqualified: {reason}");
        lead.UpdatedAt = DateTime.UtcNow;
        lead.UpdatedBy = currentUser.UserName;
        lead.LastModifiedById = ActorId;
        prospect.Status = EhcPropertyProspectStatuses.Disqualified;
        prospect.UpdatedAt = DateTime.UtcNow;
        prospect.UpdatedBy = currentUser.UserName;
        prospect.LastModifiedById = ActorId;
        await AddAuditAsync(ticket, "ProspectDisqualified", "Sales disqualified the public property prospect", reason, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return await ToDtoAsync(prospect, cancellationToken);
    }

    public async Task<PropertyEnquiryProspectDto> CreateOpportunityAsync(
        Guid ticketId,
        CreatePropertyEnquiryOpportunityRequest request,
        CancellationToken cancellationToken = default)
    {
        var (ticket, property) = await LoadTicketAsync(ticketId, cancellationToken);
        var prospect = await ProspectQuery(tracking: true).SingleOrDefaultAsync(x => x.TicketId == ticketId, cancellationToken)
            ?? throw new InvalidOperationException("Qualify this enquiry before creating an opportunity.");
        var lead = await db.Leads.AsNoTracking().SingleAsync(x => x.Id == prospect.LeadId && x.TenantId == TenantId && !x.IsDeleted, cancellationToken);
        if (!string.Equals(lead.LeadStatus, "Qualified", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Only a qualified lead can create an opportunity.");
        if (prospect.Status is not (EhcPropertyProspectStatuses.Qualified or EhcPropertyProspectStatuses.Opportunity))
            throw new InvalidOperationException("Only a qualified prospect can create an opportunity.");
        if (request.Amount <= 0) throw new InvalidOperationException("Opportunity amount must be positive.");
        if (request.ExpectedCloseDate.Date < DateTime.UtcNow.Date)
            throw new InvalidOperationException("Expected close date cannot be in the past.");
        var prospectCurrency = Currency(prospect.Currency);
        var requestCurrency = Currency(request.Currency);
        if (!string.Equals(requestCurrency, prospectCurrency, StringComparison.Ordinal))
            throw new InvalidOperationException($"Opportunity currency must match the qualified prospect currency ({prospectCurrency}).");

        if (ticket.CrmOpportunityId.HasValue && prospect.OpportunityId.HasValue
            && ticket.CrmOpportunityId.Value != prospect.OpportunityId.Value)
            throw new InvalidOperationException("The enquiry has conflicting opportunity links. Reconcile the Sales lineage before continuing.");

        var linkedOpportunityId = ticket.CrmOpportunityId ?? prospect.OpportunityId;
        Opportunity? linkedOpportunity = null;
        if (linkedOpportunityId.HasValue)
        {
            linkedOpportunity = await db.Opportunities.AsNoTracking().SingleOrDefaultAsync(x =>
                x.Id == linkedOpportunityId.Value && x.TenantId == TenantId && !x.IsDeleted, cancellationToken)
                ?? throw new InvalidOperationException("The linked Sales opportunity could not be found for this tenant.");
            if (linkedOpportunity.LeadId != lead.Id)
                throw new InvalidOperationException("The linked Sales opportunity does not belong to this prospect lead.");
            if (!string.Equals(Currency(linkedOpportunity.Currency), prospectCurrency, StringComparison.Ordinal))
                throw new InvalidOperationException("The linked Sales opportunity currency does not match the qualified prospect currency. Reconcile the Sales lineage before continuing.");
        }

        if (prospect.Status == EhcPropertyProspectStatuses.Opportunity
            && linkedOpportunity is not null
            && ticket.CrmOpportunityId == linkedOpportunity.Id
            && prospect.OpportunityId == linkedOpportunity.Id
            && (!request.ReserveProperty || prospect.SalesAllocationId.HasValue))
            return await ToDtoAsync(prospect, cancellationToken);

        SalesSaleableSource? source = null;
        string? sourceItemId = null;
        if (request.ReserveProperty && !prospect.SalesAllocationId.HasValue)
        {
            var sourceSelection = await ResolveSaleableSourceSelectionAsync(property, cancellationToken);
            source = sourceSelection.Source;
            sourceItemId = sourceSelection.SourceItemId;
            if (!source.AllowReservations) throw new InvalidOperationException("The selected saleable source does not allow reservations.");
            if (await allocations.HasActiveAllocationAsync(source.Id, sourceItemId))
                throw new InvalidOperationException("This property already has an active reservation or allocation.");
        }

        var opportunityId = linkedOpportunity?.Id;
        if (!opportunityId.HasValue)
        {
            var opportunity = await opportunities.CreateAsync(new CreateOpportunityDto
            {
                Name = Clip($"Property enquiry: {property.ListingName}", 200)!,
                Description = Clip($"Originating enquiry {ticket.TicketNumber}. Property {property.ListingReference}: {property.ListingName}.", 2000),
                LeadId = lead.Id,
                CustomerId = null,
                Stage = "Qualification",
                Probability = 20,
                Amount = request.Amount,
                Currency = prospectCurrency,
                ExpectedCloseDate = request.ExpectedCloseDate,
                LeadSource = "Public Property Listing",
                OpportunityType = "New Business",
                AssignedToId = ticket.AssignedToUserId,
                Notes = Clip($"Originating EHC ticket: {ticket.TicketNumber}. {request.Notes}", 2000)
            });
            opportunityId = opportunity.Id;
        }

        ticket.CrmOpportunityId = opportunityId.Value;
        prospect.OpportunityId = opportunityId.Value;
        prospect.Status = EhcPropertyProspectStatuses.Opportunity;
        prospect.AgreedAmount = linkedOpportunity?.Amount ?? request.Amount;
        prospect.Currency = prospectCurrency;

        if (request.ReserveProperty && source is not null)
        {
            var allocation = await allocations.CreateAllocationAsync(new CreateSalesAllocationDto
            {
                SaleableSourceId = source.Id,
                SourceItemId = sourceItemId!,
                SourceItemCode = Clip(property.ListingReference, 100),
                SourceItemName = Clip(property.ListingName, 250)!,
                SourceItemType = Clip(property.ListingType, 80),
                CustomerName = Clip(property.ContactName ?? property.BusinessPartnerName, 200),
                LeadId = lead.Id,
                OpportunityId = opportunityId.Value,
                AllocationType = "Reservation",
                Status = "Reserved",
                ReservedUntil = DateTime.UtcNow.AddDays(request.ReservationDays),
                EstimatedValue = prospect.AgreedAmount,
                AgreedValue = prospect.AgreedAmount,
                Currency = prospect.Currency,
                Notes = $"Provisional public-prospect reservation from enquiry {ticket.TicketNumber}."
            });
            prospect.SalesAllocationId = allocation.Id;
        }

        ticket.UpdatedAt = DateTime.UtcNow;
        prospect.UpdatedAt = DateTime.UtcNow;
        var reconciledLegacyOpportunity = linkedOpportunity is not null;
        await AddAuditAsync(ticket,
            reconciledLegacyOpportunity ? "OpportunityReconciled" : "OpportunityCreated",
            reconciledLegacyOpportunity
                ? "Sales accepted the existing qualified prospect opportunity"
                : "Sales created the qualified prospect opportunity",
            reconciledLegacyOpportunity
                ? $"Existing opportunity {opportunityId.Value} was explicitly accepted after qualification and its lineage was reconciled."
                : $"Opportunity {opportunityId.Value} was explicitly created after qualification.",
            cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return await ToDtoAsync(prospect, cancellationToken);
    }

    public async Task<IReadOnlyList<PropertyEnquiryBusinessPartnerMatchDto>> FindBusinessPartnerMatchesAsync(
        Guid ticketId,
        CancellationToken cancellationToken = default)
    {
        var (_, property) = await LoadTicketAsync(ticketId, cancellationToken);
        var email = Clean(property.ContactEmail)?.ToLowerInvariant();
        var phone = NormalizePhone(property.ContactPhone);
        if (email is null && phone is null) return [];

        var candidates = await db.BusinessPartners.AsNoTracking().Include(x => x.Roles)
            .Where(x => x.TenantId == TenantId && !x.IsDeleted
                && (BusinessPartnerRoles.CustomerTypes.Contains(x.PartnerType)
                    || x.Roles.Any(r => !r.IsDeleted && r.RoleType == BusinessPartnerRoleType.Customer)))
            .Where(x => (email != null && x.PrimaryEmail != null && x.PrimaryEmail.ToLower() == email)
                || (phone != null && x.PrimaryPhone != null
                    && x.PrimaryPhone.Replace(" ", "").Replace("-", "").Replace("(", "").Replace(")", "").Replace("+", "") == phone)
                || (phone != null && x.SecondaryPhone != null
                    && x.SecondaryPhone.Replace(" ", "").Replace("-", "").Replace("(", "").Replace(")", "").Replace("+", "") == phone))
            .Take(25).ToListAsync(cancellationToken);

        return candidates.Select(x =>
        {
            var matched = new List<string>();
            if (email is not null && string.Equals(Clean(x.PrimaryEmail), email, StringComparison.OrdinalIgnoreCase)) matched.Add("Email");
            if (phone is not null && (NormalizePhone(x.PrimaryPhone) == phone || NormalizePhone(x.SecondaryPhone) == phone)) matched.Add("Phone");
            return new PropertyEnquiryBusinessPartnerMatchDto(x.Id, x.PartnerCode, x.PartnerName,
                x.PrimaryEmail, x.PrimaryPhone, x.ApprovalStatus, x.IsActive, matched);
        }).Where(x => x.MatchedOn.Count > 0).ToArray();
    }

    public async Task<PropertyEnquiryProspectDto> LinkBusinessPartnerAsync(
        Guid ticketId,
        Guid businessPartnerId,
        CancellationToken cancellationToken = default)
    {
        var (ticket, _) = await LoadTicketAsync(ticketId, cancellationToken);
        var prospect = await ProspectQuery(tracking: true).SingleOrDefaultAsync(x => x.TicketId == ticketId, cancellationToken)
            ?? throw new InvalidOperationException("Qualify this enquiry before linking a Business Partner.");
        var lead = await db.Leads.AsNoTracking().SingleAsync(x => x.Id == prospect.LeadId && x.TenantId == TenantId && !x.IsDeleted, cancellationToken);
        if (!string.Equals(lead.LeadStatus, "Qualified", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Only a qualified prospect can be linked to an existing Business Partner.");
        var partner = await db.BusinessPartners.Include(x => x.Roles).SingleOrDefaultAsync(x => x.Id == businessPartnerId
            && x.TenantId == TenantId && !x.IsDeleted, cancellationToken)
            ?? throw new KeyNotFoundException("The selected Business Partner was not found.");
        if (!BusinessPartnerRoles.HasCustomer(partner.PartnerType)
            && !partner.Roles.Any(x => !x.IsDeleted && x.RoleType == BusinessPartnerRoleType.Customer))
            throw new InvalidOperationException("The selected Business Partner does not have the Customer role.");
        if (!partner.IsActive || !string.Equals(partner.ApprovalStatus, "Approved", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Only an approved, active Customer Business Partner can be linked.");

        prospect.BusinessPartnerId = partner.Id;
        prospect.BusinessPartnerLinkedAt = DateTime.UtcNow;
        prospect.BusinessPartnerLinkedById = ActorId;
        prospect.UpdatedAt = DateTime.UtcNow;
        prospect.UpdatedBy = currentUser.UserName;
        prospect.LastModifiedById = ActorId;
        await AddAuditAsync(ticket, "ExistingBusinessPartnerLinked", "Existing Customer Business Partner linked to prospect",
            $"Approved Business Partner {partner.PartnerCode} was linked for identity reuse. The prospect remains qualified until an opportunity exists and its payment threshold is met.", cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return await ToDtoAsync(prospect, cancellationToken);
    }

    public async Task<PropertyEnquiryProspectDto> CreateBusinessPartnerAsync(
        Guid ticketId,
        CreatePropertyEnquiryBusinessPartnerRequest request,
        CancellationToken cancellationToken = default)
    {
        var (ticket, property) = await LoadTicketAsync(ticketId, cancellationToken);
        var prospect = await RequireEligibleProspectAsync(ticketId, cancellationToken);
        if (prospect.BusinessPartnerId.HasValue)
            throw new InvalidOperationException("This prospect is already linked to a Business Partner.");
        var matches = await FindBusinessPartnerMatchesAsync(ticketId, cancellationToken);
        if (matches.Count > 0)
            throw new InvalidOperationException("A likely Customer Business Partner match already exists. Review and link it instead of creating a duplicate.");

        var result = await businessPartners.CreateAsync(new CreateBusinessPartnerDto
        {
            PartnerName = Clean(request.PartnerName) ?? Clean(property.ContactName) ?? property.BusinessPartnerName,
            PartnerType = "Customer",
            RoleTypes = [BusinessPartnerRoleType.Customer.ToString()],
            Email = Clean(request.Email) ?? Clean(property.ContactEmail),
            Phone = Clean(request.Phone) ?? Clean(property.ContactPhone),
            PhysicalAddress = Clean(request.PhysicalAddress),
            City = Clean(request.City),
            Country = Clean(request.Country),
            PostalCode = Clean(request.PostalCode),
            Currency = prospect.Currency,
            CustomerType = "Property Customer",
            CustomerSince = DateTime.UtcNow,
            Notes = $"Created from qualified public property enquiry {ticket.TicketNumber} after its payment threshold was met."
        });

        prospect.BusinessPartnerId = result.Id;
        prospect.BusinessPartnerLinkedAt = DateTime.UtcNow;
        prospect.BusinessPartnerLinkedById = ActorId;
        prospect.Status = EhcPropertyProspectStatuses.CustomerPendingApproval;
        prospect.UpdatedAt = DateTime.UtcNow;
        await AddAuditAsync(ticket, "BusinessPartnerCreated", "Customer Business Partner submitted for approval",
            $"Business Partner {result.PartnerCode} was created from the qualified prospect. Original enquiry contact data was preserved.", cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return await ToDtoAsync(prospect, cancellationToken);
    }

    public async Task<PropertyEnquiryProspectDto> FinalizeBusinessPartnerAsync(Guid ticketId, CancellationToken cancellationToken = default)
    {
        var (ticket, _) = await LoadTicketAsync(ticketId, cancellationToken);
        var prospect = await RequireEligibleProspectAsync(ticketId, cancellationToken);
        if (!prospect.BusinessPartnerId.HasValue)
            throw new InvalidOperationException("Create or link the Business Partner before finalizing conversion.");
        var partner = await db.BusinessPartners.AsNoTracking().SingleAsync(x => x.Id == prospect.BusinessPartnerId.Value
            && x.TenantId == TenantId && !x.IsDeleted, cancellationToken);
        if (!partner.IsActive || !string.Equals(partner.ApprovalStatus, "Approved", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Complete Business Partner approval before finalizing the customer conversion.");
        await LinkAndTransferAsync(ticket, prospect, partner.Id, cancellationToken);
        return await ToDtoAsync(prospect, cancellationToken);
    }

    public async Task<ProspectDepositReceiptDto> RecordDepositAsync(
        Guid ticketId,
        RecordProspectDepositRequest request,
        CancellationToken cancellationToken = default)
    {
        await LoadTicketAsync(ticketId, cancellationToken);
        var prospect = await ProspectQuery(tracking: true).SingleOrDefaultAsync(x => x.TicketId == ticketId, cancellationToken)
            ?? throw new InvalidOperationException("Qualify this enquiry before recording a deposit.");
        if (!prospect.OpportunityId.HasValue)
            throw new InvalidOperationException("Create the opportunity before recording a prospect deposit.");
        if (prospect.Status != EhcPropertyProspectStatuses.Opportunity)
            throw new InvalidOperationException("Prospect deposits can only be recorded while the opportunity is active and before customer conversion starts.");
        if (request.Amount <= 0) throw new InvalidOperationException("Deposit amount must be positive.");
        if (!string.Equals(Currency(request.Currency), prospect.Currency, StringComparison.Ordinal))
            throw new InvalidOperationException($"Deposit currency must be {prospect.Currency}.");

        var (_, property) = await LoadTicketAsync(ticketId, cancellationToken);
        var source = await ResolveSaleableSourceAsync(property, cancellationToken);
        var policy = await db.Set<EhcPropertyProspectDepositPolicy>().AsNoTracking().SingleOrDefaultAsync(x => x.TenantId == TenantId
            && !x.IsDeleted && x.IsActive && x.SalesSaleableSourceId == source.Id, cancellationToken)
            ?? throw new InvalidOperationException("An active deposit policy is required.");
        if (!policy.DefaultBankAccountId.HasValue && !policy.DefaultLiquidityAccountId.HasValue)
            throw new InvalidOperationException("Configure a default bank or liquidity account for prospect deposits.");

        var receipt = new ProspectDepositReceipt
        {
            TenantId = TenantId,
            ProspectId = prospect.Id,
            TicketId = ticketId,
            LeadId = prospect.LeadId,
            OpportunityId = prospect.OpportunityId.Value,
            SalesAllocationId = prospect.SalesAllocationId,
            ReceiptNumber = $"PDR-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid():N}"[..22].ToUpperInvariant(),
            Amount = request.Amount,
            Currency = prospect.Currency,
            PaymentMethod = request.PaymentMethod.Trim(),
            TransactionReference = Clean(request.TransactionReference),
            ReceivedAt = request.ReceivedAt ?? DateTime.UtcNow,
            Status = ProspectDepositReceiptStatuses.Pending,
            DepositLiabilityAccountId = policy.DepositLiabilityAccountId,
            BankAccountId = policy.DefaultBankAccountId,
            LiquidityAccountId = policy.DefaultLiquidityAccountId,
            CreatedBy = currentUser.UserName,
            CreatedById = ActorId
        };
        db.Set<ProspectDepositReceipt>().Add(receipt);
        await db.SaveChangesAsync(cancellationToken);
        return ToReceiptDto(receipt);
    }

    public async Task<IReadOnlyList<ProspectDepositReceiptDto>> GetDepositsAsync(
        Guid ticketId,
        CancellationToken cancellationToken = default)
    {
        await LoadTicketAsync(ticketId, cancellationToken);
        return await db.Set<ProspectDepositReceipt>().AsNoTracking()
            .Where(x => x.TenantId == TenantId && x.TicketId == ticketId && !x.IsDeleted)
            .OrderByDescending(x => x.ReceivedAt)
            .ThenByDescending(x => x.CreatedAt)
            .Select(x => new ProspectDepositReceiptDto(
                x.Id, x.ReceiptNumber, x.Amount, x.Currency, x.PaymentMethod,
                x.TransactionReference, x.Status, x.ReceivedAt, x.ClearedAt,
                x.ReversedAt, x.PostingEventId, x.JournalEntryId,
                x.CustomerAdvanceTransferPostingEventId, x.CustomerPaymentId))
            .ToListAsync(cancellationToken);
    }

    public async Task<ProspectDepositReceiptDto> ClearDepositAsync(
        Guid ticketId,
        Guid receiptId,
        ClearProspectDepositRequest request,
        CancellationToken cancellationToken = default)
    {
        await LoadTicketAsync(ticketId, cancellationToken);
        var receipt = await db.Set<ProspectDepositReceipt>().SingleOrDefaultAsync(x => x.Id == receiptId && x.TicketId == ticketId
            && x.TenantId == TenantId && !x.IsDeleted, cancellationToken) ?? throw new KeyNotFoundException("Deposit receipt not found.");
        if (receipt.Status == ProspectDepositReceiptStatuses.Cleared && receipt.PostingEventId.HasValue && receipt.JournalEntryId.HasValue)
            return ToReceiptDto(receipt);
        if (receipt.Status != ProspectDepositReceiptStatuses.Pending)
            throw new InvalidOperationException("Only a pending prospect deposit can be cleared.");

        // Finance must receive the operator-selected clearance date. The posting adapter uses this
        // date for period/book validation and only falls back to ReceivedAt when it is absent.
        receipt.ClearedAt = request.ClearedAt ?? DateTime.UtcNow;
        var posted = await depositPosting.PostClearedReceiptAsync(receipt, cancellationToken);
        receipt.PostingEventId = posted.PostingEventId;
        receipt.JournalEntryId = posted.JournalEntryId;
        receipt.Status = ProspectDepositReceiptStatuses.Cleared;
        receipt.ClearedById = ActorId;
        receipt.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return ToReceiptDto(receipt);
    }

    public async Task<ProspectDepositReceiptDto> ReverseDepositAsync(
        Guid ticketId,
        Guid receiptId,
        ReverseProspectDepositRequest request,
        CancellationToken cancellationToken = default)
    {
        await LoadTicketAsync(ticketId, cancellationToken);
        var receipt = await db.Set<ProspectDepositReceipt>().SingleOrDefaultAsync(x => x.Id == receiptId && x.TicketId == ticketId
            && x.TenantId == TenantId && !x.IsDeleted, cancellationToken) ?? throw new KeyNotFoundException("Deposit receipt not found.");
        if (receipt.Status == ProspectDepositReceiptStatuses.Reversed) return ToReceiptDto(receipt);
        if (receipt.Status != ProspectDepositReceiptStatuses.Cleared || !receipt.PostingEventId.HasValue)
            throw new InvalidOperationException("Only a posted, cleared prospect deposit can be reversed.");
        if (receipt.TransferredToCustomerAdvanceAt.HasValue)
            throw new InvalidOperationException("Reverse the customer-advance transfer through Finance before reversing this prospect receipt.");

        var reversal = await depositPosting.ReverseReceiptAsync(receipt, request.Reason.Trim(), request.ReversalDate, cancellationToken);
        receipt.ReversalPostingEventId = reversal.PostingEventId;
        receipt.ReversalJournalEntryId = reversal.JournalEntryId;
        receipt.Status = ProspectDepositReceiptStatuses.Reversed;
        receipt.ReversedAt = request.ReversalDate ?? DateTime.UtcNow;
        receipt.ReversedById = ActorId;
        receipt.ReversalReason = request.Reason.Trim();
        receipt.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return ToReceiptDto(receipt);
    }

    public async Task<PropertyEnquiryEmailResultDto> SendEmailAsync(
        Guid ticketId,
        SendPropertyEnquiryEmailRequest request,
        CancellationToken cancellationToken = default)
    {
        var (ticket, property) = await LoadTicketAsync(ticketId, cancellationToken);
        var recipient = Clean(property.ContactEmail) ?? throw new InvalidOperationException("This enquiry does not contain a valid contact email address.");
        var attempt = new EhcPropertyEnquiryEmailAttempt
        {
            TenantId = TenantId,
            TicketId = ticket.Id,
            Recipient = recipient,
            Sender = Clean(currentUser.Email) ?? currentUser.UserName,
            Subject = request.Subject.Trim(),
            BodySha256 = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(request.Body))),
            Status = PropertyEnquiryEmailStatuses.Pending,
            AttemptedAt = DateTime.UtcNow,
            CreatedBy = currentUser.UserName,
            CreatedById = ActorId
        };
        db.Set<EhcPropertyEnquiryEmailAttempt>().Add(attempt);
        await db.SaveChangesAsync(cancellationToken);

        try
        {
            await notifications.SendEmailAsync(recipient, request.Subject.Trim(), request.Body.Trim(), true);
            attempt.Status = PropertyEnquiryEmailStatuses.Sent;
            attempt.SentAt = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
            try
            {
                await tickets.AddAgentMessageAsync(ticket.Id,
                    new AddEhcTicketMessageRequestDto { Body = request.Body.Trim() }, cancellationToken);
            }
            catch (Exception activityException)
            {
                // Delivery already succeeded. A secondary ticket-activity failure must not mark the
                // email as failed and encourage the operator to send the customer a duplicate.
                logger.LogWarning(activityException,
                    "Property enquiry email {AttemptId} was sent, but its ticket activity could not be recorded",
                    attempt.Id);
            }
            return new(attempt.Id, true, recipient, null);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Sending property enquiry email {AttemptId} failed", attempt.Id);
            attempt.Status = PropertyEnquiryEmailStatuses.Failed;
            attempt.FailureReason = Clip(ex.Message, 1000);
            await db.SaveChangesAsync(cancellationToken);
            return new(attempt.Id, false, recipient, attempt.FailureReason);
        }
    }

    public async Task<PropertyProspectDepositPolicyDto?> GetDepositPolicyAsync(
        Guid salesSaleableSourceId,
        CancellationToken cancellationToken = default)
    {
        if (salesSaleableSourceId == Guid.Empty)
            throw new InvalidOperationException("Select a saleable source.");
        return await db.Set<EhcPropertyProspectDepositPolicy>().AsNoTracking()
            .Where(x => x.TenantId == TenantId && !x.IsDeleted
                && x.SalesSaleableSourceId == salesSaleableSourceId)
            .Select(x => new PropertyProspectDepositPolicyDto(
                x.Id, x.SalesSaleableSourceId, x.RequirementType, x.FixedAmount, x.Percentage,
                x.DepositLiabilityAccountId, x.DefaultBankAccountId, x.DefaultLiquidityAccountId,
                x.IsActive))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task UpsertDepositPolicyAsync(UpsertPropertyProspectDepositPolicyRequest request, CancellationToken cancellationToken = default)
    {
        ValidatePolicy(request);
        var source = await db.SalesSaleableSources.AsNoTracking().SingleOrDefaultAsync(x =>
            x.Id == request.SalesSaleableSourceId && x.TenantId == TenantId && !x.IsDeleted,
            cancellationToken) ?? throw new KeyNotFoundException("Saleable source not found.");

        var liability = await db.Accounts.AsNoTracking().SingleOrDefaultAsync(x =>
            x.Id == request.DepositLiabilityAccountId && x.TenantId == TenantId && !x.IsDeleted,
            cancellationToken) ?? throw new InvalidOperationException("The prospect-deposit liability account is unavailable for this tenant.");
        if (liability.Status != AccountStatus.Active || liability.AccountType != AccountType.Liability
            || liability.IsControlAccount || !liability.AllowDirectPosting)
            throw new InvalidOperationException("Select an active, direct-posting, non-control liability account.");

        Guid cashGlAccountId;
        string cashCurrency;
        if (request.DefaultBankAccountId.HasValue)
        {
            var bank = await db.BankAccounts.AsNoTracking().SingleOrDefaultAsync(x =>
                x.Id == request.DefaultBankAccountId.Value && x.TenantId == TenantId
                && !x.IsDeleted && x.IsActive, cancellationToken)
                ?? throw new InvalidOperationException("The selected bank account is unavailable for this tenant.");
            cashGlAccountId = bank.GLAccountId
                ?? throw new InvalidOperationException("The selected bank account has no GL account mapping.");
            cashCurrency = Currency(bank.Currency);
        }
        else
        {
            var liquidity = await db.LiquidityAccounts.AsNoTracking().SingleOrDefaultAsync(x =>
                x.Id == request.DefaultLiquidityAccountId!.Value && x.TenantId == TenantId
                && !x.IsDeleted && x.IsActive, cancellationToken)
                ?? throw new InvalidOperationException("The selected liquidity account is unavailable for this tenant.");
            cashGlAccountId = liquidity.GLAccountId;
            cashCurrency = Currency(liquidity.Currency);
        }

        var cashAccount = await db.Accounts.AsNoTracking().SingleOrDefaultAsync(x =>
            x.Id == cashGlAccountId && x.TenantId == TenantId && !x.IsDeleted,
            cancellationToken) ?? throw new InvalidOperationException("The selected cash account GL mapping is unavailable for this tenant.");
        if (cashAccount.Status != AccountStatus.Active || cashAccount.AccountType != AccountType.Asset)
            throw new InvalidOperationException("The selected bank or liquidity account must map to an active Asset GL account.");

        var financeCurrency = await db.FinanceSettings.AsNoTracking()
            .Where(x => x.TenantId == TenantId && !x.IsDeleted)
            .Select(x => x.BaseCurrency)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException("Finance settings are not configured for this tenant.");
        if (!string.Equals(cashCurrency, Currency(financeCurrency), StringComparison.Ordinal)
            || !string.Equals(Currency(source.DefaultCurrency), Currency(financeCurrency), StringComparison.Ordinal))
            throw new InvalidOperationException("The saleable source, cash account and Finance functional currency must match for prospect deposits.");

        var policy = await db.Set<EhcPropertyProspectDepositPolicy>().SingleOrDefaultAsync(x => x.TenantId == TenantId
            && x.SalesSaleableSourceId == request.SalesSaleableSourceId && !x.IsDeleted, cancellationToken);
        if (policy is null)
        {
            policy = new EhcPropertyProspectDepositPolicy { TenantId = TenantId, SalesSaleableSourceId = request.SalesSaleableSourceId };
            db.Set<EhcPropertyProspectDepositPolicy>().Add(policy);
        }
        policy.RequirementType = request.RequirementType;
        policy.FixedAmount = request.RequirementType == ProspectDepositRequirementTypes.Fixed ? request.FixedAmount : null;
        policy.Percentage = request.RequirementType == ProspectDepositRequirementTypes.Percentage ? request.Percentage : null;
        policy.DepositLiabilityAccountId = request.DepositLiabilityAccountId;
        policy.DefaultBankAccountId = request.DefaultBankAccountId;
        policy.DefaultLiquidityAccountId = request.DefaultLiquidityAccountId;
        policy.IsActive = request.IsActive;
        policy.UpdatedAt = DateTime.UtcNow;
        policy.UpdatedBy = currentUser.UserName;

        // Qualified prospects keep the threshold values needed for a stable read model. Reconcile
        // that snapshot whenever its source policy changes so Sales sees and enforces the current
        // rule. Converted prospects are intentionally excluded: their cleared deposits may already
        // have been transferred to customer advances and that posting history must remain immutable.
        var activeProspects = await db.Set<EhcPropertyEnquiryProspect>()
            .Include(x => x.Ticket)
            .Where(x => x.TenantId == TenantId && !x.IsDeleted
                && (x.Status == EhcPropertyProspectStatuses.Qualified
                    || x.Status == EhcPropertyProspectStatuses.Opportunity
                    || x.Status == EhcPropertyProspectStatuses.CustomerPendingApproval))
            .ToListAsync(cancellationToken);
        var reconciledAt = DateTime.UtcNow;
        var reconciled = 0;
        foreach (var prospect in activeProspects)
        {
            if (!ProspectUsesSource(prospect.Ticket.PropertyListingContextJson, source)) continue;

            var previousThreshold = DescribeThreshold(
                prospect.DepositRequirementType,
                prospect.FixedDepositAmount,
                prospect.DepositPercentage);
            prospect.DepositRequirementType = request.IsActive
                ? request.RequirementType
                : ProspectDepositRequirementTypes.Full;
            prospect.FixedDepositAmount = request.IsActive
                && request.RequirementType == ProspectDepositRequirementTypes.Fixed
                    ? request.FixedAmount
                    : null;
            prospect.DepositPercentage = request.IsActive
                && request.RequirementType == ProspectDepositRequirementTypes.Percentage
                    ? request.Percentage
                    : null;
            prospect.UpdatedAt = reconciledAt;
            prospect.UpdatedBy = currentUser.UserName;
            prospect.LastModifiedById = ActorId;
            var currentThreshold = DescribeThreshold(
                prospect.DepositRequirementType,
                prospect.FixedDepositAmount,
                prospect.DepositPercentage);
            await AddAuditAsync(prospect.Ticket,
                "ProspectDepositPolicyReconciled",
                "Prospect deposit threshold updated",
                $"The {source.DisplayName} deposit policy changed this prospect threshold from {previousThreshold} to {currentThreshold}. No receipt or Finance posting was changed.",
                cancellationToken);
            reconciled++;
        }

        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation(
            "Prospect deposit policy {PolicyId} for source {SourceId} reconciled {ProspectCount} active qualified prospects",
            policy.Id, source.Id, reconciled);
    }

    private async Task<EhcPropertyEnquiryProspect> RequireEligibleProspectAsync(Guid ticketId, CancellationToken cancellationToken)
    {
        var prospect = await ProspectQuery(tracking: true).SingleOrDefaultAsync(x => x.TicketId == ticketId, cancellationToken)
            ?? throw new InvalidOperationException("Qualify this enquiry before converting the prospect.");
        if (!prospect.OpportunityId.HasValue)
            throw new InvalidOperationException("Create the opportunity before converting the prospect.");
        if (prospect.Status is not (EhcPropertyProspectStatuses.Opportunity or EhcPropertyProspectStatuses.CustomerPendingApproval))
            throw new InvalidOperationException("The prospect is not in a customer-conversion stage.");
        var required = RequiredDeposit(prospect);
        var cleared = await ClearedDepositAsync(prospect.Id, cancellationToken);
        if (cleared < required)
            throw new InvalidOperationException($"The cleared prospect deposit is {cleared:0.00} {prospect.Currency}; {required:0.00} is required before customer registration.");
        return prospect;
    }

    private async Task LinkAndTransferAsync(EhcTicket ticket, EhcPropertyEnquiryProspect prospect, Guid businessPartnerId, CancellationToken cancellationToken)
    {
        var receipts = await db.Set<ProspectDepositReceipt>().Where(x => x.TenantId == TenantId && x.ProspectId == prospect.Id
            && !x.IsDeleted && x.Status == ProspectDepositReceiptStatuses.Cleared && !x.TransferredToCustomerAdvanceAt.HasValue)
            .OrderBy(x => x.ReceivedAt).ToListAsync(cancellationToken);
        foreach (var receipt in receipts)
        {
            var transfer = await depositPosting.TransferToCustomerAdvanceAsync(receipt, businessPartnerId, cancellationToken);
            receipt.BusinessPartnerId = businessPartnerId;
            receipt.CustomerAdvanceTransferPostingEventId = transfer.PostingEventId;
            receipt.CustomerAdvanceTransferJournalEntryId = transfer.JournalEntryId;
            receipt.CustomerPaymentId = transfer.CustomerPaymentId;
            receipt.TransferredToCustomerAdvanceAt = DateTime.UtcNow;
        }

        prospect.BusinessPartnerId = businessPartnerId;
        prospect.BusinessPartnerLinkedAt ??= DateTime.UtcNow;
        prospect.BusinessPartnerLinkedById ??= ActorId;
        prospect.Status = EhcPropertyProspectStatuses.Converted;
        prospect.UpdatedAt = DateTime.UtcNow;
        var lead = await db.Leads.SingleAsync(x => x.Id == prospect.LeadId && x.TenantId == TenantId && !x.IsDeleted, cancellationToken);
        lead.LeadStatus = "Converted";
        lead.ConvertedDate = DateTime.UtcNow;
        // ConvertedCustomerId is the legacy Finance Customer FK, not the canonical BusinessPartner id.
        // The durable BP relationship is EhcPropertyEnquiryProspect.BusinessPartnerId.
        if (prospect.SalesAllocationId.HasValue)
            await allocations.TransferAllocationAsync(prospect.SalesAllocationId.Value, new TransferSalesAllocationDto
            {
                BusinessPartnerId = businessPartnerId,
                LeadId = prospect.LeadId,
                OpportunityId = prospect.OpportunityId,
                ClearLinkedDocuments = false,
                Notes = $"Converted public prospect from enquiry {ticket.TicketNumber}."
            });
        await AddAuditAsync(ticket, "BusinessPartnerLinked", "Qualified prospect linked to Customer Business Partner",
            $"Business Partner {businessPartnerId} was linked after the cleared deposit threshold was met. Cash was not posted a second time; cleared receipts were transferred to customer advances.", cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<EhcPropertyEnquiryProspect> GetOrCreateProspectAsync(EhcTicket ticket, EhcPropertyListingContextDto property, CancellationToken cancellationToken)
    {
        var existing = await ProspectQuery(tracking: true).SingleOrDefaultAsync(x => x.TicketId == ticket.Id, cancellationToken);
        if (existing is not null) return existing;
        Lead lead;
        if (ticket.CrmLeadId.HasValue)
            lead = await db.Leads.SingleAsync(x => x.Id == ticket.CrmLeadId.Value && x.TenantId == TenantId && !x.IsDeleted, cancellationToken);
        else
        {
            var (first, last) = SplitName(property.ContactName ?? property.BusinessPartnerName);
            lead = new Lead
            {
                TenantId = TenantId,
                ReferenceNumber = ticket.TicketNumber,
                Status = "Active",
                EffectiveDate = DateTime.UtcNow,
                FirstName = first,
                LastName = last,
                CompanyName = Clip(property.BusinessPartnerName, 100),
                Email = Clip(property.ContactEmail, 100),
                Phone = Clip(property.ContactPhone, 20),
                LeadSource = "Public Property Listing",
                LeadStatus = "New",
                EstimatedValue = property.Price ?? 0m,
                AssignedToId = ticket.AssignedToUserId,
                Notes = Clip($"Originating EHC ticket: {ticket.TicketNumber}. Property: {property.ListingReference}.", 2000),
                CreatedBy = currentUser.UserName,
                CreatedById = ActorId
            };
            db.Leads.Add(lead);
            ticket.CrmLeadId = lead.Id;
        }
        var prospect = new EhcPropertyEnquiryProspect
        {
            TenantId = TenantId,
            TicketId = ticket.Id,
            LeadId = lead.Id,
            Lead = lead,
            OpportunityId = ticket.CrmOpportunityId,
            BusinessPartnerId = property.BusinessPartnerId,
            BusinessPartnerLinkedAt = property.BusinessPartnerId.HasValue ? ticket.CreatedAt : null,
            Status = EhcPropertyProspectStatuses.New,
            AgreedAmount = property.Price ?? 0m,
            Currency = Currency(property.Currency),
            CreatedBy = currentUser.UserName,
            CreatedById = ActorId
        };
        db.Set<EhcPropertyEnquiryProspect>().Add(prospect);
        return prospect;
    }

    private async Task<(EhcTicket Ticket, EhcPropertyListingContextDto Property)> LoadTicketAsync(Guid ticketId, CancellationToken cancellationToken)
    {
        var ticket = await db.EhcTickets.Include(x => x.AssignedOrganizationUnit).SingleOrDefaultAsync(x => x.Id == ticketId
            && x.TenantId == TenantId && !x.IsDeleted && x.PropertyListingContextJson != null, cancellationToken)
            ?? throw new KeyNotFoundException("Property enquiry not found.");
        if (ticket.AssignedOrganizationUnit is null || !ticket.AssignedOrganizationUnit.IsActive
            || !SalesOrganizationUnitCodes.Contains(ticket.AssignedOrganizationUnit.Code))
            throw new InvalidOperationException("Assign the property enquiry to Sales before managing the prospect.");
        EhcPropertyListingContextDto? property;
        try { property = JsonSerializer.Deserialize<EhcPropertyListingContextDto>(ticket.PropertyListingContextJson!); }
        catch (JsonException) { throw new InvalidOperationException("The saved property enquiry context is invalid."); }
        return (ticket, property ?? throw new InvalidOperationException("The property enquiry context is missing."));
    }

    private async Task<SalesSaleableSource> ResolveSaleableSourceAsync(EhcPropertyListingContextDto property, CancellationToken cancellationToken)
        => (await ResolveSaleableSourceSelectionAsync(property, cancellationToken)).Source;

    private async Task<(SalesSaleableSource Source, string SourceItemId)> ResolveSaleableSourceSelectionAsync(
        EhcPropertyListingContextDto property,
        CancellationToken cancellationToken)
    {
        var asset = await db.EstateManagedAssets.AsNoTracking()
            .Where(item => item.Id == property.ParentAssetId
                && item.TenantId == TenantId
                && !item.IsDeleted)
            .Select(item => new { item.Id, item.AssetType, item.ProjectUnitId })
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException("The property enquiry no longer resolves to an Estate asset.");

        var adapter = asset.AssetType == EstateManagedAssetType.Land
            ? "land-management"
            : "property-register";
        var sourceItemId = asset.AssetType == EstateManagedAssetType.Land
            ? property.DemarcationId?.ToString("D")
                ?? throw new InvalidOperationException("The land enquiry does not identify a demarcated land record.")
            : (asset.ProjectUnitId ?? asset.Id).ToString("D");

        var candidates = await db.SalesSaleableSources.AsNoTracking()
            .Where(item => item.TenantId == TenantId
                && !item.IsDeleted
                && item.IsActive
                && item.AdapterKey == adapter)
            .OrderBy(item => item.SortOrder)
            .ToArrayAsync(cancellationToken);
        var source = asset.AssetType == EstateManagedAssetType.Land
            ? candidates.FirstOrDefault()
            : candidates.FirstOrDefault(candidate => SourceSelectsAssetType(candidate.SettingsJson, asset.AssetType));

        return (source
                ?? throw new InvalidOperationException($"No active Sales saleable source is configured for Estate {asset.AssetType}."),
            sourceItemId);
    }

    private static bool SourceSelectsAssetType(string? settingsJson, EstateManagedAssetType assetType)
    {
        if (string.IsNullOrWhiteSpace(settingsJson)) return false;
        try
        {
            using var document = JsonDocument.Parse(settingsJson);
            if (!document.RootElement.TryGetProperty("filters", out var filters)
                || filters.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            foreach (var filter in filters.EnumerateArray())
            {
                if (!filter.TryGetProperty("field", out var field)
                    || !filter.TryGetProperty("value", out var value)
                    || field.ValueKind != JsonValueKind.String
                    || value.ValueKind != JsonValueKind.String)
                {
                    continue;
                }

                var normalizedField = field.GetString()?.Replace("_", string.Empty).Replace("-", string.Empty);
                if (normalizedField?.Equals("assetType", StringComparison.OrdinalIgnoreCase) == true
                    && value.GetString()?.Equals(assetType.ToString(), StringComparison.OrdinalIgnoreCase) == true)
                {
                    return true;
                }
            }
        }
        catch (JsonException)
        {
            return false;
        }

        return false;
    }

    private static bool ProspectUsesSource(string? propertyContextJson, SalesSaleableSource source)
    {
        if (string.IsNullOrWhiteSpace(propertyContextJson)) return false;
        EhcPropertyListingContextDto? property;
        try { property = JsonSerializer.Deserialize<EhcPropertyListingContextDto>(propertyContextJson); }
        catch (JsonException) { return false; }
        if (property is null || string.IsNullOrWhiteSpace(property.Source)) return false;

        var adapter = property.DemarcationId.HasValue
            ? "land-management"
            : "property-register";
        return string.Equals(source.AdapterKey, adapter, StringComparison.OrdinalIgnoreCase)
            || string.Equals(source.Code, adapter, StringComparison.OrdinalIgnoreCase)
            || string.Equals(source.SourceType, adapter, StringComparison.OrdinalIgnoreCase);
    }

    private static string DescribeThreshold(string requirementType, decimal? fixedAmount, decimal? percentage) =>
        requirementType switch
        {
            ProspectDepositRequirementTypes.Fixed => $"fixed amount {(fixedAmount ?? 0m):0.00}",
            ProspectDepositRequirementTypes.Percentage => $"{(percentage ?? 0m):0.####}% of agreed value",
            ProspectDepositRequirementTypes.Full => "full agreed value",
            _ => requirementType
        };

    private IQueryable<EhcPropertyEnquiryProspect> ProspectQuery(bool tracking = false)
    {
        var query = db.Set<EhcPropertyEnquiryProspect>().Where(x => x.TenantId == TenantId && !x.IsDeleted);
        return tracking ? query : query.AsNoTracking();
    }

    private async Task<PropertyEnquiryProspectDto> ToDtoAsync(EhcPropertyEnquiryProspect prospect, CancellationToken cancellationToken)
    {
        var required = RequiredDeposit(prospect);
        var cleared = await ClearedDepositAsync(prospect.Id, cancellationToken);
        var allocation = prospect.SalesAllocationId.HasValue
            ? await db.SalesAllocations.AsNoTracking()
                .Where(x => x.TenantId == TenantId
                    && x.Id == prospect.SalesAllocationId.Value
                    && !x.IsDeleted)
                .Select(x => new { x.Status, x.ReservedUntil })
                .SingleOrDefaultAsync(cancellationToken)
            : null;
        var partner = prospect.BusinessPartnerId.HasValue
            ? await db.BusinessPartners.AsNoTracking()
                .Where(x => x.TenantId == TenantId && x.Id == prospect.BusinessPartnerId.Value && !x.IsDeleted)
                .Select(x => new { x.PartnerCode, x.PartnerName })
                .SingleOrDefaultAsync(cancellationToken)
            : null;
        return new(prospect.TicketId, prospect.LeadId, prospect.OpportunityId, prospect.SalesAllocationId,
            prospect.BusinessPartnerId, partner?.PartnerCode, partner?.PartnerName,
            prospect.Status, prospect.AgreedAmount, prospect.Currency,
            prospect.DepositRequirementType, required, cleared, cleared >= required,
            prospect.QualifiedAt, prospect.BusinessPartnerLinkedAt)
        {
            SalesAllocationStatus = allocation?.Status,
            SalesAllocationReservedUntil = allocation?.ReservedUntil
        };
    }

    private async Task<decimal> ClearedDepositAsync(Guid prospectId, CancellationToken cancellationToken) =>
        await db.Set<ProspectDepositReceipt>().AsNoTracking().Where(x => x.TenantId == TenantId && x.ProspectId == prospectId
            && !x.IsDeleted && x.Status == ProspectDepositReceiptStatuses.Cleared && !x.ReversedAt.HasValue)
            .SumAsync(x => (decimal?)x.Amount, cancellationToken) ?? 0m;

    public static decimal RequiredDeposit(EhcPropertyEnquiryProspect prospect) => prospect.DepositRequirementType switch
    {
        ProspectDepositRequirementTypes.Fixed => prospect.FixedDepositAmount ?? 0m,
        ProspectDepositRequirementTypes.Percentage => Math.Round(prospect.AgreedAmount * (prospect.DepositPercentage ?? 0m) / 100m, 2, MidpointRounding.AwayFromZero),
        ProspectDepositRequirementTypes.Full => prospect.AgreedAmount,
        _ => throw new InvalidOperationException("Unsupported prospect deposit requirement.")
    };

    private async Task AddAuditAsync(EhcTicket ticket, string eventType, string title, string body, CancellationToken cancellationToken)
    {
        db.EhcTicketAuditEvents.Add(new EhcTicketAuditEvent
        {
            TenantId = TenantId, TicketId = ticket.Id, EventType = eventType, Title = title, Body = body,
            IsInternal = true, ActorUserId = ActorId, CreatedAt = DateTime.UtcNow,
            CreatedBy = currentUser.UserName, CreatedById = ActorId
        });
        await Task.CompletedTask;
    }

    private static ProspectDepositReceiptDto ToReceiptDto(ProspectDepositReceipt x) => new(x.Id, x.ReceiptNumber,
        x.Amount, x.Currency, x.PaymentMethod, x.TransactionReference, x.Status, x.ReceivedAt, x.ClearedAt,
        x.ReversedAt, x.PostingEventId, x.JournalEntryId, x.CustomerAdvanceTransferPostingEventId, x.CustomerPaymentId);

    private static void ValidatePolicy(UpsertPropertyProspectDepositPolicyRequest request)
    {
        if (request.DepositLiabilityAccountId == Guid.Empty) throw new InvalidOperationException("Select the prospect-deposit liability account.");
        if (!request.DefaultBankAccountId.HasValue && !request.DefaultLiquidityAccountId.HasValue)
            throw new InvalidOperationException("Select a default bank or liquidity account.");
        if (request.DefaultBankAccountId.HasValue && request.DefaultLiquidityAccountId.HasValue)
            throw new InvalidOperationException("Select either a bank account or a liquidity account, not both.");
        if (request.RequirementType == ProspectDepositRequirementTypes.Fixed && request.FixedAmount is not > 0)
            throw new InvalidOperationException("Enter a positive fixed deposit amount.");
        if (request.RequirementType == ProspectDepositRequirementTypes.Percentage && request.Percentage is not (> 0 and <= 100))
            throw new InvalidOperationException("Deposit percentage must be greater than zero and no more than 100.");
    }

    private Guid TenantId => currentUser.TenantId is { } value && value != Guid.Empty ? value : throw new UnauthorizedAccessException("Tenant context is required.");
    private Guid? ActorId => Guid.TryParse(currentUser.UserId, out var value) ? value : null;
    private static string Currency(string? value) => string.IsNullOrWhiteSpace(value) ? "GHS" : value.Trim().ToUpperInvariant();
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static string? NormalizePhone(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var normalized = new string(value.Where(char.IsDigit).ToArray());
        return normalized.Length == 0 ? null : normalized;
    }
    private static string AppendNote(string? current, string next) => string.IsNullOrWhiteSpace(current) ? next.Trim() : $"{current}\n{next.Trim()}";
    private static string? Clip(string? value, int length) => string.IsNullOrWhiteSpace(value) ? null : value.Trim()[..Math.Min(value.Trim().Length, length)];
    private static (string First, string Last) SplitName(string value)
    {
        var parts = value.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length switch { 0 => ("Property", "Prospect"), 1 => (Clip(parts[0], 100)!, "Prospect"), _ => (Clip(parts[0], 100)!, Clip(string.Join(' ', parts.Skip(1)), 100)!) };
    }
}
