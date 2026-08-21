using ErpSystem.Core.DTOs.Finance;

namespace ErpSystem.Core.Interfaces.Finance;

/// <summary>
/// Resolves the Finance-owned settlement identity between shared Business Partner and AP Supplier
/// masters without transferring ownership of either operational record to Finance.
/// </summary>
public interface IApSupplierIdentityService
{
    /// <summary>
    /// Performs a read-only lookup. It may return an unambiguous exact-id/code pairing, but it
    /// never persists a Finance identity link and is therefore safe for read-only UI queries.
    /// </summary>
    Task<ApSupplierIdentityDto> LookupAsync(Guid businessPartnerOrSupplierId, CancellationToken cancellationToken = default);

    Task<ApSupplierIdentityDto> ResolveAsync(Guid businessPartnerOrSupplierId, CancellationToken cancellationToken = default);
    Task<ApSupplierIdentityDto> ResolveByBusinessPartnerAsync(Guid businessPartnerId, CancellationToken cancellationToken = default);
    Task<ApSupplierIdentityDto> ResolveBySupplierAsync(Guid supplierId, CancellationToken cancellationToken = default);
}
