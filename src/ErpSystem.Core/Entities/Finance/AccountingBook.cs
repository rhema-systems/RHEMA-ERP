using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.Entities;

namespace ErpSystem.Core.Entities.Finance
{
    /// <summary>
    /// Tenant-level accounting book used for parallel reporting and postings.
    /// Examples: IFRS, Local Statutory, Management.
    /// </summary>
    public class AccountingBook : TenantEntity
    {
        [Required]
        [MaxLength(20)]
        public string Code { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? Description { get; set; }

        [MaxLength(50)]
        public string Purpose { get; set; } = "Reporting";

        public bool IsActive { get; set; } = true;

        public bool IsDefault { get; set; }

        public bool AllowsPosting { get; set; } = true;

        public bool IsSystemDefined { get; set; } = true;

        public int SortOrder { get; set; }

        public virtual ICollection<AccountAccountingBook> AccountMappings { get; set; } = new List<AccountAccountingBook>();
    }
}
