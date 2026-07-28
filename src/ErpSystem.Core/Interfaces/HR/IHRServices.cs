using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.HR.StaffAttendance;

namespace ErpSystem.Core.Interfaces.HR;

/// <summary>
/// Service interface for employee operations
/// </summary>
public interface IEmployeeService
{
    #region 1) Core Employee Lifecycle (writes - transactional)

    Task<EmployeeDetailDto> CreateEmployeeAsync(CreateEmployeeDto dto, CancellationToken cancellationToken = default);
    Task<EmployeeDetailDto> UpdateEmployeeAsync(Guid employeeId, UpdateEmployeeDto dto, CancellationToken cancellationToken = default);

    Task<EmployeeDetailDto> ActivateEmployeeAsync(Guid employeeId, CancellationToken cancellationToken = default);
    Task<EmployeeDetailDto> DeactivateEmployeeAsync(Guid employeeId, CancellationToken cancellationToken = default);

    Task<EmployeeDetailDto> TerminateEmployeeAsync(Guid employeeId, TerminateEmployeeDto dto, CancellationToken cancellationToken = default);
    Task<EmployeeDetailDto> ReinstateEmployeeAsync(Guid employeeId, string? notes = null, CancellationToken cancellationToken = default);

    Task<bool> DeleteEmployeeAsync(Guid employeeId, CancellationToken cancellationToken = default);

    #endregion

    #region 2) Employee Retrieval (intent-based reads)

    Task<EmployeeDto?> GetEmployeeSummaryByIdAsync(Guid employeeId, CancellationToken cancellationToken = default);
    Task<EmployeeDetailDto?> GetEmployeeDetailsByIdAsync(Guid employeeId, CancellationToken cancellationToken = default);
    Task<EmployeeFullProfileDto?> GetEmployeeFullProfileByIdAsync(Guid employeeId, CancellationToken cancellationToken = default);

    Task<EmployeeDto?> GetEmployeeSummaryByEmployeeNumberAsync(string employeeNumber, CancellationToken cancellationToken = default);
    Task<EmployeeDetailDto?> GetEmployeeDetailsByEmployeeNumberAsync(string employeeNumber, CancellationToken cancellationToken = default);
    Task<EmployeeFullProfileDto?> GetEmployeeFullProfileByEmployeeNumberAsync(string employeeNumber, CancellationToken cancellationToken = default);

    Task<EmployeeDto?> GetEmployeeSummaryByEmailAsync(string email, CancellationToken cancellationToken = default);

    Task<PagedResult<EmployeeDto>> GetEmployeesPagedAsync(EmployeeSearchDto searchCriteria, int page, int pageSize, CancellationToken cancellationToken = default);

    Task<IEnumerable<EmployeeDto>> GetEmployeesByOrganizationUnitAsync(Guid organizationUnitId, CancellationToken cancellationToken = default);
    Task<IEnumerable<EmployeeDto>> GetEmployeesByOrganizationLevelAsync(Guid organizationLevelId, CancellationToken cancellationToken = default);
    Task<IEnumerable<EmployeeDto>> GetEmployeesByPositionAsync(Guid positionId, CancellationToken cancellationToken = default);
    Task<IEnumerable<EmployeeDto>> GetEmployeesByLocationAsync(Guid locationId, CancellationToken cancellationToken = default);
    Task<IEnumerable<EmployeeDto>> GetEmployeesByManagerAsync(Guid managerId, CancellationToken cancellationToken = default);
    Task<IEnumerable<EmployeeDto>> GetEmployeesByStatusAsync(StaffStatus status, CancellationToken cancellationToken = default);

    #endregion

    #region 3) Relationship Management (subresources)

