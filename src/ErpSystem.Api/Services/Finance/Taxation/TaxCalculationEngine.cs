using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Data;

namespace ErpSystem.Api.Services.Finance.Taxation
{
    /// <summary>
    /// Tax calculation engine implementation
    /// Supports compound (tax-on-tax) calculations with granular control via CompoundBasis
    /// </summary>
    public class TaxCalculationEngine : ITaxCalculationEngine
    {
        private readonly ApplicationDbContext _context;
        private readonly ICurrentUserService _currentUserService;
        private readonly ILogger<TaxCalculationEngine> _logger;

        public TaxCalculationEngine(
            ApplicationDbContext context,
            ICurrentUserService currentUserService,
            ILogger<TaxCalculationEngine> logger)
        {
            _context = context;
            _currentUserService = currentUserService;
            _logger = logger;
        }

        private Guid TenantId => _currentUserService.TenantId ?? throw new InvalidOperationException("Tenant context is required.");

        public async Task<TaxCalculationResultDto> CalculateTaxesAsync(
            TaxCalculationRequestDto request,
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Calculating taxes for {TransactionType}, Base: {Amount}", 
                request.TransactionType, request.BaseAmount);

            var result = new TaxCalculationResultDto
            {
                BaseAmount = request.BaseAmount
            };

            // Determine which taxes to apply
            List<TaxGroupComponent>? components = null;
            TaxGroup? taxGroup = null;

            if (request.ManualTaxIds != null && request.ManualTaxIds.Any())
            {
                // Manual selection - create virtual components
                components = await CreateVirtualComponentsAsync(request.ManualTaxIds, cancellationToken);
                result.HasManualOverrides = true;
            }
            else if (request.TaxGroupId.HasValue)
            {
                // Specific group
                taxGroup = await GetTaxGroupWithComponentsAsync(request.TaxGroupId.Value, cancellationToken);
                components = taxGroup?.Components.ToList();
                result.TaxGroupId = taxGroup?.Id;
                result.TaxGroupName = taxGroup?.Name;
            }
            else
            {
                // Find default group for transaction type or evaluate rules
                // First, try to find a matching Tax Rule
                TaxGroup? ruleBasedGroup = await EvaluateTaxRulesAsync(request, cancellationToken);
                
                if (ruleBasedGroup != null)
                {
                    taxGroup = ruleBasedGroup;
                    components = taxGroup.Components.ToList();
                    result.TaxGroupId = taxGroup.Id;
                    result.TaxGroupName = taxGroup.Name;
                    _logger.LogInformation("Applied Tax Rule: {RuleBasedGroupName}", taxGroup.Name);
                }
                else
                {
                    // Fallback to default configuration
                    var applicability = GetApplicabilityFromTransactionType(request.TransactionType);
                    TaxGroup? defaultGroup = await GetDefaultTaxGroupEntityAsync(applicability, cancellationToken);
                    taxGroup = defaultGroup;
                    if (taxGroup != null)
                    {
                        components = taxGroup.Components.ToList();
                        result.TaxGroupId = taxGroup.Id;
                        result.TaxGroupName = taxGroup.Name;
                    }
                }
            }

            if (components == null || !components.Any())
            {
                _logger.LogWarning("No applicable taxes found for {TransactionType}", request.TransactionType);
                result.GrandTotal = request.BaseAmount;
                return result;
            }

            // Calculate taxes in order
            var calculatedTaxes = new Dictionary<string, decimal>(); // TaxCode -> Amount
            
            foreach (var component in components.OrderBy(c => c.CalculationOrder))
            {
                var tax = component.Tax;
                if (tax == null || !tax.IsActive) continue;

                // Check threshold for withholding taxes
                if (tax.Category == TaxCategory.Withholding && tax.ThresholdAmount.HasValue)
                {
                    var entityId = request.SupplierId ?? request.CustomerId;
                    var entityType = request.SupplierId.HasValue ? "Supplier" : "Customer";

                    if (entityId.HasValue)
                    {
                        var thresholdStatus = await CheckThresholdAsync(
                            tax.Id,
                            entityType,
                            entityId.Value,
                            request.BaseAmount,
                            cancellationToken);

                        if (!thresholdStatus.IsThresholdExceeded)
                        {
                            _logger.LogInformation("Threshold not exceeded for {TaxCode}, skipping", tax.Code);
                            continue;
                        }
                    }
                }

                // Calculate taxable amount based on CompoundBasis
                decimal taxableAmount = CalculateTaxableAmount(
                    request.BaseAmount,
                    calculatedTaxes,
                    component.CompoundBasis,
                    component.AppliesOnTaxCodes);

                // Calculate tax amount
                decimal taxAmount = Math.Round(taxableAmount * (tax.Rate / 100), 2);

                // Store calculated tax
                calculatedTaxes[tax.Code] = taxAmount;

                // Add to breakdown
                result.TaxBreakdowns.Add(new TaxBreakdownDto
                {
                    TaxId = tax.Id,
                    TaxCode = tax.Code,
                    TaxName = tax.Name,
                    TaxCategory = tax.Category,
                    TaxableAmount = taxableAmount,
                    TaxRate = tax.Rate,
                    TaxAmount = taxAmount,
                    CompoundBasis = component.CompoundBasis,
                    CalculationOrder = component.CalculationOrder,
                    AppliedOnTaxCodes = ParseTaxCodes(component.AppliesOnTaxCodes),
                    IsInputTaxDeductible = tax.IsInputTaxDeductible,
                    IsManualOverride = result.HasManualOverrides
                });

                _logger.LogDebug("Calculated {TaxCode}: {Amount} on {Taxable} @ {Rate}%", 
                    tax.Code, taxAmount, taxableAmount, tax.Rate);
            }

