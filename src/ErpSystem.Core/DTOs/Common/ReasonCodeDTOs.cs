using ErpSystem.Core.Enums;

namespace ErpSystem.Core.DTOs.Common;

public class ReasonCodeDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public ReasonCodeCategory Category { get; set; }
    public bool IsActive { get; set; }
}

public class CreateReasonCodeDto
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public ReasonCodeCategory Category { get; set; } = ReasonCodeCategory.General;
    public bool IsActive { get; set; } = true;
}

public class UpdateReasonCodeDto
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public ReasonCodeCategory Category { get; set; }
    public bool IsActive { get; set; }
}
