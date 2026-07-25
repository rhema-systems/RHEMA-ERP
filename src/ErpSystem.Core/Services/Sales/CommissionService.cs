using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ErpSystem.Core.DTOs.Sales;
using ErpSystem.Core.Entities.Sales;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Numbering;
using ErpSystem.Core.Interfaces.Sales;

namespace ErpSystem.Core.Services.Sales
{
    public class CommissionService : ICommissionService
    {
        private readonly IGenericRepository<CommissionRule> _ruleRepo;
        private readonly IGenericRepository<CommissionStatement> _statementRepo;
        private readonly IGenericRepository<CommissionStatementLine> _lineRepo;
        private readonly IGenericRepository<SalesOrder> _salesOrderRepo;
        private readonly ICurrentUserProvider _currentUserProvider;
        private readonly IDocumentNumberingService _documentNumberingService;

        public CommissionService(
            IGenericRepository<CommissionRule> ruleRepo,
            IGenericRepository<CommissionStatement> statementRepo,
            IGenericRepository<CommissionStatementLine> lineRepo,
            IGenericRepository<SalesOrder> salesOrderRepo,
            ICurrentUserProvider currentUserProvider,
            IDocumentNumberingService documentNumberingService)
        {
            _ruleRepo = ruleRepo;
            _statementRepo = statementRepo;
            _lineRepo = lineRepo;
            _salesOrderRepo = salesOrderRepo;
            _currentUserProvider = currentUserProvider;
            _documentNumberingService = documentNumberingService;
        }

        // ── Rules ──

        public async Task<(IEnumerable<CommissionRuleSummaryDto> Items, int TotalCount)> GetRulesAsync(int page, int pageSize, string? search = null, bool? isActive = null)
        {
            var query = _ruleRepo.GetQueryable().Where(r => !r.IsDeleted);
            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(r => r.Name.Contains(search));
            if (isActive.HasValue)
                query = query.Where(r => r.IsActive == isActive.Value);

            var totalCount = await query.CountAsync();
            var items = await query
                .OrderByDescending(r => r.CreatedAt)
                .Skip((page - 1) * pageSize).Take(pageSize)
                .Select(r => new CommissionRuleSummaryDto
                {
                    Id = r.Id, Name = r.Name, CommissionType = r.CommissionType.ToString(),
                    Rate = r.Rate, SalesRepId = r.SalesRepId,
                    MinimumSaleAmount = r.MinimumSaleAmount, MaximumCommission = r.MaximumCommission,
                    IsActive = r.IsActive, EffectiveFrom = r.EffectiveFrom, EffectiveTo = r.EffectiveTo,
                    CreatedAt = r.CreatedAt
                }).ToListAsync();

            return (items, totalCount);
        }

        public async Task<CommissionRuleDetailDto?> GetRuleByIdAsync(Guid id)
        {
            var r = await _ruleRepo.GetQueryable().FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);
            if (r == null) return null;
            return new CommissionRuleDetailDto
            {
                Id = r.Id, Name = r.Name, Description = r.Description,
                CommissionType = r.CommissionType.ToString(), Rate = r.Rate,
                TierDefinitions = r.TierDefinitions, SalesRepId = r.SalesRepId,
                ProductId = r.ProductId, CustomerId = r.CustomerId,
                MinimumSaleAmount = r.MinimumSaleAmount, MaximumCommission = r.MaximumCommission,
                IsActive = r.IsActive, EffectiveFrom = r.EffectiveFrom, EffectiveTo = r.EffectiveTo,
                CreatedAt = r.CreatedAt
            };
        }

