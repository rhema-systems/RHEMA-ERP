using System.Net;
using System.Net.Mail;
using System.Text.Json;
using ErpSystem.Core.DTOs.Inventory;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.Inventory;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Core.Services.Inventory;

public partial class PhysicalCountService
{
    public async Task<IReadOnlyList<PhysicalCountCounterOptionDto>> GetCounterOptionsAsync(
        Guid warehouseId, Guid? locationId, string? search, CancellationToken cancellationToken = default)
    {
        await EnsureAccessAsync("procurement.inventory.count", warehouseId, locationId, "physical-count-counter-options");
        var term = (search ?? string.Empty).Trim();
        if (term.Length > 100) throw new ArgumentException("Employee search cannot exceed 100 characters.");
        var employees = await _unitOfWork.Repository<Employee>().GetQueryable(x => x.TenantId == RequiredTenantId() &&
            !x.IsDeleted && x.IsActive && (term == "" || x.EmployeeNumber.Contains(term) || x.FirstName.Contains(term) || x.LastName.Contains(term)))
            .AsNoTracking().OrderBy(x => x.EmployeeNumber).Take(50).ToListAsync(cancellationToken);
        return (await ResolveCounterCandidatesAsync(employees, cancellationToken)).Select(x => x.Option).ToList();
    }

    private sealed record CounterCandidate(PhysicalCountCounterOptionDto Option, string? Email);

    private async Task<List<CounterCandidate>> ResolveCounterCandidatesAsync(List<Employee> employees, CancellationToken cancellationToken = default)
    {
        var ids = employees.Select(x => x.Id).ToList();
        var now = DateTime.UtcNow;
        var links = await _unitOfWork.Repository<UserTenant>().GetQueryable(x => x.TenantId == RequiredTenantId() &&
                !x.IsDeleted && x.Status == UserTenantStatus.Active && (!x.ExpiresAt.HasValue || x.ExpiresAt > now) &&
                x.User.IsActive && x.User.EmployeeId.HasValue && ids.Contains(x.User.EmployeeId.Value))
            .Select(x => new { x.UserId, EmployeeId = x.User.EmployeeId!.Value, x.User.Email }).Distinct().ToListAsync(cancellationToken);
        return employees.Select(employee => {
            var users = links.Where(x => x.EmployeeId == employee.Id).ToList();
            var user = users.Count == 1 ? users[0] : null;
            var email = ValidCounterEmail(employee.EmailAddress) ?? ValidCounterEmail(user?.Email);
            var reason = users.Count != 1 ? "Employee must have exactly one active linked user in this tenant."
                : email == null ? "Employee or linked user needs a valid email address for count notifications." : null;
            return new CounterCandidate(new PhysicalCountCounterOptionDto {
                EmployeeId = employee.Id, EmployeeNumber = employee.EmployeeNumber,
                EmployeeName = $"{employee.FirstName} {employee.LastName}".Trim(), UserId = user?.UserId,
                HasEmail = email != null, CanAssign = reason == null, IneligibilityReason = reason,
            }, email);
        }).ToList();
    }

    private static string? ValidCounterEmail(string? value) =>
        !string.IsNullOrWhiteSpace(value) && MailAddress.TryCreate(value.Trim(), out var address) ? address.Address : null;

    public Task<bool> AssignCountersAsync(Guid countId, Guid userId, AssignPhysicalCountCountersRequest request) =>
        InCountTransactionAsync(countId, async () => {
            EnsureActor(userId);
            var count = await _countRepository.GetWithItemsAsync(countId) ?? throw new ArgumentException("Count not found.");
            await EnsureAccessAsync(count, "procurement.inventory.count");
            var ids = request.EmployeeIds.Distinct().OrderBy(x => x).ToList();
            var recovering = count.Status != "Draft";
            if (recovering)
            {
                await EnsureAccessAsync(count, "procurement.inventory.adjust.approve");
                EnsureIndependentActor(count, userId, includeFinance: false, includeAudit: false);
            }
            var role = recovering ? "LegacyCommitteeRecovery" : "CounterAssignment";
            if (await ReplayCountActionAsync(count.Id, PhysicalCountActionType.CountersAssigned, request.IdempotencyKey,
                userId, role, request.Comment, new { EmployeeIds = ids })) return true;
            EnsureRowVersion(count.RowVersion, request.RowVersion, "The count changed. Reload before assigning counters.");
            if (recovering)
            {
                if (!await IsLegacyCommitteeRecoveryAsync(count))
                    throw new InvalidOperationException("Counters can only be changed in Draft, or assigned once to a submitted legacy count under investigation before its first recount.");
                Required(request.Comment, "Record the investigation reason for assigning the legacy count committee.", 1000);
                await AddCountActionAsync(count, PhysicalCountActionType.CountersAssigned, userId, request.IdempotencyKey,
                    request.Comment, new { EmployeeIds = ids }, role, request.CorrelationId);
                await _unitOfWork.SaveChangesAsync();
            }
            await ReplaceCountersCoreAsync(count, ids, userId, request.Comment);
            if (!recovering)
                await AddCountActionAsync(count, PhysicalCountActionType.CountersAssigned, userId, request.IdempotencyKey,
                    request.Comment, new { EmployeeIds = ids }, role, request.CorrelationId);
            count.UpdatedAt = DateTime.UtcNow;
            count.LastModifiedById = userId;
            await _countRepository.UpdateAsync(count);
            await _unitOfWork.SaveChangesAsync();
            return true;
        });

