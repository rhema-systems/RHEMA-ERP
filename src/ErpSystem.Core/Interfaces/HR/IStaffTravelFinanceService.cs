using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Interfaces.HR;

// ============================================================================
// STAFF TRAVEL — GROUP 5: FINANCE SERVICE (BUDGET, EXPENSES & ADVANCES)
// ============================================================================

#region Staff Travel Finance Service

public interface IStaffTravelFinanceService
{
    // Budget
    Task<StaffTravelBudgetDto?> GetBudgetByRequestAsync(Guid requestId, CancellationToken cancellationToken = default);
    Task<StaffTravelBudgetDto> CreateBudgetAsync(CreateStaffTravelBudgetDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default);
    Task<StaffTravelBudgetDto> UpdateBudgetAsync(UpdateStaffTravelBudgetDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default);
    /// <summary>ApproveBudget. <paramref name="approverEmployeeId"/> is the caller's employee record, recorded as the
    /// approver; never the traveller (lane 3, B10).</summary>
    Task<StaffTravelBudgetDto> ApproveBudgetAsync(Guid budgetId, Guid approverEmployeeId, CancellationToken cancellationToken = default);

    // Expense claims
    Task<StaffTravelExpenseClaimDto> GetClaimByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<StaffTravelExpenseClaimDto?> GetClaimByNumberAsync(string claimNumber, CancellationToken cancellationToken = default);
    Task<IEnumerable<StaffTravelExpenseClaimSummaryDto>> GetAllClaimsAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<StaffTravelExpenseClaimSummaryDto>> GetClaimsByRequestAsync(Guid requestId, CancellationToken cancellationToken = default);
    Task<IEnumerable<StaffTravelExpenseClaimSummaryDto>> GetClaimsByEmployeeAsync(Guid employeeId, CancellationToken cancellationToken = default);
    Task<IEnumerable<StaffTravelExpenseClaimSummaryDto>> GetClaimsByStatusAsync(TravelClaimStatus status, CancellationToken cancellationToken = default);
    Task<IEnumerable<StaffTravelExpenseClaimSummaryDto>> GetUnpaidApprovedClaimsAsync(CancellationToken cancellationToken = default);
    /// <summary>CreateClaim. <paramref name="actorEmployeeId"/> authors the internal note a fuel line's D-32 reason
    /// becomes; a reason without one is refused (lane 6).</summary>
    Task<StaffTravelExpenseClaimDto> CreateClaimAsync(CreateStaffTravelExpenseClaimDto createDto, Guid tenantId, Guid createdByUserId, Guid? actorEmployeeId = null, CancellationToken cancellationToken = default);
    Task<StaffTravelExpenseClaimDto> UpdateClaimAsync(UpdateStaffTravelExpenseClaimDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default);
    Task<bool> DeleteClaimAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> SubmitClaimAsync(Guid claimId, Guid submittedByUserId, CancellationToken cancellationToken = default);
    /// <summary>ReviewClaim. <paramref name="reviewerEmployeeId"/> is the caller's employee record and
    /// overrides any <c>FinanceReviewedById</c> on the payload — who acted is identity, not an input.</summary>
    Task<bool> ReviewClaimAsync(ReviewStaffTravelExpenseClaimDto reviewDto, Guid reviewerEmployeeId, CancellationToken cancellationToken = default);
    /// <summary>PayClaim. <paramref name="payerEmployeeId"/> is the caller's employee record: the payer is recorded
    /// and is never the claimant or a reviewer of the claim (lane 3, D-2).</summary>
    Task<bool> PayClaimAsync(PayStaffTravelExpenseClaimDto payDto, Guid payerEmployeeId, CancellationToken cancellationToken = default);
    /// <summary>VoidClaimPayment. <paramref name="voiderEmployeeId"/> is the caller's employee record: never the
    /// claimant or the payer (lane 3, T-39).</summary>
    Task<bool> VoidClaimPaymentAsync(Guid claimId, VoidStaffTravelClaimPaymentDto voidDto, Guid voiderEmployeeId, CancellationToken cancellationToken = default);

