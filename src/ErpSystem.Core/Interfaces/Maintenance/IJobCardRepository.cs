using ErpSystem.Core.DTOs.Maintenance;
using ErpSystem.Core.Entities.Maintenance;
using ErpSystem.Shared;

namespace ErpSystem.Core.Interfaces.Maintenance;

/// <summary>
/// Repository interface for JobCard operations
/// </summary>
public interface IJobCardRepository
{
    /// <summary>
    /// Gets a paginated list of job cards with filtering
    /// </summary>
    Task<PagedResult<JobCardListDto>> GetPagedAsync(JobCardFilterDto filter);

    /// <summary>
    /// Gets job card by ID with full details
    /// </summary>
    Task<JobCardDto?> GetByIdWithDetailsAsync(Guid id);

    /// <summary>
    /// Gets job card entity by ID
    /// </summary>
    Task<JobCard?> GetByIdAsync(Guid id);

    /// <summary>
    /// Gets job card by job card number
    /// </summary>
    Task<JobCardDto?> GetByNumberAsync(string jobCardNumber);

    /// <summary>
    /// Adds a new job card
    /// </summary>
    Task AddAsync(JobCard jobCard);

    /// <summary>
    /// Updates an existing job card
    /// </summary>
    Task UpdateAsync(JobCard jobCard);

    /// <summary>
    /// Deletes a job card
    /// </summary>
    Task DeleteAsync(Guid id);

    /// <summary>
    /// Gets pending approvals for a specific user
    /// </summary>
    Task<PagedResult<JobCardListDto>> GetPendingApprovalsAsync(JobCardFilterDto filter, Guid userId);

    /// <summary>
    /// Gets approved job cards ready for work order generation
    /// </summary>
    Task<List<JobCardListDto>> GetApprovedJobCardsAsync();

    /// <summary>
    /// Gets job cards by asset ID
    /// </summary>
    Task<List<JobCardListDto>> GetByAssetAsync(Guid assetId);

    /// <summary>
    /// Gets job cards by requester ID
    /// </summary>
    Task<List<JobCardListDto>> GetByRequesterAsync(Guid requesterId);

    /// <summary>
    /// Gets job cards by technician ID
    /// </summary>
    Task<List<JobCardListDto>> GetByTechnicianAsync(Guid technicianId);

    /// <summary>
    /// Gets dashboard statistics
    /// </summary>
    Task<JobCardDashboardStatsDto> GetDashboardStatsAsync();

    /// <summary>
    /// Gets the next sequence number for job card number generation
    /// </summary>
    Task<int> GetNextSequenceNumberAsync(int year);

    /// <summary>
    /// Gets the next sequence number for certificate number generation
    /// </summary>
    Task<int> GetNextCertificateSequenceNumberAsync(int year);

    /// <summary>
    /// Adds a comment to a job card
    /// </summary>
    Task AddCommentAsync(JobCardComment comment);

    /// <summary>
    /// Gets comments for a job card
    /// </summary>
    Task<List<JobCardCommentDto>> GetCommentsAsync(Guid jobCardId);

    /// <summary>
    /// Gets approval history for a job card
    /// </summary>
    Task<List<JobCardApprovalStepDto>> GetApprovalHistoryAsync(Guid jobCardId);

    /// <summary>
    /// Adds a document to a job card
    /// </summary>
    Task AddDocumentAsync(JobCardDocument document);

    /// <summary>
    /// Deletes a document from a job card
    /// </summary>
    Task DeleteDocumentAsync(Guid documentId);

    /// <summary>
    /// Gets documents for a job card
    /// </summary>
    Task<List<JobCardDocumentDto>> GetDocumentsAsync(Guid jobCardId);

    /// <summary>
    /// Gets available work order types for job card to work order conversion
    /// </summary>
    Task<List<WorkOrderTypeDto>> GetWorkOrderTypesAsync();

    /// <summary>
    /// Adds a certificate to a job card
    /// </summary>
    Task AddCertificateAsync(JobCardCertificate certificate);

    /// <summary>
    /// Gets certificates for a job card
    /// </summary>
    Task<List<JobCardCertificateDto>> GetCertificatesAsync(Guid jobCardId);
}
