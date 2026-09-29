using ErpSystem.Core.Entities;
using ErpSystem.Data;
using ErpSystem.Data.Seeders;
using ErpSystem.Web.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services;

/// <summary>
/// The Developer Test Data screen (Administration → HR → HR Settings): the HR seeding that used to
/// need <c>seed-hr-all</c> / <c>seed-hr-demo</c> on the command line, as three buttons, so developers
/// on other modules can give their database an organisation, people and logins to work with.
/// </summary>
/// <remarks>
/// <para><b>Three tiers, each including the ones before it.</b> Foundation = <c>seed-hr-all</c>
/// (reference data, the TDC organisation, positions, locations) plus HR's own approval workflows.
/// Workforce = the five "Foundation" steps of <c>seed-hr-demo</c> (leave vocabulary, the synthetic
/// workforce, the approved establishment, legacy org lookups, the salary scale) — and none of its
/// demo-only area seeders. Logins = the demo personas, linked to that workforce.</para>
///
/// <para><b>It seeds the DEFAULT tenant</b>, exactly as the command line does, so it may be run by a
/// SuperAdmin, or by a TenantAdmin whose own tenant IS DEFAULT — never by another tenant's admin on
/// DEFAULT's behalf.</para>
///
/// <para><b>Never synthetic people beside real ones.</b> The persona seeder links each login to the
/// first <c>TDC/</c> employee holding a given post, and an imported workforce carries <c>TDC/</c>
/// numbers too, so on a database with real staff it would hand demo logins (password
/// <c>Demo123!</c>) to real people. The Workforce and Logins tiers therefore refuse while the tenant
/// holds any <c>TDC/</c> employee the demo workforce seeder did not create.</para>
///
/// <para><b>Not in Production</b> unless <c>HrTestData:AllowInProduction</c> is set. Staging and
/// Development are allowed: this repo's API usually runs as Staging.</para>
///
/// <para><b>One run at a time, in the background.</b> A seed takes minutes; the request returns at
/// once and the screen polls. The run log lives in memory — a restart forgets it, but the status
/// itself is always re-read from the probes, so nothing that matters is lost.</para>
/// </remarks>
public interface IHrTestDataSeedService
{
    Task<HrTestDataStatusDto> GetStatusAsync(CancellationToken ct = default);

    Task<HrTestDataStartResult> StartAsync(
        HrTestDataTier tier, string requestedBy, Guid? requesterTenantId, bool requesterIsSuperAdmin,
        CancellationToken ct = default);
}

public enum HrTestDataTier
{
    Foundation = 1,
    Workforce = 2,
    Logins = 3,
}

public enum HrTestDataStartOutcome
{
    Started,
    Disabled,
    Forbidden,
    Busy,
    Blocked,
}

public sealed record HrTestDataStartResult(HrTestDataStartOutcome Outcome, string Message);

public sealed record HrTestDataStatusDto(
    bool Enabled,
    string? DisabledReason,
    string Environment,
    string TenantCode,
    int RealStaffCount,
    string? SyntheticStaffBlockedReason,
    IReadOnlyList<HrTestDataTierDto> Tiers,
    IReadOnlyList<HrTestDataPersonaDto> Personas,
    string PersonaPassword,
    HrTestDataRunDto? CurrentRun,
    HrTestDataRunDto? LastRun,
    string? StateError);

public sealed record HrTestDataTierDto(
    string Key, string Title, string Description, bool Complete, IReadOnlyList<HrTestDataStepDto> Steps);

public sealed record HrTestDataStepDto(string Name, bool Present, bool AlwaysRuns);

public sealed record HrTestDataPersonaDto(
    string Username, string PositionTitle, string Purpose, IReadOnlyList<string> Roles,
    bool Exists, string? LinkedEmployee);

public sealed record HrTestDataRunDto(
    Guid Id, string Tier, string RequestedBy, DateTime StartedAt, DateTime? FinishedAt,
    string State, string? Error, IReadOnlyList<HrTestDataOutcomeDto> Steps);

public sealed record HrTestDataOutcomeDto(string Name, string Result, string? Error);

public sealed class HrTestDataSeedService : IHrTestDataSeedService
{
    public const string AllowInProductionKey = "HrTestData:AllowInProduction";
    private const string DefaultTenantCode = "DEFAULT";
    private const string WorkforceSeederName = "TdcDemoWorkforceSeeder";
    private const string WorkflowsStep = "HR approval workflows (HR and leave)";
    private const string PersonasStep = "Demo persona logins";

