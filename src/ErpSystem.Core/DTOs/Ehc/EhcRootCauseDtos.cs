namespace ErpSystem.Core.DTOs.Ehc;

public sealed class EhcRootCauseCodeDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
}

public class CreateEhcRootCauseCodeRequestDto
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class UpdateEhcRootCauseCodeRequestDto : CreateEhcRootCauseCodeRequestDto
{
}
