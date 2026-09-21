using System.ComponentModel.DataAnnotations;

namespace ErpSystem.Core.DTOs.HR;

#region Job Family DTOs

public class JobFamilyDto : BaseDto
{
    public Guid TenantId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; }
    public int SubFamilyCount { get; set; }
    public List<JobSubFamilyDto> SubFamilies { get; set; } = new();
}

public class CreateJobFamilyDto : CreateDtoBase
{
    [MaxLength(50)] public string Code { get; set; } = string.Empty;
    [Required][MaxLength(150)] public string Name { get; set; } = string.Empty;
    [MaxLength(1000)] public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
}

public class UpdateJobFamilyDto : UpdateDtoBase
{
    [MaxLength(50)] public string Code { get; set; } = string.Empty;
    [Required][MaxLength(150)] public string Name { get; set; } = string.Empty;
    [MaxLength(1000)] public string? Description { get; set; }
    public bool IsActive { get; set; }
}

#endregion

#region Job Sub-Family DTOs

public class JobSubFamilyDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid JobFamilyId { get; set; }
    public string? JobFamilyName { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; }
}

public class CreateJobSubFamilyDto : CreateDtoBase
{
    [Required] public Guid JobFamilyId { get; set; }
    [MaxLength(50)] public string Code { get; set; } = string.Empty;
    [Required][MaxLength(150)] public string Name { get; set; } = string.Empty;
    [MaxLength(1000)] public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
}

public class UpdateJobSubFamilyDto : UpdateDtoBase
{
    /// <summary>
    /// The family the caller believes this sub-family belongs to. Optional, and never written.
    /// </summary>
    /// <remarks>
    /// ⚠ The DTO used to carry no family at all, so a caller who sent one to move the sub-family
    /// got 200 and no move — a silent no-op, which is the worst of the three possible answers. It
    /// is here so the attempt can be REFUSED: re-filing a sub-family re-classifies every job
    /// description under it, which is a reclassification rather than an edit, and nothing in the
    /// product is set up to do it. Sending the sub-family's own family is how a client round-trips
    /// the record, so that is accepted.
    /// </remarks>
    public Guid? JobFamilyId { get; set; }

    [MaxLength(50)] public string Code { get; set; } = string.Empty;
    [Required][MaxLength(150)] public string Name { get; set; } = string.Empty;
    [MaxLength(1000)] public string? Description { get; set; }
    public bool IsActive { get; set; }
}

#endregion

#region Job Level DTOs

public class JobLevelDto : BaseDto
{
    public Guid TenantId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int Rank { get; set; }
    public string? Description { get; set; }
    public Guid? SalaryGradeId { get; set; }
    public string? SalaryGradeName { get; set; }
    public bool IsActive { get; set; }
}

public class CreateJobLevelDto : CreateDtoBase
{
    [MaxLength(50)] public string Code { get; set; } = string.Empty;
    [Required][MaxLength(150)] public string Name { get; set; } = string.Empty;
    public int Rank { get; set; }
    [MaxLength(1000)] public string? Description { get; set; }
    public Guid? SalaryGradeId { get; set; }
    public bool IsActive { get; set; } = true;
}

public class UpdateJobLevelDto : UpdateDtoBase
{
    [MaxLength(50)] public string Code { get; set; } = string.Empty;
    [Required][MaxLength(150)] public string Name { get; set; } = string.Empty;
    public int Rank { get; set; }
    [MaxLength(1000)] public string? Description { get; set; }
    public Guid? SalaryGradeId { get; set; }
    public bool IsActive { get; set; }
}

#endregion