    // Emergency contacts
    Task<IEnumerable<EmployeeEmergencyContactDto>> GetEmergencyContactsAsync(Guid employeeId, CancellationToken cancellationToken = default);
    Task<EmployeeEmergencyContactDto> AddEmergencyContactAsync(CreateEmployeeEmergencyContactDto dto, CancellationToken cancellationToken = default);
    Task<EmployeeEmergencyContactDto> UpdateEmergencyContactAsync(UpdateEmployeeEmergencyContactDto dto, CancellationToken cancellationToken = default);
    Task<bool> RemoveEmergencyContactAsync(Guid emergencyContactId, CancellationToken cancellationToken = default);
    Task<EmployeeEmergencyContactDto> SetPrimaryEmergencyContactAsync(Guid emergencyContactId, CancellationToken cancellationToken = default);
    Task<EmployeeEmergencyContactDto> ActivateEmergencyContactAsync(Guid emergencyContactId, CancellationToken cancellationToken = default);
    Task<EmployeeEmergencyContactDto> DeactivateEmergencyContactAsync(Guid emergencyContactId, CancellationToken cancellationToken = default);

    // Address contacts
    Task<IEnumerable<EmployeeContactDto>> GetContactsAsync(Guid employeeId, CancellationToken cancellationToken = default);
    Task<EmployeeContactDto> AddContactAsync(CreateEmployeeContactDto dto, CancellationToken cancellationToken = default);
    Task<EmployeeContactDto> UpdateContactAsync(UpdateEmployeeContactDto dto, CancellationToken cancellationToken = default);
    Task<bool> RemoveContactAsync(Guid contactId, CancellationToken cancellationToken = default);
    Task<EmployeeContactDto> SetPrimaryContactAsync(Guid contactId, CancellationToken cancellationToken = default);
    Task<EmployeeContactDto?> GetContactByIdAsync(Guid contactId, CancellationToken cancellationToken = default);

    // Dependents
    Task<IEnumerable<EmployeeDependentReadDto>> GetDependentsAsync(Guid employeeId, CancellationToken cancellationToken = default);
    Task<EmployeeDependentReadDto> AddDependentAsync(EmployeeDependentCreateDto dto, CancellationToken cancellationToken = default);
    Task<EmployeeDependentReadDto> UpdateDependentAsync(EmployeeDependentUpdateDto dto, CancellationToken cancellationToken = default);
    Task<bool> RemoveDependentAsync(Guid employeeId, Guid dependentId, CancellationToken cancellationToken = default);

    // Dependent benefits
    Task<IEnumerable<EmployeeDependentBenefitDto>> GetDependentBenefitsAsync(Guid employeeDependentId, CancellationToken cancellationToken = default);
    Task<EmployeeDependentBenefitDto> AddDependentBenefitAsync(CreateEmployeeDependentBenefitDto dto, CancellationToken cancellationToken = default);
    Task<EmployeeDependentBenefitDto> UpdateDependentBenefitAsync(UpdateEmployeeDependentBenefitDto dto, CancellationToken cancellationToken = default);
    Task<bool> RemoveDependentBenefitAsync(Guid dependentBenefitId, CancellationToken cancellationToken = default);
    Task<EmployeeDependentBenefitDto> ActivateDependentBenefitAsync(Guid dependentBenefitId, CancellationToken cancellationToken = default);
    Task<EmployeeDependentBenefitDto> DeactivateDependentBenefitAsync(Guid dependentBenefitId, CancellationToken cancellationToken = default);

    // Qualifications
    Task<IEnumerable<EmployeeQualificationDto>> GetQualificationsAsync(Guid employeeId, CancellationToken cancellationToken = default);
    Task<EmployeeQualificationDto> AddQualificationAsync(CreateEmployeeQualificationDto dto, CancellationToken cancellationToken = default);
    Task<EmployeeQualificationDto> UpdateQualificationAsync(UpdateEmployeeQualificationDto dto, CancellationToken cancellationToken = default);
    Task<bool> RemoveQualificationAsync(Guid qualificationId, CancellationToken cancellationToken = default);
    Task<EmployeeQualificationDto> VerifyQualificationAsync(Guid qualificationId, CancellationToken cancellationToken = default);
    Task<EmployeeQualificationDto> UnverifyQualificationAsync(Guid qualificationId, CancellationToken cancellationToken = default);

