using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.HR.Safety;
using ErpSystem.Core.Enums.Safety;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Application.HR.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

// ============================================================================
// SHE services — Emergency (M), Regulatory (N), Signage (O), KPI (P),
// Committee & Meetings (Q) and Return-to-Work (R).
// ============================================================================

#region Emergency Service

public class SheEmergencyService : ISheEmergencyService
{
    private readonly IEmergencyPlanRepository _planRepository;
    private readonly IEmergencyDrillRepository _drillRepository;
    private readonly IEmergencyResponseTeamRepository _teamRepository;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<SheEmergencyService> _logger;

    public SheEmergencyService(
        IEmergencyPlanRepository planRepository,
        IEmergencyDrillRepository drillRepository,
        IEmergencyResponseTeamRepository teamRepository,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ILogger<SheEmergencyService> logger)
    {
        _planRepository = planRepository;
        _drillRepository = drillRepository;
        _teamRepository = teamRepository;
        _currentUserProvider = currentUserProvider;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    // The ApplicationDbContext is registered without a tenant, so its global tenant query-filter and
    // TenantId auto-stamp are inert. Following the RHEMA convention, this service scopes every read and
    // mutation to the authenticated tenant explicitly and passes it into the repository predicate.
    private Guid GetTenantId()
    {
        var tenantId = _currentUserProvider.TenantId;
        if (tenantId == Guid.Empty)
            throw new InvalidOperationException("No tenant is associated with the current user.");
        return tenantId;
    }

    private Guid RequireCurrentTenant(Guid tenantId)
    {
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");
        return current;
    }

    // Owned-loads carry the navigations the mappers read, so single reads and write responses
    // resolve names instead of mapping blanks.
    private async Task<EmergencyPlan> GetOwnedPlanAsync(Guid id)
    {
        var entity = await _planRepository.GetByIdAsync(id, p => p.PlanOwner, p => p.Location!);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Emergency plan with ID '{id}' not found.");
        return entity;
    }

    private async Task<SheAssemblyPoint> GetOwnedAssemblyPointAsync(Guid id)
    {
        var entity = await _unitOfWork.Repository<SheAssemblyPoint>().GetByIdAsync(id, a => a.Location!);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Assembly point with ID '{id}' not found.");
        return entity;
    }

    private async Task<EmergencyContact> GetOwnedContactAsync(Guid id)
    {
        var entity = await _unitOfWork.Repository<EmergencyContact>().GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Emergency contact with ID '{id}' not found.");
        return entity;
    }

    private async Task<EmergencyDrill> GetOwnedDrillAsync(Guid id)
    {
        var entity = await _drillRepository.GetByIdAsync(id, d => d.Coordinator, d => d.Location!, d => d.Department!);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Emergency drill with ID '{id}' not found.");
        return entity;
    }

    private async Task<EmergencyResponseTeam> GetOwnedTeamMemberAsync(Guid id)
    {
        var entity = await _teamRepository.GetByIdAsync(id, t => t.Employee, t => t.EmergencyPlan);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Emergency response team member with ID '{id}' not found.");
        return entity;
    }

    /// <summary>Guards the body-supplied FKs — without these a bad id surfaces as an SQL 547 /
    /// HTTP 500 instead of a 404, and a child could be attached to another tenant's plan. The
    /// guarded loads double as change-tracker fixup, so write responses resolve names.</summary>
    private async Task<Employee> GetOwnedEmployeeAsync(Guid id)
    {
        var employee = await _unitOfWork.Repository<Employee>().GetByIdAsync(id);
        if (employee == null || employee.TenantId != GetTenantId())
            throw new ArgumentException($"Employee with ID '{id}' not found.");
        return employee;
    }

    private async Task GuardOptionalReferencesAsync(Guid? locationId, Guid? departmentId = null)
    {
        if (locationId.HasValue)
        {
            var location = await _unitOfWork.Repository<Location>().GetByIdAsync(locationId.Value);
            if (location == null || location.TenantId != GetTenantId())
                throw new ArgumentException($"Location with ID '{locationId.Value}' not found.");
        }
        if (departmentId.HasValue)
        {
            var department = await _unitOfWork.Repository<Department>().GetByIdAsync(departmentId.Value);
            if (department == null || department.TenantId != GetTenantId())
                throw new ArgumentException($"Department with ID '{departmentId.Value}' not found.");
        }
    }

    public async Task<EmergencyPlanDto> GetPlanAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _planRepository.GetWithFullDetailsAsync(id);
        if (entity == null || entity.TenantId != tenantId)
            throw new ArgumentException($"Emergency plan with ID '{id}' not found.");
        return entity.ToDto();
    }

    public async Task<EmergencyPlanDto?> GetPlanByNumberAsync(string planNumber, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _planRepository.GetByNumberAsync(planNumber);
        if (entity == null || entity.TenantId != tenantId)
            return null;
        return entity.ToDto();
    }

