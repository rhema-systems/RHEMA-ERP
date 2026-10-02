using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.HR.StaffTravel;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Core.Services.HR.Finance;
using ErpSystem.Application.HR.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

// ============================================================================
// STAFF TRAVEL — GROUP 5: FINANCE SERVICE (BUDGET, EXPENSES & ADVANCES)
// ============================================================================

#region Staff Travel Finance Service

public class StaffTravelFinanceService : IStaffTravelFinanceService
{
    private readonly IStaffTravelBudgetRepository _budgetRepository;
    private readonly IStaffTravelExpenseClaimRepository _claimRepository;
    private readonly IStaffTravelExpenseClaimLineRepository _lineRepository;
    private readonly IStaffTravelAdvanceRepository _advanceRepository;
    private readonly IStaffTravelRequestRepository _requestRepository;
    private readonly HrCurrencyBridge _currency;
    private readonly StaffTravelBudgetRollup _budgetRollup;
    private readonly IStaffTravelPerDiemRateRepository _perDiemRepository;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IHrFinancePostingAdapter _financePosting;
    private readonly IHrFinancePostingAdminService _postingRegister;
    private readonly ILogger<StaffTravelFinanceService> _logger;

    public StaffTravelFinanceService(
        IStaffTravelBudgetRepository budgetRepository,
        IStaffTravelExpenseClaimRepository claimRepository,
        IStaffTravelExpenseClaimLineRepository lineRepository,
        IStaffTravelAdvanceRepository advanceRepository,
        IStaffTravelRequestRepository requestRepository,
        HrCurrencyBridge currency,
        StaffTravelBudgetRollup budgetRollup,
        IStaffTravelPerDiemRateRepository perDiemRepository,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        IHrFinancePostingAdapter financePosting,
        IHrFinancePostingAdminService postingRegister,
        ILogger<StaffTravelFinanceService> logger)
    {
        _postingRegister = postingRegister;
        _budgetRepository = budgetRepository;
        _claimRepository = claimRepository;
        _lineRepository = lineRepository;
        _advanceRepository = advanceRepository;
        _requestRepository = requestRepository;
        _currency = currency;
        _budgetRollup = budgetRollup;
        _perDiemRepository = perDiemRepository;
        _currentUserProvider = currentUserProvider;
        _unitOfWork = unitOfWork;
        _financePosting = financePosting;
        _logger = logger;
    }

    /// <summary>
    /// A claim or advance whose approval, payment or disbursement is in Finance's ledger is not
    /// edited into a different number (HR finish plan lane 8). The way out is the register's
    /// reversal, which is explicit and reasoned; then the edit.
    /// </summary>
    private Task GuardClaimNotPostedAsync(Guid claimId, string action, CancellationToken cancellationToken)
        => _financePosting.EnsureNotPostedAsync(
            HrFinancePostingEventCatalog.SourceStaffTravelExpenseClaim, claimId, action, cancellationToken);

    private Task GuardAdvanceNotPostedAsync(Guid advanceId, string action, CancellationToken cancellationToken)
        => _financePosting.EnsureNotPostedAsync(
            HrFinancePostingEventCatalog.SourceStaffTravelAdvance, advanceId, action, cancellationToken);

    // The ApplicationDbContext is registered without a tenant, so its global tenant query-filter and
    // TenantId auto-stamp are inert. Following the RHEMA convention, this service scopes every read and
    // mutation to the authenticated tenant explicitly and passes it into the repository predicate.
    private Guid GetTenantId()
    {
        var tenantId = _currentUserProvider.TenantId;
        if (tenantId == Guid.Empty)
            throw new InvalidOperationException("No tenant is associated with the current user.");
        return tenantId;
    }

    private Guid RequireCurrentTenant(Guid tenantId)
    {
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");
        return current;
    }

    private async Task<StaffTravelBudget> GetOwnedBudgetAsync(Guid id)
    {
        var entity = await _budgetRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Budget with ID '{id}' not found.");
        return entity;
    }

    private async Task<StaffTravelExpenseClaim> GetOwnedClaimAsync(Guid id)
    {
        var entity = await _claimRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Expense claim with ID '{id}' not found.");
        return entity;
    }

    private async Task<StaffTravelExpenseClaimLine> GetOwnedClaimLineAsync(Guid id)
    {
        var entity = await _lineRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Expense claim line with ID '{id}' not found.");
        return entity;
    }

    private async Task<StaffTravelAdvance> GetOwnedAdvanceAsync(Guid id)
    {
        var entity = await _advanceRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Advance with ID '{id}' not found.");
        return entity;
    }

    private async Task<StaffTravelPerDiemRate> GetOwnedPerDiemRateAsync(Guid id)
    {
        var entity = await _perDiemRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Per-diem rate with ID '{id}' not found.");
        return entity;
    }

    // ---- Budget ------------------------------------------------------------

    /// <summary>
    /// A request's travel budget, with its committed and actual spend recomputed as it is read.
    /// </summary>
    /// <remarks>
    /// ⚠ <b>Recomputed on read, not merely on write.</b> Bookings and claims change constantly and
    /// the budget row is written rarely, so a rollup persisted only at write time would be stale
    /// almost immediately — and a stale spend figure is the same failure as the hand-entered one it
    /// replaces, just slower to notice. The write path still persists the figures so the stored row
    /// is not nonsense for anything reading the table directly, but this read is the authority.
    /// </remarks>
    public async Task<StaffTravelBudgetDto?> GetBudgetByRequestAsync(Guid requestId, CancellationToken cancellationToken = default)
    {
        var entity = await _budgetRepository.GetByRequestIdAsync(requestId);
        if (entity == null || entity.TenantId != GetTenantId())
            return null;

        return await ToBudgetDtoAsync(entity, await RequireOwnedRequestAsync(entity.StaffTravelRequestId), cancellationToken);
    }

    /// <summary>
    /// The budget as read: its spend recomputed, its parts, the trip's approved budget beside it and whether either
    /// spend figure is past the approved total (lane 3, B10, O-9).
    /// </summary>
    private async Task<StaffTravelBudgetDto> ToBudgetDtoAsync(
        StaffTravelBudget entity, StaffTravelRequest request, CancellationToken cancellationToken)
    {
        var dto = entity.ToDto();
        var spend = await _budgetRollup.ComputeAsync(
            entity.TenantId, entity.StaffTravelRequestId, entity.CurrencyCode, cancellationToken);

        // ⚠ Onto the DTO, NOT onto the entity. The entity here is tracked, so assigning to it would
        // queue an UPDATE for whatever else in the request calls SaveChangesAsync — a read that
        // silently writes. The write paths do the persisting, deliberately and visibly.
        dto.TotalCommitted = spend.Committed;
        dto.TotalActual = spend.Actual;
        dto.ActualClaimsPaid = spend.ClaimsPaid;
        dto.ActualAdvancesPaidOut = spend.AdvancesPaidOut;
        dto.Variance = dto.ApprovedTotal - spend.Actual;
        dto.TripApprovedBudget = TripBudget(request);
        // Whether an overrun refuses anything is TDC's question (§ 6); the card says so either way.
        dto.CommittedOverrun = spend.Committed > dto.ApprovedTotal;
        dto.ActualOverrun = spend.Actual > dto.ApprovedTotal;
        return dto;
    }

    /// <summary>
    /// Sets a trip's budget (lane 3, B10, O-9, T-22, D-16): only once the trip is approved; in the trip's currency;
    /// its total, when none is given, the trip's approved budget, and never above it; its allocation left empty or
    /// adding up to the total. The currency and the total were the caller's — a budget in any currency, of any size,
    /// beside an approved budget it had no link to.
    /// </summary>
    public async Task<StaffTravelBudgetDto> CreateBudgetAsync(CreateStaffTravelBudgetDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        var request = await RequireOwnedRequestAsync(createDto.StaffTravelRequestId);
        RequireTripTakesBudget(request, "A budget is set");

        var existing = await _budgetRepository.GetByRequestIdAsync(createDto.StaffTravelRequestId);
        if (existing != null && existing.TenantId == tenantId)
            throw new InvalidOperationException($"Travel request {request.RequestNumber} already has a budget; change that one.");

        var total = RequireBudgetTotal(request, createDto.ApprovedTotal,
            createDto.FlightBudget, createDto.AccommodationBudget, createDto.PerDiemBudget,
            createDto.TransportBudget, createDto.MiscellaneousBudget);

        var entity = createDto.ToEntity(tenantId, createdByUserId, request.CurrencyCode, total);
        await ApplyRollupAsync(entity, cancellationToken);
        await _budgetRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return await ToBudgetDtoAsync(entity, request, cancellationToken);
    }

