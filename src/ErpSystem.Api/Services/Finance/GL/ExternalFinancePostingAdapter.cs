using System.Text.RegularExpressions;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Finance;
using ErpSystem.Core.Finance.Integration;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Data;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.Finance.GL;

/// <summary>
/// The single implementation behind multiple compiled external routes. This is not a generic
/// posting route: every call resolves one reviewed route definition from the restricted external
/// contract catalogue and persists that exact provenance.
/// </summary>
public sealed class ExternalFinancePostingAdapter : IExternalFinancePostingAdapter
{
    private static readonly Regex Sha256 = new("^[A-Fa-f0-9]{64}$", RegexOptions.Compiled);
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IFinanceSourceDimensionService _dimensions;
    private readonly IFinancePostingEngine _posting;

    public ExternalFinancePostingAdapter(
        ApplicationDbContext db,
        ICurrentUserService currentUser,
        IFinanceSourceDimensionService dimensions,
        IFinancePostingEngine posting)
    {
        _db = db;
        _currentUser = currentUser;
        _dimensions = dimensions;
        _posting = posting;
    }

    public async Task<FinanceSourceDocumentDimensionDto> ValidateDimensionsAsync(
        FinanceExternalPostingEnvelopeDto envelope,
        CancellationToken cancellationToken = default)
    {
        var validated = await ValidateEnvelopeAsync(envelope, cancellationToken);
        return await EnsureFrozenDimensionsAsync(validated, envelope, "dimension capture", cancellationToken);
    }

