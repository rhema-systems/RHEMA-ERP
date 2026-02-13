using ErpSystem.Core.DTOs.Ehc;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Interfaces.Ehc;

public interface IEhcTicketService
{
    Task<EhcTicketDetailDto> CreateExternalTicketAsync(CreateEhcTicketRequestDto request, CancellationToken cancellationToken = default);
    Task<EhcTicketDetailDto> CreateInternalTicketAsync(CreateEhcTicketRequestDto request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<EhcTicketListItemDto>> GetMyTicketsAsync(
        int page = 1,
        int pageSize = 25,
        string? q = null,
        EhcTicketStatus? status = null,
        EhcTicketType? ticketType = null,
        EhcTicketPriority? priority = null,
        EhcTicketSource? source = null,
        Guid? categoryId = null,
        DateTime? createdFrom = null,
        DateTime? createdTo = null,
        CancellationToken cancellationToken = default);
    Task<EhcTicketDetailDto?> GetMyTicketByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<EhcTicketMessageDto> AddExternalMessageAsync(Guid ticketId, AddEhcTicketMessageRequestDto request, CancellationToken cancellationToken = default);
    Task<EhcTicketAttachmentDto> AddExternalAttachmentAsync(Guid ticketId, AddEhcTicketAttachmentRequestDto request, CancellationToken cancellationToken = default);

    // Internal operations
    Task<IReadOnlyList<EhcTicketListItemDto>> GetTicketsAsync(
        int page = 1,
        int pageSize = 25,
        EhcTicketStatus? status = null,
        EhcTicketType? ticketType = null,
        EhcTicketPriority? priority = null,
        EhcTicketSource? source = null,
        Guid? categoryId = null,
        Guid? assignedDepartmentId = null,
        DateTime? createdFrom = null,
        DateTime? createdTo = null,
        CancellationToken cancellationToken = default);
    Task<EhcTicketDetailDto?> GetTicketByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task AssignTicketAsync(Guid ticketId, Guid assignedToUserId, Guid? assignedDepartmentId = null, CancellationToken cancellationToken = default);
    Task TransitionTicketAsync(Guid ticketId, EhcTicketStatus targetStatus, string? notes = null, Guid? workflowTransitionId = null, string? workflowTransitionName = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<EhcTicketAllowedTransitionDto>> GetAllowedTransitionsAsync(Guid ticketId, CancellationToken cancellationToken = default);
    Task<EhcTicketDetailDto> UpdateTicketRcaAsync(Guid ticketId, UpdateEhcTicketRcaRequestDto request, CancellationToken cancellationToken = default);
    Task<EhcTicketMessageDto> AddInternalCommentAsync(Guid ticketId, AddEhcTicketMessageRequestDto request, CancellationToken cancellationToken = default);
    Task<EhcTicketMessageDto> AddAgentMessageAsync(Guid ticketId, AddEhcTicketMessageRequestDto request, CancellationToken cancellationToken = default);
    Task<EhcTicketAttachmentDto> AddInternalAttachmentAsync(Guid ticketId, AddEhcTicketAttachmentRequestDto request, CancellationToken cancellationToken = default);
}
