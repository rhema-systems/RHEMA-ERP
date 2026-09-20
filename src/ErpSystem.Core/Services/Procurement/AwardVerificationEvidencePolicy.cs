using ErpSystem.Core.Entities.Procurement;

namespace ErpSystem.Core.Services.Procurement;

/// <summary>One evidence rule shared by verification completion and award readiness.</summary>
internal static class AwardVerificationEvidencePolicy
{
    internal static bool IsRequired(TenderAwardVerificationItemResult item) =>
        !item.IsDeleted && item.ChecklistItem is { IsDeleted: false, IsActive: true, IsRequired: true };

    internal static bool HasEvidence(TenderAwardVerificationItemResult item)
    {
        if (item.ChecklistItem is null || !item.IsVerified ||
            item.VerifiedById is null || item.VerifiedById == Guid.Empty || item.VerifiedDate is null)
            return false;

        // References already retained by this item remain usable; no duplicate upload is required.
        var hasDocument = item.Documents.Any(document => !document.IsDeleted &&
            document.TenantId == item.TenantId && document.ItemResultId == item.Id &&
            !string.IsNullOrWhiteSpace(document.FilePath));
        // The attributed decision is the review record. Comments are optional;
        // only a checklist's explicit document requirement needs an attachment.
        return !item.ChecklistItem.RequiresDocument || hasDocument;
    }

    internal static void EnsureComplete(IEnumerable<TenderAwardVerificationItemResult> results)
    {
        var required = results.Where(IsRequired).ToList();
        if (required.Count == 0 || required.Any(item => !item.IsVerified || item.Status is not ("Passed" or "Failed")))
            throw new InvalidOperationException("AWARD_VERIFICATION_REQUIRED_CHECKS: Complete every required check with a Pass or Fail decision.");
        var missing = required.Where(item => !HasEvidence(item)).ToList();
        if (missing.Count != 0)
            throw new InvalidOperationException("AWARD_VERIFICATION_EVIDENCE_REQUIRED: " +
                string.Join("; ", missing.Select(item => $"{item.ChecklistItem.ItemText}: " +
                    (item.ChecklistItem.RequiresDocument ? "attach the required document" : "retain the reviewer identity and verification time"))));
    }
}
