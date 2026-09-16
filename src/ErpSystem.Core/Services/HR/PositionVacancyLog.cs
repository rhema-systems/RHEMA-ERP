using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.HR.Recruitment;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Core.Services.HR;

/// <summary>
/// Logs a <see cref="PositionVacancy"/> when a post actually falls empty.
/// </summary>
/// <remarks>
/// <para><b>The gap this closes (G-3.2, 2026-09-15).</b> <c>PositionVacancy</c>'s own documentation
/// stated that rows <i>"are normally created automatically by <c>PositionVacancyInterceptor</c>
/// when an employee's status turns to Terminated/Retired or their <c>PositionId</c> changes
/// (promotion / transfer), so no exit path can forget to log it"</i>, and that <i>"a departure is
/// ALWAYS logged and then classified … never silently dropped"</i>. None of that was true. The name
/// <c>PositionVacancyInterceptor</c> occurred in exactly two places in the solution, both XML doc
/// comments; there was no such class, and the only interceptor registered was
/// <c>AuditInterceptor</c>. <c>new PositionVacancy</c> was constructed in exactly <b>one</b> place:
/// inside <c>ReconcilePositionVacanciesAsync</c>.</para>
///
/// <para>So a termination, retirement, promotion or transfer logged nothing. The register was only
/// as current as the last time somebody pressed <b>Reconcile</b> — which, until G-3.1 was closed
/// the same day, the HR role could not do at all. That combination is why the establishment screen
/// and the analytics screen's "seats standing empty" tile both read zero on a tenant with real
/// vacancies (G-14.2).</para>
///
/// <para><b>Why call sites rather than the documented interceptor.</b> An interceptor would catch
/// every write whatever made it, which is the stronger guarantee — but it runs with no idea
/// <i>why</i> the change happened, and the three fields that made the register decorative (G-3.4)
/// are exactly the ones that need that context: the reason, the employee who vacated, and the date
/// the seat actually fell empty rather than the date a button was pressed. A call site knows all
/// three. It is also testable, which SaveChanges-time writes are not. The trade is that a new exit
/// path must remember to call this; keep the list in <c>HR-RECRUITMENT-GAP-CLOSURE-PLAN.md</c>
/// current, and prefer adding the call to inventing a second way to end employment.</para>
///
/// <para><b>Deliberately a static helper over <see cref="IUnitOfWork"/>, not an injected
/// service.</b> The callers — <c>EmployeeService</c>, <c>StaffMovementService</c>,
/// <c>SeparationService</c> — all already hold a unit of work, and adding
/// <c>IPositionVacancyService</c> to <c>EmployeeService</c>'s constructor would have introduced a
/// dependency cycle (the vacancy service reads employees). It does not save: the caller's own
/// <c>SaveChangesAsync</c> commits the vacancy in the same transaction as the departure, so a
/// failed termination cannot leave a vacancy behind.</para>
/// </remarks>
public static class PositionVacancyLog
{
    /// <summary>Statuses that mean a vacancy on this position is already being tracked.</summary>
    private static readonly PositionVacancyStatus[] LiveStatuses =
    {
        PositionVacancyStatus.Anticipated,
        PositionVacancyStatus.Open,
        PositionVacancyStatus.UnderReview,
        PositionVacancyStatus.RequisitionRaised
    };

