using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.Extensions.Logging;
using MaintenanceDTOs = ErpSystem.Core.DTOs.Maintenance;

namespace ErpSystem.Core.Services.HR;

/// <summary>
/// Service implementation for employee operations
/// </summary>
public class EmployeeService : IEmployeeService
{
    private readonly IEmployeeRepository _employeeRepository;
    private readonly IDepartmentRepository _departmentRepository;
    private readonly IEmployeeSkillRepository _employeeSkillRepository;
    private readonly ILogger<EmployeeService> _logger;

    public EmployeeService(
        IEmployeeRepository employeeRepository,
        IDepartmentRepository departmentRepository,
        IEmployeeSkillRepository employeeSkillRepository,
        ILogger<EmployeeService> logger)
    {
        _employeeRepository = employeeRepository;
        _departmentRepository = departmentRepository;
        _employeeSkillRepository = employeeSkillRepository;
        _logger = logger;
    }

    #region Employee CRUD Operations

    public async Task<EmployeeDto?> GetByIdAsync(Guid id)
    {
        var employee = await _employeeRepository.GetByIdAsync(id);
        return employee != null ? MapToDto(employee) : null;
    }

    public async Task<EmployeeDetailDto?> GetDetailsByIdAsync(Guid id)
    {
        var employee = await _employeeRepository.GetByIdWithDetailsAsync(id);
        return employee != null ? MapToDetailDto(employee) : null;
    }

    public async Task<EmployeeDto?> GetByEmployeeNumberAsync(string employeeNumber)
    {
        var employee = await _employeeRepository.GetByEmployeeNumberAsync(employeeNumber);
        return employee != null ? MapToDto(employee) : null;
    }

    public async Task<IEnumerable<EmployeeDto>> GetAllAsync()
    {
        var employees = await _employeeRepository.GetAllAsync();
        return employees.Select(MapToDto);
    }

    public async Task<IEnumerable<EmployeeDto>> GetActiveEmployeesAsync()
    {
        var employees = await _employeeRepository.GetActiveEmployeesAsync();
        return employees.Select(MapToDto);
    }

    public async Task<IEnumerable<EmployeeDto>> SearchEmployeesAsync(EmployeeSearchDto searchCriteria)
    {
        var employees = await _employeeRepository.GetAllAsync();

        // Apply filters
        var query = employees.AsEnumerable();

        if (!string.IsNullOrEmpty(searchCriteria.SearchTerm))
        {
            var searchEmployees = await _employeeRepository.SearchEmployeesAsync(searchCriteria.SearchTerm);
            query = searchEmployees;
        }

        if (searchCriteria.DepartmentId.HasValue)
        {
            query = query.Where(e => e.DepartmentId == searchCriteria.DepartmentId.Value);
        }

        if (searchCriteria.SectionId.HasValue)
        {
            query = query.Where(e => e.SectionId == searchCriteria.SectionId.Value);
        }

        if (searchCriteria.PositionId.HasValue)
        {
            query = query.Where(e => e.PositionId == searchCriteria.PositionId.Value);
        }

        if (searchCriteria.StaffStatus.HasValue)
        {
            query = query.Where(e => e.StaffStatus == searchCriteria.StaffStatus.Value);
        }

        if (searchCriteria.ContractType.HasValue)
        {
            query = query.Where(e => e.ContractType == searchCriteria.ContractType.Value);
        }

        if (searchCriteria.IsActive.HasValue)
        {
            query = query.Where(e => e.IsActive == searchCriteria.IsActive.Value);
        }

        if (searchCriteria.IsFullTime.HasValue)
        {
            query = query.Where(e => e.IsFullTime == searchCriteria.IsFullTime.Value);
        }

        if (searchCriteria.MaintenanceTechniciansOnly == true)
        {
            query = query.Where(e => e.CanBeAssignedToMaintenance);
        }

        if (searchCriteria.HiredAfter.HasValue)
        {
            query = query.Where(e => e.DateEmployed >= searchCriteria.HiredAfter.Value);
        }

        if (searchCriteria.HiredBefore.HasValue)
        {
            query = query.Where(e => e.DateEmployed <= searchCriteria.HiredBefore.Value);
        }

        return query.Select(MapToDto);
    }

