using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Interfaces.HR;

// ============================================================================
// JOB POSTING SERVICE
// ============================================================================

#region Job Posting Service

public interface IJobPostingService
{
    // Queries
    Task<JobPostingDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<JobPostingSummaryDto>> GetByVacancyIdAsync(Guid vacancyId, CancellationToken cancellationToken = default);
    Task<IEnumerable<JobPostingSummaryDto>> GetActivePostingsAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<JobPostingSummaryDto>> GetByStatusAsync(JobPostingStatus status, CancellationToken cancellationToken = default);
    Task<IEnumerable<JobPostingSummaryDto>> GetByChannelAsync(JobPostingChannel channel, CancellationToken cancellationToken = default);
    Task<IEnumerable<JobPostingSummaryDto>> GetExpiredActivePostingsAsync(CancellationToken cancellationToken = default);
    Task<JobPostingDto?> GetByExternalPostingIdAsync(string externalPostingId, CancellationToken cancellationToken = default);

    // CRUD
    Task<JobPostingDto> CreateAsync(CreateJobPostingDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default);
    Task<JobPostingDto> UpdateAsync(UpdateJobPostingDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    // Workflow
    Task<bool> ExpireAsync(Guid postingId, Guid updatedByUserId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks a posting as actually published on its channel: sets Status = Published and stamps
    /// <c>ActualPublishDate</c> (defaults to now; HR may pass the real date a print advert ran).
    /// </summary>
    Task<JobPostingDto> PublishAsync(Guid postingId, DateTime? actualPublishDate, Guid updatedByUserId, CancellationToken cancellationToken = default);

    // Attachments (newspaper artwork, agency brief, proof of publication)
    Task<JobPostingAttachmentDto> AddAttachmentAsync(CreateJobPostingAttachmentDto createDto, Guid tenantId, Guid uploadedByUserId, CancellationToken cancellationToken = default);
    Task<IEnumerable<JobPostingAttachmentDto>> GetAttachmentsAsync(Guid postingId, CancellationToken cancellationToken = default);
    Task<bool> DeleteAttachmentAsync(Guid attachmentId, CancellationToken cancellationToken = default);
}

#endregion

// ============================================================================
// RECRUITMENT PIPELINE SERVICE
// ============================================================================

#region Recruitment Pipeline Service

public interface IRecruitmentPipelineService
{
    // Queries
    Task<RecruitmentPipelineDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<RecruitmentPipelineSummaryDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<RecruitmentPipelineDto?> GetDefaultPipelineAsync(CancellationToken cancellationToken = default);
    Task<RecruitmentPipelineDto?> GetByNameAsync(string name, CancellationToken cancellationToken = default);

    // CRUD
    Task<RecruitmentPipelineDto> CreateAsync(CreateRecruitmentPipelineDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default);
    Task<RecruitmentPipelineDto> UpdateAsync(UpdateRecruitmentPipelineDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deep-copies a pipeline and all its stages into a new pipeline (never default), so a user can start
    /// from an existing pipeline and then add/remove/edit stages.
    /// </summary>
    Task<RecruitmentPipelineDto> ClonePipelineAsync(Guid sourceId, string newName, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default);

    // Stage operations
    Task<RecruitmentPipelineStageDto> AddStageAsync(CreateRecruitmentPipelineStageDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default);
    Task<IEnumerable<RecruitmentPipelineStageDto>> GetStagesAsync(Guid pipelineId, CancellationToken cancellationToken = default);
    Task<RecruitmentPipelineStageDto> UpdateStageAsync(UpdateRecruitmentPipelineStageDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default);
    Task<bool> DeleteStageAsync(Guid stageId, CancellationToken cancellationToken = default);
    Task<bool> ReorderStagesAsync(Guid pipelineId, IEnumerable<(Guid StageId, int NewOrder)> stageOrders, Guid updatedByUserId, CancellationToken cancellationToken = default);
    Task<IEnumerable<RecruitmentPipelineStageDto>> GetStagesByTypeAsync(RecruitmentPipelineStageType stageType, CancellationToken cancellationToken = default);
    Task<RecruitmentPipelineStageDto?> GetFinalStageAsync(Guid pipelineId, CancellationToken cancellationToken = default);
    Task<int> GetMaxStageOrderAsync(Guid pipelineId, CancellationToken cancellationToken = default);
}

#endregion
