using ErpSystem.Application.HR.Extensions;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.HR.Performance;
using ErpSystem.Core.Entities.HR.PromotionTransfer;
using ErpSystem.Core.Entities.HR.StaffDiscipline;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

// ============================================================================
// STAFF PROMOTION SERVICE
// ============================================================================

#region Staff Promotion Service

public class StaffPromotionService : IStaffPromotionService
{
    private readonly IStaffPromotionRepository _repo;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<StaffPromotionService> _logger;

    public StaffPromotionService(
        IStaffPromotionRepository repo,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ILogger<StaffPromotionService> logger)
    {
        _repo       = repo;
        _currentUserProvider = currentUserProvider;
        _unitOfWork = unitOfWork;
        _logger     = logger;
    }

    // The ApplicationDbContext is registered without a tenant, so its global tenant query-filter and
    // TenantId auto-stamp are inert. Following the RHEMA convention, this service scopes every read and
    // mutation to the authenticated tenant explicitly and passes it into the repository predicate.
    private Guid GetTenantId()
    {
        var tenantId = _currentUserProvider.TenantId;
        if (tenantId == Guid.Empty)
            throw new UnauthorizedAccessException("No tenant is associated with the current user.");
        return tenantId;
    }

    private Guid RequireCurrentTenant(Guid tenantId)
    {
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");
        return current;
    }

    private async Task<StaffPromotion> GetOwnedAsync(Guid id)
    {
        var entity = await _repo.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Staff promotion with ID '{id}' was not found.");
        return entity;
    }

    /// <summary>
    /// Validates the movement a detail row is being attached to.
    ///
    /// Three things none of the subtype services checked. The parent was never tested for tenancy,
    /// so a detail row could be attached to ANOTHER TENANT'S movement by passing its id — the
    /// cross-tenant child-attach shape. The parent's type was never tested, so a promotion detail
    /// could hang off a demotion and the movement would then describe two different things at once.
    /// And the relationship is one-to-one, so a second detail row for the same movement is not an
    /// update, it is a contradiction.
    /// </summary>
    private async Task<StaffMovement> GetOwnedParentMovementAsync(
        Guid movementId, StaffMovementType expectedType, bool requireNoExistingDetail)
    {
        var tenantId = GetTenantId();

        var movement = await _unitOfWork.Repository<StaffMovement>().GetByIdAsync(movementId);
        if (movement == null || movement.IsDeleted || movement.TenantId != tenantId)
            throw new ArgumentException($"Staff movement with ID '{movementId}' was not found.");

        if (movement.MovementType != expectedType)
            throw new InvalidOperationException(
                $"That movement is a {movement.MovementType}, so it cannot carry a promotion detail record.");

        if (requireNoExistingDetail)
        {
            var existing = await _repo.GetByMovementIdAsync(movementId);
            if (existing != null && !existing.IsDeleted)
                throw new InvalidOperationException(
                    $"Movement {movement.MovementNumber} already has a promotion detail record.");
        }

        return movement;
    }

    /// <summary>
    /// How many salary-grade bands the movement crosses.
    ///
    /// The entity documents this as service-computed and it was taken from the request body instead,
    /// so the number reported to management was whatever the person filling in the form typed.
    ///
    /// ⚠ SalaryGrade carries no rank, sequence or level column — only Code, Name and a salary band —
    /// so the bands are ordered by MinSalary, which is the only orderable thing about them. That is a
    /// proxy, and it is documented as one: if TDC ever gives grades an explicit order, order by that.
    /// Grades are payroll's (read-only here).
    /// </summary>
    private async Task<int> ComputeGradeBandChangeAsync(StaffMovement movement, CancellationToken cancellationToken)
    {
        if (movement.CurrentSalaryGradeId is not Guid fromGradeId ||
            movement.NewSalaryGradeId is not Guid toGradeId ||
            fromGradeId == toGradeId)
            return 0;

        var tenantId = GetTenantId();
        var grades = await _unitOfWork.Repository<SalaryGrade>()
            .GetQueryable(g => g.TenantId == tenantId && g.IsActive)
            .OrderBy(g => g.MinSalary)
            .Select(g => g.Id)
            .ToListAsync(cancellationToken);

        var fromIndex = grades.IndexOf(fromGradeId);
        var toIndex = grades.IndexOf(toGradeId);
        if (fromIndex < 0 || toIndex < 0)
            return 0;

        return Math.Abs(toIndex - fromIndex);
    }

