using System;
using System.Collections.Generic;
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
    public class CompetitorService : ICompetitorService
    {
        private readonly IGenericRepository<Competitor> _competitorRepo;
        private readonly IGenericRepository<CompetitorDeal> _dealRepo;

        public CompetitorService(
            IGenericRepository<Competitor> competitorRepo,
            IGenericRepository<CompetitorDeal> dealRepo)
        {
            _competitorRepo = competitorRepo;
            _dealRepo = dealRepo;
        }

        public async Task<(IEnumerable<CompetitorSummaryDto> Items, int TotalCount)> GetCompetitorsAsync(int page, int pageSize, string? search = null, string? threatLevel = null)
        {
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 100);

            var query = _competitorRepo.GetQueryable().Include(c => c.Deals).Where(c => !c.IsDeleted);
            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(c => c.Name.Contains(search) || (c.Industry != null && c.Industry.Contains(search)));
            if (!string.IsNullOrWhiteSpace(threatLevel) && Enum.TryParse<CompetitorThreatLevel>(threatLevel, true, out var tl))
                query = query.Where(c => c.ThreatLevel == tl);

            var totalCount = await query.CountAsync();
            var competitors = await query
                .OrderByDescending(c => c.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (competitors.Select(MapSummary), totalCount);
        }

        public async Task<CompetitorAnalyticsDto> GetCompetitorAnalyticsAsync()
        {
            var competitors = await _competitorRepo.GetQueryable()
                .Include(c => c.Deals)
                .Where(c => !c.IsDeleted)
                .ToListAsync();

            var deals = competitors
                .SelectMany(c => c.Deals.Where(d => !d.IsDeleted))
                .ToList();
            var wonDeals = deals.Count(d => d.Outcome == CompetitorDealOutcome.Won);
            var lostDeals = deals.Count(d => d.Outcome == CompetitorDealOutcome.Lost);
            var resolvedDeals = wonDeals + lostDeals;

            return new CompetitorAnalyticsDto
            {
                TotalCompetitors = competitors.Count,
                ActiveCompetitors = competitors.Count(c => c.IsActive),
                HighThreatCompetitors = competitors.Count(c => c.ThreatLevel == CompetitorThreatLevel.High),
                CriticalThreatCompetitors = competitors.Count(c => c.ThreatLevel == CompetitorThreatLevel.Critical),
                OpenCompetitiveDeals = deals.Count(d => d.Outcome == CompetitorDealOutcome.InProgress),
                WonDeals = wonDeals,
                LostDeals = lostDeals,
                TotalCompetitiveDealValue = deals.Sum(d => d.DealValue),
                OpenCompetitiveDealValue = deals
                    .Where(d => d.Outcome == CompetitorDealOutcome.InProgress)
                    .Sum(d => d.DealValue),
                WinRate = resolvedDeals == 0 ? 0 : Math.Round((decimal)wonDeals / resolvedDeals * 100, 2),
                AverageMarketShare = competitors.Count == 0 ? 0 : Math.Round(competitors.Average(c => c.EstimatedMarketShare), 2),
                ThreatBreakdown = competitors
                    .GroupBy(c => c.ThreatLevel)
                    .OrderByDescending(group => group.Key)
                    .Select(group => new CompetitorThreatBreakdownDto
                    {
                        ThreatLevel = group.Key.ToString(),
                        CompetitorCount = group.Count(),
                        DealCount = group.SelectMany(c => c.Deals.Where(d => !d.IsDeleted)).Count(),
                        OpenDealValue = group.SelectMany(c => c.Deals.Where(d => !d.IsDeleted && d.Outcome == CompetitorDealOutcome.InProgress)).Sum(d => d.DealValue)
                    })
                    .ToList(),
                IndustryBreakdown = competitors
                    .GroupBy(c => string.IsNullOrWhiteSpace(c.Industry) ? "Unspecified" : c.Industry)
                    .OrderByDescending(group => group.Count())
                    .Take(8)
                    .Select(group => new CompetitorIndustryBreakdownDto
                    {
                        Industry = group.Key!,
                        CompetitorCount = group.Count(),
                        AverageMarketShare = Math.Round(group.Average(c => c.EstimatedMarketShare), 2)
                    })
                    .ToList(),
                RecentDeals = deals
                    .OrderByDescending(d => d.ReportedDate)
                    .Take(10)
                    .Select(MapDeal)
                    .ToList()
            };
        }

        public async Task<CompetitorDetailDto?> GetCompetitorByIdAsync(Guid id)
        {
            var c = await _competitorRepo.GetQueryable().Include(x => x.Deals).FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);
            if (c == null) return null;
            var detail = new CompetitorDetailDto
            {
                Id = c.Id, Name = c.Name, Industry = c.Industry, Website = c.Website,
                Description = c.Description, Strengths = c.Strengths, Weaknesses = c.Weaknesses,
                KeyProducts = c.KeyProducts, PricingStrategy = c.PricingStrategy,
                ThreatLevel = c.ThreatLevel.ToString(),
                EstimatedMarketShare = c.EstimatedMarketShare,
                IsActive = c.IsActive,
                CreatedAt = c.CreatedAt,
                Deals = c.Deals
                    .Where(d => !d.IsDeleted)
                    .OrderByDescending(d => d.ReportedDate)
                    .Select(MapDeal)
                    .ToList()
            };

            var summary = MapSummary(c);
            detail.DealCount = summary.DealCount;
            detail.OpenDeals = summary.OpenDeals;
            detail.WonDeals = summary.WonDeals;
            detail.LostDeals = summary.LostDeals;
            detail.TotalDealValue = summary.TotalDealValue;
            detail.OpenDealValue = summary.OpenDealValue;
            detail.WinRate = summary.WinRate;

            return detail;
        }

        public async Task<CompetitorDetailDto> CreateCompetitorAsync(CreateCompetitorDto dto)
        {
            var comp = new Competitor
            {
                Name = dto.Name, Website = dto.Website, Industry = dto.Industry,
                Description = dto.Description, Strengths = dto.Strengths, Weaknesses = dto.Weaknesses,
                KeyProducts = dto.KeyProducts, PricingStrategy = dto.PricingStrategy,
                EstimatedMarketShare = dto.EstimatedMarketShare,
                ThreatLevel = ParseThreatLevel(dto.ThreatLevel)
            };
            await _competitorRepo.AddAsync(comp);
            await _competitorRepo.SaveChangesAsync();
            return (await GetCompetitorByIdAsync(comp.Id))!;
        }

        public async Task<CompetitorDetailDto> UpdateCompetitorAsync(Guid id, CreateCompetitorDto dto)
        {
            var comp = await _competitorRepo.GetQueryable().FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted)
                ?? throw new InvalidOperationException("Competitor not found");
            comp.Name = dto.Name; comp.Website = dto.Website; comp.Industry = dto.Industry;
            comp.Description = dto.Description; comp.Strengths = dto.Strengths; comp.Weaknesses = dto.Weaknesses;
            comp.KeyProducts = dto.KeyProducts; comp.PricingStrategy = dto.PricingStrategy;
            comp.EstimatedMarketShare = dto.EstimatedMarketShare;
            comp.ThreatLevel = ParseThreatLevel(dto.ThreatLevel);
            comp.UpdatedAt = DateTime.UtcNow;
            await _competitorRepo.UpdateAsync(comp);
            await _competitorRepo.SaveChangesAsync();
            return (await GetCompetitorByIdAsync(id))!;
        }

        public async Task<CompetitorDealDto> TrackDealAsync(CreateCompetitorDealDto dto)
        {
            var deal = new CompetitorDeal
            {
                CompetitorId = dto.CompetitorId, OpportunityId = dto.OpportunityId,
                OpportunityName = dto.OpportunityName, CustomerName = dto.CustomerName,
                ThreatLevel = ParseThreatLevel(dto.ThreatLevel),
                DealValue = dto.DealValue, CompetitorProposal = dto.CompetitorProposal,
                OurDifferentiator = dto.OurDifferentiator
            };
            await _dealRepo.AddAsync(deal);
            await _dealRepo.SaveChangesAsync();
            return MapDeal(deal);
        }

        public async Task<CompetitorDealDto> UpdateDealOutcomeAsync(Guid dealId, string outcome, string? lessonsLearned = null)
        {
            var deal = await _dealRepo.GetQueryable().FirstOrDefaultAsync(d => d.Id == dealId)
                ?? throw new InvalidOperationException("Deal not found");
            deal.Outcome = ParseOutcome(outcome);
            deal.LessonsLearned = lessonsLearned;
            if (deal.Outcome != CompetitorDealOutcome.InProgress) deal.ResolvedDate = DateTime.UtcNow;
            await _dealRepo.UpdateAsync(deal);
            await _dealRepo.SaveChangesAsync();
            return MapDeal(deal);
        }

        private static CompetitorSummaryDto MapSummary(Competitor competitor)
        {
            var deals = competitor.Deals.Where(d => !d.IsDeleted).ToList();
            var wonDeals = deals.Count(d => d.Outcome == CompetitorDealOutcome.Won);
            var lostDeals = deals.Count(d => d.Outcome == CompetitorDealOutcome.Lost);
            var resolvedDeals = wonDeals + lostDeals;

            return new CompetitorSummaryDto
            {
                Id = competitor.Id,
                Name = competitor.Name,
                Industry = competitor.Industry,
                ThreatLevel = competitor.ThreatLevel.ToString(),
                EstimatedMarketShare = competitor.EstimatedMarketShare,
                IsActive = competitor.IsActive,
                DealCount = deals.Count,
                OpenDeals = deals.Count(d => d.Outcome == CompetitorDealOutcome.InProgress),
                WonDeals = wonDeals,
                LostDeals = lostDeals,
                TotalDealValue = deals.Sum(d => d.DealValue),
                OpenDealValue = deals.Where(d => d.Outcome == CompetitorDealOutcome.InProgress).Sum(d => d.DealValue),
                WinRate = resolvedDeals == 0 ? 0 : Math.Round((decimal)wonDeals / resolvedDeals * 100, 2),
                CreatedAt = competitor.CreatedAt
            };
        }

        private static CompetitorDealDto MapDeal(CompetitorDeal deal) => new()
        {
            Id = deal.Id,
            OpportunityName = deal.OpportunityName,
            CustomerName = deal.CustomerName,
            ThreatLevel = deal.ThreatLevel.ToString(),
            Outcome = deal.Outcome.ToString(),
            DealValue = deal.DealValue,
            CompetitorProposal = deal.CompetitorProposal,
            OurDifferentiator = deal.OurDifferentiator,
            LessonsLearned = deal.LessonsLearned,
            ReportedDate = deal.ReportedDate,
            ResolvedDate = deal.ResolvedDate
        };

        private static CompetitorThreatLevel ParseThreatLevel(string? value)
            => Enum.TryParse<CompetitorThreatLevel>(value, true, out var parsed)
                ? parsed
                : CompetitorThreatLevel.Medium;

        private static CompetitorDealOutcome ParseOutcome(string? value)
            => Enum.TryParse<CompetitorDealOutcome>(value, true, out var parsed)
                ? parsed
                : CompetitorDealOutcome.InProgress;
    }
}