        public async Task<CommissionRuleDetailDto> CreateRuleAsync(CreateCommissionRuleDto dto)
        {
            var rule = new CommissionRule
            {
                Name = dto.Name, Description = dto.Description,
                CommissionType = Enum.Parse<CommissionType>(dto.CommissionType),
                Rate = dto.Rate, TierDefinitions = dto.TierDefinitions,
                SalesRepId = dto.SalesRepId, ProductId = dto.ProductId, CustomerId = dto.CustomerId,
                MinimumSaleAmount = dto.MinimumSaleAmount, MaximumCommission = dto.MaximumCommission,
                EffectiveFrom = dto.EffectiveFrom ?? DateTime.UtcNow, EffectiveTo = dto.EffectiveTo
            };
            await _ruleRepo.AddAsync(rule);
            await _ruleRepo.SaveChangesAsync();
            return (await GetRuleByIdAsync(rule.Id))!;
        }

        public async Task<CommissionRuleDetailDto> UpdateRuleAsync(Guid id, CreateCommissionRuleDto dto)
        {
            var rule = await _ruleRepo.GetQueryable().FirstOrDefaultAsync(r => r.Id == id && !r.IsDeleted)
                ?? throw new InvalidOperationException("Rule not found");
            rule.Name = dto.Name; rule.Description = dto.Description;
            rule.CommissionType = Enum.Parse<CommissionType>(dto.CommissionType);
            rule.Rate = dto.Rate; rule.TierDefinitions = dto.TierDefinitions;
            rule.SalesRepId = dto.SalesRepId; rule.ProductId = dto.ProductId; rule.CustomerId = dto.CustomerId;
            rule.MinimumSaleAmount = dto.MinimumSaleAmount; rule.MaximumCommission = dto.MaximumCommission;
            rule.EffectiveFrom = dto.EffectiveFrom ?? rule.EffectiveFrom; rule.EffectiveTo = dto.EffectiveTo;
            rule.UpdatedAt = DateTime.UtcNow;
            await _ruleRepo.UpdateAsync(rule);
            await _ruleRepo.SaveChangesAsync();
            return (await GetRuleByIdAsync(id))!;
        }

        public async Task<bool> ActivateRuleAsync(Guid id)
        {
            var rule = await _ruleRepo.GetQueryable().FirstOrDefaultAsync(r => r.Id == id && !r.IsDeleted);
            if (rule == null) return false;
            rule.IsActive = true; rule.UpdatedAt = DateTime.UtcNow;
            await _ruleRepo.UpdateAsync(rule);
            await _ruleRepo.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeactivateRuleAsync(Guid id)
        {
            var rule = await _ruleRepo.GetQueryable().FirstOrDefaultAsync(r => r.Id == id && !r.IsDeleted);
            if (rule == null) return false;
            rule.IsActive = false; rule.UpdatedAt = DateTime.UtcNow;
            await _ruleRepo.UpdateAsync(rule);
            await _ruleRepo.SaveChangesAsync();
            return true;
        }

        // ── Statements ──

        public async Task<(IEnumerable<CommissionStatementSummaryDto> Items, int TotalCount)> GetStatementsAsync(int page, int pageSize, string? search = null, string? status = null)
        {
            var query = _statementRepo.GetQueryable().Include(s => s.Lines).Where(s => !s.IsDeleted);
            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(s => s.StatementNumber.Contains(search) || (s.SalesRepName != null && s.SalesRepName.Contains(search)));
            if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<CommissionStatementStatus>(status, out var st))
                query = query.Where(s => s.Status == st);

            var totalCount = await query.CountAsync();
            var items = await query.OrderByDescending(s => s.PeriodEnd)
                .Skip((page - 1) * pageSize).Take(pageSize)
                .Select(s => new CommissionStatementSummaryDto
                {
                    Id = s.Id, StatementNumber = s.StatementNumber, SalesRepName = s.SalesRepName ?? "",
                    PeriodStart = s.PeriodStart, PeriodEnd = s.PeriodEnd,
                    TotalSales = s.TotalSales, TotalCommission = s.TotalCommission,
                    Adjustments = s.Adjustments, NetCommission = s.TotalCommission + s.Adjustments,
                    Status = s.Status.ToString(), LineCount = s.Lines.Count, CreatedAt = s.CreatedAt
                }).ToListAsync();

            return (items, totalCount);
        }