    public async Task<EmployeeDto> CreateEmployeeAsync(CreateEmployeeDto createDto)
    {
        // Generate employee number if not provided
        if (string.IsNullOrEmpty(createDto.EmployeeNumber))
        {
            createDto.EmployeeNumber = await _employeeRepository.GenerateEmployeeNumberAsync();
        }

        // Validate employee number uniqueness
        if (await _employeeRepository.EmployeeNumberExistsAsync(createDto.EmployeeNumber))
        {
            throw new InvalidOperationException($"Employee number {createDto.EmployeeNumber} already exists.");
        }

        // Validate email uniqueness
        if (await _employeeRepository.EmailExistsAsync(createDto.EmailAddress))
        {
            throw new InvalidOperationException($"Email address {createDto.EmailAddress} already exists.");
        }

        var employee = new Employee
        {
            EmployeeNumber = createDto.EmployeeNumber,
            CorporateEmployeeID = createDto.CorporateEmployeeID,
            FirstName = createDto.FirstName,
            MiddleName = createDto.MiddleName,
            LastName = createDto.LastName,
            Title = createDto.Title,
            Gender = createDto.Gender,
            DateOfBirth = createDto.DateOfBirth,
            MaritalStatus = createDto.MaritalStatus,
            Religion = createDto.Religion,
            IsFullTime = createDto.IsFullTime,
            DateEmployed = createDto.DateEmployed,
            Address = createDto.Address,
            City = createDto.City,
            State = createDto.State,
            PostalCode = createDto.PostalCode,
            CountryId = createDto.CountryId,
            EmailAddress = createDto.EmailAddress,
            TelephoneNumber = createDto.TelephoneNumber,
            BusinessNumber = createDto.BusinessNumber,
            MobileNumber = createDto.MobileNumber,
            Extension = createDto.Extension,
            ContractType = createDto.ContractType,
            ProbationPeriodDays = createDto.ProbationPeriodDays,
            ConfirmationDate = createDto.ConfirmationDate,
            RetirementDate = createDto.RetirementDate,
            DepartmentId = createDto.DepartmentId,
            SectionId = createDto.SectionId,
            PositionId = createDto.PositionId,
            StaffStatus = createDto.StaffStatus,
            StationId = createDto.StationId,
            TaxNumber = createDto.TaxNumber,
            BloodType = createDto.BloodType,
            ShiftId = createDto.ShiftId,
            Salary = createDto.Salary,
            BadgeNumber = createDto.BadgeNumber,
            IsActive = true
        };

        var createdEmployee = await _employeeRepository.AddAsync(employee);
        _logger.LogInformation("Employee created successfully: {EmployeeNumber} - {FullName}",
            createdEmployee.EmployeeNumber, createdEmployee.FullName);

        return MapToDto(createdEmployee);
    }

