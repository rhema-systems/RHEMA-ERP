using ErpSystem.Core.Enums;

namespace ErpSystem.Core.DTOs.Ehc;

public sealed class EhcTicketCategoryDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public EhcTicketType? AppliesToType { get; set; }
    public Guid? ParentCategoryId { get; set; }
}

public sealed class CreateEhcTicketCategoryRequestDto
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public EhcTicketType? AppliesToType { get; set; }
    public Guid? ParentCategoryId { get; set; }
}

public sealed class UpdateEhcTicketCategoryRequestDto
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public EhcTicketType? AppliesToType { get; set; }
    public Guid? ParentCategoryId { get; set; }
}

public sealed class EhcSlaTemplateDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public EhcTicketType? TicketType { get; set; }
    public EhcTicketPriority? Priority { get; set; }
    public Guid? CategoryId { get; set; }
    public string? CategoryName { get; set; }
    public int FirstResponseMinutes { get; set; }
    public int ResolutionMinutes { get; set; }
}

public sealed class CreateEhcSlaTemplateRequestDto
{
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public EhcTicketType? TicketType { get; set; }
    public EhcTicketPriority? Priority { get; set; }
    public Guid? CategoryId { get; set; }
    public int FirstResponseMinutes { get; set; } = 60;
    public int ResolutionMinutes { get; set; } = 1440;
}

public sealed class UpdateEhcSlaTemplateRequestDto
{
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public EhcTicketType? TicketType { get; set; }
    public EhcTicketPriority? Priority { get; set; }
    public Guid? CategoryId { get; set; }
    public int FirstResponseMinutes { get; set; } = 60;
    public int ResolutionMinutes { get; set; } = 1440;
}

public sealed class EhcLookupItemDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

public sealed class EhcWorkflowRoutingRuleDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public int Priority { get; set; }
    public string WorkflowName { get; set; } = string.Empty;
    public EhcTicketType? TicketType { get; set; }
    public EhcTicketPriority? TicketPriority { get; set; }
    public Guid? CategoryId { get; set; }
    public string? CategoryName { get; set; }
    public Guid? SubcategoryId { get; set; }
    public string? SubcategoryName { get; set; }
    public Guid? AssignedDepartmentId { get; set; }
    public string? AssignedDepartmentName { get; set; }
}

public sealed class CreateEhcWorkflowRoutingRuleRequestDto
{
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public int Priority { get; set; } = 0;
    public string WorkflowName { get; set; } = string.Empty;

    public EhcTicketType? TicketType { get; set; }
    public EhcTicketPriority? TicketPriority { get; set; }
    public Guid? CategoryId { get; set; }
    public Guid? SubcategoryId { get; set; }
    public Guid? AssignedDepartmentId { get; set; }
}

public sealed class UpdateEhcWorkflowRoutingRuleRequestDto
{
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public int Priority { get; set; } = 0;
    public string WorkflowName { get; set; } = string.Empty;

    public EhcTicketType? TicketType { get; set; }
    public EhcTicketPriority? TicketPriority { get; set; }
    public Guid? CategoryId { get; set; }
    public Guid? SubcategoryId { get; set; }
    public Guid? AssignedDepartmentId { get; set; }
}
