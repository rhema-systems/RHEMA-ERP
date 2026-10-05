using System.Reflection;
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

#region Appraisal Settings

public class AppraisalSettingsService : IAppraisalSettingsService
{
    private readonly IGenericRepository<AppraisalSettings> _settingsRepository;
    private readonly IGenericRepository<PerformanceAppraisal> _appraisalRepository;
    private readonly IGenericRepository<Employee> _employeeRepository;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<AppraisalSettingsService> _logger;

    public AppraisalSettingsService(
        IGenericRepository<AppraisalSettings> settingsRepository,
        IGenericRepository<PerformanceAppraisal> appraisalRepository,
        IGenericRepository<Employee> employeeRepository,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ILogger<AppraisalSettingsService> logger)
    {
        _settingsRepository = settingsRepository;
        _appraisalRepository = appraisalRepository;
        _employeeRepository = employeeRepository;
        _currentUserProvider = currentUserProvider;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    // ─── In use (performance closure E-e, D-67) ───────────────────────────
    // An appraisal reads its profile live through its cycle — every gate, the release and visibility rules, peer
    // anonymity, the appeal window, the outcome handlers (probation extension, succession) — and finished appraisals
    // still read the visibility, anonymity and outcome settings. So once any appraisal sits on a cycle using a profile,
    // its rules are frozen and change on a clone. A cycle's profile is pinned once it opens (E-c), so a clone serves
    // the cycles that come after.

    /// <summary>The fields that change nothing an appraisal holds: they stay editable while the profile is in use.</summary>
    private static readonly HashSet<string> EditableWhileInUse = new(StringComparer.Ordinal)
    {
        nameof(AppraisalSettings.SettingsName),
        nameof(AppraisalSettings.DeadlineRiskHighDays),
        nameof(AppraisalSettings.DeadlineRiskMediumDays),
        nameof(AppraisalSettings.DeadlineRiskLowDays),
        nameof(AppraisalSettings.ManagerWorkloadThreshold),
        nameof(AppraisalSettings.DefaultHRReviewerId),
    };

    /// <summary>Not rules at all: the row's identity and audit columns, and the default flag (make-default moves it).</summary>
    private static readonly HashSet<string> NotRules = new(StringComparer.Ordinal)
    {
        nameof(AppraisalSettings.Id), nameof(AppraisalSettings.TenantId), nameof(AppraisalSettings.IsDefault),
        nameof(AppraisalSettings.CreatedAt), nameof(AppraisalSettings.UpdatedAt),
        nameof(AppraisalSettings.CreatedBy), nameof(AppraisalSettings.UpdatedBy),
        nameof(AppraisalSettings.CreatedById), nameof(AppraisalSettings.LastModifiedById),
        nameof(AppraisalSettings.IsDeleted), nameof(AppraisalSettings.DeletedAt), nameof(AppraisalSettings.DeletedBy),
    };

    private static readonly PropertyInfo[] ScalarProperties = typeof(AppraisalSettings)
        .GetProperties(BindingFlags.Public | BindingFlags.Instance)
        .Where(p => p.CanRead && p.CanWrite && IsScalar(p.PropertyType))
        .ToArray();

    private static bool IsScalar(Type type)
    {
        var t = Nullable.GetUnderlyingType(type) ?? type;
        return t.IsPrimitive || t.IsEnum || t == typeof(decimal) || t == typeof(string) || t == typeof(Guid)
            || t == typeof(DateTime) || t == typeof(DateOnly) || t == typeof(TimeSpan);
    }

    /// <summary>
    /// The profile's rules as an appraisal reads them: every scalar field but the editable and non-rule ones — so a
    /// field added later is frozen by default. A disabled evaluator's weight reads 0 on both sides, as the save
    /// zeroes it.
    /// </summary>
    private static Dictionary<string, object?> RulesOf(AppraisalSettings settings)
    {
        var rules = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var property in ScalarProperties)
        {
            if (NotRules.Contains(property.Name) || EditableWhileInUse.Contains(property.Name))
                continue;
            rules[property.Name] = property.GetValue(settings);
        }
        if (!settings.RequireSelfEvaluation) rules[nameof(AppraisalSettings.SelfEvaluationWeight)] = 0m;
        if (!settings.RequirePeerReviews) rules[nameof(AppraisalSettings.PeerEvaluationWeight)] = 0m;
        if (!settings.RequireManagerEvaluation) rules[nameof(AppraisalSettings.ManagerEvaluationWeight)] = 0m;
        return rules;
    }

