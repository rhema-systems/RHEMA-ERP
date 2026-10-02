using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Core.Services.HR;

/// <summary>One person in an employee's line of authority, and how they stand to them.</summary>
/// <param name="Relation">"supervisor", or "head of {unit}".</param>
public sealed record HrLinePerson(Guid EmployeeId, string Name, string Relation);

/// <summary>A line authority who can sign in: the person, and the login the workflow engine addresses.</summary>
public sealed record HrLineApprover(Guid EmployeeId, Guid UserId, string Name, string Relation);

/// <summary>
/// An employee's line of authority: their supervisor, and the head of their unit and of every unit
/// above it, nearest first (staff travel final closure, lane 2, decision D-7).
/// </summary>
/// <remarks>
/// <para><b>The rule is TDC's</b> (finish plan lane 7; leave round 5, B1): a supervisor is
/// <c>Employees.ManagerId</c>, a head of department is <c>OrganizationUnits.HeadEmployeeId</c>, and
/// the head of any unit above counts as well — a directorate head covers the departments beneath them.
/// Nobody is their own line authority, and a person who has left (inactive or deleted) is no one's.
/// <c>LeaveService.IsLineAuthorityAsync</c> holds the same rule for leave; this is the shared copy the
/// travel closure plan called for, and the leave service may point at it when its owner next opens it.</para>
///
/// <para><b>Static, over the unit of work, deliberately.</b> Two places must name the same people: the
/// workflow engine's context builder (<c>SimpleWorkflowService</c>, in the API), which addresses the
/// travel request's line-manager stage to them by login, and the travel service, which checks the
/// person deciding it. One rule for who is asked and who may answer.</para>
/// </remarks>
public static class HrLineAuthority
{
    /// <summary>The employee's line authorities, supervisor first, then unit heads from the nearest up.</summary>
    /// <remarks>One person appears once, under the first relation found. An empty list means the employee
    /// has no supervisor and no unit above them has a head other than themselves.</remarks>
    public static async Task<IReadOnlyList<HrLinePerson>> GetLineAsync(
        IUnitOfWork unitOfWork, Guid tenantId, Guid subjectEmployeeId, CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty || subjectEmployeeId == Guid.Empty) return [];

        var subject = await unitOfWork.Repository<Employee>().GetQueryable()
            .Where(e => e.TenantId == tenantId && e.Id == subjectEmployeeId)
            .Select(e => new { e.ManagerId, e.OrganizationUnitId })
            .FirstOrDefaultAsync(cancellationToken);
        if (subject is null) return [];

        var candidates = new List<(Guid EmployeeId, string Relation)>();
        if (subject.ManagerId is Guid managerId && managerId != subjectEmployeeId)
            candidates.Add((managerId, "supervisor"));

        if (subject.OrganizationUnitId is Guid unitId)
        {
            // The tenant's unit tree is a few hundred rows; walking it in memory is one query, where a
            // query per level would be one per unit above. Cycle-guarded, as the audience resolver is.
            var units = (await unitOfWork.Repository<OrganizationUnit>().GetQueryable()
                    .Where(u => u.TenantId == tenantId && !u.IsDeleted)
                    .Select(u => new { u.Id, u.Name, u.ParentUnitId, u.HeadEmployeeId })
                    .ToListAsync(cancellationToken))
                .ToDictionary(u => u.Id);

            var seen = new HashSet<Guid>();
            Guid? current = unitId;
            while (current is Guid id && seen.Add(id) && units.TryGetValue(id, out var unit))
            {
                if (unit.HeadEmployeeId is Guid head && head != subjectEmployeeId)
                    candidates.Add((head, $"head of {unit.Name}"));
                current = unit.ParentUnitId;
            }
        }

        if (candidates.Count == 0) return [];

        var ids = candidates.Select(c => c.EmployeeId).Distinct().ToList();
        var people = await unitOfWork.Repository<Employee>().GetQueryable()
            .Where(e => e.TenantId == tenantId && ids.Contains(e.Id) && !e.IsDeleted && e.IsActive)
            .Select(e => new { e.Id, Name = (e.FirstName + " " + e.LastName).Trim() })
            .ToDictionaryAsync(e => e.Id, e => e.Name, cancellationToken);

        var line = new List<HrLinePerson>();
        foreach (var (employeeId, relation) in candidates)
        {
            if (!people.TryGetValue(employeeId, out var name) || line.Any(p => p.EmployeeId == employeeId)) continue;
            line.Add(new HrLinePerson(employeeId, name, relation));
        }
        return line;
    }

    /// <summary>Whether <paramref name="actorEmployeeId"/> is one of the employee's line authorities.</summary>
    public static async Task<bool> IsLineAuthorityAsync(
        IUnitOfWork unitOfWork, Guid tenantId, Guid subjectEmployeeId, Guid actorEmployeeId,
        CancellationToken cancellationToken = default)
    {
        if (actorEmployeeId == Guid.Empty || actorEmployeeId == subjectEmployeeId) return false;
        var line = await GetLineAsync(unitOfWork, tenantId, subjectEmployeeId, cancellationToken);
        return line.Any(p => p.EmployeeId == actorEmployeeId);
    }

    /// <summary>
    /// The first <paramref name="take"/> line authorities who have an active login in the tenant, nearest
    /// first — the people an approval stage can be addressed to by name.
    /// </summary>
    public static async Task<IReadOnlyList<HrLineApprover>> GetLineApproversAsync(
        IUnitOfWork unitOfWork, IQueryable<ApplicationUser> users, Guid tenantId, Guid subjectEmployeeId,
        int take, CancellationToken cancellationToken = default)
        => await GetLineApproversAsync(
            await GetLineAsync(unitOfWork, tenantId, subjectEmployeeId, cancellationToken),
            users, tenantId, take, cancellationToken);

    /// <summary>The same, for a line already read with <see cref="GetLineAsync"/>.</summary>
    public static async Task<IReadOnlyList<HrLineApprover>> GetLineApproversAsync(
        IReadOnlyList<HrLinePerson> line, IQueryable<ApplicationUser> users, Guid tenantId,
        int take, CancellationToken cancellationToken = default)
    {
        if (line.Count == 0 || take <= 0) return [];

        var ids = line.Select(p => p.EmployeeId).ToList();
        var logins = await users
            .Where(u => u.TenantId == tenantId && u.IsActive && u.EmployeeId != null && ids.Contains(u.EmployeeId.Value))
            .Select(u => new { u.Id, EmployeeId = u.EmployeeId!.Value })
            .ToListAsync(cancellationToken);

        var approvers = new List<HrLineApprover>();
        foreach (var person in line)
        {
            // One login per person is the rule; if an employee somehow holds two, the choice is stable.
            var login = logins.Where(l => l.EmployeeId == person.EmployeeId).OrderBy(l => l.Id).FirstOrDefault();
            if (login is null) continue;
            approvers.Add(new HrLineApprover(person.EmployeeId, login.Id, person.Name, person.Relation));
            if (approvers.Count == take) break;
        }
        return approvers;
    }
}
