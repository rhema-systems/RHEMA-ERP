using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.Crm;

namespace ErpSystem.Core.Interfaces.Crm;

public interface ICrmService
{
    Task<CrmOverviewDto> GetOverviewAsync(int take = 10);
    Task<PagedResult<CrmAccountOverviewDto>> GetAccountsAsync(
        int page = 1,
        int pageSize = 25,
        string? search = null,
        string? healthCategory = null,
        bool atRiskOnly = false,
        string? partnerType = null);
    Task<CrmAccountDetailDto?> GetAccountDetailAsync(Guid businessPartnerId, int take = 10);
    Task<PagedResult<CrmContactListItemDto>> GetContactsAsync(
        int page = 1,
        int pageSize = 25,
        string? search = null,
        Guid? businessPartnerId = null,
        string? department = null,
        bool primaryOnly = false,
        bool atRiskOnly = false,
        string? partnerType = null);
    Task<PagedResult<CrmReadinessListItemDto>> GetReadinessAsync(
        int page = 1,
        int pageSize = 25,
        string? search = null,
        string? readinessCategory = null,
        bool expiringOnly = false,
        bool missingFinancialsOnly = false,
        string? partnerType = null);
    Task<CrmReadinessDetailDto?> GetReadinessDetailAsync(Guid businessPartnerId, int take = 10);
    Task<PagedResult<CrmRiskListItemDto>> GetRiskAsync(
        int page = 1,
        int pageSize = 25,
        string? search = null,
        string? riskCategory = null,
        bool escalationOnly = false,
        bool openIncidentOnly = false,
        string? partnerType = null);
    Task<CrmRiskDetailDto?> GetRiskDetailAsync(Guid businessPartnerId, int take = 10);
    Task<PagedResult<CrmCollaborationListItemDto>> GetCollaborationAsync(
        int page = 1,
        int pageSize = 25,
        string? search = null,
        string? collaborationCategory = null,
        bool enablementOnly = false,
        bool pendingOnboardingOnly = false,
        string? partnerType = null);
    Task<CrmCollaborationDetailDto?> GetCollaborationDetailAsync(Guid businessPartnerId, int take = 10);
    Task<PagedResult<CrmServiceListItemDto>> GetServiceAsync(
        int page = 1,
        int pageSize = 25,
        string? search = null,
        string? serviceCategory = null,
        bool attentionOnly = false,
        bool overdueOnly = false,
        string? partnerType = null);
    Task<CrmServiceDetailDto?> GetServiceDetailAsync(Guid businessPartnerId, int take = 10);
    Task<PagedResult<CrmCampaignListItemDto>> GetCampaignsAsync(
        int page = 1,
        int pageSize = 25,
        string? search = null,
        string? status = null,
        string? campaignType = null,
        bool activeOnly = false,
        Guid? businessPartnerId = null,
        Guid? leadId = null);
    Task<CrmCampaignDetailDto?> GetCampaignByIdAsync(Guid campaignId);
    Task<CrmCampaignDetailDto> CreateCampaignAsync(CreateCrmCampaignDto dto);
    Task<CrmCampaignDetailDto> UpdateCampaignAsync(Guid campaignId, UpdateCrmCampaignDto dto);
    Task DeleteCampaignAsync(Guid campaignId);
    Task<CrmCampaignMemberDto> AddCampaignMemberAsync(Guid campaignId, CreateCrmCampaignMemberDto dto);
    Task<CrmCampaignMemberDto> UpdateCampaignMemberAsync(Guid campaignId, Guid memberId, UpdateCrmCampaignMemberDto dto);
    Task DeleteCampaignMemberAsync(Guid campaignId, Guid memberId);

    Task<PagedResult<CrmLeadListItemDto>> GetLeadsAsync(
        int page = 1,
        int pageSize = 25,
        string? search = null,
        string? status = null,
        bool followUpOnly = false);