    public async Task<StaffPromotionDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(id);
        return entity.ToDto();
    }

    public async Task<StaffPromotionDto?> GetByMovementIdAsync(Guid movementId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _repo.GetByMovementIdAsync(movementId);
        return entity != null && entity.TenantId == tenantId ? entity.ToDto() : null;
    }

    public async Task<IEnumerable<StaffPromotionDto>> GetByTypeAsync(StaffPromotionType type, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _repo.GetByTypeAsync(type);
        return entities.Where(e => e.TenantId == tenantId).Select(e => e.ToDto()).ToList();
    }

    public async Task<IEnumerable<StaffPromotionDto>> GetActiveActingPromotionsAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _repo.GetActiveActingPromotionsAsync();
        return entities.Where(e => e.TenantId == tenantId).Select(e => e.ToDto()).ToList();
    }

    public async Task<StaffPromotionDto> CreateAsync(CreateStaffPromotionDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        var movement = await GetOwnedParentMovementAsync(
            createDto.MovementId, StaffMovementType.Promotion, requireNoExistingDetail: true);

        var entity = createDto.ToEntity(tenantId, createdByUserId);
        entity.GradeLevelIncrease = await ComputeGradeBandChangeAsync(movement, cancellationToken);

        await _repo.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Staff promotion detail created for movement {MovementId}", entity.MovementId);

        return entity.ToDto();
    }

    public async Task<StaffPromotionDto> UpdateAsync(UpdateStaffPromotionDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(updateDto.Id);
        var movement = await GetOwnedParentMovementAsync(
            entity.MovementId, StaffMovementType.Promotion, requireNoExistingDetail: false);

        entity.UpdateEntity(updateDto, updatedByUserId);

        // Recomputed on every edit, not just on create: the movement's grades can change while the
        // detail row exists, and a stale band count is worse than none.
        entity.GradeLevelIncrease = await ComputeGradeBandChangeAsync(movement, cancellationToken);

        await _repo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return (await _repo.GetByMovementIdAsync(entity.MovementId) ?? entity).ToDto();
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(id);

        await _repo.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }
}

#endregion

// ============================================================================
// STAFF TRANSFER SERVICE
// ============================================================================

#region Staff Transfer Service

public class StaffTransferService : IStaffTransferService
{
    private readonly IStaffTransferRepository _repo;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<StaffTransferService> _logger;

    public StaffTransferService(
        IStaffTransferRepository repo,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ILogger<StaffTransferService> logger)
    {
        _repo       = repo;
        _currentUserProvider = currentUserProvider;
        _unitOfWork = unitOfWork;
        _logger     = logger;
    }

    // The ApplicationDbContext is registered without a tenant, so its global tenant query-filter and
    // TenantId auto-stamp are inert. Following the RHEMA convention, this service scopes every read and
    // mutation to the authenticated tenant explicitly and passes it into the repository predicate.
    private Guid GetTenantId()
    {
        var tenantId = _currentUserProvider.TenantId;
        if (tenantId == Guid.Empty)
            throw new UnauthorizedAccessException("No tenant is associated with the current user.");
        return tenantId;
    }

    private Guid RequireCurrentTenant(Guid tenantId)
    {
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");
        return current;
    }

    private async Task<StaffTransfer> GetOwnedAsync(Guid id)
    {
        var entity = await _repo.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Staff transfer with ID '{id}' was not found.");
        return entity;
    }

