using ErpSystem.Application.HR.Extensions;
using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.HR.Performance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Core.Services.HR.Appraisal;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

#region Appraisal Grade Definition

public class AppraisalGradeDefinitionService : IAppraisalGradeDefinitionService
{
    private readonly IGenericRepository<AppraisalGradeDefinition> _gradeDefinitionRepository;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<LeaveService> _logger;

    public AppraisalGradeDefinitionService(
        IGenericRepository<AppraisalGradeDefinition> gradeDefinitionRepository,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ILogger<LeaveService> logger)
    {
        _gradeDefinitionRepository = gradeDefinitionRepository;
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

    // A grade definition owned by another tenant is reported as missing rather than forbidden, so the
    // endpoints do not confirm that the id exists elsewhere.
    private async Task<AppraisalGradeDefinition> GetOwnedAsync(Guid id)
    {
        var entity = await _gradeDefinitionRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Grade definition with ID '{id}' not found.");
        return entity;
    }

    /// <summary>
    /// Refuses an overall band the one grade resolver could not use (performance closure A2).
    /// A band needs both bounds inside 0–100, a rating for the talent feed, and no overlap with
    /// another active band — two bands claiming one score made the grade depend on row order
    /// (P-6). A definition with no bounds is an item-only grade and is not checked.
    /// </summary>
    private async Task ValidateOverallBandAsync(AppraisalGradeDefinition candidate, CancellationToken cancellationToken)
    {
        var min = candidate.OverallMinScore;
        var max = candidate.OverallMaxScore;
        if (min is null && max is null) return;

        if (min is null || max is null)
            throw new ArgumentException("An overall band needs both a minimum and a maximum score.");
        if (min < AppraisalScoring.MinScore || max > AppraisalScoring.MaxScore || min > max)
            throw new ArgumentException(
                $"An overall band must lie within {AppraisalScoring.MinScore:0}–{AppraisalScoring.MaxScore:0} with its minimum no higher than its maximum.");
        if (candidate.MappedRating is null)
            throw new ArgumentException(
                "An overall band needs a mapped rating — it is what the talent pools and the rating reports read.");

        if (!candidate.IsActive) return;

        var tenantId = GetTenantId();
        var clash = await _gradeDefinitionRepository.GetQueryable()
            .Where(g => g.TenantId == tenantId
                     && g.Id != candidate.Id
                     && g.IsActive
                     && g.OverallMinScore != null
                     && g.OverallMaxScore != null
                     && g.OverallMinScore <= max
                     && min <= g.OverallMaxScore)
            .Select(g => new { g.GradeName, g.OverallMinScore, g.OverallMaxScore })
            .FirstOrDefaultAsync(cancellationToken);

        if (clash != null)
            throw new ArgumentException(
                $"The band {min:0.##}–{max:0.##} overlaps \"{clash.GradeName}\" ({clash.OverallMinScore:0.##}–{clash.OverallMaxScore:0.##}). Each score must fall in one band only.");
    }

    public async Task<AppraisalGradeDefinitionDto> CreateAsync(CreateAppraisalGradeDefinitionDto createDto, CancellationToken cancellationToken = default)
    {
        var gradeDefinition = createDto.ToEntity();
        gradeDefinition.TenantId = GetTenantId();

        await ValidateOverallBandAsync(gradeDefinition, cancellationToken);

        await _gradeDefinitionRepository.AddAsync(gradeDefinition);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Appraisal grade definition created successfully: {gradeDefinitionId}", gradeDefinition.Id);

        return gradeDefinition.ToDto();
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(id);

        await _gradeDefinitionRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Grade definition deleted: {id}", id);

        return true;
    }

    public async Task<IEnumerable<AppraisalGradeDefinitionDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var gradeDefinitions = await _gradeDefinitionRepository.GetQueryable()
            .Where(g => g.TenantId == tenantId)
            .ToListAsync(cancellationToken);
        return gradeDefinitions.ToDtoList();
    }

    public async Task<AppraisalGradeDefinitionDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var gradeDefinition = await _gradeDefinitionRepository.GetQueryable()
            .Where(g => g.TenantId == GetTenantId())
            .FirstOrDefaultAsync(g => g.Id == id, cancellationToken);

        if (gradeDefinition == null)
            throw new ArgumentException($"Grade definition with ID '{id}' not found.");

        return gradeDefinition.ToDto();
    }

    public async Task<PagedResult<AppraisalGradeDefinitionDto>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var definitionsQuery = _gradeDefinitionRepository.GetQueryable()
            .Where(g => g.TenantId == tenantId);
        var totalCount = await definitionsQuery.CountAsync(cancellationToken);

        var pagedGrades = await definitionsQuery.OrderBy(g => g.GradeName)
                                                .Skip((pageNumber - 1) * pageSize)
                                                .Take(pageSize)
                                                .ToListAsync(cancellationToken);

        var gradeDtos = pagedGrades.ToDtoList();

        return new PagedResult<AppraisalGradeDefinitionDto>
        {
            Items = gradeDtos,
            TotalCount = totalCount,
            Page = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<AppraisalGradeDefinitionDto> UpdateAsync(UpdateAppraisalGradeDefinitionDto updateDto, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(updateDto.Id);

        updateDto.UpdateEntity(entity);

        await ValidateOverallBandAsync(entity, cancellationToken);

        await _gradeDefinitionRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Appraisal grade definition updated: {Id}", updateDto.Id);

        return entity.ToDto();
    }
}

#endregion Appraisal Grade Definition
