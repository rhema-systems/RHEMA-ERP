using ErpSystem.Core.Enums;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Interfaces;

namespace ErpSystem.Core.Interfaces.HR;

#region Employee Management Repositories

/// <summary>
/// Repository interface for employee operations
/// </summary>
public interface IEmployeeRepository : IGenericRepository<Employee>
{
    Task<Employee?> GetByEmployeeNumberAsync(string employeeNumber);
    Task<Employee?> GetByEmployeeNumberWithDetailsAsync(string employeeNumber);
    Task<Employee?> GetByEmployeeNumberWithFullProfileAsync(string employeeNumber);
    Task<Employee?> GetByEmailAsync(string email);
    Task<Employee?> GetByIdWithDetailsAsync(Guid id);
    Task<Employee?> GetByApplicationUserIdAsync(Guid applicationUserId);
    Task<Employee?> GetByIdWithFullProfileAsync(Guid id);
    Task<IEnumerable<Employee>> GetActiveEmployeesAsync();
    Task<IEnumerable<Employee>> GetByOrganizationUnitAsync(Guid organizationUnitId);
    Task<IEnumerable<Employee>> GetByOrganizationLevelAsync(Guid organizationLevelId);
    Task<IEnumerable<Employee>> GetByStationAsync(Guid stationId);
    Task<IEnumerable<Employee>> GetByDivisionAsync(Guid divisionId);
    Task<IEnumerable<Employee>> GetByDepartmentAsync(Guid departmentId);
    Task<IEnumerable<Employee>> GetBySectionAsync(Guid sectionId);
    Task<IEnumerable<Employee>> GetByUnitAsync(Guid unitId);
    Task<IEnumerable<Employee>> GetByPositionAsync(Guid positionId);
    Task<IEnumerable<Employee>> GetByManagerAsync(Guid managerId);
    Task<IEnumerable<Employee>> GetByStatusAsync(StaffStatus status);
    Task<IEnumerable<Employee>> GetByContractTypeAsync(ContractType contractType);
    Task<IEnumerable<Employee>> SearchEmployeesAsync(string searchTerm);
    Task<Employee?> GetEmployeeWithPositionHistoryAsync(Guid employeeId);
    Task<IEnumerable<Employee>> GetMaintenanceTechniciansAsync();
    Task<IEnumerable<Employee>> GetAvailableTechniciansAsync();
    Task<IEnumerable<Employee>> GetEmployeesBySkillAsync(Guid skillId, SkillLevel? minLevel = null);
    Task<Employee?> GetByBadgeNumberAsync(string badgeNumber);
    Task<Employee?> GetByTaxNumberAsync(string taxNumber);
    Task<Employee?> GetBySocialSecurityNumberAsync(string socialSecurityNumber);
    Task<Employee?> GetByTinNumberAsync(string tinNumber);
    Task<bool> EmployeeNumberExistsAsync(string employeeNumber);
    Task<bool> EmployeeNumberExistsAsync(string employeeNumber, Guid excludeEmployeeId);
    Task<bool> EmailExistsAsync(string email);
    Task<bool> EmailExistsAsync(string email, Guid excludeEmployeeId);
    Task<bool> BadgeNumberExistsAsync(string badgeNumber);
    Task<bool> BadgeNumberExistsAsync(string badgeNumber, Guid excludeEmployeeId);
    Task<bool> TaxNumberExistsAsync(string taxNumber);
    Task<bool> TaxNumberExistsAsync(string taxNumber, Guid excludeEmployeeId);
    Task<bool> SocialSecurityNumberExistsAsync(string socialSecurityNumber);
    Task<bool> SocialSecurityNumberExistsAsync(string socialSecurityNumber, Guid excludeEmployeeId);
    Task<bool> TinNumberExistsAsync(string tinNumber);
    Task<bool> TinNumberExistsAsync(string tinNumber, Guid excludeEmployeeId);
    Task<string> GenerateEmployeeNumberAsync();    
}

/// <summary>
/// Repository interface for employee emergency contact operations
/// </summary>
public interface IEmployeeEmergencyContactRepository : IGenericRepository<EmployeeEmergencyContact>
{
    Task<IEnumerable<EmployeeEmergencyContact>> GetByEmployeeAsync(Guid employeeId);
    Task<EmployeeEmergencyContact?> GetPrimaryContactAsync(Guid employeeId);
    Task<IEnumerable<EmployeeEmergencyContact>> GetByRelationshipAsync(Guid employeeId, string relationship);
}

/// <summary>
/// Repository interface for employee dependent operations
/// </summary>
public interface IEmployeeDependentRepository : IGenericRepository<EmployeeDependent>
{
    Task<IEnumerable<EmployeeDependent>> GetByEmployeeAsync(Guid employeeId);
    Task<IEnumerable<EmployeeDependent>> GetByRelationshipAsync(Guid employeeId, string relationship);
    Task<IEnumerable<EmployeeDependent>> GetStudentDependentsAsync(Guid employeeId);
}

