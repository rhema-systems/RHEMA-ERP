using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Interfaces.HR;

namespace ErpSystem.Data.Repositories.HR;

/// <summary>
/// The external-associate store. Everything it needs comes from <see cref="GenericRepository{T}"/>.
/// </summary>
/// <remarks>
/// ⚠ Seven bespoke methods were removed here in areas 19-23 slice 8 — see
/// <see cref="IExternalAssociateRepository"/> for why. The short version: none of them was ever
/// called, none of them took a tenant, and one of them was a second copy of the reissuing number
/// generator that this slice exists to fix.
/// </remarks>
public class ExternalAssociateRepository
    : GenericRepository<ExternalAssociate>, IExternalAssociateRepository
{
    public ExternalAssociateRepository(ApplicationDbContext context) : base(context) { }
}
