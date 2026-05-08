using System;
using System.ComponentModel.DataAnnotations;

namespace ErpSystem.Shared.DTOs.Finance
{
    public class TaxRuleDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int Priority { get; set; }
        public Guid TaxGroupId { get; set; }
        public string TaxGroupName { get; set; } = string.Empty; // For display
        public string? TransactionType { get; set; }
        public Guid? ProductCategoryId { get; set; }
        public string? ProductCategoryName { get; set; } // For display
        public string? CustomerType { get; set; }
        public string? ServiceType { get; set; }
        public bool IsActive { get; set; }
    }

    public class CreateTaxRuleDto
    {
        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Description { get; set; }

        public int Priority { get; set; }

        [Required]
        public Guid TaxGroupId { get; set; }

        [StringLength(50)]
        public string? TransactionType { get; set; }

        public Guid? ProductCategoryId { get; set; }

        [StringLength(50)]
        public string? CustomerType { get; set; }

        [StringLength(50)]
        public string? ServiceType { get; set; }

        public bool IsActive { get; set; } = true;
    }

    public class UpdateTaxRuleDto : CreateTaxRuleDto
    {
    }
}
