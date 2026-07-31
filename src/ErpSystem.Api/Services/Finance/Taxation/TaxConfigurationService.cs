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
using ErpSystem.Api.Services.Finance;
using ErpSystem.Data;
using ErpSystem.Shared;

namespace ErpSystem.Api.Services.Finance.Taxation
{
    /// <summary>
    /// Tax configuration service implementation
    /// Manages taxes, tax groups, and components
    /// </summary>
    public class TaxConfigurationService : ITaxConfigurationService
    {
        private readonly ApplicationDbContext _context;
        private readonly ICurrentUserService _currentUserService;
        private readonly ILogger<TaxConfigurationService> _logger;
        private readonly IFinanceAuditService? _financeAuditService;

        public TaxConfigurationService(
            ApplicationDbContext context,
            ICurrentUserService currentUserService,
            ILogger<TaxConfigurationService> logger,
            IFinanceAuditService? financeAuditService = null)
        {
            _context = context;
            _currentUserService = currentUserService;
            _logger = logger;
            _financeAuditService = financeAuditService;
        }

        private Guid TenantId => _currentUserService.GetRequiredFinanceTenantId();
        private string UserName => _currentUserService.UserName ?? "system";

        #region Taxes

        public async Task<IReadOnlyList<TaxDto>> GetAllTaxesAsync(CancellationToken cancellationToken = default)
        {
            var taxes = await _context.Set<Tax>()
                .Where(t => t.TenantId == TenantId && !t.IsDeleted)
                .OrderBy(t => t.Code)
                .ToListAsync(cancellationToken);

            return taxes.Select(MapToTaxDto).ToList();
        }

        public async Task<IReadOnlyList<TaxDto>> GetActiveTaxesAsync(TaxApplicability? applicability = null, CancellationToken cancellationToken = default)
        {
            var query = _context.Set<Tax>()
                .Where(t => t.TenantId == TenantId && t.IsActive && !t.IsDeleted);

            if (applicability.HasValue)
            {
                query = query.Where(t => t.Applicability == applicability.Value || t.Applicability == TaxApplicability.Both);
            }

            var taxes = await query.OrderBy(t => t.Code).ToListAsync(cancellationToken);
            return taxes.Select(MapToTaxDto).ToList();
        }

        public async Task<TaxDto?> GetTaxByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var tax = await _context.Set<Tax>()
                .FirstOrDefaultAsync(t => t.Id == id && t.TenantId == TenantId && !t.IsDeleted, cancellationToken);

            return tax == null ? null : MapToTaxDto(tax);
        }

        public async Task<TaxDto?> GetTaxByCodeAsync(string code, CancellationToken cancellationToken = default)
        {
            var tax = await _context.Set<Tax>()
                .FirstOrDefaultAsync(t => t.Code == code && t.TenantId == TenantId && !t.IsDeleted, cancellationToken);

            return tax == null ? null : MapToTaxDto(tax);
        }

        public async Task<TaxDto> CreateTaxAsync(CreateTaxDto dto, CancellationToken cancellationToken = default)
        {
            var effectiveFrom = (dto.EffectiveFrom ?? DateTime.UtcNow).Date;
            await ValidateTaxConfigurationAsync(
                dto.Code,
                dto.Name,
                dto.Rate,
                effectiveFrom,
                dto.IsActive,
                dto.TaxPayableAccountId,
                dto.TaxReceivableAccountId,
                cancellationToken);

            // Validate unique code
            var existing = await _context.Set<Tax>()
                .FirstOrDefaultAsync(t => t.Code == dto.Code && t.TenantId == TenantId && !t.IsDeleted, cancellationToken);

            if (existing != null)
                throw new InvalidOperationException($"Tax with code '{dto.Code}' already exists.");

            var tax = new Tax
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                Code = dto.Code,
                Name = dto.Name,
                Description = dto.Description,
                Rate = dto.Rate,
                EffectiveFrom = effectiveFrom,
                Applicability = dto.Applicability,
                Category = dto.Category,
                IsActive = dto.IsActive,
                IsInputTaxDeductible = dto.IsInputTaxDeductible,
                ThresholdAmount = dto.ThresholdAmount,
                TaxPayableAccountId = dto.TaxPayableAccountId,
                TaxReceivableAccountId = dto.TaxReceivableAccountId,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = UserName
            };

            await _context.Set<Tax>().AddAsync(tax, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Created tax: {Code} - {Name} @ {Rate}%", tax.Code, tax.Name, tax.Rate);
            await RecordTaxAuditAsync(
                FinanceAuditEvents.TaxRuleCreated,
                tax,
                afterValues: new
                {
                    tax.Code,
                    tax.Name,
                    tax.Rate,
                    tax.EffectiveFrom,
                    tax.Applicability,
                    tax.Category,
                    tax.IsActive,
                    tax.TaxPayableAccountId,
                    tax.TaxReceivableAccountId
                },
                cancellationToken: cancellationToken);

            return MapToTaxDto(tax);
        }