    /// <summary>
    /// Records that <paramref name="employeeId"/> has left (or is known to be leaving)
    /// <paramref name="positionId"/>, and classifies the result against the establishment.
    /// </summary>
    /// <returns>
    /// The vacancy row, whether newly added or the live one already tracking this post. Null when
    /// there is no position to log against.
    /// </returns>
    /// <remarks>
    /// <para><b>Idempotent by position.</b> A post with a live vacancy row does not get a second
    /// one — two people leaving the same three-seat team is one gap that got wider, not two
    /// registers to work. Where the existing row has no vacating employee recorded (a row opened
    /// by reconcile, which cannot know one), this fills in the reason, the employee and the date,
    /// so a reconcile-created row is upgraded by the first real departure that explains it.</para>
    ///
    /// <para><b>Does not save.</b> The caller commits.</para>
    /// </remarks>
    public static async Task<PositionVacancy?> LogDepartureAsync(
        IUnitOfWork unitOfWork,
        Guid tenantId,
        Guid employeeId,
        Guid? positionId,
        VacancyReason reason,
        DateTime vacatedDate,
        Guid actingUserId,
        bool isAnticipated = false,
        DateTime? expectedVacancyDate = null,
        string? note = null,
        CancellationToken cancellationToken = default)
    {
        if (positionId is not { } position || position == Guid.Empty) return null;

        var vacancyRepo = unitOfWork.Repository<PositionVacancy>();

        var existing = await vacancyRepo.GetQueryable()
            .Where(v => v.TenantId == tenantId
                     && v.PositionId == position
                     && !v.IsDeleted
                     && LiveStatuses.Contains(v.Status))
            .FirstOrDefaultAsync(cancellationToken);

        if (existing is not null)
        {
            var changed = false;

            // Upgrade a row that knows there is a gap but not who left or why — which is every row
            // reconcile has ever created (G-3.4). Never overwrite a departure already recorded.
            if (existing.VacatedByEmployeeId is null)
            {
                existing.VacatedByEmployeeId = employeeId;
                existing.Reason = reason;
                existing.VacatedDate = vacatedDate;
                existing.Notes = AppendNote(existing.Notes, Describe(reason, isAnticipated, note));
                changed = true;
            }

            // The anticipated row becoming real. An Anticipated vacancy says "this seat is going to
            // fall empty"; once it has, the register must stop saying that, or the Anticipated tile
            // counts seats that are already empty and the Open one misses them. The VacatedDate
            // moves to the day it actually happened, which may not be the day that was forecast.
            if (existing.Status == PositionVacancyStatus.Anticipated && !isAnticipated)
            {
                existing.Status = PositionVacancyStatus.Open;
                existing.IsAnticipated = false;
                existing.VacatedDate = vacatedDate;
                existing.Notes = AppendNote(
                    existing.Notes,
                    $"The anticipated {reason} has now happened; the seat is empty.");
                changed = true;
            }

            if (changed)
            {
                existing.UpdatedAt = DateTime.UtcNow;
                existing.LastModifiedById = actingUserId;
                await vacancyRepo.UpdateAsync(existing);
            }

            return existing;
        }

        var post = await unitOfWork.Repository<EmployeePosition>().GetQueryable().AsNoTracking()
            .Where(p => p.Id == position && p.TenantId == tenantId)
            .Select(p => new { p.ExpectedHeadcount, p.EstablishmentApprovedOn, p.OrganizationUnitId })
            .FirstOrDefaultAsync(cancellationToken);

        if (post is null) return null;

        // Headcount as it stands after the departure. An anticipated vacancy is logged BEFORE the
        // person goes, so their seat is still counted — subtract it to classify the future state,
        // which is the state the vacancy describes.
        var serving = await unitOfWork.Repository<Employee>().GetQueryable().AsNoTracking()
            .Where(e => e.TenantId == tenantId && e.PositionId == position)
            .Where(HrServingEmployees.Predicate)
            .CountAsync(cancellationToken);

        if (isAnticipated) serving = Math.Max(0, serving - 1);

        var vacancy = new PositionVacancy
        {
            TenantId = tenantId,
            PositionId = position,
            OrganizationUnitId = post.OrganizationUnitId,
            VacatedByEmployeeId = employeeId,
            Reason = reason,
            VacatedDate = vacatedDate,
            IsAnticipated = isAnticipated,
            ExpectedVacancyDate = isAnticipated ? (expectedVacancyDate ?? vacatedDate) : null,
            Status = isAnticipated ? PositionVacancyStatus.Anticipated : PositionVacancyStatus.Open,
            Classification = Classify(post.EstablishmentApprovedOn, post.ExpectedHeadcount, serving),
            ExpectedHeadcount = post.ExpectedHeadcount,
            ActiveHeadcountAtDetection = serving,
            Notes = Describe(reason, isAnticipated, note),
            CreatedById = actingUserId,
            CreatedBy = "departure",
        };

        await vacancyRepo.AddAsync(vacancy);
        return vacancy;
    }

    /// <summary>
    /// Where the post stands against its establishment once the departure is accounted for.
    /// </summary>
    /// <remarks>
    /// <para>G-3.5: <c>Classification</c> was assigned in exactly one place — reconcile — and always
    /// to <c>WithinEstablishment</c>. <c>NoShortfall</c> and <c>OverEstablishment</c> were written
    /// by nothing, so the column read the same on every row and <c>noShortfallOrOver</c> in the
    /// stats payload was permanently 0. The three-way classification was real in the enum and in
    /// the entity's documentation, and no code produced two of its three values.</para>
    ///
    /// <para>A post nobody has established is classified <c>NoShortfall</c> rather than
    /// <c>WithinEstablishment</c>: <c>ExpectedHeadcount</c> is then the column default of 1 and
    /// means "nobody has said", not "one is authorised". Calling that a shortfall within
    /// establishment is the claim area 17/18 found to be false on 132 of 146 live positions. The
    /// departure is still logged — the guarantee is that it is never silently dropped, not that it
    /// always counts as a fillable gap.</para>
    /// </remarks>
    private static VacancyClassification Classify(
        DateTime? establishmentApprovedOn, int expectedHeadcount, int servingAfterDeparture)
    {
        if (establishmentApprovedOn is null) return VacancyClassification.NoShortfall;
        if (servingAfterDeparture < expectedHeadcount) return VacancyClassification.WithinEstablishment;
        if (servingAfterDeparture > expectedHeadcount) return VacancyClassification.OverEstablishment;
        return VacancyClassification.NoShortfall;
    }

    private static string Describe(VacancyReason reason, bool isAnticipated, string? note)
    {
        var opening = isAnticipated
            ? $"Anticipated — {reason} notified, seat not yet empty."
            : $"Opened by {reason}.";

        return string.IsNullOrWhiteSpace(note) ? opening : $"{opening} {note.Trim()}";
    }

    private static string AppendNote(string? existing, string addition)
    {
        var line = $"[{DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC] {addition.Trim()}";
        return string.IsNullOrWhiteSpace(existing) ? line : $"{existing}\n{line}";
    }
}
