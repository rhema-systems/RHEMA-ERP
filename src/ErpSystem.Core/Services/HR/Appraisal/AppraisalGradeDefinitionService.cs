using ErpSystem.Application.HR.Extensions;
using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.HR.Performance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
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

    public async Task<AppraisalGradeDefinitionDto> CreateAsync(CreateAppraisalGradeDefinitionDto createDto, CancellationToken cancellationToken = default)
    {
        var gradeDefinition = createDto.ToEntity();
        gradeDefinition.TenantId = GetTenantId();

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

        await _gradeDefinitionRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Appraisal grade definition updated: {Id}", updateDto.Id);

        return entity.ToDto();
    }
}

#endregion Appraisal Grade Definition
