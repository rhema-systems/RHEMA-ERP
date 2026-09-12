using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using ErpSystem.Core.DTOs.Inventory;

namespace ErpSystem.Core.Services.Inventory;

/// <summary>Tenant settings own the labels; the two supported consequences remain enforced server-side.</summary>
public static class PhysicalCountDecisionPolicy
{
    public const string SettingKey = "Inventory.PhysicalCount.Decisions.v1";
    public const string ApproveAdjustment = "ApproveAdjustment";
    public const string Investigate = "Investigate";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static PhysicalCountDecisionSetup Read(string? value)
    {
        var decisions = value == null ? new List<PhysicalCountDecisionOption> {
            new("APPROVE", "Approve adjustment", ApproveAdjustment),
            new("INVESTIGATE", "Send for investigation", Investigate)
        } : JsonSerializer.Deserialize<List<PhysicalCountDecisionOption>>(value, Json)
            ?? throw new InvalidOperationException("Count decisions are invalid. Ask Inventory Administration to correct them.");
        Validate(decisions);
        return new() { Decisions = decisions, Revision = Revision(value) };
    }

    public static string Revision(string? value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value ?? "defaults-v1")));
    public static string Serialize(List<PhysicalCountDecisionOption> decisions) { Validate(decisions); return JsonSerializer.Serialize(decisions, Json); }

    public static void Validate(List<PhysicalCountDecisionOption> decisions)
    {
        if (decisions.Count is < 2 or > 30 || decisions.Any(d => d == null ||
            !Regex.IsMatch(d.Code ?? "", "^[A-Z][A-Z0-9_]{0,39}$") || string.IsNullOrWhiteSpace(d.Label) || d.Label.Length > 100 ||
            d.Effect is not (ApproveAdjustment or Investigate)) ||
            decisions.Select(d => d.Code).Distinct(StringComparer.OrdinalIgnoreCase).Count() != decisions.Count ||
            !decisions.Any(d => d.IsActive && d.Effect == ApproveAdjustment) || !decisions.Any(d => d.IsActive && d.Effect == Investigate))
            throw new InvalidOperationException("Use unique decision codes and labels, with at least one active approval and one active investigation decision.");
    }

    public static PhysicalCountDecisionOption Resolve(PhysicalCountDecisionSetup setup, string? code, string? revision)
    {
        if (setup.Revision != revision) throw new InvalidOperationException("Count decisions changed. Refresh before deciding.");
        return setup.Decisions.SingleOrDefault(d => d.Code == code && d.IsActive)
            ?? throw new InvalidOperationException("Select an active configured count decision.");
    }
}