    public async Task<EmployeeDto> UpdateEmployeeAsync(Guid id, UpdateEmployeeDto updateDto)
    {
        var employee = await _employeeRepository.GetByIdAsync(id) ?? throw new InvalidOperationException($"Employee with ID {id} not found.");

        // Check email uniqueness if email is being changed
        if (!string.IsNullOrEmpty(updateDto.EmailAddress) &&
            updateDto.EmailAddress != employee.EmailAddress)
        {
            if (await _employeeRepository.EmailExistsAsync(updateDto.EmailAddress))
            {
                throw new InvalidOperationException($"Email address {updateDto.EmailAddress} already exists.");
            }
            employee.EmailAddress = updateDto.EmailAddress;
        }

        // Update fields
        if (updateDto.CorporateEmployeeID != null)
        {
            employee.CorporateEmployeeID = updateDto.CorporateEmployeeID;
        }

        if (updateDto.Title != null)
        {
            employee.Title = updateDto.Title;
        }

        if (updateDto.Gender.HasValue)
        {
            employee.Gender = updateDto.Gender;
        }

        if (updateDto.DateOfBirth.HasValue)
        {
            employee.DateOfBirth = updateDto.DateOfBirth;
        }

        if (updateDto.MaritalStatus.HasValue)
        {
            employee.MaritalStatus = updateDto.MaritalStatus;
        }

        if (updateDto.Religion != null)
        {
            employee.Religion = updateDto.Religion;
        }

        employee.IsFullTime = updateDto.IsFullTime;

        // Update contact information
        if (updateDto.Address != null)
        {
            employee.Address = updateDto.Address;
        }

        if (updateDto.City != null)
        {
            employee.City = updateDto.City;
        }

        if (updateDto.State != null)
        {
            employee.State = updateDto.State;
        }

        if (updateDto.PostalCode != null)
        {
            employee.PostalCode = updateDto.PostalCode;
        }

        if (updateDto.CountryId.HasValue)
        {
            employee.CountryId = updateDto.CountryId;
        }

        if (updateDto.TelephoneNumber != null)
        {
            employee.TelephoneNumber = updateDto.TelephoneNumber;
        }

        if (updateDto.BusinessNumber != null)
        {
            employee.BusinessNumber = updateDto.BusinessNumber;
        }

        if (updateDto.MobileNumber != null)
        {
            employee.MobileNumber = updateDto.MobileNumber;
        }

        if (updateDto.Extension != null)
        {
            employee.Extension = updateDto.Extension;
        }

        // Update employment details
        if (updateDto.ContractType.HasValue)
        {
            employee.ContractType = updateDto.ContractType.Value;
        }

        if (updateDto.ProbationPeriodDays.HasValue)
        {
            employee.ProbationPeriodDays = updateDto.ProbationPeriodDays.Value;
        }

        if (updateDto.ConfirmationDate.HasValue)
        {
            employee.ConfirmationDate = updateDto.ConfirmationDate;
        }

        if (updateDto.RetirementDate.HasValue)
        {
            employee.RetirementDate = updateDto.RetirementDate;
        }

        if (updateDto.DepartmentId.HasValue)
        {
            employee.DepartmentId = updateDto.DepartmentId.Value;
        }

        if (updateDto.SectionId.HasValue)
        {
            employee.SectionId = updateDto.SectionId;
        }

        if (updateDto.PositionId.HasValue)
        {
            employee.PositionId = updateDto.PositionId.Value;
        }

        if (updateDto.StaffStatus.HasValue)
        {
            employee.StaffStatus = updateDto.StaffStatus.Value;
        }

        if (updateDto.StationId.HasValue)
        {
            employee.StationId = updateDto.StationId;
        }

        if (updateDto.TaxNumber != null)
        {
            employee.TaxNumber = updateDto.TaxNumber;
        }

        if (updateDto.BloodType.HasValue)
        {
            employee.BloodType = updateDto.BloodType;
        }

        if (updateDto.ShiftId.HasValue)
        {
            employee.ShiftId = updateDto.ShiftId;
        }

        if (updateDto.Salary.HasValue)
        {
            employee.Salary = updateDto.Salary;
        }

        if (updateDto.BadgeNumber != null)
        {
            employee.BadgeNumber = updateDto.BadgeNumber;
        }

        if (updateDto.LastPromotionDate.HasValue)
        {
            employee.LastPromotionDate = updateDto.LastPromotionDate;
        }

        if (updateDto.LastReviewDate.HasValue)
        {
            employee.LastReviewDate = updateDto.LastReviewDate;
        }

        if (updateDto.NextReviewDate.HasValue)
        {
            employee.NextReviewDate = updateDto.NextReviewDate;
        }

        if (updateDto.IsActive.HasValue)
        {
            employee.IsActive = updateDto.IsActive.Value;
        }

        await _employeeRepository.UpdateAsync(employee);
        _logger.LogInformation("Employee updated successfully: {EmployeeNumber} - {FullName}",
            employee.EmployeeNumber, employee.FullName);

        return MapToDto(employee);
    }

