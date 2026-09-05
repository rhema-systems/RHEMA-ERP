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

        topic = await EnsureRequiredWorkflowTopicAsync(evt, key, topic);

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
            var emailOptions = evt.Email;
            var (subject, htmlBody) = BuildEmail(topic, data, fallbackSubject: title, fallbackBody: message);
            if (!string.IsNullOrWhiteSpace(emailOptions?.SubjectTemplateOverride))
            {
                var subjectOverride = RenderTemplate(emailOptions.SubjectTemplateOverride, data);
                if (!string.IsNullOrWhiteSpace(subjectOverride))
                {
                    subject = subjectOverride;
                }
            }

            if (!string.IsNullOrWhiteSpace(emailOptions?.HtmlBodyTemplateOverride))
            {
                var bodyOverride = RenderTemplate(emailOptions.HtmlBodyTemplateOverride, data);
                if (!string.IsNullOrWhiteSpace(bodyOverride))
                {
                    htmlBody = bodyOverride;
                }
            }

            string? textBody = null;
            if (!string.IsNullOrWhiteSpace(emailOptions?.TextBodyTemplateOverride))
            {
                textBody = RenderTemplate(emailOptions.TextBodyTemplateOverride, data);
            }

            var emailPayload = new
            {
                email = new
                {
                    isHtml = true,
                    subject,
                    bodyHtml = htmlBody,
                    textBody,
                    templateId = topic.EmailTemplateId,
                    templateName = topic.EmailTemplate?.Name,
                    attachmentSource = emailOptions?.AttachmentSource,
                    attachmentsOmitted = false,
                    attachments = emailOptions?.Attachments?.Select(a => new
                    {
                        fileName = a.FileName,
                        contentType = a.ContentType,
                        contentBase64 = a.ContentBase64
                    }).ToList() ?? []
                },
                topic = new
                {
                    key = topic.Key
                },
                metadata = evt.Metadata ?? new Dictionary<string, object>()
            };

            var additional = JsonSerializer.Serialize(emailPayload);

            foreach (var email in emailAddresses)
            {
                queued.Add(new Notification
                {
                    Id = Guid.NewGuid(),
                    TenantId = evt.TenantId,
                    RecipientId = Guid.Empty,
                    NotificationType = string.IsNullOrWhiteSpace(evt.NotificationType) ? "Email" : evt.NotificationType,
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

    private async Task<NotificationTopic?> EnsureRequiredWorkflowTopicAsync(
        NotificationTopicEvent evt,
        string key,
        NotificationTopic? topic)
    {
        var segments = key.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length < 3) return topic;

        var activity = segments[1];
        var isApprovalRequest = string.Equals(activity, "WorkflowApprovalRequest", StringComparison.OrdinalIgnoreCase);
        var isStepAssignment = string.Equals(activity, "WorkflowStepAssignment", StringComparison.OrdinalIgnoreCase);
        var isSubcontractCharge = string.Equals(key, "QuantitySurvey.SubcontractChargeNoticeIssued", StringComparison.OrdinalIgnoreCase) ||
                                  string.Equals(key, "QuantitySurvey.SubcontractChargeDecision", StringComparison.OrdinalIgnoreCase);
        var isEvaluationAppointment = string.Equals(key,
            "ProcurementEvaluationCommittee.AppointmentCreated.Internal",
            StringComparison.OrdinalIgnoreCase);
        if (!isApprovalRequest && !isStepAssignment && !isSubcontractCharge &&
            !isEvaluationAppointment) return topic;

        var defaultTitleTemplate = isEvaluationAppointment
            ? "Evaluation committee appointment: {{SourceReference}}"
            : "{{Title}}";
        var defaultBodyTemplate = isEvaluationAppointment
            ? "You have been appointed as {{MemberKind}} to {{CommitteeName}}. Review and respond to the appointment."
            : "{{Message}}";
        var changed = false;
        if (topic == null)
        {
            topic = new NotificationTopic
            {
                Id = Guid.NewGuid(),
                TenantId = evt.TenantId,
                Key = key,
                Name = isEvaluationAppointment ? "Evaluation committee appointment" :
                    isSubcontractCharge ? "Quantity Survey subcontract charge communication" :
                    isApprovalRequest ? $"{segments[0]} approval required" : $"{segments[0]} workflow assignment",
                Description = isEvaluationAppointment ? "An appointed evaluation committee member must review and respond to an active appointment." :
                    isSubcontractCharge ? "A governed subcontract charge notice or decision is available in the external project portal." :
                    isApprovalRequest ? "A workflow request is waiting for approval." : "A workflow step has been assigned.",
                EntityType = evt.EntityType ?? segments[0],
                IsSystem = true,
                IsRequired = true,
                IsActive = true,
                EnableInApp = true,
                EnableEmail = isSubcontractCharge || isEvaluationAppointment,
                EnableSms = false,
                InAppTitleTemplate = defaultTitleTemplate,
                InAppBodyTemplate = defaultBodyTemplate,
                ActionUrlTemplate = "{{ActionUrl}}",
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "System"
            };
            await _unitOfWork.Repository<NotificationTopic>().AddAsync(topic);
            changed = true;
        }
        else
        {
            if (!topic.IsSystem) { topic.IsSystem = true; changed = true; }
            if (!topic.IsRequired) { topic.IsRequired = true; changed = true; }
            if (!topic.IsActive) { topic.IsActive = true; changed = true; }
            if (!topic.EnableInApp) { topic.EnableInApp = true; changed = true; }
            if ((isSubcontractCharge || isEvaluationAppointment) && !topic.EnableEmail) { topic.EnableEmail = true; changed = true; }
            if (string.IsNullOrWhiteSpace(topic.InAppTitleTemplate)) { topic.InAppTitleTemplate = defaultTitleTemplate; changed = true; }
            if (string.IsNullOrWhiteSpace(topic.InAppBodyTemplate)) { topic.InAppBodyTemplate = defaultBodyTemplate; changed = true; }
            if (string.IsNullOrWhiteSpace(topic.ActionUrlTemplate)) { topic.ActionUrlTemplate = "{{ActionUrl}}"; changed = true; }
            if (changed)
            {
                topic.UpdatedAt = DateTime.UtcNow;
                await _unitOfWork.Repository<NotificationTopic>().UpdateAsync(topic);
            }
        }

        var requiredRules = isSubcontractCharge
            ? new[] { (Kind: "BusinessPartnerFromData", Value: "BusinessPartnerId", SendEmail: true) }
            : isEvaluationAppointment
                ? new[] { (Kind: "UserFromData", Value: "TargetUserId", SendEmail: true) }
            : isApprovalRequest
                ? new[]
                {
                    (Kind: "UserFromData", Value: "TargetUserId", SendEmail: false),
                    (Kind: "RoleFromData", Value: "TargetRole", SendEmail: false)
                }
                : new[] { (Kind: "UserFromData", Value: "TargetUserId", SendEmail: false) };
        var activeRecipients = (topic.Recipients ?? new List<NotificationTopicRecipient>())
            .Where(recipient => !recipient.IsDeleted)
            .ToList();
        var recipientRepo = _unitOfWork.Repository<NotificationTopicRecipient>();

        foreach (var rule in requiredRules)
        {
            var matchingRecipient = activeRecipients.FirstOrDefault(recipient =>
                    string.Equals(recipient.RecipientKind, rule.Kind, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(recipient.RecipientValue, rule.Value, StringComparison.OrdinalIgnoreCase));
            if (matchingRecipient != null)
            {
                var recipientChanged = false;
                if (!matchingRecipient.IsSystem) { matchingRecipient.IsSystem = true; recipientChanged = true; }
                if (!matchingRecipient.SendInApp) { matchingRecipient.SendInApp = true; recipientChanged = true; }
                if (rule.SendEmail && !matchingRecipient.SendEmail) { matchingRecipient.SendEmail = true; recipientChanged = true; }
                if (recipientChanged)
                {
                    matchingRecipient.UpdatedAt = DateTime.UtcNow;
                    await recipientRepo.UpdateAsync(matchingRecipient);
                    changed = true;
                }
                continue;
            }

            var recipient = new NotificationTopicRecipient
            {
                Id = Guid.NewGuid(),
                TenantId = evt.TenantId,
                TopicId = topic.Id,
                RecipientKind = rule.Kind,
                RecipientValue = rule.Value,
                IsSystem = true,
                SendInApp = true,
                SendEmail = rule.SendEmail,
                SendSms = false,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "System"
            };
            await recipientRepo.AddAsync(recipient);
            topic.Recipients.Add(recipient);
            activeRecipients.Add(recipient);
            changed = true;
        }

        if (changed)
        {
            await _unitOfWork.SaveChangesAsync();
        }

        return topic;
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
