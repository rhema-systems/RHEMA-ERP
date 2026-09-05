using ErpSystem.Core.Entities.HR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Data.Seeders;

/// <summary>
/// Gives the organisation an authority hierarchy: a head on every organisation unit
/// (<c>OrganizationUnits.HeadEmployeeId</c>) and a line manager on every employee
/// (<c>Employees.ManagerId</c>).
/// </summary>
/// <remarks>
/// <para><b>⚠ This is TEST data, and it is a separate command for that reason.</b> It is deliberately
/// NOT part of <c>seed-hr-all</c>, which seeds TDC's real organisation structure and locations. Who
/// heads which unit is organisational fact of the same kind, and TDC has not supplied it — so
/// inventing it inside the command that seeds their real structure would produce fabricated
/// management lines that look authoritative. These are not cosmetic: reporting lines decide who may
/// discipline whom (FR-HR-080) and who answers a grievance (FR-HR-181, FR-HR-084).</para>
///
/// <para><b>It never overwrites.</b> A unit that already names a head keeps it; an employee who
/// already names a manager keeps them. Run against a tenant whose real hierarchy has been entered,
/// this seeder does nothing at all — which is what makes it safe to leave in the solution.</para>
///
/// <para><b>What it builds, and why that shape.</b> Measured on the DEFAULT tenant 2026-08-31: the
/// 41 units are genuine TDC structure, properly parented from the Board of Directors down — but
/// <b>1,046 of the 1,084 employees sit in a single unit, "Financial Accounts"</b>, with 14 in
/// "General" and 24 in none. Employee <i>placement</i> is an artefact of the data port, not real.
/// This seeder therefore does <b>not</b> redistribute anybody: employee master data is a separate
/// question, and moving a thousand people between departments to make a hierarchy look plausible
/// would be inventing far more than it fixed.</para>
///
/// <para>So: every unit gets a head, preferring somebody already placed in that unit and otherwise
/// borrowing from the general population; every employee's manager becomes their own unit's head;
/// a head reports to the head of the parent unit; and the root unit's head reports to nobody. The
/// one realistic branch is Financial Accounts, where a head genuinely has subordinates.</para>
///
/// <para><b>⚠ Cycles are refused, and that guard is not decorative.</b> The first run produced a
/// 4-cycle: three links written here closed a loop through a PRE-EXISTING row (the MD's Office head
/// reporting to the Management Accounts head) that never-overwrite had correctly preserved. So
/// "coherent by construction" was false — never-overwrite and acyclicity are separate properties,
/// and only the first was implemented. Every assignment is now checked against the live manager
/// graph, including links this seeder does not own, and refused if it would close a loop. An
/// employee left without a manager for that reason is logged by number.</para>
///
/// <para><b>Deterministic.</b> Employees are ordered by employee number and dealt out to units in
/// name order, so a re-run against a wiped database produces the same assignment and a harness can
/// rely on it.</para>
/// </remarks>
public class HrOrgAuthoritySeeder
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<HrOrgAuthoritySeeder> _logger;

    public HrOrgAuthoritySeeder(ApplicationDbContext context, ILogger<HrOrgAuthoritySeeder> logger)
    {
        _context = context;
        _logger = logger;
    }

    // ⚠ There is deliberately NO "already seeded?" probe here, unlike the steps in
    // HrSeedOrchestrator. This seeder makes two passes — heads, then managers — and a probe over the
    // first ("every unit has a head") short-circuits the second, which is how a cycle in the manager
    // graph survived its first correction. Idempotence comes from never overwriting instead, so
    // running this repeatedly is safe and each run reports what it kept.

    public async Task SeedAsync(Guid tenantId, CancellationToken ct = default)
    {
        var units = await _context.Set<OrganizationUnit>()
            .Where(u => u.TenantId == tenantId && !u.IsDeleted)
            .OrderBy(u => u.Name)
            .ToListAsync(ct);

        var employees = await _context.Set<Employee>()
            .Where(e => e.TenantId == tenantId && !e.IsDeleted)
            .OrderBy(e => e.EmployeeNumber)
            .ToListAsync(ct);

        if (units.Count == 0 || employees.Count == 0)
        {
            _logger.LogWarning(
                "Nothing to do — {Units} unit(s) and {Employees} employee(s). Run 'seed-hr-all' first.",
                units.Count, employees.Count);
            return;
        }

        // ── 1. a head for every unit ──────────────────────────────────────────
        // Prefer somebody already in the unit; that is the only assignment that is true rather than
        // merely coherent, and on this data it applies to Financial Accounts and General alone.
        var byUnit = employees
            .Where(e => e.OrganizationUnitId != null)
            .GroupBy(e => e.OrganizationUnitId!.Value)
            .ToDictionary(g => g.Key, g => g.ToList());

        var taken = new HashSet<Guid>(units.Where(u => u.HeadEmployeeId != null)
                                           .Select(u => u.HeadEmployeeId!.Value));
        var pool = employees.Where(e => !taken.Contains(e.Id)).ToList();
        var next = 0;

        int headsSet = 0, headsKept = 0, headsFromOwnUnit = 0;
        foreach (var unit in units)
        {
            if (unit.HeadEmployeeId != null) { headsKept++; continue; }

            Employee? head = null;

            if (byUnit.TryGetValue(unit.Id, out var members))
                head = members.FirstOrDefault(m => !taken.Contains(m.Id));

            if (head != null) headsFromOwnUnit++;

            while (head == null && next < pool.Count)
            {
                var candidate = pool[next++];
                if (!taken.Contains(candidate.Id)) head = candidate;
            }

            if (head == null)
            {
                // More units than people. Not a failure: a unit with no head resolves to nobody,
                // which every consumer of this hierarchy already treats as a supported answer.
                _logger.LogWarning("  no unassigned employee left for '{Unit}' — left headless.", unit.Name);
                continue;
            }

            unit.HeadEmployeeId = head.Id;
            unit.UpdatedAt = DateTime.UtcNow;
            unit.UpdatedBy = "seed-hr-org-authority";
            taken.Add(head.Id);
            headsSet++;
        }

        await _context.SaveChangesAsync(ct);
        _logger.LogInformation(
            "  heads: {Set} assigned ({OwnUnit} from the unit's own staff), {Kept} already set.",
            headsSet, headsFromOwnUnit, headsKept);

        // ── 2. a line manager for every employee ──────────────────────────────
        // Re-read the units so the heads just written are visible to the manager pass.
        var headOfUnit = units.Where(u => u.HeadEmployeeId != null)
                              .ToDictionary(u => u.Id, u => u.HeadEmployeeId!.Value);
        var parentOfUnit = units.ToDictionary(u => u.Id, u => u.ParentUnitId);
        var unitOfHead = headOfUnit.ToDictionary(kv => kv.Value, kv => kv.Key);

        // ⚠ The live manager graph, INCLUDING links this seeder does not own. Never-overwrite means
        // pre-existing rows survive, and a link assigned here can close a loop through one of them —
        // which is exactly what happened on the first run: three links written here closed onto a
        // pre-existing MD's-Office-head-reports-to-Management-Accounts-head row, giving a 4-cycle.
        // A cycle in reporting lines is not cosmetic: FR-HR-181's ladder walks it, and so does every
        // management-chain read.
        var managerOf = employees.Where(e => e.ManagerId != null)
                                 .ToDictionary(e => e.Id, e => e.ManagerId!.Value);

        bool WouldCycle(Guid employeeId, Guid candidateManagerId)
        {
            var seen = new HashSet<Guid> { employeeId };
            var cursor = candidateManagerId;
            while (true)
            {
                if (!seen.Add(cursor)) return true;                 // reached somebody already on the path
                if (!managerOf.TryGetValue(cursor, out var next)) return false;  // chain ends: safe
                cursor = next;
            }
        }

        int mgrSet = 0, mgrKept = 0, mgrNone = 0, mgrCycles = 0;
        foreach (var employee in employees)
        {
            if (employee.ManagerId != null) { mgrKept++; continue; }
            if (employee.OrganizationUnitId is not Guid unitId) { mgrNone++; continue; }

            Guid? managerId;

            if (unitOfHead.TryGetValue(employee.Id, out var headsThisUnit))
            {
                // ⚠ A head does not report to themselves. They report to the head of the parent
                // unit, walking up until one is found — an intermediate unit may be headless.
                managerId = null;
                var parent = parentOfUnit.TryGetValue(headsThisUnit, out var p) ? p : null;
                while (parent is Guid parentId)
                {
                    if (headOfUnit.TryGetValue(parentId, out var parentHead) && parentHead != employee.Id)
                    {
                        managerId = parentHead;
                        break;
                    }
                    parent = parentOfUnit.TryGetValue(parentId, out var gp) ? gp : null;
                }
            }
            else
            {
                managerId = headOfUnit.TryGetValue(unitId, out var h) ? h : null;
            }

            if (managerId is null) { mgrNone++; continue; }

            if (WouldCycle(employee.Id, managerId.Value))
            {
                _logger.LogWarning(
                    "  {Employee} left without a manager: reporting to {Manager} would close a cycle "
                    + "through reporting lines this seeder did not write.",
                    employee.EmployeeNumber, managerId.Value);
                mgrCycles++;
                continue;
            }

            managerOf[employee.Id] = managerId.Value;
            employee.ManagerId = managerId;
            employee.UpdatedAt = DateTime.UtcNow;
            employee.UpdatedBy = "seed-hr-org-authority";
            mgrSet++;
        }

        await _context.SaveChangesAsync(ct);
        _logger.LogInformation(
            "  managers: {Set} assigned, {Kept} already set, {None} left without one " +
            "(no unit, or nobody above them), {Cycles} refused to avoid a cycle.",
            mgrSet, mgrKept, mgrNone, mgrCycles);

        _logger.LogInformation(
            "⚠ This is TEST data. Replace it with TDC's real reporting lines before the module is " +
            "relied on: every row written here carries UpdatedBy = 'seed-hr-org-authority'.");
    }
}