    private async Task<bool> IsLegacyCommitteeRecoveryAsync(PhysicalCount count) =>
        count.Status == "UnderInvestigation" && count.ObservationSubmittedAtUtc.HasValue &&
        !count.RootPhysicalCountId.HasValue && !count.ParentPhysicalCountId.HasValue &&
        !count.StockAdjustmentId.HasValue && count.Counters.Count == 0 &&
        !await _countRepository.GetQueryable(x => x.TenantId == count.TenantId && x.RootPhysicalCountId == count.Id).AnyAsync();

    private async Task<bool> CanManageCountersAsync(PhysicalCount count)
    {
        if (!await CanAccessAsync(count, "procurement.inventory.count")) return false;
        if (count.Status == "Draft") return true;
        if (!await IsLegacyCommitteeRecoveryAsync(count) || !await CanAccessAsync(count, "procurement.inventory.adjust.approve")) return false;
        if (!Guid.TryParse(_currentUserService.UserId, out var actor)) return false;
        try { EnsureIndependentActor(count, actor, includeFinance: false, includeAudit: false); return true; }
        catch (InvalidOperationException) { return false; }
    }

    private async Task ReplaceCountersCoreAsync(PhysicalCount count, IReadOnlyCollection<Guid> employeeIds, Guid actor, string? reason)
    {
        if (employeeIds.Count is < 1 or > 100 || employeeIds.Contains(Guid.Empty))
            throw new ArgumentException("Select between one and 100 employees for the counting committee.");
        var ids = employeeIds.Distinct().ToList();
        var employees = await _unitOfWork.Repository<Employee>().GetQueryable(x => x.TenantId == count.TenantId &&
            !x.IsDeleted && x.IsActive && ids.Contains(x.Id)).ToListAsync();
        if (employees.Count != ids.Count) throw new ArgumentException("A selected employee is inactive or does not belong to this tenant.");
        var candidates = await ResolveCounterCandidatesAsync(employees);
        if (count.Status != "Draft" && candidates.Any(x => x.Option.UserId == actor))
            throw new InvalidOperationException("The investigator assigning a recovery committee cannot join that committee.");
        var invalid = candidates.FirstOrDefault(x => !x.Option.CanAssign);
        if (invalid != null) throw new ArgumentException($"{invalid.Option.EmployeeNumber}: {invalid.Option.IneligibilityReason}");
        var now = DateTime.UtcNow;
        var repository = _unitOfWork.Repository<PhysicalCountCounter>();
        foreach (var previous in count.Counters.Where(x => x.IsActive && !x.IsDeleted && (!ids.Contains(x.EmployeeId) ||
            candidates.Any(candidate => candidate.Option.EmployeeId == x.EmployeeId && candidate.Option.UserId != x.UserId))))
        {
            previous.IsActive = false;
            previous.RemovedById = actor;
            previous.RemovedAtUtc = now;
            previous.ChangeReason = Normalize(reason, 1000);
            await repository.UpdateAsync(previous);
        }
        // Persist removals before inserting replacements under the same count transaction and unique active index.
        await _unitOfWork.SaveChangesAsync();
        foreach (var candidate in candidates)
        {
            if (count.Counters.Any(x => x.IsActive && !x.IsDeleted && x.EmployeeId == candidate.Option.EmployeeId)) continue;
            var assignment = new PhysicalCountCounter {
                Id = Guid.NewGuid(), TenantId = count.TenantId, PhysicalCountId = count.Id,
                EmployeeId = candidate.Option.EmployeeId, UserId = candidate.Option.UserId!.Value,
                EmployeeNumber = candidate.Option.EmployeeNumber, EmployeeName = candidate.Option.EmployeeName,
                EmailAddress = candidate.Email, AssignedById = actor, AssignedAtUtc = now,
                CreatedById = actor, ChangeReason = Normalize(reason, 1000),
            };
            await QueueCounterAssignmentAsync(count, assignment);
            count.Counters.Add(assignment);
            await repository.AddAsync(assignment);
        }
    }

