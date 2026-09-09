// The HR entities are split across sub-namespaces by area rather than living under
// ErpSystem.Core.Entities.HR wholesale, so each area needs its own using. SalaryGrade, by contrast,
// is declared in the GLOBAL namespace and must not be imported.
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.HR.Payroll;
using ErpSystem.Core.Entities.HR.Awards;
using ErpSystem.Core.Entities.HR.Medical;
using ErpSystem.Core.Entities.HR.Orientation;
using ErpSystem.Core.Entities.HR.Performance;
using ErpSystem.Core.Entities.HR.PromotionTransfer;
using ErpSystem.Core.Entities.HR.Recruitment;
using ErpSystem.Core.Entities.HR.Safety;
using ErpSystem.Core.Entities.HR.StaffLeave;
using ErpSystem.Core.Entities.HR.Training;
using ErpSystem.Core.Interfaces.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Data.Seeders;

/// <summary>
/// Loads a demonstrable HR and SHE dataset on top of the reference data that
/// <see cref="HrSeedOrchestrator"/> installs — run it with:
/// <code>dotnet run --project src/ErpSystem.Api seed-hr-demo</code>
///
/// <para><b>Why this is a SEPARATE command from <c>seed-hr-all</c>, and must stay separate.</b>
/// <c>seed-hr-all</c> seeds facts: the countries that exist, the client's real organisation
/// structure, the client's real positions. Everything here is invented — names, salaries, incidents,
/// claims, appraisals. Putting fabricated people behind the same command that installs the real
/// organogram would make the trustworthy command untrustworthy, and there would then be no way to
/// build a clean database for anything but a demo. The split is the whole point:
/// <c>seed-hr-all</c> is safe anywhere; <b>this command is for demonstration databases only.</b>
/// </para>
///
/// <para><b>Each step is isolated.</b> Nine of the seeders invoked here were ported from the
/// standalone HR solution and deferred, which means that until now <b>they had never been
/// executed against this schema even once</b>. A step that throws is caught, reported and stepped
/// over rather than aborting the run, because a demo database missing its awards data is worth far
/// more than no demo database at all — and because the point of the first run is to find out which
/// of the nine work. The summary at the end names every failure.</para>
///
/// <para>Idempotent: every step declares an "already seeded?" probe and is skipped when satisfied,
/// so re-running after fixing one seeder does not duplicate the ones that already succeeded.</para>
/// </summary>
public class HrDemoSeedOrchestrator
{
    private readonly ApplicationDbContext _context;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<HrDemoSeedOrchestrator> _logger;
    private readonly IEnumerable<IEmailEventCatalog> _emailCatalogs;

    public HrDemoSeedOrchestrator(
        ApplicationDbContext context,
        ILoggerFactory loggerFactory,
        IEnumerable<IEmailEventCatalog>? emailCatalogs = null)
    {
        _context = context;
        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger<HrDemoSeedOrchestrator>();
        _emailCatalogs = emailCatalogs ?? Array.Empty<IEmailEventCatalog>();
    }

    private sealed record SeedStep(
        string Name,
        Func<CancellationToken, Task<bool>> AlreadySeeded,
        Func<CancellationToken, Task> RunAsync);

