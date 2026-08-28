using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.Procurement;

namespace ErpSystem.Core.Interfaces.Procurement;

/// <summary>
/// Service interface for Award Verification Checklist management
/// </summary>
public interface IAwardVerificationService
{
    #region Checklist Template Management

    /// <summary>
    /// Get all checklist templates
    /// </summary>
    Task<IEnumerable<AwardVerificationChecklistTemplateDto>> GetAllTemplatesAsync(bool includeInactive = false);

    /// <summary>
    /// Get paged checklist templates
    /// </summary>
    Task<PagedResult<AwardVerificationChecklistTemplateDto>> GetTemplatesPagedAsync(int page, int pageSize, string? search = null, bool includeInactive = false);

    /// <summary>
    /// Get a checklist template by ID with items
    /// </summary>
    Task<AwardVerificationChecklistTemplateDto?> GetTemplateByIdAsync(Guid id);

    /// <summary>
    /// Get the default template
    /// </summary>
    Task<AwardVerificationChecklistTemplateDto?> GetDefaultTemplateAsync();

    /// <summary>
    /// Get templates applicable for a contract value
    /// </summary>
    Task<IEnumerable<AwardVerificationChecklistTemplateDto>> GetTemplatesByContractValueAsync(decimal contractValue);

    /// <summary>
    /// Create a new checklist template
    /// </summary>
    Task<AwardVerificationChecklistTemplateDto> CreateTemplateAsync(CreateAwardVerificationChecklistTemplateDto dto);

    /// <summary>
    /// Update a checklist template
    /// </summary>
    Task<AwardVerificationChecklistTemplateDto> UpdateTemplateAsync(Guid id, UpdateAwardVerificationChecklistTemplateDto dto);

    /// <summary>
    /// Delete a checklist template
    /// </summary>
    Task DeleteTemplateAsync(Guid id);

    /// <summary>
    /// Add an item to a template
    /// </summary>
    Task<AwardVerificationChecklistItemDto> AddTemplateItemAsync(Guid templateId, CreateAwardVerificationChecklistItemDto dto);

    /// <summary>
    /// Update a template item
    /// </summary>
    Task<AwardVerificationChecklistItemDto> UpdateTemplateItemAsync(Guid itemId, CreateAwardVerificationChecklistItemDto dto);

    /// <summary>
    /// Delete a template item
    /// </summary>
    Task DeleteTemplateItemAsync(Guid itemId);

    #endregion

    #region Verification Process

    /// <summary>
    /// Start a verification process for a tender
    /// </summary>
    Task<TenderAwardVerificationDto> StartVerificationAsync(StartAwardVerificationDto dto);

    /// <summary>
    /// Get verification by tender ID
    /// </summary>
    Task<TenderAwardVerificationDto?> GetVerificationByTenderIdAsync(Guid tenderId);

    /// <summary>
    /// Get verification by ID
    /// </summary>
    Task<TenderAwardVerificationDto?> GetVerificationByIdAsync(Guid id);

    /// <summary>
    /// Get pending verifications
    /// </summary>
    Task<IEnumerable<TenderAwardVerificationDto>> GetPendingVerificationsAsync();

    /// <summary>
    /// Verify a checklist item for a bidder
    /// </summary>
    Task<TenderAwardVerificationItemResultDto> VerifyChecklistItemAsync(VerifyChecklistItemDto dto);

    /// <summary>
    /// Complete verification for a bidder
    /// </summary>
    Task<TenderAwardVerificationBidderDto> CompleteBidderVerificationAsync(CompleteBidderVerificationDto dto);

    /// <summary>
    /// Complete the entire verification process
    /// </summary>
    Task<TenderAwardVerificationDto> CompleteVerificationAsync(Guid verificationId, CompleteVerificationDto dto);

    /// <summary>
    /// Cancel a verification process
    /// </summary>
    Task CancelVerificationAsync(Guid verificationId, string? reason = null);

    #endregion

    #region Document Management

    /// <summary>
    /// Upload a document for a verification item result
    /// </summary>
    Task<TenderAwardVerificationItemDocumentDto> UploadDocumentAsync(Guid itemResultId,
        UploadVerificationDocumentDto dto, string fileName, string logicalFileReference,
        string? contentType, long fileSize, Guid fileUploadRecordId,
        Guid centralDocumentRecordId, Guid centralDocumentVersionId);

    /// <summary>
    /// Get documents for a verification item result
    /// </summary>
    Task<IEnumerable<TenderAwardVerificationItemDocumentDto>> GetDocumentsByItemResultIdAsync(Guid itemResultId);

    /// <summary>
    /// Get a document by ID
    /// </summary>
    Task<TenderAwardVerificationItemDocumentDto?> GetDocumentByIdAsync(Guid documentId);

    /// <summary>
    /// Delete a document
    /// </summary>
    Task DeleteDocumentAsync(Guid documentId);

    #endregion
}
