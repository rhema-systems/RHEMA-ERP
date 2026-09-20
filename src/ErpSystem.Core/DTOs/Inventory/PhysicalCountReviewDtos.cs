using System.ComponentModel.DataAnnotations;

namespace ErpSystem.Core.DTOs.Inventory;

public sealed record PhysicalCountDecisionOption(string Code, string Label, string Effect, bool IsActive = true);

public sealed class PhysicalCountDecisionSetup
{
    public string Revision { get; set; } = "";
    public List<PhysicalCountDecisionOption> Decisions { get; set; } = new();
}

public sealed class SavePhysicalCountDecisionSetup
{
    [Required] public string Revision { get; set; } = "";
    [Required, MinLength(2), MaxLength(30)] public List<PhysicalCountDecisionOption> Decisions { get; set; } = new();
}
