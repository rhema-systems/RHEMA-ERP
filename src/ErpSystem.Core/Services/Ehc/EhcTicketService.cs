using System.Text.Json;
using ErpSystem.Core.DTOs.Ehc;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Ehc;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Events;
using ErpSystem.Core.Interfaces.Ehc;
using ErpSystem.Core.Interfaces.Repositories;
using ErpSystem.Core.Interfaces.Workflow;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Core.Services.Ehc;

public sealed class EhcTicketService : IEhcTicketService
{
    private const string DefaultWorkflowName = "EHC Ticket";
    private const string ExternalPortalTicketPathPrefix = "/support/tickets";

    private readonly IEhcTicketRepository _ticketRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IAppEventBus _appEventBus;
    private readonly IWorkflowEngine _workflowEngine;
    private readonly IWorkflowStepRepository _workflowStepRepository;
    private readonly IWorkflowStepInstanceRepository _workflowStepInstanceRepository;
    private readonly IFileStorageService _fileStorageService;
    private readonly ILogger<EhcTicketService> _logger;

    public EhcTicketService(
        IEhcTicketRepository ticketRepository,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        UserManager<ApplicationUser> userManager,
        IAppEventBus appEventBus,
        IWorkflowEngine workflowEngine,
        IWorkflowStepRepository workflowStepRepository,
        IWorkflowStepInstanceRepository workflowStepInstanceRepository,
        IFileStorageService fileStorageService,
        ILogger<EhcTicketService> logger)
    {
        _ticketRepository = ticketRepository;
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _userManager = userManager;
        _appEventBus = appEventBus;
        _workflowEngine = workflowEngine;
        _workflowStepRepository = workflowStepRepository;
        _workflowStepInstanceRepository = workflowStepInstanceRepository;
        _fileStorageService = fileStorageService;
        _logger = logger;
    }

    private async Task AddAuditEventAsync(
        EhcTicket ticket,
        string eventType,
        string? title,
        string? body,
        bool isInternal,
        Guid? actorUserId,
        object? data = null)
    {
        if (ticket == null) throw new ArgumentNullException(nameof(ticket));
        if (ticket.TenantId == Guid.Empty || ticket.Id == Guid.Empty) return;

        var ev = new EhcTicketAuditEvent
        {
            TenantId = ticket.TenantId,
            TicketId = ticket.Id,
            EventType = (eventType ?? string.Empty).Trim(),
            Title = string.IsNullOrWhiteSpace(title) ? null : title.Trim(),
            Body = string.IsNullOrWhiteSpace(body) ? null : body.Trim(),
            IsInternal = isInternal,
            ActorUserId = actorUserId,
            DataJson = data == null ? null : JsonSerializer.Serialize(data),
            CreatedAt = DateTime.UtcNow,
            CreatedBy = _currentUserService.UserName ?? "System",
            CreatedById = actorUserId
        };

        await _unitOfWork.Repository<EhcTicketAuditEvent>().AddAsync(ev);
    }

