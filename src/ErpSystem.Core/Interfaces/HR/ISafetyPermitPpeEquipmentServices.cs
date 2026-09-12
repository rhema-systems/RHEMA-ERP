using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums.Safety;

namespace ErpSystem.Core.Interfaces.HR;

// ============================================================================
// SHE services — Permit-to-Work (E), PPE Management (F) and Equipment (G).
// ============================================================================

// ============================================================================
// E. PERMIT-TO-WORK SERVICE
// ============================================================================

public interface IShePermitToWorkService
{
    Task<ShePermitToWorkDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ShePermitToWorkDto?> GetByNumberAsync(string permitNumber, CancellationToken cancellationToken = default);
    Task<IEnumerable<ShePermitToWorkSummaryDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<ShePermitToWorkSummaryDto>> GetByStatusAsync(ShePermitStatus status, CancellationToken cancellationToken = default);
    Task<IEnumerable<ShePermitToWorkSummaryDto>> GetByTypeAsync(ShePermitType type, CancellationToken cancellationToken = default);
    Task<IEnumerable<ShePermitToWorkSummaryDto>> GetActiveAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<ShePermitToWorkSummaryDto>> GetByContractorAsync(Guid contractorId, CancellationToken cancellationToken = default);
    Task<IEnumerable<ShePermitToWorkSummaryDto>> GetByRequestorAsync(Guid requestedById, CancellationToken cancellationToken = default);
    Task<IEnumerable<ShePermitToWorkSummaryDto>> GetExpiringAsync(int daysAhead = 1, CancellationToken cancellationToken = default);
    Task<IEnumerable<ShePermitToWorkSummaryDto>> GetSuspendedAsync(CancellationToken cancellationToken = default);

    Task<ShePermitToWorkDto> CreateAsync(CreateShePermitToWorkDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default);
    Task<ShePermitToWorkDto> UpdateAsync(UpdateShePermitToWorkDto dto, Guid userId, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    Task<bool> ApproveAsync(ApproveShePermitToWorkDto dto, Guid userId, CancellationToken cancellationToken = default);
    Task<bool> SuspendAsync(SuspendShePermitToWorkDto dto, Guid userId, CancellationToken cancellationToken = default);
    Task<bool> ResumeAsync(Guid permitId, Guid userId, CancellationToken cancellationToken = default);
    Task<bool> CloseAsync(CloseShePermitToWorkDto dto, Guid userId, CancellationToken cancellationToken = default);

    Task<ShePermitToWorkWorkerDto> AddWorkerAsync(CreateShePermitToWorkWorkerDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default);
    Task<ShePermitToWorkWorkerDto> UpdateWorkerAsync(UpdateShePermitToWorkWorkerDto dto, Guid userId, CancellationToken cancellationToken = default);
    Task<bool> DeleteWorkerAsync(Guid workerId, CancellationToken cancellationToken = default);

    Task<ShePermitToWorkExtensionDto> AddExtensionAsync(CreateShePermitToWorkExtensionDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default);

    Task<ShePermitToWorkDocumentDto> AddDocumentAsync(CreateShePermitToWorkDocumentDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default);
    Task<bool> DeleteDocumentAsync(Guid documentId, CancellationToken cancellationToken = default);
}

// ============================================================================
// F. PPE MANAGEMENT SERVICE  (types, inventory, issuance, role requirements)
// ============================================================================

public interface IPpeManagementService
{
    // PPE types
    Task<IEnumerable<PpeTypeDto>> GetTypesAsync(bool activeOnly = false, CancellationToken cancellationToken = default);
    Task<PpeTypeDto> GetTypeAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PpeTypeDto> CreateTypeAsync(CreatePpeTypeDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default);
    Task<PpeTypeDto> UpdateTypeAsync(UpdatePpeTypeDto dto, Guid userId, CancellationToken cancellationToken = default);
    Task<bool> DeleteTypeAsync(Guid id, CancellationToken cancellationToken = default);

    // Inventory
    Task<IEnumerable<PpeInventoryDto>> GetInventoryAsync(CancellationToken cancellationToken = default);
    Task<PpeInventoryDto> GetInventoryItemAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<PpeInventoryDto>> GetInventoryByTypeAsync(Guid ppeTypeId, CancellationToken cancellationToken = default);
    Task<IEnumerable<PpeInventoryDto>> GetBelowReorderLevelAsync(CancellationToken cancellationToken = default);
    Task<PpeInventoryDto> CreateInventoryAsync(CreatePpeInventoryDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default);
    Task<PpeInventoryDto> UpdateInventoryAsync(UpdatePpeInventoryDto dto, Guid userId, CancellationToken cancellationToken = default);
    Task<bool> RestockAsync(RestockPpeInventoryDto dto, Guid userId, CancellationToken cancellationToken = default);
    Task<bool> DeleteInventoryAsync(Guid id, CancellationToken cancellationToken = default);

