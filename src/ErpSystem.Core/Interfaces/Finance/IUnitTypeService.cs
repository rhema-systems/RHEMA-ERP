using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ErpSystem.Core.DTOs.Finance;

namespace ErpSystem.Core.Interfaces.Finance
{
    /// <summary>
    /// Service contract for managing Unit Types.
    /// Unit Types define the measurement units for non-financial quantities
    /// (e.g., Employees, Square Feet, Hours).
    /// </summary>
    public interface IUnitTypeService
    {
        /// <summary>
        /// Retrieves all unit types for the current tenant.
        /// </summary>
        Task<IReadOnlyList<UnitTypeDto>> GetAllAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves active unit types only.
        /// </summary>
        Task<IReadOnlyList<UnitTypeDto>> GetActiveAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves a single unit type by ID.
        /// </summary>
        Task<UnitTypeDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves a unit type by its code.
        /// </summary>
        Task<UnitTypeDto?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);

        /// <summary>
        /// Creates a new unit type.
        /// </summary>
        Task<UnitTypeDto> CreateAsync(CreateUnitTypeDto dto, CancellationToken cancellationToken = default);

        /// <summary>
        /// Updates an existing unit type.
        /// </summary>
        Task<UnitTypeDto> UpdateAsync(Guid id, UpdateUnitTypeDto dto, CancellationToken cancellationToken = default);

        /// <summary>
        /// Activates a unit type.
        /// </summary>
        Task ActivateAsync(Guid id, CancellationToken cancellationToken = default);

        /// <summary>
        /// Deactivates a unit type (prevents new accounts from using it).
        /// </summary>
        Task DeactivateAsync(Guid id, CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes a unit type (soft delete).
        /// Fails if any unit accounts are using this type.
        /// </summary>
        Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    }
}