            // Calculate totals
            result.TotalTaxAmount = result.TaxBreakdowns.Sum(t => t.TaxAmount);
            result.GrandTotal = request.BaseAmount + result.TotalTaxAmount;
            result.EffectiveTaxRate = request.BaseAmount > 0 
                ? Math.Round((result.TotalTaxAmount / request.BaseAmount) * 100, 2) 
                : 0;

            _logger.LogInformation("Tax calculation complete: Base {Base}, Tax {Tax}, Total {Total}, Effective Rate {Rate}%",
                result.BaseAmount, result.TotalTaxAmount, result.GrandTotal, result.EffectiveTaxRate);

            return result;
        }

        public async Task<TaxGroupDto?> GetDefaultTaxGroupAsync(
            TaxApplicability applicability,
            CancellationToken cancellationToken = default)
        {
            var group = await GetDefaultTaxGroupEntityAsync(applicability, cancellationToken);
            return group != null ? MapToTaxGroupDto(group) : null;
        }

        private async Task<TaxGroup?> EvaluateTaxRulesAsync(TaxCalculationRequestDto request, CancellationToken cancellationToken)
        {
            // Fetch all active rules
            var rules = await _context.TaxRules
                .Include(r => r.TaxGroup)
                    .ThenInclude(g => g.Components.Where(c => !c.IsDeleted))
                        .ThenInclude(c => c.Tax)
                .Where(r => r.TenantId == TenantId && r.IsActive)
                .OrderBy(r => r.Priority)
                .ToListAsync(cancellationToken);

            if (!rules.Any()) return null;

            // Pre-fetch related data if needed for rules
            string? customerType = null;
            if (request.CustomerId.HasValue)
            {
                var customer = await _context.Customers
                    .FirstOrDefaultAsync(c => c.Id == request.CustomerId.Value, cancellationToken);
                customerType = customer?.CustomerType;
            }

            // Evaluate
            foreach (var rule in rules)
            {
                bool match = true;

                // 1. Transaction Type
                if (!string.IsNullOrEmpty(rule.TransactionType))
                {
                    if (!string.Equals(rule.TransactionType, request.TransactionType.ToString(), StringComparison.OrdinalIgnoreCase))
                        match = false;
                }

                // 2. Customer Type
                if (match && !string.IsNullOrEmpty(rule.CustomerType))
                {
                    if (string.IsNullOrEmpty(customerType) || !string.Equals(rule.CustomerType, customerType, StringComparison.OrdinalIgnoreCase))
                        match = false;
                }

                // 3. Service Type (Not implemented in Request yet)
                // 4. Product Category (Not implemented in Request yet)

                if (match)
                {
                    return rule.TaxGroup;
                }
            }

            return null;
        }

        private async Task<TaxGroup?> GetDefaultTaxGroupEntityAsync(
            TaxApplicability applicability,
            CancellationToken cancellationToken)
        {
            return await _context.Set<TaxGroup>()
                .Where(g => g.TenantId == TenantId 
                    && g.IsActive 
                    && !g.IsDeleted 
                    && g.IsDefault
                    && (g.Applicability == applicability || g.Applicability == TaxApplicability.Both))
                .Include(g => g.Components.Where(c => !c.IsDeleted))
                    .ThenInclude(c => c.Tax)
                .OrderByDescending(g => g.Applicability == applicability) // Prefer exact match
                .FirstOrDefaultAsync(cancellationToken);
        }

