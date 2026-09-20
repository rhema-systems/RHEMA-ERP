using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Interfaces.Procurement;
using System.Text.Json;

namespace ErpSystem.Core.Services.Procurement;

internal static class PettyPurchaseQuotationRules
{
    // SQL preserves the value but normalizes decimal scale (750 -> 750.0000).
    // Compare every JSON field structurally; never treat numeric formatting as tampering.
    internal static bool MatchesSnapshot(JsonElement expected, JsonElement actual)
    {
        if (expected.ValueKind != actual.ValueKind) return false;
        return expected.ValueKind switch
        {
            JsonValueKind.Object => expected.EnumerateObject().Count() == actual.EnumerateObject().Count() &&
                expected.EnumerateObject().All(property => actual.TryGetProperty(property.Name, out var value) && MatchesSnapshot(property.Value, value)),
            JsonValueKind.Array => expected.GetArrayLength() == actual.GetArrayLength() &&
                expected.EnumerateArray().Zip(actual.EnumerateArray()).All(pair => MatchesSnapshot(pair.First, pair.Second)),
            JsonValueKind.Number => expected.TryGetDecimal(out var left) && actual.TryGetDecimal(out var right) && left == right,
            JsonValueKind.String => expected.GetString() == actual.GetString(),
            JsonValueKind.Null or JsonValueKind.True or JsonValueKind.False => true,
            _ => false
        };
    }

    internal static decimal Validate(PettyPurchaseQuotationRequest? quote,
        IReadOnlyList<TenderItem> items, decimal? ceiling)
    {
        if (quote is null || string.IsNullOrWhiteSpace(quote.Reference) ||
            string.IsNullOrWhiteSpace(quote.EvidenceReference))
            throw Invalid("PETTY_QUOTATION_REQUIRED", "Record the supplier quotation reference, evidence and line prices before approval.");
        if (items.Count == 0 || items.Any(item => item.Quantity <= 0) ||
            quote.Items.Count != items.Count || quote.Items.Select(item => item.TenderItemId).Distinct().Count() != items.Count ||
            quote.Items.Any(item => !items.Any(source => source.Id == item.TenderItemId)))
            throw Invalid("PETTY_QUOTATION_LINES_MISMATCH", "Quote every locked requisition item exactly once; foreign, missing or duplicate lines are not allowed.");
        if (quote.Items.Any(item => item.UnitPrice <= 0 || decimal.Round(item.UnitPrice, 2) != item.UnitPrice))
            throw Invalid("PETTY_QUOTATION_PRICE_INVALID", "Enter a positive unit price with at most two decimal places for each item.");
        var total = items.Sum(item => decimal.Round(item.Quantity * quote.Items.Single(price => price.TenderItemId == item.Id).UnitPrice, 2, MidpointRounding.AwayFromZero));
        if (!ceiling.HasValue || total <= 0 || total > ceiling.Value)
            throw Invalid("PETTY_QUOTATION_EXCEEDS_APPROVAL", "The quotation exceeds the locked approved requisition value. Revise and reapprove the source instead.");
        return total;
    }

    private static ProcurementExceptionalSourcingValidationException Invalid(string code, string message) => new(code, message);
}
