namespace ErpSystem.Core.Entities.HR;

/// <summary>
/// A record that names a row of the relationship catalogue — a referee, a guarantor, a next of kin,
/// a candidate's referee.
/// </summary>
/// <remarks>
/// <para><b>Why an interface for one nullable Guid.</b> The catalogue's delete guard has to count
/// every record pointing at a value before it will erase it, across four tables that have nothing
/// else in common. Without this the count is four near-identical copies of the same query — and
/// four near-identical copies of the same idea is exactly how the four free-text
/// <c>Relationship</c> columns this catalogue replaces came to disagree with each other.</para>
///
/// <para><b>⚠ Implementing this is not enough on its own.</b> A fifth table that gains a
/// <c>RelationshipTypeId</c> must also be added to <c>RelationshipTypeService.CountUsagesAsync</c>,
/// or its values can be deleted out from under it — and the Restrict foreign key would then surface
/// as a raw SQL constraint error on a screen that had just promised the delete would work. The
/// interface makes the addition one line and impossible to typo; it does not make it automatic.</para>
///
/// <para><b>⚠ Deliberately ONE member.</b> <c>TenantId</c> and <c>IsDeleted</c> would fit here too,
/// and the count query filters on all three — but every implementer already inherits those two from
/// <c>TenantEntity</c>. Declaring them here would make two more member accesses interface-typed
/// inside an EF expression tree for no gain; leaving them on the base class keeps exactly one
/// property in that tree that EF has to resolve through an interface.</para>
///
/// <para>Round 2, lane D2 (register rows E-11a, E-11b).</para>
/// </remarks>
public interface IRelationshipTypeConsumer
{
    /// <summary>The catalogue row this record names, where it names one.</summary>
    Guid? RelationshipTypeId { get; }
}