    /// <returns><c>true</c> when the run completed, whether or not individual steps failed.</returns>
    public async Task<bool> SeedAsync(CancellationToken ct = default)
    {
        if (!await _context.Database.CanConnectAsync(ct))
        {
            _logger.LogError("Cannot connect to the database. Check the connection string, then run 'rebuild-db'.");
            return false;
        }

        var tenant = await _context.Set<Tenant>().FirstOrDefaultAsync(t => t.Code == "DEFAULT", ct);
        if (tenant is null)
        {
            _logger.LogError("DEFAULT tenant not found. Run 'rebuild-db', then 'seed', then 'seed-hr-all'.");
            return false;
        }

        var tenantId = tenant.Id;

        // The establishment must exist first. Without it the workforce seeder has no positions to
        // staff, and every seeder after it resolves an employee list that is still the 24 estate
        // fixtures — which produces a database that looks seeded and demonstrates nothing.
        var establishmentExists = await _context.Set<EmployeePosition>()
            .IgnoreQueryFilters()
            .AnyAsync(p => p.TenantId == tenantId && !p.IsDeleted, ct);

        if (!establishmentExists)
        {
            _logger.LogError(
                "No positions found for the DEFAULT tenant. Run 'seed-hr-all' first — this command "
                + "populates an establishment, it does not create one.");
            return false;
        }

        _logger.LogInformation("Seeding HR/SHE DEMONSTRATION data into the DEFAULT tenant ({TenantId}).", tenantId);

        var steps = BuildSteps(tenantId);

        int ran = 0, skipped = 0;
        var failures = new List<(string Step, string Error)>();

        foreach (var step in steps)
        {
            bool already;
            try
            {
                already = await step.AlreadySeeded(ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "  [FAIL] {Step} — could not check whether it was already seeded.", step.Name);
                failures.Add((step.Name, $"probe threw: {ex.Message}"));
                continue;
            }

            if (already)
            {
                _logger.LogInformation("  [skip] {Step} — already seeded.", step.Name);
                skipped++;
                continue;
            }

            _logger.LogInformation("  [run ] {Step} …", step.Name);
            try
            {
                await step.RunAsync(ct);
                ran++;
            }
            catch (Exception ex)
            {
                // Isolated on purpose — see the class summary. The change tracker may hold the
                // half-built graph that threw, and carrying it into the next step would make an
                // unrelated seeder fail on somebody else's rows, so it is discarded here.
                _logger.LogError(ex, "  [FAIL] {Step} — {Message}", step.Name, ex.Message);
                failures.Add((step.Name, ex.Message));
                DiscardPendingChanges();
            }
        }

        _logger.LogInformation(
            "HR demo seeding finished — {Ran} ran, {Skipped} skipped, {Failed} failed.",
            ran, skipped, failures.Count);

        foreach (var (stepName, error) in failures)
        {
            _logger.LogWarning("  FAILED: {Step} — {Error}", stepName, error);
        }

        return true;
    }

    /// <summary>
    /// Detaches everything the failed step left in the change tracker, so the next step starts
    /// clean.
    /// </summary>
    private void DiscardPendingChanges()
    {
        foreach (var entry in _context.ChangeTracker.Entries().ToList())
        {
            entry.State = EntityState.Detached;
        }
    }

