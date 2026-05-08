using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Interfaces.Finance
{
    /// <summary>
    /// Tax calculation engine interface
    /// Handles compound (tax-on-tax) calculations with granular control
    /// </summary>
    public interface ITaxCalculationEngine
    {
        /// <summary>
        /// Calculates taxes for a transaction using a tax group or manual selection
        /// </summary>
        Task<TaxCalculationResultDto> CalculateTaxesAsync(
            TaxCalculationRequestDto request,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the default tax group for a given applicability
        /// </summary>
        Task<TaxGroupDto?> GetDefaultTaxGroupAsync(
            TaxApplicability applicability,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Checks if threshold is exceeded for withholding taxes
        /// </summary>
        Task<TaxThresholdStatusDto> CheckThresholdAsync(
            Guid taxId,
            string entityType,
            Guid entityId,
            decimal amount,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Updates threshold tracking after a transaction
        /// </summary>
        Task UpdateThresholdAsync(
            Guid taxId,
            string entityType,
            Guid entityId,
            decimal amount,
            CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Tax configuration service interface - CRUD for taxes and tax groups
    /// </summary>
    public interface ITaxConfigurationService
    {
        #region Taxes

        Task<IReadOnlyList<TaxDto>> GetAllTaxesAsync(CancellationToken cancellationToken = default);
        Task<IReadOnlyList<TaxDto>> GetActiveTaxesAsync(TaxApplicability? applicability = null, CancellationToken cancellationToken = default);
        Task<TaxDto?> GetTaxByIdAsync(Guid id, CancellationToken cancellationToken = default);
        Task<TaxDto?> GetTaxByCodeAsync(string code, CancellationToken cancellationToken = default);
        Task<TaxDto> CreateTaxAsync(CreateTaxDto dto, CancellationToken cancellationToken = default);
        Task<TaxDto> UpdateTaxAsync(Guid id, UpdateTaxDto dto, CancellationToken cancellationToken = default);
        Task DeleteTaxAsync(Guid id, CancellationToken cancellationToken = default);

        #endregion

        #region Tax Rate History

        Task<IReadOnlyList<TaxRateHistoryDto>> GetTaxRateHistoryAsync(Guid taxId, CancellationToken cancellationToken = default);

        #endregion

        #region Tax Groups

        Task<IReadOnlyList<TaxGroupDto>> GetAllTaxGroupsAsync(CancellationToken cancellationToken = default);
        Task<IReadOnlyList<TaxGroupDto>> GetActiveTaxGroupsAsync(TaxApplicability? applicability = null, CancellationToken cancellationToken = default);
        Task<TaxGroupDto?> GetTaxGroupByIdAsync(Guid id, CancellationToken cancellationToken = default);
        Task<TaxGroupDto?> GetTaxGroupByCodeAsync(string code, CancellationToken cancellationToken = default);
        Task<TaxGroupDto> CreateTaxGroupAsync(CreateTaxGroupDto dto, CancellationToken cancellationToken = default);
        Task<TaxGroupDto> UpdateTaxGroupAsync(Guid id, UpdateTaxGroupDto dto, CancellationToken cancellationToken = default);
        Task DeleteTaxGroupAsync(Guid id, CancellationToken cancellationToken = default);

        #endregion

        #region Tax Group Components

        Task<TaxGroupComponentDto> AddComponentToGroupAsync(Guid groupId, AddTaxGroupComponentDto dto, CancellationToken cancellationToken = default);
        Task<TaxGroupComponentDto> UpdateComponentAsync(Guid componentId, UpdateTaxGroupComponentDto dto, CancellationToken cancellationToken = default);
        Task RemoveComponentFromGroupAsync(Guid componentId, CancellationToken cancellationToken = default);
        Task ReorderComponentsAsync(Guid groupId, List<Guid> orderedComponentIds, CancellationToken cancellationToken = default);

        #endregion

        #region Seeding

        /// <summary>
        /// Seeds Ghana default taxes and tax groups
        /// </summary>
        Task SeedGhanaTaxesAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Seeds Tax Rules
        /// </summary>
        Task SeedTaxRulesAsync(CancellationToken cancellationToken = default);

        #endregion
    }
}
