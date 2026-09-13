using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.Reference;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Data.Seeders;

/// <summary>
/// Single entry point for seeding the HR module — run it with:
/// <code>dotnet run --project src/ErpSystem.Api seed-hr-all</code>
///
/// <para><b>Idempotent by construction.</b> Every step declares an "already seeded?" probe and is
/// skipped when that probe is satisfied. This matters because most of the underlying seeders were
/// ported without their own re-run guards and would happily duplicate rows — the guard lives here so
/// the behaviour is uniform and visible in one place, rather than depending on each seeder.
/// Re-running is therefore always safe; a second run reports every step as "skipped".</para>
///
/// <para>Everything targets the <b>DEFAULT tenant</b> — the one the <c>admin</c> user signs in to —
/// so seeded data is visible on a normal login.</para>
///
/// <para><b>Prerequisites:</b> the database must exist with the HR schema, and the DEFAULT tenant and
/// admin user must be present. That means running <c>rebuild-db</c> and then <c>seed</c> first.
/// (The EF migration chain cannot currently build the schema from scratch — a pre-existing,
/// platform-wide issue — so <c>rebuild-db</c> is the supported path.)</para>
/// </summary>
public class HrSeedOrchestrator
{
    private readonly ApplicationDbContext _context;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<HrSeedOrchestrator> _logger;

    public HrSeedOrchestrator(ApplicationDbContext context, ILoggerFactory loggerFactory)
    {
        _context = context;
        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger<HrSeedOrchestrator>();
    }

    /// <summary>A unit of seeding: skipped when <paramref name="AlreadySeeded"/> reports true.</summary>
    private sealed record SeedStep(
        string Name,
        Func<CancellationToken, Task<bool>> AlreadySeeded,
        Func<CancellationToken, Task> RunAsync);

