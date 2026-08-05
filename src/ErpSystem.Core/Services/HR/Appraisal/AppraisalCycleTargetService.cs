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

#region Appraisal Cycle Target

public class AppraisalCycleTargetService : IAppraisalCycleTargetService
{
    private readonly IGenericRepository<AppraisalCycleTarget> _targetRepository;
    private readonly IGenericRepository<AppraisalCycleTargetExclusion> _exclusionRepository;
    private readonly IGenericRepository<AppraisalCycle> _cycleRepository;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<AppraisalCycleTargetService> _logger;

    public AppraisalCycleTargetService(
        IGenericRepository<AppraisalCycleTarget> targetRepository,
        IGenericRepository<AppraisalCycleTargetExclusion> exclusionRepository,
        IGenericRepository<AppraisalCycle> cycleRepository,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ILogger<AppraisalCycleTargetService> logger)
    {
        _targetRepository = targetRepository;
        _exclusionRepository = exclusionRepository;
        _cycleRepository = cycleRepository;
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

    // An appraisal cycle owned by another tenant is reported as missing rather than forbidden, so the
    // endpoints do not confirm that the id exists elsewhere.
    private async Task<AppraisalCycle> GetOwnedCycleAsync(Guid cycleId)
    {
        var entity = await _cycleRepository.GetByIdAsync(cycleId);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Appraisal cycle with ID '{cycleId}' not found.");
        return entity;
    }

    // A cycle target owned by another tenant is reported as missing rather than forbidden, so the endpoints
    // do not confirm that the id exists elsewhere.
    private async Task<AppraisalCycleTarget> GetOwnedAsync(Guid id)
    {
        var entity = await _targetRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Appraisal cycle target with ID '{id}' not found.");
        return entity;
    }

    // An exclusion owned by another tenant is reported as missing rather than forbidden.
    private async Task<AppraisalCycleTargetExclusion> GetOwnedExclusionAsync(Guid targetId, Guid exclusionId)
    {
        var entity = await _exclusionRepository.GetQueryable()
            .FirstOrDefaultAsync(e => e.Id == exclusionId && e.AppraisalCycleTargetId == targetId);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException("Exclusion not found for this cycle target.");
        return entity;
    }

    public async Task<AppraisalCycleTargetDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _targetRepository.GetQueryable()
            .Include(t => t.AppraisalCycle)
            .Include(t => t.OrganizationLevel)
            .Include(t => t.OrganizationUnit)
            .Include(t => t.Position)
            .FirstOrDefaultAsync(t => t.Id == id && t.TenantId == tenantId, cancellationToken);

        if (entity == null)
            throw new ArgumentException($"Appraisal cycle target with ID '{id}' not found.");

        return entity.ToDto();
    }

    public async Task<IEnumerable<AppraisalCycleTargetDto>> GetByCycleIdAsync(Guid cycleId, CancellationToken cancellationToken = default)
    {
        await GetOwnedCycleAsync(cycleId);
        var tenantId = GetTenantId();

        var entities = await _targetRepository.GetQueryable()
            .Include(t => t.AppraisalCycle)
            .Include(t => t.OrganizationLevel)
            .Include(t => t.OrganizationUnit)
            .Include(t => t.Position)
            .Where(t => t.TenantId == tenantId && t.AppraisalCycleId == cycleId)
            .OrderBy(t => t.TargetType)
            .ToListAsync(cancellationToken);

        return entities.ToDtoList();
    }

    public async Task<IEnumerable<AppraisalCycleTargetDto>> GetByTargetTypeAsync(AppraisalTargetType targetType, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _targetRepository.GetQueryable()
            .Include(t => t.AppraisalCycle)
            .Include(t => t.OrganizationLevel)
            .Include(t => t.OrganizationUnit)
            .Include(t => t.Position)
            .Where(t => t.TenantId == tenantId && t.TargetType == targetType)
            .ToListAsync(cancellationToken);

        return entities.ToDtoList();
    }

