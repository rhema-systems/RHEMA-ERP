using System.Linq.Expressions;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Services.HR;

/// <summary>
/// The one answer to "is this person on strength today?" for headcount arithmetic.
/// </summary>
/// <remarks>
/// <para><b>Why one predicate.</b> Round 2b (recruitment feedback, lane R2) needed the serving
/// headcount of an organisation unit and found <b>three</b> different answers already in the
/// module: the retirement list and the separation analytics use
/// <c>IsActive &amp;&amp; StaffStatus != Terminated</c>; the establishment's filled count
/// (<c>PositionVacancyRepository</c>) uses <c>StaffStatus ∉ {Terminated, Retired, Inactive}</c> and
/// ignores <c>IsActive</c>; and the organisation unit's own <c>GetEmployeeCountAsync</c> filters on
/// nothing but <c>IsDeleted</c>, so it counts leavers. A budget that pre-fills "current headcount"
/// from one of those and is checked against another would disagree with itself.</para>
///
/// <para>This is the union of what the three were trying to say: not deleted, flagged active,
/// and not in a status that means the person has left or is suspended from strength. <b>The three
/// older sites are deliberately not rewritten</b> — each has consumers whose figures would move
/// (the organogram, the unit detail, the establishment screen) and moving them belongs to a pass
/// of its own. New reads use this.</para>
///
/// <para>⚠ EF cannot translate a method, so the query form is an <see cref="Expression"/> handed
/// to <c>Where</c>; the compiled twin is for materialised rows. Keep the two identical.</para>
/// </remarks>
public static class HrServingEmployees
{
    /// <summary>The query form. <c>query.Where(HrServingEmployees.Predicate)</c>.</summary>
    public static readonly Expression<Func<Employee, bool>> Predicate = e =>
        !e.IsDeleted
        && e.IsActive
        && e.StaffStatus != StaffStatus.Terminated
        && e.StaffStatus != StaffStatus.Retired
        && e.StaffStatus != StaffStatus.Inactive;

    private static readonly Func<Employee, bool> Compiled = Predicate.Compile();

    /// <summary>The in-memory twin of <see cref="Predicate"/>.</summary>
    public static bool IsServing(Employee employee) => Compiled(employee);
}
