using ErpSystem.Core.Entities.HR.StaffTravel;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Interfaces.HR;

// ============================================================================
// STAFF TRAVEL — GROUP 5: FINANCE (BUDGET, EXPENSES & ADVANCES)
// ============================================================================

#region Staff Travel Budget

public interface IStaffTravelBudgetRepository : IGenericRepository<StaffTravelBudget>
{
    /// <summary>Returns the budget for a request (one-to-one), with approver loaded.</summary>
    Task<StaffTravelBudget?> GetByRequestIdAsync(Guid requestId);
}

#endregion

#region Staff Travel Expense Claim

public interface IStaffTravelExpenseClaimRepository : IGenericRepository<StaffTravelExpenseClaim>
{
    /// <summary>Returns the claim matching the unique claim number, with traveller and request loaded.</summary>
    Task<StaffTravelExpenseClaim?> GetByClaimNumberAsync(string claimNumber);

    /// <summary>Returns a claim with its expense lines loaded.</summary>
    Task<StaffTravelExpenseClaim?> GetWithLinesAsync(Guid id);

    /// <summary>Returns all claims for the tenant, newest-first, with traveller loaded.</summary>
    Task<IEnumerable<StaffTravelExpenseClaim>> GetAllWithDetailsAsync();

    /// <summary>Returns all claims for a travel request.</summary>
    Task<IEnumerable<StaffTravelExpenseClaim>> GetByRequestIdAsync(Guid requestId);

    /// <summary>Returns all claims raised by an employee, newest-first.</summary>
    Task<IEnumerable<StaffTravelExpenseClaim>> GetByEmployeeIdAsync(Guid employeeId);

    /// <summary>Returns claims filtered by status.</summary>
    Task<IEnumerable<StaffTravelExpenseClaim>> GetByStatusAsync(TravelClaimStatus status);

    /// <summary>Returns claims that settle against a particular advance.</summary>
    Task<IEnumerable<StaffTravelExpenseClaim>> GetByAdvanceIdAsync(Guid advanceId);

    /// <summary>Returns approved claims that have not yet been paid.</summary>
    Task<IEnumerable<StaffTravelExpenseClaim>> GetUnpaidApprovedClaimsAsync();
    // CountByYearAsync removed (lane 3, B9): a live count reissued a deleted claim's number; the service reads the
    // highest number ever issued instead.
}

#endregion

#region Staff Travel Expense Claim Line

public interface IStaffTravelExpenseClaimLineRepository : IGenericRepository<StaffTravelExpenseClaimLine>
{
    /// <summary>Returns the lines of a claim ordered by expense date.</summary>
    Task<IEnumerable<StaffTravelExpenseClaimLine>> GetByClaimIdAsync(Guid claimId);

    /// <summary>Returns the lines of a claim still pending finance review.</summary>
    Task<IEnumerable<StaffTravelExpenseClaimLine>> GetPendingReviewAsync(Guid claimId);
}

#endregion

#region Staff Travel Advance

public interface IStaffTravelAdvanceRepository : IGenericRepository<StaffTravelAdvance>
{
    /// <summary>The advance with its employee, request and actors, tenant-scoped — for reloading a write (F-12) and by-id reads (F-13).</summary>
    Task<StaffTravelAdvance?> GetWithDetailsAsync(Guid tenantId, Guid id);

    /// <summary>Returns the advance matching the unique advance number.</summary>
    Task<StaffTravelAdvance?> GetByAdvanceNumberAsync(string advanceNumber);

    /// <summary>Returns all advances for the tenant, newest-first, with traveller loaded.</summary>
    Task<IEnumerable<StaffTravelAdvance>> GetAllWithDetailsAsync();

    /// <summary>Returns all advances for a travel request.</summary>
    Task<IEnumerable<StaffTravelAdvance>> GetByRequestIdAsync(Guid requestId);

    /// <summary>Returns all advances raised by an employee, newest-first.</summary>
    Task<IEnumerable<StaffTravelAdvance>> GetByEmployeeIdAsync(Guid employeeId);

    /// <summary>Returns advances filtered by status.</summary>
    Task<IEnumerable<StaffTravelAdvance>> GetByStatusAsync(TravelAdvanceStatus status);

    /// <summary>Returns an employee's advances with cash still out (disbursed, partly settled or overdue).</summary>
    Task<IEnumerable<StaffTravelAdvance>> GetOutstandingByEmployeeAsync(Guid employeeId);

    /// <summary>Returns advances with cash still out whose settlement deadline has passed.</summary>
    Task<IEnumerable<StaffTravelAdvance>> GetOverdueSettlementsAsync();
    // CountByYearAsync removed (lane 3, B9) — see the claim repository.
}

#endregion

#region Staff Travel Per Diem Rate

public interface IStaffTravelPerDiemRateRepository : IGenericRepository<StaffTravelPerDiemRate>
{
    /// <summary>The per-diem rate with its country and staff level, tenant-scoped — for reloading a write (F-12) and by-id reads (F-13).</summary>
    Task<StaffTravelPerDiemRate?> GetWithDetailsAsync(Guid tenantId, Guid id);

    /// <summary>Returns all active per-diem rates.</summary>
    Task<IEnumerable<StaffTravelPerDiemRate>> GetActiveRatesAsync();

    /// <summary>Returns all rates defined for a country.</summary>
    Task<IEnumerable<StaffTravelPerDiemRate>> GetByCountryAsync(Guid countryId);

    /// <summary>
    /// Returns the most specific active rate effective on the given date for the supplied
    /// country/city/staff level (city- and level-specific overrides win over generic rates).
    /// </summary>
    Task<StaffTravelPerDiemRate?> GetEffectiveRateAsync(Guid countryId, string? city, Guid? staffLevelId, DateOnly onDate);
}

#endregion