    // Skills & certifications
    Task<IEnumerable<EmployeeSkillDto>> GetSkillsAsync(Guid employeeId, CancellationToken cancellationToken = default);
    Task<EmployeeSkillDto> AddSkillAsync(CreateEmployeeSkillDto dto, CancellationToken cancellationToken = default);
    Task<EmployeeSkillDto> UpdateSkillAsync(UpdateEmployeeSkillDto dto, CancellationToken cancellationToken = default);
    Task<bool> RemoveSkillAsync(Guid employeeSkillId, CancellationToken cancellationToken = default);
    Task<EmployeeSkillDto> VerifySkillAsync(Guid employeeSkillId, CancellationToken cancellationToken = default);
    Task<EmployeeSkillDto> UnverifySkillAsync(Guid employeeSkillId, CancellationToken cancellationToken = default);

    // Identification cards
    Task<IEnumerable<EmployeeIdentificationCardListDto>> GetIdentificationCardsAsync(Guid employeeId, CancellationToken cancellationToken = default);
    Task<EmployeeIdentificationCardDetailDto?> GetIdentificationCardByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<EmployeeIdentificationCardDetailDto> AddIdentificationCardAsync(CreateEmployeeIdentificationCardDto dto, CancellationToken cancellationToken = default);
    Task<EmployeeIdentificationCardDetailDto> UpdateIdentificationCardAsync(UpdateEmployeeIdentificationCardDto dto, CancellationToken cancellationToken = default);
    Task<bool> RemoveIdentificationCardAsync(Guid id, CancellationToken cancellationToken = default);
    Task<EmployeeIdentificationCardDetailDto> VerifyIdentificationCardAsync(Guid id, DateTime verifiedDate, CancellationToken cancellationToken = default);
    Task<EmployeeIdentificationCardDetailDto> UnverifyIdentificationCardAsync(Guid id, CancellationToken cancellationToken = default);

    // Work history
    Task<IEnumerable<EmployeeWorkHistoryListDto>> GetWorkHistoriesAsync(Guid employeeId, CancellationToken cancellationToken = default);
    Task<EmployeeWorkHistoryDetailDto?> GetWorkHistoryByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<EmployeeWorkHistoryDetailDto> AddWorkHistoryAsync(CreateEmployeeWorkHistoryDto dto, CancellationToken cancellationToken = default);
    Task<EmployeeWorkHistoryDetailDto> UpdateWorkHistoryAsync(UpdateEmployeeWorkHistoryDto dto, CancellationToken cancellationToken = default);
    Task<bool> RemoveWorkHistoryAsync(Guid id, CancellationToken cancellationToken = default);

    // Contracts
    Task<IEnumerable<EmployeeContractDetailDto>> GetContractsAsync(Guid employeeId, CancellationToken cancellationToken = default);
    Task<EmployeeContractDetailDto?> GetActiveContractAsync(Guid employeeId, CancellationToken cancellationToken = default);
    Task<EmployeeContractDetailDto> AddContractAsync(CreateEmployeeContractDetailDto dto, CancellationToken cancellationToken = default);
    Task<EmployeeContractDetailDto> UpdateContractAsync(UpdateEmployeeContractDetailDto dto, CancellationToken cancellationToken = default);
    Task<bool> RemoveContractAsync(Guid contractId, CancellationToken cancellationToken = default);
    Task<EmployeeContractDetailDto> ActivateContractAsync(Guid contractId, CancellationToken cancellationToken = default);
    Task<EmployeeContractDetailDto> DeactivateContractAsync(Guid contractId, CancellationToken cancellationToken = default);
    Task<EmployeeContractDetailDto> TerminateContractAsync(Guid contractId, DateOnly terminationDate, string reason, CancellationToken cancellationToken = default);

    // Expatriate assignments
    Task<IEnumerable<ExpatriateAssignmentListDto>> GetExpatriateAssignmentsAsync(Guid employeeId, CancellationToken cancellationToken = default);
    Task<ExpatriateAssignmentDetailDto?> GetExpatriateAssignmentByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ExpatriateAssignmentDetailDto> AddExpatriateAssignmentAsync(CreateExpatriateAssignmentDto dto, CancellationToken cancellationToken = default);
    Task<ExpatriateAssignmentDetailDto> UpdateExpatriateAssignmentAsync(UpdateExpatriateAssignmentDto dto, CancellationToken cancellationToken = default);
    Task<bool> RemoveExpatriateAssignmentAsync(Guid id, CancellationToken cancellationToken = default);

