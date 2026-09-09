using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

/// <summary>
/// Service implementation for employee position operations
/// </summary>
public class EmployeePositionService : IEmployeePositionService
{
    private readonly IEmployeePositionRepository _positionRepository;
    private readonly IOrganizationUnitRepository _unitRepository;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly ILogger<EmployeePositionService> _logger;

    public EmployeePositionService(
        IEmployeePositionRepository positionRepository,
        IOrganizationUnitRepository unitRepository,
        ICurrentUserProvider currentUserProvider,
        ILogger<EmployeePositionService> logger)
    {
        _positionRepository = positionRepository;
        _unitRepository = unitRepository;
        _currentUserProvider = currentUserProvider;
        _logger = logger;
    }

    // The ApplicationDbContext is registered without a tenant, so its global tenant
    // query-filter and TenantId auto-stamp are inert. Following the RHEMA convention,
    // this service scopes reads/writes to the current tenant explicitly.
    private Guid GetTenantId()
    {
        var tenantId = _currentUserProvider.TenantId;
        if (tenantId == Guid.Empty)
            throw new InvalidOperationException("No tenant is associated with the current user.");
        return tenantId;
    }

    public async Task<EmployeePositionDto?> GetByIdAsync(Guid id)
    {
        var position = await _positionRepository.GetWithSkillRequirementsAsync(id);
        return position == null ? null : MapToDto(position);
    }

    public async Task<IEnumerable<EmployeePositionDto>> GetAllAsync()
    {
        var tenantId = GetTenantId();
        var positions = await _positionRepository.GetAllAsync(
            p => p.OrganizationUnit!, p => p.OrganizationLevel!, p => p.StaffLevel);
        return positions.Where(p => p.TenantId == tenantId).OrderBy(p => p.Title).Select(MapToDto);
    }

    public async Task<IEnumerable<EmployeePositionDto>> GetActivePositionsAsync()
    {
        var tenantId = GetTenantId();
        var positions = await _positionRepository.GetActivePositionsAsync();
        return positions.Where(p => p.TenantId == tenantId).OrderBy(p => p.Title).Select(MapToDto);
    }

    /// <inheritdoc />
    /// <remarks>
    /// The ancestry is walked over <c>ParentUnitId</c> (<c>GetAncestorsAsync</c>), NOT read off the
    /// unit's <c>Path</c> as § 6.1.4 first proposed — lane B1 measured every seeded unit's path
    /// EMPTY, so a path read would have offered nothing above the unit for the whole live tree.
    /// </remarks>
    public async Task<IEnumerable<EmployeePositionDto>> GetByOrganizationUnitAsync(Guid organizationUnitId, bool includeAncestors = false)
    {
        var tenantId = GetTenantId();
        if (!includeAncestors)
        {
            var positions = await _positionRepository.GetByOrganizationUnitAsync(organizationUnitId);
            return positions.Where(p => p.TenantId == tenantId).Select(MapToDto);
        }

        var unitIds = new List<Guid> { organizationUnitId };
        unitIds.AddRange((await _unitRepository.GetAncestorsAsync(organizationUnitId)).Select(u => u.Id));
        var scoped = await _positionRepository.GetByOrganizationUnitsAsync(unitIds);
        return scoped.Where(p => p.TenantId == tenantId).Select(MapToDto);
    }

