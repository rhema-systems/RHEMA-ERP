using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.DTOs.MobilePos;
using ErpSystem.Core.Entities.MobilePos;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Data;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.MobilePos;

public interface IMobilePosTillSessionService
{
    Task<CashierTillSessionDto?> GetCurrentAsync(string installationId, CancellationToken cancellationToken);
    Task<CashierTillSessionDto> OpenAsync(MobilePosOpenTillSessionRequestDto dto, CancellationToken cancellationToken);
    Task<MobilePosTillReconciliationDto> GetReconciliationAsync(
        Guid sessionId,
        string installationId,
        CancellationToken cancellationToken);
    Task<MobilePosTillCloseSubmissionDto?> GetCloseSubmissionAsync(
        Guid sessionId,
        string installationId,
        CancellationToken cancellationToken);
    Task<MobilePosTillCloseSubmissionDto> SubmitCloseAsync(
        Guid sessionId,
        MobilePosSubmitTillCloseRequestDto dto,
        CancellationToken cancellationToken);
    Task<IReadOnlyList<MobilePosTillCloseSubmissionDto>> GetReviewQueueAsync(
        CancellationToken cancellationToken);
    Task<IReadOnlyList<MobilePosTillCloseSubmissionDto>> GetTillCloseReportAsync(
        CancellationToken cancellationToken);
    Task<MobilePosTillCloseSubmissionDto> ResolvePendingSyncAsync(
        Guid submissionId,
        MobilePosResolvePendingSyncRequestDto dto,
        CancellationToken cancellationToken);
    Task<BankDepositDto> CreateBankDepositProposalAsync(
        Guid submissionId,
        MobilePosCreateDepositProposalRequestDto dto,
        CancellationToken cancellationToken);
}

/// <summary>
/// Binds the existing Finance custody service to the authenticated mobile device, store and till.
/// It does not maintain another till balance or bypass Finance ownership and concurrency controls.
/// </summary>
public sealed class MobilePosTillSessionService : IMobilePosTillSessionService
{
    private readonly IMobilePosFoundationService _foundation;
    private readonly ICashierTillService _cashierTills;
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IBankingSettlementService? _banking;

    public MobilePosTillSessionService(
        IMobilePosFoundationService foundation,
        ICashierTillService cashierTills,
        ApplicationDbContext db,
        ICurrentUserService currentUser,
        IBankingSettlementService? banking = null)
    {
        _foundation = foundation;
        _cashierTills = cashierTills;
        _db = db;
        _currentUser = currentUser;
        _banking = banking;
    }

    private Guid TenantId => _currentUser.TenantId is { } id && id != Guid.Empty
        ? id
        : throw new UnauthorizedAccessException("A current tenant is required for Mobile POS.");

    private Guid UserId => Guid.TryParse(_currentUser.UserId, out var id) && id != Guid.Empty
        ? id
        : throw new UnauthorizedAccessException("An authenticated user is required for Mobile POS.");

    private string UserName => string.IsNullOrWhiteSpace(_currentUser.UserName)
        ? "Unknown"
        : _currentUser.UserName.Trim();

    public async Task<CashierTillSessionDto?> GetCurrentAsync(
        string installationId,
        CancellationToken cancellationToken)
    {
        var bootstrap = await _foundation.GetBootstrapAsync(installationId, cancellationToken);
        if (!bootstrap.CurrentTillSessionId.HasValue)
            return null;

        var session = await _cashierTills.GetSessionAsync(
            bootstrap.CurrentTillSessionId.Value,
            cancellationToken);
        if (session == null || session.LiquidityAccountId != bootstrap.Till.LiquidityAccountId)
            throw new InvalidOperationException("The active cashier session no longer belongs to the assigned Mobile POS till.");

        return session;
    }

