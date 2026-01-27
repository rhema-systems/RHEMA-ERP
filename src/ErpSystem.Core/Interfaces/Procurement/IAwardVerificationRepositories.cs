using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.Entities.Procurement;

namespace ErpSystem.Core.Interfaces.Procurement;

/// <summary>
/// Repository for Award Verification Checklist Templates
/// </summary>
public interface IAwardVerificationChecklistTemplateRepository
{
    Task<AwardVerificationChecklistTemplate?> GetByIdAsync(Guid id);
    Task<AwardVerificationChecklistTemplate?> GetByIdWithItemsAsync(Guid id);
    Task<AwardVerificationChecklistTemplate?> GetByNameAsync(string name);
    Task<AwardVerificationChecklistTemplate?> GetDefaultTemplateAsync();
    Task<IEnumerable<AwardVerificationChecklistTemplate>> GetAllAsync(bool includeInactive = false);
    Task<IEnumerable<AwardVerificationChecklistTemplate>> GetByContractValueAsync(decimal contractValue);
    Task<PagedResult<AwardVerificationChecklistTemplate>> GetPagedAsync(int page, int pageSize, string? search = null, bool includeInactive = false);
    Task<AwardVerificationChecklistTemplate> CreateAsync(AwardVerificationChecklistTemplate template);
    Task<AwardVerificationChecklistTemplate> UpdateAsync(AwardVerificationChecklistTemplate template);
    Task DeleteAsync(Guid id);
}

/// <summary>
/// Repository for Award Verification Checklist Items
/// </summary>
public interface IAwardVerificationChecklistItemRepository
{
    Task<AwardVerificationChecklistItem?> GetByIdAsync(Guid id);
    Task<IEnumerable<AwardVerificationChecklistItem>> GetByTemplateIdAsync(Guid templateId);
    Task<AwardVerificationChecklistItem> CreateAsync(AwardVerificationChecklistItem item);
    Task<AwardVerificationChecklistItem> UpdateAsync(AwardVerificationChecklistItem item);
    Task DeleteAsync(Guid id);
    Task DeleteByTemplateIdAsync(Guid templateId);
}

/// <summary>
/// Repository for Tender Award Verifications
/// </summary>
public interface ITenderAwardVerificationRepository
{
    Task<TenderAwardVerification?> GetByIdAsync(Guid id);
    Task<TenderAwardVerification?> GetByIdWithDetailsAsync(Guid id);
    Task<TenderAwardVerification?> GetByTenderIdAsync(Guid tenderId);
    Task<TenderAwardVerification?> GetByTenderIdWithDetailsAsync(Guid tenderId);
    Task<IEnumerable<TenderAwardVerification>> GetPendingVerificationsAsync();
    Task<PagedResult<TenderAwardVerification>> GetPagedAsync(int page, int pageSize, string? status = null);
    Task<TenderAwardVerification> CreateAsync(TenderAwardVerification verification);
    Task<TenderAwardVerification> UpdateAsync(TenderAwardVerification verification);
    Task DeleteAsync(Guid id);
}

/// <summary>
/// Repository for Tender Award Verification Bidders
/// </summary>
public interface ITenderAwardVerificationBidderRepository
{
    Task<TenderAwardVerificationBidder?> GetByIdAsync(Guid id);
    Task<TenderAwardVerificationBidder?> GetByIdWithItemsAsync(Guid id);
    Task<IEnumerable<TenderAwardVerificationBidder>> GetByVerificationIdAsync(Guid verificationId);
    Task<TenderAwardVerificationBidder> CreateAsync(TenderAwardVerificationBidder bidder);
    Task<TenderAwardVerificationBidder> UpdateAsync(TenderAwardVerificationBidder bidder);
    Task DeleteAsync(Guid id);
}

/// <summary>
/// Repository for Tender Award Verification Item Results
/// </summary>
public interface ITenderAwardVerificationItemResultRepository
{
    Task<TenderAwardVerificationItemResult?> GetByIdAsync(Guid id);
    Task<TenderAwardVerificationItemResult?> GetByIdWithDocumentsAsync(Guid id);
    Task<TenderAwardVerificationItemResult?> GetByBidderAndItemAsync(Guid bidderId, Guid checklistItemId);
    Task<IEnumerable<TenderAwardVerificationItemResult>> GetByBidderIdAsync(Guid bidderId);
    Task<IEnumerable<TenderAwardVerificationItemResult>> GetByBidderIdWithDocumentsAsync(Guid bidderId);
    Task<TenderAwardVerificationItemResult> CreateAsync(TenderAwardVerificationItemResult result);
    Task<TenderAwardVerificationItemResult> UpdateAsync(TenderAwardVerificationItemResult result);
    Task DeleteAsync(Guid id);
    Task CreateRangeAsync(IEnumerable<TenderAwardVerificationItemResult> results);
}

/// <summary>
/// Repository for Tender Award Verification Item Documents
/// </summary>
public interface ITenderAwardVerificationItemDocumentRepository
{
    Task<TenderAwardVerificationItemDocument?> GetByIdAsync(Guid id);
    Task<IEnumerable<TenderAwardVerificationItemDocument>> GetByItemResultIdAsync(Guid itemResultId);
    Task<TenderAwardVerificationItemDocument> CreateAsync(TenderAwardVerificationItemDocument document);
    Task DeleteAsync(Guid id);
}