        public async Task<TaxThresholdStatusDto> CheckThresholdAsync(
            Guid taxId,
            string entityType,
            Guid entityId,
            decimal amount,
            CancellationToken cancellationToken = default)
        {
            var currentYear = DateTime.UtcNow.Year;

            var tax = await _context.Set<Tax>()
                .FirstOrDefaultAsync(t => t.Id == taxId && t.TenantId == TenantId && !t.IsDeleted, cancellationToken);

            var defaultThreshold = tax?.ThresholdAmount ?? 2000m; // Default GHS 2,000

            var threshold = await _context.Set<TaxThreshold>()
                .FirstOrDefaultAsync(t => t.TaxId == taxId 
                    && t.EntityType == entityType 
                    && t.EntityId == entityId 
                    && t.FiscalYear == currentYear
                    && t.TenantId == TenantId
                    && !t.IsDeleted, cancellationToken);

            if (threshold == null)
            {
                return new TaxThresholdStatusDto
                {
                    TaxId = taxId,
                    TaxName = tax?.Name ?? "Unknown",
                    EntityId = entityId,
                    EntityType = entityType,
                    FiscalYear = currentYear,
                    CumulativeAmount = amount,
                    ThresholdAmount = defaultThreshold,
                    RemainingAmount = defaultThreshold - amount,
                    IsThresholdExceeded = amount >= defaultThreshold,
                    PercentageUsed = Math.Round((amount / defaultThreshold) * 100, 2)
                };
            }

            var newCumulative = threshold.CumulativeAmount + amount;
            var remaining = threshold.ThresholdAmount - newCumulative;

            return new TaxThresholdStatusDto
            {
                TaxId = taxId,
                TaxName = tax?.Name ?? "Unknown",
                EntityId = entityId,
                EntityType = entityType,
                FiscalYear = currentYear,
                CumulativeAmount = newCumulative,
                ThresholdAmount = threshold.ThresholdAmount,
                RemainingAmount = remaining > 0 ? remaining : 0,
                IsThresholdExceeded = newCumulative >= threshold.ThresholdAmount,
                ThresholdExceededDate = threshold.ThresholdExceededDate,
                PercentageUsed = Math.Round((newCumulative / threshold.ThresholdAmount) * 100, 2)
            };
        }

