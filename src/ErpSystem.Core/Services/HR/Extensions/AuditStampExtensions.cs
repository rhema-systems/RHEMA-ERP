using ErpSystem.Core.Entities;
using ErpSystem.Core.Interfaces;

namespace ErpSystem.Core.Services.HR.Extensions;

/// <summary>
/// Stamps the author onto a row.
///
/// <para>⚠ <b>Nothing in this codebase stamps <c>CreatedBy</c> or <c>UpdatedBy</c> automatically.</b>
/// <c>ApplicationDbContext.UpdateAuditableEntities</c> sets <c>CreatedAt</c>, <c>UpdatedAt</c> and
/// the soft-delete flag, and stops there — the author columns are each service's own job. A service
/// that forgets produces rows that are perfectly valid, pass every test, and answer "who did this?"
/// with a blank forever.</para>
///
/// <para>Slice 3 of areas 19–23 found that on the organisation-unit change log and fixed it inline.
/// Slice 11's content audit then found the same hole in the two registers the same bundle built
/// afterwards — <c>Unions</c> 0 of 2 rows stamped and <c>Teams</c> 0 of 2, against
/// <c>ExternalAssociates</c> 7 of 7 and <c>FacilityServices</c> 3 of 3 next door. The lesson had
/// been learned two slices earlier and not carried across, which is what happens to a rule that
/// lives as an expression copied into one file rather than as a name.</para>
///
/// <para>So it is a name now. The <c>FullName</c>-then-<c>Username</c> fallback is part of the rule
/// and not an implementation detail: a service account has no display name, and stamping its blank
/// would read exactly like the bug this replaces.</para>
///
/// <para>⚠ <c>BaseEntity</c> has <c>CreatedById</c> and no <c>UpdatedById</c> — deliberately not
/// added here. A new column is a migration and a schema change in three places, and this slice has
/// no evidence anyone needs to resolve the last editor back to a user record. If that need appears,
/// it is a change to this one file plus the column.</para>
/// </summary>
public static class AuditStampExtensions
{
    /// <summary>The display name to record for whoever is acting — null rather than blank when
    /// there is genuinely nobody, so an unauthenticated write is distinguishable from an unstamped
    /// one.</summary>
    public static string? AuthorName(this ICurrentUserProvider? user)
    {
        if (user is null) return null;
        var name = string.IsNullOrWhiteSpace(user.FullName) ? user.Username : user.FullName;
        return string.IsNullOrWhiteSpace(name) ? null : name;
    }

    /// <summary>
    /// Author columns for a row being created.
    ///
    /// <para>This also sets <c>UpdatedBy</c>. A row that has never been edited was last touched by
    /// whoever made it, and leaving that null makes an unedited row look identical to one whose
    /// editor was never recorded — which is the defect this whole helper exists to remove.</para>
    /// </summary>
    public static T StampCreated<T>(this T entity, ICurrentUserProvider? user) where T : BaseEntity
    {
        var name = user.AuthorName();
        entity.CreatedBy = name;
        entity.CreatedById = user?.UserId is { } id && id != Guid.Empty ? id : null;
        entity.UpdatedBy = name;
        return entity;
    }

    /// <summary>
    /// Author columns for a row being modified. <c>CreatedBy</c> is deliberately untouched —
    /// overwriting it would erase the only record of who introduced the row.
    /// </summary>
    public static T StampUpdated<T>(this T entity, ICurrentUserProvider? user) where T : BaseEntity
    {
        entity.UpdatedBy = user.AuthorName();
        return entity;
    }
}