    // Position history
    Task<IEnumerable<EmployeePositionHistoryListDto>> GetPositionHistoryAsync(Guid employeeId, CancellationToken cancellationToken = default);
    Task<EmployeePositionHistoryDetailDto?> GetPositionHistoryByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<EmployeePositionHistoryDetailDto> AddPositionHistoryAsync(CreateEmployeePositionHistoryDto dto, CancellationToken cancellationToken = default);
    Task<EmployeePositionHistoryDetailDto> UpdatePositionHistoryAsync(UpdateEmployeePositionHistoryDto dto, CancellationToken cancellationToken = default);
    Task<bool> RemovePositionHistoryAsync(Guid id, CancellationToken cancellationToken = default);

    // Salary assignments
    Task<IEnumerable<EmployeeSalaryAssignmentListDto>> GetSalaryAssignmentsAsync(Guid employeeId, CancellationToken cancellationToken = default);
    Task<EmployeeSalaryAssignmentDetailDto?> GetSalaryAssignmentByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<EmployeeSalaryAssignmentDetailDto> AssignSalaryAsync(CreateEmployeeSalaryAssignmentDto dto, CancellationToken cancellationToken = default);
    Task<EmployeeSalaryAssignmentDetailDto> UpdateSalaryAssignmentAsync(UpdateEmployeeSalaryAssignmentDto dto, CancellationToken cancellationToken = default);
    Task<bool> RemoveSalaryAssignmentAsync(Guid id, CancellationToken cancellationToken = default);

    // Referees
    Task<IEnumerable<EmployeeRefereeListDto>> GetRefereesAsync(Guid employeeId, CancellationToken cancellationToken = default);
    Task<EmployeeRefereeDetailDto?> GetRefereeByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<EmployeeRefereeDetailDto> AddRefereeAsync(CreateEmployeeRefereeDto dto, CancellationToken cancellationToken = default);
    Task<EmployeeRefereeDetailDto> UpdateRefereeAsync(UpdateEmployeeRefereeDto dto, CancellationToken cancellationToken = default);
    Task<bool> RemoveRefereeAsync(Guid id, CancellationToken cancellationToken = default);
    Task<EmployeeRefereeDetailDto> SetPrimaryRefereeAsync(Guid id, CancellationToken cancellationToken = default);
    Task<EmployeeRefereeDetailDto> ActivateRefereeAsync(Guid id, CancellationToken cancellationToken = default);
    Task<EmployeeRefereeDetailDto> DeactivateRefereeAsync(Guid id, CancellationToken cancellationToken = default);

    // Guarantors
    Task<IEnumerable<EmployeeGuarantorListDto>> GetGuarantorsAsync(Guid employeeId, CancellationToken cancellationToken = default);
    Task<EmployeeGuarantorDetailDto?> GetGuarantorByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<EmployeeGuarantorDetailDto> AddGuarantorAsync(CreateEmployeeGuarantorDto dto, CancellationToken cancellationToken = default);
    Task<EmployeeGuarantorDetailDto> UpdateGuarantorAsync(UpdateEmployeeGuarantorDto dto, CancellationToken cancellationToken = default);
    Task<bool> RemoveGuarantorAsync(Guid id, CancellationToken cancellationToken = default);
    Task<EmployeeGuarantorDetailDto> SetPrimaryGuarantorAsync(Guid id, CancellationToken cancellationToken = default);
    Task<EmployeeGuarantorDetailDto> VerifyGuarantorAsync(Guid id, Guid verifiedByEmployeeId, DateTime verifiedDate, CancellationToken cancellationToken = default);
    Task<EmployeeGuarantorDetailDto> UnverifyGuarantorAsync(Guid id, CancellationToken cancellationToken = default);
    Task<EmployeeGuarantorDetailDto> ActivateGuarantorAsync(Guid id, CancellationToken cancellationToken = default);
    Task<EmployeeGuarantorDetailDto> DeactivateGuarantorAsync(Guid id, CancellationToken cancellationToken = default);