    private async Task PublishTicketTopicAsync(
        Guid tenantId,
        string activity,
        string audience,
        Guid ticketId,
        Guid? triggeredByUserId,
        Dictionary<string, object> data,
        CancellationToken cancellationToken)
    {
        if (tenantId == Guid.Empty) return;

        try
        {
            await _appEventBus.PublishAsync(new EntityActivityEvent
            {
                TenantId = tenantId,
                EntityType = "EhcTicket",
                EntityId = ticketId,
                Activity = activity,
                Audience = audience,
                TriggeredByUserId = triggeredByUserId,
                Data = data ?? new Dictionary<string, object>()
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to publish EHC ticket topic event {Activity}.{Audience} for ticket {TicketId}", activity, audience, ticketId);
        }
    }

    private async Task<string> ResolveRequesterActionUrlAsync(EhcTicket ticket, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var requester = await _userManager.FindByIdAsync(ticket.RequesterUserId.ToString());
        if (requester?.AuthenticationProvider == AuthenticationProvider.Local)
        {
            return $"{ExternalPortalTicketPathPrefix}/{ticket.Id}";
        }

        return $"/helpdesk/tickets/{ticket.Id}";
    }

    private static int GetRuleSpecificity(EhcWorkflowRoutingRule r)
    {
        var s = 0;
        if (r.TicketType.HasValue) s++;
        if (r.TicketPriority.HasValue) s++;
        if (r.CategoryId.HasValue && r.CategoryId.Value != Guid.Empty) s++;
        if (r.SubcategoryId.HasValue && r.SubcategoryId.Value != Guid.Empty) s++;
        if (r.AssignedDepartmentId.HasValue && r.AssignedDepartmentId.Value != Guid.Empty) s++;
        return s;
    }

    private async Task<string> ResolveWorkflowNameForTicketAsync(EhcTicket ticket, CancellationToken cancellationToken)
    {
        var tenantId = ticket.TenantId;
        if (tenantId == Guid.Empty)
        {
            return DefaultWorkflowName;
        }

        var repo = _unitOfWork.Repository<EhcWorkflowRoutingRule>();
        var rules = await repo
            .GetQueryable(r => r.TenantId == tenantId && !r.IsDeleted && r.IsActive)
            .ToListAsync(cancellationToken);

        if (rules.Count == 0)
        {
            return DefaultWorkflowName;
        }

        bool Matches(EhcWorkflowRoutingRule r)
        {
            if (r.TicketType.HasValue && r.TicketType.Value != ticket.TicketType) return false;
            if (r.TicketPriority.HasValue && r.TicketPriority.Value != ticket.Priority) return false;
            if (r.AssignedDepartmentId.HasValue && r.AssignedDepartmentId.Value != Guid.Empty)
            {
                if (!ticket.AssignedDepartmentId.HasValue || ticket.AssignedDepartmentId.Value != r.AssignedDepartmentId.Value) return false;
            }
            if (r.CategoryId.HasValue && r.CategoryId.Value != Guid.Empty)
            {
                if (!ticket.CategoryId.HasValue || ticket.CategoryId.Value != r.CategoryId.Value) return false;
            }
            if (r.SubcategoryId.HasValue && r.SubcategoryId.Value != Guid.Empty)
            {
                if (!ticket.SubcategoryId.HasValue || ticket.SubcategoryId.Value != r.SubcategoryId.Value) return false;
            }
            return true;
        }

        var best = rules
            .Where(Matches)
            .OrderByDescending(r => r.Priority)
            .ThenByDescending(GetRuleSpecificity)
            .ThenBy(r => r.CreatedAt)
            .FirstOrDefault();

        var name = (best?.WorkflowName ?? string.Empty).Trim();
        return string.IsNullOrWhiteSpace(name) ? DefaultWorkflowName : name;
    }

    public async Task<EhcTicketDetailDto> CreateExternalTicketAsync(CreateEhcTicketRequestDto request, CancellationToken cancellationToken = default)
    {
        var tenantId = _currentUserService.TenantId ?? Guid.Empty;
        if (tenantId == Guid.Empty)
        {
            throw new InvalidOperationException("Tenant context is required.");
        }

        if (!Guid.TryParse(_currentUserService.UserId, out var requesterUserId) || requesterUserId == Guid.Empty)
        {
            throw new InvalidOperationException("Authenticated user context is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Description))
        {
            throw new ArgumentException("Description is required.", nameof(request.Description));
        }

        if (!request.CategoryId.HasValue || request.CategoryId.Value == Guid.Empty)
        {
            throw new ArgumentException("Category is required.", nameof(request.CategoryId));
        }

        var categoryRepo = _unitOfWork.Repository<EhcTicketCategory>();
        var category = await categoryRepo.GetByIdAsync(request.CategoryId.Value);
        if (category == null || category.TenantId != tenantId || category.IsDeleted)
        {
            throw new ArgumentException("Invalid category.", nameof(request.CategoryId));
        }

        if (request.SubcategoryId.HasValue && request.SubcategoryId.Value != Guid.Empty)
        {
            var sub = await categoryRepo.GetByIdAsync(request.SubcategoryId.Value);
            if (sub == null || sub.TenantId != tenantId || sub.IsDeleted)
            {
                throw new ArgumentException("Invalid subcategory.", nameof(request.SubcategoryId));
            }

            // Allow selecting a subcategory at any depth, as long as it's within the selected category subtree.
            var current = sub;
            var isWithinCategory = false;
            while (current.ParentCategoryId.HasValue && current.ParentCategoryId.Value != Guid.Empty)
            {
                if (current.ParentCategoryId.Value == category.Id)
                {
                    isWithinCategory = true;
                    break;
                }

                var parent = await categoryRepo.GetByIdAsync(current.ParentCategoryId.Value);
                if (parent == null || parent.TenantId != tenantId || parent.IsDeleted)
                {
                    break;
                }

                current = parent;
            }

            if (!isWithinCategory)
            {
                throw new ArgumentException("Invalid subcategory.", nameof(request.SubcategoryId));
            }
        }

        var ticketNumber = await _ticketRepository.GenerateTicketNumberAsync(tenantId, cancellationToken);
        var now = DateTime.UtcNow;

        var ticket = new EhcTicket
        {
            TenantId = tenantId,
            TicketNumber = ticketNumber,
            TicketType = request.TicketType,
            CategoryId = request.CategoryId,
            SubcategoryId = request.SubcategoryId,
            Priority = request.Priority,
            Source = request.Source,
            Subject = request.Subject?.Trim(),
            Description = request.Description.Trim(),
            RequesterUserId = requesterUserId,
            Status = EhcTicketStatus.New,
            RelatedEntityType = request.RelatedEntityType?.Trim(),
            RelatedEntityReference = request.RelatedEntityReference?.Trim(),
            CreatedBy = _currentUserService.UserName,
            CreatedById = requesterUserId,
            CreatedAt = now
        };

        var sla = await ResolveSlaTemplateAsync(tenantId, request, cancellationToken);
        if (sla != null)
        {
            ticket.FirstResponseDueAt = now.AddMinutes(Math.Max(1, sla.FirstResponseMinutes));
            ticket.ResolutionDueAt = now.AddMinutes(Math.Max(1, sla.ResolutionMinutes));
        }

        var ticketRepo = _unitOfWork.Repository<EhcTicket>();
        await ticketRepo.AddAsync(ticket);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Start workflow instance (drives status transitions)
        var workflowName = await ResolveWorkflowNameForTicketAsync(ticket, cancellationToken);
        var workflowInstance = await _workflowEngine.StartWorkflowAsync(
            workflowName,
            ticket.Id,
            requesterUserId,
            dataContext: new
            {
                ticketId = ticket.Id,
                ticketNumber = ticket.TicketNumber,
                ticketType = ticket.TicketType.ToString(),
                priority = ticket.Priority.ToString()
            });

        ticket.WorkflowInstanceId = workflowInstance.Id;
        await ticketRepo.UpdateAsync(ticket);

        var historyRepo = _unitOfWork.Repository<EhcTicketStatusHistory>();
        await historyRepo.AddAsync(new EhcTicketStatusHistory
        {
            TenantId = tenantId,
            TicketId = ticket.Id,
            FromStatus = null,
            ToStatus = EhcTicketStatus.New,
            ChangedByUserId = requesterUserId,
            CreatedBy = _currentUserService.UserName,
            CreatedById = requesterUserId,
            CreatedAt = now,
            Notes = "Ticket created"
        });

        await AddAuditEventAsync(
            ticket,
            eventType: "TicketCreated",
            title: "Ticket created",
            body: $"Ticket {ticket.TicketNumber} created.",
            isInternal: false,
            actorUserId: requesterUserId,
            data: new
            {
                ticketId = ticket.Id,
                ticketNumber = ticket.TicketNumber,
                ticketType = ticket.TicketType.ToString(),
                priority = ticket.Priority.ToString(),
                source = ticket.Source.ToString(),
                status = ticket.Status.ToString()
            });

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await PublishTicketTopicAsync(
            tenantId,
            activity: "Created",
            audience: "Requester",
            ticketId: ticket.Id,
            triggeredByUserId: requesterUserId,
            data: new Dictionary<string, object>
            {
                ["ticketId"] = ticket.Id,
                ["ticketNumber"] = ticket.TicketNumber,
                ["ticketType"] = ticket.TicketType.ToString(),
                ["priority"] = ticket.Priority.ToString(),
                ["source"] = ticket.Source.ToString(),
                ["status"] = ticket.Status.ToString(),
                ["requesterUserId"] = requesterUserId,
                ["ActionUrl"] = $"{ExternalPortalTicketPathPrefix}/{ticket.Id}"
            },
            cancellationToken);

        await PublishTicketTopicAsync(
            tenantId,
            activity: "Created",
            audience: "Internal",
            ticketId: ticket.Id,
            triggeredByUserId: requesterUserId,
            data: new Dictionary<string, object>
            {
                ["ticketId"] = ticket.Id,
                ["ticketNumber"] = ticket.TicketNumber,
                ["ticketType"] = ticket.TicketType.ToString(),
                ["priority"] = ticket.Priority.ToString(),
                ["source"] = ticket.Source.ToString(),
                ["status"] = ticket.Status.ToString(),
                ["requesterUserId"] = requesterUserId,
                ["ActionUrl"] = $"/helpdesk/tickets/{ticket.Id}"
            },
            cancellationToken);

        var dto = await GetMyTicketByIdAsync(ticket.Id, cancellationToken);
        if (dto == null)
        {
            throw new InvalidOperationException("Failed to load created ticket.");
        }

        return dto;
    }

    public async Task<EhcTicketDetailDto> CreateInternalTicketAsync(CreateEhcTicketRequestDto request, CancellationToken cancellationToken = default)
    {
        var tenantId = _currentUserService.TenantId ?? Guid.Empty;
        if (tenantId == Guid.Empty)
        {
            throw new InvalidOperationException("Tenant context is required.");
        }

        if (!Guid.TryParse(_currentUserService.UserId, out var requesterUserId) || requesterUserId == Guid.Empty)
        {
            throw new InvalidOperationException("Authenticated user context is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Description))
        {
            throw new ArgumentException("Description is required.", nameof(request.Description));
        }

        if (!request.CategoryId.HasValue || request.CategoryId.Value == Guid.Empty)
        {
            throw new ArgumentException("Category is required.", nameof(request.CategoryId));
        }

        Guid? assignedDepartmentId = request.AssignedDepartmentId;
        if (!assignedDepartmentId.HasValue || assignedDepartmentId.Value == Guid.Empty)
        {
            var employeeId = _currentUserService.EmployeeId;
            if (employeeId.HasValue && employeeId.Value != Guid.Empty)
            {
                var empRepo = _unitOfWork.Repository<Employee>();
                var emp = await empRepo.GetByIdAsync(employeeId.Value);
                if (emp != null && emp.TenantId == tenantId && !emp.IsDeleted && emp.IsActive)
                {
                    assignedDepartmentId = emp.DepartmentId;
                }
            }
        }

        if (assignedDepartmentId.HasValue && assignedDepartmentId.Value != Guid.Empty)
        {
            var deptRepo = _unitOfWork.Repository<Department>();
            var dept = await deptRepo.GetByIdAsync(assignedDepartmentId.Value);
            if (dept == null || dept.TenantId != tenantId || dept.IsDeleted || !dept.IsActive)
            {
                throw new ArgumentException("Invalid department.", nameof(request.AssignedDepartmentId));
            }
        }

        var categoryRepo = _unitOfWork.Repository<EhcTicketCategory>();
        var category = await categoryRepo.GetByIdAsync(request.CategoryId.Value);
        if (category == null || category.TenantId != tenantId || category.IsDeleted)
        {
            throw new ArgumentException("Invalid category.", nameof(request.CategoryId));
        }

        if (request.SubcategoryId.HasValue && request.SubcategoryId.Value != Guid.Empty)
        {
            var sub = await categoryRepo.GetByIdAsync(request.SubcategoryId.Value);
            if (sub == null || sub.TenantId != tenantId || sub.IsDeleted)
            {
                throw new ArgumentException("Invalid subcategory.", nameof(request.SubcategoryId));
            }

            // Allow selecting a subcategory at any depth, as long as it's within the selected category subtree.
            var current = sub;
            var isWithinCategory = false;
            while (current.ParentCategoryId.HasValue && current.ParentCategoryId.Value != Guid.Empty)
            {
                if (current.ParentCategoryId.Value == category.Id)
                {
                    isWithinCategory = true;
                    break;
                }

                var parent = await categoryRepo.GetByIdAsync(current.ParentCategoryId.Value);
                if (parent == null || parent.TenantId != tenantId || parent.IsDeleted)
                {
                    break;
                }

                current = parent;
            }

            if (!isWithinCategory)
            {
                throw new ArgumentException("Invalid subcategory.", nameof(request.SubcategoryId));
            }
        }

        var ticketNumber = await _ticketRepository.GenerateTicketNumberAsync(tenantId, cancellationToken);
        var now = DateTime.UtcNow;

        var ticket = new EhcTicket
        {
            TenantId = tenantId,
            TicketNumber = ticketNumber,
            TicketType = request.TicketType,
            CategoryId = request.CategoryId,
            SubcategoryId = request.SubcategoryId,
            Priority = request.Priority,
            Source = request.Source,
            Subject = request.Subject?.Trim(),
            Description = request.Description.Trim(),
            RequesterUserId = requesterUserId,
            AssignedDepartmentId = assignedDepartmentId.HasValue && assignedDepartmentId.Value != Guid.Empty ? assignedDepartmentId : null,
            Status = EhcTicketStatus.New,
            RelatedEntityType = request.RelatedEntityType?.Trim(),
            RelatedEntityReference = request.RelatedEntityReference?.Trim(),
            CreatedBy = _currentUserService.UserName,
            CreatedById = requesterUserId,
            CreatedAt = now
        };

        var sla = await ResolveSlaTemplateAsync(tenantId, request, cancellationToken);
        if (sla != null)
        {
            ticket.FirstResponseDueAt = now.AddMinutes(Math.Max(1, sla.FirstResponseMinutes));
            ticket.ResolutionDueAt = now.AddMinutes(Math.Max(1, sla.ResolutionMinutes));
        }

        var ticketRepo = _unitOfWork.Repository<EhcTicket>();
        await ticketRepo.AddAsync(ticket);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Start workflow instance (drives status transitions)
        var workflowName = await ResolveWorkflowNameForTicketAsync(ticket, cancellationToken);
        var workflowInstance = await _workflowEngine.StartWorkflowAsync(
            workflowName,
            ticket.Id,
            requesterUserId,
            dataContext: new
            {
                ticketId = ticket.Id,
                ticketNumber = ticket.TicketNumber,
                ticketType = ticket.TicketType.ToString(),
                priority = ticket.Priority.ToString()
            });

        ticket.WorkflowInstanceId = workflowInstance.Id;
        await ticketRepo.UpdateAsync(ticket);

        var historyRepo = _unitOfWork.Repository<EhcTicketStatusHistory>();
        await historyRepo.AddAsync(new EhcTicketStatusHistory
        {
            TenantId = tenantId,
            TicketId = ticket.Id,
            FromStatus = null,
            ToStatus = EhcTicketStatus.New,
            ChangedByUserId = requesterUserId,
            CreatedBy = _currentUserService.UserName,
            CreatedById = requesterUserId,
            CreatedAt = now,
            Notes = "Ticket created"
        });

        await AddAuditEventAsync(
            ticket,
            eventType: "TicketCreated",
            title: "Ticket created",
            body: $"Ticket {ticket.TicketNumber} created (internal).",
            isInternal: true,
            actorUserId: requesterUserId,
            data: new
            {
                ticketId = ticket.Id,
                ticketNumber = ticket.TicketNumber,
                ticketType = ticket.TicketType.ToString(),
                priority = ticket.Priority.ToString(),
                source = ticket.Source.ToString(),
                status = ticket.Status.ToString()
            });

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await PublishTicketTopicAsync(
            tenantId,
            activity: "Created",
            audience: "Internal",
            ticketId: ticket.Id,
            triggeredByUserId: requesterUserId,
            data: new Dictionary<string, object>
            {
                ["ticketId"] = ticket.Id,
                ["ticketNumber"] = ticket.TicketNumber,
                ["ticketType"] = ticket.TicketType.ToString(),
                ["priority"] = ticket.Priority.ToString(),
                ["source"] = ticket.Source.ToString(),
                ["status"] = ticket.Status.ToString(),
                ["requesterUserId"] = requesterUserId,
                ["ActionUrl"] = $"/helpdesk/tickets/{ticket.Id}"
            },
            cancellationToken);

        var dto = await GetTicketByIdAsync(ticket.Id, cancellationToken);
        if (dto == null)
        {
            throw new InvalidOperationException("Failed to load created ticket.");
        }

        return dto;
    }

    public async Task<IReadOnlyList<EhcTicketListItemDto>> GetMyTicketsAsync(
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
        CancellationToken cancellationToken = default)
    {
        var tenantId = _currentUserService.TenantId ?? Guid.Empty;
        if (tenantId == Guid.Empty)
        {
            return Array.Empty<EhcTicketListItemDto>();
        }

        if (!Guid.TryParse(_currentUserService.UserId, out var requesterUserId) || requesterUserId == Guid.Empty)
        {
            return Array.Empty<EhcTicketListItemDto>();
        }

        var tickets = await _ticketRepository.GetForRequesterAsync(
            tenantId,
            requesterUserId,
            page,
            pageSize,
            q: q,
            status: status,
            ticketType: ticketType,
            priority: priority,
            source: source,
            categoryId: categoryId,
            createdFrom: createdFrom,
            createdTo: createdTo,
            cancellationToken: cancellationToken);
        return tickets.Select(MapToExternalListItemDto).ToList();
    }

    public async Task<EhcTicketDetailDto?> GetMyTicketByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tenantId = _currentUserService.TenantId ?? Guid.Empty;
        if (tenantId == Guid.Empty)
        {
            return null;
        }

        if (!Guid.TryParse(_currentUserService.UserId, out var requesterUserId) || requesterUserId == Guid.Empty)
        {
            return null;
        }

        var ticket = await _ticketRepository.GetByIdForRequesterAsync(id, tenantId, requesterUserId, cancellationToken);
        return ticket == null ? null : await MapToDetailDtoAsync(ticket, includeInternal: false, cancellationToken);
    }

    public async Task<EhcTicketMessageDto> AddExternalMessageAsync(Guid ticketId, AddEhcTicketMessageRequestDto request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Body))
        {
            throw new ArgumentException("Message body is required.", nameof(request.Body));
        }

        var tenantId = _currentUserService.TenantId ?? Guid.Empty;
        if (tenantId == Guid.Empty)
        {
            throw new InvalidOperationException("Tenant context is required.");
        }

        if (!Guid.TryParse(_currentUserService.UserId, out var requesterUserId) || requesterUserId == Guid.Empty)
        {
            throw new InvalidOperationException("Authenticated user context is required.");
        }

        // Ensure external user can only post to their own ticket
        var ticket = await _ticketRepository.GetByIdForRequesterAsync(ticketId, tenantId, requesterUserId, cancellationToken);
        if (ticket == null)
        {
            throw new KeyNotFoundException("Ticket not found.");
        }

        var now = DateTime.UtcNow;
        var message = new EhcTicketMessage
        {
            TenantId = tenantId,
            TicketId = ticketId,
            Body = request.Body.Trim(),
            IsInternal = false,
            AuthorUserId = requesterUserId,
            CreatedAt = now,
            CreatedBy = _currentUserService.UserName,
            CreatedById = requesterUserId
        };

        await _unitOfWork.Repository<EhcTicketMessage>().AddAsync(message);

        // If agent requested info, user replying should move back to InProgress (phase 1 rule)
        var didAutoResume = false;
        if (ticket.Status == EhcTicketStatus.PendingUser)
        {
            ticket.Status = EhcTicketStatus.InProgress;
            ticket.UpdatedAt = now;
            await _unitOfWork.Repository<EhcTicket>().UpdateAsync(ticket);
            await AddStatusHistoryAsync(ticket, from: EhcTicketStatus.PendingUser, to: EhcTicketStatus.InProgress, "User replied", requesterUserId);
            didAutoResume = true;
        }

        await AddAuditEventAsync(
            ticket,
            eventType: "RequesterMessage",
            title: "Requester replied",
            body: "Requester sent a message.",
            isInternal: false,
            actorUserId: requesterUserId,
            data: new { messageId = message.Id });

        if (didAutoResume)
        {
            await AddAuditEventAsync(
                ticket,
                eventType: "StatusChanged",
                title: "Status updated",
                body: "Status: PendingUser → InProgress",
                isInternal: false,
                actorUserId: requesterUserId,
                data: new { fromStatus = EhcTicketStatus.PendingUser.ToString(), toStatus = EhcTicketStatus.InProgress.ToString() });
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await PublishTicketTopicAsync(
            tenantId,
            activity: "Message",
            audience: "Internal",
            ticketId: ticket.Id,
            triggeredByUserId: requesterUserId,
            data: new Dictionary<string, object>
            {
                ["ticketId"] = ticket.Id,
                ["ticketNumber"] = ticket.TicketNumber,
                ["assignedToUserId"] = ticket.AssignedToUserId ?? Guid.Empty,
                ["requesterUserId"] = requesterUserId,
                ["ActionUrl"] = $"/helpdesk/tickets/{ticket.Id}"
            },
            cancellationToken);

        if (didAutoResume)
        {
            await PublishTicketTopicAsync(
                tenantId,
                activity: "StatusChanged",
                audience: "Internal",
                ticketId: ticket.Id,
                triggeredByUserId: requesterUserId,
                data: new Dictionary<string, object>
                {
                    ["ticketId"] = ticket.Id,
                    ["ticketNumber"] = ticket.TicketNumber,
                    ["fromStatus"] = EhcTicketStatus.PendingUser.ToString(),
                    ["toStatus"] = EhcTicketStatus.InProgress.ToString(),
                    ["assignedToUserId"] = ticket.AssignedToUserId ?? Guid.Empty,
                    ["ActionUrl"] = $"/helpdesk/tickets/{ticket.Id}"
                },
                cancellationToken);
        }

        return new EhcTicketMessageDto
        {
            Id = message.Id,
            Body = message.Body,
            IsInternal = false,
            AuthorUserId = requesterUserId,
            AuthorName = _currentUserService.UserName,
            CreatedAt = message.CreatedAt
        };
    }

    public async Task<EhcTicketAttachmentDto> AddExternalAttachmentAsync(Guid ticketId, AddEhcTicketAttachmentRequestDto request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.FilePath) || string.IsNullOrWhiteSpace(request.FileName))
        {
            throw new ArgumentException("FilePath and FileName are required.");
        }

        var tenantId = _currentUserService.TenantId ?? Guid.Empty;
        if (tenantId == Guid.Empty)
        {
            throw new InvalidOperationException("Tenant context is required.");
        }

        if (!Guid.TryParse(_currentUserService.UserId, out var requesterUserId) || requesterUserId == Guid.Empty)
        {
            throw new InvalidOperationException("Authenticated user context is required.");
        }

        // Ensure external user can only attach to their own ticket
        var ticket = await _ticketRepository.GetByIdForRequesterAsync(ticketId, tenantId, requesterUserId, cancellationToken);
        if (ticket == null)
        {
            throw new KeyNotFoundException("Ticket not found.");
        }

        var now = DateTime.UtcNow;
        var attachment = new EhcTicketAttachment
        {
            TenantId = tenantId,
            TicketId = ticketId,
            MessageId = request.MessageId,
            FilePath = request.FilePath.Trim(),
            FileName = request.FileName.Trim(),
            ContentType = request.ContentType,
            FileSize = request.FileSize,
            IsInternal = false,
            CreatedAt = now,
            CreatedBy = _currentUserService.UserName,
            CreatedById = requesterUserId
        };

        await _unitOfWork.Repository<EhcTicketAttachment>().AddAsync(attachment);

        await AddAuditEventAsync(
            ticket,
            eventType: "RequesterAttachment",
            title: "Attachment uploaded",
            body: $"Requester uploaded {attachment.FileName}.",
            isInternal: false,
            actorUserId: requesterUserId,
            data: new { attachmentId = attachment.Id, fileName = attachment.FileName, fileSize = attachment.FileSize });

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await PublishTicketTopicAsync(
            tenantId,
            activity: "Attachment",
            audience: "Internal",
            ticketId: ticket.Id,
            triggeredByUserId: requesterUserId,
            data: new Dictionary<string, object>
            {
                ["ticketId"] = ticket.Id,
                ["ticketNumber"] = ticket.TicketNumber,
                ["assignedToUserId"] = ticket.AssignedToUserId ?? Guid.Empty,
                ["requesterUserId"] = requesterUserId,
                ["fileName"] = attachment.FileName,
                ["ActionUrl"] = $"/helpdesk/tickets/{ticket.Id}"
            },
            cancellationToken);

        return await MapToAttachmentDtoAsync(attachment);
    }

    public async Task<EhcTicketMessageDto> AddAgentMessageAsync(Guid ticketId, AddEhcTicketMessageRequestDto request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Body))
        {
            throw new ArgumentException("Message body is required.", nameof(request.Body));
        }

        var tenantId = _currentUserService.TenantId ?? Guid.Empty;
        if (tenantId == Guid.Empty)
        {
            throw new InvalidOperationException("Tenant context is required.");
        }

        if (!Guid.TryParse(_currentUserService.UserId, out var authorUserId) || authorUserId == Guid.Empty)
        {
            throw new InvalidOperationException("Authenticated user context is required.");
        }

        var ticketRepo = _unitOfWork.Repository<EhcTicket>();
        var ticket = await ticketRepo.GetByIdAsync(ticketId);
        if (ticket == null || ticket.TenantId != tenantId)
        {
            throw new KeyNotFoundException("Ticket not found.");
        }

        var now = DateTime.UtcNow;
        var message = new EhcTicketMessage
        {
            TenantId = tenantId,
            TicketId = ticketId,
            Body = request.Body.Trim(),
            IsInternal = false,
            AuthorUserId = authorUserId,
            CreatedAt = now,
            CreatedBy = _currentUserService.UserName,
            CreatedById = authorUserId
        };

        await _unitOfWork.Repository<EhcTicketMessage>().AddAsync(message);

        // Mark first response timestamp when an internal agent first interacts
        if (ticket.FirstRespondedAt == null && ticket.Status is EhcTicketStatus.New or EhcTicketStatus.Reopened)
        {
            ticket.FirstRespondedAt = now;
            await ticketRepo.UpdateAsync(ticket);
        }

        await AddAuditEventAsync(
            ticket,
            eventType: "AgentMessage",
            title: "Support replied",
            body: "Support sent a message.",
            isInternal: false,
            actorUserId: authorUserId,
            data: new { messageId = message.Id });

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var requesterActionUrl = await ResolveRequesterActionUrlAsync(ticket, cancellationToken);

        await PublishTicketTopicAsync(
            tenantId,
            activity: "Message",
            audience: "Requester",
            ticketId: ticket.Id,
            triggeredByUserId: authorUserId,
            data: new Dictionary<string, object>
            {
                ["ticketId"] = ticket.Id,
                ["ticketNumber"] = ticket.TicketNumber,
                ["requesterUserId"] = ticket.RequesterUserId,
                ["ActionUrl"] = requesterActionUrl
            },
            cancellationToken);

        return new EhcTicketMessageDto
        {
            Id = message.Id,
            Body = message.Body,
            IsInternal = false,
            AuthorUserId = authorUserId,
            AuthorName = _currentUserService.UserName,
            CreatedAt = message.CreatedAt
        };
    }