    /// <summary>Per profile: how many appraisals sit on cycles using it, and those cycles' names.</summary>
    private async Task<Dictionary<Guid, (int Count, List<string> Cycles)>> GetUsageAsync(
        IReadOnlyCollection<Guid> profileIds, CancellationToken cancellationToken)
    {
        if (profileIds.Count == 0)
            return new Dictionary<Guid, (int, List<string>)>();

        var tenantId = GetTenantId();
        var rows = await _appraisalRepository.GetQueryable()
            .Where(a => a.TenantId == tenantId && !a.AppraisalCycle.IsDeleted
                     && profileIds.Contains(a.AppraisalCycle.AppraisalSettingsId))
            .GroupBy(a => new { a.AppraisalCycle.AppraisalSettingsId, a.AppraisalCycle.CycleName })
            .Select(g => new { g.Key.AppraisalSettingsId, g.Key.CycleName, Count = g.Count() })
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(r => r.AppraisalSettingsId)
            .ToDictionary(g => g.Key, g => (g.Sum(r => r.Count), g.Select(r => r.CycleName).OrderBy(n => n).ToList()));
    }

    private async Task<List<AppraisalSettingsDto>> WithUsageAsync(List<AppraisalSettingsDto> profiles, CancellationToken cancellationToken)
    {
        var usage = await GetUsageAsync(profiles.Select(p => p.Id).ToList(), cancellationToken);
        foreach (var profile in profiles)
        {
            if (!usage.TryGetValue(profile.Id, out var use))
                continue;
            profile.IsInUse = true;
            profile.InUseAppraisalCount = use.Count;
            profile.InUseCycleNames = use.Cycles;
        }
        return profiles;
    }