        public async Task UpdateThresholdAsync(
            Guid taxId,
            string entityType,
            Guid entityId,
            decimal amount,
            CancellationToken cancellationToken = default)
        {
            var currentYear = DateTime.UtcNow.Year;

            var tax = await _context.Set<Tax>()
                .FirstOrDefaultAsync(t => t.Id == taxId && t.TenantId == TenantId && !t.IsDeleted, cancellationToken);

            var defaultThreshold = tax?.ThresholdAmount ?? 2000m;

            var threshold = await _context.Set<TaxThreshold>()
                .FirstOrDefaultAsync(t => t.TaxId == taxId 
                    && t.EntityType == entityType 
                    && t.EntityId == entityId 
                    && t.FiscalYear == currentYear
                    && t.TenantId == TenantId
                    && !t.IsDeleted, cancellationToken);

            if (threshold == null)
            {
                threshold = new TaxThreshold
                {
                    Id = Guid.NewGuid(),
                    TenantId = TenantId,
                    TaxId = taxId,
                    EntityType = entityType,
                    EntityId = entityId,
                    FiscalYear = currentYear,
                    CumulativeAmount = amount,
                    ThresholdAmount = defaultThreshold,
                    IsThresholdExceeded = amount >= defaultThreshold,
                    ThresholdExceededDate = amount >= defaultThreshold ? DateTime.UtcNow : null,
                    LastUpdatedDate = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = _currentUserService.UserName ?? "system"
                };

                await _context.Set<TaxThreshold>().AddAsync(threshold, cancellationToken);
            }
            else
            {
                threshold.CumulativeAmount += amount;
                threshold.LastUpdatedDate = DateTime.UtcNow;
                threshold.UpdatedAt = DateTime.UtcNow;
                threshold.UpdatedBy = _currentUserService.UserName ?? "system";

                if (!threshold.IsThresholdExceeded && threshold.CumulativeAmount >= threshold.ThresholdAmount)
                {
                    threshold.IsThresholdExceeded = true;
                    threshold.ThresholdExceededDate = DateTime.UtcNow;
                }

                _context.Set<TaxThreshold>().Update(threshold);
            }

            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Updated threshold for {Tax}, {EntityType} {EntityId}: {Amount}",
                taxId, entityType, entityId, threshold.CumulativeAmount);
        }

        #region Private Helper Methods

        private decimal CalculateTaxableAmount(
            decimal baseAmount,
            Dictionary<string, decimal> calculatedTaxes,
            CompoundBasis compoundBasis,
            string? appliesOnTaxCodes)
        {
            return compoundBasis switch
            {
                CompoundBasis.BaseOnly => baseAmount,
                CompoundBasis.Cumulative => baseAmount + calculatedTaxes.Values.Sum(),
                CompoundBasis.Specific => CalculateSpecificBase(baseAmount, calculatedTaxes, appliesOnTaxCodes),
                _ => baseAmount
            };
        }

        private decimal CalculateSpecificBase(
            decimal baseAmount,
            Dictionary<string, decimal> calculatedTaxes,
            string? appliesOnTaxCodes)
        {
            if (string.IsNullOrWhiteSpace(appliesOnTaxCodes))
                return baseAmount;

            var taxCodes = appliesOnTaxCodes.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var specificTaxAmount = taxCodes
                .Where(code => calculatedTaxes.ContainsKey(code))
                .Sum(code => calculatedTaxes[code]);

            return baseAmount + specificTaxAmount;
        }

        private List<string>? ParseTaxCodes(string? appliesOnTaxCodes)
        {
            if (string.IsNullOrWhiteSpace(appliesOnTaxCodes))
                return null;

            return appliesOnTaxCodes
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();
        }

        private TaxApplicability GetApplicabilityFromTransactionType(TaxTransactionType transactionType)
        {
            return transactionType switch
            {
                TaxTransactionType.SaleOfGoods => TaxApplicability.Sales,
                TaxTransactionType.SaleOfServices => TaxApplicability.Sales,
                TaxTransactionType.PurchaseOfGoods => TaxApplicability.Purchases,
                TaxTransactionType.PurchaseOfServices => TaxApplicability.Purchases,
                TaxTransactionType.Works => TaxApplicability.Purchases,
                TaxTransactionType.Rent => TaxApplicability.Purchases,
                TaxTransactionType.Dividend => TaxApplicability.Purchases,
                TaxTransactionType.Interest => TaxApplicability.Purchases,
                _ => TaxApplicability.Both
            };
        }

        private async Task<TaxGroup?> GetTaxGroupWithComponentsAsync(Guid taxGroupId, CancellationToken cancellationToken)
        {
            return await _context.Set<TaxGroup>()
                .Where(g => g.Id == taxGroupId && g.TenantId == TenantId && !g.IsDeleted)
                .Include(g => g.Components.Where(c => !c.IsDeleted))
                    .ThenInclude(c => c.Tax)
                .FirstOrDefaultAsync(cancellationToken);
        }

        private async Task<List<TaxGroupComponent>> CreateVirtualComponentsAsync(List<Guid> taxIds, CancellationToken cancellationToken)
        {
            var taxes = await _context.Set<Tax>()
                .Where(t => taxIds.Contains(t.Id) && t.TenantId == TenantId && t.IsActive && !t.IsDeleted)
                .ToListAsync(cancellationToken);

            return taxes.Select((tax, index) => new TaxGroupComponent
            {
                Id = Guid.NewGuid(),
                TaxId = tax.Id,
                Tax = tax,
                CalculationOrder = index + 1,
                CompoundBasis = CompoundBasis.BaseOnly // Default for manual selection
            }).ToList();
        }

        private static TaxGroupDto MapToTaxGroupDto(TaxGroup group)
        {
            return new TaxGroupDto
            {
                Id = group.Id,
                TenantId = group.TenantId,
                Code = group.Code,
                Name = group.Name,
                Description = group.Description,
                Applicability = group.Applicability,
                IsDefault = group.IsDefault,
                IsActive = group.IsActive,
                Components = group.Components
                    .Where(c => !c.IsDeleted && c.Tax != null)
                    .OrderBy(c => c.CalculationOrder)
                    .Select(c => new TaxGroupComponentDto
                    {
                        Id = c.Id,
                        TaxId = c.TaxId,
                        TaxCode = c.Tax.Code,
                        TaxName = c.Tax.Name,
                        TaxRate = c.Tax.Rate,
                        TaxCategory = c.Tax.Category,
                        CalculationOrder = c.CalculationOrder,
                        CompoundBasis = c.CompoundBasis,
                        AppliesOnTaxCodes = string.IsNullOrWhiteSpace(c.AppliesOnTaxCodes) 
                            ? null 
                            : c.AppliesOnTaxCodes.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList()
                    }).ToList(),
                CreatedBy = group.CreatedBy,
                CreatedAt = group.CreatedAt,
                UpdatedBy = group.UpdatedBy,
                UpdatedAt = group.UpdatedAt
            };
        }

        #endregion
    }
}