    Task<CrmLeadDetailDto?> GetLeadByIdAsync(Guid leadId);
    Task<CrmLeadDetailDto> CreateLeadAsync(CreateCrmLeadDto dto);
    Task<CrmLeadDetailDto> UpdateLeadAsync(Guid leadId, UpdateCrmLeadDto dto);
    Task DeleteLeadAsync(Guid leadId);

    Task<PagedResult<CrmOpportunityListItemDto>> GetOpportunitiesAsync(
        int page = 1,
        int pageSize = 25,
        string? search = null,
        string? stage = null,
        Guid? businessPartnerId = null,
        Guid? leadId = null,
        string? opportunityType = null,
        Guid? stageDefinitionId = null,
        Guid? reachedStageDefinitionId = null,
        DateTime? stageEnteredFrom = null,
        DateTime? stageEnteredTo = null);

    Task<IReadOnlyList<CrmOpportunityStageDefinitionDto>> GetOpportunityStagesAsync(bool includeInactive = false);
    Task<IReadOnlyList<CrmOpportunityStageDefinitionDto>> UpdateOpportunityStagesAsync(UpdateCrmOpportunityStagesDto dto);

    Task<CrmOpportunityDetailDto?> GetOpportunityByIdAsync(Guid opportunityId);
    Task<CrmOpportunityDetailDto> CreateOpportunityAsync(CreateCrmOpportunityDto dto);
    Task<CrmOpportunityDetailDto> UpdateOpportunityAsync(Guid opportunityId, UpdateCrmOpportunityDto dto);
    Task DeleteOpportunityAsync(Guid opportunityId);

    Task<PagedResult<CrmActivityListItemDto>> GetActivitiesAsync(
        int page = 1,
        int pageSize = 25,
        string? search = null,
        string? status = null,
        string? activityType = null,
        bool followUpOnly = false,
        Guid? businessPartnerId = null,
        Guid? opportunityId = null,
        Guid? leadId = null);

    Task<CrmActivityDetailDto?> GetActivityByIdAsync(Guid activityId);
    Task<CrmActivityDetailDto> CreateActivityAsync(CreateCrmActivityDto dto);
    Task<CrmActivityDetailDto> UpdateActivityAsync(Guid activityId, UpdateCrmActivityDto dto);
    Task DeleteActivityAsync(Guid activityId);

    Task<PagedResult<CrmQuoteListItemDto>> GetQuotesAsync(
        int page = 1,
        int pageSize = 25,
        string? search = null,
        string? status = null,
        Guid? businessPartnerId = null,
        Guid? opportunityId = null,
        Guid? leadId = null);

    Task<CrmQuoteDetailDto?> GetQuoteByIdAsync(Guid quoteId);
    Task<PagedResult<CrmProjectListItemDto>> GetProjectsAsync(
        int page = 1,
        int pageSize = 25,
        string? search = null,
        string? status = null,
        Guid? businessPartnerId = null,
        Guid? contractId = null);

    Task<CrmProjectDetailDto?> GetProjectByIdAsync(Guid projectId);
    Task<PagedResult<CrmContractListItemDto>> GetContractsAsync(
        int page = 1,
        int pageSize = 25,
        string? search = null,
        string? status = null,
        Guid? businessPartnerId = null,
        bool expiringOnly = false);

    Task<CrmContractDetailDto?> GetContractByIdAsync(Guid contractId);
    Task<PagedResult<CrmTenderListItemDto>> GetTendersAsync(
        int page = 1,
        int pageSize = 25,
        string? search = null,
        string? entityType = null,
        string? status = null,
        Guid? businessPartnerId = null,
        Guid? tenderId = null);

    Task<CrmTenderDetailDto?> GetTenderByEntityAsync(string entityType, Guid entityId);
    Task<CrmReportingDto> GetReportingAsync(int take = 10);
    Task<CrmForecastDto> GetForecastAsync(
        int months = 6,
        string? opportunityType = null,
        Guid? businessPartnerId = null);
    Task<CrmConversionsDto> GetConversionsAsync(
        int months = 6,
        string? opportunityType = null,
        Guid? businessPartnerId = null);
}