    public async Task<AppraisalCycleTargetDto> CreateAsync(CreateAppraisalCycleTargetDto createDto, CancellationToken cancellationToken = default)
    {
        await GetOwnedCycleAsync(createDto.AppraisalCycleId);
        var tenantId = GetTenantId();

        // Validate that exactly one target type is specified
        var targetCount = new[] { createDto.OrganizationLevelId, createDto.OrganizationUnitId, createDto.PositionId }
            .Count(id => id.HasValue);

        if (targetCount != 1)
        {
            throw new InvalidOperationException("Exactly one target type must be specified (OrganizationLevel, OrganizationUnit, or Position).");
        }

        // ── Data integrity: no duplicate scope for the same cycle (per tenant) ──────────────
        bool duplicate = createDto.TargetType switch
        {
            AppraisalTargetType.OrganizationLevel =>
                await _targetRepository.GetQueryable()
                    .AnyAsync(t => t.TenantId == tenantId
                                && t.AppraisalCycleId == createDto.AppraisalCycleId
                                && t.OrganizationLevelId == createDto.OrganizationLevelId, cancellationToken),
            AppraisalTargetType.OrganizationUnit =>
                await _targetRepository.GetQueryable()
                    .AnyAsync(t => t.TenantId == tenantId
                                && t.AppraisalCycleId == createDto.AppraisalCycleId
                                && t.OrganizationUnitId == createDto.OrganizationUnitId, cancellationToken),
            AppraisalTargetType.Position =>
                await _targetRepository.GetQueryable()
                    .AnyAsync(t => t.TenantId == tenantId
                                && t.AppraisalCycleId == createDto.AppraisalCycleId
                                && t.PositionId == createDto.PositionId, cancellationToken),
            _ => false
        };

        if (duplicate)
            throw new InvalidOperationException(
                $"A cycle target of type '{createDto.TargetType}' already exists for this scope in the specified appraisal cycle.");

        var entity = createDto.ToEntity();
        entity.TenantId = tenantId;

        await _targetRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Reload with includes
        entity = await _targetRepository.GetQueryable()
            .Include(t => t.AppraisalCycle)
            .Include(t => t.OrganizationLevel)
            .Include(t => t.OrganizationUnit)
            .Include(t => t.Position)
            .FirstOrDefaultAsync(t => t.Id == entity.Id && t.TenantId == tenantId, cancellationToken);

        _logger.LogInformation("Appraisal cycle target created successfully: {targetId}", entity!.Id);

        return entity.ToDto();
    }

    public async Task<AppraisalCycleTargetDto> UpdateAsync(UpdateAppraisalCycleTargetDto updateDto, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(updateDto.Id);

        // Validate that exactly one target type is specified
        var targetCount = new[] { updateDto.OrganizationLevelId, updateDto.OrganizationUnitId, updateDto.PositionId }
            .Count(id => id.HasValue);

        if (targetCount != 1)
        {
            throw new InvalidOperationException("Exactly one target type must be specified.");
        }

        updateDto.UpdateEntity(entity);

        await _targetRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Appraisal cycle target updated successfully: {targetId}", entity.Id);

        return entity.ToDto();
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(id);

        await _targetRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Appraisal cycle target deleted: {targetId}", id);

        return true;
    }

    public async Task<bool> ValidateTargetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(id);

        // Validate that exactly one target type is specified
        var targetCount = new[] { entity.OrganizationLevelId, entity.OrganizationUnitId, entity.PositionId }
            .Count(x => x.HasValue);

        var isValid = targetCount == 1;

        if (!isValid)
        {
            _logger.LogWarning("Appraisal cycle target {targetId} has invalid configuration. Target count: {targetCount}", id, targetCount);
        }

        return isValid;
    }

    // ─── Exclusions ───────────────────────────────────────────────────────────

    public async Task<AppraisalCycleTargetExclusionDto> AddExclusionAsync(
        Guid targetId, CreateAppraisalCycleTargetExclusionDto dto, CancellationToken cancellationToken = default)
    {
        await GetOwnedAsync(targetId);
        var tenantId = GetTenantId();

        var entity = dto.ToEntity();
        entity.AppraisalCycleTargetId = targetId;
        entity.IsActive = true;
        entity.TenantId = tenantId;

        await _exclusionRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Exclusion added to cycle target {TargetId}: {ExclusionId}", targetId, entity.Id);

        entity = await _exclusionRepository.GetQueryable()
            .Include(e => e.OrganizationLevel)
            .Include(e => e.OrganizationUnit)
            .Include(e => e.Position)
            .Include(e => e.Employee)
            .FirstOrDefaultAsync(e => e.Id == entity.Id && e.TenantId == tenantId, cancellationToken);

        return entity!.ToDto();
    }

    public async Task<IEnumerable<AppraisalCycleTargetExclusionDto>> GetExclusionsAsync(
        Guid targetId, CancellationToken cancellationToken = default)
    {
        await GetOwnedAsync(targetId);
        var tenantId = GetTenantId();

        var entities = await _exclusionRepository.GetQueryable()
            .Where(e => e.TenantId == tenantId && e.AppraisalCycleTargetId == targetId)
            .Include(e => e.OrganizationLevel)
            .Include(e => e.OrganizationUnit)
            .Include(e => e.Position)
            .Include(e => e.Employee)
            .OrderByDescending(e => e.IsActive)
            .ToListAsync(cancellationToken);

        return entities.ToDtoList();
    }

    public async Task<AppraisalCycleTargetExclusionDto> UpdateExclusionAsync(
        Guid targetId, UpdateAppraisalCycleTargetExclusionDto dto, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedExclusionAsync(targetId, dto.Id);

        dto.UpdateEntity(entity);
        await _exclusionRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Cycle target exclusion updated: {ExclusionId}", entity.Id);
        return entity.ToDto();
    }

    public async Task<bool> RemoveExclusionAsync(
        Guid targetId, Guid exclusionId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedExclusionAsync(targetId, exclusionId);

        await _exclusionRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Cycle target exclusion removed: {ExclusionId}", exclusionId);
        return true;
    }
}

#endregion Appraisal Cycle Target