    /// <summary>
    /// The reports-to rules: the target exists in this tenant, is not the position itself, and
    /// reporting to it would not close a loop. Existence is an <see cref="ArgumentException"/>
    /// (the not-found idiom); the other two are <see cref="InvalidOperationException"/>.
    /// </summary>
    /// <remarks>
    /// <para>Demo feedback round 2, C1 (P-1). Until this slice the server checked NOTHING about
    /// <c>ReportsToPositionId</c> — a position could report to itself, to a position that did
    /// not exist, or to its own subordinate, and the organogram's position view would then loop.
    /// The only guard was the edit page dropping the position from its own dropdown.</para>
    /// <para>The unit is deliberately NOT enforced (Q-2, soft): matrix and dotted-line reporting
    /// exist, so the form narrows the offer to the unit's ancestry and lets the user widen it.</para>
    /// <para>The walk is bounded. A loop that already exists in the data (written before this
    /// rule) must not hang a save; after the bound it is reported as a cycle, which it is.</para>
    /// </remarks>
    private async Task ValidateReportsToAsync(Guid? reportsToPositionId, Guid? selfId, string selfTitle)
    {
        if (!reportsToPositionId.HasValue)
            return;

        var tenantId = GetTenantId();

        if (selfId.HasValue && reportsToPositionId.Value == selfId.Value)
            throw new InvalidOperationException("A position cannot report to itself.");

        var target = await _positionRepository.GetByIdAsync(reportsToPositionId.Value);
        if (target == null || target.TenantId != tenantId)
            throw new ArgumentException($"Reports-to position '{reportsToPositionId}' was not found.");

        if (!selfId.HasValue)
            return; // A position that does not exist yet cannot be on anyone's chain.

        var chain = new List<string> { selfTitle, target.Title };
        var visited = new HashSet<Guid> { target.Id };
        var current = target;
        const int bound = 100;
        for (var hops = 0; current.ReportsToPositionId.HasValue; hops++)
        {
            var nextId = current.ReportsToPositionId.Value;
            if (nextId == selfId.Value)
                throw new InvalidOperationException(
                    $"That would create a reporting cycle: {string.Join(" → ", chain)} → {selfTitle}.");

            if (!visited.Add(nextId) || hops >= bound)
                throw new InvalidOperationException(
                    $"The reporting line above '{target.Title}' already loops; it must be corrected before anything can report into it.");

            var next = await _positionRepository.GetByIdAsync(nextId);
            if (next == null)
                break; // A dangling link above the target is not this save's fault.
            chain.Add(next.Title);
            current = next;
        }
    }

    public async Task<IEnumerable<EmployeePositionDto>> GetByDepartmentAsync(Guid departmentId)
    {
        var positions = await _positionRepository.GetByDepartmentAsync(departmentId);
        return positions.Select(MapToDto);
    }

    public async Task<EmployeePositionDto?> GetByCodeAsync(string code)
    {
        var position = await _positionRepository.GetByCodeAsync(code);
        return position == null ? null : MapToDto(position);
    }

    public async Task<EmployeePositionDto> CreatePositionAsync(CreateEmployeePositionDto createDto)
    {
        if (!string.IsNullOrWhiteSpace(createDto.Code) && await _positionRepository.CodeExistsAsync(createDto.Code))
        {
            throw new InvalidOperationException($"Position code '{createDto.Code}' already exists.");
        }

        await ValidateReportsToAsync(createDto.ReportsToPositionId, selfId: null, createDto.Title);

        var position = new EmployeePosition
        {
            Title = createDto.Title,
            Code = createDto.Code ?? string.Empty,
            Description = createDto.Description,
            TenantId = GetTenantId(),
            OrganizationLevelId = createDto.OrganizationLevelId,
            OrganizationUnitId = createDto.OrganizationUnitId,
            Level = createDto.Level,
            MinimumExperienceYears = createDto.MinimumExperienceYears,
            MinimumAge = createDto.MinimumAge,
            MaximumAge = createDto.MaximumAge,
            ExpectedHeadcount = createDto.ExpectedHeadcount,
            SalaryGradeId = createDto.SalaryGradeId,
            WorkMode = createDto.WorkMode,
            RequiresCertification = createDto.RequiresCertification,
            RequiresGuarantor = createDto.RequiresGuarantor,
            RequiredGuarantorAmount = createDto.RequiredGuarantorAmount,
            RequiredGuarantorCurrencyCode = createDto.RequiredGuarantorCurrencyCode,
            RequiresLicense = createDto.RequiresLicense,
            StaffLevelId = createDto.StaffLevelId,
            ReportsToPositionId = createDto.ReportsToPositionId,
            ProbationPeriodMonths = createDto.ProbationPeriodMonths,
            NoticePeriodMonths = createDto.NoticePeriodMonths,
            IsActive = true
        };

        if (createDto.SkillRequirements is { Count: > 0 })
        {
            position.SkillRequirements = createDto.SkillRequirements
                .GroupBy(x => x.SkillId)
                .Select(g => g.First())
                .Select(x => new PositionSkillRequirement
                {
                    TenantId = GetTenantId(),
                    SkillId = x.SkillId,
                    RequiredLevel = x.RequiredLevel,
                    IsRequired = x.IsRequired,
                    Priority = x.Priority
                })
                .ToList();
        }

        if (createDto.PositionBenefits is { Count: > 0 })
        {
            position.PositionBenefits = createDto.PositionBenefits
                .GroupBy(x => x.PolicyId)
                .Select(g => g.First())
                .Select(x => new EmployeePositionBenefit
                {
                    TenantId = GetTenantId(),
                    PolicyId = x.PolicyId,
                    ExpiryDate = x.ExpiryDate,
                    PositionAmount = x.PositionAmount
                })
                .ToList();
        }

        var created = await _positionRepository.AddAsync(position);
        await _positionRepository.SaveChangesAsync();
        _logger.LogInformation("Employee position created: {PositionId} ({Code})", created.Id, created.Code);

        var createdWithUnit = await _positionRepository.GetWithSkillRequirementsAsync(created.Id);
        return createdWithUnit == null ? MapToDto(created) : MapToDto(createdWithUnit);
    }

