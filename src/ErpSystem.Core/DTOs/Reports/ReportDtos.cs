using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace ErpSystem.Core.DTOs.Reports
{
    public class ReportDefinitionDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string CreatedBy { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime? LastRun { get; set; }
        public DateTime? NextRun { get; set; }
        public bool IsScheduled { get; set; }
        public bool IsFavorite { get; set; }
        public Guid? ModuleId { get; set; }
        public string? ModuleName { get; set; }
        public Dictionary<string, object>? Parameters { get; set; }
        public string? Query { get; set; }
        public List<ReportColumnDto>? Columns { get; set; }
        public ReportVisualizationDto? Visualization { get; set; }
        public List<string>? Tags { get; set; }
        public List<string>? AssignedRoles { get; set; } // Role names assigned to this report
    }

    public class CreateReportDto
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public Dictionary<string, object>? Parameters { get; set; }
        public string? Query { get; set; }
        public List<ReportColumnDto>? Columns { get; set; }
        public ReportVisualizationDto? Visualization { get; set; }
        public List<string>? Tags { get; set; }
    }

    public class UpdateReportDto
    {
        public string? Name { get; set; }
        public string? Description { get; set; }
        public string? Status { get; set; }
        public Dictionary<string, object>? Parameters { get; set; }
        public string? Query { get; set; }
        public List<ReportColumnDto>? Columns { get; set; }
        public ReportVisualizationDto? Visualization { get; set; }
        public List<string>? Tags { get; set; }
    }

    public class ReportColumnDto
    {
        public string Name { get; set; } = string.Empty;
        public string DataType { get; set; } = string.Empty;
        public string? DisplayName { get; set; }
        public bool IsVisible { get; set; } = true;
        public string? Format { get; set; }
        public int Order { get; set; }
        public string? AggregationType { get; set; } // Sum, Average, Count, etc.
    }

    public class ReportVisualizationDto
    {
        public string Type { get; set; } = string.Empty; // Chart, Table, Dashboard
        public string? ChartType { get; set; } // Bar, Line, Pie, etc.
        public Dictionary<string, object>? Configuration { get; set; }
        public string? XAxis { get; set; }
        public string? YAxis { get; set; }
        public List<string>? Series { get; set; }
    }

    public class ExecuteReportDto
    {
        public Dictionary<string, object>? Parameters { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public int? MaxRows { get; set; }
        public bool IncludeMetadata { get; set; } = true;
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 100;

        /// <summary>
        /// Marks an execution initiated by the server's authorized export workflow. This value is
        /// never accepted from HTTP JSON; protected providers may use it to apply export authority
        /// and their saved export ceiling without weakening interactive report-run authorization.
        /// </summary>
        [JsonIgnore]
        public bool IsExportExecution { get; set; }

        [JsonIgnore]
        public ReportTemplateGenerationContextDto? TemplateContext { get; set; }
    }

    public class ReportResultDto
    {
        public Guid ReportId { get; set; }
        public string ReportName { get; set; } = string.Empty;
        public DateTime ExecutedAt { get; set; }
        public TimeSpan ExecutionTime { get; set; }
        public int TotalRows { get; set; }
        public List<ReportColumnDto> Columns { get; set; } = new();
        public List<Dictionary<string, object>> Data { get; set; } = new();
        public ReportMetadataDto? Metadata { get; set; }
        public List<ReportChartDataDto>? ChartData { get; set; }
        // Pagination info
        public int CurrentPage { get; set; } = 1;
        public int PageSize { get; set; } = 100;
        public int TotalPages { get; set; }
        public bool HasNextPage { get; set; }
        public bool HasPreviousPage { get; set; }
    }

    public class ReportMetadataDto
    {
        public Dictionary<string, object>? Parameters { get; set; }
        public string? Query { get; set; }
        public DateTime? DataAsOf { get; set; }
        public string? DataSource { get; set; }
        public Dictionary<string, object>? Statistics { get; set; }
        public ReportTemplateGenerationContextDto? TemplateGeneration { get; set; }
    }

    public class ReportChartDataDto
    {
        public string Label { get; set; } = string.Empty;
        public object Value { get; set; } = new();
        public string? Color { get; set; }
        public Dictionary<string, object>? Metadata { get; set; }
    }

    public class ExportReportDto
    {
        public string Format { get; set; } = "pdf"; // pdf, csv, xlsx, json
        public Dictionary<string, object>? Parameters { get; set; }
        public bool IncludeCharts { get; set; } = true;
        public bool IncludeHeaders { get; set; } = true;
        public string? Template { get; set; }

        [JsonIgnore]
        public ReportTemplateGenerationContextDto? TemplateContext { get; set; }
    }

    public class ReportExportResultDto
    {
        /// <summary>
        /// Identifies the durable export audit record. Scheduled delivery uses
        /// this key to attach private-file evidence without creating a duplicate
        /// export record alongside the existing reporting service.
        /// </summary>
        public Guid ExportId { get; set; }
        public Guid ReportId { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime ExportedAt { get; set; }
        public string DownloadUrl { get; set; } = string.Empty;
        public byte[] Data { get; set; } = Array.Empty<byte>();
        public string ContentType { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public long FileSize { get; set; }
    }

    public class CreateReportScheduleDto
    {
        public string Name { get; set; } = string.Empty;
        public string Frequency { get; set; } = string.Empty; // daily, weekly, monthly, quarterly
        public TimeOnly TimeOfDay { get; set; }
        public int? DayOfWeek { get; set; } // For weekly schedules (0 = Sunday)
        public int? DayOfMonth { get; set; } // For monthly schedules
        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public List<string>? EmailRecipients { get; set; }
        public string? ExportFormat { get; set; } = "pdf";
        public Dictionary<string, object>? Parameters { get; set; }
        public bool IsActive { get; set; } = true;
    }

    public class ReportScheduleDto
    {
        public Guid Id { get; set; }
        public Guid ReportId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Frequency { get; set; } = string.Empty;
        public TimeOnly TimeOfDay { get; set; }
        public int? DayOfWeek { get; set; }
        public int? DayOfMonth { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public DateTime? NextExecutionDate { get; set; }
        public DateTime? LastExecutionDate { get; set; }
        public List<string>? EmailRecipients { get; set; }
        public string? ExportFormat { get; set; }
        public Dictionary<string, object>? Parameters { get; set; }
        public bool IsActive { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }

    public class ReportTemplateDto
    {
        public Guid Id { get; set; }
        public Guid? ReportId { get; set; }
        public string? ReportName { get; set; }
        public string TemplateKey { get; set; } = string.Empty;
        public int Version { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Audience { get; set; } = string.Empty;
        public string Cadence { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string DefaultOutputFormat { get; set; } = string.Empty;
        public List<string> OutputFormats { get; set; } = new();
        public Dictionary<string, object>? SavedFilters { get; set; }
        public Dictionary<string, object>? GenerationMetadata { get; set; }
        public string? ChartType { get; set; }
        public bool IsCustom { get; set; }
        public string CreatedBy { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime? LastUsed { get; set; }
        public int UsageCount { get; set; }
        public List<string>? Tags { get; set; }
        public string? PreviewImage { get; set; }
        public Dictionary<string, object>? Configuration { get; set; }
        public DateTime? LastGeneratedAt { get; set; }
        public Guid? LastGeneratedBy { get; set; }
        public string? LastGenerationFormat { get; set; }
        public string RowVersion { get; set; } = string.Empty;
    }

    public class ReportAnalyticsDto
    {
        public int TotalReports { get; set; }
        public int ScheduledReports { get; set; }
        public int ReportsRunToday { get; set; }
        public int DataSourcesConnected { get; set; }
        public int TotalExports { get; set; }
        public double AvgGenerationTime { get; set; }
        public List<ReportUsageStatsDto>? UsageStats { get; set; }
        public List<ReportPerformanceDto>? PerformanceMetrics { get; set; }
        public List<TopReportDto>? TopReports { get; set; }
    }

    public class ReportUsageStatsDto
    {
        public string Period { get; set; } = string.Empty;
        public int ReportsRun { get; set; }
        public int UniqueUsers { get; set; }
        public double AvgExecutionTime { get; set; }
        public DateTime Date { get; set; }
    }

    public class ReportPerformanceDto
    {
        public Guid ReportId { get; set; }
        public string ReportName { get; set; } = string.Empty;
        public double AvgExecutionTime { get; set; }
        public int ExecutionCount { get; set; }
        public DateTime LastRun { get; set; }
        public string Status { get; set; } = string.Empty;
    }

    public class TopReportDto
    {
        public Guid ReportId { get; set; }
        public string ReportName { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public int ExecutionCount { get; set; }
        public int UniqueUsers { get; set; }
        public DateTime LastRun { get; set; }
        public double AvgRating { get; set; }
    }

    public class CreateReportTemplateDto
    {
        [Required]
        public Guid ReportId { get; set; }

        [Required, StringLength(80)]
        public string TemplateKey { get; set; } = string.Empty;

        [Required, StringLength(200)]
        public string Name { get; set; } = string.Empty;

        [StringLength(1000)]
        public string Description { get; set; } = string.Empty;

        [Required, StringLength(50)]
        public string Category { get; set; } = string.Empty;

        [Required, StringLength(20)]
        public string Type { get; set; } = string.Empty;
        [Required] public string Audience { get; set; } = "Finance";
        [Required] public string Cadence { get; set; } = "AdHoc";
        [Required] public string DefaultOutputFormat { get; set; } = "Online";
        public List<string> OutputFormats { get; set; } = ["Online"];
        public Dictionary<string, object>? SavedFilters { get; set; }
        public Dictionary<string, object>? GenerationMetadata { get; set; }
        public string? ChartType { get; set; }
        public bool IsCustom { get; set; } = true;
        public List<string>? Tags { get; set; }
        public string? PreviewImage { get; set; }
        public Dictionary<string, object>? Configuration { get; set; }
    }

    public class UpdateReportTemplateDto : CreateReportTemplateDto
    {
        [Required]
        public string RowVersion { get; set; } = string.Empty;
    }

    public class ReportTemplateLifecycleActionDto
    {
        [Required]
        public string RowVersion { get; set; } = string.Empty;
    }

    public class CloneReportTemplateDto : ReportTemplateLifecycleActionDto
    {
        [StringLength(200)]
        public string? Name { get; set; }
    }

    public class GenerateReportTemplateDto
    {
        public string Format { get; set; } = "Online";
        public Dictionary<string, object>? FilterOverrides { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 100;
        public bool IncludeCharts { get; set; } = true;
        public bool IncludeHeaders { get; set; } = true;
    }

    public class ReportTemplateGenerationContextDto
    {
        public Guid TemplateId { get; set; }
        public string TemplateKey { get; set; } = string.Empty;
        public string TemplateName { get; set; } = string.Empty;
        public int Version { get; set; }
        public string Audience { get; set; } = string.Empty;
        public string Cadence { get; set; } = string.Empty;
        public string OutputFormat { get; set; } = string.Empty;
        public Dictionary<string, object>? GenerationMetadata { get; set; }
    }

    public class ReportRoleAssignmentDto
    {
        public Guid Id { get; set; }
        public Guid ReportId { get; set; }
        public string ReportName { get; set; } = string.Empty;
        public Guid RoleId { get; set; }
        public string RoleName { get; set; } = string.Empty;
        public bool CanRead { get; set; }
        public bool CanExecute { get; set; }
        public bool CanExport { get; set; }
        public bool CanEdit { get; set; }
        public bool CanSchedule { get; set; }
        public DateTime AssignedAt { get; set; }
        public string? AssignedBy { get; set; }
    }

    public class CreateReportRoleAssignmentDto
    {
        public Guid ReportId { get; set; }
        public Guid RoleId { get; set; }
        public bool CanRead { get; set; } = true;
        public bool CanExecute { get; set; } = true;
        public bool CanExport { get; set; } = false;
        public bool CanEdit { get; set; } = false;
        public bool CanSchedule { get; set; } = false;
    }

    public class UpdateReportRoleAssignmentDto
    {
        public bool CanRead { get; set; }
        public bool CanExecute { get; set; }
        public bool CanExport { get; set; }
        public bool CanEdit { get; set; }
        public bool CanSchedule { get; set; }
    }

    public class BulkAssignRolesToReportDto
    {
        public Guid ReportId { get; set; }
        public List<CreateReportRoleAssignmentDto> RoleAssignments { get; set; } = new();
    }

    public class ReportAccessDto
    {
        public Guid ReportId { get; set; }
        public string ReportName { get; set; } = string.Empty;
        public bool HasAccess { get; set; }
        public bool CanRead { get; set; }
        public bool CanExecute { get; set; }
        public bool CanExport { get; set; }
        public bool CanEdit { get; set; }
        public bool CanSchedule { get; set; }
        public List<string> AccessibleRoles { get; set; } = new();
    }

    public class AssignModuleDto
    {
        [Required(ErrorMessage = "ModuleId is required")]
        public Guid ModuleId { get; set; }
    }
}
