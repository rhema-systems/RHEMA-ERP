using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.Entities.Procurement;

namespace ErpSystem.Core.Interfaces.Procurement;

/// <summary>
/// Tender repository interface
/// </summary>
public interface ITenderRepository
{
    Task<Tender?> GetByIdAsync(Guid id);
    Task<Tender?> GetWithAllRelatedDataAsync(Guid id);
    Task<Tender?> GetByTenderNumberAsync(string tenderNumber);
    Task<PagedResult<Tender>> GetTendersAsync(int page, int pageSize, string? search = null, string? status = null, string? tenderType = null);
    Task<IEnumerable<Tender>> GetPublishedTendersAsync();
    Task<IEnumerable<Tender>> GetActiveTendersAsync();
    Task<IEnumerable<Tender>> GetTendersByStatusAsync(string status);
    Task<Tender> CreateAsync(Tender tender);
    Task<Tender> UpdateAsync(Tender tender);
    Task DeleteAsync(Guid id);
    Task<string> GenerateTenderNumberAsync();
    Task<int> GetTotalViewsAsync(Guid tenderId);
    Task<int> GetTotalDownloadsAsync(Guid tenderId);
}

/// <summary>
/// Tender item repository interface
/// </summary>
public interface ITenderItemRepository
{
    Task<TenderItem?> GetByIdAsync(Guid id);
    Task<IEnumerable<TenderItem>> GetByTenderIdAsync(Guid tenderId);
    Task<TenderItem> CreateAsync(TenderItem item);
    Task<TenderItem> UpdateAsync(TenderItem item);
    Task DeleteAsync(Guid id);
    Task DeleteByTenderIdAsync(Guid tenderId);
}

/// <summary>
/// Tender document repository interface
/// </summary>
public interface ITenderDocumentRepository
{
    Task<TenderDocument?> GetByIdAsync(Guid id);
    Task<IEnumerable<TenderDocument>> GetByTenderIdAsync(Guid tenderId);
    Task<IEnumerable<TenderDocument>> GetPublicDocumentsByTenderIdAsync(Guid tenderId);
    Task<TenderDocument> CreateAsync(TenderDocument document);
    Task<TenderDocument> UpdateAsync(TenderDocument document);
    Task DeleteAsync(Guid id);
}

/// <summary>
/// Tender invitation repository interface
/// </summary>
public interface ITenderInvitationRepository
{
    Task<TenderInvitation?> GetByIdAsync(Guid id);
    Task<TenderInvitation?> GetByTenderAndPartnerAsync(Guid tenderId, Guid businessPartnerId);
    Task<IEnumerable<TenderInvitation>> GetByTenderIdAsync(Guid tenderId);
    Task<IEnumerable<TenderInvitation>> GetByBusinessPartnerIdAsync(Guid businessPartnerId);
    Task<TenderInvitation> CreateAsync(TenderInvitation invitation);
    Task<TenderInvitation> UpdateAsync(TenderInvitation invitation);
    Task DeleteAsync(Guid id);
    Task<int> GetInvitationCountByTenderIdAsync(Guid tenderId);
}

/// <summary>
/// Tender bid repository interface
/// </summary>
public interface ITenderBidRepository
{
    Task<TenderBid?> GetByIdAsync(Guid id);
    Task<TenderBid?> GetWithAllRelatedDataAsync(Guid id);
    Task<TenderBid?> GetByBidNumberAsync(string bidNumber);
    Task<TenderBid?> GetByTenderAndPartnerAsync(Guid tenderId, Guid businessPartnerId);
    Task<IEnumerable<TenderBid>> GetByTenderIdAsync(Guid tenderId);
    Task<IEnumerable<TenderBid>> GetByBusinessPartnerIdAsync(Guid businessPartnerId);
    Task<PagedResult<TenderBid>> GetBidsAsync(int page, int pageSize, string? search = null, string? status = null);
    Task<TenderBid> CreateAsync(TenderBid bid);
    Task<TenderBid> UpdateAsync(TenderBid bid);
    Task DeleteAsync(Guid id);
    Task<string> GenerateBidNumberAsync();
    Task<int> GetBidCountByTenderIdAsync(Guid tenderId);
    Task UpdateBidRankingsAsync(Guid tenderId);
}