    // Issuance
    Task<IEnumerable<PpeIssuanceDto>> GetIssuancesByEmployeeAsync(Guid employeeId, CancellationToken cancellationToken = default);
    Task<IEnumerable<PpeIssuanceDto>> GetOutstandingIssuancesAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<PpeIssuanceDto>> GetOverdueReturnsAsync(CancellationToken cancellationToken = default);
    Task<PpeIssuanceDto> IssueAsync(CreatePpeIssuanceDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default);
    Task<bool> ReturnAsync(ReturnPpeIssuanceDto dto, Guid userId, CancellationToken cancellationToken = default);

    // Job-role requirements
    Task<IEnumerable<JobRolePpeRequirementDto>> GetRequirementsAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<JobRolePpeRequirementDto>> GetRequirementsByJobRoleAsync(string jobRoleCode, CancellationToken cancellationToken = default);
    Task<JobRolePpeRequirementDto> AddRequirementAsync(CreateJobRolePpeRequirementDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default);
    Task<JobRolePpeRequirementDto> UpdateRequirementAsync(UpdateJobRolePpeRequirementDto dto, Guid userId, CancellationToken cancellationToken = default);
    Task<bool> DeleteRequirementAsync(Guid requirementId, CancellationToken cancellationToken = default);
}

// ============================================================================
// G. SAFETY EQUIPMENT SERVICE
// ============================================================================

public interface ISafetyEquipmentService
{
    Task<SafetyEquipmentDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<SafetyEquipmentDto?> GetByNumberAsync(string equipmentNumber, CancellationToken cancellationToken = default);
    Task<IEnumerable<SafetyEquipmentSummaryDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<SafetyEquipmentSummaryDto>> GetByStatusAsync(SheSafetyEquipmentStatus status, CancellationToken cancellationToken = default);
    Task<IEnumerable<SafetyEquipmentSummaryDto>> GetByTypeAsync(SheSafetyEquipmentType type, CancellationToken cancellationToken = default);
    Task<IEnumerable<SafetyEquipmentSummaryDto>> GetByLocationAsync(Guid locationId, CancellationToken cancellationToken = default);
    Task<IEnumerable<SafetyEquipmentSummaryDto>> GetDueForInspectionAsync(int daysAhead = 30, CancellationToken cancellationToken = default);
    Task<IEnumerable<SafetyEquipmentSummaryDto>> GetDueForMaintenanceAsync(int daysAhead = 30, CancellationToken cancellationToken = default);
    Task<IEnumerable<SafetyEquipmentSummaryDto>> GetExpiringCertificationAsync(int daysAhead = 30, CancellationToken cancellationToken = default);
    Task<IEnumerable<SafetyEquipmentSummaryDto>> GetOutOfServiceAsync(CancellationToken cancellationToken = default);

    Task<SafetyEquipmentDto> CreateAsync(CreateSafetyEquipmentDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default);
    Task<SafetyEquipmentDto> UpdateAsync(UpdateSafetyEquipmentDto dto, Guid userId, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    // Inspections (updates the equipment's last/next inspection dates)
    Task<SafetyEquipmentInspectionDto> AddInspectionAsync(CreateSafetyEquipmentInspectionDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default);
    Task<SafetyEquipmentInspectionDto> UpdateInspectionAsync(UpdateSafetyEquipmentInspectionDto dto, Guid userId, CancellationToken cancellationToken = default);
    Task<IEnumerable<SafetyEquipmentInspectionDto>> GetInspectionsForEquipmentAsync(Guid equipmentId, CancellationToken cancellationToken = default);
    Task<SafetyEquipmentInspectionActionDto> AddInspectionActionAsync(CreateSafetyEquipmentInspectionActionDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default);
    Task<SafetyEquipmentInspectionActionDto> UpdateInspectionActionAsync(UpdateSafetyEquipmentInspectionActionDto dto, Guid userId, CancellationToken cancellationToken = default);
    Task<bool> DeleteInspectionActionAsync(Guid actionId, CancellationToken cancellationToken = default);

    // Maintenance
    Task<SafetyEquipmentMaintenanceDto> AddMaintenanceAsync(CreateSafetyEquipmentMaintenanceDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default);
    Task<SafetyEquipmentMaintenanceDto> UpdateMaintenanceAsync(UpdateSafetyEquipmentMaintenanceDto dto, Guid userId, CancellationToken cancellationToken = default);
    Task<bool> DeleteMaintenanceAsync(Guid maintenanceId, CancellationToken cancellationToken = default);
}
