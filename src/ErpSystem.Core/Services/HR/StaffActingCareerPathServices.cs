using ErpSystem.Application.HR.Extensions;
using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.HR.PromotionTransfer;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

// ============================================================================
// STAFF ACTING APPOINTMENT SERVICE
// ============================================================================

#region Staff Acting Appointment Service

public class StaffActingAppointmentService : IStaffActingAppointmentService
{
    private readonly IStaffActingAppointmentRepository _repo;
    private readonly IStaffMovementRepository _movementRepo;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<StaffActingAppointmentService> _logger;

    public StaffActingAppointmentService(
        IStaffActingAppointmentRepository repo,
        IStaffMovementRepository movementRepo,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ILogger<StaffActingAppointmentService> logger)
    {
        _repo         = repo;
        _movementRepo = movementRepo;
        _currentUserProvider = currentUserProvider;
        _unitOfWork   = unitOfWork;
        _logger       = logger;
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

    private async Task<StaffActingAppointment> GetOwnedAsync(Guid id)
    {
        var entity = await _repo.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Acting appointment with ID '{id}' was not found.");
        return entity;
    }

    // ── Queries ───────────────────────────────────────────────────────────────

    public async Task<StaffActingAppointmentDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(id);
        return entity.ToDto();
    }

    public async Task<StaffActingAppointmentDto?> GetByAppointmentNumberAsync(string appointmentNumber, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _repo.GetByAppointmentNumberAsync(appointmentNumber);
        return entity != null && entity.TenantId == tenantId ? entity.ToDto() : null;
    }

    public async Task<StaffActingAppointmentDto> GetWithDetailsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _repo.GetWithDetailsAsync(id);

        if (entity == null || entity.TenantId != tenantId)
            throw new ArgumentException($"Acting appointment with ID '{id}' was not found.");

