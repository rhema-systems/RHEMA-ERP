namespace ErpSystem.Core.DTOs.Ehc;

public sealed class EhcFaqCategoryDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; }
}

public sealed class CreateEhcFaqCategoryRequestDto
{
    public string? Code { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class UpdateEhcFaqCategoryRequestDto
{
    public string? Code { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class EhcFaqItemListItemDto
{
    public Guid Id { get; set; }
    public string Question { get; set; } = string.Empty;
    public Guid? CategoryId { get; set; }
    public string? CategoryName { get; set; }
    public bool IsPublished { get; set; }
    public bool IsInternalOnly { get; set; }
    public int SortOrder { get; set; }
    public int ViewCount { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class EhcFaqItemDetailDto
{
    public Guid Id { get; set; }
    public string Question { get; set; } = string.Empty;
    public string Answer { get; set; } = string.Empty;
    public Guid? CategoryId { get; set; }
    public string? CategoryName { get; set; }
    public bool IsPublished { get; set; }
    public bool IsInternalOnly { get; set; }
    public int SortOrder { get; set; }
    public int ViewCount { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class CreateEhcFaqItemRequestDto
{
    public string? Question { get; set; }
    public string? Answer { get; set; }
    public Guid? CategoryId { get; set; }
    public bool IsPublished { get; set; } = true;
    public bool IsInternalOnly { get; set; } = true;
    public int SortOrder { get; set; } = 0;
}

public sealed class UpdateEhcFaqItemRequestDto
{
    public string? Question { get; set; }
    public string? Answer { get; set; }
    public Guid? CategoryId { get; set; }
    public bool IsPublished { get; set; } = true;
    public bool IsInternalOnly { get; set; } = true;
    public int SortOrder { get; set; } = 0;
}
