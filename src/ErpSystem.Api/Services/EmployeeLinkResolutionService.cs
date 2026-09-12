using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Data;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services;

/// <summary>
/// Area 25 (employee self-service): the one place the user↔employee matching rules live.
///
/// Three callers share these rules on purpose — the D1 login resolver (employee number as a
/// login identifier), the D5 LDAP auto-link, and the HR unlinked-users queue's suggestions —
/// so what the queue shows HR is exactly what the auto-link would have done, and the harness
/// can prove the matrix through the queue without a live directory.
///
/// Matching is EXACT only. A wrong link is an identity breach (one person reading another's
/// payslips), so ambiguity always resolves to "no link": zero or multiple candidates leaves
/// the user unlinked for a human to decide.
/// </summary>
public interface IEmployeeLinkResolutionService
{
    /// <summary>
    /// D1: treats a login identifier as an employee number and resolves it to the LINKED
    /// user's id. An unlinked employee's number resolves to nothing (there is no user to
    /// authenticate); so does a number linked ambiguously.
    /// </summary>
    Task<Guid?> ResolveUserIdByEmployeeNumberAsync(string identifier, Guid? tenantId, CancellationToken ct = default);

    /// <summary>
    /// D5: the auto-link target for a just-provisioned LDAP user. Email exact-one wins,
    /// else employee-number exact-one; anything else is null.
    /// </summary>
    Task<LinkMatch?> FindExactMatchAsync(Guid tenantId, string? email, string? employeeNumber, CancellationToken ct = default);

    /// <summary>
    /// The queue: every exact-rule candidate for one unlinked user, ambiguous ones included
    /// (flagged), so HR sees why the auto-link held back.
    /// </summary>
    Task<IReadOnlyList<LinkSuggestion>> SuggestForUserAsync(ApplicationUser user, CancellationToken ct = default);
}

public sealed record LinkMatch(Guid EmployeeId, string MatchedBy);

public sealed class LinkSuggestion
{
    public Guid EmployeeId { get; init; }
    public string EmployeeName { get; init; } = string.Empty;
    public string EmployeeNumber { get; init; } = string.Empty;
    public string? EmployeeEmail { get; init; }
    /// <summary>"Email" or "EmployeeNumber" — which exact rule produced this candidate.</summary>
    public string MatchedBy { get; init; } = string.Empty;
    /// <summary>False when the rule matched more than one employee — auto-link would refuse.</summary>
    public bool IsExact { get; init; }
}

public sealed class EmployeeLinkResolutionService : IEmployeeLinkResolutionService
{
    private readonly ApplicationDbContext _context;

    public EmployeeLinkResolutionService(ApplicationDbContext context) => _context = context;

    // The login path runs anonymously, where the model's tenant filter may or may not be
    // compiled in — so every query here ignores the global filters and states its own
    // tenant + soft-delete predicates explicitly.
    private IQueryable<Employee> LiveEmployees(Guid? tenantId)
    {
        var q = _context.Employees.IgnoreQueryFilters().Where(e => !e.IsDeleted);
        return tenantId.HasValue ? q.Where(e => e.TenantId == tenantId.Value) : q;
    }

    public async Task<Guid?> ResolveUserIdByEmployeeNumberAsync(string identifier, Guid? tenantId, CancellationToken ct = default)
    {
        var number = identifier?.Trim();
        if (string.IsNullOrEmpty(number)) return null;

        var linkedUserIds = await LiveEmployees(tenantId)
            .Where(e => e.EmployeeNumber == number)
            .Join(_context.Users, e => e.Id, u => u.EmployeeId, (e, u) => u.Id)
            .Take(2)
            .ToListAsync(ct);

        return linkedUserIds.Count == 1 ? linkedUserIds[0] : null;
    }

    public async Task<LinkMatch?> FindExactMatchAsync(Guid tenantId, string? email, string? employeeNumber, CancellationToken ct = default)
    {
        var byEmail = await UnlinkedCandidatesAsync(tenantId, email, byEmail: true, ct);
        if (byEmail.Count == 1) return new LinkMatch(byEmail[0].Id, "Email");

        var byNumber = await UnlinkedCandidatesAsync(tenantId, employeeNumber, byEmail: false, ct);
        if (byNumber.Count == 1) return new LinkMatch(byNumber[0].Id, "EmployeeNumber");

        return null;
    }

    public async Task<IReadOnlyList<LinkSuggestion>> SuggestForUserAsync(ApplicationUser user, CancellationToken ct = default)
    {
        var byEmail = await UnlinkedCandidatesAsync(user.TenantId, user.Email, byEmail: true, ct);
        var byNumber = await UnlinkedCandidatesAsync(user.TenantId, user.UserName, byEmail: false, ct);

        var suggestions = new List<LinkSuggestion>();
        suggestions.AddRange(byEmail.Select(e => ToSuggestion(e, "Email", byEmail.Count == 1)));
        suggestions.AddRange(byNumber
            .Where(e => byEmail.All(m => m.Id != e.Id))
            .Select(e => ToSuggestion(e, "EmployeeNumber", byNumber.Count == 1)));
        return suggestions;
    }

    /// <summary>Active, UNLINKED employees matching one exact rule. Capped — 3 is already "ambiguous".</summary>
    private async Task<List<Employee>> UnlinkedCandidatesAsync(Guid tenantId, string? value, bool byEmail, CancellationToken ct)
    {
        var needle = value?.Trim();
        if (string.IsNullOrEmpty(needle) || (byEmail && needle.EndsWith("@ldap.local", StringComparison.OrdinalIgnoreCase)))
            return new List<Employee>();

        var q = LiveEmployees(tenantId)
            .Where(e => !_context.Users.Any(u => u.EmployeeId == e.Id));
        q = byEmail
            ? q.Where(e => e.EmailAddress != null && e.EmailAddress == needle)
            : q.Where(e => e.EmployeeNumber == needle);

        return await q.Take(3).ToListAsync(ct);
    }

    private static LinkSuggestion ToSuggestion(Employee e, string matchedBy, bool isExact) => new()
    {
        EmployeeId = e.Id,
        EmployeeName = e.FullName,
        EmployeeNumber = e.EmployeeNumber,
        EmployeeEmail = e.EmailAddress,
        MatchedBy = matchedBy,
        IsExact = isExact,
    };
}
