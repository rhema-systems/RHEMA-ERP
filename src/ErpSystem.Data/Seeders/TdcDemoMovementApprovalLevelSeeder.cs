using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.HR.PromotionTransfer;
using ErpSystem.Core.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Data.Seeders;

/// <summary>
/// Seeds the per-movement approval ladder for the <b>DEFAULT tenant</b>'s demonstration data.
///
/// <para><b>Why a seeder and not a scenario.</b> <c>StaffMovementApprovalLevel</c> is the one
/// movement table with no create door anywhere in the API: <c>StaffMovementsController</c> exposes
/// three GETs over it (<c>approval-levels</c>, <c>approval-levels/current-pending</c>,
/// <c>approval-levels/all-approved</c>) and <c>IStaffMovementService</c> declares only the two read
/// methods. <c>CreateStaffMovementApprovalLevelDto</c> and its <c>ToEntity</c> mapper exist and are
/// unreachable — nothing constructs one. Submitting a movement starts a workflow instance instead,
/// and the engine keeps its own steps, so the ladder table is never written by the running system.
/// Until a writer exists, the three screens that read it open empty.</para>
///
/// <para>⚠ <b>Recorded as a defect, not papered over.</b> The rows below make the ladder screens
/// demonstrable; they do not make the ladder operative. Nothing consumes these rows to decide who
/// may approve — the workflow definition still does that — so the two must be kept in step by hand
/// until the module owner decides which of them is the authority.</para>
///
/// <para>The ladder mirrors what the workflow definition actually routes: the receiving head of
/// department, then HR, then the Managing Director. Levels already cleared on a movement that has
/// been through them are recorded as Approved with the date the movement moved; the level the
/// movement is sitting at is Pending.</para>
///
/// Idempotent: skips entirely when any live approval level already exists for the tenant.
/// </summary>
public class TdcDemoMovementApprovalLevelSeeder
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<TdcDemoMovementApprovalLevelSeeder> _logger;

    private const string By = "TdcDemoMovementApprovalLevelSeeder";

    public TdcDemoMovementApprovalLevelSeeder(
        ApplicationDbContext context, ILogger<TdcDemoMovementApprovalLevelSeeder> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task SeedAsync(CancellationToken ct = default)
    {
        var tenant = await _context.Set<Tenant>().FirstOrDefaultAsync(t => t.Code == "DEFAULT", ct);
        if (tenant is null)
        {
            _logger.LogError("DEFAULT tenant not found — cannot seed staff-movement approval levels.");
            return;
        }

        var tenantId = tenant.Id;

        if (await _context.Set<StaffMovementApprovalLevel>().IgnoreQueryFilters()
                .AnyAsync(l => l.TenantId == tenantId && !l.IsDeleted, ct))
        {
            _logger.LogInformation("Staff-movement approval levels already present for the DEFAULT tenant. Skipping.");
            return;
        }

        var movements = await _context.Set<StaffMovement>().IgnoreQueryFilters()
            .Where(m => m.TenantId == tenantId && !m.IsDeleted)
            .OrderBy(m => m.MovementNumber)
            .ToListAsync(ct);

        if (movements.Count == 0)
        {
            _logger.LogWarning(
                "No staff movements for the DEFAULT tenant — run the movements scenario before this seeder.");
            return;
        }

        // Approvers are resolved by employee number, never by hardcoded GUID: TDC/00006 is the Head
        // of Development, TDC/00009 the Head of HR & Administration, TDC/00001 the Managing Director.
        var approvers = await _context.Set<Employee>().IgnoreQueryFilters()
            .Where(e => e.TenantId == tenantId && !e.IsDeleted
                     && (e.EmployeeNumber == "TDC/00006"
                      || e.EmployeeNumber == "TDC/00009"
                      || e.EmployeeNumber == "TDC/00001"))
            .ToDictionaryAsync(e => e.EmployeeNumber, e => e.Id, ct);

        if (approvers.Count < 3)
        {
            _logger.LogWarning(
                "Expected TDC/00001, TDC/00006 and TDC/00009 for the approval ladder; found {Count}. Skipping.",
                approvers.Count);
            return;
        }

        var ladder = new (int Level, string RoleName, string EmployeeNumber)[]
        {
            (1, "Receiving Head of Department", "TDC/00006"),
            (2, "Head of HR & Administration",  "TDC/00009"),
            (3, "Managing Director",            "TDC/00001"),
        };

        var now = DateTime.UtcNow;
        var added = 0;

        foreach (var movement in movements)
        {
            // A movement still in Draft has not been asked of anybody, so its ladder is entirely
            // pending. One that has been submitted has cleared the first rung.
            var clearedThrough = movement.Status switch
            {
                StaffMovementStatus.Draft => 0,
                StaffMovementStatus.Submitted => 1,
                StaffMovementStatus.Approved => 3,
                StaffMovementStatus.Implemented => 3,
                StaffMovementStatus.Rejected => 1,
                StaffMovementStatus.Cancelled => 0,
                _ => 1,
            };

            foreach (var (level, roleName, employeeNumber) in ladder)
            {
                var cleared = level <= clearedThrough;

                _context.Set<StaffMovementApprovalLevel>().Add(new StaffMovementApprovalLevel
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    MovementId = movement.Id,
                    Level = level,
                    RoleName = roleName,
                    ApproverId = approvers[employeeNumber],
                    Status = cleared ? ApprovalStatus.Approved : ApprovalStatus.Pending,
                    ActionDate = cleared ? movement.RequestSubmissionDate : (DateTime?)null,
                    Comments = cleared
                        ? "Recommended. The receiving section has the vacancy and the establishment allows it."
                        : null,
                    CreatedAt = now,
                    CreatedBy = By,
                });

                added++;
            }
        }

        await _context.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Seeded {Added} staff-movement approval levels across {Movements} movements. ⚠ These rows "
            + "are read by the ladder screens but are NOT what the approval engine routes on.",
            added, movements.Count);
    }
}
