using System;
using System.Threading;
using System.Threading.Tasks;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance;

namespace ErpSystem.Core.Interfaces.Finance
{
    /// <summary>
    /// Service contract for foreign currency revaluation operations.
    /// Handles unrealized gain/loss calculations and journal entry generation.
    /// </summary>
    public interface ICurrencyRevaluationService
    {
        /// <summary>
        /// Runs currency revaluation for accounts with foreign currency balances.
        /// Creates journal entries to record unrealized gains/losses.
        /// </summary>
        /// <param name="request">Revaluation parameters including date, currency filter, and preview mode</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Journal entry with revaluation adjustments</returns>
        Task<JournalEntry> RunCurrencyRevaluationAsync(
            RevaluationRequestDto request,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the history of revaluation journal entries for a specific period.
        /// </summary>
        /// <param name="startDate">Start date of the period</param>
        /// <param name="endDate">End date of the period</param>
        /// <param name="currencyCode">Optional currency filter</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>List of revaluation journal entries</returns>
        Task<System.Collections.Generic.IReadOnlyList<JournalEntry>> GetRevaluationHistoryAsync(
            DateTime startDate,
            DateTime endDate,
            string? currencyCode = null,
            CancellationToken cancellationToken = default);
    }
}
