using ErpSystem.Core.Enums;

namespace ErpSystem.Core.DTOs.Ehc;

public sealed class EhcProblemListItemDto
{
    public Guid Id { get; set; }
    public string ProblemNumber { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public EhcProblemStatus Status { get; set; }
    public EhcTicketPriority Priority { get; set; }
    public string? DepartmentName { get; set; }
    public string? OwnerName { get; set; }
    public int LinkedTicketsCount { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class EhcProblemDetailDto
{
    public Guid Id { get; set; }
    public string ProblemNumber { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public EhcProblemStatus Status { get; set; }
    public EhcTicketPriority Priority { get; set; }
    public Guid? CategoryId { get; set; }
    public string? CategoryName { get; set; }
    public Guid? SubcategoryId { get; set; }
    public string? SubcategoryName { get; set; }
    public Guid? DepartmentId { get; set; }
    public string? DepartmentName { get; set; }
    public Guid? OwnerUserId { get; set; }
    public string? OwnerName { get; set; }

    public Guid? RootCauseId { get; set; }
    public string? RootCauseCode { get; set; }
    public string? RootCauseName { get; set; }
    public string? RootCauseDetails { get; set; }
    public string? ResolutionSummary { get; set; }

    public Guid? CreatedFromTicketId { get; set; }
    public string? CreatedFromTicketNumber { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public List<EhcProblemLinkedTicketDto> LinkedTickets { get; set; } = new();
    public List<EhcCapaTaskDto> CapaTasks { get; set; } = new();
    public List<EhcProblemAuditEventDto> AuditTrail { get; set; } = new();
}

public sealed class EhcProblemLinkedTicketDto
{
    public Guid TicketId { get; set; }
    public string TicketNumber { get; set; } = string.Empty;
    public string? Subject { get; set; }
    public EhcTicketStatus Status { get; set; }
    public EhcTicketPriority Priority { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? Notes { get; set; }
    public DateTime LinkedAt { get; set; }
}

public sealed class CreateEhcProblemRequestDto
{
    public string? Title { get; set; }
    public string? Description { get; set; }
    public EhcTicketPriority Priority { get; set; } = EhcTicketPriority.Medium;
    public Guid? CategoryId { get; set; }
    public Guid? SubcategoryId { get; set; }
    public Guid? DepartmentId { get; set; }
    public Guid? OwnerUserId { get; set; }
}

public sealed class UpdateEhcProblemRequestDto
{
    public string? Title { get; set; }
    public string? Description { get; set; }
    public EhcTicketPriority Priority { get; set; } = EhcTicketPriority.Medium;
    public Guid? CategoryId { get; set; }
    public Guid? SubcategoryId { get; set; }
    public Guid? DepartmentId { get; set; }
    public Guid? OwnerUserId { get; set; }
    public EhcProblemStatus Status { get; set; } = EhcProblemStatus.Open;
    public Guid? RootCauseId { get; set; }
    public string? RootCauseDetails { get; set; }
    public string? ResolutionSummary { get; set; }
}

public sealed class ConvertEhcTicketToProblemRequestDto
{
    public string? Title { get; set; }
    public string? Description { get; set; }
    public EhcTicketPriority? Priority { get; set; }
    public Guid? DepartmentId { get; set; }
    public Guid? OwnerUserId { get; set; }
    public bool LinkOnly { get; set; } = false;
}

public sealed class LinkEhcTicketToProblemRequestDto
{
    public Guid TicketId { get; set; }
    public string? Notes { get; set; }
}

public sealed class EhcCapaTaskDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public EhcCapaTaskStatus Status { get; set; }
    public Guid? AssignedToUserId { get; set; }
    public string? AssignedToName { get; set; }
    public Guid? AssignedDepartmentId { get; set; }
    public string? AssignedDepartmentName { get; set; }
    public DateTime? DueAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class CreateEhcCapaTaskRequestDto
{
    public string? Title { get; set; }
    public string? Description { get; set; }
    public Guid? AssignedToUserId { get; set; }
    public Guid? AssignedDepartmentId { get; set; }
    public DateTime? DueAt { get; set; }
}

public sealed class UpdateEhcCapaTaskRequestDto
{
    public string? Title { get; set; }
    public string? Description { get; set; }
    public Guid? AssignedToUserId { get; set; }
    public Guid? AssignedDepartmentId { get; set; }
    public DateTime? DueAt { get; set; }
    public EhcCapaTaskStatus Status { get; set; } = EhcCapaTaskStatus.Open;
}

public sealed class EhcProblemAuditEventDto
{
    public Guid Id { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string? Title { get; set; }
    public string? Body { get; set; }
    public Guid? ActorUserId { get; set; }
    public string? ActorName { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class EhcProblemLookupItemDto
{
    public Guid Id { get; set; }
    public string ProblemNumber { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public EhcProblemStatus Status { get; set; }
    public EhcTicketPriority Priority { get; set; }
    public DateTime CreatedAt { get; set; }
}
