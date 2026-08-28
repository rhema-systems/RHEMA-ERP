using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Data;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.Finance.Budget;

/// <summary>
/// Module-neutral Finance budget commitment boundary. Producer modules own their source
/// workflows and call this service; they never mutate Finance budget or reservation tables.
/// </summary>
public sealed class FinanceBudgetCommitmentService : IFinanceBudgetCommitmentService
{
    internal const string ReservedStatus = "Reserved";
    internal const string ReleasedStatus = "Released";
    internal const string ConsumedStatus = "Consumed";

    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IExchangeRateService _exchangeRates;

    public FinanceBudgetCommitmentService(
        ApplicationDbContext db,
        ICurrentUserService currentUser,
        IExchangeRateService exchangeRates)
    {
        _db = db;
        _currentUser = currentUser;
        _exchangeRates = exchangeRates;
    }

    private Guid TenantId => _currentUser.GetRequiredFinanceTenantId();
    private Guid UserId => Guid.TryParse(_currentUser.UserId, out var id) && id != Guid.Empty
        ? id
        : throw new UnauthorizedAccessException("An authenticated actor is required for Finance budget commitments.");

    public async Task<IReadOnlyList<FinanceBudgetCellDto>> GetEligibleBudgetCellsAsync(
        FinanceBudgetCellQueryDto query,
        CancellationToken cancellationToken = default)
    {
        var tenantId = TenantId;
        var date = RequireDate(query.BudgetDate);
        var period = await ResolvePeriodAsync(tenantId, date, query.FiscalPeriodId, cancellationToken);
        var scenario = await ResolveOfficialScenarioAsync(tenantId, period.FiscalYearId, date, cancellationToken);
        if (scenario is null)
            return Array.Empty<FinanceBudgetCellDto>();

        var entries = await _db.BudgetEntries.AsNoTracking()
            .Include(x => x.Account)
            .Include(x => x.FiscalPeriod)
            .Include(x => x.BudgetReturn).ThenInclude(x => x!.SegmentValue)
            .Include(x => x.FinanceDimensionSet).ThenInclude(x => x!.Items)
                .ThenInclude(x => x.FinanceDimensionDefinition)
            .Include(x => x.FinanceDimensionSet).ThenInclude(x => x!.Items)
                .ThenInclude(x => x.FinanceDimensionValue)
            .Where(x => x.TenantId == tenantId && !x.IsDeleted
                && x.FiscalPeriodId == period.Id
                && x.BudgetReturn!.TenantId == tenantId && !x.BudgetReturn.IsDeleted
                && x.BudgetReturn.BudgetScenarioId == scenario.Id
                && x.BudgetReturn.Status == "Approved"
                && x.Account!.TenantId == tenantId && !x.Account.IsDeleted
                && x.Account.IsActive && x.Account.BudgetTrackingEnabled
                && x.Account.AccountType == AccountType.Expense)
            .Where(x => !query.AccountId.HasValue || x.AccountId == query.AccountId.Value)
            .Where(x => !query.SegmentValueId.HasValue || x.BudgetReturn!.SegmentValueId == query.SegmentValueId.Value)
            .OrderBy(x => x.Account!.AccountNumber)
            .ThenBy(x => x.BudgetReturn!.SegmentValueId)
            .ToListAsync(cancellationToken);

        var result = new List<FinanceBudgetCellDto>(entries.Count);
        foreach (var entry in entries)
        {
            if (!await BudgetReturnMatchesAccountAsync(entry, date, cancellationToken))
                continue;
            if (!EntryContainsAssignments(entry, query.DimensionAssignments))
                continue;
            result.Add(await BuildPositionAsync(entry, scenario, date, null, null, null, cancellationToken));
        }
        return result;
    }

    public async Task<FinanceBudgetPositionDto> GetBudgetPositionAsync(
        Guid budgetEntryId,
        DateTime asOfDate,
        CancellationToken cancellationToken = default)
    {
        var position = await GetPositionCoreAsync(
            TenantId, budgetEntryId, RequireDate(asOfDate), null, null, null, cancellationToken);
        return CopyPosition(position);
    }

    public Task<FinanceBudgetCommitmentEvaluationDto> EvaluateAsync(
        FinanceBudgetCommitmentRequestDto request,
        CancellationToken cancellationToken = default) =>
        EvaluateCoreAsync(TenantId, request, cancellationToken);

