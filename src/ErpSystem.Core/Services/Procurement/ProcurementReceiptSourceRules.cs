using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Interfaces.Procurement;

namespace ErpSystem.Core.Services.Procurement;

public static class ProcurementReceiptSourceRules
{
    public const decimal MaximumTolerancePercent = 100m;

    public static bool IsReceivableStatus(string? status, bool existingReceipt)
    {
        var normalized = string.Concat(
            (status ?? string.Empty).Where(char.IsLetterOrDigit));
        return string.Equals(
                   normalized,
                   "Approved",
                   StringComparison.OrdinalIgnoreCase) ||
               string.Equals(
                   normalized,
                   "PartiallyReceived",
                   StringComparison.OrdinalIgnoreCase) ||
               existingReceipt &&
               string.Equals(
                   normalized,
                   "Received",
                   StringComparison.OrdinalIgnoreCase);
    }

    public static decimal NormalizeTolerance(decimal? tolerancePercent)
    {
        var tolerance = tolerancePercent ?? 0m;
        if (tolerance is < 0m or > MaximumTolerancePercent)
        {
            throw new ProcurementReceiptSourceValidationException(
                "RCV_TOLERANCE_INVALID",
                $"Receipt tolerance must be between 0 and {MaximumTolerancePercent:0.##} percent.");
        }

        return decimal.Round(tolerance, 4, MidpointRounding.AwayFromZero);
    }

    public static (decimal ToleranceQuantity, decimal MaximumQuantity, decimal RemainingQuantity)
        CalculateCapacity(
            decimal orderedQuantity,
            decimal previouslyReceiptedQuantity,
            decimal tolerancePercent)
    {
        if (orderedQuantity <= 0)
        {
            throw new ProcurementReceiptSourceValidationException(
                "RCV_ORDERED_QUANTITY_INVALID",
                "The purchase-order line must have a positive ordered quantity.");
        }

        var toleranceQuantity = decimal.Round(
            orderedQuantity * tolerancePercent / 100m,
            4,
            MidpointRounding.AwayFromZero);
        var maximumQuantity = orderedQuantity + toleranceQuantity;
        var remainingQuantity = Math.Max(0m, maximumQuantity - previouslyReceiptedQuantity);
        return (toleranceQuantity, maximumQuantity, remainingQuantity);
    }

    public static (bool Allowed, string Code, string Message) EvaluateLine(
        decimal requestedQuantity,
        decimal remainingQuantity)
    {
        if (requestedQuantity <= 0)
        {
            return (
                false,
                "RCV_QUANTITY_REQUIRED",
                "Received quantity must be greater than zero.");
        }

        if (requestedQuantity > remainingQuantity)
        {
            return (
                false,
                "RCV_REMAINING_QUANTITY_EXCEEDED",
                $"Received quantity {requestedQuantity:0.####} exceeds the governed remaining quantity {remainingQuantity:0.####}, including tolerance.");
        }

        return (
            true,
            "RCV_LINE_READY",
            "The receipt line matches the purchase order and remains within governed capacity.");
    }

    public static string Hash(params object?[] values)
    {
        var canonical = string.Join(
            "|",
            values.Select(value => value switch
            {
                null => string.Empty,
                DateTime date => date.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
                decimal number => number.ToString("0.####", CultureInfo.InvariantCulture),
                _ => value.ToString()?.Trim() ?? string.Empty
            }));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }
}
