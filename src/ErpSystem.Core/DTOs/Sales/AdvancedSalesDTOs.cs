using System;
using System.Collections.Generic;

namespace ErpSystem.Core.DTOs.Sales
{
    public class CompetitorSummaryDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Industry { get; set; }
        public string ThreatLevel { get; set; } = string.Empty;
        public decimal EstimatedMarketShare { get; set; }
        public bool IsActive { get; set; }
        public int DealCount { get; set; }
        public int OpenDeals { get; set; }
        public int WonDeals { get; set; }
        public int LostDeals { get; set; }
        public decimal TotalDealValue { get; set; }
        public decimal OpenDealValue { get; set; }
        public decimal WinRate { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class CompetitorDetailDto : CompetitorSummaryDto
    {
        public string? Website { get; set; }
        public string? Description { get; set; }
        public string? Strengths { get; set; }
        public string? Weaknesses { get; set; }
        public string? KeyProducts { get; set; }
        public string? PricingStrategy { get; set; }
        public List<CompetitorDealDto> Deals { get; set; } = new();
    }

    public class CompetitorDealDto
    {
        public Guid Id { get; set; }
        public string? OpportunityName { get; set; }
        public string? CustomerName { get; set; }
        public string ThreatLevel { get; set; } = string.Empty;
        public string Outcome { get; set; } = string.Empty;
        public decimal DealValue { get; set; }
        public string? CompetitorProposal { get; set; }
        public string? OurDifferentiator { get; set; }
        public string? LessonsLearned { get; set; }
        public DateTime ReportedDate { get; set; }
        public DateTime? ResolvedDate { get; set; }
    }

    public class CreateCompetitorDto
    {
        public string Name { get; set; } = string.Empty;
        public string? Website { get; set; }
        public string? Industry { get; set; }
        public string? Description { get; set; }
        public string? Strengths { get; set; }
        public string? Weaknesses { get; set; }
        public string? KeyProducts { get; set; }
        public string? PricingStrategy { get; set; }
        public decimal EstimatedMarketShare { get; set; }
        public string ThreatLevel { get; set; } = "Medium";
    }

    public class CreateCompetitorDealDto
    {
        public Guid CompetitorId { get; set; }
        public Guid? OpportunityId { get; set; }
        public string? OpportunityName { get; set; }
        public string? CustomerName { get; set; }
        public string ThreatLevel { get; set; } = "Medium";
        public decimal DealValue { get; set; }
        public string? CompetitorProposal { get; set; }
        public string? OurDifferentiator { get; set; }
    }

    public class CompetitorAnalyticsDto
    {
        public int TotalCompetitors { get; set; }
        public int ActiveCompetitors { get; set; }
        public int HighThreatCompetitors { get; set; }
        public int CriticalThreatCompetitors { get; set; }
        public int OpenCompetitiveDeals { get; set; }
        public int WonDeals { get; set; }
        public int LostDeals { get; set; }
        public decimal TotalCompetitiveDealValue { get; set; }
        public decimal OpenCompetitiveDealValue { get; set; }
        public decimal WinRate { get; set; }
        public decimal AverageMarketShare { get; set; }
        public List<CompetitorThreatBreakdownDto> ThreatBreakdown { get; set; } = new();
        public List<CompetitorIndustryBreakdownDto> IndustryBreakdown { get; set; } = new();
        public List<CompetitorDealDto> RecentDeals { get; set; } = new();
    }

    public class CompetitorThreatBreakdownDto
    {
        public string ThreatLevel { get; set; } = string.Empty;
        public int CompetitorCount { get; set; }
        public int DealCount { get; set; }
        public decimal OpenDealValue { get; set; }
    }

    public class CompetitorIndustryBreakdownDto
    {
        public string Industry { get; set; } = string.Empty;
        public int CompetitorCount { get; set; }
        public decimal AverageMarketShare { get; set; }
    }

    // ── Sales Reporting DTOs ──

    public class SalesReportSummaryDto
    {
        public decimal TotalRevenue { get; set; }
        public int TotalOrders { get; set; }
        public int CompletedOrders { get; set; }
        public decimal AverageOrderValue { get; set; }
        public int TotalLeads { get; set; }
        public int ConvertedLeads { get; set; }
        public decimal ConversionRate { get; set; }
        public int OpenOpportunities { get; set; }
        public decimal PipelineValue { get; set; }
        public decimal WeightedPipelineValue { get; set; }
        public int ActiveCampaigns { get; set; }
        public decimal CampaignSpend { get; set; }
        public int ActiveAllocations { get; set; }
        public int ReservedAllocations { get; set; }
        public int SoldAllocations { get; set; }
        public int LeasedAllocations { get; set; }
        public int ReleasedAllocations { get; set; }
        public decimal AllocationTrackedValue { get; set; }
        public decimal ReservationExposure { get; set; }
        public int ActiveSalesAgreements { get; set; }
        public int AgreementsExpiringSoon { get; set; }
        public int PendingRefunds { get; set; }
        public decimal RefundExposure { get; set; }
        public int CompetitorOpenDeals { get; set; }
        public decimal CompetitorOpenDealValue { get; set; }
        public decimal CompetitorWinRate { get; set; }
        public int HighThreatCompetitors { get; set; }
        public List<TopProductDto> TopProducts { get; set; } = new();
        public List<TopSalesRepDto> TopSalesReps { get; set; } = new();
        public List<MonthlySalesDto> MonthlySales { get; set; } = new();
        public List<SalesMetricBreakdownDto> AllocationBySource { get; set; } = new();
        public List<SalesMetricBreakdownDto> AllocationByStatus { get; set; } = new();
    }

    public class SalesMetricBreakdownDto
    {
        public string Label { get; set; } = string.Empty;
        public int Count { get; set; }
        public decimal Amount { get; set; }
    }

    public class TopProductDto
    {
        public string ProductName { get; set; } = string.Empty;
        public int OrderCount { get; set; }
        public decimal TotalRevenue { get; set; }
    }

    public class TopSalesRepDto
    {
        public string SalesRepName { get; set; } = string.Empty;
        public int OrderCount { get; set; }
        public decimal TotalRevenue { get; set; }
        public decimal CommissionEarned { get; set; }
    }

    public class MonthlySalesDto
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public string MonthName { get; set; } = string.Empty;
        public decimal Revenue { get; set; }
        public int OrderCount { get; set; }
    }

