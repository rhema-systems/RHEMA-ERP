using System.ComponentModel.DataAnnotations;

namespace ErpSystem.Core.DTOs.HR;

#region Union DTOs

public class UnionDto : BaseDto
{
    public Guid TenantId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? ContactPerson { get; set; }
    public string? ContactEmail { get; set; }
    public string? ContactPhone { get; set; }
    public bool IsActive { get; set; }
    public int AgreementCount { get; set; }
    public List<CollectiveBargainingAgreementDto> Agreements { get; set; } = new();
}

public class CreateUnionDto : CreateDtoBase
{
    [MaxLength(50)]
    public string Code { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    [MaxLength(150)]
    public string? ContactPerson { get; set; }

    [MaxLength(150)]
    public string? ContactEmail { get; set; }

    [MaxLength(50)]
    public string? ContactPhone { get; set; }

    public bool IsActive { get; set; } = true;
}

public class UpdateUnionDto : UpdateDtoBase
{
    [MaxLength(50)]
    public string Code { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    [MaxLength(150)]
    public string? ContactPerson { get; set; }

    [MaxLength(150)]
    public string? ContactEmail { get; set; }

    [MaxLength(50)]
    public string? ContactPhone { get; set; }

    public bool IsActive { get; set; }
}

#endregion

#region Collective Bargaining Agreement DTOs

public class CollectiveBargainingAgreementDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid UnionId { get; set; }
    public string? UnionName { get; set; }
    public string? ReferenceNumber { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTime EffectiveDate { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public string? Summary { get; set; }
    public string? DocumentReference { get; set; }
    public bool IsActive { get; set; }
}

public class CreateCollectiveBargainingAgreementDto : CreateDtoBase
{
    [Required]
    public Guid UnionId { get; set; }

    [MaxLength(50)]
    public string? ReferenceNumber { get; set; }

    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required]
    public DateTime EffectiveDate { get; set; }

    public DateTime? ExpiryDate { get; set; }

    [MaxLength(2000)]
    public string? Summary { get; set; }

    [MaxLength(500)]
    public string? DocumentReference { get; set; }

    public bool IsActive { get; set; } = true;
}

public class UpdateCollectiveBargainingAgreementDto : UpdateDtoBase
{
    [MaxLength(50)]
    public string? ReferenceNumber { get; set; }

    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required]
    public DateTime EffectiveDate { get; set; }

    public DateTime? ExpiryDate { get; set; }

    [MaxLength(2000)]
    public string? Summary { get; set; }

    [MaxLength(500)]
    public string? DocumentReference { get; set; }

    public bool IsActive { get; set; }
}

#endregion
