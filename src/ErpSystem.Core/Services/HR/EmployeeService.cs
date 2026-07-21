using ErpSystem.Core.Services.HR.Extensions;
using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

/// <summary>
/// Application service for the Employee aggregate. Orchestrates repositories, enforces rules, and manages transactions.
/// </summary>
public class EmployeeService : IEmployeeService
{
    private readonly IEmployeeRepository _employeeRepository;
    private readonly IOrganizationUnitRepository _organizationUnitRepository;
    private readonly IEmployeePositionRepository _positionRepository;
    private readonly ILocationRepository _locationRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<EmployeeService> _logger;

    public EmployeeService(
        IEmployeeRepository employeeRepository,
        IOrganizationUnitRepository organizationUnitRepository,
        IEmployeePositionRepository positionRepository,
        ILocationRepository locationRepository,
        IUnitOfWork unitOfWork,
        ILogger<EmployeeService> logger)
    {
        _employeeRepository = employeeRepository;
        _organizationUnitRepository = organizationUnitRepository;
        _positionRepository = positionRepository;
        _locationRepository = locationRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    #region 1) Core Employee Lifecycle

    public async Task<EmployeeDetailDto> CreateEmployeeAsync(CreateEmployeeDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var employeeNumber = string.IsNullOrWhiteSpace(dto.EmployeeNumber)
            ? await _employeeRepository.GenerateEmployeeNumberAsync()
            : dto.EmployeeNumber.Trim();

        var email = NormalizeEmail(dto.EmailAddress);

        if (!await IsEmployeeNumberUniqueAsync(employeeNumber, null, cancellationToken))
            throw new InvalidOperationException($"Employee number '{employeeNumber}' already exists.");

        if (!await IsEmailUniqueAsync(email, null, cancellationToken))
            throw new InvalidOperationException($"Email address '{email}' already exists.");

        if (!string.IsNullOrWhiteSpace(dto.BadgeNumber) &&
            !await IsBadgeNumberUniqueAsync(dto.BadgeNumber.Trim(), null, cancellationToken))
            throw new InvalidOperationException($"Badge number '{dto.BadgeNumber}' already exists.");

        if (!string.IsNullOrWhiteSpace(dto.TaxNumber) &&
            !await IsTaxNumberUniqueAsync(dto.TaxNumber.Trim(), null, cancellationToken))
            throw new InvalidOperationException($"Tax number '{dto.TaxNumber}' already exists.");

        if (!string.IsNullOrWhiteSpace(dto.SocialSecurityNumber) &&
            !await IsSocialSecurityNumberUniqueAsync(dto.SocialSecurityNumber.Trim(), null, cancellationToken))
            throw new InvalidOperationException("Social security number already exists.");

        if (!string.IsNullOrWhiteSpace(dto.TINNumber) &&
            !await IsTinNumberUniqueAsync(dto.TINNumber.Trim(), null, cancellationToken))
            throw new InvalidOperationException("TIN number already exists.");

        // Validate deprecated Department field only if a real value is provided (backward compatibility)
        if (dto.DepartmentId != Guid.Empty)
        {
            var departmentRepo = _unitOfWork.Repository<Department>();
            var department = await departmentRepo.GetByIdAsync(dto.DepartmentId);
            if (department == null)
                throw new ArgumentException("Department not found.");
            if (!department.IsActive)
                throw new ArgumentException("Department is not active.");
        }

        // Validate deprecated Section field only if a real value is provided (backward compatibility)
        if (dto.SectionId.HasValue && dto.SectionId.Value != Guid.Empty)
        {
            var sectionRepo = _unitOfWork.Repository<Section>();
            var section = await sectionRepo.GetByIdAsync(dto.SectionId.Value);
            if (section == null)
                throw new ArgumentException("Section not found.");
            if (!section.IsActive)
                throw new ArgumentException("Section is not active.");
            
            // Validate section belongs to department if both provided
            if (dto.DepartmentId != Guid.Empty && section.DepartmentId != dto.DepartmentId)
                throw new InvalidOperationException("Selected section does not belong to the specified department.");
        }

        if (dto.OrganizationUnitId == Guid.Empty)
            throw new ArgumentException("OrganizationUnitId is required.");

        var orgUnit = await _organizationUnitRepository.GetWithDetailsAsync(dto.OrganizationUnitId);
        if (orgUnit == null || !orgUnit.IsActive)
            throw new ArgumentException("Organization unit not found or inactive.");

        var position = await _positionRepository.GetByIdAsync(dto.PositionId);
        if (position == null)
            throw new ArgumentException("Position not found.");

        if (position.OrganizationUnitId != dto.OrganizationUnitId)
            throw new InvalidOperationException("Selected position does not belong to the specified organization unit.");

        if (!dto.LocationId.HasValue || dto.LocationId.Value == Guid.Empty)
            throw new ArgumentException("LocationId is required for employee assignment.");

        var location = await _locationRepository.GetWithDetailsAsync(dto.LocationId.Value);
        if (location == null || !location.IsActive)
            throw new ArgumentException("Location not found or inactive.");

        await ValidatePayrollFlagsAsync(dto.PayTax, dto.SSFund, dto.GrossUp, dto.Tier2Only, dto.Overtime, cancellationToken);

        if (dto.ManagerId.HasValue)
        {
            await ValidateManagerAssignmentAsync(Guid.Empty, dto.ManagerId.Value, cancellationToken);
        }

        var employeeEntity = dto.ToEntity(employeeNumber, orgUnit.OrganizationLevelId, location.LocationLevelId);
        employeeEntity.EmailAddress = email;

        await _employeeRepository.AddAsync(employeeEntity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await EnsureInitialPositionHistoryExistsAsync(employeeEntity, cancellationToken);

        _logger.LogInformation("Employee created: {EmployeeNumber} ({EmployeeId})", employeeEntity.EmployeeNumber, employeeEntity.Id);

        var created = await _employeeRepository.GetByIdWithDetailsAsync(employeeEntity.Id);
        if (created == null) throw new InvalidOperationException("Employee created but could not be reloaded.");
        return created.ToDetailDto();
    }

    private async Task EnsureInitialPositionHistoryExistsAsync(Employee employee, CancellationToken cancellationToken)
    {
        var repo = _unitOfWork.Repository<EmployeePositionHistory>();

        var anyExisting = await repo.GetQueryable()
            .AnyAsync(x => x.EmployeeId == employee.Id, cancellationToken);

        if (anyExisting)
        {
            return;
        }

        var startDate = employee.DateEmployed.HasValue
            ? employee.DateEmployed.Value.ToDateTime(TimeOnly.MinValue)
            : DateTime.UtcNow;

        var initial = new EmployeePositionHistory
        {
            EmployeeId = employee.Id,
            LocationLevelId = employee.LocationLevelId ?? Guid.Empty,
            LocationId = employee.LocationId,
            OrganizationLevelId = employee.OrganizationLevelId ?? Guid.Empty,
            OrganizationUnitId = employee.OrganizationUnitId,
            PositionId = employee.PositionId,
            StartDate = startDate,
            EndDate = null,
            ChangeReason = PositionChangeReason.InitialAssignment,
            Notes = "Initial position assignment"
        };

        await repo.AddAsync(initial);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<EmployeeDetailDto> UpdateEmployeeAsync(Guid employeeId, UpdateEmployeeDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var employee = await _employeeRepository.GetByIdAsync(employeeId);
        if (employee == null) throw new ArgumentException($"Employee with ID '{employeeId}' not found.");

        if (!string.IsNullOrWhiteSpace(dto.EmployeeNumber) &&
            !await IsEmployeeNumberUniqueAsync(dto.EmployeeNumber.Trim(), employeeId, cancellationToken))
            throw new InvalidOperationException($"Employee number '{dto.EmployeeNumber}' already exists.");

        if (!string.IsNullOrWhiteSpace(dto.EmailAddress) &&
            !await IsEmailUniqueAsync(dto.EmailAddress, employeeId, cancellationToken))
            throw new InvalidOperationException($"Email address '{dto.EmailAddress}' already exists.");

        if (!string.IsNullOrWhiteSpace(dto.BadgeNumber) &&
            !await IsBadgeNumberUniqueAsync(dto.BadgeNumber.Trim(), employeeId, cancellationToken))
            throw new InvalidOperationException($"Badge number '{dto.BadgeNumber}' already exists.");

        if (!string.IsNullOrWhiteSpace(dto.TaxNumber) &&
            !await IsTaxNumberUniqueAsync(dto.TaxNumber.Trim(), employeeId, cancellationToken))
            throw new InvalidOperationException($"Tax number '{dto.TaxNumber}' already exists.");

        if (!string.IsNullOrWhiteSpace(dto.SocialSecurityNumber) &&
            !await IsSocialSecurityNumberUniqueAsync(dto.SocialSecurityNumber.Trim(), employeeId, cancellationToken))
            throw new InvalidOperationException("Social security number already exists.");

        if (!string.IsNullOrWhiteSpace(dto.TINNumber) &&
            !await IsTinNumberUniqueAsync(dto.TINNumber.Trim(), employeeId, cancellationToken))
            throw new InvalidOperationException("TIN number already exists.");

        // Validate org unit / position changes
        Guid? newOrgLevelId = null;
        if (dto.OrganizationUnitId.HasValue)
        {
            var orgUnit = await _organizationUnitRepository.GetWithDetailsAsync(dto.OrganizationUnitId.Value);
            if (orgUnit == null || !orgUnit.IsActive)
                throw new ArgumentException("Organization unit not found or inactive.");

            newOrgLevelId = orgUnit.OrganizationLevelId;

            if (dto.PositionId.HasValue)
            {
                var position = await _positionRepository.GetByIdAsync(dto.PositionId.Value);
                if (position == null)
                    throw new ArgumentException("Position not found.");
                if (position.OrganizationUnitId != orgUnit.Id)
                    throw new InvalidOperationException("Selected position does not belong to the specified organization unit.");
            }
            else
            {
                // If org unit changed but position didn't, ensure current position still belongs.
                var currentPosition = await _positionRepository.GetByIdAsync(employee.PositionId);
                if (currentPosition != null && currentPosition.OrganizationUnitId != orgUnit.Id)
                    throw new InvalidOperationException("Current position does not belong to the specified organization unit.");
            }
        }

        Guid? newLocationLevelId = null;
        if (dto.LocationId.HasValue)
        {
            var location = await _locationRepository.GetWithDetailsAsync(dto.LocationId.Value);
            if (location == null || !location.IsActive)
                throw new ArgumentException("Location not found or inactive.");
            newLocationLevelId = location.LocationLevelId;
        }

        // Manager change validation
        if (dto.ManagerId.HasValue)
            await ValidateManagerAssignmentAsync(employeeId, dto.ManagerId.Value, cancellationToken);

        // Payroll flags validation (only validate if any flag is being changed or was supplied)
        var payTax = dto.PayTax ?? employee.PayTax;
        var ssFund = dto.SSFund ?? employee.SSFund;
        var grossUp = dto.GrossUp ?? employee.GrossUp;
        var tier2Only = dto.Tier2Only ?? employee.Tier2Only;
        var overtime = dto.Overtime ?? employee.Overtime;
        await ValidatePayrollFlagsAsync(payTax, ssFund, grossUp, tier2Only, overtime, cancellationToken);

        // Track position changes for history
        var oldPositionId = employee.PositionId;
        var isPositionChanging = dto.PositionId.HasValue && dto.PositionId.Value != oldPositionId;

        dto.Apply(employee, newOrgLevelId, newLocationLevelId);
        if (!string.IsNullOrWhiteSpace(dto.EmailAddress))
            employee.EmailAddress = NormalizeEmail(dto.EmailAddress);

        await _employeeRepository.UpdateAsync(employee);

        // Handle position history if position changed
        if (isPositionChanging && dto.PositionId.HasValue)
        {
            var posHistoryRepo = _unitOfWork.Repository<EmployeePositionHistory>();
            var today = DateTime.Today;

            // Close the current position history entry
            var currentHistory = await posHistoryRepo
                .FirstOrDefaultAsync(ph => ph.EmployeeId == employeeId &&
                                     (ph.EndDate == null || ph.EndDate > today));
            
            if (currentHistory != null)
            {
                currentHistory.EndDate = today;
                currentHistory.ChangeReason = PositionChangeReason.Transfer; // or determine based on context
                await posHistoryRepo.UpdateAsync(currentHistory);
            }

            // Create new position history entry
            var newPosition = await _positionRepository.GetByIdAsync(dto.PositionId.Value);
            if (newPosition != null)
            {
                var newHistory = new EmployeePositionHistory
                {
                    EmployeeId = employeeId,
                    PositionId = dto.PositionId.Value,
                    LocationLevelId = (newLocationLevelId ?? employee.LocationLevelId) ?? Guid.Empty,
                    LocationId = dto.LocationId ?? employee.LocationId,
                    OrganizationLevelId = (newOrgLevelId ?? employee.OrganizationLevelId) ?? Guid.Empty,
                    OrganizationUnitId = dto.OrganizationUnitId ?? employee.OrganizationUnitId,
                    StartDate = today,
                    EndDate = null,
                    ChangeReason = PositionChangeReason.Transfer, // or determine based on context
                    Notes = "Position changed via employee update"
                };
                await posHistoryRepo.AddAsync(newHistory);
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Employee updated: {EmployeeId}", employeeId);

        var updated = await _employeeRepository.GetByIdWithDetailsAsync(employeeId);
        if (updated == null) throw new InvalidOperationException("Employee updated but could not be reloaded.");
        return updated.ToDetailDto();
    }

    public async Task<EmployeeDetailDto> ActivateEmployeeAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        var employee = await _employeeRepository.GetByIdAsync(employeeId);
        if (employee == null) throw new ArgumentException($"Employee with ID '{employeeId}' not found.");

        if (employee.StaffStatus == StaffStatus.Terminated)
            throw new InvalidOperationException("Cannot activate a terminated employee. Use reinstate.");

        employee.IsActive = true;
        employee.StaffStatus = StaffStatus.Active;

        await _employeeRepository.UpdateAsync(employee);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var updated = await _employeeRepository.GetByIdWithDetailsAsync(employeeId);
        if (updated == null) throw new InvalidOperationException("Employee updated but could not be reloaded.");
        return updated.ToDetailDto();
    }

    public async Task<EmployeeDetailDto> DeactivateEmployeeAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        var employee = await _employeeRepository.GetByIdAsync(employeeId);
        if (employee == null) throw new ArgumentException($"Employee with ID '{employeeId}' not found.");

        if (employee.StaffStatus == StaffStatus.Terminated)
            throw new InvalidOperationException("Cannot deactivate a terminated employee.");

        employee.IsActive = false;
        employee.StaffStatus = StaffStatus.Inactive;

        await _employeeRepository.UpdateAsync(employee);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var updated = await _employeeRepository.GetByIdWithDetailsAsync(employeeId);
        if (updated == null) throw new InvalidOperationException("Employee updated but could not be reloaded.");
        return updated.ToDetailDto();
    }

    public async Task<EmployeeDetailDto> TerminateEmployeeAsync(Guid employeeId, TerminateEmployeeDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var employee = await _employeeRepository.GetByIdAsync(employeeId);
        if (employee == null) throw new ArgumentException($"Employee with ID '{employeeId}' not found.");

        if (employee.StaffStatus == StaffStatus.Terminated)
            throw new InvalidOperationException("Employee is already terminated.");

        // Basic termination eligibility
        if (!await CanTerminateEmployeeAsync(employeeId, cancellationToken))
            throw new InvalidOperationException("Employee cannot be terminated due to active dependencies/constraints.");

        employee.StaffStatus = StaffStatus.Terminated;
        employee.IsActive = false;
        employee.TerminationDate = dto.TerminationDate;
        employee.TerminationReason = Enum.TryParse<TerminationReason>(dto.TerminationReason, out var tr) ? tr : null;
        employee.TerminationNotes = dto.TerminationNotes;

        // Terminate active contracts
        var contractRepo = _unitOfWork.Repository<EmployeeContractDetail>();
        var activeContracts = await contractRepo.FindAsync(c => c.EmployeeId == employeeId && c.IsActive && !c.IsDeleted);
        foreach (var c in activeContracts)
        {
            c.IsActive = false;
            c.ContractStatus = ContractStatus.Terminated;
            c.TerminationDate = DateOnly.FromDateTime(dto.TerminationDate);
            c.TerminationReason = dto.TerminationReason;
            c.EndDate ??= DateOnly.FromDateTime(dto.TerminationDate);
            await contractRepo.UpdateAsync(c);
        }

        // Close current position history
        var posHistoryRepo = _unitOfWork.Repository<EmployeePositionHistory>();
        var currentHistory = await posHistoryRepo
            .FirstOrDefaultAsync(ph => ph.EmployeeId == employeeId && (ph.EndDate == null || ph.EndDate > dto.TerminationDate));

        if (currentHistory != null)
        {
            currentHistory.EndDate = dto.TerminationDate;
            currentHistory.ChangeReason = PositionChangeReason.Termination;
            await posHistoryRepo.UpdateAsync(currentHistory);
        }

        await _employeeRepository.UpdateAsync(employee);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var terminated = await _employeeRepository.GetByIdWithDetailsAsync(employeeId);
        if (terminated == null) throw new InvalidOperationException("Employee terminated but could not be reloaded.");
        return terminated.ToDetailDto();
    }

    public async Task<EmployeeDetailDto> ReinstateEmployeeAsync(Guid employeeId, string? notes = null, CancellationToken cancellationToken = default)
    {
        var employee = await _employeeRepository.GetByIdAsync(employeeId);
        if (employee == null) throw new ArgumentException($"Employee with ID '{employeeId}' not found.");

        if (employee.StaffStatus != StaffStatus.Terminated)
            throw new InvalidOperationException("Only terminated employees can be reinstated.");

        employee.StaffStatus = StaffStatus.Active;
        employee.IsActive = true;
        employee.TerminationDate = null;
        employee.TerminationReason = null;
        employee.TerminationNotes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();

        await _employeeRepository.UpdateAsync(employee);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var updated = await _employeeRepository.GetByIdWithDetailsAsync(employeeId);
        if (updated == null) throw new InvalidOperationException("Employee updated but could not be reloaded.");
        return updated.ToDetailDto();
    }

    public async Task<bool> DeleteEmployeeAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        await _employeeRepository.DeleteAsync(employeeId);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Employee soft-deleted: {EmployeeId}", employeeId);
        return true;
    }

    #endregion

    #region 2) Employee Retrieval (intent-based)

    public async Task<EmployeeDto?> GetEmployeeSummaryByIdAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        var employee = await _employeeRepository.GetByIdAsync(employeeId,
            e => e.Position,
            e => e.Department!,
            e => e.Section!,
            e => e.OrganizationUnit!,
            e => e.OrganizationLevel!,
            e => e.Location!,
            e => e.LocationLevel!);

        return employee?.ToSummaryDto();
    }

    public async Task<EmployeeDetailDto?> GetEmployeeDetailsByIdAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        var employee = await _employeeRepository.GetByIdWithDetailsAsync(employeeId);
        return employee?.ToDetailDto();
    }

