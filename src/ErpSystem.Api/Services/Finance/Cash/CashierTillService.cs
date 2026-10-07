using System.Data;
using ErpSystem.Api.Services.Finance;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Core.Interfaces.Numbering;
using ErpSystem.Data;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.Finance.Cash;

/// <summary>
/// Controls physical cashier custody while deliberately reusing the existing liquidity subledger.
/// The service never accepts or maintains a client-authored expected balance: movements come from
/// immutable liquidity entries and posted deposit allocations within the session's UTC window.
/// </summary>
public sealed class CashierTillService : ICashierTillService
{
    private const decimal CurrencyTolerance = 0.01m;
    private const int MinimumCorrectionReasonLength = 20;
    private const int MinimumCancellationReasonLength = 10;
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IDocumentNumberingService _numbering;
    private readonly IFinanceAuditService? _audit;

    public CashierTillService(
        ApplicationDbContext context,
        ICurrentUserService currentUser,
        IDocumentNumberingService numbering,
        IFinanceAuditService? audit = null)
    {
        _context = context;
        _currentUser = currentUser;
        _numbering = numbering;
        _audit = audit;
    }

    private Guid TenantId => _currentUser.GetRequiredFinanceTenantId();

    private Guid UserId
        => Guid.TryParse(_currentUser.UserId, out var userId) && userId != Guid.Empty
            ? userId
            : throw new InvalidOperationException("An authenticated application user is required.");

    private string UserName
        => string.IsNullOrWhiteSpace(_currentUser.UserName) ? "Unknown" : _currentUser.UserName!.Trim();

    public async Task<IReadOnlyList<CashierTillSessionDto>> GetSessionsAsync(
        CashierTillSessionStatus? status = null,
        Guid? liquidityAccountId = null,
        DateTime? businessDate = null,
        CancellationToken cancellationToken = default)
    {
        var tenantId = TenantId;
        var query = SessionQuery().Where(item => item.TenantId == tenantId);
        if (status.HasValue)
        {
            query = query.Where(item => item.Status == status.Value);
        }
        if (liquidityAccountId.HasValue)
        {
            query = query.Where(item => item.LiquidityAccountId == liquidityAccountId.Value);
        }
        if (businessDate.HasValue)
        {
            var date = businessDate.Value.Date;
            query = query.Where(item => item.BusinessDate == date);
        }

        var sessions = await query
            .OrderByDescending(item => item.OpenedAt)
            .Take(500)
            .ToListAsync(cancellationToken);
        var liveActivities = await LoadLiveActivitiesAsync(sessions, cancellationToken);
        var result = new List<CashierTillSessionDto>(sessions.Count);
        foreach (var session in sessions)
        {
            // Open sessions remain live. Recalculate their expected amount for the list so the
            // dashboard never presents a stale operational balance as if it were approved.
            liveActivities.TryGetValue(session.Id, out var liveActivity);
            result.Add(await MapAsync(
                session,
                includeCustodyEntries: false,
                cancellationToken,
                liveActivity));
        }
        return result;
    }

