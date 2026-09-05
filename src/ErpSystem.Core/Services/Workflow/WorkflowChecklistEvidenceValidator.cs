using ErpSystem.Core.DTOs.Workflow;
using System.Text.Json;

namespace ErpSystem.Core.Services.Workflow;

public static class WorkflowChecklistEvidenceValidator
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public static List<string> Validate(
        IReadOnlyCollection<WorkflowQualityCheckDto> checklist,
        IReadOnlyCollection<WorkflowApprovalChecklistResponseDto> responses,
        IReadOnlyCollection<WorkflowTaskAttachmentDto> attachments)
    {
        var errors = new List<string>();
        if (checklist.Count == 0)
        {
            return errors;
        }

        var responseLookup = responses
            .Where(response => !string.IsNullOrWhiteSpace(response.Id) || !string.IsNullOrWhiteSpace(response.Name))
            .GroupBy(response => NormalizeKey(response.Id, response.Name), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        foreach (var item in checklist.Where(item => !string.IsNullOrWhiteSpace(item.Name)))
        {
            var key = NormalizeKey(item.Id, item.Name);
            responseLookup.TryGetValue(key, out var response);

            if (item.IsRequired && response?.IsSatisfied != true)
            {
                errors.Add($"Required checklist item '{item.Name}' must be satisfied before this step can continue.");
                continue;
            }

            if (!item.RequiresDocument || (!item.IsRequired && response?.IsSatisfied != true))
            {
                continue;
            }

            if (!GetAttachmentsForItem(item, attachments).Any())
            {
                var documentName = string.IsNullOrWhiteSpace(item.DocumentName)
                    ? "the required document"
                    : $"'{item.DocumentName.Trim()}'";
                errors.Add($"Attach {documentName} for checklist item '{item.Name}' before this step can continue.");
            }
        }

        return errors;
    }

    public static IReadOnlyList<WorkflowTaskAttachmentDto> GetAttachmentsForItem(
        WorkflowQualityCheckDto item,
        IEnumerable<WorkflowTaskAttachmentDto> attachments)
    {
        var key = NormalizeKey(item.Id, item.Name);
        var documentName = NormalizeDocumentName(item.DocumentName);
        return attachments
            .Where(attachment =>
                string.Equals(
                    NormalizeKey(attachment.ChecklistItemId, attachment.RequirementKey),
                    key,
                    StringComparison.OrdinalIgnoreCase) ||
                (!string.IsNullOrWhiteSpace(documentName) &&
                 string.Equals(
                     NormalizeDocumentName(attachment.DocumentName),
                     documentName,
                     StringComparison.OrdinalIgnoreCase)))
            .ToList();
    }

    public static string NormalizeKey(string? id, string? name)
    {
        return !string.IsNullOrWhiteSpace(id)
            ? id.Trim()
            : (name ?? string.Empty).Trim();
    }

    public static List<WorkflowApprovalChecklistResponseDto> ReadResponses(string? resultData)
    {
        if (string.IsNullOrWhiteSpace(resultData))
        {
            return [];
        }

        try
        {
            using var document = JsonDocument.Parse(resultData);
            if (document.RootElement.ValueKind == JsonValueKind.Array)
            {
                return JsonSerializer.Deserialize<List<WorkflowApprovalChecklistResponseDto>>(
                           document.RootElement.GetRawText(), JsonOptions) ?? [];
            }

            if (document.RootElement.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in document.RootElement.EnumerateObject())
                {
                    if (string.Equals(property.Name, "approvalChecklistResponses", StringComparison.OrdinalIgnoreCase)
                        && property.Value.ValueKind == JsonValueKind.Array)
                    {
                        return JsonSerializer.Deserialize<List<WorkflowApprovalChecklistResponseDto>>(
                                   property.Value.GetRawText(), JsonOptions) ?? [];
                    }
                }
            }
        }
        catch (JsonException)
        {
            return [];
        }

        return [];
    }

    private static string NormalizeDocumentName(string? value)
        => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
}
