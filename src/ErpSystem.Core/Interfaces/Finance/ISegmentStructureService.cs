using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ErpSystem.Core.DTOs.Finance;

namespace ErpSystem.Core.Interfaces.Finance
{
    /// <summary>
    /// Service contract for managing Segment Structures and Lookup Values.
    /// Handles segmented Chart of Accounts configuration.
    /// </summary>
    public interface ISegmentStructureService
    {
        // Segment Structure Operations
        
        /// <summary>
        /// Retrieves all segment structures for the current tenant.
        /// </summary>
        Task<IReadOnlyList<SegmentStructureDto>> GetSegmentStructuresAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves a single segment structure by ID.
        /// </summary>
        Task<SegmentStructureDto?> GetSegmentStructureByIdAsync(Guid id, CancellationToken cancellationToken = default);

        /// <summary>
        /// Creates a new segment structure.
        /// </summary>
        Task<SegmentStructureDto> CreateSegmentStructureAsync(SegmentStructureCreateDto dto, CancellationToken cancellationToken = default);

        /// <summary>
        /// Updates an existing segment structure.
        /// </summary>
        Task<SegmentStructureDto> UpdateSegmentStructureAsync(Guid id, SegmentStructureUpdateDto dto, CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes a segment structure (if not in use).
        /// </summary>
        Task DeleteSegmentStructureAsync(Guid id, CancellationToken cancellationToken = default);

        // Segment Lookup Value Operations
        
        /// <summary>
        /// Retrieves all lookup values for a specific segment.
        /// </summary>
        Task<IReadOnlyList<SegmentLookupValueDto>> GetSegmentLookupValuesAsync(Guid segmentStructureId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves valid account-backed values for a reporting dimension.
        /// </summary>
        Task<ReportingSegmentOptionsDto> GetReportingOptionsAsync(
            Guid segmentStructureId,
            string? search,
            int take,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Adds a new lookup value to a segment.
        /// </summary>
        Task<SegmentLookupValueDto> AddSegmentLookupValueAsync(Guid segmentStructureId, SegmentLookupValueCreateDto dto, CancellationToken cancellationToken = default);

        /// <summary>
        /// Updates an existing segment lookup value.
        /// </summary>
        Task<SegmentLookupValueDto> UpdateSegmentLookupValueAsync(Guid valueId, SegmentLookupValueUpdateDto dto, CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes a segment lookup value.
        /// </summary>
        Task DeleteSegmentLookupValueAsync(Guid valueId, CancellationToken cancellationToken = default);

        // Account Number Utility Operations
        
        /// <summary>
        /// Validates an account number against the segment structure.
        /// </summary>
        Task<bool> ValidateAccountNumberAsync(string accountNumber, CancellationToken cancellationToken = default);

        /// <summary>
        /// Constructs an account number from segment values.
        /// </summary>
        Task<string> ConstructAccountNumberAsync(Dictionary<int, string> segmentValues, CancellationToken cancellationToken = default);

        /// <summary>
        /// Parses an account number into its segment values.
        /// </summary>
        Task<List<SegmentValueDto>> ParseAccountNumberAsync(string accountNumber, CancellationToken cancellationToken = default);
    }
}
