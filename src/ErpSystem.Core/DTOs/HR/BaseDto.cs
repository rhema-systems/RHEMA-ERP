namespace ErpSystem.Core.DTOs.HR;

/// <summary>
/// Base DTO for all entities
/// </summary>
public abstract class BaseDto
{
    public Guid Id { get; set; }
    
    public DateTime CreatedAt { get; set; }

    public string CreatedBy { get; set; } = string.Empty;
    
    public DateTime? UpdatedAt { get; set; }
    
    public string? UpdatedBy { get; set; }
}

/// <summary>
/// Base DTO for create operations
/// </summary>
public abstract class CreateDtoBase
{
}

/// <summary>
/// Base DTO for update operations
/// </summary>
public abstract class UpdateDtoBase
{
    public Guid Id { get; set; }
}
