using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance;

namespace ErpSystem.Api.Services.Finance.GL;

/// <summary>
/// Applies an ordered line definition while preserving the identity of lines
/// already owned by an editable template. Generated occurrence snapshots can
/// therefore retain stable source-line evidence.
/// </summary>
internal sealed record RecurringJournalLineEditResult(
    IReadOnlyList<RecurringJournalTemplateLine> AddedLines,
    IReadOnlyList<RecurringJournalTemplateLine> RemovedLines);

internal static class RecurringJournalLineDefinitionEditor
{
    public static RecurringJournalLineEditResult Apply(
        RecurringJournalTemplate template,
        IReadOnlyList<RecurringJournalTemplateLineInputDto> inputs,
        DateTime now,
        Guid? userId,
        string? userName,
        bool allowExistingLineIds)
    {
        var existing = template.Lines.Where(line => !line.IsDeleted).ToDictionary(line => line.Id);
        var retained = new HashSet<Guid>();
        var ordered = new List<RecurringJournalTemplateLine>(inputs.Count);
        var nextLineNumber = existing.Count == 0 ? 1 : existing.Values.Max(line => line.LineNumber) + 1;

        for (var index = 0; index < inputs.Count; index++)
        {
            var input = inputs[index];
            RecurringJournalTemplateLine line;
            if (input.Id.HasValue)
            {
                if (!allowExistingLineIds)
                    throw new InvalidOperationException("New recurring-journal lines cannot supply persisted identities.");
                if (!existing.TryGetValue(input.Id.Value, out line!))
                    throw new InvalidOperationException("A recurring-journal line does not belong to this editable template.");
                if (!retained.Add(line.Id))
                    throw new InvalidOperationException("Recurring-journal line identities must be unique.");
                line.UpdatedAt = now;
                line.UpdatedBy = userName;
                line.LastModifiedById = userId;
            }
            else
            {
                line = new RecurringJournalTemplateLine
                {
                    Id = Guid.NewGuid(),
                    TenantId = template.TenantId,
                    TemplateId = template.Id,
                    CreatedAt = now,
                    CreatedBy = userName,
                    CreatedById = userId
                };
                line.LineNumber = nextLineNumber++;
            }

            line.AccountId = input.AccountId;
            line.IsDebit = input.IsDebit;
            line.FixedAmount = decimal.Round(input.FixedAmount, 2, MidpointRounding.AwayFromZero);
            line.Description = NormalizeOptional(input.Description, 500);
            line.DimensionValuesJson = string.IsNullOrWhiteSpace(input.DimensionValuesJson)
                ? "{}"
                : input.DimensionValuesJson.Trim();
            ordered.Add(line);
        }

        var removedLines = existing.Values.Where(line => !retained.Contains(line.Id)).ToList();
        foreach (var removed in removedLines)
        {
            // Finance entities use soft deletion. Marking the row directly avoids
            // severing a required relationship and retains historical line evidence.
            removed.IsDeleted = true;
            removed.DeletedAt = now;
            removed.UpdatedAt = now;
            removed.UpdatedBy = userName;
            removed.LastModifiedById = userId;
        }
        var addedLines = ordered.Where(line => !existing.ContainsKey(line.Id)).ToList();
        foreach (var added in addedLines)
            template.Lines.Add(added);

        // Retained line numbers are intentionally stable. New lines are appended
        // above the previous maximum, avoiding transient unique-index collisions
        // when an earlier line is removed in the same database transaction.
        return new RecurringJournalLineEditResult(addedLines, removedLines);
    }

    private static string? NormalizeOptional(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var trimmed = value.Trim();
        if (trimmed.Length > maxLength)
            throw new InvalidOperationException($"Value exceeds the maximum length of {maxLength} characters.");
        return trimmed;
    }
}
