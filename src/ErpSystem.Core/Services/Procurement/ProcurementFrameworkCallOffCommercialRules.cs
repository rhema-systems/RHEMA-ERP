using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Services.Procurement;

public static class ProcurementFrameworkCallOffCommercialRules
{
    public sealed record AgreementRevisionState(
        Guid AgreementId,
        Guid AgreementKey,
        int Version,
        string CurrencyCode,
        decimal CeilingAmount,
        DateTime EffectiveFromUtc,
        DateTime EffectiveEndUtc,
        bool IsPublished);

    public sealed record MovementState(
        Guid AgreementId,
        ProcurementFrameworkBalanceMovementType MovementType,
        decimal Amount);

    public sealed record FamilyCapacity(
        decimal CeilingAmount,
        decimal CommittedAmount,
        decimal AvailableAmount,
        decimal ProposedAmount,
        bool CanReserve);

    public sealed record FamilySummary(
        Guid AgreementKey,
        Guid? CurrentAgreementId,
        string CurrencyCode,
        decimal CeilingAmount,
        decimal CommittedAmount,
        decimal IssuedAmount,
        decimal AvailableAmount,
        bool IsEffective,
        DateTime? EffectiveEndUtc);

    public static (decimal Quantity, decimal LineTotal) NormalizeLine(
        decimal quantity,
        decimal unitPrice)
    {
        var normalizedQuantity = decimal.Round(
            quantity,
            4,
            MidpointRounding.AwayFromZero);
        var lineTotal = decimal.Round(
            normalizedQuantity * unitPrice,
            2,
            MidpointRounding.AwayFromZero);
        return (normalizedQuantity, lineTotal);
    }

    public static FamilyCapacity EvaluateFamilyCapacity(
        decimal ceilingAmount,
        decimal committedAmount,
        decimal proposedAmount)
    {
        var ceiling = RoundMoney(ceilingAmount);
        var committed = RoundMoney(Math.Max(0m, committedAmount));
        var proposed = RoundMoney(Math.Max(0m, proposedAmount));
        var available = RoundMoney(Math.Max(0m, ceiling - committed));
        return new FamilyCapacity(
            ceiling,
            committed,
            available,
            proposed,
            proposed <= available);
    }

    public static IReadOnlyList<AgreementRevisionState>
        SelectCurrentEffectiveRevisions(
            IEnumerable<AgreementRevisionState> revisions,
            DateTime atUtc)
    {
        var at = EnsureUtc(atUtc);
        return revisions
            .Where(item =>
                item.IsPublished &&
                item.EffectiveFromUtc <= at &&
                item.EffectiveEndUtc > at)
            .GroupBy(item => item.AgreementKey)
            .Select(group => group
                .OrderByDescending(item => item.Version)
                .ThenByDescending(item => item.EffectiveFromUtc)
                .ThenByDescending(item => item.AgreementId)
                .First())
            .ToList();
    }

    public static bool IsCurrentEffectiveRevision(
        AgreementRevisionState revision,
        IEnumerable<AgreementRevisionState> familyRevisions,
        DateTime atUtc) =>
        SelectCurrentEffectiveRevisions(familyRevisions, atUtc)
            .SingleOrDefault(item =>
                item.AgreementKey == revision.AgreementKey)
            ?.AgreementId == revision.AgreementId;

    public static IReadOnlyList<FamilySummary> SummarizeFamilies(
        IEnumerable<AgreementRevisionState> revisions,
        IEnumerable<MovementState> movements,
        DateTime atUtc)
    {
        var revisionList = revisions.ToList();
        var movementsByAgreement = movements
            .GroupBy(item => item.AgreementId)
            .ToDictionary(
                group => group.Key,
                group => group.ToList());
        var currentByFamily = SelectCurrentEffectiveRevisions(
                revisionList,
                atUtc)
            .ToDictionary(
                item => item.AgreementKey);
        var summaries = new List<FamilySummary>();

        foreach (var family in revisionList.GroupBy(
                     item => item.AgreementKey))
        {
            currentByFamily.TryGetValue(family.Key, out var current);
            var reference = current ?? family
                .OrderByDescending(item => item.Version)
                .ThenByDescending(item => item.EffectiveFromUtc)
                .ThenByDescending(item => item.AgreementId)
                .First();
            var agreementIds = family
                .Select(item => item.AgreementId)
                .ToList();
            var familyMovements = agreementIds
                .Where(movementsByAgreement.ContainsKey)
                .SelectMany(item => movementsByAgreement[item])
                .ToList();
            var committed = RoundMoney(Math.Max(
                0m,
                familyMovements.Sum(item => item.MovementType switch
                {
                    ProcurementFrameworkBalanceMovementType.Commitment =>
                        item.Amount,
                    ProcurementFrameworkBalanceMovementType.Release =>
                        -item.Amount,
                    _ => 0m
                })));
            var issued = RoundMoney(familyMovements
                .Where(item =>
                    item.MovementType ==
                    ProcurementFrameworkBalanceMovementType.Issue)
                .Sum(item => item.Amount));
            var ceiling = RoundMoney(reference.CeilingAmount);
            var available = current is null
                ? 0m
                : EvaluateFamilyCapacity(
                    ceiling,
                    committed,
                    0m).AvailableAmount;

            summaries.Add(new FamilySummary(
                family.Key,
                current?.AgreementId,
                reference.CurrencyCode,
                ceiling,
                committed,
                issued,
                available,
                current is not null,
                current?.EffectiveEndUtc));
        }

        return summaries;
    }

    public static IReadOnlyDictionary<Guid, FamilySummary>
        SummarizeRevisionFamilies(
            IEnumerable<AgreementRevisionState> revisions,
            IEnumerable<MovementState> movements,
            DateTime atUtc)
    {
        var revisionList = revisions.ToList();
        var summariesByFamily = SummarizeFamilies(
                revisionList,
                movements,
                atUtc)
            .ToDictionary(item => item.AgreementKey);

        return revisionList.ToDictionary(
            item => item.AgreementId,
            item => summariesByFamily[item.AgreementKey]);
    }

    private static DateTime EnsureUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };

    private static decimal RoundMoney(decimal value) =>
        decimal.Round(value, 2, MidpointRounding.AwayFromZero);
}