    /// <summary>
    /// Approves a trip's budget (lane 3, B10, T-22): a travel administrator who is not the traveller, once. The
    /// budget's <c>ApprovedById</c> and <c>ApprovedAt</c> had no writer, so every budget read as unapproved.
    /// </summary>
    public async Task<StaffTravelBudgetDto> ApproveBudgetAsync(Guid budgetId, Guid approverEmployeeId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedBudgetAsync(budgetId);
        var request = await RequireOwnedRequestAsync(entity.StaffTravelRequestId);
        RequireTripTakesBudget(request, "A budget is approved");
        if (entity.ApprovedAt is DateTime approvedAt)
            throw new InvalidOperationException(
                $"The budget of travel request {request.RequestNumber} was approved on {approvedAt:d MMM yyyy}.");
        if (request.EmployeeId == approverEmployeeId)
            throw new UnauthorizedAccessException(
                $"You cannot approve the budget of your own trip ({request.RequestNumber}). Another officer must.");
        // The trip's approved budget is fixed once the trip is approved, but a budget set before lane 3 was never
        // held to it — nor to the trip's currency, nor to a total at all: approval is where it is.
        if (entity.CurrencyCode != request.CurrencyCode)
            throw new InvalidOperationException(
                $"The budget is in {entity.CurrencyCode} and its trip in {request.CurrencyCode}. Change the budget to state it " +
                $"in {request.CurrencyCode}, then approve it.");
        if (entity.ApprovedTotal <= 0m)
            throw new InvalidOperationException("The budget has no total. Change it to give one, then approve it.");
        RequireBudgetTotal(request, entity.ApprovedTotal,
            entity.FlightBudget, entity.AccommodationBudget, entity.PerDiemBudget,
            entity.TransportBudget, entity.MiscellaneousBudget);

        entity.ApprovedById = approverEmployeeId;   // the caller, not a payload value
        entity.ApprovedAt = DateTime.UtcNow;
        entity.UpdatedBy = _currentUserProvider.UserId.ToString();
        entity.UpdatedAt = DateTime.UtcNow;
        await ApplyRollupAsync(entity, cancellationToken);
        // Tracked, so the save writes it; `UpdateAsync` would mark the approver loaded beside it modified too.
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Travel budget approved for {RequestNumber}: {Currency} {Total}",
            request.RequestNumber, entity.CurrencyCode, entity.ApprovedTotal);
        var reloaded = await _budgetRepository.GetByRequestIdAsync(entity.StaffTravelRequestId);
        return await ToBudgetDtoAsync(reloaded ?? entity, request, cancellationToken);
    }

    /// <summary>The trip's approved budget, or its estimate on a trip approved before lane 2 set one.</summary>
    private static decimal TripBudget(StaffTravelRequest request) => request.ApprovedBudget ?? request.EstimatedTotalCost;

    /// <summary>D-16: a budget is for a trip that is approved, under way or completed.</summary>
    private static void RequireTripTakesBudget(StaffTravelRequest request, string what)
    {
        if (request.Status is StaffTravelRequestStatus.Approved or StaffTravelRequestStatus.InProgress or StaffTravelRequestStatus.Completed)
            return;
        throw new InvalidOperationException(
            $"{what} only once its trip is approved; travel request {request.RequestNumber} is " +
            $"{Humanise(request.Status.ToString()).ToLowerInvariant()}.");
    }

    /// <summary>
    /// O-9: the budget's total — the trip's approved budget when 0 is given — above zero and within the trip's approved
    /// budget; its allocation left at zero or adding up to the total exactly.
    /// </summary>
    private static decimal RequireBudgetTotal(
        StaffTravelRequest request, decimal given, decimal flight, decimal accommodation, decimal perDiem, decimal transport, decimal miscellaneous)
    {
        var tripBudget = TripBudget(request);
        var tripBudgetName = request.ApprovedBudget.HasValue ? "approved budget" : "estimate";
        var total = given == 0m ? tripBudget : given;
        if (total <= 0m)
            throw new InvalidOperationException(
                $"Travel request {request.RequestNumber} has no {tripBudgetName} to take the total from; give the budget's total.");
        if (total > tripBudget)
            throw new InvalidOperationException(
                $"The budget's total ({request.CurrencyCode} {total:N2}) is above the trip's {tripBudgetName} " +
                $"({request.CurrencyCode} {tripBudget:N2}).");

        var allocated = flight + accommodation + perDiem + transport + miscellaneous;
        if (allocated != 0m && allocated != total)
            throw new InvalidOperationException(
                $"The budget's parts add up to {request.CurrencyCode} {allocated:N2}, not its total of " +
                $"{request.CurrencyCode} {total:N2}. Make them add up to the total, or leave them all at zero.");
        return total;
    }

    /// <summary>
    /// Recomputes a budget's committed and actual spend from the request's own bookings and claims.
    /// </summary>
    /// <remarks>
    /// <para>All three figures were caller-declared — <c>UpdateEntity</c> assigned
    /// <c>TotalCommitted</c> and <c>TotalActual</c> straight off the DTO and derived
    /// <c>Variance</c> from them — so the budget screen showed whatever was last typed while the
    /// records that constitute the spend sat unread on the same request. See
    /// <see cref="StaffTravelBudgetRollup"/> for what each figure means and why they are not
    /// summed.</para>
    ///
    /// <para><b>Variance is now approved-versus-actual</b> (<c>ApprovedTotal - TotalActual</c>),
    /// which is what a budget variance means and what <c>JobAnalysisService</c> already computes
    /// for its own budgets. It was <c>TotalActual - TotalCommitted</c> — a different quantity
    /// entirely, with the sign of an overspend inverted relative to the rest of the codebase.</para>
    ///
    /// <para>Recomputed on every write rather than cached against booking changes: a travel budget
    /// is read far less often than its bookings are edited, and a stale rollup is precisely the
    /// failure this replaces.</para>
    /// </remarks>
    private async Task ApplyRollupAsync(StaffTravelBudget entity, CancellationToken cancellationToken)
    {
        var spend = await _budgetRollup.ComputeAsync(
            entity.TenantId, entity.StaffTravelRequestId, entity.CurrencyCode, cancellationToken);

        entity.TotalCommitted = spend.Committed;
        entity.TotalActual = spend.Actual;
        entity.Variance = entity.ApprovedTotal - spend.Actual;
    }

    /// <summary>
    /// Changes a trip's budget under the rules of <see cref="CreateBudgetAsync"/>. A change to an approved budget
    /// withdraws its approval: what was approved is not what it now says. A budget kept in another currency before
    /// lane 3 moves to the trip's — the form states the figures in the trip's currency.
    /// </summary>
    public async Task<StaffTravelBudgetDto> UpdateBudgetAsync(UpdateStaffTravelBudgetDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedBudgetAsync(updateDto.Id);
        var request = await RequireOwnedRequestAsync(entity.StaffTravelRequestId);
        RequireTripTakesBudget(request, "A budget is changed");

        var total = RequireBudgetTotal(request, updateDto.ApprovedTotal,
            updateDto.FlightBudget, updateDto.AccommodationBudget, updateDto.PerDiemBudget,
            updateDto.TransportBudget, updateDto.MiscellaneousBudget);

        var changed = entity.ApprovedTotal != total || entity.BudgetYear != updateDto.BudgetYear
            || entity.FlightBudget != updateDto.FlightBudget || entity.AccommodationBudget != updateDto.AccommodationBudget
            || entity.PerDiemBudget != updateDto.PerDiemBudget || entity.TransportBudget != updateDto.TransportBudget
            || entity.MiscellaneousBudget != updateDto.MiscellaneousBudget || entity.CurrencyCode != request.CurrencyCode;

        entity.UpdateEntity(updateDto, updatedByUserId, total);
        entity.CurrencyCode = request.CurrencyCode;
        if (changed)
        {
            entity.ApprovedById = null;
            entity.ApprovedAt = null;
        }
        await ApplyRollupAsync(entity, cancellationToken);
        // Tracked, so the save writes it; `UpdateAsync` would mark the approver loaded beside it modified too.
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var reloaded = await _budgetRepository.GetByRequestIdAsync(entity.StaffTravelRequestId);
        return await ToBudgetDtoAsync(reloaded ?? entity, request, cancellationToken);
    }

    // ---- Expense claims ----------------------------------------------------

    public async Task<StaffTravelExpenseClaimDto> GetClaimByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _claimRepository.GetWithLinesAsync(id);
        if (entity == null || entity.TenantId != tenantId)
            throw new ArgumentException($"Expense claim with ID '{id}' not found.");
        return entity.ToDto();
    }

    public async Task<StaffTravelExpenseClaimDto?> GetClaimByNumberAsync(string claimNumber, CancellationToken cancellationToken = default)
    {
        var entity = await _claimRepository.GetByClaimNumberAsync(claimNumber);
        if (entity == null || entity.TenantId != GetTenantId())
            return null;
        return entity.ToDto();
    }

    public async Task<IEnumerable<StaffTravelExpenseClaimSummaryDto>> GetAllClaimsAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _claimRepository.GetAllWithDetailsAsync())
            .Where(c => c.TenantId == tenantId)
            .Select(c => c.ToSummaryDto())
            .ToList();
    }

    public async Task<IEnumerable<StaffTravelExpenseClaimSummaryDto>> GetClaimsByRequestAsync(Guid requestId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _claimRepository.GetByRequestIdAsync(requestId))
            .Where(c => c.TenantId == tenantId)
            .Select(c => c.ToSummaryDto())
            .ToList();
    }

    public async Task<IEnumerable<StaffTravelExpenseClaimSummaryDto>> GetClaimsByEmployeeAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _claimRepository.GetByEmployeeIdAsync(employeeId))
            .Where(c => c.TenantId == tenantId)
            .Select(c => c.ToSummaryDto())
            .ToList();
    }

    public async Task<IEnumerable<StaffTravelExpenseClaimSummaryDto>> GetClaimsByStatusAsync(TravelClaimStatus status, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _claimRepository.GetByStatusAsync(status))
            .Where(c => c.TenantId == tenantId)
            .Select(c => c.ToSummaryDto())
            .ToList();
    }

    public async Task<IEnumerable<StaffTravelExpenseClaimSummaryDto>> GetUnpaidApprovedClaimsAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _claimRepository.GetUnpaidApprovedClaimsAsync())
            .Where(c => c.TenantId == tenantId)
            .Select(c => c.ToSummaryDto())
            .ToList();
    }

    /// <summary>
    /// Files a claim for the trip's traveller, kept in the base currency (lane 3, B3 and B11): only on a trip that
    /// is approved, under way or completed; an advance it names is this trip's and this traveller's; lines sent with
    /// it are checked and valued exactly as lines added later (B5 — they were stored at rate 0, worth nothing).
    /// </summary>
    public async Task<StaffTravelExpenseClaimDto> CreateClaimAsync(CreateStaffTravelExpenseClaimDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        var request = await RequireOwnedRequestAsync(createDto.StaffTravelRequestId);
        StaffTravelRequestGuards.RequireOpen(request, "an expense claim");
        RequireTripTakesClaims(request, "An expense claim can be filed");
        await RequireClaimableAdvanceAsync(request, createDto.TravelAdvanceId, cancellationToken);
        var baseCurrency = await RequireBaseCurrencyAsync(cancellationToken);

        var entity = createDto.ToEntity(tenantId, createdByUserId, request.EmployeeId, baseCurrency);
        entity.ClaimNumber = await GenerateClaimNumberAsync(tenantId, cancellationToken);
        entity.Status = TravelClaimStatus.Draft;
        foreach (var line in entity.Lines)
        {
            await RequireLineReferencesAsync(request, line.ReceiptAttachmentId, line.PerDiemRateId, cancellationToken);
            await ApplyBaseCurrencyAmountAsync(line, cancellationToken);
        }
        entity.TotalClaimed = entity.Lines.Sum(l => l.AmountBaseCurrency);

        // Nothing is approved on a draft claim, so the payable figure is provisional and equals the
        // claim less any advance. RecomputeClaimTotalsAsync then owns it from the first line review
        // onwards, on the approved figure. Both go through the same helper so the two cannot drift:
        // this field previously had one formula here and a different one there.
        entity.NetPayable = ComputeNetPayable(entity.TotalClaimed, entity.AdvanceDeducted);

        await _claimRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Expense claim created: {ClaimNumber}", entity.ClaimNumber);

        var refreshed = await _claimRepository.GetWithLinesAsync(entity.Id);
        if (refreshed == null || refreshed.TenantId != GetTenantId())
            throw new ArgumentException($"Expense claim with ID '{entity.Id}' not found.");
        return refreshed.ToDto();
    }

    /// <summary>The claim's type and the advance it settles — while it is a draft or returned (lane 3, B6).</summary>
    public async Task<StaffTravelExpenseClaimDto> UpdateClaimAsync(UpdateStaffTravelExpenseClaimDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedClaimAsync(updateDto.Id);

        RequireClaimEditable(entity);
        await GuardClaimNotPostedAsync(entity.Id, "Editing this claim", cancellationToken);
        var request = await RequireOwnedRequestAsync(entity.StaffTravelRequestId);
        await RequireClaimableAdvanceAsync(request, updateDto.TravelAdvanceId, cancellationToken);

        entity.UpdateEntity(updateDto, updatedByUserId);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var refreshed = await _claimRepository.GetWithLinesAsync(entity.Id);
        if (refreshed == null || refreshed.TenantId != GetTenantId())
            throw new ArgumentException($"Expense claim with ID '{entity.Id}' not found.");
        return refreshed.ToDto();
    }

    public async Task<bool> DeleteClaimAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedClaimAsync(id);

        if (entity.Status != TravelClaimStatus.Draft)
            throw new InvalidOperationException("Only draft claims can be deleted.");

        await _claimRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    /// <summary>
    /// Submits a draft or returned claim (lane 3, D-1's claim half): with at least one expense, on a trip that
    /// takes claims; under the approved policy the trip was checked against, every expense above its receipt
    /// threshold carries a receipt (a per diem excepted), and a first submission comes within its claim window
    /// after the trip ends. A returned claim was filed in time, so the window does not apply to it again.
    /// </summary>
    public async Task<bool> SubmitClaimAsync(Guid claimId, Guid submittedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedClaimAsync(claimId);

        if (entity.Status is not (TravelClaimStatus.Draft or TravelClaimStatus.Returned))
            throw new InvalidOperationException(
                $"Claim {entity.ClaimNumber} is {Describe(entity.Status)}; only a draft or returned claim can be submitted.");
        var request = await RequireOwnedRequestAsync(entity.StaffTravelRequestId);
        RequireTripTakesClaims(request, "A claim can be submitted");
        var lines = await ClaimLinesAsync(entity, cancellationToken);
        if (lines.Count == 0)
            throw new InvalidOperationException("Add the expenses before submitting the claim.");

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var policy = await ApprovedPolicyAsync(request, cancellationToken);
        if (policy is not null)
        {
            if (entity.Status == TravelClaimStatus.Draft && policy.ExpenseSubmissionDays > 0)
            {
                var lastDay = request.TravelEndDate.AddDays(policy.ExpenseSubmissionDays);
                if (today > lastDay)
                    throw new InvalidOperationException(
                        $"Claims for {request.RequestNumber} had to be submitted by {lastDay:d MMM yyyy} — " +
                        $"{policy.ExpenseSubmissionDays} days after the trip ended, under {policy.PolicyName}.");
            }

            if (policy.ReceiptRequiredAbove > 0m)
            {
                var threshold = await _currency.ConvertBetweenAsync(
                    policy.ReceiptRequiredAbove, policy.CurrencyCode ?? entity.CurrencyCode, entity.CurrencyCode, today, cancellationToken);
                // A per diem is a flat daily allowance — there is no receipt behind it to ask for.
                var missing = lines
                    .Where(l => !l.IsPerDiem && l.ReceiptAttachmentId is null && l.AmountBaseCurrency > threshold)
                    .OrderBy(l => l.ExpenseDate)
                    .ToList();
                if (missing.Count > 0)
                    throw new InvalidOperationException(
                        $"{missing.Count} expense(s) above {entity.CurrencyCode} {threshold:N2} have no receipt, which " +
                        $"{policy.PolicyName} requires: " +
                        string.Join("; ", missing.Take(3).Select(l =>
                            $"{Humanise(l.ExpenseCategory.ToString())} on {l.ExpenseDate:d MMM yyyy} ({entity.CurrencyCode} {l.AmountBaseCurrency:N2})")) +
                        ". Attach each receipt to the trip and link it to its expense.");
            }
        }

        entity.Status = TravelClaimStatus.Submitted;
        entity.SubmittedAt = DateTime.UtcNow;
        entity.UpdatedBy = submittedByUserId.ToString();
        entity.UpdatedAt = DateTime.UtcNow;

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    /// <summary>
    /// Records the review's outcome (lane 3, B1, B4, D-2, N4). Only a submitted claim or one under review; never the
    /// claimant's own. Approving takes what the lines' reviews approved — every line decided and something approved —
    /// and records Approved when all of it was, PartiallyApproved otherwise; it was set outright, so a claim with
    /// no reviewed line was approved and paid its whole claimed total while Finance recognised nothing. Rejecting
    /// or returning needs the reason, kept on the claim for the claimant.
    /// </summary>
    public async Task<bool> ReviewClaimAsync(ReviewStaffTravelExpenseClaimDto reviewDto, Guid reviewerEmployeeId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedClaimAsync(reviewDto.ClaimId);

        if (entity.Status is not (TravelClaimStatus.Submitted or TravelClaimStatus.UnderReview))
            throw new InvalidOperationException(
                $"Claim {entity.ClaimNumber} is {Describe(entity.Status)}; only a submitted claim or one under review is reviewed.");
        RefuseOwnClaim(entity, reviewerEmployeeId, "review");
        var notes = string.IsNullOrWhiteSpace(reviewDto.Notes) ? null : reviewDto.Notes.Trim();

        TravelClaimStatus outcome;
        switch (reviewDto.NewStatus)
        {
            case TravelClaimStatus.UnderReview:
                if (entity.Status == TravelClaimStatus.UnderReview)
                    throw new InvalidOperationException($"Claim {entity.ClaimNumber} is already under review.");
                outcome = TravelClaimStatus.UnderReview;
                break;
            case TravelClaimStatus.Rejected:
            case TravelClaimStatus.Returned:
                if (notes is null)
                    throw new InvalidOperationException(
                        $"Say why the claim is {(reviewDto.NewStatus == TravelClaimStatus.Rejected ? "rejected" : "returned")} — the claimant is told.");
                outcome = reviewDto.NewStatus;
                break;
            case TravelClaimStatus.Approved:
            case TravelClaimStatus.PartiallyApproved:
                var lines = await ClaimLinesAsync(entity, cancellationToken);
                var undecided = lines.Count(l => l.Status is not (TravelExpenseLineStatus.Approved or TravelExpenseLineStatus.Rejected));
                if (lines.Count == 0 || undecided > 0)
                    throw new InvalidOperationException(
                        $"Review every expense first — {undecided} of {lines.Count} still to decide.");
                var approved = lines.Sum(l => l.AmountApproved ?? 0m);
                if (approved <= 0m)
                    throw new InvalidOperationException("Nothing on this claim was approved. Reject it instead, with the reason.");
                outcome = approved == lines.Sum(l => l.AmountBaseCurrency)
                    ? TravelClaimStatus.Approved
                    : TravelClaimStatus.PartiallyApproved;
                break;
            default:
                throw new InvalidOperationException(
                    $"'{reviewDto.NewStatus}' is not a review outcome — approve, reject or return the claim, or mark it under review.");
        }

        var approving = outcome is TravelClaimStatus.Approved or TravelClaimStatus.PartiallyApproved;
        // Moving a claim whose approval Finance already holds to anything other than an approved
        // state would leave the expense recognised with nothing behind it; reverse first.
        if (!approving)
            await GuardClaimNotPostedAsync(entity.Id, "Changing the review outcome", cancellationToken);

        // Review and its Finance recognition commit together (HR finish plan lane 8). A second
        // approval of the same claim is answered by the register as a duplicate, not re-posted.
        await _financePosting.RunAsync(async ct =>
        {
            entity.Status = outcome;
            // The outcome's own words; marking a claim under review keeps the last ones. A returned claim
            // approved later must not keep showing why it was returned.
            if (outcome != TravelClaimStatus.UnderReview || notes is not null)
                entity.ReviewNotes = notes;
            entity.FinanceReviewedById = reviewerEmployeeId;   // the caller, not a payload value
            entity.FinanceReviewedAt = DateTime.UtcNow;        // ...and the clock, not one either

            entity.UpdatedBy = _currentUserProvider.UserId.ToString();
            entity.UpdatedAt = DateTime.UtcNow;

            await _unitOfWork.SaveChangesAsync(ct);

            return approving ? HrFinancePostingCommandFactory.TravelClaimApproved(entity) : null;
        }, _currentUserProvider.UserId, cancellationToken);

        _logger.LogInformation("Expense claim reviewed: {ClaimNumber}, NewStatus: {Status}", entity.ClaimNumber, entity.Status);
        return true;
    }

    /// <summary>
    /// Pays an approved or partly approved claim, once (lane 3, B1, D-2, D-10, O-2): on a trip that takes claims;
    /// never by payroll offset, which pays nobody yet; never by the claimant, the claim's reviewer or anyone who
    /// reviewed one of its expenses; and not in full past advance cash the traveller holds on the trip that the
    /// claim does not name, unless the payer records why. The payer is recorded.
    /// </summary>
    public async Task<bool> PayClaimAsync(PayStaffTravelExpenseClaimDto payDto, Guid payerEmployeeId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedClaimAsync(payDto.ClaimId);

        if (entity.Status is not (TravelClaimStatus.Approved or TravelClaimStatus.PartiallyApproved))
            throw new InvalidOperationException(entity.Status == TravelClaimStatus.Paid
                ? $"Claim {entity.ClaimNumber} was paid on {entity.PaidAt:d MMM yyyy}."
                : $"Claim {entity.ClaimNumber} is {Describe(entity.Status)}; only an approved claim can be paid.");
        if (entity.TotalApproved <= 0m)
            throw new InvalidOperationException($"Nothing on claim {entity.ClaimNumber} is approved to pay.");
        var request = await RequireOwnedRequestAsync(entity.StaffTravelRequestId);
        RequireTripTakesClaims(request, "A claim can be paid");
        if (payDto.PaymentMethod == TravelPaymentMethod.PayrollOffset)
            throw new InvalidOperationException(
                "Payroll cannot receive travel claims yet, so a claim paid by payroll offset would reach nobody. " +
                "Pay it by bank transfer, cash, cheque or corporate card.");
        RefuseOwnClaim(entity, payerEmployeeId, "pay");
        if (entity.FinanceReviewedById == payerEmployeeId)
            throw new UnauthorizedAccessException(
                $"You reviewed claim {entity.ClaimNumber}, so another officer must pay it — the person who decides an amount " +
                "does not also pay it out.");
        var lines = await ClaimLinesAsync(entity, cancellationToken);
        if (lines.Any(l => l.ReviewedById == payerEmployeeId))
            throw new UnauthorizedAccessException(
                $"You reviewed an expense on claim {entity.ClaimNumber}, so another officer must pay it.");

        // O-2 (T-57): advance cash the claim does not name. Paid as it stands, the claim pays in full and the advance
        // stays outstanding — the traveller is paid twice.
        var named = entity.TravelAdvanceId ?? Guid.Empty;
        var unnamed = await _unitOfWork.Repository<StaffTravelAdvance>()
            .GetQueryable(a => a.TenantId == entity.TenantId && a.StaffTravelRequestId == entity.StaffTravelRequestId
                            && a.EmployeeId == entity.EmployeeId && a.Id != named)
            .Where(StaffTravelAdvanceRules.CashOut)
            .OrderBy(a => a.AdvanceNumber)
            .Select(a => new { a.AdvanceNumber, a.CurrencyCode, a.UnsettledAmount })
            .FirstOrDefaultAsync(cancellationToken);
        var waiver = string.IsNullOrWhiteSpace(payDto.AdvanceWaiverReason) ? null : payDto.AdvanceWaiverReason.Trim();
        if (unnamed is not null && waiver is null)
            throw new InvalidOperationException(
                $"The traveller still holds advance {unnamed.AdvanceNumber} ({unnamed.CurrencyCode} {unnamed.UnsettledAmount:N2}) " +
                "on this trip and the claim does not name it, so paid as it stands the claim pays in full and the advance stays " +
                "outstanding. Link the advance to the claim, or give the reason it is paid in full.");

        // Payment, the advance settlement and the Finance journal commit together (lane 8): the
        // settlement posting carries the advance recovery as its own leg, so it must be built AFTER
        // SettleLinkedAdvanceAsync has decided how much was recovered.
        await _financePosting.RunAsync(async ct =>
        {
            entity.Status = TravelClaimStatus.Paid;
            entity.PaymentMethod = payDto.PaymentMethod;
            entity.PaymentReference = payDto.PaymentReference;
            // When money left is the clock's answer, not the caller's. `PaidAt` is the date every
            // downstream reconciliation will key off, and it was whatever the payload said.
            entity.PaidAt = DateTime.UtcNow;
            entity.PaidById = payerEmployeeId;
            entity.AdvanceWaiverReason = unnamed is null ? null : waiver;
            entity.UpdatedBy = _currentUserProvider.UserId.ToString();
            entity.UpdatedAt = DateTime.UtcNow;

            await SettleLinkedAdvanceAsync(entity, ct);

            await _unitOfWork.SaveChangesAsync(ct);

            return HrFinancePostingCommandFactory.TravelClaimPaid(entity);
        }, _currentUserProvider.UserId, cancellationToken);

        _logger.LogInformation(
            "Expense claim paid: {ClaimNumber}, net {NetPayable}, advance deducted {AdvanceDeducted}",
            entity.ClaimNumber, entity.NetPayable, entity.AdvanceDeducted);
        return true;
    }

    /// <summary>
    /// Voids a claim's payment (lane 3, T-39): a travel administrator who is neither the claimant nor the payer, with
    /// the reason. The payment's journal is reversed, the advance settlement it made is undone, and the claim goes back
    /// to approved — to be paid again, or not. A claim paid in error needed SQL.
    /// </summary>
    /// <remarks>
    /// <para><b>The journal first.</b> A posted <c>TRAVEL_CLAIM_PAID</c> row is reversed through the posting register —
    /// Finance's exact reversal, in the register's own transaction — before the claim changes. Should the claim's save
    /// then fail, the claim still reads Paid beside a reversed row, which the register's retry posts again; the void
    /// can be repeated. A row that never posted (no rule, or Finance refused) is marked Skipped with the reason, so the
    /// register does not hold out a payment that no longer exists. Paying again posts afresh either way: a Reversed
    /// row as its next generation, a Skipped one as a new attempt.</para>
    ///
    /// <para><b>The advance.</b> The payment recovered <c>AdvanceDeducted</c> (in the claim's currency) from the
    /// advance it names; an advance in another currency was settled at Finance's rate on the payment date, so that
    /// rate takes the same amount back (D-15). What comes back is capped at what claims settled on the advance — never
    /// cash the traveller handed back — and a written-off advance is refused: its write-off already accounted for the
    /// rest. The approval's journal stands: the claim is still approved.</para>
    /// </remarks>
    public async Task<bool> VoidClaimPaymentAsync(Guid claimId, VoidStaffTravelClaimPaymentDto voidDto, Guid voiderEmployeeId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedClaimAsync(claimId);
        if (entity.Status != TravelClaimStatus.Paid)
            throw new InvalidOperationException(
                $"Claim {entity.ClaimNumber} is {Describe(entity.Status)}; only a paid claim's payment can be voided.");
        var reason = voidDto.Reason?.Trim() ?? string.Empty;
        if (reason.Length < 5)
            throw new InvalidOperationException("Give the reason the payment is voided, in at least five characters.");
        RefuseOwnClaim(entity, voiderEmployeeId, "void the payment of");
        if (entity.PaidById == voiderEmployeeId)
            throw new UnauthorizedAccessException(
                $"You paid claim {entity.ClaimNumber}, so another officer must void the payment — the person who paid it out " +
                "does not also undo it.");
        var request = await RequireOwnedRequestAsync(entity.StaffTravelRequestId);
        if (request.Status == StaffTravelRequestStatus.Closed)
            throw new InvalidOperationException(
                $"Travel request {request.RequestNumber} is closed, so the payment of claim {entity.ClaimNumber} stands.");

        var paidOn = DateOnly.FromDateTime(entity.PaidAt ?? DateTime.UtcNow);
        StaffTravelAdvance? advance = null;
        var settledBack = 0m;
        if (entity.AdvanceDeducted > 0m && entity.TravelAdvanceId is Guid advanceId)
        {
            advance = await _advanceRepository.GetByIdAsync(advanceId);
            if (advance != null && advance.TenantId == entity.TenantId)
            {
                if (advance.Status == TravelAdvanceStatus.WrittenOff)
                    throw new InvalidOperationException(
                        $"Advance {advance.AdvanceNumber}, which this payment settled in part, has since been written off, so " +
                        "the settlement cannot be undone. Reverse the write-off in Finance's register first.");
                settledBack = advance.CurrencyCode == entity.CurrencyCode
                    ? entity.AdvanceDeducted
                    : decimal.Round(
                        entity.AdvanceDeducted / await _currency.GetRateToBaseAsync(advance.CurrencyCode, paidOn, cancellationToken),
                        2, MidpointRounding.AwayFromZero);
                settledBack = Math.Min(settledBack, advance.SettledAmount - advance.RefundedAmount);
            }
            else advance = null;
        }

        var paidRow = await _unitOfWork.Repository<HrFinancePostingRecord>()
            .GetQueryable(r => r.TenantId == entity.TenantId && !r.IsDeleted && r.SourceDocumentId == entity.Id
                            && r.EventCode == HrFinancePostingEventCatalog.TravelClaimPaid)
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);
        if (paidRow is { Status: HrFinancePostingStatus.Posted })
            await _postingRegister.ReverseAsync(paidRow.Id,
                new ReverseHrFinancePostingDto { Reason = $"Payment of claim {entity.ClaimNumber} voided: {reason}" },
                cancellationToken);

        var now = DateTime.UtcNow;
        var userId = _currentUserProvider.UserId.ToString();
        var lines = await ClaimLinesAsync(entity, cancellationToken);
        var note =
            $"Payment of claim {entity.ClaimNumber} voided. It was paid on {entity.PaidAt:d MMM yyyy}" +
            (entity.PaymentMethod is TravelPaymentMethod method ? $" by {Humanise(method.ToString()).ToLowerInvariant()}" : string.Empty) +
            (string.IsNullOrWhiteSpace(entity.PaymentReference) ? string.Empty : $" (reference {entity.PaymentReference.Trim()})") +
            $": {entity.CurrencyCode} {entity.NetPayable:N2}" +
            (advance is null ? "." : $", after {entity.CurrencyCode} {entity.AdvanceDeducted:N2} recovered from advance {advance.AdvanceNumber}, " +
                                     $"whose {advance.CurrencyCode} {settledBack:N2} is outstanding again.") +
            (paidRow is { Status: HrFinancePostingStatus.Posted } ? $" Its journal {paidRow.JournalEntryNumber} is reversed." : string.Empty) +
            $" Reason: {reason}";

        entity.Status = lines.Sum(l => l.AmountApproved ?? 0m) == lines.Sum(l => l.AmountBaseCurrency)
            ? TravelClaimStatus.Approved
            : TravelClaimStatus.PartiallyApproved;
        entity.PaidAt = null;
        entity.PaidById = null;
        entity.PaymentMethod = null;
        entity.PaymentReference = null;
        entity.AdvanceWaiverReason = null;
        entity.AdvanceDeducted = 0m;
        entity.NetPayable = ComputeNetPayable(entity.TotalApproved, 0m);
        entity.PaymentVoidedAt = now;
        entity.PaymentVoidedById = voiderEmployeeId;   // the caller, not a payload value
        entity.PaymentVoidReason = reason;
        entity.UpdatedBy = userId;
        entity.UpdatedAt = now;

        if (advance is not null && settledBack > 0m)
        {
            advance.SettledAmount -= settledBack;
            advance.UnsettledAmount = (advance.ApprovedAmount ?? 0m) - advance.SettledAmount;
            advance.Status = StaffTravelAdvanceRules.SettlementStatus(advance, DateOnly.FromDateTime(now));
            advance.UpdatedBy = userId;
            advance.UpdatedAt = now;
        }

        if (paidRow is { Status: HrFinancePostingStatus.Unposted or HrFinancePostingStatus.Failed })
        {
            var rowId = paidRow.Id;
            var row = await _unitOfWork.Repository<HrFinancePostingRecord>()
                .GetQueryable(r => r.Id == rowId)
                .FirstAsync(cancellationToken);
            row.Status = HrFinancePostingStatus.Skipped;
            var why = $"The payment was voided on {now:d MMM yyyy}: {reason}";
            row.StatusReason = why.Length > 2000 ? why[..2000] : why;
            row.LastActedByUserId = _currentUserProvider.UserId;
            row.UpdatedBy = userId;
            row.UpdatedAt = now;
        }

        await _unitOfWork.Repository<StaffTravelRequestComment>().AddAsync(new StaffTravelRequestComment
        {
            TenantId = entity.TenantId,
            StaffTravelRequestId = request.Id,
            AuthorId = voiderEmployeeId,
            CommentType = TravelRequestCommentType.InternalNote,
            Body = note.Length > 2000 ? note[..2000] : note,
            IsVisibleToTraveller = false,
            CreatedBy = userId,
        });

        // One save: the claim, the advance, the register row and the note together. All tracked — the claim and the
        // advance were read alone — so nothing beside them is marked modified.
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Expense claim payment voided: {ClaimNumber}, advance settlement undone {SettledBack}, journal {Journal}",
            entity.ClaimNumber, settledBack, paidRow?.JournalEntryNumber ?? "none");
        return true;
    }

    // ---- Expense claim lines -----------------------------------------------

    /// <summary>Adds an expense to a draft or returned claim (lane 3, B6), its receipt and per-diem rate checked (B3).</summary>
    public async Task<StaffTravelExpenseClaimLineDto> AddClaimLineAsync(CreateStaffTravelExpenseClaimLineDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        var claim = await GetOwnedClaimAsync(createDto.StaffTravelExpenseClaimId);
        RequireClaimEditable(claim);
        await GuardClaimNotPostedAsync(createDto.StaffTravelExpenseClaimId, "Adding a line to this claim", cancellationToken);
        var request = await RequireOwnedRequestAsync(claim.StaffTravelRequestId);
        await RequireLineReferencesAsync(request, createDto.ReceiptAttachmentId, createDto.PerDiemRateId, cancellationToken);

        var entity = createDto.ToEntity(tenantId, createdByUserId);
        await ApplyBaseCurrencyAmountAsync(entity, cancellationToken);
        await _lineRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await RecomputeClaimTotalsAsync(entity.StaffTravelExpenseClaimId, cancellationToken);
        return entity.ToDto();
    }

    public async Task<IEnumerable<StaffTravelExpenseClaimLineDto>> GetClaimLinesAsync(Guid claimId, CancellationToken cancellationToken = default)
    {
        await GetOwnedClaimAsync(claimId);
        var tenantId = GetTenantId();
        return (await _lineRepository.GetByClaimIdAsync(claimId))
            .Where(l => l.TenantId == tenantId)
            .Select(l => l.ToDto())
            .ToList();
    }

    /// <summary>
    /// Changes an expense on a draft or returned claim (lane 3, B6). An expense already reviewed — on a claim
    /// returned to the claimant — goes back to be reviewed again: the review was of what it said before.
    /// </summary>
    public async Task<StaffTravelExpenseClaimLineDto> UpdateClaimLineAsync(UpdateStaffTravelExpenseClaimLineDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedClaimLineAsync(updateDto.Id);
        var claim = await GetOwnedClaimAsync(entity.StaffTravelExpenseClaimId);
        RequireClaimEditable(claim);
        await GuardClaimNotPostedAsync(entity.StaffTravelExpenseClaimId, "Editing a line of this claim", cancellationToken);
        var request = await RequireOwnedRequestAsync(claim.StaffTravelRequestId);
        await RequireLineReferencesAsync(request, updateDto.ReceiptAttachmentId, updateDto.PerDiemRateId, cancellationToken);

        entity.UpdateEntity(updateDto, updatedByUserId);
        entity.Status = TravelExpenseLineStatus.Pending;
        entity.AmountApproved = null;
        entity.AmountRejected = null;
        entity.RejectionReason = null;
        entity.ReviewedById = null;
        entity.ReviewedAt = null;

        // ⚠ The SAME valuation as the create path, and it was missing here. Slice 4 stopped a caller
        // declaring the converted amount and slice 6 stopped them declaring the rate — but both fixes
        // landed on the add path only, so a line could be added at the organisation's published
        // rate and then EDITED to any rate and any base amount, with RecomputeClaimTotalsAsync
        // summing whatever the payload said. The half-fix shape: when a derived field is taken back
        // from the caller, take it back on every path that writes it.
        await ApplyBaseCurrencyAmountAsync(entity, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await RecomputeClaimTotalsAsync(entity.StaffTravelExpenseClaimId, cancellationToken);
        return entity.ToDto();
    }

    /// <summary>
    /// Decides one expense (lane 3, B4, D-2): while its claim is submitted or under review, never on one's own claim.
    /// Approved takes an amount up to the line — the whole line when none is given — and the rest is rejected; the
    /// server works out the rejected part, so approved and rejected always make the line (they could exceed it). Any
    /// rejected part needs its reason.
    /// </summary>
    public async Task<bool> ReviewClaimLineAsync(ReviewStaffTravelExpenseClaimLineDto reviewDto, Guid reviewerEmployeeId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedClaimLineAsync(reviewDto.LineId);
        var claim = await GetOwnedClaimAsync(entity.StaffTravelExpenseClaimId);
        if (claim.Status is not (TravelClaimStatus.Submitted or TravelClaimStatus.UnderReview))
            throw new InvalidOperationException(
                $"Claim {claim.ClaimNumber} is {Describe(claim.Status)}; its expenses are reviewed while it is submitted or under review.");
        RefuseOwnClaim(claim, reviewerEmployeeId, "review");
        await GuardClaimNotPostedAsync(entity.StaffTravelExpenseClaimId, "Reviewing a line of this claim", cancellationToken);

        var amount = entity.AmountBaseCurrency;
        var reason = string.IsNullOrWhiteSpace(reviewDto.RejectionReason) ? null : reviewDto.RejectionReason.Trim();
        decimal approved;
        switch (reviewDto.Status)
        {
            case TravelExpenseLineStatus.Approved:
                approved = reviewDto.AmountApproved ?? amount;
                if (approved <= 0m)
                    throw new InvalidOperationException("Approve an amount above zero, or reject the expense with the reason.");
                if (approved > amount)
                    throw new InvalidOperationException(
                        $"{claim.CurrencyCode} {approved:N2} is more than the expense ({claim.CurrencyCode} {amount:N2}).");
                if (approved < amount && reason is null)
                    throw new InvalidOperationException(
                        $"Say why {claim.CurrencyCode} {amount - approved:N2} of the expense is not approved — the claimant is told.");
                break;
            case TravelExpenseLineStatus.Rejected:
                if (reason is null)
                    throw new InvalidOperationException("Say why the expense is rejected — the claimant is told.");
                approved = 0m;
                break;
            default:
                throw new InvalidOperationException("Approve the expense, in whole or in part, or reject it.");
        }

        entity.Status = reviewDto.Status;
        entity.AmountApproved = approved;
        entity.AmountRejected = amount - approved;
        entity.RejectionReason = approved < amount ? reason : null;
        entity.ReviewedById = reviewerEmployeeId;   // the caller, not a payload value
        entity.ReviewedAt = DateTime.UtcNow;        // ...and the clock, not one either

        entity.UpdatedBy = _currentUserProvider.UserId.ToString();
        entity.UpdatedAt = DateTime.UtcNow;

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await RecomputeClaimTotalsAsync(entity.StaffTravelExpenseClaimId, cancellationToken);
        return true;
    }

    /// <summary>A travel administrator removes an expense from a draft or returned claim (lane 3, B6).</summary>
    public async Task<bool> DeleteClaimLineAsync(Guid lineId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedClaimLineAsync(lineId);
        RequireClaimEditable(await GetOwnedClaimAsync(entity.StaffTravelExpenseClaimId));

        var claimId = entity.StaffTravelExpenseClaimId;
        await GuardClaimNotPostedAsync(claimId, "Deleting a line of this claim", cancellationToken);
        await _lineRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await RecomputeClaimTotalsAsync(claimId, cancellationToken);
        return true;
    }

    // ---- Claim rules (lane 3) ------------------------------------------------

    private static string Describe(TravelClaimStatus status) => status switch
    {
        TravelClaimStatus.UnderReview => "under review",
        TravelClaimStatus.PartiallyApproved => "partly approved",
        _ => status.ToString().ToLowerInvariant(),
    };

    private static string Humanise(string pascal)
        => System.Text.RegularExpressions.Regex.Replace(pascal, "(?<!^)([A-Z])", " $1");

    /// <summary>A claim is for a trip that went ahead or is going ahead: approved, under way or completed (B3).</summary>
    private static void RequireTripTakesClaims(StaffTravelRequest request, string what)
    {
        if (request.Status is StaffTravelRequestStatus.Approved or StaffTravelRequestStatus.InProgress or StaffTravelRequestStatus.Completed)
            return;
        throw new InvalidOperationException(
            $"{what} only on a trip that is approved, under way or completed; travel request {request.RequestNumber} is " +
            $"{Humanise(request.Status.ToString()).ToLowerInvariant()}.");
    }

    /// <summary>A claim's expenses are fixed once it is submitted; a returned claim is the claimant's to change (B6).</summary>
    private static void RequireClaimEditable(StaffTravelExpenseClaim claim)
    {
        if (claim.Status is TravelClaimStatus.Draft or TravelClaimStatus.Returned) return;
        throw new InvalidOperationException(
            $"Claim {claim.ClaimNumber} is {Describe(claim.Status)}; its expenses are fixed once it is submitted. " +
            "A reviewer returns it to the claimant to change them.");
    }

    /// <summary>D-2: nobody reviews, approves or pays their own claim.</summary>
    private static void RefuseOwnClaim(StaffTravelExpenseClaim claim, Guid callerEmployeeId, string verb)
    {
        if (claim.EmployeeId == callerEmployeeId)
            throw new UnauthorizedAccessException(
                $"You cannot {verb} your own expense claim ({claim.ClaimNumber}). Another officer must.");
    }

    /// <summary>
    /// The advance a claim names is this trip's and this traveller's (B3 — a claim could settle someone else's), and
    /// still something a claim can settle.
    /// </summary>
    private async Task RequireClaimableAdvanceAsync(StaffTravelRequest request, Guid? advanceId, CancellationToken cancellationToken)
    {
        if (advanceId is not Guid id) return;
        var advance = await _advanceRepository.GetByIdAsync(id);
        if (advance == null || advance.TenantId != request.TenantId
            || advance.StaffTravelRequestId != request.Id || advance.EmployeeId != request.EmployeeId)
            throw new ArgumentException($"Advance '{id}' is not one of travel request {request.RequestNumber}'s for its traveller.");
        if (advance.Status is TravelAdvanceStatus.Rejected or TravelAdvanceStatus.Cancelled or TravelAdvanceStatus.WrittenOff)
            throw new InvalidOperationException(
                $"Advance {advance.AdvanceNumber} is {Describe(advance.Status)}; there is nothing on it for a claim to settle.");
    }

    /// <summary>A receipt is an attachment of the claim's own trip; a per-diem rate is this organisation's (B3).</summary>
    private async Task RequireLineReferencesAsync(StaffTravelRequest request, Guid? receiptId, Guid? perDiemRateId, CancellationToken cancellationToken)
    {
        if (receiptId is Guid receipt)
        {
            var onTrip = await _unitOfWork.Repository<StaffTravelRequestAttachment>()
                .GetQueryable(a => a.Id == receipt && a.TenantId == request.TenantId && a.StaffTravelRequestId == request.Id)
                .AnyAsync(cancellationToken);
            if (!onTrip)
                throw new ArgumentException($"Receipt '{receipt}' is not an attachment of travel request {request.RequestNumber}.");
        }
        if (perDiemRateId is Guid rate)
            await GetOwnedPerDiemRateAsync(rate);
    }

    private async Task<string> RequireBaseCurrencyAsync(CancellationToken cancellationToken)
        => await _currency.GetBaseCurrencyCodeAsync(cancellationToken)
           ?? throw new InvalidOperationException(
               "Finance marks no base currency, so a claim has no currency to be kept in. Set the base currency in Finance.");

    /// <summary>The claim's live lines, tracked; read apart from the claim so the claim is never saved as a graph.</summary>
    private async Task<List<StaffTravelExpenseClaimLine>> ClaimLinesAsync(StaffTravelExpenseClaim claim, CancellationToken cancellationToken)
        => (await _lineRepository.GetByClaimIdAsync(claim.Id)).Where(l => l.TenantId == claim.TenantId).ToList();

    /// <summary>The approved policy the trip was checked against at submission, if any.</summary>
    private async Task<StaffTravelPolicy?> ApprovedPolicyAsync(StaffTravelRequest request, CancellationToken cancellationToken)
    {
        if (request.PolicyId is not Guid policyId) return null;
        return await _unitOfWork.Repository<StaffTravelPolicy>()
            .GetQueryable(p => p.Id == policyId && p.TenantId == request.TenantId && p.ApprovedById != null)
            .FirstOrDefaultAsync(cancellationToken);
    }

    // ---- Advances ----------------------------------------------------------

    public async Task<StaffTravelAdvanceDto> GetAdvanceByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _advanceRepository.GetWithDetailsAsync(GetTenantId(), id)
            ?? throw new ArgumentException($"Advance with ID '{id}' not found.");
        return entity.ToDto();
    }

    public async Task<StaffTravelAdvanceDto?> GetAdvanceByNumberAsync(string advanceNumber, CancellationToken cancellationToken = default)
    {
        var entity = await _advanceRepository.GetByAdvanceNumberAsync(advanceNumber);
        if (entity == null || entity.TenantId != GetTenantId())
            return null;
        return entity.ToDto();
    }

    public async Task<IEnumerable<StaffTravelAdvanceSummaryDto>> GetAllAdvancesAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _advanceRepository.GetAllWithDetailsAsync())
            .Where(a => a.TenantId == tenantId)
            .Select(a => a.ToSummaryDto())
            .ToList();
    }

    public async Task<IEnumerable<StaffTravelAdvanceSummaryDto>> GetAdvancesByStatusAsync(TravelAdvanceStatus status, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _advanceRepository.GetByStatusAsync(status))
            .Where(a => a.TenantId == tenantId)
            .Select(a => a.ToSummaryDto())
            .ToList();
    }

    public async Task<IEnumerable<StaffTravelAdvanceSummaryDto>> GetAdvancesByRequestAsync(Guid requestId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _advanceRepository.GetByRequestIdAsync(requestId))
            .Where(a => a.TenantId == tenantId)
            .Select(a => a.ToSummaryDto())
            .ToList();
    }

    public async Task<IEnumerable<StaffTravelAdvanceSummaryDto>> GetAdvancesByEmployeeAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _advanceRepository.GetByEmployeeIdAsync(employeeId))
            .Where(a => a.TenantId == tenantId)
            .Select(a => a.ToSummaryDto())
            .ToList();
    }

    public async Task<IEnumerable<StaffTravelAdvanceSummaryDto>> GetOutstandingAdvancesByEmployeeAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _advanceRepository.GetOutstandingByEmployeeAsync(employeeId))
            .Where(a => a.TenantId == tenantId)
            .Select(a => a.ToSummaryDto())
            .ToList();
    }

    public async Task<IEnumerable<StaffTravelAdvanceSummaryDto>> GetOverdueSettlementsAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _advanceRepository.GetOverdueSettlementsAsync())
            .Where(a => a.TenantId == tenantId)
            .Select(a => a.ToSummaryDto())
            .ToList();
    }

    /// <summary>
    /// Raises an advance for the trip's traveller (lane 3). Only on an approved trip or one under way — money after the
    /// trip is a claim (D-16); never for someone who has not settled an overdue advance; nothing is owed until the
    /// advance is disbursed.
    /// </summary>
    public async Task<StaffTravelAdvanceDto> CreateAdvanceAsync(CreateStaffTravelAdvanceDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        await _currency.RequireKnownCurrencyAsync(createDto.CurrencyCode, cancellationToken);
        var request = await RequireOwnedRequestAsync(createDto.StaffTravelRequestId);
        StaffTravelRequestGuards.RequireOpen(request, "an advance");
        RequireTripTakesAdvances(request, "An advance can be requested");
        if (createDto.RequestedAmount <= 0m)
            throw new InvalidOperationException("An advance must be for more than nothing.");
        RequireDeadlineAfterTrip(createDto.SettlementDeadline, request);
        await RefuseWhileOverdueAsync(tenantId, request.EmployeeId, cancellationToken);

        var entity = createDto.ToEntity(tenantId, createdByUserId, request.EmployeeId);
        entity.AdvanceNumber = await GenerateAdvanceNumberAsync(tenantId, cancellationToken);
        entity.Status = TravelAdvanceStatus.Requested;

        await _advanceRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Travel advance created: {AdvanceNumber}", entity.AdvanceNumber);
        var reloaded = await _advanceRepository.GetWithDetailsAsync(entity.TenantId, entity.Id);
        return (reloaded ?? entity).ToDto();
    }

    /// <summary>
    /// A requested advance only (N3): it refused nothing but Disbursed, so a partly settled, settled, overdue or
    /// written-off advance could be re-currencied and re-dated.
    /// </summary>
    public async Task<StaffTravelAdvanceDto> UpdateAdvanceAsync(UpdateStaffTravelAdvanceDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAdvanceAsync(updateDto.Id);

        if (entity.Status != TravelAdvanceStatus.Requested)
            throw new InvalidOperationException(
                $"Advance {entity.AdvanceNumber} is {Describe(entity.Status)}; only a requested advance can be changed.");
        await GuardAdvanceNotPostedAsync(entity.Id, "Editing this advance", cancellationToken);
        await _currency.RequireKnownCurrencyAsync(updateDto.CurrencyCode, cancellationToken);
        if (updateDto.RequestedAmount <= 0m)
            throw new InvalidOperationException("An advance must be for more than nothing.");
        RequireDeadlineAfterTrip(updateDto.SettlementDeadline, await RequireOwnedRequestAsync(entity.StaffTravelRequestId));

        entity.UpdateEntity(updateDto, updatedByUserId);
        entity.UnsettledAmount = 0m;   // nothing is owed before disbursement (B8)
        await _advanceRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        var reloaded = await _advanceRepository.GetWithDetailsAsync(entity.TenantId, entity.Id);
        return (reloaded ?? entity).ToDto();
    }

    public async Task<bool> DeleteAdvanceAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAdvanceAsync(id);

        if (entity.Status != TravelAdvanceStatus.Requested)
            throw new InvalidOperationException("Only requested advances can be deleted.");

        await _advanceRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    /// <summary>
    /// Approves a requested advance (lane 3): never the traveller's own (D-2); more than nothing and no more than was
    /// asked (B8, O-8 — a typed 0 approved GHS 0, and the dialog's "or less" bound nothing); and with every other
    /// approved advance on the trip, within the trip's approved budget.
    /// </summary>
    public async Task<bool> ApproveAdvanceAsync(ApproveStaffTravelAdvanceDto approveDto, Guid approverEmployeeId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAdvanceAsync(approveDto.AdvanceId);

        if (entity.Status != TravelAdvanceStatus.Requested)
            throw new InvalidOperationException(
                $"Advance {entity.AdvanceNumber} is {Describe(entity.Status)}; only a requested advance can be approved.");
        RefuseOwnAdvance(entity, approverEmployeeId, "approve");
        var request = await RequireOwnedRequestAsync(entity.StaffTravelRequestId);
        RequireTripTakesAdvances(request, "An advance can be approved");
        if (approveDto.ApprovedAmount <= 0m)
            throw new InvalidOperationException("Approve an amount above zero, or reject the advance.");
        if (approveDto.ApprovedAmount > entity.RequestedAmount)
            throw new InvalidOperationException(
                $"{entity.CurrencyCode} {approveDto.ApprovedAmount:N2} is more than the {entity.CurrencyCode} {entity.RequestedAmount:N2} " +
                "requested. Approve the amount requested or less.");
        await RequireWithinTripBudgetAsync(request, entity, approveDto.ApprovedAmount, cancellationToken);

        entity.Status = TravelAdvanceStatus.Approved;
        entity.ApprovedById = approverEmployeeId;   // the caller, not a payload value
        entity.ApprovedAmount = approveDto.ApprovedAmount;
        entity.UnsettledAmount = 0m;                // nothing is owed until it is disbursed (B8)
        entity.UpdatedBy = _currentUserProvider.UserId.ToString();
        entity.UpdatedAt = DateTime.UtcNow;

        await _advanceRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Travel advance approved: {AdvanceNumber}", entity.AdvanceNumber);
        return true;
    }

    /// <summary>The desk refuses a requested advance, with a reason (lane 3, B8). Never the traveller's own.</summary>
    public async Task<bool> RejectAdvanceAsync(Guid advanceId, string reason, Guid rejecterEmployeeId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAdvanceAsync(advanceId);

        if (entity.Status != TravelAdvanceStatus.Requested)
            throw new InvalidOperationException(entity.Status == TravelAdvanceStatus.Approved
                ? $"Advance {entity.AdvanceNumber} is already approved. Cancel it instead, with the reason."
                : $"Advance {entity.AdvanceNumber} is {Describe(entity.Status)}; only a requested advance can be rejected.");
        RefuseOwnAdvance(entity, rejecterEmployeeId, "reject");
        var why = RequireReason(reason, "rejecting an advance");

        var now = DateTime.UtcNow;
        entity.Status = TravelAdvanceStatus.Rejected;
        entity.RejectedAt = now;
        entity.RejectedById = rejecterEmployeeId;
        entity.RejectionReason = why;
        entity.UnsettledAmount = 0m;
        entity.UpdatedBy = _currentUserProvider.UserId.ToString();
        entity.UpdatedAt = now;

        await _advanceRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Travel advance rejected: {AdvanceNumber}", entity.AdvanceNumber);
        return true;
    }

    /// <summary>
    /// Withdraws an advance before any money goes out, with a reason (lane 3, B8). A trip's own cancel does the same
    /// for each of its undisbursed advances (<see cref="StaffTravelAdvanceRules.ApplyCancellation"/>). Once money has
    /// gone out, the way back is a claim, cash handed back or a write-off.
    /// </summary>
    public async Task<bool> CancelAdvanceAsync(Guid advanceId, string reason, Guid cancellerEmployeeId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAdvanceAsync(advanceId);

        if (!StaffTravelAdvanceRules.IsUndisbursed(entity.Status))
            throw new InvalidOperationException(StaffTravelAdvanceRules.IsCashOutStatus(entity.Status)
                ? $"Advance {entity.AdvanceNumber} has been paid out. Settle it through an expense claim, record the cash " +
                  "handed back, or write it off."
                : $"Advance {entity.AdvanceNumber} is {Describe(entity.Status)}; there is nothing to cancel.");
        var why = RequireReason(reason, "cancelling an advance");

        StaffTravelAdvanceRules.ApplyCancellation(entity, why, cancellerEmployeeId, _currentUserProvider.UserId, DateTime.UtcNow);
        await _advanceRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Travel advance cancelled: {AdvanceNumber}", entity.AdvanceNumber);
        return true;
    }

    /// <summary>
    /// Pays the advance out (lane 3): never to oneself, and never by the officer who approved it (D-2 — the person who
    /// decides an amount does not also release it); only while the trip is approved or under way. What the traveller
    /// owes starts here, and a settlement deadline is given when none was set (O-8 — without one the overdue sweep
    /// never sees the advance).
    /// </summary>
    public async Task<bool> DisburseAdvanceAsync(DisburseStaffTravelAdvanceDto disburseDto, Guid disburserEmployeeId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAdvanceAsync(disburseDto.AdvanceId);

        if (entity.Status != TravelAdvanceStatus.Approved)
            throw new InvalidOperationException(
                $"Advance {entity.AdvanceNumber} is {Describe(entity.Status)}; only an approved advance can be disbursed.");
        RefuseOwnAdvance(entity, disburserEmployeeId, "disburse");
        if (entity.ApprovedById == disburserEmployeeId)
            throw new UnauthorizedAccessException(
                $"You approved advance {entity.AdvanceNumber}, so another officer must disburse it — the person who approves " +
                "money does not also pay it out.");
        var request = await RequireOwnedRequestAsync(entity.StaffTravelRequestId);
        RequireTripTakesAdvances(request, "An advance can be disbursed");
        var deadline = entity.SettlementDeadline
            ?? StaffTravelAdvanceRules.DefaultDeadline(request.TravelEndDate, await ClaimWindowDaysAsync(request, cancellationToken));

        // Disbursement and its Finance receivable commit together (lane 8).
        await _financePosting.RunAsync(async ct =>
        {
            var now = DateTime.UtcNow;
            entity.Status = TravelAdvanceStatus.Disbursed;
            entity.DisbursedById = disburserEmployeeId;   // the caller, not a payload value
            // ...and the clock, not a payload value either: the settlement deadline and the overdue sweep run off dates.
            entity.DisbursedAt = now;
            entity.SettlementDeadline = deadline;
            entity.UnsettledAmount = (entity.ApprovedAmount ?? 0m) - entity.SettledAmount;
            entity.UpdatedBy = _currentUserProvider.UserId.ToString();
            entity.UpdatedAt = now;

            await _advanceRepository.UpdateAsync(entity);
            await _unitOfWork.SaveChangesAsync(ct);

            return HrFinancePostingCommandFactory.TravelAdvanceDisbursed(entity);
        }, _currentUserProvider.UserId, cancellationToken);

        _logger.LogInformation("Travel advance disbursed: {AdvanceNumber}", entity.AdvanceNumber);
        return true;
    }

    /// <summary>
    /// Records unused cash the traveller handed back (lane 3, O-8): it settles the advance as a claim's recovery does
    /// and posts through the same adapter (<c>TRAVEL_ADVANCE_REFUNDED</c>). One refund per advance — the posting
    /// register keeps one row per event and advance, so a second would be answered as a duplicate and never reach
    /// Finance. Never recorded by the traveller.
    /// </summary>
    public async Task<bool> RecordAdvanceRefundAsync(Guid advanceId, RefundStaffTravelAdvanceDto refundDto, Guid recorderEmployeeId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAdvanceAsync(advanceId);

        if (!StaffTravelAdvanceRules.IsCashOutStatus(entity.Status) || entity.UnsettledAmount <= 0m)
            throw new InvalidOperationException(
                $"Advance {entity.AdvanceNumber} is {Describe(entity.Status)} with nothing outstanding; there is no cash to hand back.");
        if (entity.RefundedAt is not null)
            throw new InvalidOperationException(
                $"Advance {entity.AdvanceNumber} already records a refund of {entity.CurrencyCode} {entity.RefundedAmount:N2} " +
                $"(ref {entity.RefundReference}). Settle what is left through a claim or write it off.");
        RefuseOwnAdvance(entity, recorderEmployeeId, "record a refund on");
        if (refundDto.Amount <= 0m)
            throw new InvalidOperationException("Enter the amount handed back.");
        if (refundDto.Amount > entity.UnsettledAmount)
            throw new InvalidOperationException(
                $"{entity.CurrencyCode} {refundDto.Amount:N2} is more than the {entity.CurrencyCode} {entity.UnsettledAmount:N2} " +
                $"still outstanding on advance {entity.AdvanceNumber}.");
        var reference = RequireReason(refundDto.Reference, "a refund's receipt reference");

        await _financePosting.RunAsync(async ct =>
        {
            var now = DateTime.UtcNow;
            entity.RefundedAmount = refundDto.Amount;
            entity.RefundedAt = now;
            entity.RefundedById = recorderEmployeeId;
            entity.RefundReference = reference;
            entity.SettledAmount += refundDto.Amount;
            entity.UnsettledAmount = (entity.ApprovedAmount ?? 0m) - entity.SettledAmount;
            entity.Status = StaffTravelAdvanceRules.SettlementStatus(entity, DateOnly.FromDateTime(now));
            entity.UpdatedBy = _currentUserProvider.UserId.ToString();
            entity.UpdatedAt = now;

            await _advanceRepository.UpdateAsync(entity);
            await _unitOfWork.SaveChangesAsync(ct);

            return HrFinancePostingCommandFactory.TravelAdvanceRefunded(entity);
        }, _currentUserProvider.UserId, cancellationToken);

        _logger.LogInformation("Travel advance refund recorded: {AdvanceNumber}", entity.AdvanceNumber);
        return true;
    }

    /// <summary>
    /// Writes off what is left on an advance with cash out — a travel administrator's verb, with a reason, never on
    /// their own advance (lane 3, B8). The balance written off posts (<c>TRAVEL_ADVANCE_WRITTEN_OFF</c>), so Finance's
    /// receivable falls with travel's; without it the ledger would keep an advance travel says is gone.
    /// </summary>
    public async Task<bool> WriteOffAdvanceAsync(Guid advanceId, string reason, Guid writerEmployeeId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAdvanceAsync(advanceId);

        if (!StaffTravelAdvanceRules.IsCashOutStatus(entity.Status) || entity.UnsettledAmount <= 0m)
            throw new InvalidOperationException(
                $"Advance {entity.AdvanceNumber} is {Describe(entity.Status)} with nothing outstanding; there is nothing to write off.");
        RefuseOwnAdvance(entity, writerEmployeeId, "write off");
        var why = RequireReason(reason, "writing off an advance");

        await _financePosting.RunAsync(async ct =>
        {
            var now = DateTime.UtcNow;
            entity.Status = TravelAdvanceStatus.WrittenOff;
            entity.WrittenOffAt = now;
            entity.WrittenOffById = writerEmployeeId;
            entity.WriteOffReason = why;
            // What was written off stays readable as the approved amount less what was settled; nothing is owed now.
            entity.UnsettledAmount = 0m;
            entity.UpdatedBy = _currentUserProvider.UserId.ToString();
            entity.UpdatedAt = now;

            await _advanceRepository.UpdateAsync(entity);
            await _unitOfWork.SaveChangesAsync(ct);

            return HrFinancePostingCommandFactory.TravelAdvanceWrittenOff(entity);
        }, _currentUserProvider.UserId, cancellationToken);

        _logger.LogInformation("Travel advance written off: {AdvanceNumber}", entity.AdvanceNumber);
        return true;
    }

    // ---- Advance rules (lane 3) ----------------------------------------------

    private static string Describe(TravelAdvanceStatus status) => status switch
    {
        TravelAdvanceStatus.PartiallySettled => "partly settled",
        TravelAdvanceStatus.FullySettled => "fully settled",
        TravelAdvanceStatus.WrittenOff => "written off",
        _ => status.ToString().ToLowerInvariant(),
    };

    private static string RequireReason(string? text, string what)
    {
        var trimmed = text?.Trim();
        if (string.IsNullOrEmpty(trimmed))
            throw new InvalidOperationException($"Give the reason for {what}.");
        return trimmed;
    }

    /// <summary>D-2: nobody decides, pays out, settles or writes off their own advance.</summary>
    private static void RefuseOwnAdvance(StaffTravelAdvance advance, Guid callerEmployeeId, string verb)
    {
        if (advance.EmployeeId == callerEmployeeId)
            throw new UnauthorizedAccessException(
                $"You cannot {verb} your own travel advance ({advance.AdvanceNumber}). Another officer must do it.");
    }

    /// <summary>D-16: an advance is cash for a trip that is going ahead — approved, or under way.</summary>
    private static void RequireTripTakesAdvances(StaffTravelRequest request, string what)
    {
        if (request.Status is StaffTravelRequestStatus.Approved or StaffTravelRequestStatus.InProgress)
            return;

        var finished = request.Status is StaffTravelRequestStatus.Completed or StaffTravelRequestStatus.Closed;
        var status = System.Text.RegularExpressions.Regex.Replace(request.Status.ToString(), "(?<!^)([A-Z])", " $1").ToLowerInvariant();
        throw new InvalidOperationException(
            $"{what} only while its trip is approved or under way; travel request {request.RequestNumber} is {status}." +
            (finished ? " Money spent on a finished trip is claimed, not advanced." : string.Empty));
    }

    private static void RequireDeadlineAfterTrip(DateOnly? deadline, StaffTravelRequest request)
    {
        if (deadline is DateOnly d && d < request.TravelEndDate)
            throw new InvalidOperationException(
                $"The settlement deadline ({d:d MMM yyyy}) falls before the trip ends ({request.TravelEndDate:d MMM yyyy}).");
    }

    /// <summary>O-8: no new advance for a traveller who still holds one past its deadline.</summary>
    private async Task RefuseWhileOverdueAsync(Guid tenantId, Guid employeeId, CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var overdue = await _unitOfWork.Repository<StaffTravelAdvance>()
            .GetQueryable(a => a.TenantId == tenantId && !a.IsDeleted && a.EmployeeId == employeeId
                            && a.SettlementDeadline != null && a.SettlementDeadline < today)
            .Where(StaffTravelAdvanceRules.CashOut)
            .OrderBy(a => a.SettlementDeadline)
            .Select(a => new { a.AdvanceNumber, a.CurrencyCode, a.UnsettledAmount, a.SettlementDeadline })
            .FirstOrDefaultAsync(cancellationToken);

        if (overdue is not null)
            throw new InvalidOperationException(
                $"The traveller still holds advance {overdue.AdvanceNumber} ({overdue.CurrencyCode} {overdue.UnsettledAmount:N2}), " +
                $"overdue since {overdue.SettlementDeadline:d MMM yyyy}. It must be settled — by a claim, cash handed back or a " +
                "write-off — before another advance is raised.");
    }

    /// <summary>
    /// O-8: the advances approved on a trip stay within its approved budget (the estimate, on a trip approved before
    /// the budget was recorded). Each is converted into the trip's currency at Finance's rate.
    /// </summary>
    private async Task RequireWithinTripBudgetAsync(
        StaffTravelRequest request, StaffTravelAdvance approving, decimal amount, CancellationToken cancellationToken)
    {
        var budget = request.ApprovedBudget ?? request.EstimatedTotalCost;
        var budgetName = request.ApprovedBudget.HasValue ? "approved budget" : "estimate";
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var others = await _unitOfWork.Repository<StaffTravelAdvance>()
            .GetQueryable(a => a.TenantId == request.TenantId && !a.IsDeleted
                            && a.StaffTravelRequestId == request.Id && a.Id != approving.Id)
            .Select(a => new { a.Status, a.ApprovedAmount, a.CurrencyCode })
            .ToListAsync(cancellationToken);

        var total = await _currency.ConvertBetweenAsync(amount, approving.CurrencyCode, request.CurrencyCode, today, cancellationToken);
        foreach (var other in others.Where(o => StaffTravelAdvanceRules.CountsAgainstBudget(o.Status) && o.ApprovedAmount > 0m))
            total += await _currency.ConvertBetweenAsync(other.ApprovedAmount!.Value, other.CurrencyCode, request.CurrencyCode, today, cancellationToken);
        total = decimal.Round(total, 2, MidpointRounding.AwayFromZero);

        if (total > budget)
            throw new InvalidOperationException(
                $"Approving this would bring the advances approved on {request.RequestNumber} to {request.CurrencyCode} {total:N2}, " +
                $"above its {budgetName} of {request.CurrencyCode} {budget:N2}.");
    }

    /// <summary>The claim window of the approved policy the trip was checked against at submission, if any.</summary>
    private async Task<int?> ClaimWindowDaysAsync(StaffTravelRequest request, CancellationToken cancellationToken)
        => (await ApprovedPolicyAsync(request, cancellationToken))?.ExpenseSubmissionDays;

    // ---- Per-diem rates ----------------------------------------------------

    public async Task<StaffTravelPerDiemRateDto> GetPerDiemRateByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _perDiemRepository.GetWithDetailsAsync(GetTenantId(), id)
            ?? throw new ArgumentException($"PerDiemRate with ID '{id}' not found.");
        return entity.ToDto();
    }

    public async Task<IEnumerable<StaffTravelPerDiemRateDto>> GetActivePerDiemRatesAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _perDiemRepository.GetActiveRatesAsync())
            .Where(r => r.TenantId == tenantId)
            .Select(r => r.ToDto())
            .ToList();
    }

    public async Task<IEnumerable<StaffTravelPerDiemRateDto>> GetPerDiemRatesByCountryAsync(Guid countryId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _perDiemRepository.GetByCountryAsync(countryId))
            .Where(r => r.TenantId == tenantId)
            .Select(r => r.ToDto())
            .ToList();
    }

    public async Task<StaffTravelPerDiemRateDto?> GetEffectivePerDiemRateAsync(Guid countryId, string? city, Guid? staffLevelId, DateOnly onDate, CancellationToken cancellationToken = default)
    {
        var entity = await _perDiemRepository.GetEffectiveRateAsync(countryId, city, staffLevelId, onDate);
        if (entity == null || entity.TenantId != GetTenantId())
            return null;
        return entity.ToDto();
    }

    public async Task<StaffTravelPerDiemRateDto> CreatePerDiemRateAsync(CreateStaffTravelPerDiemRateDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        await _currency.RequireKnownCurrencyAsync(createDto.CurrencyCode, cancellationToken);
        var entity = createDto.ToEntity(tenantId, createdByUserId);
        await _perDiemRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        var reloaded = await _perDiemRepository.GetWithDetailsAsync(entity.TenantId, entity.Id);
        return (reloaded ?? entity).ToDto();
    }

    public async Task<StaffTravelPerDiemRateDto> UpdatePerDiemRateAsync(UpdateStaffTravelPerDiemRateDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedPerDiemRateAsync(updateDto.Id);

        entity.UpdateEntity(updateDto, updatedByUserId);
        await _perDiemRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        var reloaded = await _perDiemRepository.GetWithDetailsAsync(entity.TenantId, entity.Id);
        return (reloaded ?? entity).ToDto();
    }

    public async Task<bool> DeletePerDiemRateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedPerDiemRateAsync(id);

        await _perDiemRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ---- Helpers -----------------------------------------------------------

    /// <summary>
    /// Derives a line's base-currency amount from its own original amount and rate.
    /// </summary>
    /// <remarks>
    /// <c>AmountBaseCurrency</c> arrived straight from the payload, so a caller could claim
    /// 100 USD at a rate of 15 and declare the base amount to be anything at all — and
    /// <c>TotalClaimed</c>, which sums this field, believed it. The arithmetic is the server's.
    ///
    /// The rate is Finance's too, read for the expense date, so a claim is valued at the
    /// organisation's own published rate and cannot disagree with what Finance reports the trip
    /// cost. Applied on BOTH the add and the update path.
    /// </remarks>
    private async Task ApplyBaseCurrencyAmountAsync(
        StaffTravelExpenseClaimLine line, CancellationToken cancellationToken)
    {
        // Slice 4 stopped the caller declaring the converted AMOUNT. This stops them declaring the
        // RATE as well: it is read from Finance's ExchangeRate for the expense date, so a claim is
        // valued at the organisation's own published rate and cannot disagree with what Finance
        // reports the trip cost.
        line.ExchangeRate = await _currency.GetRateToBaseAsync(
            line.CurrencyOriginal, line.ExpenseDate, cancellationToken);

        line.AmountBaseCurrency = decimal.Round(
            line.AmountOriginal * line.ExchangeRate, 2, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// Applies the claim against the advance it settles, if it names one.
    /// </summary>
    /// <remarks>
    /// <para><b>This was missing entirely, and it meant paying twice.</b> Nothing anywhere wrote
    /// <c>StaffTravelExpenseClaim.AdvanceDeducted</c> or <c>StaffTravelAdvance.SettledAmount</c> —
    /// both were read-only fields with no writer. So an employee who drew a GHS 3,000 advance and
    /// then claimed GHS 4,000 of expenses was paid the full 4,000, because <c>AdvanceDeducted</c>
    /// was 0 and <c>NetPayable</c> equalled the whole claim. Meanwhile the advance's
    /// <c>UnsettledAmount</c> never moved off its full value, so it stayed on
    /// <c>advances/overdue-settlements</c> permanently — the register said the money was still
    /// outstanding while the traveller had in effect been given it twice.</para>
    ///
    /// <para>Deduction is capped at the outstanding balance: a claim smaller than the advance
    /// settles part of it and leaves the rest outstanding, and a claim larger than the advance
    /// settles all of it and pays the difference. It is never negative and never over-recovers.</para>
    ///
    /// <para>This is the travel-side arithmetic only. The GL side is the <c>TravelClaimPaid</c>
    /// posting (<c>HrFinancePostingCommandFactory</c>, since 2026-09-20): it clears the payable,
    /// credits the advance receivable with what this method recovered, and posts the net payment —
    /// when a posting rule for the event is enabled; without one the claim is recorded Unposted.
    /// (This remark said the GL entries were out of scope until the travel final closure, lane 0.)</para>
    ///
    /// <para><b>An overdue advance is recovered too</b> (lane 3, N1): only Disbursed and PartiallySettled were, so
    /// the day the sweep marked an advance overdue, a claim against it would have paid out in full.</para>
    ///
    /// <para><b>An advance in another currency</b> (lane 3, D-15) is recovered in its own currency at Finance's rate
    /// on the day the claim is paid: the claim's figures are in the base currency, so the advance's outstanding
    /// balance is valued at that rate, the smaller of the two is deducted, and the advance is settled by the same
    /// amount in its own currency. Whatever a rate movement leaves on the advance is refunded or written off. It was
    /// deducted unconverted — USD 1,000 outstanding took GHS 1,000 off a claim (B11).</para>
    /// </remarks>
    private async Task SettleLinkedAdvanceAsync(
        StaffTravelExpenseClaim claim, CancellationToken cancellationToken)
    {
        if (claim.TravelAdvanceId is not Guid advanceId) return;

        var advance = await _advanceRepository.GetByIdAsync(advanceId);
        if (advance == null || advance.TenantId != claim.TenantId) return;

        // Only money that actually left the company — and has not come back — can be recovered from a claim.
        if (!StaffTravelAdvanceRules.IsCashOutStatus(advance.Status)) return;

        var outstanding = advance.UnsettledAmount;
        if (outstanding <= 0m) return;

        // Recover against what the claim is worth before any deduction, not after — its APPROVED total. It fell back
        // to the claimed total when nothing was approved, which recovered and paid on a claim Finance recognised as
        // zero (B4); since lane 3 a claim reaches payment only with something approved.
        var recoverable = claim.TotalApproved;
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var rate = await _currency.GetRateToBaseAsync(advance.CurrencyCode, today, cancellationToken);
        var outstandingInBase = decimal.Round(outstanding * rate, 2, MidpointRounding.AwayFromZero);

        var deduction = Math.Min(outstandingInBase, recoverable);
        if (deduction <= 0m) return;
        // The advance is settled in its own currency; all of it when the claim covers all of it, so no rounding
        // remainder is left behind on an advance the claim fully recovered.
        var settledInAdvanceCurrency = deduction == outstandingInBase
            ? outstanding
            : decimal.Round(deduction / rate, 2, MidpointRounding.AwayFromZero);

        claim.AdvanceDeducted = deduction;
        claim.NetPayable = ComputeNetPayable(recoverable, deduction);

        advance.SettledAmount += settledInAdvanceCurrency;
        advance.UnsettledAmount = (advance.ApprovedAmount ?? 0m) - advance.SettledAmount;
        advance.Status = StaffTravelAdvanceRules.SettlementStatus(advance, today);
        advance.UpdatedAt = DateTime.UtcNow;
        // Tracked, so the caller's save writes it. `UpdateAsync` would mark the advance's whole loaded graph —
        // the claim it settles, that claim's lines — modified as well.
    }

    /// <summary>
    /// Confirms the travel request exists in the caller's tenant before money is hung off it.
    /// Budgets, claims and advances all took StaffTravelRequestId straight from the payload.
    /// </summary>
    private async Task<StaffTravelRequest> RequireOwnedRequestAsync(Guid requestId)
    {
        var request = await _requestRepository.GetByIdAsync(requestId);
        if (request == null || request.TenantId != GetTenantId())
            throw new ArgumentException($"Staff travel request with ID '{requestId}' not found.");
        return request;
    }

    /// <summary>What the employee is actually owed: the payable total less any advance held.</summary>
    private static decimal ComputeNetPayable(decimal payableTotal, decimal advanceDeducted)
        => payableTotal - advanceDeducted;

    private async Task RecomputeClaimTotalsAsync(Guid claimId, CancellationToken cancellationToken)
    {
        var tenantId = GetTenantId();
        // The claim alone, then its lines: saved by tracking, not as a graph (lane 3 — `UpdateAsync` on the claim
        // read with every navigation marked its traveller, trip, reviewer and advance modified too).
        var claim = await _claimRepository.GetByIdAsync(claimId);
        if (claim == null || claim.TenantId != tenantId) return;
        var lines = await ClaimLinesAsync(claim, cancellationToken);

        claim.TotalClaimed = lines.Sum(l => l.AmountBaseCurrency);
        claim.TotalApproved = lines.Sum(l => l.AmountApproved ?? 0m);
        claim.TotalRejected = lines.Sum(l => l.AmountRejected ?? 0m);

        // Once anything has been reviewed the payable figure is the APPROVED total less the
        // advance. Before that there is nothing approved, so fall back to the claimed total rather
        // than telling the traveller they are owed minus-the-advance.
        var reviewed = lines.Any(l => l.AmountApproved.HasValue || l.AmountRejected.HasValue);
        claim.NetPayable = ComputeNetPayable(
            reviewed ? claim.TotalApproved : claim.TotalClaimed, claim.AdvanceDeducted);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// The next claim number: the highest this tenant ever issued in the year, deleted claims included, plus one.
    /// </summary>
    /// <remarks>
    /// B9 (lane 3): it counted LIVE claims across every tenant, so deleting a draft claim made the next create reissue
    /// a number — a 500 against the unfiltered index until batch 1 filtered it, and since then a number used twice.
    /// The shape of <c>StaffTravelRequestService.GenerateRequestNumberAsync</c>; not atomic under concurrent creates,
    /// as that one records.
    /// </remarks>
    private async Task<string> GenerateClaimNumberAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var prefix = $"EXP-{DateTime.UtcNow.Year}-";
        var issued = await _claimRepository
            .GetQueryableIncludingDeleted(c => c.TenantId == tenantId && c.ClaimNumber.StartsWith(prefix))
            .Select(c => c.ClaimNumber)
            .ToListAsync(cancellationToken);
        return $"{prefix}{(HighestIssued(issued, prefix) + 1):D5}";
    }

    /// <summary>The next advance number — as <see cref="GenerateClaimNumberAsync"/>.</summary>
    private async Task<string> GenerateAdvanceNumberAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var prefix = $"ADV-{DateTime.UtcNow.Year}-";
        var issued = await _advanceRepository
            .GetQueryableIncludingDeleted(a => a.TenantId == tenantId && a.AdvanceNumber.StartsWith(prefix))
            .Select(a => a.AdvanceNumber)
            .ToListAsync(cancellationToken);
        return $"{prefix}{(HighestIssued(issued, prefix) + 1):D5}";
    }

    /// <summary>A number that does not parse (a renamed test row, say) counts as 0, never as the highest.</summary>
    private static int HighestIssued(IEnumerable<string> numbers, string prefix)
        => numbers
            .Select(number => int.TryParse(number[prefix.Length..], out var value) ? value : 0)
            .DefaultIfEmpty(0)
            .Max();
}

#endregion
