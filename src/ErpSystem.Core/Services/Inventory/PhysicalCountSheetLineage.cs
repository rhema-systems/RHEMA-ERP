using System.Text.Json;
using ErpSystem.Core.DTOs.Inventory;
using ErpSystem.Core.Entities.Inventory;

namespace ErpSystem.Core.Services.Inventory;

public static class PhysicalCountSheetLineage
{
    public static PhysicalCountSheetBinding? Read(PhysicalCountAction? action)
    {
        if (action?.ActionType != PhysicalCountActionType.CountRecorded) return null;
        using var json = JsonDocument.Parse(action.SnapshotJson);
        if (!json.RootElement.TryGetProperty("payload", out var payload) ||
            !payload.TryGetProperty("countSheet", out var sheet)) return null;
        return sheet.Deserialize<PhysicalCountSheetBinding>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
    }

    // A later manual quantity edit invalidates the previous sheet as the current source.
    public static PhysicalCountSheetBinding? Current(IEnumerable<PhysicalCountAction> actions) =>
        Read(actions.Where(a => !a.IsDeleted && a.ActionType == PhysicalCountActionType.CountRecorded)
            .OrderByDescending(a => a.Sequence).FirstOrDefault());
}
