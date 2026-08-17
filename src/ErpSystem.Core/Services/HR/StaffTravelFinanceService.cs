using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Entities.HR.StaffTravel;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Application.HR.Extensions;
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
    private readonly IStaffTravelPerDiemRateRepository _perDiemRepository;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<StaffTravelFinanceService> _logger;

    public StaffTravelFinanceService(
        IStaffTravelBudgetRepository budgetRepository,
        IStaffTravelExpenseClaimRepository claimRepository,
        IStaffTravelExpenseClaimLineRepository lineRepository,
        IStaffTravelAdvanceRepository advanceRepository,
        IStaffTravelRequestRepository requestRepository,
        IStaffTravelPerDiemRateRepository perDiemRepository,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ILogger<StaffTravelFinanceService> logger)
    {
        _budgetRepository = budgetRepository;
        _claimRepository = claimRepository;
        _lineRepository = lineRepository;
        _advanceRepository = advanceRepository;
        _requestRepository = requestRepository;
        _perDiemRepository = perDiemRepository;
        _currentUserProvider = currentUserProvider;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

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

    public async Task<StaffTravelBudgetDto?> GetBudgetByRequestAsync(Guid requestId, CancellationToken cancellationToken = default)
    {
        var entity = await _budgetRepository.GetByRequestIdAsync(requestId);
        if (entity == null || entity.TenantId != GetTenantId())
            return null;
        return entity.ToDto();
    }

    public async Task<StaffTravelBudgetDto> CreateBudgetAsync(CreateStaffTravelBudgetDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        await RequireOwnedRequestAsync(createDto.StaffTravelRequestId);

        var existing = await _budgetRepository.GetByRequestIdAsync(createDto.StaffTravelRequestId);
        if (existing != null && existing.TenantId == tenantId)
            throw new InvalidOperationException("A budget already exists for this request.");

        var entity = createDto.ToEntity(tenantId, createdByUserId);
        await _budgetRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<StaffTravelBudgetDto> UpdateBudgetAsync(UpdateStaffTravelBudgetDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedBudgetAsync(updateDto.Id);

        entity.UpdateEntity(updateDto, updatedByUserId);
        await _budgetRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
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

    public async Task<StaffTravelExpenseClaimDto> CreateClaimAsync(CreateStaffTravelExpenseClaimDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        await RequireOwnedRequestAsync(createDto.StaffTravelRequestId);

        var entity = createDto.ToEntity(tenantId, createdByUserId);
        entity.ClaimNumber = await GenerateClaimNumberAsync(cancellationToken);
        entity.Status = TravelClaimStatus.Draft;
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

    public async Task<StaffTravelExpenseClaimDto> UpdateClaimAsync(UpdateStaffTravelExpenseClaimDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedClaimAsync(updateDto.Id);

        if (entity.Status is TravelClaimStatus.Paid or TravelClaimStatus.Approved)
            throw new InvalidOperationException($"A claim in status '{entity.Status}' cannot be edited.");

        entity.UpdateEntity(updateDto, updatedByUserId);
        await _claimRepository.UpdateAsync(entity);
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

    public async Task<bool> SubmitClaimAsync(Guid claimId, Guid submittedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedClaimAsync(claimId);

        if (entity.Status is not (TravelClaimStatus.Draft or TravelClaimStatus.Returned))
            throw new InvalidOperationException("Only draft or returned claims can be submitted.");

        entity.Status = TravelClaimStatus.Submitted;
        entity.SubmittedAt = DateTime.UtcNow;
        entity.UpdatedBy = submittedByUserId.ToString();
        entity.UpdatedAt = DateTime.UtcNow;

        await _claimRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> ReviewClaimAsync(ReviewStaffTravelExpenseClaimDto reviewDto, Guid reviewerEmployeeId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedClaimAsync(reviewDto.ClaimId);

        entity.Status = reviewDto.NewStatus;
        entity.FinanceReviewedById = reviewerEmployeeId;   // the caller, not a payload value
        entity.FinanceReviewedAt = reviewDto.ReviewedAt;
        entity.UpdatedBy = reviewerEmployeeId.ToString();
        entity.UpdatedAt = DateTime.UtcNow;

        await _claimRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Expense claim reviewed: {ClaimNumber}, NewStatus: {Status}", entity.ClaimNumber, entity.Status);
        return true;
    }

    public async Task<bool> PayClaimAsync(PayStaffTravelExpenseClaimDto payDto, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedClaimAsync(payDto.ClaimId);

        if (entity.Status is not (TravelClaimStatus.Approved or TravelClaimStatus.PartiallyApproved))
            throw new InvalidOperationException("Only approved claims can be paid.");

        entity.Status = TravelClaimStatus.Paid;
        entity.PaymentMethod = payDto.PaymentMethod;
        entity.PaymentReference = payDto.PaymentReference;
        entity.PaidAt = payDto.PaidAt;
        entity.UpdatedAt = DateTime.UtcNow;

        await SettleLinkedAdvanceAsync(entity, cancellationToken);

        await _claimRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Expense claim paid: {ClaimNumber}, net {NetPayable}, advance deducted {AdvanceDeducted}",
            entity.ClaimNumber, entity.NetPayable, entity.AdvanceDeducted);
        return true;
    }

    // ---- Expense claim lines -----------------------------------------------

    public async Task<StaffTravelExpenseClaimLineDto> AddClaimLineAsync(CreateStaffTravelExpenseClaimLineDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        await GetOwnedClaimAsync(createDto.StaffTravelExpenseClaimId);

        var entity = createDto.ToEntity(tenantId, createdByUserId);
        ApplyBaseCurrencyAmount(entity);
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

    public async Task<StaffTravelExpenseClaimLineDto> UpdateClaimLineAsync(UpdateStaffTravelExpenseClaimLineDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedClaimLineAsync(updateDto.Id);

        entity.UpdateEntity(updateDto, updatedByUserId);
        await _lineRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await RecomputeClaimTotalsAsync(entity.StaffTravelExpenseClaimId, cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> ReviewClaimLineAsync(ReviewStaffTravelExpenseClaimLineDto reviewDto, Guid reviewerEmployeeId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedClaimLineAsync(reviewDto.LineId);

        entity.Status = reviewDto.Status;
        entity.AmountApproved = reviewDto.AmountApproved;
        entity.AmountRejected = reviewDto.AmountRejected;
        entity.RejectionReason = reviewDto.RejectionReason;
        entity.ReviewedById = reviewerEmployeeId;   // the caller, not a payload value
        entity.ReviewedAt = reviewDto.ReviewedAt;
        entity.UpdatedBy = reviewerEmployeeId.ToString();
        entity.UpdatedAt = DateTime.UtcNow;

        await _lineRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await RecomputeClaimTotalsAsync(entity.StaffTravelExpenseClaimId, cancellationToken);
        return true;
    }

    public async Task<bool> DeleteClaimLineAsync(Guid lineId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedClaimLineAsync(lineId);

        var claimId = entity.StaffTravelExpenseClaimId;
        await _lineRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await RecomputeClaimTotalsAsync(claimId, cancellationToken);
        return true;
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

    public async Task<StaffTravelAdvanceDto> CreateAdvanceAsync(CreateStaffTravelAdvanceDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        await RequireOwnedRequestAsync(createDto.StaffTravelRequestId);

        var entity = createDto.ToEntity(tenantId, createdByUserId);
        entity.AdvanceNumber = await GenerateAdvanceNumberAsync(cancellationToken);
        entity.Status = TravelAdvanceStatus.Requested;

        await _advanceRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Travel advance created: {AdvanceNumber}", entity.AdvanceNumber);
        var reloaded = await _advanceRepository.GetWithDetailsAsync(entity.TenantId, entity.Id);
        return (reloaded ?? entity).ToDto();
    }

    public async Task<StaffTravelAdvanceDto> UpdateAdvanceAsync(UpdateStaffTravelAdvanceDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAdvanceAsync(updateDto.Id);

        if (entity.Status is TravelAdvanceStatus.Disbursed)
            throw new InvalidOperationException("A disbursed advance cannot be edited.");

        entity.UpdateEntity(updateDto, updatedByUserId);
        entity.UnsettledAmount = (entity.ApprovedAmount ?? entity.RequestedAmount) - entity.SettledAmount;
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

    public async Task<bool> ApproveAdvanceAsync(ApproveStaffTravelAdvanceDto approveDto, Guid approverEmployeeId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAdvanceAsync(approveDto.AdvanceId);

        if (entity.Status != TravelAdvanceStatus.Requested)
            throw new InvalidOperationException("Only requested advances can be approved.");

        entity.Status = TravelAdvanceStatus.Approved;
        entity.ApprovedById = approverEmployeeId;   // the caller, not a payload value
        entity.ApprovedAmount = approveDto.ApprovedAmount;
        entity.UnsettledAmount = approveDto.ApprovedAmount - entity.SettledAmount;
        entity.UpdatedBy = approverEmployeeId.ToString();
        entity.UpdatedAt = DateTime.UtcNow;

        await _advanceRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Travel advance approved: {AdvanceNumber}", entity.AdvanceNumber);
        return true;
    }

    public async Task<bool> DisburseAdvanceAsync(DisburseStaffTravelAdvanceDto disburseDto, Guid disburserEmployeeId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAdvanceAsync(disburseDto.AdvanceId);

        if (entity.Status != TravelAdvanceStatus.Approved)
            throw new InvalidOperationException("Only approved advances can be disbursed.");

        entity.Status = TravelAdvanceStatus.Disbursed;
        entity.DisbursedById = disburserEmployeeId;   // the caller, not a payload value
        entity.DisbursedAt = disburseDto.DisbursedAt;
        entity.UpdatedBy = disburserEmployeeId.ToString();
        entity.UpdatedAt = DateTime.UtcNow;

        await _advanceRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Travel advance disbursed: {AdvanceNumber}", entity.AdvanceNumber);
        return true;
    }

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
    /// ⚠ The RATE is still caller-supplied. Sourcing it from Finance's ExchangeRate is slice 6
    /// (§7.2); this only stops the product disagreeing with its own factors.
    /// </remarks>
    private static void ApplyBaseCurrencyAmount(StaffTravelExpenseClaimLine line)
    {
        if (line.ExchangeRate <= 0m)
            throw new InvalidOperationException(
                "An exchange rate greater than zero is required to convert a claim line to the base currency.");

        line.AmountBaseCurrency = decimal.Round(line.AmountOriginal * line.ExchangeRate, 2, MidpointRounding.AwayFromZero);
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
    /// <para>⚠ This is the travel-side arithmetic only. The GL entries that ought to accompany it —
    /// clearing an employee receivable, posting the net payment — are deliberately out of scope per
    /// decision D-4 and are registered as items 12.1–12.3 in
    /// <c>docs/HR-FINANCE-INTEGRATION-BACKLOG.md</c>.</para>
    /// </remarks>
    private async Task SettleLinkedAdvanceAsync(
        StaffTravelExpenseClaim claim, CancellationToken cancellationToken)
    {
        if (claim.TravelAdvanceId is not Guid advanceId) return;

        var advance = await _advanceRepository.GetByIdAsync(advanceId);
        if (advance == null || advance.TenantId != claim.TenantId) return;

        // Only money that actually left the company can be recovered from a claim.
        if (advance.Status is not (TravelAdvanceStatus.Disbursed or TravelAdvanceStatus.PartiallySettled))
            return;

        var outstanding = advance.UnsettledAmount;
        if (outstanding <= 0m) return;

        // Recover against what the claim is worth before any deduction, not after.
        var recoverable = claim.TotalApproved > 0m ? claim.TotalApproved : claim.TotalClaimed;
        var deduction = Math.Min(outstanding, recoverable);
        if (deduction <= 0m) return;

        claim.AdvanceDeducted = deduction;
        claim.NetPayable = ComputeNetPayable(recoverable, deduction);

        advance.SettledAmount += deduction;
        advance.UnsettledAmount = (advance.ApprovedAmount ?? 0m) - advance.SettledAmount;
        advance.Status = advance.UnsettledAmount <= 0m
            ? TravelAdvanceStatus.FullySettled
            : TravelAdvanceStatus.PartiallySettled;
        advance.UpdatedAt = DateTime.UtcNow;

        await _advanceRepository.UpdateAsync(advance);
    }

    /// <summary>
    /// Confirms the travel request exists in the caller's tenant before money is hung off it.
    /// Budgets, claims and advances all took StaffTravelRequestId straight from the payload.
    /// </summary>
    private async Task RequireOwnedRequestAsync(Guid requestId)
    {
        var request = await _requestRepository.GetByIdAsync(requestId);
        if (request == null || request.TenantId != GetTenantId())
            throw new ArgumentException($"Staff travel request with ID '{requestId}' not found.");
    }

    /// <summary>What the employee is actually owed: the payable total less any advance held.</summary>
    private static decimal ComputeNetPayable(decimal payableTotal, decimal advanceDeducted)
        => payableTotal - advanceDeducted;

    private async Task RecomputeClaimTotalsAsync(Guid claimId, CancellationToken cancellationToken)
    {
        var tenantId = GetTenantId();
        var claim = await _claimRepository.GetWithLinesAsync(claimId);
        if (claim == null || claim.TenantId != tenantId) return;

        claim.TotalClaimed = claim.Lines.Sum(l => l.AmountBaseCurrency);
        claim.TotalApproved = claim.Lines.Sum(l => l.AmountApproved ?? 0m);
        claim.TotalRejected = claim.Lines.Sum(l => l.AmountRejected ?? 0m);

        // Once anything has been reviewed the payable figure is the APPROVED total less the
        // advance. Before that there is nothing approved, so fall back to the claimed total rather
        // than telling the traveller they are owed minus-the-advance.
        var reviewed = claim.Lines.Any(l => l.AmountApproved.HasValue || l.AmountRejected.HasValue);
        claim.NetPayable = ComputeNetPayable(
            reviewed ? claim.TotalApproved : claim.TotalClaimed, claim.AdvanceDeducted);

        await _claimRepository.UpdateAsync(claim);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task<string> GenerateClaimNumberAsync(CancellationToken cancellationToken)
    {
        var year = DateTime.UtcNow.Year;
        var count = await _claimRepository.CountByYearAsync(year);
        return $"EXP-{year}-{(count + 1):D5}";
    }

    private async Task<string> GenerateAdvanceNumberAsync(CancellationToken cancellationToken)
    {
        var year = DateTime.UtcNow.Year;
        var count = await _advanceRepository.CountByYearAsync(year);
        return $"ADV-{year}-{(count + 1):D5}";
    }
}

#endregion
