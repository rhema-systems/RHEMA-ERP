using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Finance.Integration;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Data;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.Finance.FixedAssets;

public sealed class FixedAssetDimensionService : IFixedAssetDimensionService
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IFinanceSourceDimensionService _sourceDimensions;
    private readonly IFinanceSourceDimensionAssignmentStore _assignments;

    public FixedAssetDimensionService(
        ApplicationDbContext db,
        ICurrentUserService currentUser,
        IFinanceSourceDimensionService sourceDimensions,
        IFinanceSourceDimensionAssignmentStore assignments)
    {
        _db = db;
        _currentUser = currentUser;
        _sourceDimensions = sourceDimensions;
        _assignments = assignments;
    }

    private Guid TenantId => _currentUser.GetRequiredFinanceTenantId();

    public async Task<FinanceSourceDocumentDimensionDto> SynchronizeAsync(
        FinancePostingProducerContext producer,
        Guid sourceDocumentId,
        DateTime documentDate,
        IReadOnlyList<FinancePostingLineDto> economicLines,
        FinanceSourceDocumentDimensionInputDto? input,
        IReadOnlyDictionary<Guid, Guid>? inheritedAssetJournalBySourceLine,
        string reason,
        CancellationToken cancellationToken = default)
    {
        EnsureFixedAssetRoute(producer);
        var contexts = BuildContexts(economicLines.ToArray());
        var existing = await _assignments.GetDocumentAssignmentsAsync(
            producer, sourceDocumentId, cancellationToken);
        if (existing.Count == 0 && inheritedAssetJournalBySourceLine is { Count: > 0 })
        {
            input = await SeedInheritedLinesAsync(
                input, contexts, inheritedAssetJournalBySourceLine, cancellationToken);
        }

        return await _sourceDimensions.SynchronizeDraftAsync(
            producer,
            sourceDocumentId,
            documentDate,
            contexts,
            input,
            inheritDefaultForUnassignedLines: true,
            budgetReservationSourceDocumentType: null,
            reason,
            cancellationToken);
    }

    public Task<FinanceSourceDocumentDimensionDto> GetAsync(
        FinancePostingProducerContext producer,
        Guid sourceDocumentId,
        DateTime documentDate,
        IReadOnlyList<FinancePostingLineDto> economicLines,
        CancellationToken cancellationToken = default)
    {
        EnsureFixedAssetRoute(producer);
        return _sourceDimensions.GetAsync(
            producer, sourceDocumentId, documentDate, BuildContexts(economicLines), cancellationToken);
    }

    public async Task<FinanceSourceDocumentDimensionDto> ValidateFreezeAndApplyAsync(
        FinancePostingProducerContext producer,
        Guid sourceDocumentId,
        DateTime documentDate,
        IList<FinancePostingLineDto> economicLines,
        CancellationToken cancellationToken = default)
    {
        EnsureFixedAssetRoute(producer);
        var contexts = BuildContexts(economicLines.ToArray());
        var result = await _sourceDimensions.ValidateAndFreezeAsync(
            producer,
            sourceDocumentId,
            documentDate,
            contexts,
            requireCurrentBudgetEvidence: false,
            cancellationToken);

        foreach (var line in economicLines)
        {
            var sourceLineId = line.SourceDocumentLineId
                ?? throw new InvalidOperationException("Every fixed-asset posting line requires a stable source-line id.");
            line.Dimensions = await _sourceDimensions.ResolvePostingDimensionsAsync(
                producer,
                sourceDocumentId,
                sourceLineId,
                line.AccountId,
                documentDate,
                cancellationToken);
        }
        return result;
    }

    public async Task RegisterHistoricalReversalAsync(
        FinancePostingProducerContext producer,
        Guid reversalDocumentId,
        Guid originalJournalEntryId,
        IList<FinancePostingLineDto> reversalLines,
        CancellationToken cancellationToken = default)
    {
        EnsureFixedAssetRoute(producer);
        if (originalJournalEntryId == Guid.Empty)
            throw new InvalidOperationException("An original journal is required for fixed-asset reversal evidence.");

        var originals = await _db.AccountTransactions.AsNoTracking()
            .Where(item => item.TenantId == TenantId
                && item.JournalEntryId == originalJournalEntryId && !item.IsDeleted)
            .OrderBy(item => item.LineNumber)
            .ToListAsync(cancellationToken);
        if (originals.Count != reversalLines.Count)
            throw new InvalidOperationException("The fixed-asset reversal plan no longer matches the original journal lines.");

        await _assignments.RegisterDocumentAsync(producer, reversalDocumentId, cancellationToken);
        for (var index = 0; index < reversalLines.Count; index++)
        {
            var reversal = reversalLines[index];
            var original = originals.SingleOrDefault(item =>
                item.LineNumber == (reversal.LineNumber ?? index + 1)
                && item.AccountId == reversal.AccountId
                && item.DebitAmount == reversal.CreditAmount
                && item.CreditAmount == reversal.DebitAmount)
                ?? throw new InvalidOperationException("A reversal component could not be bound to its exact original journal line.");
            if (reversal.FinanceDimensionSetId != original.FinanceDimensionSetId)
                throw new InvalidOperationException("A reversal component does not retain its original Finance dimension set.");

            var sourceLineId = original.SourceDocumentLineId
                ?? FinanceSourceLineIdentity.Create(
                    reversalDocumentId,
                    $"HISTORICAL-REVERSAL-{original.LineNumber}",
                    original.Id);
            reversal.SourceDocumentLineId = sourceLineId;
            await _assignments.FreezeLineAsync(
                producer,
                reversalDocumentId,
                sourceLineId,
                original.FinanceDimensionSetId,
                original.FinanceDimensionSnapshotId,
                cancellationToken);
        }
    }

    private async Task<IReadOnlyList<FinancePostingDimensionValueDto>> LoadFrozenJournalDimensionsAsync(
        Guid journalEntryId,
        CancellationToken cancellationToken)
    {
        var source = await _db.AccountTransactions.AsNoTracking()
            .Include(item => item.FinanceDimensionSnapshot)!
                .ThenInclude(snapshot => snapshot!.Items)
            .Include(item => item.FinanceDimensionSet)!
                .ThenInclude(set => set!.Items)
            .Where(item => item.TenantId == TenantId
                && item.JournalEntryId == journalEntryId && !item.IsDeleted
                && item.FinanceDimensionSetId.HasValue)
            .OrderByDescending(item => item.DebitAmount > 0m)
            .ThenBy(item => item.LineNumber)
            .FirstOrDefaultAsync(cancellationToken);
        if (source?.FinanceDimensionSnapshot is not null)
            return source.FinanceDimensionSnapshot.Items
                .OrderBy(item => item.DimensionCodeSnapshot, StringComparer.Ordinal)
                .Select(item => new FinancePostingDimensionValueDto
                {
                    DimensionCode = item.DimensionCodeSnapshot,
                    ValueCode = item.DimensionValueCodeSnapshot
                }).ToArray();
        if (source?.FinanceDimensionSet is not null)
            return source.FinanceDimensionSet.Items
                .OrderBy(item => item.DimensionCodeSnapshot, StringComparer.Ordinal)
                .Select(item => new FinancePostingDimensionValueDto
                {
                    DimensionCode = item.DimensionCodeSnapshot,
                    ValueCode = item.DimensionValueCodeSnapshot
                }).ToArray();
        return Array.Empty<FinancePostingDimensionValueDto>();
    }

    private async Task<FinanceSourceDocumentDimensionInputDto> SeedInheritedLinesAsync(
        FinanceSourceDocumentDimensionInputDto? input,
        IReadOnlyList<FinanceSourceDocumentLineContext> contexts,
        IReadOnlyDictionary<Guid, Guid> inheritedJournalBySourceLine,
        CancellationToken cancellationToken)
    {
        var supplied = (input?.Lines ?? Array.Empty<FinanceSourceLineDimensionInputDto>())
            .Where(item => item.SourceLineId.HasValue)
            .ToDictionary(item => item.SourceLineId!.Value);
        var journalCache = new Dictionary<Guid, IReadOnlyList<FinancePostingDimensionValueDto>>();
        var lines = new List<FinanceSourceLineDimensionInputDto>(
            supplied.Count + inheritedJournalBySourceLine.Count);
        foreach (var context in contexts)
        {
            if (supplied.TryGetValue(context.SourceLineId, out var explicitValue))
            {
                lines.Add(explicitValue);
                continue;
            }
            if (!inheritedJournalBySourceLine.TryGetValue(context.SourceLineId, out var journalEntryId))
                continue;
            if (!journalCache.TryGetValue(journalEntryId, out var inherited))
            {
                inherited = await LoadFrozenJournalDimensionsAsync(journalEntryId, cancellationToken);
                journalCache[journalEntryId] = inherited;
            }
            lines.Add(new FinanceSourceLineDimensionInputDto
            {
                SourceLineId = context.SourceLineId,
                AccountId = context.AccountId,
                Dimensions = inherited
            });
        }
        var defaultDimensions = input?.DefaultDimensions ?? Array.Empty<FinancePostingDimensionValueDto>();
        return new FinanceSourceDocumentDimensionInputDto
        {
            DefaultDimensions = defaultDimensions,
            Lines = lines,
            ApplyDefaultToEligibleLines = input?.ApplyDefaultToEligibleLines ?? false
        };
    }

    private static IReadOnlyList<FinanceSourceDocumentLineContext> BuildContexts(
        IReadOnlyList<FinancePostingLineDto> economicLines)
    {
        if (economicLines.Count == 0)
            throw new InvalidOperationException("A fixed-asset source document requires economic lines.");
        var contexts = economicLines.Select(line => new FinanceSourceDocumentLineContext(
            line.SourceDocumentLineId
                ?? throw new InvalidOperationException("Every fixed-asset economic line requires a stable source-line id."),
            line.AccountId)).ToArray();
        if (contexts.Select(item => item.SourceLineId).Distinct().Count() != contexts.Length)
            throw new InvalidOperationException("Fixed-asset source-line identities must be unique per economic component.");
        return contexts;
    }

    private static void EnsureFixedAssetRoute(FinancePostingProducerContext producer)
    {
        ArgumentNullException.ThrowIfNull(producer);
        if (producer.Definition.Owner != "Finance / Fixed Assets")
            throw new InvalidOperationException("The producer is not a compiled Finance Fixed Assets dimension route.");
    }
}
