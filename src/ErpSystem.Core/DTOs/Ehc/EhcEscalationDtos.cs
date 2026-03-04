using ErpSystem.Core.Entities.Ehc;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.DTOs.Ehc;

public sealed class EhcEscalationPolicyLevelDto
{
    public int Level { get; set; } = 1;
    public int DelayMinutes { get; set; } = 0;
    public string[] NotifyRoles { get; set; } = Array.Empty<string>();
    public bool NotifyAssignedAgent { get; set; } = true;
    public Guid? NotifyUserId { get; set; }
    public bool AddInternalComment { get; set; } = true;
    public string? ReassignToRole { get; set; }
    public Guid? ReassignToUserId { get; set; }
}

public sealed class EhcEscalationPolicyDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public int Priority { get; set; } = 0;
    public EhcEscalationTrigger Trigger { get; set; }
    public int DueSoonMinutes { get; set; } = 15;

    public EhcTicketType? TicketType { get; set; }
    public EhcTicketPriority? TicketPriority { get; set; }
    public Guid? CategoryId { get; set; }
    public Guid? SubcategoryId { get; set; }
    public Guid? DepartmentId { get; set; }

    public List<EhcEscalationPolicyLevelDto> Levels { get; set; } = new();
}

public class CreateEhcEscalationPolicyRequestDto
{
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public int Priority { get; set; } = 0;
    public EhcEscalationTrigger Trigger { get; set; }
    public int DueSoonMinutes { get; set; } = 15;

    public EhcTicketType? TicketType { get; set; }
    public EhcTicketPriority? TicketPriority { get; set; }
    public Guid? CategoryId { get; set; }
    public Guid? SubcategoryId { get; set; }
    public Guid? DepartmentId { get; set; }

    public List<EhcEscalationPolicyLevelDto> Levels { get; set; } = new();
}

public sealed class UpdateEhcEscalationPolicyRequestDto : CreateEhcEscalationPolicyRequestDto
{
}