    /// <summary>
    /// Validates the movement a detail row is being attached to.
    ///
    /// Three things none of the subtype services checked. The parent was never tested for tenancy,
    /// so a detail row could be attached to ANOTHER TENANT'S movement by passing its id — the
    /// cross-tenant child-attach shape. The parent's type was never tested, so a promotion detail
    /// could hang off a demotion and the movement would then describe two different things at once.
    /// And the relationship is one-to-one, so a second detail row for the same movement is not an
    /// update, it is a contradiction.
    /// </summary>
    private async Task<StaffMovement> GetOwnedParentMovementAsync(
        Guid movementId, StaffMovementType expectedType, bool requireNoExistingDetail)
    {
        var tenantId = GetTenantId();

        var movement = await _unitOfWork.Repository<StaffMovement>().GetByIdAsync(movementId);
        if (movement == null || movement.IsDeleted || movement.TenantId != tenantId)
            throw new ArgumentException($"Staff movement with ID '{movementId}' was not found.");

        if (movement.MovementType != expectedType)
            throw new InvalidOperationException(
                $"That movement is a {movement.MovementType}, so it cannot carry a transfer detail record.");

        if (requireNoExistingDetail)
        {
            var existing = await _repo.GetByMovementIdAsync(movementId);
            if (existing != null && !existing.IsDeleted)
                throw new InvalidOperationException(
                    $"Movement {movement.MovementNumber} already has a transfer detail record.");
        }

        return movement;
    }

    /// <summary>
    /// Validates the replacement employee, if one is named. Unvalidated, a bad id reached SQL as an
    /// FK violation and surfaced as an unexplained 500; the guard also leaves the row tracked, so
    /// the write response resolves the replacement's name.
    /// </summary>
    private async Task ValidateReplacementAsync(Guid? replacementEmployeeId)
    {
        if (replacementEmployeeId is not Guid employeeId) return;

        var employee = await _unitOfWork.Repository<Employee>().GetByIdAsync(employeeId);
        if (employee == null || employee.IsDeleted || employee.TenantId != GetTenantId())
            throw new ArgumentException($"The replacement employee with ID '{employeeId}' was not found.");
    }

    public async Task<StaffTransferDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(id);
        return entity.ToDto();
    }

    public async Task<StaffTransferDto?> GetByMovementIdAsync(Guid movementId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _repo.GetByMovementIdAsync(movementId);
        return entity != null && entity.TenantId == tenantId ? entity.ToDto() : null;
    }

    public async Task<IEnumerable<StaffTransferDto>> GetByTypeAsync(StaffTransferType type, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _repo.GetByTypeAsync(type);
        return entities.Where(e => e.TenantId == tenantId).Select(e => e.ToDto()).ToList();
    }

    public async Task<IEnumerable<StaffTransferDto>> GetByReasonCategoryAsync(StaffTransferReasonCategory reasonCategory, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _repo.GetByReasonCategoryAsync(reasonCategory);
        return entities.Where(e => e.TenantId == tenantId).Select(e => e.ToDto()).ToList();
    }

    public async Task<IEnumerable<StaffTransferDto>> GetInterCompanyTransfersAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _repo.GetInterCompanyTransfersAsync();
        return entities.Where(e => e.TenantId == tenantId).Select(e => e.ToDto()).ToList();
    }

    public async Task<IEnumerable<StaffTransferDto>> GetRelocationTransfersAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _repo.GetRelocationTransfersAsync();
        return entities.Where(e => e.TenantId == tenantId).Select(e => e.ToDto()).ToList();
    }

    public async Task<IEnumerable<StaffTransferDto>> GetInTransitionAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _repo.GetInTransitionAsync();
        return entities.Where(e => e.TenantId == tenantId).Select(e => e.ToDto()).ToList();
    }

    public async Task<StaffTransferDto> CreateAsync(CreateStaffTransferDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        await GetOwnedParentMovementAsync(
            createDto.MovementId, StaffMovementType.Transfer, requireNoExistingDetail: true);
        await ValidateReplacementAsync(createDto.ReplacementEmployeeId);

        var entity = createDto.ToEntity(tenantId, createdByUserId);

        await _repo.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Staff transfer detail created for movement {MovementId}", entity.MovementId);

        return entity.ToDto();
    }

    public async Task<StaffTransferDto> UpdateAsync(UpdateStaffTransferDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(updateDto.Id);
        await ValidateReplacementAsync(updateDto.ReplacementEmployeeId);

        entity.UpdateEntity(updateDto, updatedByUserId);

        await _repo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return entity.ToDto();
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(id);

        await _repo.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }
}

