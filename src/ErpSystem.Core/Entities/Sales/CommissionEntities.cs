using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Entities.Sales
{
    /// <summary>
    /// Commission rule defining how commissions are calculated for a sales rep or product
    /// </summary>
    public class CommissionRule : BaseEntity
    {
        [Required, MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(1000)]
        public string? Description { get; set; }

        public CommissionType CommissionType { get; set; } = CommissionType.FlatPercentage;

        /// <summary>
        /// Flat percentage rate (used when CommissionType = FlatPercentage)
        /// </summary>
        [Column(TypeName = "decimal(5,2)")]
        public decimal Rate { get; set; }

        /// <summary>
        /// JSON-encoded tier definitions (used when CommissionType = Tiered)
        /// </summary>
        public string? TierDefinitions { get; set; }

        public string? SalesRepId { get; set; }
        public Guid? ProductId { get; set; }
        public Guid? CustomerId { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal MinimumSaleAmount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal MaximumCommission { get; set; }

        public bool IsActive { get; set; } = true;
        public DateTime EffectiveFrom { get; set; } = DateTime.UtcNow;
        public DateTime? EffectiveTo { get; set; }
    }

    /// <summary>
    /// Commission statement for a period — collects all commissions earned by a sales rep
    /// </summary>
    public class CommissionStatement : BaseEntity
    {
        [Required, MaxLength(50)]
        public string StatementNumber { get; set; } = string.Empty;

        [Required]
        public string SalesRepId { get; set; } = string.Empty;

        [MaxLength(200)]
        public string? SalesRepName { get; set; }

        public DateTime PeriodStart { get; set; }
        public DateTime PeriodEnd { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalSales { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalCommission { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Adjustments { get; set; }

        [NotMapped]
        public decimal NetCommission => TotalCommission + Adjustments;

        public CommissionStatementStatus Status { get; set; } = CommissionStatementStatus.Draft;

        public DateTime? ApprovedDate { get; set; }
        public string? ApprovedBy { get; set; }
        public DateTime? PaidDate { get; set; }
        public string? PaymentReference { get; set; }

        [MaxLength(1000)]
        public string? Notes { get; set; }

        public ICollection<CommissionStatementLine> Lines { get; set; } = new List<CommissionStatementLine>();
    }

    /// <summary>
    /// Individual commission line item within a statement
    /// </summary>
    public class CommissionStatementLine : BaseEntity
    {
        public Guid CommissionStatementId { get; set; }
        public CommissionStatement CommissionStatement { get; set; } = null!;

        public Guid? CommissionRuleId { get; set; }
        public CommissionRule? CommissionRule { get; set; }

        public Guid? SalesOrderId { get; set; }

        [MaxLength(100)]
        public string? SalesOrderNumber { get; set; }

        [MaxLength(200)]
        public string? CustomerName { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal SaleAmount { get; set; }

        [Column(TypeName = "decimal(5,2)")]
        public decimal CommissionRate { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal CommissionAmount { get; set; }

        [MaxLength(500)]
        public string? Description { get; set; }

        public DateTime TransactionDate { get; set; }
    }
}