    private async Task QueueCounterAssignmentAsync(PhysicalCount count, PhysicalCountCounter counter)
    {
        // The existing notification worker owns email delivery. Both queue records commit with the assignment.
        var path = $"/inventory/physical-counts?recordId={count.Id}";
        if (!Uri.TryCreate(_counterNotificationConfiguration?["FrontendUrl"], UriKind.Absolute, out var frontend) ||
            (frontend.Scheme != "http" && frontend.Scheme != "https"))
            throw new InvalidOperationException("FrontendUrl must be configured before emailing count assignments.");
        var actionUrl = new Uri(frontend, path).AbsoluteUri;
        var title = $"Stock count assignment: {count.CountNumber}";
        var warehouse = count.Warehouse?.Name ?? (await _warehouseRepository.GetByIdAsync(count.WarehouseId))?.Name ?? count.WarehouseId.ToString();
        var message = $"You are assigned to stock count {count.CountNumber} at {warehouse}, dated {count.CountDate:yyyy-MM-dd}. " +
            $"Assigned by {_currentUserService.FullName}. Open the count to review its scope and record the physical quantities when counting begins.";
        if (count.RootPhysicalCountId.HasValue)
            message += $" Recount attempt {count.RecountAttempt}. " + string.Join("; ", count.Items.Select(x => $"{x.ItemCode}: {x.RecountReason}"));
        var notifications = _unitOfWork.Repository<Notification>();
        var inApp = new Notification { Id = Guid.NewGuid(), TenantId = count.TenantId, RecipientId = counter.UserId,
            NotificationType = "PhysicalCountCounterAssigned", Title = title, Message = message,
            DeliveryMethods = "InApp", Status = "Pending", EntityType = "PhysicalCount", EntityId = count.Id, ActionUrl = path,
            AdditionalData = JsonSerializer.Serialize(new { counterAssignmentId = counter.Id }) };
        var emailBody = WebUtility.HtmlEncode(message) + $"<p><a href=\"{WebUtility.HtmlEncode(actionUrl)}\">Open stock count</a></p>";
        var email = new Notification { Id = Guid.NewGuid(), TenantId = count.TenantId, RecipientId = Guid.Empty,
            NotificationType = "PhysicalCountCounterAssigned", Title = title, Message = emailBody,
            DeliveryMethods = "Email", EmailAddress = counter.EmailAddress, Status = "Pending",
            EntityType = "PhysicalCount", EntityId = count.Id,
            AdditionalData = JsonSerializer.Serialize(new { email = new { isHtml = true, subject = title, bodyHtml = emailBody, textBody = message + " " + actionUrl }, counterAssignmentId = counter.Id }) };
        await notifications.AddAsync(inApp);
        await notifications.AddAsync(email);
        counter.InAppNotificationId = inApp.Id;
        counter.EmailNotificationId = email.Id;
    }

    private async Task EnsureCurrentCounterIdentityAsync(PhysicalCount count, Guid userId)
    {
        if (count.Counters.Count == 0) return; // Historical single-counter counts preserve their original contract.
        var employeeIds = count.Counters.Where(x => x.IsActive && !x.IsDeleted && x.TenantId == count.TenantId && x.UserId == userId)
            .Select(x => x.EmployeeId).ToList();
        var now = DateTime.UtcNow;
        var activeEmployeeIds = _unitOfWork.Repository<Employee>().GetQueryable(x => x.TenantId == count.TenantId &&
            !x.IsDeleted && x.IsActive && employeeIds.Contains(x.Id)).Select(x => x.Id);
        if (!await _unitOfWork.Repository<UserTenant>().GetQueryable(x => x.TenantId == count.TenantId && x.UserId == userId &&
                !x.IsDeleted && x.Status == UserTenantStatus.Active && (!x.ExpiresAt.HasValue || x.ExpiresAt > now) &&
                x.User.IsActive && x.User.EmployeeId.HasValue && activeEmployeeIds.Contains(x.User.EmployeeId.Value)).AnyAsync())
            throw new UnauthorizedAccessException("The assigned employee no longer has this active linked user. Ask the count manager to review the assignment.");
    }

    private static bool IsAssignedCounter(PhysicalCount count, Guid userId) => count.Counters.Count == 0
        ? count.CountedById == userId
        : count.Counters.Any(x => !x.IsDeleted && x.IsActive && x.UserId == userId && x.TenantId == count.TenantId);

    private static bool WasCounter(PhysicalCount count, Guid userId) => count.CountedById == userId ||
        count.Counters.Any(x => x.UserId == userId && x.TenantId == count.TenantId) || count.Items.Any(x => x.CountedById == userId);

    private static List<PhysicalCountCounterDto> MapCounters(PhysicalCount count) => count.Counters.OrderBy(x => x.AssignedAtUtc).Select(x => new PhysicalCountCounterDto {
        Id = x.Id, EmployeeId = x.EmployeeId, EmployeeNumber = x.EmployeeNumber, EmployeeName = x.EmployeeName,
        UserId = x.UserId, IsActive = x.IsActive && !x.IsDeleted, AssignedById = x.AssignedById, AssignedAtUtc = x.AssignedAtUtc,
        RemovedById = x.RemovedById, RemovedAtUtc = x.RemovedAtUtc, ChangeReason = x.ChangeReason,
        EmailNotificationQueued = x.EmailNotificationId.HasValue,
    }).ToList();
}