#endregion

// ============================================================================
// STAFF DEMOTION SERVICE
// ============================================================================

#region Staff Demotion Service

public class StaffDemotionService : IStaffDemotionService
{
    private readonly IStaffDemotionRepository _repo;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<StaffDemotionService> _logger;

    public StaffDemotionService(
        IStaffDemotionRepository repo,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ILogger<StaffDemotionService> logger)
    {
        _repo       = repo;
        _currentUserProvider = currentUserProvider;
        _unitOfWork = unitOfWork;
        _logger     = logger;
    }

    // The ApplicationDbContext is registered without a tenant, so its global tenant query-filter and
    // TenantId auto-stamp are inert. Following the RHEMA convention, this service scopes every read and
    // mutation to the authenticated tenant explicitly and passes it into the repository predicate.
    private Guid GetTenantId()
    {
        var tenantId = _currentUserProvider.TenantId;
        if (tenantId == Guid.Empty)
            throw new UnauthorizedAccessException("No tenant is associated with the current user.");
        return tenantId;
    }

    private Guid RequireCurrentTenant(Guid tenantId)
    {
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");
        return current;
    }

    private async Task<StaffDemotion> GetOwnedAsync(Guid id)
    {
        var entity = await _repo.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Staff demotion with ID '{id}' was not found.");
        return entity;
    }

    /// <summary>
    /// Validates the movement a detail row is being attached to.
    ///
    /// Three things none of the subtype services checked. The parent was never tested for tenancy,
    /// so a detail row could be attached to ANOTHER TENANT'S movement by passing its id — the
    /// cross-tenant child-attach shape. The parent's type was never tested, so a promotion detail
    /// could hang off a demotion and the movement would then describe two different things at once.
    /// And the relationship is one-to-one, so a second detail row for the same movement is not an
    /// update, it is a contradiction.
    /// </summary>
    private async Task<StaffMovement> GetOwnedParentMovementAsync(
        Guid movementId, StaffMovementType expectedType, bool requireNoExistingDetail)
    {
        var tenantId = GetTenantId();

        var movement = await _unitOfWork.Repository<StaffMovement>().GetByIdAsync(movementId);
        if (movement == null || movement.IsDeleted || movement.TenantId != tenantId)
            throw new ArgumentException($"Staff movement with ID '{movementId}' was not found.");

        if (movement.MovementType != expectedType)
            throw new InvalidOperationException(
                $"That movement is a {movement.MovementType}, so it cannot carry a demotion detail record.");

        if (requireNoExistingDetail)
        {
            var existing = await _repo.GetByMovementIdAsync(movementId);
            if (existing != null && !existing.IsDeleted)
                throw new InvalidOperationException(
                    $"Movement {movement.MovementNumber} already has a demotion detail record.");
        }

        return movement;
    }

    /// <summary>
    /// How many salary-grade bands the movement crosses — see the note on the promotion service.
    /// Bands are ordered by MinSalary because SalaryGrade carries no explicit rank.
    /// </summary>
    private async Task<int> ComputeGradeBandChangeAsync(StaffMovement movement, CancellationToken cancellationToken)
    {
        if (movement.CurrentSalaryGradeId is not Guid fromGradeId ||
            movement.NewSalaryGradeId is not Guid toGradeId ||
            fromGradeId == toGradeId)
            return 0;

        var tenantId = GetTenantId();
        var grades = await _unitOfWork.Repository<SalaryGrade>()
            .GetQueryable(g => g.TenantId == tenantId && g.IsActive)
            .OrderBy(g => g.MinSalary)
            .Select(g => g.Id)
            .ToListAsync(cancellationToken);

        var fromIndex = grades.IndexOf(fromGradeId);
        var toIndex = grades.IndexOf(toGradeId);
        if (fromIndex < 0 || toIndex < 0)
            return 0;

        return Math.Abs(toIndex - fromIndex);
    }

