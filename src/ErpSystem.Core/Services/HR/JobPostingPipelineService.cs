using ErpSystem.Application.HR.Extensions;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR.Recruitment;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

// ============================================================================
// JOB POSTING SERVICE
// ============================================================================

public class JobPostingService : IJobPostingService
{
    private readonly IJobPostingRepository _postingRepository;
    private readonly IGenericRepository<JobPostingAttachment> _attachmentRepository;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<JobPostingService> _logger;

    public JobPostingService(
        IJobPostingRepository postingRepository,
        IGenericRepository<JobPostingAttachment> attachmentRepository,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ILogger<JobPostingService> logger)
    {
        _postingRepository = postingRepository;
        _attachmentRepository = attachmentRepository;
        _currentUserProvider = currentUserProvider;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    // The ApplicationDbContext is registered without a tenant, so its global tenant query-filter and
    // TenantId auto-stamp are inert. Following the RHEMA convention, this service scopes reads/writes to
    // the current tenant explicitly.
    private Guid GetTenantId()
    {
        var tenantId = _currentUserProvider.TenantId;
        if (tenantId == Guid.Empty)
            throw new InvalidOperationException("No tenant is associated with the current user.");
        return tenantId;
    }

    // A posting owned by another tenant is reported as missing rather than forbidden, so the endpoints do
    // not confirm that the id exists elsewhere.
    private async Task<JobPosting> GetOwnedAsync(Guid id)
    {
        var entity = await _postingRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Job posting with ID '{id}' not found.");
        return entity;
    }

    private async Task<JobPostingAttachment?> TryGetOwnedAttachmentAsync(Guid id)
    {
        var entity = await _attachmentRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            return null;
        return entity;
    }

    public async Task<JobPostingDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(id);
        return entity.ToDto();
    }

