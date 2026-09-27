namespace ErpSystem.Core.DTOs.Finance;

/// <summary>Bounded record navigation summaries, without ledger amounts or document details.</summary>
public sealed class FinanceRecordSearchDto
{
    public Guid Id { get; set; }
    public string Number { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}
