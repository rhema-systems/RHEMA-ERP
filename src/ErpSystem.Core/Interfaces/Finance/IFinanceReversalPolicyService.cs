namespace ErpSystem.Core.Interfaces.Finance;

/// <summary>
/// Normalized policy decision used by source-document reversal services.
/// </summary>
public sealed record FinanceReversalPolicyDecision(string Reason, DateTime ReversalDate);

/// <summary>
/// Applies the tenant's common Finance reversal controls. Keeping reason and fiscal-date
/// validation here prevents AP, AR, cash/bank, and asset workflows from drifting into different
/// interpretations of the same policy as the remaining FIN-LIM reversals are implemented.
/// </summary>
public interface IFinanceReversalPolicyService
{
    Task<FinanceReversalPolicyDecision> ResolveAsync(
        DateTime sourceDocumentDate,
        string? reason,
        DateTime? requestedReversalDate = null,
        CancellationToken cancellationToken = default);
}