    private static string CycleList(List<string> cycles) =>
        cycles.Count <= 3
            ? string.Join(", ", cycles.Select(c => $"'{c}'"))
            : $"{string.Join(", ", cycles.Take(3).Select(c => $"'{c}'"))} and {cycles.Count - 3} more";

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
        return (await WithUsageAsync(new List<AppraisalSettingsDto> { entity.ToDto() }, cancellationToken))[0];
    }

    public async Task<IEnumerable<AppraisalSettingsDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _settingsRepository.GetQueryable()
            .Where(s => s.TenantId == tenantId)
            .ToListAsync(cancellationToken);
        return await WithUsageAsync(entities.ToDtoList(), cancellationToken);
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
            Items = await WithUsageAsync(items.ToDtoList(), cancellationToken),
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

        ValidateFields(
            createDto.SelfEvaluationWeight, createDto.PeerEvaluationWeight, createDto.ManagerEvaluationWeight,
            createDto.SuccessionPoolName, createDto.PeerNominationMode, createDto.PeerEvaluationOpenMode,
            createDto.HRReviewTiming, createDto.ReviewFrequency, createDto.InterimReviewDepth, createDto.SuccessionDefaultReadiness);

        var totalWeight = createDto.SelfEvaluationWeight + createDto.PeerEvaluationWeight + createDto.ManagerEvaluationWeight;
        if (Math.Abs(totalWeight - 1.0m) > 0.005m)
            throw new InvalidOperationException($"Total evaluation weights must equal 1.0. Current total: {totalWeight:F2}");

        ValidateProfile(
            createDto.MinPeerEvaluators, createDto.MaxPeerEvaluators,
            createDto.MinGoalsPerEmployee, createDto.MaxGoalsPerEmployee,
            createDto.DeadlineRiskHighDays, createDto.DeadlineRiskMediumDays, createDto.DeadlineRiskLowDays);
        await EnsureDefaultHRReviewerAsync(createDto.DefaultHRReviewerId, cancellationToken);

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

        ValidateFields(
            updateDto.SelfEvaluationWeight, updateDto.PeerEvaluationWeight, updateDto.ManagerEvaluationWeight,
            updateDto.SuccessionPoolName, updateDto.PeerNominationMode, updateDto.PeerEvaluationOpenMode,
            updateDto.HRReviewTiming, updateDto.ReviewFrequency, updateDto.InterimReviewDepth, updateDto.SuccessionDefaultReadiness);

        var totalWeight = updateDto.SelfEvaluationWeight + updateDto.PeerEvaluationWeight + updateDto.ManagerEvaluationWeight;
        if (Math.Abs(totalWeight - 1.0m) > 0.005m)
            throw new InvalidOperationException($"Total evaluation weights must equal 1.0. Current total: {totalWeight:F2}");

        ValidateProfile(
            updateDto.MinPeerEvaluators, updateDto.MaxPeerEvaluators,
            updateDto.MinGoalsPerEmployee, updateDto.MaxGoalsPerEmployee,
            updateDto.DeadlineRiskHighDays, updateDto.DeadlineRiskMediumDays, updateDto.DeadlineRiskLowDays);
        if (updateDto.DefaultHRReviewerId != entity.DefaultHRReviewerId)
            await EnsureDefaultHRReviewerAsync(updateDto.DefaultHRReviewerId, cancellationToken);

        // In use, its rules stay as they are (D-67): compared as the appraisals read them, before and after.
        var before = RulesOf(entity);
        updateDto.UpdateEntity(entity);
        var usage = await GetUsageAsync(new[] { entity.Id }, cancellationToken);
        if (usage.TryGetValue(entity.Id, out var use))
        {
            var after = RulesOf(entity);
            var changed = before.Keys.Where(k => !Equals(before[k], after.GetValueOrDefault(k))).ToList();
            if (changed.Count > 0)
                throw new AppraisalConfigurationLockedException(
                    $"This profile is in use: {use.Count} appraisal{(use.Count == 1 ? "" : "s")} on {CycleList(use.Cycles)} " +
                    $"read its rules, so they cannot change underneath {(use.Count == 1 ? "it" : "them")} " +
                    $"(changed: {string.Join(", ", changed)}). Clone the profile and change the copy; its name, " +
                    "deadline-risk bands, workload threshold and default HR reviewer can still be changed here.");
        }

        await _settingsRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Appraisal settings updated successfully: {settingsId}", entity.Id);

        return (await WithUsageAsync(new List<AppraisalSettingsDto> { entity.ToDto() }, cancellationToken))[0];
    }

    /// <summary>
    /// Copies the profile under a new name (D-46, D-67): every rule and setting, not the default flag. The way to
    /// change the rules of a profile in use — the copy serves the cycles created after, or once made the default.
    /// </summary>
    public async Task<AppraisalSettingsDto> CloneAsync(Guid id, CloneAppraisalSettingsDto dto, CancellationToken cancellationToken = default)
    {
        var source = await GetOwnedAsync(id);
        var tenantId = GetTenantId();

        var name = (dto.SettingsName ?? string.Empty).Trim();
        if (name.Length == 0)
            throw new InvalidOperationException("A name is required for the copy.");
        if (await _settingsRepository.GetQueryable().AnyAsync(s => s.TenantId == tenantId && s.SettingsName == name, cancellationToken))
            throw new InvalidOperationException($"An appraisal settings named '{name}' already exists.");

        var copy = new AppraisalSettings();
        foreach (var property in ScalarProperties.Where(p => !NotRules.Contains(p.Name)))
            property.SetValue(copy, property.GetValue(source));
        copy.SettingsName = name;
        copy.TenantId = tenantId;

        await _settingsRepository.AddAsync(copy);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Appraisal settings {SourceId} copied to {CopyId} as '{Name}'", id, copy.Id, name);
        return copy.ToDto();
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

        return entity == null
            ? null
            : (await WithUsageAsync(new List<AppraisalSettingsDto> { entity.ToDto() }, cancellationToken))[0];
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
        return (await WithUsageAsync(new List<AppraisalSettingsDto> { entity.ToDto() }, cancellationToken))[0];
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

    /// <summary>
    /// The fields the attributes never held (performance closure E-e). Each weight is a share between 0 and 1 —
    /// <c>[Range(0, 1)]</c> has int bounds, so it rounded −0.4 to 0 and 1.4 to 1 and let both through, and a negative
    /// weight then dropped out of the score; the pool name fits its column (a longer one was a 500); and each enum
    /// holds one of its values.
    /// </summary>
    private static void ValidateFields(
        decimal selfWeight, decimal peerWeight, decimal managerWeight, string? successionPoolName,
        PeerNominationMode nominationMode, PeerEvaluationOpenMode peerOpenMode, HRReviewTiming hrReviewTiming,
        ReviewFrequency reviewFrequency, InterimReviewDepth interimReviewDepth, ReadinessLevel successionReadiness)
    {
        foreach (var (name, weight) in new[] { ("self-evaluation", selfWeight), ("peer", peerWeight), ("manager", managerWeight) })
            if (weight < 0m || weight > 1m)
                throw new InvalidOperationException($"The {name} weight must be between 0 and 1; it is {weight}.");

        if (successionPoolName is { Length: > 100 })
            throw new InvalidOperationException("The succession pool's name is at most 100 characters.");

        if (!Enum.IsDefined(nominationMode))
            throw new InvalidOperationException($"'{(int)nominationMode}' is not a peer nomination mode.");
        if (!Enum.IsDefined(peerOpenMode))
            throw new InvalidOperationException($"'{(int)peerOpenMode}' is not a peer evaluation opening mode.");
        if (!Enum.IsDefined(hrReviewTiming))
            throw new InvalidOperationException($"'{(int)hrReviewTiming}' is not an HR review timing.");
        if (!Enum.IsDefined(reviewFrequency))
            throw new InvalidOperationException($"'{(int)reviewFrequency}' is not a review frequency.");
        if (!Enum.IsDefined(interimReviewDepth))
            throw new InvalidOperationException($"'{(int)interimReviewDepth}' is not an interim review depth.");
        if (!Enum.IsDefined(successionReadiness))
            throw new InvalidOperationException($"'{(int)successionReadiness}' is not a readiness level.");
    }

    /// <summary>
    /// The default HR reviewer is an employee of this organisation (performance closure E-e): it was saved unchecked,
    /// and an unknown id fell back silently when HR's review was assigned.
    /// </summary>
    private async Task EnsureDefaultHRReviewerAsync(Guid? employeeId, CancellationToken cancellationToken)
    {
        if (employeeId is not Guid id || id == Guid.Empty)
            return;

        var tenantId = GetTenantId();
        if (!await _employeeRepository.GetQueryable().AnyAsync(e => e.Id == id && e.TenantId == tenantId, cancellationToken))
            throw new InvalidOperationException("The default HR reviewer must be an employee of this organisation.");
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