    // Bank details
    Task<IEnumerable<EmployeeBankDetailDto>> GetBankDetailsAsync(Guid employeeId, CancellationToken cancellationToken = default);
    Task<EmployeeBankDetailDto?> GetBankDetailByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<EmployeeBankDetailDto> AddBankDetailAsync(CreateEmployeeBankDetailDto dto, CancellationToken cancellationToken = default);
    Task<EmployeeBankDetailDto> UpdateBankDetailAsync(UpdateEmployeeBankDetailDto dto, CancellationToken cancellationToken = default);
    Task<bool> RemoveBankDetailAsync(Guid id, CancellationToken cancellationToken = default);
    Task<EmployeeBankDetailDto> SetPrimaryBankDetailAsync(Guid id, CancellationToken cancellationToken = default);
    Task<EmployeeBankDetailDto> VerifyBankDetailAsync(Guid id, Guid verifiedByEmployeeId, DateTime verifiedDate, CancellationToken cancellationToken = default);
    Task<EmployeeBankDetailDto> UnverifyBankDetailAsync(Guid id, CancellationToken cancellationToken = default);
    Task<EmployeeBankDetailDto> ActivateBankDetailAsync(Guid id, CancellationToken cancellationToken = default);
    Task<EmployeeBankDetailDto> DeactivateBankDetailAsync(Guid id, CancellationToken cancellationToken = default);

    #endregion

    #region 4) Manager & Hierarchy Operations

    Task<EmployeeDetailDto> AssignManagerAsync(Guid employeeId, Guid managerId, CancellationToken cancellationToken = default);
    Task<EmployeeDetailDto> RemoveManagerAsync(Guid employeeId, CancellationToken cancellationToken = default);
    Task<IEnumerable<EmployeeDto>> GetDirectReportsAsync(Guid managerId, CancellationToken cancellationToken = default);
    Task<IEnumerable<EmployeeDto>> GetManagementChainAsync(Guid employeeId, CancellationToken cancellationToken = default);
    Task<bool> WouldCreateCircularReportingAsync(Guid employeeId, Guid proposedManagerId, CancellationToken cancellationToken = default);

    #endregion

    #region 5) Payroll, Tax & Contract Logic

    Task ValidatePayrollFlagsAsync(bool payTax, bool ssFund, bool grossUp, bool tier2Only, bool overtime, CancellationToken cancellationToken = default);

    #endregion

    #region 6) Validation & Business Rules

    Task<bool> IsEmployeeNumberUniqueAsync(string employeeNumber, Guid? excludeEmployeeId = null, CancellationToken cancellationToken = default);
    Task<bool> IsEmailUniqueAsync(string email, Guid? excludeEmployeeId = null, CancellationToken cancellationToken = default);
    Task<bool> IsBadgeNumberUniqueAsync(string badgeNumber, Guid? excludeEmployeeId = null, CancellationToken cancellationToken = default);
    Task<bool> IsTaxNumberUniqueAsync(string taxNumber, Guid? excludeEmployeeId = null, CancellationToken cancellationToken = default);
    Task<bool> IsSocialSecurityNumberUniqueAsync(string socialSecurityNumber, Guid? excludeEmployeeId = null, CancellationToken cancellationToken = default);
    Task<bool> IsTinNumberUniqueAsync(string tinNumber, Guid? excludeEmployeeId = null, CancellationToken cancellationToken = default);

    Task<bool> HasActiveContractAsync(Guid employeeId, CancellationToken cancellationToken = default);
    Task<bool> HasActiveDependentsAsync(Guid employeeId, CancellationToken cancellationToken = default);
    Task<bool> HasActiveGuarantorsAsync(Guid employeeId, CancellationToken cancellationToken = default);
    Task<bool> IsEligibleForExpatriateAssignmentAsync(Guid employeeId, CancellationToken cancellationToken = default);
    Task<bool> CanTerminateEmployeeAsync(Guid employeeId, CancellationToken cancellationToken = default);

    #endregion

