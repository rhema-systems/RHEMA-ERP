using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Entities.Sales
{
    /// <summary>
    /// Sales forecast header — captures a forecast for a specific period
    /// </summary>
    public class SalesForecast : TenantEntity
    {
        [Required, MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(1000)]
        public string? Description { get; set; }

        public SalesForecastMethod Method { get; set; } = SalesForecastMethod.Manual;
        public SalesForecastStatus Status { get; set; } = SalesForecastStatus.Draft;

        public DateTime PeriodStart { get; set; }
        public DateTime PeriodEnd { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalForecastAmount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalActualAmount { get; set; }

        [NotMapped]
        public decimal Variance => TotalActualAmount - TotalForecastAmount;

        [NotMapped]
        public decimal VariancePercentage => TotalForecastAmount != 0
            ? Math.Round((Variance / TotalForecastAmount) * 100, 2) : 0;

        public string? OwnerId { get; set; }

        [MaxLength(200)]
        public string? OwnerName { get; set; }

        public DateTime? SubmittedDate { get; set; }
        public DateTime? ApprovedDate { get; set; }
        public string? ApprovedBy { get; set; }

        [MaxLength(1000)]
        public string? Notes { get; set; }

        public ICollection<SalesForecastLine> Lines { get; set; } = new List<SalesForecastLine>();
    }

    /// <summary>
    /// Individual forecast line item — per product, category, or sales rep
    /// </summary>
    public class SalesForecastLine : TenantEntity, ErpSystem.Core.Interfaces.Inventory.ICommercialQuantityEvidenceLine
    {
        public Guid SalesForecastId { get; set; }
        public SalesForecast SalesForecast { get; set; } = null!;

        [MaxLength(200)]
        public string? Category { get; set; }

        [MaxLength(200)]
        public string? ProductName { get; set; }

        public Guid? ProductId { get; set; }

        [MaxLength(200)]
        public string? SalesRepName { get; set; }

        public string? SalesRepId { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal ForecastQuantity { get; set; }

        [MaxLength(50)] public string? Unit { get; set; }
        public Guid? UnitOfMeasureId { get; set; }
        [MaxLength(20)] public string? UnitOfMeasureCodeSnapshot { get; set; }
        public int? UnitOfMeasureDecimalPlacesSnapshot { get; set; }
        [Column(TypeName = "decimal(18,6)")] public decimal? UnitOfMeasureRoundingIncrementSnapshot { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal ForecastAmount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal ActualQuantity { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal ActualAmount { get; set; }

        [NotMapped]
        public decimal Variance => ActualAmount - ForecastAmount;

        [MaxLength(500)]
        public string? Notes { get; set; }
    }
}