/// <summary>
/// Tender bid item repository interface
/// </summary>
public interface ITenderBidItemRepository
{
    Task<TenderBidItem?> GetByIdAsync(Guid id);
    Task<IEnumerable<TenderBidItem>> GetByBidIdAsync(Guid bidId);
    Task<TenderBidItem> CreateAsync(TenderBidItem item);
    Task<TenderBidItem> UpdateAsync(TenderBidItem item);
    Task DeleteAsync(Guid id);
    Task DeleteByBidIdAsync(Guid bidId);
}

/// <summary>
/// Tender bid document repository interface
/// </summary>
public interface ITenderBidDocumentRepository
{
    Task<TenderBidDocument?> GetByIdAsync(Guid id);
    Task<IEnumerable<TenderBidDocument>> GetByBidIdAsync(Guid bidId);
    Task<TenderBidDocument> CreateAsync(TenderBidDocument document);
    Task<TenderBidDocument> UpdateAsync(TenderBidDocument document);
    Task DeleteAsync(Guid id);
}

/// <summary>
/// Tender fee repository interface
/// </summary>
public interface ITenderFeeRepository
{
    Task<TenderFee?> GetByIdAsync(Guid id);
    Task<IEnumerable<TenderFee>> GetByTenderIdAsync(Guid tenderId);
    Task<TenderFee> CreateAsync(TenderFee fee);
    Task<TenderFee> UpdateAsync(TenderFee fee);
    Task DeleteAsync(Guid id);
}

/// <summary>
/// Tender payment repository interface
/// </summary>
public interface ITenderPaymentRepository
{
    Task<TenderPayment?> GetByIdAsync(Guid id);
    Task<TenderPayment?> GetByReferenceAsync(string paymentReference);
    Task<IEnumerable<TenderPayment>> GetByFeeIdAsync(Guid feeId);
    Task<IEnumerable<TenderPayment>> GetByBusinessPartnerIdAsync(Guid businessPartnerId);
    Task<TenderPayment> CreateAsync(TenderPayment payment);
    Task<TenderPayment> UpdateAsync(TenderPayment payment);
    Task DeleteAsync(Guid id);
    Task<string> GeneratePaymentReferenceAsync();
}

/// <summary>
/// Tender evaluator repository interface
/// </summary>
public interface ITenderEvaluatorRepository
{
    Task<TenderEvaluator?> GetByIdAsync(Guid id);
    Task<IEnumerable<TenderEvaluator>> GetByTenderIdAsync(Guid tenderId);
    Task<IEnumerable<TenderEvaluator>> GetByUserIdAsync(Guid userId);
    Task<TenderEvaluator?> GetByTenderAndUserAsync(Guid tenderId, Guid userId);
    Task<TenderEvaluator> CreateAsync(TenderEvaluator evaluator);
    Task<TenderEvaluator> UpdateAsync(TenderEvaluator evaluator);
    Task DeleteAsync(Guid id);
}

/// <summary>
/// Tender evaluation repository interface
/// </summary>
public interface ITenderEvaluationRepository
{
    Task<TenderEvaluation?> GetByIdAsync(Guid id);
    Task<IEnumerable<TenderEvaluation>> GetByBidIdAsync(Guid bidId);
    Task<IEnumerable<TenderEvaluation>> GetByEvaluatorIdAsync(Guid evaluatorId);
    Task<TenderEvaluation?> GetByBidAndEvaluatorAsync(Guid bidId, Guid evaluatorId);
    Task<TenderEvaluation> CreateAsync(TenderEvaluation evaluation);
    Task<TenderEvaluation> UpdateAsync(TenderEvaluation evaluation);
    Task DeleteAsync(Guid id);
    Task<decimal?> GetAverageScoreByBidIdAsync(Guid bidId);
    Task<int> CountByEvaluatorIdAsync(Guid evaluatorId);
}

/// <summary>
/// Tender interview repository interface
/// </summary>
public interface ITenderInterviewRepository
{
    Task<TenderInterview?> GetByIdAsync(Guid id);
    Task<IEnumerable<TenderInterview>> GetByTenderIdAsync(Guid tenderId);
    Task<IEnumerable<TenderInterview>> GetByBidIdAsync(Guid bidId);
    Task<IEnumerable<TenderInterview>> GetUpcomingInterviewsAsync();
    Task<TenderInterview> CreateAsync(TenderInterview interview);
    Task<TenderInterview> UpdateAsync(TenderInterview interview);
    Task DeleteAsync(Guid id);
}

