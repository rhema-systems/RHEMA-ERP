using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.Crm;
using ErpSystem.Core.DTOs.Ehc;
using ErpSystem.Core.Entities.Ehc;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Crm;
using ErpSystem.Core.Interfaces.Ehc;
using ErpSystem.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Controllers.Crm;

[ApiController]
[Route("api/crm")]
[Authorize]
public class CrmController : ControllerBase
{
    private readonly ICrmService _crmService;

    public CrmController(ICrmService crmService)
    {
        _crmService = crmService;
    }

    [HttpGet("overview")]
    public async Task<ActionResult<CrmOverviewDto>> GetOverview([FromQuery] int take = 10)
    {
        var overview = await _crmService.GetOverviewAsync(take);
        return Ok(overview);
    }

    [HttpGet("accounts")]
    public async Task<ActionResult<PagedResult<CrmAccountOverviewDto>>> GetAccounts(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] string? search = null,
        [FromQuery] string? healthCategory = null,
        [FromQuery] bool atRiskOnly = false,
        [FromQuery] string? partnerType = null)
    {
        var accounts = await _crmService.GetAccountsAsync(page, pageSize, search, healthCategory, atRiskOnly, partnerType);
        return Ok(accounts);
    }

    [HttpGet("reports")]
    public async Task<ActionResult<CrmReportingDto>> GetReporting([FromQuery] int take = 10)
    {
        var reporting = await _crmService.GetReportingAsync(take);
        return Ok(reporting);
    }

    [HttpGet("forecast")]
    public async Task<ActionResult<CrmForecastDto>> GetForecast(
        [FromQuery] int months = 6,
        [FromQuery] string? opportunityType = null,
        [FromQuery] Guid? businessPartnerId = null)
    {
        var forecast = await _crmService.GetForecastAsync(months, opportunityType, businessPartnerId);
        return Ok(forecast);
    }

    [HttpGet("conversions")]
    public async Task<ActionResult<CrmConversionsDto>> GetConversions(
        [FromQuery] int months = 6,
        [FromQuery] string? opportunityType = null,
        [FromQuery] Guid? businessPartnerId = null)
    {
        var conversions = await _crmService.GetConversionsAsync(months, opportunityType, businessPartnerId);
        return Ok(conversions);
    }

    [HttpGet("accounts/{businessPartnerId:guid}")]
    public async Task<ActionResult<CrmAccountDetailDto>> GetAccountDetail(Guid businessPartnerId, [FromQuery] int take = 10)
    {
        var account = await _crmService.GetAccountDetailAsync(businessPartnerId, take);
        return account == null ? NotFound() : Ok(account);
    }

    [HttpGet("contacts")]
    public async Task<ActionResult<PagedResult<CrmContactListItemDto>>> GetContacts(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] string? search = null,
        [FromQuery] Guid? businessPartnerId = null,
        [FromQuery] string? department = null,
        [FromQuery] bool primaryOnly = false,
        [FromQuery] bool atRiskOnly = false,
        [FromQuery] string? partnerType = null)
    {
        var contacts = await _crmService.GetContactsAsync(
            page,
            pageSize,
            search,
            businessPartnerId,
            department,
            primaryOnly,
            atRiskOnly,
            partnerType);

        return Ok(contacts);
    }

    [HttpGet("readiness")]
    public async Task<ActionResult<PagedResult<CrmReadinessListItemDto>>> GetReadiness(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] string? search = null,
        [FromQuery] string? readinessCategory = null,
        [FromQuery] bool expiringOnly = false,
        [FromQuery] bool missingFinancialsOnly = false,
        [FromQuery] string? partnerType = null)
    {
        var readiness = await _crmService.GetReadinessAsync(
            page,
            pageSize,
            search,
            readinessCategory,
            expiringOnly,
            missingFinancialsOnly,
            partnerType);

        return Ok(readiness);
    }

    [HttpGet("readiness/{businessPartnerId:guid}")]
    public async Task<ActionResult<CrmReadinessDetailDto>> GetReadinessDetail(Guid businessPartnerId, [FromQuery] int take = 10)
    {
        var readiness = await _crmService.GetReadinessDetailAsync(businessPartnerId, take);
        return readiness == null ? NotFound() : Ok(readiness);
    }

    [HttpGet("risk")]
    public async Task<ActionResult<PagedResult<CrmRiskListItemDto>>> GetRisk(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] string? search = null,
        [FromQuery] string? riskCategory = null,
        [FromQuery] bool escalationOnly = false,
        [FromQuery] bool openIncidentOnly = false,
        [FromQuery] string? partnerType = null)
    {
        var risk = await _crmService.GetRiskAsync(
            page,
            pageSize,
            search,
            riskCategory,
            escalationOnly,
            openIncidentOnly,
            partnerType);

        return Ok(risk);
    }

    [HttpGet("risk/{businessPartnerId:guid}")]
    public async Task<ActionResult<CrmRiskDetailDto>> GetRiskDetail(Guid businessPartnerId, [FromQuery] int take = 10)
    {
        var risk = await _crmService.GetRiskDetailAsync(businessPartnerId, take);
        return risk == null ? NotFound() : Ok(risk);
    }

    [HttpGet("collaboration")]
    public async Task<ActionResult<PagedResult<CrmCollaborationListItemDto>>> GetCollaboration(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] string? search = null,
        [FromQuery] string? collaborationCategory = null,
        [FromQuery] bool enablementOnly = false,
        [FromQuery] bool pendingOnboardingOnly = false,
        [FromQuery] string? partnerType = null)
    {
        var collaboration = await _crmService.GetCollaborationAsync(
            page,
            pageSize,
            search,
            collaborationCategory,
            enablementOnly,
            pendingOnboardingOnly,
            partnerType);

        return Ok(collaboration);
    }

    [HttpGet("collaboration/{businessPartnerId:guid}")]
    public async Task<ActionResult<CrmCollaborationDetailDto>> GetCollaborationDetail(Guid businessPartnerId, [FromQuery] int take = 10)
    {
        var collaboration = await _crmService.GetCollaborationDetailAsync(businessPartnerId, take);
        return collaboration == null ? NotFound() : Ok(collaboration);
    }

    [HttpGet("service")]
    public async Task<ActionResult<PagedResult<CrmServiceListItemDto>>> GetService(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] string? search = null,
        [FromQuery] string? serviceCategory = null,
        [FromQuery] bool attentionOnly = false,
        [FromQuery] bool overdueOnly = false,
        [FromQuery] string? partnerType = null)
    {
        var service = await _crmService.GetServiceAsync(
            page,
            pageSize,
            search,
            serviceCategory,
            attentionOnly,
            overdueOnly,
            partnerType);

        return Ok(service);
    }

    [HttpGet("service/{businessPartnerId:guid}")]
    public async Task<ActionResult<CrmServiceDetailDto>> GetServiceDetail(Guid businessPartnerId, [FromQuery] int take = 10)
    {
        var service = await _crmService.GetServiceDetailAsync(businessPartnerId, take);
        return service == null ? NotFound() : Ok(service);
    }

    [HttpGet("campaigns")]
    public async Task<ActionResult<PagedResult<CrmCampaignListItemDto>>> GetCampaigns(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] string? search = null,
        [FromQuery] string? status = null,
        [FromQuery] string? campaignType = null,
        [FromQuery] bool activeOnly = false,
        [FromQuery] Guid? businessPartnerId = null,
        [FromQuery] Guid? leadId = null)
    {
        var campaigns = await _crmService.GetCampaignsAsync(
            page,
            pageSize,
            search,
            status,
            campaignType,
            activeOnly,
            businessPartnerId,
            leadId);

        return Ok(campaigns);
    }

    [HttpGet("campaigns/{campaignId:guid}")]
    public async Task<ActionResult<CrmCampaignDetailDto>> GetCampaign(Guid campaignId)
    {
        var campaign = await _crmService.GetCampaignByIdAsync(campaignId);
        return campaign == null ? NotFound() : Ok(campaign);
    }

    [HttpPost("campaigns")]
    public async Task<ActionResult<CrmCampaignDetailDto>> CreateCampaign([FromBody] CreateCrmCampaignDto dto)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var campaign = await _crmService.CreateCampaignAsync(dto);
        return CreatedAtAction(nameof(GetCampaign), new { campaignId = campaign.CampaignId }, campaign);
    }

    [HttpPut("campaigns/{campaignId:guid}")]
    public async Task<ActionResult<CrmCampaignDetailDto>> UpdateCampaign(Guid campaignId, [FromBody] UpdateCrmCampaignDto dto)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var existing = await _crmService.GetCampaignByIdAsync(campaignId);
        if (existing == null)
        {
            return NotFound();
        }

        var campaign = await _crmService.UpdateCampaignAsync(campaignId, dto);
        return Ok(campaign);
    }

    [HttpDelete("campaigns/{campaignId:guid}")]
    public async Task<IActionResult> DeleteCampaign(Guid campaignId)
    {
        var existing = await _crmService.GetCampaignByIdAsync(campaignId);
        if (existing == null)
        {
            return NotFound();
        }

        await _crmService.DeleteCampaignAsync(campaignId);
        return NoContent();
    }

    [HttpPost("campaigns/{campaignId:guid}/members")]
    public async Task<ActionResult<CrmCampaignMemberDto>> AddCampaignMember(Guid campaignId, [FromBody] CreateCrmCampaignMemberDto dto)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var existing = await _crmService.GetCampaignByIdAsync(campaignId);
        if (existing == null)
        {
            return NotFound();
        }

        try
        {
            var member = await _crmService.AddCampaignMemberAsync(campaignId, dto);
            return Ok(member);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPut("campaigns/{campaignId:guid}/members/{memberId:guid}")]
    public async Task<ActionResult<CrmCampaignMemberDto>> UpdateCampaignMember(
        Guid campaignId,
        Guid memberId,
        [FromBody] UpdateCrmCampaignMemberDto dto)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var existing = await _crmService.GetCampaignByIdAsync(campaignId);
        if (existing == null)
        {
            return NotFound();
        }

        try
        {
            var member = await _crmService.UpdateCampaignMemberAsync(campaignId, memberId, dto);
            return Ok(member);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpDelete("campaigns/{campaignId:guid}/members/{memberId:guid}")]
    public async Task<IActionResult> DeleteCampaignMember(Guid campaignId, Guid memberId)
    {
        var existing = await _crmService.GetCampaignByIdAsync(campaignId);
        if (existing == null)
        {
            return NotFound();
        }

        try
        {
            await _crmService.DeleteCampaignMemberAsync(campaignId, memberId);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpGet("leads")]
    public async Task<ActionResult<PagedResult<CrmLeadListItemDto>>> GetLeads(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] string? search = null,
        [FromQuery] string? status = null,
        [FromQuery] bool followUpOnly = false)
    {
        var leads = await _crmService.GetLeadsAsync(page, pageSize, search, status, followUpOnly);
        return Ok(leads);
    }

    [HttpGet("leads/{leadId:guid}")]
    public async Task<ActionResult<CrmLeadDetailDto>> GetLead(Guid leadId)
    {
        var lead = await _crmService.GetLeadByIdAsync(leadId);
        return lead == null ? NotFound() : Ok(lead);
    }

    [HttpPost("leads")]
    public async Task<ActionResult<CrmLeadDetailDto>> CreateLead([FromBody] CreateCrmLeadDto dto)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var lead = await _crmService.CreateLeadAsync(dto);
        return CreatedAtAction(nameof(GetLead), new { leadId = lead.LeadId }, lead);
    }

    [HttpPut("leads/{leadId:guid}")]
    public async Task<ActionResult<CrmLeadDetailDto>> UpdateLead(Guid leadId, [FromBody] UpdateCrmLeadDto dto)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var existing = await _crmService.GetLeadByIdAsync(leadId);
        if (existing == null)
        {
            return NotFound();
        }

        var lead = await _crmService.UpdateLeadAsync(leadId, dto);
        return Ok(lead);
    }

    [HttpDelete("leads/{leadId:guid}")]
    public async Task<IActionResult> DeleteLead(Guid leadId)
    {
        var existing = await _crmService.GetLeadByIdAsync(leadId);
        if (existing == null)
        {
            return NotFound();
        }

        try
        {
            await _crmService.DeleteLeadAsync(leadId);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpGet("opportunities")]
    public async Task<ActionResult<PagedResult<CrmOpportunityListItemDto>>> GetOpportunities(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] string? search = null,
        [FromQuery] string? stage = null,
        [FromQuery] Guid? businessPartnerId = null,
        [FromQuery] Guid? leadId = null,
        [FromQuery] string? opportunityType = null)
    {
        var opportunities = await _crmService.GetOpportunitiesAsync(page, pageSize, search, stage, businessPartnerId, leadId, opportunityType);
        return Ok(opportunities);
    }

    [HttpGet("opportunities/{opportunityId:guid}")]
    public async Task<ActionResult<CrmOpportunityDetailDto>> GetOpportunity(Guid opportunityId)
    {
        var opportunity = await _crmService.GetOpportunityByIdAsync(opportunityId);
        return opportunity == null ? NotFound() : Ok(opportunity);
    }

    [HttpPost("opportunities")]
    public async Task<ActionResult<CrmOpportunityDetailDto>> CreateOpportunity([FromBody] CreateCrmOpportunityDto dto)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        try
        {
            var opportunity = await _crmService.CreateOpportunityAsync(dto);
            return CreatedAtAction(nameof(GetOpportunity), new { opportunityId = opportunity.OpportunityId }, opportunity);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPut("opportunities/{opportunityId:guid}")]
    public async Task<ActionResult<CrmOpportunityDetailDto>> UpdateOpportunity(Guid opportunityId, [FromBody] UpdateCrmOpportunityDto dto)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var existing = await _crmService.GetOpportunityByIdAsync(opportunityId);
        if (existing == null)
        {
            return NotFound();
        }

        try
        {
            var opportunity = await _crmService.UpdateOpportunityAsync(opportunityId, dto);
            return Ok(opportunity);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpDelete("opportunities/{opportunityId:guid}")]
    public async Task<IActionResult> DeleteOpportunity(Guid opportunityId)
    {
        var existing = await _crmService.GetOpportunityByIdAsync(opportunityId);
        if (existing == null)
        {
            return NotFound();
        }

        try
        {
            await _crmService.DeleteOpportunityAsync(opportunityId);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpGet("activities")]
    public async Task<ActionResult<PagedResult<CrmActivityListItemDto>>> GetActivities(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] string? search = null,
        [FromQuery] string? status = null,
        [FromQuery] string? activityType = null,
        [FromQuery] bool followUpOnly = false,
        [FromQuery] Guid? businessPartnerId = null,
        [FromQuery] Guid? opportunityId = null,
        [FromQuery] Guid? leadId = null)
    {
        var activities = await _crmService.GetActivitiesAsync(
            page,
            pageSize,
            search,
            status,
            activityType,
            followUpOnly,
            businessPartnerId,
            opportunityId,
            leadId);

        return Ok(activities);
    }

    [HttpGet("activities/{activityId:guid}")]
    public async Task<ActionResult<CrmActivityDetailDto>> GetActivity(
        Guid activityId,
        [FromServices] ApplicationDbContext db,
        [FromServices] ICurrentUserService currentUser,
        CancellationToken cancellationToken)
    {
        if (currentUser.TenantId is not Guid tenantId) return Forbid();
        var activity = await _crmService.GetActivityByIdAsync(activityId);
        if (activity is null) return NotFound();
        await AddPropertyEnquiryContextAsync(activity, db, tenantId, cancellationToken);
        return Ok(activity);
    }

    [HttpPost("activities")]
    public async Task<ActionResult<CrmActivityDetailDto>> CreateActivity(
        [FromBody] CreateCrmActivityDto dto,
        [FromServices] ApplicationDbContext db,
        [FromServices] ICurrentUserService currentUser,
        [FromServices] IPropertyEnquiryProspectService propertyProspects,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        try
        {
            if (currentUser.TenantId is not Guid tenantId) return Forbid();
            PropertyEnquiryTarget? propertyEnquiry = null;
            if (dto.PropertyEnquiryTicketId.HasValue)
            {
                if (!CanManagePropertyEnquiry(User)) return Forbid();
                propertyEnquiry = await FindPropertyEnquiryAsync(
                    db,
                    tenantId,
                    dto.PropertyEnquiryTicketId.Value,
                    cancellationToken);
                if (propertyEnquiry is null)
                    return Problem(
                        statusCode: StatusCodes.Status400BadRequest,
                        title: "Property enquiry is unavailable",
                        detail: "Select a current property enquiry assigned to Sales.");
            }

            await using var transaction = db.Database.IsRelational()
                && db.Database.CurrentTransaction is null
                    ? await db.Database.BeginTransactionAsync(cancellationToken)
                    : null;

            if (propertyEnquiry is not null && IsCompletedContact(dto))
            {
                var existingProspect = await propertyProspects.GetAsync(
                    propertyEnquiry.TicketId,
                    cancellationToken);
                var prospect = existingProspect is null
                    || existingProspect.Status is EhcPropertyProspectStatuses.New
                        or EhcPropertyProspectStatuses.Contacted
                        ? await propertyProspects.RecordContactAsync(
                            propertyEnquiry.TicketId,
                            new RecordPropertyEnquiryContactRequest
                            {
                                Notes = BuildContactNotes(dto)
                            },
                            cancellationToken)
                        : existingProspect;
                dto.LeadId = prospect.LeadId;
            }

            var activity = await _crmService.CreateActivityAsync(dto);
            if (propertyEnquiry is not null)
            {
                var actorId = Guid.TryParse(currentUser.UserId, out var parsedActorId)
                    ? parsedActorId
                    : (Guid?)null;
                db.EhcCrmEngagementLinks.Add(new EhcCrmEngagementLink
                {
                    TenantId = tenantId,
                    TicketId = propertyEnquiry.TicketId,
                    CrmActivityId = activity.ActivityId,
                    SourceKey = $"ManualCrmActivity:{activity.ActivityId:D}",
                    EngagementType = IsCompletedContact(dto)
                        ? "SalesContact"
                        : dto.ActivityType.Trim(),
                    CreatedBy = currentUser.UserName,
                    CreatedById = actorId
                });
                db.EhcTicketAuditEvents.Add(new EhcTicketAuditEvent
                {
                    TenantId = tenantId,
                    TicketId = propertyEnquiry.TicketId,
                    EventType = "CrmActivityLinked",
                    Title = "CRM activity linked to property enquiry",
                    Body = $"{dto.ActivityStatus} {dto.ActivityType} activity '{dto.Subject.Trim()}' was linked to {propertyEnquiry.TicketNumber}.",
                    IsInternal = true,
                    ActorUserId = actorId,
                    CreatedBy = currentUser.UserName,
                    CreatedById = actorId
                });
                await db.SaveChangesAsync(cancellationToken);
                activity.PropertyEnquiryTicketId = propertyEnquiry.TicketId;
                activity.PropertyEnquiryTicketNumber = propertyEnquiry.TicketNumber;
                activity.PropertyEnquirySubject = propertyEnquiry.Subject;
            }

            if (transaction is not null) await transaction.CommitAsync(cancellationToken);
            return CreatedAtAction(nameof(GetActivity), new { activityId = activity.ActivityId }, activity);
        }
        catch (InvalidOperationException ex)
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "CRM activity could not be created",
                detail: ex.Message);
        }
    }

    [HttpPut("activities/{activityId:guid}")]
    public async Task<ActionResult<CrmActivityDetailDto>> UpdateActivity(Guid activityId, [FromBody] UpdateCrmActivityDto dto)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var existing = await _crmService.GetActivityByIdAsync(activityId);
        if (existing == null)
        {
            return NotFound();
        }

        try
        {
            var activity = await _crmService.UpdateActivityAsync(activityId, dto);
            return Ok(activity);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpDelete("activities/{activityId:guid}")]
    public async Task<IActionResult> DeleteActivity(Guid activityId)
    {
        var existing = await _crmService.GetActivityByIdAsync(activityId);
        if (existing == null)
        {
            return NotFound();
        }

        await _crmService.DeleteActivityAsync(activityId);
        return NoContent();
    }

    [HttpGet("quotes")]
    public async Task<ActionResult<PagedResult<CrmQuoteListItemDto>>> GetQuotes(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] string? search = null,
        [FromQuery] string? status = null,
        [FromQuery] Guid? businessPartnerId = null,
        [FromQuery] Guid? opportunityId = null,
        [FromQuery] Guid? leadId = null)
    {
        var quotes = await _crmService.GetQuotesAsync(page, pageSize, search, status, businessPartnerId, opportunityId, leadId);
        return Ok(quotes);
    }

    [HttpGet("quotes/{quoteId:guid}")]
    public async Task<ActionResult<CrmQuoteDetailDto>> GetQuote(Guid quoteId)
    {
        var quote = await _crmService.GetQuoteByIdAsync(quoteId);
        return quote == null ? NotFound() : Ok(quote);
    }

    [HttpGet("projects")]
    public async Task<ActionResult<PagedResult<CrmProjectListItemDto>>> GetProjects(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] string? search = null,
        [FromQuery] string? status = null,
        [FromQuery] Guid? businessPartnerId = null,
        [FromQuery] Guid? contractId = null)
    {
        var projects = await _crmService.GetProjectsAsync(page, pageSize, search, status, businessPartnerId, contractId);
        return Ok(projects);
    }

    [HttpGet("projects/{projectId:guid}")]
    public async Task<ActionResult<CrmProjectDetailDto>> GetProject(Guid projectId)
    {
        var project = await _crmService.GetProjectByIdAsync(projectId);
        return project == null ? NotFound() : Ok(project);
    }

    [HttpGet("contracts")]
    public async Task<ActionResult<PagedResult<CrmContractListItemDto>>> GetContracts(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] string? search = null,
        [FromQuery] string? status = null,
        [FromQuery] Guid? businessPartnerId = null,
        [FromQuery] bool expiringOnly = false)
    {
        var contracts = await _crmService.GetContractsAsync(page, pageSize, search, status, businessPartnerId, expiringOnly);
        return Ok(contracts);
    }

    [HttpGet("contracts/{contractId:guid}")]
    public async Task<ActionResult<CrmContractDetailDto>> GetContract(Guid contractId)
    {
        var contract = await _crmService.GetContractByIdAsync(contractId);
        return contract == null ? NotFound() : Ok(contract);
    }

    [HttpGet("tenders")]
    public async Task<ActionResult<PagedResult<CrmTenderListItemDto>>> GetTenders(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] string? search = null,
        [FromQuery] string? entityType = null,
        [FromQuery] string? status = null,
        [FromQuery] Guid? businessPartnerId = null,
        [FromQuery] Guid? tenderId = null)
    {
        var tenders = await _crmService.GetTendersAsync(page, pageSize, search, entityType, status, businessPartnerId, tenderId);
        return Ok(tenders);
    }

    [HttpGet("tenders/{entityType}/{entityId:guid}")]
    public async Task<ActionResult<CrmTenderDetailDto>> GetTender(string entityType, Guid entityId)
    {
        var tender = await _crmService.GetTenderByEntityAsync(entityType, entityId);
        return tender == null ? NotFound() : Ok(tender);
    }

    private static bool CanManagePropertyEnquiry(System.Security.Claims.ClaimsPrincipal user) =>
        user.IsInRole("Sales User")
        || user.IsInRole("Sales Officer")
        || user.IsInRole("Sales Manager")
        || user.IsInRole("TenantAdmin")
        || user.IsInRole("SuperAdmin");

    private static bool IsCompletedContact(CrmActivityUpsertDto dto) =>
        string.Equals(dto.ActivityStatus, "Completed", StringComparison.OrdinalIgnoreCase)
        && (string.Equals(dto.ActivityType, "Call", StringComparison.OrdinalIgnoreCase)
            || string.Equals(dto.ActivityType, "Meeting", StringComparison.OrdinalIgnoreCase)
            || string.Equals(dto.ActivityType, "Email", StringComparison.OrdinalIgnoreCase));

    private static string BuildContactNotes(CrmActivityUpsertDto dto)
    {
        var details = new[] { dto.Description, dto.Outcome, dto.Notes }
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!.Trim());
        var result = string.Join(" ", details);
        if (string.IsNullOrWhiteSpace(result)) result = dto.Subject.Trim();
        return result.Length <= 2000 ? result : result[..2000];
    }

    private static async Task<PropertyEnquiryTarget?> FindPropertyEnquiryAsync(
        ApplicationDbContext db,
        Guid tenantId,
        Guid ticketId,
        CancellationToken cancellationToken) =>
        await db.EhcTickets.AsNoTracking()
            .Where(ticket => ticket.Id == ticketId
                && ticket.TenantId == tenantId
                && !ticket.IsDeleted
                && ticket.TicketType == EhcTicketType.Enquiry
                && ticket.Status != EhcTicketStatus.New
                && ticket.PropertyListingContextJson != null
                && ticket.AssignedOrganizationUnit != null
                && !ticket.AssignedOrganizationUnit.IsDeleted
                && ticket.AssignedOrganizationUnit.IsActive
                && (ticket.AssignedOrganizationUnit.Code == "DEPT-SALES"
                    || ticket.AssignedOrganizationUnit.Code == "UNIT-MKT"))
            .Select(ticket => new PropertyEnquiryTarget(
                ticket.Id,
                ticket.TicketNumber,
                ticket.Subject))
            .SingleOrDefaultAsync(cancellationToken);

    private static async Task AddPropertyEnquiryContextAsync(
        CrmActivityDetailDto activity,
        ApplicationDbContext db,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var context = await db.EhcCrmEngagementLinks.AsNoTracking()
            .Where(link => link.TenantId == tenantId
                && !link.IsDeleted
                && link.CrmActivityId == activity.ActivityId
                && !link.Ticket.IsDeleted)
            .Select(link => new PropertyEnquiryTarget(
                link.TicketId,
                link.Ticket.TicketNumber,
                link.Ticket.Subject))
            .SingleOrDefaultAsync(cancellationToken);
        if (context is null) return;
        activity.PropertyEnquiryTicketId = context.TicketId;
        activity.PropertyEnquiryTicketNumber = context.TicketNumber;
        activity.PropertyEnquirySubject = context.Subject;
    }

    private sealed record PropertyEnquiryTarget(
        Guid TicketId,
        string TicketNumber,
        string? Subject);
}
