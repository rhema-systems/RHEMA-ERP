using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Entities.Base;

namespace ErpSystem.Core.Entities.Finance
{
    [Table("TaxRules")]
    public class TaxRule : TenantEntity
    {
        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Description { get; set; }

        public int Priority { get; set; } = 0;

        [Required]
        public Guid TaxGroupId { get; set; }

        // Conditions (Null means "Any")
        
        [StringLength(50)]
        public string? TransactionType { get; set; } // e.g., "SaleOfGoods", "Purchase"

        public Guid? ProductCategoryId { get; set; }

        [StringLength(50)]
        public string? CustomerType { get; set; } // e.g., "Corporate", "Individual", "Foreign"

        [StringLength(50)]
        public string? ServiceType { get; set; }

        public bool IsActive { get; set; } = true;

        [ForeignKey(nameof(TaxGroupId))]
        public virtual TaxGroup? TaxGroup { get; set; }
    }
}