/// <summary>
/// Tender clarification repository interface
/// </summary>
public interface ITenderClarificationRepository
{
    Task<TenderClarification?> GetByIdAsync(Guid id);
    Task<IEnumerable<TenderClarification>> GetByTenderIdAsync(Guid tenderId);
    Task<IEnumerable<TenderClarification>> GetPublicClarificationsByTenderIdAsync(Guid tenderId);
    Task<IEnumerable<TenderClarification>> GetByBusinessPartnerIdAsync(Guid businessPartnerId);
    Task<TenderClarification> CreateAsync(TenderClarification clarification);
    Task<TenderClarification> UpdateAsync(TenderClarification clarification);
    Task DeleteAsync(Guid id);
}

/// <summary>
/// Tender revision repository interface
/// </summary>
public interface ITenderRevisionRepository
{
    Task<TenderRevision?> GetByIdAsync(Guid id);
    Task<IEnumerable<TenderRevision>> GetByTenderIdAsync(Guid tenderId);
    Task<TenderRevision> CreateAsync(TenderRevision revision);
    Task<TenderRevision> UpdateAsync(TenderRevision revision);
    Task DeleteAsync(Guid id);
    Task<string> GenerateRevisionNumberAsync(Guid tenderId);
}

/// <summary>
/// Tender award repository interface
/// </summary>
public interface ITenderAwardRepository
{
    Task<TenderAward?> GetByIdAsync(Guid id);
    Task<TenderAward?> GetByTenderIdAsync(Guid tenderId);
    Task<TenderAward?> GetByBidIdAsync(Guid bidId);
    Task<IEnumerable<TenderAward>> GetAwardsAsync();
    Task<PagedResult<TenderAward>> GetAwardsPagedAsync(int page, int pageSize, string? search = null, string? status = null);
    Task<TenderAward> CreateAsync(TenderAward award);
    Task<TenderAward> UpdateAsync(TenderAward award);
    Task DeleteAsync(Guid id);
}

/// <summary>
/// Tender template repository interface
/// </summary>
public interface ITenderTemplateRepository
{
    Task<TenderTemplate?> GetByIdAsync(Guid id);
    Task<IEnumerable<TenderTemplate>> GetActiveTemplatesAsync();
    Task<IEnumerable<TenderTemplate>> GetByTenderTypeAsync(string tenderType);
    Task<IEnumerable<TenderTemplate>> GetByCategoryAsync(string category);
    Task<TenderTemplate> CreateAsync(TenderTemplate template);
    Task<TenderTemplate> UpdateAsync(TenderTemplate template);
    Task DeleteAsync(Guid id);
}

/// <summary>
/// Tender view log repository interface
/// </summary>
public interface ITenderViewLogRepository
{
    Task<TenderViewLog> CreateAsync(TenderViewLog viewLog);
    Task<int> GetViewCountByTenderIdAsync(Guid tenderId);
    Task<int> GetDownloadCountByTenderIdAsync(Guid tenderId);
    Task<IEnumerable<TenderViewLog>> GetByTenderIdAsync(Guid tenderId);
    Task<IEnumerable<TenderViewLog>> GetByBusinessPartnerIdAsync(Guid businessPartnerId);
}

/// <summary>
/// Evaluation criterion repository interface
/// </summary>
public interface IEvaluationCriterionRepository
{
    Task<EvaluationCriterion?> GetByIdAsync(Guid id);
    Task<EvaluationCriterion?> GetByCodeAsync(string code);
    Task<IEnumerable<EvaluationCriterion>> GetAllAsync();
    Task<IEnumerable<EvaluationCriterion>> GetActiveAsync();
    Task<IEnumerable<EvaluationCriterion>> GetByCategoryAsync(string category);
    Task<EvaluationCriterion> CreateAsync(EvaluationCriterion criterion);
    Task<EvaluationCriterion> UpdateAsync(EvaluationCriterion criterion);
    Task DeleteAsync(Guid id);
}

/// <summary>
/// Tender document type repository interface
/// </summary>
public interface ITenderDocumentTypeRepository
{
    Task<TenderDocumentType?> GetByIdAsync(Guid id);
    Task<TenderDocumentType?> GetByCodeAsync(string code);
    Task<IEnumerable<TenderDocumentType>> GetAllAsync();
    Task<IEnumerable<TenderDocumentType>> GetActiveAsync();
    Task<IEnumerable<TenderDocumentType>> GetByCategoryAsync(string category);
    Task<TenderDocumentType> CreateAsync(TenderDocumentType documentType);
    Task<TenderDocumentType> UpdateAsync(TenderDocumentType documentType);
    Task DeleteAsync(Guid id);
}

