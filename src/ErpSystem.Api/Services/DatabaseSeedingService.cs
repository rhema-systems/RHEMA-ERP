using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Ehc;
using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Enums;
using ErpSystem.Data;
using ErpSystem.Data.Seeders;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Web.Services
{
    public interface IDatabaseSeedingService
    {
        Task SeedAsync();
        Task SeedBasicDataAsync();
        Task SeedTestUsersAsync();
        Task SeedMaintenanceE2ETestDataAsync();
        Task<bool> HasSeedDataAsync();
    }

    public class DatabaseSeedingService : IDatabaseSeedingService
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<ApplicationRole> _roleManager;
        private readonly ILogger<DatabaseSeedingService> _logger;
        private readonly IWebHostEnvironment _environment;

        public DatabaseSeedingService(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            RoleManager<ApplicationRole> roleManager,
            ILogger<DatabaseSeedingService> logger,
            IWebHostEnvironment environment)
        {
            _context = context;
            _userManager = userManager;
            _roleManager = roleManager;
            _logger = logger;
            _environment = environment;
        }

        public async Task SeedAsync()
        {
            try
            {
                _logger.LogInformation("Starting database seeding...");

                // Ensure database is created and migrated
                await _context.Database.MigrateAsync();

                // Always ensure roles exist (safe/idempotent; required for new module roles on existing DBs)
                _logger.LogInformation("Ensuring roles are seeded...");
                await SeedRolesAsync();

                // Check if we already have seed data
                var hasData = await HasSeedDataAsync();
                if (!hasData)
                {
                    // Seed basic data
                    await SeedBasicDataAsync();
                }
                else
                {
                    _logger.LogInformation("Basic data already exists, skipping basic seeding");

                    // But always ensure tenant modules are seeded
                    _logger.LogInformation("Ensuring tenant modules are seeded...");
                    await SeedDefaultTenantModulesAsync();
                }

                // Always ensure baseline EHC workflow exists (required for ticket lifecycle management)
                _logger.LogInformation("Ensuring EHC workflow is seeded...");
                await EnsureEhcWorkflowSeededAsync();

                // Always ensure baseline EHC workflow routing rules exist (workflow selection by type/category/priority/department)
                _logger.LogInformation("Ensuring EHC workflow routing rules are seeded...");
                await EnsureEhcWorkflowRoutingRulesSeededAsync();

                // Always ensure baseline EHC categories and SLA templates exist (external portal UI depends on them)
                _logger.LogInformation("Ensuring EHC categories and SLA templates are seeded...");
                await EnsureEhcCategoriesSeededAsync();
                await EnsureEhcSlaTemplatesSeededAsync();

                // Always ensure baseline EHC notification topics exist (templated in-app/email notifications)
                _logger.LogInformation("Ensuring EHC notification topics are seeded...");
                await EnsureEhcNotificationTopicsSeededAsync();

                // Always seed/update test users in development to ensure correct passwords
                if (_environment.IsDevelopment())
                {
                    _logger.LogInformation("Ensuring test users have correct passwords...");
                    await SeedTestUsersAsync();

                    // Always ensure maintenance configuration is seeded in development
                    _logger.LogInformation("Ensuring maintenance configuration is seeded...");
                    await SeedMaintenanceConfigurationAsync();

                    // Seed comprehensive maintenance data (inventory, assets, templates, checklists)
                    _logger.LogInformation("Ensuring comprehensive maintenance data is seeded...");
                    await SeedMaintenanceComprehensiveDataAsync();

                    // Seed quality control checklists
                    _logger.LogInformation("Ensuring QC checklists are seeded...");
                    await SeedQualityControlChecklistsAsync();
                }

                await _context.SaveChangesAsync();
                _logger.LogInformation("Database seeding completed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during database seeding");
                throw;
            }
        }

        public async Task SeedBasicDataAsync()
        {
            _logger.LogInformation("Seeding basic data...");

            // Seed default tenant
            await SeedDefaultTenantAsync();

            // Seed default tenant modules
            await SeedDefaultTenantModulesAsync();

            // Seed HR data (departments, positions, employees)
            await SeedHRDataAsync();

            // Seed maintenance configuration (work order types, priority levels, maintenance types)
            await SeedMaintenanceConfigurationAsync();

            // Seed baseline EHC workflow definition
            await EnsureEhcWorkflowSeededAsync();

            _logger.LogInformation("Basic data seeding completed");
        }

        private async Task EnsureEhcWorkflowSeededAsync()
        {
            try
            {
                var tenants = await _context.Tenants.Where(t => !t.IsDeleted && t.Status == TenantStatus.Active).ToListAsync();
                foreach (var tenant in tenants)
                {
                    var entityType = await _context.WorkflowEntityTypes
                        .FirstOrDefaultAsync(et => !et.IsDeleted && et.TenantId == tenant.Id && et.Code == "EHC_TICKET");

                    if (entityType == null)
                    {
                        entityType = new WorkflowEntityType
                        {
                            Id = Guid.NewGuid(),
                            TenantId = tenant.Id,
                            Code = "EHC_TICKET",
                            Name = "EHC Ticket",
                            Description = "Enquiry, Helpdesk & Complaints Ticket",
                            EntityClassName = typeof(EhcTicket).FullName,
                            IsActive = true,
                            DisplayOrder = 50,
                            Icon = "ticket",
                            ColorCode = "#2563EB",
                            CreatedAt = DateTime.UtcNow,
                            CreatedBy = "System"
                        };

                        _context.WorkflowEntityTypes.Add(entityType);
                        await _context.SaveChangesAsync();
                    }

                    var existingDefinition = await _context.WorkflowDefinitions
                        .FirstOrDefaultAsync(d => !d.IsDeleted && d.TenantId == tenant.Id && d.Name == "EHC Ticket");

                    if (existingDefinition != null)
                    {
                        continue;
                    }

                    var definitionId = Guid.NewGuid();
                    var definition = new WorkflowDefinition
                    {
                        Id = definitionId,
                        TenantId = tenant.Id,
                        Name = "EHC Ticket",
                        Description = "Baseline ticket lifecycle: New → Acknowledged → InProgress → Resolved → Closed",
                        EntityTypeId = entityType.Id,
                        Version = 1,
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = "System"
                    };

                    _context.WorkflowDefinitions.Add(definition);

                    var stepNew = new WorkflowStep
                    {
                        Id = Guid.NewGuid(),
                        TenantId = tenant.Id,
                        WorkflowDefinitionId = definitionId,
                        Name = EhcTicketStatus.New.ToString(),
                        StepType = WorkflowStepType.Manual,
                        Order = 1,
                        IsStartStep = true,
                        IsEndStep = false,
                        IsRequired = true,
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = "System"
                    };

                    var stepAck = new WorkflowStep
                    {
                        Id = Guid.NewGuid(),
                        TenantId = tenant.Id,
                        WorkflowDefinitionId = definitionId,
                        Name = EhcTicketStatus.Acknowledged.ToString(),
                        StepType = WorkflowStepType.Manual,
                        Order = 2,
                        IsStartStep = false,
                        IsEndStep = false,
                        IsRequired = true,
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = "System"
                    };

                    var stepInProgress = new WorkflowStep
                    {
                        Id = Guid.NewGuid(),
                        TenantId = tenant.Id,
                        WorkflowDefinitionId = definitionId,
                        Name = EhcTicketStatus.InProgress.ToString(),
                        StepType = WorkflowStepType.Manual,
                        Order = 3,
                        IsStartStep = false,
                        IsEndStep = false,
                        IsRequired = true,
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = "System"
                    };

                    var stepResolved = new WorkflowStep
                    {
                        Id = Guid.NewGuid(),
                        TenantId = tenant.Id,
                        WorkflowDefinitionId = definitionId,
                        Name = EhcTicketStatus.Resolved.ToString(),
                        StepType = WorkflowStepType.Manual,
                        Order = 4,
                        IsStartStep = false,
                        IsEndStep = false,
                        IsRequired = true,
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = "System"
                    };

                    var stepClosed = new WorkflowStep
                    {
                        Id = Guid.NewGuid(),
                        TenantId = tenant.Id,
                        WorkflowDefinitionId = definitionId,
                        Name = EhcTicketStatus.Closed.ToString(),
                        StepType = WorkflowStepType.Manual,
                        Order = 5,
                        IsStartStep = false,
                        IsEndStep = true,
                        IsRequired = true,
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = "System"
                    };

                    _context.WorkflowSteps.AddRange(stepNew, stepAck, stepInProgress, stepResolved, stepClosed);

                    _context.WorkflowTransitions.AddRange(
                        new WorkflowTransition
                        {
                            Id = Guid.NewGuid(),
                            TenantId = tenant.Id,
                            WorkflowDefinitionId = definitionId,
                            FromStepId = stepNew.Id,
                            ToStepId = stepAck.Id,
                            Name = "Acknowledge",
                            IsDefault = true,
                            Priority = 0,
                            CreatedAt = DateTime.UtcNow,
                            CreatedBy = "System"
                        },
                        new WorkflowTransition
                        {
                            Id = Guid.NewGuid(),
                            TenantId = tenant.Id,
                            WorkflowDefinitionId = definitionId,
                            FromStepId = stepAck.Id,
                            ToStepId = stepInProgress.Id,
                            Name = "Start Progress",
                            IsDefault = true,
                            Priority = 0,
                            CreatedAt = DateTime.UtcNow,
                            CreatedBy = "System"
                        },
                        new WorkflowTransition
                        {
                            Id = Guid.NewGuid(),
                            TenantId = tenant.Id,
                            WorkflowDefinitionId = definitionId,
                            FromStepId = stepInProgress.Id,
                            ToStepId = stepResolved.Id,
                            Name = "Resolve",
                            IsDefault = true,
                            Priority = 0,
                            CreatedAt = DateTime.UtcNow,
                            CreatedBy = "System"
                        },
                        new WorkflowTransition
                        {
                            Id = Guid.NewGuid(),
                            TenantId = tenant.Id,
                            WorkflowDefinitionId = definitionId,
                            FromStepId = stepResolved.Id,
                            ToStepId = stepClosed.Id,
                            Name = "Close",
                            IsDefault = true,
                            Priority = 0,
                            CreatedAt = DateTime.UtcNow,
                            CreatedBy = "System"
                        });

                    await _context.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to seed EHC workflow");
            }
        }

        private async Task EnsureEhcNotificationTopicsSeededAsync()
        {
            var tenants = await _context.Tenants
                .AsNoTracking()
                .Where(t => !t.IsDeleted && t.Status == TenantStatus.Active)
                .Select(t => t.Id)
                .ToListAsync();

            foreach (var tenantId in tenants)
            {
                // Requester-facing templates (email enabled).
                var createdTemplateId = await EnsureEmailTemplateAsync(
                    tenantId,
                    name: "EHC Ticket Created (Requester)",
                    subject: "Ticket created: {{ticketNumber}}",
                    htmlBody:
                    """
                    <h2>Ticket created</h2>
                    <p>Your ticket <strong>{{ticketNumber}}</strong> has been created.</p>
                    <p><strong>Status:</strong> {{status}}</p>
                    <p>You can view your ticket here: <a href="{{ActionUrl}}">{{ActionUrl}}</a></p>
                    """);

                var statusChangedTemplateId = await EnsureEmailTemplateAsync(
                    tenantId,
                    name: "EHC Ticket Status Changed (Requester)",
                    subject: "Ticket updated: {{ticketNumber}}",
                    htmlBody:
                    """
                    <h2>Ticket updated</h2>
                    <p>Your ticket <strong>{{ticketNumber}}</strong> status changed.</p>
                    <p><strong>From:</strong> {{fromStatus}}</p>
                    <p><strong>To:</strong> {{toStatus}}</p>
                    <p>Open ticket: <a href="{{ActionUrl}}">{{ActionUrl}}</a></p>
                    """);

                var messageTemplateId = await EnsureEmailTemplateAsync(
                    tenantId,
                    name: "EHC Ticket Message (Requester)",
                    subject: "New message: {{ticketNumber}}",
                    htmlBody:
                    """
                    <h2>New message</h2>
                    <p>Support posted a new message on ticket <strong>{{ticketNumber}}</strong>.</p>
                    <p>Open ticket: <a href="{{ActionUrl}}">{{ActionUrl}}</a></p>
                    """);

                var attachmentTemplateId = await EnsureEmailTemplateAsync(
                    tenantId,
                    name: "EHC Ticket Attachment (Requester)",
                    subject: "New attachment: {{ticketNumber}}",
                    htmlBody:
                    """
                    <h2>New attachment</h2>
                    <p>Support uploaded an attachment on ticket <strong>{{ticketNumber}}</strong>.</p>
                    <p><strong>File:</strong> {{fileName}}</p>
                    <p>Open ticket: <a href="{{ActionUrl}}">{{ActionUrl}}</a></p>
                    """);

                await EnsureNotificationTopicAsync(
                    tenantId,
                    key: "EhcTicket.Created.Requester",
                    name: "EHC Ticket Created (Requester)",
                    description: "Notify the requester when a ticket is created.",
                    entityType: "EhcTicket",
                    isRequired: true,
                    enableInApp: true,
                    enableEmail: true,
                    inAppTitleTemplate: "Ticket created: {{ticketNumber}}",
                    inAppBodyTemplate: "We received ticket {{ticketNumber}} ({{ticketType}}). Status: {{status}}.",
                    actionUrlTemplate: "{{ActionUrl}}",
                    emailTemplateId: createdTemplateId,
                    recipients: new[]
                    {
                        (kind: "UserFromData", value: "requesterUserId", inApp: true, email: true)
                    });

                await EnsureNotificationTopicAsync(
                    tenantId,
                    key: "EhcTicket.StatusChanged.Requester",
                    name: "EHC Ticket Status Changed (Requester)",
                    description: "Notify the requester when ticket status changes.",
                    entityType: "EhcTicket",
                    isRequired: true,
                    enableInApp: true,
                    enableEmail: true,
                    inAppTitleTemplate: "Ticket updated: {{ticketNumber}}",
                    inAppBodyTemplate: "Status: {{fromStatus}} → {{toStatus}}",
                    actionUrlTemplate: "{{ActionUrl}}",
                    emailTemplateId: statusChangedTemplateId,
                    recipients: new[]
                    {
                        (kind: "UserFromData", value: "requesterUserId", inApp: true, email: true)
                    });

                await EnsureNotificationTopicAsync(
                    tenantId,
                    key: "EhcTicket.Message.Requester",
                    name: "EHC Ticket Message (Requester)",
                    description: "Notify the requester when an agent posts a message.",
                    entityType: "EhcTicket",
                    isRequired: false,
                    enableInApp: true,
                    enableEmail: true,
                    inAppTitleTemplate: "New message: {{ticketNumber}}",
                    inAppBodyTemplate: "Support sent a new message.",
                    actionUrlTemplate: "{{ActionUrl}}",
                    emailTemplateId: messageTemplateId,
                    recipients: new[]
                    {
                        (kind: "UserFromData", value: "requesterUserId", inApp: true, email: true)
                    });

                await EnsureNotificationTopicAsync(
                    tenantId,
                    key: "EhcTicket.Attachment.Requester",
                    name: "EHC Ticket Attachment (Requester)",
                    description: "Notify the requester when an agent uploads an attachment.",
                    entityType: "EhcTicket",
                    isRequired: false,
                    enableInApp: true,
                    enableEmail: true,
                    inAppTitleTemplate: "New attachment: {{ticketNumber}}",
                    inAppBodyTemplate: "Support uploaded {{fileName}}.",
                    actionUrlTemplate: "{{ActionUrl}}",
                    emailTemplateId: attachmentTemplateId,
                    recipients: new[]
                    {
                        (kind: "UserFromData", value: "requesterUserId", inApp: true, email: true)
                    });

                // Internal topics (in-app only by default).
                await EnsureNotificationTopicAsync(
                    tenantId,
                    key: "EhcTicket.Created.Internal",
                    name: "EHC Ticket Created (Internal)",
                    description: "Notify internal helpdesk users when a new ticket is created.",
                    entityType: "EhcTicket",
                    isRequired: false,
                    enableInApp: true,
                    enableEmail: false,
                    inAppTitleTemplate: "New ticket: {{ticketNumber}}",
                    inAppBodyTemplate: "A new ticket was created ({{ticketType}} • {{priority}}).",
                    actionUrlTemplate: "{{ActionUrl}}",
                    emailTemplateId: null,
                    recipients: new[]
                    {
                        (kind: "Role", value: Constants.Roles.HelpdeskSupervisor, inApp: true, email: false),
                        (kind: "Role", value: Constants.Roles.HelpdeskAgent, inApp: true, email: false)
                    });

                await EnsureNotificationTopicAsync(
                    tenantId,
                    key: "EhcTicket.Assigned.Internal",
                    name: "EHC Ticket Assigned (Internal)",
                    description: "Notify the assigned agent when a ticket is assigned.",
                    entityType: "EhcTicket",
                    isRequired: false,
                    enableInApp: true,
                    enableEmail: false,
                    inAppTitleTemplate: "Ticket assigned: {{ticketNumber}}",
                    inAppBodyTemplate: "A ticket was assigned to you.",
                    actionUrlTemplate: "{{ActionUrl}}",
                    emailTemplateId: null,
                    recipients: new[]
                    {
                        (kind: "UserFromData", value: "assignedToUserId", inApp: true, email: false),
                        (kind: "Role", value: Constants.Roles.HelpdeskSupervisor, inApp: true, email: false)
                    });

                await EnsureNotificationTopicAsync(
                    tenantId,
                    key: "EhcTicket.Message.Internal",
                    name: "EHC Ticket Message (Internal)",
                    description: "Notify internal users when a requester posts a message.",
                    entityType: "EhcTicket",
                    isRequired: false,
                    enableInApp: true,
                    enableEmail: false,
                    inAppTitleTemplate: "Requester replied: {{ticketNumber}}",
                    inAppBodyTemplate: "The requester posted a new message.",
                    actionUrlTemplate: "{{ActionUrl}}",
                    emailTemplateId: null,
                    recipients: new[]
                    {
                        (kind: "UserFromData", value: "assignedToUserId", inApp: true, email: false),
                        (kind: "Role", value: Constants.Roles.HelpdeskSupervisor, inApp: true, email: false)
                    });

                await EnsureNotificationTopicAsync(
                    tenantId,
                    key: "EhcTicket.Attachment.Internal",
                    name: "EHC Ticket Attachment (Internal)",
                    description: "Notify internal users when a requester uploads an attachment.",
                    entityType: "EhcTicket",
                    isRequired: false,
                    enableInApp: true,
                    enableEmail: false,
                    inAppTitleTemplate: "Requester attachment: {{ticketNumber}}",
                    inAppBodyTemplate: "Requester uploaded {{fileName}}.",
                    actionUrlTemplate: "{{ActionUrl}}",
                    emailTemplateId: null,
                    recipients: new[]
                    {
                        (kind: "UserFromData", value: "assignedToUserId", inApp: true, email: false),
                        (kind: "Role", value: Constants.Roles.HelpdeskSupervisor, inApp: true, email: false)
                    });

                await EnsureNotificationTopicAsync(
                    tenantId,
                    key: "EhcTicket.StatusChanged.Internal",
                    name: "EHC Ticket Status Changed (Internal)",
                    description: "Notify internal users when ticket status changes.",
                    entityType: "EhcTicket",
                    isRequired: false,
                    enableInApp: true,
                    enableEmail: false,
                    inAppTitleTemplate: "Ticket updated: {{ticketNumber}}",
                    inAppBodyTemplate: "Status: {{fromStatus}} → {{toStatus}}",
                    actionUrlTemplate: "{{ActionUrl}}",
                    emailTemplateId: null,
                    recipients: new[]
                    {
                        (kind: "UserFromData", value: "assignedToUserId", inApp: true, email: false),
                        (kind: "Role", value: Constants.Roles.HelpdeskSupervisor, inApp: true, email: false)
                    });

                await EnsureNotificationTopicAsync(
                    tenantId,
                    key: "EhcTicket.SlaWarning.Internal",
                    name: "EHC SLA Warning (Internal)",
                    description: "Notify internal users for near-breach SLA warnings.",
                    entityType: "EhcTicket",
                    isRequired: false,
                    enableInApp: true,
                    enableEmail: false,
                    inAppTitleTemplate: "SLA warning: {{ticketNumber}}",
                    inAppBodyTemplate: "SLA due soon. Minutes left: {{minutesLeft}}",
                    actionUrlTemplate: "{{ActionUrl}}",
                    emailTemplateId: null,
                    recipients: new[]
                    {
                        (kind: "UserFromData", value: "assignedToUserId", inApp: true, email: false),
                        (kind: "Role", value: Constants.Roles.HelpdeskSupervisor, inApp: true, email: false)
                    });

                await EnsureNotificationTopicAsync(
                    tenantId,
                    key: "EhcTicket.SlaBreach.Internal",
                    name: "EHC SLA Breach (Internal)",
                    description: "Notify internal users for SLA breaches.",
                    entityType: "EhcTicket",
                    isRequired: false,
                    enableInApp: true,
                    enableEmail: false,
                    inAppTitleTemplate: "SLA breach: {{ticketNumber}}",
                    inAppBodyTemplate: "A ticket breached the SLA ({{breachKind}}).",
                    actionUrlTemplate: "{{ActionUrl}}",
                    emailTemplateId: null,
                    recipients: new[]
                    {
                        (kind: "UserFromData", value: "assignedToUserId", inApp: true, email: false),
                        (kind: "Role", value: Constants.Roles.HelpdeskSupervisor, inApp: true, email: false),
                        (kind: "Role", value: Constants.Roles.HelpdeskManager, inApp: true, email: false)
                    });

                await EnsureNotificationTopicAsync(
                    tenantId,
                    key: "EhcTicket.SlaEscalation.Internal",
                    name: "EHC SLA Escalation (Internal)",
                    description: "Notify internal users when SLA escalation auto-assigns a ticket.",
                    entityType: "EhcTicket",
                    isRequired: false,
                    enableInApp: true,
                    enableEmail: false,
                    inAppTitleTemplate: "Ticket escalated: {{ticketNumber}}",
                    inAppBodyTemplate: "Ticket auto-assigned due to SLA: {{reason}}",
                    actionUrlTemplate: "{{ActionUrl}}",
                    emailTemplateId: null,
                    recipients: new[]
                    {
                        (kind: "UserFromData", value: "assignedToUserId", inApp: true, email: false),
                        (kind: "Role", value: Constants.Roles.HelpdeskSupervisor, inApp: true, email: false),
                        (kind: "Role", value: Constants.Roles.HelpdeskManager, inApp: true, email: false)
                    });
            }

            await _context.SaveChangesAsync();
        }

        private async Task<Guid?> EnsureEmailTemplateAsync(Guid tenantId, string name, string subject, string htmlBody)
        {
            var existing = await _context.EmailTemplates
                .FirstOrDefaultAsync(t => !t.IsDeleted && t.TenantId == tenantId && t.Module == "Notifications" && t.Name == name);

            if (existing != null)
            {
                return existing.Id;
            }

            var template = new EmailTemplate
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Module = "Notifications",
                Name = name,
                Subject = subject,
                HtmlBody = htmlBody,
                IsActive = true,
                Category = "EHC",
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "System"
            };

            _context.EmailTemplates.Add(template);
            await _context.SaveChangesAsync();
            return template.Id;
        }

        private async Task EnsureNotificationTopicAsync(
            Guid tenantId,
            string key,
            string name,
            string description,
            string entityType,
            bool isRequired,
            bool enableInApp,
            bool enableEmail,
            string? inAppTitleTemplate,
            string? inAppBodyTemplate,
            string? actionUrlTemplate,
            Guid? emailTemplateId,
            (string kind, string value, bool inApp, bool email)[] recipients)
        {
            var topic = await _context.NotificationTopics
                .Include(t => t.Recipients)
                .FirstOrDefaultAsync(t => !t.IsDeleted && t.TenantId == tenantId && t.Key == key);

            if (topic == null)
            {
                topic = new NotificationTopic
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    Key = key,
                    Name = name,
                    Description = description,
                    EntityType = entityType,
                    IsSystem = true,
                    IsRequired = isRequired,
                    IsActive = true,
                    EnableInApp = enableInApp,
                    EnableEmail = enableEmail,
                    InAppTitleTemplate = inAppTitleTemplate,
                    InAppBodyTemplate = inAppBodyTemplate,
                    ActionUrlTemplate = actionUrlTemplate,
                    EmailTemplateId = emailTemplateId,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = "System"
                };

                _context.NotificationTopics.Add(topic);
                await _context.SaveChangesAsync();
            }

            var existingRules = (topic.Recipients ?? new List<NotificationTopicRecipient>())
                .Where(r => !r.IsDeleted)
                .Select(r => $"{r.RecipientKind}:{r.RecipientValue}")
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var (kind, value, inApp, email) in recipients)
            {
                var k = $"{kind}:{value}";
                if (existingRules.Contains(k)) continue;

                _context.NotificationTopicRecipients.Add(new NotificationTopicRecipient
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    TopicId = topic.Id,
                    RecipientKind = kind,
                    RecipientValue = value,
                    IsSystem = true,
                    SendInApp = inApp,
                    SendEmail = email,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = "System"
                });
            }
        }

        private async Task EnsureEhcWorkflowRoutingRulesSeededAsync()
        {
            try
            {
                var tenants = await _context.Tenants.Where(t => !t.IsDeleted && t.Status == TenantStatus.Active).ToListAsync();
                foreach (var tenant in tenants)
                {
                    var hasAny = await _context.EhcWorkflowRoutingRules.AnyAsync(r => r.TenantId == tenant.Id && !r.IsDeleted);
                    if (hasAny)
                    {
                        continue;
                    }

                    _context.EhcWorkflowRoutingRules.Add(new EhcWorkflowRoutingRule
                    {
                        Id = Guid.NewGuid(),
                        TenantId = tenant.Id,
                        Name = "Default EHC Ticket Workflow",
                        IsActive = true,
                        Priority = 0,
                        WorkflowName = "EHC Ticket",
                        TicketType = null,
                        TicketPriority = null,
                        CategoryId = null,
                        SubcategoryId = null,
                        AssignedDepartmentId = null,
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = "System"
                    });

                    await _context.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to seed EHC workflow routing rules");
            }
        }

        private async Task EnsureEhcCategoriesSeededAsync()
        {
            try
            {
                var tenants = await _context.Tenants.Where(t => !t.IsDeleted && t.Status == TenantStatus.Active).ToListAsync();
                foreach (var tenant in tenants)
                {
                    var hasAny = await _context.EhcTicketCategories.AnyAsync(c => c.TenantId == tenant.Id && !c.IsDeleted);
                    if (hasAny)
                    {
                        continue;
                    }

                    var now = DateTime.UtcNow;

                    var general = new ErpSystem.Core.Entities.Ehc.EhcTicketCategory
                    {
                        Id = Guid.NewGuid(),
                        TenantId = tenant.Id,
                        Code = "GENERAL",
                        Name = "General",
                        Description = "General enquiries and requests",
                        CreatedAt = now,
                        CreatedBy = "System"
                    };
                    var technical = new ErpSystem.Core.Entities.Ehc.EhcTicketCategory
                    {
                        Id = Guid.NewGuid(),
                        TenantId = tenant.Id,
                        Code = "TECH",
                        Name = "Technical Support",
                        Description = "Technical issues and helpdesk requests",
                        AppliesToType = ErpSystem.Core.Enums.EhcTicketType.Helpdesk,
                        CreatedAt = now,
                        CreatedBy = "System"
                    };
                    var complaints = new ErpSystem.Core.Entities.Ehc.EhcTicketCategory
                    {
                        Id = Guid.NewGuid(),
                        TenantId = tenant.Id,
                        Code = "COMPLAINTS",
                        Name = "Complaints",
                        Description = "Service/product complaints",
                        AppliesToType = ErpSystem.Core.Enums.EhcTicketType.Complaint,
                        CreatedAt = now,
                        CreatedBy = "System"
                    };

                    _context.EhcTicketCategories.AddRange(general, technical, complaints);

                    // A few starter subcategories
                    _context.EhcTicketCategories.AddRange(
                        new ErpSystem.Core.Entities.Ehc.EhcTicketCategory
                        {
                            Id = Guid.NewGuid(),
                            TenantId = tenant.Id,
                            ParentCategoryId = technical.Id,
                            Code = "LOGIN",
                            Name = "Login / Access",
                            AppliesToType = ErpSystem.Core.Enums.EhcTicketType.Helpdesk,
                            CreatedAt = now,
                            CreatedBy = "System"
                        },
                        new ErpSystem.Core.Entities.Ehc.EhcTicketCategory
                        {
                            Id = Guid.NewGuid(),
                            TenantId = tenant.Id,
                            ParentCategoryId = technical.Id,
                            Code = "BUG",
                            Name = "System Bug",
                            AppliesToType = ErpSystem.Core.Enums.EhcTicketType.Helpdesk,
                            CreatedAt = now,
                            CreatedBy = "System"
                        },
                        new ErpSystem.Core.Entities.Ehc.EhcTicketCategory
                        {
                            Id = Guid.NewGuid(),
                            TenantId = tenant.Id,
                            ParentCategoryId = complaints.Id,
                            Code = "SERVICE",
                            Name = "Service Quality",
                            AppliesToType = ErpSystem.Core.Enums.EhcTicketType.Complaint,
                            CreatedAt = now,
                            CreatedBy = "System"
                        });

                    await _context.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to seed EHC categories");
            }
        }

        private async Task EnsureEhcSlaTemplatesSeededAsync()
        {
            try
            {
                var tenants = await _context.Tenants.Where(t => !t.IsDeleted && t.Status == TenantStatus.Active).ToListAsync();
                foreach (var tenant in tenants)
                {
                    var hasAny = await _context.EhcSlaTemplates.AnyAsync(s => s.TenantId == tenant.Id && !s.IsDeleted);
                    if (hasAny)
                    {
                        continue;
                    }

                    var now = DateTime.UtcNow;
                    _context.EhcSlaTemplates.AddRange(
                        new ErpSystem.Core.Entities.Ehc.EhcSlaTemplate
                        {
                            Id = Guid.NewGuid(),
                            TenantId = tenant.Id,
                            Name = "Default - Low",
                            IsActive = true,
                            Priority = ErpSystem.Core.Enums.EhcTicketPriority.Low,
                            FirstResponseMinutes = 240,
                            ResolutionMinutes = 4320,
                            CreatedAt = now,
                            CreatedBy = "System"
                        },
                        new ErpSystem.Core.Entities.Ehc.EhcSlaTemplate
                        {
                            Id = Guid.NewGuid(),
                            TenantId = tenant.Id,
                            Name = "Default - Medium",
                            IsActive = true,
                            Priority = ErpSystem.Core.Enums.EhcTicketPriority.Medium,
                            FirstResponseMinutes = 120,
                            ResolutionMinutes = 2880,
                            CreatedAt = now,
                            CreatedBy = "System"
                        },
                        new ErpSystem.Core.Entities.Ehc.EhcSlaTemplate
                        {
                            Id = Guid.NewGuid(),
                            TenantId = tenant.Id,
                            Name = "Default - High",
                            IsActive = true,
                            Priority = ErpSystem.Core.Enums.EhcTicketPriority.High,
                            FirstResponseMinutes = 60,
                            ResolutionMinutes = 1440,
                            CreatedAt = now,
                            CreatedBy = "System"
                        },
                        new ErpSystem.Core.Entities.Ehc.EhcSlaTemplate
                        {
                            Id = Guid.NewGuid(),
                            TenantId = tenant.Id,
                            Name = "Default - Critical",
                            IsActive = true,
                            Priority = ErpSystem.Core.Enums.EhcTicketPriority.Critical,
                            FirstResponseMinutes = 30,
                            ResolutionMinutes = 480,
                            CreatedAt = now,
                            CreatedBy = "System"
                        });

                    await _context.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to seed EHC SLA templates");
            }
        }

        public async Task SeedTestUsersAsync()
        {
            _logger.LogInformation("Seeding test users...");

            var defaultTenant = await _context.Tenants.FirstOrDefaultAsync(t => t.Code == "DEFAULT");
            if (defaultTenant == null)
            {
                _logger.LogError("Default tenant not found for test user seeding");
                return;
            }

            // Only create test users if they don't exist - don't update existing users
            // Ensure these accounts exist and remain usable on every Development seed run.
            // (CreateTestUserAsync is idempotent and will update existing users as needed.)
            await CreateTestUserAsync("admin", "admin@default.com", "Admin123!",
                "System", "Administrator", defaultTenant.Id, Constants.Roles.SuperAdmin, AuthenticationProvider.LDAP);

            await CreateTestUserAsync("manager", "manager@default.com", "Manager123!",
                "John", "Manager", defaultTenant.Id, Constants.Roles.Manager, AuthenticationProvider.LDAP);

            await CreateTestUserAsync("employee", "employee@default.com", "Employee123!",
                "Jane", "Employee", defaultTenant.Id, Constants.Roles.Employee, AuthenticationProvider.LDAP);

            await CreateTestUserAsync("helpdesk.agent", "helpdesk.agent@default.com", "Helpdesk123!",
                "Helpdesk", "Agent", defaultTenant.Id, Constants.Roles.HelpdeskAgent, AuthenticationProvider.LDAP);

            await CreateTestUserAsync("helpdesk.supervisor", "helpdesk.supervisor@default.com", "Helpdesk123!",
                "Helpdesk", "Supervisor", defaultTenant.Id, Constants.Roles.HelpdeskSupervisor, AuthenticationProvider.LDAP);

            await CreateTestUserAsync("helpdesk.manager", "helpdesk.manager@default.com", "Helpdesk123!",
                "Helpdesk", "Manager", defaultTenant.Id, Constants.Roles.HelpdeskManager, AuthenticationProvider.LDAP);

            // External portal user (Local auth) for testing support portal flows
            await CreateTestUserAsync("external", "external@default.com", "External123!",
                "External", "User", defaultTenant.Id, Constants.Roles.ExternalUser, AuthenticationProvider.Local);

            _logger.LogInformation("Test users seeding completed");
        }

        public async Task SeedMaintenanceE2ETestDataAsync()
        {
            _logger.LogInformation("Seeding Maintenance E2E test data...");

            try
            {
                // Create a logger factory to get the properly typed logger
                var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
                var seederLogger = loggerFactory.CreateLogger<MaintenanceE2ETestSeeder>();

                var seeder = new MaintenanceE2ETestSeeder(_context, seederLogger);
                await seeder.SeedAsync();
                _logger.LogInformation("Maintenance E2E test data seeding completed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error seeding Maintenance E2E test data");
                throw;
            }
        }

        private async Task SeedHRDataAsync()
        {
            _logger.LogInformation("Seeding HR data...");

            try
            {
                var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
                var seederLogger = loggerFactory.CreateLogger<HRDataSeeder>();

                var seeder = new HRDataSeeder(_context, seederLogger);
                await seeder.SeedAsync();
                _logger.LogInformation("HR data seeding completed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error seeding HR data");
                throw;
            }
        }

        private async Task SeedMaintenanceConfigurationAsync()
        {
            _logger.LogInformation("Seeding maintenance configuration...");

            try
            {
                var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
                var seederLogger = loggerFactory.CreateLogger<MaintenanceConfigurationSeeder>();

                var seeder = new MaintenanceConfigurationSeeder(_context, seederLogger);
                await seeder.SeedAsync();
                _logger.LogInformation("Maintenance configuration seeding completed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error seeding maintenance configuration");
                throw;
            }
        }

        private async Task SeedMaintenanceComprehensiveDataAsync()
        {
            _logger.LogInformation("Seeding comprehensive maintenance data...");

            try
            {
                var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
                var seederLogger = loggerFactory.CreateLogger<MaintenanceComprehensiveDataSeeder>();

                var seeder = new MaintenanceComprehensiveDataSeeder(_context, seederLogger);
                await seeder.SeedAsync();
                _logger.LogInformation("Comprehensive maintenance data seeding completed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error seeding comprehensive maintenance data");
                throw;
            }
        }

        private async Task SeedQualityControlChecklistsAsync()
        {
            _logger.LogInformation("Seeding quality control checklists...");

            try
            {
                var defaultTenant = await _context.Tenants.FirstOrDefaultAsync(t => t.Code == "DEFAULT");
                if (defaultTenant == null)
                {
                    _logger.LogWarning("Default tenant not found, skipping QC checklist seeding");
                    return;
                }

                var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
                var seederLogger = loggerFactory.CreateLogger<QualityControlChecklistSeeder>();

                var seeder = new QualityControlChecklistSeeder(_context, seederLogger);
                await seeder.SeedAsync(defaultTenant.Id);
                _logger.LogInformation("Quality control checklists seeding completed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error seeding quality control checklists");
                throw;
            }
        }

        public async Task<bool> HasSeedDataAsync()
        {
            var hasRoles = await _roleManager.Roles.AnyAsync();
            var hasTenants = await _context.Tenants.AnyAsync();
            return hasRoles && hasTenants;
        }

        private async Task SeedRolesAsync()
        {
            var roles = new[]
            {
                new { Name = Constants.Roles.SuperAdmin, Description = "System Super Administrator with full access" },
                new { Name = Constants.Roles.TenantAdmin, Description = "Tenant Administrator with tenant-wide access" },
                new { Name = Constants.Roles.Manager, Description = "Manager with departmental access" },
                new { Name = Constants.Roles.Employee, Description = "Standard employee with limited access" },
                new { Name = Constants.Roles.ExternalUser, Description = "External portal user (customers/vendors/partners/citizens)" },
                new { Name = Constants.Roles.HelpdeskAgent, Description = "Helpdesk agent for managing tickets" },
                new { Name = Constants.Roles.HelpdeskSupervisor, Description = "Helpdesk supervisor for assignment and escalation" },
                new { Name = Constants.Roles.HelpdeskManager, Description = "Helpdesk manager for dashboards and configuration" },
                new { Name = "Finance User", Description = "User with access to finance module" },
                new { Name = "HR User", Description = "User with access to HR module" },
                new { Name = "Sales User", Description = "User with access to sales module" },
                new { Name = "Inventory User", Description = "User with access to inventory module" },
                new { Name = "Procurement User", Description = "User with access to procurement module" },
                new { Name = "Marketing User", Description = "User with access to marketing module" }
            };

            foreach (var roleInfo in roles)
            {
                var existingRole = await _roleManager.FindByNameAsync(roleInfo.Name);
                if (existingRole == null)
                {
                    var role = new ApplicationRole(roleInfo.Name)
                    {
                        Description = roleInfo.Description,
                        IsSystemRole = roleInfo.Name.Contains("Admin") || roleInfo.Name.Contains("Manager"),
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = "System"
                    };

                    var result = await _roleManager.CreateAsync(role);
                    if (result.Succeeded)
                    {
                        _logger.LogDebug("Created role: {RoleName}", roleInfo.Name);
                    }
                    else
                    {
                        _logger.LogError("Failed to create role {RoleName}: {Errors}",
                            roleInfo.Name, string.Join(", ", result.Errors.Select(e => e.Description)));
                    }
                }
            }
        }

        private async Task SeedDefaultTenantAsync()
        {
            var existingTenant = await _context.Tenants.FirstOrDefaultAsync(t => t.Code == "DEFAULT");
            if (existingTenant == null)
            {
                var tenant = new Tenant
                {
                    Name = "Default Company",
                    Code = "DEFAULT",
                    Description = "Default tenant for system operations",
                    Status = TenantStatus.Active,
                    ContactEmail = "admin@default.com",
                    ContactPhone = "+1-555-0100",
                    Address = "123 Default Street, Default City, DC 12345",
                    SubscriptionStartDate = DateTime.UtcNow,
                    SubscriptionEndDate = DateTime.UtcNow.AddYears(1),
                    LdapEnabled = false,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = "System"
                };

                _context.Tenants.Add(tenant);
                await _context.SaveChangesAsync();
                _logger.LogDebug("Created default tenant: {TenantName}", tenant.Name);
            }
        }

        private async Task SeedDefaultTenantModulesAsync()
        {
            var defaultTenant = await _context.Tenants.FirstOrDefaultAsync(t => t.Code == "DEFAULT");
            if (defaultTenant == null)
            {
                _logger.LogError("Default tenant not found for module seeding");
                return;
            }

            // IMPORTANT: TenantModules has a unique index on (TenantId, ModuleName) and uses soft-delete.
            // Seeding must be idempotent and must not insert duplicates when a record already exists (even if soft-deleted).
            var modules = new[]
            {
                new { ModuleName = "Finance", Description = "Financial reports and analytics" },
                new { ModuleName = "Sales", Description = "Sales performance and CRM reports" },
                new { ModuleName = "HR", Description = "HR and employee reports" },
                new { ModuleName = "Inventory", Description = "Stock and inventory reports" },
                new { ModuleName = "Procurement", Description = "Purchasing and supplier reports" },
                new { ModuleName = "Marketing", Description = "Marketing campaigns and analytics" },
                new { ModuleName = "WorkflowEngine", Description = "Workflow automation and BPM" }
            };

            var now = DateTime.UtcNow;

            var existing = await _context.TenantModules
                .IgnoreQueryFilters()
                .Where(tm => tm.TenantId == defaultTenant.Id)
                .ToListAsync();

            var existingByName = existing
                .Where(m => !string.IsNullOrWhiteSpace(m.ModuleName))
                .ToDictionary(m => m.ModuleName.Trim(), m => m, StringComparer.OrdinalIgnoreCase);

            var created = 0;
            var updated = 0;

            foreach (var moduleInfo in modules)
            {
                if (existingByName.TryGetValue(moduleInfo.ModuleName, out var module))
                {
                    var changed = false;

                    if (module.IsDeleted)
                    {
                        module.IsDeleted = false;
                        module.DeletedAt = null;
                        module.DeletedBy = null;
                        changed = true;
                    }

                    if (module.Status != ModuleStatus.Enabled)
                    {
                        module.Status = ModuleStatus.Enabled;
                        changed = true;
                    }

                    // Keep existing descriptions unless empty; some tenants may customize descriptions.
                    if (string.IsNullOrWhiteSpace(module.Description))
                    {
                        module.Description = moduleInfo.Description;
                        changed = true;
                    }

                    if (module.EnabledDate == null)
                    {
                        module.EnabledDate = now;
                        changed = true;
                    }

                    if (changed)
                    {
                        module.UpdatedAt = now;
                        module.UpdatedBy = "System";
                        updated++;
                        _logger.LogDebug("Updated tenant module: {ModuleName} for tenant {TenantName}", moduleInfo.ModuleName, defaultTenant.Name);
                    }

                    continue;
                }

                _context.TenantModules.Add(new TenantModule
                {
                    Id = Guid.NewGuid(),
                    TenantId = defaultTenant.Id,
                    ModuleName = moduleInfo.ModuleName,
                    Description = moduleInfo.Description,
                    Status = ModuleStatus.Enabled,
                    EnabledDate = now,
                    CreatedAt = now,
                    CreatedBy = "System"
                });

                created++;
                _logger.LogDebug("Created tenant module: {ModuleName} for tenant {TenantName}", moduleInfo.ModuleName, defaultTenant.Name);
            }

            if (created > 0 || updated > 0)
            {
                await _context.SaveChangesAsync();
                _logger.LogInformation(
                    "Ensured default tenant modules for {TenantName}: created={Created}, updated={Updated}.",
                    defaultTenant.Name,
                    created,
                    updated);
            }
            else
            {
                _logger.LogInformation("Default tenant modules already up to date for {TenantName}.", defaultTenant.Name);
            }
        }

        private async Task CreateTestUserAsync(
            string username,
            string email,
            string password,
            string firstName,
            string lastName,
            Guid tenantId,
            string roleName,
            AuthenticationProvider authenticationProvider = AuthenticationProvider.Local)
        {
            var existingUser = await _userManager.FindByNameAsync(username);
            if (existingUser != null)
            {
                // Ensure tenant, profile fields, and active status are correct
                var needsUpdate = false;
                if (existingUser.TenantId != tenantId) { existingUser.TenantId = tenantId; needsUpdate = true; }
                if (existingUser.Email != email) { existingUser.Email = email; needsUpdate = true; }
                if (existingUser.FirstName != firstName) { existingUser.FirstName = firstName; needsUpdate = true; }
                if (existingUser.LastName != lastName) { existingUser.LastName = lastName; needsUpdate = true; }
                if (!existingUser.EmailConfirmed) { existingUser.EmailConfirmed = true; needsUpdate = true; }
                if (!existingUser.IsActive) { existingUser.IsActive = true; needsUpdate = true; }
                if (existingUser.AuthenticationProvider != authenticationProvider) { existingUser.AuthenticationProvider = authenticationProvider; needsUpdate = true; }

                if (needsUpdate)
                {
                    await _userManager.UpdateAsync(existingUser);
                }

                // Ensure role assignment
                var inRole = await _userManager.IsInRoleAsync(existingUser, roleName);
                if (!inRole)
                {
                    var addRoleResult = await _userManager.AddToRoleAsync(existingUser, roleName);
                    if (!addRoleResult.Succeeded)
                    {
                        _logger.LogError("Failed to ensure role {Role} for user {Username}: {Errors}", roleName, username,
                            string.Join(", ", addRoleResult.Errors.Select(e => e.Description)));
                    }
                }

                // Reset password to the expected strong password to align with docs/login page
                var resetToken = await _userManager.GeneratePasswordResetTokenAsync(existingUser);
                var resetResult = await _userManager.ResetPasswordAsync(existingUser, resetToken, password);
                if (resetResult.Succeeded)
                {
                    // Clear lockout just in case
                    await _userManager.SetLockoutEndDateAsync(existingUser, null);
                    await _userManager.ResetAccessFailedCountAsync(existingUser);
                    _logger.LogInformation("Updated existing user {Username} and reset password.", username);
                }
                else
                {
                    _logger.LogError("Failed to reset password for {Username}: {Errors}", username,
                        string.Join(", ", resetResult.Errors.Select(e => e.Description)));
                }

                return;
            }

            var user = new ApplicationUser
            {
                UserName = username,
                Email = email,
                EmailConfirmed = true,
                FirstName = firstName,
                LastName = lastName,
                TenantId = tenantId,
                AuthenticationProvider = authenticationProvider,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "System",
                PhoneNumberConfirmed = false
            };

            var result = await _userManager.CreateAsync(user, password);
            if (result.Succeeded)
            {
                // Add user to role
                var roleResult = await _userManager.AddToRoleAsync(user, roleName);
                if (roleResult.Succeeded)
                {
                    _logger.LogInformation("Created test user: {Username} with role {Role}", username, roleName);
                }
                else
                {
                    _logger.LogError("Failed to add role {Role} to user {Username}: {Errors}",
                        roleName, username, string.Join(", ", roleResult.Errors.Select(e => e.Description)));
                }
            }
            else
            {
                _logger.LogError("Failed to create test user {Username}: {Errors}",
                    username, string.Join(", ", result.Errors.Select(e => e.Description)));
            }
        }
    }

    // Extension methods for easy registration
    public static class DatabaseSeedingServiceExtensions
    {
        public static IServiceCollection AddDatabaseSeeding(this IServiceCollection services)
        {
            services.AddScoped<IDatabaseSeedingService, DatabaseSeedingService>();
            return services;
        }

        public static async Task SeedDatabaseAsync(this IServiceProvider serviceProvider)
        {
            using var scope = serviceProvider.CreateScope();
            var seedingService = scope.ServiceProvider.GetRequiredService<IDatabaseSeedingService>();
            await seedingService.SeedAsync();
        }
    }
}