    #region Legacy / Backward compatibility (keep controllers compiling)

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
    Task<bool> TerminateEmployeeAsync(Guid id, TerminateEmployeeDto dto);
    Task<IEnumerable<EmployeeDto>> GetByDepartmentAsync(Guid departmentId);
    Task<IEnumerable<EmployeeDto>> GetBySectionAsync(Guid sectionId);
    Task<IEnumerable<EmployeeDto>> GetByPositionAsync(Guid positionId);
    Task<IEnumerable<EmployeeDto>> GetByStatusAsync(StaffStatus status);
    Task<IEnumerable<EmployeeDto>> GetByEmploymentTypeAsync(EmploymentType employmentType);
    Task<IEnumerable<MaintenanceTechnicianDto>> GetMaintenanceTechniciansAsync();
    Task<IEnumerable<MaintenanceTechnicianDto>> GetAvailableTechniciansAsync();
    Task<MaintenanceTechnicianDto?> GetTechnicianByIdAsync(Guid employeeId);
    Task<TechnicianAvailabilityDto?> GetTechnicianAvailabilityAsync(Guid employeeId);
    Task<IEnumerable<MaintenanceTechnicianDto>> GetTechniciansWithSkillAsync(Guid skillId, SkillLevel? minLevel = null);
    Task<bool> EmployeeNumberExistsAsync(string employeeNumber);
    Task<bool> EmailExistsAsync(string email);
    Task<string> GenerateEmployeeNumberAsync();
    Task<int> GetTotalEmployeeCountAsync();
    Task<int> GetActiveEmployeeCountAsync();
    Task<Dictionary<StaffStatus, int>> GetEmployeeCountByStatusAsync();
    Task<Dictionary<string, int>> GetEmployeeCountByDepartmentAsync();

    #endregion
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
    Task<IEnumerable<EmployeePositionDto>> GetByOrganizationUnitAsync(Guid organizationUnitId);
    Task<IEnumerable<EmployeePositionDto>> GetByDepartmentAsync(Guid departmentId);
    Task<EmployeePositionDto?> GetByCodeAsync(string code);
    Task<EmployeePositionDto> CreatePositionAsync(CreateEmployeePositionDto createDto);
    Task<EmployeePositionDto> UpdatePositionAsync(Guid id, UpdateEmployeePositionDto updateDto);
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
/// Service interface for the qualification catalogue (master reference data).
/// Mirrors ISkillService in structure.
/// </summary>
public interface IQualificationCatalogueService
{
    Task<QualificationCatalogueDto?> GetByIdAsync(Guid id);
    Task<IEnumerable<QualificationCatalogueDto>> GetAllAsync();
    Task<IEnumerable<QualificationCatalogueDto>> GetActiveAsync();
    Task<IEnumerable<QualificationCatalogueDto>> GetByTypeAsync(QualificationType type);
    Task<QualificationCatalogueDto?> GetByNameAsync(string name);
    Task<QualificationCatalogueDto> CreateAsync(CreateQualificationCatalogueDto dto);
    Task<QualificationCatalogueDto> UpdateAsync(Guid id, CreateQualificationCatalogueDto dto);
    Task<bool> DeleteAsync(Guid id);
    Task<bool> NameExistsAsync(string name);
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
    Task<CountryDto> CreateAsync(CreateCountryDto dto);
    Task<CountryDto> UpdateAsync(Guid id, UpdateCountryDto dto);
    Task<bool> DeleteAsync(Guid id);
}

/// <summary>
/// Service interface for attendance operations
/// </summary>
public interface IAttendanceService
{
    Task<IEnumerable<StaffAttendanceRecord>> GetByEmployeeAsync(Guid employeeId);
    Task<IEnumerable<StaffAttendanceRecord>> GetByDateRangeAsync(Guid employeeId, DateOnly startDate, DateOnly endDate);
    Task<StaffAttendanceRecord?> GetByEmployeeAndDateAsync(Guid employeeId, DateOnly date);
    Task<IEnumerable<StaffAttendanceRecord>> GetByDateAsync(DateOnly date);
    Task<StaffAttendanceRecord> RecordCheckInAsync(Guid employeeId, TimeOnly checkInTime, DateOnly? date = null);
    Task<StaffAttendanceRecord> RecordCheckOutAsync(Guid employeeId, TimeOnly checkOutTime, DateOnly? date = null);
    Task<IEnumerable<StaffAttendanceRecord>> GetAbsentEmployeesAsync(DateOnly date);
    Task<IEnumerable<StaffAttendanceRecord>> GetLateEmployeesAsync(DateOnly date);
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
