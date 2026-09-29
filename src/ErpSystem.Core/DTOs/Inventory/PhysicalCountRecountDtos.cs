using System.ComponentModel.DataAnnotations;

namespace ErpSystem.Core.DTOs.Inventory;

public sealed class CreatePhysicalCountRecountRequest : PhysicalCountMutationRequest
{
    [Required, MinLength(1), MaxLength(10000)]
    public List<PhysicalCountRecountSelection> Items { get; set; } = new();
}

public sealed class PhysicalCountRecountSelection
{
    [Required] public Guid PhysicalCountItemId { get; set; }
    [Required] public string ItemRowVersion { get; set; } = string.Empty;
    [Required, MaxLength(2000)] public string Reason { get; set; } = string.Empty;
}