    public async Task<EmployeeFullProfileDto?> GetEmployeeFullProfileByIdAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        var employee = await _employeeRepository.GetByIdWithFullProfileAsync(employeeId);
        return employee?.ToFullProfileDto();
    }

    public async Task<EmployeeDto?> GetEmployeeSummaryByEmployeeNumberAsync(string employeeNumber, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(employeeNumber)) return null;
        var employee = await _employeeRepository.GetByEmployeeNumberAsync(employeeNumber.Trim());
        return employee?.ToSummaryDto();
    }

    public async Task<EmployeeDetailDto?> GetEmployeeDetailsByEmployeeNumberAsync(string employeeNumber, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(employeeNumber)) return null;
        var employee = await _employeeRepository.GetByEmployeeNumberWithDetailsAsync(employeeNumber.Trim());
        return employee?.ToDetailDto();
    }

    public async Task<EmployeeFullProfileDto?> GetEmployeeFullProfileByEmployeeNumberAsync(string employeeNumber, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(employeeNumber)) return null;
        var employee = await _employeeRepository.GetByEmployeeNumberWithFullProfileAsync(employeeNumber.Trim());
        return employee?.ToFullProfileDto();
    }

    public async Task<EmployeeDto?> GetEmployeeSummaryByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email)) return null;
        var employee = await _employeeRepository.GetByEmailAsync(NormalizeEmail(email));
        return employee?.ToSummaryDto();
    }

    public async Task<PagedResult<EmployeeDto>> GetEmployeesPagedAsync(EmployeeSearchDto searchCriteria, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(searchCriteria);

        if (page <= 0) page = 1;
        if (pageSize <= 0) pageSize = 20;
        if (pageSize > 200) pageSize = 200;

        IQueryable<Employee> q = _employeeRepository.GetQueryable();

        q = q
            .Include(e => e.Position)
            .Include(e => e.Department)
            .Include(e => e.Section)
            .Include(e => e.OrganizationUnit)
            .Include(e => e.OrganizationLevel)
            .Include(e => e.Location)
            .Include(e => e.LocationLevel);

        if (!string.IsNullOrWhiteSpace(searchCriteria.SearchTerm))
        {
            var term = searchCriteria.SearchTerm.Trim();
            var like = $"%{term}%";
            q = q.Where(e =>
                EF.Functions.Like(e.FirstName, like) ||
                EF.Functions.Like(e.LastName, like) ||
                EF.Functions.Like(e.EmployeeNumber, like) ||
                EF.Functions.Like(e.EmailAddress, like) ||
                EF.Functions.Like((e.FirstName + " " + e.LastName), like));
        }

        if (searchCriteria.DepartmentId.HasValue) q = q.Where(e => e.DepartmentId == searchCriteria.DepartmentId);
        if (searchCriteria.SectionId.HasValue) q = q.Where(e => e.SectionId == searchCriteria.SectionId);
        if (searchCriteria.PositionId.HasValue) q = q.Where(e => e.PositionId == searchCriteria.PositionId);
        if (searchCriteria.StaffStatus.HasValue) q = q.Where(e => e.StaffStatus == searchCriteria.StaffStatus);
        if (searchCriteria.EmploymentType.HasValue) q = q.Where(e => e.EmploymentType == searchCriteria.EmploymentType);
        if (searchCriteria.IsActive.HasValue) q = q.Where(e => e.IsActive == searchCriteria.IsActive);
        if (searchCriteria.IsFullTime.HasValue) q = q.Where(e => e.IsFullTime == searchCriteria.IsFullTime);
        if (searchCriteria.MaintenanceTechniciansOnly == true) q = q.Where(e => e.CanBeAssignedToMaintenance);
        if (searchCriteria.HiredAfter.HasValue) q = q.Where(e => e.DateEmployed >= searchCriteria.HiredAfter);
        if (searchCriteria.HiredBefore.HasValue) q = q.Where(e => e.DateEmployed <= searchCriteria.HiredBefore);

        var totalCount = await q.CountAsync(cancellationToken);

        var employees = await q.OrderBy(e => e.LastName)
            .ThenBy(e => e.FirstName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<EmployeeDto>
        {
            Items = employees.Select(e => e.ToSummaryDto()).ToList(),
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<IEnumerable<EmployeeDto>> GetEmployeesByOrganizationUnitAsync(Guid organizationUnitId, CancellationToken cancellationToken = default)
        => (await _employeeRepository.GetByOrganizationUnitAsync(organizationUnitId)).Select(e => e.ToSummaryDto());

    public async Task<IEnumerable<EmployeeDto>> GetEmployeesByOrganizationLevelAsync(Guid organizationLevelId, CancellationToken cancellationToken = default)
        => (await _employeeRepository.GetByOrganizationLevelAsync(organizationLevelId)).Select(e => e.ToSummaryDto());

    public async Task<IEnumerable<EmployeeDto>> GetEmployeesByPositionAsync(Guid positionId, CancellationToken cancellationToken = default)
        => (await _employeeRepository.GetByPositionAsync(positionId)).Select(e => e.ToSummaryDto());

    public async Task<IEnumerable<EmployeeDto>> GetEmployeesByLocationAsync(Guid locationId, CancellationToken cancellationToken = default)
        => (await _employeeRepository.FindAsync(e => e.LocationId == locationId,
            e => e.Position,
            e => e.OrganizationUnit!,
            e => e.OrganizationLevel!,
            e => e.Location!,
            e => e.LocationLevel!)).Select(e => e.ToSummaryDto());

    public async Task<IEnumerable<EmployeeDto>> GetEmployeesByManagerAsync(Guid managerId, CancellationToken cancellationToken = default)
        => (await _employeeRepository.GetByManagerAsync(managerId)).Select(e => e.ToSummaryDto());

    public async Task<IEnumerable<EmployeeDto>> GetEmployeesByStatusAsync(StaffStatus status, CancellationToken cancellationToken = default)
        => (await _employeeRepository.GetByStatusAsync(status)).Select(e => e.ToSummaryDto());

    #endregion

    #region 3) Relationship Management (subresources)

    public async Task<IEnumerable<EmployeeEmergencyContactDto>> GetEmergencyContactsAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.Repository<EmployeeEmergencyContact>();
        var items = await repo.FindAsync(e => e.EmployeeId == employeeId);
        return items.OrderByDescending(x => x.IsPrimary).ThenBy(x => x.LastName).Select(x => x.ToDto());
    }

    public async Task<EmployeeEmergencyContactDto> AddEmergencyContactAsync(CreateEmployeeEmergencyContactDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        await EnsureEmployeeExistsAsync(dto.EmployeeId);

        var repo = _unitOfWork.Repository<EmployeeEmergencyContact>();
        var entity = dto.ToEntity();

        if (entity.IsPrimary)
        {
            // Ensure single primary per employee
            var existing = await repo.FindAsync(x => x.EmployeeId == dto.EmployeeId && x.IsPrimary);
            foreach (var c in existing)
            {
                c.IsPrimary = false;
                await repo.UpdateAsync(c);
            }
        }

        await repo.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return entity.ToDto();
    }

    public async Task<EmployeeEmergencyContactDto> UpdateEmergencyContactAsync(UpdateEmployeeEmergencyContactDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var repo = _unitOfWork.Repository<EmployeeEmergencyContact>();
        var entity = await repo.GetByIdAsync(dto.Id);
        if (entity == null) throw new ArgumentException($"Emergency contact '{dto.Id}' not found.");

        dto.Apply(entity);

        if (entity.IsPrimary)
        {
            var others = await repo.FindAsync(x => x.EmployeeId == entity.EmployeeId && x.Id != entity.Id && x.IsPrimary);
            foreach (var c in others)
            {
                c.IsPrimary = false;
                await repo.UpdateAsync(c);
            }
        }

        await repo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return entity.ToDto();
    }

    public async Task<bool> RemoveEmergencyContactAsync(Guid emergencyContactId, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.Repository<EmployeeEmergencyContact>();
        var entity = await repo.GetByIdAsync(emergencyContactId);
        if (entity == null) return false;

        await repo.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<EmployeeEmergencyContactDto> SetPrimaryEmergencyContactAsync(Guid emergencyContactId, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.Repository<EmployeeEmergencyContact>();
        var entity = await repo.GetByIdAsync(emergencyContactId);
        if (entity == null) throw new ArgumentException($"Emergency contact '{emergencyContactId}' not found.");

        var existing = await repo.FindAsync(x => x.EmployeeId == entity.EmployeeId && x.IsPrimary && x.Id != entity.Id);
        foreach (var c in existing)
        {
            c.IsPrimary = false;
            await repo.UpdateAsync(c);
        }

        entity.IsPrimary = true;
        entity.IsActive = true;
        await repo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return entity.ToDto();
    }

    public async Task<EmployeeEmergencyContactDto> ActivateEmergencyContactAsync(Guid emergencyContactId, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.Repository<EmployeeEmergencyContact>();
        var entity = await repo.GetByIdAsync(emergencyContactId);
        if (entity == null) throw new ArgumentException($"Emergency contact '{emergencyContactId}' not found.");

        entity.IsActive = true;
        await repo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<EmployeeEmergencyContactDto> DeactivateEmergencyContactAsync(Guid emergencyContactId, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.Repository<EmployeeEmergencyContact>();
        var entity = await repo.GetByIdAsync(emergencyContactId);
        if (entity == null) throw new ArgumentException($"Emergency contact '{emergencyContactId}' not found.");

        if (entity.IsPrimary)
            throw new InvalidOperationException("Primary emergency contact cannot be deactivated. Set another primary first.");

        entity.IsActive = false;
        await repo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<IEnumerable<EmployeeContactDto>> GetContactsAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.Repository<EmployeeContact>();
        var items = await repo.FindAsync(e => e.EmployeeId == employeeId);
        return items.OrderByDescending(x => x.IsPrimary).ThenBy(x => x.ContactType.ToString()).Select(x => x.ToDto());
    }

    public async Task<EmployeeContactDto> AddContactAsync(CreateEmployeeContactDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        await EnsureEmployeeExistsAsync(dto.EmployeeId);

        var repo = _unitOfWork.Repository<EmployeeContact>();
        var entity = dto.ToEntity();

        if (entity.IsPrimary)
        {
            var existing = await repo.FindAsync(x => x.EmployeeId == dto.EmployeeId && x.IsPrimary);
            foreach (var c in existing)
            {
                c.IsPrimary = false;
                await repo.UpdateAsync(c);
            }
        }

        await repo.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return entity.ToDto();
    }

    public async Task<EmployeeContactDto> UpdateContactAsync(UpdateEmployeeContactDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var repo = _unitOfWork.Repository<EmployeeContact>();
        var entity = await repo.GetByIdAsync(dto.Id);
        if (entity == null) throw new ArgumentException($"Contact '{dto.Id}' not found.");

        dto.Apply(entity);

        if (entity.IsPrimary)
        {
            var others = await repo.FindAsync(x => x.EmployeeId == entity.EmployeeId && x.Id != entity.Id && x.IsPrimary);
            foreach (var c in others)
            {
                c.IsPrimary = false;
                await repo.UpdateAsync(c);
            }
        }

        await repo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return entity.ToDto();
    }

    public async Task<bool> RemoveContactAsync(Guid contactId, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.Repository<EmployeeContact>();
        var entity = await repo.GetByIdAsync(contactId);
        if (entity == null) return false;

        await repo.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<EmployeeContactDto> SetPrimaryContactAsync(Guid contactId, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.Repository<EmployeeContact>();
        var entity = await repo.GetByIdAsync(contactId);
        if (entity == null) throw new ArgumentException($"Contact '{contactId}' not found.");

        var existing = await repo.FindAsync(x => x.EmployeeId == entity.EmployeeId && x.IsPrimary && x.Id != entity.Id);
        foreach (var c in existing)
        {
            c.IsPrimary = false;
            await repo.UpdateAsync(c);
        }

        entity.IsPrimary = true;
        await repo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return entity.ToDto();
    }

    public async Task<IEnumerable<EmployeeDependentReadDto>> GetDependentsAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.Repository<EmployeeDependent>();
        var items = await repo.FindAsync(d => d.EmployeeId == employeeId);
        return items.OrderBy(d => d.LastName).ThenBy(d => d.FirstName).Select(d => d.ToReadDto());
    }

    public async Task<EmployeeDependentReadDto> AddDependentAsync(EmployeeDependentCreateDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        await EnsureEmployeeExistsAsync(dto.EmployeeId);

        var repo = _unitOfWork.Repository<EmployeeDependent>();
        var entity = dto.ToEntity();

        await repo.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return entity.ToReadDto();
    }

    public async Task<EmployeeDependentReadDto> UpdateDependentAsync(EmployeeDependentUpdateDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var repo = _unitOfWork.Repository<EmployeeDependent>();
        var entity = await repo.GetByIdAsync(dto.Id);
        if (entity == null) throw new ArgumentException($"Dependent '{dto.Id}' not found.");

        dto.Apply(entity);
        await repo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return entity.ToReadDto();
    }

    public async Task<bool> RemoveDependentAsync(Guid employeeId, Guid dependentId, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.Repository<EmployeeDependent>();
        var entity = await repo.GetByIdAsync(dependentId);
        if (entity == null) return false;

        if (entity.EmployeeId != employeeId)
            throw new ArgumentException("Dependent does not belong to the specified employee.");

        await repo.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<IEnumerable<EmployeeDependentBenefitDto>> GetDependentBenefitsAsync(Guid employeeDependentId, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.Repository<EmployeeDependentBenefit>();
        var q = repo.GetQueryable()
            .Include(b => b.BenefitPolicy)
            .Where(b => b.EmployeeDependentId == employeeDependentId);

        var items = await q.ToListAsync(cancellationToken);
        return items.Select(b => b.ToDto());
    }

    public async Task<EmployeeDependentBenefitDto> AddDependentBenefitAsync(CreateEmployeeDependentBenefitDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        // Ensure dependent exists and is eligible for benefits
        var dependentRepo = _unitOfWork.Repository<EmployeeDependent>();
        var dependent = await dependentRepo.GetByIdAsync(dto.EmployeeDependentId);
        if (dependent == null) throw new ArgumentException("Dependent not found.");
        if (!dependent.IsEligibleForBenefits) throw new InvalidOperationException("Dependent is not eligible for benefits.");

        var repo = _unitOfWork.Repository<EmployeeDependentBenefit>();
        var entity = dto.ToEntity();

        await repo.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // reload with policy for display
        var reloaded = await repo.GetQueryable().Include(b => b.BenefitPolicy).FirstOrDefaultAsync(b => b.Id == entity.Id, cancellationToken);
        return (reloaded ?? entity).ToDto();
    }

    public async Task<EmployeeDependentBenefitDto> UpdateDependentBenefitAsync(UpdateEmployeeDependentBenefitDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var repo = _unitOfWork.Repository<EmployeeDependentBenefit>();
        var entity = await repo.GetByIdAsync(dto.Id);
        if (entity == null) throw new ArgumentException("Dependent benefit not found.");

        dto.Apply(entity);
        await repo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var reloaded = await repo.GetQueryable().Include(b => b.BenefitPolicy).FirstOrDefaultAsync(b => b.Id == entity.Id, cancellationToken);
        return (reloaded ?? entity).ToDto();
    }

    public async Task<bool> RemoveDependentBenefitAsync(Guid dependentBenefitId, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.Repository<EmployeeDependentBenefit>();
        var entity = await repo.GetByIdAsync(dependentBenefitId);
        if (entity == null) return false;

        await repo.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<EmployeeDependentBenefitDto> ActivateDependentBenefitAsync(Guid dependentBenefitId, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.Repository<EmployeeDependentBenefit>();
        var entity = await repo.GetByIdAsync(dependentBenefitId);
        if (entity == null) throw new ArgumentException("Dependent benefit not found.");

        entity.IsActive = true;
        await repo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var reloaded = await repo.GetQueryable().Include(b => b.BenefitPolicy).FirstOrDefaultAsync(b => b.Id == entity.Id, cancellationToken);
        return (reloaded ?? entity).ToDto();
    }

    public async Task<EmployeeDependentBenefitDto> DeactivateDependentBenefitAsync(Guid dependentBenefitId, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.Repository<EmployeeDependentBenefit>();
        var entity = await repo.GetByIdAsync(dependentBenefitId);
        if (entity == null) throw new ArgumentException("Dependent benefit not found.");

        entity.IsActive = false;
        await repo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var reloaded = await repo.GetQueryable().Include(b => b.BenefitPolicy).FirstOrDefaultAsync(b => b.Id == entity.Id, cancellationToken);
        return (reloaded ?? entity).ToDto();
    }

    public async Task<IEnumerable<EmployeeQualificationDto>> GetQualificationsAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.Repository<EmployeeQualification>();
        var q = repo.GetQueryable()
            .Include(x => x.Qualification)
            .Include(x => x.Country)
            .Where(x => x.EmployeeId == employeeId);

        var items = await q.ToListAsync(cancellationToken);
        return items.Select(x => x.ToDto());
    }

    public async Task<EmployeeQualificationDto> AddQualificationAsync(CreateEmployeeQualificationDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        await EnsureEmployeeExistsAsync(dto.EmployeeId);

        // Validate that either QualificationId or CustomQualificationName is provided
        if ((!dto.QualificationId.HasValue || dto.QualificationId.Value == Guid.Empty) && 
            string.IsNullOrWhiteSpace(dto.CustomQualificationName))
            throw new ArgumentException("Either QualificationId or CustomQualificationName is required.");

        var repo = _unitOfWork.Repository<EmployeeQualification>();
        var entity = dto.ToEntity();

        await repo.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var reloaded = await repo.GetQueryable()
            .Include(x => x.Qualification)
            .Include(x => x.Country)
            .FirstOrDefaultAsync(x => x.Id == entity.Id, cancellationToken);

        return (reloaded ?? entity).ToDto();
    }

    public async Task<EmployeeQualificationDto> UpdateQualificationAsync(UpdateEmployeeQualificationDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var repo = _unitOfWork.Repository<EmployeeQualification>();
        var entity = await repo.GetByIdAsync(dto.Id);
        if (entity == null) throw new ArgumentException("Qualification not found.");

        dto.Apply(entity);
        await repo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var reloaded = await repo.GetQueryable()
            .Include(x => x.Qualification)
            .Include(x => x.Country)
            .FirstOrDefaultAsync(x => x.Id == entity.Id, cancellationToken);

        return (reloaded ?? entity).ToDto();
    }

    public async Task<bool> RemoveQualificationAsync(Guid qualificationId, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.Repository<EmployeeQualification>();
        var entity = await repo.GetByIdAsync(qualificationId);
        if (entity == null) return false;

        await repo.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<EmployeeQualificationDto> VerifyQualificationAsync(Guid qualificationId, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.Repository<EmployeeQualification>();
        var entity = await repo.GetByIdAsync(qualificationId);
        if (entity == null) throw new ArgumentException("Qualification not found.");

        entity.IsVerified = true;
        await repo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var reloaded = await repo.GetQueryable().Include(x => x.Qualification).Include(x => x.Country).FirstOrDefaultAsync(x => x.Id == entity.Id, cancellationToken);
        return (reloaded ?? entity).ToDto();
    }

    public async Task<EmployeeQualificationDto> UnverifyQualificationAsync(Guid qualificationId, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.Repository<EmployeeQualification>();
        var entity = await repo.GetByIdAsync(qualificationId);
        if (entity == null) throw new ArgumentException("Qualification not found.");

        entity.IsVerified = false;
        await repo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var reloaded = await repo.GetQueryable().Include(x => x.Qualification).Include(x => x.Country).FirstOrDefaultAsync(x => x.Id == entity.Id, cancellationToken);
        return (reloaded ?? entity).ToDto();
    }

    public async Task<IEnumerable<EmployeeSkillDto>> GetSkillsAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.Repository<EmployeeSkill>();
        var items = await repo.GetQueryable()
            .Include(s => s.Skill)
            .Where(s => s.EmployeeId == employeeId)
            .OrderByDescending(s => s.IsVerified)
            .ThenByDescending(s => s.SkillLevel)
            .ToListAsync(cancellationToken);

        return items.Select(s => s.ToDto());
    }

    public async Task<EmployeeSkillDto> AddSkillAsync(CreateEmployeeSkillDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        await EnsureEmployeeExistsAsync(dto.EmployeeId);

        var repo = _unitOfWork.Repository<EmployeeSkill>();

        var exists = await repo.ExistsAsync(s => s.EmployeeId == dto.EmployeeId && s.SkillId == dto.SkillId);
        if (exists) throw new InvalidOperationException("Employee already has this skill assigned.");

        var entity = dto.ToEntity();

        await repo.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var reloaded = await repo.GetQueryable().Include(s => s.Skill).FirstOrDefaultAsync(s => s.Id == entity.Id, cancellationToken);
        return (reloaded ?? entity).ToDto();
    }

    public async Task<EmployeeSkillDto> UpdateSkillAsync(UpdateEmployeeSkillDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var repo = _unitOfWork.Repository<EmployeeSkill>();
        var entity = await repo.GetByIdAsync(dto.Id);
        if (entity == null) throw new ArgumentException("Employee skill not found.");

        dto.Apply(entity);
        await repo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var reloaded = await repo.GetQueryable().Include(s => s.Skill).FirstOrDefaultAsync(s => s.Id == entity.Id, cancellationToken);
        return (reloaded ?? entity).ToDto();
    }

    public async Task<bool> RemoveSkillAsync(Guid employeeSkillId, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.Repository<EmployeeSkill>();
        var entity = await repo.GetByIdAsync(employeeSkillId);
        if (entity == null) return false;

        await repo.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<EmployeeSkillDto> VerifySkillAsync(Guid employeeSkillId, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.Repository<EmployeeSkill>();
        var entity = await repo.GetByIdAsync(employeeSkillId);
        if (entity == null) throw new ArgumentException("Employee skill not found.");

        entity.IsVerified = true;

        await repo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var reloaded = await repo.GetQueryable().Include(s => s.Skill).FirstOrDefaultAsync(s => s.Id == entity.Id, cancellationToken);
        return (reloaded ?? entity).ToDto();
    }

    public async Task<EmployeeSkillDto> UnverifySkillAsync(Guid employeeSkillId, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.Repository<EmployeeSkill>();
        var entity = await repo.GetByIdAsync(employeeSkillId);
        if (entity == null) throw new ArgumentException("Employee skill not found.");

        entity.IsVerified = false;

        await repo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var reloaded = await repo.GetQueryable().Include(s => s.Skill).FirstOrDefaultAsync(s => s.Id == entity.Id, cancellationToken);
        return (reloaded ?? entity).ToDto();
    }

    public async Task<IEnumerable<EmployeeIdentificationCardListDto>> GetIdentificationCardsAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.Repository<EmployeeIdentificationCard>();
        var items = await repo.GetQueryable()
            .Include(x => x.IdentificationType)
                .ThenInclude(it => it.IssuingCountry)
            .Where(x => x.EmployeeId == employeeId)
            .OrderByDescending(x => x.ExpiryDate)
            .ToListAsync(cancellationToken);

        return items.Select(x => x.ToListDto());
    }

    public async Task<EmployeeIdentificationCardDetailDto?> GetIdentificationCardByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.Repository<EmployeeIdentificationCard>();
        var entity = await repo.GetQueryable()
            .Include(x => x.IdentificationType)
                .ThenInclude(it => it.IssuingCountry)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        return entity?.ToDetailDto();
    }

    public async Task<EmployeeIdentificationCardDetailDto> AddIdentificationCardAsync(CreateEmployeeIdentificationCardDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        await EnsureEmployeeExistsAsync(dto.EmployeeId);

        var repo = _unitOfWork.Repository<EmployeeIdentificationCard>();
        var entity = dto.ToEntity();

        await repo.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var reloaded = await repo.GetQueryable()
            .Include(x => x.IdentificationType)
                .ThenInclude(it => it.IssuingCountry)
            .FirstOrDefaultAsync(x => x.Id == entity.Id, cancellationToken);
        return (reloaded ?? entity).ToDetailDto();
    }

    public async Task<EmployeeIdentificationCardDetailDto> UpdateIdentificationCardAsync(UpdateEmployeeIdentificationCardDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var repo = _unitOfWork.Repository<EmployeeIdentificationCard>();
        var entity = await repo.GetByIdAsync(dto.Id);
        if (entity == null) throw new ArgumentException("Identification card not found.");

        dto.Apply(entity);
        await repo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var reloaded = await repo.GetQueryable()
            .Include(x => x.IdentificationType)
                .ThenInclude(it => it.IssuingCountry)
            .FirstOrDefaultAsync(x => x.Id == entity.Id, cancellationToken);
        return (reloaded ?? entity).ToDetailDto();
    }

    public async Task<bool> RemoveIdentificationCardAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.Repository<EmployeeIdentificationCard>();
        var entity = await repo.GetByIdAsync(id);
        if (entity == null) return false;

        await repo.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<EmployeeIdentificationCardDetailDto> VerifyIdentificationCardAsync(Guid id, DateTime verifiedDate, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.Repository<EmployeeIdentificationCard>();
        var entity = await repo.GetByIdAsync(id);
        if (entity == null) throw new ArgumentException("Identification card not found.");

        entity.IsVerified = true;
        entity.VerifiedDate = verifiedDate;

        await repo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var reloaded = await repo.GetQueryable().Include(x => x.IdentificationType).FirstOrDefaultAsync(x => x.Id == entity.Id, cancellationToken);
        return (reloaded ?? entity).ToDetailDto();
    }

    public async Task<EmployeeIdentificationCardDetailDto> UnverifyIdentificationCardAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.Repository<EmployeeIdentificationCard>();
        var entity = await repo.GetByIdAsync(id);
        if (entity == null) throw new ArgumentException("Identification card not found.");

        entity.IsVerified = false;
        entity.VerifiedDate = null;

        await repo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var reloaded = await repo.GetQueryable().Include(x => x.IdentificationType).FirstOrDefaultAsync(x => x.Id == entity.Id, cancellationToken);
        return (reloaded ?? entity).ToDetailDto();
    }

    public async Task<IEnumerable<EmployeeWorkHistoryListDto>> GetWorkHistoriesAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.Repository<EmployeeWorkHistory>();
        var items = await repo.FindAsync(x => x.EmployeeId == employeeId);
        return items.OrderByDescending(x => x.StartDate).Select(x => x.ToListDto());
    }

    public async Task<EmployeeWorkHistoryDetailDto?> GetWorkHistoryByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.Repository<EmployeeWorkHistory>();
        var entity = await repo.GetByIdAsync(id);
        return entity?.ToDetailDto();
    }

    public async Task<EmployeeWorkHistoryDetailDto> AddWorkHistoryAsync(CreateEmployeeWorkHistoryDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        await EnsureEmployeeExistsAsync(dto.EmployeeId);

        var repo = _unitOfWork.Repository<EmployeeWorkHistory>();
        var entity = dto.ToEntity();

        await repo.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return entity.ToDetailDto();
    }

    public async Task<EmployeeWorkHistoryDetailDto> UpdateWorkHistoryAsync(UpdateEmployeeWorkHistoryDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var repo = _unitOfWork.Repository<EmployeeWorkHistory>();
        var entity = await repo.GetByIdAsync(dto.Id);
        if (entity == null) throw new ArgumentException("Work history not found.");

        dto.Apply(entity);
        await repo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return entity.ToDetailDto();
    }

    public async Task<bool> RemoveWorkHistoryAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.Repository<EmployeeWorkHistory>();
        var entity = await repo.GetByIdAsync(id);
        if (entity == null) return false;

        await repo.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<IEnumerable<EmployeeContractDetailDto>> GetContractsAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.Repository<EmployeeContractDetail>();
        var items = await repo.FindAsync(x => x.EmployeeId == employeeId);
        return items.OrderByDescending(x => x.StartDate).Select(x => x.ToDto());
    }

    public async Task<EmployeeContractDetailDto?> GetActiveContractAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.Repository<EmployeeContractDetail>();
        var entity = await repo.FirstOrDefaultAsync(x => x.EmployeeId == employeeId && x.IsActive);
        return entity?.ToDto();
    }

    public async Task<EmployeeContractDetailDto> AddContractAsync(CreateEmployeeContractDetailDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        await EnsureEmployeeExistsAsync(dto.EmployeeId);

        // One active contract rule
        if (await HasActiveContractAsync(dto.EmployeeId, cancellationToken) && dto.IsActive && dto.ContractStatus == ContractStatus.Active)
            throw new InvalidOperationException("Employee already has an active contract.");

        ValidateContractTaxRules(dto.TaxTreatmentType, dto.WithholdingTaxRate);

        var repo = _unitOfWork.Repository<EmployeeContractDetail>();
        var entity = new EmployeeContractDetail
        {
            EmployeeId = dto.EmployeeId,
            ContractNumber = dto.ContractNumber.Trim(),
            EmploymentType = dto.EmploymentType,
            StartDate = dto.StartDate,
            EndDate = dto.EndDate,
            Salary = dto.Salary,
            PayFrequency = dto.PayFrequency,
            TaxTreatmentType = dto.TaxTreatmentType,
            WithholdingTaxRate = dto.WithholdingTaxRate,
            IsPensionApplicable = dto.IsPensionApplicable,
            IsTaxExempt = dto.IsTaxExempt,
            WorkingHoursPerWeek = dto.WorkingHoursPerWeek,
            VacationDaysPerYear = dto.VacationDaysPerYear,
            SickDaysPerYear = dto.SickDaysPerYear,
            ProbationPeriodDays = dto.ProbationPeriodDays,
            ConfirmationDate = dto.ConfirmationDate,
            Terms = dto.Terms,
            IsActive = dto.IsActive,
            ContractPath = dto.ContractPath,
            ContractStatus = dto.ContractStatus,
            TerminationDate = dto.TerminationDate,
            TerminationReason = dto.TerminationReason
        };

        await repo.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return entity.ToDto();
    }

    public async Task<EmployeeContractDetailDto> UpdateContractAsync(UpdateEmployeeContractDetailDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var repo = _unitOfWork.Repository<EmployeeContractDetail>();
        var entity = await repo.GetByIdAsync(dto.Id);
        if (entity == null) throw new ArgumentException("Contract not found.");

        if (dto.EmploymentType.HasValue) entity.EmploymentType = dto.EmploymentType.Value;
        if (dto.StartDate.HasValue) entity.StartDate = dto.StartDate.Value;
        if (dto.EndDate.HasValue) entity.EndDate = dto.EndDate;
        if (dto.Salary.HasValue) entity.Salary = dto.Salary.Value;
        if (dto.PayFrequency.HasValue) entity.PayFrequency = dto.PayFrequency.Value;
        if (dto.TaxTreatmentType.HasValue) entity.TaxTreatmentType = dto.TaxTreatmentType.Value;
        if (dto.WithholdingTaxRate.HasValue) entity.WithholdingTaxRate = dto.WithholdingTaxRate;
        if (dto.IsPensionApplicable.HasValue) entity.IsPensionApplicable = dto.IsPensionApplicable.Value;
        if (dto.IsTaxExempt.HasValue) entity.IsTaxExempt = dto.IsTaxExempt.Value;
        if (dto.WorkingHoursPerWeek.HasValue) entity.WorkingHoursPerWeek = dto.WorkingHoursPerWeek.Value;
        if (dto.VacationDaysPerYear.HasValue) entity.VacationDaysPerYear = dto.VacationDaysPerYear.Value;
        if (dto.SickDaysPerYear.HasValue) entity.SickDaysPerYear = dto.SickDaysPerYear.Value;
        if (dto.ProbationPeriodDays.HasValue) entity.ProbationPeriodDays = dto.ProbationPeriodDays;
        if (dto.ConfirmationDate.HasValue) entity.ConfirmationDate = dto.ConfirmationDate;
        if (dto.Terms != null) entity.Terms = dto.Terms;
        if (dto.IsActive.HasValue) entity.IsActive = dto.IsActive.Value;
        if (dto.ContractPath != null) entity.ContractPath = dto.ContractPath;
        if (dto.ContractStatus.HasValue) entity.ContractStatus = dto.ContractStatus.Value;
        if (dto.TerminationDate.HasValue) entity.TerminationDate = dto.TerminationDate;
        if (dto.TerminationReason != null) entity.TerminationReason = dto.TerminationReason;

        ValidateContractTaxRules(entity.TaxTreatmentType, entity.WithholdingTaxRate);

        await repo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return entity.ToDto();
    }

    public async Task<bool> RemoveContractAsync(Guid contractId, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.Repository<EmployeeContractDetail>();
        var entity = await repo.GetByIdAsync(contractId);
        if (entity == null) return false;

        await repo.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<EmployeeContractDetailDto> ActivateContractAsync(Guid contractId, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.Repository<EmployeeContractDetail>();
        var entity = await repo.GetByIdAsync(contractId);
        if (entity == null) throw new ArgumentException("Contract not found.");

        if (await HasActiveContractAsync(entity.EmployeeId, cancellationToken))
            throw new InvalidOperationException("Employee already has an active contract.");

        entity.IsActive = true;
        entity.ContractStatus = ContractStatus.Active;

        await repo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return entity.ToDto();
    }

    public async Task<EmployeeContractDetailDto> DeactivateContractAsync(Guid contractId, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.Repository<EmployeeContractDetail>();
        var entity = await repo.GetByIdAsync(contractId);
        if (entity == null) throw new ArgumentException("Contract not found.");

        entity.IsActive = false;
        entity.ContractStatus = entity.ContractStatus == ContractStatus.Active ? ContractStatus.Expired : entity.ContractStatus;

        await repo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return entity.ToDto();
    }

    public async Task<EmployeeContractDetailDto> TerminateContractAsync(Guid contractId, DateOnly terminationDate, string reason, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("Termination reason is required.");

        var repo = _unitOfWork.Repository<EmployeeContractDetail>();
        var entity = await repo.GetByIdAsync(contractId);
        if (entity == null) throw new ArgumentException("Contract not found.");

        entity.IsActive = false;
        entity.ContractStatus = ContractStatus.Terminated;
        entity.TerminationDate = terminationDate;
        entity.TerminationReason = reason.Trim();
        entity.EndDate ??= terminationDate;

        await repo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return entity.ToDto();
    }

    public async Task<IEnumerable<ExpatriateAssignmentListDto>> GetExpatriateAssignmentsAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.Repository<ExpatriateAssignment>();
        var items = await repo.GetQueryable()
            .Include(x => x.Country)
            .Where(x => x.EmployeeId == employeeId)
            .OrderByDescending(x => x.StartDate)
            .ToListAsync(cancellationToken);
        return items.Select(x => x.ToListDto());
    }

    public async Task<ExpatriateAssignmentDetailDto?> GetExpatriateAssignmentByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.Repository<ExpatriateAssignment>();
        var entity = await repo.GetQueryable().Include(x => x.Country).FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        return entity?.ToDetailDto();
    }

    public async Task<ExpatriateAssignmentDetailDto> AddExpatriateAssignmentAsync(CreateExpatriateAssignmentDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        await EnsureEmployeeExistsAsync(dto.EmployeeId);
        if (!await IsEligibleForExpatriateAssignmentAsync(dto.EmployeeId, cancellationToken))
            throw new InvalidOperationException("Employee is not eligible for expatriate assignment.");

        var repo = _unitOfWork.Repository<ExpatriateAssignment>();
        var entity = dto.ToEntity();

        await repo.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var reloaded = await repo.GetQueryable().Include(x => x.Country).FirstOrDefaultAsync(x => x.Id == entity.Id, cancellationToken);
        return (reloaded ?? entity).ToDetailDto();
    }

    public async Task<ExpatriateAssignmentDetailDto> UpdateExpatriateAssignmentAsync(UpdateExpatriateAssignmentDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var repo = _unitOfWork.Repository<ExpatriateAssignment>();
        var entity = await repo.GetByIdAsync(dto.Id);
        if (entity == null) throw new ArgumentException("Expatriate assignment not found.");

        dto.Apply(entity);
        await repo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var reloaded = await repo.GetQueryable().Include(x => x.Country).FirstOrDefaultAsync(x => x.Id == entity.Id, cancellationToken);
        return (reloaded ?? entity).ToDetailDto();
    }

    public async Task<bool> RemoveExpatriateAssignmentAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.Repository<ExpatriateAssignment>();
        var entity = await repo.GetByIdAsync(id);
        if (entity == null) return false;

        await repo.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<IEnumerable<EmployeePositionHistoryListDto>> GetPositionHistoryAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.Repository<EmployeePositionHistory>();
        var items = await repo.GetQueryable()
            .Include(x => x.Position)
            .Where(x => x.EmployeeId == employeeId)
            .OrderByDescending(x => x.StartDate)
            .ToListAsync(cancellationToken);

        if (items.Count == 0)
        {
            var employee = await _employeeRepository.GetByIdAsync(employeeId);
            if (employee != null)
            {
                await EnsureInitialPositionHistoryExistsAsync(employee, cancellationToken);

                items = await repo.GetQueryable()
                    .Include(x => x.Position)
                    .Where(x => x.EmployeeId == employeeId)
                    .OrderByDescending(x => x.StartDate)
                    .ToListAsync(cancellationToken);
            }
        }

        return items.Select(x => x.ToListDto());
    }

    public async Task<EmployeePositionHistoryDetailDto?> GetPositionHistoryByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.Repository<EmployeePositionHistory>();
        var entity = await repo.GetQueryable().Include(x => x.Position).FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        return entity?.ToDetailDto();
    }

    public async Task<EmployeePositionHistoryDetailDto> AddPositionHistoryAsync(CreateEmployeePositionHistoryDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        await EnsureEmployeeExistsAsync(dto.EmployeeId);

        var repo = _unitOfWork.Repository<EmployeePositionHistory>();
        var entity = dto.ToEntity();

        // close current history if overlapping
        var current = await repo.FirstOrDefaultAsync(x => x.EmployeeId == dto.EmployeeId && x.EndDate == null);
        if (current != null && current.StartDate <= entity.StartDate)
        {
            current.EndDate = entity.StartDate.AddSeconds(-1);
            await repo.UpdateAsync(current);
        }

        await repo.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var reloaded = await repo.GetQueryable().Include(x => x.Position).FirstOrDefaultAsync(x => x.Id == entity.Id, cancellationToken);
        return (reloaded ?? entity).ToDetailDto();
    }

    public async Task<EmployeePositionHistoryDetailDto> UpdatePositionHistoryAsync(UpdateEmployeePositionHistoryDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var repo = _unitOfWork.Repository<EmployeePositionHistory>();
        var entity = await repo.GetByIdAsync(dto.Id);
        if (entity == null) throw new ArgumentException("Position history not found.");

        dto.Apply(entity);
        await repo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var reloaded = await repo.GetQueryable().Include(x => x.Position).FirstOrDefaultAsync(x => x.Id == entity.Id, cancellationToken);
        return (reloaded ?? entity).ToDetailDto();
    }

    public async Task<bool> RemovePositionHistoryAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.Repository<EmployeePositionHistory>();
        var entity = await repo.GetByIdAsync(id);
        if (entity == null) return false;

        await repo.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<IEnumerable<EmployeeSalaryAssignmentListDto>> GetSalaryAssignmentsAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.Repository<EmployeeSalaryAssignment>();
        var items = await repo.GetQueryable()
            .Include(x => x.Grade)
            .Include(x => x.Level)
            .Include(x => x.Notch)
            .Where(x => x.EmployeeId == employeeId)
            .OrderByDescending(x => x.EffectiveDate)
            .ToListAsync(cancellationToken);
        return items.Select(x => x.ToListDto());
    }

    public async Task<EmployeeSalaryAssignmentDetailDto?> GetSalaryAssignmentByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.Repository<EmployeeSalaryAssignment>();
        var entity = await repo.GetQueryable()
            .Include(x => x.Grade)
            .Include(x => x.Level)
            .Include(x => x.Notch)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        return entity?.ToDetailDto();
    }

    public async Task<EmployeeSalaryAssignmentDetailDto> AssignSalaryAsync(CreateEmployeeSalaryAssignmentDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        await EnsureEmployeeExistsAsync(dto.EmployeeId);

        if (dto.EffectiveDate == default) throw new ArgumentException("EffectiveDate is required.");

        var repo = _unitOfWork.Repository<EmployeeSalaryAssignment>();
        var entity = dto.ToEntity();

        // Prevent overlaps: close any assignment overlapping the new EffectiveDate
        var current = await repo.FirstOrDefaultAsync(a =>
            a.EmployeeId == dto.EmployeeId &&
            a.EffectiveDate <= dto.EffectiveDate &&
            (a.EffectiveTo == null || a.EffectiveTo >= dto.EffectiveDate));

        if (current != null)
        {
            current.EffectiveTo = dto.EffectiveDate.AddDays(-1);
            await repo.UpdateAsync(current);
        }

        await repo.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var reloaded = await repo.GetQueryable().Include(x => x.Grade).Include(x => x.Level).Include(x => x.Notch).FirstOrDefaultAsync(x => x.Id == entity.Id, cancellationToken);
        return (reloaded ?? entity).ToDetailDto();
    }

    public async Task<EmployeeSalaryAssignmentDetailDto> UpdateSalaryAssignmentAsync(UpdateEmployeeSalaryAssignmentDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var repo = _unitOfWork.Repository<EmployeeSalaryAssignment>();
        var entity = await repo.GetByIdAsync(dto.Id);
        if (entity == null) throw new ArgumentException("Salary assignment not found.");

        dto.Apply(entity);
        await repo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var reloaded = await repo.GetQueryable().Include(x => x.Grade).Include(x => x.Level).Include(x => x.Notch).FirstOrDefaultAsync(x => x.Id == entity.Id, cancellationToken);
        return (reloaded ?? entity).ToDetailDto();
    }

    public async Task<bool> RemoveSalaryAssignmentAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.Repository<EmployeeSalaryAssignment>();
        var entity = await repo.GetByIdAsync(id);
        if (entity == null) return false;

        await repo.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<IEnumerable<EmployeeRefereeListDto>> GetRefereesAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.Repository<EmployeeReferee>();
        var items = await repo.FindAsync(x => x.EmployeeId == employeeId);
        return items.OrderByDescending(x => x.IsPrimary).ThenBy(x => x.FullName).Select(x => x.ToListDto());
    }

    public async Task<EmployeeRefereeDetailDto?> GetRefereeByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.Repository<EmployeeReferee>();
        var entity = await repo.GetByIdAsync(id);
        return entity?.ToDetailDto();
    }

    public async Task<EmployeeRefereeDetailDto> AddRefereeAsync(CreateEmployeeRefereeDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        await EnsureEmployeeExistsAsync(dto.EmployeeId);

        var repo = _unitOfWork.Repository<EmployeeReferee>();
        var entity = dto.ToEntity();

        if (entity.IsPrimary)
        {
            var others = await repo.FindAsync(x => x.EmployeeId == dto.EmployeeId && x.IsPrimary);
            foreach (var r in others)
            {
                r.IsPrimary = false;
                await repo.UpdateAsync(r);
            }
        }

        await repo.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return entity.ToDetailDto();
    }

    public async Task<EmployeeRefereeDetailDto> UpdateRefereeAsync(UpdateEmployeeRefereeDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var repo = _unitOfWork.Repository<EmployeeReferee>();
        var entity = await repo.GetByIdAsync(dto.Id);
        if (entity == null) throw new ArgumentException("Referee not found.");

        dto.Apply(entity);
        if (entity.IsPrimary)
        {
            var others = await repo.FindAsync(x => x.EmployeeId == entity.EmployeeId && x.Id != entity.Id && x.IsPrimary);
            foreach (var r in others)
            {
                r.IsPrimary = false;
                await repo.UpdateAsync(r);
            }
        }
        await repo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return entity.ToDetailDto();
    }

    public async Task<bool> RemoveRefereeAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.Repository<EmployeeReferee>();
        var entity = await repo.GetByIdAsync(id);
        if (entity == null) return false;

        await repo.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<EmployeeRefereeDetailDto> SetPrimaryRefereeAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.Repository<EmployeeReferee>();
        var entity = await repo.GetByIdAsync(id);
        if (entity == null) throw new ArgumentException("Referee not found.");

        var others = await repo.FindAsync(x => x.EmployeeId == entity.EmployeeId && x.Id != entity.Id && x.IsPrimary);
        foreach (var r in others)
        {
            r.IsPrimary = false;
            await repo.UpdateAsync(r);
        }
        entity.IsPrimary = true;
        entity.IsActive = true;
        await repo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return entity.ToDetailDto();
    }

    public async Task<EmployeeRefereeDetailDto> ActivateRefereeAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.Repository<EmployeeReferee>();
        var entity = await repo.GetByIdAsync(id);
        if (entity == null) throw new ArgumentException("Referee not found.");
        entity.IsActive = true;
        await repo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDetailDto();
    }

    public async Task<EmployeeRefereeDetailDto> DeactivateRefereeAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.Repository<EmployeeReferee>();
        var entity = await repo.GetByIdAsync(id);
        if (entity == null) throw new ArgumentException("Referee not found.");
        if (entity.IsPrimary) throw new InvalidOperationException("Primary referee cannot be deactivated.");
        entity.IsActive = false;
        await repo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDetailDto();
    }

    public async Task<IEnumerable<EmployeeGuarantorListDto>> GetGuarantorsAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.Repository<EmployeeGuarantor>();
        var items = await repo.FindAsync(x => x.EmployeeId == employeeId);
        return items.OrderByDescending(x => x.IsPrimary).ThenBy(x => x.LastName).Select(x => x.ToListDto());
    }

    public async Task<EmployeeGuarantorDetailDto?> GetGuarantorByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.Repository<EmployeeGuarantor>();
        var entity = await repo.GetByIdAsync(id);
        return entity?.ToDetailDto();
    }

    public async Task<EmployeeGuarantorDetailDto> AddGuarantorAsync(CreateEmployeeGuarantorDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        await EnsureEmployeeExistsAsync(dto.EmployeeId);

        var repo = _unitOfWork.Repository<EmployeeGuarantor>();
        var entity = dto.ToEntity();

        if (entity.IsPrimary)
        {
            var others = await repo.FindAsync(x => x.EmployeeId == dto.EmployeeId && x.IsPrimary);
            foreach (var g in others)
            {
                g.IsPrimary = false;
                await repo.UpdateAsync(g);
            }
        }
        await repo.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return entity.ToDetailDto();
    }

    public async Task<EmployeeGuarantorDetailDto> UpdateGuarantorAsync(UpdateEmployeeGuarantorDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var repo = _unitOfWork.Repository<EmployeeGuarantor>();
        var entity = await repo.GetByIdAsync(dto.Id);
        if (entity == null) throw new ArgumentException("Guarantor not found.");

        dto.Apply(entity);
        if (entity.IsPrimary)
        {
            var others = await repo.FindAsync(x => x.EmployeeId == entity.EmployeeId && x.Id != entity.Id && x.IsPrimary);
            foreach (var g in others)
            {
                g.IsPrimary = false;
                await repo.UpdateAsync(g);
            }
        }
        await repo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return entity.ToDetailDto();
    }

    public async Task<bool> RemoveGuarantorAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.Repository<EmployeeGuarantor>();
        var entity = await repo.GetByIdAsync(id);
        if (entity == null) return false;

        await repo.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<EmployeeGuarantorDetailDto> SetPrimaryGuarantorAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.Repository<EmployeeGuarantor>();
        var entity = await repo.GetByIdAsync(id);
        if (entity == null) throw new ArgumentException("Guarantor not found.");

        var others = await repo.FindAsync(x => x.EmployeeId == entity.EmployeeId && x.Id != entity.Id && x.IsPrimary);
        foreach (var g in others)
        {
            g.IsPrimary = false;
            await repo.UpdateAsync(g);
        }
        entity.IsPrimary = true;
        entity.IsActive = true;
        await repo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return entity.ToDetailDto();
    }

    public async Task<EmployeeGuarantorDetailDto> VerifyGuarantorAsync(Guid id, Guid verifiedByEmployeeId, DateTime verifiedDate, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.Repository<EmployeeGuarantor>();
        var entity = await repo.GetByIdAsync(id);
        if (entity == null) throw new ArgumentException("Guarantor not found.");

        // Ensure verifier exists
        await EnsureEmployeeExistsAsync(verifiedByEmployeeId);

        entity.IsVerified = true;
        entity.VerificationDate = verifiedDate;
        entity.VerifiedByEmployeeId = verifiedByEmployeeId;

        await repo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return entity.ToDetailDto();
    }

    public async Task<EmployeeGuarantorDetailDto> UnverifyGuarantorAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.Repository<EmployeeGuarantor>();
        var entity = await repo.GetByIdAsync(id);
        if (entity == null) throw new ArgumentException("Guarantor not found.");

        entity.IsVerified = false;
        entity.VerificationDate = null;
        entity.VerifiedByEmployeeId = null;

        await repo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return entity.ToDetailDto();
    }

    public async Task<EmployeeGuarantorDetailDto> ActivateGuarantorAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.Repository<EmployeeGuarantor>();
        var entity = await repo.GetByIdAsync(id);
        if (entity == null) throw new ArgumentException("Guarantor not found.");
        entity.IsActive = true;
        await repo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDetailDto();
    }

    public async Task<EmployeeGuarantorDetailDto> DeactivateGuarantorAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.Repository<EmployeeGuarantor>();
        var entity = await repo.GetByIdAsync(id);
        if (entity == null) throw new ArgumentException("Guarantor not found.");
        if (entity.IsPrimary) throw new InvalidOperationException("Primary guarantor cannot be deactivated.");
        entity.IsActive = false;
        await repo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDetailDto();
    }

    // ── Bank Details ─────────────────────────────────────────────────────────

    public async Task<IEnumerable<EmployeeBankDetailDto>> GetBankDetailsAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.Repository<EmployeeBankDetail>();
        var items = await repo.FindAsync(x => x.EmployeeId == employeeId);
        return items.OrderByDescending(x => x.IsPrimary).ThenBy(x => x.BankName).Select(x => x.ToDto());
    }

    public async Task<EmployeeBankDetailDto?> GetBankDetailByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.Repository<EmployeeBankDetail>();
        var entity = await repo.GetByIdAsync(id);
        return entity?.ToDto();
    }

    public async Task<EmployeeBankDetailDto> AddBankDetailAsync(CreateEmployeeBankDetailDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        await EnsureEmployeeExistsAsync(dto.EmployeeId);

        var repo = _unitOfWork.Repository<EmployeeBankDetail>();

        // Enforce single primary: if new record is primary, clear others
        if (dto.IsPrimary)
        {
            var existing = await repo.FindAsync(x => x.EmployeeId == dto.EmployeeId && x.IsPrimary);
            foreach (var e in existing)
            {
                e.IsPrimary = false;
                await repo.UpdateAsync(e);
            }
        }

        var entity = dto.ToEntity();
        await repo.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<EmployeeBankDetailDto> UpdateBankDetailAsync(UpdateEmployeeBankDetailDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var repo = _unitOfWork.Repository<EmployeeBankDetail>();
        var entity = await repo.GetByIdAsync(dto.Id);
        if (entity == null) throw new ArgumentException("Bank detail not found.");

        // Enforce single primary: if patching to primary, clear others first
        if (dto.IsPrimary == true && !entity.IsPrimary)
        {
            var others = await repo.FindAsync(x => x.EmployeeId == entity.EmployeeId && x.IsPrimary && x.Id != entity.Id);
            foreach (var o in others)
            {
                o.IsPrimary = false;
                await repo.UpdateAsync(o);
            }
        }

        dto.Apply(entity);
        await repo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> RemoveBankDetailAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.Repository<EmployeeBankDetail>();
        var entity = await repo.GetByIdAsync(id);
        if (entity == null) return false;
        if (entity.IsPrimary) throw new InvalidOperationException("Cannot remove the primary bank account. Set another account as primary first.");
        await repo.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<EmployeeBankDetailDto> SetPrimaryBankDetailAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.Repository<EmployeeBankDetail>();
        var entity = await repo.GetByIdAsync(id);
        if (entity == null) throw new ArgumentException("Bank detail not found.");

        var others = await repo.FindAsync(x => x.EmployeeId == entity.EmployeeId && x.Id != entity.Id && x.IsPrimary);
        foreach (var o in others)
        {
            o.IsPrimary = false;
            await repo.UpdateAsync(o);
        }
        entity.IsPrimary = true;
        entity.IsActive = true;
        await repo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<EmployeeBankDetailDto> VerifyBankDetailAsync(Guid id, Guid verifiedByEmployeeId, DateTime verifiedDate, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.Repository<EmployeeBankDetail>();
        var entity = await repo.GetByIdAsync(id);
        if (entity == null) throw new ArgumentException("Bank detail not found.");

        await EnsureEmployeeExistsAsync(verifiedByEmployeeId);

        entity.IsVerified  = true;
        entity.VerifiedDate = verifiedDate;
        entity.VerifiedById = verifiedByEmployeeId;

        await repo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<EmployeeBankDetailDto> UnverifyBankDetailAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.Repository<EmployeeBankDetail>();
        var entity = await repo.GetByIdAsync(id);
        if (entity == null) throw new ArgumentException("Bank detail not found.");
        entity.IsVerified   = false;
        entity.VerifiedDate = null;
        entity.VerifiedById = null;
        await repo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<EmployeeBankDetailDto> ActivateBankDetailAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.Repository<EmployeeBankDetail>();
        var entity = await repo.GetByIdAsync(id);
        if (entity == null) throw new ArgumentException("Bank detail not found.");
        entity.IsActive = true;
        await repo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<EmployeeBankDetailDto> DeactivateBankDetailAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.Repository<EmployeeBankDetail>();
        var entity = await repo.GetByIdAsync(id);
        if (entity == null) throw new ArgumentException("Bank detail not found.");
        if (entity.IsPrimary) throw new InvalidOperationException("Primary bank account cannot be deactivated.");
        entity.IsActive = false;
        await repo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    #endregion

    #region 4) Manager & Hierarchy

    public async Task<EmployeeDetailDto> AssignManagerAsync(Guid employeeId, Guid managerId, CancellationToken cancellationToken = default)
    {
        await ValidateManagerAssignmentAsync(employeeId, managerId, cancellationToken);

        var employee = await _employeeRepository.GetByIdAsync(employeeId);
        if (employee == null) throw new ArgumentException($"Employee '{employeeId}' not found.");

        employee.ManagerId = managerId;

        await _employeeRepository.UpdateAsync(employee);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var updated = await _employeeRepository.GetByIdWithDetailsAsync(employeeId);
        if (updated == null) throw new InvalidOperationException("Employee updated but could not be reloaded.");
        return updated.ToDetailDto();
    }

    public async Task<EmployeeDetailDto> RemoveManagerAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        var employee = await _employeeRepository.GetByIdAsync(employeeId);
        if (employee == null) throw new ArgumentException($"Employee '{employeeId}' not found.");

        employee.ManagerId = null;

        await _employeeRepository.UpdateAsync(employee);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var updated = await _employeeRepository.GetByIdWithDetailsAsync(employeeId);
        if (updated == null) throw new InvalidOperationException("Employee updated but could not be reloaded.");
        return updated.ToDetailDto();
    }

    public async Task<IEnumerable<EmployeeDto>> GetDirectReportsAsync(Guid managerId, CancellationToken cancellationToken = default)
        => (await _employeeRepository.GetByManagerAsync(managerId)).Select(e => e.ToSummaryDto());

    public async Task<IEnumerable<EmployeeDto>> GetManagementChainAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        var chain = new List<Employee>();
        var visited = new HashSet<Guid>();

        var current = await _employeeRepository.GetByIdAsync(employeeId);
        if (current == null) throw new ArgumentException($"Employee '{employeeId}' not found.");

        var managerId = current.ManagerId;
        while (managerId.HasValue)
        {
            if (!visited.Add(managerId.Value))
                throw new InvalidOperationException("Circular reporting detected while building management chain.");

            var manager = await _employeeRepository.GetByIdAsync(managerId.Value, e => e.Position, e => e.OrganizationUnit!, e => e.OrganizationLevel!);
            if (manager == null) break;
            chain.Add(manager);
            managerId = manager.ManagerId;
        }

        return chain.Select(e => e.ToSummaryDto());
    }

    public async Task<bool> WouldCreateCircularReportingAsync(Guid employeeId, Guid proposedManagerId, CancellationToken cancellationToken = default)
    {
        if (employeeId == proposedManagerId) return true;

        var visited = new HashSet<Guid> { employeeId };
        var current = proposedManagerId;

        while (true)
        {
            if (!visited.Add(current)) return true;
            var manager = await _employeeRepository.GetByIdAsync(current);
            if (manager == null || !manager.ManagerId.HasValue) return false;
            current = manager.ManagerId.Value;
        }
    }

    #endregion

    #region 5) Payroll, Tax & Contract Logic

    public Task ValidatePayrollFlagsAsync(bool payTax, bool ssFund, bool grossUp, bool tier2Only, bool overtime, CancellationToken cancellationToken = default)
    {
        // Coherence rules (policy-level; can be expanded safely without changing persistence):
        // - Gross-up only makes sense if PAYE is enabled (otherwise nothing to gross-up)
        // - Tier2Only implies SSFund (pension) is enabled
        if (!payTax && grossUp)
            throw new InvalidOperationException("GrossUp requires PayTax to be enabled.");

        if (tier2Only && !ssFund)
            throw new InvalidOperationException("Tier2Only requires SSFund to be enabled.");

        _ = overtime; // reserved for future validations / payroll calendar checks
        return Task.CompletedTask;
    }

    #endregion

    #region 6) Validation & Business Rules

    public async Task<bool> IsEmployeeNumberUniqueAsync(string employeeNumber, Guid? excludeEmployeeId = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(employeeNumber)) return false;
        var num = employeeNumber.Trim();

        return excludeEmployeeId.HasValue
            ? !await _employeeRepository.EmployeeNumberExistsAsync(num, excludeEmployeeId.Value)
            : !await _employeeRepository.EmployeeNumberExistsAsync(num);
    }

    public async Task<bool> IsEmailUniqueAsync(string email, Guid? excludeEmployeeId = null, CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeEmail(email);
        if (string.IsNullOrWhiteSpace(normalized)) return false;

        return excludeEmployeeId.HasValue
            ? !await _employeeRepository.EmailExistsAsync(normalized, excludeEmployeeId.Value)
            : !await _employeeRepository.EmailExistsAsync(normalized);
    }

    public async Task<bool> IsBadgeNumberUniqueAsync(string badgeNumber, Guid? excludeEmployeeId = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(badgeNumber)) return true;
        var value = badgeNumber.Trim();
        return excludeEmployeeId.HasValue
            ? !await _employeeRepository.BadgeNumberExistsAsync(value, excludeEmployeeId.Value)
            : !await _employeeRepository.BadgeNumberExistsAsync(value);
    }

    public async Task<bool> IsTaxNumberUniqueAsync(string taxNumber, Guid? excludeEmployeeId = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(taxNumber)) return true;
        var value = taxNumber.Trim();
        return excludeEmployeeId.HasValue
            ? !await _employeeRepository.TaxNumberExistsAsync(value, excludeEmployeeId.Value)
            : !await _employeeRepository.TaxNumberExistsAsync(value);
    }

    public async Task<bool> IsSocialSecurityNumberUniqueAsync(string socialSecurityNumber, Guid? excludeEmployeeId = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(socialSecurityNumber)) return true;
        var value = socialSecurityNumber.Trim();
        return excludeEmployeeId.HasValue
            ? !await _employeeRepository.SocialSecurityNumberExistsAsync(value, excludeEmployeeId.Value)
            : !await _employeeRepository.SocialSecurityNumberExistsAsync(value);
    }

    public async Task<bool> IsTinNumberUniqueAsync(string tinNumber, Guid? excludeEmployeeId = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tinNumber)) return true;
        var value = tinNumber.Trim();
        return excludeEmployeeId.HasValue
            ? !await _employeeRepository.TinNumberExistsAsync(value, excludeEmployeeId.Value)
            : !await _employeeRepository.TinNumberExistsAsync(value);
    }

    public async Task<bool> HasActiveContractAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.Repository<EmployeeContractDetail>();
        return await repo.ExistsAsync(x => x.EmployeeId == employeeId && x.IsActive);
    }

    public async Task<bool> HasActiveDependentsAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.Repository<EmployeeDependent>();
        return await repo.ExistsAsync(x => x.EmployeeId == employeeId);
    }

    public async Task<bool> HasActiveGuarantorsAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.Repository<EmployeeGuarantor>();
        return await repo.ExistsAsync(x => x.EmployeeId == employeeId && x.IsActive);
    }

    public async Task<bool> IsEligibleForExpatriateAssignmentAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        var employee = await _employeeRepository.GetByIdAsync(employeeId);
        if (employee == null) return false;

        // Simple baseline eligibility: active employee, not terminated
        return employee.IsActive && employee.StaffStatus != StaffStatus.Terminated;
    }

    public async Task<bool> CanTerminateEmployeeAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        var employee = await _employeeRepository.GetByIdAsync(employeeId);
        if (employee == null) return false;
        if (employee.StaffStatus == StaffStatus.Terminated) return false;

        // Termination is allowed, but we enforce: no active guarantor verification pending? (simplified)
        return true;
    }

    #endregion

    #region Legacy / Backward compatibility

    public Task<EmployeeDto?> GetByIdAsync(Guid id) => GetEmployeeSummaryByIdAsync(id);
    public Task<EmployeeDetailDto?> GetDetailsByIdAsync(Guid id) => GetEmployeeDetailsByIdAsync(id);
    public Task<EmployeeDto?> GetByEmployeeNumberAsync(string employeeNumber) => GetEmployeeSummaryByEmployeeNumberAsync(employeeNumber);

    public async Task<IEnumerable<EmployeeDto>> GetAllAsync()
    {
        var employees = await _employeeRepository.GetAllAsync(e => e.Position, e => e.OrganizationUnit!, e => e.OrganizationLevel!);
        return employees.Select(e => e.ToSummaryDto());
    }

    public async Task<IEnumerable<EmployeeDto>> GetActiveEmployeesAsync()
        => (await _employeeRepository.GetActiveEmployeesAsync()).Select(e => e.ToSummaryDto());

    public async Task<IEnumerable<EmployeeDto>> SearchEmployeesAsync(EmployeeSearchDto searchCriteria)
        => (await GetEmployeesPagedAsync(searchCriteria, 1, 200)).Items;

    public async Task<EmployeeDto> CreateEmployeeAsync(CreateEmployeeDto createDto)
    {
        var detail = await CreateEmployeeAsync(createDto, CancellationToken.None);
        var summary = await GetEmployeeSummaryByIdAsync(detail.Id, CancellationToken.None);
        return summary ?? ToSummaryFromDetail(detail);
    }

    public async Task<EmployeeDto> UpdateEmployeeAsync(Guid id, UpdateEmployeeDto updateDto)
    {
        var detail = await UpdateEmployeeAsync(id, updateDto, CancellationToken.None);
        var summary = await GetEmployeeSummaryByIdAsync(detail.Id, CancellationToken.None);
        return summary ?? ToSummaryFromDetail(detail);
    }

    public Task<bool> DeleteEmployeeAsync(Guid id) => DeleteEmployeeAsync(id, CancellationToken.None);

    public async Task<bool> DeactivateEmployeeAsync(Guid id)
    {
        await DeactivateEmployeeAsync(id, CancellationToken.None);
        return true;
    }

    public async Task<bool> ActivateEmployeeAsync(Guid id)
    {
        await ActivateEmployeeAsync(id, CancellationToken.None);
        return true;
    }

    public async Task<bool> TerminateEmployeeAsync(Guid id, TerminateEmployeeDto dto)
    {
        await TerminateEmployeeAsync(id, dto, CancellationToken.None);
        return true;
    }

    public async Task<IEnumerable<EmployeeDto>> GetByDepartmentAsync(Guid departmentId)
        => (await _employeeRepository.GetByDepartmentAsync(departmentId)).Select(e => e.ToSummaryDto());

    public async Task<IEnumerable<EmployeeDto>> GetBySectionAsync(Guid sectionId)
        => (await _employeeRepository.GetBySectionAsync(sectionId)).Select(e => e.ToSummaryDto());

    public async Task<IEnumerable<EmployeeDto>> GetByPositionAsync(Guid positionId)
        => (await _employeeRepository.GetByPositionAsync(positionId)).Select(e => e.ToSummaryDto());

    public async Task<IEnumerable<EmployeeDto>> GetByStatusAsync(StaffStatus status)
        => (await _employeeRepository.GetByStatusAsync(status)).Select(e => e.ToSummaryDto());

    public async Task<IEnumerable<EmployeeDto>> GetByEmploymentTypeAsync(EmploymentType employmentType)
        => (await _employeeRepository.GetByEmploymentTypeAsync(employmentType)).Select(e => e.ToSummaryDto());

    // Maintenance integration left as existing (no aggregate writes)
    public async Task<IEnumerable<MaintenanceTechnicianDto>> GetMaintenanceTechniciansAsync()
    {
        var technicians = await _employeeRepository.GetMaintenanceTechniciansAsync();
        return technicians.Select(MapToMaintenanceTechnicianDto);
    }

    public async Task<IEnumerable<MaintenanceTechnicianDto>> GetAvailableTechniciansAsync()
    {
        var technicians = await _employeeRepository.GetAvailableTechniciansAsync();
        return technicians.Select(MapToMaintenanceTechnicianDto);
    }

    public async Task<MaintenanceTechnicianDto?> GetTechnicianByIdAsync(Guid employeeId)
    {
        var employee = await _employeeRepository.GetByIdWithDetailsAsync(employeeId);
        return employee == null || !employee.CanBeAssignedToMaintenance ? null : MapToMaintenanceTechnicianDto(employee);
    }

    public async Task<TechnicianAvailabilityDto?> GetTechnicianAvailabilityAsync(Guid employeeId)
    {
        var employee = await _employeeRepository.GetByIdAsync(employeeId);
        if (employee == null || !employee.CanBeAssignedToMaintenance) return null;

        return new TechnicianAvailabilityDto
        {
            EmployeeId = employee.Id,
            EmployeeName = employee.FullName,
            IsAvailable = employee.IsActive && employee.StaffStatus == StaffStatus.Active,
            AvailableFrom = employee.StaffStatus == StaffStatus.Active ? DateTime.UtcNow : null,
            UnavailabilityReason = employee.StaffStatus != StaffStatus.Active ? employee.StaffStatus.ToString() : null,
            CurrentWorkOrders = 0,
            WorkloadPercentage = 0
        };
    }

    public async Task<IEnumerable<MaintenanceTechnicianDto>> GetTechniciansWithSkillAsync(Guid skillId, SkillLevel? minLevel = null)
    {
        var employees = await _employeeRepository.GetEmployeesBySkillAsync(skillId, minLevel);
        return employees.Where(e => e.CanBeAssignedToMaintenance).Select(MapToMaintenanceTechnicianDto);
    }

    public Task<bool> EmployeeNumberExistsAsync(string employeeNumber) => _employeeRepository.EmployeeNumberExistsAsync(employeeNumber);
    public Task<bool> EmailExistsAsync(string email) => _employeeRepository.EmailExistsAsync(NormalizeEmail(email));
    public Task<string> GenerateEmployeeNumberAsync() => _employeeRepository.GenerateEmployeeNumberAsync();

    public async Task<int> GetTotalEmployeeCountAsync() => await _employeeRepository.CountAsync();
    public async Task<int> GetActiveEmployeeCountAsync() => await _employeeRepository.CountAsync(e => e.IsActive);

    public async Task<Dictionary<StaffStatus, int>> GetEmployeeCountByStatusAsync()
    {
        var employees = await _employeeRepository.GetAllAsync();
        return employees.GroupBy(e => e.StaffStatus).ToDictionary(g => g.Key, g => g.Count());
    }

    public async Task<Dictionary<string, int>> GetEmployeeCountByDepartmentAsync()
    {
        var employees = await _employeeRepository.GetAllAsync(e => e.Department!);
        return employees.GroupBy(e => e.Department?.Name ?? "Unassigned").ToDictionary(g => g.Key, g => g.Count());
    }

    #endregion

    #region Private helpers

    private async Task EnsureEmployeeExistsAsync(Guid employeeId)
    {
        var employee = await _employeeRepository.GetByIdAsync(employeeId);
        if (employee == null) throw new ArgumentException($"Employee '{employeeId}' not found.");
    }

    private static string NormalizeEmail(string? email)
        => string.IsNullOrWhiteSpace(email) ? string.Empty : email.Trim().ToLowerInvariant();

    private async Task ValidateManagerAssignmentAsync(Guid employeeId, Guid managerId, CancellationToken cancellationToken)
    {
        if (employeeId == managerId)
            throw new InvalidOperationException("Employee cannot be their own manager.");

        var manager = await _employeeRepository.GetByIdAsync(managerId);
        if (manager == null) throw new ArgumentException("Manager not found.");
        if (!manager.IsActive || manager.StaffStatus == StaffStatus.Terminated)
            throw new InvalidOperationException("Manager must be active and not terminated.");

        if (employeeId != Guid.Empty && await WouldCreateCircularReportingAsync(employeeId, managerId, cancellationToken))
            throw new InvalidOperationException("Manager assignment would create circular reporting.");
    }

    private static EmployeeDto ToSummaryFromDetail(EmployeeDetailDto d)
        => new()
        {
            Id = d.Id,
            EmployeeNumber = d.EmployeeNumber,
            FirstName = d.FirstName,
            MiddleName = d.MiddleName,
            LastName = d.LastName,
            FullName = d.FullName,
            DisplayName = d.DisplayName,
            Title = d.Title,
            Gender = d.Gender,
            EmailAddress = d.EmailAddress,
            MobileNumber = d.MobileNumber,
            DepartmentName = d.DepartmentName,
            SectionName = d.SectionName,
            PositionTitle = d.PositionTitle,
            OrganizationLevelName = d.OrganizationLevelName,
            OrganizationUnitName = d.OrganizationUnitName,
            LocationLevelName = d.LocationLevelName,
            LocationName = d.LocationName,
            StaffStatus = d.StaffStatus,
            EmploymentType = d.EmploymentType,
            IsActive = d.IsActive,
            IsFullTime = d.IsFullTime,
            IsExpatriate = d.IsExpatriate,
            DateEmployed = d.DateEmployed,
            YearsOfService = d.YearsOfService,
            CanBeAssignedToMaintenance = d.CanBeAssignedToMaintenance,
            PicturePath = d.PicturePath
        };

    private static void ValidateContractTaxRules(TaxTreatmentType taxTreatmentType, decimal? withholdingTaxRate)
    {
        if (taxTreatmentType == TaxTreatmentType.WithholdingTax && !withholdingTaxRate.HasValue)
            throw new InvalidOperationException("Withholding tax contracts require a WithholdingTaxRate.");

        if (withholdingTaxRate.HasValue && (withholdingTaxRate < 0 || withholdingTaxRate > 100))
            throw new InvalidOperationException("WithholdingTaxRate must be between 0 and 100.");
    }

    private static MaintenanceTechnicianDto MapToMaintenanceTechnicianDto(Employee employee)
    {
        return new MaintenanceTechnicianDto
        {
            Id = employee.Id,
            EmployeeNumber = employee.EmployeeNumber,
            FullName = employee.FullName,
            DisplayName = employee.DisplayName,
            EmailAddress = employee.EmailAddress,
            MobileNumber = employee.MobileNumber,
            PositionTitle = employee.Position?.Title ?? string.Empty,
            IsActive = employee.IsActive,
            IsAvailable = employee.IsActive && employee.StaffStatus == StaffStatus.Active,
            Skills = employee.Skills?.Select(es => new ErpSystem.Core.DTOs.Maintenance.UserTechnicianSkillDto
            {
                SkillName = es.Skill.Name,
                Level = (int)es.SkillLevel,
                IsCertified = es.CertificationDate.HasValue
            }).ToList() ?? new List<ErpSystem.Core.DTOs.Maintenance.UserTechnicianSkillDto>(),
            CurrentWorkOrders = 0,
            WorkloadScore = 0,
            BadgeNumber = employee.BadgeNumber,
            ShiftName = null
        };
    }

    #endregion
}
