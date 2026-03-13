using System.ComponentModel.DataAnnotations;

namespace ErpSystem.Core.DTOs.HR;

/// <summary>
/// Base (list/reference) DTO for a salary grade.
/// A salary grade groups multiple salary levels and becomes effective from a given date.
/// </summary>
public class SalaryGradeDto
{
    /// <summary>
    /// Unique identifier of the salary grade.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Tenant that owns this salary grade.
    /// </summary>
    public Guid TenantId { get; set; }

    /// <summary>
    /// Business code used to identify the salary grade (unique per tenant).
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// Display name of the salary grade.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Optional description to explain usage or scope of the grade.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Minimum salary allowed in this grade.
    /// </summary>
    public decimal MinSalary { get; set; }

    /// <summary>
    /// Maximum salary allowed in this grade.
    /// </summary>
    public decimal MaxSalary { get; set; }

    /// <summary>
    /// Whether the grade is currently active and available for assignment.
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// Date from which this grade is effective.
    /// </summary>
    public DateTime EffectiveDate { get; set; }

    /// <summary>
    /// Optional date when this grade stops being effective.
    /// </summary>
    public DateTime? EndDate { get; set; }

    /// <summary>
    /// Audit timestamp: when this record was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Audit timestamp: when this record was last updated.
    /// </summary>
    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    /// Optional audit field: user identifier that created the record.
    /// </summary>
    public Guid? CreatedById { get; set; }

    /// <summary>
    /// Optional audit field: user identifier that last modified the record.
    /// </summary>
    public Guid? LastModifiedById { get; set; }

    /// <summary>
    /// Optional audit field: username/display name that created the record.
    /// </summary>
    public string? CreatedBy { get; set; }

    /// <summary>
    /// Optional audit field: username/display name that last modified the record.
    /// </summary>
    public string? UpdatedBy { get; set; }
}

/// <summary>
/// Detail/read DTO for a salary grade.
/// Includes the nested hierarchy of levels and notches for UI display and GET endpoints.
/// </summary>
public class SalaryGradeDetailDto : SalaryGradeDto
{
    /// <summary>
    /// Salary levels within this grade.
    /// </summary>
    public ICollection<SalaryLevelDto> Levels { get; set; } = new List<SalaryLevelDto>();
}

/// <summary>
/// DTO used to create a salary grade.
/// Write model is flat by design (no nested creation of levels/notches).
/// </summary>
public class CreateSalaryGradeDto
{
    /// <summary>
    /// Tenant that will own the new salary grade.
    /// In a secured API, this is typically derived from the current tenant context.
    /// </summary>
    [Required]
    public Guid TenantId { get; set; }

    /// <summary>
    /// Business code used to identify the salary grade (unique per tenant).
    /// </summary>
    [Required]
    [StringLength(50)]
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// Display name of the salary grade.
    /// </summary>
    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Optional description to explain usage or scope of the grade.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Minimum salary allowed in this grade.
    /// </summary>
    [Required]
    [DataType(DataType.Currency)]
    [Range(typeof(decimal), "0", "79228162514264337593543950335")]
    public decimal MinSalary { get; set; }

    /// <summary>
    /// Maximum salary allowed in this grade.
    /// </summary>
    [Required]
    [DataType(DataType.Currency)]
    [Range(typeof(decimal), "0", "79228162514264337593543950335")]
    public decimal MaxSalary { get; set; }

    /// <summary>
    /// Whether the grade is active.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Date from which this grade is effective.
    /// </summary>
    [Required]
    [DataType(DataType.Date)]
    public DateTime EffectiveDate { get; set; }

    /// <summary>
    /// Optional date when this grade stops being effective.
    /// </summary>
    [DataType(DataType.Date)]
    public DateTime? EndDate { get; set; }
}

/// <summary>
/// DTO used to update an existing salary grade.
/// </summary>
public class UpdateSalaryGradeDto
{
    /// <summary>
    /// Unique identifier of the salary grade.
    /// </summary>
    [Required]
    public Guid Id { get; set; }

    /// <summary>
    /// Tenant that owns this salary grade.
    /// </summary>
    [Required]
    public Guid TenantId { get; set; }

    /// <summary>
    /// Business code used to identify the salary grade (unique per tenant).
    /// </summary>
    [Required]
    [StringLength(50)]
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// Display name of the salary grade.
    /// </summary>
    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Optional description to explain usage or scope of the grade.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Minimum salary allowed in this grade.
    /// </summary>
    [Required]
    [DataType(DataType.Currency)]
    [Range(typeof(decimal), "0", "79228162514264337593543950335")]
    public decimal MinSalary { get; set; }

    /// <summary>
    /// Maximum salary allowed in this grade.
    /// </summary>
    [Required]
    [DataType(DataType.Currency)]
    [Range(typeof(decimal), "0", "79228162514264337593543950335")]
    public decimal MaxSalary { get; set; }

    /// <summary>
    /// Whether the grade is active.
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// Date from which this grade is effective.
    /// </summary>
    [Required]
    [DataType(DataType.Date)]
    public DateTime EffectiveDate { get; set; }