    private readonly IServiceScopeFactory _scopes;
    private readonly IWebHostEnvironment _environment;
    private readonly IConfiguration _configuration;
    private readonly ILogger<HrTestDataSeedService> _logger;

    private readonly object _gate = new();
    private RunState? _current;
    private RunState? _last;

    public HrTestDataSeedService(
        IServiceScopeFactory scopes, IWebHostEnvironment environment, IConfiguration configuration,
        ILogger<HrTestDataSeedService> logger)
    {
        _scopes = scopes;
        _environment = environment;
        _configuration = configuration;
        _logger = logger;
    }

    private string? DisabledReason =>
        _environment.IsProduction() && !_configuration.GetValue<bool>(AllowInProductionKey)
            ? $"This API runs as {_environment.EnvironmentName}. Test data is not seeded in production "
              + $"unless '{AllowInProductionKey}' is set to true."
            : null;

    public async Task<HrTestDataStatusDto> GetStatusAsync(CancellationToken ct = default)
    {
        RunState? current, last;
        lock (_gate) { current = _current; last = _last; }

        var tiers = new List<HrTestDataTierDto>();
        var personas = new List<HrTestDataPersonaDto>();
        int realStaff = 0;
        string? stateError = null;

        try
        {
            using var scope = _scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var loggers = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();

            var foundation = await new HrSeedOrchestrator(db, loggers).GetStateAsync(ct);
            var tenantId = await DefaultTenantIdAsync(db, ct);
            if (foundation is null || tenantId is null)
            {
                stateError = "There is no DEFAULT tenant to seed. Build the database and run the core seed "
                             + "('seed-db') first.";
            }
            else
            {
                var foundationSteps = foundation
                    .Select(s => new HrTestDataStepDto(s.Name, s.Present, s.AlwaysRuns))
                    .Append(new HrTestDataStepDto(WorkflowsStep, false, true))
                    .ToList();
                tiers.Add(Tier(HrTestDataTier.Foundation, foundationSteps));

                var workforce = await new HrDemoSeedOrchestrator(db, loggers)
                    .GetStateAsync(HrDemoSeedOrchestrator.WorkforceStepNames, ct) ?? Array.Empty<HrSeedStepState>();
                tiers.Add(Tier(HrTestDataTier.Workforce,
                    workforce.Select(s => new HrTestDataStepDto(s.Name, s.Present, s.AlwaysRuns)).ToList()));

                var usernames = TdcDemoPersonaSeeder.Personas.Select(p => p.Username).ToList();
                var users = await db.Users.AsNoTracking()
                    .Where(u => u.UserName != null && usernames.Contains(u.UserName))
                    .Select(u => new { u.UserName, u.EmployeeId })
                    .ToListAsync(ct);
                var linkedIds = users.Where(u => u.EmployeeId != null).Select(u => u.EmployeeId!.Value).ToList();
                var linked = await db.Employees.IgnoreQueryFilters().AsNoTracking()
                    .Where(e => linkedIds.Contains(e.Id))
                    .Select(e => new { e.Id, Label = e.EmployeeNumber + " " + e.FirstName + " " + e.LastName })
                    .ToDictionaryAsync(e => e.Id, e => e.Label, ct);

                foreach (var p in TdcDemoPersonaSeeder.Personas)
                {
                    var user = users.FirstOrDefault(u => string.Equals(u.UserName, p.Username, StringComparison.OrdinalIgnoreCase));
                    personas.Add(new HrTestDataPersonaDto(
                        p.Username, p.PositionTitle, p.Purpose, p.Roles, user is not null,
                        user?.EmployeeId is Guid id && linked.TryGetValue(id, out var label) ? label : null));
                }

                tiers.Add(new HrTestDataTierDto(
                    "logins", "Logins",
                    "The demo personas (hr.head, head.dev, staff, she.officer, …), each linked to an employee "
                    + "of the workforce, so approvals and self-service have real people to act as.",
                    personas.All(p => p.Exists),
                    new[] { new HrTestDataStepDto(PersonasStep, personas.All(p => p.Exists), false) }));

                realStaff = await CountRealStaffAsync(db, tenantId.Value, ct);
            }
        }
        catch (Exception ex)
        {
            // Almost always a missing table: the schema is older than this build.
            _logger.LogWarning(ex, "Developer test data: could not read the seed state.");
            stateError = "Could not read the seed state — the database schema may be out of date: " + ex.Message;
        }

        return new HrTestDataStatusDto(
            DisabledReason is null, DisabledReason, _environment.EnvironmentName, DefaultTenantCode,
            realStaff, BlockedReason(realStaff), tiers, personas, TdcDemoPersonaSeeder.Password,
            current?.ToDto(), last?.ToDto(), stateError);
    }