    public async Task<IEnumerable<EmergencyPlanSummaryDto>> GetAllPlansAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _planRepository.GetAllSummaryAsync())
            .Where(e => e.TenantId == tenantId)
            .ToSummaryDtoList();
    }

    public async Task<IEnumerable<EmergencyPlanSummaryDto>> GetPlansByTypeAsync(SheEmergencyType type, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _planRepository.GetByTypeAsync(type))
            .Where(e => e.TenantId == tenantId)
            .ToSummaryDtoList();
    }

    public async Task<IEnumerable<EmergencyPlanSummaryDto>> GetActivePlansAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _planRepository.GetActiveAsync())
            .Where(e => e.TenantId == tenantId)
            .ToSummaryDtoList();
    }

    public async Task<IEnumerable<EmergencyPlanSummaryDto>> GetPlansDueForReviewAsync(int daysAhead = 30, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _planRepository.GetDueForReviewAsync(daysAhead))
            .Where(e => e.TenantId == tenantId)
            .ToSummaryDtoList();
    }

    public async Task<EmergencyPlanDto> CreatePlanAsync(CreateEmergencyPlanDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        var planNumber = dto.PlanNumber.Trim();
        var exists = await _planRepository.GetQueryable()
            .AnyAsync(p => p.TenantId == tenantId && p.PlanNumber == planNumber, cancellationToken);
        if (exists)
            throw new InvalidOperationException($"An emergency plan with number '{planNumber}' already exists for this tenant.");
        await GetOwnedEmployeeAsync(dto.PlanOwnerId);
        await GuardOptionalReferencesAsync(dto.LocationId);

        var entity = dto.ToEntity(tenantId, userId);
        await _planRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<EmergencyPlanDto> UpdatePlanAsync(UpdateEmergencyPlanDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedPlanAsync(dto.Id);
        await GetOwnedEmployeeAsync(dto.PlanOwnerId);
        await GuardOptionalReferencesAsync(dto.LocationId);
        entity.UpdateEntity(dto, userId);
        await _planRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> DeletePlanAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedPlanAsync(id);
        await _planRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ── Assembly points ──
    public async Task<SheAssemblyPointDto> AddAssemblyPointAsync(CreateSheAssemblyPointDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        await GetOwnedPlanAsync(dto.EmergencyPlanId);
        await GuardOptionalReferencesAsync(dto.LocationId);
        var entity = dto.ToEntity(tenantId, userId);
        await _unitOfWork.Repository<SheAssemblyPoint>().AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<SheAssemblyPointDto> UpdateAssemblyPointAsync(UpdateSheAssemblyPointDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAssemblyPointAsync(dto.Id);
        await GuardOptionalReferencesAsync(dto.LocationId);
        entity.UpdateEntity(dto, userId);
        await _unitOfWork.Repository<SheAssemblyPoint>().UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> DeleteAssemblyPointAsync(Guid assemblyPointId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAssemblyPointAsync(assemblyPointId);
        await _unitOfWork.Repository<SheAssemblyPoint>().DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ── Emergency contacts ──
    public async Task<EmergencyContactDto> AddContactAsync(CreateEmergencyContactDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        await GetOwnedPlanAsync(dto.EmergencyPlanId);
        var entity = dto.ToEntity(tenantId, userId);
        await _unitOfWork.Repository<EmergencyContact>().AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<EmergencyContactDto> UpdateContactAsync(UpdateEmergencyContactDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedContactAsync(dto.Id);
        entity.UpdateEntity(dto, userId);
        await _unitOfWork.Repository<EmergencyContact>().UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> DeleteContactAsync(Guid contactId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedContactAsync(contactId);
        await _unitOfWork.Repository<EmergencyContact>().DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ── Drills ──
    public async Task<IEnumerable<EmergencyDrillDto>> GetDrillsForPlanAsync(Guid planId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _drillRepository.GetByPlanIdAsync(planId))
            .Where(e => e.TenantId == tenantId)
            .Select(e => e.ToDto());
    }

    public async Task<IEnumerable<EmergencyDrillDto>> GetUpcomingDrillsAsync(int daysAhead = 30, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _drillRepository.GetUpcomingAsync(daysAhead))
            .Where(e => e.TenantId == tenantId)
            .Select(e => e.ToDto());
    }

    public async Task<EmergencyDrillDto> AddDrillAsync(CreateEmergencyDrillDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        await GetOwnedPlanAsync(dto.EmergencyPlanId);
        var drillNumber = dto.DrillNumber.Trim();
        var exists = await _drillRepository.GetQueryable()
            .AnyAsync(d => d.TenantId == tenantId && d.DrillNumber == drillNumber, cancellationToken);
        if (exists)
            throw new InvalidOperationException($"A drill with number '{drillNumber}' already exists for this tenant.");
        await GetOwnedEmployeeAsync(dto.CoordinatorId);
        await GuardOptionalReferencesAsync(dto.LocationId, dto.DepartmentId);

        var entity = dto.ToEntity(tenantId, userId);
        await _drillRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<EmergencyDrillDto> UpdateDrillAsync(UpdateEmergencyDrillDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedDrillAsync(dto.Id);
        await GetOwnedEmployeeAsync(dto.CoordinatorId);
        await GuardOptionalReferencesAsync(dto.LocationId, dto.DepartmentId);
        entity.UpdateEntity(dto, userId);
        await _drillRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> DeleteDrillAsync(Guid drillId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedDrillAsync(drillId);
        await _drillRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ── Response team ──
    public async Task<EmergencyResponseTeamDto> AddTeamMemberAsync(CreateEmergencyResponseTeamDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        await GetOwnedPlanAsync(dto.EmergencyPlanId);
        await GetOwnedEmployeeAsync(dto.EmployeeId);
        // Deleted rows don't block: removing someone from the team and re-adding them is normal.
        var exists = await _teamRepository.GetQueryable()
            .AnyAsync(t => t.TenantId == tenantId && t.EmergencyPlanId == dto.EmergencyPlanId
                        && t.EmployeeId == dto.EmployeeId && !t.IsDeleted, cancellationToken);
        if (exists)
            throw new InvalidOperationException("This employee is already on the response team for this plan.");

        var entity = dto.ToEntity(tenantId, userId);
        await _teamRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<EmergencyResponseTeamDto> UpdateTeamMemberAsync(UpdateEmergencyResponseTeamDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedTeamMemberAsync(dto.Id);
        entity.UpdateEntity(dto, userId);
        await _teamRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> DeleteTeamMemberAsync(Guid teamMemberId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedTeamMemberAsync(teamMemberId);
        await _teamRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<IEnumerable<EmergencyResponseTeamDto>> GetExpiringTeamCertificatesAsync(int daysAhead = 30, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _teamRepository.GetExpiringCertificatesAsync(daysAhead))
            .Where(e => e.TenantId == tenantId)
            .Select(e => e.ToDto());
    }

    public async Task<IEnumerable<EmergencyResponseTeamDto>> GetTeamMembershipsByEmployeeAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _teamRepository.GetByEmployeeAsync(employeeId))
            .Where(e => e.TenantId == tenantId)
            .Select(e => e.ToDto());
    }
}

#endregion

#region Regulatory Compliance Service

public class SheRegulatoryComplianceService : ISheRegulatoryComplianceService
{
    private readonly ISheRegulatoryObligationRepository _obligationRepository;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<SheRegulatoryComplianceService> _logger;

    public SheRegulatoryComplianceService(
        ISheRegulatoryObligationRepository obligationRepository,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ILogger<SheRegulatoryComplianceService> logger)
    {
        _obligationRepository = obligationRepository;
        _currentUserProvider = currentUserProvider;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    // The ApplicationDbContext is registered without a tenant, so its global tenant query-filter and
    // TenantId auto-stamp are inert. Following the RHEMA convention, this service scopes every read and
    // mutation to the authenticated tenant explicitly and passes it into the repository predicate.
    private Guid GetTenantId()
    {
        var tenantId = _currentUserProvider.TenantId;
        if (tenantId == Guid.Empty)
            throw new InvalidOperationException("No tenant is associated with the current user.");
        return tenantId;
    }

    private Guid RequireCurrentTenant(Guid tenantId)
    {
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");
        return current;
    }

    private async Task<SheRegulatoryObligation> GetOwnedObligationAsync(Guid id)
    {
        var entity = await _obligationRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Regulatory obligation with ID '{id}' not found.");
        return entity;
    }

    private async Task<SheRegulatoryComplianceEvidence> GetOwnedEvidenceAsync(Guid id)
    {
        var entity = await _unitOfWork.Repository<SheRegulatoryComplianceEvidence>().GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Compliance evidence with ID '{id}' not found.");
        return entity;
    }

    public async Task<SheRegulatoryObligationDto> GetObligationAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _obligationRepository.GetWithEvidenceAsync(id);
        if (entity == null || entity.TenantId != tenantId)
            throw new ArgumentException($"Regulatory obligation with ID '{id}' not found.");
        return entity.ToDto();
    }

    public async Task<SheRegulatoryObligationDto?> GetObligationByCodeAsync(string obligationCode, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _obligationRepository.GetByCodeAsync(obligationCode);
        if (entity == null || entity.TenantId != tenantId)
            return null;
        return entity.ToDto();
    }

    public async Task<IEnumerable<SheRegulatoryObligationSummaryDto>> GetAllObligationsAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _obligationRepository.GetAllSummaryAsync())
            .Where(e => e.TenantId == tenantId)
            .ToSummaryDtoList();
    }

    public async Task<IEnumerable<SheRegulatoryObligationSummaryDto>> GetObligationsByDomainAsync(SheRegulatoryDomain domain, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _obligationRepository.GetByDomainAsync(domain))
            .Where(e => e.TenantId == tenantId)
            .ToSummaryDtoList();
    }

    public async Task<IEnumerable<SheRegulatoryObligationSummaryDto>> GetObligationsByStatusAsync(SheComplianceStatus status, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _obligationRepository.GetByComplianceStatusAsync(status))
            .Where(e => e.TenantId == tenantId)
            .ToSummaryDtoList();
    }

    public async Task<IEnumerable<SheRegulatoryObligationSummaryDto>> GetObligationsByOwnerAsync(Guid ownerId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _obligationRepository.GetByOwnerAsync(ownerId))
            .Where(e => e.TenantId == tenantId)
            .ToSummaryDtoList();
    }

    public async Task<IEnumerable<SheRegulatoryObligationSummaryDto>> GetObligationsDueForReviewAsync(int daysAhead = 30, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _obligationRepository.GetDueForReviewAsync(daysAhead))
            .Where(e => e.TenantId == tenantId)
            .ToSummaryDtoList();
    }

    public async Task<IEnumerable<SheRegulatoryObligationSummaryDto>> GetNonCompliantObligationsAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _obligationRepository.GetNonCompliantAsync())
            .Where(e => e.TenantId == tenantId)
            .ToSummaryDtoList();
    }

    public async Task<SheRegulatoryObligationDto> CreateObligationAsync(CreateSheRegulatoryObligationDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        var entity = dto.ToEntity(tenantId, userId);
        await _obligationRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<SheRegulatoryObligationDto> UpdateObligationAsync(UpdateSheRegulatoryObligationDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedObligationAsync(dto.Id);
        entity.UpdateEntity(dto, userId);
        await _obligationRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> DeleteObligationAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedObligationAsync(id);
        await _obligationRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<SheRegulatoryComplianceEvidenceDto> AddEvidenceAsync(CreateSheRegulatoryComplianceEvidenceDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        var entity = dto.ToEntity(tenantId, userId);
        await _unitOfWork.Repository<SheRegulatoryComplianceEvidence>().AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> DeleteEvidenceAsync(Guid evidenceId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedEvidenceAsync(evidenceId);
        await _unitOfWork.Repository<SheRegulatoryComplianceEvidence>().DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }
}

#endregion

#region Safety Signage Service

public class SafetySignageService : ISafetySignageService
{
    private readonly ISafetySignRepository _signRepository;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<SafetySignageService> _logger;

    public SafetySignageService(
        ISafetySignRepository signRepository,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ILogger<SafetySignageService> logger)
    {
        _signRepository = signRepository;
        _currentUserProvider = currentUserProvider;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    // The ApplicationDbContext is registered without a tenant, so its global tenant query-filter and
    // TenantId auto-stamp are inert. Following the RHEMA convention, this service scopes every read and
    // mutation to the authenticated tenant explicitly and passes it into the repository predicate.
    private Guid GetTenantId()
    {
        var tenantId = _currentUserProvider.TenantId;
        if (tenantId == Guid.Empty)
            throw new InvalidOperationException("No tenant is associated with the current user.");
        return tenantId;
    }

    private Guid RequireCurrentTenant(Guid tenantId)
    {
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");
        return current;
    }

    private async Task<SafetySign> GetOwnedSignAsync(Guid id)
    {
        var entity = await _signRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Safety sign with ID '{id}' not found.");
        return entity;
    }

    public async Task<SafetySignDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _signRepository.GetByIdAsync(id, s => s.Location);
        if (entity == null || entity.TenantId != tenantId)
            throw new ArgumentException($"Safety sign with ID '{id}' not found.");
        return entity.ToDto();
    }

    public async Task<SafetySignDto?> GetByCodeAsync(string signCode, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _signRepository.GetByCodeAsync(signCode);
        if (entity == null || entity.TenantId != tenantId)
            return null;
        return entity.ToDto();
    }

    public async Task<IEnumerable<SafetySignDto>> GetByLocationAsync(Guid locationId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _signRepository.GetByLocationAsync(locationId))
            .Where(e => e.TenantId == tenantId)
            .Select(e => e.ToDto());
    }

    public async Task<IEnumerable<SafetySignDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _signRepository.GetAllSummaryAsync())
            .Where(e => e.TenantId == tenantId)
            .Select(e => e.ToDto());
    }

    public async Task<IEnumerable<SafetySignDto>> GetByTypeAsync(SheSafetySignType type, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _signRepository.GetByTypeAsync(type))
            .Where(e => e.TenantId == tenantId)
            .Select(e => e.ToDto());
    }

    public async Task<IEnumerable<SafetySignDto>> GetByStatusAsync(SheSafetySignStatus status, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _signRepository.GetByStatusAsync(status))
            .Where(e => e.TenantId == tenantId)
            .Select(e => e.ToDto());
    }

    public async Task<IEnumerable<SafetySignDto>> GetActiveAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _signRepository.GetActiveAsync())
            .Where(e => e.TenantId == tenantId)
            .Select(e => e.ToDto());
    }

    public async Task<IEnumerable<SafetySignDto>> GetDueForInspectionAsync(int daysAhead = 30, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _signRepository.GetDueForInspectionAsync(daysAhead))
            .Where(e => e.TenantId == tenantId)
            .Select(e => e.ToDto());
    }

    public async Task<SafetySignDto> CreateAsync(CreateSafetySignDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        var entity = dto.ToEntity(tenantId, userId);
        await _signRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<SafetySignDto> UpdateAsync(UpdateSafetySignDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedSignAsync(dto.Id);
        entity.UpdateEntity(dto, userId);
        await _signRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedSignAsync(id);
        await _signRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }
}

