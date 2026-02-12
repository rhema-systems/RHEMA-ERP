using ErpSystem.Core.Entities.Ehc;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Interfaces.Ehc;

public interface IEhcTicketRepository
{
    IQueryable<EhcTicket> Query();
    Task<EhcTicket?> GetByIdWithDetailsAsync(Guid id, Guid tenantId, CancellationToken cancellationToken = default);
    Task<EhcTicket?> GetByIdForRequesterAsync(Guid id, Guid tenantId, Guid requesterUserId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<EhcTicket>> GetForRequesterAsync(
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
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<EhcTicket>> GetPagedAsync(Guid tenantId, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<EhcTicket>> GetPagedByStatusAsync(Guid tenantId, int page, int pageSize, string status, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<EhcTicket>> GetPagedFilteredAsync(
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
        CancellationToken cancellationToken = default);
    Task<string> GenerateTicketNumberAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task AddAsync(EhcTicket ticket, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