        return entity.ToDto();
    }

    public async Task<IEnumerable<StaffActingAppointmentSummaryDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _repo.GetAllAsync();
        return entities.Where(e => e.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<PagedResult<StaffActingAppointmentSummaryDto>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var query = _repo.GetQueryable().Where(a => a.TenantId == tenantId && !a.IsDeleted);

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(a => a.StartDate)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<StaffActingAppointmentSummaryDto>
        {
            Items      = items.ToSummaryDtoList(),
            TotalCount = totalCount,
            Page       = pageNumber,
            PageSize   = pageSize
        };
    }

    public async Task<IEnumerable<StaffActingAppointmentSummaryDto>> GetByEmployeeIdAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _repo.GetByEmployeeIdAsync(employeeId);
        return entities.Where(e => e.TenantId == tenantId).ToSummaryDtoList();
    }

    /// <summary>
    /// The full appointment rows for one employee — the self-service read (area 25 slice 7).
    ///
    /// The summary above deliberately omits the allowance and the covering-for name (it backs
    /// org-wide register rows); the portal shows the SUBJECT their own appointment, and whether
    /// they are paid for acting is exactly what the subject opens the page to see. The repo's
    /// by-employee read already includes ActingPosition and ActingForEmployee, so the full
    /// mapping resolves without a second query.
    /// </summary>
    public async Task<IEnumerable<StaffActingAppointmentDto>> GetDetailedByEmployeeIdAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _repo.GetByEmployeeIdAsync(employeeId);
        return entities.Where(e => e.TenantId == tenantId).Select(e => e.ToDto()).ToList();
    }

    public async Task<IEnumerable<StaffActingAppointmentSummaryDto>> GetByStatusAsync(StaffActingStatus status, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _repo.GetByStatusAsync(status);
        return entities.Where(e => e.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<IEnumerable<StaffActingAppointmentSummaryDto>> GetActiveAppointmentsAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _repo.GetActiveAppointmentsAsync();
        return entities.Where(e => e.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<IEnumerable<StaffActingAppointmentSummaryDto>> GetByActingPositionAsync(Guid positionId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _repo.GetByActingPositionAsync(positionId);
        return entities.Where(e => e.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<IEnumerable<StaffActingAppointmentSummaryDto>> GetExpiringAppointmentsAsync(int daysAhead = 14, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _repo.GetExpiringAppointmentsAsync(daysAhead);
        return entities.Where(e => e.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<IEnumerable<StaffActingAppointmentSummaryDto>> GetConvertedToPermanentAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _repo.GetConvertedToPermanentAsync();
        return entities.Where(e => e.TenantId == tenantId).ToSummaryDtoList();
    }

    // ── CRUD & Workflow ───────────────────────────────────────────────────────

    /// <summary>
    /// Validates a body-supplied employee id and returns the row.
    ///
    /// The guard stops an unknown or another tenant's id reaching the database as an FK violation —
    /// which surfaced as an unexplained 500 rather than "that employee was not found" — and because
    /// the row ends up tracked, the write response resolves the name instead of an empty string.
    /// </summary>
    private async Task<Employee> GetOwnedEmployeeAsync(Guid employeeId, string role)
    {
        var employee = await _unitOfWork.Repository<Employee>().GetByIdAsync(employeeId);
        if (employee == null || employee.IsDeleted || employee.TenantId != GetTenantId())
            throw new ArgumentException($"The {role} employee with ID '{employeeId}' was not found.");
        return employee;
    }

    public async Task<StaffActingAppointmentDto> CreateAsync(CreateStaffActingAppointmentDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);

        await GetOwnedEmployeeAsync(createDto.EmployeeId, "acting");
        if (createDto.ActingForEmployeeId is Guid actingForId)
            await GetOwnedEmployeeAsync(actingForId, "acted-for");

        var position = await _unitOfWork.Repository<EmployeePosition>().GetByIdAsync(createDto.ActingPositionId);
        if (position == null || position.IsDeleted || position.TenantId != tenantId)
            throw new ArgumentException($"The acting position with ID '{createDto.ActingPositionId}' was not found.");

        if (createDto.EndDate is DateTime end && end.Date <= createDto.StartDate.Date)
            throw new InvalidOperationException("An acting appointment must end after it starts.");

        // One person cannot be acting in two posts at once. Nothing checked this, so the same
        // employee could hold overlapping appointments — and each would independently qualify them
        // for an acting allowance.
        var overlapping = await _repo
            .GetQueryable(a => a.TenantId == tenantId
                            && a.EmployeeId == createDto.EmployeeId
                            && (a.Status == StaffActingStatus.Active || a.Status == StaffActingStatus.Extended))
            .ToListAsync(cancellationToken);

        var clash = overlapping.FirstOrDefault(a =>
            (a.EndDate ?? DateTime.MaxValue).Date >= createDto.StartDate.Date &&
            a.StartDate.Date <= (createDto.EndDate ?? DateTime.MaxValue).Date);

        if (clash != null)
            throw new InvalidOperationException(
                $"That employee is already acting under {clash.AppointmentNumber} over the same period.");

        var entity = createDto.ToEntity(tenantId, createdByUserId);
        entity.AppointmentNumber = await GenerateAppointmentNumberAsync(cancellationToken);
        entity.Status            = StaffActingStatus.Active;

        await _repo.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Acting appointment created: {AppointmentNumber}", entity.AppointmentNumber);

        return (await _repo.GetWithDetailsAsync(entity.Id) ?? entity).ToDto();
    }

    public async Task<StaffActingAppointmentDto> UpdateAsync(UpdateStaffActingAppointmentDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(updateDto.Id);

        if (entity.Status == StaffActingStatus.Completed)
            throw new InvalidOperationException("A completed acting appointment cannot be edited.");

        // ⚠ Both of the checks below exist on CreateAsync and existed nowhere else, so the edit
        // was the way round them — the recurring "the guard is on the sibling, not on this one"
        // shape (cf. D-23, where an approved travel policy could not be edited but could be
        // deleted). Proven by hr-movements/probe-lane3-acting.mjs, which moved one appointment's
        // end date past the start of the same employee's next one.
        //
        // The update accepts no StartDate, so the STORED start is the only thing to check against.
        if (updateDto.EndDate is DateTime newEnd && newEnd.Date <= entity.StartDate.Date)
            throw new InvalidOperationException("An acting appointment must end after it starts.");

        // One person cannot be acting in two posts at once — each would independently qualify them
        // for an acting allowance. CreateAsync says exactly this; moving an end date reaches the
        // same state, so the same rule applies. Self is excluded, or every edit clashes with itself.
        var overlapping = await _repo
            .GetQueryable(a => a.TenantId == entity.TenantId
                            && a.EmployeeId == entity.EmployeeId
                            && a.Id != entity.Id
                            && (a.Status == StaffActingStatus.Active || a.Status == StaffActingStatus.Extended))
            .ToListAsync(cancellationToken);

        var clash = overlapping.FirstOrDefault(a =>
            (a.EndDate ?? DateTime.MaxValue).Date >= entity.StartDate.Date &&
            a.StartDate.Date <= (updateDto.EndDate ?? DateTime.MaxValue).Date);

        if (clash != null)
            throw new InvalidOperationException(
                $"That employee is already acting under {clash.AppointmentNumber} over the same period.");

        entity.UpdateEntity(updateDto, updatedByUserId);

        await _repo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // EmployeeName, EmployeeNumber, ActingPositionTitle, ActingForEmployeeName and
        // MovementNumber are all resolved off navigations GetOwnedAsync does not load, so the edit
        // response named nobody and no position. GetWithDetailsAsync exists for exactly this.
        return (await _repo.GetWithDetailsAsync(entity.Id) ?? entity).ToDto();
    }

    public async Task<bool> CompleteAsync(CompleteStaffActingAppointmentDto dto, Guid completedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(dto.AppointmentId);

        if (entity.Status == StaffActingStatus.Completed)
            throw new InvalidOperationException("This acting appointment is already completed.");

        entity.Status         = StaffActingStatus.Completed;
        entity.CompletionDate = DateTime.UtcNow;
        entity.Notes          = dto.Notes ?? entity.Notes;

        await _repo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Acting appointment completed: {AppointmentNumber}", entity.AppointmentNumber);

        return true;
    }

    public async Task<StaffActingAppointmentDto> ExtendAsync(
        ExtendStaffActingAppointmentDto dto,
        Guid updatedByUserId,
        CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(dto.AppointmentId);

        if (entity.Status is not (StaffActingStatus.Active or StaffActingStatus.Extended))
            throw new InvalidOperationException("Only active or extended acting appointments can be extended.");

        if (!entity.EndDate.HasValue)
            throw new InvalidOperationException("The appointment has no end date to extend.");

        if (dto.NewEndDate.Date <= entity.EndDate.Value.Date)
            throw new InvalidOperationException("The new end date must be after the current end date.");

        entity.EndDate = dto.NewEndDate.Date;
        entity.Status  = StaffActingStatus.Extended;
        if (!string.IsNullOrWhiteSpace(dto.Notes))
            entity.Notes = dto.Notes;

        await _repo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Acting appointment {AppointmentNumber} extended to {NewEndDate:yyyy-MM-dd}",
            entity.AppointmentNumber,
            dto.NewEndDate);

        return entity.ToDto();
    }

    public async Task<bool> ConvertToPermanentAsync(ConvertActingToPermanentDto dto, Guid convertedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(dto.AppointmentId);

        if (entity.ConvertedToPermanent)
            throw new InvalidOperationException("This acting appointment has already been converted to a permanent appointment.");

        if (dto.ConversionMovementId == Guid.Empty)
            throw new InvalidOperationException("A promotion movement is required before converting to permanent.");

        var tenantId = GetTenantId();
        var promotionMovement = await _movementRepo.GetByIdAsync(dto.ConversionMovementId);
        if (promotionMovement is null || promotionMovement.IsDeleted || promotionMovement.TenantId != tenantId)
            throw new InvalidOperationException("The selected promotion movement was not found.");

        if (promotionMovement.MovementType != StaffMovementType.Promotion)
            throw new InvalidOperationException("Conversion must reference a promotion movement.");

        if (promotionMovement.EmployeeId != entity.EmployeeId)
            throw new InvalidOperationException("The promotion movement must belong to the same employee as the acting appointment.");

        entity.ConvertedToPermanent   = true;
        entity.ConversionDate         = DateTime.UtcNow;
        entity.ConversionMovementId   = dto.ConversionMovementId;

        entity.Status         = StaffActingStatus.ConvertedToPermanent;
        entity.CompletionDate ??= DateTime.UtcNow;

        await _repo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Acting appointment {AppointmentNumber} converted to permanent via movement {MovementId}",
            entity.AppointmentNumber, dto.ConversionMovementId);

        return true;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(id);

        if (entity.Status == StaffActingStatus.Active)
            throw new InvalidOperationException("An active acting appointment cannot be deleted. Complete or cancel it first.");

        await _repo.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    /// <remarks>
    /// Highest issued suffix, over rows INCLUDING soft-deleted ones — see the note on
    /// <c>StaffMovementService.GenerateMovementNumberAsync</c>. Counting live rows re-issues a number
    /// as soon as one is deleted, and the unique index then rejects the next appointment.
    /// </remarks>
    private async Task<string> GenerateAppointmentNumberAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var prefix   = $"ACT-{DateTime.UtcNow:yyyyMMdd}-";

        var issued = await _repo
            .GetQueryableIncludingDeleted(a => a.TenantId == tenantId && a.AppointmentNumber.StartsWith(prefix))
            .Select(a => a.AppointmentNumber)
            .ToListAsync(cancellationToken);

        var highest = issued
            .Select(number => int.TryParse(number[prefix.Length..], out var value) ? value : 0)
            .DefaultIfEmpty(0)
            .Max();

        return $"{prefix}{(highest + 1):D4}";
    }
}

#endregion

// ============================================================================
// EMPLOYEE CAREER PATH SERVICE
// ============================================================================

#region Employee Career Path Service

public class EmployeeCareerPathService : IEmployeeCareerPathService
{
    private readonly IEmployeeCareerPathRepository _repo;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<EmployeeCareerPathService> _logger;

    public EmployeeCareerPathService(
        IEmployeeCareerPathRepository repo,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ILogger<EmployeeCareerPathService> logger)
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

    private async Task<EmployeeCareerPath> GetOwnedAsync(Guid id)
    {
        var entity = await _repo.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Career path record with ID '{id}' was not found.");
        return entity;
    }

    public async Task<EmployeeCareerPathDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(id);
        return entity.ToDto();
    }

    public async Task<EmployeeCareerPathDto> GetWithDetailsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _repo.GetWithDetailsAsync(id);

        if (entity == null || entity.TenantId != tenantId)
            throw new ArgumentException($"Career path record with ID '{id}' was not found.");

        return entity.ToDto();
    }

    public async Task<IEnumerable<EmployeeCareerPathSummaryDto>> GetByEmployeeIdAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _repo.GetByEmployeeIdAsync(employeeId);
        return entities.Where(e => e.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<EmployeeCareerPathDto?> GetCurrentPositionAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _repo.GetCurrentPositionAsync(employeeId);
        return entity != null && entity.TenantId == tenantId ? entity.ToDto() : null;
    }

    public async Task<IEnumerable<EmployeeCareerPathSummaryDto>> GetByOrganizationUnitIdAsync(Guid organizationUnitId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _repo.GetByOrganizationUnitIdAsync(organizationUnitId);
        return entities.Where(e => e.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<EmployeeCareerPathDto?> GetByMovementIdAsync(Guid movementId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _repo.GetByMovementIdAsync(movementId);
        return entity != null && entity.TenantId == tenantId ? entity.ToDto() : null;
    }

    public async Task<IEnumerable<EmployeeCareerPathSummaryDto>> GetBySalaryGradeIdAsync(Guid salaryGradeId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _repo.GetBySalaryGradeIdAsync(salaryGradeId);
        return entities.Where(e => e.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<IEnumerable<EmployeeCareerPathSummaryDto>> GetCurrentOccupantsForUnitAsync(Guid organizationUnitId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _repo.GetCurrentOccupantsForUnitAsync(organizationUnitId);
        return entities.Where(e => e.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<EmployeeCareerPathDto> CreateAsync(CreateEmployeeCareerPathDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);

        if (createDto.IsCurrent)
            await ClosePreviousCurrentPositionAsync(createDto.EmployeeId, createDto.StartDate, createdByUserId, cancellationToken);

        var entity = createDto.ToEntity(tenantId, createdByUserId);

        await _repo.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Career path record created for employee {EmployeeId}", createDto.EmployeeId);

        return entity.ToDto();
    }

    public async Task<EmployeeCareerPathDto> UpdateAsync(UpdateEmployeeCareerPathDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(updateDto.Id);

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

    // ── Private helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// When creating a new current position, closes any previously open current record
    /// for the employee by setting its end date to the day before the new record's start date.
    /// </summary>
    private async Task ClosePreviousCurrentPositionAsync(Guid employeeId, DateTime newStartDate, Guid updatedByUserId, CancellationToken cancellationToken)
    {
        var tenantId = GetTenantId();
        var currentPosition = await _repo.GetCurrentPositionAsync(employeeId);

        if (currentPosition == null || currentPosition.TenantId != tenantId) return;

        currentPosition.IsCurrent = false;
        currentPosition.EndDate   = newStartDate.AddDays(-1);

        await _repo.UpdateAsync(currentPosition);
    }
}

#endregion