/// <summary>
/// Repository interface for employee qualification operations
/// </summary>
public interface IEmployeeQualificationRepository : IGenericRepository<EmployeeQualification>
{
    Task<IEnumerable<EmployeeQualification>> GetByEmployeeAsync(Guid employeeId);
    Task<IEnumerable<EmployeeQualification>> GetVerifiedQualificationsAsync(Guid employeeId);
    Task<IEnumerable<EmployeeQualification>> GetByInstitutionAsync(string institution);
}

/// <summary>
/// Repository interface for employee identification card operations
/// </summary>
public interface IEmployeeIdentificationCardRepository : IGenericRepository<EmployeeIdentificationCard>
{
    Task<IEnumerable<EmployeeIdentificationCard>> GetByEmployeeAsync(Guid employeeId);
    Task<EmployeeIdentificationCard?> GetByDocumentNumberAsync(string documentNumber);
    Task<IEnumerable<EmployeeIdentificationCard>> GetExpiringDocumentsAsync(DateTime withinDays);
}

/// <summary>
/// Repository interface for employee work history operations
/// </summary>
public interface IEmployeeWorkHistoryRepository : IGenericRepository<EmployeeWorkHistory>
{
    Task<IEnumerable<EmployeeWorkHistory>> GetByEmployeeAsync(Guid employeeId);
    Task<IEnumerable<EmployeeWorkHistory>> GetByCompanyAsync(string companyName);
}

/// <summary>
/// Repository interface for employee contract detail operations
/// </summary>
public interface IEmployeeContractDetailRepository : IGenericRepository<EmployeeContractDetail>
{
    Task<IEnumerable<EmployeeContractDetail>> GetByEmployeeAsync(Guid employeeId);
    Task<EmployeeContractDetail?> GetActiveContractAsync(Guid employeeId);
    Task<IEnumerable<EmployeeContractDetail>> GetExpiringContractsAsync(DateTime withinDate);
    Task<EmployeeContractDetail?> GetByContractNumberAsync(string contractNumber);
    Task<bool> ContractNumberExistsAsync(string contractNumber);
}

/// <summary>
/// Repository interface for employee skill operations
/// </summary>
public interface IEmployeeSkillRepository : IGenericRepository<EmployeeSkill>
{
    Task<IEnumerable<EmployeeSkill>> GetByEmployeeAsync(Guid employeeId);
    Task<IEnumerable<EmployeeSkill>> GetBySkillAsync(Guid skillId);
    Task<EmployeeSkill?> GetByEmployeeAndSkillAsync(Guid employeeId, Guid skillId);
    Task<IEnumerable<EmployeeSkill>> GetVerifiedSkillsAsync(Guid employeeId);
    Task<IEnumerable<EmployeeSkill>> GetExpiringCertificationsAsync(DateTime withinDate);
    Task<IEnumerable<EmployeeSkill>> GetBySkillLevelAsync(SkillLevel level);
}

/// <summary>
/// Repository interface for employee biometric operations
/// </summary>
public interface IEmployeeBiometricRepository : IGenericRepository<EmployeeBiometric>
{
    Task<IEnumerable<EmployeeBiometric>> GetByEmployeeAsync(Guid employeeId);
    Task<IEnumerable<EmployeeBiometric>> GetActiveByEmployeeAsync(Guid employeeId);
    Task<IEnumerable<EmployeeBiometric>> GetByTypeAsync(string biometricType);
    Task<EmployeeBiometric?> GetByEmployeeAndTypeAsync(Guid employeeId, string biometricType);
}

/// <summary>
/// Repository interface for employee shift preference operations
/// </summary>
public interface IEmployeeShiftPreferenceRepository : IGenericRepository<EmployeeShiftPreference>
{
    Task<IEnumerable<EmployeeShiftPreference>> GetByEmployeeAsync(Guid employeeId);
    Task<IEnumerable<EmployeeShiftPreference>> GetByShiftAsync(Guid shiftId);
    Task<EmployeeShiftPreference?> GetByEmployeeAndShiftAsync(Guid employeeId, Guid shiftId);
    Task<IEnumerable<EmployeeShiftPreference>> GetOrderedPreferencesAsync(Guid employeeId);
}

#endregion Employee Management Repositories

#region Organizational Structure Repositories

/// <summary>
/// Repository interface for department operations
/// </summary>
public interface IDepartmentRepository : IGenericRepository<Department>
{
    Task<IEnumerable<Department>> GetActiveDepartmentsAsync();
    Task<IEnumerable<Department>> GetRootDepartmentsAsync();
    Task<IEnumerable<Department>> GetSubDepartmentsAsync(Guid parentDepartmentId);
    Task<Department?> GetByCodeAsync(string code);
    Task<Department?> GetByTypeAsync(DepartmentType departmentType);
    Task<IEnumerable<Department>> GetByTypeAsync(IEnumerable<DepartmentType> departmentTypes);
    Task<Department?> GetWithEmployeesAsync(Guid id);
    Task<bool> CodeExistsAsync(string code);
}

