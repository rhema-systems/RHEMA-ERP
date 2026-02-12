using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ErpSystem.Core.DTOs.Finance;

namespace ErpSystem.Core.Interfaces.Finance
{
    /// <summary>
    /// Service contract for managing Ratio Definitions and performing ratio calculations.
    /// Ratios can combine financial accounts, unit accounts, and constants
    /// to calculate KPIs like Revenue per Employee or Cost per Square Foot.
    /// </summary>
    public interface IRatioDefinitionService
    {
        /// <summary>
        /// Retrieves all ratio definitions for the current tenant.
        /// </summary>
        Task<IReadOnlyList<RatioDefinitionDto>> GetAllAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves active ratio definitions only.
        /// </summary>
        Task<IReadOnlyList<RatioDefinitionDto>> GetActiveAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves a single ratio definition by ID.
        /// </summary>
        Task<RatioDefinitionDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves a ratio definition by code.
        /// </summary>
        Task<RatioDefinitionDto?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);

        /// <summary>
        /// Creates a new ratio definition.
        /// </summary>
        Task<RatioDefinitionDto> CreateAsync(CreateRatioDefinitionDto dto, CancellationToken cancellationToken = default);

        /// <summary>
        /// Updates an existing ratio definition.
        /// </summary>
        Task<RatioDefinitionDto> UpdateAsync(Guid id, UpdateRatioDefinitionDto dto, CancellationToken cancellationToken = default);

        /// <summary>
        /// Activates a ratio definition.
        /// </summary>
        Task ActivateAsync(Guid id, CancellationToken cancellationToken = default);

        /// <summary>
        /// Deactivates a ratio definition.
        /// </summary>
        Task DeactivateAsync(Guid id, CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes a ratio definition (soft delete).
        /// </summary>
        Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);

        /// <summary>
        /// Calculates a ratio for a specific period.
        /// </summary>
        Task<RatioCalculationResultDto> CalculateAsync(
            Guid ratioId,
            Guid fiscalPeriodId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Calculates a ratio for a custom date range.
        /// </summary>
        Task<RatioCalculationResultDto> CalculateForDateRangeAsync(
            Guid ratioId,
            DateTime startDate,
            DateTime endDate,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Calculates all active ratios for a specific period.
        /// </summary>
        Task<IReadOnlyList<RatioCalculationResultDto>> CalculateAllAsync(
            Guid fiscalPeriodId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets ratio trend data across multiple periods.
        /// </summary>
        Task<RatioTrendResultDto> GetTrendAsync(
            Guid ratioId,
            Guid fiscalYearId,
            CancellationToken cancellationToken = default);
    }
}
