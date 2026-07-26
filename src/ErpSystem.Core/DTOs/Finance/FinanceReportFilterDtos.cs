using System;

namespace ErpSystem.Core.DTOs.Finance;

public class FinanceSegmentFilterDto
{
    public Guid? SegmentStructureId { get; set; }
    public string? SegmentCode { get; set; }
    public int? SegmentPosition { get; set; }
    public string SegmentValue { get; set; } = string.Empty;
}