    public async Task<HrTestDataStartResult> StartAsync(
        HrTestDataTier tier, string requestedBy, Guid? requesterTenantId, bool requesterIsSuperAdmin,
        CancellationToken ct = default)
    {
        if (DisabledReason is { } disabled)
            return new(HrTestDataStartOutcome.Disabled, disabled);

        Guid? defaultTenantId;
        int realStaff;
        using (var scope = _scopes.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            defaultTenantId = await DefaultTenantIdAsync(db, ct);
            if (defaultTenantId is null)
                return new(HrTestDataStartOutcome.Blocked,
                    "There is no DEFAULT tenant to seed. Build the database and run the core seed ('seed-db') first.");
            realStaff = tier == HrTestDataTier.Foundation ? 0 : await CountRealStaffAsync(db, defaultTenantId.Value, ct);
        }

        if (!requesterIsSuperAdmin && requesterTenantId != defaultTenantId)
            return new(HrTestDataStartOutcome.Forbidden,
                "This seeds the DEFAULT tenant. Only a SuperAdmin, or an admin of the DEFAULT tenant itself, may run it.");

        if (BlockedReason(realStaff) is { } blocked)
            return new(HrTestDataStartOutcome.Blocked, blocked);

        RunState run;
        lock (_gate)
        {
            if (_current is not null)
                return new(HrTestDataStartOutcome.Busy,
                    $"A {_current.Tier.ToString().ToLowerInvariant()} seed started by {_current.RequestedBy} is still running.");
            run = new RunState(tier, requestedBy);
            _current = run;
        }

        _logger.LogInformation("Developer test data: {Tier} seed started by {User}.", tier, requestedBy);
        // Not awaited, and not tied to the request: the request returns 202 while this runs.
        _ = Task.Run(() => RunAsync(run));
        return new(HrTestDataStartOutcome.Started, $"{tier} seed started.");
    }

    private async Task RunAsync(RunState run)
    {
        var progress = new SyncProgress(run.Add);
        try
        {
            // Each phase in its own scope, so a fresh DbContext: the orchestrators were written for
            // separate command-line processes, and the thousands of rows one leaves tracked would only
            // slow the next and risk it tripping over them.
            using (var scope = _scopes.CreateScope())
            {
                var sp = scope.ServiceProvider;
                var ok = await new HrSeedOrchestrator(sp.GetRequiredService<ApplicationDbContext>(),
                        sp.GetRequiredService<ILoggerFactory>())
                    .SeedAsync(CancellationToken.None, progress);
                if (!ok) throw new InvalidOperationException("The foundation could not start — see the API log.");
            }

            using (var scope = _scopes.CreateScope())
            {
                await scope.ServiceProvider.GetRequiredService<IDatabaseSeedingService>().SeedHrWorkflowDefinitionsAsync();
                run.Add(new HrSeedStepOutcome(WorkflowsStep, HrSeedStepResult.Ran));
            }

            if (run.Tier >= HrTestDataTier.Workforce)
            {
                using var scope = _scopes.CreateScope();
                var sp = scope.ServiceProvider;
                var ok = await new HrDemoSeedOrchestrator(sp.GetRequiredService<ApplicationDbContext>(),
                        sp.GetRequiredService<ILoggerFactory>())
                    .SeedAsync(CancellationToken.None, progress, HrDemoSeedOrchestrator.WorkforceStepNames);
                if (!ok) throw new InvalidOperationException("The workforce could not start — see the API log.");
            }

            if (run.Tier >= HrTestDataTier.Logins)
            {
                using var scope = _scopes.CreateScope();
                var sp = scope.ServiceProvider;
                var created = await new TdcDemoPersonaSeeder(
                        sp.GetRequiredService<ApplicationDbContext>(),
                        sp.GetRequiredService<UserManager<ApplicationUser>>(),
                        sp.GetRequiredService<RoleManager<ApplicationRole>>(),
                        sp.GetRequiredService<ILoggerFactory>().CreateLogger<TdcDemoPersonaSeeder>())
                    .SeedAsync();
                run.Add(new HrSeedStepOutcome(PersonasStep,
                    created > 0 ? HrSeedStepResult.Ran : HrSeedStepResult.Skipped,
                    created > 0 ? $"{created} login(s) created" : null));
            }

            run.Finish(null);
            _logger.LogInformation("Developer test data: {Tier} seed finished.", run.Tier);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Developer test data: {Tier} seed failed.", run.Tier);
            run.Finish(ex.Message);
        }
        finally
        {
            lock (_gate)
            {
                _last = run;
                _current = null;
            }
        }
    }

