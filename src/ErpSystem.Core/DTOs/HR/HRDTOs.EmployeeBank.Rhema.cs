// Restored during HRApi Wave-1 port: RHEMA hrdev EmployeeBank/EmployeeBankBranch DTOs.
// HRApi's HRDTOs.cs uses Bank/BankBranch naming; these preserve the RHEMA EmployeeBank naming
// used by the kept EmployeeBankService / EmployeeBankBranchService / controllers.
using System.ComponentModel.DataAnnotations;

namespace ErpSystem.Core.DTOs.HR;

public class EmployeeBankDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? SwiftCode { get; set; }
    public Guid? CountryId { get; set; }
    public string? CountryName { get; set; }
    public bool IsActive { get; set; }
    public int BranchCount { get; set; }
}

public class CreateEmployeeBankDto
{
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [MaxLength(20)]
    public string Code { get; set; } = string.Empty;

    [MaxLength(20)]
    public string? SwiftCode { get; set; }

    public Guid? CountryId { get; set; }

    public bool IsActive { get; set; } = true;
}

public class UpdateEmployeeBankDto
{
    [Required]
    public Guid Id { get; set; }

    [MaxLength(200)]
    public string? Name { get; set; }

    [MaxLength(20)]
    public string? Code { get; set; }

    [MaxLength(20)]
    public string? SwiftCode { get; set; }

    public Guid? CountryId { get; set; }

    public bool? IsActive { get; set; }
}

public class EmployeeBankBranchDto
{
    public Guid Id { get; set; }
    public Guid BankId { get; set; }
    public string BankName { get; set; } = string.Empty;
    public string BankCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Code { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public Guid? CountryId { get; set; }
    public string? CountryName { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Email { get; set; }
    public bool IsActive { get; set; }
}

public class CreateEmployeeBankBranchDto
{
    [Required]
    public Guid BankId { get; set; }

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(20)]
    public string? Code { get; set; }

    [MaxLength(500)]
    public string? Address { get; set; }

    [MaxLength(100)]
    public string? City { get; set; }

    public Guid? CountryId { get; set; }

    [MaxLength(50)]
    public string? PhoneNumber { get; set; }

    [MaxLength(200)]
    [EmailAddress]
    public string? Email { get; set; }

    public bool IsActive { get; set; } = true;
}

public class UpdateEmployeeBankBranchDto
{
    [Required]
    public Guid Id { get; set; }

    [MaxLength(200)]
    public string? Name { get; set; }

    [MaxLength(20)]
    public string? Code { get; set; }

    [MaxLength(500)]
    public string? Address { get; set; }

    [MaxLength(100)]
    public string? City { get; set; }

    public Guid? CountryId { get; set; }

    [MaxLength(50)]
    public string? PhoneNumber { get; set; }

    [MaxLength(200)]
    [EmailAddress]
    public string? Email { get; set; }

    public bool? IsActive { get; set; }
}
