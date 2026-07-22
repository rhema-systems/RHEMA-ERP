using System.ComponentModel.DataAnnotations;

namespace ErpSystem.Core.DTOs.Crm;

public class CrmOverviewDto
{
    public int TotalLeadCount { get; set; }
    public int QualifiedLeadCount { get; set; }
    public int LeadsNeedingFollowUpCount { get; set; }
    public int OpenOpportunityCount { get; set; }
    public decimal OpenOpportunityValue { get; set; }
    public decimal WeightedPipelineValue { get; set; }
    public int ActiveQuoteCount { get; set; }
    public decimal ActiveQuoteValue { get; set; }
    public int ActiveAccountCount { get; set; }
    public int AtRiskAccountCount { get; set; }
    public decimal AverageAccountHealthScore { get; set; }
    public int ActiveProjectCount { get; set; }
    public int ActiveContractCount { get; set; }
    public int ExpiringContractCount { get; set; }
    public List<CrmAccountOverviewDto> Accounts { get; set; } = new();
    public List<CrmOpportunityOverviewDto> Opportunities { get; set; } = new();
    public List<CrmFollowUpOverviewDto> FollowUps { get; set; } = new();
}

public class CrmAccountOverviewDto
{
    public Guid BusinessPartnerId { get; set; }
    public string PartnerCode { get; set; } = string.Empty;
    public string PartnerName { get; set; } = string.Empty;
    public string PartnerType { get; set; } = string.Empty;
    public string RegistrationStatus { get; set; } = string.Empty;
    public string? CustomerType { get; set; }
    public string? SalesTerritory { get; set; }
    public string? RiskLevel { get; set; }
    public decimal? PerformanceRating { get; set; }
    public int RelatedLeadCount { get; set; }
    public int OpenOpportunityCount { get; set; }
    public decimal OpenOpportunityValue { get; set; }
    public decimal WeightedPipelineValue { get; set; }
    public int ActiveQuoteCount { get; set; }
    public decimal ActiveQuoteValue { get; set; }
    public int TotalProjectCount { get; set; }
    public int ActiveProjectCount { get; set; }
    public decimal ProjectValue { get; set; }
    public int TotalContractCount { get; set; }
    public int ActiveContractCount { get; set; }
    public decimal ContractValue { get; set; }
    public int TenderInvitationCount { get; set; }
    public int TenderBidCount { get; set; }
    public int TenderAwardCount { get; set; }
    public decimal TenderAwardedValue { get; set; }
    public bool HasOpenFollowUp { get; set; }
    public bool IsAtRisk { get; set; }
    public int HealthScore { get; set; }
    public string HealthCategory { get; set; } = string.Empty;
    public DateTime? NextMilestoneDate { get; set; }
}

public class CrmAccountDetailDto
{
    public Guid BusinessPartnerId { get; set; }
    public string PartnerCode { get; set; } = string.Empty;
    public string PartnerName { get; set; } = string.Empty;
    public string PartnerType { get; set; } = string.Empty;
    public string RegistrationStatus { get; set; } = string.Empty;
    public string? CustomerType { get; set; }
    public string? SalesTerritory { get; set; }
    public string? RiskLevel { get; set; }
    public decimal? PerformanceRating { get; set; }
    public string? PrimaryContactName { get; set; }
    public string? PrimaryContactTitle { get; set; }
    public string? PrimaryEmail { get; set; }
    public string? PrimaryPhone { get; set; }
    public string? Website { get; set; }
    public string? PhysicalAddress { get; set; }
    public string? PhysicalCity { get; set; }
    public string? PhysicalCountry { get; set; }
    public string? PaymentTerms { get; set; }
    public string? Currency { get; set; }
    public decimal? CreditLimit { get; set; }
    public decimal? OutstandingBalance { get; set; }
    public bool IsOnCreditHold { get; set; }
    public string? CreditHoldReason { get; set; }
    public DateTime? CustomerSince { get; set; }
    public string? Notes { get; set; }
    public bool HasOpenFollowUp { get; set; }
    public bool IsAtRisk { get; set; }
    public int HealthScore { get; set; }
    public string HealthCategory { get; set; } = string.Empty;
    public DateTime? NextMilestoneDate { get; set; }
    public int RelatedLeadCount { get; set; }
    public int OpenOpportunityCount { get; set; }
    public decimal OpenOpportunityValue { get; set; }
    public decimal WeightedPipelineValue { get; set; }
    public int ActiveQuoteCount { get; set; }
    public decimal ActiveQuoteValue { get; set; }
    public int ActiveProjectCount { get; set; }
    public decimal ProjectValue { get; set; }
    public int ActiveContractCount { get; set; }
    public decimal ContractValue { get; set; }
    public int ExpiringContractCount { get; set; }
    public int TenderInvitationCount { get; set; }
    public int TenderBidCount { get; set; }
    public int TenderAwardCount { get; set; }
    public decimal TenderAwardedValue { get; set; }
    public List<CrmAccountContactDto> Contacts { get; set; } = new();
    public List<CrmLeadListItemDto> Leads { get; set; } = new();
    public List<CrmOpportunityOverviewDto> Opportunities { get; set; } = new();
    public List<CrmQuoteSummaryDto> Quotes { get; set; } = new();
    public List<CrmActivitySummaryDto> Activities { get; set; } = new();
    public List<CrmProjectSummaryDto> Projects { get; set; } = new();
    public List<CrmContractSummaryDto> Contracts { get; set; } = new();
    public List<CrmTenderSummaryDto> Tenders { get; set; } = new();
    public List<CrmHealthSignalDto> HealthSignals { get; set; } = new();
}