    public async Task<bool> DeleteEmployeeAsync(Guid id)
    {
        await _employeeRepository.DeleteAsync(id);
        bool result = true; // Assuming successful deletion
        if (result)
        {
            _logger.LogInformation("Employee deleted successfully: {EmployeeId}", id);
        }
        return result;
    }

    public async Task<bool> DeactivateEmployeeAsync(Guid id)
    {
        var employee = await _employeeRepository.GetByIdAsync(id);
        if (employee == null)
        {
            return false;
        }

        employee.IsActive = false;
        employee.StaffStatus = StaffStatus.Inactive;
        await _employeeRepository.UpdateAsync(employee);

        _logger.LogInformation("Employee deactivated: {EmployeeNumber} - {FullName}",
            employee.EmployeeNumber, employee.FullName);
        return true;
    }

    public async Task<bool> ActivateEmployeeAsync(Guid id)
    {
        var employee = await _employeeRepository.GetByIdAsync(id);
        if (employee == null)
        {
            return false;
        }

        employee.IsActive = true;
        employee.StaffStatus = StaffStatus.Active;
        await _employeeRepository.UpdateAsync(employee);

        _logger.LogInformation("Employee activated: {EmployeeNumber} - {FullName}",
            employee.EmployeeNumber, employee.FullName);
        return true;
    }

    #endregion

    #region Employee Filtering and Grouping

    public async Task<IEnumerable<EmployeeDto>> GetByDepartmentAsync(Guid departmentId)
    {
        var employees = await _employeeRepository.GetByDepartmentAsync(departmentId);
        return employees.Select(MapToDto);
    }

    public async Task<IEnumerable<EmployeeDto>> GetBySectionAsync(Guid sectionId)
    {
        var employees = await _employeeRepository.GetBySectionAsync(sectionId);
        return employees.Select(MapToDto);
    }

    public async Task<IEnumerable<EmployeeDto>> GetByPositionAsync(Guid positionId)
    {
        var employees = await _employeeRepository.GetByPositionAsync(positionId);
        return employees.Select(MapToDto);
    }

    public async Task<IEnumerable<EmployeeDto>> GetByStatusAsync(StaffStatus status)
    {
        var employees = await _employeeRepository.GetByStatusAsync(status);
        return employees.Select(MapToDto);
    }

    public async Task<IEnumerable<EmployeeDto>> GetByContractTypeAsync(ContractType contractType)
    {
        var employees = await _employeeRepository.GetAllAsync();
        return employees.Where(e => e.ContractType == contractType).Select(MapToDto);
    }

    #endregion

    #region Maintenance Integration

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
        if (employee == null || !employee.CanBeAssignedToMaintenance)
        {
            return null;
        }

