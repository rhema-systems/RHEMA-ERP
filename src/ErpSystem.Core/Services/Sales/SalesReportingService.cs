using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ErpSystem.Core.DTOs.Sales;
using ErpSystem.Core.Entities.Sales;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Sales;

namespace ErpSystem.Core.Services.Sales
{
    public class SalesReportingService : ISalesReportingService
    {
        private readonly IGenericRepository<SalesOrder> _salesOrderRepo;
        private readonly IGenericRepository<Lead> _leadRepo;
        private readonly IGenericRepository<Opportunity> _opRepo;
        private readonly IGenericRepository<Campaign> _campaignRepo;
        private readonly IGenericRepository<SalesAllocation> _allocationRepo;
        private readonly IGenericRepository<SalesAgreement> _agreementRepo;
        private readonly IGenericRepository<Refund> _refundRepo;
        private readonly IGenericRepository<Competitor> _competitorRepo;

        public SalesReportingService(
            IGenericRepository<SalesOrder> salesOrderRepo,
            IGenericRepository<Lead> leadRepo,
            IGenericRepository<Opportunity> opRepo,
            IGenericRepository<Campaign> campaignRepo,
            IGenericRepository<SalesAllocation> allocationRepo,
            IGenericRepository<SalesAgreement> agreementRepo,
            IGenericRepository<Refund> refundRepo,
            IGenericRepository<Competitor> competitorRepo)
        {
            _salesOrderRepo = salesOrderRepo;
            _leadRepo = leadRepo;
            _opRepo = opRepo;
            _campaignRepo = campaignRepo;
            _allocationRepo = allocationRepo;
            _agreementRepo = agreementRepo;
            _refundRepo = refundRepo;
            _competitorRepo = competitorRepo;
        }

