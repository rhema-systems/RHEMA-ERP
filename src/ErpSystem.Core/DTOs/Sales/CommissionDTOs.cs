using System;
using System.Collections.Generic;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.DTOs.Sales
{
    // ── Commission Rule DTOs ──

    public class CommissionRuleSummaryDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string CommissionType { get; set; } = string.Empty;
        public decimal Rate { get; set; }
        public string? SalesRepId { get; set; }
        public decimal MinimumSaleAmount { get; set; }
        public decimal MaximumCommission { get; set; }
        public bool IsActive { get; set; }
        public DateTime EffectiveFrom { get; set; }
        public DateTime? EffectiveTo { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class CommissionRuleDetailDto : CommissionRuleSummaryDto
    {
        public string? Description { get; set; }
        public string? TierDefinitions { get; set; }
        public Guid? ProductId { get; set; }
        public Guid? CustomerId { get; set; }
    }

    public class CreateCommissionRuleDto
    {
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string CommissionType { get; set; } = "FlatPercentage";
        public decimal Rate { get; set; }
        public string? TierDefinitions { get; set; }
        public string? SalesRepId { get; set; }
        public Guid? ProductId { get; set; }
        public Guid? CustomerId { get; set; }
        public decimal MinimumSaleAmount { get; set; }
        public decimal MaximumCommission { get; set; }
        public DateTime? EffectiveFrom { get; set; }
        public DateTime? EffectiveTo { get; set; }
    }

    // ── Commission Statement DTOs ──

    public class CommissionStatementSummaryDto
    {
        public Guid Id { get; set; }
        public string StatementNumber { get; set; } = string.Empty;
        public string SalesRepName { get; set; } = string.Empty;
        public DateTime PeriodStart { get; set; }
        public DateTime PeriodEnd { get; set; }
        public decimal TotalSales { get; set; }
        public decimal TotalCommission { get; set; }
        public decimal Adjustments { get; set; }
        public decimal NetCommission { get; set; }
        public string Status { get; set; } = string.Empty;
        public int LineCount { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class CommissionStatementDetailDto : CommissionStatementSummaryDto
    {
        public string SalesRepId { get; set; } = string.Empty;
        public DateTime? ApprovedDate { get; set; }
        public string? ApprovedBy { get; set; }
        public DateTime? PaidDate { get; set; }
        public string? PaymentReference { get; set; }
        public string? Notes { get; set; }
        public List<CommissionStatementLineDto> Lines { get; set; } = new();
    }

    public class CommissionStatementLineDto
    {
        public Guid Id { get; set; }
        public string? SalesOrderNumber { get; set; }
        public string? CustomerName { get; set; }
        public decimal SaleAmount { get; set; }
        public decimal CommissionRate { get; set; }
        public decimal CommissionAmount { get; set; }
        public string? Description { get; set; }
        public DateTime TransactionDate { get; set; }
    }

    public class GenerateStatementDto
    {
        public string SalesRepId { get; set; } = string.Empty;
        public DateTime PeriodStart { get; set; }
        public DateTime PeriodEnd { get; set; }
    }
}