        public async Task<CommissionStatementDetailDto?> GetStatementByIdAsync(Guid id)
        {
            var s = await _statementRepo.GetQueryable()
                .Include(x => x.Lines)
                .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);
            if (s == null) return null;
            return MapStatementDetail(s);
        }

        public async Task<CommissionStatementDetailDto> GenerateStatementAsync(GenerateStatementDto dto)
        {
            var rules = await _ruleRepo.GetQueryable()
                .Where(r => !r.IsDeleted && r.IsActive &&
                    (r.SalesRepId == null || r.SalesRepId == dto.SalesRepId) &&
                    r.EffectiveFrom <= dto.PeriodEnd &&
                    (r.EffectiveTo == null || r.EffectiveTo >= dto.PeriodStart))
                .ToListAsync();

            var orders = await _salesOrderRepo.GetQueryable()
                .Where(o => !o.IsDeleted &&
                    o.DocumentDate >= dto.PeriodStart && o.DocumentDate <= dto.PeriodEnd &&
                    o.OrderStatus == SalesOrderStatus.Closed)
                .ToListAsync();

            var statement = new CommissionStatement
            {
                StatementNumber = await _documentNumberingService.GenerateAsync(
                    DocumentNumberingModules.Sales,
                    SalesDocumentTypes.CommissionStatement,
                    _currentUserProvider.TenantId,
                    dto.PeriodEnd,
                    nameof(CommissionStatement)),
                SalesRepId = dto.SalesRepId, SalesRepName = dto.SalesRepId,
                PeriodStart = dto.PeriodStart, PeriodEnd = dto.PeriodEnd,
                Status = CommissionStatementStatus.Calculated
            };

            decimal totalSales = 0, totalCommission = 0;

            foreach (var order in orders)
            {
                var rule = FindBestRule(rules, order);
                if (rule == null) continue;

                var saleAmount = order.TotalAmount;
                if (saleAmount < rule.MinimumSaleAmount) continue;

                var commission = CalculateCommission(rule, saleAmount);
                if (rule.MaximumCommission > 0 && commission > rule.MaximumCommission)
                    commission = rule.MaximumCommission;

                totalSales += saleAmount;
                totalCommission += commission;

                statement.Lines.Add(new CommissionStatementLine
                {
                    CommissionStatementId = statement.Id,
                    CommissionRuleId = rule.Id,
                    SalesOrderId = order.Id,
                    SalesOrderNumber = order.DocumentNumber,
                    SaleAmount = saleAmount,
                    CommissionRate = rule.Rate,
                    CommissionAmount = commission,
                    Description = $"Commission on order {order.DocumentNumber}",
                    TransactionDate = order.DocumentDate
                });
            }

            statement.TotalSales = totalSales;
            statement.TotalCommission = totalCommission;

            await _statementRepo.AddAsync(statement);
            await _statementRepo.SaveChangesAsync();
            return (await GetStatementByIdAsync(statement.Id))!;
        }

        public async Task<CommissionStatementDetailDto> ApproveStatementAsync(Guid id)
        {
            var s = await _statementRepo.GetQueryable().Include(x => x.Lines)
                .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted)
                ?? throw new InvalidOperationException("Statement not found");
            if (s.Status != CommissionStatementStatus.Calculated && s.Status != CommissionStatementStatus.Draft)
                throw new InvalidOperationException($"Cannot approve statement in {s.Status} status");
            s.Status = CommissionStatementStatus.Approved;
            s.ApprovedDate = DateTime.UtcNow; s.UpdatedAt = DateTime.UtcNow;
            await _statementRepo.UpdateAsync(s);
            await _statementRepo.SaveChangesAsync();
            return MapStatementDetail(s);
        }

        public async Task<CommissionStatementDetailDto> MarkAsPaidAsync(Guid id, string? paymentReference = null)
        {
            var s = await _statementRepo.GetQueryable().Include(x => x.Lines)
                .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted)
                ?? throw new InvalidOperationException("Statement not found");
            if (s.Status != CommissionStatementStatus.Approved)
                throw new InvalidOperationException("Statement must be approved before payment");
            s.Status = CommissionStatementStatus.Paid;
            s.PaidDate = DateTime.UtcNow; s.PaymentReference = paymentReference;
            s.UpdatedAt = DateTime.UtcNow;
            await _statementRepo.UpdateAsync(s);
            await _statementRepo.SaveChangesAsync();
            return MapStatementDetail(s);
        }

        public async Task<CommissionStatementDetailDto> DisputeStatementAsync(Guid id, string? reason = null)
        {
            var s = await _statementRepo.GetQueryable().Include(x => x.Lines)
                .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted)
                ?? throw new InvalidOperationException("Statement not found");
            s.Status = CommissionStatementStatus.Disputed;
            s.Notes = reason ?? s.Notes; s.UpdatedAt = DateTime.UtcNow;
            await _statementRepo.UpdateAsync(s);
            await _statementRepo.SaveChangesAsync();
            return MapStatementDetail(s);
        }

        // ── Helpers ──

        private CommissionRule? FindBestRule(List<CommissionRule> rules, SalesOrder order)
        {
            return rules.OrderByDescending(r => (r.ProductId.HasValue ? 2 : 0) + (r.CustomerId.HasValue ? 1 : 0))
                .FirstOrDefault();
        }

        private decimal CalculateCommission(CommissionRule rule, decimal saleAmount)
        {
            return rule.CommissionType switch
            {
                CommissionType.FlatPercentage => saleAmount * rule.Rate / 100m,
                CommissionType.Tiered => CalculateTieredCommission(rule, saleAmount),
                CommissionType.RuleBased => saleAmount * rule.Rate / 100m,
                _ => 0m
            };
        }

        private decimal CalculateTieredCommission(CommissionRule rule, decimal saleAmount)
        {
            if (string.IsNullOrEmpty(rule.TierDefinitions))
                return saleAmount * rule.Rate / 100m;
            try
            {
                var tiers = System.Text.Json.JsonSerializer.Deserialize<List<CommissionTier>>(rule.TierDefinitions);
                if (tiers == null) return saleAmount * rule.Rate / 100m;
                decimal totalCommission = 0, remaining = saleAmount;
                foreach (var tier in tiers.OrderBy(t => t.Min))
                {
                    if (remaining <= 0) break;
                    var tierAmount = Math.Min(remaining, tier.Max - tier.Min);
                    if (tierAmount > 0) { totalCommission += tierAmount * tier.Rate / 100m; remaining -= tierAmount; }
                }
                return totalCommission;
            }
            catch { return saleAmount * rule.Rate / 100m; }
        }

        private CommissionStatementDetailDto MapStatementDetail(CommissionStatement s)
        {
            return new CommissionStatementDetailDto
            {
                Id = s.Id, StatementNumber = s.StatementNumber,
                SalesRepId = s.SalesRepId, SalesRepName = s.SalesRepName ?? "",
                PeriodStart = s.PeriodStart, PeriodEnd = s.PeriodEnd,
                TotalSales = s.TotalSales, TotalCommission = s.TotalCommission,
                Adjustments = s.Adjustments, NetCommission = s.TotalCommission + s.Adjustments,
                Status = s.Status.ToString(), LineCount = s.Lines.Count,
                ApprovedDate = s.ApprovedDate, ApprovedBy = s.ApprovedBy,
                PaidDate = s.PaidDate, PaymentReference = s.PaymentReference,
                Notes = s.Notes, CreatedAt = s.CreatedAt,
                Lines = s.Lines.Select(l => new CommissionStatementLineDto
                {
                    Id = l.Id, SalesOrderNumber = l.SalesOrderNumber, CustomerName = l.CustomerName,
                    SaleAmount = l.SaleAmount, CommissionRate = l.CommissionRate,
                    CommissionAmount = l.CommissionAmount, Description = l.Description,
                    TransactionDate = l.TransactionDate
                }).ToList()
            };
        }

        private class CommissionTier { public decimal Min { get; set; } public decimal Max { get; set; } public decimal Rate { get; set; } }
    }
}