    /// <summary>
    /// Optional date when this grade stops being effective.
    /// </summary>
    [DataType(DataType.Date)]
    public DateTime? EndDate { get; set; }
}

/// <summary>
/// Base (list/reference) DTO for a salary level.
/// A salary level belongs to a salary grade and defines the min/mid/max salary band.
/// </summary>
public class SalaryLevelDto
{
    /// <summary>
    /// Unique identifier of the salary level.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Tenant that owns this salary level.
    /// </summary>
    public Guid TenantId { get; set; }

    /// <summary>
    /// Parent salary grade identifier.
    /// </summary>
    public Guid SalaryGradeId { get; set; }

    /// <summary>
    /// Business code used to identify the salary level (unique per grade per tenant).
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// Display name of the salary level.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Minimum salary for this level.
    /// </summary>
    public decimal MinSalary { get; set; }

    /// <summary>
    /// Midpoint salary for this level.
    /// </summary>
    public decimal MidSalary { get; set; }

    /// <summary>
    /// Maximum salary for this level.
    /// </summary>
    public decimal MaxSalary { get; set; }

    /// <summary>
    /// Order within the grade (e.g., 1 = entry, 2 = intermediate).
    /// </summary>
    public int Sequence { get; set; }

    /// <summary>
    /// Whether the level is currently active and available for assignment.
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// Audit timestamp: when this record was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Audit timestamp: when this record was last updated.
    /// </summary>
    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    /// Optional audit field: user identifier that created the record.
    /// </summary>
    public Guid? CreatedById { get; set; }

    /// <summary>
    /// Optional audit field: user identifier that last modified the record.
    /// </summary>
    public Guid? LastModifiedById { get; set; }

    /// <summary>
    /// Optional audit field: username/display name that created the record.
    /// </summary>
    public string? CreatedBy { get; set; }

    /// <summary>
    /// Optional audit field: username/display name that last modified the record.
    /// </summary>
    public string? UpdatedBy { get; set; }
}

/// <summary>
/// Detail/read DTO for a salary level.
/// Includes the nested list of notches for UI display and GET endpoints.
/// </summary>
public class SalaryLevelDetailDto : SalaryLevelDto
{
    /// <summary>
    /// Salary notches within this level.
    /// </summary>
    public ICollection<SalaryNotchDto> Notches { get; set; } = new List<SalaryNotchDto>();
}

/// <summary>
/// DTO used to create a salary level.
/// Write model is flat by design.
/// </summary>
public class CreateSalaryLevelDto
{
    /// <summary>
    /// Tenant that will own the new salary level.
    /// In a secured API, this is typically derived from the current tenant context.
    /// </summary>
    [Required]
    public Guid TenantId { get; set; }

    /// <summary>
    /// Parent salary grade identifier.
    /// </summary>
    [Required]
    public Guid SalaryGradeId { get; set; }

    /// <summary>
    /// Business code used to identify the salary level.
    /// </summary>
    [Required]
    [StringLength(50)]
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// Display name of the salary level.
    /// </summary>
    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Minimum salary for this level.
    /// </summary>
    [Required]
    [DataType(DataType.Currency)]
    [Range(typeof(decimal), "0", "79228162514264337593543950335")]
    public decimal MinSalary { get; set; }

    /// <summary>
    /// Midpoint salary for this level.
    /// </summary>
    [Required]
    [DataType(DataType.Currency)]
    [Range(typeof(decimal), "0", "79228162514264337593543950335")]
    public decimal MidSalary { get; set; }

    /// <summary>
    /// Maximum salary for this level.
    /// </summary>
    [Required]
    [DataType(DataType.Currency)]
    [Range(typeof(decimal), "0", "79228162514264337593543950335")]
    public decimal MaxSalary { get; set; }

    /// <summary>
    /// Order within the grade (e.g., 1 = entry, 2 = intermediate).
    /// </summary>
    [Required]
    [Range(0, int.MaxValue)]
    public int Sequence { get; set; }

    /// <summary>
    /// Whether the level is active.
    /// </summary>
    public bool IsActive { get; set; } = true;
}

/// <summary>
/// DTO used to update an existing salary level.
/// </summary>
public class UpdateSalaryLevelDto
{
    /// <summary>
    /// Unique identifier of the salary level.
    /// </summary>
    [Required]
    public Guid Id { get; set; }

    /// <summary>
    /// Tenant that owns this salary level.
    /// </summary>
    [Required]
    public Guid TenantId { get; set; }

    /// <summary>
    /// Parent salary grade identifier.
    /// </summary>
    [Required]
    public Guid SalaryGradeId { get; set; }

    /// <summary>
    /// Business code used to identify the salary level.
    /// </summary>
    [Required]
    [StringLength(50)]
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// Display name of the salary level.
    /// </summary>
    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Minimum salary for this level.
    /// </summary>
    [Required]
    [DataType(DataType.Currency)]
    [Range(typeof(decimal), "0", "79228162514264337593543950335")]
    public decimal MinSalary { get; set; }

