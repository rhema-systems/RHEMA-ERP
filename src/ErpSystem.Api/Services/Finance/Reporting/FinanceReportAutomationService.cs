using System.Security.Cryptography;
using System.Text.Json;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.DTOs.Reports;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Core.Models;
using ErpSystem.Core.Services.Finance;
using ErpSystem.Data;
using ErpSystem.Shared;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.Finance.Reporting;

/// <summary>
/// User-facing lifecycle for FR-RP-010. It extends the shared report/template
/// model so scheduled output remains visible in the same reporting audit trail
/// as an interactive export.
/// </summary>
public sealed class FinanceReportAutomationService : IFinanceReportAutomationService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IFinanceAuditService _audit;
    private readonly IFileStorageService _storage;
    private readonly FinanceReportAutomationProcessor _processor;

    public FinanceReportAutomationService(
        ApplicationDbContext db,
        ICurrentUserService currentUser,
        IFinanceAuditService audit,
        IFileStorageService storage,
        FinanceReportAutomationProcessor processor)
    {
        _db = db;
        _currentUser = currentUser;
        _audit = audit;
        _storage = storage;
        _processor = processor;
    }

    public async Task<FinanceReportAutomationWorkspaceDto> GetWorkspaceAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = _currentUser.GetRequiredFinanceTenantId();
        var schedules = await _db.ReportSchedules.AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.ReportTemplateId != null && !item.IsDeleted)
            .Include(item => item.Report)
            .Include(item => item.ReportTemplate)
            .OrderBy(item => item.NextExecutionDate)
            .ToListAsync(cancellationToken);

        var scheduleIds = schedules.Select(item => item.Id).ToList();
        var executions = await _db.ReportExecutions.AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.ReportScheduleId != null
                && scheduleIds.Contains(item.ReportScheduleId.Value) && !item.IsDeleted)
            .Include(item => item.ReportSchedule)
            .Include(item => item.Report)
            .Include(item => item.ReportExport)
            .OrderByDescending(item => item.StartedAt ?? item.ExecutedAt)
            .Take(100)
            .ToListAsync(cancellationToken);

        var templates = await _db.ReportTemplates.AsNoTracking()
            .Where(item => item.TenantId == tenantId && !item.IsDeleted && item.Status == "Published"
                && item.Audience == "Finance" && item.ReportId != null)
            .Include(item => item.Report)
            .OrderBy(item => item.Name)
            .ThenByDescending(item => item.Version)
            .ToListAsync(cancellationToken);

        var recipients = await _db.Users.AsNoTracking()
            .Where(item => item.IsActive &&
                (item.TenantId == tenantId || item.UserTenants.Any(membership =>
                    membership.TenantId == tenantId
                    && !membership.IsDeleted
                    && membership.Status == UserTenantStatus.Active
                    && (membership.ExpiresAt == null || membership.ExpiresAt > DateTime.UtcNow))))
            .OrderBy(item => item.FirstName).ThenBy(item => item.LastName)
            .Select(item => new FinanceReportAutomationRecipientDto
            {
                Id = item.Id,
                Name = (item.FirstName + " " + item.LastName).Trim(),
                Email = item.Email ?? string.Empty
            })
            .ToListAsync(cancellationToken);

        return new FinanceReportAutomationWorkspaceDto
        {
            Schedules = schedules.Select(Map).ToList(),
            RecentExecutions = executions.Select(Map).ToList(),
            Templates = templates.Select(item => new FinanceReportAutomationTemplateDto
            {
                Id = item.Id,
                Name = item.Name,
                ReportName = item.Report?.Name ?? item.Name,
                Version = item.Version,
                OutputFormats = DeserializeList<string>(item.OutputFormats)
                    .Where(format => ReportAutomationValues.ExportFormats.Contains(format)).ToList()
            }).ToList(),
            Recipients = recipients,
            ActiveSchedules = schedules.Count(item => item.IsActive && item.Status == ReportAutomationValues.Active),
            FailedExecutions = executions.Count(item => item.Status == ReportAutomationValues.Failed),
            ArtifactsReady = executions.Count(item => item.ReportExport?.StoragePath != null)
        };
    }

    public async Task<FinanceReportScheduleDto> CreateAsync(
        CreateFinanceReportScheduleDto request, CancellationToken cancellationToken = default)
    {
        var tenantId = _currentUser.GetRequiredFinanceTenantId();
        var userId = CurrentUserId();
        var template = await ValidateRequestAsync(request, tenantId, cancellationToken);
        var now = DateTime.UtcNow;
        var schedule = new ReportSchedule
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ReportId = template.ReportId!.Value,
            ReportTemplateId = template.Id,
            Name = request.Name.Trim(),
            Frequency = CanonicalFrequency(request.Frequency),
            TimeOfDay = request.TimeOfDay,
            DayOfWeek = request.DayOfWeek,
            DayOfMonth = request.DayOfMonth,
            StartDate = DateTime.SpecifyKind(request.StartDate.Date, DateTimeKind.Utc),
            EndDate = request.EndDate.HasValue ? DateTime.SpecifyKind(request.EndDate.Value.Date, DateTimeKind.Utc) : null,
            ExportFormat = request.ExportFormat.Trim().ToUpperInvariant(),
            Parameters = Serialize(request.FilterOverrides),
            RecipientUserIds = JsonSerializer.Serialize(RecipientsOrOwner(request.RecipientUserIds, userId), JsonOptions),
            RunAsUserId = userId,
            MaximumRetryAttempts = request.MaximumRetryAttempts,
            IsActive = true,
            Status = ReportAutomationValues.Active,
            CreatedBy = _currentUser.UserName,
            CreatedById = userId
        };
        schedule.NextExecutionDate = FinanceReportScheduleCalculator.FirstOccurrence(
            schedule.Frequency, schedule.StartDate, schedule.TimeOfDay,
            schedule.DayOfWeek, schedule.DayOfMonth, now);
        if (schedule.EndDate.HasValue && schedule.NextExecutionDate > schedule.EndDate.Value.Date.AddDays(1).AddTicks(-1))
            throw new InvalidOperationException("The schedule has no occurrence inside its start/end date range.");

        _db.ReportSchedules.Add(schedule);
        await _db.SaveChangesAsync(cancellationToken);
        await AuditAsync(FinanceAuditEvents.ReportScheduleCreated, schedule, null, request, cancellationToken);
        return await RequiredDtoAsync(schedule.Id, tenantId, cancellationToken);
    }

    public async Task<FinanceReportScheduleDto> UpdateAsync(
        Guid id, UpdateFinanceReportScheduleDto request, CancellationToken cancellationToken = default)
    {
        var tenantId = _currentUser.GetRequiredFinanceTenantId();
        var schedule = await RequiredAsync(id, tenantId, cancellationToken);
        var before = Map(schedule);
        var template = await ValidateRequestAsync(request, tenantId, cancellationToken);
        SetConcurrencyToken(schedule, request.RowVersion);

        schedule.ReportId = template.ReportId!.Value;
        schedule.ReportTemplateId = template.Id;
        schedule.Name = request.Name.Trim();
        schedule.Frequency = CanonicalFrequency(request.Frequency);
        schedule.TimeOfDay = request.TimeOfDay;
        schedule.DayOfWeek = request.DayOfWeek;
        schedule.DayOfMonth = request.DayOfMonth;
        schedule.StartDate = DateTime.SpecifyKind(request.StartDate.Date, DateTimeKind.Utc);
        schedule.EndDate = request.EndDate.HasValue ? DateTime.SpecifyKind(request.EndDate.Value.Date, DateTimeKind.Utc) : null;
        schedule.ExportFormat = request.ExportFormat.Trim().ToUpperInvariant();
        schedule.Parameters = Serialize(request.FilterOverrides);
        schedule.RecipientUserIds = JsonSerializer.Serialize(
            RecipientsOrOwner(request.RecipientUserIds, schedule.RunAsUserId ?? CurrentUserId()), JsonOptions);
        schedule.MaximumRetryAttempts = request.MaximumRetryAttempts;
        schedule.NextExecutionDate = FinanceReportScheduleCalculator.FirstOccurrence(
            schedule.Frequency, schedule.StartDate, schedule.TimeOfDay,
            schedule.DayOfWeek, schedule.DayOfMonth, DateTime.UtcNow);
        schedule.UpdatedAt = DateTime.UtcNow;
        schedule.UpdatedBy = _currentUser.UserName;
        schedule.LastModifiedById = CurrentUserId();

        await SaveWithConcurrencyMessageAsync(cancellationToken);
        await AuditAsync(FinanceAuditEvents.ReportScheduleUpdated, schedule, before, request, cancellationToken);
        return await RequiredDtoAsync(id, tenantId, cancellationToken);
    }

    public Task<FinanceReportScheduleDto> PauseAsync(
        Guid id, FinanceReportScheduleDecisionDto request, CancellationToken cancellationToken = default) =>
        ChangeStatusAsync(id, request, ReportAutomationValues.Paused, false,
            FinanceAuditEvents.ReportSchedulePaused, cancellationToken);

    public Task<FinanceReportScheduleDto> ResumeAsync(
        Guid id, FinanceReportScheduleDecisionDto request, CancellationToken cancellationToken = default) =>
        ChangeStatusAsync(id, request, ReportAutomationValues.Active, true,
            FinanceAuditEvents.ReportScheduleResumed, cancellationToken);

    public async Task<FinanceReportAutomationProcessResultDto> RunNowAsync(
        Guid id, CancellationToken cancellationToken = default)
    {
        var tenantId = _currentUser.GetRequiredFinanceTenantId();
        _ = await RequiredAsync(id, tenantId, cancellationToken);
        return await _processor.ProcessScheduleAsync(id, DateTime.UtcNow, true, tenantId, cancellationToken);
    }

    public Task<FinanceReportAutomationProcessResultDto> ProcessDueAsync(
        DateTime asOfUtc, CancellationToken cancellationToken = default) =>
        _processor.ProcessDueAsync(DateTime.SpecifyKind(asOfUtc, DateTimeKind.Utc),
            _currentUser.GetRequiredFinanceTenantId(), cancellationToken);

    public async Task<FinanceReportArtifactDto> DownloadArtifactAsync(
        Guid exportId, CancellationToken cancellationToken = default)
    {
        var tenantId = _currentUser.GetRequiredFinanceTenantId();
        var export = await _db.ReportExports.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == exportId && item.TenantId == tenantId && !item.IsDeleted,
                cancellationToken) ?? throw new KeyNotFoundException("Report artifact not found.");
        if (string.IsNullOrWhiteSpace(export.StoragePath))
            throw new InvalidOperationException("This report export does not have a retained artifact.");

        var content = await _storage.DownloadFileAsync(export.StoragePath, export.Id);
        await _audit.RecordAsync(new FinanceAuditEventDto
        {
            EventType = FinanceAuditEvents.ReportScheduleArtifactDownloaded,
            TenantId = tenantId,
            Resource = nameof(ReportExport),
            ResourceId = export.Id.ToString(),
            Context = new { export.FileName, export.Sha256Checksum }
        }, cancellationToken);
        return new FinanceReportArtifactDto
        {
            Content = content,
            ContentType = export.ContentType ?? "application/octet-stream",
            FileName = export.FileName
        };
    }

    private async Task<FinanceReportScheduleDto> ChangeStatusAsync(
        Guid id, FinanceReportScheduleDecisionDto request, string status, bool active,
        string auditEvent, CancellationToken cancellationToken)
    {
        var tenantId = _currentUser.GetRequiredFinanceTenantId();
        var schedule = await RequiredAsync(id, tenantId, cancellationToken);
        var before = Map(schedule);
        SetConcurrencyToken(schedule, request.RowVersion);
        schedule.Status = status;
        schedule.IsActive = active;
        schedule.PausedAt = active ? null : DateTime.UtcNow;
        schedule.PausedById = active ? null : CurrentUserId();
        schedule.LastError = active ? null : schedule.LastError;
        if (active)
        {
            // Resuming recalculates from now so a deliberately paused schedule
            // does not emit a burst of stale reports.
            schedule.NextExecutionDate = FinanceReportScheduleCalculator.FirstOccurrence(
                schedule.Frequency, schedule.StartDate, schedule.TimeOfDay,
                schedule.DayOfWeek, schedule.DayOfMonth, DateTime.UtcNow);
        }
        await SaveWithConcurrencyMessageAsync(cancellationToken);
        await AuditAsync(auditEvent, schedule, before, new { request.Reason }, cancellationToken);
        return await RequiredDtoAsync(id, tenantId, cancellationToken);
    }

    private async Task<ReportTemplate> ValidateRequestAsync(
        CreateFinanceReportScheduleDto request, Guid tenantId, CancellationToken cancellationToken)
    {
        FinanceReportScheduleCalculator.Validate(request.Frequency, request.DayOfWeek, request.DayOfMonth);
        if (!ReportAutomationValues.ExportFormats.Contains(request.ExportFormat))
            throw new InvalidOperationException("Scheduled Finance reports support PDF or XLSX output.");
        if (request.EndDate.HasValue && request.EndDate.Value.Date < request.StartDate.Date)
            throw new InvalidOperationException("End date cannot be earlier than start date.");

        var template = await _db.ReportTemplates
            .Include(item => item.Report)
            .SingleOrDefaultAsync(item => item.Id == request.ReportTemplateId && item.TenantId == tenantId
                && !item.IsDeleted, cancellationToken)
            ?? throw new KeyNotFoundException("Report template not found.");
        if (template.Status != "Published" || template.Audience != "Finance" || template.ReportId == null)
            throw new InvalidOperationException("Only a published Finance template linked to a report can be scheduled.");
        var formats = DeserializeList<string>(template.OutputFormats);
        if (!formats.Contains(request.ExportFormat, StringComparer.OrdinalIgnoreCase))
            throw new InvalidOperationException("The selected template does not allow the requested export format.");

        var recipientIds = request.RecipientUserIds.Distinct().ToList();
        if (recipientIds.Count > 0)
        {
            // A user's primary TenantId is not the complete access model. TDC
            // staff can be granted active membership in more than one tenant,
            // so recipient validation deliberately follows the same UserTenant
            // rules as authorization instead of rejecting legitimate recipients.
            var validCount = await _db.Users.CountAsync(item => recipientIds.Contains(item.Id)
                && item.IsActive
                && (item.TenantId == tenantId || item.UserTenants.Any(membership =>
                    membership.TenantId == tenantId
                    && !membership.IsDeleted
                    && membership.Status == UserTenantStatus.Active
                    && (membership.ExpiresAt == null || membership.ExpiresAt > DateTime.UtcNow))),
                cancellationToken);
            if (validCount != recipientIds.Count)
                throw new InvalidOperationException("Every report recipient must be an active user in the current tenant.");
        }
        return template;
    }

    private async Task<ReportSchedule> RequiredAsync(Guid id, Guid tenantId, CancellationToken cancellationToken) =>
        await _db.ReportSchedules.Include(item => item.Report).Include(item => item.ReportTemplate)
            .SingleOrDefaultAsync(item => item.Id == id && item.TenantId == tenantId && !item.IsDeleted, cancellationToken)
        ?? throw new KeyNotFoundException("Finance report schedule not found.");

    private async Task<FinanceReportScheduleDto> RequiredDtoAsync(Guid id, Guid tenantId, CancellationToken cancellationToken) =>
        Map(await RequiredAsync(id, tenantId, cancellationToken));

    private async Task AuditAsync(string eventType, ReportSchedule schedule, object? before, object after,
        CancellationToken cancellationToken) => await _audit.RecordAsync(new FinanceAuditEventDto
        {
            EventType = eventType,
            TenantId = schedule.TenantId,
            Resource = nameof(ReportSchedule),
            ResourceId = schedule.Id.ToString(),
            BeforeValues = before,
            AfterValues = after
        }, cancellationToken);

    private Guid CurrentUserId() => Guid.TryParse(_currentUser.UserId, out var id) && id != Guid.Empty
        ? id : throw new InvalidOperationException("Finance report automation requires an authenticated user.");

    private void SetConcurrencyToken(ReportSchedule schedule, string encoded)
    {
        try { _db.Entry(schedule).Property(item => item.RowVersion).OriginalValue = Convert.FromBase64String(encoded); }
        catch (FormatException) { throw new InvalidOperationException("The schedule row version is invalid. Refresh and try again."); }
    }

    private async Task SaveWithConcurrencyMessageAsync(CancellationToken cancellationToken)
    {
        try { await _db.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { throw new InvalidOperationException("This schedule changed after you opened it. Refresh and try again."); }
    }

    private static string CanonicalFrequency(string value) =>
        ReportAutomationValues.Frequencies.Single(item => item.Equals(value, StringComparison.OrdinalIgnoreCase));
    private static string? Serialize(object? value) => value == null ? null : JsonSerializer.Serialize(value, JsonOptions);
    private static List<Guid> RecipientsOrOwner(IEnumerable<Guid> values, Guid owner) =>
        values.Append(owner).Distinct().ToList();
    private static List<T> DeserializeList<T>(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try { return JsonSerializer.Deserialize<List<T>>(json, JsonOptions) ?? []; }
        catch (JsonException) { return []; }
    }

    internal static FinanceReportScheduleDto Map(ReportSchedule item) => new()
    {
        Id = item.Id,
        ReportTemplateId = item.ReportTemplateId ?? Guid.Empty,
        ReportId = item.ReportId,
        Name = item.Name,
        ReportName = item.Report?.Name ?? string.Empty,
        TemplateName = item.ReportTemplate?.Name ?? string.Empty,
        TemplateVersion = item.ReportTemplate?.Version ?? 0,
        Frequency = item.Frequency,
        TimeOfDay = item.TimeOfDay,
        DayOfWeek = item.DayOfWeek,
        DayOfMonth = item.DayOfMonth,
        StartDate = item.StartDate,
        EndDate = item.EndDate,
        NextExecutionDate = item.NextExecutionDate,
        LastExecutionDate = item.LastExecutionDate,
        ExportFormat = item.ExportFormat ?? "PDF",
        RecipientUserIds = DeserializeList<Guid>(item.RecipientUserIds),
        MaximumRetryAttempts = item.MaximumRetryAttempts,
        ConsecutiveFailureCount = item.ConsecutiveFailureCount,
        LastError = item.LastError,
        Status = item.Status,
        RowVersion = Convert.ToBase64String(item.RowVersion)
    };

    private static FinanceReportExecutionDto Map(ReportExecution item) => new()
    {
        Id = item.Id,
        ScheduleId = item.ReportScheduleId ?? Guid.Empty,
        ScheduleName = item.ReportSchedule?.Name ?? string.Empty,
        ReportName = item.Report?.Name ?? string.Empty,
        TemplateVersion = item.TemplateVersion,
        ScheduledFor = item.ScheduledFor,
        StartedAt = item.StartedAt,
        CompletedAt = item.CompletedAt,
        AttemptNumber = item.AttemptNumber,
        Trigger = item.Trigger,
        Status = item.Status,
        ErrorMessage = item.ErrorMessage,
        ExportId = item.ReportExportId,
        FileName = item.ReportExport?.FileName,
        FileSize = item.ReportExport?.FileSize
    };
}