    // Expense claim lines
    Task<StaffTravelExpenseClaimLineDto> AddClaimLineAsync(CreateStaffTravelExpenseClaimLineDto createDto, Guid tenantId, Guid createdByUserId, Guid? actorEmployeeId = null, CancellationToken cancellationToken = default);
    Task<IEnumerable<StaffTravelExpenseClaimLineDto>> GetClaimLinesAsync(Guid claimId, CancellationToken cancellationToken = default);
    Task<StaffTravelExpenseClaimLineDto> UpdateClaimLineAsync(UpdateStaffTravelExpenseClaimLineDto updateDto, Guid updatedByUserId, Guid? actorEmployeeId = null, CancellationToken cancellationToken = default);
    /// <summary>Lane 6 (D-30, D-32): the claim's trip's company-vehicle trips and the fuel Fleet already logs on them.</summary>
    Task<StaffTravelFleetFuelOptionsDto> GetClaimFleetFuelAsync(Guid claimId, CancellationToken cancellationToken = default);
    /// <summary>ReviewClaimLine. <paramref name="reviewerEmployeeId"/> is the caller's employee record and
    /// overrides any <c>ReviewedById</c> on the payload — who acted is identity, not an input.</summary>
    Task<bool> ReviewClaimLineAsync(ReviewStaffTravelExpenseClaimLineDto reviewDto, Guid reviewerEmployeeId, CancellationToken cancellationToken = default);
    Task<bool> DeleteClaimLineAsync(Guid lineId, CancellationToken cancellationToken = default);

    // Advances
    Task<StaffTravelAdvanceDto> GetAdvanceByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<StaffTravelAdvanceDto?> GetAdvanceByNumberAsync(string advanceNumber, CancellationToken cancellationToken = default);
    Task<IEnumerable<StaffTravelAdvanceSummaryDto>> GetAllAdvancesAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<StaffTravelAdvanceSummaryDto>> GetAdvancesByStatusAsync(TravelAdvanceStatus status, CancellationToken cancellationToken = default);
    Task<IEnumerable<StaffTravelAdvanceSummaryDto>> GetAdvancesByRequestAsync(Guid requestId, CancellationToken cancellationToken = default);
    Task<IEnumerable<StaffTravelAdvanceSummaryDto>> GetAdvancesByEmployeeAsync(Guid employeeId, CancellationToken cancellationToken = default);
    Task<IEnumerable<StaffTravelAdvanceSummaryDto>> GetOutstandingAdvancesByEmployeeAsync(Guid employeeId, CancellationToken cancellationToken = default);
    Task<IEnumerable<StaffTravelAdvanceSummaryDto>> GetOverdueSettlementsAsync(CancellationToken cancellationToken = default);
    Task<StaffTravelAdvanceDto> CreateAdvanceAsync(CreateStaffTravelAdvanceDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default);
    Task<StaffTravelAdvanceDto> UpdateAdvanceAsync(UpdateStaffTravelAdvanceDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default);
    Task<bool> DeleteAdvanceAsync(Guid id, CancellationToken cancellationToken = default);
    /// <summary>ApproveAdvance. <paramref name="approverEmployeeId"/> is the caller's employee record and
    /// overrides any <c>ApprovedById</c> on the payload — who acted is identity, not an input.</summary>
    Task<bool> ApproveAdvanceAsync(ApproveStaffTravelAdvanceDto approveDto, Guid approverEmployeeId, CancellationToken cancellationToken = default);
    /// <summary>DisburseAdvance. <paramref name="disburserEmployeeId"/> is the caller's employee record and
    /// overrides any <c>DisbursedById</c> on the payload — who acted is identity, not an input.</summary>
    Task<bool> DisburseAdvanceAsync(DisburseStaffTravelAdvanceDto disburseDto, Guid disburserEmployeeId, CancellationToken cancellationToken = default);
    // Lane 3: the verbs an advance lacked. Each actor is the caller's employee record.
    Task<bool> RejectAdvanceAsync(Guid advanceId, string reason, Guid rejecterEmployeeId, CancellationToken cancellationToken = default);
    Task<bool> CancelAdvanceAsync(Guid advanceId, string reason, Guid cancellerEmployeeId, CancellationToken cancellationToken = default);
    Task<bool> RecordAdvanceRefundAsync(Guid advanceId, RefundStaffTravelAdvanceDto refundDto, Guid recorderEmployeeId, CancellationToken cancellationToken = default);
    Task<bool> WriteOffAdvanceAsync(Guid advanceId, string reason, Guid writerEmployeeId, CancellationToken cancellationToken = default);

    // Per-diem rates
    Task<StaffTravelPerDiemRateDto> GetPerDiemRateByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<StaffTravelPerDiemRateDto>> GetActivePerDiemRatesAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<StaffTravelPerDiemRateDto>> GetPerDiemRatesByCountryAsync(Guid countryId, CancellationToken cancellationToken = default);
    Task<StaffTravelPerDiemRateDto?> GetEffectivePerDiemRateAsync(Guid countryId, string? city, Guid? staffLevelId, DateOnly onDate, CancellationToken cancellationToken = default);
    Task<StaffTravelPerDiemRateDto> CreatePerDiemRateAsync(CreateStaffTravelPerDiemRateDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default);
    Task<StaffTravelPerDiemRateDto> UpdatePerDiemRateAsync(UpdateStaffTravelPerDiemRateDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default);
    Task<bool> DeletePerDiemRateAsync(Guid id, CancellationToken cancellationToken = default);
}

#endregion
