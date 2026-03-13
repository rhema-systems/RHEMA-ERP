using ErpSystem.Core.Enums;

namespace ErpSystem.Core.DTOs.Ehc;

public sealed class EhcTicketLinkDto
{
    public Guid Id { get; set; }
    public EhcTicketLinkType LinkType { get; set; }
    public string RelationshipLabel { get; set; } = string.Empty;

    public Guid LinkedTicketId { get; set; }
    public string LinkedTicketNumber { get; set; } = string.Empty;
    public string? LinkedSubject { get; set; }
    public EhcTicketStatus LinkedStatus { get; set; }
    public EhcTicketPriority LinkedPriority { get; set; }
    public DateTime LinkedCreatedAt { get; set; }

    public DateTime CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
}

public sealed class EhcExternalTicketLinkDto
{
    public Guid Id { get; set; }
    public EhcTicketLinkType LinkType { get; set; }
    public string RelationshipLabel { get; set; } = string.Empty;

    public Guid LinkedTicketId { get; set; }
    public string LinkedTicketNumber { get; set; } = string.Empty;
    public string? LinkedSubject { get; set; }
    public EhcTicketStatus LinkedStatus { get; set; }
    public EhcTicketPriority LinkedPriority { get; set; }
    public DateTime LinkedCreatedAt { get; set; }

    public DateTime CreatedAt { get; set; }
}

public sealed class CreateEhcTicketLinkRequestDto
{
    public Guid RelatedTicketId { get; set; }
    public EhcTicketLinkType LinkType { get; set; } = EhcTicketLinkType.Related;
    public string? Notes { get; set; }
    public bool ReverseDirection { get; set; } = false;
}

public sealed class CloseEhcDuplicateTicketsRequestDto
{
    /// <summary>
    /// Target status to apply to duplicate tickets (default: Closed).
    /// </summary>
    public EhcTicketStatus TargetStatus { get; set; } = EhcTicketStatus.Closed;

    /// <summary>
    /// Optional notes appended to the audit/status transition notes.
    /// </summary>
    public string? Notes { get; set; }
}

public sealed class CloseEhcDuplicateTicketFailureDto
{
    public Guid TicketId { get; set; }
    public string? TicketNumber { get; set; }
    public string Reason { get; set; } = string.Empty;
}

public sealed class CloseEhcDuplicateTicketsResultDto
{
    public int TotalDuplicates { get; set; }
    public int ClosedCount { get; set; }
    public int SkippedCount { get; set; }
    public List<CloseEhcDuplicateTicketFailureDto> Failed { get; set; } = new();
}