    public async Task<EmployeePositionDto> UpdatePositionAsync(Guid id, UpdateEmployeePositionDto updateDto)
    {
        var position = await _positionRepository.GetWithSkillRequirementsAsync(id);
        if (position == null)
        {
            throw new InvalidOperationException($"Employee position with ID {id} not found.");
        }

        if (!string.IsNullOrWhiteSpace(updateDto.Code) && updateDto.Code != position.Code)
        {
            if (await _positionRepository.CodeExistsAsync(updateDto.Code))
            {
                throw new InvalidOperationException($"Position code '{updateDto.Code}' already exists.");
            }
            position.Code = updateDto.Code;
        }

        // Validated against the stored graph BEFORE the reassignment below, so a refused save leaves
        // the tracked entity as it was.
        await ValidateReportsToAsync(updateDto.ReportsToPositionId, selfId: position.Id, updateDto.Title);

        position.Title = updateDto.Title;
        position.Description = updateDto.Description;
        position.OrganizationLevelId = updateDto.OrganizationLevelId;
        position.OrganizationUnitId = updateDto.OrganizationUnitId;
        position.Level = updateDto.Level;
        position.SalaryGradeId = updateDto.SalaryGradeId;
        position.WorkMode = updateDto.WorkMode;
        position.RequiresCertification = updateDto.RequiresCertification;
        position.RequiresGuarantor = updateDto.RequiresGuarantor;
        position.RequiredGuarantorAmount = updateDto.RequiredGuarantorAmount;
        position.RequiredGuarantorCurrencyCode = updateDto.RequiredGuarantorCurrencyCode;
        position.RequiresLicense = updateDto.RequiresLicense;
        position.ExpectedHeadcount = updateDto.ExpectedHeadcount;
        position.MinimumExperienceYears = updateDto.MinimumExperienceYears;
        position.MinimumAge = updateDto.MinimumAge;
        position.MaximumAge = updateDto.MaximumAge;
        position.StaffLevelId = updateDto.StaffLevelId;
        position.ReportsToPositionId = updateDto.ReportsToPositionId;
        position.ProbationPeriodMonths = updateDto.ProbationPeriodMonths;
        position.NoticePeriodMonths = updateDto.NoticePeriodMonths;
        position.IsActive = updateDto.IsActive;

        await SyncSkillRequirementsAsync(position, updateDto.SkillRequirements);
        await SyncPositionBenefitsAsync(position, updateDto.PositionBenefits);

        // Don't call UpdateAsync (which calls _dbSet.Update) — the entity graph is already
        // tracked by EF. Calling Update() forces all navigation entities (including newly
        // Added benefits that have Guid IDs from BaseEntity's constructor) into Modified state,
        // causing SaveChanges to issue UPDATEs for rows that don't exist yet.
        position.UpdatedAt = DateTime.UtcNow;
        await _positionRepository.SaveChangesAsync();
        _logger.LogInformation("Employee position updated: {PositionId} ({Code})", position.Id, position.Code);

        var updatedWithUnit = await _positionRepository.GetWithSkillRequirementsAsync(position.Id);
        return updatedWithUnit == null ? MapToDto(position) : MapToDto(updatedWithUnit);
    }