    /// <summary>
    /// Steps in dependency order. The leave vocabulary and the workforce come first because almost
    /// everything after them resolves employees by query and silently seeds nothing when the list is
    /// empty.
    /// </summary>
    private List<SeedStep> BuildSteps(Guid tenantId) => new()
    {
        // ── Foundation ──────────────────────────────────────────────────────────────────────────

        new SeedStep(
            "Leave types and the Ghana holiday calendar",
            ct => _context.Set<LeaveType>().IgnoreQueryFilters()
                          .AnyAsync(t => t.TenantId == tenantId && !t.IsDeleted, ct),
            ct => new TdcDemoLeaveCalendarSeeder(_context, Log<TdcDemoLeaveCalendarSeeder>()).SeedAsync(ct)),

        new SeedStep(
            "Demo workforce (staffs the establishment)",
            // Keyed on the register THIS seeder issues into, not on "any employee": the estate
            // module contributes 24 fixtures to a fresh database, so an any-employee probe would
            // skip the step for ever.
            ct => _context.Employees.IgnoreQueryFilters()
                          .AnyAsync(e => e.TenantId == tenantId && e.EmployeeNumber.StartsWith("TDC/"), ct),
            ct => new TdcDemoWorkforceSeeder(_context, Log<TdcDemoWorkforceSeeder>()).SeedAsync(ct)),

        new SeedStep(
            // Three lookup tables that have no API door at all (DbSet, table and migration only):
            // nothing else can ever fill them, so they are seeded here rather than by a scenario.
            "Legacy org lookups (contract types, divisions, work stations)",
            ct => _context.Set<EmployeeContractType>().IgnoreQueryFilters()
                          .AnyAsync(t => t.TenantId == tenantId && !t.IsDeleted, ct),
            ct => new TdcDemoLegacyOrgSeeder(_context, Log<TdcDemoLegacyOrgSeeder>()).SeedAsync(ct)),

        new SeedStep(
            // TDC's 2026 salary scale (records shared/ERP Salary Scale 2026.xlsx) into PAYROLL's
            // tables, which are the master; the projection carries it into HR on the next read.
            // Keyed on a notch the demo-smoke scenario never wrote: 141 invents five per grade, the
            // scale runs to twenty and beyond. The seeder itself is an ensure — it updates the five.
            "Salary scale 2026 (payroll grades and notches)",
            ct => _context.Set<PayrollGradeNotch>().IgnoreQueryFilters()
                          .AnyAsync(n => n.TenantId == tenantId && !n.IsDeleted && n.GradeId == "M1" && n.Notch == "20", ct),
            ct => new TdcSalaryScaleSeeder(_context, Log<TdcSalaryScaleSeeder>()).SeedAsync(ct)),

        // ── Pay and benefits ────────────────────────────────────────────────────────────────────

        new SeedStep(
            "Emoluments (pay components)",
            ct => _context.Set<PayComponent>().IgnoreQueryFilters()
                          .AnyAsync(c => c.TenantId == tenantId && !c.IsDeleted, ct),
            async ct => await new EmolumentDataSeeder(_context, Log<EmolumentDataSeeder>()).SeedAsync()),

        new SeedStep(
            "Benefit policies",
            ct => _context.Set<BenefitPolicy>().IgnoreQueryFilters()
                          .AnyAsync(p => p.TenantId == tenantId && !p.IsDeleted, ct),
            async ct => await new BenefitPolicyDataSeeder(_context, Log<BenefitPolicyDataSeeder>()).SeedAsync()),

        new SeedStep(
            "Benefit enrolments and enterprise data",
            ct => _context.Set<EmployeeBenefitEnrollment>().IgnoreQueryFilters()
                          .AnyAsync(e => e.TenantId == tenantId && !e.IsDeleted, ct),
            async ct => await new BenefitEnterpriseDataSeeder(_context, Log<BenefitEnterpriseDataSeeder>()).SeedAsync()),

        // ── Employee lifecycle ──────────────────────────────────────────────────────────────────

        new SeedStep(
            "Orientation and onboarding",
            ct => _context.Set<OrientationProgram>().IgnoreQueryFilters()
                          .AnyAsync(p => p.TenantId == tenantId && !p.IsDeleted, ct),
            async ct => await new OrientationDataSeeder(_context, Log<OrientationDataSeeder>()).SeedAsync()),

        new SeedStep(
            "Performance appraisal",
            // Probed on a grade definition, which is what this seeder actually creates. An earlier
            // probe asked for an AppraisalCycle — a row the seeder never writes — so the step would
            // have reported "not seeded" and re-run on every invocation for ever.
            ct => _context.Set<AppraisalGradeDefinition>().IgnoreQueryFilters()
                          .AnyAsync(g => g.TenantId == tenantId && !g.IsDeleted, ct),
            ct => new PerformanceAppraisalDataSeeder(_context, Log<PerformanceAppraisalDataSeeder>())
                      .SeedForTenantAsync(tenantId, ct)),

        new SeedStep(
            // A free-text item on the appraisal template. Without one, AppraisalCustomQuestionResponses
            // can never hold a row, and the template cannot be edited through the API once the demo
            // cycle is open (409) — so it is added here, before any cycle exists.
            "Appraisal free-text question",
            ct => _context.Set<AppraisalTemplateItem>().IgnoreQueryFilters()
                          .AnyAsync(i => i.TenantId == tenantId && !i.IsDeleted
                                      && i.CompetencyId == null && i.KpiDefinitionId == null
                                      && i.CustomQuestion != null, ct),
            ct => new TdcDemoAppraisalCustomQuestionSeeder(_context, Log<TdcDemoAppraisalCustomQuestionSeeder>()).SeedAsync(ct)),

        new SeedStep(
            "Awards and recognition",
            ct => _context.Set<AwardType>().IgnoreQueryFilters()
                          .AnyAsync(a => a.TenantId == tenantId && !a.IsDeleted, ct),
            ct => new AwardDataSeeder(_context, Log<AwardDataSeeder>())
                      .SeedForTenantAsync(tenantId, ct)),

        new SeedStep(
            // TeamAwardRecipients has a repository and no writer anywhere in the API, so a team
            // award can be nominated but never conferred through a door. Seeded so the screen has
            // last year's team award to show.
            "Team award recipients (the one awards table with no door)",
            ct => _context.Set<TeamAwardRecipient>().IgnoreQueryFilters()
                          .AnyAsync(r => r.TenantId == tenantId && !r.IsDeleted, ct),
            ct => new TdcDemoTeamAwardSeeder(_context, Log<TdcDemoTeamAwardSeeder>()).SeedAsync(ct)),

        // ── Health and safety ───────────────────────────────────────────────────────────────────

        new SeedStep(
            "Medical and health",
            // Probed on the first thing the seeder creates — the facility register — rather than on
            // a claim. Claims hang off employees, providers and policies, so an empty claims table
            // is ambiguous: it means either "not seeded" or "seeded, and the claim step failed".
            ct => _context.Set<HealthcareFacility>().IgnoreQueryFilters()
                          .AnyAsync(f => f.TenantId == tenantId && !f.IsDeleted, ct),
            async ct => await new MedicalDataSeeder(_context, Log<MedicalDataSeeder>()).SeedAsync()),

        new SeedStep(
            "SHE — safety, health and environment",
            ct => _context.Set<SafetyIncident>().IgnoreQueryFilters()
                          .AnyAsync(i => i.TenantId == tenantId && !i.IsDeleted, ct),
            async ct => await new SheDataSeeder(_context, Log<SheDataSeeder>()).SeedAsync()),

        // ── Supporting ──────────────────────────────────────────────────────────────────────────

        new SeedStep(
            "External associates",
            ct => _context.Set<ExternalAssociate>().IgnoreQueryFilters()
                          .AnyAsync(a => a.TenantId == tenantId && !a.IsDeleted, ct),
            ct => new ExternalAssociateDataSeeder(_context, Log<ExternalAssociateDataSeeder>())
                      .SeedForTenantAsync(tenantId, ct)),

        new SeedStep(
            "Email templates",
            // Skipped rather than failed when no catalog is registered: the seed host builds a
            // reduced service graph, and an empty catalog list means "nothing to seed here", not
            // "something is broken".
            ct => _emailCatalogs.Any()
                ? _context.Set<EmailTemplate>().IgnoreQueryFilters()
                          .AnyAsync(t => t.TenantId == tenantId && !t.IsDeleted, ct)
                : Task.FromResult(true),
            ct => new EmailTemplateCatalogSeeder(_context, _emailCatalogs, Log<EmailTemplateCatalogSeeder>())
                      .SeedAsync(ct)),

        new SeedStep(
            "HR awards report definitions",
            ct => Task.FromResult(false),
            ct => new HrAwardsReportSeeder(_context, Log<HrAwardsReportSeeder>())
                      .SeedTenantAsync(tenantId, ct)),

        // ── Second pass: tables whose only route is blocked, and which hang off SCENARIO rows ────
        //
        // These three read rows that dev-harness/hr-demo-smoke/scenarios.mjs creates through the API
        // (training budgets, staff movements, probation periods), so on a fresh database they find
        // nothing and skip. New-UatDatabase.ps1 therefore runs 'seed-hr-demo' a SECOND time after the
        // scenarios; every other step's guard makes that second pass a no-op. Each of the three exists
        // because the API door is unusable: the approve doors need an HR.*.Admin permission held only
        // by accounts with no employee link, and the movement ladder has no writer at all.

        new SeedStep(
            "Training budget approval and payments (the approve door needs HR.Training.Admin)",
            ct => _context.Set<TrainingBudgetTransaction>().IgnoreQueryFilters()
                          .AnyAsync(t => t.TenantId == tenantId && !t.IsDeleted, ct),
            ct => new TdcDemoTrainingBudgetSeeder(_context, Log<TdcDemoTrainingBudgetSeeder>()).SeedAsync(ct)),

        new SeedStep(
            "Staff-movement approval ladder (needs the movements scenario first)",
            ct => _context.Set<StaffMovementApprovalLevel>().IgnoreQueryFilters()
                          .AnyAsync(l => l.TenantId == tenantId && !l.IsDeleted, ct),
            ct => new TdcDemoMovementApprovalLevelSeeder(_context, Log<TdcDemoMovementApprovalLevelSeeder>()).SeedAsync(ct)),

        new SeedStep(
            "Probation extension (needs the probation scenario first)",
            ct => _context.Set<ProbationExtension>().IgnoreQueryFilters()
                          .AnyAsync(e => e.TenantId == tenantId && !e.IsDeleted, ct),
            ct => new TdcDemoProbationExtensionSeeder(_context, Log<TdcDemoProbationExtensionSeeder>()).SeedAsync(ct)),
    };

    private ILogger<T> Log<T>() => _loggerFactory.CreateLogger<T>();
}