#endregion

#region SHE Performance Service

public class ShePerformanceService : IShePerformanceService
{
    private readonly IShePerformanceSnapshotRepository _snapshotRepository;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ShePerformanceService> _logger;

    public ShePerformanceService(
        IShePerformanceSnapshotRepository snapshotRepository,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ILogger<ShePerformanceService> logger)
    {
        _snapshotRepository = snapshotRepository;
        _currentUserProvider = currentUserProvider;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    // The ApplicationDbContext is registered without a tenant, so its global tenant query-filter and
    // TenantId auto-stamp are inert. Following the RHEMA convention, this service scopes every read and
    // mutation to the authenticated tenant explicitly and passes it into the repository predicate.
    private Guid GetTenantId()
    {
        var tenantId = _currentUserProvider.TenantId;
        if (tenantId == Guid.Empty)
            throw new InvalidOperationException("No tenant is associated with the current user.");
        return tenantId;
    }

    private Guid RequireCurrentTenant(Guid tenantId)
    {
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");
        return current;
    }

    private async Task<ShePerformanceSnapshot> GetOwnedSnapshotAsync(Guid id)
    {
        var entity = await _snapshotRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Performance snapshot with ID '{id}' not found.");
        return entity;
    }

    public async Task<ShePerformanceSnapshotDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _snapshotRepository.GetByIdAsync(id, s => s.Location, s => s.PreparedBy, s => s.ReviewedBy);
        if (entity == null || entity.TenantId != tenantId)
            throw new ArgumentException($"Performance snapshot with ID '{id}' not found.");
        return entity.ToDto();
    }

