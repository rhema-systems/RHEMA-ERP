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
            var query = _competitorRepo.GetQueryable().Include(c => c.Deals).Where(c => !c.IsDeleted);
            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(c => c.Name.Contains(search) || (c.Industry != null && c.Industry.Contains(search)));
            if (!string.IsNullOrWhiteSpace(threatLevel) && Enum.TryParse<CompetitorThreatLevel>(threatLevel, out var tl))
                query = query.Where(c => c.ThreatLevel == tl);

            var totalCount = await query.CountAsync();
            var items = await query.OrderByDescending(c => c.CreatedAt)
                .Skip((page - 1) * pageSize).Take(pageSize)
                .Select(c => new CompetitorSummaryDto
                {
                    Id = c.Id, Name = c.Name, Industry = c.Industry,
                    ThreatLevel = c.ThreatLevel.ToString(),
                    EstimatedMarketShare = c.EstimatedMarketShare,
                    IsActive = c.IsActive, DealCount = c.Deals.Count,
                    WonDeals = c.Deals.Count(d => d.Outcome == CompetitorDealOutcome.Won),
                    LostDeals = c.Deals.Count(d => d.Outcome == CompetitorDealOutcome.Lost),
                    CreatedAt = c.CreatedAt
                }).ToListAsync();

            return (items, totalCount);
        }

        public async Task<CompetitorDetailDto?> GetCompetitorByIdAsync(Guid id)
        {
            var c = await _competitorRepo.GetQueryable().Include(x => x.Deals).FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);
            if (c == null) return null;
            return new CompetitorDetailDto
            {
                Id = c.Id, Name = c.Name, Industry = c.Industry, Website = c.Website,
                Description = c.Description, Strengths = c.Strengths, Weaknesses = c.Weaknesses,
                KeyProducts = c.KeyProducts, PricingStrategy = c.PricingStrategy,
                ThreatLevel = c.ThreatLevel.ToString(), EstimatedMarketShare = c.EstimatedMarketShare,
                IsActive = c.IsActive, DealCount = c.Deals.Count,
                WonDeals = c.Deals.Count(d => d.Outcome == CompetitorDealOutcome.Won),
                LostDeals = c.Deals.Count(d => d.Outcome == CompetitorDealOutcome.Lost),
                CreatedAt = c.CreatedAt,
                Deals = c.Deals.Select(d => new CompetitorDealDto
                {
                    Id = d.Id, OpportunityName = d.OpportunityName, CustomerName = d.CustomerName,
                    ThreatLevel = d.ThreatLevel.ToString(), Outcome = d.Outcome.ToString(),
                    DealValue = d.DealValue, CompetitorProposal = d.CompetitorProposal,
                    OurDifferentiator = d.OurDifferentiator, LessonsLearned = d.LessonsLearned,
                    ReportedDate = d.ReportedDate
                }).ToList()
            };
        }

        public async Task<CompetitorDetailDto> CreateCompetitorAsync(CreateCompetitorDto dto)
        {
            var comp = new Competitor
            {
                Name = dto.Name, Website = dto.Website, Industry = dto.Industry,
                Description = dto.Description, Strengths = dto.Strengths, Weaknesses = dto.Weaknesses,
                KeyProducts = dto.KeyProducts, PricingStrategy = dto.PricingStrategy,
                EstimatedMarketShare = dto.EstimatedMarketShare,
                ThreatLevel = Enum.Parse<CompetitorThreatLevel>(dto.ThreatLevel)
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
            comp.ThreatLevel = Enum.Parse<CompetitorThreatLevel>(dto.ThreatLevel);
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
                ThreatLevel = Enum.Parse<CompetitorThreatLevel>(dto.ThreatLevel),
                DealValue = dto.DealValue, CompetitorProposal = dto.CompetitorProposal,
                OurDifferentiator = dto.OurDifferentiator
            };
            await _dealRepo.AddAsync(deal);
            await _dealRepo.SaveChangesAsync();
            return new CompetitorDealDto
            {
                Id = deal.Id, OpportunityName = deal.OpportunityName, CustomerName = deal.CustomerName,
                ThreatLevel = deal.ThreatLevel.ToString(), Outcome = deal.Outcome.ToString(),
                DealValue = deal.DealValue, CompetitorProposal = deal.CompetitorProposal,
                OurDifferentiator = deal.OurDifferentiator, ReportedDate = deal.ReportedDate
            };
        }

        public async Task<CompetitorDealDto> UpdateDealOutcomeAsync(Guid dealId, string outcome, string? lessonsLearned = null)
        {
            var deal = await _dealRepo.GetQueryable().FirstOrDefaultAsync(d => d.Id == dealId)
                ?? throw new InvalidOperationException("Deal not found");
            deal.Outcome = Enum.Parse<CompetitorDealOutcome>(outcome);
            deal.LessonsLearned = lessonsLearned;
            if (deal.Outcome != CompetitorDealOutcome.InProgress) deal.ResolvedDate = DateTime.UtcNow;
            await _dealRepo.UpdateAsync(deal);
            await _dealRepo.SaveChangesAsync();
            return new CompetitorDealDto
            {
                Id = deal.Id, OpportunityName = deal.OpportunityName, CustomerName = deal.CustomerName,
                ThreatLevel = deal.ThreatLevel.ToString(), Outcome = deal.Outcome.ToString(),
                DealValue = deal.DealValue, LessonsLearned = deal.LessonsLearned, ReportedDate = deal.ReportedDate
            };
        }
    }
}