    /// <summary>
    /// A demotion may cite a disciplinary action or a PIP as its cause. Both are cross-area links —
    /// discipline is area 9, the PIP is area 5 — so both are validated for existence and tenancy
    /// rather than trusted from the body, where a wrong id would silently attribute someone's
    /// demotion to an unrelated case.
    /// </summary>
    private async Task ValidateCausesAsync(Guid? disciplinaryActionId, Guid? pipId)
    {
        var tenantId = GetTenantId();

        if (disciplinaryActionId is Guid actionId)
        {
            var action = await _unitOfWork.Repository<StaffDisciplinaryAction>().GetByIdAsync(actionId);
            if (action == null || action.IsDeleted || action.TenantId != tenantId)
                throw new ArgumentException($"The disciplinary action with ID '{actionId}' was not found.");
        }

        if (pipId is Guid planId)
        {
            var plan = await _unitOfWork.Repository<PerformanceImprovementPlan>().GetByIdAsync(planId);
            if (plan == null || plan.IsDeleted || plan.TenantId != tenantId)
                throw new ArgumentException($"The performance improvement plan with ID '{planId}' was not found.");
        }
    }

    public async Task<StaffDemotionDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(id);
        return entity.ToDto();
    }

    public async Task<StaffDemotionDto?> GetByMovementIdAsync(Guid movementId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _repo.GetByMovementIdAsync(movementId);
        return entity != null && entity.TenantId == tenantId ? entity.ToDto() : null;
    }

    public async Task<IEnumerable<StaffDemotionDto>> GetDisciplinaryDemotionsAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _repo.GetDisciplinaryDemotionsAsync();
        return entities.Where(e => e.TenantId == tenantId).Select(e => e.ToDto()).ToList();
    }

    public async Task<IEnumerable<StaffDemotionDto>> GetPerformanceRelatedDemotionsAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _repo.GetPerformanceRelatedDemotionsAsync();
        return entities.Where(e => e.TenantId == tenantId).Select(e => e.ToDto()).ToList();
    }

    public async Task<IEnumerable<StaffDemotionDto>> GetWithPendingAppealsAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _repo.GetWithPendingAppealsAsync();
        return entities.Where(e => e.TenantId == tenantId).Select(e => e.ToDto()).ToList();
    }

    public async Task<StaffDemotionDto> CreateAsync(CreateStaffDemotionDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        var movement = await GetOwnedParentMovementAsync(
            createDto.MovementId, StaffMovementType.Demotion, requireNoExistingDetail: true);
        await ValidateCausesAsync(createDto.DisciplinaryActionId, createDto.PerformanceImprovementPlanId);

        var entity = createDto.ToEntity(tenantId, createdByUserId);
        entity.GradeLevelDecrease = await ComputeGradeBandChangeAsync(movement, cancellationToken);

        await _repo.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Staff demotion detail created for movement {MovementId}", entity.MovementId);

        return entity.ToDto();
    }

    public async Task<StaffDemotionDto> UpdateAsync(UpdateStaffDemotionDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(updateDto.Id);
        var movement = await GetOwnedParentMovementAsync(
            entity.MovementId, StaffMovementType.Demotion, requireNoExistingDetail: false);

        // The cause links (disciplinary action, PIP) are not on the update DTO — they are set when
        // the demotion detail is raised and are not re-pointed afterwards, so there is nothing to
        // re-validate here.
        entity.UpdateEntity(updateDto, updatedByUserId);
        entity.GradeLevelDecrease = await ComputeGradeBandChangeAsync(movement, cancellationToken);

        await _repo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return entity.ToDto();
    }

    public async Task<bool> RecordEmployeeResponseAsync(Guid demotionId, string response, Guid respondingEmployeeId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(demotionId);

        // The demoted employee's own words. The actor was accepted and discarded before, so anyone
        // could file an appeal — or an acceptance — in someone else's name, on the one record where
        // that testimony decides whether the demotion is contested.
        var tenantId = GetTenantId();
        var subjectEmployeeId = await _repo.GetQueryable()
            .Where(d => d.Id == demotionId && d.TenantId == tenantId)
            .Select(d => (Guid?)d.Movement.EmployeeId)
            .FirstOrDefaultAsync(cancellationToken);

        if (subjectEmployeeId != respondingEmployeeId)
            throw new UnauthorizedAccessException("Only the demoted employee can respond to this demotion notice.");

        if (!string.IsNullOrWhiteSpace(entity.EmployeeResponse))
            throw new InvalidOperationException("A response to this demotion notice has already been recorded.");

        entity.EmployeeResponse     = response;
        entity.EmployeeResponseDate = DateTime.UtcNow;
        entity.EmployeeNotified     = true;
        entity.NotificationDate   ??= DateTime.UtcNow;

        await _repo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Employee response recorded for demotion {DemotionId}", demotionId);

        return true;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(id);

        await _repo.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }
}

