namespace ErpSystem.Core.DTOs.Numbering;

public class DocumentSequenceDefinitionDto
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Module { get; set; } = string.Empty;
    public string DocumentType { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Format { get; set; } = string.Empty;
    public long NextNumber { get; set; }
    public long StartNumber { get; set; }
    public int MinimumDigits { get; set; }
    public string ResetPolicy { get; set; } = string.Empty;
    public string? LastResetPeriodKey { get; set; }
    public bool IsContinuous { get; set; }
    public bool AllowManualEntry { get; set; }
    public bool IsActive { get; set; }
    public bool IsDefault { get; set; }
    public DateTime? EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
    public string? Description { get; set; }
}

public class UpdateDocumentSequenceDefinitionDto
{
    public string? Name { get; set; }
    public string? Format { get; set; }
    public long? NextNumber { get; set; }
    public long? StartNumber { get; set; }
    public int? MinimumDigits { get; set; }
    public string? ResetPolicy { get; set; }
    public bool? IsContinuous { get; set; }
    public bool? AllowManualEntry { get; set; }
    public bool? IsActive { get; set; }
    public bool? IsDefault { get; set; }
    public DateTime? EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
    public string? Description { get; set; }
}

public class GenerateDocumentNumberRequestDto
{
    public string Module { get; set; } = string.Empty;
    public string DocumentType { get; set; } = string.Empty;
    public Guid? TenantId { get; set; }
    public DateTime? DocumentDate { get; set; }
    public string? EntityType { get; set; }
    public Guid? EntityId { get; set; }
}

public class GeneratedDocumentNumberDto
{
    public string DocumentNumber { get; set; } = string.Empty;
}
