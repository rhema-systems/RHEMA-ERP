using ErpSystem.Core.Entities.HR;

namespace ErpSystem.Core.Interfaces.HR;

/// <summary>
/// Repository interface for ExternalAssociate operations.
/// </summary>
/// <remarks>
/// ⚠ This interface used to carry seven bespoke members — <c>GetByAssociateNumberAsync</c>,
/// <c>GetByEmailAsync</c>, <c>GetActiveAsync</c>, <c>SearchAsync</c>,
/// <c>AssociateNumberExistsAsync</c>, <c>EmailExistsAsync</c> and <c>GenerateAssociateNumberAsync</c>
/// — and <b>nothing called any of them</b>. Every one read across <i>all</i> tenants, because none
/// took a tenant, while <c>ExternalAssociateService</c> states the same five rules again in a
/// tenant-scoped form. Areas 19-23 slice 8 removed them: the copy of the number generator was D-10
/// itself, preserved in a form no caller could reach, and it would have handed the defect straight
/// back to the first person who used it. A rule stated twice drifts; a rule stated twice where one
/// statement is unreachable has already drifted.
/// </remarks>
public interface IExternalAssociateRepository : IGenericRepository<ExternalAssociate>
{
}
