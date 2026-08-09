using System.ComponentModel.DataAnnotations;

namespace ErpSystem.Core.DTOs.Finance;

/// <summary>
/// Defines one governed, template-backed Finance report delivery schedule.
/// Times are interpreted in Ghana time (UTC, with no daylight-saving shift).
/// </summary>
public class CreateFinanceReportScheduleDto
{
    [Required]
    public Guid ReportTemplateId { get; set; }

    [Required, StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required, StringLength(20)]
    public string Frequency { get; set; } = "Monthly";

    public TimeOnly TimeOfDay { get; set; } = new(7, 0);
    public int? DayOfWeek { get; set; }
    public int? DayOfMonth { get; set; }
    public DateTime StartDate { get; set; } = DateTime.UtcNow.Date;
    public DateTime? EndDate { get; set; }

    [Required, StringLength(10)]
    public string ExportFormat { get; set; } = "PDF";

    public Dictionary<string, object>? FilterOverrides { get; set; }
    public List<Guid> RecipientUserIds { get; set; } = [];

    [Range(1, 10)]
    public int MaximumRetryAttempts { get; set; } = 3;
}

public sealed class UpdateFinanceReportScheduleDto : CreateFinanceReportScheduleDto
{
    [Required]
    public string RowVersion { get; set; } = string.Empty;
}

public sealed class FinanceReportScheduleDecisionDto
{
    [Required, StringLength(500, MinimumLength = 5)]
    public string Reason { get; set; } = string.Empty;

    [Required]
    public string RowVersion { get; set; } = string.Empty;
}

public sealed class FinanceReportScheduleDto
{
    public Guid Id { get; set; }
    public Guid ReportTemplateId { get; set; }
    public Guid ReportId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ReportName { get; set; } = string.Empty;
    public string TemplateName { get; set; } = string.Empty;
    public int TemplateVersion { get; set; }
    public string Frequency { get; set; } = string.Empty;
    public TimeOnly TimeOfDay { get; set; }
    public int? DayOfWeek { get; set; }
    public int? DayOfMonth { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public DateTime? NextExecutionDate { get; set; }
    public DateTime? LastExecutionDate { get; set; }
    public string ExportFormat { get; set; } = string.Empty;
    public List<Guid> RecipientUserIds { get; set; } = [];
    public int MaximumRetryAttempts { get; set; }
    public int ConsecutiveFailureCount { get; set; }
    public string? LastError { get; set; }
    public string Status { get; set; } = string.Empty;
    public string RowVersion { get; set; } = string.Empty;
}

public sealed class FinanceReportExecutionDto
{
    public Guid Id { get; set; }
    public Guid ScheduleId { get; set; }
    public string ScheduleName { get; set; } = string.Empty;
    public string ReportName { get; set; } = string.Empty;
    public int? TemplateVersion { get; set; }
    public DateTime? ScheduledFor { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int AttemptNumber { get; set; }
    public string Trigger { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
    public Guid? ExportId { get; set; }
    public string? FileName { get; set; }
    public long? FileSize { get; set; }
}

public sealed class FinanceReportAutomationTemplateDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ReportName { get; set; } = string.Empty;
    public int Version { get; set; }
    public List<string> OutputFormats { get; set; } = [];
}

public sealed class FinanceReportAutomationRecipientDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}

public sealed class FinanceReportAutomationWorkspaceDto
{
    public List<FinanceReportScheduleDto> Schedules { get; set; } = [];
    public List<FinanceReportExecutionDto> RecentExecutions { get; set; } = [];
    public List<FinanceReportAutomationTemplateDto> Templates { get; set; } = [];
    public List<FinanceReportAutomationRecipientDto> Recipients { get; set; } = [];
    public int ActiveSchedules { get; set; }
    public int FailedExecutions { get; set; }
    public int ArtifactsReady { get; set; }
}

public sealed class FinanceReportAutomationProcessResultDto
{
    public int DueCount { get; set; }
    public int SucceededCount { get; set; }
    public int FailedCount { get; set; }
    public int SkippedCount { get; set; }
}

public sealed class FinanceReportArtifactDto
{
    public Stream Content { get; set; } = Stream.Null;
    public string ContentType { get; set; } = "application/octet-stream";
    public string FileName { get; set; } = string.Empty;
}