/// <summary>
/// Scoped execution engine used by both the API and the hosted worker. One
/// durable execution row is reused for all retries of a scheduled occurrence;
/// this prevents duplicate reports and duplicate recipient notifications.
/// </summary>
public sealed class FinanceReportAutomationProcessor
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly ApplicationDbContext _db;
    private readonly IReportTemplateLifecycleService _templates;
    private readonly IFileStorageService _storage;
    private readonly INotificationService _notifications;
    private readonly ILogger<FinanceReportAutomationProcessor> _logger;

    public FinanceReportAutomationProcessor(ApplicationDbContext db, IReportTemplateLifecycleService templates,
        IFileStorageService storage, INotificationService notifications, ILogger<FinanceReportAutomationProcessor> logger)
    {
        _db = db;
        _templates = templates;
        _storage = storage;
        _notifications = notifications;
        _logger = logger;
    }

    public async Task<FinanceReportAutomationProcessResultDto> ProcessDueAsync(
        DateTime asOfUtc, Guid? tenantId = null, CancellationToken cancellationToken = default)
    {
        var query = _db.ReportSchedules.IgnoreQueryFilters().AsNoTracking()
            .Where(item => !item.IsDeleted && item.IsActive && item.Status == ReportAutomationValues.Active
                && item.ReportTemplateId != null && item.NextExecutionDate != null
                && item.NextExecutionDate <= asOfUtc && (item.EndDate == null || item.EndDate >= asOfUtc.Date));
        if (tenantId.HasValue) query = query.Where(item => item.TenantId == tenantId.Value);
        var ids = await query.OrderBy(item => item.NextExecutionDate).Take(50).Select(item => item.Id)
            .ToListAsync(cancellationToken);
        var total = new FinanceReportAutomationProcessResultDto { DueCount = ids.Count };
        foreach (var id in ids)
        {
            var result = await ProcessScheduleAsync(id, asOfUtc, false, tenantId, cancellationToken);
            total.SucceededCount += result.SucceededCount;
            total.FailedCount += result.FailedCount;
            total.SkippedCount += result.SkippedCount;
        }
        return total;
    }

    public async Task<FinanceReportAutomationProcessResultDto> ProcessScheduleAsync(
        Guid scheduleId, DateTime asOfUtc, bool manual, Guid? requiredTenantId,
        CancellationToken cancellationToken = default)
    {
        var result = new FinanceReportAutomationProcessResultDto { DueCount = 1 };
        var schedule = await _db.ReportSchedules.IgnoreQueryFilters()
            .Include(item => item.ReportTemplate).Include(item => item.Report)
            .SingleOrDefaultAsync(item => item.Id == scheduleId && !item.IsDeleted, cancellationToken);
        if (schedule == null || (requiredTenantId.HasValue && schedule.TenantId != requiredTenantId.Value))
            throw new KeyNotFoundException("Finance report schedule not found.");
        if (schedule.ReportTemplateId == null || schedule.ReportTemplate == null || schedule.RunAsUserId == null)
            throw new InvalidOperationException("The schedule is missing its template or execution owner.");
        if (!manual && (!schedule.IsActive || schedule.Status != ReportAutomationValues.Active
            || schedule.NextExecutionDate == null || schedule.NextExecutionDate > asOfUtc))
        {
            result.SkippedCount = 1;
            return result;
        }

        var slot = manual ? DateTime.SpecifyKind(asOfUtc, DateTimeKind.Utc) : schedule.NextExecutionDate!.Value;
        var execution = await _db.ReportExecutions.IgnoreQueryFilters()
            .SingleOrDefaultAsync(item => item.TenantId == schedule.TenantId
                && item.ReportScheduleId == schedule.Id && item.ScheduledFor == slot && !item.IsDeleted,
                cancellationToken);
        if (execution?.Status == ReportAutomationValues.Succeeded)
        {
            result.SkippedCount = 1;
            return result;
        }
        if (execution?.Status == ReportAutomationValues.Running && execution.StartedAt > asOfUtc.AddMinutes(-30))
        {
            result.SkippedCount = 1;
            return result;
        }

        var isNewOccurrence = execution == null;
        execution ??= new ReportExecution
        {
            Id = Guid.NewGuid(),
            TenantId = schedule.TenantId,
            ReportId = schedule.ReportId,
            UserId = schedule.RunAsUserId.Value,
            ReportScheduleId = schedule.Id,
            ReportTemplateId = schedule.ReportTemplateId,
            TemplateVersion = schedule.ReportTemplate.Version,
            ScheduledFor = slot,
            Trigger = manual ? ReportAutomationValues.ManualTrigger : ReportAutomationValues.ScheduledTrigger,
            AttemptNumber = 0,
            CreatedBy = "FinanceReportAutomation"
        };
        if (execution.AttemptNumber >= schedule.MaximumRetryAttempts)
        {
            if (!manual) MarkScheduleInError(schedule, execution.ErrorMessage);
            await _db.SaveChangesAsync(cancellationToken);
            result.SkippedCount = 1;
            return result;
        }

        if (_db.Entry(execution).State == EntityState.Detached) _db.ReportExecutions.Add(execution);
        execution.AttemptNumber++;
        execution.Status = ReportAutomationValues.Running;
        execution.StartedAt = asOfUtc;
        execution.ExecutedAt = asOfUtc;
        execution.CompletedAt = null;
        execution.ErrorMessage = null;
        execution.Parameters = schedule.Parameters;
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException) when (isNewOccurrence)
        {
            // Multiple application nodes can observe the same due schedule. The
            // filtered unique index on tenant/schedule/slot is the final arbiter;
            // if another node inserted the occurrence first, this node backs out
            // without exporting or notifying recipients a second time.
            _db.Entry(execution).State = EntityState.Detached;
            var claimedByAnotherWorker = await _db.ReportExecutions.IgnoreQueryFilters().AsNoTracking()
                .AnyAsync(item => item.TenantId == schedule.TenantId
                    && item.ReportScheduleId == schedule.Id
                    && item.ScheduledFor == slot
                    && !item.IsDeleted, cancellationToken);
            if (!claimedByAnotherWorker) throw;
            result.SkippedCount = 1;
            return result;
        }

        Guid? generatedExportId = null;
        try
        {
            var exported = await _templates.ExportAsync(schedule.ReportTemplateId.Value, new GenerateReportTemplateDto
            {
                Format = schedule.ExportFormat ?? "PDF",
                FilterOverrides = DeserializeDictionary(schedule.Parameters),
                IncludeCharts = true,
                IncludeHeaders = true,
                PageSize = 1000
            }, schedule.TenantId, schedule.RunAsUserId.Value, true, cancellationToken);
            generatedExportId = exported.ExportId;

            await using var content = new MemoryStream(exported.Data, writable: false);
            var upload = await _storage.UploadFileAsync(new FileUploadRequest
            {
                FileStream = content,
                FileName = exported.FileName,
                FileSize = exported.Data.LongLength,
                ContentType = exported.ContentType,
                Category = "finance-report-artifacts",
                TenantId = schedule.TenantId.ToString(),
                Metadata = new Dictionary<string, string>
                {
                    ["scheduleId"] = schedule.Id.ToString(),
                    ["templateId"] = schedule.ReportTemplateId.Value.ToString(),
                    ["scheduledForUtc"] = slot.ToString("O")
                }
            });
            if (!upload.Success) throw new InvalidOperationException(upload.ErrorMessage ?? "Report artifact storage failed.");

            var exportRecord = await _db.ReportExports.IgnoreQueryFilters()
                .SingleAsync(item => item.Id == exported.ExportId && item.TenantId == schedule.TenantId, cancellationToken);
            exportRecord.StoragePath = upload.FilePath;
            exportRecord.ContentType = exported.ContentType;
            exportRecord.Sha256Checksum = Convert.ToHexString(SHA256.HashData(exported.Data)).ToLowerInvariant();
            exportRecord.RetainUntil = DateTime.UtcNow.AddYears(7);

            execution.ReportExportId = exportRecord.Id;
            execution.Status = ReportAutomationValues.Succeeded;
            execution.CompletedAt = DateTime.UtcNow;
            execution.ExecutionTime = execution.CompletedAt.Value - execution.StartedAt!.Value;
            execution.ResultMetadata = JsonSerializer.Serialize(new
            {
                exportRecord.FileName,
                exportRecord.FileSize,
                exportRecord.Sha256Checksum,
                Delivery = "InApplication"
            }, JsonOptions);
            if (!manual)
            {
                schedule.LastExecutionDate = slot;
                schedule.NextExecutionDate = FinanceReportScheduleCalculator.NextOccurrence(
                    schedule.Frequency, slot, schedule.DayOfWeek, schedule.DayOfMonth);
                schedule.ConsecutiveFailureCount = 0;
                schedule.LastError = null;
                if (schedule.EndDate.HasValue && schedule.NextExecutionDate > schedule.EndDate.Value.Date.AddDays(1).AddTicks(-1))
                {
                    schedule.IsActive = false;
                    schedule.Status = ReportAutomationValues.Paused;
                }
            }
            await _db.SaveChangesAsync(cancellationToken);
            await NotifyRecipientsAsync(schedule, exportRecord, cancellationToken);
            result.SucceededCount = 1;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogError(exception, "Finance report schedule {ScheduleId} failed for slot {ScheduledFor}.", schedule.Id, slot);
            execution.Status = ReportAutomationValues.Failed;
            execution.CompletedAt = DateTime.UtcNow;
            execution.ExecutionTime = execution.CompletedAt.Value - execution.StartedAt!.Value;
            execution.ErrorMessage = Truncate(exception.Message, 1000);
            if (generatedExportId.HasValue)
            {
                // The shared exporter persists its audit row before private file
                // storage begins. Keep that row, link it to the failed attempt,
                // and label it honestly so operators do not mistake an orphaned
                // metadata record for a downloadable completed artifact.
                var failedExport = _db.ReportExports.Local
                    .FirstOrDefault(item => item.Id == generatedExportId.Value);
                if (failedExport != null)
                {
                    failedExport.Status = "failed";
                    failedExport.ErrorMessage = execution.ErrorMessage;
                    execution.ReportExportId = failedExport.Id;
                }
            }
            if (!manual)
            {
                schedule.ConsecutiveFailureCount++;
                schedule.LastError = execution.ErrorMessage;
                if (execution.AttemptNumber >= schedule.MaximumRetryAttempts) MarkScheduleInError(schedule, execution.ErrorMessage);
            }
            await _db.SaveChangesAsync(cancellationToken);
            result.FailedCount = 1;
        }
        return result;
    }

    private async Task NotifyRecipientsAsync(ReportSchedule schedule, ReportExport export, CancellationToken cancellationToken)
    {
        var recipients = DeserializeList<Guid>(schedule.RecipientUserIds);
        if (schedule.RunAsUserId.HasValue) recipients.Add(schedule.RunAsUserId.Value);
        foreach (var recipient in recipients.Distinct())
        {
            try
            {
                await _notifications.CreateInAppNotificationAsync(recipient,
                    $"Finance report ready: {schedule.Name}",
                    $"{export.FileName} has been generated and is ready in Report Automation.",
                    "FinanceReportReady",
                    new Dictionary<string, object>
                    {
                        ["actionUrl"] = "/reports/automation",
                        ["scheduleId"] = schedule.Id,
                        ["exportId"] = export.Id
                    }, schedule.TenantId);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // The artifact is already durable. A notification transport issue
                // must not falsely mark a valid accounting report as failed.
                _logger.LogWarning(exception, "Could not notify user {UserId} for Finance report export {ExportId}.", recipient, export.Id);
            }
        }
    }

    private static void MarkScheduleInError(ReportSchedule schedule, string? error)
    {
        schedule.IsActive = false;
        schedule.Status = ReportAutomationValues.Error;
        schedule.LastError = Truncate(error ?? "Maximum retry attempts reached.", 1000);
    }

    private static Dictionary<string, object>? DeserializeDictionary(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try { return JsonSerializer.Deserialize<Dictionary<string, object>>(json, JsonOptions); }
        catch (JsonException) { return null; }
    }

    private static List<T> DeserializeList<T>(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try { return JsonSerializer.Deserialize<List<T>>(json, JsonOptions) ?? []; }
        catch (JsonException) { return []; }
    }

    private static string Truncate(string value, int length) => value.Length <= length ? value : value[..length];
}
