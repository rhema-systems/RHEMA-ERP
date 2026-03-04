using System.Text.Json;
using System.Text.RegularExpressions;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Procurement;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.Notifications;

public class NotificationTopicPublisher : INotificationTopicPublisher
{
    private static readonly Regex TokenRegex = new(@"\{\{\s*([A-Za-z0-9_.-]+)\s*\}\}", RegexOptions.Compiled);

    private readonly IUnitOfWork _unitOfWork;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IBusinessPartnerRepository _businessPartnerRepository;
    private readonly IBusinessPartnerUserRepository _businessPartnerUserRepository;
    private readonly ILogger<NotificationTopicPublisher> _logger;

    public NotificationTopicPublisher(
        IUnitOfWork unitOfWork,
        UserManager<ApplicationUser> userManager,
        IBusinessPartnerRepository businessPartnerRepository,
        IBusinessPartnerUserRepository businessPartnerUserRepository,
        ILogger<NotificationTopicPublisher> logger)
    {
        _unitOfWork = unitOfWork;
        _userManager = userManager;
        _businessPartnerRepository = businessPartnerRepository;
        _businessPartnerUserRepository = businessPartnerUserRepository;
        _logger = logger;
    }

    public async Task PublishAsync(NotificationTopicEvent evt, CancellationToken cancellationToken = default)
    {
        if (evt == null) throw new ArgumentNullException(nameof(evt));
        if (evt.TenantId == Guid.Empty) throw new InvalidOperationException("TenantId is required");

        var key = (evt.TopicKey ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new InvalidOperationException("TopicKey is required");
        }

        var topicRepo = _unitOfWork.Repository<NotificationTopic>();
        var topic = await topicRepo.FirstOrDefaultAsync(
            t => t.TenantId == evt.TenantId && t.Key == key && !t.IsDeleted,
            t => t.Recipients,
            t => t.EmailTemplate);

        if (topic == null || !topic.IsActive)
        {
            return;
        }

        var recipients = (topic.Recipients ?? new List<NotificationTopicRecipient>())
            .Where(r => !r.IsDeleted)
            .ToList();

        if (recipients.Count == 0)
        {
            return;
        }

        var data = evt.Data ?? new Dictionary<string, object>();

        var inAppUserIds = new HashSet<Guid>();
        var emailAddresses = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var smsNumbers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Resolve recipients.
        foreach (var rule in recipients)
        {
            var kind = (rule.RecipientKind ?? string.Empty).Trim();
            var value = (rule.RecipientValue ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(kind) || string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            if (!topic.EnableInApp && !topic.EnableEmail && !topic.EnableSms)
            {
                continue;
            }

            var wantInApp = topic.EnableInApp && rule.SendInApp;
            var wantEmail = topic.EnableEmail && rule.SendEmail;
            var wantSms = topic.EnableSms && rule.SendSms;

            if (!wantInApp && !wantEmail && !wantSms)
            {
                continue;
            }

            if (string.Equals(kind, "User", StringComparison.OrdinalIgnoreCase))
            {
                if (!Guid.TryParse(value, out var userId) || userId == Guid.Empty)
                    continue;

                if (wantInApp)
                    inAppUserIds.Add(userId);

                if (wantEmail || wantSms)
                {
                    var user = await _userManager.FindByIdAsync(userId.ToString());
                    if (user != null && user.IsActive && user.TenantId == evt.TenantId)
                    {
                        if (wantEmail && !string.IsNullOrWhiteSpace(user.Email))
                            emailAddresses.Add(user.Email);
                        if (wantSms && !string.IsNullOrWhiteSpace(user.PhoneNumber))
                            smsNumbers.Add(user.PhoneNumber);
                    }
                }

                continue;
            }

            if (string.Equals(kind, "Role", StringComparison.OrdinalIgnoreCase))
            {
                var users = await _userManager.GetUsersInRoleAsync(value);
                foreach (var user in users.Where(u => u.IsActive && u.TenantId == evt.TenantId))
                {
                    if (wantInApp)
                        inAppUserIds.Add(user.Id);

                    if (wantEmail && !string.IsNullOrWhiteSpace(user.Email))
                        emailAddresses.Add(user.Email);

                    if (wantSms && !string.IsNullOrWhiteSpace(user.PhoneNumber))
                        smsNumbers.Add(user.PhoneNumber);
                }

                continue;
            }

            if (string.Equals(kind, "UserFromData", StringComparison.OrdinalIgnoreCase))
            {
                if (!TryGetGuidFromData(data, value, out var userId))
                    continue;

                var user = await _userManager.FindByIdAsync(userId.ToString());
                if (user == null || !user.IsActive || user.TenantId != evt.TenantId)
                    continue;

                if (wantInApp)
                    inAppUserIds.Add(user.Id);

                if (wantEmail && !string.IsNullOrWhiteSpace(user.Email))
                    emailAddresses.Add(user.Email);

                if (wantSms && !string.IsNullOrWhiteSpace(user.PhoneNumber))
                    smsNumbers.Add(user.PhoneNumber);

                continue;
            }

            if (string.Equals(kind, "UserFromEmployeeIdData", StringComparison.OrdinalIgnoreCase))
            {
                if (!TryGetGuidFromData(data, value, out var employeeId))
                    continue;

                var users = await _userManager.Users
                    .Where(u => u.TenantId == evt.TenantId && u.IsActive && u.EmployeeId == employeeId)
                    .ToListAsync(cancellationToken);

                foreach (var user in users)
                {
                    if (wantInApp)
                        inAppUserIds.Add(user.Id);

                    if (wantEmail && !string.IsNullOrWhiteSpace(user.Email))
                        emailAddresses.Add(user.Email);

                    if (wantSms && !string.IsNullOrWhiteSpace(user.PhoneNumber))
                        smsNumbers.Add(user.PhoneNumber);
                }

                continue;
            }

            if (string.Equals(kind, "UsersFromData", StringComparison.OrdinalIgnoreCase))
            {
                if (!TryGetGuidsFromData(data, value, out var userIds))
                    continue;

                foreach (var uid in userIds)
                {
                    var user = await _userManager.FindByIdAsync(uid.ToString());
                    if (user == null || !user.IsActive || user.TenantId != evt.TenantId)
                        continue;

                    if (wantInApp)
                        inAppUserIds.Add(user.Id);

                    if (wantEmail && !string.IsNullOrWhiteSpace(user.Email))
                        emailAddresses.Add(user.Email);

                    if (wantSms && !string.IsNullOrWhiteSpace(user.PhoneNumber))
                        smsNumbers.Add(user.PhoneNumber);
                }

                continue;
            }

            if (string.Equals(kind, "RoleFromData", StringComparison.OrdinalIgnoreCase))
            {
                if (!wantInApp && !wantEmail)
                    continue;

                if (!data.TryGetValue(value, out var raw) || raw == null)
                    continue;

                var roleName = (raw.ToString() ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(roleName))
                    continue;

                var users = await _userManager.GetUsersInRoleAsync(roleName);
                foreach (var user in users.Where(u => u.IsActive && u.TenantId == evt.TenantId))
                {
                    if (wantInApp)
                        inAppUserIds.Add(user.Id);

                    if (wantEmail && !string.IsNullOrWhiteSpace(user.Email))
                        emailAddresses.Add(user.Email);

                    if (wantSms && !string.IsNullOrWhiteSpace(user.PhoneNumber))
                        smsNumbers.Add(user.PhoneNumber);
                }

                continue;
            }

            if (string.Equals(kind, "BusinessPartner", StringComparison.OrdinalIgnoreCase))
            {
                if (!Guid.TryParse(value, out var businessPartnerId) || businessPartnerId == Guid.Empty)
                    continue;

                var bp = await _businessPartnerRepository.GetByIdAsync(businessPartnerId);
                if (bp == null || bp.IsDeleted || bp.TenantId != evt.TenantId)
                    continue;

                if (wantInApp)
                {
                    if (bp.UserId.HasValue && bp.UserId.Value != Guid.Empty)
                        inAppUserIds.Add(bp.UserId.Value);

                    try
                    {
                        var subUsers = await _businessPartnerUserRepository.GetActiveByBusinessPartnerIdAsync(bp.Id);
                        foreach (var u in subUsers)
                            inAppUserIds.Add(u.UserId);
                    }
                    catch
                    {
                        // Best-effort.
                    }
                }

                if (wantEmail)
                {
                    if (!string.IsNullOrWhiteSpace(bp.PrimaryEmail))
                        emailAddresses.Add(bp.PrimaryEmail);

                    var portalUserIds = new HashSet<Guid>();
                    if (bp.UserId.HasValue && bp.UserId.Value != Guid.Empty)
                        portalUserIds.Add(bp.UserId.Value);

                    try
                    {
                        var subUsers = await _businessPartnerUserRepository.GetActiveByBusinessPartnerIdAsync(bp.Id);
                        foreach (var u in subUsers)
                            portalUserIds.Add(u.UserId);
                    }
                    catch
                    {
                        // Best-effort.
                    }

                    foreach (var uid in portalUserIds)
                    {
                        var user = await _userManager.FindByIdAsync(uid.ToString());
                        if (user != null && user.IsActive && user.TenantId == evt.TenantId && !string.IsNullOrWhiteSpace(user.Email))
                            emailAddresses.Add(user.Email);
                    }
                }

                if (wantSms)
                {
                    if (!string.IsNullOrWhiteSpace(bp.PrimaryPhone))
                        smsNumbers.Add(bp.PrimaryPhone);

                    var portalUserIds = new HashSet<Guid>();
                    if (bp.UserId.HasValue && bp.UserId.Value != Guid.Empty)
                        portalUserIds.Add(bp.UserId.Value);

                    try
                    {
                        var subUsers = await _businessPartnerUserRepository.GetActiveByBusinessPartnerIdAsync(bp.Id);
                        foreach (var u in subUsers)
                            portalUserIds.Add(u.UserId);
                    }
                    catch
                    {
                        // Best-effort.
                    }

                    foreach (var uid in portalUserIds)
                    {
                        var user = await _userManager.FindByIdAsync(uid.ToString());
                        if (user != null && user.IsActive && user.TenantId == evt.TenantId && !string.IsNullOrWhiteSpace(user.PhoneNumber))
                            smsNumbers.Add(user.PhoneNumber);
                    }
                }

                continue;
            }

            if (string.Equals(kind, "BusinessPartnerFromData", StringComparison.OrdinalIgnoreCase))
            {
                if (!TryGetGuidFromData(data, value, out var businessPartnerId))
                    continue;

                var bp = await _businessPartnerRepository.GetByIdAsync(businessPartnerId);
                if (bp == null || bp.IsDeleted || bp.TenantId != evt.TenantId)
                    continue;

                if (wantInApp)
                {
                    if (bp.UserId.HasValue && bp.UserId.Value != Guid.Empty)
                        inAppUserIds.Add(bp.UserId.Value);

                    try
                    {
                        var subUsers = await _businessPartnerUserRepository.GetActiveByBusinessPartnerIdAsync(bp.Id);
                        foreach (var u in subUsers)
                            inAppUserIds.Add(u.UserId);
                    }
                    catch
                    {
                        // Best-effort.
                    }
                }

                if (wantEmail)
                {
                    if (!string.IsNullOrWhiteSpace(bp.PrimaryEmail))
                        emailAddresses.Add(bp.PrimaryEmail);

                    // Also include portal users' emails (best-effort).
                    var portalUserIds = new HashSet<Guid>();
                    if (bp.UserId.HasValue && bp.UserId.Value != Guid.Empty)
                        portalUserIds.Add(bp.UserId.Value);

                    try
                    {
                        var subUsers = await _businessPartnerUserRepository.GetActiveByBusinessPartnerIdAsync(bp.Id);
                        foreach (var u in subUsers)
                            portalUserIds.Add(u.UserId);
                    }
                    catch
                    {
                        // Best-effort.
                    }

                    foreach (var uid in portalUserIds)
                    {
                        var user = await _userManager.FindByIdAsync(uid.ToString());
                        if (user != null && user.IsActive && user.TenantId == evt.TenantId && !string.IsNullOrWhiteSpace(user.Email))
                            emailAddresses.Add(user.Email);
                    }
                }

                if (wantSms)
                {
                    if (!string.IsNullOrWhiteSpace(bp.PrimaryPhone))
                        smsNumbers.Add(bp.PrimaryPhone);

                    var portalUserIds = new HashSet<Guid>();
                    if (bp.UserId.HasValue && bp.UserId.Value != Guid.Empty)
                        portalUserIds.Add(bp.UserId.Value);

                    try
                    {
                        var subUsers = await _businessPartnerUserRepository.GetActiveByBusinessPartnerIdAsync(bp.Id);
                        foreach (var u in subUsers)
                            portalUserIds.Add(u.UserId);
                    }
                    catch
                    {
                        // Best-effort.
                    }

                    foreach (var uid in portalUserIds)
                    {
                        var user = await _userManager.FindByIdAsync(uid.ToString());
                        if (user != null && user.IsActive && user.TenantId == evt.TenantId && !string.IsNullOrWhiteSpace(user.PhoneNumber))
                            smsNumbers.Add(user.PhoneNumber);
                    }
                }

                continue;
            }

            if (string.Equals(kind, "EmailFromData", StringComparison.OrdinalIgnoreCase))
            {
                if (!wantEmail)
                    continue;

                if (!data.TryGetValue(value, out var raw) || raw == null)
                    continue;

                foreach (var email in ParseEmails(raw))
                {
                    emailAddresses.Add(email);
                }

                continue;
            }

            if (string.Equals(kind, "DepartmentType", StringComparison.OrdinalIgnoreCase))
            {
                if (!wantInApp && !wantEmail && !wantSms)
                    continue;

                if (!TryParseDepartmentType(value, out var deptType))
                    continue;

                // Resolve all active employees in the requested department type, then map to active users by EmployeeId.
                var employeeIds = await _unitOfWork.Repository<Employee>()
                    .GetQueryable(e =>
                        e.TenantId == evt.TenantId &&
                        !e.IsDeleted &&
                        e.IsActive &&
                        e.Department != null &&
                        e.Department.DepartmentType == deptType)
                    .Select(e => e.Id)
                    .ToListAsync(cancellationToken);

                if (employeeIds.Count == 0)
                    continue;

                var users = await _userManager.Users
                    .Where(u =>
                        u.TenantId == evt.TenantId &&
                        u.IsActive &&
                        u.EmployeeId.HasValue &&
                        employeeIds.Contains(u.EmployeeId.Value))
                    .ToListAsync(cancellationToken);

                foreach (var user in users)
                {
                    if (wantInApp)
                        inAppUserIds.Add(user.Id);

                    if (wantEmail && !string.IsNullOrWhiteSpace(user.Email))
                        emailAddresses.Add(user.Email);

                    if (wantSms && !string.IsNullOrWhiteSpace(user.PhoneNumber))
                        smsNumbers.Add(user.PhoneNumber);
                }
            }
        }

        if (inAppUserIds.Count == 0 && emailAddresses.Count == 0 && smsNumbers.Count == 0)
        {
            return;
        }

        // Build content.
        var titleTemplate = string.IsNullOrWhiteSpace(topic.InAppTitleTemplate) ? topic.Name : topic.InAppTitleTemplate;
        var bodyTemplate = string.IsNullOrWhiteSpace(topic.InAppBodyTemplate) ? topic.Description : topic.InAppBodyTemplate;

        var title = RenderTemplate(titleTemplate ?? string.Empty, data);
        var message = RenderTemplate(bodyTemplate ?? string.Empty, data);
        var smsTemplate = string.IsNullOrWhiteSpace(topic.SmsBodyTemplate) ? message : topic.SmsBodyTemplate;
        var smsMessage = RenderTemplate(smsTemplate ?? string.Empty, data);

        if (string.IsNullOrWhiteSpace(title)) title = topic.Name;
        if (string.IsNullOrWhiteSpace(message)) message = topic.Description ?? string.Empty;
        if (string.IsNullOrWhiteSpace(smsMessage)) smsMessage = message;

        var actionUrl = ResolveActionUrl(topic, data);

        var inAppData = new Dictionary<string, object>(data)
        {
            ["TopicKey"] = topic.Key,
        };

        if (!string.IsNullOrWhiteSpace(evt.EntityType))
            inAppData["EntityType"] = evt.EntityType!;
        if (evt.EntityId.HasValue && evt.EntityId.Value != Guid.Empty)
            inAppData["EntityId"] = evt.EntityId.Value;
        if (!string.IsNullOrWhiteSpace(actionUrl))
            inAppData["ActionUrl"] = actionUrl!;

        // In-app notifications.
        // Deliver via the durable notification queue (Notification table) so email/in-app can be retried by the dispatcher
        // and so publishing doesn't block the caller.
        var queued = new List<Notification>();
        var notificationRepo = _unitOfWork.Repository<Notification>();
        var now = DateTime.UtcNow;

        if (topic.EnableInApp && inAppUserIds.Count > 0)
        {
            foreach (var userId in inAppUserIds)
            {
                queued.Add(new Notification
                {
                    Id = Guid.NewGuid(),
                    TenantId = evt.TenantId,
                    RecipientId = userId,
                    NotificationType = "Topic",
                    Title = title,
                    Message = message,
                    Priority = "Normal",
                    Status = "Pending",
                    IsRead = false,
                    ScheduledFor = now,
                    SentAt = null,
                    AttemptCount = 0,
                    LastError = null,
                    DeliveryMethods = "InApp",
                    EntityType = evt.EntityType,
                    EntityId = evt.EntityId,
                    ActionUrl = string.IsNullOrWhiteSpace(actionUrl) ? null : actionUrl,
                    AdditionalData = JsonSerializer.Serialize(inAppData)
                });
            }
        }

        if (topic.EnableEmail && emailAddresses.Count > 0)
        {
            var (subject, htmlBody) = BuildEmail(topic, data, fallbackSubject: title, fallbackBody: message);
            var emailPayload = new
            {
                email = new
                {
                    isHtml = true,
                    attachmentsOmitted = true,
                    attachments = Array.Empty<object>()
                },
                topic = new
                {
                    key = topic.Key
                }
            };

            var additional = JsonSerializer.Serialize(emailPayload);

            foreach (var email in emailAddresses)
            {
                queued.Add(new Notification
                {
                    Id = Guid.NewGuid(),
                    TenantId = evt.TenantId,
                    RecipientId = Guid.Empty,
                    NotificationType = "Email",
                    Title = subject,
                    Message = htmlBody,
                    Priority = "Normal",
                    Status = "Pending",
                    IsRead = false,
                    ScheduledFor = now,
                    SentAt = null,
                    AttemptCount = 0,
                    LastError = null,
                    DeliveryMethods = "Email",
                    EmailAddress = email,
                    EntityType = evt.EntityType,
                    EntityId = evt.EntityId,
                    ActionUrl = null,
                    AdditionalData = additional
                });
            }
        }

        if (topic.EnableSms && smsNumbers.Count > 0)
        {
            foreach (var phone in smsNumbers)
            {
                queued.Add(new Notification
                {
                    Id = Guid.NewGuid(),
                    TenantId = evt.TenantId,
                    RecipientId = Guid.Empty,
                    NotificationType = "SMS",
                    Title = title,
                    Message = smsMessage,
                    Priority = "Normal",
                    Status = "Pending",
                    IsRead = false,
                    ScheduledFor = now,
                    SentAt = null,
                    AttemptCount = 0,
                    LastError = null,
                    DeliveryMethods = "SMS",
                    PhoneNumber = phone,
                    EntityType = evt.EntityType,
                    EntityId = evt.EntityId,
                    ActionUrl = null,
                    AdditionalData = JsonSerializer.Serialize(new { topic = new { key = topic.Key } })
                });
            }
        }

        if (queued.Count > 0)
        {
            try
            {
                await notificationRepo.AddRangeAsync(queued);
                await _unitOfWork.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to enqueue topic notifications for {TopicKey}", topic.Key);
            }
        }
    }

    private static bool TryGetGuidFromData(Dictionary<string, object> data, string key, out Guid id)
    {
        id = Guid.Empty;
        if (!data.TryGetValue(key, out var raw) || raw == null) return false;

        if (raw is Guid g)
        {
            id = g;
            return id != Guid.Empty;
        }

        if (raw is string s && Guid.TryParse(s, out var parsed))
        {
            id = parsed;
            return id != Guid.Empty;
        }

        return false;
    }

    private static bool TryParseDepartmentType(string raw, out DepartmentType departmentType)
    {
        departmentType = default;
        if (string.IsNullOrWhiteSpace(raw)) return false;

        var s = raw.Trim();

        // Accept numeric values (e.g., "6") or enum names (e.g., "Maintenance").
        if (int.TryParse(s, out var i) && Enum.IsDefined(typeof(DepartmentType), i))
        {
            departmentType = (DepartmentType)i;
            return true;
        }

        return Enum.TryParse(s, ignoreCase: true, out departmentType);
    }

    private static bool TryGetGuidsFromData(Dictionary<string, object> data, string key, out List<Guid> ids)
    {
        ids = new List<Guid>();
        if (!data.TryGetValue(key, out var raw) || raw == null) return false;

        if (raw is IEnumerable<Guid> guidList)
        {
            ids = guidList.Where(g => g != Guid.Empty).Distinct().ToList();
            return ids.Count > 0;
        }

        if (raw is IEnumerable<string> stringList)
        {
            ids = stringList
                .Select(s => Guid.TryParse(s, out var g) ? g : Guid.Empty)
                .Where(g => g != Guid.Empty)
                .Distinct()
                .ToList();
            return ids.Count > 0;
        }

        var text = raw.ToString() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(text)) return false;

        var parts = text.Split(new[] { ',', ';', '\n', '\r', '\t', ' ' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        ids = parts
            .Select(p => Guid.TryParse(p, out var g) ? g : Guid.Empty)
            .Where(g => g != Guid.Empty)
            .Distinct()
            .ToList();

        return ids.Count > 0;
    }

    private static IEnumerable<string> ParseEmails(object raw)
    {
        var text = raw.ToString() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(text)) yield break;

        var parts = text.Split(new[] { ',', ';', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var p in parts)
        {
            if (p.Contains('@'))
                yield return p;
        }
    }

    private static string? ResolveActionUrl(NotificationTopic topic, Dictionary<string, object> data)
    {
        if (data.TryGetValue("ActionUrl", out var au) && au != null)
        {
            var s = au.ToString();
            if (!string.IsNullOrWhiteSpace(s)) return s;
        }

        if (string.IsNullOrWhiteSpace(topic.ActionUrlTemplate)) return null;
        return RenderTemplate(topic.ActionUrlTemplate, data);
    }

    private static (string Subject, string HtmlBody) BuildEmail(NotificationTopic topic, Dictionary<string, object> data, string fallbackSubject, string fallbackBody)
    {
        if (topic.EmailTemplate != null && topic.EmailTemplate.IsActive && !topic.EmailTemplate.IsDeleted)
        {
            var subject = RenderTemplate(topic.EmailTemplate.Subject, data);
            var body = RenderTemplate(topic.EmailTemplate.HtmlBody, data);
            if (string.IsNullOrWhiteSpace(subject)) subject = fallbackSubject;
            if (string.IsNullOrWhiteSpace(body)) body = fallbackBody;
            return (subject, body);
        }

        return (fallbackSubject, fallbackBody);
    }

    private static string RenderTemplate(string template, Dictionary<string, object> data)
    {
        if (string.IsNullOrWhiteSpace(template)) return string.Empty;
        if (data == null || data.Count == 0) return template;

        return TokenRegex.Replace(template, m =>
        {
            var key = m.Groups[1].Value;
            if (data.TryGetValue(key, out var v) && v != null)
                return v.ToString() ?? string.Empty;
            return string.Empty;
        });
    }
}