    public async Task<FinanceBudgetCommitmentResultDto> ReserveAsync(
        FinanceBudgetCommitmentRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var tenantId = TenantId;
        var actor = UserId;
        ValidateMutationHeader(request.IdempotencyKey, request.CorrelationId);
        var requestHash = RequestHash(request);
        if (_db.Database.CurrentTransaction is not null)
            return await ReserveWithinTransactionAsync(tenantId, actor, request, requestHash, cancellationToken);

        var strategy = _db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = _db.Database.IsRelational()
                ? await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken)
                : null;
            try
            {
                var result = await ReserveWithinTransactionAsync(
                    tenantId, actor, request, requestHash, cancellationToken);
                if (transaction is not null)
                    await transaction.CommitAsync(cancellationToken);
                return result;
            }
            catch
            {
                if (transaction is not null)
                    await transaction.RollbackAsync(cancellationToken);
                _db.ChangeTracker.Clear();
                throw;
            }
        });
    }

    private async Task<FinanceBudgetCommitmentResultDto> ReserveWithinTransactionAsync(
        Guid tenantId,
        Guid actor,
        FinanceBudgetCommitmentRequestDto request,
        string requestHash,
        CancellationToken cancellationToken)
    {
        await AcquireReservationLockAsync(tenantId, cancellationToken);
        var replay = await FindReplayAsync(tenantId, request.IdempotencyKey, requestHash, cancellationToken);
        if (replay is not null)
            return await MapReserveReplayAsync(replay, cancellationToken);

        var evaluation = await EvaluateCoreAsync(tenantId, request, cancellationToken);
        EnsureAllowed(evaluation);
        var existing = await _db.FinanceBudgetReservations
            .Where(x => x.TenantId == tenantId && !x.IsDeleted
                && x.SourceDocumentType == Normalize(request.SourceDocumentType, 50, "source document type")
                && x.SourceDocumentId == request.SourceDocumentId
                && x.Status == ReservedStatus)
            .ToListAsync(cancellationToken);

        var now = DateTime.UtcNow;
        List<FinanceBudgetReservation> reservations;
        if (existing.Count > 0)
        {
            EnsureExistingMatches(existing, evaluation, request);
            reservations = existing;
        }
        else
        {
            reservations = evaluation.Lines.Select(line => new FinanceBudgetReservation
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                BudgetScenarioId = line.BudgetScenarioId,
                BudgetReturnId = line.BudgetReturnId,
                BudgetEntryId = line.BudgetEntryId,
                AccountId = line.AccountId,
                FiscalPeriodId = line.FiscalPeriodId,
                SegmentValueId = line.SegmentValueId,
                FinanceDimensionSetId = line.FinanceDimensionSetId,
                DimensionCombinationHashSnapshot = line.DimensionCombinationHash,
                CurrencyCode = evaluation.FunctionalCurrencyCode,
                SourceDocumentType = Normalize(request.SourceDocumentType, 50, "source document type"),
                SourceDocumentId = request.SourceDocumentId,
                SourceDocumentReference = Normalize(request.SourceDocumentReference, 100, "source document reference"),
                SourceVersion = Normalize(request.SourceVersion, 64, "source version"),
                BudgetDate = request.BudgetDate.Date,
                SourceLineIdsJson = JsonSerializer.Serialize(line.SourceLineIds),
                TransactionCurrencyCode = line.TransactionCurrencyCode,
                TransactionAmount = line.TransactionAmount,
                ExchangeRateId = line.ExchangeRateId,
                ExchangeRate = line.FunctionalConversionRate,
                ReservationVersion = 1,
                ReservedAmount = line.RequestedFunctionalAmount,
                BudgetAmountSnapshot = line.BudgetAmount,
                PostedActualSnapshot = line.PostedActualAmount,
                OtherReservationsSnapshot = line.OtherReservationsAmount,
                AvailableBeforeReservationSnapshot = line.AvailableAmount,
                Status = ReservedStatus,
                EvaluationHash = evaluation.EvaluationHash,
                ReservedByUserId = actor,
                ReservedAt = now,
                CreatedAt = now,
                CreatedById = actor,
                CreatedBy = _currentUser.UserName
            }).ToList();
            _db.FinanceBudgetReservations.AddRange(reservations);
        }

        _db.FinanceBudgetReservationOperations.Add(NewOperation(
            tenantId, null, reservations.Select(x => x.Id), "Reserve", request.IdempotencyKey,
            requestHash, existing.Count > 0 ? ReservedStatus : "None", ReservedStatus,
            existing.Sum(x => x.ReservedAmount), reservations.Sum(x => x.ReservedAmount),
            request.CorrelationId, actor, now));
        await _db.SaveChangesAsync(cancellationToken);
        return new FinanceBudgetCommitmentResultDto
        {
            IdempotentReplay = existing.Count > 0,
            EvaluationHash = evaluation.EvaluationHash,
            Reservations = reservations.Select(x => MapReservation(x, existing.Count > 0)).ToList()
        };
    }

    public Task<FinanceBudgetReservationDto> SetReservationAmountAsync(
        Guid reservationId,
        SetFinanceBudgetReservationAmountDto request,
        CancellationToken cancellationToken = default)
    {
        if (request.DesiredTransactionAmount <= 0m)
            throw Validation("BUDGET_DESIRED_AMOUNT_INVALID", "Desired reservation amount must be positive; use Release for zero exposure.");
        var payload = Hash(FormattableString.Invariant(
            $"SET|{reservationId:N}|{request.DesiredTransactionAmount:0.00}|{request.ExpectedVersion}|{request.SourceVersion}"));
        return MutateReservationAsync(reservationId, "SetAmount", request.IdempotencyKey, request.CorrelationId,
            payload, request.ExpectedVersion, async (reservation, now, token) =>
            {
                EnsureReserved(reservation);
                var desiredFunctional = RoundMoney(request.DesiredTransactionAmount * reservation.ExchangeRate);
                var position = await GetPositionCoreAsync(
                    reservation.TenantId, reservation.BudgetEntryId, reservation.BudgetDate,
                    null, null, reservation.Id, token);
                if (desiredFunctional > position.AvailableAmount)
                    throw Conflict("BUDGET_INSUFFICIENT",
                        $"Available Finance budget is short by {desiredFunctional - position.AvailableAmount:0.00} {position.FunctionalCurrencyCode}.");
                reservation.TransactionAmount = RoundMoney(request.DesiredTransactionAmount);
                reservation.ReservedAmount = desiredFunctional;
                reservation.SourceVersion = Normalize(request.SourceVersion, 64, "source version");
                reservation.BudgetAmountSnapshot = position.ApprovedAmount;
                reservation.PostedActualSnapshot = position.PostedActualAmount;
                reservation.OtherReservationsSnapshot = position.ReservedAmount;
                reservation.AvailableBeforeReservationSnapshot = position.AvailableAmount;
                reservation.ReservationVersion += 1;
                reservation.EvaluationHash = payload;
                reservation.UpdatedAt = now;
                reservation.LastModifiedById = UserId;
            }, cancellationToken);
    }

    public Task<FinanceBudgetReservationDto> ReleaseAsync(
        Guid reservationId,
        ReleaseFinanceBudgetReservationDto request,
        CancellationToken cancellationToken = default)
    {
        var reason = Normalize(request.Reason, 500, "release reason");
        var payload = Hash($"RELEASE|{reservationId:N}|{request.ExpectedVersion}|{reason}");
        return MutateReservationAsync(reservationId, "Release", request.IdempotencyKey, request.CorrelationId,
            payload, request.ExpectedVersion, (reservation, now, _) =>
            {
                EnsureReserved(reservation);
                reservation.Status = ReleasedStatus;
                reservation.ReleasedAt = now;
                reservation.ReleasedByUserId = UserId;
                reservation.ReleaseReason = reason;
                reservation.ReservationVersion += 1;
                reservation.UpdatedAt = now;
                reservation.LastModifiedById = UserId;
                return Task.CompletedTask;
            }, cancellationToken);
    }

    public async Task<FinanceBudgetReservationDto> ApplyPostingOutcomeAsync(
        Guid reservationId,
        ApplyFinanceBudgetPostingOutcomeDto request,
        CancellationToken cancellationToken = default)
    {
        if (request.RemainingTransactionAmount < 0m)
            throw Validation("BUDGET_REMAINING_AMOUNT_INVALID", "Remaining reservation amount cannot be negative.");
        var postingSourceType = Normalize(
            request.PostingSourceDocumentType, 100, "posting source document type");
        var postingAction = Normalize(request.PostingAction, 50, "posting action");
        if (request.PostingSourceDocumentId == Guid.Empty)
            throw Validation("BUDGET_POSTING_SOURCE_ID_REQUIRED", "A stable posting source-document ID is required.");
        var tenantId = TenantId;
        var postingEvent = await _db.FinancePostingEvents.AsNoTracking().FirstOrDefaultAsync(x =>
            x.TenantId == tenantId && x.Id == request.PostingEventId && !x.IsDeleted
            && x.PostingStatus == "Posted" && x.JournalEntryId == request.JournalEntryId
            && x.SourceDocumentType == postingSourceType
            && x.SourceDocumentId == request.PostingSourceDocumentId
            && x.PostingAction == postingAction,
            cancellationToken) ?? throw NotFound("BUDGET_POSTING_EVENT_NOT_FOUND",
            "The exact posted Finance source, action, event and journal evidence were not found in this tenant.");

        var payload = Hash(FormattableString.Invariant(
            $"POSTING|{reservationId:N}|{request.PostingEventId:N}|{request.JournalEntryId:N}|{postingSourceType}|{request.PostingSourceDocumentId:N}|{postingAction}|{request.RemainingTransactionAmount:0.00}|{request.ExpectedVersion}"));
        return await MutateReservationAsync(reservationId, "ApplyPostingOutcome", request.IdempotencyKey,
            request.CorrelationId, payload, request.ExpectedVersion, (reservation, now, _) =>
            {
                EnsureReserved(reservation);
                if (request.RemainingTransactionAmount > reservation.TransactionAmount)
                    throw Conflict("BUDGET_REMAINING_AMOUNT_EXCEEDS_RESERVATION",
                        "A posting outcome cannot increase the remaining reservation. Use SetReservationAmount with approved source evidence.");
                reservation.TransactionAmount = RoundMoney(request.RemainingTransactionAmount);
                reservation.ReservedAmount = RoundMoney(request.RemainingTransactionAmount * reservation.ExchangeRate);
                reservation.Status = request.RemainingTransactionAmount == 0m ? ConsumedStatus : ReservedStatus;
                reservation.ConsumedAt = request.RemainingTransactionAmount == 0m ? now : null;
                reservation.ConsumedByUserId = request.RemainingTransactionAmount == 0m ? UserId : null;
                reservation.PostingEventId = request.PostingEventId;
                reservation.JournalEntryId = request.JournalEntryId;
                reservation.ReservationVersion += 1;
                reservation.UpdatedAt = now;
                reservation.LastModifiedById = UserId;
                return Task.CompletedTask;
            }, cancellationToken, request.JournalEntryId, request.PostingEventId);
    }

    public async Task ConsumeForPostingAsync(
        Guid tenantId,
        string sourceDocumentType,
        Guid sourceDocumentId,
        IReadOnlyList<Guid> reservationIds,
        Guid journalEntryId,
        Guid postingEventId,
        CancellationToken cancellationToken = default)
    {
        if (tenantId != TenantId)
            throw new UnauthorizedAccessException("Finance budget reservations belong to another tenant.");
        if (sourceDocumentId == Guid.Empty || journalEntryId == Guid.Empty || postingEventId == Guid.Empty)
            throw Validation("BUDGET_POSTING_EVIDENCE_REQUIRED", "Source, journal and posting-event identities are required.");
        if (!string.Equals(
                _db.Database.ProviderName,
                "Microsoft.EntityFrameworkCore.InMemory",
                StringComparison.Ordinal) &&
            _db.Database.CurrentTransaction is null)
            throw new InvalidOperationException(
                "Finance budget reservations can be consumed only inside the central posting transaction.");
        var normalizedSourceType = Normalize(sourceDocumentType, 50, "source document type");
        var distinctIds = reservationIds.Distinct().ToArray();
        if (distinctIds.Length == 0 || distinctIds.Length != reservationIds.Count)
            throw Validation("BUDGET_RESERVATION_IDS_INVALID", "Budget reservation IDs must be non-empty and unique.");

        var reservations = await _db.FinanceBudgetReservations
            .Where(row => row.TenantId == tenantId && distinctIds.Contains(row.Id) && !row.IsDeleted)
            .ToListAsync(cancellationToken);
        if (reservations.Count != distinctIds.Length || reservations.Any(row =>
                row.SourceDocumentType != normalizedSourceType
                || row.SourceDocumentId != sourceDocumentId
                || row.Status != ReservedStatus))
            throw Conflict("BUDGET_POSTING_RESERVATION_MISMATCH",
                "Finance budget reservation evidence is missing, stale, or belongs to another source document.");

        var now = DateTime.UtcNow;
        var priorReservedAmount = reservations.Sum(reservation => reservation.ReservedAmount);
        foreach (var reservation in reservations)
        {
            reservation.Status = ConsumedStatus;
            reservation.TransactionAmount = 0m;
            reservation.ReservedAmount = 0m;
            reservation.ConsumedAt = now;
            reservation.ConsumedByUserId = UserId;
            reservation.JournalEntryId = journalEntryId;
            reservation.PostingEventId = postingEventId;
            reservation.ReservationVersion += 1;
            reservation.UpdatedAt = now;
            reservation.LastModifiedById = UserId;
        }

        var operationKey = $"POST:{postingEventId:N}:BudgetConsume";
        var operationHash = Hash(string.Join('|', new[]
        {
            normalizedSourceType,
            sourceDocumentId.ToString("N"),
            string.Join(',', distinctIds.OrderBy(id => id).Select(id => id.ToString("N"))),
            journalEntryId.ToString("N"),
            postingEventId.ToString("N")
        }));
        _db.FinanceBudgetReservationOperations.Add(NewOperation(
            tenantId,
            null,
            distinctIds,
            "ConsumeForPosting",
            operationKey,
            operationHash,
            ReservedStatus,
            ConsumedStatus,
            priorReservedAmount,
            0m,
            $"POST:{normalizedSourceType}:{sourceDocumentId:N}",
            UserId,
            now,
            journalEntryId,
            postingEventId));
    }

    private async Task<FinanceBudgetReservationDto> MutateReservationAsync(
        Guid reservationId,
        string operationType,
        string idempotencyKey,
        string correlationId,
        string payloadHash,
        int expectedVersion,
        Func<FinanceBudgetReservation, DateTime, CancellationToken, Task> mutate,
        CancellationToken cancellationToken,
        Guid? journalEntryId = null,
        Guid? postingEventId = null)
    {
        ValidateMutationHeader(idempotencyKey, correlationId);
        var tenantId = TenantId;
        var actor = UserId;
        if (_db.Database.CurrentTransaction is not null)
            return await MutateReservationWithinTransactionAsync(
                reservationId, operationType, idempotencyKey, correlationId, payloadHash,
                expectedVersion, mutate, tenantId, actor, cancellationToken, journalEntryId, postingEventId);

        var strategy = _db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = _db.Database.IsRelational()
                ? await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken)
                : null;
            try
            {
                var result = await MutateReservationWithinTransactionAsync(
                    reservationId, operationType, idempotencyKey, correlationId, payloadHash,
                    expectedVersion, mutate, tenantId, actor, cancellationToken, journalEntryId, postingEventId);
                if (transaction is not null)
                    await transaction.CommitAsync(cancellationToken);
                return result;
            }
            catch
            {
                if (transaction is not null)
                    await transaction.RollbackAsync(cancellationToken);
                _db.ChangeTracker.Clear();
                throw;
            }
        });
    }

    private async Task<FinanceBudgetReservationDto> MutateReservationWithinTransactionAsync(
        Guid reservationId,
        string operationType,
        string idempotencyKey,
        string correlationId,
        string payloadHash,
        int expectedVersion,
        Func<FinanceBudgetReservation, DateTime, CancellationToken, Task> mutate,
        Guid tenantId,
        Guid actor,
        CancellationToken cancellationToken,
        Guid? journalEntryId,
        Guid? postingEventId)
    {
        await AcquireReservationLockAsync(tenantId, cancellationToken);
        var replay = await FindReplayAsync(tenantId, idempotencyKey, payloadHash, cancellationToken);
        if (replay is not null)
        {
            var replayReservationId = replay.FinanceBudgetReservationId
                ?? ReadReservationIds(replay).Single();
            var replayReservation = await RequireReservationAsync(tenantId, replayReservationId, cancellationToken);
            return MapReservation(replayReservation, true);
        }

        var reservation = await RequireReservationAsync(tenantId, reservationId, cancellationToken);
        if (reservation.ReservationVersion != expectedVersion)
            throw Conflict("BUDGET_RESERVATION_VERSION_CONFLICT",
                "The Finance budget reservation changed. Reload its current position before retrying.");
        var priorStatus = reservation.Status;
        var priorAmount = reservation.ReservedAmount;
        var now = DateTime.UtcNow;
        await mutate(reservation, now, cancellationToken);
        _db.FinanceBudgetReservationOperations.Add(NewOperation(
            tenantId, reservation.Id, new[] { reservation.Id }, operationType, idempotencyKey,
            payloadHash, priorStatus, reservation.Status, priorAmount, reservation.ReservedAmount,
            correlationId, actor, now, journalEntryId, postingEventId));
        await _db.SaveChangesAsync(cancellationToken);
        return MapReservation(reservation, false);
    }

    private async Task<FinanceBudgetCommitmentEvaluationDto> EvaluateCoreAsync(
        Guid tenantId,
        FinanceBudgetCommitmentRequestDto request,
        CancellationToken cancellationToken)
    {
        ValidateRequest(request);
        var date = request.BudgetDate.Date;
        var functionalCurrency = await FunctionalCurrencyAsync(tenantId, cancellationToken);
        var entryIds = request.Lines.Select(x => x.BudgetEntryId).Distinct().ToArray();
        var entries = await _db.BudgetEntries.AsNoTracking()
            .Include(x => x.Account)
            .Include(x => x.FiscalPeriod)
            .Include(x => x.BudgetReturn).ThenInclude(x => x!.BudgetScenario)
            .Include(x => x.BudgetReturn).ThenInclude(x => x!.SegmentValue)
            .Include(x => x.FinanceDimensionSet).ThenInclude(x => x!.Items)
                .ThenInclude(x => x.FinanceDimensionDefinition)
            .Include(x => x.FinanceDimensionSet).ThenInclude(x => x!.Items)
                .ThenInclude(x => x.FinanceDimensionValue)
            .Where(x => x.TenantId == tenantId && entryIds.Contains(x.Id) && !x.IsDeleted)
            .ToDictionaryAsync(x => x.Id, cancellationToken);
        if (entries.Count != entryIds.Length)
            throw NotFound("BUDGET_ENTRY_NOT_FOUND", "One or more Finance budget entries were not found in this tenant.");

        var output = new List<FinanceBudgetCommitmentEvaluationLineDto>();
        foreach (var group in request.Lines.GroupBy(x => x.BudgetEntryId).OrderBy(x => x.Key))
        {
            var entry = entries[group.Key];
            await ValidateBudgetCellAsync(entry, date, cancellationToken);
            var lines = group.OrderBy(x => x.SourceLineId, StringComparer.Ordinal).ToList();
            foreach (var line in lines)
                ValidateLineMatchesCell(line, entry);
            if (lines.Select(x => NormalizeCurrency(x.TransactionCurrencyCode)).Distinct().Count() != 1
                || lines.Select(x => x.ExchangeRateId).Distinct().Count() != 1)
                throw Validation("BUDGET_CELL_CURRENCY_AMBIGUOUS",
                    "Lines aggregated into one Finance budget cell must use one transaction currency and exchange-rate record.");

            var currency = NormalizeCurrency(lines[0].TransactionCurrencyCode);
            var rate = await ResolveFunctionalRateAsync(
                tenantId, functionalCurrency, currency, lines[0].ExchangeRateId, date, cancellationToken);
            var transactionAmount = RoundMoney(lines.Sum(x => x.TransactionAmount));
            var functionalAmount = RoundMoney(transactionAmount * rate.Multiplier);
            var position = await GetPositionCoreAsync(
                tenantId, entry.Id, date,
                Normalize(request.SourceDocumentType, 50, "source document type"),
                request.SourceDocumentId, null, cancellationToken);
            var shortfall = Math.Max(0m, functionalAmount - position.AvailableAmount);
            output.Add(new FinanceBudgetCommitmentEvaluationLineDto
            {
                BudgetScenarioId = position.BudgetScenarioId,
                BudgetReturnId = position.BudgetReturnId,
                BudgetEntryId = entry.Id,
                AccountId = entry.AccountId,
                FiscalPeriodId = entry.FiscalPeriodId,
                SegmentValueId = entry.BudgetReturn!.SegmentValueId,
                FinanceDimensionSetId = entry.FinanceDimensionSetId,
                DimensionCombinationHash = entry.FinanceDimensionSet?.CombinationHash,
                DimensionAssignments = MapAssignments(entry),
                SourceLineIds = lines.Select(x => Normalize(x.SourceLineId, 100, "source line ID")).ToList(),
                TransactionCurrencyCode = currency,
                TransactionAmount = transactionAmount,
                ExchangeRateId = rate.ExchangeRateId,
                FunctionalConversionRate = rate.Multiplier,
                RequestedFunctionalAmount = functionalAmount,
                BudgetAmount = position.ApprovedAmount,
                PostedActualAmount = position.PostedActualAmount,
                OtherReservationsAmount = position.ReservedAmount,
                AvailableAmount = position.AvailableAmount,
                ShortfallAmount = shortfall,
                DecisionCode = shortfall == 0m ? "AVAILABLE" : "INSUFFICIENT_BUDGET",
                Message = shortfall == 0m
                    ? "Finance budget is available."
                    : $"Available Finance budget is short by {shortfall:0.00} {functionalCurrency}."
            });
        }

        var evaluation = new FinanceBudgetCommitmentEvaluationDto
        {
            SourceDocumentType = Normalize(request.SourceDocumentType, 50, "source document type"),
            SourceDocumentId = request.SourceDocumentId,
            FunctionalCurrencyCode = functionalCurrency,
            Lines = output,
            IsAllowed = output.Count > 0 && output.All(x => x.ShortfallAmount == 0m),
            TotalRequestedFunctionalAmount = output.Sum(x => x.RequestedFunctionalAmount),
            TotalShortfallAmount = output.Sum(x => x.ShortfallAmount)
        };
        evaluation.EvaluationHash = EvaluationHash(tenantId, request, evaluation);
        return evaluation;
    }

    private async Task<FinanceBudgetCellDto> GetPositionCoreAsync(
        Guid tenantId,
        Guid budgetEntryId,
        DateTime date,
        string? excludedSourceType,
        Guid? excludedSourceId,
        Guid? excludedReservationId,
        CancellationToken cancellationToken)
    {
        var entry = await _db.BudgetEntries.AsNoTracking()
            .Include(x => x.Account)
            .Include(x => x.FiscalPeriod)
            .Include(x => x.BudgetReturn).ThenInclude(x => x!.BudgetScenario)
            .Include(x => x.BudgetReturn).ThenInclude(x => x!.SegmentValue)
            .Include(x => x.FinanceDimensionSet).ThenInclude(x => x!.Items)
                .ThenInclude(x => x.FinanceDimensionDefinition)
            .Include(x => x.FinanceDimensionSet).ThenInclude(x => x!.Items)
                .ThenInclude(x => x.FinanceDimensionValue)
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == budgetEntryId && !x.IsDeleted, cancellationToken)
            ?? throw NotFound("BUDGET_ENTRY_NOT_FOUND", "The Finance budget entry was not found in this tenant.");
        await ValidateBudgetCellAsync(entry, date, cancellationToken);
        return await BuildPositionAsync(
            entry, entry.BudgetReturn!.BudgetScenario!, date,
            excludedSourceType, excludedSourceId, excludedReservationId, cancellationToken);
    }

    private async Task<FinanceBudgetCellDto> BuildPositionAsync(
        BudgetEntry entry,
        BudgetScenario scenario,
        DateTime date,
        string? excludedSourceType,
        Guid? excludedSourceId,
        Guid? excludedReservationId,
        CancellationToken cancellationToken)
    {
        var tenantId = entry.TenantId;
        var actualQuery = _db.AccountTransactions.AsNoTracking()
            .Where(x => x.TenantId == tenantId && !x.IsDeleted
                && x.AccountId == entry.AccountId && x.FiscalPeriodId == entry.FiscalPeriodId
                && x.JournalEntry.PostingStatus == "Posted" && !x.JournalEntry.IsDeleted);
        if (entry.FinanceDimensionSet is not null)
        {
            var requiredValueIds = entry.FinanceDimensionSet.Items
                .Select(item => item.FinanceDimensionValueId).ToArray();
            var matchingSetIds = _db.FinanceDimensionSetItems.AsNoTracking()
                .Where(item => item.TenantId == tenantId && !item.IsDeleted
                    && requiredValueIds.Contains(item.FinanceDimensionValueId))
                .GroupBy(item => item.FinanceDimensionSetId)
                .Where(group => group.Count() == requiredValueIds.Length)
                .Select(group => group.Key);
            actualQuery = actualQuery.Where(transaction =>
                transaction.FinanceDimensionSetId.HasValue
                && matchingSetIds.Contains(transaction.FinanceDimensionSetId.Value));
        }
        var actual = await actualQuery.SumAsync(
            x => x.DebitAmount - x.CreditAmount, cancellationToken);
        var reservationsQuery = _db.FinanceBudgetReservations.AsNoTracking()
            .Where(x => x.TenantId == tenantId && !x.IsDeleted
                && x.BudgetEntryId == entry.Id && x.Status == ReservedStatus);
        if (excludedSourceType is not null && excludedSourceId.HasValue)
            reservationsQuery = reservationsQuery.Where(x =>
                x.SourceDocumentType != excludedSourceType || x.SourceDocumentId != excludedSourceId.Value);
        if (excludedReservationId.HasValue)
            reservationsQuery = reservationsQuery.Where(x => x.Id != excludedReservationId.Value);
        var reserved = await reservationsQuery.SumAsync(x => x.ReservedAmount, cancellationToken);
        var functionalCurrency = await FunctionalCurrencyAsync(tenantId, cancellationToken);
        return new FinanceBudgetCellDto
        {
            BudgetScenarioId = scenario.Id,
            BudgetScenarioName = scenario.Name,
            BudgetScenarioVersion = scenario.VersionNumber,
            BudgetReturnId = entry.BudgetReturnId,
            BudgetEntryId = entry.Id,
            AccountId = entry.AccountId,
            AccountNumber = entry.Account!.AccountNumber,
            AccountName = entry.Account.AccountName,
            FiscalYearId = entry.FiscalPeriod!.FiscalYearId,
            FiscalPeriodId = entry.FiscalPeriodId,
            FiscalPeriodCode = entry.FiscalPeriod.PeriodCode,
            SegmentValueId = entry.BudgetReturn!.SegmentValueId,
            SegmentValue = entry.BudgetReturn.SegmentValue?.SegmentValue,
            FinanceDimensionSetId = entry.FinanceDimensionSetId,
            DimensionCombinationHash = entry.FinanceDimensionSet?.CombinationHash,
            DimensionAssignments = MapAssignments(entry),
            FunctionalCurrencyCode = functionalCurrency,
            ApprovedAmount = entry.AmountBase,
            PostedActualAmount = actual,
            ReservedAmount = reserved,
            AvailableAmount = entry.AmountBase - actual - reserved,
            PositionAsOfUtc = DateTime.UtcNow
        };
    }

    private async Task ValidateBudgetCellAsync(BudgetEntry entry, DateTime date, CancellationToken cancellationToken)
    {
        if (entry.Account is null || entry.Account.TenantId != entry.TenantId || entry.Account.IsDeleted
            || !entry.Account.IsActive || !entry.Account.BudgetTrackingEnabled
            || entry.Account.AccountType != AccountType.Expense)
            throw Validation("BUDGET_ACCOUNT_NOT_ELIGIBLE", "The Finance budget entry does not use an active budget-tracked expense account.");
        if (entry.FiscalPeriod is null || entry.FiscalPeriod.TenantId != entry.TenantId || entry.FiscalPeriod.IsDeleted
            || date < entry.FiscalPeriod.StartDate.Date || date > entry.FiscalPeriod.EndDate.Date)
            throw Validation("BUDGET_PERIOD_MISMATCH", "The budget date is outside the Finance budget entry fiscal period.");
        if (entry.BudgetReturn is null || entry.BudgetReturn.TenantId != entry.TenantId || entry.BudgetReturn.IsDeleted
            || entry.BudgetReturn.Status != "Approved" || entry.BudgetReturn.BudgetScenario is null)
            throw Validation("BUDGET_RETURN_NOT_APPROVED", "The Finance budget return is not approved.");
        var official = await ResolveOfficialScenarioAsync(
            entry.TenantId, entry.FiscalPeriod.FiscalYearId, date, cancellationToken);
        if (official is null || official.Id != entry.BudgetReturn.BudgetScenarioId)
            throw Validation("BUDGET_SCENARIO_NOT_EFFECTIVE", "The Finance budget entry is not in the adopted scenario effective for this date.");
        var controlDimensionIds = await _db.BudgetScenarioControlDimensions.AsNoTracking()
            .Where(item => item.TenantId == entry.TenantId && !item.IsDeleted
                && item.BudgetScenarioId == entry.BudgetReturn.BudgetScenarioId)
            .Select(item => item.FinanceDimensionDefinitionId)
            .ToListAsync(cancellationToken);
        var assignedDefinitionIds = entry.FinanceDimensionSet?.Items
            .Select(item => item.FinanceDimensionDefinitionId).ToHashSet()
            ?? new HashSet<Guid>();
        if (!assignedDefinitionIds.SetEquals(controlDimensionIds))
            throw Validation("BUDGET_DIMENSION_GRAIN_INVALID",
                "The Finance budget entry does not contain exactly the scenario's controlling dimension assignments.");
        if (!await BudgetReturnMatchesAccountAsync(entry, date, cancellationToken))
            throw Validation("BUDGET_SEGMENT_MISMATCH", "The budget return department/cost-centre does not match the account combination.");
    }

    private async Task<bool> BudgetReturnMatchesAccountAsync(
        BudgetEntry entry,
        DateTime date,
        CancellationToken cancellationToken)
    {
        var controlSegments = await _db.AccountSegmentValues.AsNoTracking()
            .Include(x => x.SegmentStructure)
            .Where(x => x.TenantId == entry.TenantId && !x.IsDeleted && x.AccountId == entry.AccountId
                && x.EffectiveDate.Date <= date
                && (!x.EndDate.HasValue || x.EndDate.Value.Date >= date))
            .Where(x => !x.SegmentStructure.IsNaturalAccount && x.SegmentStructure.IsReportingDimension)
            .ToListAsync(cancellationToken);
        controlSegments = controlSegments.Where(x => IsControllingSegment(x.SegmentStructure)).ToList();
        return entry.BudgetReturn!.SegmentValueId.HasValue
            ? controlSegments.Any(x => x.SegmentLookupValueId == entry.BudgetReturn.SegmentValueId)
            : controlSegments.Count == 0;
    }

    private async Task<FiscalPeriod> ResolvePeriodAsync(
        Guid tenantId,
        DateTime date,
        Guid? requestedPeriodId,
        CancellationToken cancellationToken)
    {
        var periods = await _db.FiscalPeriods.AsNoTracking()
            .Where(x => x.TenantId == tenantId && !x.IsDeleted
                && x.StartDate.Date <= date && x.EndDate.Date >= date
                && (!requestedPeriodId.HasValue || x.Id == requestedPeriodId.Value))
            .ToListAsync(cancellationToken);
        return periods.Count switch
        {
            1 => periods[0],
            0 => throw NotFound("BUDGET_FISCAL_PERIOD_NOT_FOUND", "No Finance fiscal period covers the budget date."),
            _ => throw Conflict("BUDGET_FISCAL_PERIOD_AMBIGUOUS", "More than one Finance fiscal period covers the budget date.")
        };
    }

    private async Task<BudgetScenario?> ResolveOfficialScenarioAsync(
        Guid tenantId,
        Guid fiscalYearId,
        DateTime date,
        CancellationToken cancellationToken) =>
        await _db.BudgetScenarios.AsNoTracking()
            .Where(x => x.TenantId == tenantId && !x.IsDeleted && x.FiscalYearId == fiscalYearId
                && x.AdoptedAt != null && x.AdoptionEffectiveDate != null
                && x.AdoptionEffectiveDate.Value.Date <= date
                && (x.Status == "Approved" || x.Status == "Superseded"))
            .OrderByDescending(x => x.AdoptionEffectiveDate)
            .ThenByDescending(x => x.VersionNumber)
            .FirstOrDefaultAsync(cancellationToken);

    private async Task<FunctionalRate> ResolveFunctionalRateAsync(
        Guid tenantId,
        string functionalCurrency,
        string transactionCurrency,
        Guid? exchangeRateId,
        DateTime date,
        CancellationToken cancellationToken)
    {
        if (transactionCurrency == functionalCurrency)
        {
            if (exchangeRateId.HasValue)
                throw Validation("BUDGET_EXCHANGE_RATE_UNEXPECTED", "Functional-currency commitments must not carry an exchange-rate ID.");
            return new FunctionalRate(null, 1m);
        }
        if (!exchangeRateId.HasValue)
            throw Validation("BUDGET_EXCHANGE_RATE_REQUIRED", "A Finance exchange-rate record is required for a foreign-currency commitment.");
        var rate = await _exchangeRates.GetExchangeRateByIdAsync(exchangeRateId.Value, cancellationToken)
            ?? throw NotFound("BUDGET_EXCHANGE_RATE_NOT_FOUND", "The Finance exchange-rate record was not found.");
        if (rate.TenantId != tenantId || !rate.IsActive
            || !string.Equals(rate.ApprovalStatus, "Approved", StringComparison.OrdinalIgnoreCase)
            || rate.EffectiveDate.Date > date || (rate.ExpiryDate.HasValue && rate.ExpiryDate.Value.Date < date))
            throw Validation("BUDGET_EXCHANGE_RATE_NOT_EFFECTIVE", "The Finance exchange-rate record is not approved and effective for the budget date.");
        var baseCode = NormalizeCurrency(rate.BaseCurrencyCode);
        var targetCode = NormalizeCurrency(rate.TargetCurrencyCode);
        decimal multiplier;
        if (baseCode == functionalCurrency && targetCode == transactionCurrency)
            multiplier = rate.InverseRate > 0m ? rate.InverseRate : rate.Rate > 0m ? 1m / rate.Rate : 0m;
        else if (baseCode == transactionCurrency && targetCode == functionalCurrency)
            multiplier = rate.Rate;
        else
            throw Validation("BUDGET_EXCHANGE_RATE_PAIR_MISMATCH", "The exchange-rate currency pair does not convert the transaction currency to Finance functional currency.");
        if (multiplier <= 0m)
            throw Validation("BUDGET_EXCHANGE_RATE_INVALID", "The Finance exchange-rate record has no positive functional-currency conversion rate.");
        return new FunctionalRate(rate.Id, decimal.Round(multiplier, 6, MidpointRounding.AwayFromZero));
    }

    private async Task<string> FunctionalCurrencyAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var currency = await _db.FinanceSettings.AsNoTracking()
            .Where(x => x.TenantId == tenantId && !x.IsDeleted)
            .Select(x => x.BaseCurrency)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw NotFound("BUDGET_FINANCE_SETTINGS_NOT_FOUND", "Finance functional-currency settings are missing.");
        return NormalizeCurrency(currency);
    }

    private async Task<FinanceBudgetReservationOperation?> FindReplayAsync(
        Guid tenantId,
        string idempotencyKey,
        string payloadHash,
        CancellationToken cancellationToken)
    {
        var key = Normalize(idempotencyKey, 100, "idempotency key");
        var existing = await _db.FinanceBudgetReservationOperations.AsNoTracking()
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && !x.IsDeleted && x.IdempotencyKey == key, cancellationToken);
        if (existing is not null && existing.PayloadHash != payloadHash)
            throw Conflict("BUDGET_IDEMPOTENCY_CONFLICT", "The idempotency key was already used with a different Finance budget payload.");
        return existing;
    }

    private async Task<FinanceBudgetCommitmentResultDto> MapReserveReplayAsync(
        FinanceBudgetReservationOperation operation,
        CancellationToken cancellationToken)
    {
        var ids = ReadReservationIds(operation);
        var rows = await _db.FinanceBudgetReservations.AsNoTracking()
            .Where(x => x.TenantId == operation.TenantId && ids.Contains(x.Id) && !x.IsDeleted)
            .ToListAsync(cancellationToken);
        if (rows.Count != ids.Count)
            throw Conflict("BUDGET_IDEMPOTENCY_EVIDENCE_INCOMPLETE", "Finance reservation evidence for the idempotent operation is incomplete.");
        return new FinanceBudgetCommitmentResultDto
        {
            IdempotentReplay = true,
            EvaluationHash = rows.Select(x => x.EvaluationHash).Distinct().Count() == 1 ? rows[0].EvaluationHash : string.Empty,
            Reservations = rows.Select(x => MapReservation(x, true)).ToList()
        };
    }

    private async Task<FinanceBudgetReservation> RequireReservationAsync(
        Guid tenantId,
        Guid reservationId,
        CancellationToken cancellationToken) =>
        await _db.FinanceBudgetReservations.FirstOrDefaultAsync(x =>
            x.TenantId == tenantId && x.Id == reservationId && !x.IsDeleted, cancellationToken)
        ?? throw NotFound("BUDGET_RESERVATION_NOT_FOUND", "The Finance budget reservation was not found in this tenant.");

    private async Task AcquireReservationLockAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        if (!_db.Database.IsSqlServer())
            return;
        var resource = $"FINANCE:BUDGET:RESERVATION:{tenantId:N}";
        await _db.Database.ExecuteSqlInterpolatedAsync($@"
DECLARE @result int;
EXEC @result = sp_getapplock @Resource={resource}, @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=15000;
IF @result < 0 THROW 51000, 'Unable to acquire Finance budget reservation lock.', 1;", cancellationToken);
    }

    private static FinanceBudgetReservationOperation NewOperation(
        Guid tenantId,
        Guid? reservationId,
        IEnumerable<Guid> reservationIds,
        string operationType,
        string idempotencyKey,
        string payloadHash,
        string priorStatus,
        string resultStatus,
        decimal priorAmount,
        decimal resultAmount,
        string correlationId,
        Guid actor,
        DateTime now,
        Guid? journalEntryId = null,
        Guid? postingEventId = null) => new()
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            FinanceBudgetReservationId = reservationId,
            ReservationIdsJson = JsonSerializer.Serialize(reservationIds.OrderBy(x => x).ToArray()),
            OperationType = Normalize(operationType, 30, "operation type"),
            IdempotencyKey = Normalize(idempotencyKey, 100, "idempotency key"),
            PayloadHash = payloadHash,
            PriorStatus = Normalize(priorStatus, 20, "prior status"),
            ResultStatus = Normalize(resultStatus, 20, "result status"),
            PriorReservedAmount = priorAmount,
            ResultReservedAmount = resultAmount,
            CorrelationId = Normalize(correlationId, 100, "correlation ID"),
            ActorUserId = actor,
            OccurredAt = now,
            JournalEntryId = journalEntryId,
            PostingEventId = postingEventId,
            CreatedAt = now,
            CreatedById = actor
        };

    private static void ValidateRequest(FinanceBudgetCommitmentRequestDto request)
    {
        _ = Normalize(request.SourceDocumentType, 50, "source document type");
        _ = Normalize(request.SourceDocumentReference, 100, "source document reference");
        _ = Normalize(request.SourceVersion, 64, "source version");
        if (request.SourceDocumentId == Guid.Empty)
            throw Validation("BUDGET_SOURCE_ID_REQUIRED", "A stable source-document ID is required.");
        _ = RequireDate(request.BudgetDate);
        if (request.Lines.Count is < 1 or > 200)
            throw Validation("BUDGET_LINES_INVALID", "A Finance budget request must contain between 1 and 200 lines.");
        var sourceLineIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var line in request.Lines)
        {
            var sourceLineId = Normalize(line.SourceLineId, 100, "source line ID");
            if (!sourceLineIds.Add(sourceLineId))
                throw Validation("BUDGET_SOURCE_LINE_DUPLICATE", "Source line IDs must be unique within a Finance budget request.");
            if (line.BudgetEntryId == Guid.Empty || line.AccountId == Guid.Empty || line.FiscalPeriodId == Guid.Empty)
                throw Validation("BUDGET_CELL_ID_REQUIRED", "Budget entry, account and fiscal-period IDs are required.");
            ValidateAssignments(line.DimensionAssignments);
            if (line.TransactionAmount <= 0m)
                throw Validation("BUDGET_LINE_AMOUNT_INVALID", "Budget commitment line amounts must be positive.");
            _ = NormalizeCurrency(line.TransactionCurrencyCode);
        }
    }

    private static void ValidateLineMatchesCell(FinanceBudgetCommitmentLineDto line, BudgetEntry entry)
    {
        if (line.AccountId != entry.AccountId || line.FiscalPeriodId != entry.FiscalPeriodId
            || line.SegmentValueId != entry.BudgetReturn!.SegmentValueId
            || !EntryMatchesAssignments(entry, line.DimensionAssignments))
            throw Validation("BUDGET_CELL_MISMATCH", "The producer line does not match the canonical Finance budget cell.");
    }

    private static void ValidateMutationHeader(string idempotencyKey, string correlationId)
    {
        _ = Normalize(idempotencyKey, 100, "idempotency key");
        _ = Normalize(correlationId, 100, "correlation ID");
    }

    private static void EnsureAllowed(FinanceBudgetCommitmentEvaluationDto evaluation)
    {
        if (!evaluation.IsAllowed)
            throw Conflict("BUDGET_INSUFFICIENT", evaluation.Lines.FirstOrDefault(x => x.ShortfallAmount > 0)?.Message
                ?? "The Finance budget commitment is not allowed.");
    }

    private static void EnsureExistingMatches(
        IReadOnlyCollection<FinanceBudgetReservation> existing,
        FinanceBudgetCommitmentEvaluationDto evaluation,
        FinanceBudgetCommitmentRequestDto request)
    {
        if (existing.Count != evaluation.Lines.Count)
            throw Conflict("BUDGET_ACTIVE_RESERVATION_CONFLICT", "The source already has a different active Finance budget reservation set.");
        foreach (var line in evaluation.Lines)
        {
            var row = existing.SingleOrDefault(x => x.BudgetEntryId == line.BudgetEntryId);
            if (row is null || row.ReservedAmount != line.RequestedFunctionalAmount
                || row.TransactionAmount != line.TransactionAmount
                || row.SourceVersion != request.SourceVersion
                || row.FinanceDimensionSetId != line.FinanceDimensionSetId
                || row.DimensionCombinationHashSnapshot != line.DimensionCombinationHash
                || row.EvaluationHash != evaluation.EvaluationHash)
                throw Conflict("BUDGET_ACTIVE_RESERVATION_CONFLICT", "The source or Finance budget position changed after its active reservation. Use the target-state adjustment operation.");
        }
    }

    private static void EnsureReserved(FinanceBudgetReservation reservation)
    {
        if (reservation.Status != ReservedStatus)
            throw Conflict("BUDGET_RESERVATION_NOT_ACTIVE", $"The Finance budget reservation is already {reservation.Status}.");
    }

    private static FinanceBudgetReservationDto MapReservation(FinanceBudgetReservation row, bool replay) => new()
    {
        Id = row.Id,
        BudgetEntryId = row.BudgetEntryId,
        FinanceDimensionSetId = row.FinanceDimensionSetId,
        DimensionCombinationHash = row.DimensionCombinationHashSnapshot,
        SourceDocumentType = row.SourceDocumentType,
        SourceDocumentId = row.SourceDocumentId,
        SourceDocumentReference = row.SourceDocumentReference,
        SourceVersion = row.SourceVersion,
        BudgetDate = row.BudgetDate,
        SourceLineIds = ReadSourceLineIds(row),
        TransactionCurrencyCode = string.IsNullOrWhiteSpace(row.TransactionCurrencyCode) ? row.CurrencyCode : row.TransactionCurrencyCode,
        TransactionAmount = row.TransactionAmount == 0m ? row.ReservedAmount : row.TransactionAmount,
        FunctionalAmount = row.ReservedAmount,
        FunctionalCurrencyCode = row.CurrencyCode,
        Status = row.Status,
        Version = row.ReservationVersion,
        ReservedAt = row.ReservedAt,
        ReleasedAt = row.ReleasedAt,
        ConsumedAt = row.ConsumedAt,
        JournalEntryId = row.JournalEntryId,
        PostingEventId = row.PostingEventId,
        IdempotentReplay = replay
    };

    private static FinanceBudgetPositionDto CopyPosition(FinanceBudgetCellDto source) => new()
    {
        BudgetScenarioId = source.BudgetScenarioId,
        BudgetScenarioName = source.BudgetScenarioName,
        BudgetScenarioVersion = source.BudgetScenarioVersion,
        BudgetReturnId = source.BudgetReturnId,
        BudgetEntryId = source.BudgetEntryId,
        AccountId = source.AccountId,
        AccountNumber = source.AccountNumber,
        AccountName = source.AccountName,
        FiscalYearId = source.FiscalYearId,
        FiscalPeriodId = source.FiscalPeriodId,
        FiscalPeriodCode = source.FiscalPeriodCode,
        SegmentValueId = source.SegmentValueId,
        SegmentValue = source.SegmentValue,
        FinanceDimensionSetId = source.FinanceDimensionSetId,
        DimensionCombinationHash = source.DimensionCombinationHash,
        DimensionAssignments = source.DimensionAssignments,
        FunctionalCurrencyCode = source.FunctionalCurrencyCode,
        ApprovedAmount = source.ApprovedAmount,
        PostedActualAmount = source.PostedActualAmount,
        ReservedAmount = source.ReservedAmount,
        AvailableAmount = source.AvailableAmount,
        PositionAsOfUtc = source.PositionAsOfUtc
    };

    private static IReadOnlyList<Guid> ReadReservationIds(FinanceBudgetReservationOperation operation)
    {
        try
        {
            var values = JsonSerializer.Deserialize<Guid[]>(operation.ReservationIdsJson ?? "[]") ?? Array.Empty<Guid>();
            if (values.Length == 0 && operation.FinanceBudgetReservationId.HasValue)
                return new[] { operation.FinanceBudgetReservationId.Value };
            return values;
        }
        catch (JsonException)
        {
            throw Conflict("BUDGET_IDEMPOTENCY_EVIDENCE_INVALID", "Finance reservation idempotency evidence is invalid.");
        }
    }

    private static IReadOnlyList<string> ReadSourceLineIds(FinanceBudgetReservation row)
    {
        try
        {
            return JsonSerializer.Deserialize<string[]>(row.SourceLineIdsJson ?? "[]") ?? Array.Empty<string>();
        }
        catch (JsonException)
        {
            return Array.Empty<string>();
        }
    }

    private static bool IsControllingSegment(AccountSegmentStructure structure)
    {
        var key = $"{structure.SegmentCode} {structure.SegmentName}"
            .Replace(" ", string.Empty).Replace("_", string.Empty).Replace("-", string.Empty);
        return key.Contains("DEPARTMENT", StringComparison.OrdinalIgnoreCase)
            || key.Contains("DEPT", StringComparison.OrdinalIgnoreCase)
            || key.Contains("COSTCENTRE", StringComparison.OrdinalIgnoreCase)
            || key.Contains("COSTCENTER", StringComparison.OrdinalIgnoreCase);
    }

    private static bool EntryContainsAssignments(
        BudgetEntry entry,
        IReadOnlyCollection<BudgetDimensionAssignmentInputDto>? requested)
    {
        if (requested is null || requested.Count == 0)
            return true;
        ValidateAssignments(requested);
        if (entry.FinanceDimensionSet is null)
            return false;
        return requested.All(input => entry.FinanceDimensionSet.Items.Any(item =>
            item.FinanceDimensionDefinitionId == input.FinanceDimensionDefinitionId
            && item.FinanceDimensionValueId == input.FinanceDimensionValueId));
    }

    private static bool EntryMatchesAssignments(
        BudgetEntry entry,
        IReadOnlyCollection<BudgetDimensionAssignmentInputDto>? supplied)
    {
        var assignments = supplied ?? Array.Empty<BudgetDimensionAssignmentInputDto>();
        ValidateAssignments(assignments);
        if (entry.FinanceDimensionSet is null)
            return true;
        return entry.FinanceDimensionSet.Items.All(required => assignments.Any(actual =>
            actual.FinanceDimensionDefinitionId == required.FinanceDimensionDefinitionId
            && actual.FinanceDimensionValueId == required.FinanceDimensionValueId));
    }

    private static void ValidateAssignments(
        IReadOnlyCollection<BudgetDimensionAssignmentInputDto> assignments)
    {
        if (assignments.Any(item => item.FinanceDimensionDefinitionId == Guid.Empty
                || item.FinanceDimensionValueId == Guid.Empty)
            || assignments.Select(item => item.FinanceDimensionDefinitionId).Distinct().Count()
                != assignments.Count)
            throw Validation("BUDGET_DIMENSIONS_INVALID",
                "Budget dimension assignments must contain one non-empty value per dimension.");
    }

    private static IReadOnlyList<BudgetDimensionAssignmentDto> MapAssignments(BudgetEntry entry) =>
        entry.FinanceDimensionSet?.Items
            .OrderBy(item => item.FinanceDimensionDefinition.DisplayOrder)
            .ThenBy(item => item.DimensionCodeSnapshot)
            .Select(item => new BudgetDimensionAssignmentDto
            {
                FinanceDimensionDefinitionId = item.FinanceDimensionDefinitionId,
                FinanceDimensionValueId = item.FinanceDimensionValueId,
                DimensionCode = item.DimensionCodeSnapshot,
                DimensionName = item.FinanceDimensionDefinition.Name,
                ValueCode = item.DimensionValueCodeSnapshot,
                ValueName = item.DimensionValueNameSnapshot
            }).ToList() ?? new List<BudgetDimensionAssignmentDto>();

    private static string AssignmentHash(
        IEnumerable<BudgetDimensionAssignmentInputDto>? assignments) =>
        string.Join(',', (assignments ?? Array.Empty<BudgetDimensionAssignmentInputDto>())
            .OrderBy(item => item.FinanceDimensionDefinitionId)
            .Select(item => $"{item.FinanceDimensionDefinitionId:N}:{item.FinanceDimensionValueId:N}"));

    private static string RequestHash(FinanceBudgetCommitmentRequestDto request) => Hash(string.Join('|', new[]
    {
        Normalize(request.SourceDocumentType, 50, "source document type"),
        request.SourceDocumentId.ToString("N"),
        Normalize(request.SourceDocumentReference, 100, "source document reference"),
        Normalize(request.SourceVersion, 64, "source version"),
        request.BudgetDate.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        string.Join(';', request.Lines.OrderBy(x => x.SourceLineId, StringComparer.Ordinal).Select(x =>
            FormattableString.Invariant($"{Normalize(x.SourceLineId, 100, "source line ID")},{x.BudgetEntryId:N},{x.AccountId:N},{x.FiscalPeriodId:N},{x.SegmentValueId?.ToString("N") ?? "NONE"},{AssignmentHash(x.DimensionAssignments)},{x.TransactionAmount:0.00},{NormalizeCurrency(x.TransactionCurrencyCode)},{x.ExchangeRateId?.ToString("N") ?? "NONE"}")))
    }));

    private static string EvaluationHash(
        Guid tenantId,
        FinanceBudgetCommitmentRequestDto request,
        FinanceBudgetCommitmentEvaluationDto evaluation) => Hash(string.Join('|', new[]
    {
        tenantId.ToString("N"), RequestHash(request), evaluation.FunctionalCurrencyCode,
        string.Join(';', evaluation.Lines.OrderBy(x => x.BudgetEntryId).Select(x => FormattableString.Invariant(
            $"{x.BudgetEntryId:N},{x.BudgetScenarioId:N},{x.DimensionCombinationHash ?? "NONE"},{x.RequestedFunctionalAmount:0.00},{x.BudgetAmount:0.00},{x.PostedActualAmount:0.00},{x.OtherReservationsAmount:0.00},{x.AvailableAmount:0.00}")))
    }));

    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private static decimal RoundMoney(decimal value) => decimal.Round(value, 2, MidpointRounding.AwayFromZero);

    private static string Normalize(string value, int maxLength, string label)
    {
        var normalized = value?.Trim() ?? string.Empty;
        if (normalized.Length == 0 || normalized.Length > maxLength)
            throw Validation("BUDGET_CONTRACT_VALUE_INVALID", $"Finance budget {label} is required and must not exceed {maxLength} characters.");
        return normalized;
    }

    private static string NormalizeCurrency(string value)
    {
        var normalized = value?.Trim().ToUpperInvariant() ?? string.Empty;
        if (normalized.Length != 3 || normalized.Any(x => x is < 'A' or > 'Z'))
            throw Validation("BUDGET_CURRENCY_INVALID", "A three-letter ISO transaction currency is required.");
        return normalized;
    }

    private static DateTime RequireDate(DateTime value)
    {
        if (value == default)
            throw Validation("BUDGET_DATE_REQUIRED", "A budget date is required.");
        return value.Date;
    }

    private static FinanceBudgetCommitmentValidationException Validation(string code, string message) => new(code, message);
    private static FinanceBudgetCommitmentConflictException Conflict(string code, string message) => new(code, message);
    private static FinanceBudgetCommitmentNotFoundException NotFound(string code, string message) => new(code, message);

    private sealed record FunctionalRate(Guid? ExchangeRateId, decimal Multiplier);
}
