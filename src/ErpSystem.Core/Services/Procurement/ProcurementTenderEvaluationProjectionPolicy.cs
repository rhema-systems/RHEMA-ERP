using ErpSystem.Core.Entities.Procurement;

namespace ErpSystem.Core.Services.Procurement;

internal static class ProcurementTenderEvaluationProjectionPolicy
{
    // Keep one current projection per bidder/voter, including replacement drafts.
    // Filter completion status only after selection so recalled scores are not reused.
    // Callers retain the original rows for history and segregation-of-duties checks.
    internal static List<TenderEvaluation> SelectCurrent(IEnumerable<TenderEvaluation> evaluations) =>
        evaluations
            .GroupBy(item => new { item.TenderBidId, item.TenderEvaluatorId })
            .Select(group => group
                .OrderByDescending(item => item.SubmittedDate ?? item.UpdatedAt ?? item.EvaluationDate)
                .ThenByDescending(item => item.CreatedAt)
                .ThenByDescending(item => item.Id)
                .First())
            .ToList();
}
