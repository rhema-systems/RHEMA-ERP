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

        public SalesReportingService(
            IGenericRepository<SalesOrder> salesOrderRepo,
            IGenericRepository<Lead> leadRepo,
            IGenericRepository<Opportunity> opRepo,
            IGenericRepository<Campaign> campaignRepo)
        {
            _salesOrderRepo = salesOrderRepo;
            _leadRepo = leadRepo;
            _opRepo = opRepo;
            _campaignRepo = campaignRepo;
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
                MonthlySales = monthlySales
            };
        }
    }
}