        public async Task<TaxDto> UpdateTaxAsync(Guid id, UpdateTaxDto dto, CancellationToken cancellationToken = default)
        {
            var tax = await _context.Set<Tax>()
                .FirstOrDefaultAsync(t => t.Id == id && t.TenantId == TenantId && !t.IsDeleted, cancellationToken);

            if (tax == null)
                throw new InvalidOperationException("Tax not found.");

            if (!tax.IsActive)
            {
                throw new InvalidOperationException(
                    "Inactive tax configurations are locked and cannot be edited. Create a new tax configuration for any future effective change.");
            }

            var beforeValues = new
            {
                tax.Code,
                tax.Name,
                tax.Description,
                tax.Rate,
                tax.EffectiveFrom,
                tax.Applicability,
                tax.Category,
                tax.IsActive,
                tax.IsInputTaxDeductible,
                tax.ThresholdAmount,
                tax.TaxPayableAccountId,
                tax.TaxReceivableAccountId
            };

            var newRate = dto.Rate ?? tax.Rate;
            var newEffectiveFrom = (dto.EffectiveFrom ?? (dto.Rate.HasValue && dto.Rate.Value != tax.Rate ? DateTime.UtcNow : tax.EffectiveFrom)).Date;
            var newIsActive = dto.IsActive ?? tax.IsActive;
            Guid? newPayableAccountId = dto.ClearTaxPayableAccount
                ? null
                : dto.TaxPayableAccountId ?? tax.TaxPayableAccountId;
            Guid? newReceivableAccountId = dto.ClearTaxReceivableAccount
                ? null
                : dto.TaxReceivableAccountId ?? tax.TaxReceivableAccountId;
            var hasConfigurationChange =
                (dto.Name != null && dto.Name != tax.Name)
                || (dto.Description != null && dto.Description != tax.Description)
                || newRate != tax.Rate
                || newEffectiveFrom != tax.EffectiveFrom.Date
                || (dto.Applicability.HasValue && dto.Applicability.Value != tax.Applicability)
                || (dto.Category.HasValue && dto.Category.Value != tax.Category)
                || newIsActive != tax.IsActive
                || (dto.IsInputTaxDeductible.HasValue && dto.IsInputTaxDeductible.Value != tax.IsInputTaxDeductible)
                || (dto.ThresholdAmount.HasValue && dto.ThresholdAmount.Value != tax.ThresholdAmount)
                || newPayableAccountId != tax.TaxPayableAccountId
                || newReceivableAccountId != tax.TaxReceivableAccountId;

            if (!hasConfigurationChange)
                return MapToTaxDto(tax);

            await ValidateTaxConfigurationAsync(
                tax.Code,
                dto.Name ?? tax.Name,
                newRate,
                newEffectiveFrom,
                newIsActive,
                newPayableAccountId,
                newReceivableAccountId,
                cancellationToken);

            if (dto.Rate.HasValue
                && dto.Rate.Value != tax.Rate
                && newEffectiveFrom <= tax.EffectiveFrom.Date)
            {
                throw new InvalidOperationException("New tax rate effective date must be after the current effective date. Use a new effective-dated version instead of overwriting historical rates.");
            }

            await ArchiveTaxConfigurationAsync(tax, dto.ChangeReason, cancellationToken);

            // Track rate changes for history
            if (dto.Rate.HasValue && dto.Rate.Value != tax.Rate)
            {
                await AddRateHistoryAsync(tax, newEffectiveFrom, cancellationToken);
                tax.Rate = dto.Rate.Value;
                tax.EffectiveFrom = newEffectiveFrom;
            }

            if (dto.Name != null) tax.Name = dto.Name;
            if (dto.Description != null) tax.Description = dto.Description;
            if (dto.Applicability.HasValue) tax.Applicability = dto.Applicability.Value;
            if (dto.Category.HasValue) tax.Category = dto.Category.Value;
            if (dto.IsActive.HasValue) tax.IsActive = dto.IsActive.Value;
            if (dto.IsInputTaxDeductible.HasValue) tax.IsInputTaxDeductible = dto.IsInputTaxDeductible.Value;
            if (dto.ThresholdAmount.HasValue) tax.ThresholdAmount = dto.ThresholdAmount;
            if (dto.ClearTaxPayableAccount) tax.TaxPayableAccountId = null;
            else if (dto.TaxPayableAccountId.HasValue) tax.TaxPayableAccountId = dto.TaxPayableAccountId;
            if (dto.ClearTaxReceivableAccount) tax.TaxReceivableAccountId = null;
            else if (dto.TaxReceivableAccountId.HasValue) tax.TaxReceivableAccountId = dto.TaxReceivableAccountId;

            tax.UpdatedAt = DateTime.UtcNow;
            tax.UpdatedBy = UserName;

            _context.Set<Tax>().Update(tax);
            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Updated tax: {Code}", tax.Code);
            await RecordTaxAuditAsync(
                FinanceAuditEvents.TaxRuleUpdated,
                tax,
                beforeValues: beforeValues,
                afterValues: new
                {
                    tax.Code,
                    tax.Name,
                    tax.Description,
                    tax.Rate,
                    tax.EffectiveFrom,
                    tax.Applicability,
                    tax.Category,
                    tax.IsActive,
                    tax.IsInputTaxDeductible,
                    tax.ThresholdAmount,
                    tax.TaxPayableAccountId,
                    tax.TaxReceivableAccountId
                },
                cancellationToken: cancellationToken);

            if (!Equals(beforeValues.TaxPayableAccountId, tax.TaxPayableAccountId)
                || !Equals(beforeValues.TaxReceivableAccountId, tax.TaxReceivableAccountId))
            {
                await RecordTaxAuditAsync(
                    FinanceAuditEvents.TaxAccountMappingChanged,
                    tax,
                    beforeValues: new
                    {
                        beforeValues.TaxPayableAccountId,
                        beforeValues.TaxReceivableAccountId
                    },
                    afterValues: new
                    {
                        tax.TaxPayableAccountId,
                        tax.TaxReceivableAccountId
                    },
                    cancellationToken: cancellationToken);
            }

            return MapToTaxDto(tax);
        }

        public async Task DeleteTaxAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var tax = await _context.Set<Tax>()
                .FirstOrDefaultAsync(t => t.Id == id && t.TenantId == TenantId && !t.IsDeleted, cancellationToken);

            if (tax == null)
                throw new InvalidOperationException("Tax not found.");

            if (!tax.IsActive)
                throw new InvalidOperationException("Inactive tax configurations are locked and retained for audit. They cannot be deleted.");

            await ArchiveTaxConfigurationAsync(tax, "Tax configuration retired", cancellationToken);
            tax.IsActive = false;
            tax.UpdatedAt = DateTime.UtcNow;
            tax.UpdatedBy = UserName;

