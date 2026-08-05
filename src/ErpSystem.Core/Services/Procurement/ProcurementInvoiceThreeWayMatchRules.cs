using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ErpSystem.Core.Services.Procurement;

public static class ProcurementInvoiceThreeWayMatchRules
{
    public const string RuleCode = "AP-002";
    public const string RuleVersion = "TDC-0504";
    public const string EvaluationAction = "InvoiceThreeWayMatchEvaluated";
    public const string ExceptionRuleCode = "AP-006";
    public const string ExceptionRuleVersion = "TDC-0507";
    public const string ExceptionApprovalAction = "InvoiceMatchExceptionApproved";

    public static readonly IReadOnlyList<string> DecisionKeys = Enumerable.Range(1, 14)
        .Select(number => $"DEC-{number:000}")
        .ToArray();

    public static bool IsRequired(Guid? purchaseOrderId, bool isOpeningBalance) =>
        purchaseOrderId.HasValue && !isOpeningBalance;

    public static decimal NormalizeTolerance(decimal value) =>
        decimal.Round(Math.Clamp(value, 0m, 100m), 2, MidpointRounding.AwayFromZero);

    public static bool IsPriceWithinTolerance(decimal invoicePrice, decimal orderPrice, decimal tolerancePercent)
    {
        if (orderPrice < 0m || invoicePrice < 0m) return false;
        if (orderPrice == 0m) return invoicePrice == 0m;
        return PercentageVariance(invoicePrice, orderPrice) <= NormalizeTolerance(tolerancePercent);
    }

    public static bool IsCumulativeQuantityWithinTolerance(
        decimal cumulativeInvoiceQuantity,
        decimal acceptedQuantity,
        decimal tolerancePercent)
    {
        if (cumulativeInvoiceQuantity < 0m || acceptedQuantity < 0m) return false;
        var maximum = acceptedQuantity + acceptedQuantity * NormalizeTolerance(tolerancePercent) / 100m;
        return cumulativeInvoiceQuantity <= maximum;
    }

    public static decimal PercentageVariance(decimal actual, decimal expected)
    {
        if (expected == 0m) return actual == 0m ? 0m : 100m;
        return decimal.Round(Math.Abs(actual - expected) / Math.Abs(expected) * 100m,
            4, MidpointRounding.AwayFromZero);
    }

    public static string HashSnapshot(object snapshot)
    {
        var json = JsonSerializer.Serialize(snapshot, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json))).ToLowerInvariant();
    }
}
