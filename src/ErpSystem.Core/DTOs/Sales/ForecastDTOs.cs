using System;
using System.Collections.Generic;

namespace ErpSystem.Core.DTOs.Sales
{
    public class ForecastSummaryDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Method { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime PeriodStart { get; set; }
        public DateTime PeriodEnd { get; set; }
        public decimal TotalForecastAmount { get; set; }
        public decimal TotalActualAmount { get; set; }
        public decimal Variance { get; set; }
        public decimal VariancePercentage { get; set; }
        public string? OwnerName { get; set; }
        public int LineCount { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class ForecastDetailDto : ForecastSummaryDto
    {
        public string? OwnerId { get; set; }
        public string? Description { get; set; }
        public DateTime? SubmittedDate { get; set; }
        public DateTime? ApprovedDate { get; set; }
        public string? ApprovedBy { get; set; }
        public string? Notes { get; set; }
        public List<ForecastLineDto> Lines { get; set; } = new();
    }

    public class ForecastLineDto
    {
        public Guid Id { get; set; }
        public string? Category { get; set; }
        public string? ProductName { get; set; }
        public string? SalesRepName { get; set; }
        public decimal ForecastQuantity { get; set; }
        public decimal ForecastAmount { get; set; }
        public decimal ActualQuantity { get; set; }
        public decimal ActualAmount { get; set; }
        public decimal Variance { get; set; }
        public string? Notes { get; set; }
    }

    public class CreateForecastDto
    {
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string Method { get; set; } = "Manual";
        public DateTime PeriodStart { get; set; }
        public DateTime PeriodEnd { get; set; }
        public string? OwnerId { get; set; }
        public string? Notes { get; set; }
        public List<CreateForecastLineDto> Lines { get; set; } = new();
    }

    public class CreateForecastLineDto
    {
        public string? Category { get; set; }
        public string? ProductName { get; set; }
        public Guid? ProductId { get; set; }
        public string? SalesRepName { get; set; }
        public string? SalesRepId { get; set; }
        public decimal ForecastQuantity { get; set; }
        public decimal ForecastAmount { get; set; }
        public string? Notes { get; set; }
    }
}