    public async Task<CashierTillSessionDto?> GetSessionAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var session = await SessionQuery().FirstOrDefaultAsync(
            item => item.TenantId == TenantId && item.Id == id,
            cancellationToken);
        return session == null
            ? null
            : await MapAsync(session, includeCustodyEntries: true, cancellationToken);
    }

    public async Task<CashierTillSessionDto> OpenSessionAsync(
        OpenCashierTillSessionDto dto,
        CancellationToken cancellationToken = default)
    {
        if (dto.OpeningFloatAmount < 0m)
        {
            throw new InvalidOperationException("Opening float cannot be negative.");
        }
        if (dto.BusinessDate == default)
        {
            throw new InvalidOperationException("Till business date is required.");
        }
        if (dto.BusinessDate.Date > DateTime.UtcNow.Date.AddDays(1))
        {
            throw new InvalidOperationException("Till business date cannot be more than one day in the future.");
        }

        var tenantId = TenantId;
        var till = await _context.LiquidityAccounts.FirstOrDefaultAsync(
            item => item.TenantId == tenantId &&
                    item.Id == dto.LiquidityAccountId &&
                    item.AccountType == LiquidityAccountType.CashTill &&
                    item.IsActive,
            cancellationToken)
            ?? throw new InvalidOperationException("Select an active CashTill liquidity account belonging to this tenant.");
        await ValidateEvidenceAsync(dto.OpeningEvidenceFileId, "opening", cancellationToken);

        var strategy = _context.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            // SERIALIZABLE plus the focused account/status index closes the race in which two users
            // attempt to take custody of the same physical till at nearly the same time.
            await using var transaction = _context.Database.IsRelational()
                ? await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken)
                : null;
            var hasActiveSession = await _context.CashierTillSessions.AnyAsync(
                item => item.TenantId == tenantId &&
                        item.LiquidityAccountId == till.Id &&
                        (item.Status == CashierTillSessionStatus.Open ||
                         item.Status == CashierTillSessionStatus.PendingReview),
                cancellationToken);
            if (hasActiveSession)
            {
                throw new InvalidOperationException("This till already has an open or pending-review custody session.");
            }

            var now = DateTime.UtcNow;
            var session = new CashierTillSession
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                SessionNumber = await _numbering.GenerateAsync(
                    DocumentNumberingModules.Finance,
                    FinanceDocumentTypes.CashTillSession,
                    tenantId,
                    dto.BusinessDate,
                    nameof(CashierTillSession),
                    cancellationToken: cancellationToken),
                LiquidityAccountId = till.Id,
                BusinessDate = dto.BusinessDate.Date,
                Currency = till.Currency.ToUpperInvariant(),
                CashierUserId = UserId,
                CashierName = UserName,
                Status = CashierTillSessionStatus.Open,
                OpeningFloatAmount = RoundMoney(dto.OpeningFloatAmount),
                OpeningNotes = Clean(dto.OpeningNotes),
                OpeningEvidenceFileId = dto.OpeningEvidenceFileId,
                OpenedAt = now,
                OpenedById = UserId,
                CreatedAt = now,
                CreatedBy = UserName,
                CreatedById = UserId
            };
            _context.CashierTillSessions.Add(session);
            await _context.SaveChangesAsync(cancellationToken);
            if (transaction != null)
            {
                await transaction.CommitAsync(cancellationToken);
            }
            await RecordAuditAsync("Finance.CashTill.Opened", session, null, new
            {
                session.OpeningFloatAmount,
                session.BusinessDate,
                session.OpeningEvidenceFileId
            }, cancellationToken);
            return await GetRequiredSessionDtoAsync(session.Id, cancellationToken);
        });
    }

    public async Task<CashierTillSessionDto> UpdateOpeningAsync(
        Guid id,
        UpdateCashierTillOpeningDto dto,
        CancellationToken cancellationToken = default)
    {
        if (dto.OpeningFloatAmount < 0m)
        {
            throw new InvalidOperationException("Opening float cannot be negative.");
        }
        await ValidateEvidenceAsync(dto.OpeningEvidenceFileId, "opening", cancellationToken);

        var strategy = _context.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = _context.Database.IsRelational()
                ? await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken)
                : null;
            var session = await LoadForActionAsync(id, cancellationToken);
            EnsureOwner(session, "edit this till session");
            SetRowVersion(session, dto.RowVersion);
            var activity = await CalculateActivityAsync(session, DateTime.UtcNow, includeEntries: false, cancellationToken);
            var lockReason = GetOpeningDetailsLockReason(session, activity);
            if (lockReason != null)
            {
                throw new InvalidOperationException(lockReason);
            }

            var before = new
            {
                session.OpeningFloatAmount,
                session.OpeningNotes,
                session.OpeningEvidenceFileId
            };
            var now = DateTime.UtcNow;
            session.OpeningFloatAmount = RoundMoney(dto.OpeningFloatAmount);
            session.OpeningNotes = Clean(dto.OpeningNotes);
            session.OpeningEvidenceFileId = dto.OpeningEvidenceFileId;
            StampModified(session, now);
            await _context.SaveChangesAsync(cancellationToken);
            if (transaction != null)
            {
                await transaction.CommitAsync(cancellationToken);
            }
            await RecordAuditAsync("Finance.CashTill.OpeningDetailsUpdated", session, before, new
            {
                session.OpeningFloatAmount,
                session.OpeningNotes,
                session.OpeningEvidenceFileId
            }, cancellationToken);
            return await GetRequiredSessionDtoAsync(id, cancellationToken);
        });
    }

    public async Task<CashierTillSessionDto> CancelSessionAsync(
        Guid id,
        CancelCashierTillSessionDto dto,
        CancellationToken cancellationToken = default)
    {
        var reason = RequireText(dto.Reason, "Cancellation reason", 1000);
        if (reason.Length < MinimumCancellationReasonLength)
        {
            throw new InvalidOperationException($"Cancellation reason must contain at least {MinimumCancellationReasonLength} characters.");
        }

        var strategy = _context.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = _context.Database.IsRelational()
                ? await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken)
                : null;
            var session = await LoadForActionAsync(id, cancellationToken);
            EnsureOwner(session, "cancel this till session");
            SetRowVersion(session, dto.RowVersion);
            var now = DateTime.UtcNow;
            var activity = await CalculateActivityAsync(session, now, includeEntries: false, cancellationToken);
            var lockReason = GetOpeningDetailsLockReason(session, activity);
            if (lockReason != null)
            {
                throw new InvalidOperationException(lockReason);
            }

            session.Status = CashierTillSessionStatus.Cancelled;
            session.ActivityCutoffAt = now;
            session.TransactionMovementAmount = activity.TransactionMovementAmount;
            session.DepositedAmount = activity.DepositedAmount;
            session.ExpectedClosingAmount = activity.ExpectedClosingAmount;
            session.CustodyEntryCount = activity.EntryCount;
            session.CancelledAt = now;
            session.CancelledById = UserId;
            session.CancellationReason = reason;
            StampModified(session, now);
            await _context.SaveChangesAsync(cancellationToken);
            if (transaction != null)
            {
                await transaction.CommitAsync(cancellationToken);
            }
            await RecordAuditAsync("Finance.CashTill.Cancelled", session, null, new
            {
                reason,
                session.CancelledAt,
                session.CancelledById
            }, cancellationToken);
            return await GetRequiredSessionDtoAsync(id, cancellationToken);
        });
    }

    public async Task<CashierTillSessionDto> SubmitCountAsync(
        Guid id,
        SubmitCashierTillCountDto dto,
        CancellationToken cancellationToken = default)
    {
        var session = await LoadForActionAsync(id, cancellationToken);
        if (session.Status != CashierTillSessionStatus.Open)
        {
            throw new InvalidOperationException("Only an open till session can be submitted for review.");
        }
        if (session.CashierUserId != UserId)
        {
            throw new UnauthorizedAccessException("Only the cashier holding this till can submit its count.");
        }
        SetRowVersion(session, dto.RowVersion);
        await ValidateEvidenceAsync(dto.ClosingEvidenceFileId, "closing", cancellationToken);

        var normalizedLines = NormalizeCountLines(dto.CountLines);
        var cutoff = DateTime.UtcNow;
        var activity = await CalculateActivityAsync(session, cutoff, includeEntries: false, cancellationToken);
        var counted = RoundMoney(normalizedLines.Sum(item => item.LineAmount));
        var variance = RoundMoney(counted - activity.ExpectedClosingAmount);
        var settings = await GetSettingsAsync(cancellationToken);
        var threshold = RoundMoney(settings.CashTillVarianceApprovalThreshold);
        var reason = Clean(dto.VarianceReason);
        if (Math.Abs(variance) >= CurrencyTolerance && string.IsNullOrWhiteSpace(reason))
        {
            throw new InvalidOperationException("A reason is required for every non-zero till variance.");
        }

        // A count returned by the reviewer may be resubmitted. Replacing the child rows while the
        // session is Open preserves a single unambiguous denomination snapshot for each review.
        _context.CashierTillCountLines.RemoveRange(session.CountLines);
        session.CountLines.Clear();
        foreach (var line in normalizedLines)
        {
            var countLine = new CashierTillCountLine
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                CashierTillSessionId = session.Id,
                Denomination = line.Denomination,
                Quantity = line.Quantity,
                LineAmount = line.LineAmount,
                CreatedAt = cutoff,
                CreatedBy = UserName,
                CreatedById = UserId
            };
            // Add explicitly because the application assigns GUIDs before persistence. Merely
            // attaching a pre-keyed child to a tracked collection can be classified as Modified,
            // causing an UPDATE against a denomination row that does not yet exist.
            _context.CashierTillCountLines.Add(countLine);
        }

        session.ActivityCutoffAt = cutoff;
        session.TransactionMovementAmount = activity.TransactionMovementAmount;
        session.DepositedAmount = activity.DepositedAmount;
        session.ExpectedClosingAmount = activity.ExpectedClosingAmount;
        session.CountedClosingAmount = counted;
        session.VarianceAmount = variance;
        session.VarianceApprovalThresholdAmount = threshold;
        // Compact activity queries intentionally omit the detailed entry payload. Persist the
        // separately calculated count so the frozen close snapshot remains complete.
        session.CustodyEntryCount = activity.EntryCount;
        session.VarianceReason = reason;
        session.ClosingEvidenceFileId = dto.ClosingEvidenceFileId;
        session.Status = CashierTillSessionStatus.PendingReview;
        session.SubmittedAt = cutoff;
        session.SubmittedById = UserId;
        session.ReviewedAt = null;
        session.ReviewedById = null;
        session.ReviewComments = null;
        StampModified(session, cutoff);
        await _context.SaveChangesAsync(cancellationToken);
        await RecordAuditAsync("Finance.CashTill.CountSubmitted", session, null, new
        {
            session.TransactionMovementAmount,
            session.DepositedAmount,
            session.ExpectedClosingAmount,
            session.CountedClosingAmount,
            session.VarianceAmount,
            session.VarianceApprovalThresholdAmount,
            session.CustodyEntryCount,
            denominationLineCount = normalizedLines.Count
        }, cancellationToken);
        return await GetRequiredSessionDtoAsync(id, cancellationToken);
    }

    public async Task<CashierTillSessionDto> ApproveClosureAsync(
        Guid id,
        ReviewCashierTillSessionDto dto,
        CancellationToken cancellationToken = default)
    {
        var session = await LoadForActionAsync(id, cancellationToken);
        if (session.Status != CashierTillSessionStatus.PendingReview)
        {
            throw new InvalidOperationException("Only a submitted till count can be approved.");
        }
        SetRowVersion(session, dto.RowVersion);
        // A till closure is always a maker-checker decision. The historical tenant switch cannot
        // weaken this custody boundary: permission to review is not authority to approve cash held
        // and counted by the same user.
        if (session.CashierUserId == UserId)
        {
            throw new UnauthorizedAccessException("The cashier cannot approve their own till closure.");
        }
        var comments = Clean(dto.Comments);
        if (Math.Abs(session.VarianceAmount) > session.VarianceApprovalThresholdAmount &&
            string.IsNullOrWhiteSpace(comments))
        {
            throw new InvalidOperationException("Reviewer comments are required when the variance exceeds the tenant threshold.");
        }

        var now = DateTime.UtcNow;
        session.Status = CashierTillSessionStatus.Closed;
        session.ReviewedAt = now;
        session.ReviewedById = UserId;
        session.ReviewComments = comments;
        session.ClosedAt = now;
        StampModified(session, now);
        await _context.SaveChangesAsync(cancellationToken);
        await RecordAuditAsync("Finance.CashTill.ClosureApproved", session, null, new
        {
            session.ExpectedClosingAmount,
            session.CountedClosingAmount,
            session.VarianceAmount,
            thresholdExceeded = Math.Abs(session.VarianceAmount) > session.VarianceApprovalThresholdAmount,
            comments
        }, cancellationToken);
        return await GetRequiredSessionDtoAsync(id, cancellationToken);
    }

    public async Task<CashierTillSessionDto> ReturnForRecountAsync(
        Guid id,
        ReviewCashierTillSessionDto dto,
        CancellationToken cancellationToken = default)
    {
        var session = await LoadForActionAsync(id, cancellationToken);
        if (session.Status != CashierTillSessionStatus.PendingReview)
        {
            throw new InvalidOperationException("Only a submitted till count can be returned for recount.");
        }
        SetRowVersion(session, dto.RowVersion);
        if (session.CashierUserId == UserId)
        {
            throw new UnauthorizedAccessException("The cashier cannot review their own till count.");
        }
        var comments = RequireText(dto.Comments, "Return comments", 1000);
        var now = DateTime.UtcNow;
        session.Status = CashierTillSessionStatus.Open;
        session.ActivityCutoffAt = null;
        session.SubmittedAt = null;
        session.SubmittedById = null;
        session.ReviewedAt = now;
        session.ReviewedById = UserId;
        session.ReviewComments = comments;
        StampModified(session, now);
        await _context.SaveChangesAsync(cancellationToken);
        await RecordAuditAsync("Finance.CashTill.ReturnedForRecount", session, null, new { comments }, cancellationToken);
        return await GetRequiredSessionDtoAsync(id, cancellationToken);
    }

    public async Task<CashierTillSessionDto> ReopenAsCorrectionAsync(
        Guid id,
        ReopenCashierTillSessionDto dto,
        CancellationToken cancellationToken = default)
    {
        var source = await LoadForActionAsync(id, cancellationToken);
        if (source.Status != CashierTillSessionStatus.Closed)
        {
            throw new InvalidOperationException("Only a closed till session can be reopened as a correction.");
        }
        SetRowVersion(source, dto.RowVersion);
        var reason = RequireText(dto.Reason, "Correction reason", 1000);
        if (reason.Length < MinimumCorrectionReasonLength)
        {
            throw new InvalidOperationException($"Correction reason must contain at least {MinimumCorrectionReasonLength} characters.");
        }
        await ValidateEvidenceAsync(dto.OpeningEvidenceFileId, "correction opening", cancellationToken);

        // A historical session cannot be reopened underneath later custody. Doing so would make
        // physical ownership intervals overlap and render the expected-cash calculation ambiguous.
        var hasLaterOrActiveSession = await _context.CashierTillSessions.AnyAsync(
            item => item.TenantId == TenantId &&
                    item.LiquidityAccountId == source.LiquidityAccountId &&
                    item.Id != source.Id &&
                    (item.Status == CashierTillSessionStatus.Open ||
                     item.Status == CashierTillSessionStatus.PendingReview ||
                     (item.Status == CashierTillSessionStatus.Closed && item.OpenedAt > source.OpenedAt)),
            cancellationToken);
        if (hasLaterOrActiveSession)
        {
            throw new InvalidOperationException("This closed session cannot be corrected after a later or active custody session exists.");
        }

        var openDto = new OpenCashierTillSessionDto
        {
            LiquidityAccountId = source.LiquidityAccountId,
            BusinessDate = source.BusinessDate,
            OpeningFloatAmount = source.CountedClosingAmount,
            OpeningNotes = $"Correction of {source.SessionNumber}: {reason}",
            OpeningEvidenceFileId = dto.OpeningEvidenceFileId
        };
        var correction = await OpenSessionAsync(openDto, cancellationToken);
        var correctionEntity = await LoadForActionAsync(correction.Id, cancellationToken);
        correctionEntity.CorrectsSessionId = source.Id;
        correctionEntity.CorrectionReason = reason;
        StampModified(correctionEntity, DateTime.UtcNow);
        await _context.SaveChangesAsync(cancellationToken);
        await RecordAuditAsync("Finance.CashTill.CorrectionOpened", correctionEntity, null, new
        {
            correctsSessionId = source.Id,
            source.SessionNumber,
            reason
        }, cancellationToken);
        return await GetRequiredSessionDtoAsync(correctionEntity.Id, cancellationToken);
    }

    private async Task<CashierTillSessionDto> GetRequiredSessionDtoAsync(
        Guid id,
        CancellationToken cancellationToken)
        => await GetSessionAsync(id, cancellationToken)
           ?? throw new InvalidOperationException("Till session disappeared after the operation completed.");

    private IQueryable<CashierTillSession> SessionQuery()
        => _context.CashierTillSessions
            .AsNoTracking()
            .Include(item => item.LiquidityAccount)
            .Include(item => item.CountLines)
            .AsSplitQuery();

    private async Task<CashierTillSession> LoadForActionAsync(Guid id, CancellationToken cancellationToken)
        => await _context.CashierTillSessions
               .Include(item => item.LiquidityAccount)
               .Include(item => item.CountLines)
               .FirstOrDefaultAsync(item => item.TenantId == TenantId && item.Id == id, cancellationToken)
           ?? throw new KeyNotFoundException("Cashier till session was not found.");

    private async Task<CashierTillSessionDto> MapAsync(
        CashierTillSession session,
        bool includeCustodyEntries,
        CancellationToken cancellationToken,
        TillActivity? precomputedActivity = null)
    {
        var cutoff = session.ActivityCutoffAt ?? DateTime.UtcNow;
        var activity = precomputedActivity ?? (session.Status == CashierTillSessionStatus.Open
            ? await CalculateActivityAsync(session, cutoff, includeCustodyEntries, cancellationToken)
            : includeCustodyEntries
                ? await CalculateActivityAsync(session, cutoff, includeEntries: true, cancellationToken)
                : new TillActivity(
                    session.TransactionMovementAmount,
                    session.DepositedAmount,
                    session.ExpectedClosingAmount,
                    session.CustodyEntryCount,
                    0,
                    Array.Empty<LiquidityAccountEntry>()));
        var transactionMovement = session.Status == CashierTillSessionStatus.Open
            ? activity.TransactionMovementAmount
            : session.TransactionMovementAmount;
        var depositedAmount = session.Status == CashierTillSessionStatus.Open
            ? activity.DepositedAmount
            : session.DepositedAmount;
        var expected = session.Status == CashierTillSessionStatus.Open
            ? activity.ExpectedClosingAmount
            : session.ExpectedClosingAmount;
        var openingDetailsLockReason = GetOpeningDetailsLockReason(session, activity);

        return new CashierTillSessionDto
        {
            Id = session.Id,
            SessionNumber = session.SessionNumber,
            LiquidityAccountId = session.LiquidityAccountId,
            TillCode = session.LiquidityAccount.Code,
            TillName = session.LiquidityAccount.Name,
            BusinessDate = session.BusinessDate,
            Currency = session.Currency,
            CashierUserId = session.CashierUserId,
            CashierName = session.CashierName,
            Status = session.Status,
            OpeningFloatAmount = session.OpeningFloatAmount,
            OpeningNotes = session.OpeningNotes,
            OpeningEvidenceFileId = session.OpeningEvidenceFileId,
            OpenedAt = session.OpenedAt,
            ActivityCutoffAt = session.ActivityCutoffAt,
            TransactionMovementAmount = transactionMovement,
            DepositedAmount = depositedAmount,
            ExpectedClosingAmount = expected,
            CountedClosingAmount = session.CountedClosingAmount,
            VarianceAmount = session.Status == CashierTillSessionStatus.Open
                ? 0m
                : session.VarianceAmount,
            VarianceApprovalThresholdAmount = session.VarianceApprovalThresholdAmount,
            VarianceExceedsThreshold = Math.Abs(session.VarianceAmount) > session.VarianceApprovalThresholdAmount,
            CustodyEntryCount = session.Status == CashierTillSessionStatus.Open
                ? activity.EntryCount
                : session.CustodyEntryCount,
            VarianceReason = session.VarianceReason,
            ClosingEvidenceFileId = session.ClosingEvidenceFileId,
            SubmittedAt = session.SubmittedAt,
            SubmittedById = session.SubmittedById,
            ReviewedAt = session.ReviewedAt,
            ReviewedById = session.ReviewedById,
            ReviewComments = session.ReviewComments,
            ClosedAt = session.ClosedAt,
            CancelledAt = session.CancelledAt,
            CancelledById = session.CancelledById,
            CancellationReason = session.CancellationReason,
            OpeningDetailsMutable = openingDetailsLockReason == null,
            OpeningDetailsLockReason = openingDetailsLockReason,
            CorrectsSessionId = session.CorrectsSessionId,
            CorrectionReason = session.CorrectionReason,
            CountLines = session.CountLines
                .OrderByDescending(item => item.Denomination)
                .Select(item => new CashierTillCountLineDto
                {
                    Id = item.Id,
                    Denomination = item.Denomination,
                    Quantity = item.Quantity,
                    LineAmount = item.LineAmount
                })
                .ToArray(),
            CustodyEntries = activity.Entries
                .OrderBy(item => item.CreatedAt)
                .Select(item => new CashierTillCustodyEntryDto
                {
                    Id = item.Id,
                    EntryNumber = item.EntryNumber,
                    RecordedAt = item.CreatedAt,
                    EntryDate = item.EntryDate,
                    EntryType = item.EntryType,
                    Direction = item.Direction,
                    SignedAmount = item.Direction == LiquidityEntryDirection.Increase ? item.Amount : -item.Amount,
                    SourceDocumentType = item.SourceDocumentType,
                    SourceDocumentId = item.SourceDocumentId,
                    ReferenceNumber = item.ReferenceNumber,
                    Description = item.Description
                })
                .ToArray(),
            // SQL Server always supplies rowversion bytes. The deterministic non-relational token
            // keeps service tests able to exercise the command contract without pretending that
            // EF's in-memory provider implements database-generated rowversion semantics.
            RowVersion = Convert.ToBase64String(
                session.RowVersion.Length == 0 && !_context.Database.IsRelational()
                    ? [0]
                    : session.RowVersion)
        };
    }

    private async Task<IReadOnlyDictionary<Guid, TillActivity>> LoadLiveActivitiesAsync(
        IReadOnlyCollection<CashierTillSession> sessions,
        CancellationToken cancellationToken)
    {
        var openSessions = sessions
            .Where(item => item.Status == CashierTillSessionStatus.Open)
            .ToArray();
        if (openSessions.Length == 0)
        {
            return new Dictionary<Guid, TillActivity>();
        }

        var tenantId = TenantId;
        var now = DateTime.UtcNow;
        var earliestOpening = openSessions.Min(item => item.OpenedAt);
        var latestCutoff = openSessions.Max(item => item.ActivityCutoffAt ?? now);
        var liquidityAccountIds = openSessions
            .Select(item => item.LiquidityAccountId)
            .Distinct()
            .ToArray();

        // Load the operational window twice (entries and posted deposit allocations), then
        // calculate every open session in memory. The previous implementation issued both
        // queries once per session, so a 500-row dashboard could generate 1,000 SQL calls.
        var entries = await _context.LiquidityAccountEntries
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId &&
                           liquidityAccountIds.Contains(item.LiquidityAccountId) &&
                           item.CreatedAt >= earliestOpening &&
                           item.CreatedAt <= latestCutoff)
            .ToListAsync(cancellationToken);

        var postedAllocations = await _context.BankDepositAllocations
            .AsNoTracking()
            .Where(allocation => allocation.TenantId == tenantId &&
                                 liquidityAccountIds.Contains(allocation.LiquidityAccountEntry.LiquidityAccountId) &&
                                 allocation.BankDepositBatch.Status == BankDepositStatus.Posted &&
                                 allocation.BankDepositBatch.PostedAt >= earliestOpening &&
                                 allocation.BankDepositBatch.PostedAt <= latestCutoff)
            .Select(allocation => new TillDepositActivity(
                allocation.LiquidityAccountEntry.LiquidityAccountId,
                allocation.BankDepositBatch.PostedAt!.Value,
                allocation.AllocationType,
                allocation.Amount))
            .ToListAsync(cancellationToken);

        return openSessions.ToDictionary(
            session => session.Id,
            session =>
            {
                var cutoff = session.ActivityCutoffAt ?? now;
                var sessionEntries = entries
                    .Where(item => item.LiquidityAccountId == session.LiquidityAccountId &&
                                   item.CreatedAt >= session.OpenedAt &&
                                   item.CreatedAt <= cutoff)
                    .ToArray();
                var movement = RoundMoney(sessionEntries.Sum(item =>
                    item.Direction == LiquidityEntryDirection.Increase ? item.Amount : -item.Amount));
                var deposited = RoundMoney(postedAllocations
                    .Where(item => item.LiquidityAccountId == session.LiquidityAccountId &&
                                   item.PostedAt >= session.OpenedAt &&
                                   item.PostedAt <= cutoff)
                    .Sum(item => item.AllocationType == BankDepositAllocationType.Receipt
                        ? item.Amount
                        : -item.Amount));

                return new TillActivity(
                    movement,
                    deposited,
                    RoundMoney(session.OpeningFloatAmount + movement - deposited),
                    sessionEntries.Length,
                    postedAllocations.Count(item =>
                        item.LiquidityAccountId == session.LiquidityAccountId &&
                        item.PostedAt >= session.OpenedAt &&
                        item.PostedAt <= cutoff),
                    Array.Empty<LiquidityAccountEntry>());
            });
    }

    private async Task<TillActivity> CalculateActivityAsync(
        CashierTillSession session,
        DateTime cutoff,
        bool includeEntries,
        CancellationToken cancellationToken)
    {
        var entryQuery = _context.LiquidityAccountEntries
            .AsNoTracking()
            .Where(item => item.TenantId == TenantId &&
                           item.LiquidityAccountId == session.LiquidityAccountId &&
                           item.CreatedAt >= session.OpenedAt &&
                           item.CreatedAt <= cutoff);
        var entries = await entryQuery.OrderBy(item => item.CreatedAt).ToListAsync(cancellationToken);
        var movement = RoundMoney(entries.Sum(item =>
            item.Direction == LiquidityEntryDirection.Increase ? item.Amount : -item.Amount));

        // Only a posted deposit represents physical cash leaving the till. Draft/submitted
        // allocations are reservations and therefore must not reduce the cashier's expected count.
        var postedAllocations = await _context.BankDepositAllocations
            .AsNoTracking()
            .Where(allocation => allocation.TenantId == TenantId &&
                                 allocation.LiquidityAccountEntry.LiquidityAccountId == session.LiquidityAccountId &&
                                 allocation.BankDepositBatch.Status == BankDepositStatus.Posted &&
                                 allocation.BankDepositBatch.PostedAt >= session.OpenedAt &&
                                 allocation.BankDepositBatch.PostedAt <= cutoff)
            .Select(allocation => new { allocation.AllocationType, allocation.Amount })
            .ToListAsync(cancellationToken);
        var deposited = RoundMoney(postedAllocations.Sum(item =>
            item.AllocationType == BankDepositAllocationType.Receipt ? item.Amount : -item.Amount));
        var expected = RoundMoney(session.OpeningFloatAmount + movement - deposited);

        // Balance calculation must inspect every custody entry even for a compact list response.
        // Return the count separately, then omit the entry payload unless the caller explicitly
        // requested session details; this keeps list responses small without presenting stale data.
        return new TillActivity(
            movement,
            deposited,
            expected,
            entries.Count,
            postedAllocations.Count,
            includeEntries ? entries : Array.Empty<LiquidityAccountEntry>());
    }

    private static string? GetOpeningDetailsLockReason(CashierTillSession session, TillActivity activity)
    {
        if (session.Status != CashierTillSessionStatus.Open)
        {
            return "Only an open till session can be edited or cancelled.";
        }
        if (session.CorrectsSessionId.HasValue)
        {
            return "A linked correction session cannot have its opening evidence edited or cancelled.";
        }
        if (session.CountLines.Count > 0 ||
            session.ActivityCutoffAt.HasValue ||
            session.SubmittedAt.HasValue ||
            session.SubmittedById.HasValue ||
            session.ClosingEvidenceFileId.HasValue ||
            session.ReviewedAt.HasValue ||
            session.ReviewedById.HasValue ||
            session.ClosedAt.HasValue)
        {
            return "This till session already contains count, submission, or review evidence and is immutable.";
        }
        if (activity.EntryCount > 0 || activity.DepositAllocationCount > 0)
        {
            return "This till session already has canonical custody activity and is immutable.";
        }
        return null;
    }

    private void EnsureOwner(CashierTillSession session, string action)
    {
        if (session.CashierUserId != UserId)
        {
            throw new UnauthorizedAccessException($"Only the cashier holding this till can {action}.");
        }
    }

    private async Task<FinanceSettings> GetSettingsAsync(CancellationToken cancellationToken)
        => await _context.FinanceSettings.FirstOrDefaultAsync(
               item => item.TenantId == TenantId,
               cancellationToken)
           ?? throw new InvalidOperationException("Finance settings have not been configured for this tenant.");

    private async Task ValidateEvidenceAsync(
        Guid? fileId,
        string label,
        CancellationToken cancellationToken)
    {
        if (!fileId.HasValue)
        {
            return;
        }
        var file = await _context.FileUploadRecords.FirstOrDefaultAsync(
            item => item.TenantId == TenantId && item.Id == fileId.Value,
            cancellationToken)
            ?? throw new InvalidOperationException($"Till {label} evidence was not found for this tenant.");
        if (file.VirusScanStatus == FileVirusScanStatus.Infected)
        {
            throw new InvalidOperationException($"Infected till {label} evidence cannot be linked.");
        }
    }

    private static IReadOnlyList<NormalizedCountLine> NormalizeCountLines(
        IReadOnlyList<CashierTillCountLineInputDto>? lines)
    {
        if (lines == null || lines.Count == 0)
        {
            throw new InvalidOperationException("At least one denomination count line is required.");
        }
        if (lines.Count > 100)
        {
            throw new InvalidOperationException("A till count cannot contain more than 100 denomination lines.");
        }
        if (lines.Any(item => item.Denomination <= 0m))
        {
            throw new InvalidOperationException("Every denomination must be greater than zero.");
        }
        if (lines.Any(item => item.Quantity < 0))
        {
            throw new InvalidOperationException("Denomination quantity cannot be negative.");
        }
        if (lines.GroupBy(item => RoundMoney(item.Denomination)).Any(group => group.Count() > 1))
        {
            throw new InvalidOperationException("Each denomination can appear only once in a till count.");
        }
        return lines
            .Select(item => new NormalizedCountLine(
                RoundMoney(item.Denomination),
                item.Quantity,
                RoundMoney(item.Denomination * item.Quantity)))
            .OrderByDescending(item => item.Denomination)
            .ToArray();
    }

    private void SetRowVersion(CashierTillSession session, string rowVersion)
    {
        if (string.IsNullOrWhiteSpace(rowVersion))
        {
            throw new InvalidOperationException("Row version is required.");
        }
        try
        {
            var suppliedVersion = Convert.FromBase64String(rowVersion);
            if (_context.Database.IsRelational())
            {
                // SQL Server compares this original rowversion in the UPDATE predicate and raises
                // DbUpdateConcurrencyException if another cashier/reviewer changed the session.
                _context.Entry(session).Property(item => item.RowVersion).OriginalValue = suppliedVersion;
            }
            // EF's in-memory provider neither generates nor persists SQL Server rowversion
            // correctly. Parsing still verifies the API contract, while concurrency behavior is
            // covered by relational integration; never assign OriginalValue for this provider.
        }
        catch (FormatException)
        {
            throw new InvalidOperationException("Row version is invalid.");
        }
    }

    private void StampModified(BaseEntity entity, DateTime now)
    {
        entity.UpdatedAt = now;
        entity.UpdatedBy = UserName;
        entity.LastModifiedById = UserId;
    }

    private async Task RecordAuditAsync(
        string eventType,
        CashierTillSession session,
        object? before,
        object? after,
        CancellationToken cancellationToken)
    {
        if (_audit == null)
        {
            return;
        }
        await _audit.RecordAsync(new FinanceAuditEventDto
        {
            TenantId = TenantId,
            EventType = eventType,
            SourceModule = "CashBank",
            SourceDocumentType = nameof(CashierTillSession),
            SourceDocumentId = session.Id,
            Resource = "Finance.CashTillSession",
            ResourceId = session.Id.ToString(),
            BeforeValues = before,
            AfterValues = after
        }, cancellationToken);
    }

    private static decimal RoundMoney(decimal value)
        => decimal.Round(value, 2, MidpointRounding.AwayFromZero);

    private static string? Clean(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string RequireText(string? value, string label, int maximumLength)
    {
        var cleaned = Clean(value);
        if (string.IsNullOrWhiteSpace(cleaned))
        {
            throw new InvalidOperationException($"{label} is required.");
        }
        if (cleaned.Length > maximumLength)
        {
            throw new InvalidOperationException($"{label} cannot exceed {maximumLength} characters.");
        }
        return cleaned;
    }

    private sealed record NormalizedCountLine(decimal Denomination, int Quantity, decimal LineAmount);

    private sealed record TillActivity(
        decimal TransactionMovementAmount,
        decimal DepositedAmount,
        decimal ExpectedClosingAmount,
        int EntryCount,
        int DepositAllocationCount,
        IReadOnlyList<LiquidityAccountEntry> Entries);

    private sealed record TillDepositActivity(
        Guid LiquidityAccountId,
        DateTime PostedAt,
        BankDepositAllocationType AllocationType,
        decimal Amount);
}