    public async Task<FinancePostingResultDto> PostAsync(
        FinanceExternalPostingEnvelopeDto envelope,
        CancellationToken cancellationToken = default)
    {
        // Reject malformed/cross-tenant input before opening a transaction. Revalidate under the
        // transaction below so account and route evidence cannot change between validation/posting.
        _ = await ValidateEnvelopeAsync(envelope, cancellationToken);
        var strategy = _db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
            var validated = await ValidateEnvelopeAsync(envelope, cancellationToken);
            await EnsureFrozenDimensionsAsync(validated, envelope, "posting capture", cancellationToken);

            var postingLines = new List<FinancePostingLineDto>(envelope.Lines.Count);
            foreach (var line in envelope.Lines)
            {
                var sourceLineId = line.SourceDocumentLineId!.Value;
                var resolved = await _dimensions.ResolvePostingDimensionsAsync(
                    validated.Producer,
                    envelope.SourceDocumentId,
                    sourceLineId,
                    line.AccountId,
                    envelope.PostingDate,
                    cancellationToken);
                postingLines.Add(CloneLine(line, resolved));
            }

            var result = await _posting.PostAsync(new FinancePostingRequestDto
            {
                SourceModule = validated.Route.PostingSourceModule,
                OriginModuleCode = ResolveOriginModule(validated.Route.ProducerModule),
                SourceDocumentType = validated.Route.DocumentType,
                SourceDocumentId = envelope.SourceDocumentId,
                SourceDocumentTenantId = envelope.TenantId,
                PostingAction = "Post",
                SourceDocumentReference = envelope.SourceDocumentReference.Trim(),
                Description = envelope.Description.Trim(),
                PostingDate = envelope.PostingDate,
                JournalType = envelope.JournalType.Trim(),
                BookClassification = envelope.BookClassification.Trim(),
                FunctionalCurrencyCode = envelope.FunctionalCurrencyCode.Trim().ToUpperInvariant(),
                IdempotencyKey = $"{validated.Route.SourceRoute}:{envelope.TenantId:N}:{envelope.SourceDocumentId:N}:{envelope.IdempotencyKey.Trim()}",
                ReturnExistingOnDuplicate = true,
                Lines = postingLines
            }, validated.Producer, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return result;
        });
    }

    private async Task<ValidatedEnvelope> ValidateEnvelopeAsync(
        FinanceExternalPostingEnvelopeDto envelope,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        var tenantId = _currentUser.GetRequiredFinanceTenantId();
        if (envelope.TenantId == Guid.Empty || envelope.TenantId != tenantId)
            throw new InvalidOperationException("External Finance payload tenant does not match the authenticated tenant.");
        if (envelope.SourceDocumentId == Guid.Empty) throw new ArgumentException("Source document id is required.");
        Required(envelope.SourceDocumentReference, "Source document reference", 100);
        Required(envelope.Description, "Description", 500);
        Required(envelope.IdempotencyKey, "Idempotency key", 200);
        Required(envelope.ApprovalReference, "Approval reference", 200);
        if (!envelope.SourceApproved || envelope.ApprovedByUserId == Guid.Empty)
            throw new InvalidOperationException("Finance accepts only independently approved external source evidence.");
        if (envelope.ApprovedAtUtc.Kind != DateTimeKind.Utc || envelope.ApprovedAtUtc > DateTime.UtcNow.AddMinutes(1))
            throw new ArgumentException("Approval timestamp must be a valid UTC timestamp.");
        if (!Sha256.IsMatch(envelope.SourceEvidenceHash ?? string.Empty))
            throw new ArgumentException("Source evidence hash must be a SHA-256 hexadecimal value.");
        if (envelope.PostingDate == default) throw new ArgumentException("Posting date is required.");
        var currency = Required(envelope.FunctionalCurrencyCode, "Functional currency", 3).ToUpperInvariant();
        if (currency.Length != 3 || !currency.All(char.IsLetter))
            throw new ArgumentException("Functional currency must be a three-letter code.");
        if (!string.Equals(envelope.PostingAction, "Post", StringComparison.Ordinal))
            throw new ArgumentException("The v1 external adapter accepts only the Post action; reversals require an explicit compensating contract.");
        if (envelope.Lines is not { Count: >= 2 })
            throw new ArgumentException("A balanced external posting requires at least two lines.");

        var producer = FinanceExternalProducerContractCatalog.GetRequired(envelope.ContractId);
        var route = producer.Definition;
        var lineIds = new HashSet<Guid>();
        var accounts = new HashSet<Guid>();
        decimal debit = 0m;
        decimal credit = 0m;
        foreach (var line in envelope.Lines)
        {
            if (!line.SourceDocumentLineId.HasValue || line.SourceDocumentLineId == Guid.Empty)
                throw new ArgumentException("Every external posting line requires a stable source-line id.");
            if (!lineIds.Add(line.SourceDocumentLineId.Value))
                throw new ArgumentException("External source-line ids must be unique per economic posting line.");
            if (line.AccountId == Guid.Empty) throw new ArgumentException("Every external posting line requires an account.");
            accounts.Add(line.AccountId);
            if (line.DebitAmount < 0m || line.CreditAmount < 0m || (line.DebitAmount > 0m) == (line.CreditAmount > 0m))
                throw new ArgumentException("Each external posting line must contain exactly one positive debit or credit.");
            if (!string.IsNullOrWhiteSpace(line.TransactionCurrency)
                && !string.Equals(line.TransactionCurrency.Trim(), currency, StringComparison.OrdinalIgnoreCase)
                && (!line.ExchangeRate.HasValue || line.ExchangeRate <= 0m || !line.ExchangeRateId.HasValue))
                throw new ArgumentException("Foreign-currency lines require positive rate and immutable exchange-rate evidence.");
            debit += line.DebitAmount;
            credit += line.CreditAmount;
        }
        if (decimal.Round(debit, 2) != decimal.Round(credit, 2))
            throw new ArgumentException("External Finance posting lines are not balanced.");

        var accountCount = await _db.Accounts.AsNoTracking().CountAsync(account =>
            account.TenantId == tenantId && accounts.Contains(account.Id) && !account.IsDeleted,
            cancellationToken);
        if (accountCount != accounts.Count)
            throw new InvalidOperationException("One or more external posting accounts are missing or cross-tenant.");

        var sourceLines = envelope.Lines.Select(line =>
            new FinanceSourceDocumentLineContext(line.SourceDocumentLineId!.Value, line.AccountId)).ToArray();
        return new ValidatedEnvelope(producer, route, sourceLines);
    }

    private async Task<FinanceSourceDocumentDimensionDto> EnsureFrozenDimensionsAsync(
        ValidatedEnvelope validated,
        FinanceExternalPostingEnvelopeDto envelope,
        string operation,
        CancellationToken cancellationToken)
    {
        var expectedLineIds = validated.SourceLines.Select(line => line.SourceLineId).ToHashSet();
        var persisted = await _db.FinanceSourceDimensionAssignments.AsNoTracking()
            .Where(item => item.TenantId == envelope.TenantId
                && item.RouteId == validated.Producer.RouteId
                && item.SourceDocumentId == envelope.SourceDocumentId
                && !item.IsDeleted)
            .ToListAsync(cancellationToken);
        var frozenLineIds = persisted.Where(item => item.SourceLineId.HasValue && item.EvidenceFrozenAt.HasValue)
            .Select(item => item.SourceLineId!.Value).ToHashSet();

        // Idempotent retries and a separate validate-then-post sequence must retain the original
        // frozen evidence. SynchronizeDraftAsync is intentionally called only before first freeze.
        if (persisted.Any(item => !item.SourceLineId.HasValue) && frozenLineIds.SetEquals(expectedLineIds))
        {
            return await _dimensions.ValidateAndFreezeAsync(
                validated.Producer, envelope.SourceDocumentId, envelope.PostingDate,
                validated.SourceLines, false, cancellationToken);
        }

        await _dimensions.SynchronizeDraftAsync(
            validated.Producer,
            envelope.SourceDocumentId,
            envelope.PostingDate,
            validated.SourceLines,
            envelope.FinanceDimensions,
            inheritDefaultForUnassignedLines: envelope.FinanceDimensions?.ApplyDefaultToEligibleLines == true,
            budgetReservationSourceDocumentType: null,
            reason: $"External producer {validated.Route.SourceRoute} {operation}",
            cancellationToken);
        return await _dimensions.ValidateAndFreezeAsync(
            validated.Producer,
            envelope.SourceDocumentId,
            envelope.PostingDate,
            validated.SourceLines,
            requireCurrentBudgetEvidence: false,
            cancellationToken);
    }

    private static FinancePostingLineDto CloneLine(
        FinancePostingLineDto line,
        IReadOnlyList<FinancePostingDimensionValueDto> dimensions) => new()
    {
        AccountId = line.AccountId,
        SourceDocumentLineId = line.SourceDocumentLineId,
        Description = line.Description,
        DebitAmount = line.DebitAmount,
        CreditAmount = line.CreditAmount,
        TransactionCurrency = line.TransactionCurrency,
        TransactionDebitAmount = line.TransactionDebitAmount,
        TransactionCreditAmount = line.TransactionCreditAmount,
        ForeignCurrencyAmount = line.ForeignCurrencyAmount,
        ExchangeRateId = line.ExchangeRateId,
        ExchangeRate = line.ExchangeRate,
        ExchangeRateSource = line.ExchangeRateSource,
        ExchangeRateDate = line.ExchangeRateDate,
        SourceReferenceNumber = line.SourceReferenceNumber,
        LineNumber = line.LineNumber,
        Dimensions = dimensions,
        SegmentString = line.SegmentString,
        Notes = line.Notes,
        TransactionTag = line.TransactionTag
    };

    private static string ResolveOriginModule(string producer) => producer switch
    {
        "Procurement" => FinanceModuleLockCatalog.Procurement,
        "Inventory" => FinanceModuleLockCatalog.Inventory,
        "Sales" => FinanceModuleLockCatalog.Sales,
        "HR" => FinanceModuleLockCatalog.HumanResources,
        "QuantitySurvey" => FinanceModuleLockCatalog.QuantitySurvey,
        "Estate" => FinanceModuleLockCatalog.Estate,
        "Legal" => FinanceModuleLockCatalog.Legal,
        "Maintenance" => FinanceModuleLockCatalog.Maintenance,
        _ => throw new InvalidOperationException($"External Finance producer '{producer}' has no module-lock identity.")
    };

    private static string Required(string? value, string label, int max)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized) || normalized.Length > max)
            throw new ArgumentException($"{label} is required and cannot exceed {max} characters.");
        return normalized;
    }

    private sealed record ValidatedEnvelope(
        FinancePostingProducerContext Producer,
        FinanceDimensionRouteDefinition Route,
        IReadOnlyList<FinanceSourceDocumentLineContext> SourceLines);
}
