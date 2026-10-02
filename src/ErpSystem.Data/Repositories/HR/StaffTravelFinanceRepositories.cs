using ErpSystem.Core.Entities.HR.StaffTravel;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Data.Repositories.HR;

// ============================================================================
// STAFF TRAVEL — GROUP 5: FINANCE (BUDGET, EXPENSES & ADVANCES)
// ============================================================================

#region Staff Travel Budget Repository

public class StaffTravelBudgetRepository : GenericRepository<StaffTravelBudget>, IStaffTravelBudgetRepository
{
    public StaffTravelBudgetRepository(ApplicationDbContext context) : base(context) { }

    public async Task<StaffTravelBudget?> GetByRequestIdAsync(Guid requestId)
    {
        return await _dbSet
            .Include(b => b.ApprovedBy)
            .FirstOrDefaultAsync(b => b.StaffTravelRequestId == requestId && !b.IsDeleted);
    }
}

#endregion

#region Staff Travel Expense Claim Repository

public class StaffTravelExpenseClaimRepository : GenericRepository<StaffTravelExpenseClaim>, IStaffTravelExpenseClaimRepository
{
    public StaffTravelExpenseClaimRepository(ApplicationDbContext context) : base(context) { }

    public async Task<StaffTravelExpenseClaim?> GetByClaimNumberAsync(string claimNumber)
    {
        return await _dbSet
            .Include(c => c.Employee)
            .Include(c => c.StaffTravelRequest)
            .FirstOrDefaultAsync(c => c.ClaimNumber == claimNumber && !c.IsDeleted);
    }

