using ErpSystem.Core.DTOs.Maintenance;

namespace ErpSystem.Core.Interfaces.Maintenance;

/// <summary>
/// Service interface for Job Card operations
/// </summary>
public interface IJobCardService
{
    /// <summary>
    /// Gets a paginated list of job cards with optional filtering
    /// </summary>
    Task<PagedResult<JobCardListDto>> GetJobCardsPagedAsync(JobCardFilterDto filter);

    /// <summary>
    /// Gets a specific job card by ID with all details
    /// </summary>
    Task<JobCardDto?> GetJobCardByIdAsync(Guid id);

    /// <summary>
    /// Gets a specific job card by job card number
    /// </summary>
    Task<JobCardDto?> GetJobCardByNumberAsync(string jobCardNumber);

    /// <summary>
    /// Creates a new job card
    /// </summary>
    Task<JobCardDto> CreateJobCardAsync(CreateJobCardDto createDto);

    /// <summary>
    /// Updates an existing job card (only allowed in Draft status)
    /// </summary>
    Task<JobCardDto> UpdateJobCardAsync(Guid id, UpdateJobCardDto updateDto);

    /// <summary>
    /// Deletes a job card (only allowed if not submitted for approval)
    /// </summary>
    Task DeleteJobCardAsync(Guid id);

    /// <summary>
    /// Submits a job card for approval workflow
    /// </summary>
    Task<JobCardDto> SubmitJobCardAsync(Guid id, SubmitJobCardDto submitDto);

    /// <summary>
    /// Processes an approval action on a job card
    /// </summary>
    Task<JobCardDto> ProcessApprovalAsync(Guid id, JobCardApprovalActionDto approvalDto);

    /// <summary>
    /// Cancels a job card (marks as cancelled)
    /// </summary>
    Task<JobCardDto> CancelJobCardAsync(Guid id, string reason);

    /// <summary>
    /// Adds a comment to a job card
    /// </summary>
    Task<JobCardCommentDto> AddCommentAsync(Guid id, AddJobCardCommentDto commentDto);

    /// <summary>
    /// Gets all comments for a job card
    /// </summary>
    Task<List<JobCardCommentDto>> GetCommentsAsync(Guid id);

    /// <summary>
    /// Gets approval history for a job card
    /// </summary>
    Task<List<JobCardApprovalStepDto>> GetApprovalHistoryAsync(Guid id);

    /// <summary>
    /// Gets job cards pending approval for the current user
    /// </summary>
    Task<PagedResult<JobCardListDto>> GetPendingApprovalsAsync(JobCardFilterDto filter);

    /// <summary>
    /// Gets job cards that have been approved and are ready for work order generation
    /// </summary>
    Task<List<JobCardListDto>> GetApprovedJobCardsAsync();

    /// <summary>
    /// Generates a work order from an approved job card
    /// </summary>
    Task<Guid> GenerateWorkOrderAsync(Guid jobCardId);

    /// <summary>
    /// Gets job cards by asset ID
    /// </summary>
    Task<List<JobCardListDto>> GetJobCardsByAssetAsync(Guid assetId);

    /// <summary>
    /// Gets job cards requested by a specific user
    /// </summary>
    Task<List<JobCardListDto>> GetJobCardsByRequesterAsync(Guid requesterId);

    /// <summary>
    /// Gets job cards assigned to a specific technician
    /// </summary>
    Task<List<JobCardListDto>> GetJobCardsByTechnicianAsync(Guid technicianId);

    /// <summary>
    /// Gets dashboard statistics for job cards
    /// </summary>
    Task<JobCardDashboardStatsDto> GetDashboardStatsAsync();

    /// <summary>
    /// Uploads a document/attachment to a job card
    /// </summary>
    Task<JobCardDocumentDto> UploadDocumentAsync(Guid id, Stream fileStream, string fileName, string documentType);

    /// <summary>
    /// Deletes a document from a job card
    /// </summary>
    Task DeleteDocumentAsync(Guid jobCardId, Guid documentId);

    /// <summary>
    /// Gets all documents for a job card
    /// </summary>
    Task<List<JobCardDocumentDto>> GetDocumentsAsync(Guid id);

    /// <summary>
    /// Completes a job card with completion details
    /// </summary>
    Task<JobCardDto> CompleteJobCardAsync(Guid id, CompleteJobCardDto completeDto);

    /// <summary>
    /// Performs quality check on a completed job card
    /// </summary>
    Task<JobCardDto> PerformQualityCheckAsync(Guid id, JobCardQualityCheckDto qualityCheckDto);

    /// <summary>
    /// Records customer/user acceptance of completed work
    /// </summary>
    Task<JobCardDto> RecordAcceptanceAsync(Guid id, JobCardAcceptanceDto acceptanceDto);

    /// <summary>
    /// Generates a certificate for a completed and accepted job card
    /// </summary>
    Task<JobCardCertificateDto> GenerateCertificateAsync(Guid id, GenerateJobCardCertificateDto certificateDto);

    /// <summary>
    /// Gets all certificates for a job card
    /// </summary>
    Task<List<JobCardCertificateDto>> GetCertificatesAsync(Guid id);

    /// <summary>
    /// Gets a specific certificate by ID
    /// </summary>
    Task<JobCardCertificateDto?> GetCertificateByIdAsync(Guid certificateId);
}

/// <summary>
/// DTO for job card dashboard statistics
/// </summary>
public class JobCardDashboardStatsDto
{
    public int TotalJobCards { get; set; }
    public int DraftJobCards { get; set; }
    public int SubmittedJobCards { get; set; }
    public int UnderReviewJobCards { get; set; }
    public int ApprovedJobCards { get; set; }
    public int RejectedJobCards { get; set; }
    public int CancelledJobCards { get; set; }
    public int PendingApprovals { get; set; }
    public int ReadyForWorkOrder { get; set; }
    public int ConvertedToWorkOrders { get; set; }
    public decimal TotalEstimatedCost { get; set; }
    public double TotalEstimatedHours { get; set; }
    public List<JobCardsByPriorityDto> ByPriority { get; set; } = new();
    public List<JobCardsByMaintenanceTypeDto> ByMaintenanceType { get; set; } = new();
    public List<JobCardsByStatusDto> ByStatus { get; set; } = new();
}

public class JobCardsByPriorityDto
{
    public string Priority { get; set; } = string.Empty;
    public int Count { get; set; }
    public decimal EstimatedCost { get; set; }
}

public class JobCardsByMaintenanceTypeDto
{
    public string MaintenanceType { get; set; } = string.Empty;
    public int Count { get; set; }
    public decimal EstimatedCost { get; set; }
}

public class JobCardsByStatusDto
{
    public string Status { get; set; } = string.Empty;
    public int Count { get; set; }
    public decimal EstimatedCost { get; set; }
}