/// <summary>
/// Evaluation template repository interface
/// </summary>
public interface IEvaluationTemplateRepository
{
    Task<EvaluationTemplate?> GetByIdAsync(Guid id);
    Task<EvaluationTemplate?> GetByIdWithCriteriaAsync(Guid id);
    Task<EvaluationTemplate?> GetByCodeAsync(string code);
    Task<IEnumerable<EvaluationTemplate>> GetAllAsync();
    Task<IEnumerable<EvaluationTemplate>> GetActiveAsync();
    Task<IEnumerable<EvaluationTemplate>> GetByCategoryAsync(string category);
    Task<IEnumerable<EvaluationTemplate>> GetByTenderTypeAsync(string tenderType);
    Task<EvaluationTemplate?> GetDefaultAsync(string category, string tenderType);
    Task<EvaluationTemplate> CreateAsync(EvaluationTemplate template);
    Task<EvaluationTemplate> UpdateAsync(EvaluationTemplate template);
    Task DeleteAsync(Guid id);
}

/// <summary>
/// Evaluation template criterion repository interface
/// </summary>
public interface IEvaluationTemplateCriterionRepository
{
    Task<EvaluationTemplateCriterion?> GetByIdAsync(Guid id);
    Task<IEnumerable<EvaluationTemplateCriterion>> GetByTemplateIdAsync(Guid templateId);
    Task<EvaluationTemplateCriterion> CreateAsync(EvaluationTemplateCriterion criterion);
    Task<EvaluationTemplateCriterion> UpdateAsync(EvaluationTemplateCriterion criterion);
    Task DeleteAsync(Guid id);
    Task DeleteByTemplateIdAsync(Guid templateId);
}

/// <summary>
/// Tender lot repository interface for managing LOTs within a tender
/// </summary>
public interface ITenderLotRepository
{
    Task<TenderLot?> GetByIdAsync(Guid id);
    Task<TenderLot?> GetByIdWithItemsAsync(Guid id);
    Task<IEnumerable<TenderLot>> GetByTenderIdAsync(Guid tenderId);
    Task<TenderLot?> GetByTenderAndLotCodeAsync(Guid tenderId, string lotCode);
    Task<TenderLot> CreateAsync(TenderLot lot);
    Task<TenderLot> UpdateAsync(TenderLot lot);
    Task DeleteAsync(Guid id);
    Task DeleteByTenderIdAsync(Guid tenderId);
    Task<string> GenerateLotCodeAsync(Guid tenderId);
}

/// <summary>
/// Tender bid lot repository interface for managing LOT-level bids
/// </summary>
public interface ITenderBidLotRepository
{
    Task<TenderBidLot?> GetByIdAsync(Guid id);
    Task<TenderBidLot?> GetByIdWithItemsAsync(Guid id);
    Task<IEnumerable<TenderBidLot>> GetByBidIdAsync(Guid bidId);
    Task<IEnumerable<TenderBidLot>> GetByLotIdAsync(Guid lotId);
    Task<TenderBidLot?> GetByBidAndLotAsync(Guid bidId, Guid lotId);
    Task<TenderBidLot> CreateAsync(TenderBidLot bidLot);
    Task<TenderBidLot> UpdateAsync(TenderBidLot bidLot);
    Task DeleteAsync(Guid id);
    Task DeleteByBidIdAsync(Guid bidId);
}

/// <summary>
/// Performance bond request repository interface
/// </summary>
public interface IPerformanceBondRequestRepository
{
    Task<PerformanceBondRequest?> GetByIdAsync(Guid id);
    Task<PerformanceBondRequest?> GetByAwardIdAsync(Guid awardId);
    Task<PerformanceBondRequest?> GetByBidIdAsync(Guid bidId);
    Task<IEnumerable<PerformanceBondRequest>> GetByBusinessPartnerIdAsync(Guid businessPartnerId);
    Task<IEnumerable<PerformanceBondRequest>> GetPendingRequestsAsync();
    Task<IEnumerable<PerformanceBondRequest>> GetByStatusAsync(string status);
    Task<PerformanceBondRequest> CreateAsync(PerformanceBondRequest request);
    Task<PerformanceBondRequest> UpdateAsync(PerformanceBondRequest request);
    Task DeleteAsync(Guid id);
}
