using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Entities.HR;

namespace ErpSystem.Core.Interfaces.HR;

/// <summary>
/// Service interface for employee operations
/// </summary>
public interface IEmployeeService
{
    // Employee CRUD operations
    Task<EmployeeDto?> GetByIdAsync(Guid id);
    Task<EmployeeDetailDto?> GetDetailsByIdAsync(Guid id);
    Task<EmployeeDto?> GetByEmployeeNumberAsync(string employeeNumber);
    Task<IEnumerable<EmployeeDto>> GetAllAsync();
    Task<IEnumerable<EmployeeDto>> GetActiveEmployeesAsync();
    Task<IEnumerable<EmployeeDto>> SearchEmployeesAsync(EmployeeSearchDto searchCriteria);
    Task<EmployeeDto> CreateEmployeeAsync(CreateEmployeeDto createDto);
    Task<EmployeeDto> UpdateEmployeeAsync(Guid id, UpdateEmployeeDto updateDto);
    Task<bool> DeleteEmployeeAsync(Guid id);
    Task<bool> DeactivateEmployeeAsync(Guid id);
    Task<bool> ActivateEmployeeAsync(Guid id);

    // Employee filtering and grouping
    Task<IEnumerable<EmployeeDto>> GetByDepartmentAsync(Guid departmentId);
    Task<IEnumerable<EmployeeDto>> GetBySectionAsync(Guid sectionId);
    Task<IEnumerable<EmployeeDto>> GetByPositionAsync(Guid positionId);
    Task<IEnumerable<EmployeeDto>> GetByStatusAsync(StaffStatus status);
    Task<IEnumerable<EmployeeDto>> GetByContractTypeAsync(ContractType contractType);

    // Maintenance integration
    Task<IEnumerable<MaintenanceTechnicianDto>> GetMaintenanceTechniciansAsync();
    Task<IEnumerable<MaintenanceTechnicianDto>> GetAvailableTechniciansAsync();
    Task<MaintenanceTechnicianDto?> GetTechnicianByIdAsync(Guid employeeId);
    Task<TechnicianAvailabilityDto?> GetTechnicianAvailabilityAsync(Guid employeeId);
    Task<IEnumerable<MaintenanceTechnicianDto>> GetTechniciansWithSkillAsync(Guid skillId, SkillLevel? minLevel = null);

    // Employee validation
    Task<bool> EmployeeNumberExistsAsync(string employeeNumber);
    Task<bool> EmailExistsAsync(string email);
    Task<string> GenerateEmployeeNumberAsync();

    // Employee statistics
    Task<int> GetTotalEmployeeCountAsync();
    Task<int> GetActiveEmployeeCountAsync();
    Task<Dictionary<StaffStatus, int>> GetEmployeeCountByStatusAsync();
    Task<Dictionary<string, int>> GetEmployeeCountByDepartmentAsync();
}

/// <summary>
/// Service interface for department operations
/// </summary>
public interface IDepartmentService
{
    Task<DepartmentDto?> GetByIdAsync(Guid id);
    Task<IEnumerable<DepartmentDto>> GetAllAsync();
    Task<IEnumerable<DepartmentDto>> GetActiveDepartmentsAsync();
    Task<IEnumerable<DepartmentDto>> GetRootDepartmentsAsync();
    Task<IEnumerable<DepartmentDto>> GetSubDepartmentsAsync(Guid parentDepartmentId);
    Task<DepartmentDto?> GetByCodeAsync(string code);
    Task<DepartmentDto> CreateDepartmentAsync(CreateDepartmentDto createDto);
    Task<DepartmentDto> UpdateDepartmentAsync(Guid id, CreateDepartmentDto updateDto);
    Task<bool> DeleteDepartmentAsync(Guid id);
    Task<bool> CodeExistsAsync(string code);
}

/// <summary>
/// Service interface for section operations
/// </summary>
public interface ISectionService
{
    Task<SectionDto?> GetByIdAsync(Guid id);
    Task<IEnumerable<SectionDto>> GetAllAsync();
    Task<IEnumerable<SectionDto>> GetActiveSectionsAsync();
    Task<IEnumerable<SectionDto>> GetByDepartmentAsync(Guid departmentId);
    Task<SectionDto?> GetByCodeAsync(string code);
    Task<SectionDto> CreateSectionAsync(CreateSectionDto createDto);
    Task<SectionDto> UpdateSectionAsync(Guid id, CreateSectionDto updateDto);
    Task<bool> DeleteSectionAsync(Guid id);
    Task<bool> CodeExistsAsync(string code);
}

/// <summary>
/// Service interface for employee position operations
/// </summary>
public interface IEmployeePositionService
{
    Task<EmployeePositionDto?> GetByIdAsync(Guid id);
    Task<IEnumerable<EmployeePositionDto>> GetAllAsync();
    Task<IEnumerable<EmployeePositionDto>> GetActivePositionsAsync();
    Task<IEnumerable<EmployeePositionDto>> GetByDepartmentAsync(Guid departmentId);
    Task<EmployeePositionDto?> GetByCodeAsync(string code);
    Task<EmployeePositionDto> CreatePositionAsync(CreateEmployeePositionDto createDto);
    Task<EmployeePositionDto> UpdatePositionAsync(Guid id, CreateEmployeePositionDto updateDto);
    Task<bool> DeletePositionAsync(Guid id);
    Task<bool> CodeExistsAsync(string code);
}

