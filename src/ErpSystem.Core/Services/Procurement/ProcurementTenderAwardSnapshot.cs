using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ErpSystem.Core.Services.Procurement;

internal static class ProcurementTenderAwardSnapshot
{
    // This is a comparison only: never rewrite an approved PO's snapshot/hash.
    // The caller must additionally prove the matching governed activation.
    internal static bool IsContractActivationOnlyChange(
        string? persistedJson, string? persistedHash,
        string? currentJson, string? currentHash)
    {
        if (!HasHash(persistedJson, persistedHash) || !HasHash(currentJson, currentHash))
            return false;

        try
        {
            using var previous = JsonDocument.Parse(persistedJson!);
            using var current = JsonDocument.Parse(currentJson!);
            if (!HasUniqueStatus(previous.RootElement, "Awarded") ||
                !HasUniqueStatus(current.RootElement, "ContractSigned"))
                return false;

            using var buffer = new MemoryStream();
            using (var writer = new Utf8JsonWriter(buffer))
            {
                writer.WriteStartObject();
                foreach (var property in current.RootElement.EnumerateObject())
                {
                    if (property.Name == "status")
                        writer.WriteString("status", "Awarded");
                    else
                        property.WriteTo(writer);
                }
                writer.WriteEndObject();
            }

            return string.Equals(Convert.ToHexString(SHA256.HashData(buffer.ToArray())),
                persistedHash, StringComparison.OrdinalIgnoreCase);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool HasUniqueStatus(JsonElement root, string expected) =>
        root.ValueKind == JsonValueKind.Object &&
        root.EnumerateObject().Select(property => property.Name).Distinct(StringComparer.Ordinal).Count() ==
            root.EnumerateObject().Count() &&
        root.TryGetProperty("status", out var status) &&
        status.ValueKind == JsonValueKind.String && status.GetString() == expected;

    private static bool HasHash(string? json, string? hash) =>
        !string.IsNullOrWhiteSpace(json) && !string.IsNullOrWhiteSpace(hash) &&
        string.Equals(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json))),
            hash, StringComparison.OrdinalIgnoreCase);
}
