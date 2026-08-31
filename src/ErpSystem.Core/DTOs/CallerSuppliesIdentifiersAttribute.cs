namespace ErpSystem.Core.DTOs;

/// <summary>
/// Marks a write DTO whose required identifiers are genuinely the caller's to supply.
/// </summary>
/// <remarks>
/// <para>Read by <c>HrRequiredGuidActionFilter</c>, which then rejects an empty <see cref="Guid"/> on
/// a <c>[Required]</c> property by name — instead of letting it pass validation, reach the database
/// and die on a foreign key as a 500 that names neither the field nor the constraint (ledger D-17).
/// </para>
///
/// <para><b>⚠ Opt-in, deliberately.</b> An empty required Guid is not always a caller's mistake: a
/// controller may be about to fill it from the route (<c>dto.JobDescriptionId = jobDescriptionId</c>)
/// or from the authenticated employee (<c>dto.ReportedById</c>, <c>dto.EmployeeId</c>). Neither is
/// visible to a rule based on names or route shape, and refusing one of those refuses a request that
/// has always been correct. So put this attribute on a DTO only after checking that its controller
/// fills nothing in — then the rule is a statement about that payload rather than a guess.</para>
/// </remarks>
[AttributeUsage(AttributeTargets.Class, Inherited = true)]
public sealed class CallerSuppliesIdentifiersAttribute : Attribute
{
}