    private static HrTestDataTierDto Tier(HrTestDataTier tier, IReadOnlyList<HrTestDataStepDto> steps) => tier switch
    {
        HrTestDataTier.Foundation => new("foundation", "Foundation",
            "Reference data (countries, Ghana's geography, qualifications, languages, job architecture), the "
            + "TDC organisation — levels, grades, units, positions — its locations, and HR's approval workflows. "
            + "No people.",
            steps.All(s => s.Present || s.AlwaysRuns), steps),
        _ => new("workforce", "Workforce",
            "Leave types and the Ghana holiday calendar, a synthetic workforce of about a hundred staff on the "
            + "TDC establishment with their line managers, the approved establishment, contract types and "
            + "divisions, and the 2026 salary scale.",
            steps.All(s => s.Present), steps),
    };

    private static Task<Guid?> DefaultTenantIdAsync(ApplicationDbContext db, CancellationToken ct) =>
        db.Tenants.Where(t => t.Code == DefaultTenantCode).Select(t => (Guid?)t.Id).FirstOrDefaultAsync(ct);

    /// <summary><c>TDC/</c> staff the demo workforce seeder did not create: imported, or entered by hand.</summary>
    private static Task<int> CountRealStaffAsync(ApplicationDbContext db, Guid tenantId, CancellationToken ct) =>
        db.Employees.IgnoreQueryFilters().AsNoTracking()
            .CountAsync(e => e.TenantId == tenantId && !e.IsDeleted
                          && e.EmployeeNumber.StartsWith("TDC/")
                          && (e.CreatedBy == null || e.CreatedBy != WorkforceSeederName), ct);

    private static string? BlockedReason(int realStaff) => realStaff == 0
        ? null
        : $"The DEFAULT tenant holds {realStaff} TDC/ staff record(s) the demo seeder did not create — imported "
          + "or entered by hand. Synthetic staff and demo logins are not added beside real people, because the "
          + "logins would be linked to them. The Foundation tier is still available.";

    private sealed class SyncProgress : IProgress<HrSeedStepOutcome>
    {
        private readonly Action<HrSeedStepOutcome> _report;
        public SyncProgress(Action<HrSeedStepOutcome> report) => _report = report;
        public void Report(HrSeedStepOutcome value) => _report(value);
    }

    private sealed class RunState
    {
        private readonly object _lock = new();
        private readonly List<HrSeedStepOutcome> _steps = new();

        public RunState(HrTestDataTier tier, string requestedBy)
        {
            Tier = tier;
            RequestedBy = requestedBy;
        }

        public Guid Id { get; } = Guid.NewGuid();
        public HrTestDataTier Tier { get; }
        public string RequestedBy { get; }
        public DateTime StartedAt { get; } = DateTime.UtcNow;
        public DateTime? FinishedAt { get; private set; }
        public string? Error { get; private set; }

        public void Add(HrSeedStepOutcome outcome) { lock (_lock) _steps.Add(outcome); }

        public void Finish(string? error) { lock (_lock) { Error = error; FinishedAt = DateTime.UtcNow; } }

        public HrTestDataRunDto ToDto()
        {
            lock (_lock)
            {
                var state = FinishedAt is null ? "Running"
                    : Error is not null || _steps.Any(s => s.Result == HrSeedStepResult.Failed) ? "Failed"
                    : "Succeeded";
                return new HrTestDataRunDto(
                    Id, Tier.ToString(), RequestedBy, StartedAt, FinishedAt, state, Error,
                    _steps.Select(s => new HrTestDataOutcomeDto(s.Name, s.Result.ToString(), s.Error)).ToList());
            }
        }
    }
}