/// <summary>
/// Repository interface for section operations
/// </summary>
public interface ISectionRepository : IGenericRepository<Section>
{
    Task<IEnumerable<Section>> GetActiveSectionsAsync();
    Task<IEnumerable<Section>> GetByDepartmentAsync(Guid departmentId);
    Task<Section?> GetByCodeAsync(string code);
    Task<Section?> GetWithEmployeesAsync(Guid id);
    Task<bool> CodeExistsAsync(string code);
}

/// <summary>
/// Repository interface for employee position operations
/// </summary>
public interface IEmployeePositionRepository : IGenericRepository<EmployeePosition>
{
    Task<IEnumerable<EmployeePosition>> GetActivePositionsAsync();
    Task<IEnumerable<EmployeePosition>> GetByDepartmentAsync(Guid departmentId);
    Task<EmployeePosition?> GetByCodeAsync(string code);
    Task<EmployeePosition?> GetWithSkillRequirementsAsync(Guid id);
    Task<IEnumerable<EmployeePosition>> GetByLevelAsync(int level);
    Task<bool> CodeExistsAsync(string code);
}

/// <summary>
/// Repository interface for position skill requirement operations
/// </summary>
public interface IPositionSkillRequirementRepository : IGenericRepository<PositionSkillRequirement>
{
    Task<IEnumerable<PositionSkillRequirement>> GetByPositionAsync(Guid positionId);
    Task<IEnumerable<PositionSkillRequirement>> GetRequiredSkillsAsync(Guid positionId);
    Task<IEnumerable<PositionSkillRequirement>> GetBySkillAsync(Guid skillId);
    Task<PositionSkillRequirement?> GetByPositionAndSkillAsync(Guid positionId, Guid skillId);
}

/// <summary>
/// Repository interface for work station operations
/// </summary>
public interface IWorkStationRepository : IGenericRepository<WorkStation>
{
    Task<IEnumerable<WorkStation>> GetActiveStationsAsync();
    Task<IEnumerable<WorkStation>> GetByDepartmentAsync(Guid departmentId);
    Task<WorkStation?> GetByCodeAsync(string code);
    Task<bool> CodeExistsAsync(string code);
}

#endregion Organizational Structure Repositories

#region Attendance Management Repositories

/// <summary>
/// Repository interface for shift operations
/// </summary>
public interface IShiftRepository : IGenericRepository<Shift>
{
    Task<IEnumerable<Shift>> GetActiveShiftsAsync();
    Task<Shift?> GetByNameAsync(string name);
    Task<IEnumerable<Shift>> GetOverlappingShiftsAsync(TimeOnly startTime, TimeOnly endTime);
    Task<bool> NameExistsAsync(string name);
}

/// <summary>
/// Repository interface for attendance record operations
/// </summary>
public interface IAttendanceRecordRepository : IGenericRepository<AttendanceRecord>
{
    Task<IEnumerable<AttendanceRecord>> GetByEmployeeAsync(Guid employeeId);
    Task<IEnumerable<AttendanceRecord>> GetByDateRangeAsync(Guid employeeId, DateOnly startDate, DateOnly endDate);
    Task<AttendanceRecord?> GetByEmployeeAndDateAsync(Guid employeeId, DateOnly date);
    Task<IEnumerable<AttendanceRecord>> GetByDateAsync(DateOnly date);
    Task<IEnumerable<AttendanceRecord>> GetAbsentEmployeesAsync(DateOnly date);
    Task<IEnumerable<AttendanceRecord>> GetLateEmployeesAsync(DateOnly date);
}

/// <summary>
/// Repository interface for shift assignment operations
/// </summary>
public interface IShiftAssignmentRepository : IGenericRepository<ShiftAssignment>
{
    Task<IEnumerable<ShiftAssignment>> GetByEmployeeAsync(Guid employeeId);
    Task<IEnumerable<ShiftAssignment>> GetByShiftAsync(Guid shiftId);
    Task<ShiftAssignment?> GetActiveAssignmentAsync(Guid employeeId);
    Task<IEnumerable<ShiftAssignment>> GetActiveAssignmentsAsync();
    Task<IEnumerable<ShiftAssignment>> GetByDateRangeAsync(DateOnly startDate, DateOnly endDate);
}
#endregion Attendance Management Repositories

#region Misc Repositories
/// <summary>
/// Repository interface for skill operations
/// </summary>
public interface ISkillRepository : IGenericRepository<Skill>
{
    Task<IEnumerable<Skill>> GetActiveSkillsAsync();
    Task<IEnumerable<Skill>> GetByCategoryAsync(string category);
    Task<IEnumerable<Skill>> GetCertificationRequiredSkillsAsync();
    Task<Skill?> GetByNameAsync(string name);
    Task<bool> NameExistsAsync(string name);
}

/// <summary>
/// Repository interface for country operations
/// </summary>
public interface ICountryRepository : IGenericRepository<Country>
{
    Task<IEnumerable<Country>> GetActiveCountriesAsync();
    Task<Country?> GetByCodeAsync(string code);
    Task<Country?> GetByAlpha2CodeAsync(string alpha2Code);
    Task<bool> CodeExistsAsync(string code);
    Task<bool> Alpha2CodeExistsAsync(string alpha2Code);
}
#endregion Misc Repositories