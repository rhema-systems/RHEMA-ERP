using ErpSystem.Core.Enums;

namespace ErpSystem.Core.DTOs.Ehc;

public sealed class EhcTicketAllowedTransitionDto
{
    public Guid? TransitionId { get; set; }
    public string? TransitionName { get; set; }
    public string? TransitionDescription { get; set; }
    public EhcTicketStatus TargetStatus { get; set; }
}
