// The technician-role rule (round 4, lane O). An employee's CanBeAssignedToMaintenance follows their
// position's IsTechnicianRole, unless HR set it by hand for that person. Kept in its own partial so a
// Maintenance reviewer can read HR's whole footprint on the answer to "is this person a technician?"
// in one file.
//
// Enforced HERE, at save time, because the rule has to hold on every path that puts a person in a
// post — HR's create and edit, a hire from an offer, a staff movement and its reversal, the import,
// the seeders — and a rule each of those paths had to remember to call is how the question came to
// have four disagreeing answers. The inventory cost projection (ApplicationDbContext.InventoryCosts.cs)
// is the precedent for a projection maintained in the save.
using ErpSystem.Core.Entities.HR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace ErpSystem.Data;

public partial class ApplicationDbContext
{
    /// <summary>
    /// Re-establishes the technician-role rule on every employee and every position this save writes.
    /// </summary>
    /// <remarks>
    /// <para>Three steps, in this order:</para>
    /// <list type="number">
    /// <item>A write of <c>CanBeAssignedToMaintenance</c> that did not come from this rule is a choice
    /// made by hand, and is marked so. Another module's decision must not be quietly undone by the
    /// next save, and marking it makes it visible on HR's employee form.</item>
    /// <item>Every employee saved, and not set by hand, takes their position's flag.</item>
    /// <item>Every position saved takes its holders not set by hand with it — in THIS save, so the flag
    /// and the people it governs are never stored out of step.</item>
    /// </list>
    /// <para>⚠ It asks "does the row agree with its post?", not "what changed?". The employee update
    /// path marks every property modified (<c>GenericRepository.UpdateAsync</c> calls
    /// <c>Update()</c>), so a change test would report everything as changed, and on a detached copy
    /// attached by <c>Update()</c> it would report nothing.</para>
    /// </remarks>
    private async Task ApplyTechnicianRoleRuleAsync(bool asynchronous, CancellationToken token)
    {
        var employees = ChangeTracker.Entries<Employee>()
            .Where(entry => (entry.State is EntityState.Added or EntityState.Modified) && !entry.Entity.IsDeleted)
            .ToList();

        // One pass over the tracked positions: the flag each one carries NOW (a position this very
        // save is changing answers with its new value) and the ones this save writes.
        var flags = new Dictionary<Guid, bool>();
        var savedPositions = new List<EmployeePosition>();
        foreach (var entry in ChangeTracker.Entries<EmployeePosition>())
        {
            if (entry.State == EntityState.Deleted) continue;
            flags[entry.Entity.Id] = entry.Entity.IsTechnicianRole;
            if (entry.State == EntityState.Modified && !entry.Entity.IsDeleted) savedPositions.Add(entry.Entity);
        }

        if (employees.Count == 0 && savedPositions.Count == 0) return;

        var untracked = employees
            .Select(entry => entry.Entity.PositionId)
            .Where(id => id != Guid.Empty && !flags.ContainsKey(id))
            .Distinct()
            .ToList();
        if (untracked.Count > 0)
        {
            var query = Set<EmployeePosition>().IgnoreQueryFilters().AsNoTracking()
                .Where(position => untracked.Contains(position.Id))
                .Select(position => new { position.Id, position.IsTechnicianRole });
            var rows = asynchronous ? await query.ToListAsync(token) : query.ToList();
            foreach (var row in rows) flags[row.Id] = row.IsTechnicianRole;
        }

        var changed = false;
        foreach (var entry in employees)
        {
            var employee = entry.Entity;

            // Step 1 — a value written by anything but this rule is somebody's decision.
            if (!employee.MaintenanceAssignmentSetByHand && WasSetOutsideTheRule(entry, flags))
            {
                employee.MaintenanceAssignmentSetByHand = true;
                changed = true;
            }
            if (employee.MaintenanceAssignmentSetByHand) continue;

            // Step 2 — the post decides.
            if (!flags.TryGetValue(employee.PositionId, out var isTechnicianRole)) continue;
            if (employee.CanBeAssignedToMaintenance == isTechnicianRole) continue;
            employee.CanBeAssignedToMaintenance = isTechnicianRole;
            changed = true;
        }

        // Step 3 — the post takes its people with it. Tracked on purpose: they save with the position,
        // and the audit pass that runs next stamps them.
        foreach (var position in savedPositions)
        {
            var flag = position.IsTechnicianRole;
            var positionId = position.Id;
            var query = Set<Employee>().IgnoreQueryFilters()
                .Where(employee => employee.PositionId == positionId
                    && !employee.IsDeleted
                    && !employee.MaintenanceAssignmentSetByHand
                    && employee.CanBeAssignedToMaintenance != flag);
            var holders = asynchronous ? await query.ToListAsync(token) : query.ToList();
            foreach (var holder in holders)
            {
                // Already tracked with unsaved changes of its own? Then it answers to where it is going,
                // which step 2 has already settled.
                if (holder.PositionId != positionId || holder.MaintenanceAssignmentSetByHand) continue;
                holder.CanBeAssignedToMaintenance = flag;
                changed = true;
            }
        }

        if (changed) ChangeTracker.DetectChanges();
    }

    /// <summary>
    /// Whether <c>CanBeAssignedToMaintenance</c> was written by something other than this rule.
    /// </summary>
    /// <remarks>
    /// For a row being updated: the column moved while the "set by hand" marker did not (following
    /// the position moves the MARKER, and leaves the column to this rule). For a new row: it arrives
    /// ticked where its post is not a technician role — the column defaults to false, so a tick is a
    /// choice somebody made.
    /// </remarks>
    private static bool WasSetOutsideTheRule(EntityEntry<Employee> entry, IReadOnlyDictionary<Guid, bool> flags)
    {
        var employee = entry.Entity;
        if (entry.State == EntityState.Added)
        {
            return employee.CanBeAssignedToMaintenance
                && flags.TryGetValue(employee.PositionId, out var isTechnicianRole)
                && !isTechnicianRole;
        }

        var column = entry.Property(e => e.CanBeAssignedToMaintenance);
        var marker = entry.Property(e => e.MaintenanceAssignmentSetByHand);
        return column.OriginalValue != column.CurrentValue && marker.OriginalValue == marker.CurrentValue;
    }
}