    public async Task<EhcTicketAttachmentDto> AddInternalAttachmentAsync(Guid ticketId, AddEhcTicketAttachmentRequestDto request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.FilePath) || string.IsNullOrWhiteSpace(request.FileName))
        {
            throw new ArgumentException("FilePath and FileName are required.");
        }

        var tenantId = _currentUserService.TenantId ?? Guid.Empty;
        if (tenantId == Guid.Empty)
        {
            throw new InvalidOperationException("Tenant context is required.");
        }

        if (!Guid.TryParse(_currentUserService.UserId, out var actorUserId) || actorUserId == Guid.Empty)
        {
            throw new InvalidOperationException("Authenticated user context is required.");
        }

        var ticketRepo = _unitOfWork.Repository<EhcTicket>();
        var ticket = await ticketRepo.GetByIdAsync(ticketId);
        if (ticket == null || ticket.TenantId != tenantId)
        {
            throw new KeyNotFoundException("Ticket not found.");
        }

        EhcTicketMessage? message = null;
        if (request.MessageId.HasValue && request.MessageId.Value != Guid.Empty)
        {
            message = await _unitOfWork.Repository<EhcTicketMessage>().GetByIdAsync(request.MessageId.Value);
            if (message == null || message.TenantId != tenantId || message.TicketId != ticketId)
            {
                throw new ArgumentException("Invalid messageId.", nameof(request.MessageId));
            }
        }

        var now = DateTime.UtcNow;
        var isInternal = message?.IsInternal == true || request.IsInternal;

        var attachment = new EhcTicketAttachment
        {
            TenantId = tenantId,
            TicketId = ticketId,
            MessageId = request.MessageId,
            FilePath = request.FilePath.Trim(),
            FileName = request.FileName.Trim(),
            ContentType = request.ContentType,
            FileSize = request.FileSize,
            IsInternal = isInternal,
            CreatedAt = now,
            CreatedBy = _currentUserService.UserName,
            CreatedById = actorUserId
        };

        await _unitOfWork.Repository<EhcTicketAttachment>().AddAsync(attachment);

        await AddAuditEventAsync(
            ticket,
            eventType: isInternal ? "InternalAttachment" : "AgentAttachment",
            title: "Attachment uploaded",
            body: $"{(isInternal ? "Internal" : "Support")} attachment uploaded: {attachment.FileName}.",
            isInternal: isInternal,
            actorUserId: actorUserId,
            data: new { attachmentId = attachment.Id, fileName = attachment.FileName, fileSize = attachment.FileSize, isInternal });

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Notify requester if this attachment is external-facing.
        if (!isInternal)
        {
            var requesterActionUrl = await ResolveRequesterActionUrlAsync(ticket, cancellationToken);

            await PublishTicketTopicAsync(
                tenantId,
                activity: "Attachment",
                audience: "Requester",
                ticketId: ticket.Id,
                triggeredByUserId: actorUserId,
                data: new Dictionary<string, object>
                {
                    ["ticketId"] = ticket.Id,
                    ["ticketNumber"] = ticket.TicketNumber,
                    ["requesterUserId"] = ticket.RequesterUserId,
                    ["fileName"] = attachment.FileName,
                    ["ActionUrl"] = requesterActionUrl
                },
                cancellationToken);
        }

        return await MapToAttachmentDtoAsync(attachment);
    }

    public async Task<IReadOnlyList<EhcTicketListItemDto>> GetTicketsAsync(
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
        CancellationToken cancellationToken = default)
    {
        var tenantId = _currentUserService.TenantId ?? Guid.Empty;
        if (tenantId == Guid.Empty)
        {
            return Array.Empty<EhcTicketListItemDto>();
        }

        var tickets = await _ticketRepository.GetPagedFilteredAsync(
            tenantId,
            page,
            pageSize,
            status: status,
            ticketType: ticketType,
            priority: priority,
            source: source,
            categoryId: categoryId,
            assignedDepartmentId: assignedDepartmentId,
            createdFrom: createdFrom,
            createdTo: createdTo,
            cancellationToken: cancellationToken);

        return tickets.Select(MapToInternalListItemDto).ToList();
    }

    public async Task<EhcTicketDetailDto?> GetTicketByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tenantId = _currentUserService.TenantId ?? Guid.Empty;
        if (tenantId == Guid.Empty)
        {
            return null;
        }

        var ticket = await _ticketRepository.GetByIdWithDetailsAsync(id, tenantId, cancellationToken);
        return ticket == null ? null : await MapToDetailDtoAsync(ticket, includeInternal: true, cancellationToken);
    }

    public async Task AssignTicketAsync(Guid ticketId, Guid assignedToUserId, Guid? assignedDepartmentId = null, CancellationToken cancellationToken = default)
    {
        var tenantId = _currentUserService.TenantId ?? Guid.Empty;
        if (tenantId == Guid.Empty)
        {
            throw new InvalidOperationException("Tenant context is required.");
        }

        if (!Guid.TryParse(_currentUserService.UserId, out var actorUserId) || actorUserId == Guid.Empty)
        {
            throw new InvalidOperationException("Authenticated user context is required.");
        }

        var ticketRepo = _unitOfWork.Repository<EhcTicket>();
        var ticket = await ticketRepo.GetByIdAsync(ticketId);
        if (ticket == null || ticket.TenantId != tenantId)
        {
            throw new KeyNotFoundException("Ticket not found.");
        }

        var previousAssignee = ticket.AssignedToUserId;
        var previousDepartment = ticket.AssignedDepartmentId;

        ticket.AssignedToUserId = assignedToUserId;
        ticket.AssignedDepartmentId = assignedDepartmentId;
        ticket.UpdatedAt = DateTime.UtcNow;
        ticket.UpdatedBy = _currentUserService.UserName;
        ticket.LastModifiedById = actorUserId;

        await ticketRepo.UpdateAsync(ticket);

        // Add an internal audit message for traceability (hidden from external users).
        if (previousAssignee != assignedToUserId || previousDepartment != assignedDepartmentId)
        {
            var assigneeName = assignedToUserId.ToString();

            string? deptName = null;
            if (assignedDepartmentId.HasValue && assignedDepartmentId.Value != Guid.Empty)
            {
                var dept = await _unitOfWork.Repository<Department>().GetByIdAsync(assignedDepartmentId.Value);
                deptName = dept?.Name;
            }

            var who = _currentUserService.UserName ?? "System";
            var body = deptName != null
                ? $"Assignment updated by {who}: {assigneeName} • {deptName}"
                : $"Assignment updated by {who}: {assigneeName}";

            await _unitOfWork.Repository<EhcTicketMessage>().AddAsync(new EhcTicketMessage
            {
                TenantId = tenantId,
                TicketId = ticketId,
                Body = body,
                IsInternal = true,
                AuthorUserId = actorUserId,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = who,
                CreatedById = actorUserId
            });

            await AddAuditEventAsync(
                ticket,
                eventType: "AssignmentChanged",
                title: "Assignment updated",
                body: body,
                isInternal: true,
                actorUserId: actorUserId,
                data: new
                {
                    previousAssignee = previousAssignee,
                    previousDepartment = previousDepartment,
                    assignedToUserId,
                    assignedDepartmentId
                });
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        if (previousAssignee != assignedToUserId || previousDepartment != assignedDepartmentId)
        {
            await PublishTicketTopicAsync(
                tenantId,
                activity: "Assigned",
                audience: "Internal",
                ticketId: ticket.Id,
                triggeredByUserId: actorUserId,
                data: new Dictionary<string, object>
                {
                    ["ticketId"] = ticket.Id,
                    ["ticketNumber"] = ticket.TicketNumber,
                    ["assignedToUserId"] = assignedToUserId,
                    ["assignedDepartmentId"] = assignedDepartmentId ?? Guid.Empty,
                    ["ActionUrl"] = $"/helpdesk/tickets/{ticket.Id}"
                },
                cancellationToken);
        }
    }

    public async Task TransitionTicketAsync(Guid ticketId, EhcTicketStatus targetStatus, string? notes = null, Guid? workflowTransitionId = null, string? workflowTransitionName = null, CancellationToken cancellationToken = default)
    {
        var tenantId = _currentUserService.TenantId ?? Guid.Empty;
        if (tenantId == Guid.Empty)
        {
            throw new InvalidOperationException("Tenant context is required.");
        }

        if (!Guid.TryParse(_currentUserService.UserId, out var actorUserId) || actorUserId == Guid.Empty)
        {
            throw new InvalidOperationException("Authenticated user context is required.");
        }

        var ticketRepo = _unitOfWork.Repository<EhcTicket>();
        var ticket = await ticketRepo.GetByIdAsync(ticketId);
        if (ticket == null || ticket.TenantId != tenantId)
        {
            throw new KeyNotFoundException("Ticket not found.");
        }

        var fromStatus = ticket.Status;
        if (fromStatus == targetStatus)
        {
            return;
        }

        var now = DateTime.UtcNow;
        var requesterActionUrl = await ResolveRequesterActionUrlAsync(ticket, cancellationToken);

        // Pending statuses are represented as a workflow "RequestInformation" pause in phase 1.
        if (targetStatus is EhcTicketStatus.PendingUser or EhcTicketStatus.PendingThirdParty)
        {
            if (fromStatus is EhcTicketStatus.Resolved or EhcTicketStatus.Closed)
            {
                throw new InvalidOperationException("Cannot request information for a resolved/closed ticket.");
            }

            await PauseWorkflowForInformationAsync(ticket, actorUserId, notes, cancellationToken);
            ticket.Status = targetStatus;
            ticket.UpdatedAt = now;
            await ticketRepo.UpdateAsync(ticket);
            await AddStatusHistoryAsync(ticket, fromStatus, targetStatus, notes, actorUserId);

            await AddAuditEventAsync(
                ticket,
                eventType: "StatusChanged",
                title: "Status updated",
                body: $"Status: {fromStatus} → {targetStatus}",
                isInternal: false,
                actorUserId: actorUserId,
                data: new { fromStatus = fromStatus.ToString(), toStatus = targetStatus.ToString() });

            if (!string.IsNullOrWhiteSpace(notes))
            {
                await AddAuditEventAsync(
                    ticket,
                    eventType: "StatusNotes",
                    title: "Transition notes",
                    body: notes,
                    isInternal: true,
                    actorUserId: actorUserId,
                    data: new { toStatus = targetStatus.ToString() });
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            await PublishTicketTopicAsync(
                tenantId,
                activity: "StatusChanged",
                audience: "Requester",
                ticketId: ticket.Id,
                triggeredByUserId: actorUserId,
                data: new Dictionary<string, object>
                {
                    ["ticketId"] = ticket.Id,
                    ["ticketNumber"] = ticket.TicketNumber,
                    ["fromStatus"] = fromStatus.ToString(),
                    ["toStatus"] = targetStatus.ToString(),
                    ["requesterUserId"] = ticket.RequesterUserId,
                    ["ActionUrl"] = requesterActionUrl
                },
                cancellationToken);

            await PublishTicketTopicAsync(
                tenantId,
                activity: "StatusChanged",
                audience: "Internal",
                ticketId: ticket.Id,
                triggeredByUserId: actorUserId,
                data: new Dictionary<string, object>
                {
                    ["ticketId"] = ticket.Id,
                    ["ticketNumber"] = ticket.TicketNumber,
                    ["fromStatus"] = fromStatus.ToString(),
                    ["toStatus"] = targetStatus.ToString(),
                    ["assignedToUserId"] = ticket.AssignedToUserId ?? Guid.Empty,
                    ["ActionUrl"] = $"/helpdesk/tickets/{ticket.Id}"
                },
                cancellationToken);
            return;
        }

        // Resume from a pending state without advancing workflow (workflow step is already paused on the same step).
        if (targetStatus == EhcTicketStatus.InProgress && fromStatus is EhcTicketStatus.PendingUser or EhcTicketStatus.PendingThirdParty)
        {
            ticket.Status = EhcTicketStatus.InProgress;
            ticket.UpdatedAt = now;
            ticket.UpdatedBy = _currentUserService.UserName;
            ticket.LastModifiedById = actorUserId;

            await ticketRepo.UpdateAsync(ticket);
            await AddStatusHistoryAsync(ticket, fromStatus, EhcTicketStatus.InProgress, notes ?? "Resumed", actorUserId);

            await AddAuditEventAsync(
                ticket,
                eventType: "StatusChanged",
                title: "Status updated",
                body: $"Status: {fromStatus} → InProgress",
                isInternal: false,
                actorUserId: actorUserId,
                data: new { fromStatus = fromStatus.ToString(), toStatus = EhcTicketStatus.InProgress.ToString() });

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            await PublishTicketTopicAsync(
                tenantId,
                activity: "StatusChanged",
                audience: "Requester",
                ticketId: ticket.Id,
                triggeredByUserId: actorUserId,
                data: new Dictionary<string, object>
                {
                    ["ticketId"] = ticket.Id,
                    ["ticketNumber"] = ticket.TicketNumber,
                    ["fromStatus"] = fromStatus.ToString(),
                    ["toStatus"] = EhcTicketStatus.InProgress.ToString(),
                    ["requesterUserId"] = ticket.RequesterUserId,
                    ["ActionUrl"] = requesterActionUrl
                },
                cancellationToken);

            await PublishTicketTopicAsync(
                tenantId,
                activity: "StatusChanged",
                audience: "Internal",
                ticketId: ticket.Id,
                triggeredByUserId: actorUserId,
                data: new Dictionary<string, object>
                {
                    ["ticketId"] = ticket.Id,
                    ["ticketNumber"] = ticket.TicketNumber,
                    ["fromStatus"] = fromStatus.ToString(),
                    ["toStatus"] = EhcTicketStatus.InProgress.ToString(),
                    ["assignedToUserId"] = ticket.AssignedToUserId ?? Guid.Empty,
                    ["ActionUrl"] = $"/helpdesk/tickets/{ticket.Id}"
                },
                cancellationToken);
            return;
        }

        if (targetStatus == EhcTicketStatus.Reopened)
        {
            if (fromStatus is not (EhcTicketStatus.Resolved or EhcTicketStatus.Closed))
            {
                throw new InvalidOperationException("Only resolved/closed tickets can be reopened.");
            }

            // Reopen is modeled as a new workflow instance in phase 1.
            var workflowInstance = await _workflowEngine.StartWorkflowAsync(DefaultWorkflowName, ticket.Id, actorUserId, new
            {
                ticketId = ticket.Id,
                ticketNumber = ticket.TicketNumber,
                ticketType = ticket.TicketType.ToString(),
                priority = ticket.Priority.ToString(),
                reopenedFrom = ticket.Status.ToString()
            });

            ticket.WorkflowInstanceId = workflowInstance.Id;
            ticket.Status = EhcTicketStatus.Reopened;
            ticket.ResolvedAt = null;
            ticket.ClosedAt = null;
            ticket.UpdatedAt = now;
            await ticketRepo.UpdateAsync(ticket);
            await AddStatusHistoryAsync(ticket, fromStatus, EhcTicketStatus.Reopened, notes ?? "Ticket reopened", actorUserId);

            await AddAuditEventAsync(
                ticket,
                eventType: "StatusChanged",
                title: "Ticket reopened",
                body: $"Status: {fromStatus} → Reopened",
                isInternal: false,
                actorUserId: actorUserId,
                data: new { fromStatus = fromStatus.ToString(), toStatus = EhcTicketStatus.Reopened.ToString() });

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            await PublishTicketTopicAsync(
                tenantId,
                activity: "StatusChanged",
                audience: "Requester",
                ticketId: ticket.Id,
                triggeredByUserId: actorUserId,
                data: new Dictionary<string, object>
                {
                    ["ticketId"] = ticket.Id,
                    ["ticketNumber"] = ticket.TicketNumber,
                    ["fromStatus"] = fromStatus.ToString(),
                    ["toStatus"] = EhcTicketStatus.Reopened.ToString(),
                    ["requesterUserId"] = ticket.RequesterUserId,
                    ["ActionUrl"] = requesterActionUrl
                },
                cancellationToken);

            await PublishTicketTopicAsync(
                tenantId,
                activity: "StatusChanged",
                audience: "Internal",
                ticketId: ticket.Id,
                triggeredByUserId: actorUserId,
                data: new Dictionary<string, object>
                {
                    ["ticketId"] = ticket.Id,
                    ["ticketNumber"] = ticket.TicketNumber,
                    ["fromStatus"] = fromStatus.ToString(),
                    ["toStatus"] = EhcTicketStatus.Reopened.ToString(),
                    ["assignedToUserId"] = ticket.AssignedToUserId ?? Guid.Empty,
                    ["ActionUrl"] = $"/helpdesk/tickets/{ticket.Id}"
                },
                cancellationToken);
            return;
        }

        if (!ticket.WorkflowInstanceId.HasValue || ticket.WorkflowInstanceId == Guid.Empty)
        {
            throw new InvalidOperationException("Ticket has no workflow instance.");
        }

        // Branching workflows: execute a specific transition when provided, otherwise resolve unambiguously by status.
        var availableTransitions = await _workflowEngine.GetAvailableTransitionsAsync(ticket.WorkflowInstanceId.Value, new
        {
            ticketId = ticket.Id,
            ticketNumber = ticket.TicketNumber
        });

        var candidates = new List<Core.Entities.Workflow.WorkflowTransition>();
        foreach (var t in availableTransitions ?? Enumerable.Empty<Core.Entities.Workflow.WorkflowTransition>())
        {
            cancellationToken.ThrowIfCancellationRequested();

            var step = t.ToStep ?? await _workflowStepRepository.GetByIdAsync(t.ToStepId);
            var stepName = step?.Name?.Trim();
            if (string.IsNullOrWhiteSpace(stepName))
            {
                continue;
            }

            if (!Enum.TryParse<EhcTicketStatus>(stepName, ignoreCase: true, out var status))
            {
                continue;
            }

            if (status == targetStatus)
            {
                candidates.Add(t);
            }
        }

        Core.Entities.Workflow.WorkflowTransition? selected = null;
        if (workflowTransitionId.HasValue && workflowTransitionId.Value != Guid.Empty)
        {
            selected = candidates.FirstOrDefault(x => x.Id == workflowTransitionId.Value);
            if (selected == null)
            {
                throw new InvalidOperationException("Selected workflow transition is not available for the chosen status.");
            }
        }
        else if (!string.IsNullOrWhiteSpace(workflowTransitionName))
        {
            var matches = candidates
                .Where(x => x.Name != null && x.Name.Equals(workflowTransitionName.Trim(), StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (matches.Count == 0)
            {
                throw new InvalidOperationException("Selected workflow transition is not available for the chosen status.");
            }
            if (matches.Count > 1)
            {
                throw new InvalidOperationException("Multiple workflow transitions match this name. Please select by transitionId.");
            }

            selected = matches[0];
        }
        else
        {
            if (candidates.Count == 1)
            {
                selected = candidates[0];
            }
            else if (candidates.Count > 1)
            {
                throw new InvalidOperationException("Multiple workflow transitions are available for the chosen status. Please select a specific transition.");
            }
        }

        if (selected == null)
        {
            var allowedStatuses = new HashSet<EhcTicketStatus>();
            foreach (var t in availableTransitions ?? Enumerable.Empty<Core.Entities.Workflow.WorkflowTransition>())
            {
                var step = t.ToStep ?? await _workflowStepRepository.GetByIdAsync(t.ToStepId);
                var stepName = step?.Name?.Trim();
                if (string.IsNullOrWhiteSpace(stepName)) continue;
                if (!Enum.TryParse<EhcTicketStatus>(stepName, ignoreCase: true, out var s)) continue;
                allowedStatuses.Add(s);
            }

            var allowedText = allowedStatuses.Count == 0
                ? "none"
                : string.Join(", ", allowedStatuses.OrderBy(x => x).Select(x => x.ToString()));

            throw new InvalidOperationException($"Transition '{fromStatus}' → '{targetStatus}' is not allowed by the workflow. Allowed next statuses: {allowedText}.");
        }

        await _workflowEngine.ExecuteTransitionAsync(ticket.WorkflowInstanceId.Value, actorUserId, selected.Id, new
        {
            ticketId = ticket.Id,
            ticketNumber = ticket.TicketNumber,
            targetStatus = targetStatus.ToString(),
            workflowTransitionId = selected.Id,
            workflowTransitionName = selected.Name
        });

        // Update ticket status derived from workflow step name (baseline: step names match status).
        ticket.Status = targetStatus;
        if (targetStatus == EhcTicketStatus.Acknowledged && ticket.FirstRespondedAt == null)
        {
            ticket.FirstRespondedAt = now;
        }
        if (targetStatus == EhcTicketStatus.Resolved)
        {
            ticket.ResolvedAt = now;
        }
        if (targetStatus == EhcTicketStatus.Closed)
        {
            ticket.ClosedAt = now;
        }

        ticket.UpdatedAt = now;
        ticket.UpdatedBy = _currentUserService.UserName;
        ticket.LastModifiedById = actorUserId;

        await ticketRepo.UpdateAsync(ticket);
        await AddStatusHistoryAsync(ticket, fromStatus, targetStatus, notes, actorUserId);

        await AddAuditEventAsync(
            ticket,
            eventType: "StatusChanged",
            title: "Status updated",
            body: $"Status: {fromStatus} → {targetStatus}",
            isInternal: false,
            actorUserId: actorUserId,
            data: new { fromStatus = fromStatus.ToString(), toStatus = targetStatus.ToString() });

        if (!string.IsNullOrWhiteSpace(notes))
        {
            await AddAuditEventAsync(
                ticket,
                eventType: "StatusNotes",
                title: "Transition notes",
                body: notes,
                isInternal: true,
                actorUserId: actorUserId,
                data: new { toStatus = targetStatus.ToString() });
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await PublishTicketTopicAsync(
            tenantId,
            activity: "StatusChanged",
            audience: "Requester",
            ticketId: ticket.Id,
            triggeredByUserId: actorUserId,
            data: new Dictionary<string, object>
            {
                ["ticketId"] = ticket.Id,
                ["ticketNumber"] = ticket.TicketNumber,
                ["fromStatus"] = fromStatus.ToString(),
                ["toStatus"] = targetStatus.ToString(),
                ["requesterUserId"] = ticket.RequesterUserId,
                ["ActionUrl"] = requesterActionUrl
            },
            cancellationToken);

        await PublishTicketTopicAsync(
            tenantId,
            activity: "StatusChanged",
            audience: "Internal",
            ticketId: ticket.Id,
            triggeredByUserId: actorUserId,
            data: new Dictionary<string, object>
            {
                ["ticketId"] = ticket.Id,
                ["ticketNumber"] = ticket.TicketNumber,
                ["fromStatus"] = fromStatus.ToString(),
                ["toStatus"] = targetStatus.ToString(),
                ["assignedToUserId"] = ticket.AssignedToUserId ?? Guid.Empty,
                ["ActionUrl"] = $"/helpdesk/tickets/{ticket.Id}"
            },
            cancellationToken);
    }

    public async Task<IReadOnlyList<EhcTicketAllowedTransitionDto>> GetAllowedTransitionsAsync(Guid ticketId, CancellationToken cancellationToken = default)
    {
        var tenantId = _currentUserService.TenantId ?? Guid.Empty;
        if (tenantId == Guid.Empty)
        {
            throw new InvalidOperationException("Tenant context is required.");
        }

        var ticketRepo = _unitOfWork.Repository<EhcTicket>();
        var ticket = await ticketRepo.GetByIdAsync(ticketId);
        if (ticket == null || ticket.TenantId != tenantId)
        {
            throw new KeyNotFoundException("Ticket not found.");
        }

        var result = new List<EhcTicketAllowedTransitionDto>();

        if (ticket.WorkflowInstanceId.HasValue && ticket.WorkflowInstanceId.Value != Guid.Empty)
        {
            var transitions = await _workflowEngine.GetAvailableTransitionsAsync(ticket.WorkflowInstanceId.Value, new
            {
                ticketId = ticket.Id,
                ticketNumber = ticket.TicketNumber
            });

            foreach (var t in transitions ?? Enumerable.Empty<Core.Entities.Workflow.WorkflowTransition>())
            {
                cancellationToken.ThrowIfCancellationRequested();

                var step = t.ToStep ?? await _workflowStepRepository.GetByIdAsync(t.ToStepId);
                var stepName = step?.Name?.Trim();
                if (string.IsNullOrWhiteSpace(stepName))
                {
                    continue;
                }

                if (!Enum.TryParse<EhcTicketStatus>(stepName, ignoreCase: true, out var status))
                {
                    continue;
                }

                result.Add(new EhcTicketAllowedTransitionDto
                {
                    TransitionId = t.Id,
                    TransitionName = t.Name,
                    TransitionDescription = t.Description,
                    TargetStatus = status
                });
            }
        }

        // Phase 1: allow requesting info from active tickets.
        if (ticket.Status is not (EhcTicketStatus.Resolved or EhcTicketStatus.Closed))
        {
            result.Add(new EhcTicketAllowedTransitionDto
            {
                TransitionName = "Request info (user)",
                TransitionDescription = "Pause the ticket and request more details from the requester.",
                TargetStatus = EhcTicketStatus.PendingUser
            });
            result.Add(new EhcTicketAllowedTransitionDto
            {
                TransitionName = "Request info (3rd party)",
                TransitionDescription = "Pause the ticket while awaiting information from an external party/vendor.",
                TargetStatus = EhcTicketStatus.PendingThirdParty
            });
        }

        // Phase 1: allow resume from pending.
        if (ticket.Status is EhcTicketStatus.PendingUser or EhcTicketStatus.PendingThirdParty)
        {
            result.Add(new EhcTicketAllowedTransitionDto
            {
                TransitionName = "Resume",
                TransitionDescription = "Resume work on the ticket after pending information is received.",
                TargetStatus = EhcTicketStatus.InProgress
            });
        }

        // Phase 1: allow reopen from resolved/closed.
        if (ticket.Status is EhcTicketStatus.Resolved or EhcTicketStatus.Closed)
        {
            result.Add(new EhcTicketAllowedTransitionDto
            {
                TransitionName = "Reopen",
                TransitionDescription = "Reopen the ticket and restart the workflow lifecycle.",
                TargetStatus = EhcTicketStatus.Reopened
            });
        }

        return result
            .OrderBy(x => x.TargetStatus)
            .ThenBy(x => x.TransitionName)
            .ToList();
    }

    public async Task<EhcTicketDetailDto> UpdateTicketRcaAsync(Guid ticketId, UpdateEhcTicketRcaRequestDto request, CancellationToken cancellationToken = default)
    {
        request ??= new UpdateEhcTicketRcaRequestDto();

        var tenantId = _currentUserService.TenantId ?? Guid.Empty;
        if (tenantId == Guid.Empty)
        {
            throw new InvalidOperationException("Tenant context is required.");
        }

        if (!Guid.TryParse(_currentUserService.UserId, out var actorUserId) || actorUserId == Guid.Empty)
        {
            throw new InvalidOperationException("Authenticated user context is required.");
        }

        var ticketRepo = _unitOfWork.Repository<EhcTicket>();
        var ticket = await ticketRepo.FirstOrDefaultAsync(
            t => t.Id == ticketId && t.TenantId == tenantId && !t.IsDeleted,
            t => t.RootCause,
            t => t.Category,
            t => t.Subcategory,
            t => t.AssignedDepartment,
            t => t.AssignedToUser,
            t => t.RequesterUser,
            t => t.Messages!,
            t => t.Attachments!,
            t => t.StatusHistory!,
            t => t.AuditEvents!);

        if (ticket == null)
        {
            throw new KeyNotFoundException("Ticket not found.");
        }

        if (ticket.TicketType != EhcTicketType.Complaint)
        {
            throw new InvalidOperationException("RCA fields are only applicable to Complaint tickets.");
        }

        Guid? rootCauseId = request.RootCauseId.HasValue && request.RootCauseId.Value == Guid.Empty
            ? (Guid?)null
            : request.RootCauseId;

        EhcRootCauseCode? rootCause = null;
        if (rootCauseId.HasValue)
        {
            rootCause = await _unitOfWork.Repository<EhcRootCauseCode>().FirstOrDefaultAsync(
                r => r.Id == rootCauseId.Value && r.TenantId == tenantId && !r.IsDeleted && r.IsActive);

            if (rootCause == null)
            {
                throw new ArgumentException("Selected root cause does not exist.");
            }
        }

        ticket.RootCauseId = rootCauseId;
        ticket.RootCause = rootCause;
        ticket.RootCauseDetails = string.IsNullOrWhiteSpace(request.RootCauseDetails) ? null : request.RootCauseDetails.Trim();
        ticket.ResolutionSummary = string.IsNullOrWhiteSpace(request.ResolutionSummary) ? null : request.ResolutionSummary.Trim();
        ticket.UpdatedAt = DateTime.UtcNow;
        ticket.UpdatedBy = _currentUserService.UserName;
        ticket.LastModifiedById = actorUserId;

        await ticketRepo.UpdateAsync(ticket);

        await AddAuditEventAsync(
            ticket,
            eventType: "RcaUpdated",
            title: "RCA updated",
            body: $"Root cause: {(rootCause == null ? "—" : $"{rootCause.Code} - {rootCause.Name}")}",
            isInternal: true,
            actorUserId: actorUserId,
            data: new
            {
                rootCauseId = rootCauseId,
                rootCauseCode = rootCause?.Code,
                rootCauseName = rootCause?.Name
            });

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await PublishTicketTopicAsync(
            tenantId,
            activity: "Updated",
            audience: "Internal",
            ticketId: ticket.Id,
            triggeredByUserId: actorUserId,
            data: new Dictionary<string, object>
            {
                ["ticketId"] = ticket.Id,
                ["ticketNumber"] = ticket.TicketNumber,
                ["ActionUrl"] = $"/helpdesk/tickets/{ticket.Id}"
            },
            cancellationToken);

        // Return full internal view.
        return await MapToDetailDtoAsync(ticket, includeInternal: true, cancellationToken);
    }

    private async Task<HashSet<EhcTicketStatus>> GetAllowedTransitionsInternalAsync(EhcTicket ticket, CancellationToken cancellationToken)
    {
        var statuses = new HashSet<EhcTicketStatus>();

        if (ticket.WorkflowInstanceId.HasValue && ticket.WorkflowInstanceId.Value != Guid.Empty)
        {
            var transitions = await _workflowEngine.GetAvailableTransitionsAsync(ticket.WorkflowInstanceId.Value, new
            {
                ticketId = ticket.Id,
                ticketNumber = ticket.TicketNumber
            });

            foreach (var t in transitions ?? Enumerable.Empty<Core.Entities.Workflow.WorkflowTransition>())
            {
                cancellationToken.ThrowIfCancellationRequested();

                var step = t.ToStep ?? await _workflowStepRepository.GetByIdAsync(t.ToStepId);
                var stepName = step?.Name?.Trim();
                if (string.IsNullOrWhiteSpace(stepName))
                {
                    continue;
                }

                if (!Enum.TryParse<EhcTicketStatus>(stepName, ignoreCase: true, out var status))
                {
                    continue;
                }

                statuses.Add(status);
            }
        }

        // Phase 1: allow resume from pending
        if (ticket.Status is EhcTicketStatus.PendingUser or EhcTicketStatus.PendingThirdParty)
        {
            statuses.Add(EhcTicketStatus.InProgress);
        }

        return statuses;
    }

    public async Task<EhcTicketMessageDto> AddInternalCommentAsync(Guid ticketId, AddEhcTicketMessageRequestDto request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Body))
        {
            throw new ArgumentException("Message body is required.", nameof(request.Body));
        }

        var tenantId = _currentUserService.TenantId ?? Guid.Empty;
        if (tenantId == Guid.Empty)
        {
            throw new InvalidOperationException("Tenant context is required.");
        }

        if (!Guid.TryParse(_currentUserService.UserId, out var authorUserId) || authorUserId == Guid.Empty)
        {
            throw new InvalidOperationException("Authenticated user context is required.");
        }

        var ticketRepo = _unitOfWork.Repository<EhcTicket>();
        var ticket = await ticketRepo.GetByIdAsync(ticketId);
        if (ticket == null || ticket.TenantId != tenantId)
        {
            throw new KeyNotFoundException("Ticket not found.");
        }

        var now = DateTime.UtcNow;
        var message = new EhcTicketMessage
        {
            TenantId = tenantId,
            TicketId = ticketId,
            Body = request.Body.Trim(),
            IsInternal = true,
            AuthorUserId = authorUserId,
            CreatedAt = now,
            CreatedBy = _currentUserService.UserName,
            CreatedById = authorUserId
        };

        await _unitOfWork.Repository<EhcTicketMessage>().AddAsync(message);

        // Mark first response timestamp when an internal agent first interacts
        if (ticket.FirstRespondedAt == null && ticket.Status is EhcTicketStatus.New or EhcTicketStatus.Reopened)
        {
            ticket.FirstRespondedAt = now;
            await ticketRepo.UpdateAsync(ticket);
        }

        await AddAuditEventAsync(
            ticket,
            eventType: "InternalComment",
            title: "Internal note added",
            body: "An internal note was added.",
            isInternal: true,
            actorUserId: authorUserId,
            data: new { messageId = message.Id });

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new EhcTicketMessageDto
        {
            Id = message.Id,
            Body = message.Body,
            IsInternal = true,
            AuthorUserId = authorUserId,
            AuthorName = _currentUserService.UserName,
            CreatedAt = message.CreatedAt
        };
    }

    private async Task<EhcSlaTemplate?> ResolveSlaTemplateAsync(Guid tenantId, CreateEhcTicketRequestDto request, CancellationToken cancellationToken)
    {
        var templates = (await _unitOfWork.Repository<EhcSlaTemplate>()
                .FindAsync(t => t.TenantId == tenantId && t.IsActive))
            .ToList();

        if (templates.Count == 0)
        {
            return null;
        }

        // Phase 1 scoring: prioritize most specific match.
        var best = templates
            .Select(t => new
            {
                Template = t,
                Score =
                    (t.CategoryId.HasValue && t.CategoryId == request.CategoryId ? 100 : 0) +
                    (t.TicketType.HasValue && t.TicketType == request.TicketType ? 10 : 0) +
                    (t.Priority.HasValue && t.Priority == request.Priority ? 1 : 0)
            })
            .OrderByDescending(x => x.Score)
            .ThenByDescending(x => x.Template.FirstResponseMinutes)
            .FirstOrDefault();

        return best?.Template;
    }

    private async Task AddStatusHistoryAsync(EhcTicket ticket, EhcTicketStatus? from, EhcTicketStatus to, string? notes, Guid actorUserId)
    {
        await _unitOfWork.Repository<EhcTicketStatusHistory>().AddAsync(new EhcTicketStatusHistory
        {
            TenantId = ticket.TenantId,
            TicketId = ticket.Id,
            FromStatus = from,
            ToStatus = to,
            ChangedByUserId = actorUserId,
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
            CreatedAt = DateTime.UtcNow,
            CreatedBy = _currentUserService.UserName,
            CreatedById = actorUserId
        });
    }

    private async Task PauseWorkflowForInformationAsync(EhcTicket ticket, Guid actorUserId, string? notes, CancellationToken cancellationToken)
    {
        if (!ticket.WorkflowInstanceId.HasValue || ticket.WorkflowInstanceId == Guid.Empty)
        {
            return;
        }

        var stepInstance = await _workflowStepInstanceRepository.GetCurrentStepAsync(ticket.WorkflowInstanceId.Value);
        if (stepInstance == null)
        {
            return;
        }

        await _workflowEngine.ProcessStepAsync(
            stepInstance.Id,
            actorUserId,
            WorkflowStepAction.RequestInformation,
            resultData: null,
            comments: notes ?? "Additional information requested");
    }

    private async Task AdvanceWorkflowToStatusAsync(EhcTicket ticket, EhcTicketStatus targetStatus, Guid actorUserId, CancellationToken cancellationToken)
    {
        if (!ticket.WorkflowInstanceId.HasValue || ticket.WorkflowInstanceId == Guid.Empty)
        {
            throw new InvalidOperationException("Ticket has no workflow instance.");
        }

        // Baseline definition assumes step names match the status labels.
        // We advance linearly until we reach the desired step.
        for (var i = 0; i < 10; i++)
        {
            var stepInstance = await _workflowStepInstanceRepository.GetCurrentStepAsync(ticket.WorkflowInstanceId.Value);
            if (stepInstance == null)
            {
                break;
            }

            var step = stepInstance.WorkflowStep ?? await _workflowStepRepository.GetByIdAsync(stepInstance.WorkflowStepId);
            var stepName = step?.Name?.Trim() ?? string.Empty;
            if (string.Equals(stepName, targetStatus.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            await _workflowEngine.ExecuteNextStepAsync(ticket.WorkflowInstanceId.Value, actorUserId, new
            {
                targetStatus = targetStatus.ToString()
            });
        }
    }

    private static EhcTicketListItemDto MapToExternalListItemDto(EhcTicket ticket)
    {
        return new EhcTicketListItemDto
        {
            Id = ticket.Id,
            TicketNumber = ticket.TicketNumber,
            TicketType = ticket.TicketType,
            Priority = ticket.Priority,
            Source = ticket.Source,
            Status = ticket.Status,
            Subject = ticket.Subject,
            CategoryName = ticket.Category?.Name,
            CreatedAt = ticket.CreatedAt,
            UpdatedAt = ticket.UpdatedAt,
            AssignedDepartmentName = null,
            AssignedToName = null,
            RequesterName = null,
            RequesterAuthenticationProvider = null
        };
    }

    private static EhcTicketListItemDto MapToInternalListItemDto(EhcTicket ticket)
    {
        var requesterName = ticket.RequesterUser != null
            ? $"{ticket.RequesterUser.FirstName} {ticket.RequesterUser.LastName}".Trim()
            : null;

        return new EhcTicketListItemDto
        {
            Id = ticket.Id,
            TicketNumber = ticket.TicketNumber,
            TicketType = ticket.TicketType,
            Priority = ticket.Priority,
            Source = ticket.Source,
            Status = ticket.Status,
            Subject = ticket.Subject,
            CategoryName = ticket.Category?.Name,
            CreatedAt = ticket.CreatedAt,
            UpdatedAt = ticket.UpdatedAt,
            AssignedDepartmentName = ticket.AssignedDepartment?.Name,
            AssignedToName = ticket.AssignedToUser != null
                ? $"{ticket.AssignedToUser.FirstName} {ticket.AssignedToUser.LastName}".Trim()
                : null,
            RequesterName = string.IsNullOrWhiteSpace(requesterName) ? ticket.RequesterUser?.UserName : requesterName,
            RequesterAuthenticationProvider = ticket.RequesterUser?.AuthenticationProvider.ToString()
        };
    }

    private async Task<EhcTicketDetailDto> MapToDetailDtoAsync(EhcTicket ticket, bool includeInternal, CancellationToken cancellationToken)
    {
        var audit = (ticket.AuditEvents ?? new List<EhcTicketAuditEvent>())
            .Where(e => includeInternal || !e.IsInternal)
            .OrderBy(e => e.CreatedAt)
            .Select(e => new EhcTicketAuditEventDto
            {
                Id = e.Id,
                EventType = e.EventType,
                Title = e.Title,
                Body = e.Body,
                IsInternal = e.IsInternal,
                ActorUserId = includeInternal ? e.ActorUserId : null,
                ActorName = includeInternal && e.ActorUser != null
                    ? $"{e.ActorUser.FirstName} {e.ActorUser.LastName}".Trim()
                    : null,
                CreatedAt = e.CreatedAt
            })
            .ToList();

        var messages = new List<EhcTicketMessageDto>();
        foreach (var m in (ticket.Messages ?? new List<EhcTicketMessage>())
                     .Where(x => includeInternal || !x.IsInternal)
                     .OrderBy(x => x.CreatedAt))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var msgAttachments = (m.Attachments ?? new List<EhcTicketAttachment>())
                .Where(a => includeInternal || !a.IsInternal)
                .OrderBy(a => a.CreatedAt)
                .ToList();

            var mappedMsgAttachments = msgAttachments.Count == 0
                ? new List<EhcTicketAttachmentDto>()
                : (await Task.WhenAll(msgAttachments.Select(MapToAttachmentDtoAsync))).ToList();

            messages.Add(new EhcTicketMessageDto
            {
                Id = m.Id,
                Body = m.Body,
                IsInternal = m.IsInternal,
                AuthorUserId = m.AuthorUserId,
                AuthorName = m.AuthorUser != null
                    ? $"{m.AuthorUser.FirstName} {m.AuthorUser.LastName}".Trim()
                    : null,
                CreatedAt = m.CreatedAt,
                Attachments = mappedMsgAttachments
            });
        }

        var ticketAttachments = (ticket.Attachments ?? new List<EhcTicketAttachment>())
            .Where(a => includeInternal || !a.IsInternal)
            .OrderBy(a => a.CreatedAt)
            .ToList();

        var attachments = ticketAttachments.Count == 0
            ? new List<EhcTicketAttachmentDto>()
            : (await Task.WhenAll(ticketAttachments.Select(MapToAttachmentDtoAsync))).ToList();

        var history = (ticket.StatusHistory ?? new List<EhcTicketStatusHistory>())
            .OrderBy(h => h.CreatedAt)
            .Select(h => new EhcTicketStatusHistoryDto
            {
                Id = h.Id,
                FromStatus = h.FromStatus,
                ToStatus = h.ToStatus,
                ChangedByUserId = includeInternal ? h.ChangedByUserId : null,
                ChangedByName = includeInternal && h.ChangedByUser != null
                    ? $"{h.ChangedByUser.FirstName} {h.ChangedByUser.LastName}".Trim()
                    : null,
                Notes = h.Notes,
                CreatedAt = h.CreatedAt
            })
            .ToList();

        return new EhcTicketDetailDto
        {
            Id = ticket.Id,
            TicketNumber = ticket.TicketNumber,
            TicketType = ticket.TicketType,
            Priority = ticket.Priority,
            Source = ticket.Source,
            Status = ticket.Status,
            Subject = ticket.Subject,
            Description = ticket.Description,
            CategoryName = ticket.Category?.Name,
            SubcategoryName = ticket.Subcategory?.Name,
            RelatedEntityType = ticket.RelatedEntityType,
            RelatedEntityReference = ticket.RelatedEntityReference,
            CreatedAt = ticket.CreatedAt,
            UpdatedAt = ticket.UpdatedAt,
            CreatedBy = includeInternal ? ticket.CreatedBy : null,
            UpdatedBy = includeInternal ? ticket.UpdatedBy : null,
            FirstResponseDueAt = includeInternal ? ticket.FirstResponseDueAt : null,
            ResolutionDueAt = includeInternal ? ticket.ResolutionDueAt : null,
            FirstRespondedAt = includeInternal ? ticket.FirstRespondedAt : null,
            ResolvedAt = ticket.ResolvedAt,
            ClosedAt = ticket.ClosedAt,
            AssignedDepartmentId = includeInternal ? ticket.AssignedDepartmentId : null,
            AssignedDepartmentName = includeInternal ? ticket.AssignedDepartment?.Name : null,
            AssignedToUserId = includeInternal ? ticket.AssignedToUserId : null,
            AssignedToName = includeInternal && ticket.AssignedToUser != null
                ? $"{ticket.AssignedToUser.FirstName} {ticket.AssignedToUser.LastName}".Trim()
                : null,
            RequesterName = includeInternal
                ? (ticket.RequesterUser != null ? $"{ticket.RequesterUser.FirstName} {ticket.RequesterUser.LastName}".Trim() : null)
                : null,
            RequesterEmail = includeInternal ? ticket.RequesterUser?.Email : null,
            RequesterAuthenticationProvider = includeInternal ? ticket.RequesterUser?.AuthenticationProvider.ToString() : null,
            RootCauseId = includeInternal ? ticket.RootCauseId : null,
            RootCauseCode = includeInternal ? ticket.RootCause?.Code : null,
            RootCauseName = includeInternal ? ticket.RootCause?.Name : null,
            RootCauseDetails = includeInternal ? ticket.RootCauseDetails : null,
            ResolutionSummary = includeInternal ? ticket.ResolutionSummary : null,
            Messages = messages,
            Attachments = attachments,
            StatusHistory = history,
            AuditTrail = audit
        };
    }

    private async Task<EhcTicketAttachmentDto> MapToAttachmentDtoAsync(EhcTicketAttachment attachment)
    {
        string? publicUrl = null;
        try
        {
            publicUrl = await _fileStorageService.GetPublicUrlAsync(attachment.FilePath);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to resolve public URL for EHC attachment {AttachmentId}", attachment.Id);
        }

        return new EhcTicketAttachmentDto
        {
            Id = attachment.Id,
            FilePath = attachment.FilePath,
            PublicUrl = publicUrl,
            FileName = attachment.FileName,
            ContentType = attachment.ContentType,
            FileSize = attachment.FileSize,
            IsInternal = attachment.IsInternal,
            CreatedAt = attachment.CreatedAt,
            CreatedBy = attachment.CreatedBy
        };
    }
}