    public async Task<IEnumerable<JobPostingSummaryDto>> GetByVacancyIdAsync(Guid vacancyId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _postingRepository.GetByVacancyIdAsync(vacancyId);
        return entities.Where(e => e.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<IEnumerable<JobPostingSummaryDto>> GetActivePostingsAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _postingRepository.GetActivePostingsAsync();
        return entities.Where(e => e.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<IEnumerable<JobPostingSummaryDto>> GetByStatusAsync(JobPostingStatus status, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _postingRepository.GetByStatusAsync(status);
        return entities.Where(e => e.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<IEnumerable<JobPostingSummaryDto>> GetByChannelAsync(JobPostingChannel channel, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _postingRepository.GetByChannelAsync(channel);
        return entities.Where(e => e.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<IEnumerable<JobPostingSummaryDto>> GetExpiredActivePostingsAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _postingRepository.GetExpiredActivePostingsAsync();
        return entities.Where(e => e.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<JobPostingDto> CreateAsync(CreateJobPostingDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");

        var entity = createDto.ToEntity(current, createdByUserId);
        entity.Status = JobPostingStatus.Draft;

        await _postingRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Job posting created for vacancy {VacancyId} on channel {Channel}", createDto.JobVacancyId, createDto.Channel);
        return entity.ToDto();
    }

    public async Task<JobPostingDto> UpdateAsync(UpdateJobPostingDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(updateDto.Id);

        entity.UpdateEntity(updateDto, updatedByUserId);
        await _postingRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Job posting updated: {PostingId}", entity.Id);
        return entity.ToDto();
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(id);

        if (entity.Status == JobPostingStatus.Published)
            throw new InvalidOperationException("A published job posting must be expired before it can be deleted.");

        await _postingRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> ExpireAsync(Guid postingId, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(postingId);

        entity.Status = JobPostingStatus.Expired;
        entity.IsActive = false;
        entity.ExpiryDate = DateTime.UtcNow;

        await _postingRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }
    public async Task<JobPostingDto?> GetByExternalPostingIdAsync(string externalPostingId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _postingRepository.GetByExternalPostingIdAsync(externalPostingId);
        return entity == null || entity.TenantId != tenantId ? null : entity.ToDto();
    }

    public async Task<JobPostingDto> PublishAsync(Guid postingId, DateTime? actualPublishDate, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(postingId);

        entity.Status = JobPostingStatus.Published;
        entity.IsActive = true;
        entity.ActualPublishDate = actualPublishDate ?? DateTime.UtcNow;
        entity.PostedById ??= updatedByUserId;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = updatedByUserId.ToString();

        await _postingRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Job posting {PostingId} published (actual {Date:d}).", entity.Id, entity.ActualPublishDate);
        return entity.ToDto();
    }

    // ── Attachments ─────────────────────────────────────────────────────────

    public async Task<JobPostingAttachmentDto> AddAttachmentAsync(
        CreateJobPostingAttachmentDto createDto, Guid tenantId, Guid uploadedByUserId, CancellationToken cancellationToken = default)
    {
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");

        await GetOwnedAsync(createDto.JobPostingId);

        var entity = new JobPostingAttachment
        {
            TenantId = current,
            JobPostingId = createDto.JobPostingId,
            FileName = createDto.FileName,
            FilePath = createDto.FilePath,
            Description = createDto.Description,
            UploadDate = DateTime.UtcNow,
            UploadedById = uploadedByUserId,
            CreatedById = uploadedByUserId,
            CreatedBy = uploadedByUserId.ToString(),
        };

        await _attachmentRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<IEnumerable<JobPostingAttachmentDto>> GetAttachmentsAsync(Guid postingId, CancellationToken cancellationToken = default)
    {
        await GetOwnedAsync(postingId);
        var tenantId = GetTenantId();
        var entities = await _attachmentRepository.FindAsync(a => a.JobPostingId == postingId && !a.IsDeleted, a => a.UploadedBy);
        return entities.Where(a => a.TenantId == tenantId).Select(a => a.ToDto());
    }

    public async Task<bool> DeleteAttachmentAsync(Guid attachmentId, CancellationToken cancellationToken = default)
    {
        var entity = await TryGetOwnedAttachmentAsync(attachmentId);
        if (entity == null)
            return false;

        await _attachmentRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }
}

// ============================================================================
// RECRUITMENT PIPELINE SERVICE
// ============================================================================

public class RecruitmentPipelineService : IRecruitmentPipelineService
{
    private readonly IRecruitmentPipelineRepository _pipelineRepository;
    private readonly IRecruitmentPipelineStageRepository _stageRepository;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<RecruitmentPipelineService> _logger;

    public RecruitmentPipelineService(
        IRecruitmentPipelineRepository pipelineRepository,
        IRecruitmentPipelineStageRepository stageRepository,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ILogger<RecruitmentPipelineService> logger)
    {
        _pipelineRepository = pipelineRepository;
        _stageRepository = stageRepository;
        _currentUserProvider = currentUserProvider;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    // The ApplicationDbContext is registered without a tenant, so its global tenant query-filter and
    // TenantId auto-stamp are inert. Following the RHEMA convention, this service scopes reads/writes to
    // the current tenant explicitly.
    private Guid GetTenantId()
    {
        var tenantId = _currentUserProvider.TenantId;
        if (tenantId == Guid.Empty)
            throw new InvalidOperationException("No tenant is associated with the current user.");
        return tenantId;
    }

    // A pipeline owned by another tenant is reported as missing rather than forbidden, so the endpoints do
    // not confirm that the id exists elsewhere.
    private async Task<RecruitmentPipeline> GetOwnedAsync(Guid id)
    {
        var entity = await _pipelineRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Recruitment pipeline with ID '{id}' not found.");
        return entity;
    }

    private async Task<RecruitmentPipeline> GetOwnedWithStagesAsync(Guid id)
    {
        var entity = await _pipelineRepository.GetWithStagesAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Recruitment pipeline with ID '{id}' not found.");
        return entity;
    }

    private async Task<RecruitmentPipelineStage> GetOwnedStageAsync(Guid id)
    {
        var entity = await _stageRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Pipeline stage with ID '{id}' not found.");
        return entity;
    }

    public async Task<RecruitmentPipelineDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedWithStagesAsync(id);
        return entity.ToDto();
    }

    public async Task<IEnumerable<RecruitmentPipelineSummaryDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _pipelineRepository.GetAllWithStagesAsync();
        return entities.Where(e => e.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<RecruitmentPipelineDto?> GetDefaultPipelineAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _pipelineRepository.GetDefaultPipelineAsync();
        return entity == null || entity.TenantId != tenantId ? null : entity.ToDto();
    }

    public async Task<RecruitmentPipelineDto> CreateAsync(CreateRecruitmentPipelineDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");

        // If this is set as default, clear the existing default for this tenant only.
        if (createDto.IsDefault)
        {
            var existingDefault = await _pipelineRepository.GetDefaultPipelineAsync();
            if (existingDefault != null && existingDefault.TenantId == current)
            {
                existingDefault.IsDefault = false;
                await _pipelineRepository.UpdateAsync(existingDefault);
            }
        }

        var entity = createDto.ToEntity(current, createdByUserId);
        await _pipelineRepository.AddAsync(entity);

        // Add stages if provided
        foreach (var stageDto in createDto.Stages)
        {
            stageDto.RecruitmentPipelineId = entity.Id;
            var stage = stageDto.ToEntity(current, createdByUserId);
            await _stageRepository.AddAsync(stage);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Recruitment pipeline created: {Name}", entity.Name);
        return entity.ToDto();
    }

    public async Task<RecruitmentPipelineDto> ClonePipelineAsync(
        Guid sourceId, string newName, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");

        if (string.IsNullOrWhiteSpace(newName))
            throw new ArgumentException("A name is required for the cloned pipeline.", nameof(newName));

        var source = await GetOwnedWithStagesAsync(sourceId);

        var trimmedName = newName.Trim();
        var duplicate = await _pipelineRepository.GetByNameAsync(trimmedName);
        if (duplicate != null && duplicate.TenantId == current)
            throw new InvalidOperationException($"A pipeline named '{trimmedName}' already exists.");

        var clone = new RecruitmentPipeline
        {
            TenantId = current,
            Name = trimmedName,
            Description = source.Description,
            IsDefault = false, // a clone is never the default
            IsActive = source.IsActive,
            DefaultTimeToCompleteDays = source.DefaultTimeToCompleteDays,
            CreatedById = createdByUserId,
            CreatedBy = createdByUserId.ToString(),
        };
        await _pipelineRepository.AddAsync(clone);

        foreach (var stage in source.Stages.Where(s => !s.IsDeleted).OrderBy(s => s.Order))
        {
            await _stageRepository.AddAsync(new RecruitmentPipelineStage
            {
                TenantId = current,
                RecruitmentPipelineId = clone.Id,
                Name = stage.Name,
                Description = stage.Description,
                Order = stage.Order,
                StageType = stage.StageType,
                IsFinalStage = stage.IsFinalStage,
                IsActive = stage.IsActive,
                IsRequired = stage.IsRequired,
                DefaultTimeToCompleteDays = stage.DefaultTimeToCompleteDays,
                CanSkip = stage.CanSkip,
                CanRepeat = stage.CanRepeat,
                MaxAttempts = stage.MaxAttempts,
                Instructions = stage.Instructions,
                CreatedById = createdByUserId,
                CreatedBy = createdByUserId.ToString(),
            });
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Recruitment pipeline cloned: {Source} -> {NewName}", source.Name, clone.Name);

        // Reload with stages for a complete DTO.
        var reloaded = await _pipelineRepository.GetWithStagesAsync(clone.Id);
        return (reloaded ?? clone).ToDto();
    }

    public async Task<RecruitmentPipelineDto> UpdateAsync(UpdateRecruitmentPipelineDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedWithStagesAsync(updateDto.Id);

        if (updateDto.IsDefault && !entity.IsDefault)
        {
            var existingDefault = await _pipelineRepository.GetDefaultPipelineAsync();
            if (existingDefault != null && existingDefault.Id != entity.Id && existingDefault.TenantId == entity.TenantId)
            {
                existingDefault.IsDefault = false;
                await _pipelineRepository.UpdateAsync(existingDefault);
            }
        }

        entity.UpdateEntity(updateDto, updatedByUserId);
        await _pipelineRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Recruitment pipeline updated: {Name}", entity.Name);
        return entity.ToDto();
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(id);

        if (entity.IsDefault)
            throw new InvalidOperationException("The default recruitment pipeline cannot be deleted. Assign a new default first.");

        await _pipelineRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ── Stage operations ──────────────────────────────────────────────────────

    public async Task<RecruitmentPipelineStageDto> AddStageAsync(CreateRecruitmentPipelineStageDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");

        await GetOwnedAsync(createDto.RecruitmentPipelineId);

        var entity = createDto.ToEntity(current, createdByUserId);
        await _stageRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<IEnumerable<RecruitmentPipelineStageDto>> GetStagesAsync(Guid pipelineId, CancellationToken cancellationToken = default)
    {
        await GetOwnedAsync(pipelineId);
        var tenantId = GetTenantId();
        var entities = await _stageRepository.GetByPipelineIdAsync(pipelineId);
        return entities.Where(e => e.TenantId == tenantId).Select(e => e.ToDto());
    }

    public async Task<RecruitmentPipelineStageDto> UpdateStageAsync(UpdateRecruitmentPipelineStageDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedStageAsync(updateDto.Id);

        entity.UpdateEntity(updateDto, updatedByUserId);
        await _stageRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> DeleteStageAsync(Guid stageId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedStageAsync(stageId);

        await _stageRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> ReorderStagesAsync(Guid pipelineId, IEnumerable<(Guid StageId, int NewOrder)> stageOrders, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        await GetOwnedAsync(pipelineId);
        var tenantId = GetTenantId();
        var stages = (await _stageRepository.GetByPipelineIdAsync(pipelineId))
            .Where(s => s.TenantId == tenantId);
        var stageMap = stages.ToDictionary(s => s.Id);

        foreach (var (stageId, newOrder) in stageOrders)
        {
            if (stageMap.TryGetValue(stageId, out var stage))
            {
                stage.Order = newOrder;
                await _stageRepository.UpdateAsync(stage);
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<RecruitmentPipelineDto?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _pipelineRepository.GetByNameAsync(name);
        return entity == null || entity.TenantId != tenantId ? null : entity.ToDto();
    }

    public async Task<IEnumerable<RecruitmentPipelineStageDto>> GetStagesByTypeAsync(RecruitmentPipelineStageType stageType, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _stageRepository.GetByStageTypeAsync(stageType);
        return entities.Where(e => e.TenantId == tenantId).Select(e => e.ToDto());
    }

    public async Task<RecruitmentPipelineStageDto?> GetFinalStageAsync(Guid pipelineId, CancellationToken cancellationToken = default)
    {
        await GetOwnedAsync(pipelineId);
        var tenantId = GetTenantId();
        var entity = await _stageRepository.GetFinalStageAsync(pipelineId);
        return entity == null || entity.TenantId != tenantId ? null : entity.ToDto();
    }

    public async Task<int> GetMaxStageOrderAsync(Guid pipelineId, CancellationToken cancellationToken = default)
    {
        await GetOwnedAsync(pipelineId);
        var tenantId = GetTenantId();
        var stages = await _stageRepository.GetByPipelineIdAsync(pipelineId);
        return stages.Where(s => s.TenantId == tenantId).Select(s => s.Order).DefaultIfEmpty(0).Max();
    }
}
