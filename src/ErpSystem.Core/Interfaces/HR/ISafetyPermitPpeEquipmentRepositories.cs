using ErpSystem.Core.Entities.HR.Safety;
using ErpSystem.Core.Enums.Safety;

namespace ErpSystem.Core.Interfaces.HR;

// ============================================================================
// SHE repositories — Permit-to-Work (E), PPE Management (F) and Equipment (G).
// ============================================================================

// ============================================================================
// E. PERMIT-TO-WORK
// ============================================================================

public interface IShePermitToWorkRepository : IGenericRepository<ShePermitToWork>
{
    Task<ShePermitToWork?> GetByNumberAsync(string permitNumber);
    Task<ShePermitToWork?> GetWithFullDetailsAsync(Guid id);
    Task<IEnumerable<ShePermitToWork>> GetAllSummaryAsync();
    Task<IEnumerable<ShePermitToWork>> GetByStatusAsync(ShePermitStatus status);
    Task<IEnumerable<ShePermitToWork>> GetByTypeAsync(ShePermitType type);
    Task<IEnumerable<ShePermitToWork>> GetActiveAsync();
    Task<IEnumerable<ShePermitToWork>> GetByContractorAsync(Guid contractorId);
    Task<IEnumerable<ShePermitToWork>> GetByRequestorAsync(Guid requestedById);

    /// <summary>Returns active/approved permits whose planned end falls within the specified number of days.</summary>
    Task<IEnumerable<ShePermitToWork>> GetExpiringAsync(int daysAhead = 1);

    Task<IEnumerable<ShePermitToWork>> GetSuspendedAsync();
    Task<string> GetNextPermitNumberAsync();
}

// ============================================================================
// F. PPE MANAGEMENT
// ============================================================================

public interface IPpeTypeRepository : IGenericRepository<PpeType>
{
    Task<PpeType?> GetByCodeAsync(string code);
    Task<IEnumerable<PpeType>> GetActiveAsync();
    Task<IEnumerable<PpeType>> GetByCategoryAsync(ShePpeCategory category);
}

public interface IPpeInventoryRepository : IGenericRepository<PpeInventory>
{
    Task<PpeInventory?> GetByItemCodeAsync(string itemCode);
    Task<IEnumerable<PpeInventory>> GetByPpeTypeAsync(Guid ppeTypeId);

    /// <summary>Returns inventory items whose stock is at or below their reorder level.</summary>
    Task<IEnumerable<PpeInventory>> GetBelowReorderLevelAsync();
}

public interface IPpeIssuanceRepository : IGenericRepository<PpeIssuance>
{
    Task<IEnumerable<PpeIssuance>> GetByEmployeeAsync(Guid employeeId);
    Task<IEnumerable<PpeIssuance>> GetByPpeTypeAsync(Guid ppeTypeId);

    /// <summary>Returns issuances that have not yet been returned.</summary>
    Task<IEnumerable<PpeIssuance>> GetOutstandingAsync();

    /// <summary>Returns un-returned issuances whose PPE expiry date falls within the specified number of days.</summary>
    Task<IEnumerable<PpeIssuance>> GetExpiringAsync(int daysAhead = 30);

    /// <summary>Returns un-returned issuances whose expected return date has passed.</summary>
    Task<IEnumerable<PpeIssuance>> GetOverdueReturnsAsync();
}

public interface IJobRolePpeRequirementRepository : IGenericRepository<JobRolePpeRequirement>
{
    Task<IEnumerable<JobRolePpeRequirement>> GetByJobRoleAsync(string jobRoleCode);
    Task<IEnumerable<JobRolePpeRequirement>> GetByPpeTypeAsync(Guid ppeTypeId);
}

// ============================================================================
// G. SAFETY EQUIPMENT
// ============================================================================

public interface ISafetyEquipmentRepository : IGenericRepository<SafetyEquipment>
{
    Task<SafetyEquipment?> GetByNumberAsync(string equipmentNumber);
    Task<SafetyEquipment?> GetWithFullDetailsAsync(Guid id);
    Task<IEnumerable<SafetyEquipment>> GetAllSummaryAsync();
    Task<IEnumerable<SafetyEquipment>> GetByStatusAsync(SheSafetyEquipmentStatus status);
    Task<IEnumerable<SafetyEquipment>> GetByTypeAsync(SheSafetyEquipmentType type);
    Task<IEnumerable<SafetyEquipment>> GetByLocationAsync(Guid locationId);

    /// <summary>Returns equipment whose next inspection due date falls within the specified number of days.</summary>
    Task<IEnumerable<SafetyEquipment>> GetDueForInspectionAsync(int daysAhead = 30);

    /// <summary>Returns equipment whose next maintenance due date falls within the specified number of days.</summary>
    Task<IEnumerable<SafetyEquipment>> GetDueForMaintenanceAsync(int daysAhead = 30);

    /// <summary>Returns certified equipment whose certification expires within the specified number of days.</summary>
    Task<IEnumerable<SafetyEquipment>> GetExpiringCertificationAsync(int daysAhead = 30);

    Task<IEnumerable<SafetyEquipment>> GetOutOfServiceAsync();
    Task<string> GetNextEquipmentNumberAsync();
}

public interface ISafetyEquipmentInspectionRepository : IGenericRepository<SafetyEquipmentInspection>
{
    Task<IEnumerable<SafetyEquipmentInspection>> GetByEquipmentIdAsync(Guid equipmentId);
    Task<IEnumerable<SafetyEquipmentInspection>> GetByResultAsync(SheInspectionResult result);
}
