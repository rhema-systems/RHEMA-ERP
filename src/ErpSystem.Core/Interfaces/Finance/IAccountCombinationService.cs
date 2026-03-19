// FILE: src/ErpSystem.Core/Interfaces/Finance/IAccountCombinationService.cs

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ErpSystem.Core.DTOs.Finance;

namespace ErpSystem.Core.Interfaces.Finance
{
    /// <summary>
    /// Service contract for automatic segmented account combination generation.
    /// 
    /// RESPONSIBILITIES:
    /// - Generate Cartesian product of selected segment lookup values
    /// - Construct account numbers respecting current SegmentPosition ordering
    /// - Detect duplicate accounts before creation
    /// - Bulk create validated account combinations
    /// 
    /// INTEGRATION WITH SEGMENT REORDERING:
    /// - Account numbers are always constructed using current SegmentPosition
    /// - If segments are reordered after generation, existing accounts are updated by ReorderSegmentsAsync
    /// - New combinations will use the current (potentially reordered) segment positions
    /// 
    /// NOTES:
    /// - All methods are asynchronous and cancellable
    /// - TenantId is resolved inside the implementation via ICurrentUserService
    /// </summary>
    public interface IAccountCombinationService
    {
        /// <summary>
        /// Generates a preview of all account combinations from selected segment values.
        /// 
        /// ALGORITHM:
        /// 1. Fetch all segment structures ordered by SegmentPosition
        /// 2. For each segment, get selected lookup values
        /// 3. Generate Cartesian product of all selected values
        /// 4. For each combination:
        ///    - Construct account number using separator
        ///    - Generate account name from segment descriptions
        ///    - Check for existing accounts (mark as Duplicate)
        /// 5. Return list of previews with validation status
        /// 
        /// PERFORMANCE:
        /// - For 3 segments with 10 values each = 1,000 combinations
        /// - Includes duplicate detection against existing accounts
        /// </summary>
        /// <param name="request">Request with segment selections and account properties</param>
        /// <param name="cancellationToken">Optional cancellation token</param>
        /// <returns>List of account combination previews</returns>
        Task<List<AccountCombinationPreviewDto>> GenerateCombinationsAsync(
            CombinationRequestDto request,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Bulk creates accounts from previewed combinations.
        /// 
        /// BEHAVIOR:
        /// - Creates accounts for all Valid combinations
        /// - Skips Duplicate combinations (if SkipDuplicates is true)
        /// - Uses transaction for atomicity
        /// - Returns summary of created/skipped/failed accounts
        /// </summary>
        /// <param name="request">Request with combinations to create</param>
        /// <param name="cancellationToken">Optional cancellation token</param>
        /// <returns>Result summary with created account IDs and any errors</returns>
        Task<BulkCreationResultDto> BulkCreateAccountsAsync(
            BulkCreateAccountsRequestDto request,
            CancellationToken cancellationToken = default);
    }
}
