using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.HR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Data.Seeders;

/// <summary>
/// Marks the TDC establishment as approved, by stamping <c>EstablishmentApprovedOn</c> on every
/// position in the TDC organisation tree.
///
/// <para><b>Why this is needed, and what breaks without it.</b> Round 2b lane R4a changed the
/// position-vacancy reconcile so that <b>a gap is only opened on a post whose headcount was
/// authorised</b> — <c>IsEstablished = EstablishmentApprovedOn != null</c> in
/// <c>PositionVacancyRepository</c>. Before that change every unestablished post with nobody in it
/// picked up a vacancy from the column default of 1, which produced 38 rows on the live tenant,
/// none of them on an established post. That was the bug R4a fixed.</para>
///
/// <para>Nothing, however, ever sets the column. Measured on a freshly rebuilt demonstration
/// database on 2026-09-14: <b>142 positions, 0 with an approval date</b>, 38 genuinely below
/// headcount, and <b>0 rows in <c>PositionVacancies</c></b> — so
/// <c>/hr/recruitment/establishment</c>, the screen the recruitment walkthrough opens on, was
/// empty, and <c>POST /position-vacancies/reconcile</c> reported "scanned 142, opened 0". The
/// establishment is the first thing the recruitment story rests on: a requisition is meant to
/// answer to an approved post. With no approved post there is nothing for it to answer to.</para>
///
/// <para><b>Why it excludes the "General" unit.</b> Nineteen positions sit under a unit called
/// General — the estate and legal-admin module's own fixtures (<c>LA-…</c>, <c>FAC-…</c>: "Executive
/// Approver", "Acquisition Committee"). They carry 18 of the 38 gaps and are not part of the TDC
/// establishment. Establishing them would fill the screen with another module's test data, which is
/// worse than leaving it empty. Excluding them leaves 123 TDC positions and <b>20 real gaps</b> —
/// the same 20 vacant posts the demo harness reports from live headcount.</para>
///
/// <para>The gaps themselves are NOT written here. They are opened by the product's own reconcile,
/// which <c>scenarios/050-recruitment.mjs</c> §14 calls once the establishment is approved — so the
/// register is built by the feature rather than by a seeder pretending to be it.</para>
///
/// <para>Idempotent: skipped once any position carries an approval date.</para>
/// </summary>
public class TdcDemoEstablishmentApprovalSeeder
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<TdcDemoEstablishmentApprovalSeeder> _logger;

    private const string By = "TdcDemoEstablishmentApprovalSeeder";

    /// <summary>The organisation unit whose positions belong to other modules' fixtures.</summary>
    private const string FixtureUnitName = "General";

    public TdcDemoEstablishmentApprovalSeeder(
        ApplicationDbContext context, ILogger<TdcDemoEstablishmentApprovalSeeder> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task SeedAsync(CancellationToken ct = default)
    {
        var tenant = await _context.Set<Tenant>().FirstOrDefaultAsync(t => t.Code == "DEFAULT", ct);
        if (tenant is null)
        {
            _logger.LogError("DEFAULT tenant not found — cannot approve the establishment.");
            return;
        }

        var tenantId = tenant.Id;

        var fixtureUnitIds = await _context.Set<OrganizationUnit>().IgnoreQueryFilters()
            .Where(u => u.TenantId == tenantId && !u.IsDeleted && u.Name == FixtureUnitName)
            .Select(u => u.Id)
            .ToListAsync(ct);

        var positions = await _context.Set<EmployeePosition>().IgnoreQueryFilters()
            .Where(p => p.TenantId == tenantId && !p.IsDeleted
                     && p.EstablishmentApprovedOn == null
                     && !fixtureUnitIds.Contains(p.OrganizationUnitId))
            .ToListAsync(ct);

        if (positions.Count == 0)
        {
            _logger.LogInformation("Every TDC position already carries an establishment approval date. Skipping.");
            return;
        }

        // The establishment is approved with the annual budget, so it is dated to the start of the
        // financial year rather than to the moment of the rebuild.
        var approvedOn = new DateTime(DateTime.UtcNow.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var now = DateTime.UtcNow;

        foreach (var position in positions)
        {
            position.EstablishmentApprovedOn = approvedOn;

            // A post with no expected headcount cannot state a gap either way; one is the
            // establishment for every TDC post that is not explicitly multi-holder.
            if (position.ExpectedHeadcount < 1) position.ExpectedHeadcount = 1;

            position.UpdatedAt = now;
            position.UpdatedBy = By;
        }

        await _context.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Establishment approved for {Count} TDC positions, dated {Date:d MMMM yyyy}. The estate "
            + "module's {Fixtures} fixture positions under \"{Unit}\" are deliberately left "
            + "unestablished. Gaps are opened by the product's own reconcile, not written here.",
            positions.Count, approvedOn, fixtureUnitIds.Count == 0 ? 0 : await CountFixturesAsync(tenantId, fixtureUnitIds, ct), FixtureUnitName);
    }

    private Task<int> CountFixturesAsync(Guid tenantId, List<Guid> fixtureUnitIds, CancellationToken ct) =>
        _context.Set<EmployeePosition>().IgnoreQueryFilters()
            .CountAsync(p => p.TenantId == tenantId && !p.IsDeleted
                          && fixtureUnitIds.Contains(p.OrganizationUnitId), ct);
}
