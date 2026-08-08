using ErpSystem.Core.Enums;
using ErpSystem.Core.Entities.HR;

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
    Task<Employee?> GetByIdWithFullProfileAsync(Guid id);
    Task<IEnumerable<Employee>> GetActiveEmployeesAsync();

    // Organizational assignment (preferred over legacy Department/Section)
    Task<IEnumerable<Employee>> GetByOrganizationUnitAsync(Guid organizationUnitId);
    Task<IEnumerable<Employee>> GetByOrganizationLevelAsync(Guid organizationLevelId);

    // Legacy organizational structure (kept for backward compatibility)
    Task<IEnumerable<Employee>> GetByDepartmentAsync(Guid departmentId);
    Task<IEnumerable<Employee>> GetBySectionAsync(Guid sectionId);
    Task<IEnumerable<Employee>> GetByPositionAsync(Guid positionId);
    Task<IEnumerable<Employee>> GetByManagerAsync(Guid managerId);
    Task<IEnumerable<Employee>> GetByStatusAsync(StaffStatus status);
    Task<IEnumerable<Employee>> GetByEmploymentTypeAsync(EmploymentType employmentType);
    Task<IEnumerable<Employee>> SearchEmployeesAsync(string searchTerm);
    Task<Employee?> GetEmployeeWithPositionHistoryAsync(Guid employeeId);
    Task<IEnumerable<Employee>> GetMaintenanceTechniciansAsync();
    Task<IEnumerable<Employee>> GetAvailableTechniciansAsync();
    Task<IEnumerable<Employee>> GetEmployeesBySkillAsync(Guid skillId, SkillLevel? minLevel = null);

    // Common identifier lookups (UI validations, payroll/HR integrations)
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
/// Repository interface for employee address/contact record operations
/// </summary>
public interface IEmployeeContactRepository : IGenericRepository<EmployeeContact>
{
    Task<IEnumerable<EmployeeContact>> GetByEmployeeAsync(Guid employeeId);
    Task<EmployeeContact?> GetPrimaryContactAsync(Guid employeeId);
    Task<IEnumerable<EmployeeContact>> GetByTypeAsync(Guid employeeId, EmployeeContactType contactType);
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
/// Repository interface for employee position history operations.
/// </summary>
public interface IEmployeePositionHistoryRepository : IGenericRepository<EmployeePositionHistory>
{
    Task<IEnumerable<EmployeePositionHistory>> GetByEmployeeAsync(Guid employeeId);
    Task<EmployeePositionHistory?> GetCurrentAsync(Guid employeeId);
}

/// <summary>
/// Repository interface for employee salary assignment operations.
/// </summary>
public interface IEmployeeSalaryAssignmentRepository : IGenericRepository<EmployeeSalaryAssignment>
{
    Task<IEnumerable<EmployeeSalaryAssignment>> GetByEmployeeAsync(Guid employeeId);
    Task<EmployeeSalaryAssignment?> GetCurrentAsync(Guid employeeId);
}

/// <summary>
/// Repository interface for employee referee operations.
/// </summary>
public interface IEmployeeRefereeRepository : IGenericRepository<EmployeeReferee>
{
    Task<IEnumerable<EmployeeReferee>> GetByEmployeeAsync(Guid employeeId);
}

/// <summary>
/// Repository interface for employee bank detail operations
/// </summary>
public interface IEmployeeBankDetailRepository : IGenericRepository<EmployeeBankDetail>
{
    Task<IEnumerable<EmployeeBankDetail>> GetByEmployeeAsync(Guid employeeId);
    Task<EmployeeBankDetail?> GetPrimaryBankDetailAsync(Guid employeeId);
    Task<IEnumerable<EmployeeBankDetail>> GetActiveByEmployeeAsync(Guid employeeId);
}

/// <summary>
/// Repository interface for bank (financial institution) operations
/// </summary>
public interface IBankRepository : IGenericRepository<EmployeeBank>
{
    Task<IEnumerable<EmployeeBank>> GetActiveAsync(Guid tenantId);
    Task<EmployeeBank?> GetByCodeAsync(Guid tenantId, string code);
    Task<EmployeeBank?> GetWithBranchesAsync(Guid tenantId, Guid id);
    Task<bool> CodeExistsAsync(Guid tenantId, string code, Guid? excludeId = null);
}

/// <summary>
/// Repository interface for bank branch operations
/// </summary>
public interface IBankBranchRepository : IGenericRepository<EmployeeBankBranch>
{
    Task<IEnumerable<EmployeeBankBranch>> GetByBankAsync(Guid tenantId, Guid bankId);
    Task<IEnumerable<EmployeeBankBranch>> GetActiveByBankAsync(Guid tenantId, Guid bankId);
    Task<EmployeeBankBranch?> GetByCodeAsync(Guid tenantId, Guid bankId, string code);
    Task<bool> CodeExistsAsync(Guid tenantId, Guid bankId, string code, Guid? excludeId = null);
}

/// <summary>
/// Repository interface for bank (financial institution) operations
/// </summary>
public interface IEmployeeBankRepository : IGenericRepository<EmployeeBank>
{
    Task<IEnumerable<EmployeeBank>> GetActiveAsync(Guid tenantId);
    Task<EmployeeBank?> GetByCodeAsync(Guid tenantId, string code);
    Task<EmployeeBank?> GetWithBranchesAsync(Guid tenantId, Guid id);
    Task<bool> CodeExistsAsync(Guid tenantId, string code, Guid? excludeId = null);
}

/// <summary>
/// Repository interface for bank branch operations
/// </summary>
public interface IEmployeeBankBranchRepository : IGenericRepository<EmployeeBankBranch>
{
    Task<IEnumerable<EmployeeBankBranch>> GetByBankAsync(Guid tenantId, Guid bankId);
    Task<IEnumerable<EmployeeBankBranch>> GetActiveByBankAsync(Guid tenantId, Guid bankId);
    Task<EmployeeBankBranch?> GetByCodeAsync(Guid tenantId, Guid bankId, string code);
    Task<bool> CodeExistsAsync(Guid tenantId, Guid bankId, string code, Guid? excludeId = null);
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
    Task<IEnumerable<EmployeePosition>> GetByOrganizationUnitAsync(Guid organizationUnitId);
    Task<IEnumerable<EmployeePosition>> GetByDepartmentAsync(Guid departmentId);
    Task<EmployeePosition?> GetByCodeAsync(string code);
    Task<EmployeePosition?> GetWithSkillRequirementsAsync(Guid id);
    Task<IEnumerable<EmployeePosition>> GetByLevelAsync(int level);
    Task<bool> CodeExistsAsync(string code);
    /// <summary>Explicitly marks a new benefit as Added so EF Core inserts rather than updates it.</summary>
    void TrackBenefit(EmployeePositionBenefit benefit);
    /// <summary>Explicitly marks a new skill requirement as Added so EF Core inserts rather than updates it.</summary>
    void TrackSkillRequirement(PositionSkillRequirement requirement);

    /// <summary>
    /// A position's benefit entitlements <b>including soft-deleted ones</b>, tracked so they can be
    /// revived. The ordinary include cannot serve this: the global soft-delete filter applies to
    /// included navigations too, so a removed row is invisible to a later save — which then tries to
    /// insert a duplicate and violates the unique index on (TenantId, PositionId, PolicyId).
    /// </summary>
    Task<IReadOnlyList<EmployeePositionBenefit>> GetBenefitsIncludingDeletedAsync(Guid positionId);

    /// <summary>
    /// A position's skill requirements <b>including soft-deleted ones</b>, for the same reason as
    /// <see cref="GetBenefitsIncludingDeletedAsync"/>.
    /// </summary>
    Task<IReadOnlyList<PositionSkillRequirement>> GetSkillRequirementsIncludingDeletedAsync(Guid positionId);
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

public interface IQualificationCatalogueRepository : IGenericRepository<Qualification>
{
    Task<IEnumerable<Qualification>> GetActiveAsync();
    Task<IEnumerable<Qualification>> GetByTypeAsync(QualificationType type);
    Task<Qualification?> GetByNameAsync(string name);
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