    /// <summary>
    /// Midpoint salary for this level.
    /// </summary>
    [Required]
    [DataType(DataType.Currency)]
    [Range(typeof(decimal), "0", "79228162514264337593543950335")]
    public decimal MidSalary { get; set; }

    /// <summary>
    /// Maximum salary for this level.
    /// </summary>
    [Required]
    [DataType(DataType.Currency)]
    [Range(typeof(decimal), "0", "79228162514264337593543950335")]
    public decimal MaxSalary { get; set; }

    /// <summary>
    /// Order within the grade.
    /// </summary>
    [Required]
    [Range(0, int.MaxValue)]
    public int Sequence { get; set; }

    /// <summary>
    /// Whether the level is active.
    /// </summary>
    public bool IsActive { get; set; }
}

/// <summary>
/// Base DTO for a salary notch.
/// A notch is a step within a salary level and represents a specific salary amount.
/// </summary>
public class SalaryNotchDto
{
    /// <summary>
    /// Unique identifier of the salary notch.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Tenant that owns this salary notch.
    /// </summary>
    public Guid TenantId { get; set; }

    /// <summary>
    /// Parent salary level identifier.
    /// </summary>
    public Guid SalaryLevelId { get; set; }

    /// <summary>
    /// Sequential notch number within the level (e.g., 1, 2, 3...).
    /// </summary>
    public int NotchNumber { get; set; }

    /// <summary>
    /// Fixed salary amount associated with this notch.
    /// </summary>
    public decimal SalaryAmount { get; set; }

    /// <summary>
    /// Whether the notch is currently active.
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// Audit timestamp: when this record was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Audit timestamp: when this record was last updated.
    /// </summary>
    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    /// Optional audit field: user identifier that created the record.
    /// </summary>
    public Guid? CreatedById { get; set; }

    /// <summary>
    /// Optional audit field: user identifier that last modified the record.
    /// </summary>
    public Guid? LastModifiedById { get; set; }

    /// <summary>
    /// Optional audit field: username/display name that created the record.
    /// </summary>
    public string? CreatedBy { get; set; }

    /// <summary>
    /// Optional audit field: username/display name that last modified the record.
    /// </summary>
    public string? UpdatedBy { get; set; }
}

/// <summary>
/// DTO used to create a salary notch.
/// Write model is flat by design.
/// </summary>
public class CreateSalaryNotchDto
{
    /// <summary>
    /// Tenant that will own the new salary notch.
    /// In a secured API, this is typically derived from the current tenant context.
    /// </summary>
    [Required]
    public Guid TenantId { get; set; }

    /// <summary>
    /// Parent salary level identifier.
    /// </summary>
    [Required]
    public Guid SalaryLevelId { get; set; }

    /// <summary>
    /// Sequential notch number within the level (e.g., 1, 2, 3...).
    /// </summary>
    [Required]
    [Range(1, int.MaxValue)]
    public int NotchNumber { get; set; }

    /// <summary>
    /// Fixed salary amount associated with this notch.
    /// </summary>
    [Required]
    [DataType(DataType.Currency)]
    [Range(typeof(decimal), "0", "79228162514264337593543950335")]
    public decimal SalaryAmount { get; set; }

    /// <summary>
    /// Whether the notch is active.
    /// </summary>
    public bool IsActive { get; set; } = true;
}

/// <summary>
/// DTO used to resequence salary levels within a grade.
/// </summary>
public class ResequenceSalaryLevelsDto
{
    /// <summary>
    /// Tenant that owns the grade/levels.
    /// In a secured API, this is typically derived from the current tenant context.
    /// </summary>
    [Required]
    public Guid TenantId { get; set; }

    /// <summary>
    /// Salary grade whose levels are being resequenced.
    /// </summary>
    [Required]
    public Guid SalaryGradeId { get; set; }

    /// <summary>
    /// Ordered list of salary level identifiers.
    /// The first item will be sequence 1, then 2, etc.
    /// </summary>
    [Required]
    [MinLength(1)]
    public List<Guid> OrderedLevelIds { get; set; } = new();
}

/// <summary>
/// DTO used to update an existing salary notch.
/// </summary>
public class UpdateSalaryNotchDto
{
    /// <summary>
    /// Unique identifier of the salary notch.
    /// </summary>
    [Required]
    public Guid Id { get; set; }

    /// <summary>
    /// Tenant that owns this salary notch.
    /// </summary>
    [Required]
    public Guid TenantId { get; set; }

    /// <summary>
    /// Parent salary level identifier.
    /// </summary>
    [Required]
    public Guid SalaryLevelId { get; set; }

    /// <summary>
    /// Sequential notch number within the level (e.g., 1, 2, 3...).
    /// </summary>
    [Required]
    [Range(1, int.MaxValue)]
    public int NotchNumber { get; set; }

    /// <summary>
    /// Fixed salary amount associated with this notch.
    /// </summary>
    [Required]
    [DataType(DataType.Currency)]
    [Range(typeof(decimal), "0", "79228162514264337593543950335")]
    public decimal SalaryAmount { get; set; }

    /// <summary>
    /// Whether the notch is active.
    /// </summary>
    public bool IsActive { get; set; }
}
