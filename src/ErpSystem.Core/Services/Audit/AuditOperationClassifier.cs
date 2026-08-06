using System.Text;
using ErpSystem.Core.Entities;

namespace ErpSystem.Core.Services.Audit;

public static class AuditOperationClassifier
{
    public static AuditOperationKind Classify(string? action)
    {
        var value = Normalize(action);
        if (value.Length == 0) return AuditOperationKind.Other;

        if (ContainsAny(value, "reverse", "reversal", "reversed", "void")) return AuditOperationKind.Reverse;
        if (ContainsAny(value, "override", "emergency", "bypass", "waiver")) return AuditOperationKind.Override;
        if (ContainsAny(value, "reject", "rejected", "decline", "declined")) return AuditOperationKind.Reject;
        if (ContainsAny(value, "dispatch", "dispatched", "issue", "issued", "send", "sent")) return AuditOperationKind.Dispatch;
        if (ContainsAny(value, "receive", "received", "receipt", "acknowledge", "acknowledged")) return AuditOperationKind.Receive;
        if (ContainsAny(value, "post", "posted", "posting", "capitalize", "capitalized")) return AuditOperationKind.Post;
        if (ContainsAny(value, "approve", "approved", "authorize", "authorized", "accept", "accepted")) return AuditOperationKind.Approve;
        if (ContainsAny(value, "update", "updated", "amend", "amended", "edit", "edited", "change", "changed", "revise", "revised", "correct", "corrected")) return AuditOperationKind.Update;
        if (ContainsAny(value, "create", "created", "register", "registered", "generate", "generated", "submit", "submitted")) return AuditOperationKind.Create;
        return AuditOperationKind.Other;
    }

    private static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var result = new StringBuilder(value.Length);
        foreach (var character in value)
            result.Append(char.IsLetterOrDigit(character) ? char.ToLowerInvariant(character) : ' ');
        return string.Join(' ', result.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    private static bool ContainsAny(string value, params string[] terms)
        => terms.Any(term => value.Contains(term, StringComparison.Ordinal));
}