            _context.Set<Tax>().Update(tax);
            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Retired tax: {Code}", tax.Code);
            await RecordTaxAuditAsync(
                FinanceAuditEvents.TaxRuleDeactivated,
                tax,
                beforeValues: new { tax.Code, wasActive = true },
                afterValues: new { tax.Code, tax.IsActive },
                cancellationToken: cancellationToken);
        }

        private async Task AddRateHistoryAsync(Tax tax, DateTime newEffectiveFrom, CancellationToken cancellationToken)
        {
            var history = new TaxRateHistory
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                TaxId = tax.Id,
                Rate = tax.Rate,
                EffectiveFrom = tax.EffectiveFrom,
                EffectiveTo = newEffectiveFrom.AddTicks(-1),
                Notes = $"Rate changed from {tax.Rate}%",
                CreatedAt = DateTime.UtcNow,
                CreatedBy = UserName
            };

            await _context.Set<TaxRateHistory>().AddAsync(history, cancellationToken);
        }

        private async Task ArchiveTaxConfigurationAsync(
            Tax tax,
            string? changeReason,
            CancellationToken cancellationToken)
        {
            var now = DateTime.UtcNow;
            var latestVersion = await _context.Set<TaxConfigurationVersion>()
                .Where(v => v.TenantId == TenantId && v.TaxId == tax.Id && !v.IsDeleted)
                .OrderByDescending(v => v.VersionNumber)
                .FirstOrDefaultAsync(cancellationToken);

            var snapshot = new TaxConfigurationVersion
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                TaxId = tax.Id,
                VersionNumber = (latestVersion?.VersionNumber ?? 0) + 1,
                Code = tax.Code,
                Name = tax.Name,
                Description = tax.Description,
                Rate = tax.Rate,
                EffectiveFrom = tax.EffectiveFrom,
                Applicability = tax.Applicability,
                Category = tax.Category,
                IsActive = tax.IsActive,
                IsInputTaxDeductible = tax.IsInputTaxDeductible,
                ThresholdAmount = tax.ThresholdAmount,
                TaxPayableAccountId = tax.TaxPayableAccountId,
                TaxReceivableAccountId = tax.TaxReceivableAccountId,
                ValidFrom = latestVersion?.ValidTo ?? tax.CreatedAt,
                ValidTo = now,
                ChangeReason = string.IsNullOrWhiteSpace(changeReason) ? null : changeReason.Trim(),
                CreatedAt = now,
                CreatedBy = UserName
            };

            await _context.Set<TaxConfigurationVersion>().AddAsync(snapshot, cancellationToken);
        }

        #endregion

        #region Tax Rate History

        public async Task<IReadOnlyList<TaxRateHistoryDto>> GetTaxRateHistoryAsync(Guid taxId, CancellationToken cancellationToken = default)
        {
            var history = await _context.Set<TaxRateHistory>()
                .Where(h => h.TaxId == taxId && h.TenantId == TenantId && !h.IsDeleted)
                .OrderByDescending(h => h.EffectiveFrom)
                .ToListAsync(cancellationToken);

            return history.Select(h => new TaxRateHistoryDto
            {
                Id = h.Id,
                TaxId = h.TaxId,
                Rate = h.Rate,
                EffectiveFrom = h.EffectiveFrom,
                EffectiveTo = h.EffectiveTo,
                Notes = h.Notes,
                CreatedBy = h.CreatedBy,
                CreatedAt = h.CreatedAt
            }).ToList();
        }

        public async Task<IReadOnlyList<TaxConfigurationVersionDto>> GetTaxConfigurationVersionsAsync(
            Guid taxId,
            CancellationToken cancellationToken = default)
        {
            var tax = await _context.Set<Tax>()
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    t => t.Id == taxId && t.TenantId == TenantId && !t.IsDeleted,
                    cancellationToken);

            if (tax == null)
                throw new InvalidOperationException("Tax not found.");

            var versions = await _context.Set<TaxConfigurationVersion>()
                .AsNoTracking()
                .Where(v => v.TaxId == taxId && v.TenantId == TenantId && !v.IsDeleted)
                .OrderBy(v => v.VersionNumber)
                .ToListAsync(cancellationToken);

            var result = versions.Select(v => new TaxConfigurationVersionDto
            {
                Id = v.Id,
                TaxId = v.TaxId,
                VersionNumber = v.VersionNumber,
                Code = v.Code,
                Name = v.Name,
                Description = v.Description,
                Rate = v.Rate,
                EffectiveFrom = v.EffectiveFrom,
                Applicability = v.Applicability,
                Category = v.Category,
                IsActive = v.IsActive,
                IsInputTaxDeductible = v.IsInputTaxDeductible,
                ThresholdAmount = v.ThresholdAmount,
                TaxPayableAccountId = v.TaxPayableAccountId,
                TaxReceivableAccountId = v.TaxReceivableAccountId,
                ValidFrom = v.ValidFrom,
                ValidTo = v.ValidTo,
                ChangeReason = v.ChangeReason,
                ChangedBy = v.CreatedBy,
                IsCurrent = false
            }).ToList();

            var currentVersionNumber = (versions.LastOrDefault()?.VersionNumber ?? 0) + 1;
            result.Add(new TaxConfigurationVersionDto
            {
                Id = tax.Id,
                TaxId = tax.Id,
                VersionNumber = currentVersionNumber,
                Code = tax.Code,
                Name = tax.Name,
                Description = tax.Description,
                Rate = tax.Rate,
                EffectiveFrom = tax.EffectiveFrom,
                Applicability = tax.Applicability,
                Category = tax.Category,
                IsActive = tax.IsActive,
                IsInputTaxDeductible = tax.IsInputTaxDeductible,
                ThresholdAmount = tax.ThresholdAmount,
                TaxPayableAccountId = tax.TaxPayableAccountId,
                TaxReceivableAccountId = tax.TaxReceivableAccountId,
                ValidFrom = versions.LastOrDefault()?.ValidTo ?? tax.CreatedAt,
                ValidTo = null,
                ChangedBy = tax.UpdatedBy ?? tax.CreatedBy,
                IsCurrent = true
            });

            return result.OrderByDescending(v => v.VersionNumber).ToList();
        }

        #endregion

        #region Tax Groups

        public async Task<IReadOnlyList<TaxGroupDto>> GetAllTaxGroupsAsync(CancellationToken cancellationToken = default)
        {
            var groups = await _context.Set<TaxGroup>()
                .Where(g => g.TenantId == TenantId && !g.IsDeleted)
                .Include(g => g.Components.Where(c => !c.IsDeleted))
                    .ThenInclude(c => c.Tax)
                .OrderBy(g => g.Code)
                .ToListAsync(cancellationToken);

            return groups.Select(MapToTaxGroupDto).ToList();
        }

        public async Task<IReadOnlyList<TaxGroupDto>> GetActiveTaxGroupsAsync(TaxApplicability? applicability = null, CancellationToken cancellationToken = default)
        {
            var query = _context.Set<TaxGroup>()
                .Where(g => g.TenantId == TenantId && g.IsActive && !g.IsDeleted);

            if (applicability.HasValue)
            {
                query = query.Where(g => g.Applicability == applicability.Value || g.Applicability == TaxApplicability.Both);
            }

            var groups = await query
                .Include(g => g.Components.Where(c => !c.IsDeleted))
                    .ThenInclude(c => c.Tax)
                .OrderBy(g => g.Code)
                .ToListAsync(cancellationToken);

            return groups.Select(MapToTaxGroupDto).ToList();
        }

        public async Task<TaxGroupDto?> GetTaxGroupByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var group = await _context.Set<TaxGroup>()
                .Include(g => g.Components.Where(c => !c.IsDeleted))
                    .ThenInclude(c => c.Tax)
                .FirstOrDefaultAsync(g => g.Id == id && g.TenantId == TenantId && !g.IsDeleted, cancellationToken);

            return group == null ? null : MapToTaxGroupDto(group);
        }

        public async Task<TaxGroupDto?> GetTaxGroupByCodeAsync(string code, CancellationToken cancellationToken = default)
        {
            var group = await _context.Set<TaxGroup>()
                .Include(g => g.Components.Where(c => !c.IsDeleted))
                    .ThenInclude(c => c.Tax)
                .FirstOrDefaultAsync(g => g.Code == code && g.TenantId == TenantId && !g.IsDeleted, cancellationToken);

            return group == null ? null : MapToTaxGroupDto(group);
        }

        public async Task<TaxGroupDto> CreateTaxGroupAsync(CreateTaxGroupDto dto, CancellationToken cancellationToken = default)
        {
            // Validate unique code
            var existing = await _context.Set<TaxGroup>()
                .FirstOrDefaultAsync(g => g.Code == dto.Code && g.TenantId == TenantId && !g.IsDeleted, cancellationToken);

            if (existing != null)
                throw new InvalidOperationException($"Tax group with code '{dto.Code}' already exists.");

            // If setting as default, unset other defaults
            if (dto.IsDefault)
            {
                await UnsetDefaultGroupsAsync(dto.Applicability, cancellationToken);
            }

            var group = new TaxGroup
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                Code = dto.Code,
                Name = dto.Name,
                Description = dto.Description,
                Applicability = dto.Applicability,
                IsDefault = dto.IsDefault,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = UserName
            };

            await _context.Set<TaxGroup>().AddAsync(group, cancellationToken);

            // Add components if provided
            if (dto.Components != null && dto.Components.Any())
            {
                foreach (var compDto in dto.Components)
                {
                    var component = new TaxGroupComponent
                    {
                        Id = Guid.NewGuid(),
                        TenantId = TenantId,
                        TaxGroupId = group.Id,
                        TaxId = compDto.TaxId,
                        CalculationOrder = compDto.CalculationOrder,
                        CompoundBasis = compDto.CompoundBasis,
                        AppliesOnTaxCodes = compDto.AppliesOnTaxCodes != null ? string.Join(",", compDto.AppliesOnTaxCodes) : null,
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = UserName
                    };
                    await _context.Set<TaxGroupComponent>().AddAsync(component, cancellationToken);
                }
            }

            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Created tax group: {Code} - {Name}", group.Code, group.Name);

            // Reload with components
            return (await GetTaxGroupByIdAsync(group.Id, cancellationToken))!;
        }

        public async Task<TaxGroupDto> UpdateTaxGroupAsync(Guid id, UpdateTaxGroupDto dto, CancellationToken cancellationToken = default)
        {
            var group = await _context.Set<TaxGroup>()
                .FirstOrDefaultAsync(g => g.Id == id && g.TenantId == TenantId && !g.IsDeleted, cancellationToken);

            if (group == null)
                throw new InvalidOperationException("Tax group not found.");

            // If setting as default, unset other defaults
            if (dto.IsDefault == true && !group.IsDefault)
            {
                await UnsetDefaultGroupsAsync(dto.Applicability ?? group.Applicability, cancellationToken);
            }

            if (dto.Name != null) group.Name = dto.Name;
            if (dto.Description != null) group.Description = dto.Description;
            if (dto.Applicability.HasValue) group.Applicability = dto.Applicability.Value;
            if (dto.IsDefault.HasValue) group.IsDefault = dto.IsDefault.Value;
            if (dto.IsActive.HasValue) group.IsActive = dto.IsActive.Value;

            group.UpdatedAt = DateTime.UtcNow;
            group.UpdatedBy = UserName;

            _context.Set<TaxGroup>().Update(group);
            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Updated tax group: {Code}", group.Code);

            return (await GetTaxGroupByIdAsync(group.Id, cancellationToken))!;
        }

        public async Task DeleteTaxGroupAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var group = await _context.Set<TaxGroup>()
                .FirstOrDefaultAsync(g => g.Id == id && g.TenantId == TenantId && !g.IsDeleted, cancellationToken);

            if (group == null)
                throw new InvalidOperationException("Tax group not found.");

            // Check for usage
            var hasCalculations = await _context.Set<TaxCalculation>()
                .AnyAsync(c => c.TaxGroupId == id && c.TenantId == TenantId && !c.IsDeleted, cancellationToken);

            if (hasCalculations)
                throw new InvalidOperationException("Cannot delete tax group with existing calculations. Deactivate instead.");

            group.IsDeleted = true;
            group.UpdatedAt = DateTime.UtcNow;
            group.UpdatedBy = UserName;

            _context.Set<TaxGroup>().Update(group);
            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Deleted tax group: {Code}", group.Code);
        }

        private async Task UnsetDefaultGroupsAsync(TaxApplicability applicability, CancellationToken cancellationToken)
        {
            var defaults = await _context.Set<TaxGroup>()
                .Where(g => g.TenantId == TenantId && g.IsDefault && !g.IsDeleted 
                    && (g.Applicability == applicability || g.Applicability == TaxApplicability.Both))
                .ToListAsync(cancellationToken);

            foreach (var g in defaults)
            {
                g.IsDefault = false;
                g.UpdatedAt = DateTime.UtcNow;
                g.UpdatedBy = UserName;
            }
        }

        private async Task ValidateTaxConfigurationAsync(
            string code,
            string name,
            decimal rate,
            DateTime effectiveFrom,
            bool isActive,
            Guid? taxPayableAccountId,
            Guid? taxReceivableAccountId,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(code))
                throw new InvalidOperationException("Tax code is required.");

            if (string.IsNullOrWhiteSpace(name))
                throw new InvalidOperationException("Tax name is required.");

            if (rate < 0)
                throw new InvalidOperationException("Tax rate cannot be negative.");

            if (effectiveFrom == default)
                throw new InvalidOperationException("Tax effective date is required.");

            if (IsCovidHealthRecoveryLevy(code, name) && isActive && effectiveFrom.Date <= DateTime.UtcNow.Date)
            {
                throw new InvalidOperationException("COVID-19 Health Recovery Levy must not be active for current Ghana postings.");
            }

            await ValidateTaxAccountAsync(taxPayableAccountId, "tax payable account", cancellationToken);
            await ValidateTaxAccountAsync(taxReceivableAccountId, "tax receivable account", cancellationToken);
        }

        private async Task ValidateTaxAccountAsync(
            Guid? accountId,
            string label,
            CancellationToken cancellationToken)
        {
            if (!accountId.HasValue)
            {
                return;
            }

            var account = await _context.Accounts
                .FirstOrDefaultAsync(a => a.TenantId == TenantId && a.Id == accountId.Value && !a.IsDeleted, cancellationToken);

            if (account == null)
                throw new InvalidOperationException($"Configured {label} was not found for this tenant.");

            if (account.Status != AccountStatus.Active)
                throw new InvalidOperationException($"Configured {label} is not active.");
        }

        private static bool IsCovidHealthRecoveryLevy(string code, string name)
        {
            return code.Contains("COVID", StringComparison.OrdinalIgnoreCase)
                || name.Contains("COVID", StringComparison.OrdinalIgnoreCase)
                || name.Contains("Health Recovery", StringComparison.OrdinalIgnoreCase);
        }

        #endregion

        #region Tax Group Components

        public async Task<TaxGroupComponentDto> AddComponentToGroupAsync(Guid groupId, AddTaxGroupComponentDto dto, CancellationToken cancellationToken = default)
        {
            var group = await _context.Set<TaxGroup>()
                .Include(g => g.Components.Where(c => !c.IsDeleted))
                .FirstOrDefaultAsync(g => g.Id == groupId && g.TenantId == TenantId && !g.IsDeleted, cancellationToken);

            if (group == null)
                throw new InvalidOperationException("Tax group not found.");

            var tax = await _context.Set<Tax>()
                .FirstOrDefaultAsync(t => t.Id == dto.TaxId && t.TenantId == TenantId && !t.IsDeleted, cancellationToken);

            if (tax == null)
                throw new InvalidOperationException("Tax not found.");

            // Check if already in group
            if (group.Components.Any(c => c.TaxId == dto.TaxId))
                throw new InvalidOperationException("Tax is already in this group.");

            var order = dto.CalculationOrder ?? (group.Components.Any() ? group.Components.Max(c => c.CalculationOrder) + 1 : 1);

            var component = new TaxGroupComponent
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                TaxGroupId = groupId,
                TaxId = dto.TaxId,
                CalculationOrder = order,
                CompoundBasis = dto.CompoundBasis,
                AppliesOnTaxCodes = dto.AppliesOnTaxCodes != null ? string.Join(",", dto.AppliesOnTaxCodes) : null,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = UserName
            };

            await _context.Set<TaxGroupComponent>().AddAsync(component, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Added {TaxCode} to group {GroupCode}", tax.Code, group.Code);

            return new TaxGroupComponentDto
            {
                Id = component.Id,
                TaxId = tax.Id,
                TaxCode = tax.Code,
                TaxName = tax.Name,
                TaxRate = tax.Rate,
                TaxCategory = tax.Category,
                CalculationOrder = component.CalculationOrder,
                CompoundBasis = component.CompoundBasis,
                AppliesOnTaxCodes = dto.AppliesOnTaxCodes
            };
        }

        public async Task<TaxGroupComponentDto> UpdateComponentAsync(Guid componentId, UpdateTaxGroupComponentDto dto, CancellationToken cancellationToken = default)
        {
            var component = await _context.Set<TaxGroupComponent>()
                .Include(c => c.Tax)
                .FirstOrDefaultAsync(c => c.Id == componentId && c.TenantId == TenantId && !c.IsDeleted, cancellationToken);

            if (component == null)
                throw new InvalidOperationException("Component not found.");

            if (dto.CalculationOrder.HasValue) component.CalculationOrder = dto.CalculationOrder.Value;
            if (dto.CompoundBasis.HasValue)
            {
                component.CompoundBasis = dto.CompoundBasis.Value;
                if (dto.CompoundBasis.Value != CompoundBasis.Specific)
                {
                    component.AppliesOnTaxCodes = null;
                }
            }
            if (dto.AppliesOnTaxCodes != null) component.AppliesOnTaxCodes = string.Join(",", dto.AppliesOnTaxCodes);

            component.UpdatedAt = DateTime.UtcNow;
            component.UpdatedBy = UserName;

            _context.Set<TaxGroupComponent>().Update(component);
            await _context.SaveChangesAsync(cancellationToken);

            return new TaxGroupComponentDto
            {
                Id = component.Id,
                TaxId = component.TaxId,
                TaxCode = component.Tax.Code,
                TaxName = component.Tax.Name,
                TaxRate = component.Tax.Rate,
                TaxCategory = component.Tax.Category,
                CalculationOrder = component.CalculationOrder,
                CompoundBasis = component.CompoundBasis,
                AppliesOnTaxCodes = string.IsNullOrWhiteSpace(component.AppliesOnTaxCodes) 
                    ? null 
                    : component.AppliesOnTaxCodes.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList()
            };
        }

        public async Task RemoveComponentFromGroupAsync(Guid componentId, CancellationToken cancellationToken = default)
        {
            var component = await _context.Set<TaxGroupComponent>()
                .FirstOrDefaultAsync(c => c.Id == componentId && c.TenantId == TenantId && !c.IsDeleted, cancellationToken);

            if (component == null)
                throw new InvalidOperationException("Component not found.");

            component.IsDeleted = true;
            component.UpdatedAt = DateTime.UtcNow;
            component.UpdatedBy = UserName;

            _context.Set<TaxGroupComponent>().Update(component);
            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Removed component {Id} from group", componentId);
        }

        public async Task ReorderComponentsAsync(Guid groupId, List<Guid> orderedComponentIds, CancellationToken cancellationToken = default)
        {
            var components = await _context.Set<TaxGroupComponent>()
                .Where(c => c.TaxGroupId == groupId && c.TenantId == TenantId && !c.IsDeleted)
                .ToListAsync(cancellationToken);

            for (int i = 0; i < orderedComponentIds.Count; i++)
            {
                var component = components.FirstOrDefault(c => c.Id == orderedComponentIds[i]);
                if (component != null)
                {
                    component.CalculationOrder = i + 1;
                    component.UpdatedAt = DateTime.UtcNow;
                    component.UpdatedBy = UserName;
                }
            }

            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Reordered components in group {GroupId}", groupId);
        }

        #endregion

        #region Seeding

        public async Task SeedGhanaTaxesAsync(CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Seeding Ghana taxes for tenant {TenantId}", TenantId);

            // Check if already seeded
            var existing = await _context.Set<Tax>()
                .AnyAsync(t => t.TenantId == TenantId
                    && (t.Code == "VAT" || t.Code == "VAT-STD")
                    && !t.IsDeleted, cancellationToken);

            if (existing)
            {
                await RepairGhanaVatStandardComponentsAsync(cancellationToken);
                _logger.LogInformation("Ghana taxes already seeded for tenant {TenantId}; repaired standard VAT component settings", TenantId);
                return;
            }

            var now = DateTime.UtcNow;
            var effectiveFrom = new DateTime(2024, 1, 1);

            // Create individual taxes
            var nhil = new Tax
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                Code = "NHIL",
                Name = "National Health Insurance Levy",
                Description = "Ghana NHIL at 2.5%",
                Rate = 2.50m,
                EffectiveFrom = effectiveFrom,
                Applicability = TaxApplicability.Both,
                Category = TaxCategory.Levy,
                IsActive = true,
                IsInputTaxDeductible = true,
                CreatedAt = now,
                CreatedBy = "system"
            };

            var getfl = new Tax
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                Code = "GETFL",
                Name = "Ghana Education Trust Fund Levy",
                Description = "Ghana GETFL at 2.5%",
                Rate = 2.50m,
                EffectiveFrom = effectiveFrom,
                Applicability = TaxApplicability.Both,
                Category = TaxCategory.Levy,
                IsActive = true,
                IsInputTaxDeductible = true,
                CreatedAt = now,
                CreatedBy = "system"
            };

            var covid = new Tax
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                Code = "COVID",
                Name = "COVID-19 Health Recovery Levy",
                Description = "Historical Ghana COVID-19 Health Recovery Levy. Inactive for current postings.",
                Rate = 1.00m,
                EffectiveFrom = effectiveFrom,
                Applicability = TaxApplicability.Both,
                Category = TaxCategory.Levy,
                IsActive = false,
                IsInputTaxDeductible = true,
                CreatedAt = now,
                CreatedBy = "system"
            };

            var vat = new Tax
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                Code = "VAT",
                Name = "Value Added Tax",
                Description = "Ghana VAT at 15% on the base taxable amount",
                Rate = 15.00m,
                EffectiveFrom = effectiveFrom,
                Applicability = TaxApplicability.Both,
                Category = TaxCategory.Standard,
                IsActive = true,
                IsInputTaxDeductible = true,
                CreatedAt = now,
                CreatedBy = "system"
            };

            var whtSvc = new Tax
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                Code = "WHT-SVC",
                Name = "Withholding Tax - Services",
                Description = "WHT on services at 7.5% (threshold: GHS 2,000)",
                Rate = 7.50m,
                EffectiveFrom = effectiveFrom,
                Applicability = TaxApplicability.Purchases,
                Category = TaxCategory.Withholding,
                IsActive = true,
                IsInputTaxDeductible = false,
                ThresholdAmount = 2000m,
                CreatedAt = now,
                CreatedBy = "system"
            };

            var whtGen = new Tax
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                Code = "WHT-GEN",
                Name = "Withholding Tax - General",
                Description = "WHT on goods at 3% (threshold: GHS 2,000)",
                Rate = 3.00m,
                EffectiveFrom = effectiveFrom,
                Applicability = TaxApplicability.Purchases,
                Category = TaxCategory.Withholding,
                IsActive = true,
                IsInputTaxDeductible = false,
                ThresholdAmount = 2000m,
                CreatedAt = now,
                CreatedBy = "system"
            };

            await _context.Set<Tax>().AddRangeAsync(new[] { nhil, getfl, covid, vat, whtSvc, whtGen }, cancellationToken);

            // Create tax groups
            var salesGroup = new TaxGroup
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                Code = "GH-SALES-STD",
                Name = "Ghana Standard Sales Tax",
                Description = "VAT 15%, NHIL 2.5%, and GETFund 2.5% on the same taxable base.",
                Applicability = TaxApplicability.Sales,
                IsDefault = true,
                IsActive = true,
                CreatedAt = now,
                CreatedBy = "system"
            };

            var purchaseGroup = new TaxGroup
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                Code = "GH-PURCH-STD",
                Name = "Ghana Standard Purchase Tax",
                Description = "VAT 15%, NHIL 2.5%, and GETFund 2.5% on the same taxable base.",
                Applicability = TaxApplicability.Purchases,
                IsDefault = true,
                IsActive = true,
                CreatedAt = now,
                CreatedBy = "system"
            };

            var whtSvcGroup = new TaxGroup
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                Code = "GH-WHT-SVC",
                Name = "Ghana WHT - Services",
                Description = "Withholding Tax on services (7.5%)",
                Applicability = TaxApplicability.Purchases,
                IsDefault = false,
                IsActive = true,
                CreatedAt = now,
                CreatedBy = "system"
            };

            var whtGenGroup = new TaxGroup
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                Code = "GH-WHT-GEN",
                Name = "Ghana WHT - General",
                Description = "Withholding Tax on goods (3%)",
                Applicability = TaxApplicability.Purchases,
                IsDefault = false,
                IsActive = true,
                CreatedAt = now,
                CreatedBy = "system"
            };

            await _context.Set<TaxGroup>().AddRangeAsync(new[] { salesGroup, purchaseGroup, whtSvcGroup, whtGenGroup }, cancellationToken);

            // Create components for sales group
            await _context.Set<TaxGroupComponent>().AddRangeAsync(new[]
            {
                new TaxGroupComponent
                {
                    Id = Guid.NewGuid(), TenantId = TenantId, TaxGroupId = salesGroup.Id, TaxId = nhil.Id,
                    CalculationOrder = 1, CompoundBasis = CompoundBasis.BaseOnly, CreatedAt = now, CreatedBy = "system"
                },
                new TaxGroupComponent
                {
                    Id = Guid.NewGuid(), TenantId = TenantId, TaxGroupId = salesGroup.Id, TaxId = getfl.Id,
                    CalculationOrder = 2, CompoundBasis = CompoundBasis.BaseOnly, CreatedAt = now, CreatedBy = "system"
                },
                new TaxGroupComponent
                {
                    Id = Guid.NewGuid(), TenantId = TenantId, TaxGroupId = salesGroup.Id, TaxId = vat.Id,
                    CalculationOrder = 3, CompoundBasis = CompoundBasis.BaseOnly,
                    CreatedAt = now, CreatedBy = "system"
                }
            }, cancellationToken);

            // Create components for purchase group (same structure)
            await _context.Set<TaxGroupComponent>().AddRangeAsync(new[]
            {
                new TaxGroupComponent
                {
                    Id = Guid.NewGuid(), TenantId = TenantId, TaxGroupId = purchaseGroup.Id, TaxId = nhil.Id,
                    CalculationOrder = 1, CompoundBasis = CompoundBasis.BaseOnly, CreatedAt = now, CreatedBy = "system"
                },
                new TaxGroupComponent
                {
                    Id = Guid.NewGuid(), TenantId = TenantId, TaxGroupId = purchaseGroup.Id, TaxId = getfl.Id,
                    CalculationOrder = 2, CompoundBasis = CompoundBasis.BaseOnly, CreatedAt = now, CreatedBy = "system"
                },
                new TaxGroupComponent
                {
                    Id = Guid.NewGuid(), TenantId = TenantId, TaxGroupId = purchaseGroup.Id, TaxId = vat.Id,
                    CalculationOrder = 3, CompoundBasis = CompoundBasis.BaseOnly,
                    CreatedAt = now, CreatedBy = "system"
                }
            }, cancellationToken);

            // WHT groups
            await _context.Set<TaxGroupComponent>().AddAsync(new TaxGroupComponent
            {
                Id = Guid.NewGuid(), TenantId = TenantId, TaxGroupId = whtSvcGroup.Id, TaxId = whtSvc.Id,
                CalculationOrder = 1, CompoundBasis = CompoundBasis.BaseOnly, CreatedAt = now, CreatedBy = "system"
            }, cancellationToken);

            await _context.Set<TaxGroupComponent>().AddAsync(new TaxGroupComponent
            {
                Id = Guid.NewGuid(), TenantId = TenantId, TaxGroupId = whtGenGroup.Id, TaxId = whtGen.Id,
                CalculationOrder = 1, CompoundBasis = CompoundBasis.BaseOnly, CreatedAt = now, CreatedBy = "system"
            }, cancellationToken);

            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Successfully seeded Ghana taxes: 6 taxes, 4 groups");
        }

        private async Task RepairGhanaVatStandardComponentsAsync(CancellationToken cancellationToken)
        {
            var candidateGroups = await _context.Set<TaxGroup>()
                .Include(g => g.Components)
                    .ThenInclude(c => c.Tax)
                .Where(g => g.TenantId == TenantId
                    && !g.IsDeleted
                    && (g.Code == "VAT-STD-SCHEME"
                        || g.Code == "GH-SALES-STD"
                        || g.Code == "GH-PURCH-STD"
                        || g.Name.Contains("VAT Standard")
                        || g.Name.Contains("Ghana Standard")))
                .ToListAsync(cancellationToken);

            var now = DateTime.UtcNow;
            var changed = false;

            var covidTaxes = await _context.Set<Tax>()
                .Where(t => t.TenantId == TenantId
                    && !t.IsDeleted
                    && (t.Code.Contains("COVID") || t.Name.Contains("COVID") || t.Name.Contains("Health Recovery")))
                .ToListAsync(cancellationToken);

            foreach (var tax in covidTaxes)
            {
                if (tax.IsActive)
                {
                    tax.IsActive = false;
                    tax.UpdatedAt = now;
                    tax.UpdatedBy = "system";
                    changed = true;
                }

                if (string.IsNullOrWhiteSpace(tax.Description)
                    || !tax.Description.Contains("Inactive for current postings", StringComparison.OrdinalIgnoreCase))
                {
                    tax.Description = "Historical Ghana COVID-19 Health Recovery Levy. Inactive for current postings.";
                    tax.UpdatedAt = now;
                    tax.UpdatedBy = "system";
                    changed = true;
                }
            }

            foreach (var group in candidateGroups)
            {
                var activeDescription = "VAT 15%, NHIL 2.5%, and GETFund 2.5% on the same taxable base.";
                if (string.Equals(group.Code, "VAT-STD-SCHEME", StringComparison.OrdinalIgnoreCase))
                {
                    if (!string.Equals(group.Description, activeDescription, StringComparison.Ordinal))
                    {
                        group.Description = activeDescription;
                        group.UpdatedAt = now;
                        group.UpdatedBy = "system";
                        changed = true;
                    }
                }
                else if (string.Equals(group.Code, "GH-SALES-STD", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(group.Code, "GH-PURCH-STD", StringComparison.OrdinalIgnoreCase))
                {
                    if (!string.Equals(group.Description, activeDescription, StringComparison.Ordinal))
                    {
                        group.Description = activeDescription;
                        group.UpdatedAt = now;
                        group.UpdatedBy = "system";
                        changed = true;
                    }
                }

                foreach (var component in group.Components.Where(c => !c.IsDeleted))
                {
                    var taxCode = component.Tax?.Code ?? string.Empty;
                    var taxName = component.Tax?.Name ?? string.Empty;
                    var isCovidComponent =
                        taxCode.Contains("COVID", StringComparison.OrdinalIgnoreCase)
                        || taxName.Contains("COVID", StringComparison.OrdinalIgnoreCase)
                        || taxName.Contains("Health Recovery", StringComparison.OrdinalIgnoreCase);

                    if (isCovidComponent)
                    {
                        component.IsDeleted = true;
                        component.DeletedAt = now;
                        component.DeletedBy = "system";
                        component.UpdatedAt = now;
                        component.UpdatedBy = "system";
                        changed = true;
                        continue;
                    }

                    var isVatComponent =
                        string.Equals(taxCode, "VAT", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(taxCode, "VAT-STD", StringComparison.OrdinalIgnoreCase)
                        || taxName.Contains("Value Added Tax", StringComparison.OrdinalIgnoreCase);

                    if (!isVatComponent)
                    {
                        continue;
                    }

                    if (component.CompoundBasis != CompoundBasis.BaseOnly)
                    {
                        component.CompoundBasis = CompoundBasis.BaseOnly;
                        changed = true;
                    }

                    if (!string.IsNullOrWhiteSpace(component.AppliesOnTaxCodes))
                    {
                        component.AppliesOnTaxCodes = null;
                        changed = true;
                    }

                    component.UpdatedAt = now;
                    component.UpdatedBy = "system";
                }
            }

            if (changed)
            {
                await _context.SaveChangesAsync(cancellationToken);
                _logger.LogInformation("Repaired Ghana standard VAT components for tenant {TenantId}", TenantId);
            }
        }

        public async Task SeedTaxRulesAsync(CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Seeding tax rules for tenant {TenantId}", TenantId);

            if (await _context.TaxRules.AnyAsync(r => r.TenantId == TenantId, cancellationToken))
            {
                _logger.LogInformation("Tax rules already seeded for tenant {TenantId}", TenantId);
                return;
            }

            var salesGroup = await _context.Set<TaxGroup>().FirstOrDefaultAsync(g => g.Code == "GH-SALES-STD" && g.TenantId == TenantId, cancellationToken);
            var whtSvcGroup = await _context.Set<TaxGroup>().FirstOrDefaultAsync(g => g.Code == "GH-WHT-SVC" && g.TenantId == TenantId, cancellationToken);

            if (salesGroup == null)
            {
                 _logger.LogWarning("Cannot seed rules: Required tax groups not found.");
                 return;
            }

            var rules = new List<TaxRule>();
            var now = DateTime.UtcNow;

            // 1. Corporate Sales Rule
            rules.Add(new TaxRule
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                Name = "Corporate Sales GST",
                Description = "Standard GST for Corporate Customers",
                Priority = 1,
                TaxGroupId = salesGroup.Id,
                TransactionType = "SaleOfGoods",
                CustomerType = "Corporate",
                IsActive = true,
                CreatedAt = now
            });

            // 2. Individual Sales Rule
            rules.Add(new TaxRule
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                Name = "Individual Sales GST",
                Description = "Standard GST for Individual Customers",
                Priority = 2,
                TaxGroupId = salesGroup.Id,
                TransactionType = "SaleOfGoods",
                CustomerType = "Individual",
                IsActive = true,
                CreatedAt = now
            });

            // 3. WHT Services Rule
            if (whtSvcGroup != null)
            {
                rules.Add(new TaxRule
                {
                    Id = Guid.NewGuid(),
                    TenantId = TenantId,
                    Name = "Service WHT",
                    Description = "Withholding Tax for Purchase of Services",
                    Priority = 1,
                    TaxGroupId = whtSvcGroup.Id,
                    TransactionType = "PurchaseOfServices",
                    IsActive = true,
                    CreatedAt = now
                });
            }

            await _context.TaxRules.AddRangeAsync(rules, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Seeded {Count} tax rules", rules.Count);
        }

        #endregion

        #region Private Helper Methods

        private static TaxDto MapToTaxDto(Tax tax)
        {
            return new TaxDto
            {
                Id = tax.Id,
                TenantId = tax.TenantId,
                Code = tax.Code,
                Name = tax.Name,
                Description = tax.Description,
                Rate = tax.Rate,
                EffectiveFrom = tax.EffectiveFrom,
                Applicability = tax.Applicability,
                Category = tax.Category,
                IsActive = tax.IsActive,
                IsInputTaxDeductible = tax.IsInputTaxDeductible,
                ThresholdAmount = tax.ThresholdAmount,
                TaxPayableAccountId = tax.TaxPayableAccountId,
                TaxReceivableAccountId = tax.TaxReceivableAccountId,
                CreatedBy = tax.CreatedBy,
                CreatedAt = tax.CreatedAt,
                UpdatedBy = tax.UpdatedBy,
                UpdatedAt = tax.UpdatedAt
            };
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

        private async Task RecordTaxAuditAsync(
            string eventType,
            Tax tax,
            object? beforeValues = null,
            object? afterValues = null,
            CancellationToken cancellationToken = default)
        {
            if (_financeAuditService == null)
            {
                return;
            }

            await _financeAuditService.RecordAsync(new FinanceAuditEventDto
            {
                EventType = eventType,
                TenantId = TenantId,
                SourceModule = "Tax",
                SourceDocumentType = "Tax",
                SourceDocumentId = tax.Id,
                Resource = "Finance.Tax",
                ResourceId = tax.Id.ToString(),
                BeforeValues = beforeValues,
                AfterValues = afterValues ?? new
                {
                    tax.Code,
                    tax.Name,
                    tax.Rate,
                    tax.EffectiveFrom,
                    tax.IsActive
                }
            }, cancellationToken);
        }

        private async Task RecordTaxGroupAuditAsync(
            string eventType,
            TaxGroup group,
            object? beforeValues = null,
            object? afterValues = null,
            CancellationToken cancellationToken = default)
        {
            if (_financeAuditService == null)
            {
                return;
            }

            await _financeAuditService.RecordAsync(new FinanceAuditEventDto
            {
                EventType = eventType,
                TenantId = TenantId,
                SourceModule = "Tax",
                SourceDocumentType = "TaxGroup",
                SourceDocumentId = group.Id,
                Resource = "Finance.TaxGroup",
                ResourceId = group.Id.ToString(),
                BeforeValues = beforeValues,
                AfterValues = afterValues ?? new
                {
                    group.Code,
                    group.Name,
                    group.Applicability,
                    group.IsDefault,
                    group.IsActive
                }
            }, cancellationToken);
        }

        #endregion
    }
}
