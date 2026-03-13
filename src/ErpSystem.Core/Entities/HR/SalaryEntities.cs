using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Entities;

public class SalaryGrade : TenantEntity
{
    [Required]
    [StringLength(50)]
    public string Code { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    [Column(TypeName = "decimal(18,2)")]
    public decimal MinSalary { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal MaxSalary { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime EffectiveDate { get; set; }

    public DateTime? EndDate { get; set; }

    public virtual ICollection<SalaryLevel> Levels { get; set; } = new List<SalaryLevel>();
}

public class SalaryLevel : TenantEntity
{
    [Required]
    public Guid SalaryGradeId { get; set; } // Foreign key to SalaryGrade

    [ForeignKey(nameof(SalaryGradeId))]
    public virtual SalaryGrade Grade { get; set; } = null!;

    [Required]
    [StringLength(50)]
    public string Code { get; set; } = string.Empty;
    
    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [Column(TypeName = "decimal(18,2)")]
    public decimal MinSalary { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal MidSalary { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal MaxSalary { get; set; }

    public int Sequence { get; set; } // Order within the grade (e.g., 1 for entry, 2 for mid)

    public bool IsActive { get; set; } = true;

    public virtual ICollection<SalaryNotch> Notches { get; set; } = new List<SalaryNotch>();
}

public class SalaryNotch : TenantEntity
{
    [Required]
    public Guid SalaryLevelId { get; set; }

    [ForeignKey(nameof(SalaryLevelId))]
    public virtual SalaryLevel Level { get; set; } = null!;

    [Required]
    public int NotchNumber { get; set; } // e.g., 1, 2, 3... (sequential within level)

    [Column(TypeName = "decimal(18,2)")]
    public decimal SalaryAmount { get; set; } // Fixed amount at this notch

    public bool IsActive { get; set; } = true;
}