    public async Task<ShePerformanceSnapshotDto?> GetByNumberAsync(string snapshotNumber, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _snapshotRepository.GetByNumberAsync(snapshotNumber);
        if (entity == null || entity.TenantId != tenantId)
            return null;
        return entity.ToDto();
    }

    public async Task<IEnumerable<ShePerformanceSnapshotSummaryDto>> GetByYearAsync(int year, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _snapshotRepository.GetByYearAsync(year))
            .Where(e => e.TenantId == tenantId)
            .Select(e => e.ToSummaryDto());
    }

    public async Task<IEnumerable<ShePerformanceSnapshotSummaryDto>> GetByLocationAsync(Guid locationId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _snapshotRepository.GetByLocationAsync(locationId))
            .Where(e => e.TenantId == tenantId)
            .Select(e => e.ToSummaryDto());
    }

    public async Task<ShePerformanceSnapshotDto?> GetLatestAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _snapshotRepository.GetLatestAsync(tenantId);
        return entity?.ToDto();
    }

    public async Task<ShePerformanceSnapshotDto> CreateAsync(CreateShePerformanceSnapshotDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);

        // Snapshot numbers are user-entered, and GetByNumberAsync resolves by number — a duplicate
        // would make that lookup (and the screens built on it) answer with an arbitrary row.
        var sameNumber = await _snapshotRepository.GetByNumberAsync(dto.SnapshotNumber);
        if (sameNumber != null && sameNumber.TenantId == tenantId)
            throw new InvalidOperationException($"A performance snapshot with number '{dto.SnapshotNumber}' already exists.");

        // One snapshot per period+location: a second row for the same period would silently fork the
        // reported figures (these are hand-reported, not computed — see slice 14).
        var samePeriod = await _snapshotRepository.GetByPeriodAsync(dto.PeriodType, dto.Year, dto.PeriodNumber, dto.LocationId);
        if (samePeriod != null && samePeriod.TenantId == tenantId)
            throw new InvalidOperationException(
                $"A performance snapshot for {dto.PeriodType} {dto.Year}{(dto.PeriodNumber is int p ? $" period {p}" : "")} already exists ('{samePeriod.SnapshotNumber}').");

        var entity = dto.ToEntity(tenantId, userId);
        await _snapshotRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Re-read through the include-bearing path: the tracked entity's PreparedBy/Location navs
        // are unloaded here, and mapping them straight to the DTO returns blank names.
        return await GetByIdAsync(entity.Id, cancellationToken);
    }

    public async Task<ShePerformanceSnapshotDto> UpdateAsync(UpdateShePerformanceSnapshotDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedSnapshotAsync(dto.Id);

        // A reviewed snapshot is the record management signed off on — corrections after that
        // point would silently invalidate the review.
        if (entity.ReviewedById != null)
            throw new InvalidOperationException(
                $"Snapshot '{entity.SnapshotNumber}' has already been reviewed and is locked. Delete and re-enter it if the figures are wrong.");

        entity.UpdateEntity(dto, userId);
        await _snapshotRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Same nav-loading reasoning as CreateAsync.
        return await GetByIdAsync(entity.Id, cancellationToken);
    }

    public async Task<bool> ReviewAsync(ReviewShePerformanceSnapshotDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedSnapshotAsync(dto.SnapshotId);

        entity.ReviewedById = dto.ReviewedById;
        entity.ReviewedDate = dto.ReviewedDate;
        entity.ManagementComments = dto.ManagementComments;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();

        await _snapshotRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedSnapshotAsync(id);
        await _snapshotRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }
}

