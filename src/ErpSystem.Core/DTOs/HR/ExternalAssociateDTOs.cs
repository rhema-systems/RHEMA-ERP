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
    /// <remarks>
    /// ⚠ <b>Dormant.</b> Nothing anywhere reads either of these — grepped across every service,
    /// template and screen in areas 19-23 slice 8, and the only hits are this DTO, the mapping and
    /// the entity. They are carried so an existing value survives a round trip, and they are
    /// deliberately absent from the register's form: an input that changes nothing is worse than a
    /// missing one. See the slice-6 rule — find out what READS a field before deciding it is
    /// peripheral — which is what settled this the other way for <c>JobDescription.UnionId</c>.
    /// </remarks>
    public bool    HasFixedModule   { get; set; }
    /// <inheritdoc cref="HasFixedModule"/>
    public int?    ModuleId         { get; set; }
    public bool    IsActive         { get; set; }
    public DateTime DateAdded       { get; set; }

    /// <summary>
    /// How many interview panels this associate sits on. Nonzero means the delete will be refused,
    /// and the detail screen says so before the button is pressed rather than after.
    /// </summary>
    public int     InterviewPanelCount { get; set; }
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

    // ⚠ No PicturePath. It is the LEGACY caller-supplied file location and the photo download
    // still falls back to it, so accepting one on a write let a caller name another file. The
    // photograph is set by uploading it to POST {id}/photo.

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

    // ⚠ No PicturePath. It is the LEGACY caller-supplied file location and the photo download
    // still falls back to it, so accepting one on a write let a caller name another file. The
    // photograph is set by uploading it to POST {id}/photo.

    public bool HasFixedModule { get; set; }
    public int? ModuleId       { get; set; }
    public bool IsActive       { get; set; }
}

#endregion
