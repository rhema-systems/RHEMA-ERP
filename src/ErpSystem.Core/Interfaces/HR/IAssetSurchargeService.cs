using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR.Assets;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Interfaces.HR;

/// <summary>
/// Charging an employee for a company asset they damaged, lost or never returned — <b>AST-3</b>,
/// defect <b>D-d</b>, decision <b>D9</b>. Area 16, slice 7.
/// </summary>
/// <remarks>
/// <para>The life of a charge: <c>Draft</c> → put to the employee (<c>WithEmployee</c>) → they
/// accept or dispute → <c>Submitted</c> to the workflow engine → <c>Approved</c> or
/// <c>Rejected</c> → a recovery plan → <c>Recovering</c> → <c>Recovered</c>. It can be waived at
/// any point after it is raised, and cancelled before anybody has ruled on it.</para>
///
/// <para><b>What this deliberately does not do.</b> It deducts nothing. The recovery plan is a
/// declaration payroll consumes through <see cref="GetPayrollDeductionLinesAsync"/>, and
/// <see cref="RecordRecoveryAsync"/> records what was actually collected. No GL posting — that is
/// registered in <c>docs/HR-FINANCE-INTEGRATION-BACKLOG.md</c> for the sweep after the module.</para>
/// </remarks>
public interface IAssetSurchargeService
{
    Task<AssetSurchargeDto?> GetByIdAsync(Guid id);
    Task<IEnumerable<AssetSurchargeSummaryDto>> GetAllAsync();
    Task<PagedResult<AssetSurchargeSummaryDto>> GetPagedAsync(
        int page, int pageSize, string? searchTerm = null, AssetSurchargeStatus? status = null);

    /// <summary>Every charge against one employee. Self-or-HR.</summary>
    Task<IEnumerable<AssetSurchargeSummaryDto>> GetByEmployeeIdAsync(Guid employeeId);

    Task<IEnumerable<AssetSurchargeSummaryDto>> GetByAssignmentIdAsync(Guid assignmentId);

    /// <summary>Approved charges with a balance still to collect — the debt this module holds.</summary>
    Task<IEnumerable<AssetSurchargeSummaryDto>> GetOutstandingAsync();

    /// <summary>
    /// The read-only projection payroll consumes. HR declares; payroll deducts.
    /// </summary>
    Task<IEnumerable<AssetSurchargePayrollLineDto>> GetPayrollDeductionLinesAsync();

    Task<AssetSurchargeDto> CreateAsync(CreateAssetSurchargeDto dto);
    Task<AssetSurchargeDto> UpdateAsync(Guid id, UpdateAssetSurchargeDto dto);
    Task DeleteAsync(Guid id);

    /// <summary>Puts the charge to the employee. Until this, they cannot see it — D9.</summary>
    Task<AssetSurchargeDto> NotifyEmployeeAsync(Guid id);

    /// <summary>The employee's own answer. Refused for everybody else, HR included.</summary>
    Task<AssetSurchargeDto> RespondAsync(Guid id, RespondToAssetSurchargeDto dto);

    Task<AssetSurchargeDto> SubmitAsync(Guid id, SubmitAssetSurchargeDto dto);
    Task<AssetSurchargeDto> RecallAsync(Guid id, string? reason);
    Task ApproveAsync(Guid id, ApproveAssetSurchargeDto dto);
    Task RejectAsync(Guid id, RejectAssetSurchargeDto dto);

    Task<AssetSurchargeDto> SetRecoveryPlanAsync(Guid id, SetAssetSurchargeRecoveryPlanDto dto);
    Task<AssetSurchargeDto> RecordRecoveryAsync(Guid id, RecordAssetSurchargeRecoveryDto dto);
    Task<AssetSurchargeDto> WaiveAsync(Guid id, WaiveAssetSurchargeDto dto);
    Task<AssetSurchargeDto> CancelAsync(Guid id, CancelAssetSurchargeDto dto);
}

public interface IAssetSurchargeRepository : IGenericRepository<AssetSurcharge>
{
    Task<IEnumerable<AssetSurcharge>> GetByTenantAsync(Guid tenantId);
    Task<AssetSurcharge?> GetWithDetailsAsync(Guid id);
    Task<IEnumerable<AssetSurcharge>> GetByEmployeeIdAsync(Guid employeeId);
    Task<IEnumerable<AssetSurcharge>> GetByAssignmentIdAsync(Guid assignmentId);

    /// <summary>Approved or part-collected charges that still carry a balance.</summary>
    Task<IEnumerable<AssetSurcharge>> GetOutstandingAsync(Guid tenantId);
}

public interface IAssetSurchargeRecoveryRepository : IGenericRepository<AssetSurchargeRecovery>
{
    Task<IEnumerable<AssetSurchargeRecovery>> GetBySurchargeIdAsync(Guid surchargeId);
}