    public async Task<StaffTravelExpenseClaim?> GetWithLinesAsync(Guid id)
    {
        return await _dbSet
            .Include(c => c.Employee)
            .Include(c => c.TravelAdvance)
            .Include(c => c.Lines).ThenInclude(l => l.PerDiemRate)
            .Include(c => c.Lines).ThenInclude(l => l.ReviewedBy)
            .FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted);
    }

    public async Task<IEnumerable<StaffTravelExpenseClaim>> GetAllWithDetailsAsync()
    {
        return await _dbSet
            .Include(c => c.Employee)
            .Include(c => c.StaffTravelRequest)
            .Where(c => !c.IsDeleted)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffTravelExpenseClaim>> GetByRequestIdAsync(Guid requestId)
    {
        return await _dbSet
            .Include(c => c.Employee)
            .Where(c => c.StaffTravelRequestId == requestId && !c.IsDeleted)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffTravelExpenseClaim>> GetByEmployeeIdAsync(Guid employeeId)
    {
        return await _dbSet
            .Where(c => c.EmployeeId == employeeId && !c.IsDeleted)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffTravelExpenseClaim>> GetByStatusAsync(TravelClaimStatus status)
    {
        return await _dbSet
            .Include(c => c.Employee)
            .Where(c => c.Status == status && !c.IsDeleted)
            .OrderByDescending(c => c.SubmittedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffTravelExpenseClaim>> GetByAdvanceIdAsync(Guid advanceId)
    {
        return await _dbSet
            .Where(c => c.TravelAdvanceId == advanceId && !c.IsDeleted)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffTravelExpenseClaim>> GetUnpaidApprovedClaimsAsync()
    {
        return await _dbSet
            .Include(c => c.Employee)
            .Where(c => c.Status == TravelClaimStatus.Approved && c.PaidAt == null && !c.IsDeleted)
            .OrderBy(c => c.SubmittedAt)
            .ToListAsync();
    }
}

#endregion

#region Staff Travel Expense Claim Line Repository

public class StaffTravelExpenseClaimLineRepository : GenericRepository<StaffTravelExpenseClaimLine>, IStaffTravelExpenseClaimLineRepository
{
    public StaffTravelExpenseClaimLineRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<StaffTravelExpenseClaimLine>> GetByClaimIdAsync(Guid claimId)
    {
        return await _dbSet
            .Include(l => l.PerDiemRate)
            .Where(l => l.StaffTravelExpenseClaimId == claimId && !l.IsDeleted)
            .OrderBy(l => l.ExpenseDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffTravelExpenseClaimLine>> GetPendingReviewAsync(Guid claimId)
    {
        return await _dbSet
            .Where(l => l.StaffTravelExpenseClaimId == claimId
                     && l.Status == TravelExpenseLineStatus.Pending && !l.IsDeleted)
            .OrderBy(l => l.ExpenseDate)
            .ToListAsync();
    }
}

#endregion

#region Staff Travel Advance Repository

public class StaffTravelAdvanceRepository : GenericRepository<StaffTravelAdvance>, IStaffTravelAdvanceRepository
{
    public StaffTravelAdvanceRepository(ApplicationDbContext context) : base(context) { }

    public async Task<StaffTravelAdvance?> GetWithDetailsAsync(Guid tenantId, Guid id)
    {
        return await _dbSet
            .Include(a => a.Employee)
            .Include(a => a.StaffTravelRequest)
            .Include(a => a.ApprovedBy)
            .Include(a => a.DisbursedBy)
            .Include(a => a.RejectedBy)
            .Include(a => a.CancelledBy)
            .Include(a => a.WrittenOffBy)
            .Include(a => a.RefundedBy)
            .FirstOrDefaultAsync(a => a.Id == id && a.TenantId == tenantId && !a.IsDeleted);
    }

    public async Task<StaffTravelAdvance?> GetByAdvanceNumberAsync(string advanceNumber)
    {
        return await _dbSet
            .Include(a => a.Employee)
            .Include(a => a.StaffTravelRequest)
            .FirstOrDefaultAsync(a => a.AdvanceNumber == advanceNumber && !a.IsDeleted);
    }

    // The summary carries the trip's number (lane 3), so every list read includes the request.

    public async Task<IEnumerable<StaffTravelAdvance>> GetAllWithDetailsAsync()
    {
        return await _dbSet
            .Include(a => a.Employee)
            .Include(a => a.StaffTravelRequest)
            .Where(a => !a.IsDeleted)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffTravelAdvance>> GetByRequestIdAsync(Guid requestId)
    {
        return await _dbSet
            .Include(a => a.Employee)
            .Include(a => a.StaffTravelRequest)
            .Where(a => a.StaffTravelRequestId == requestId && !a.IsDeleted)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffTravelAdvance>> GetByEmployeeIdAsync(Guid employeeId)
    {
        return await _dbSet
            .Include(a => a.Employee)
            .Include(a => a.StaffTravelRequest)
            .Where(a => a.EmployeeId == employeeId && !a.IsDeleted)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffTravelAdvance>> GetByStatusAsync(TravelAdvanceStatus status)
    {
        return await _dbSet
            .Include(a => a.Employee)
            .Include(a => a.StaffTravelRequest)
            .Where(a => a.Status == status && !a.IsDeleted)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync();
    }

    /// <summary>Money the employee still holds (<see cref="ErpSystem.Core.Services.HR.StaffTravelAdvanceRules.CashOut"/>).
    /// It read every row with something unsettled, so a requested advance — unsettled from creation until lane 3 —
    /// counted as money owed.</summary>
    public async Task<IEnumerable<StaffTravelAdvance>> GetOutstandingByEmployeeAsync(Guid employeeId)
    {
        return await _dbSet
            .Include(a => a.Employee)
            .Include(a => a.StaffTravelRequest)
            .Where(a => a.EmployeeId == employeeId && !a.IsDeleted)
            .Where(ErpSystem.Core.Services.HR.StaffTravelAdvanceRules.CashOut)
            .OrderBy(a => a.SettlementDeadline)
            .ToListAsync();
    }

    /// <summary>Cash out past its deadline, whether or not the sweep has written Overdue yet.</summary>
    public async Task<IEnumerable<StaffTravelAdvance>> GetOverdueSettlementsAsync()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        return await _dbSet
            .Include(a => a.Employee)
            .Include(a => a.StaffTravelRequest)
            .Where(a => !a.IsDeleted && a.SettlementDeadline != null && a.SettlementDeadline < today)
            .Where(ErpSystem.Core.Services.HR.StaffTravelAdvanceRules.CashOut)
            .OrderBy(a => a.SettlementDeadline)
            .ToListAsync();
    }
}

#endregion

#region Staff Travel Per Diem Rate Repository

public class StaffTravelPerDiemRateRepository : GenericRepository<StaffTravelPerDiemRate>, IStaffTravelPerDiemRateRepository
{
    public StaffTravelPerDiemRateRepository(ApplicationDbContext context) : base(context) { }

    public async Task<StaffTravelPerDiemRate?> GetWithDetailsAsync(Guid tenantId, Guid id)
    {
        return await _dbSet
            .Include(r => r.Country)
            .Include(r => r.StaffLevel)
            .FirstOrDefaultAsync(r => r.Id == id && r.TenantId == tenantId && !r.IsDeleted);
    }

    public async Task<IEnumerable<StaffTravelPerDiemRate>> GetActiveRatesAsync()
    {
        return await _dbSet
            .Include(r => r.Country)
            .Include(r => r.StaffLevel)
            .Where(r => r.IsActive && !r.IsDeleted)
            .OrderBy(r => r.Country.Name)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffTravelPerDiemRate>> GetByCountryAsync(Guid countryId)
    {
        return await _dbSet
            .Include(r => r.StaffLevel)
            .Where(r => r.CountryId == countryId && !r.IsDeleted)
            .OrderByDescending(r => r.EffectiveFrom)
            .ToListAsync();
    }

    public async Task<StaffTravelPerDiemRate?> GetEffectiveRateAsync(Guid countryId, string? city, Guid? staffLevelId, DateOnly onDate)
    {
        var candidates = await _dbSet
            .Where(r => r.CountryId == countryId && r.IsActive && !r.IsDeleted
                     && r.EffectiveFrom <= onDate
                     && (r.EffectiveTo == null || r.EffectiveTo >= onDate)
                     && (r.City == null || r.City == city)
                     && (r.StaffLevelId == null || r.StaffLevelId == staffLevelId))
            .ToListAsync();

        // Prefer the most specific match: city- and level-specific overrides win over generic rates.
        return candidates
            .OrderByDescending(r => r.City != null ? 1 : 0)
            .ThenByDescending(r => r.StaffLevelId != null ? 1 : 0)
            .ThenByDescending(r => r.EffectiveFrom)
            .FirstOrDefault();
    }
}

#endregion