#endregion

#region Safety Committee Service

public class SafetyCommitteeService : ISafetyCommitteeService
{
    private readonly ISafetyCommitteeRepository _committeeRepository;
    private readonly ISafetyCommitteeMemberRepository _memberRepository;
    private readonly ISafetyMeetingRepository _meetingRepository;
    private readonly ISafetyMeetingActionItemRepository _actionItemRepository;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<SafetyCommitteeService> _logger;

    public SafetyCommitteeService(
        ISafetyCommitteeRepository committeeRepository,
        ISafetyCommitteeMemberRepository memberRepository,
        ISafetyMeetingRepository meetingRepository,
        ISafetyMeetingActionItemRepository actionItemRepository,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ILogger<SafetyCommitteeService> logger)
    {
        _committeeRepository = committeeRepository;
        _memberRepository = memberRepository;
        _meetingRepository = meetingRepository;
        _actionItemRepository = actionItemRepository;
        _currentUserProvider = currentUserProvider;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    // The ApplicationDbContext is registered without a tenant, so its global tenant query-filter and
    // TenantId auto-stamp are inert. Following the RHEMA convention, this service scopes every read and
    // mutation to the authenticated tenant explicitly and passes it into the repository predicate.
    private Guid GetTenantId()
    {
        var tenantId = _currentUserProvider.TenantId;
        if (tenantId == Guid.Empty)
            throw new InvalidOperationException("No tenant is associated with the current user.");
        return tenantId;
    }

    private Guid RequireCurrentTenant(Guid tenantId)
    {
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");
        return current;
    }

    private async Task<SafetyCommittee> GetOwnedCommitteeAsync(Guid id)
    {
        var entity = await _committeeRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Safety committee with ID '{id}' not found.");
        return entity;
    }

    private async Task<SafetyCommitteeMember> GetOwnedMemberAsync(Guid id)
    {
        var entity = await _memberRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Committee member with ID '{id}' not found.");
        return entity;
    }

    private async Task<SafetyMeeting> GetOwnedMeetingAsync(Guid id)
    {
        var entity = await _meetingRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Safety meeting with ID '{id}' not found.");
        return entity;
    }

    private async Task<SafetyMeetingAttendee> GetOwnedAttendeeAsync(Guid id)
    {
        var entity = await _unitOfWork.Repository<SafetyMeetingAttendee>().GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Meeting attendee with ID '{id}' not found.");
        return entity;
    }

    private async Task<SafetyMeetingActionItem> GetOwnedActionItemAsync(Guid id)
    {
        var entity = await _actionItemRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Meeting action item with ID '{id}' not found.");
        return entity;
    }

    private async Task<SafetyMeetingDocument> GetOwnedMeetingDocumentAsync(Guid id)
    {
        var entity = await _unitOfWork.Repository<SafetyMeetingDocument>().GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Meeting document with ID '{id}' not found.");
        return entity;
    }

    // ── Committees ──
    public async Task<SafetyCommitteeDto> GetCommitteeAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _committeeRepository.GetWithMembersAsync(id);
        if (entity == null || entity.TenantId != tenantId)
            throw new ArgumentException($"Safety committee with ID '{id}' not found.");
        return entity.ToDto();
    }