public class CrmAccountContactDto
{
    public Guid ContactId { get; set; }
    public string ContactName { get; set; } = string.Empty;
    public string? ContactTitle { get; set; }
    public string? Department { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Mobile { get; set; }
    public bool IsPrimary { get; set; }
}

public class CrmContactListItemDto
{
    public Guid ContactId { get; set; }
    public Guid BusinessPartnerId { get; set; }
    public string PartnerCode { get; set; } = string.Empty;
    public string PartnerName { get; set; } = string.Empty;
    public string PartnerType { get; set; } = string.Empty;
    public string RegistrationStatus { get; set; } = string.Empty;
    public string ContactName { get; set; } = string.Empty;
    public string? ContactTitle { get; set; }
    public string? Department { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Mobile { get; set; }
    public bool IsPrimary { get; set; }
    public string? SalesTerritory { get; set; }
    public string? CustomerType { get; set; }
    public bool HasOpenFollowUp { get; set; }
    public bool IsAtRisk { get; set; }
    public int HealthScore { get; set; }
    public string HealthCategory { get; set; } = string.Empty;
    public int OpenOpportunityCount { get; set; }
    public decimal OpenOpportunityValue { get; set; }
    public int ActiveProjectCount { get; set; }
    public int ActiveContractCount { get; set; }
    public DateTime? NextMilestoneDate { get; set; }
}

public class CrmReadinessListItemDto
{
    public Guid BusinessPartnerId { get; set; }
    public string PartnerCode { get; set; } = string.Empty;
    public string PartnerName { get; set; } = string.Empty;
    public string PartnerType { get; set; } = string.Empty;
    public string RegistrationStatus { get; set; } = string.Empty;
    public string? CustomerType { get; set; }
    public string? SalesTerritory { get; set; }
    public int DocumentCount { get; set; }
    public int VerifiedDocumentCount { get; set; }
    public int ExpiringDocumentCount { get; set; }
    public int ExpiredDocumentCount { get; set; }
    public int LicenseCount { get; set; }
    public int ExpiringLicenseCount { get; set; }
    public int ExpiredLicenseCount { get; set; }
    public int FinancialRecordCount { get; set; }
    public int? LatestFinancialYear { get; set; }
    public decimal? LatestAnnualRevenue { get; set; }
    public string? CreditRating { get; set; }
    public int OpenOpportunityCount { get; set; }
    public int ActiveProjectCount { get; set; }
    public int ActiveContractCount { get; set; }
    public bool IsAtRisk { get; set; }
    public int HealthScore { get; set; }
    public string HealthCategory { get; set; } = string.Empty;
    public int ReadinessScore { get; set; }
    public string ReadinessCategory { get; set; } = string.Empty;
    public bool HasCriticalGap { get; set; }
    public DateTime? NextComplianceDate { get; set; }
}

public class CrmReadinessDetailDto : CrmReadinessListItemDto
{
    public string? PrimaryContactName { get; set; }
    public string? PrimaryEmail { get; set; }
    public string? PrimaryPhone { get; set; }
    public List<CrmReadinessDocumentDto> Documents { get; set; } = new();
    public List<CrmReadinessLicenseDto> Licenses { get; set; } = new();
    public List<CrmReadinessFinancialDto> Financials { get; set; } = new();
    public List<CrmReadinessSignalDto> Signals { get; set; } = new();
}

public class CrmReadinessDocumentDto
{
    public Guid DocumentId { get; set; }
    public string DocumentType { get; set; } = string.Empty;
    public string DocumentName { get; set; } = string.Empty;
    public bool IsVerified { get; set; }
    public DateTime? IssueDate { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public DateTime UploadedAt { get; set; }
    public bool IsExpired { get; set; }
    public bool IsExpiringSoon { get; set; }
}

public class CrmReadinessLicenseDto
{
    public Guid LicenseId { get; set; }
    public string LicenseTypeName { get; set; } = string.Empty;
    public string LicenseNumber { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? IssuingAuthority { get; set; }
    public DateTime? IssueDate { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public bool IsExpired { get; set; }
    public bool IsExpiringSoon { get; set; }
}

public class CrmReadinessFinancialDto
{
    public Guid FinancialId { get; set; }
    public int FinancialYear { get; set; }
    public decimal? AnnualRevenue { get; set; }
    public decimal? NetProfit { get; set; }
    public decimal? TotalAssets { get; set; }
    public decimal? TotalLiabilities { get; set; }
    public string? CreditRating { get; set; }
    public bool IsAudited { get; set; }
    public string? AuditorName { get; set; }
    public DateTime? AuditDate { get; set; }
}

public class CrmReadinessSignalDto
{
    public string Label { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public int ScoreImpact { get; set; }
}

public class CrmRiskListItemDto
{
    public Guid BusinessPartnerId { get; set; }
    public string PartnerCode { get; set; } = string.Empty;
    public string PartnerName { get; set; } = string.Empty;
    public string PartnerType { get; set; } = string.Empty;
    public string RegistrationStatus { get; set; } = string.Empty;
    public string? CustomerType { get; set; }
    public string? SalesTerritory { get; set; }
    public string? RiskLevel { get; set; }
    public decimal? PerformanceRating { get; set; }
    public bool IsBlacklisted { get; set; }
    public bool IsOnCreditHold { get; set; }
    public int OpenIncidentCount { get; set; }
    public int CriticalIncidentCount { get; set; }
    public int PendingAppealCount { get; set; }
    public int OpenReviewFollowUpCount { get; set; }
    public decimal? LatestMetricScore { get; set; }
    public string? LatestMetricGrade { get; set; }
    public string? LatestMetricPeriod { get; set; }
    public DateTime? LatestMetricCalculatedAt { get; set; }
    public DateTime? LatestReviewDate { get; set; }
    public int OpenOpportunityCount { get; set; }
    public int ActiveProjectCount { get; set; }
    public int ActiveContractCount { get; set; }
    public bool IsAtRisk { get; set; }
    public int HealthScore { get; set; }
    public string HealthCategory { get; set; } = string.Empty;
    public int RiskScore { get; set; }
    public string RiskCategory { get; set; } = string.Empty;
    public bool RequiresEscalation { get; set; }
    public DateTime? NextMilestoneDate { get; set; }
}

public class CrmRiskDetailDto : CrmRiskListItemDto
{
    public string? PrimaryContactName { get; set; }
    public string? PrimaryEmail { get; set; }
    public string? PrimaryPhone { get; set; }
    public decimal? CreditLimit { get; set; }
    public decimal? OutstandingBalance { get; set; }
    public List<CrmRiskPerformanceMetricDto> PerformanceMetrics { get; set; } = new();
    public List<CrmRiskIncidentDto> Incidents { get; set; } = new();
    public List<CrmRiskReviewDto> Reviews { get; set; } = new();
    public List<CrmRiskAppealDto> Appeals { get; set; } = new();
    public List<CrmRiskSignalDto> Signals { get; set; } = new();
}

public class CrmRiskPerformanceMetricDto
{
    public Guid MetricId { get; set; }
    public string MetricPeriod { get; set; } = string.Empty;
    public int Year { get; set; }
    public int? Month { get; set; }
    public int? Quarter { get; set; }
    public decimal OverallPerformanceScore { get; set; }
    public string? PerformanceGrade { get; set; }
    public decimal OnTimeDeliveryRate { get; set; }
    public decimal QualityAcceptanceRate { get; set; }
    public decimal ComplianceScore { get; set; }
    public int ComplaintsReceived { get; set; }
    public int ComplaintsResolved { get; set; }
    public int ContractViolations { get; set; }
    public DateTime CalculatedAt { get; set; }
}

public class CrmRiskIncidentDto
{
    public Guid IncidentId { get; set; }
    public string IncidentNumber { get; set; } = string.Empty;
    public DateTime IncidentDate { get; set; }
    public string IncidentType { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal? FinancialImpact { get; set; }
    public bool RequiresSupplierResponse { get; set; }
    public DateTime? SupplierResponseDate { get; set; }
    public DateTime? ResolvedDate { get; set; }
}

public class CrmRiskReviewDto
{
    public Guid ReviewId { get; set; }
    public string ReviewNumber { get; set; } = string.Empty;
    public DateTime ReviewDate { get; set; }
    public string ReviewPeriod { get; set; } = string.Empty;
    public decimal OverallScore { get; set; }
    public string? OverallGrade { get; set; }
    public string Status { get; set; } = string.Empty;
    public bool RequiresFollowUp { get; set; }
    public DateTime? FollowUpDate { get; set; }
}

public class CrmRiskAppealDto
{
    public Guid AppealId { get; set; }
    public string AppealNumber { get; set; } = string.Empty;
    public DateTime AppealDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public bool? RemoveBlacklist { get; set; }
    public DateTime? ReviewedDate { get; set; }
    public DateTime? ApprovedDate { get; set; }
    public DateTime? NewBlacklistExpiryDate { get; set; }
}

public class CrmRiskSignalDto
{
    public string Label { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public int ScoreImpact { get; set; }
}

public class CrmCollaborationListItemDto
{
    public Guid BusinessPartnerId { get; set; }
    public string PartnerCode { get; set; } = string.Empty;
    public string PartnerName { get; set; } = string.Empty;
    public string PartnerType { get; set; } = string.Empty;
    public string RegistrationStatus { get; set; } = string.Empty;
    public string? CustomerType { get; set; }
    public string? SalesTerritory { get; set; }
    public string? LatestApplicationNumber { get; set; }
    public string? LatestRegistrationLifecycleStatus { get; set; }
    public DateTime? LatestRegistrationSubmittedDate { get; set; }
    public DateTime? LatestRegistrationApprovedDate { get; set; }
    public int RegistrationDocumentCount { get; set; }
    public int PortalUserCount { get; set; }
    public int ActivePortalUserCount { get; set; }
    public int AdminUserCount { get; set; }
    public int TenderAssignmentCount { get; set; }
    public int AssignedTenderCount { get; set; }
    public int PortalProjectCount { get; set; }
    public int CollaborationProjectCount { get; set; }
    public int OpenOpportunityCount { get; set; }
    public int ActiveProjectCount { get; set; }
    public int ActiveContractCount { get; set; }
    public bool IsAtRisk { get; set; }
    public int HealthScore { get; set; }
    public string HealthCategory { get; set; } = string.Empty;
    public int CollaborationScore { get; set; }
    public string CollaborationCategory { get; set; } = string.Empty;
    public bool RequiresEnablement { get; set; }
    public DateTime? NextMilestoneDate { get; set; }
}

public class CrmCollaborationDetailDto : CrmCollaborationListItemDto
{
    public string? PrimaryContactName { get; set; }
    public string? PrimaryEmail { get; set; }
    public string? PrimaryPhone { get; set; }
    public List<CrmCollaborationRegistrationDto> Registrations { get; set; } = new();
    public List<CrmCollaborationPortalUserDto> PortalUsers { get; set; } = new();
    public List<CrmCollaborationTenderAssignmentDto> TenderAssignments { get; set; } = new();
    public List<CrmCollaborationProjectDto> Projects { get; set; } = new();
    public List<CrmCollaborationSignalDto> Signals { get; set; } = new();
}

public class CrmCollaborationRegistrationDto
{
    public Guid RegistrationId { get; set; }
    public string ApplicationNumber { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? SubmittedDate { get; set; }
    public DateTime? ReviewedDate { get; set; }
    public DateTime? ApprovedDate { get; set; }
    public int DocumentCount { get; set; }
    public int VerifiedDocumentCount { get; set; }
    public int RejectedDocumentCount { get; set; }
    public DateTime? LastStatusChangeDate { get; set; }
}

public class CrmCollaborationPortalUserDto
{
    public Guid PortalUserId { get; set; }
    public Guid UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string Role { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime GrantedAt { get; set; }
    public string? Notes { get; set; }
}

public class CrmCollaborationTenderAssignmentDto
{
    public Guid AssignmentId { get; set; }
    public Guid TenderId { get; set; }
    public string TenderNumber { get; set; } = string.Empty;
    public string TenderTitle { get; set; } = string.Empty;
    public string AssignmentType { get; set; } = string.Empty;
    public string? AssignedToUserName { get; set; }
    public DateTime AssignedAt { get; set; }
    public string? Notes { get; set; }
}

public class CrmCollaborationProjectDto
{
    public Guid ProjectId { get; set; }
    public string ProjectCode { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public bool ExternalPortalAccessEnabled { get; set; }
    public bool ExternalCollaborationEnabled { get; set; }
    public DateTime? TargetEndDate { get; set; }
}

public class CrmCollaborationSignalDto
{
    public string Label { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public int ScoreImpact { get; set; }
}

public class CrmServiceListItemDto
{
    public Guid BusinessPartnerId { get; set; }
    public string PartnerCode { get; set; } = string.Empty;
    public string PartnerName { get; set; } = string.Empty;
    public string PartnerType { get; set; } = string.Empty;
    public string RegistrationStatus { get; set; } = string.Empty;
    public string? CustomerType { get; set; }
    public string? SalesTerritory { get; set; }
    public int PortalUserCount { get; set; }
    public int ActivePortalUserCount { get; set; }
    public int TicketCount { get; set; }
    public int OpenTicketCount { get; set; }
    public int OverdueTicketCount { get; set; }
    public int ComplaintTicketCount { get; set; }
    public int HelpdeskTicketCount { get; set; }
    public int EnquiryTicketCount { get; set; }
    public int LinkedProblemCount { get; set; }
    public int OpenProblemCount { get; set; }
    public int ResolvedTicketCount30Days { get; set; }
    public decimal? AverageFeedbackRating { get; set; }
    public int FeedbackResponseCount { get; set; }
    public int OpenOpportunityCount { get; set; }
    public int ActiveContractCount { get; set; }
    public bool IsAtRisk { get; set; }
    public int HealthScore { get; set; }
    public string HealthCategory { get; set; } = string.Empty;
    public int ServiceScore { get; set; }
    public string ServiceCategory { get; set; } = string.Empty;
    public bool RequiresAttention { get; set; }
    public bool HasSlaBreachRisk { get; set; }
    public DateTime? LastTicketCreatedAt { get; set; }
    public DateTime? LastResolvedAt { get; set; }
    public DateTime? NextMilestoneDate { get; set; }
}

public class CrmServiceDetailDto : CrmServiceListItemDto
{
    public string? PrimaryContactName { get; set; }
    public string? PrimaryEmail { get; set; }
    public string? PrimaryPhone { get; set; }
    public List<CrmServiceTicketDto> Tickets { get; set; } = new();
    public List<CrmServiceProblemDto> Problems { get; set; } = new();
    public List<CrmServiceSignalDto> Signals { get; set; } = new();
}

public class CrmServiceTicketDto
{
    public Guid TicketId { get; set; }
    public string TicketNumber { get; set; } = string.Empty;
    public string TicketType { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? Subject { get; set; }
    public string? CategoryName { get; set; }
    public string? RequesterName { get; set; }
    public string? AssignedToName { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? FirstResponseDueAt { get; set; }
    public DateTime? ResolutionDueAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public int? FeedbackRating { get; set; }
    public bool IsOpen { get; set; }
    public bool IsOverdue { get; set; }
    public bool IsComplaint { get; set; }
}

public class CrmServiceProblemDto
{
    public Guid ProblemId { get; set; }
    public string ProblemNumber { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public string? OwnerName { get; set; }
    public Guid? CreatedFromTicketId { get; set; }
    public int LinkedTicketCount { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CrmServiceSignalDto
{
    public string Label { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public int ScoreImpact { get; set; }
}

public class CrmCampaignListItemDto
{
    public Guid CampaignId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string CampaignType { get; set; } = string.Empty;
    public string CampaignStatus { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public decimal Budget { get; set; }
    public decimal ActualCost { get; set; }
    public decimal ExpectedRevenue { get; set; }
    public decimal ActualRevenue { get; set; }
    public decimal RoiPercent { get; set; }
    public int TargetAudience { get; set; }
    public int ActualAudience { get; set; }
    public int ResponseCount { get; set; }
    public decimal ResponseRate { get; set; }
    public int LeadsGenerated { get; set; }
    public int OpportunitiesGenerated { get; set; }
    public int MemberCount { get; set; }
    public int ActiveMemberCount { get; set; }
    public int RespondedMemberCount { get; set; }
    public int QualifiedLeadCount { get; set; }
    public int ConvertedLeadCount { get; set; }
    public int OpenOpportunityCount { get; set; }
    public decimal WeightedPipelineValue { get; set; }
    public int InfluencedAccountCount { get; set; }
    public bool IsActive { get; set; }
    public bool IsEndingSoon { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CrmCampaignDetailDto : CrmCampaignListItemDto
{
    public string? Description { get; set; }
    public string? Notes { get; set; }
    public List<CrmCampaignMemberDto> Members { get; set; } = new();
    public List<CrmCampaignInfluenceAccountDto> InfluencedAccounts { get; set; } = new();
    public List<CrmOpportunityOverviewDto> Opportunities { get; set; } = new();
}

public class CrmCampaignMemberDto
{
    public Guid MemberId { get; set; }
    public Guid LeadId { get; set; }
    public string LeadName { get; set; } = string.Empty;
    public string? CompanyName { get; set; }
    public string LeadStatus { get; set; } = string.Empty;
    public int QualificationScore { get; set; }
    public decimal EstimatedValue { get; set; }
    public Guid? ConvertedBusinessPartnerId { get; set; }
    public string? ConvertedBusinessPartnerName { get; set; }
    public string MemberStatus { get; set; } = string.Empty;
    public DateTime DateAdded { get; set; }
    public DateTime? ResponseDate { get; set; }
    public string? ResponseType { get; set; }
    public string? Notes { get; set; }
    public int OpenOpportunityCount { get; set; }
    public decimal WeightedPipelineValue { get; set; }
    public bool NeedsFollowUp { get; set; }
}

public class CrmCampaignInfluenceAccountDto
{
    public Guid BusinessPartnerId { get; set; }
    public string PartnerCode { get; set; } = string.Empty;
    public string PartnerName { get; set; } = string.Empty;
    public string PartnerType { get; set; } = string.Empty;
    public int ConvertedLeadCount { get; set; }
    public int OpenOpportunityCount { get; set; }
    public decimal WeightedPipelineValue { get; set; }
}

public class CrmOpportunityOverviewDto
{
    public Guid OpportunityId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Stage { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "USD";
    public int Probability { get; set; }
    public decimal WeightedValue { get; set; }
    public DateTime ExpectedCloseDate { get; set; }
    public string OpportunityType { get; set; } = string.Empty;
    public string LeadSource { get; set; } = string.Empty;
    public Guid? CustomerId { get; set; }
    public Guid? BusinessPartnerId { get; set; }
    public string? BusinessPartnerName { get; set; }
    public Guid? LeadId { get; set; }
    public string? LeadName { get; set; }
}

public class CrmOpportunityListItemDto : CrmOpportunityOverviewDto
{
    public DateTime? ActualCloseDate { get; set; }
    public bool IsClosingSoon { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CrmOpportunityDetailDto : CrmOpportunityListItemDto
{
    public string? Description { get; set; }
    public Guid? AssignedToId { get; set; }
    public string? Competitors { get; set; }
    public string? Notes { get; set; }
    public string? LossReason { get; set; }
    public List<CrmQuoteListItemDto> Quotes { get; set; } = new();
    public List<CrmContractSummaryDto> RelatedContracts { get; set; } = new();
    public List<CrmProjectSummaryDto> RelatedProjects { get; set; } = new();
    public CrmConversionChainDto ConversionChain { get; set; } = new();
}

public class CrmLeadListItemDto
{
    public Guid LeadId { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? CompanyName { get; set; }
    public string? JobTitle { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string LeadSource { get; set; } = "Unknown";
    public string LeadStatus { get; set; } = "New";
    public int QualificationScore { get; set; }
    public decimal EstimatedValue { get; set; }
    public DateTime? LastContactDate { get; set; }
    public DateTime? NextFollowUpDate { get; set; }
    public Guid? AssignedToId { get; set; }
    public int OpportunityCount { get; set; }
    public bool NeedsFollowUp { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CrmLeadDetailDto : CrmLeadListItemDto
{
    public string? Mobile { get; set; }
    public string? AddressLine1 { get; set; }
    public string? AddressLine2 { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? PostalCode { get; set; }
    public string? Country { get; set; }
    public string? Notes { get; set; }
    public Guid? ConvertedBusinessPartnerId { get; set; }
    public DateTime? ConvertedDate { get; set; }
    public List<CrmOpportunityOverviewDto> Opportunities { get; set; } = new();
}

public class CrmQuoteSummaryDto
{
    public Guid QuoteId { get; set; }
    public Guid OpportunityId { get; set; }
    public string QuoteName { get; set; } = string.Empty;
    public string QuoteStatus { get; set; } = string.Empty;
    public decimal Value { get; set; }
    public string Currency { get; set; } = "USD";
    public DateTime ValidUntil { get; set; }
    public string? OpportunityName { get; set; }
    public Guid? BusinessPartnerId { get; set; }
    public string? BusinessPartnerName { get; set; }
    public bool IsExpiringSoon { get; set; }
}

public class CrmQuoteListItemDto : CrmQuoteSummaryDto
{
    public string DocumentNumber { get; set; } = string.Empty;
    public DateTime DocumentDate { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? SentDate { get; set; }
    public DateTime? AcceptedDate { get; set; }
    public Guid? LeadId { get; set; }
    public string? LeadName { get; set; }
    public bool IsAccepted { get; set; }
}

public class CrmQuoteDetailDto : CrmQuoteListItemDto
{
    public string? Proposal { get; set; }
    public Guid? ConvertedInvoiceId { get; set; }
    public List<CrmQuoteLineItemDto> LineItems { get; set; } = new();
    public CrmConversionChainDto ConversionChain { get; set; } = new();
}

public class CrmQuoteLineItemDto
{
    public string Description { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }
    public string? ProductCode { get; set; }
    public string? Unit { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TaxAmount { get; set; }
}

public class CrmActivitySummaryDto
{
    public Guid ActivityId { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string ActivityType { get; set; } = string.Empty;
    public string ActivityStatus { get; set; } = string.Empty;
    public DateTime ActivityDate { get; set; }
    public DateTime? DueDate { get; set; }
    public bool RequiresFollowUp { get; set; }
    public DateTime? NextFollowUpDate { get; set; }
    public int Priority { get; set; }
    public Guid? AssignedToId { get; set; }
    public Guid? BusinessPartnerId { get; set; }
    public string? BusinessPartnerName { get; set; }
    public Guid? LeadId { get; set; }
    public string? LeadName { get; set; }
    public Guid? OpportunityId { get; set; }
    public string? OpportunityName { get; set; }
    public string? RelatedEntityType { get; set; }
    public Guid? RelatedEntityId { get; set; }
    public string? RelatedEntityHref { get; set; }
}

public class CrmActivityListItemDto : CrmActivitySummaryDto
{
    public bool IsOverdue { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CrmActivityDetailDto : CrmActivityListItemDto
{
    public string? Description { get; set; }
    public int? Duration { get; set; }
    public string? Location { get; set; }
    public string? Attendees { get; set; }
    public string? Outcome { get; set; }
    public string? Notes { get; set; }
}

public class CrmProjectSummaryDto
{
    public Guid ProjectId { get; set; }
    public string ProjectCode { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public decimal Value { get; set; }
    public decimal ProgressPercent { get; set; }
    public Guid? BusinessPartnerId { get; set; }
    public Guid? ContractId { get; set; }
    public string RelationshipType { get; set; } = string.Empty;
    public DateTime? TargetEndDate { get; set; }
}

public class CrmProjectListItemDto : CrmProjectSummaryDto
{
    public Guid? ResolvedBusinessPartnerId { get; set; }
    public string? BusinessPartnerName { get; set; }
    public string? ContractNumber { get; set; }
    public string? ContractTitle { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? ActualEndDate { get; set; }
    public string? StatusRemarks { get; set; }
    public bool IsOverdue { get; set; }
    public bool IsLinkedToActiveContract { get; set; }
}

public class CrmProjectDetailDto : CrmProjectListItemDto
{
    public string? Summary { get; set; }
    public string? BusinessCase { get; set; }
    public string? Objectives { get; set; }
    public string Methodology { get; set; } = string.Empty;
    public decimal? EstimatedBudget { get; set; }
    public decimal? ApprovedBudget { get; set; }
    public decimal? ActualCost { get; set; }
    public string BudgetStatus { get; set; } = string.Empty;
    public bool ExternalPortalAccessEnabled { get; set; }
    public bool ExternalCollaborationEnabled { get; set; }
}

public class CrmContractSummaryDto
{
    public Guid ContractId { get; set; }
    public string ContractNumber { get; set; } = string.Empty;
    public string ContractTitle { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public decimal ContractValue { get; set; }
    public Guid BusinessPartnerId { get; set; }
    public Guid TenderAwardId { get; set; }
    public Guid TenderId { get; set; }
    public string RelationshipType { get; set; } = string.Empty;
    public DateTime? EndDate { get; set; }
}

public class CrmContractListItemDto : CrmContractSummaryDto
{
    public string? BusinessPartnerName { get; set; }
    public string Currency { get; set; } = "USD";
    public string ContractType { get; set; } = string.Empty;
    public DateTime? StartDate { get; set; }
    public DateTime? SignedDate { get; set; }
    public string? PaymentTerms { get; set; }
    public int ProjectCount { get; set; }
    public int ActiveProjectCount { get; set; }
    public bool IsActive { get; set; }
    public bool IsExpiringSoon { get; set; }
}

public class CrmContractDetailDto : CrmContractListItemDto
{
    public string? ScopeOfWork { get; set; }
    public string? Deliverables { get; set; }
    public string? SpecialConditions { get; set; }
    public string? PenaltyClause { get; set; }
    public string? SignedByName { get; set; }
    public string? ContractorSignatoryName { get; set; }
    public DateTime? ActivatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? TerminatedAt { get; set; }
    public string? TerminationReason { get; set; }
    public string? Notes { get; set; }
}

public class CrmTenderSummaryDto
{
    public Guid EntityId { get; set; }
    public Guid TenderId { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public Guid BusinessPartnerId { get; set; }
    public string? BusinessPartnerName { get; set; }
    public string TenderNumber { get; set; } = string.Empty;
    public string TenderTitle { get; set; } = string.Empty;
    public string TenderType { get; set; } = string.Empty;
    public string Currency { get; set; } = "USD";
    public string ReferenceNumber { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? SubmissionDeadline { get; set; }
    public Guid? RelatedContractId { get; set; }
    public string? RelatedContractNumber { get; set; }
}

public class CrmTenderListItemDto : CrmTenderSummaryDto
{
    public string TenderStatus { get; set; } = string.Empty;
    public DateTime? PublishDate { get; set; }
    public DateTime? AwardDate { get; set; }
    public bool IsClosingSoon { get; set; }
}

public class CrmTenderDetailDto : CrmTenderListItemDto
{
    public Guid? TenderBidId { get; set; }
    public Guid? TenderAwardId { get; set; }
    public string? RelatedContractTitle { get; set; }
    public DateTime? InvitedDate { get; set; }
    public DateTime? ViewedDate { get; set; }
    public DateTime? ResponseDate { get; set; }
    public string? DeclineReason { get; set; }
    public DateTime? SubmittedDate { get; set; }
    public decimal? TotalScore { get; set; }
    public int? Rank { get; set; }
    public int? DeliveryDays { get; set; }
    public string? PaymentTerms { get; set; }
    public string? WarrantyTerms { get; set; }
    public bool? IsCompliant { get; set; }
    public string? NonComplianceReasons { get; set; }
    public decimal? OriginalBidAmount { get; set; }
    public bool? IsNegotiated { get; set; }
    public string? AwardJustification { get; set; }
    public Guid? PurchaseOrderId { get; set; }
    public string? Notes { get; set; }
    public string? RelationshipNote { get; set; }
}

public class CrmFollowUpOverviewDto
{
    public string EntityType { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime DueDate { get; set; }
    public string? Context { get; set; }
}

public class CrmHealthSignalDto
{
    public string Label { get; set; } = string.Empty;
    public string Direction { get; set; } = string.Empty;
    public int ScoreImpact { get; set; }
}

public class CrmConversionChainDto
{
    public Guid OpportunityId { get; set; }
    public string OpportunityName { get; set; } = string.Empty;
    public Guid? BusinessPartnerId { get; set; }
    public string? BusinessPartnerName { get; set; }
    public Guid? LeadId { get; set; }
    public string? LeadName { get; set; }
    public List<CrmConversionChainNodeDto> Nodes { get; set; } = new();
}

public class CrmConversionChainNodeDto
{
    public string Stage { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public decimal? Amount { get; set; }
    public string? Currency { get; set; }
    public DateTime? ReferenceDate { get; set; }
    public string RelationshipType { get; set; } = string.Empty;
    public string? RelationshipNote { get; set; }
    public string? ReferenceCode { get; set; }
}

public class CrmReportingDto
{
    public int TotalLeadCount { get; set; }
    public int QualifiedLeadCount { get; set; }
    public int ConvertedLeadCount { get; set; }
    public decimal LeadConversionRate { get; set; }
    public int OpenOpportunityCount { get; set; }
    public decimal OpenOpportunityValue { get; set; }
    public decimal WeightedPipelineValue { get; set; }
    public int TotalQuoteCount { get; set; }
    public int AcceptedQuoteCount { get; set; }
    public decimal AcceptedQuoteValue { get; set; }
    public decimal QuoteAcceptanceRate { get; set; }
    public int ActiveAccountCount { get; set; }
    public decimal AverageAccountHealthScore { get; set; }
    public int AtRiskAccountCount { get; set; }
    public List<CrmPipelineStageReportDto> PipelineByStage { get; set; } = new();
    public List<CrmAccountHealthReportDto> AccountHealth { get; set; } = new();
    public List<CrmOpportunityListItemDto> ClosingOpportunities { get; set; } = new();
    public List<CrmContractSummaryDto> ExpiringContracts { get; set; } = new();
}

public class CrmForecastDto
{
    public int HorizonMonths { get; set; }
    public DateTime HorizonStart { get; set; }
    public DateTime HorizonEnd { get; set; }
    public int OpportunityCount { get; set; }
    public decimal BestCaseValue { get; set; }
    public decimal WeightedPipelineValue { get; set; }
    public decimal CommitValue { get; set; }
    public decimal CampaignBackedWeightedValue { get; set; }
    public decimal RenewalContractValue { get; set; }
    public decimal RenewalCoverageValue { get; set; }
    public decimal RenewalGapValue { get; set; }
    public decimal AverageProbability { get; set; }
    public List<CrmForecastBucketDto> Buckets { get; set; } = new();
    public List<CrmForecastDealDto> HighConfidenceDeals { get; set; } = new();
    public List<CrmForecastRenewalDto> RenewalWatchlist { get; set; } = new();
}

public class CrmForecastBucketDto
{
    public DateTime PeriodStart { get; set; }
    public string PeriodLabel { get; set; } = string.Empty;
    public int OpportunityCount { get; set; }
    public int RenewalOpportunityCount { get; set; }
    public int CampaignBackedOpportunityCount { get; set; }
    public decimal BestCaseValue { get; set; }
    public decimal WeightedValue { get; set; }
    public decimal CommitValue { get; set; }
    public decimal QuoteCoverageValue { get; set; }
    public decimal RenewalContractValue { get; set; }
    public decimal RenewalCoverageValue { get; set; }
}

public class CrmForecastDealDto
{
    public Guid OpportunityId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Stage { get; set; } = string.Empty;
    public string OpportunityType { get; set; } = string.Empty;
    public Guid? BusinessPartnerId { get; set; }
    public string? BusinessPartnerName { get; set; }
    public Guid? LeadId { get; set; }
    public string? LeadName { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "USD";
    public int Probability { get; set; }
    public decimal WeightedValue { get; set; }
    public DateTime ExpectedCloseDate { get; set; }
    public int QuoteCount { get; set; }
    public int CampaignCount { get; set; }
    public string ForecastCategory { get; set; } = string.Empty;
    public string? CampaignContext { get; set; }
}

public class CrmForecastRenewalDto
{
    public Guid ContractId { get; set; }
    public string ContractNumber { get; set; } = string.Empty;
    public string ContractTitle { get; set; } = string.Empty;
    public Guid BusinessPartnerId { get; set; }
    public string BusinessPartnerName { get; set; } = string.Empty;
    public DateTime? EndDate { get; set; }
    public decimal ContractValue { get; set; }
    public int RenewalOpportunityCount { get; set; }
    public decimal RenewalWeightedValue { get; set; }
    public decimal RenewalCommitValue { get; set; }
    public decimal CoverageGapValue { get; set; }
    public int ActiveCampaignCount { get; set; }
    public string CoverageCategory { get; set; } = string.Empty;
}

public class CrmConversionsDto
{
    public int HorizonMonths { get; set; }
    public DateTime HorizonStart { get; set; }
    public DateTime HorizonEnd { get; set; }
    public int LeadCount { get; set; }
    public int LeadWithOpportunityCount { get; set; }
    public int OpportunityCount { get; set; }
    public int QuotedOpportunityCount { get; set; }
    public int ContractBackedOpportunityCount { get; set; }
    public int ProjectBackedOpportunityCount { get; set; }
    public decimal LeadToOpportunityRate { get; set; }
    public decimal OpportunityToQuoteRate { get; set; }
    public decimal OpportunityToContractRate { get; set; }
    public decimal OpportunityToProjectRate { get; set; }
    public decimal TotalOpportunityValue { get; set; }
    public decimal WeightedPipelineValue { get; set; }
    public List<CrmConversionStageMetricDto> Funnel { get; set; } = new();
    public List<CrmConversionJourneyDto> Journeys { get; set; } = new();
    public List<CrmConversionLeakDto> Leakage { get; set; } = new();
}

public class CrmConversionStageMetricDto
{
    public string Stage { get; set; } = string.Empty;
    public int EntityCount { get; set; }
    public int RelatedOpportunityCount { get; set; }
    public decimal TotalValue { get; set; }
    public decimal ConversionRate { get; set; }
}

public class CrmConversionJourneyDto
{
    public Guid OpportunityId { get; set; }
    public string OpportunityName { get; set; } = string.Empty;
    public string Stage { get; set; } = string.Empty;
    public string OpportunityType { get; set; } = string.Empty;
    public Guid? BusinessPartnerId { get; set; }
    public string? BusinessPartnerName { get; set; }
    public Guid? LeadId { get; set; }
    public string? LeadName { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "USD";
    public decimal WeightedValue { get; set; }
    public DateTime ExpectedCloseDate { get; set; }
    public int QuoteCount { get; set; }
    public int ContractCount { get; set; }
    public int ProjectCount { get; set; }
    public string CoverageStatus { get; set; } = string.Empty;
    public string? LeakageReason { get; set; }
    public CrmConversionChainDto Chain { get; set; } = new();
}

public class CrmConversionLeakDto
{
    public string EntityType { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string LeakageStage { get; set; } = string.Empty;
    public string LeakageReason { get; set; } = string.Empty;
    public Guid? BusinessPartnerId { get; set; }
    public string? BusinessPartnerName { get; set; }
    public decimal? Amount { get; set; }
    public string? Currency { get; set; }
    public DateTime? ReferenceDate { get; set; }
}

public class CrmPipelineStageReportDto
{
    public string Stage { get; set; } = string.Empty;
    public int OpportunityCount { get; set; }
    public decimal TotalValue { get; set; }
    public decimal WeightedValue { get; set; }
    public int QuoteCount { get; set; }
}

public class CrmAccountHealthReportDto
{
    public Guid BusinessPartnerId { get; set; }
    public string PartnerCode { get; set; } = string.Empty;
    public string PartnerName { get; set; } = string.Empty;
    public int HealthScore { get; set; }
    public string HealthCategory { get; set; } = string.Empty;
    public string? RiskLevel { get; set; }
    public decimal? PerformanceRating { get; set; }
    public int OpenOpportunityCount { get; set; }
    public decimal OpenOpportunityValue { get; set; }
    public int ActiveProjectCount { get; set; }
    public int ActiveContractCount { get; set; }
    public bool HasOpenFollowUp { get; set; }
    public bool IsAtRisk { get; set; }
    public DateTime? NextMilestoneDate { get; set; }
}

public abstract class CrmLeadUpsertDto
{
    [Required]
    [MaxLength(100)]
    public string FirstName { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string LastName { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? CompanyName { get; set; }

    [MaxLength(100)]
    public string? JobTitle { get; set; }

    [EmailAddress]
    [MaxLength(100)]
    public string? Email { get; set; }

    [MaxLength(20)]
    public string? Phone { get; set; }

    [MaxLength(20)]
    public string? Mobile { get; set; }

    [MaxLength(200)]
    public string? AddressLine1 { get; set; }

    [MaxLength(200)]
    public string? AddressLine2 { get; set; }

    [MaxLength(100)]
    public string? City { get; set; }

    [MaxLength(100)]
    public string? State { get; set; }

    [MaxLength(20)]
    public string? PostalCode { get; set; }

    [MaxLength(100)]
    public string? Country { get; set; }

    [MaxLength(50)]
    public string LeadSource { get; set; } = "Unknown";

    [MaxLength(50)]
    public string LeadStatus { get; set; } = "New";

    [Range(0, 100)]
    public int QualificationScore { get; set; }

    [Range(0, double.MaxValue)]
    public decimal EstimatedValue { get; set; }

    public DateTime? LastContactDate { get; set; }
    public DateTime? NextFollowUpDate { get; set; }
    public Guid? AssignedToId { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }
}

public class CreateCrmLeadDto : CrmLeadUpsertDto
{
}

public class UpdateCrmLeadDto : CrmLeadUpsertDto
{
}

public abstract class CrmOpportunityUpsertDto
{
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Description { get; set; }

    public Guid? BusinessPartnerId { get; set; }
    public Guid? LeadId { get; set; }

    [MaxLength(50)]
    public string Stage { get; set; } = "Prospecting";

    [Range(0, 100)]
    public int Probability { get; set; } = 10;

    [Range(0, double.MaxValue)]
    public decimal Amount { get; set; }

    [Required]
    [MaxLength(3)]
    public string Currency { get; set; } = "USD";

    public DateTime ExpectedCloseDate { get; set; } = DateTime.UtcNow.Date.AddDays(30);
    public DateTime? ActualCloseDate { get; set; }

    [MaxLength(50)]
    public string LeadSource { get; set; } = "Unknown";

    [MaxLength(50)]
    public string OpportunityType { get; set; } = "New Business";

    public Guid? AssignedToId { get; set; }

    [MaxLength(500)]
    public string? Competitors { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }

    [MaxLength(2000)]
    public string? LossReason { get; set; }
}

public class CreateCrmOpportunityDto : CrmOpportunityUpsertDto
{
}

public class UpdateCrmOpportunityDto : CrmOpportunityUpsertDto
{
}

public abstract class CrmActivityUpsertDto
{
    [Required]
    [MaxLength(200)]
    public string Subject { get; set; } = string.Empty;

    [MaxLength(50)]
    public string ActivityType { get; set; } = "Call";

    [MaxLength(2000)]
    public string? Description { get; set; }

    public DateTime ActivityDate { get; set; } = DateTime.UtcNow;
    public DateTime? DueDate { get; set; }

    [MaxLength(50)]
    public string ActivityStatus { get; set; } = "Planned";

    [Range(1, 4)]
    public int Priority { get; set; } = 2;

    public int? Duration { get; set; }
    public Guid? AssignedToId { get; set; }
    public Guid? BusinessPartnerId { get; set; }
    public Guid? LeadId { get; set; }
    public Guid? OpportunityId { get; set; }

    [MaxLength(200)]
    public string? Location { get; set; }

    [MaxLength(1000)]
    public string? Attendees { get; set; }

    [MaxLength(50)]
    public string? Outcome { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }

    public bool RequiresFollowUp { get; set; }
    public DateTime? NextFollowUpDate { get; set; }
}

public class CreateCrmActivityDto : CrmActivityUpsertDto
{
}

public class UpdateCrmActivityDto : CrmActivityUpsertDto
{
}

public abstract class CrmCampaignUpsertDto
{
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(50)]
    public string CampaignType { get; set; } = "Email";

    [MaxLength(2000)]
    public string? Description { get; set; }

    public DateTime StartDate { get; set; } = DateTime.UtcNow.Date;
    public DateTime? EndDate { get; set; }

    [MaxLength(50)]
    public string CampaignStatus { get; set; } = "Planning";

    [Range(0, double.MaxValue)]
    public decimal Budget { get; set; }

    [Range(0, double.MaxValue)]
    public decimal ActualCost { get; set; }

    [Range(0, double.MaxValue)]
    public decimal ExpectedRevenue { get; set; }

    [Range(0, double.MaxValue)]
    public decimal ActualRevenue { get; set; }

    [Range(0, int.MaxValue)]
    public int TargetAudience { get; set; }

    [Range(0, int.MaxValue)]
    public int ActualAudience { get; set; }

    [Range(0, int.MaxValue)]
    public int ResponseCount { get; set; }

    [Range(0, int.MaxValue)]
    public int LeadsGenerated { get; set; }

    [Range(0, int.MaxValue)]
    public int OpportunitiesGenerated { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }
}

public class CreateCrmCampaignDto : CrmCampaignUpsertDto
{
}

public class UpdateCrmCampaignDto : CrmCampaignUpsertDto
{
}

public class CreateCrmCampaignMemberDto
{
    [Required]
    public Guid LeadId { get; set; }

    [MaxLength(50)]
    public string MemberStatus { get; set; } = "Active";

    public DateTime? ResponseDate { get; set; }

    [MaxLength(50)]
    public string? ResponseType { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }
}

public class UpdateCrmCampaignMemberDto
{
    [MaxLength(50)]
    public string MemberStatus { get; set; } = "Active";

    public DateTime? ResponseDate { get; set; }

    [MaxLength(50)]
    public string? ResponseType { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }
}
