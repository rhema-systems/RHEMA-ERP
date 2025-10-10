namespace ErpSystem.Shared.Interfaces;

/// <summary>
/// Interface for entities that support audit tracking
/// </summary>
public interface IAuditable
{
    /// <summary>
    /// When the entity was created
    /// </summary>
    DateTime CreatedAt { get; set; }

    /// <summary>
    /// When the entity was last updated
    /// </summary>
    DateTime? UpdatedAt { get; set; }

    /// <summary>
    /// Who created the entity
    /// </summary>
    string? CreatedBy { get; set; }

    /// <summary>
    /// Who last updated the entity
    /// </summary>
    string? UpdatedBy { get; set; }
}