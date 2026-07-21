using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

/// <summary>
/// Service implementation for employee position operations
/// </summary>
public class EmployeePositionService : IEmployeePositionService
{
    private readonly IEmployeePositionRepository _positionRepository;
    private readonly ILogger<EmployeePositionService> _logger;

    public EmployeePositionService(
        IEmployeePositionRepository positionRepository,
        ILogger<EmployeePositionService> logger)
    {
        _positionRepository = positionRepository;
        _logger = logger;
    }

    public async Task<EmployeePositionDto?> GetByIdAsync(Guid id)
    {
        var position = await _positionRepository.GetWithSkillRequirementsAsync(id);
        return position == null ? null : MapToDto(position);
    }

    public async Task<IEnumerable<EmployeePositionDto>> GetAllAsync()
    {
        var positions = await _positionRepository.GetAllAsync(p => p.OrganizationUnit!, p => p.StaffLevel);
        return positions.Select(MapToDto);
    }

    public async Task<IEnumerable<EmployeePositionDto>> GetActivePositionsAsync()
    {
        var positions = await _positionRepository.GetActivePositionsAsync();
        return positions.Select(MapToDto);
    }

    public async Task<IEnumerable<EmployeePositionDto>> GetByOrganizationUnitAsync(Guid organizationUnitId)
    {
        var positions = await _positionRepository.GetByOrganizationUnitAsync(organizationUnitId);
        return positions.Select(MapToDto);
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

        var position = new EmployeePosition
        {
            Title = createDto.Title,
            Code = createDto.Code ?? string.Empty,
            Description = createDto.Description,
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

        position.Title = updateDto.Title;
        position.Description = updateDto.Description;
        position.OrganizationLevelId = updateDto.OrganizationLevelId;
        position.OrganizationUnitId = updateDto.OrganizationUnitId;
        position.Level = updateDto.Level;
        position.SalaryGradeId = updateDto.SalaryGradeId;
        position.WorkMode = updateDto.WorkMode;
        position.RequiresCertification = updateDto.RequiresCertification;
        position.RequiresGuarantor = updateDto.RequiresGuarantor;
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

        SyncSkillRequirements(position, updateDto.SkillRequirements);
        SyncPositionBenefits(position, updateDto.PositionBenefits);

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

    private void SyncSkillRequirements(EmployeePosition position, ICollection<CreatePositionSkillRequirementDto> desired)
    {
        desired ??= new List<CreatePositionSkillRequirementDto>();

        var desiredDistinct = desired
            .Where(x => x.SkillId != Guid.Empty)
            .GroupBy(x => x.SkillId)
            .Select(g => g.First())
            .ToList();

        // Include soft-deleted entries so we can re-activate them instead of inserting
        // duplicates (which would violate the unique index on TenantId+PositionId+SkillId).
        var allBySkillId = position.SkillRequirements
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
                    SkillId = req.SkillId,
                    RequiredLevel = req.RequiredLevel,
                    IsRequired = req.IsRequired,
                    Priority = req.Priority
                };
                _positionRepository.TrackSkillRequirement(newReq);
            }
        }

        var desiredSkillIds = desiredDistinct.Select(x => x.SkillId).ToHashSet();
        foreach (var entity in position.SkillRequirements.Where(x => !x.IsDeleted))
        {
            if (!desiredSkillIds.Contains(entity.SkillId))
            {
                entity.IsDeleted = true;
                entity.DeletedAt = DateTime.UtcNow;
            }
        }
    }

    private void SyncPositionBenefits(EmployeePosition position, ICollection<CreateEmployeePositionBenefitDto> desired)
    {
        desired ??= new List<CreateEmployeePositionBenefitDto>();

        var desiredDistinct = desired
            .Where(x => x.PolicyId != Guid.Empty)
            .GroupBy(x => x.PolicyId)
            .Select(g => g.First())
            .ToList();

        // Include soft-deleted entries so we can re-activate them instead of inserting
        // duplicates (which would violate the unique index on TenantId+PositionId+PolicyId).
        var allByPolicyId = position.PositionBenefits
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
                    PolicyId = ben.PolicyId,
                    ExpiryDate = ben.ExpiryDate,
                    PositionAmount = ben.PositionAmount
                };
                _positionRepository.TrackBenefit(newBenefit);
            }
        }

        var desiredPolicyIds = desiredDistinct.Select(x => x.PolicyId).ToHashSet();
        foreach (var entity in position.PositionBenefits.Where(x => !x.IsDeleted))
        {
            if (!desiredPolicyIds.Contains(entity.PolicyId))
            {
                entity.IsDeleted = true;
                entity.DeletedAt = DateTime.UtcNow;
            }
        }
    }
}