    public async Task<MobilePosTillReconciliationDto> GetReconciliationAsync(
        Guid sessionId,
        string installationId,
        CancellationToken cancellationToken)
    {
        if (sessionId == Guid.Empty)
            throw new KeyNotFoundException("The till session was not found.");

        var bootstrap = await _foundation.GetBootstrapAsync(installationId, cancellationToken);
        var session = await _cashierTills.GetSessionAsync(sessionId, cancellationToken)
            ?? throw new KeyNotFoundException("The till session was not found.");
        if (session.LiquidityAccountId != bootstrap.Till.LiquidityAccountId
            || session.CashierUserId != UserId)
        {
            throw new UnauthorizedAccessException("This till session does not belong to the current operator and assigned Mobile POS till.");
        }

        var tenantId = TenantId;
        var sales = await _db.MobilePosSales.AsNoTracking()
            .Where(item => item.TenantId == tenantId
                && item.CashierTillSessionId == sessionId
                && item.MobilePosStoreId == bootstrap.Store.Id
                && item.MobilePosTillId == bootstrap.Till.Id
                && !item.IsDeleted)
            .Select(item => new
            {
                item.Status,
                item.SubTotal,
                item.TaxAmount,
                item.DiscountAmount,
                item.TotalAmount,
                WasRecordedOffline = item.MobilePosOfflineGrantId.HasValue
            })
            .ToListAsync(cancellationToken);

        var completed = sales.Where(item => item.Status == MobilePosSaleStatus.Completed).ToArray();
        var tenderRows = await _db.MobilePosTenders.AsNoTracking()
            .Where(item => item.TenantId == tenantId
                && item.MobilePosSale.CashierTillSessionId == sessionId
                && item.MobilePosSale.MobilePosStoreId == bootstrap.Store.Id
                && item.MobilePosSale.MobilePosTillId == bootstrap.Till.Id
                && item.MobilePosSale.Status == MobilePosSaleStatus.Completed
                && !item.IsDeleted
                && !item.MobilePosSale.IsDeleted)
            .Select(item => new
            {
                item.PaymentMethodId,
                PaymentMethodCode = item.PaymentMethod.Code ?? string.Empty,
                PaymentMethodName = item.PaymentMethod.Name,
                PaymentMethodType = item.PaymentMethod.Type.ToString(),
                item.Amount,
                item.WasRecordedOffline,
                item.CustomerPaymentId,
                item.Status
            })
            .ToListAsync(cancellationToken);

        var completedTenders = tenderRows
            .Where(item => item.Status == MobilePosTenderStatus.Completed)
            .ToArray();
        var salesTotal = completed.Sum(item => item.TotalAmount);
        var tenderTotal = completedTenders.Sum(item => item.Amount);
        var difference = decimal.Round(tenderTotal - salesTotal, 4, MidpointRounding.AwayFromZero);

        return new MobilePosTillReconciliationDto
        {
            GeneratedAtUtc = DateTime.UtcNow,
            Session = session,
            CompletedSaleCount = completed.Length,
            OfflineSaleCount = completed.Count(item => item.WasRecordedOffline),
            PendingSaleCount = sales.Count(item => item.Status == MobilePosSaleStatus.Pending),
            RejectedSaleCount = sales.Count(item => item.Status == MobilePosSaleStatus.Rejected),
            SubTotal = completed.Sum(item => item.SubTotal),
            TaxAmount = completed.Sum(item => item.TaxAmount),
            DiscountAmount = completed.Sum(item => item.DiscountAmount),
            SalesTotal = salesTotal,
            TenderTotal = tenderTotal,
            SalesTenderDifference = difference,
            SalesAndTendersBalance = Math.Abs(difference) < 0.01m,
            IncompleteTenderCount = tenderRows.Count(item => item.Status != MobilePosTenderStatus.Completed),
            Tenders = completedTenders
                .GroupBy(item => new
                {
                    item.PaymentMethodId,
                    item.PaymentMethodCode,
                    item.PaymentMethodName,
                    item.PaymentMethodType
                })
                .OrderBy(group => group.Key.PaymentMethodName)
                .Select(group => new MobilePosTillTenderReconciliationDto
                {
                    PaymentMethodId = group.Key.PaymentMethodId,
                    PaymentMethodCode = group.Key.PaymentMethodCode,
                    PaymentMethodName = group.Key.PaymentMethodName,
                    PaymentMethodType = group.Key.PaymentMethodType,
                    TenderCount = group.Count(),
                    Amount = group.Sum(item => item.Amount),
                    OfflineTenderCount = group.Count(item => item.WasRecordedOffline),
                    OfflineAmount = group.Where(item => item.WasRecordedOffline).Sum(item => item.Amount),
                    CanonicalPaymentCount = group.Count(item => item.CustomerPaymentId.HasValue)
                })
                .ToArray()
        };
    }

