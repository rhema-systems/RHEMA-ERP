using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.Procurement;

namespace ErpSystem.Core.Interfaces.Procurement;

/// <summary>
/// Tender service interface
/// </summary>
public interface ITenderService
{
    Task<TenderDetailDto?> GetTenderByIdAsync(Guid id);
    Task<TenderDetailDto?> GetTenderByNumberAsync(string tenderNumber);
    Task<PagedResult<TenderDto>> GetTendersAsync(int page, int pageSize, string? search = null, string? status = null, string? tenderType = null);
    Task<IEnumerable<TenderDto>> GetPublishedTendersAsync();
    Task<IEnumerable<TenderDto>> GetActiveTendersAsync();
    Task<TenderDetailDto> CreateTenderAsync(CreateTenderDto dto);
    Task<TenderDetailDto> UpdateTenderAsync(Guid id, UpdateTenderDto dto);
    Task<TenderDetailDto> PublishTenderAsync(Guid id, PublishTenderDto dto);
    Task DeleteTenderAsync(Guid id);
    
    // Tender Items
    Task<TenderItemDto> AddTenderItemAsync(Guid tenderId, CreateTenderItemDto dto);
    Task<TenderItemDto> UpdateTenderItemAsync(Guid itemId, CreateTenderItemDto dto);
    Task DeleteTenderItemAsync(Guid itemId);
    
    // Tender Documents
    Task<TenderDocumentDto> UploadTenderDocumentAsync(Guid tenderId, UploadTenderDocumentDto dto, string filePath, string? fileType, long? fileSize);
    Task DeleteTenderDocumentAsync(Guid documentId);
    Task<IEnumerable<TenderDocumentDto>> GetTenderDocumentsAsync(Guid tenderId, bool includeInternal = false);
    
    // Tender Invitations
    Task InviteTenderersAsync(Guid tenderId, InviteTenderersDto dto);
    Task<IEnumerable<TenderInvitationDto>> GetTenderInvitationsAsync(Guid tenderId);
    
    // Tender Fees
    Task<TenderFeeDto> AddTenderFeeAsync(Guid tenderId, CreateTenderFeeDto dto);
    Task<TenderFeeDto> UpdateTenderFeeAsync(Guid feeId, CreateTenderFeeDto dto);
    Task DeleteTenderFeeAsync(Guid feeId);
    
    // Tender Evaluators
    Task AssignEvaluatorsAsync(Guid tenderId, AssignEvaluatorsDto dto);
    Task<IEnumerable<TenderEvaluatorDto>> GetTenderEvaluatorsAsync(Guid tenderId);
    Task RemoveEvaluatorAsync(Guid evaluatorId);
    Task<IEnumerable<TenderDto>> GetMyAssignedTendersAsync();
    
    // Tender Clarifications
    Task<TenderClarificationDto> CreateClarificationAsync(Guid tenderId, CreateClarificationDto dto);
    Task<TenderClarificationDto> AnswerClarificationAsync(Guid clarificationId, AnswerClarificationDto dto);
    Task<IEnumerable<TenderClarificationDto>> GetTenderClarificationsAsync(Guid tenderId, bool publicOnly = false);
    
    // Tender Revisions
    Task<TenderRevisionDto> CreateRevisionAsync(Guid tenderId, CreateRevisionDto dto);
    Task<IEnumerable<TenderRevisionDto>> GetTenderRevisionsAsync(Guid tenderId);
    
    // Tender Statistics
    Task<int> GetTotalViewsAsync(Guid tenderId);
    Task<int> GetTotalDownloadsAsync(Guid tenderId);
    Task LogTenderViewAsync(Guid tenderId, Guid? businessPartnerId, string actionType);
}

/// <summary>
/// Tender bid service interface
/// </summary>
public interface ITenderBidService
{
    Task<TenderBidDetailDto?> GetBidByIdAsync(Guid id);
    Task<TenderBidDetailDto?> GetBidByNumberAsync(string bidNumber);
    Task<PagedResult<TenderBidSummaryDto>> GetBidsAsync(int page, int pageSize, string? search = null, string? status = null);
    Task<IEnumerable<TenderBidSummaryDto>> GetBidsByTenderIdAsync(Guid tenderId);
    Task<IEnumerable<TenderBidSummaryDto>> GetMyBidsAsync(Guid businessPartnerId);
    Task<TenderBidDetailDto?> GetMyDraftBidByTenderIdAsync(Guid tenderId);
    Task<TenderBidDetailDto> CreateBidAsync(CreateTenderBidDto dto);
    Task<TenderBidDetailDto> UpdateBidAsync(Guid id, UpdateTenderBidDto dto);
    Task<TenderBidDetailDto> SubmitBidAsync(Guid id, SubmitTenderBidDto dto);
    Task WithdrawBidAsync(Guid id, WithdrawTenderBidDto dto);
    Task<TenderBidDetailDto> OpenBidAsync(Guid id);
    Task<int> OpenAllBidsByTenderAsync(Guid tenderId);
    Task<List<SupplierBidListItemDto>> GetSupplierBidListByTenderAsync(Guid tenderId);
    Task DeleteBidAsync(Guid id);
    
