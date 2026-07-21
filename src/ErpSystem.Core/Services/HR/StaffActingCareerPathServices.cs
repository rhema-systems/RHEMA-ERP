using ErpSystem.Application.HR.Extensions;
using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
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
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<StaffActingAppointmentService> _logger;

    public StaffActingAppointmentService(
        IStaffActingAppointmentRepository repo,
        IStaffMovementRepository movementRepo,
        IUnitOfWork unitOfWork,
        ILogger<StaffActingAppointmentService> logger)
    {
        _repo         = repo;
        _movementRepo = movementRepo;
        _unitOfWork   = unitOfWork;
        _logger       = logger;
    }

    // ── Queries ───────────────────────────────────────────────────────────────

    public async Task<StaffActingAppointmentDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _repo.GetByIdAsync(id);

        if (entity == null)
            throw new ArgumentException($"Acting appointment with ID '{id}' was not found.");

        return entity.ToDto();
    }

    public async Task<StaffActingAppointmentDto?> GetByAppointmentNumberAsync(string appointmentNumber, CancellationToken cancellationToken = default)
    {
        var entity = await _repo.GetByAppointmentNumberAsync(appointmentNumber);
        return entity?.ToDto();
    }

    public async Task<StaffActingAppointmentDto> GetWithDetailsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _repo.GetWithDetailsAsync(id);

        if (entity == null)
            throw new ArgumentException($"Acting appointment with ID '{id}' was not found.");

        return entity.ToDto();
    }

    public async Task<IEnumerable<StaffActingAppointmentSummaryDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _repo.GetAllAsync();
        return entities.ToSummaryDtoList();
    }

    public async Task<PagedResult<StaffActingAppointmentSummaryDto>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = _repo.GetQueryable().Where(a => !a.IsDeleted);

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
        var entities = await _repo.GetByEmployeeIdAsync(employeeId);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<StaffActingAppointmentSummaryDto>> GetByStatusAsync(StaffActingStatus status, CancellationToken cancellationToken = default)
    {
        var entities = await _repo.GetByStatusAsync(status);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<StaffActingAppointmentSummaryDto>> GetActiveAppointmentsAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _repo.GetActiveAppointmentsAsync();
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<StaffActingAppointmentSummaryDto>> GetByActingPositionAsync(Guid positionId, CancellationToken cancellationToken = default)
    {
        var entities = await _repo.GetByActingPositionAsync(positionId);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<StaffActingAppointmentSummaryDto>> GetExpiringAppointmentsAsync(int daysAhead = 14, CancellationToken cancellationToken = default)
    {
        var entities = await _repo.GetExpiringAppointmentsAsync(daysAhead);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<StaffActingAppointmentSummaryDto>> GetConvertedToPermanentAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _repo.GetConvertedToPermanentAsync();
        return entities.ToSummaryDtoList();
    }

    // ── CRUD & Workflow ───────────────────────────────────────────────────────

    public async Task<StaffActingAppointmentDto> CreateAsync(CreateStaffActingAppointmentDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var entity = createDto.ToEntity(tenantId, createdByUserId);
        entity.AppointmentNumber = await GenerateAppointmentNumberAsync(cancellationToken);
        entity.Status            = StaffActingStatus.Active;

        await _repo.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Acting appointment created: {AppointmentNumber}", entity.AppointmentNumber);

        return entity.ToDto();
    }

    public async Task<StaffActingAppointmentDto> UpdateAsync(UpdateStaffActingAppointmentDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await _repo.GetByIdAsync(updateDto.Id);

        if (entity == null)
            throw new ArgumentException($"Acting appointment with ID '{updateDto.Id}' was not found.");

        if (entity.Status == StaffActingStatus.Completed)
            throw new InvalidOperationException("A completed acting appointment cannot be edited.");

        entity.UpdateEntity(updateDto, updatedByUserId);

        await _repo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return entity.ToDto();
    }

    public async Task<bool> CompleteAsync(CompleteStaffActingAppointmentDto dto, Guid completedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await _repo.GetByIdAsync(dto.AppointmentId);

        if (entity == null)
            throw new ArgumentException($"Acting appointment with ID '{dto.AppointmentId}' was not found.");

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
        var entity = await _repo.GetByIdAsync(dto.AppointmentId);

        if (entity == null)
            throw new ArgumentException($"Acting appointment with ID '{dto.AppointmentId}' was not found.");

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
        var entity = await _repo.GetByIdAsync(dto.AppointmentId);

        if (entity == null)
            throw new ArgumentException($"Acting appointment with ID '{dto.AppointmentId}' was not found.");

        if (entity.ConvertedToPermanent)
            throw new InvalidOperationException("This acting appointment has already been converted to a permanent appointment.");

        if (dto.ConversionMovementId == Guid.Empty)
            throw new InvalidOperationException("A promotion movement is required before converting to permanent.");

        var promotionMovement = await _movementRepo.GetByIdAsync(dto.ConversionMovementId);
        if (promotionMovement is null || promotionMovement.IsDeleted)
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
        var entity = await _repo.GetByIdAsync(id);

        if (entity == null)
            throw new ArgumentException($"Acting appointment with ID '{id}' was not found.");

        if (entity.Status == StaffActingStatus.Active)
            throw new InvalidOperationException("An active acting appointment cannot be deleted. Complete or cancel it first.");

        await _repo.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private async Task<string> GenerateAppointmentNumberAsync(CancellationToken cancellationToken = default)
    {
        var prefix     = $"ACT-{DateTime.UtcNow:yyyyMMdd}";
        var countToday = await _repo.GetQueryable()
            .Where(a => a.AppointmentNumber.StartsWith(prefix))
            .CountAsync(cancellationToken);

        return $"{prefix}-{(countToday + 1):D4}";
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
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<EmployeeCareerPathService> _logger;

    public EmployeeCareerPathService(
        IEmployeeCareerPathRepository repo,
        IUnitOfWork unitOfWork,
        ILogger<EmployeeCareerPathService> logger)
    {
        _repo       = repo;
        _unitOfWork = unitOfWork;
        _logger     = logger;
    }

    public async Task<EmployeeCareerPathDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _repo.GetByIdAsync(id);

        if (entity == null)
            throw new ArgumentException($"Career path record with ID '{id}' was not found.");

        return entity.ToDto();
    }

    public async Task<EmployeeCareerPathDto> GetWithDetailsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _repo.GetWithDetailsAsync(id);

        if (entity == null)
            throw new ArgumentException($"Career path record with ID '{id}' was not found.");

        return entity.ToDto();
    }

    public async Task<IEnumerable<EmployeeCareerPathSummaryDto>> GetByEmployeeIdAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        var entities = await _repo.GetByEmployeeIdAsync(employeeId);
        return entities.ToSummaryDtoList();
    }

    public async Task<EmployeeCareerPathDto?> GetCurrentPositionAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        var entity = await _repo.GetCurrentPositionAsync(employeeId);
        return entity?.ToDto();
    }

    public async Task<IEnumerable<EmployeeCareerPathSummaryDto>> GetByOrganizationUnitIdAsync(Guid organizationUnitId, CancellationToken cancellationToken = default)
    {
        var entities = await _repo.GetByOrganizationUnitIdAsync(organizationUnitId);
        return entities.ToSummaryDtoList();
    }

    public async Task<EmployeeCareerPathDto?> GetByMovementIdAsync(Guid movementId, CancellationToken cancellationToken = default)
    {
        var entity = await _repo.GetByMovementIdAsync(movementId);
        return entity?.ToDto();
    }

    public async Task<IEnumerable<EmployeeCareerPathSummaryDto>> GetBySalaryGradeIdAsync(Guid salaryGradeId, CancellationToken cancellationToken = default)
    {
        var entities = await _repo.GetBySalaryGradeIdAsync(salaryGradeId);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<EmployeeCareerPathSummaryDto>> GetCurrentOccupantsForUnitAsync(Guid organizationUnitId, CancellationToken cancellationToken = default)
    {
        var entities = await _repo.GetCurrentOccupantsForUnitAsync(organizationUnitId);
        return entities.ToSummaryDtoList();
    }

    public async Task<EmployeeCareerPathDto> CreateAsync(CreateEmployeeCareerPathDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
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
        var entity = await _repo.GetByIdAsync(updateDto.Id);

        if (entity == null)
            throw new ArgumentException($"Career path record with ID '{updateDto.Id}' was not found.");

        entity.UpdateEntity(updateDto, updatedByUserId);

        await _repo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return entity.ToDto();
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _repo.GetByIdAsync(id);

        if (entity == null)
            throw new ArgumentException($"Career path record with ID '{id}' was not found.");

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
        var currentPosition = await _repo.GetCurrentPositionAsync(employeeId);

        if (currentPosition == null) return;

        currentPosition.IsCurrent = false;
        currentPosition.EndDate   = newStartDate.AddDays(-1);

        await _repo.UpdateAsync(currentPosition);
    }
}

#endregion
