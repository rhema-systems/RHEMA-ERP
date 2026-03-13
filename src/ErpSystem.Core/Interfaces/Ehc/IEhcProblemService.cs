using ErpSystem.Core.DTOs.Ehc;

namespace ErpSystem.Core.Interfaces.Ehc;

public interface IEhcProblemService
{
    Task<IReadOnlyList<EhcProblemListItemDto>> GetProblemsAsync(
        int page = 1,
        int pageSize = 25,
        string? q = null,
        ErpSystem.Core.Enums.EhcProblemStatus? status = null,
        ErpSystem.Core.Enums.EhcTicketPriority? priority = null,
        Guid? departmentId = null,
        Guid? ownerUserId = null,
        DateTime? createdFrom = null,
        DateTime? createdTo = null,
        CancellationToken cancellationToken = default);

    Task<EhcProblemDetailDto?> GetProblemByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<EhcProblemDetailDto> CreateProblemAsync(CreateEhcProblemRequestDto request, CancellationToken cancellationToken = default);
    Task<EhcProblemDetailDto> UpdateProblemAsync(Guid id, UpdateEhcProblemRequestDto request, CancellationToken cancellationToken = default);
    Task DeleteProblemAsync(Guid id, CancellationToken cancellationToken = default);

    Task<EhcProblemDetailDto> ConvertTicketToProblemAsync(Guid ticketId, ConvertEhcTicketToProblemRequestDto request, CancellationToken cancellationToken = default);
    Task LinkTicketAsync(Guid problemId, LinkEhcTicketToProblemRequestDto request, CancellationToken cancellationToken = default);
    Task UnlinkTicketAsync(Guid problemId, Guid ticketId, CancellationToken cancellationToken = default);

    Task<EhcCapaTaskDto> CreateCapaTaskAsync(Guid problemId, CreateEhcCapaTaskRequestDto request, CancellationToken cancellationToken = default);
    Task<EhcCapaTaskDto> UpdateCapaTaskAsync(Guid problemId, Guid taskId, UpdateEhcCapaTaskRequestDto request, CancellationToken cancellationToken = default);
    Task DeleteCapaTaskAsync(Guid problemId, Guid taskId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<EhcProblemLookupItemDto>> SearchProblemsAsync(string q, int limit = 20, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<EhcProblemLookupItemDto>> GetTicketProblemsAsync(Guid ticketId, CancellationToken cancellationToken = default);
}