    /// <returns><c>true</c> when every step either ran or was skipped; <c>false</c> on a hard stop.</returns>
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
            _logger.LogError(
                "DEFAULT tenant not found — nothing to seed against. Run 'rebuild-db' then 'seed' first.");
            return false;
        }

        var tenantId = tenant.Id;
        _logger.LogInformation("Seeding HR data into the DEFAULT tenant ({TenantId}).", tenantId);

        var steps = BuildSteps(tenantId);

        int ran = 0, skipped = 0;
        foreach (var step in steps)
        {
            bool already;
            try
            {
                already = await step.AlreadySeeded(ct);
            }
            catch (Exception ex)
            {
                // Almost always a missing table: the HR schema was never created.
                _logger.LogError(ex,
                    "Could not check '{Step}'. The HR schema is probably missing — run 'rebuild-db' first.",
                    step.Name);
                return false;
            }

            if (already)
            {
                _logger.LogInformation("  [skip] {Step} — already seeded.", step.Name);
                skipped++;
                continue;
            }

            _logger.LogInformation("  [run ] {Step} …", step.Name);
            await step.RunAsync(ct);
            ran++;
        }

        _logger.LogInformation("HR seeding complete — {Ran} step(s) ran, {Skipped} skipped.", ran, skipped);

        if (DeferredSteps.Length > 0)
        {
            _logger.LogInformation(
                "Not seeded yet ({Count}) — see HrSeedOrchestrator.DeferredSteps: {Names}",
                DeferredSteps.Length, string.Join(", ", DeferredSteps.Select(d => d.Name)));
        }

        return true;
    }

    /// <summary>
    /// Steps in dependency order. Reference lookups first (locations resolve Ghana from Countries),
    /// then the TDC organisation, and finally number-sequence reconciliation, which reads whatever
    /// document numbers exist by then.
    /// </summary>
    private List<SeedStep> BuildSteps(Guid tenantId) => new()
    {
        new SeedStep(
            "Countries",
            ct => _context.Set<Country>().AnyAsync(x => x.TenantId == tenantId, ct),
            ct => new CountrySeeder(_context, Log<CountrySeeder>()).SeedAsync(tenantId)),

        // Depends on Countries above, to resolve Ghana. Shared reference data rather than HR's, but
        // it seeds here because this is the only orchestrator that runs — see
        // docs/GEOGRAPHY-REFERENCE-DESIGN.md. The probe asks for the scheme THIS seed creates, not
        // for "any scheme exists": a tenant that had added a scheme of its own would otherwise
        // silently never receive Ghana's, which is the trap the job-architecture step below records.
        new SeedStep(
            "Ghana administrative geography (Region → District → Town → Community)",
            ct => _context.Set<GeoScheme>()
                          .AnyAsync(s => s.TenantId == tenantId
                                      && s.Code == GhanaGeographySeeder.SchemeCode && !s.IsDeleted, ct),
            ct => new GhanaGeographySeeder(_context, Log<GhanaGeographySeeder>()).SeedAsync(tenantId, ct)),

        new SeedStep(
            "Identification types",
            ct => _context.Set<IdentificationType>().AnyAsync(x => x.TenantId == tenantId, ct),
            ct => new IdentificationTypeSeeder(_context, Log<IdentificationTypeSeeder>()).SeedAsync(tenantId)),

        new SeedStep(
            "Qualifications",
            ct => _context.Set<Qualification>().AnyAsync(x => x.TenantId == tenantId, ct),
            ct => new QualificationSeeder(_context, Log<QualificationSeeder>()).SeedAsync(tenantId)),

        // The relationship vocabulary the referee, guarantor, next-of-kin and candidate-referee
        // screens pick from (round 2, lane D2). Depends on nothing. The probe asks for a row THIS
        // seed creates rather than for a non-empty table — see the job-architecture step below for
        // why that distinction is not pedantry.
        new SeedStep(
            "Relationship types (familial, professional, other)",
            ct => _context.Set<RelationshipType>()
                          .AnyAsync(x => x.TenantId == tenantId
                                      && x.Code == RelationshipTypeSeeder.ProbeCode, ct),
            ct => new RelationshipTypeSeeder(_context, Log<RelationshipTypeSeeder>()).SeedAsync(tenantId, ct)),

        // Round 3, lane C1: the language catalogue a candidate picks from. Probes on the code
        // "EN" rather than a non-empty table, like the relationship types above.
        new SeedStep(
            "Languages (Ghanaian, regional, international)",
            ct => _context.Set<Language>()
                          .AnyAsync(x => x.TenantId == tenantId
                                      && x.Code == LanguageSeeder.ProbeCode, ct),
            ct => new LanguageSeeder(_context, Log<LanguageSeeder>()).SeedAsync(tenantId, ct)),

        // Round 3, lane P2: the disability catalogue the employee and dependant forms pick from.
        // Probes on the code "VISUAL", like the two catalogues above.
        new SeedStep(
            "Disability types (census / Act 715 groupings)",
            ct => _context.Set<DisabilityType>()
                          .AnyAsync(x => x.TenantId == tenantId
                                      && x.Code == DisabilityTypeSeeder.ProbeCode, ct),
            ct => new DisabilityTypeSeeder(_context, Log<DisabilityTypeSeeder>()).SeedAsync(tenantId, ct)),

        new SeedStep(
            "Skills",
            ct => _context.Set<Skill>().AnyAsync(x => x.TenantId == tenantId, ct),
            ct => new SkillDataSeeder(_context, Log<SkillDataSeeder>()).SeedForDefaultTenantAsync(ct)),

        // The job-family / sub-family / career-level vocabulary a job description is classified
        // against (area 17, decision D-5). Deliberately after Skills and before the organogram:
        // it depends on neither, and belongs with the other reference lookups.
        new SeedStep(
            "Job architecture (families, sub-families, career levels)",
            // The probe asks for a row THIS SEED creates, not for a non-empty table. Measured
            // 2026-08-19: an "any job family exists?" probe skipped the step entirely, because the
            // area-17 harness had already created twenty families of its own — so a tenant where
            // anyone had ever added one job family would silently never receive the starter
            // vocabulary. A starter-vocabulary seed and a create-the-baseline seed need different
            // questions: "is the table empty" is only the right signal for the latter.
            ct => _context.Set<JobFamily>().AnyAsync(x => x.TenantId == tenantId && x.Code == "EXE", ct),
            ct => new JobArchitectureSeeder(_context, Log<JobArchitectureSeeder>()).SeedAsync(tenantId, ct)),

        // TDC organisation: levels, staff bands, the 8 salary grades, units and positions.
        new SeedStep(
            "TDC organisation structure",
            ct => _context.Set<OrganizationStructure>()
                          .AnyAsync(s => s.Code == "TDC" && s.TenantId == tenantId, ct),
            ct => new TdcOrganogramSeeder(_context, Log<TdcOrganogramSeeder>()).SeedAsync()),

        // Depends on Countries above, to link Ghana.
        new SeedStep(
            "TDC locations",
            ct => _context.Set<LocationStructure>()
                          .AnyAsync(s => s.Code == "TDC-LOC" && s.TenantId == tenantId, ct),
            ct => new TdcLocationSeeder(_context, Log<TdcLocationSeeder>()).SeedAsync()),

        // Reconciliation, not data creation: it only ever raises a sequence, never lowers it, so it
        // is inherently safe to repeat and deliberately has no skip probe.
        new SeedStep(
            "Number sequences (reconcile)",
            _ => Task.FromResult(false),
            ct => new NumberSequenceSeeder(_context, Log<NumberSequenceSeeder>()).SeedAsync(ct)),
    };

    /// <summary>
    /// Ported seeders deliberately NOT wired in yet, with the reason. Add a step above to enable one.
    /// </summary>
    internal static readonly (string Name, string Reason)[] DeferredSteps =
    {
        ("EmployeePositionDataSeeder",
            "Superseded — TdcOrganogramSeeder now seeds the real TDC position catalogue. Running both " +
            "would create two competing sets of positions."),
        ("HRFullDataSeeder",
            "Creates generic demo employees/departments. Employee seeding is deferred until the real " +
            "TDC employee-details file is available."),
        ("AwardDataSeeder, BenefitPolicyDataSeeder, BenefitEnterpriseDataSeeder, EmolumentDataSeeder, " +
         "MedicalDataSeeder, OrientationDataSeeder, PerformanceAppraisalDataSeeder, SheDataSeeder, " +
         "ExternalAssociateDataSeeder",
            "Still carry generic sample data from the standalone HR solution. The TDC HR questionnaire " +
            "supplies real values (leave, allowances, pensions, orientation checklist, separation and " +
            "loan types) — these should be rewritten against it before being seeded."),
        ("EmailTemplateCatalogSeeder",
            "Templates are not TDC-branded yet; enable once the wording is agreed. Seeds editable "
            + "EmailTemplate rows from EVERY registered IEmailEventCatalog — recruitment's "
            + "transactional emails, the FR-HR-032 confirmation letter, and AST-5's asset "
            + "responsibility-and-terms form. Until it runs, all of them render from their built-in "
            + "catalog defaults and none is listed in the email-template designer. It replaces the "
            + "per-module RecruitmentEmailTemplateSeeder and ProbationEmailTemplateSeeder, which were "
            + "identical but for the catalog they read."),
    };

    private ILogger<T> Log<T>() => _loggerFactory.CreateLogger<T>();
}