/// <summary>
/// Service interface for skill operations
/// </summary>
public interface ISkillService
{
    Task<SkillDto?> GetByIdAsync(Guid id);
    Task<IEnumerable<SkillDto>> GetAllAsync();
    Task<IEnumerable<SkillDto>> GetActiveSkillsAsync();
    Task<IEnumerable<SkillDto>> GetByCategoryAsync(string category);
    Task<SkillDto?> GetByNameAsync(string name);
    Task<SkillDto> CreateSkillAsync(CreateSkillDto createDto);
    Task<SkillDto> UpdateSkillAsync(Guid id, CreateSkillDto updateDto);
    Task<bool> DeleteSkillAsync(Guid id);
    Task<bool> NameExistsAsync(string name);
    Task<IEnumerable<string>> GetSkillCategoriesAsync();
}

/// <summary>
/// Service interface for employee skill operations
/// </summary>
public interface IEmployeeSkillService
{
    Task<IEnumerable<EmployeeSkillDto>> GetByEmployeeAsync(Guid employeeId);
    Task<IEnumerable<EmployeeSkillDto>> GetVerifiedSkillsAsync(Guid employeeId);
    Task<IEnumerable<EmployeeSkillDto>> GetExpiringCertificationsAsync(DateTime withinDate);
    Task<EmployeeSkillDto?> GetByEmployeeAndSkillAsync(Guid employeeId, Guid skillId);
    Task<EmployeeSkillDto> CreateEmployeeSkillAsync(CreateEmployeeSkillDto createDto);
    Task<EmployeeSkillDto> UpdateEmployeeSkillAsync(Guid id, CreateEmployeeSkillDto updateDto);
    Task<bool> DeleteEmployeeSkillAsync(Guid id);
    Task<bool> VerifySkillCertificationAsync(Guid id);
    Task<bool> RenewCertificationAsync(Guid id, DateOnly newExpiryDate);
}

/// <summary>
/// Service interface for employee emergency contact operations
/// </summary>
public interface IEmployeeEmergencyContactService
{
    Task<IEnumerable<EmployeeEmergencyContactDto>> GetByEmployeeAsync(Guid employeeId);
    Task<EmployeeEmergencyContactDto?> GetPrimaryContactAsync(Guid employeeId);
    Task<EmployeeEmergencyContactDto> CreateEmergencyContactAsync(CreateEmployeeEmergencyContactDto createDto);
    Task<EmployeeEmergencyContactDto> UpdateEmergencyContactAsync(Guid id, CreateEmployeeEmergencyContactDto updateDto);
    Task<bool> DeleteEmergencyContactAsync(Guid id);
    Task<bool> SetPrimaryContactAsync(Guid id);
}

/// <summary>
/// Service interface for employee dependent operations
/// </summary>
public interface IEmployeeDependentService
{
    Task<IEnumerable<EmployeeDependentDto>> GetByEmployeeAsync(Guid employeeId);
    Task<IEnumerable<EmployeeDependentDto>> GetStudentDependentsAsync(Guid employeeId);
    Task<EmployeeDependentDto> CreateDependentAsync(CreateEmployeeDependentDto createDto);
    Task<EmployeeDependentDto> UpdateDependentAsync(Guid id, CreateEmployeeDependentDto updateDto);
    Task<bool> DeleteDependentAsync(Guid id);
}

/// <summary>
/// Service interface for employee qualification operations
/// </summary>
public interface IEmployeeQualificationService
{
    Task<IEnumerable<EmployeeQualificationDto>> GetByEmployeeAsync(Guid employeeId);
    Task<IEnumerable<EmployeeQualificationDto>> GetVerifiedQualificationsAsync(Guid employeeId);
    Task<EmployeeQualificationDto> CreateQualificationAsync(CreateEmployeeQualificationDto createDto);
    Task<EmployeeQualificationDto> UpdateQualificationAsync(Guid id, CreateEmployeeQualificationDto updateDto);
    Task<bool> DeleteQualificationAsync(Guid id);
    Task<bool> VerifyQualificationAsync(Guid id);
}

/// <summary>
/// Service interface for employee contract operations
/// </summary>
public interface IEmployeeContractService
{
    Task<IEnumerable<EmployeeContractDetailDto>> GetByEmployeeAsync(Guid employeeId);
    Task<EmployeeContractDetailDto?> GetActiveContractAsync(Guid employeeId);
    Task<IEnumerable<EmployeeContractDetailDto>> GetExpiringContractsAsync(DateTime withinDate);
    Task<EmployeeContractDetailDto> CreateContractAsync(EmployeeContractDetailDto createDto);
    Task<EmployeeContractDetailDto> UpdateContractAsync(Guid id, EmployeeContractDetailDto updateDto);
    Task<bool> DeleteContractAsync(Guid id);
    Task<bool> ActivateContractAsync(Guid id);
    Task<bool> DeactivateContractAsync(Guid id);
    Task<string> GenerateContractNumberAsync();
}

