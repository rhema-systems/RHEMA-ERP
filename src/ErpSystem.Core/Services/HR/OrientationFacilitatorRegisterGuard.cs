using ErpSystem.Core.Entities.HR.Orientation;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Core.Services.HR;

/// <summary>
/// Whether a training vendor or trainer can leave the register (round 4, lane M3): not while an
/// orientation session that has not happened yet names them as its facilitator.
/// </summary>
/// <remarks>
/// <para>A register delete is a soft delete, so the foreign key never fires — without this check a
/// vendor booked for next week's induction could be removed, and the session would go on naming a
/// facilitator nobody could look up. A session that is over does not block anything: its facilitator
/// keeps the snapshot of what was agreed, and the session page says the vendor has since been removed.</para>
///
/// <para>"Not happened yet" is a session neither completed nor cancelled whose end — or start, when
/// it has no end — is still ahead, or that is not dated at all. A session left open after its date
/// does not hold the register hostage.</para>
/// </remarks>
public static class OrientationFacilitatorRegisterGuard
{
    /// <summary>The refusal, in words — or null when nothing coming up names this vendor or trainer.</summary>
    public static async Task<string?> WhyStillBookedAsync(
        this IUnitOfWork uow, Guid tenantId, Guid? vendorId, Guid? trainerId, string name, string instead,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var facilitators = uow.Repository<OrientationSessionFacilitator>().GetQueryable()
            .Where(f => f.TenantId == tenantId && !f.IsDeleted);
        facilitators = vendorId is { } vendor
            ? facilitators.Where(f => f.ExternalFacilitatorVendorId == vendor)
            : facilitators.Where(f => f.ExternalFacilitatorTrainerProfileId == trainerId);

        var codes = await facilitators
            .Join(uow.Repository<OrientationSession>().GetQueryable(), f => f.SessionId, s => s.Id, (f, s) => s)
            .Where(s => !s.IsDeleted
                        && s.Status != OrientationSessionStatus.Completed
                        && s.Status != OrientationSessionStatus.Cancelled
                        && ((s.ScheduledEndAt ?? s.ScheduledStartAt) == null || (s.ScheduledEndAt ?? s.ScheduledStartAt) >= now))
            .Select(s => s.SessionCode)
            .Distinct()
            .OrderBy(code => code)
            .ToListAsync(cancellationToken);

        if (codes.Count == 0) return null;
        var sessions = codes.Count == 1 ? "an orientation session" : $"{codes.Count} orientation sessions";
        return $"{name} is booked to facilitate {sessions} that {(codes.Count == 1 ? "has" : "have")} not happened yet " +
               $"({string.Join(", ", codes)}). Change those facilitators first — or {instead}.";
    }
}