    public async Task<MobilePosTillCloseSubmissionDto?> GetCloseSubmissionAsync(
        Guid sessionId,
        string installationId,
        CancellationToken cancellationToken)
    {
        var bootstrap = await _foundation.GetBootstrapAsync(installationId, cancellationToken);
        var session = await RequireOwnedSessionAsync(sessionId, bootstrap, cancellationToken);
        var submission = await _db.MobilePosTillCloseSubmissions.AsNoTracking()
            .SingleOrDefaultAsync(item => item.TenantId == TenantId
                && item.CashierTillSessionId == sessionId && !item.IsDeleted,
                cancellationToken);
        return submission == null ? null : MapSubmission(submission, session);
    }

    public async Task<MobilePosTillCloseSubmissionDto> SubmitCloseAsync(
        Guid sessionId,
        MobilePosSubmitTillCloseRequestDto dto,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var bootstrap = await _foundation.GetBootstrapAsync(dto.InstallationId, cancellationToken);
        var session = await RequireOwnedSessionAsync(sessionId, bootstrap, cancellationToken);
        var pendingIds = NormalizePendingMutationIds(dto.PendingClientMutationIds);
        var policyAllowsPendingSync = bootstrap.OfflinePolicy?.AllowDayEndSubmissionWithPendingSync == true;
        if (pendingIds.Length > 0 && !policyAllowsPendingSync)
        {
            throw new InvalidOperationException(
                "Synchronize or resolve all Mobile POS work before submitting day end. The assigned offline policy does not allow a pending-sync submission.");
        }

        var strategy = _db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = _db.Database.IsRelational()
                ? await _db.Database.BeginTransactionAsync(cancellationToken)
                : null;
            var submittedSession = await _cashierTills.SubmitCountAsync(
                sessionId,
                new SubmitCashierTillCountDto
                {
                    CountLines = dto.CountLines,
                    VarianceReason = dto.VarianceReason,
                    ClosingEvidenceFileId = dto.ClosingEvidenceFileId,
                    RowVersion = dto.SessionRowVersion
                },
                cancellationToken);

            var now = DateTime.UtcNow;
            var submission = await _db.MobilePosTillCloseSubmissions.SingleOrDefaultAsync(item =>
                item.TenantId == TenantId && item.CashierTillSessionId == sessionId && !item.IsDeleted,
                cancellationToken);
            if (submission == null)
            {
                submission = new MobilePosTillCloseSubmission
                {
                    Id = Guid.NewGuid(),
                    TenantId = TenantId,
                    CashierTillSessionId = sessionId,
                    MobilePosStoreId = bootstrap.Store.Id,
                    MobilePosTillId = bootstrap.Till.Id,
                    MobilePosDeviceId = bootstrap.Device.Id,
                    CreatedAt = now,
                    CreatedBy = UserName,
                    CreatedById = UserId
                };
                _db.MobilePosTillCloseSubmissions.Add(submission);
            }
            else
            {
                submission.MobilePosStoreId = bootstrap.Store.Id;
                submission.MobilePosTillId = bootstrap.Till.Id;
                submission.MobilePosDeviceId = bootstrap.Device.Id;
                submission.UpdatedAt = now;
                submission.UpdatedBy = UserName;
                submission.LastModifiedById = UserId;
            }

            submission.MobilePosOfflinePolicyId = bootstrap.Store.OfflinePolicyId;
            submission.SubmittedByUserId = UserId;
            submission.SubmittedAtUtc = now;
            submission.PolicyAllowedPendingSync = policyAllowsPendingSync;
            submission.PendingMutationCount = pendingIds.Length;
            submission.PendingMutationIdsJson = JsonSerializer.Serialize(pendingIds);
            submission.PendingMutationDigest = HashPendingMutationIds(pendingIds);
            submission.Status = pendingIds.Length == 0
                ? MobilePosTillCloseSubmissionStatus.ReadyForReview
                : MobilePosTillCloseSubmissionStatus.PendingSync;
            submission.SyncExceptionResolvedAtUtc = null;
            submission.SyncExceptionResolvedByUserId = null;
            submission.SyncExceptionResolutionReason = null;
            submission.FinalizedAtUtc = null;
            submission.FinalizedByUserId = null;
            await _db.SaveChangesAsync(cancellationToken);
            if (transaction != null)
                await transaction.CommitAsync(cancellationToken);
            return MapSubmission(submission, submittedSession);
        });
    }

    public async Task<IReadOnlyList<MobilePosTillCloseSubmissionDto>> GetReviewQueueAsync(
        CancellationToken cancellationToken)
    {
        var submissions = await _db.MobilePosTillCloseSubmissions.AsNoTracking()
            .Where(item => item.TenantId == TenantId && !item.IsDeleted
                && (item.Status == MobilePosTillCloseSubmissionStatus.ReadyForReview
                    || item.Status == MobilePosTillCloseSubmissionStatus.PendingSync
                    || item.Status == MobilePosTillCloseSubmissionStatus.SyncExceptionResolved))
            .OrderBy(item => item.SubmittedAtUtc)
            .Take(500)
            .ToListAsync(cancellationToken);
        var result = new List<MobilePosTillCloseSubmissionDto>(submissions.Count);
        foreach (var submission in submissions)
        {
            var session = await _cashierTills.GetSessionAsync(submission.CashierTillSessionId, cancellationToken);
            if (session != null)
                result.Add(MapSubmission(submission, session));
        }
        return result;
    }

    public async Task<IReadOnlyList<MobilePosTillCloseSubmissionDto>> GetTillCloseReportAsync(
        CancellationToken cancellationToken)
    {
        var submissions = await _db.MobilePosTillCloseSubmissions.AsNoTracking()
            .Include(item => item.BankDepositBatch)
            .Where(item => item.TenantId == TenantId && !item.IsDeleted)
            .OrderByDescending(item => item.SubmittedAtUtc)
            .Take(1000)
            .ToListAsync(cancellationToken);
        var result = new List<MobilePosTillCloseSubmissionDto>(submissions.Count);
        foreach (var submission in submissions)
        {
            var session = await _cashierTills.GetSessionAsync(submission.CashierTillSessionId, cancellationToken);
            if (session != null)
                result.Add(MapSubmission(submission, session));
        }
        return result;
    }

    public async Task<MobilePosTillCloseSubmissionDto> ResolvePendingSyncAsync(
        Guid submissionId,
        MobilePosResolvePendingSyncRequestDto dto,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var reason = dto.Reason?.Trim() ?? string.Empty;
        if (reason.Length < 20)
            throw new InvalidOperationException("The sync exception resolution reason must contain at least 20 characters.");
        var submission = await _db.MobilePosTillCloseSubmissions
            .SingleOrDefaultAsync(item => item.TenantId == TenantId && item.Id == submissionId && !item.IsDeleted,
                cancellationToken)
            ?? throw new KeyNotFoundException("The Mobile POS till-close submission was not found.");
        if (submission.Status != MobilePosTillCloseSubmissionStatus.PendingSync)
            throw new InvalidOperationException("Only a pending-sync till close can receive an HQ exception resolution.");
        var session = await _cashierTills.GetSessionAsync(submission.CashierTillSessionId, cancellationToken)
            ?? throw new KeyNotFoundException("The Finance till session was not found.");
        if (session.CashierUserId == UserId)
            throw new UnauthorizedAccessException("The cashier cannot resolve their own pending-sync day-end exception.");
        SetRowVersion(submission, dto.RowVersion);
        var now = DateTime.UtcNow;
        submission.Status = MobilePosTillCloseSubmissionStatus.SyncExceptionResolved;
        submission.SyncExceptionResolvedAtUtc = now;
        submission.SyncExceptionResolvedByUserId = UserId;
        submission.SyncExceptionResolutionReason = reason;
        submission.UpdatedAt = now;
        submission.UpdatedBy = UserName;
        submission.LastModifiedById = UserId;
        await _db.SaveChangesAsync(cancellationToken);
        return MapSubmission(submission, session);
    }

    public async Task<BankDepositDto> CreateBankDepositProposalAsync(
        Guid submissionId,
        MobilePosCreateDepositProposalRequestDto dto,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dto);
        if (_banking == null)
            throw new InvalidOperationException("The Finance banking workflow is not available.");
        if (dto.BankAccountId == Guid.Empty)
            throw new InvalidOperationException("Select the destination bank account.");
        var reference = dto.DepositReference?.Trim() ?? string.Empty;
        if (reference.Length < 3)
            throw new InvalidOperationException("Enter the deposit slip or preparation reference.");

        var submission = await _db.MobilePosTillCloseSubmissions
            .Include(item => item.CashierTillSession)
            .SingleOrDefaultAsync(item => item.TenantId == TenantId && item.Id == submissionId && !item.IsDeleted,
                cancellationToken)
            ?? throw new KeyNotFoundException("The Mobile POS till-close submission was not found.");
        if (submission.Status != MobilePosTillCloseSubmissionStatus.Finalized
            || submission.CashierTillSession.Status != CashierTillSessionStatus.Closed)
        {
            throw new InvalidOperationException("A bank deposit proposal can be generated only after independent till finalization.");
        }
        if (submission.BankDepositBatchId.HasValue)
        {
            return await _banking.GetDepositAsync(submission.BankDepositBatchId.Value, cancellationToken)
                ?? throw new InvalidOperationException("The linked Finance bank deposit could not be loaded.");
        }

        SetRowVersion(submission, dto.RowVersion);
        var session = submission.CashierTillSession;
        var sourceMarker = $"[MobilePosTillClose:{submission.Id:N}]";
        var existingDeposit = await _db.BankDepositBatches.AsNoTracking()
            .SingleOrDefaultAsync(item => item.TenantId == TenantId
                && item.BankAccountId == dto.BankAccountId
                && item.DepositReference == reference
                && !item.IsDeleted,
                cancellationToken);
        if (existingDeposit != null)
        {
            if (!ContainsSourceMarker(existingDeposit.Notes, sourceMarker))
            {
                throw new InvalidOperationException(
                    "The deposit reference is already used by another Finance bank deposit for the selected account.");
            }
            return await LinkExistingDepositAsync(submission.Id, existingDeposit.Id, cancellationToken);
        }

        var cutoff = session.ActivityCutoffAt
            ?? throw new InvalidOperationException("The finalized till session has no activity cutoff.");
        var entries = await _db.LiquidityAccountEntries.AsNoTracking()
            .Where(item => item.TenantId == TenantId
                && item.LiquidityAccountId == session.LiquidityAccountId
                && item.CreatedAt >= session.OpenedAt
                && item.CreatedAt <= cutoff
                && !item.IsReversed
                && item.Amount > item.AllocatedAmount)
            .OrderBy(item => item.CreatedAt)
            .ToListAsync(cancellationToken);
        if (entries.Count == 0)
            throw new InvalidOperationException("This finalized till session has no unallocated cash custody entries to propose for deposit.");

        BankDepositDto deposit;
        try
        {
            deposit = await _banking.CreateDepositAsync(new CreateBankDepositDto
            {
                BankAccountId = dto.BankAccountId,
                DepositDate = dto.DepositDate.Date,
                DepositReference = reference,
                Notes = string.Join(" ", new[]
                {
                    sourceMarker,
                    $"Mobile POS till-close proposal for {session.SessionNumber}.",
                    dto.Notes?.Trim()
                }.Where(value => !string.IsNullOrWhiteSpace(value))),
                Allocations = entries.Select(item => new BankDepositAllocationRequestDto
                {
                    LiquidityAccountEntryId = item.Id,
                    AllocationType = item.Direction == LiquidityEntryDirection.Increase
                        ? BankDepositAllocationType.Receipt
                        : BankDepositAllocationType.Deduction,
                    Amount = item.Amount - item.AllocatedAmount,
                    Notes = $"Proposed from Mobile POS till session {session.SessionNumber}."
                }).ToList()
            }, cancellationToken);
        }
        catch (DbUpdateException)
        {
            // A concurrent retry can win the canonical Finance unique reference constraint after
            // this request's initial lookup. Recover only the deposit carrying this submission's
            // immutable source marker; unrelated duplicate references must still fail.
            var concurrentDeposit = await _db.BankDepositBatches.AsNoTracking()
                .SingleOrDefaultAsync(item => item.TenantId == TenantId
                    && item.BankAccountId == dto.BankAccountId
                    && item.DepositReference == reference
                    && !item.IsDeleted,
                    cancellationToken);
            if (concurrentDeposit == null || !ContainsSourceMarker(concurrentDeposit.Notes, sourceMarker))
                throw;
            return await LinkExistingDepositAsync(submission.Id, concurrentDeposit.Id, cancellationToken);
        }

        var now = DateTime.UtcNow;
        submission.BankDepositBatchId = deposit.Id;
        submission.BankDepositProposedAtUtc = now;
        submission.BankDepositProposedByUserId = UserId;
        submission.UpdatedAt = now;
        submission.UpdatedBy = UserName;
        submission.LastModifiedById = UserId;
        await _db.SaveChangesAsync(cancellationToken);
        return deposit;
    }

    private async Task<BankDepositDto> LinkExistingDepositAsync(
        Guid submissionId,
        Guid bankDepositBatchId,
        CancellationToken cancellationToken)
    {
        var submission = await _db.MobilePosTillCloseSubmissions
            .SingleAsync(item => item.TenantId == TenantId && item.Id == submissionId && !item.IsDeleted,
                cancellationToken);
        if (submission.BankDepositBatchId.HasValue && submission.BankDepositBatchId != bankDepositBatchId)
            throw new InvalidOperationException("This till close is already linked to a different Finance bank deposit.");
        var now = DateTime.UtcNow;
        submission.BankDepositBatchId = bankDepositBatchId;
        submission.BankDepositProposedAtUtc ??= now;
        submission.BankDepositProposedByUserId ??= UserId;
        submission.UpdatedAt = now;
        submission.UpdatedBy = UserName;
        submission.LastModifiedById = UserId;
        await _db.SaveChangesAsync(cancellationToken);
        return await _banking!.GetDepositAsync(bankDepositBatchId, cancellationToken)
            ?? throw new InvalidOperationException("The recovered Finance bank deposit could not be loaded.");
    }

    private static bool ContainsSourceMarker(string? notes, string sourceMarker)
        => notes?.Contains(sourceMarker, StringComparison.Ordinal) == true;

    private async Task<CashierTillSessionDto> RequireOwnedSessionAsync(
        Guid sessionId,
        MobilePosBootstrapDto bootstrap,
        CancellationToken cancellationToken)
    {
        var session = await _cashierTills.GetSessionAsync(sessionId, cancellationToken)
            ?? throw new KeyNotFoundException("The till session was not found.");
        if (session.LiquidityAccountId != bootstrap.Till.LiquidityAccountId
            || session.CashierUserId != UserId)
        {
            throw new UnauthorizedAccessException("This till session does not belong to the current operator and assigned Mobile POS till.");
        }
        return session;
    }

    private static string[] NormalizePendingMutationIds(IReadOnlyList<string>? values)
    {
        var normalized = (values ?? [])
            .Select(value => value?.Trim() ?? string.Empty)
            .Where(value => value.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        if (normalized.Length > 500)
            throw new InvalidOperationException("A till-close submission can reference at most 500 pending Mobile POS mutations.");
        if (normalized.Any(value => value.Length < 8 || value.Length > 100
            || value.Any(character => !(char.IsLetterOrDigit(character) || character is '-' or '_' or '.' or ':'))))
        {
            throw new InvalidOperationException("A pending Mobile POS mutation ID is invalid.");
        }
        return normalized;
    }

    internal static string HashPendingMutationIds(IReadOnlyList<string> values)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("\n", values))));

    private static MobilePosTillCloseSubmissionDto MapSubmission(
        MobilePosTillCloseSubmission submission,
        CashierTillSessionDto session) => new()
    {
        Id = submission.Id,
        CashierTillSessionId = submission.CashierTillSessionId,
        MobilePosStoreId = submission.MobilePosStoreId,
        MobilePosTillId = submission.MobilePosTillId,
        MobilePosDeviceId = submission.MobilePosDeviceId,
        MobilePosOfflinePolicyId = submission.MobilePosOfflinePolicyId,
        SubmittedByUserId = submission.SubmittedByUserId,
        SubmittedAtUtc = submission.SubmittedAtUtc,
        PolicyAllowedPendingSync = submission.PolicyAllowedPendingSync,
        PendingMutationCount = submission.PendingMutationCount,
        PendingMutationDigest = submission.PendingMutationDigest,
        PendingClientMutationIds = DeserializePendingMutationIds(submission.PendingMutationIdsJson),
        Status = submission.Status,
        SyncExceptionResolvedAtUtc = submission.SyncExceptionResolvedAtUtc,
        SyncExceptionResolvedByUserId = submission.SyncExceptionResolvedByUserId,
        SyncExceptionResolutionReason = submission.SyncExceptionResolutionReason,
        FinalizedAtUtc = submission.FinalizedAtUtc,
        FinalizedByUserId = submission.FinalizedByUserId,
        BankDepositBatchId = submission.BankDepositBatchId,
        BankDepositNumber = submission.BankDepositBatch?.DepositNumber,
        BankDepositStatus = submission.BankDepositBatch?.Status,
        BankDepositProposedAtUtc = submission.BankDepositProposedAtUtc,
        BankDepositProposedByUserId = submission.BankDepositProposedByUserId,
        RowVersion = Convert.ToBase64String(submission.RowVersion),
        Session = session
    };

    internal static string[] DeserializePendingMutationIds(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<string[]>(json) ?? [];
        }
        catch (JsonException)
        {
            throw new InvalidOperationException("The retained pending-sync evidence is invalid.");
        }
    }

    private void SetRowVersion(MobilePosTillCloseSubmission submission, string rowVersion)
    {
        if (string.IsNullOrWhiteSpace(rowVersion))
            throw new InvalidOperationException("The till-close submission version is required. Refresh and try again.");
        try
        {
            var suppliedVersion = Convert.FromBase64String(rowVersion);
            if (_db.Database.IsRelational())
                _db.Entry(submission).Property(item => item.RowVersion).OriginalValue = suppliedVersion;
        }
        catch (FormatException)
        {
            throw new InvalidOperationException("The till-close submission version is invalid. Refresh and try again.");
        }
    }

    public async Task<CashierTillSessionDto> OpenAsync(
        MobilePosOpenTillSessionRequestDto dto,
        CancellationToken cancellationToken)
    {
        var bootstrap = await _foundation.GetBootstrapAsync(dto.InstallationId, cancellationToken);
        if (bootstrap.CurrentTillSessionId.HasValue)
            throw new InvalidOperationException("This operator already has an open session on the assigned till.");

        var businessDate = ResolveBusinessDate(bootstrap.Store.TimeZoneId, bootstrap.ServerTimeUtc);
        var session = await _cashierTills.OpenSessionAsync(new OpenCashierTillSessionDto
        {
            LiquidityAccountId = bootstrap.Till.LiquidityAccountId,
            BusinessDate = businessDate,
            OpeningFloatAmount = dto.OpeningFloatAmount,
            OpeningNotes = dto.OpeningNotes,
            OpeningEvidenceFileId = dto.OpeningEvidenceFileId
        }, cancellationToken);

        if (session.LiquidityAccountId != bootstrap.Till.LiquidityAccountId)
            throw new InvalidOperationException("Finance opened a cashier session for an unexpected till.");

        return session;
    }

    private static DateTime ResolveBusinessDate(string timeZoneId, DateTime serverTimeUtc)
    {
        try
        {
            var timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            return TimeZoneInfo.ConvertTimeFromUtc(
                DateTime.SpecifyKind(serverTimeUtc, DateTimeKind.Utc),
                timeZone).Date;
        }
        catch (TimeZoneNotFoundException)
        {
            throw new InvalidOperationException($"The Mobile POS store time zone '{timeZoneId}' is not available on this server.");
        }
        catch (InvalidTimeZoneException)
        {
            throw new InvalidOperationException($"The Mobile POS store time zone '{timeZoneId}' is invalid.");
        }
    }
}
