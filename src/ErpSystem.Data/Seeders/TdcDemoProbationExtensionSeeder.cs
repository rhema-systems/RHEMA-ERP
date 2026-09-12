using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.HR.Recruitment;
using ErpSystem.Core.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Data.Seeders;

/// <summary>
/// Records one probation extension on the <b>DEFAULT tenant</b>'s demonstration data.
///
/// <para><b>Why a seeder and not a scenario.</b> Both doors onto <c>ProbationExtension</c> are shut:</para>
/// <list type="number">
///   <item><c>POST /api/probations/{id}/extend</c> is gated on <c>HR.Policy.ProbationAdmin</c>, which
///   only SuperAdmin and TenantAdmin hold, and its first act is <c>RequireEmployeeId()</c> — which
///   throws for exactly those accounts, because no admin login is linked to an employee. The caller
///   gets 403 "You do not have permission to access this resource", which is not what is wrong. The
///   same shape blocks <c>/confirm</c>, <c>/terminate</c> and <c>DELETE /probations/{id}</c>.</item>
///   <item>The appraisal route — an <c>ExtendProbation</c> outcome recommendation — reaches
///   <c>ExtendProbationHandler</c>, which calls <c>ProbationService.ExtendAsync(..., CurrentUserProvider.UserId)</c>.
///   That value lands in <c>ProbationExtension.ExtendedById</c>, an <b>Employee</b> foreign key, so
///   the save fails and approving the recommendation answers 500 (measured 2026-09-04).</item>
/// </list>
///
/// <para>⚠ Both are recorded as defects. This seeder writes the row the extension screens read; it
/// does not make either door work.</para>
///
/// <para>The extension belongs to Patrick Appiah (TDC/00018), whose probation runs alongside
/// disciplinary case DC-2026-00001 for persistent lateness — the two records are meant to be read
/// together. It is deliberately NOT Kojo Ansah (TDC/00063): book 1 §7 walks his first review on an
/// unextended probation.</para>
///
/// Idempotent: skips when any live extension already exists for the tenant.
/// ⚠ Depends on probation periods, which the 065-probation scenario creates through the API — so
/// this runs AFTER `node scenarios.mjs`, not in the pre-scenario seed pass.
/// </summary>
public class TdcDemoProbationExtensionSeeder
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<TdcDemoProbationExtensionSeeder> _logger;

    private const string By = "TdcDemoProbationExtensionSeeder";
    private const string SubjectNumber = "TDC/00018";
    private const int Months = 3;

    public TdcDemoProbationExtensionSeeder(
        ApplicationDbContext context, ILogger<TdcDemoProbationExtensionSeeder> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task SeedAsync(CancellationToken ct = default)
    {
        var tenant = await _context.Set<Tenant>().FirstOrDefaultAsync(t => t.Code == "DEFAULT", ct);
        if (tenant is null)
        {
            _logger.LogError("DEFAULT tenant not found — cannot seed the probation extension.");
            return;
        }

        var tenantId = tenant.Id;

        if (await _context.Set<ProbationExtension>().IgnoreQueryFilters()
                .AnyAsync(e => e.TenantId == tenantId && !e.IsDeleted, ct))
        {
            _logger.LogInformation("A probation extension is already recorded for the DEFAULT tenant. Skipping.");
            return;
        }

        var subject = await _context.Set<Employee>().IgnoreQueryFilters()
            .FirstOrDefaultAsync(e => e.TenantId == tenantId && !e.IsDeleted
                                   && e.EmployeeNumber == SubjectNumber, ct);
        var actor = await _context.Set<Employee>().IgnoreQueryFilters()
            .FirstOrDefaultAsync(e => e.TenantId == tenantId && !e.IsDeleted
                                   && e.EmployeeNumber == "TDC/00009", ct);   // Head of HR & Administration

        if (subject is null || actor is null)
        {
            _logger.LogWarning(
                "Expected {Subject} and TDC/00009 on the establishment; one is missing. Skipping the probation extension.",
                SubjectNumber);
            return;
        }

        var probation = await _context.Set<ProbationPeriod>().IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.TenantId == tenantId && !p.IsDeleted
                                   && p.EmployeeId == subject.Id
                                   && p.Status == ProbationStatus.Active, ct);

        if (probation is null)
        {
            _logger.LogWarning(
                "No active probation for {Subject} — run the 065-probation scenario before this seeder.",
                SubjectNumber);
            return;
        }

        var previousEnd = probation.CurrentEndDate;
        var newEnd = previousEnd.AddMonths(Months);
        var now = DateTime.UtcNow;

        _context.Set<ProbationExtension>().Add(new ProbationExtension
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProbationPeriodId = probation.Id,
            PreviousEndDate = previousEnd,
            NewEndDate = newEnd,
            ExtensionMonths = Months,
            Reason = "Attendance has not met the standard set at appointment: four late arrivals in a "
                   + "single month, which are the subject of disciplinary case DC-2026-00001. The line "
                   + "manager is otherwise satisfied with the work and recommends three further months "
                   + "rather than a refusal to confirm.",
            ExtendedById = actor.Id,
            ExtendedDate = now,
            Comments = "Reviewed monthly with the Head of Development. A clean attendance record over "
                     + "the extension confirms the appointment; a repeat brings the confirmation "
                     + "decision forward.",
            CreatedAt = now,
            CreatedBy = By,
        });

        // The period itself moves with the extension, exactly as ProbationService.RecordExtensionAsync
        // does it — an extension row whose period still ends on the old date is a record that
        // disagrees with itself, and ExtensionCount is what the summary screens count.
        probation.CurrentEndDate = newEnd;
        probation.ExtensionCount += 1;
        probation.UpdatedAt = now;
        probation.UpdatedBy = By;

        await _context.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Probation for {Subject} extended by {Months} months to {NewEnd}. ⚠ Written directly: both "
            + "API doors onto ProbationExtension are broken (403 on the admin route, 500 on the "
            + "appraisal route).",
            SubjectNumber, Months, newEnd);
    }
}
