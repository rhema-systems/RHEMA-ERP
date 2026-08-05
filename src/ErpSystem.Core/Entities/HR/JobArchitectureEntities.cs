using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ErpSystem.Core.Entities.HR;

/// <summary>
/// Top-level grouping of related jobs (e.g. Engineering, Finance, Operations).
/// Foundation of the job architecture / job catalog.
/// </summary>
public class JobFamily : TenantEntity
{
    [MaxLength(50)]
    public string Code { get; set; } = string.Empty;

    [Required(ErrorMessage = "Job family name is required")]
    [MaxLength(150)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;

    public virtual ICollection<JobSubFamily> SubFamilies { get; set; } = new List<JobSubFamily>();
}

/// <summary>
/// A sub-grouping within a <see cref="JobFamily"/> (e.g. Backend Engineering under Engineering).
/// </summary>
public class JobSubFamily : TenantEntity
{
    public Guid JobFamilyId { get; set; }

    [ForeignKey(nameof(JobFamilyId))]
    public virtual JobFamily JobFamily { get; set; } = null!;

    [MaxLength(50)]
    public string Code { get; set; } = string.Empty;

    [Required(ErrorMessage = "Sub-family name is required")]
    [MaxLength(150)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;
}

/// <summary>
/// A career / job level in the architecture (e.g. L1 Associate … L6 Principal),
/// ordered by <see cref="Rank"/> and optionally mapped to a salary grade.
/// </summary>
public class CareerLevel : TenantEntity
{
    [MaxLength(50)]
    public string Code { get; set; } = string.Empty;

    [Required(ErrorMessage = "Level name is required")]
    [MaxLength(150)]
    public string Name { get; set; } = string.Empty;

    /// <summary>Ordering rank (1 = lowest). Higher = more senior.</summary>
    public int Rank { get; set; }

    [MaxLength(1000)]
    public string? Description { get; set; }

    public Guid? SalaryGradeId { get; set; }

    [ForeignKey(nameof(SalaryGradeId))]
    public virtual SalaryGrade? SalaryGrade { get; set; }

    public bool IsActive { get; set; } = true;
}
