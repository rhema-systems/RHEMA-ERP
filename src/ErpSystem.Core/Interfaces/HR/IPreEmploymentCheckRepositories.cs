using ErpSystem.Core.Entities.HR.Recruitment;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Interfaces.HR;

// ============================================================================
// PRE-EMPLOYMENT CHECK
// ============================================================================

#region Pre-Employment Check

public interface IPreEmploymentCheckRepository : IGenericRepository<PreEmploymentCheck>
{
    /// <summary>Returns the pre-employment check linked to an offer, with items loaded.</summary>
    Task<PreEmploymentCheck?> GetByOfferIdAsync(Guid offerId);

    /// <summary>Returns a fully-loaded pre-employment check including all check items and reference responses.</summary>
    Task<PreEmploymentCheck?> GetWithItemsAsync(Guid id);

    /// <summary>Returns pre-employment checks filtered by overall status.</summary>
    Task<IEnumerable<PreEmploymentCheck>> GetByStatusAsync(PreEmploymentCheckStatus status);
}

#endregion

// ============================================================================
// PRE-EMPLOYMENT CHECK ITEM
// ============================================================================

#region Pre-Employment Check Item

public interface IPreEmploymentCheckItemRepository : IGenericRepository<PreEmploymentCheckItem>
{
    /// <summary>Returns all check items for a pre-employment check, ordered by display order.</summary>
    Task<IEnumerable<PreEmploymentCheckItem>> GetByPreEmploymentCheckIdAsync(Guid preEmploymentCheckId);

    /// <summary>Returns check items filtered by status, optionally scoped to a check.</summary>
    Task<IEnumerable<PreEmploymentCheckItem>> GetByStatusAsync(CheckItemStatus status, Guid? preEmploymentCheckId = null);

    /// <summary>Returns all mandatory check items for a pre-employment check.</summary>
    Task<IEnumerable<PreEmploymentCheckItem>> GetMandatoryItemsAsync(Guid preEmploymentCheckId);

    /// <summary>Returns all failed check items that are configured to block the hire.</summary>
    Task<IEnumerable<PreEmploymentCheckItem>> GetBlockingFailuresAsync(Guid preEmploymentCheckId);
}

#endregion

// ============================================================================
// REFERENCE CHECK RESPONSE
// ============================================================================

#region Reference Check Response

public interface IReferenceCheckResponseRepository : IGenericRepository<ReferenceCheckResponse>
{
    /// <summary>Returns all reference responses for a check item.</summary>
    Task<IEnumerable<ReferenceCheckResponse>> GetByCheckItemIdAsync(Guid checkItemId);

    /// <summary>Returns all reference responses provided by a given referee.</summary>
    Task<IEnumerable<ReferenceCheckResponse>> GetByRefereeIdAsync(Guid refereeId);
}

#endregion

// ============================================================================
// PRE-EMPLOYMENT CHECK TEMPLATE
// ============================================================================

#region Pre-Employment Check Template

public interface IPreEmploymentCheckTemplateRepository : IGenericRepository<PreEmploymentCheckTemplate>
{
    /// <summary>
    /// Returns all active templates for the given tenant, with items included.
    ///
    /// <para>⚠ The tenant is a parameter rather than an assumption. This method's summary already
    /// said "for the current tenant" while neither it nor its caller applied any tenant predicate —
    /// the documentation was aspirational and the read spanned every tenant in the database.</para>
    /// </summary>
    Task<IEnumerable<PreEmploymentCheckTemplate>> GetAllActiveAsync(Guid tenantId);

    /// <summary>Returns a template with all its items loaded, scoped to the given tenant.</summary>
    Task<PreEmploymentCheckTemplate?> GetByIdWithItemsAsync(Guid id, Guid tenantId);

    /// <summary>Explicitly adds a new template item to the EF context as Added, guaranteeing an INSERT on SaveChanges.</summary>
    Task AddTemplateItemAsync(PreEmploymentCheckTemplateItem item);
}

#endregion
