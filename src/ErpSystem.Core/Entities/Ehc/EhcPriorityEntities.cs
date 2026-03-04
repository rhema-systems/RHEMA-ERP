using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Entities.Ehc;

[Table("EhcTicketPriorityLevels")]
public class EhcTicketPriorityLevel : TenantEntity
{
    public EhcTicketPriority Priority { get; set; } = EhcTicketPriority.Medium;

    [Required]
    [StringLength(100)]
    public string DisplayName { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;

    public int SortOrder { get; set; } = 0;
}

