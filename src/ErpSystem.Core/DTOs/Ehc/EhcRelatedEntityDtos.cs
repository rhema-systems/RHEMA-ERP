namespace ErpSystem.Core.DTOs.Ehc;

public sealed class EhcRelatedEntityLookupItemDto
{
    public Guid Id { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public string Reference { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string? OpenUrl { get; set; }
}

public sealed class EhcRelatedEntityResolveDto
{
    public bool Exists { get; set; }
    public Guid? Id { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public string Reference { get; set; } = string.Empty;
    public string? Label { get; set; }
    public string? OpenUrl { get; set; }
}

