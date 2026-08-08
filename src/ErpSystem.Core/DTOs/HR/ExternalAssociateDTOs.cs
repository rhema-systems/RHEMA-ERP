using System.ComponentModel.DataAnnotations;

namespace ErpSystem.Core.DTOs.HR;

// ============================================================================
// EXTERNAL ASSOCIATE DTOs
// ============================================================================

#region Read DTOs

/// <summary>Full representation returned from GET /api/external-associates/{id}.</summary>
public class ExternalAssociateDto : BaseDto
{
    public Guid    TenantId        { get; set; }
    public string  AssociateNumber { get; set; } = string.Empty;
    public string? Title           { get; set; }
    public string  FirstName       { get; set; } = string.Empty;
    public string  MiddleName      { get; set; } = string.Empty;
    public string  LastName        { get; set; } = string.Empty;
    public string  FullName        => $"{Title} {FirstName} {MiddleName} {LastName}".Trim().Replace("  ", " ");
    public string  Email           { get; set; } = string.Empty;
    public string  PhoneNumber     { get; set; } = string.Empty;
    public string? CompanyName     { get; set; }
    public string? Role            { get; set; }
    public string  PicturePath     { get; set; } = string.Empty;
    public bool    HasFixedModule   { get; set; }
    public int?    ModuleId         { get; set; }
    public bool    IsActive         { get; set; }
    public DateTime DateAdded       { get; set; }
}

/// <summary>Lightweight row for list and typeahead surfaces.</summary>
public class ExternalAssociateSummaryDto
{
    public Guid    Id              { get; set; }
    public string  AssociateNumber { get; set; } = string.Empty;
    public string? Title           { get; set; }
    public string  FirstName       { get; set; } = string.Empty;
    public string  LastName        { get; set; } = string.Empty;
    public string  FullName        => string.IsNullOrWhiteSpace(Title)
                                        ? $"{FirstName} {LastName}".Trim()
                                        : $"{Title} {FirstName} {LastName}".Trim();
    public string  Email           { get; set; } = string.Empty;
    public string  PhoneNumber     { get; set; } = string.Empty;
    public string? CompanyName     { get; set; }
    public string? Role            { get; set; }
    public bool    IsActive         { get; set; }
}

/// <summary>Minimal projection used by typeahead/panelist-picker endpoints.</summary>
public class ExternalAssociateSearchResultDto
{
    public Guid    Id              { get; set; }
    public string  AssociateNumber { get; set; } = string.Empty;
    public string  FullName        { get; set; } = string.Empty;
    public string  Email           { get; set; } = string.Empty;
    public string? PhoneNumber     { get; set; }
    public string? Role            { get; set; }
    public string? CompanyName     { get; set; }
}

#endregion

#region Write DTOs

public class CreateExternalAssociateDto : CreateDtoBase
{
    [MaxLength(10)]
    public string? Title { get; set; }

    [Required]
    [MaxLength(50)]
    public string FirstName { get; set; } = string.Empty;

    [MaxLength(50)]
    public string MiddleName { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string LastName { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    [MaxLength(15)]
    [Phone]
    public string PhoneNumber { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? CompanyName { get; set; }

    [MaxLength(100)]
    public string? Role { get; set; }

    [MaxLength(500)]
    public string? PicturePath { get; set; }

    public bool HasFixedModule { get; set; }
    public int? ModuleId       { get; set; }
    public bool IsActive       { get; set; } = true;
}

public class UpdateExternalAssociateDto : UpdateDtoBase
{
    [MaxLength(10)]
    public string? Title { get; set; }

    [Required]
    [MaxLength(50)]
    public string FirstName { get; set; } = string.Empty;

    [MaxLength(50)]
    public string MiddleName { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string LastName { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    [MaxLength(15)]
    [Phone]
    public string PhoneNumber { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? CompanyName { get; set; }

    [MaxLength(100)]
    public string? Role { get; set; }

    [MaxLength(500)]
    public string? PicturePath { get; set; }

    public bool HasFixedModule { get; set; }
    public int? ModuleId       { get; set; }
    public bool IsActive       { get; set; }
}

#endregion
