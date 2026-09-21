using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Interfaces.HR;

/// <summary>
/// The tenant's vocabulary for how one person is tied to another — what the referee, guarantor,
/// next-of-kin and candidate-referee screens pick from.
/// </summary>
/// <remarks>
/// Round 2, lane D2 (register rows E-11a, E-11b). See <see cref="Entities.HR.RelationshipType"/>
/// for why dependants deliberately keep their enum.
/// </remarks>
public interface IRelationshipTypeService
{
    /// <summary>
    /// The catalogue. <paramref name="categories"/> is what a PICKER passes — the screen's accepted
    /// set — and it must be paired with <paramref name="activeOnly"/>; the master screen passes
    /// neither and sees everything, retired rows included.
    /// </summary>
    Task<IEnumerable<RelationshipTypeDto>> GetAllAsync(
        bool activeOnly = false,
        IEnumerable<RelationshipCategory>? categories = null,
        CancellationToken cancellationToken = default);

    Task<RelationshipTypeDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<RelationshipTypeDto> CreateAsync(CreateRelationshipTypeDto dto, CancellationToken cancellationToken = default);

    Task<RelationshipTypeDto> UpdateAsync(Guid id, UpdateRelationshipTypeDto dto, CancellationToken cancellationToken = default);

    /// <summary>Retires a value: it stops being offered, and every record already naming it keeps it.</summary>
    Task DeactivateAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a value outright — allowed ONLY while nothing names it.
    /// </summary>
    /// <remarks>
    /// ⚠ Refused with a count when any of the four consumers still points at the row. The foreign
    /// keys are Restrict, so a hard delete would fail at the database with a constraint name no
    /// user can act on; and a SOFT delete would be worse — it releases nothing, and the row would
    /// vanish from every read while live records still held its id. That is the exact failure the
    /// geography module was built to end, and the succession lane met it again in August.
    /// </remarks>
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