    public async Task<bool> DeletePositionAsync(Guid id)
    {
        await _positionRepository.DeleteAsync(id);
        await _positionRepository.SaveChangesAsync();
        _logger.LogInformation("Employee position deleted: {PositionId}", id);
        return true;
    }

    public Task<bool> CodeExistsAsync(string code)
        => _positionRepository.CodeExistsAsync(code);

    private static EmployeePositionDto MapToDto(EmployeePosition position)
    {
        return new EmployeePositionDto
        {
            Id = position.Id,
            Title = position.Title,
            Code = position.Code,
            Description = position.Description,
            OrganizationLevelId = position.OrganizationLevelId,
            OrganizationLevelName = position.OrganizationLevel?.Name ?? string.Empty,
            OrganizationUnitId = position.OrganizationUnitId,
            OrganizationUnitName = position.OrganizationUnit?.Name ?? string.Empty,
            StaffLevelId = position.StaffLevelId,
            StaffLevelName = position.StaffLevel?.Name,
            ReportsToPositionId = position.ReportsToPositionId,
            ReportsToPositionTitle = position.ReportsToPosition?.Title,
            Level = position.Level,
            MinimumExperienceYears = position.MinimumExperienceYears,
            MinimumAge = position.MinimumAge,
            MaximumAge = position.MaximumAge,
            ExpectedHeadcount = position.ExpectedHeadcount,
            SalaryGradeId = position.SalaryGradeId,
            SalaryGradeName = position.SalaryGrade?.Name,
            WorkMode = position.WorkMode,
            ProbationPeriodMonths = position.ProbationPeriodMonths,
            NoticePeriodMonths = position.NoticePeriodMonths,
            RequiresCertification = position.RequiresCertification,
            RequiresGuarantor = position.RequiresGuarantor,
            RequiredGuarantorAmount = position.RequiredGuarantorAmount,
            RequiredGuarantorCurrencyCode = position.RequiredGuarantorCurrencyCode,
            RequiresLicense = position.RequiresLicense,
            IsActive = position.IsActive,
            EmployeeCount = 0,
            SkillRequirements = position.SkillRequirements
                .Where(x => !x.IsDeleted)
                .OrderByDescending(x => x.Priority)
                .ThenBy(x => x.Skill.Name)
                .Select(x => new PositionSkillRequirementDto
                {
                    Id = x.Id,
                    SkillId = x.SkillId,
                    SkillName = x.Skill?.Name ?? string.Empty,
                    RequiredLevel = x.RequiredLevel,
                    IsRequired = x.IsRequired,
                    Priority = x.Priority
                })
                .ToList(),
            PositionBenefits = position.PositionBenefits
                .Where(x => !x.IsDeleted)
                .OrderBy(x => x.BenefitPolicy.PolicyName)
                .Select(x => new EmployeePositionBenefitDto
                {
                    Id = x.Id,
                    PositionId = x.PositionId,
                    PolicyId = x.PolicyId,
                    PolicyName = x.BenefitPolicy?.PolicyName ?? string.Empty,
                    ExpiryDate = x.ExpiryDate,
                    PositionAmount = x.PositionAmount,
                    IsActive = true
                })
                .ToList()
        };
    }

