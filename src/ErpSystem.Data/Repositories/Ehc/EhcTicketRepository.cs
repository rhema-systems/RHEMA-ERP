using System.Text.RegularExpressions;
using ErpSystem.Core.Entities.Ehc;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces.Ehc;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Data.Repositories.Ehc;

public sealed class EhcTicketRepository : IEhcTicketRepository
{
    private static readonly Regex TicketNumberRegex = new("^EHC-(?<yy>\\d{2})-(?<seq>\\d{6})$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private readonly ApplicationDbContext _db;

    public EhcTicketRepository(ApplicationDbContext db)
    {
        _db = db;
    }

    public IQueryable<EhcTicket> Query()
    {
        return _db.EhcTickets.AsQueryable();
    }

    public async Task<EhcTicket?> GetByIdWithDetailsAsync(Guid id, Guid tenantId, CancellationToken cancellationToken = default)
    {
        return await _db.EhcTickets
            .AsNoTracking()
            .Include(t => t.Category)
            .Include(t => t.Subcategory)
            .Include(t => t.RootCause)
            .Include(t => t.RequesterUser)
            .Include(t => t.AssignedToUser)
            .Include(t => t.AssignedDepartment)
            .Include(t => t.Attachments)
            .Include(t => t.Messages)
                .ThenInclude(m => m.AuthorUser)
            .Include(t => t.Messages)
                .ThenInclude(m => m.Attachments)
            .Include(t => t.StatusHistory)
                .ThenInclude(h => h.ChangedByUser)
            .Include(t => t.AuditEvents)
                .ThenInclude(a => a.ActorUser)
            .FirstOrDefaultAsync(t => t.Id == id && t.TenantId == tenantId && !t.IsDeleted, cancellationToken);
    }

    public async Task<EhcTicket?> GetByIdForRequesterAsync(Guid id, Guid tenantId, Guid requesterUserId, CancellationToken cancellationToken = default)
    {
        return await _db.EhcTickets
            .AsNoTracking()
            .Include(t => t.Category)
            .Include(t => t.Subcategory)
            .Include(t => t.RootCause)
            .Include(t => t.Attachments)
            .Include(t => t.Messages)
                .ThenInclude(m => m.AuthorUser)
            .Include(t => t.Messages)
                .ThenInclude(m => m.Attachments)
            .Include(t => t.StatusHistory)
                .ThenInclude(h => h.ChangedByUser)
            .Include(t => t.AuditEvents)
                .ThenInclude(a => a.ActorUser)
            .FirstOrDefaultAsync(t =>
                    t.Id == id &&
                    t.TenantId == tenantId &&
                    t.RequesterUserId == requesterUserId &&
                    !t.IsDeleted,
                cancellationToken);
    }

    public async Task<IReadOnlyList<EhcTicket>> GetForRequesterAsync(
        Guid tenantId,
        Guid requesterUserId,
        int page,
        int pageSize,
        string? q = null,
        EhcTicketStatus? status = null,
        EhcTicketType? ticketType = null,
        EhcTicketPriority? priority = null,
        EhcTicketSource? source = null,
        Guid? categoryId = null,
        DateTime? createdFrom = null,
        DateTime? createdTo = null,
        CancellationToken cancellationToken = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 25;
        if (pageSize > 100) pageSize = 100;

        var query = _db.EhcTickets
            .AsNoTracking()
            .Include(t => t.Category)
            .Where(t => t.TenantId == tenantId && t.RequesterUserId == requesterUserId && !t.IsDeleted);

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(t =>
                (t.TicketNumber != null && t.TicketNumber.Contains(term)) ||
                (t.Subject != null && t.Subject.Contains(term)));
        }

        if (status.HasValue)
        {
            query = query.Where(t => t.Status == status.Value);
        }

        if (ticketType.HasValue)
        {
            query = query.Where(t => t.TicketType == ticketType.Value);
        }

        if (priority.HasValue)
        {
            query = query.Where(t => t.Priority == priority.Value);
        }

        if (source.HasValue)
        {
            query = query.Where(t => t.Source == source.Value);
        }

        if (categoryId.HasValue && categoryId.Value != Guid.Empty)
        {
            var cid = categoryId.Value;
            query = query.Where(t => t.CategoryId == cid || t.SubcategoryId == cid);
        }

        if (createdFrom.HasValue)
        {
            var from = createdFrom.Value.Date;
            query = query.Where(t => t.CreatedAt >= from);
        }

        if (createdTo.HasValue)
        {
            var toExclusive = createdTo.Value.Date.AddDays(1);
            query = query.Where(t => t.CreatedAt < toExclusive);
        }

        return await query
            .OrderByDescending(t => t.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<EhcTicket>> GetPagedAsync(Guid tenantId, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 25;
        if (pageSize > 100) pageSize = 100;

        return await _db.EhcTickets
            .AsNoTracking()
            .Include(t => t.Category)
            .Include(t => t.RequesterUser)
            .Include(t => t.AssignedToUser)
            .Include(t => t.AssignedDepartment)
            .Where(t => t.TenantId == tenantId && !t.IsDeleted)
            .OrderByDescending(t => t.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<EhcTicket>> GetPagedByStatusAsync(Guid tenantId, int page, int pageSize, string status, CancellationToken cancellationToken = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 25;
        if (pageSize > 100) pageSize = 100;

        return await _db.EhcTickets
            .AsNoTracking()
            .Include(t => t.Category)
            .Include(t => t.RequesterUser)
            .Include(t => t.AssignedToUser)
            .Include(t => t.AssignedDepartment)
            .Where(t => t.TenantId == tenantId && t.Status.ToString() == status && !t.IsDeleted)
            .OrderByDescending(t => t.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<EhcTicket>> GetPagedFilteredAsync(
        Guid tenantId,
        int page,
        int pageSize,
        EhcTicketStatus? status = null,
        EhcTicketType? ticketType = null,
        EhcTicketPriority? priority = null,
        EhcTicketSource? source = null,
        Guid? categoryId = null,
        Guid? assignedDepartmentId = null,
        DateTime? createdFrom = null,
        DateTime? createdTo = null,
        CancellationToken cancellationToken = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 25;
        if (pageSize > 1000) pageSize = 1000;

        var q = _db.EhcTickets
            .AsNoTracking()
            .Include(t => t.Category)
            .Include(t => t.RequesterUser)
            .Include(t => t.AssignedToUser)
            .Include(t => t.AssignedDepartment)
            .Where(t => t.TenantId == tenantId && !t.IsDeleted);

        if (status.HasValue)
        {
            q = q.Where(t => t.Status == status.Value);
        }

        if (ticketType.HasValue)
        {
            q = q.Where(t => t.TicketType == ticketType.Value);
        }

        if (priority.HasValue)
        {
            q = q.Where(t => t.Priority == priority.Value);
        }

        if (source.HasValue)
        {
            q = q.Where(t => t.Source == source.Value);
        }

        if (categoryId.HasValue && categoryId.Value != Guid.Empty)
        {
            var cid = categoryId.Value;
            q = q.Where(t => t.CategoryId == cid || t.SubcategoryId == cid);
        }

        if (assignedDepartmentId.HasValue && assignedDepartmentId.Value != Guid.Empty)
        {
            q = q.Where(t => t.AssignedDepartmentId == assignedDepartmentId.Value);
        }

        if (createdFrom.HasValue)
        {
            var from = createdFrom.Value.Date;
            q = q.Where(t => t.CreatedAt >= from);
        }

        if (createdTo.HasValue)
        {
            var toExclusive = createdTo.Value.Date.AddDays(1);
            q = q.Where(t => t.CreatedAt < toExclusive);
        }

        return await q
            .OrderByDescending(t => t.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
    }

    public async Task<string> GenerateTicketNumberAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var year = DateTime.UtcNow.Year;
        var yy = (year % 100).ToString("D2");
        var prefix = $"EHC-{yy}-";

        var last = await _db.EhcTickets
            .AsNoTracking()
            .Where(t => t.TenantId == tenantId && !t.IsDeleted && t.TicketNumber.StartsWith(prefix))
            .OrderByDescending(t => t.CreatedAt)
            .Select(t => t.TicketNumber)
            .FirstOrDefaultAsync(cancellationToken);

        var nextSeq = 1;
        if (!string.IsNullOrWhiteSpace(last))
        {
            var m = TicketNumberRegex.Match(last);
            if (m.Success && int.TryParse(m.Groups["seq"].Value, out var lastSeq))
            {
                nextSeq = lastSeq + 1;
            }
        }

        return $"{prefix}{nextSeq:D6}";
    }

    public async Task AddAsync(EhcTicket ticket, CancellationToken cancellationToken = default)
    {
        await _db.EhcTickets.AddAsync(ticket, cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return _db.SaveChangesAsync(cancellationToken);
    }
}
