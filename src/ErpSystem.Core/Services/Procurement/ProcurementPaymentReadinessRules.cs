using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ErpSystem.Core.Entities.Finance;

namespace ErpSystem.Core.Services.Procurement;

/// <summary>
/// Canonical AP payment-readiness policy shared by direct allocations, supplier
/// advances, payment posting and payment batches. TDC-0506 owns the later
/// processor-versus-payment-approver identity rule; this policy deliberately
/// composes the existing Finance permissions and batch workflow instead.
/// </summary>
public static class ProcurementPaymentReadinessRules
{
    public const string RuleCode = "AP-003";
    public const string RuleVersion = "TDC-0505";
    public const string EventType = "VendorInvoicePaymentReadiness";

    public const string AllocateAction = "PaymentAllocationAuthorized";
    public const string SupplierAdvanceAction = "SupplierAdvanceApplicationAuthorized";
    public const string BatchCreateAction = "PaymentBatchInvoiceSelected";
    public const string BatchApproveAction = "PaymentBatchInvoiceApproved";
    public const string BatchProcessAction = "PaymentBatchInvoiceProcessed";
    public const string PostAction = "PaymentPostingAuthorized";

    public static readonly IReadOnlyList<string> DecisionKeys = Enumerable.Range(1, 14)
        .Select(number => $"DEC-{number:000}")
        .ToArray();

    public static bool IsInvoiceStatePaymentEligible(VendorInvoiceStatus status) =>
        status is VendorInvoiceStatus.Approved
            or VendorInvoiceStatus.PartiallyPaid
            or VendorInvoiceStatus.Overdue;

    public static string HashSnapshot(object snapshot)
    {
        var json = JsonSerializer.Serialize(snapshot, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json))).ToLowerInvariant();
    }
}

public sealed class VendorPaymentControlException : Exception
{
    public VendorPaymentControlException(string code, string message) : base(message) => Code = code;
    public string Code { get; }
}