    private async Task SyncSkillRequirementsAsync(EmployeePosition position, ICollection<CreatePositionSkillRequirementDto> desired)
    {
        desired ??= new List<CreatePositionSkillRequirementDto>();

        var desiredDistinct = desired
            .Where(x => x.SkillId != Guid.Empty)
            .GroupBy(x => x.SkillId)
            .Select(g => g.First())
            .ToList();

        // Read through a filter-ignoring query for the same reason as the benefits sync: an
        // Include cannot see soft-deleted rows, so removing a skill and adding it back would hit
        // the unique index on TenantId+PositionId+SkillId instead of reviving the row.
        var allBySkillId = (await _positionRepository.GetSkillRequirementsIncludingDeletedAsync(position.Id))
            .GroupBy(x => x.SkillId)
            .ToDictionary(g => g.Key, g => g.First());

        foreach (var req in desiredDistinct)
        {
            if (allBySkillId.TryGetValue(req.SkillId, out var entity))
            {
                // Re-activate if previously soft-deleted, then update values.
                entity.IsDeleted = false;
                entity.DeletedAt = null;
                entity.RequiredLevel = req.RequiredLevel;
                entity.IsRequired = req.IsRequired;
                entity.Priority = req.Priority;
            }
            else
            {
                // Truly new — set FK explicitly and track via the repository to guarantee
                // EntityState.Added. Adding to the nav-collection alone is not safe here
                // because EF infers Unchanged (not Added) for non-default Guid keys when the
                // parent is already Modified, which later causes a zero-row UPDATE.
                var newReq = new PositionSkillRequirement
                {
                    PositionId = position.Id,
                    TenantId = GetTenantId(),
                    SkillId = req.SkillId,
                    RequiredLevel = req.RequiredLevel,
                    IsRequired = req.IsRequired,
                    Priority = req.Priority
                };
                _positionRepository.TrackSkillRequirement(newReq);
            }
        }

        var desiredSkillIds = desiredDistinct.Select(x => x.SkillId).ToHashSet();
        foreach (var entity in allBySkillId.Values.Where(x => !x.IsDeleted))
        {
            if (!desiredSkillIds.Contains(entity.SkillId))
            {
                entity.IsDeleted = true;
                entity.DeletedAt = DateTime.UtcNow;
            }
        }
    }

    private async Task SyncPositionBenefitsAsync(EmployeePosition position, ICollection<CreateEmployeePositionBenefitDto> desired)
    {
        desired ??= new List<CreateEmployeePositionBenefitDto>();

        var desiredDistinct = desired
            .Where(x => x.PolicyId != Guid.Empty)
            .GroupBy(x => x.PolicyId)
            .Select(g => g.First())
            .ToList();

        // Soft-deleted entries are re-activated rather than re-inserted, which would violate the
        // unique index on TenantId+PositionId+PolicyId. They have to be read through a
        // filter-ignoring query: position.PositionBenefits comes from an Include, and the global
        // soft-delete filter applies to included navigations, so a removed entitlement is simply
        // absent there — making re-adding one a 500 rather than a revival.
        var allByPolicyId = (await _positionRepository.GetBenefitsIncludingDeletedAsync(position.Id))
            .GroupBy(x => x.PolicyId)
            .ToDictionary(g => g.Key, g => g.First());

        foreach (var ben in desiredDistinct)
        {
            if (allByPolicyId.TryGetValue(ben.PolicyId, out var entity))
            {
                // Re-activate if previously soft-deleted, then update values.
                entity.IsDeleted = false;
                entity.DeletedAt = null;
                entity.ExpiryDate = ben.ExpiryDate;
                entity.PositionAmount = ben.PositionAmount;
            }
            else
            {
                // Truly new — set FK explicitly and track via the repository to guarantee
                // EntityState.Added. Adding to the nav-collection alone is not safe here
                // because EF infers Unchanged (not Added) for non-default Guid keys when the
                // parent is already Modified, which later causes a zero-row UPDATE.
                var newBenefit = new EmployeePositionBenefit
                {
                    PositionId = position.Id,
                    TenantId = GetTenantId(),
                    PolicyId = ben.PolicyId,
                    ExpiryDate = ben.ExpiryDate,
                    PositionAmount = ben.PositionAmount
                };
                _positionRepository.TrackBenefit(newBenefit);
            }
        }

        var desiredPolicyIds = desiredDistinct.Select(x => x.PolicyId).ToHashSet();
        foreach (var entity in allByPolicyId.Values.Where(x => !x.IsDeleted))
        {
            if (!desiredPolicyIds.Contains(entity.PolicyId))
            {
                entity.IsDeleted = true;
                entity.DeletedAt = DateTime.UtcNow;
            }
        }
    }
}