    // ── Sales Journal Template DTOs ──

    public class SalesJournalTemplateSummaryDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string TransactionType { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public int LineCount { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class SalesJournalTemplateDetailDto : SalesJournalTemplateSummaryDto
    {
        public string? PostingBehavior { get; set; }
        public bool AutoPost { get; set; }
        public List<SalesJournalTemplateLineDto> Lines { get; set; } = new();
    }

    public class SalesJournalTemplateLineDto
    {
        public Guid Id { get; set; }
        public int Sequence { get; set; }
        public string AccountType { get; set; } = string.Empty;
        public string? AccountCode { get; set; }
        public string? AccountName { get; set; }
        public string EntryType { get; set; } = string.Empty; // Debit or Credit
        public string AmountSource { get; set; } = string.Empty; // TotalAmount, TaxAmount, DiscountAmount, etc.
        public decimal? FixedAmount { get; set; }
        public decimal? Percentage { get; set; }
        public string? Description { get; set; }
    }

    public class CreateSalesJournalTemplateDto
    {
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string TransactionType { get; set; } = string.Empty;
        public string? PostingBehavior { get; set; }
        public bool AutoPost { get; set; }
        public List<CreateSalesJournalTemplateLineDto> Lines { get; set; } = new();
    }

    public class CreateSalesJournalTemplateLineDto
    {
        public int Sequence { get; set; }
        public string AccountType { get; set; } = string.Empty;
        public string? AccountCode { get; set; }
        public string? AccountName { get; set; }
        public string EntryType { get; set; } = "Debit";
        public string AmountSource { get; set; } = "TotalAmount";
        public decimal? FixedAmount { get; set; }
        public decimal? Percentage { get; set; }
        public string? Description { get; set; }
    }
}