    public async Task<IEnumerable<SafetyCommitteeDto>> GetActiveCommitteesAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _committeeRepository.GetActiveAsync())
            .Where(e => e.TenantId == tenantId)
            .Select(e => e.ToDto());
    }

    public async Task<SafetyCommitteeDto> CreateCommitteeAsync(CreateSafetyCommitteeDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        var entity = dto.ToEntity(tenantId, userId);
        await _committeeRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<SafetyCommitteeDto> UpdateCommitteeAsync(UpdateSafetyCommitteeDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedCommitteeAsync(dto.Id);
        entity.UpdateEntity(dto, userId);
        await _committeeRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> DeleteCommitteeAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedCommitteeAsync(id);
        await _committeeRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ── Members ──
    public async Task<IEnumerable<SafetyCommitteeMemberDto>> GetMembersAsync(Guid committeeId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _memberRepository.GetByCommitteeIdAsync(committeeId))
            .Where(e => e.TenantId == tenantId)
            .Select(e => e.ToDto());
    }

    public async Task<SafetyCommitteeMemberDto> AddMemberAsync(CreateSafetyCommitteeMemberDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        var entity = dto.ToEntity(tenantId, userId);
        await _memberRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<SafetyCommitteeMemberDto> UpdateMemberAsync(UpdateSafetyCommitteeMemberDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedMemberAsync(dto.Id);
        entity.UpdateEntity(dto, userId);
        await _memberRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> RemoveMemberAsync(Guid memberId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedMemberAsync(memberId);
        await _memberRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ── Meetings ──
    public async Task<SafetyMeetingDto> GetMeetingAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _meetingRepository.GetWithFullDetailsAsync(id);
        if (entity == null || entity.TenantId != tenantId)
            throw new ArgumentException($"Safety meeting with ID '{id}' not found.");
        return entity.ToDto();
    }

    public async Task<IEnumerable<SafetyMeetingSummaryDto>> GetMeetingsByCommitteeAsync(Guid committeeId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _meetingRepository.GetByCommitteeIdAsync(committeeId))
            .Where(e => e.TenantId == tenantId)
            .ToSummaryDtoList();
    }

    public async Task<IEnumerable<SafetyMeetingSummaryDto>> GetMeetingsByDateRangeAsync(DateTime fromDate, DateTime toDate, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _meetingRepository.GetByDateRangeAsync(fromDate, toDate))
            .Where(e => e.TenantId == tenantId)
            .ToSummaryDtoList();
    }

    public async Task<SafetyMeetingDto> CreateMeetingAsync(CreateSafetyMeetingDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        var entity = dto.ToEntity(tenantId, userId);
        await _meetingRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<SafetyMeetingDto> UpdateMeetingAsync(UpdateSafetyMeetingDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedMeetingAsync(dto.Id);
        entity.UpdateEntity(dto, userId);
        await _meetingRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> DeleteMeetingAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedMeetingAsync(id);
        await _meetingRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ── Attendees ──
    public async Task<SafetyMeetingAttendeeDto> AddAttendeeAsync(CreateSafetyMeetingAttendeeDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        var entity = dto.ToEntity(tenantId, userId);
        await _unitOfWork.Repository<SafetyMeetingAttendee>().AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> RemoveAttendeeAsync(Guid attendeeId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAttendeeAsync(attendeeId);
        await _unitOfWork.Repository<SafetyMeetingAttendee>().DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ── Action items ──
    public async Task<IEnumerable<SafetyMeetingActionItemDto>> GetOpenActionItemsAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _actionItemRepository.GetOpenAsync())
            .Where(e => e.TenantId == tenantId)
            .Select(e => e.ToDto());
    }

    public async Task<IEnumerable<SafetyMeetingActionItemDto>> GetOverdueActionItemsAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _actionItemRepository.GetOverdueAsync())
            .Where(e => e.TenantId == tenantId)
            .Select(e => e.ToDto());
    }

    public async Task<IEnumerable<SafetyMeetingActionItemDto>> GetActionItemsByAssigneeAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _actionItemRepository.GetByAssigneeAsync(employeeId))
            .Where(e => e.TenantId == tenantId)
            .Select(e => e.ToDto());
    }

    public async Task<SafetyMeetingActionItemDto> AddActionItemAsync(CreateSafetyMeetingActionItemDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        var entity = dto.ToEntity(tenantId, userId);
        await _actionItemRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<SafetyMeetingActionItemDto> UpdateActionItemAsync(UpdateSafetyMeetingActionItemDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedActionItemAsync(dto.Id);
        entity.UpdateEntity(dto, userId);
        await _actionItemRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> DeleteActionItemAsync(Guid actionItemId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedActionItemAsync(actionItemId);
        await _actionItemRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ── Documents ──
    public async Task<SafetyMeetingDocumentDto> AddMeetingDocumentAsync(CreateSafetyMeetingDocumentDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        var entity = dto.ToEntity(tenantId, userId);
        await _unitOfWork.Repository<SafetyMeetingDocument>().AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> DeleteMeetingDocumentAsync(Guid documentId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedMeetingDocumentAsync(documentId);
        await _unitOfWork.Repository<SafetyMeetingDocument>().DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }
}

#endregion

#region Return-to-Work Service

public class SheReturnToWorkService : ISheReturnToWorkService
{
    private readonly ISheReturnToWorkPlanRepository _planRepository;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<SheReturnToWorkService> _logger;

    public SheReturnToWorkService(
        ISheReturnToWorkPlanRepository planRepository,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ILogger<SheReturnToWorkService> logger)
    {
        _planRepository = planRepository;
        _currentUserProvider = currentUserProvider;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    // The ApplicationDbContext is registered without a tenant, so its global tenant query-filter and
    // TenantId auto-stamp are inert. Following the RHEMA convention, this service scopes every read and
    // mutation to the authenticated tenant explicitly and passes it into the repository predicate.
    private Guid GetTenantId()
    {
        var tenantId = _currentUserProvider.TenantId;
        if (tenantId == Guid.Empty)
            throw new InvalidOperationException("No tenant is associated with the current user.");
        return tenantId;
    }

    private Guid RequireCurrentTenant(Guid tenantId)
    {
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");
        return current;
    }

    private async Task<SheReturnToWorkPlan> GetOwnedPlanAsync(Guid id)
    {
        var entity = await _planRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Return-to-work plan with ID '{id}' not found.");
        return entity;
    }

    private async Task<SheReturnToWorkPhase> GetOwnedPhaseAsync(Guid id)
    {
        var entity = await _unitOfWork.Repository<SheReturnToWorkPhase>().GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Return-to-work phase with ID '{id}' not found.");
        return entity;
    }

    public async Task<SheReturnToWorkPlanDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _planRepository.GetWithFullDetailsAsync(id);
        if (entity == null || entity.TenantId != tenantId)
            throw new ArgumentException($"Return-to-work plan with ID '{id}' not found.");
        return entity.ToDto();
    }

    public async Task<SheReturnToWorkPlanDto?> GetByNumberAsync(string planNumber, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _planRepository.GetByNumberAsync(planNumber);
        if (entity == null || entity.TenantId != tenantId)
            return null;
        return entity.ToDto();
    }

    public async Task<IEnumerable<SheReturnToWorkPlanSummaryDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _planRepository.GetAllSummaryAsync())
            .Where(e => e.TenantId == tenantId)
            .ToSummaryDtoList();
    }

    public async Task<IEnumerable<SheReturnToWorkPlanSummaryDto>> GetByEmployeeAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _planRepository.GetByEmployeeAsync(employeeId))
            .Where(e => e.TenantId == tenantId)
            .ToSummaryDtoList();
    }

    public async Task<IEnumerable<SheReturnToWorkPlanSummaryDto>> GetByStatusAsync(SheReturnToWorkStatus status, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _planRepository.GetByStatusAsync(status))
            .Where(e => e.TenantId == tenantId)
            .ToSummaryDtoList();
    }

    public async Task<IEnumerable<SheReturnToWorkPlanSummaryDto>> GetByIncidentAsync(Guid safetyIncidentId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _planRepository.GetByIncidentAsync(safetyIncidentId))
            .Where(e => e.TenantId == tenantId)
            .ToSummaryDtoList();
    }

    public async Task<IEnumerable<SheReturnToWorkPlanSummaryDto>> GetActiveAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _planRepository.GetActiveAsync())
            .Where(e => e.TenantId == tenantId)
            .ToSummaryDtoList();
    }

    public async Task<SheReturnToWorkPlanDto> CreateAsync(CreateSheReturnToWorkPlanDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        var entity = dto.ToEntity(tenantId, userId);
        await _planRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<SheReturnToWorkPlanDto> UpdateAsync(UpdateSheReturnToWorkPlanDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedPlanAsync(dto.Id);
        entity.UpdateEntity(dto, userId);
        await _planRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedPlanAsync(id);
        await _planRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<SheReturnToWorkPhaseDto> AddPhaseAsync(CreateSheReturnToWorkPhaseDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        var entity = dto.ToEntity(tenantId, userId);
        await _unitOfWork.Repository<SheReturnToWorkPhase>().AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<SheReturnToWorkPhaseDto> UpdatePhaseAsync(UpdateSheReturnToWorkPhaseDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedPhaseAsync(dto.Id);
        entity.UpdateEntity(dto, userId);
        await _unitOfWork.Repository<SheReturnToWorkPhase>().UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> DeletePhaseAsync(Guid phaseId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedPhaseAsync(phaseId);
        await _unitOfWork.Repository<SheReturnToWorkPhase>().DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<IEnumerable<SheReturnToWorkReviewDto>> GetReviewsAsync(Guid planId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var reviews = await _unitOfWork.Repository<SheReturnToWorkReview>()
            .FindAsync(r => r.TenantId == tenantId && r.ReturnToWorkPlanId == planId, r => r.ReviewedBy);
        return reviews.OrderBy(r => r.ReviewNumber).Select(r => r.ToDto());
    }

    public async Task<SheReturnToWorkReviewDto> AddReviewAsync(CreateSheReturnToWorkReviewDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        var entity = dto.ToEntity(tenantId, userId);
        await _unitOfWork.Repository<SheReturnToWorkReview>().AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }
}

#endregion