#endregion

// ============================================================================
// STAFF SECONDMENT SERVICE
// ============================================================================

#region Staff Secondment Service

public class StaffSecondmentService : IStaffSecondmentService
{
    private readonly IStaffSecondmentRepository _repo;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<StaffSecondmentService> _logger;

    public StaffSecondmentService(
        IStaffSecondmentRepository repo,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ILogger<StaffSecondmentService> logger)
    {
        _repo       = repo;
        _currentUserProvider = currentUserProvider;
        _unitOfWork = unitOfWork;
        _logger     = logger;
    }

    // The ApplicationDbContext is registered without a tenant, so its global tenant query-filter and
    // TenantId auto-stamp are inert. Following the RHEMA convention, this service scopes every read and
    // mutation to the authenticated tenant explicitly and passes it into the repository predicate.
    private Guid GetTenantId()
    {
        var tenantId = _currentUserProvider.TenantId;
        if (tenantId == Guid.Empty)
            throw new UnauthorizedAccessException("No tenant is associated with the current user.");
        return tenantId;
    }

    private Guid RequireCurrentTenant(Guid tenantId)
    {
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");
        return current;
    }

    private async Task<StaffSecondment> GetOwnedAsync(Guid id)
    {
        var entity = await _repo.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Staff secondment with ID '{id}' was not found.");
        return entity;
    }

    /// <summary>
    /// Validates the movement a detail row is being attached to.
    ///
    /// Three things none of the subtype services checked. The parent was never tested for tenancy,
    /// so a detail row could be attached to ANOTHER TENANT'S movement by passing its id — the
    /// cross-tenant child-attach shape. The parent's type was never tested, so a promotion detail
    /// could hang off a demotion and the movement would then describe two different things at once.
    /// And the relationship is one-to-one, so a second detail row for the same movement is not an
    /// update, it is a contradiction.
    /// </summary>
    private async Task<StaffMovement> GetOwnedParentMovementAsync(
        Guid movementId, StaffMovementType expectedType, bool requireNoExistingDetail)
    {
        var tenantId = GetTenantId();

        var movement = await _unitOfWork.Repository<StaffMovement>().GetByIdAsync(movementId);
        if (movement == null || movement.IsDeleted || movement.TenantId != tenantId)
            throw new ArgumentException($"Staff movement with ID '{movementId}' was not found.");

        if (movement.MovementType != expectedType)
            throw new InvalidOperationException(
                $"That movement is a {movement.MovementType}, so it cannot carry a secondment detail record.");

        if (requireNoExistingDetail)
        {
            var existing = await _repo.GetByMovementIdAsync(movementId);
            if (existing != null && !existing.IsDeleted)
                throw new InvalidOperationException(
                    $"Movement {movement.MovementNumber} already has a secondment detail record.");
        }

        return movement;
    }