    // Bid Items
    Task<TenderBidItemDto> AddBidItemAsync(Guid bidId, CreateTenderBidItemDto dto);
    Task<TenderBidItemDto> UpdateBidItemAsync(Guid itemId, CreateTenderBidItemDto dto);
    Task DeleteBidItemAsync(Guid itemId);
    
    // Bid Documents
    Task<TenderBidDocumentDto> UploadBidDocumentAsync(Guid bidId, UploadBidDocumentDto dto, string filePath, string? fileType, long? fileSize);
    Task DeleteBidDocumentAsync(Guid documentId);
    Task<IEnumerable<TenderBidDocumentDto>> GetBidDocumentsAsync(Guid bidId);
    
    // Bid Payments
    Task<TenderPaymentDto> RecordPaymentAsync(CreateTenderPaymentDto dto);
    Task<TenderPaymentDto> VerifyPaymentAsync(Guid paymentId, VerifyPaymentDto dto);
    Task<IEnumerable<TenderPaymentDto>> GetBidPaymentsAsync(Guid bidId);
    
    // Bid Interviews
    Task<TenderInterviewDto> ScheduleInterviewAsync(ScheduleInterviewDto dto);
    Task<TenderInterviewDto> UpdateInterviewAsync(Guid interviewId, ScheduleInterviewDto dto);
    Task<IEnumerable<TenderInterviewDto>> GetBidInterviewsAsync(Guid bidId);
    Task<IEnumerable<TenderInterviewDto>> GetUpcomingInterviewsAsync();
}

/// <summary>
/// Tender evaluation service interface
/// </summary>
public interface ITenderEvaluationService
{
    Task<TenderEvaluationDto?> GetEvaluationByIdAsync(Guid id);
    Task<IEnumerable<TenderEvaluationDto>> GetBidEvaluationsAsync(Guid bidId);
    Task<IEnumerable<TenderEvaluationDto>> GetMyEvaluationsAsync(Guid evaluatorId);
    Task<IEnumerable<TenderEvaluationDto>> GetMyEvaluationsByUserIdAsync(Guid userId);
    Task<TenderEvaluationDto> CreateEvaluationAsync(CreateEvaluationDto dto);
    Task<TenderEvaluationDto> UpdateEvaluationAsync(Guid id, UpdateEvaluationDto dto);
    Task<TenderEvaluationDto> SubmitEvaluationAsync(Guid id, SubmitEvaluationDto dto);
    Task DeleteEvaluationAsync(Guid id);

    // Evaluation Reports
    Task<EvaluationScorecardDto> GetBidScorecardAsync(Guid bidId);
    Task<EvaluationReportDto> GetTenderEvaluationReportAsync(Guid tenderId);
    Task<IEnumerable<ConsolidatedEvaluationDto>> GetConsolidatedEvaluationsAsync(Guid tenderId);
}

/// <summary>
/// Tender award service interface
/// </summary>
public interface ITenderAwardService
{
    Task<TenderAwardDto?> GetAwardByIdAsync(Guid id);
    Task<TenderAwardDto?> GetAwardByTenderIdAsync(Guid tenderId);
    Task<PagedResult<TenderAwardDto>> GetAwardsAsync(int page, int pageSize, string? search = null, string? status = null);
    Task<AwardRecommendationDto> GenerateAwardRecommendationAsync(Guid tenderId);
    Task<TenderAwardDto> CreateAwardAsync(Guid tenderId, CreateAwardDto dto);
    Task<TenderAwardDto> UpdateAwardAsync(Guid id, CreateAwardDto dto);
    Task CancelAwardAsync(Guid id, CancelAwardDto dto);
    Task SendAwardNotificationsAsync(Guid tenderId, AwardNotificationDto dto);
}

/// <summary>
/// Tender notification service interface
/// </summary>
public interface ITenderNotificationService
{
    Task SendTenderPublishedNotificationAsync(Guid tenderId, List<Guid> businessPartnerIds);
    Task SendBidSubmittedNotificationAsync(Guid bidId);
    Task SendEvaluationAssignedNotificationAsync(Guid tenderId, List<Guid> evaluatorIds);
    Task SendInterviewScheduledNotificationAsync(Guid interviewId);
    Task SendAwardNotificationAsync(Guid awardId);
    Task SendRejectionNotificationsAsync(Guid tenderId, List<Guid> rejectedBidIds);
    Task SendClarificationNotificationAsync(Guid clarificationId, bool isAnswer = false);
    Task SendRevisionNotificationAsync(Guid revisionId);
}

/// <summary>
/// Tender template service interface
/// </summary>
public interface ITenderTemplateService
{
    Task<TenderTemplateDto?> GetTemplateByIdAsync(Guid id);
    Task<IEnumerable<TenderTemplateDto>> GetActiveTemplatesAsync();
    Task<IEnumerable<TenderTemplateDto>> GetTemplatesByTypeAsync(string tenderType);
    Task<TenderTemplateDto> CreateTemplateAsync(CreateTenderTemplateDto dto);
    Task<TenderTemplateDto> UpdateTemplateAsync(Guid id, CreateTenderTemplateDto dto);
    Task DeleteTemplateAsync(Guid id);
    Task<TenderDetailDto> CreateTenderFromTemplateAsync(Guid templateId, string title);
}