        return MapToMaintenanceTechnicianDto(employee);
    }

    public async Task<TechnicianAvailabilityDto?> GetTechnicianAvailabilityAsync(Guid employeeId)
    {
        var employee = await _employeeRepository.GetByIdAsync(employeeId);
        if (employee == null || !employee.CanBeAssignedToMaintenance)
        {
            return null;
        }

        // This would typically check against work orders, schedules, etc.
        // For now, return basic availability based on status
        return new TechnicianAvailabilityDto
        {
            EmployeeId = employee.Id,
            EmployeeName = employee.FullName,
            IsAvailable = employee.IsActive && employee.StaffStatus == StaffStatus.Active,
            AvailableFrom = employee.StaffStatus == StaffStatus.Active ? DateTime.Now : null,
            UnavailabilityReason = employee.StaffStatus != StaffStatus.Active ? employee.StaffStatus.ToString() : null,
            CurrentWorkOrders = 0, // Would come from work order service
            WorkloadPercentage = 0 // Would come from workload calculation
        };
    }

    public async Task<IEnumerable<MaintenanceTechnicianDto>> GetTechniciansWithSkillAsync(Guid skillId, SkillLevel? minLevel = null)
    {
        var employees = await _employeeRepository.GetEmployeesBySkillAsync(skillId, minLevel);
        var technicians = employees.Where(e => e.CanBeAssignedToMaintenance);
        return technicians.Select(MapToMaintenanceTechnicianDto);
    }

    #endregion

    #region Employee Validation

    public async Task<bool> EmployeeNumberExistsAsync(string employeeNumber)
    {
        return await _employeeRepository.EmployeeNumberExistsAsync(employeeNumber);
    }

    public async Task<bool> EmailExistsAsync(string email)
    {
        return await _employeeRepository.EmailExistsAsync(email);
    }

    public async Task<string> GenerateEmployeeNumberAsync()
    {
        return await _employeeRepository.GenerateEmployeeNumberAsync();
    }

    #endregion

    #region Employee Statistics

    public async Task<int> GetTotalEmployeeCountAsync()
    {
        var employees = await _employeeRepository.GetAllAsync();
        return employees.Count();
    }

    public async Task<int> GetActiveEmployeeCountAsync()
    {
        var employees = await _employeeRepository.GetActiveEmployeesAsync();
        return employees.Count();
    }

    public async Task<Dictionary<StaffStatus, int>> GetEmployeeCountByStatusAsync()
    {
        var employees = await _employeeRepository.GetAllAsync();
        return employees.GroupBy(e => e.StaffStatus)
                      .ToDictionary(g => g.Key, g => g.Count());
    }

    public async Task<Dictionary<string, int>> GetEmployeeCountByDepartmentAsync()
    {
        var employees = await _employeeRepository.GetAllAsync();
        return employees.GroupBy(e => e.Department.Name)
                      .ToDictionary(g => g.Key, g => g.Count());
    }

    #endregion

    #region Private Mapping Methods

    private static EmployeeDto MapToDto(Employee employee)
    {
        return new EmployeeDto
        {
            Id = employee.Id,
            EmployeeNumber = employee.EmployeeNumber,
            CorporateEmployeeID = employee.CorporateEmployeeID,
            FirstName = employee.FirstName,
            MiddleName = employee.MiddleName,
            LastName = employee.LastName,
            FullName = employee.FullName,
            DisplayName = employee.DisplayName,
            Title = employee.Title,
            Gender = employee.Gender,
            EmailAddress = employee.EmailAddress,
            MobileNumber = employee.MobileNumber,
            DepartmentName = employee.Department?.Name ?? string.Empty,
            SectionName = employee.Section?.Name,
            PositionTitle = employee.Position?.Title ?? string.Empty,
            StaffStatus = employee.StaffStatus,
            ContractType = employee.ContractType,
            IsActive = employee.IsActive,
            IsFullTime = employee.IsFullTime,
            DateEmployed = employee.DateEmployed,
            YearsOfService = employee.YearsOfService,
            CanBeAssignedToMaintenance = employee.CanBeAssignedToMaintenance,
            PicturePath = employee.PicturePath
        };
    }

    private static EmployeeDetailDto MapToDetailDto(Employee employee)
    {
        var basicDto = MapToDto(employee);
        return new EmployeeDetailDto
        {
            Id = basicDto.Id,
            EmployeeNumber = basicDto.EmployeeNumber,
            CorporateEmployeeID = basicDto.CorporateEmployeeID,
            FirstName = basicDto.FirstName,
            MiddleName = basicDto.MiddleName,
            LastName = basicDto.LastName,
            FullName = basicDto.FullName,
            DisplayName = basicDto.DisplayName,
            Title = basicDto.Title,
            Gender = basicDto.Gender,
            EmailAddress = basicDto.EmailAddress,
            MobileNumber = basicDto.MobileNumber,
            DepartmentName = basicDto.DepartmentName,
            SectionName = basicDto.SectionName,
            PositionTitle = basicDto.PositionTitle,
            StaffStatus = basicDto.StaffStatus,
            ContractType = basicDto.ContractType,
            IsActive = basicDto.IsActive,
            IsFullTime = basicDto.IsFullTime,
            DateEmployed = basicDto.DateEmployed,
            YearsOfService = basicDto.YearsOfService,
            CanBeAssignedToMaintenance = basicDto.CanBeAssignedToMaintenance,
            PicturePath = basicDto.PicturePath,

            // Additional detail fields
            DateOfBirth = employee.DateOfBirth,
            MaritalStatus = employee.MaritalStatus,
            Religion = employee.Religion,
            Address = employee.Address,
            City = employee.City,
            State = employee.State,
            PostalCode = employee.PostalCode,
            CountryName = employee.Country?.Name,
            TelephoneNumber = employee.TelephoneNumber,
            BusinessNumber = employee.BusinessNumber,
            Extension = employee.Extension,
            ProbationPeriodDays = employee.ProbationPeriodDays,
            ConfirmationDate = employee.ConfirmationDate,
            RetirementDate = employee.RetirementDate,
            TaxNumber = employee.TaxNumber,
            BloodType = employee.BloodType,
            ShiftName = employee.Shift?.Name,
            Salary = employee.Salary,
            BadgeNumber = employee.BadgeNumber,
            LastPromotionDate = employee.LastPromotionDate,
            LastReviewDate = employee.LastReviewDate,
            NextReviewDate = employee.NextReviewDate,
            StationName = employee.Station?.Name,

            // Related collections would be mapped here
            EmergencyContacts = employee.EmergencyContacts?.Select(ec => new EmployeeEmergencyContactDto
            {
                Id = ec.Id,
                EmployeeId = ec.EmployeeId,
                FirstName = ec.FirstName,
                LastName = ec.LastName,
                Relationship = ec.Relationship,
                PhoneNumber = ec.PhoneNumber,
                AlternatePhoneNumber = ec.AlternatePhoneNumber,
                EmailAddress = ec.EmailAddress,
                Address = ec.Address,
                IsPrimary = ec.IsPrimary
            }).ToList() ?? new List<EmployeeEmergencyContactDto>(),

            Skills = employee.Skills?.Select(es => new EmployeeSkillDto
            {
                Id = es.Id,
                EmployeeId = es.EmployeeId,
                SkillId = es.SkillId,
                SkillName = es.Skill.Name,
                SkillCategory = es.Skill.Category,
                SkillLevel = es.SkillLevel,
                AcquiredDate = es.AcquiredDate,
                CertificationDate = es.CertificationDate,
                CertificationExpiryDate = es.CertificationExpiryDate,
                CertificationNumber = es.CertificationNumber,
                CertifyingBody = es.CertifyingBody,
                IsVerified = es.IsVerified,
                IsCertificationExpired = es.CertificationExpiryDate.HasValue &&
                                       es.CertificationExpiryDate < DateOnly.FromDateTime(DateTime.Now),
                Notes = es.Notes
            }).ToList() ?? new List<EmployeeSkillDto>()
        };
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
            Skills = employee.Skills?.Select(es => new MaintenanceDTOs.UserTechnicianSkillDto
            {
                SkillName = es.Skill.Name,
                Level = (int)es.SkillLevel,
                IsCertified = es.CertificationDate.HasValue
            }).ToList() ?? new List<MaintenanceDTOs.UserTechnicianSkillDto>(),
            CurrentWorkOrders = 0, // Would come from work order service
            WorkloadScore = 0, // Would come from workload calculation
            BadgeNumber = employee.BadgeNumber,
            ShiftName = employee.Shift?.Name
        };
    }

    #endregion
}
