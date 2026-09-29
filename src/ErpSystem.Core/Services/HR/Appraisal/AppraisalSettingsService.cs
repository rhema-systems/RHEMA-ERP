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

#region Appraisal Settings

public class AppraisalSettingsService : IAppraisalSettingsService
{
    private readonly IGenericRepository<AppraisalSettings> _settingsRepository;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<AppraisalSettingsService> _logger;

    public AppraisalSettingsService(
        IGenericRepository<AppraisalSettings> settingsRepository,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ILogger<AppraisalSettingsService> logger)
    {
        _settingsRepository = settingsRepository;
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

    // An appraisal settings record owned by another tenant is reported as missing rather than forbidden,
    // so the endpoints do not confirm that the id exists elsewhere.
    private async Task<AppraisalSettings> GetOwnedAsync(Guid id)
    {
        var entity = await _settingsRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Appraisal settings with ID '{id}' not found.");
        return entity;
    }

    public async Task<AppraisalSettingsDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(id);
        return entity.ToDto();
    }

    public async Task<IEnumerable<AppraisalSettingsDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _settingsRepository.GetQueryable()
            .Where(s => s.TenantId == tenantId)
            .ToListAsync(cancellationToken);
        return entities.ToDtoList();
    }

    public async Task<PagedResult<AppraisalSettingsDto>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var query = _settingsRepository.GetQueryable().Where(s => s.TenantId == tenantId);
        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query.OrderByDescending(s => s.CreatedAt)
                            .Skip((pageNumber - 1) * pageSize)
                            .Take(pageSize)
                            .ToListAsync(cancellationToken);

        return new PagedResult<AppraisalSettingsDto>
        {
            Items = items.ToDtoList(),
            TotalCount = totalCount,
            Page = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<AppraisalSettingsDto> CreateAsync(CreateAppraisalSettingsDto createDto, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();

        // Unique name check — scoped per tenant so one tenant's names do not block another's.
        var nameExists = await _settingsRepository.GetQueryable()
            .AnyAsync(s => s.TenantId == tenantId && s.SettingsName == createDto.SettingsName, cancellationToken);
        if (nameExists)
            throw new InvalidOperationException($"An appraisal settings named '{createDto.SettingsName}' already exists.");

        // Zero out weights for disabled evaluators, then validate
        if (!createDto.RequireSelfEvaluation) createDto.SelfEvaluationWeight = 0;
        if (!createDto.RequirePeerReviews) createDto.PeerEvaluationWeight = 0;
        if (!createDto.RequireManagerEvaluation) createDto.ManagerEvaluationWeight = 0;

        if (!createDto.RequireSelfEvaluation && !createDto.RequirePeerReviews && !createDto.RequireManagerEvaluation)
            throw new InvalidOperationException("At least one evaluation type (self, peer, or manager) must be enabled.");

        var totalWeight = createDto.SelfEvaluationWeight + createDto.PeerEvaluationWeight + createDto.ManagerEvaluationWeight;
        if (Math.Abs(totalWeight - 1.0m) > 0.005m)
            throw new InvalidOperationException($"Total evaluation weights must equal 1.0. Current total: {totalWeight:F2}");

        ValidateProfile(
            createDto.MinPeerEvaluators, createDto.MaxPeerEvaluators,
            createDto.MinGoalsPerEmployee, createDto.MaxGoalsPerEmployee,
            createDto.DeadlineRiskHighDays, createDto.DeadlineRiskMediumDays, createDto.DeadlineRiskLowDays);

        var entity = createDto.ToEntity();
        entity.TenantId = tenantId;

        await _settingsRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Appraisal settings created successfully: {settingsId}", entity.Id);

        return entity.ToDto();
    }

    public async Task<AppraisalSettingsDto> UpdateAsync(UpdateAppraisalSettingsDto updateDto, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(updateDto.Id);
        var tenantId = GetTenantId();

        // Unique name check (exclude self) — scoped per tenant.
        var nameExists = await _settingsRepository.GetQueryable()
            .AnyAsync(s => s.TenantId == tenantId && s.SettingsName == updateDto.SettingsName && s.Id != updateDto.Id, cancellationToken);
        if (nameExists)
            throw new InvalidOperationException($"An appraisal settings named '{updateDto.SettingsName}' already exists.");

        // Zero out weights for disabled evaluators, then validate
        if (!updateDto.RequireSelfEvaluation) updateDto.SelfEvaluationWeight = 0;
        if (!updateDto.RequirePeerReviews) updateDto.PeerEvaluationWeight = 0;
        if (!updateDto.RequireManagerEvaluation) updateDto.ManagerEvaluationWeight = 0;

        if (!updateDto.RequireSelfEvaluation && !updateDto.RequirePeerReviews && !updateDto.RequireManagerEvaluation)
            throw new InvalidOperationException("At least one evaluation type (self, peer, or manager) must be enabled.");

        var totalWeight = updateDto.SelfEvaluationWeight + updateDto.PeerEvaluationWeight + updateDto.ManagerEvaluationWeight;
        if (Math.Abs(totalWeight - 1.0m) > 0.005m)
            throw new InvalidOperationException($"Total evaluation weights must equal 1.0. Current total: {totalWeight:F2}");

        ValidateProfile(
            updateDto.MinPeerEvaluators, updateDto.MaxPeerEvaluators,
            updateDto.MinGoalsPerEmployee, updateDto.MaxGoalsPerEmployee,
            updateDto.DeadlineRiskHighDays, updateDto.DeadlineRiskMediumDays, updateDto.DeadlineRiskLowDays);

        updateDto.UpdateEntity(entity);

        await _settingsRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Appraisal settings updated successfully: {settingsId}", entity.Id);

        return entity.ToDto();
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(id);

        // B6: the default stays until another profile takes the flag.
        if (entity.IsDefault)
            throw new InvalidOperationException(
                "This is the default appraisal settings profile. Make another profile the default before deleting it.");

        // Check if settings are being used by any cycles in this tenant.
        var cyclesCount = await _settingsRepository.GetQueryable()
            .Where(s => s.TenantId == entity.TenantId && s.Id == id)
            .SelectMany(s => s.AppraisalCycles)
            .CountAsync(cancellationToken);

        if (cyclesCount > 0)
        {
            throw new InvalidOperationException($"Cannot delete appraisal settings that are being used by {cyclesCount} cycle(s).");
        }

        await _settingsRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Appraisal settings deleted: {settingsId}", id);

        return true;
    }

    /// <summary>
    /// The tenant's default profile — the one HR flagged (B6, P-2) — or null when none is. It was
    /// the most recently created profile, which a test run or a draft variant could win.
    /// </summary>
    public async Task<AppraisalSettingsDto?> GetDefaultSettingsAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();

        var entity = await _settingsRepository.GetQueryable()
            .Where(s => s.TenantId == tenantId && s.IsDefault)
            .FirstOrDefaultAsync(cancellationToken);

        return entity?.ToDto();
    }

    /// <summary>
    /// Makes this profile the tenant's default, and no other (B6). The previous default is cleared
    /// and saved first, inside one transaction, so the one-default index never sees two.
    /// </summary>
    public async Task<AppraisalSettingsDto> MakeDefaultAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(id);
        if (entity.IsDefault)
            return entity.ToDto();

        var tenantId = GetTenantId();
        await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var previous = await _settingsRepository.GetQueryable()
                .Where(s => s.TenantId == tenantId && s.IsDefault && s.Id != id)
                .ToListAsync(ct);
            foreach (var profile in previous)
            {
                profile.IsDefault = false;
                await _settingsRepository.UpdateAsync(profile);
            }
            if (previous.Count > 0)
                await _unitOfWork.SaveChangesAsync(ct);

            entity.IsDefault = true;
            await _settingsRepository.UpdateAsync(entity);
        }, cancellationToken);

        _logger.LogInformation("Appraisal settings {SettingsId} is now the tenant's default profile", id);
        return entity.ToDto();
    }

    /// <summary>
    /// What a profile must hold together (B6): the peer and goal minimums at or under their maximums,
    /// and the deadline-risk bands in order — high inside medium inside low — none negative. The
    /// weights' sum of 1 is checked beside it.
    /// </summary>
    private static void ValidateProfile(
        int minPeers, int maxPeers, int? minGoals, int? maxGoals, int highDays, int mediumDays, int lowDays)
    {
        if (minPeers < 0 || maxPeers < 0)
            throw new InvalidOperationException("The number of peer evaluators cannot be negative.");
        if (minPeers > maxPeers)
            throw new InvalidOperationException(
                $"The minimum number of peer evaluators ({minPeers}) is above the maximum ({maxPeers}).");

        if (minGoals < 0 || maxGoals < 0)
            throw new InvalidOperationException("The number of goals per employee cannot be negative.");
        if (minGoals is int lo && maxGoals is int hi && hi > 0 && lo > hi)
            throw new InvalidOperationException(
                $"The minimum number of goals per employee ({lo}) is above the maximum ({hi}).");

        if (highDays < 0 || mediumDays < 0 || lowDays < 0)
            throw new InvalidOperationException("The deadline-risk bands cannot be negative.");
        if (highDays > mediumDays || mediumDays > lowDays)
            throw new InvalidOperationException(
                $"The deadline-risk bands must run high ≤ medium ≤ low (days before the deadline); they are {highDays}, {mediumDays} and {lowDays}.");
    }

    public async Task<bool> ValidateWeightsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(id);

        var totalWeight = entity.SelfEvaluationWeight + entity.PeerEvaluationWeight + entity.ManagerEvaluationWeight;
        var isValid = Math.Abs(totalWeight - 1.0m) <= 0.01m;

        if (!isValid)
        {
            _logger.LogWarning("Appraisal settings {settingsId} has invalid weights. Total: {totalWeight}", id, totalWeight);
        }

        return isValid;
    }
}

#endregion Appraisal Settings