/// <summary>
/// Service interface for work station operations
/// </summary>
public interface IWorkStationService
{
    Task<WorkStationDto?> GetByIdAsync(Guid id);
    Task<IEnumerable<WorkStationDto>> GetAllAsync();
    Task<IEnumerable<WorkStationDto>> GetActiveStationsAsync();
    Task<IEnumerable<WorkStationDto>> GetByDepartmentAsync(Guid departmentId);
    Task<WorkStationDto?> GetByCodeAsync(string code);
    Task<WorkStationDto> CreateStationAsync(WorkStationDto createDto);
    Task<WorkStationDto> UpdateStationAsync(Guid id, WorkStationDto updateDto);
    Task<bool> DeleteStationAsync(Guid id);
    Task<bool> CodeExistsAsync(string code);
}

/// <summary>
/// Service interface for shift operations
/// </summary>
public interface IShiftService
{
    Task<ShiftDto?> GetByIdAsync(Guid id);
    Task<IEnumerable<ShiftDto>> GetAllAsync();
    Task<IEnumerable<ShiftDto>> GetActiveShiftsAsync();
    Task<ShiftDto?> GetByNameAsync(string name);
    Task<ShiftDto> CreateShiftAsync(ShiftDto createDto);
    Task<ShiftDto> UpdateShiftAsync(Guid id, ShiftDto updateDto);
    Task<bool> DeleteShiftAsync(Guid id);
    Task<bool> NameExistsAsync(string name);
    Task<bool> HasOverlappingShiftsAsync(TimeOnly startTime, TimeOnly endTime, Guid? excludeId = null);
}

/// <summary>
/// Service interface for country operations
/// </summary>
public interface ICountryService
{
    Task<CountryDto?> GetByIdAsync(Guid id);
    Task<IEnumerable<CountryDto>> GetAllAsync();
    Task<IEnumerable<CountryDto>> GetActiveCountriesAsync();
    Task<CountryDto?> GetByCodeAsync(string code);
    Task<CountryDto?> GetByAlpha2CodeAsync(string alpha2Code);
}

/// <summary>
/// Service interface for attendance operations
/// </summary>
public interface IAttendanceService
{
    Task<IEnumerable<AttendanceRecord>> GetByEmployeeAsync(Guid employeeId);
    Task<IEnumerable<AttendanceRecord>> GetByDateRangeAsync(Guid employeeId, DateOnly startDate, DateOnly endDate);
    Task<AttendanceRecord?> GetByEmployeeAndDateAsync(Guid employeeId, DateOnly date);
    Task<IEnumerable<AttendanceRecord>> GetByDateAsync(DateOnly date);
    Task<AttendanceRecord> RecordCheckInAsync(Guid employeeId, TimeOnly checkInTime, DateOnly? date = null);
    Task<AttendanceRecord> RecordCheckOutAsync(Guid employeeId, TimeOnly checkOutTime, DateOnly? date = null);
    Task<IEnumerable<AttendanceRecord>> GetAbsentEmployeesAsync(DateOnly date);
    Task<IEnumerable<AttendanceRecord>> GetLateEmployeesAsync(DateOnly date);
    Task<Dictionary<string, object>> GetAttendanceSummaryAsync(Guid employeeId, DateOnly startDate, DateOnly endDate);
}

/// <summary>
/// Service interface for HR reporting and analytics
/// </summary>
public interface IHRReportingService
{
    // Employee statistics
    Task<Dictionary<string, object>> GetEmployeeStatisticsAsync();
    Task<Dictionary<string, object>> GetDepartmentStatisticsAsync();
    Task<Dictionary<string, object>> GetSkillStatisticsAsync();
    
    // Turnover and retention
    Task<Dictionary<string, object>> GetTurnoverAnalysisAsync(int months = 12);
    Task<IEnumerable<EmployeeDto>> GetNewHiresAsync(DateOnly fromDate, DateOnly? toDate = null);
    Task<IEnumerable<EmployeeDto>> GetTerminatedEmployeesAsync(DateOnly fromDate, DateOnly? toDate = null);
    
    // Certification and compliance
    Task<IEnumerable<EmployeeSkillDto>> GetExpiringCertificationsAsync(int withinDays = 30);
    Task<IEnumerable<EmployeeDto>> GetEmployeesWithoutRequiredSkillsAsync(Guid positionId);
    
    // Contract management
    Task<IEnumerable<EmployeeContractDetailDto>> GetExpiringContractsAsync(int withinDays = 60);
    Task<IEnumerable<EmployeeDto>> GetProbationaryEmployeesAsync();
    
    // Organizational analysis
    Task<Dictionary<string, object>> GetOrganizationalChartDataAsync();
    Task<Dictionary<string, object>> GetHeadcountByDepartmentAsync();
    Task<Dictionary<string, object>> GetSalaryAnalysisAsync(Guid? departmentId = null);
}
