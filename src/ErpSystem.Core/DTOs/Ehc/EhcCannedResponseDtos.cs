using ErpSystem.Core.Enums;

namespace ErpSystem.Core.DTOs.Ehc;

public sealed class EhcCannedResponseDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public EhcTicketType? AppliesToType { get; set; }
    public Guid? CategoryId { get; set; }
}

public class CreateEhcCannedResponseRequestDto
{
    public string Code { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public EhcTicketType? AppliesToType { get; set; }
    public Guid? CategoryId { get; set; }
}

public sealed class UpdateEhcCannedResponseRequestDto : CreateEhcCannedResponseRequestDto
{
}

