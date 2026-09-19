using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Data;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services;

/// <summary>
/// Creates the demonstration <b>logins</b>: one user per persona a demo needs to show, each linked
/// to a real seeded employee and carrying the roles that persona would hold.
///
/// <para><b>Why the admin login is not enough.</b> The smoke run of 2026-09-02 drove 938 reads as
/// <c>admin</c> and 126 of them refused it — every <c>/me</c>, <c>/mine</c>, <c>/employee-portal</c>
/// and "awaiting my approval" surface answered <i>"your user account is not linked to an employee
/// record"</i> or 403. That is correct behaviour: the SuperAdmin is not an employee, has no line
/// manager, no leave balance, no team and no inbox. The whole self-service portal, every approval
/// queue and every "my team" screen is therefore undemonstrable from the only login the database
/// shipped with. A demo needs to <i>be</i> somebody.</para>
///
/// <para><b>Personas are resolved by position TITLE, not by staff number.</b> Staff numbers are
/// deterministic today, but they shift the moment the establishment gains a position or the vacancy
/// rule changes. "Head of HR &amp; Administration" is a fact of the organogram and survives both.
/// Where a title has several holders the lowest staff number is taken, which the workforce seeder
/// guarantees is the most senior.</para>
///
/// <para><b>Lives in the Api project, not beside the other seeders in Data.</b> It needs
/// <see cref="UserManager{TUser}"/> for password hashing and role membership, which is an Identity
/// concern the Data layer's seeders deliberately do not take a dependency on. Same placement as
/// <c>DatabaseSeedingService</c>, for the same reason.</para>
///
/// <para>Idempotent: an existing user is re-linked and re-roled but its password is never touched,
/// mirroring <c>DatabaseSeedingService.CreateTestUserAsync</c>.</para>
/// </summary>
public class TdcDemoPersonaSeeder
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly ILogger<TdcDemoPersonaSeeder> _logger;

    private const string By = "TdcDemoPersonaSeeder";

    /// <summary>
    /// One password for every persona. A demo presenter switching between six logins on stage does
    /// not need six passwords to remember; the runbook prints it once.
    /// </summary>
    public const string Password = "Demo123!";

    public TdcDemoPersonaSeeder(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        RoleManager<ApplicationRole> roleManager,
        ILogger<TdcDemoPersonaSeeder> logger)
    {
        _context = context;
        _userManager = userManager;
        _roleManager = roleManager;
        _logger = logger;
    }

    /// <summary>A demo login: who they are in the organogram, and what the system lets them do.</summary>
    /// <param name="PositionTitles">
    /// The post(s) the persona is bound to, in order of preference. The demo workforce leaves every
    /// sixth post vacant, so a persona on a junior post names a fallback rather than vanishing.
    /// </param>
    private sealed record Persona(string Username, string[] PositionTitles, string Purpose, params string[] Roles)
    {
        public string PositionTitle => string.Join(" / ", PositionTitles);
    }

    private static string[] Post(params string[] titles) => titles;

    // The cast. Shared platform roles use Constants.Roles so a rename cannot strand a persona with
    // a role that no longer exists. Module-specific roles still use their seeded display names.
    // Employee is on all of them: the self-service portal is gated on it, and even the Managing
    // Director has a payslip.
    /// <summary>
    /// Roles a persona used to carry and must no longer hold. The role loop below only ADDS, so a
    /// persona re-cast on an existing demo database keeps its old roles unless they are listed
    /// here. she.officer carried HR until DR-10 gave the safety desk its own role (2026-09-03).
    /// </summary>
    private static readonly IReadOnlyDictionary<string, string[]> RetiredRoles =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["she.officer"] = new[] { Constants.Roles.Hr },
        };

    private static readonly Persona[] Cast =
    {
        new("hr.head", Post("Head of HR & Administration"),
            "Drives every HR register and admin screen; also a line manager and an employee",
            Constants.Roles.Hr, Constants.Roles.Manager, Constants.Roles.Employee),

        // DR-10 (2026-09-03): the safety function has its own two roles and no HR role. The desk
        // officer sits on a junior SHE post and WORKS every register; the HSE Supervisor — the
        // head of the SHE Section — holds SHE Manager and is the one who CONFIGURES it (catalogues,
        // checklists, PPE requirements, the reminder engine) and may delete. hr.head can read all
        // of SHE and edit none of it. she.officer is listed before she.manager so that a database
        // seeded before this date re-links she.officer off the HSE Supervisor first.
        new("she.officer", Post("Environmental Officer", "HSE Assistant"),
            "Works the whole SHE desk — incidents, permits, PPE issue, inspections, environmental — but cannot configure it",
            Constants.Roles.SafetyOfficer, Constants.Roles.Employee),
        new("she.manager", Post("HSE Supervisor"),
            "Head of the SHE Section: everything the officer does, plus the Safety (SHE) settings tree and deletion",
            Constants.Roles.SheManager, Constants.Roles.Employee),

        // Not "md": the login request validates usernames at 3-100 characters, and a two-letter
        // persona fails before it reaches the password check. Not "managing.director" either: the
        // base seeders already create that account, unlinked and with their own password.
        new("md.tdc", Post("Managing Director"),
            "Signs separations (FR-HR-092), top of every approval chain, sees the executive view",
            Constants.Roles.ManagingDirector, Constants.Roles.Manager, Constants.Roles.Employee),

        new("gm.ops", Post("General Manager - Operations"),
            "Directorate-level approver above Development, Estates and Development Control",
            Constants.Roles.Manager, Constants.Roles.Employee),

        new("head.estate", Post("Head of Estates"),
            "Owns Estate approvals, property oversight, and Estate-side workflow closeout",
            "Head of Estate", "Estate Manager", "Property Manager", Constants.Roles.Manager, Constants.Roles.Employee),

        new("property.manager", Post("Estates Manager - Housing", "Estates Manager - Lands"),
            "Manages property listing applications, agreement checks, occupancy, and Estate balance decisions",
            "Property Manager", "Estate Manager", Constants.Roles.Manager, Constants.Roles.Employee),

        new("property.officer", Post("Estates Officer - Housing", "Estates Officer - Lands"),
            "Works property listing applications, availability checks, and Estate balance follow-up",
            "Property Officer", "Estate Officer", Constants.Roles.Employee),

        new("records.officer", Post("Records Officer"),
            "Handles Estate records, Central DMS indexing, dispatch, and property record closeout",
            "Records Officer", "Document Control Officer", "Estate Officer", Constants.Roles.Employee),

        new("authorised.signatory", Post("Head of Estates"),
            "Signs approved Estate agreements and controlled DMS documents",
            "Authorised Signatory", "Executive Approver", Constants.Roles.Manager, Constants.Roles.Employee),

        new("sales.manager", Post("Head of Sales & Marketing", "Sales & Marketing Officer"),
            "Owns the Sales side of public property enquiries and completes Estate handoffs",
            "Sales User", "Sales Manager", "Marketing User", Constants.Roles.Manager, Constants.Roles.Employee),

        new("sales.officer", Post("Sales & Marketing Officer", "Sales & Marketing Assistant"),
            "Works public property enquiries, follows up with customers, and prepares closed-won handoffs",
            "Sales User", "Sales Officer", "Marketing User", Constants.Roles.Employee),

        new("marketing.officer", Post("Sales & Marketing Assistant", "Sales & Marketing Officer"),
            "Receives marketing-side property enquiry notifications and supports customer follow-up",
            "Marketing User", "Sales User", Constants.Roles.Employee),

        new("head.dev", Post("Head of Development"),
            "A working line manager: approvals, team appraisals, direct reports, nominations",
            Constants.Roles.Manager, Constants.Roles.Employee),

        new("staff", Post("Project Coordinator"),
            "An ordinary employee under head.dev — raises leave, claims, requests; the self-service view",
            Constants.Roles.Employee),

        new("new.hire", Post("Supervising Architect"),
            "Hired within the last few months — probation, onboarding and orientation from their side",
            Constants.Roles.Employee),

        new("auditor", Post("Chief Internal Auditor"),
            "Reviews separation settlements (FR-HR-185); reports to the Board, not to management",
            Constants.Roles.InternalAudit, Constants.Roles.Employee),
    };

    public async Task SeedAsync(CancellationToken ct = default)
    {
        var tenant = await _context.Tenants.FirstOrDefaultAsync(t => t.Code == "DEFAULT", ct);
        if (tenant is null)
        {
            _logger.LogError("DEFAULT tenant not found — cannot seed demo personas.");
            return;
        }

        var tenantId = tenant.Id;

        // Every role the cast needs, created if absent. TDC_INTERNAL_AUDIT is the known gap: the
        // reference database has never had it, which is why a separation settlement review has had
        // no possible reviewer on any demo so far.
        foreach (var roleName in Cast.SelectMany(p => p.Roles).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            await EnsureRoleAsync(roleName);
        }

        var created = 0;
        foreach (var persona in Cast)
        {
            // First staffed post in the persona's order of preference.
            Employee? employee = null;
            foreach (var title in persona.PositionTitles)
            {
                employee = await _context.Employees
                    .IgnoreQueryFilters()
                    .Where(e => e.TenantId == tenantId && !e.IsDeleted
                             && e.EmployeeNumber.StartsWith("TDC/")
                             && _context.Set<EmployeePosition>().Any(p => p.Id == e.PositionId && p.Title == title))
                    .OrderBy(e => e.EmployeeNumber)
                    .FirstOrDefaultAsync(ct);
                if (employee is not null) break;
            }

            if (employee is null)
            {
                // Not fatal: the other personas are still worth having. But it is loud, because a
                // missing persona is a missing chapter of the demo.
                _logger.LogWarning(
                    "Persona '{Username}' skipped — no seeded employee holds the position \"{Title}\". "
                    + "Has the establishment changed, or is that post one of the deliberate vacancies?",
                    persona.Username, persona.PositionTitle);
                continue;
            }

            var user = await _userManager.FindByNameAsync(persona.Username);
            if (user is null)
            {
                user = new ApplicationUser
                {
                    UserName = persona.Username,
                    // Identity requires a unique email, and multiple demo personas can map to one senior post.
                    Email = $"{persona.Username}@demo.tdc.local",
                    EmailConfirmed = true,
                    FirstName = employee.FirstName,
                    LastName = employee.LastName,
                    TenantId = tenantId,
                    AuthenticationProvider = AuthenticationProvider.Local,
                    IsActive = true,
                    MustChangePassword = false,
                    EmployeeId = employee.Id,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = By,
                    PhoneNumberConfirmed = false
                };

                var result = await _userManager.CreateAsync(user, Password);
                if (!result.Succeeded)
                {
                    _logger.LogError("Could not create persona '{Username}': {Errors}",
                        persona.Username, string.Join("; ", result.Errors.Select(e => e.Description)));
                    continue;
                }

                created++;
            }
            else if (user.EmployeeId != employee.Id)
            {
                user.EmployeeId = employee.Id;
                user.FirstName = employee.FirstName;
                user.LastName = employee.LastName;
                user.UpdatedAt = DateTime.UtcNow;
                user.UpdatedBy = By;
                await _userManager.UpdateAsync(user);
            }

            await EnsureTenantAccessAsync(user.Id, tenantId, ct);

            foreach (var roleName in persona.Roles)
            {
                if (await _userManager.IsInRoleAsync(user, roleName)) continue;

                var roleResult = await _userManager.AddToRoleAsync(user, roleName);
                if (!roleResult.Succeeded)
                {
                    _logger.LogError("Could not add '{Username}' to role '{Role}': {Errors}",
                        persona.Username, roleName, string.Join("; ", roleResult.Errors.Select(e => e.Description)));
                }
            }

            if (RetiredRoles.TryGetValue(persona.Username, out var retired))
            {
                foreach (var roleName in retired)
                {
                    if (!await _userManager.IsInRoleAsync(user, roleName)) continue;
                    var removal = await _userManager.RemoveFromRoleAsync(user, roleName);
                    if (removal.Succeeded)
                        _logger.LogInformation("Persona {Username} no longer carries the retired role '{Role}'", persona.Username, roleName);
                    else
                        _logger.LogError("Could not remove retired role '{Role}' from '{Username}': {Errors}",
                            roleName, persona.Username, string.Join("; ", removal.Errors.Select(e => e.Description)));
                }
            }

            _logger.LogInformation(
                "Persona {Username,-12} = {Number} {Name,-24} {Title,-34} [{Roles}]",
                persona.Username, employee.EmployeeNumber, $"{employee.FirstName} {employee.LastName}",
                persona.PositionTitle, string.Join(", ", persona.Roles));
        }

        await _context.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Demo personas ready: {Created} created, {Total} in the cast. Password for all of them: {Password}",
            created, Cast.Length, Password);
    }

    private async Task EnsureRoleAsync(string roleName)
    {
        if (await _roleManager.RoleExistsAsync(roleName)) return;

        var result = await _roleManager.CreateAsync(new ApplicationRole
        {
            Name = roleName,
            NormalizedName = roleName.ToUpperInvariant(),
            Description = "Created by the demo persona seeder",
            IsSystemRole = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = By
        });

        if (result.Succeeded)
        {
            _logger.LogInformation("Created missing role '{Role}'.", roleName);
        }
        else
        {
            _logger.LogError("Could not create role '{Role}': {Errors}",
                roleName, string.Join("; ", result.Errors.Select(e => e.Description)));
        }
    }

    /// <summary>
    /// A user with no <see cref="UserTenant"/> row can authenticate and then reach nothing: the
    /// token carries no tenant, and every tenant-scoped query returns empty. This is the same shape
    /// <c>DatabaseSeedingService.EnsureTestUserTenantAccessAsync</c> writes.
    /// </summary>
    private async Task EnsureTenantAccessAsync(Guid userId, Guid tenantId, CancellationToken ct)
    {
        var existing = await _context.UserTenants
            .FirstOrDefaultAsync(ut => ut.UserId == userId && ut.TenantId == tenantId && !ut.IsDeleted, ct);

        if (existing is not null)
        {
            existing.Status = UserTenantStatus.Active;
            existing.IsDefault = true;
            return;
        }

        _context.UserTenants.Add(new UserTenant
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TenantId = tenantId,
            AccessLevel = UserTenantAccessLevel.Standard,
            Status = UserTenantStatus.Active,
            IsDefault = true,
            GrantedAt = DateTime.UtcNow,
            GrantedBy = By,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = By
        });
    }
}