        public async Task<SalesReportSummaryDto> GetSalesSummaryAsync(DateTime? from = null, DateTime? to = null)
        {
            var startDate = from ?? DateTime.UtcNow.AddMonths(-12);
            var endDate = to ?? DateTime.UtcNow;

            var ordersQuery = _salesOrderRepo.GetQueryable().Where(o => !o.IsDeleted && o.DocumentDate >= startDate && o.DocumentDate <= endDate);
            var orders = await ordersQuery.ToListAsync();
            var completedOrders = orders.Where(o => o.OrderStatus == SalesOrderStatus.Closed).ToList();
            var totalRevenue = completedOrders.Sum(o => o.TotalAmount);

            var leadsQuery = _leadRepo.GetQueryable().Where(l => !l.IsDeleted);
            var totalLeads = await leadsQuery.CountAsync();
            var convertedLeads = await leadsQuery.CountAsync(l => l.LeadStatus == "Converted");

            var openOps = await _opRepo.GetQueryable()
                .Where(o => !o.IsDeleted && o.Stage != "Closed Won" && o.Stage != "Closed Lost")
                .ToListAsync();

            var activeC = await _campaignRepo.GetQueryable()
                .Where(c => !c.IsDeleted && c.CampaignStatus == "Active").ToListAsync();

            var allocations = await _allocationRepo.GetQueryable()
                .Where(a => !a.IsDeleted)
                .ToListAsync();
            var activeAllocationStatuses = new[] { "Reserved", "PendingApproval", "Approved", "Allocated", "Sold", "Leased" };
            var releaseStatuses = new[] { "Released", "Cancelled", "Expired", "Rejected" };
            var activeAllocations = allocations
                .Where(a => activeAllocationStatuses.Contains(a.Status))
                .ToList();
            var reservationStatuses = new[] { "Reserved", "PendingApproval", "Approved" };

            var today = DateTime.UtcNow.Date;
            var agreements = await _agreementRepo.GetQueryable()
                .Where(a => !a.IsDeleted)
                .ToListAsync();
            var activeAgreementStatuses = new[] { SalesAgreementStatus.Active, SalesAgreementStatus.Expiring };

            var refunds = await _refundRepo.GetQueryable()
                .Where(r => !r.IsDeleted && r.DocumentDate >= startDate && r.DocumentDate <= endDate)
                .ToListAsync();
            var refundExposureStatuses = new[] { RefundStatus.PendingApproval, RefundStatus.Approved, RefundStatus.Processing };

            var competitors = await _competitorRepo.GetQueryable()
                .Include(c => c.Deals)
                .Where(c => !c.IsDeleted)
                .ToListAsync();
            var competitorDeals = competitors
                .SelectMany(c => c.Deals.Where(d => !d.IsDeleted))
                .ToList();
            var competitorWins = competitorDeals.Count(d => d.Outcome == CompetitorDealOutcome.Won);
            var competitorLosses = competitorDeals.Count(d => d.Outcome == CompetitorDealOutcome.Lost);
            var resolvedCompetitorDeals = competitorWins + competitorLosses;

            var monthlySales = completedOrders
                .GroupBy(o => new { o.DocumentDate.Year, o.DocumentDate.Month })
                .Select(g => new MonthlySalesDto
                {
                    Year = g.Key.Year, Month = g.Key.Month,
                    MonthName = CultureInfo.CurrentCulture.DateTimeFormat.GetAbbreviatedMonthName(g.Key.Month),
                    Revenue = g.Sum(o => o.TotalAmount),
                    OrderCount = g.Count()
                })
                .OrderBy(m => m.Year).ThenBy(m => m.Month).ToList();

            return new SalesReportSummaryDto
            {
                TotalRevenue = totalRevenue,
                TotalOrders = orders.Count,
                CompletedOrders = completedOrders.Count,
                AverageOrderValue = completedOrders.Count > 0 ? totalRevenue / completedOrders.Count : 0,
                TotalLeads = totalLeads,
                ConvertedLeads = convertedLeads,
                ConversionRate = totalLeads > 0 ? Math.Round((decimal)convertedLeads / totalLeads * 100, 1) : 0,
                OpenOpportunities = openOps.Count,
                PipelineValue = openOps.Sum(o => o.Amount),
                WeightedPipelineValue = openOps.Sum(o => o.Amount * o.Probability / 100),
                ActiveCampaigns = activeC.Count,
                CampaignSpend = activeC.Sum(c => c.ActualCost),
                ActiveAllocations = activeAllocations.Count,
                ReservedAllocations = allocations.Count(a => a.Status == "Reserved"),
                SoldAllocations = allocations.Count(a => a.Status == "Sold"),
                LeasedAllocations = allocations.Count(a => a.Status == "Leased"),
                ReleasedAllocations = allocations.Count(a => releaseStatuses.Contains(a.Status)),
                AllocationTrackedValue = activeAllocations.Sum(a => a.AgreedValue ?? a.EstimatedValue ?? 0),
                ReservationExposure = allocations
                    .Where(a => reservationStatuses.Contains(a.Status))
                    .Sum(a => a.AgreedValue ?? a.EstimatedValue ?? 0),
                ActiveSalesAgreements = agreements.Count(a => activeAgreementStatuses.Contains(a.AgreementStatus)),
                AgreementsExpiringSoon = agreements.Count(a =>
                    a.EndDate.HasValue
                    && a.EndDate.Value.Date >= today
                    && a.EndDate.Value.Date <= today.AddDays(a.ExpiryWarningDays > 0 ? a.ExpiryWarningDays : 30)),
                PendingRefunds = refunds.Count(r => refundExposureStatuses.Contains(r.RefundStatus)),
                RefundExposure = refunds
                    .Where(r => refundExposureStatuses.Contains(r.RefundStatus))
                    .Sum(r => r.RefundAmount),
                CompetitorOpenDeals = competitorDeals.Count(d => d.Outcome == CompetitorDealOutcome.InProgress),
                CompetitorOpenDealValue = competitorDeals
                    .Where(d => d.Outcome == CompetitorDealOutcome.InProgress)
                    .Sum(d => d.DealValue),
                CompetitorWinRate = resolvedCompetitorDeals == 0 ? 0 : Math.Round((decimal)competitorWins / resolvedCompetitorDeals * 100, 2),
                HighThreatCompetitors = competitors.Count(c => c.ThreatLevel is CompetitorThreatLevel.High or CompetitorThreatLevel.Critical),
                MonthlySales = monthlySales,
                AllocationBySource = activeAllocations
                    .GroupBy(a => string.IsNullOrWhiteSpace(a.SourceCode) ? a.SourceType : a.SourceCode)
                    .OrderByDescending(group => group.Sum(a => a.AgreedValue ?? a.EstimatedValue ?? 0))
                    .Take(8)
                    .Select(group => new SalesMetricBreakdownDto
                    {
                        Label = group.Key ?? "Unspecified",
                        Count = group.Count(),
                        Amount = group.Sum(a => a.AgreedValue ?? a.EstimatedValue ?? 0)
                    })
                    .ToList(),
                AllocationByStatus = allocations
                    .GroupBy(a => a.Status)
                    .OrderByDescending(group => group.Count())
                    .Select(group => new SalesMetricBreakdownDto
                    {
                        Label = group.Key,
                        Count = group.Count(),
                        Amount = group.Sum(a => a.AgreedValue ?? a.EstimatedValue ?? 0)
                    })
                    .ToList()
            };
        }
    }
}