    public async Task<StaffSecondmentDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(id);
        return entity.ToDto();
    }

    public async Task<StaffSecondmentDto?> GetByMovementIdAsync(Guid movementId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _repo.GetByMovementIdAsync(movementId);
        return entity != null && entity.TenantId == tenantId ? entity.ToDto() : null;
    }

    public async Task<IEnumerable<StaffSecondmentDto>> GetByTypeAsync(StaffSecondmentType type, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _repo.GetByTypeAsync(type);
        return entities.Where(e => e.TenantId == tenantId).Select(e => e.ToDto()).ToList();
    }

    public async Task<IEnumerable<StaffSecondmentDto>> GetExternalSecondmentsAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _repo.GetExternalSecondmentsAsync();
        return entities.Where(e => e.TenantId == tenantId).Select(e => e.ToDto()).ToList();
    }

    public async Task<IEnumerable<StaffSecondmentDto>> GetByHostOrganizationAsync(string hostOrganization, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _repo.GetByHostOrganizationAsync(hostOrganization);
        return entities.Where(e => e.TenantId == tenantId).Select(e => e.ToDto()).ToList();
    }

    public async Task<IEnumerable<StaffSecondmentDto>> GetEndingSoonAsync(int daysAhead = 30, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _repo.GetEndingSoonAsync(daysAhead);
        return entities.Where(e => e.TenantId == tenantId).Select(e => e.ToDto()).ToList();
    }

    public async Task<StaffSecondmentDto> CreateAsync(CreateStaffSecondmentDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        await GetOwnedParentMovementAsync(
            createDto.MovementId, StaffMovementType.Secondment, requireNoExistingDetail: true);

        if (createDto.EndDate.Date <= createDto.StartDate.Date)
            throw new InvalidOperationException("A secondment must end after it starts.");

        var entity = createDto.ToEntity(tenantId, createdByUserId);

        await _repo.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Staff secondment detail created for movement {MovementId}", entity.MovementId);

        return entity.ToDto();
    }

    public async Task<StaffSecondmentDto> UpdateAsync(UpdateStaffSecondmentDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(updateDto.Id);

        entity.UpdateEntity(updateDto, updatedByUserId);

        await _repo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return entity.ToDto();
    }

    public async Task<StaffSecondmentDto> ExtendAsync(ExtendStaffSecondmentDto dto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(dto.SecondmentId);

        if (!entity.ExtensionAllowed)
            throw new InvalidOperationException("Extension is not permitted for this secondment.");

        if (entity.MaxExtensionMonths.HasValue && dto.ExtensionMonths > entity.MaxExtensionMonths.Value)
            throw new InvalidOperationException($"Extension cannot exceed {entity.MaxExtensionMonths} months for this secondment.");

        if (dto.NewEndDate.Date <= entity.EndDate.Date)
            throw new InvalidOperationException("The new end date must be after the current one.");

        // The cap above is checked against ExtensionMonths, but the date being WRITTEN is
        // NewEndDate, and nothing tied the two together: a request could pass one month — clearing
        // a one-month cap — and a new end date five years out. The two now have to agree.
        var impliedEnd = entity.EndDate.AddMonths(dto.ExtensionMonths);
        if (dto.NewEndDate.Date > impliedEnd.Date)
            throw new InvalidOperationException(
                $"An extension of {dto.ExtensionMonths} month(s) ends on {impliedEnd:yyyy-MM-dd}, " +
                $"not {dto.NewEndDate:yyyy-MM-dd}.");

        entity.EndDate = dto.NewEndDate;

        await _repo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Secondment {SecondmentId} extended to {NewEndDate}", dto.SecondmentId, dto.NewEndDate);

        return entity.ToDto();
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(id);

        await _repo.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }
}

#endregion
