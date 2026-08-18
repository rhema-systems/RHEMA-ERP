using System.Data;
using System.Text.Json;
using System.Globalization;
using ClosedXML.Excel;
using ErpSystem.Core.DTOs.Reports;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Services;
using iText.Kernel.Pdf;
using iText.Layout;
using iText.Layout.Element;
using iText.Layout.Properties;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Data.Services;

public class DatabaseReportsService : IReportsService
{
    private readonly IReportRepository _reportRepository;
    private readonly IReportScheduleRepository _scheduleRepository;
    private readonly IReportTemplateRepository _templateRepository;
    private readonly IReportExecutionRepository _executionRepository;
    private readonly IUserReportFavoriteRepository _favoriteRepository;
    private readonly IReportExportRepository _exportRepository;
    private readonly IReportRoleAssignmentRepository _roleAssignmentRepository;
    private readonly ILogger<DatabaseReportsService> _logger;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IConfiguration _configuration;
    private readonly IReadOnlyList<ISystemReportProvider> _systemReportProviders;

    public DatabaseReportsService(
        IReportRepository reportRepository,
        IReportScheduleRepository scheduleRepository,
        IReportTemplateRepository templateRepository,
        IReportExecutionRepository executionRepository,
        IUserReportFavoriteRepository favoriteRepository,
        IReportExportRepository exportRepository,
        IReportRoleAssignmentRepository roleAssignmentRepository,
        ILogger<DatabaseReportsService> logger,
        IUnitOfWork unitOfWork,
        IConfiguration configuration,
        IEnumerable<ISystemReportProvider> systemReportProviders)
    {
        _reportRepository = reportRepository;
        _scheduleRepository = scheduleRepository;
        _templateRepository = templateRepository;
        _executionRepository = executionRepository;
        _favoriteRepository = favoriteRepository;
        _exportRepository = exportRepository;
        _roleAssignmentRepository = roleAssignmentRepository;
        _logger = logger;
        _unitOfWork = unitOfWork;
        _configuration = configuration;
        _systemReportProviders = systemReportProviders.ToList();
    }

    public async Task<List<ReportDefinitionDto>> GetReportsAsync(Guid tenantId, Guid userId, string? type = null, string? status = null, bool? favoriteOnly = null, bool bypassRoleFiltering = false)
    {
        try
        {
            IEnumerable<Report> reports;

            if (favoriteOnly == true)
            {
                reports = await _reportRepository.GetFavoriteReportsByUserAsync(userId, tenantId);
            }
            else
            {
                reports = await _reportRepository.GetReportsByTenantAsync(tenantId, type, status);
            }

            // An administrator may bypass custom report-role assignments, but record-level system
            // visibility still applies. In particular, a private Finance ad hoc definition must
            // not leak merely because the shared catalogue was opened in administration mode.
            if (bypassRoleFiltering)
            {
                var reportList = reports.ToList();
                var systemReports = reportList.Where(report => ProviderFor(report.Query) is not null);
                var authorizedSystemReports = await AuthorizeSystemReportsAsync(systemReports, true);
                reports = reportList.Where(report => !IsSystemIdentifier(report.Query))
                    .Concat(authorizedSystemReports);
            }
            // System-owned reports use their module provider's responsibility/permission model.
            // Custom reports retain the report-role assignment model.
            else
            {
                var reportList = reports.ToList();
                var systemReports = reportList.Where(r => ProviderFor(r.Query) is not null).ToList();
                // A provider-owned prefix is reserved even when the suffix is unknown.
                // Do not let a tampered catalogue row fall back to the custom SQL path.
                IEnumerable<Report> customReports = reportList.Where(report => !IsSystemIdentifier(report.Query));
                // Get accessible report IDs based on user's role assignments
                var accessibleReportIds = await _roleAssignmentRepository.GetAccessibleReportIdsForUserAsync(userId, tenantId);

                // Filter reports based on role assignments (if any assignments exist)
                if (accessibleReportIds.Any())
                {
                    customReports = customReports.Where(r => accessibleReportIds.Contains(r.Id));
                }
                // If no role assignments exist for this tenant, show all reports (fallback behavior)
                else
                {
                    // Check if there are any role assignments configured for this tenant
                    var hasAnyRoleAssignments = await HasAnyRoleAssignmentsAsync(tenantId);
                    if (hasAnyRoleAssignments)
                    {
                        // If role assignments exist but user has no access, return empty list
                        customReports = new List<Report>();
                    }
                }

                var authorizedSystemReports = await AuthorizeSystemReportsAsync(systemReports, false);
                reports = customReports.Concat(authorizedSystemReports);
            }

            var reportDtos = new List<ReportDefinitionDto>();

            foreach (var report in reports)
            {
                // Get assigned role names for this report
                var roleAssignments = await _roleAssignmentRepository.GetAssignmentsByReportAsync(report.Id, tenantId);
                var assignedRoleNames = roleAssignments.Select(ra => ra.Role?.Name).Where(name => !string.IsNullOrEmpty(name)).Cast<string>().ToList();

                var dto = MapToReportDefinitionDto(report, assignedRoleNames);
                dto.IsFavorite = await _reportRepository.IsReportFavoriteAsync(report.Id, userId, tenantId);
                reportDtos.Add(dto);
            }

            return reportDtos;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving reports for tenant {TenantId}", tenantId);
            throw;
        }
    }

    public async Task<ReportDefinitionDto?> GetReportAsync(Guid reportId, Guid tenantId, Guid userId, bool isAdminUser = false)
    {
        try
        {
            var report = await _reportRepository.GetReportWithDetailsAsync(reportId, tenantId);
            if (report == null)
            {
                return null;
            }

            var provider = ProviderFor(report.Query);
            if (provider is not null)
            {
                if (!await provider.CanReadReportAsync(report.Query!, isAdminUser))
                    throw new UnauthorizedAccessException("Access denied: system report read permission is required.");
            }
            else if (IsSystemIdentifier(report.Query))
            {
                throw new InvalidOperationException("The system report identifier is not registered by its owning provider.");
            }
            else if (!isAdminUser && !await ValidateReportAccessAsync(reportId, userId, tenantId, "read"))
            {
                throw new UnauthorizedAccessException("Access denied: you do not have permission to read this report.");
            }

            // Get assigned role names for this report
            var roleAssignments = await _roleAssignmentRepository.GetAssignmentsByReportAsync(reportId, tenantId);
            var assignedRoleNames = roleAssignments.Select(ra => ra.Role?.Name).Where(name => !string.IsNullOrEmpty(name)).Cast<string>().ToList();

            return MapToReportDefinitionDto(report, assignedRoleNames);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving report {ReportId} for tenant {TenantId}", reportId, tenantId);
            throw;
        }
    }

    public async Task<ReportDefinitionDto> CreateReportAsync(CreateReportDto createReportDto, Guid tenantId, Guid userId)
    {
        try
        {
            if (_systemReportProviders.Any(provider => provider.OwnsIdentifier(createReportDto.Query)))
                throw new InvalidOperationException("System report identifiers cannot be created through the custom report builder.");
            var report = new Report
            {
                Name = createReportDto.Name,
                Description = createReportDto.Description,
                Type = createReportDto.Type,
                Status = "draft",
                Query = createReportDto.Query,
                Parameters = createReportDto.Parameters != null ? JsonSerializer.Serialize(createReportDto.Parameters) : null,
                Columns = createReportDto.Columns != null ? JsonSerializer.Serialize(createReportDto.Columns) : null,
                Visualization = createReportDto.Visualization != null ? JsonSerializer.Serialize(createReportDto.Visualization) : null,
                Tags = createReportDto.Tags != null ? JsonSerializer.Serialize(createReportDto.Tags) : null,
                TenantId = tenantId,
                CreatedBy = userId.ToString()
            };

            await _reportRepository.AddAsync(report);
            await _unitOfWork.SaveChangesAsync();

            // Get assigned role names for the new report (should be empty for new reports)
            var roleAssignments = await _roleAssignmentRepository.GetAssignmentsByReportAsync(report.Id, tenantId);
            var assignedRoleNames = roleAssignments.Select(ra => ra.Role?.Name).Where(name => !string.IsNullOrEmpty(name)).Cast<string>().ToList();

            return MapToReportDefinitionDto(report, assignedRoleNames);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating report for tenant {TenantId}", tenantId);
            throw;
        }
    }

    public async Task<ReportDefinitionDto?> UpdateReportAsync(Guid reportId, UpdateReportDto updateReportDto, Guid tenantId, Guid userId, bool isAdminUser = false)
    {
        try
        {
            var report = await _reportRepository.GetReportWithDetailsAsync(reportId, tenantId);
            if (report == null)
            {
                return null;
            }

            if (IsSystemIdentifier(report.Query))
                throw new InvalidOperationException("System-defined reports cannot be edited.");

            // Check if user has permission to edit this report (admins bypass role/module filtering)
            if (!isAdminUser)
            {
                var hasEditPermission = await ValidateReportAccessAsync(reportId, userId, tenantId, "edit");
                if (!hasEditPermission)
                {
                    throw new UnauthorizedAccessException("Access denied: You do not have the required role permissions to edit this report. Please contact your administrator to request access or ensure you have the appropriate role assigned.");
                }
            }

            // Update only provided fields
            if (!string.IsNullOrEmpty(updateReportDto.Name))
            {
                report.Name = updateReportDto.Name;
            }

            if (!string.IsNullOrEmpty(updateReportDto.Description))
            {
                report.Description = updateReportDto.Description;
            }

            if (!string.IsNullOrEmpty(updateReportDto.Status))
            {
                report.Status = updateReportDto.Status;
            }

            if (updateReportDto.Parameters != null)
            {
                report.Parameters = JsonSerializer.Serialize(updateReportDto.Parameters);
            }

            if (!string.IsNullOrEmpty(updateReportDto.Query))
            {
                report.Query = updateReportDto.Query;
            }

            if (updateReportDto.Columns != null)
            {
                report.Columns = JsonSerializer.Serialize(updateReportDto.Columns);
            }

            if (updateReportDto.Visualization != null)
            {
                report.Visualization = JsonSerializer.Serialize(updateReportDto.Visualization);
            }

            if (updateReportDto.Tags != null)
            {
                report.Tags = JsonSerializer.Serialize(updateReportDto.Tags);
            }

            report.UpdatedAt = DateTime.UtcNow;
            report.UpdatedBy = userId.ToString();

            await _reportRepository.UpdateAsync(report);
            await _unitOfWork.SaveChangesAsync();

            // Get assigned role names for the updated report
            var roleAssignments = await _roleAssignmentRepository.GetAssignmentsByReportAsync(report.Id, tenantId);
            var assignedRoleNames = roleAssignments.Select(ra => ra.Role?.Name).Where(name => !string.IsNullOrEmpty(name)).Cast<string>().ToList();

            return MapToReportDefinitionDto(report, assignedRoleNames);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating report {ReportId} for tenant {TenantId}", reportId, tenantId);
            throw;
        }
    }

    public async Task<bool> DeleteReportAsync(Guid reportId, Guid tenantId, Guid userId)
    {
        try
        {
            var report = await _reportRepository.GetByIdAsync(reportId);
            if (report == null || report.TenantId != tenantId)
            {
                return false;
            }

            if (IsSystemIdentifier(report.Query))
                throw new InvalidOperationException("System-defined reports cannot be deleted.");

            report.IsDeleted = true;
            report.DeletedAt = DateTime.UtcNow;
            report.DeletedBy = userId.ToString();

            await _reportRepository.UpdateAsync(report);
            await _unitOfWork.SaveChangesAsync();

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting report {ReportId} for tenant {TenantId}", reportId, tenantId);
            throw;
        }
    }

    public async Task<ReportResultDto> ExecuteReportAsync(Guid reportId, ExecuteReportDto executeReportDto, Guid tenantId, Guid userId, bool isAdminUser = false)
    {
        var startTime = DateTime.UtcNow;

        try
        {
            var report = await _reportRepository.GetReportWithDetailsAsync(reportId, tenantId) ?? throw new InvalidOperationException("Report not found");

            var systemProvider = ProviderFor(report.Query);
            if (systemProvider is null && IsSystemIdentifier(report.Query))
                throw new InvalidOperationException("The system report identifier is not registered by its owning provider.");
            var isSystemReport = systemProvider is not null;
            // Custom reports retain report-role authorization. The provider enforces the
            // tenant-scoped procurement permission for system reports.
            if (!isAdminUser && !isSystemReport)
            {
                var hasExecutePermission = await ValidateReportAccessAsync(reportId, userId, tenantId, "execute");
                if (!hasExecutePermission)
                {
                    throw new UnauthorizedAccessException("Access denied: You do not have the required role permissions to execute this report. Please contact your administrator to request access or ensure you have the appropriate role assigned.");
                }
            }

            // Check if the report has a query
            if (string.IsNullOrEmpty(report.Query))
            {
                _logger.LogWarning("Report {ReportId} has no query defined, returning sample data", reportId);

                var sampleEndTime = DateTime.UtcNow;
                var sampleExecutionTime = sampleEndTime - startTime;

                // Return sample data for reports without queries
                var sampleResult = CreateSampleReportResult(report, sampleEndTime, sampleExecutionTime);

                // Log sample execution
                var sampleExecutionLog = new ReportExecution
                {
                    ReportId = reportId,
                    UserId = userId,
                    TenantId = tenantId,
                    ExecutedAt = startTime,
                    ExecutionTime = sampleExecutionTime,
                    TotalRows = sampleResult.TotalRows,
                    Status = "success",
                    Parameters = SerializeExecutionParameters(executeReportDto),
                    ResultMetadata = JsonSerializer.Serialize(new
                    {
                        ResultHash = Guid.NewGuid().ToString(),
                        IsSampleData = true,
                        Template = executeReportDto.TemplateContext
                    }),
                    CreatedBy = userId.ToString()
                };

                await _executionRepository.AddAsync(sampleExecutionLog);
                await _reportRepository.UpdateLastRunAsync(reportId, sampleEndTime);
                await _unitOfWork.SaveChangesAsync();

                return sampleResult;
            }

            _logger.LogInformation("Executing report {ReportId} with query: {Query}", reportId, report.Query);

            ReportResultDto result;
            if (systemProvider is not null)
            {
                result = await systemProvider.ExecuteAsync(report.Query!, executeReportDto, isAdminUser);
            }
            else
            {
                var connectionString = _configuration.GetConnectionString("DefaultConnection");
                if (string.IsNullOrEmpty(connectionString))
                    throw new InvalidOperationException("Database connection string not configured");
                result = await ExecuteSqlQueryAsync(report, executeReportDto, connectionString);
            }
            var endTime = DateTime.UtcNow;
            var executionTime = endTime - startTime;

            // Update the result with execution metadata
            result.ReportId = reportId;
            result.ReportName = report.Name;
            result.ExecutedAt = endTime;
            result.ExecutionTime = executionTime;
            if (executeReportDto.TemplateContext is not null)
            {
                result.Metadata ??= new ReportMetadataDto();
                result.Metadata.TemplateGeneration = executeReportDto.TemplateContext;
            }

            // Simple execution logging - create a single record after successful execution
            var executionLog = new ReportExecution
            {
                ReportId = reportId,
                UserId = userId,
                TenantId = tenantId,
                ExecutedAt = startTime,
                ExecutionTime = executionTime,
                TotalRows = result.TotalRows,
                Status = "success",
                Parameters = SerializeExecutionParameters(executeReportDto),
                ResultMetadata = JsonSerializer.Serialize(new
                {
                    ResultHash = Guid.NewGuid().ToString(),
                    SystemReportCode = systemProvider?.ResolveCode(report.Query),
                    result.Metadata?.DataAsOf,
                    result.Metadata?.Parameters,
                    Template = executeReportDto.TemplateContext
                }),
                CreatedBy = userId.ToString()
            };

            await _executionRepository.AddAsync(executionLog);
            await _reportRepository.UpdateLastRunAsync(reportId, endTime);
            await _unitOfWork.SaveChangesAsync();

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing report {ReportId} for tenant {TenantId}", reportId, tenantId);

            // Log failed execution - create new record instead of updating
            try
            {
                var failedExecutionLog = new ReportExecution
                {
                    ReportId = reportId,
                    UserId = userId,
                    TenantId = tenantId,
                    ExecutedAt = startTime,
                    ExecutionTime = DateTime.UtcNow - startTime,
                    TotalRows = 0,
                    Status = "failed",
                    ErrorMessage = ex.Message,
                    Parameters = SerializeExecutionParameters(executeReportDto),
                    ResultMetadata = executeReportDto.TemplateContext is null
                        ? null
                        : JsonSerializer.Serialize(new { Template = executeReportDto.TemplateContext }),
                    CreatedBy = userId.ToString()
                };

                await _executionRepository.AddAsync(failedExecutionLog);
                await _unitOfWork.SaveChangesAsync();
            }
            catch (Exception logEx)
            {
                _logger.LogError(logEx, "Failed to log execution error for report {ReportId}", reportId);
            }

            throw;
        }
    }

    public async Task<ReportExportResultDto> ExportReportAsync(Guid reportId, ExportReportDto exportReportDto, Guid tenantId, Guid userId, bool isAdminUser = false)
    {
        try
        {
            var report = await _reportRepository.GetByIdAsync(reportId);
            if (report == null || report.TenantId != tenantId)
            {
                throw new InvalidOperationException("Report not found");
            }

            var systemProvider = ProviderFor(report.Query);
            if (systemProvider is null && IsSystemIdentifier(report.Query))
                throw new InvalidOperationException("The system report identifier is not registered by its owning provider.");
            if (systemProvider is not null)
            {
                await systemProvider.AuthorizeExportAsync(report.Query!, isAdminUser);
            }
            else if (!isAdminUser)
            {
                var hasExportPermission = await ValidateReportAccessAsync(reportId, userId, tenantId, "export");
                if (!hasExportPermission)
                {
                    throw new UnauthorizedAccessException("Access denied: You do not have the required role permissions to export this report. Please contact your administrator to request access or ensure you have the appropriate role assigned.");
                }
            }

            // Execute report first to get data
            var executeDto = new ExecuteReportDto
            {
                Parameters = exportReportDto.Parameters,
                IncludeMetadata = false,
                Page = 1,
                PageSize = 1000,
                MaxRows = 1000,
                // This marker is set only after the export endpoint has completed its dedicated
                // authorization. Providers can distinguish export-only roles from interactive
                // report runners without exposing a client-controlled permission bypass.
                IsExportExecution = true,
                TemplateContext = exportReportDto.TemplateContext
            };

            var reportResult = await ExecuteReportAsync(reportId, executeDto, tenantId, userId, isAdminUser);
            var exportRows = new List<Dictionary<string, object>>(reportResult.Data);
            while (reportResult.HasNextPage)
            {
                executeDto.Page++;
                var nextPage = await ExecuteReportAsync(reportId, executeDto, tenantId, userId, isAdminUser);
                exportRows.AddRange(nextPage.Data);
                reportResult.HasNextPage = nextPage.HasNextPage;
            }
            reportResult.Data = exportRows;

            // Normalize the requested format once so the generated payload, audit record,
            // content type, and download name can never disagree.
            var normalizedFormat = exportReportDto.Format.Trim().ToLowerInvariant();
            var content = GenerateExportContent(reportResult, normalizedFormat);
            var fileName = $"{SanitizeExportFileName(report.Name)}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.{normalizedFormat}";

            // Log the export
            var export = new ReportExport
            {
                ReportId = reportId,
                UserId = userId,
                TenantId = tenantId,
                Format = normalizedFormat,
                FileName = fileName,
                FileSize = content.Length,
                ExportedAt = DateTime.UtcNow,
                Status = "completed",
                Parameters = SerializeExportParameters(exportReportDto),
                CreatedBy = userId.ToString()
            };

            await _exportRepository.AddAsync(export);
            await _unitOfWork.SaveChangesAsync();

            return new ReportExportResultDto
            {
                ExportId = export.Id,
                ReportId = reportId,
                Status = "completed",
                ExportedAt = export.ExportedAt,
                Data = content,
                ContentType = GetContentType(normalizedFormat),
                FileName = fileName,
                FileSize = content.Length
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting report {ReportId} for tenant {TenantId}", reportId, tenantId);
            throw;
        }
    }

    public async Task<ReportScheduleDto> ScheduleReportAsync(Guid reportId, CreateReportScheduleDto scheduleDto, Guid tenantId, Guid userId)
    {
        try
        {
            var report = await _reportRepository.GetByIdAsync(reportId);
            if (report == null || report.TenantId != tenantId)
            {
                throw new InvalidOperationException("Report not found");
            }

            var schedule = new ReportSchedule
            {
                ReportId = reportId,
                Name = scheduleDto.Name,
                Frequency = scheduleDto.Frequency,
                TimeOfDay = scheduleDto.TimeOfDay,
                DayOfWeek = scheduleDto.DayOfWeek,
                DayOfMonth = scheduleDto.DayOfMonth,
                StartDate = scheduleDto.StartDate,
                EndDate = scheduleDto.EndDate,
                EmailRecipients = scheduleDto.EmailRecipients != null ? JsonSerializer.Serialize(scheduleDto.EmailRecipients) : null,
                ExportFormat = scheduleDto.ExportFormat,
                Parameters = scheduleDto.Parameters != null ? JsonSerializer.Serialize(scheduleDto.Parameters) : null,
                IsActive = scheduleDto.IsActive,
                TenantId = tenantId,
                CreatedBy = userId.ToString()
            };

            // Calculate next execution date
            schedule.NextExecutionDate = CalculateNextExecutionDate(schedule);

            await _scheduleRepository.AddAsync(schedule);

            // Update report scheduled flag
            report.IsScheduled = true;
            await _reportRepository.UpdateAsync(report);

            await _unitOfWork.SaveChangesAsync();

            return MapToReportScheduleDto(schedule);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error scheduling report {ReportId} for tenant {TenantId}", reportId, tenantId);
            throw;
        }
    }

    public async Task<ReportScheduleDto?> GetReportScheduleAsync(Guid scheduleId, Guid tenantId)
    {
        try
        {
            var schedule = await _scheduleRepository.GetByIdAsync(scheduleId);
            if (schedule == null || schedule.TenantId != tenantId)
            {
                return null;
            }

            return MapToReportScheduleDto(schedule);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving schedule {ScheduleId} for tenant {TenantId}", scheduleId, tenantId);
            throw;
        }
    }

    public async Task<bool> DeleteReportScheduleAsync(Guid scheduleId, Guid tenantId, Guid userId)
    {
        try
        {
            var schedule = await _scheduleRepository.GetByIdAsync(scheduleId);
            if (schedule == null || schedule.TenantId != tenantId)
            {
                return false;
            }

            schedule.IsDeleted = true;
            schedule.DeletedAt = DateTime.UtcNow;
            schedule.DeletedBy = userId.ToString();

            await _scheduleRepository.UpdateAsync(schedule);
            await _unitOfWork.SaveChangesAsync();

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting schedule {ScheduleId} for tenant {TenantId}", scheduleId, tenantId);
            throw;
        }
    }

    public async Task<List<ReportTemplateDto>> GetReportTemplatesAsync(Guid tenantId, string? category = null)
    {
        try
        {
            var templates = await _templateRepository.GetTemplatesByTenantAsync(tenantId, category);
            return templates.Select(MapToReportTemplateDto).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving report templates for tenant {TenantId}", tenantId);
            throw;
        }
    }

    public async Task<ReportAnalyticsDto> GetReportAnalyticsAsync(Guid tenantId, string period, string? tenantFilter = null, bool isSuperAdmin = false)
    {
        try
        {
            // Get basic counts
            var reports = await _reportRepository.GetReportsByTenantAsync(tenantId);
            var scheduledReports = await _reportRepository.GetScheduledReportsAsync(tenantId);
            var recentExecutions = await _executionRepository.GetRecentExecutionsAsync(tenantId);

            var totalReports = reports.Count();
            var scheduledCount = scheduledReports.Count();
            var executionsToday = recentExecutions.Count(e => e.ExecutedAt.Date == DateTime.Today);

            // Calculate analytics
            var analytics = new ReportAnalyticsDto
            {
                TotalReports = totalReports,
                ScheduledReports = scheduledCount,
                ReportsRunToday = executionsToday,
                DataSourcesConnected = 3, // This would come from actual data sources
                TotalExports = recentExecutions.Count(),
                AvgGenerationTime = recentExecutions.Any()
                    ? recentExecutions.Average(e => e.ExecutionTime.TotalSeconds)
                    : 0,
                UsageStats = GenerateUsageStats(recentExecutions),
                TopReports = GenerateTopReports(reports, recentExecutions)
            };

            return analytics;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving report analytics for tenant {TenantId}", tenantId);
            throw;
        }
    }

    public async Task<bool> ToggleFavoriteAsync(Guid reportId, Guid tenantId, Guid userId)
    {
        try
        {
            return await _favoriteRepository.ToggleFavoriteAsync(reportId, userId, tenantId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error toggling favorite for report {ReportId}, user {UserId}, tenant {TenantId}", reportId, userId, tenantId);
            throw;
        }
    }

    public async Task<ReportTemplateDto> CreateReportTemplateAsync(CreateReportTemplateDto createTemplateDto, Guid tenantId, Guid userId)
    {
        _logger.LogInformation("🏁 CreateReportTemplateAsync called: Name={Name}, TenantId={TenantId}, UserId={UserId}",
            createTemplateDto.Name, tenantId, userId);

        try
        {
            _logger.LogInformation("📄 Creating ReportTemplate entity with data: {@CreateTemplateDto}", createTemplateDto);

            var template = new ReportTemplate
            {
                ReportId = createTemplateDto.ReportId,
                TemplateKey = createTemplateDto.TemplateKey.Trim().ToUpperInvariant(),
                Version = 1,
                Name = createTemplateDto.Name,
                Description = createTemplateDto.Description,
                Category = createTemplateDto.Category,
                Type = createTemplateDto.Type,
                Audience = createTemplateDto.Audience,
                Cadence = createTemplateDto.Cadence,
                Status = "Draft",
                DefaultOutputFormat = createTemplateDto.DefaultOutputFormat,
                OutputFormats = JsonSerializer.Serialize(createTemplateDto.OutputFormats),
                SavedFilters = createTemplateDto.SavedFilters != null ? JsonSerializer.Serialize(createTemplateDto.SavedFilters) : null,
                GenerationMetadata = createTemplateDto.GenerationMetadata != null ? JsonSerializer.Serialize(createTemplateDto.GenerationMetadata) : null,
                ChartType = createTemplateDto.ChartType,
                IsCustom = createTemplateDto.IsCustom,
                Tags = createTemplateDto.Tags != null ? JsonSerializer.Serialize(createTemplateDto.Tags) : null,
                PreviewImage = createTemplateDto.PreviewImage,
                Configuration = createTemplateDto.Configuration != null ? JsonSerializer.Serialize(createTemplateDto.Configuration) : null,
                TenantId = tenantId,
                CreatedBy = userId.ToString(),
                UsageCount = 0
            };

            _logger.LogInformation("💾 Adding template to repository: Id={Id}, Name={Name}, TenantId={TenantId}",
                template.Id, template.Name, template.TenantId);

            await _templateRepository.AddAsync(template);

            _logger.LogInformation("🔄 Calling SaveChangesAsync on unit of work...");
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("✅ Report template created successfully: Id={Id}, Name={Name}",
                template.Id, template.Name);

            var result = MapToReportTemplateDto(template);
            _logger.LogInformation("📤 Returning mapped DTO: Id={Id}, Name={Name}", result.Id, result.Name);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error creating report template for tenant {TenantId}: {ErrorMessage}",
                tenantId, ex.Message);
            throw;
        }
    }

    // Private helper methods
    private static ReportDefinitionDto MapToReportDefinitionDto(Report report, List<string>? assignedRoleNames = null)
    {
        return new ReportDefinitionDto
        {
            Id = report.Id,
            Name = report.Name,
            Description = report.Description,
            Type = report.Type,
            Status = report.Status,
            CreatedBy = report.CreatedBy ?? "Unknown",
            CreatedAt = report.CreatedAt,
            LastRun = report.LastRun,
            NextRun = report.NextRun,
            IsScheduled = report.IsScheduled,
            IsFavorite = false, // This will be set by the calling method
            Query = report.Query,
            ModuleId = report.ModuleId,
            ModuleName = report.Module?.ModuleName,
            Parameters = !string.IsNullOrEmpty(report.Parameters)
                ? JsonSerializer.Deserialize<Dictionary<string, object>>(report.Parameters)
                : null,
            Columns = !string.IsNullOrEmpty(report.Columns)
                ? JsonSerializer.Deserialize<List<ReportColumnDto>>(report.Columns)
                : null,
            Visualization = !string.IsNullOrEmpty(report.Visualization)
                ? JsonSerializer.Deserialize<ReportVisualizationDto>(report.Visualization)
                : null,
            Tags = !string.IsNullOrEmpty(report.Tags)
                ? JsonSerializer.Deserialize<List<string>>(report.Tags)
                : null,
            AssignedRoles = assignedRoleNames ?? new List<string>()
        };
    }

    private static ReportScheduleDto MapToReportScheduleDto(ReportSchedule schedule)
    {
        return new ReportScheduleDto
        {
            Id = schedule.Id,
            ReportId = schedule.ReportId,
            Name = schedule.Name,
            Frequency = schedule.Frequency,
            TimeOfDay = schedule.TimeOfDay,
            DayOfWeek = schedule.DayOfWeek,
            DayOfMonth = schedule.DayOfMonth,
            StartDate = schedule.StartDate,
            EndDate = schedule.EndDate,
            NextExecutionDate = schedule.NextExecutionDate,
            LastExecutionDate = schedule.LastExecutionDate,
            EmailRecipients = !string.IsNullOrEmpty(schedule.EmailRecipients)
                ? JsonSerializer.Deserialize<List<string>>(schedule.EmailRecipients)
                : null,
            ExportFormat = schedule.ExportFormat,
            Parameters = !string.IsNullOrEmpty(schedule.Parameters)
                ? JsonSerializer.Deserialize<Dictionary<string, object>>(schedule.Parameters)
                : null,
            IsActive = schedule.IsActive,
            Status = schedule.Status,
            CreatedAt = schedule.CreatedAt
        };
    }

    private ReportTemplateDto MapToReportTemplateDto(ReportTemplate template)
    {
        return new ReportTemplateDto
        {
            Id = template.Id,
            ReportId = template.ReportId,
            ReportName = template.Report?.Name,
            TemplateKey = template.TemplateKey,
            Version = template.Version,
            Name = template.Name,
            Description = template.Description,
            Category = template.Category,
            Type = template.Type,
            Audience = template.Audience,
            Cadence = template.Cadence,
            Status = template.Status,
            DefaultOutputFormat = template.DefaultOutputFormat,
            OutputFormats = !string.IsNullOrEmpty(template.OutputFormats)
                ? JsonSerializer.Deserialize<List<string>>(template.OutputFormats) ?? []
                : [],
            SavedFilters = !string.IsNullOrEmpty(template.SavedFilters)
                ? JsonSerializer.Deserialize<Dictionary<string, object>>(template.SavedFilters)
                : null,
            GenerationMetadata = !string.IsNullOrEmpty(template.GenerationMetadata)
                ? JsonSerializer.Deserialize<Dictionary<string, object>>(template.GenerationMetadata)
                : null,
            ChartType = template.ChartType,
            IsCustom = template.IsCustom,
            CreatedBy = template.CreatedBy ?? "Unknown",
            CreatedAt = template.CreatedAt,
            LastUsed = template.LastUsed,
            UsageCount = template.UsageCount,
            Tags = !string.IsNullOrEmpty(template.Tags)
                ? JsonSerializer.Deserialize<List<string>>(template.Tags)
                : null,
            PreviewImage = template.PreviewImage,
            Configuration = !string.IsNullOrEmpty(template.Configuration)
                ? JsonSerializer.Deserialize<Dictionary<string, object>>(template.Configuration)
                : null,
            LastGeneratedAt = template.LastGeneratedAt,
            LastGeneratedBy = template.LastGeneratedBy,
            LastGenerationFormat = template.LastGenerationFormat,
            RowVersion = Convert.ToBase64String(template.RowVersion)
        };
    }

    private static DateTime? CalculateNextExecutionDate(ReportSchedule schedule)
    {
        var baseDate = DateTime.Today;
        var time = schedule.TimeOfDay;
        var startDateTime = new DateTime(baseDate.Year, baseDate.Month, baseDate.Day, time.Hour, time.Minute, 0, DateTimeKind.Utc);

        return schedule.Frequency.ToLower() switch
        {
            "daily" => startDateTime.AddDays(1),
            "weekly" => startDateTime.AddDays(7),
            "monthly" => startDateTime.AddMonths(1),
            "quarterly" => startDateTime.AddMonths(3),
            _ => null
        };
    }

    private byte[] GenerateExportContent(ReportResultDto reportResult, string format)
    {
        return format.ToLower() switch
        {
            "csv" => GenerateCsvContent(reportResult),
            "xlsx" => GenerateExcelContent(reportResult),
            "json" => GenerateJsonContent(reportResult),
            "pdf" => GeneratePdfContent(reportResult),
            _ => throw new ArgumentException($"Unsupported export format: {format}")
        };
    }

    private byte[] GenerateCsvContent(ReportResultDto reportResult)
    {
        var sb = new System.Text.StringBuilder();

        // Add headers
        var visibleColumns = reportResult.Columns.Where(c => c.IsVisible).OrderBy(c => c.Order);
        sb.AppendLine(string.Join(",", visibleColumns.Select(c => $"\"{c.DisplayName ?? c.Name}\"")));

        // Add data rows
        foreach (var row in reportResult.Data)
        {
            var values = visibleColumns.Select(column =>
            {
                var value = row.TryGetValue(column.Name, out var tempValue) ? tempValue : "";
                var stringValue = value?.ToString() ?? "";
                return $"\"{stringValue.Replace("\"", "\"\"")}\"";
            });
            sb.AppendLine(string.Join(",", values));
        }

        return System.Text.Encoding.UTF8.GetBytes(sb.ToString());
    }

    private static byte[] GenerateExcelContent(ReportResultDto reportResult)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Report Data");

        var visibleColumns = reportResult.Columns.Where(c => c.IsVisible).OrderBy(c => c.Order).ToList();

        // Add headers
        for (int i = 0; i < visibleColumns.Count; i++)
        {
            worksheet.Cell(1, i + 1).Value = visibleColumns[i].DisplayName ?? visibleColumns[i].Name;
            worksheet.Cell(1, i + 1).Style.Font.Bold = true;
        }

        // Add data rows
        for (int row = 0; row < reportResult.Data.Count; row++)
        {
            var dataRow = reportResult.Data[row];
            for (int col = 0; col < visibleColumns.Count; col++)
            {
                var column = visibleColumns[col];
                var value = dataRow.TryGetValue(column.Name, out var tempValue) ? tempValue : null;
                SetExcelCellValue(worksheet.Cell(row + 2, col + 1), value);
            }
        }

        worksheet.ColumnsUsed().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static void SetExcelCellValue(IXLCell cell, object? value)
    {
        if (value == null)
        {
            cell.Clear(XLClearOptions.Contents);
            return;
        }

        cell.Value = value switch
        {
            DateTime dateTime => dateTime,
            DateTimeOffset dateTimeOffset => dateTimeOffset.DateTime,
            bool boolean => boolean,
            byte number => number,
            short number => number,
            int number => number,
            long number => number,
            float number => number,
            double number => number,
            decimal number => number,
            Guid guid => guid.ToString(),
            string text => text,
            _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty
        };
    }

    private static byte[] GenerateJsonContent(ReportResultDto reportResult)
    {
        var exportData = new
        {
            ReportName = reportResult.ReportName,
            ExecutedAt = reportResult.ExecutedAt,
            TotalRows = reportResult.TotalRows,
            Columns = reportResult.Columns.Where(c => c.IsVisible).OrderBy(c => c.Order),
            Data = reportResult.Data
        };

        var json = JsonSerializer.Serialize(exportData, new JsonSerializerOptions { WriteIndented = true });
        return System.Text.Encoding.UTF8.GetBytes(json);
    }

    private byte[] GeneratePdfContent(ReportResultDto reportResult)
    {
        using var stream = new MemoryStream();
        using var writer = new PdfWriter(stream);
        using var pdf = new PdfDocument(writer);
        using var document = new Document(pdf);

        // Add title
        document.Add(new Paragraph(reportResult.ReportName)
            .SetFontSize(20)
            .SetBold()
            .SetMarginBottom(10));

        // Add metadata
        document.Add(new Paragraph($"Executed: {reportResult.ExecutedAt:yyyy-MM-dd HH:mm:ss}")
            .SetFontSize(10)
            .SetMarginBottom(5));
        document.Add(new Paragraph($"Total Rows: {reportResult.TotalRows}")
            .SetFontSize(10)
            .SetMarginBottom(15));

        // Create table
        var visibleColumns = reportResult.Columns.Where(c => c.IsVisible).OrderBy(c => c.Order).ToList();
        var table = new Table(visibleColumns.Count);
        table.SetWidth(UnitValue.CreatePercentValue(100));

        // Add headers
        foreach (var column in visibleColumns)
        {
            table.AddHeaderCell(new Cell()
                .Add(new Paragraph(column.DisplayName ?? column.Name))
                .SetBold()
                .SetBackgroundColor(iText.Kernel.Colors.ColorConstants.LIGHT_GRAY));
        }

        // Add data rows
        foreach (var row in reportResult.Data)
        {
            foreach (var column in visibleColumns)
            {
                var value = row.TryGetValue(column.Name, out var tempValue) ? tempValue : null;
                table.AddCell(new Cell().Add(new Paragraph(value?.ToString() ?? "")));
            }
        }

        document.Add(table);
        document.Close();

        return stream.ToArray();
    }

    private static string GetContentType(string format)
    {
        return format.ToLower() switch
        {
            "csv" => "text/csv",
            "xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "json" => "application/json",
            "pdf" => "application/pdf",
            _ => "application/octet-stream"
        };
    }

    private static string? SerializeExecutionParameters(ExecuteReportDto request) =>
        request.TemplateContext is null
            ? request.Parameters is null ? null : JsonSerializer.Serialize(request.Parameters)
            : JsonSerializer.Serialize(new
            {
                Filters = request.Parameters,
                Template = request.TemplateContext
            });

    private static string? SerializeExportParameters(ExportReportDto request) =>
        request.TemplateContext is null
            ? request.Parameters is null ? null : JsonSerializer.Serialize(request.Parameters)
            : JsonSerializer.Serialize(new
            {
                Filters = request.Parameters,
                Template = request.TemplateContext,
                request.IncludeCharts,
                request.IncludeHeaders
            });

    private static string SanitizeExportFileName(string reportName)
    {
        var invalidCharacters = Path.GetInvalidFileNameChars();
        var sanitizedCharacters = reportName
            .Trim()
            .Select(character =>
                char.IsControl(character) || Array.IndexOf(invalidCharacters, character) >= 0
                    ? '_'
                    : character)
            .ToArray();
        var sanitizedName = string.Join(
            " ",
            new string(sanitizedCharacters).Split(
                ' ',
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Trim('.', ' ');

        return string.IsNullOrWhiteSpace(sanitizedName) ? "Report" : sanitizedName;
    }

    private static List<ReportUsageStatsDto> GenerateUsageStats(IEnumerable<ReportExecution> executions)
    {
        return executions
            .GroupBy(e => e.ExecutedAt.Date)
            .Select(g => new ReportUsageStatsDto
            {
                Period = g.Key.ToString("yyyy-MM-dd"),
                Date = g.Key,
                ReportsRun = g.Count(),
                UniqueUsers = g.Select(e => e.UserId).Distinct().Count(),
                AvgExecutionTime = g.Average(e => e.ExecutionTime.TotalSeconds)
            })
            .OrderByDescending(s => s.Date)
            .Take(30)
            .ToList();
    }

    private static List<TopReportDto> GenerateTopReports(IEnumerable<Report> reports, IEnumerable<ReportExecution> executions)
    {
        var reportExecutionStats = executions
            .GroupBy(e => e.ReportId)
            .ToDictionary(g => g.Key, g => new
            {
                Count = g.Count(),
                UniqueUsers = g.Select(e => e.UserId).Distinct().Count(),
                LastRun = g.Max(e => e.ExecutedAt)
            });

        return reports
            .Where(r => reportExecutionStats.ContainsKey(r.Id))
            .Select(r => new TopReportDto
            {
                ReportId = r.Id,
                ReportName = r.Name,
                Type = r.Type,
                ExecutionCount = reportExecutionStats[r.Id].Count,
                UniqueUsers = reportExecutionStats[r.Id].UniqueUsers,
                LastRun = reportExecutionStats[r.Id].LastRun,
                AvgRating = 4.5 // This would come from actual ratings
            })
            .OrderByDescending(r => r.ExecutionCount)
            .Take(10)
            .ToList();
    }

    private async Task<ReportResultDto> ExecuteSqlQueryAsync(Report report, ExecuteReportDto executeReportDto, string connectionString)
    {
        var data = new List<Dictionary<string, object>>();
        var columns = new List<ReportColumnDto>();
        var totalRows = 0;
        var actualRowCount = 0;

        try
        {
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();

            // Apply parameters to the query if any
            var baseQuery = ApplyParametersToQuery(report.Query, executeReportDto.Parameters);

            // First, get total count for pagination
            var countQuery = $"SELECT COUNT(*) FROM ({baseQuery}) AS CountQuery";
            await using (var countCommand = new SqlCommand(countQuery, connection) { CommandTimeout = 30 })
            {
                var countResult = await countCommand.ExecuteScalarAsync();
                totalRows = Convert.ToInt32(countResult);
            }

            // Apply pagination to the query
            var page = Math.Max(1, executeReportDto.Page);
            var pageSize = Math.Min(Math.Max(1, executeReportDto.PageSize), 1000); // Max 1000 records per page
            var offset = (page - 1) * pageSize;

            var paginatedQuery = $@"
                SELECT * FROM (
                    SELECT *, ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) as RowNum 
                    FROM ({baseQuery}) AS BaseQuery
                ) AS PaginatedQuery 
                WHERE RowNum > {offset} AND RowNum <= {offset + pageSize}
                ORDER BY RowNum";

            await using var command = new SqlCommand(paginatedQuery, connection)
            {
                CommandTimeout = 30 // 30 second timeout
            };

            _logger.LogDebug("Executing paginated SQL query: {Query}", paginatedQuery);

            await using var reader = await command.ExecuteReaderAsync();

            // Build column metadata (exclude RowNum column)
            for (int i = 0; i < reader.FieldCount; i++)
            {
                var fieldName = reader.GetName(i);
                if (fieldName == "RowNum")
                {
                    continue; // Skip pagination column
                }

                var fieldType = reader.GetFieldType(i);

                columns.Add(new ReportColumnDto
                {
                    Name = fieldName,
                    DisplayName = fieldName,
                    DataType = GetSqlTypeName(fieldType),
                    IsVisible = true,
                    Order = i
                });
            }

            // Read data rows
            while (await reader.ReadAsync())
            {
                var row = new Dictionary<string, object>();
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    var columnName = reader.GetName(i);
                    if (columnName == "RowNum")
                    {
                        continue; // Skip pagination column
                    }

                    var value = reader.IsDBNull(i) ? null : reader.GetValue(i);
                    row[columnName] = value;
                }
                data.Add(row);
                actualRowCount++;
            }

            // Calculate pagination info
            var totalPages = (int)Math.Ceiling((double)totalRows / pageSize);
            var hasNextPage = page < totalPages;
            var hasPreviousPage = page > 1;

            _logger.LogInformation("Query executed successfully. Returned {ActualRowCount} rows (page {Page} of {TotalPages}) with {ColumnCount} columns. Total records: {TotalRows}",
                actualRowCount, page, totalPages, columns.Count, totalRows);

            return new ReportResultDto
            {
                TotalRows = totalRows,
                Columns = columns,
                Data = data,
                CurrentPage = page,
                PageSize = pageSize,
                TotalPages = totalPages,
                HasNextPage = hasNextPage,
                HasPreviousPage = hasPreviousPage
            };

        }
        catch (SqlException ex)
        {
            _logger.LogError(ex, "SQL error executing report query: {Query}", report.Query);
            throw new InvalidOperationException($"SQL execution error: {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing report query: {Query}", report.Query);
            throw new InvalidOperationException($"Query execution failed: {ex.Message}", ex);
        }
    }

    private string ApplyParametersToQuery(string query, Dictionary<string, object>? parameters)
    {
        if (parameters == null || !parameters.Any())
        {
            return query;
        }

        var processedQuery = query;
        foreach (var param in parameters)
        {
            // Simple parameter substitution - in production, consider using SqlParameters for security
            var paramPlaceholder = $"@{param.Key}";
            if (processedQuery.Contains(paramPlaceholder))
            {
                var paramValue = param.Value?.ToString() ?? "NULL";
                // Add basic SQL injection protection for string values
                if (param.Value is string stringValue)
                {
                    paramValue = $"'{stringValue.Replace("'", "''")}'";
                }
                processedQuery = processedQuery.Replace(paramPlaceholder, paramValue);
            }
        }

        _logger.LogDebug("Applied parameters to query. Original: {OriginalQuery}, Processed: {ProcessedQuery}", query, processedQuery);
        return processedQuery;
    }

    private static string GetSqlTypeName(Type fieldType)
    {
        return fieldType.Name switch
        {
            "String" => "String",
            "Int32" => "Integer",
            "Int64" => "BigInteger",
            "Decimal" => "Decimal",
            "Double" => "Float",
            "Single" => "Float",
            "Boolean" => "Boolean",
            "DateTime" => "DateTime",
            "DateOnly" => "Date",
            "TimeOnly" => "Time",
            "Guid" => "UniqueIdentifier",
            "Byte[]" => "Binary",
            _ => "String"
        };
    }

    private static ReportResultDto CreateSampleReportResult(Report report, DateTime executedAt, TimeSpan executionTime)
    {
        return new ReportResultDto
        {
            ReportId = report.Id,
            ReportName = report.Name,
            ExecutedAt = executedAt,
            ExecutionTime = executionTime,
            TotalRows = 3,
            Columns = new List<ReportColumnDto>
            {
                new ReportColumnDto { Name = "Message", DataType = "String", DisplayName = "Message", IsVisible = true, Order = 1 },
                new ReportColumnDto { Name = "Status", DataType = "String", DisplayName = "Status", IsVisible = true, Order = 2 },
                new ReportColumnDto { Name = "Timestamp", DataType = "DateTime", DisplayName = "Timestamp", IsVisible = true, Order = 3 }
            },
            Data = new List<Dictionary<string, object>>
            {
                new Dictionary<string, object>
                {
                    ["Message"] = "This report has no SQL query configured yet",
                    ["Status"] = "No Query",
                    ["Timestamp"] = DateTime.Now
                },
                new Dictionary<string, object>
                {
                    ["Message"] = "Please add a SQL query to this report to see real data",
                    ["Status"] = "Configuration Needed",
                    ["Timestamp"] = DateTime.Now
                },
                new Dictionary<string, object>
                {
                    ["Message"] = "Example: SELECT TOP 10 * FROM YourTable WHERE IsActive = 1",
                    ["Status"] = "Example",
                    ["Timestamp"] = DateTime.Now
                }
            }
        };
    }

    /// <summary>
    /// Check if any role assignments exist for the tenant
    /// </summary>
    private async Task<bool> HasAnyRoleAssignmentsAsync(Guid tenantId)
    {
        var assignments = await _roleAssignmentRepository.FindAsync(rra => rra.TenantId == tenantId && !rra.IsDeleted);
        return assignments.Any();
    }

    /// <summary>
    /// Check if user has specific permission for a report
    /// </summary>
    private async Task<bool> HasReportPermissionAsync(Guid reportId, Guid userId, Guid tenantId, string permission)
    {
        return await _roleAssignmentRepository.HasReportAccessAsync(reportId, userId, tenantId, permission);
    }

    /// <summary>
    /// Validate user has permission to perform action on report
    /// </summary>
    private async Task<bool> ValidateReportAccessAsync(Guid reportId, Guid userId, Guid tenantId, string permission)
    {
        // Check if role assignments exist for this tenant
        var hasRoleAssignments = await HasAnyRoleAssignmentsAsync(tenantId);

        // If no role assignments configured, allow access (fallback behavior)
        if (!hasRoleAssignments)
        {
            return true;
        }

        // If role assignments exist, check specific permission
        return await HasReportPermissionAsync(reportId, userId, tenantId, permission);
    }

    public async Task<DateTime> PublishReportAsync(Guid reportId, Guid tenantId, Guid userId)
    {
        try
        {
            var report = await _reportRepository.GetReportWithDetailsAsync(reportId, tenantId) ?? throw new ArgumentException("Report not found", nameof(reportId));
            if (IsSystemIdentifier(report.Query))
                return report.UpdatedAt ?? report.CreatedAt;
            report.Status = "published";
            report.UpdatedAt = DateTime.UtcNow;
            report.UpdatedBy = userId.ToString();

            await _reportRepository.UpdateAsync(report);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Report {ReportId} published by user {UserId} in tenant {TenantId}", reportId, userId, tenantId);

            return report.UpdatedAt.Value;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error publishing report {ReportId} for tenant {TenantId}", reportId, tenantId);
            throw;
        }
    }

    public async Task<DateTime> UnpublishReportAsync(Guid reportId, Guid tenantId, Guid userId)
    {
        try
        {
            var report = await _reportRepository.GetReportWithDetailsAsync(reportId, tenantId) ?? throw new ArgumentException("Report not found", nameof(reportId));
            if (IsSystemIdentifier(report.Query))
                throw new InvalidOperationException("System-defined reports cannot be unpublished.");
            report.Status = "draft";
            report.UpdatedAt = DateTime.UtcNow;
            report.UpdatedBy = userId.ToString();

            await _reportRepository.UpdateAsync(report);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Report {ReportId} unpublished by user {UserId} in tenant {TenantId}", reportId, userId, tenantId);

            return report.UpdatedAt.Value;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error unpublishing report {ReportId} for tenant {TenantId}", reportId, tenantId);
            throw;
        }
    }

    public async Task<DateTime> AssignReportToModuleAsync(Guid reportId, Guid moduleId, Guid tenantId, Guid userId)
    {
        try
        {
            var report = await _reportRepository.GetReportWithDetailsAsync(reportId, tenantId) ?? throw new ArgumentException("Report not found", nameof(reportId));
            if (IsSystemIdentifier(report.Query))
                throw new InvalidOperationException("System-defined report module assignments are controlled by the catalogue.");

            // Verify module exists and belongs to the same tenant
            // You might want to add a module repository check here

            report.ModuleId = moduleId;
            report.UpdatedAt = DateTime.UtcNow;
            report.UpdatedBy = userId.ToString();

            await _reportRepository.UpdateAsync(report);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Report {ReportId} assigned to module {ModuleId} by user {UserId} in tenant {TenantId}",
                reportId, moduleId, userId, tenantId);

            return report.UpdatedAt.Value;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error assigning report {ReportId} to module {ModuleId} for tenant {TenantId}",
                reportId, moduleId, tenantId);
            throw;
        }
    }

    public async Task<DateTime> UnassignReportFromModuleAsync(Guid reportId, Guid tenantId, Guid userId)
    {
        try
        {
            var report = await _reportRepository.GetReportWithDetailsAsync(reportId, tenantId) ?? throw new ArgumentException("Report not found", nameof(reportId));
            if (IsSystemIdentifier(report.Query))
                throw new InvalidOperationException("System-defined report module assignments are controlled by the catalogue.");
            report.ModuleId = null;
            report.UpdatedAt = DateTime.UtcNow;
            report.UpdatedBy = userId.ToString();

            await _reportRepository.UpdateAsync(report);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Report {ReportId} unassigned from module by user {UserId} in tenant {TenantId}",
                reportId, userId, tenantId);

            return report.UpdatedAt.Value;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error unassigning report {ReportId} from module for tenant {TenantId}",
                reportId, tenantId);
            throw;
        }
    }

    private ISystemReportProvider? ProviderFor(string? reportQuery) =>
        _systemReportProviders.FirstOrDefault(provider => provider.CanHandle(reportQuery));

    private async Task<List<Report>> AuthorizeSystemReportsAsync(
        IEnumerable<Report> reports,
        bool isAdministrator)
    {
        var authorized = new List<Report>();
        foreach (var providerGroup in reports.GroupBy(report => ProviderFor(report.Query)!))
        {
            if (!providerGroup.Key.RequiresRecordLevelReadAuthorization)
            {
                // Inventory, Procurement, and other static providers apply one permission to all
                // their definitions. Resolve it once per provider to avoid serial authorization
                // queries for every catalogue row.
                if (await providerGroup.Key.CanReadAsync(isAdministrator))
                    authorized.AddRange(providerGroup);
                continue;
            }

            // Dynamic providers such as the Finance ad hoc builder mix private and Finance-shared
            // records under one prefix, so each identifier must be checked before metadata leaks.
            foreach (var report in providerGroup)
            {
                if (await providerGroup.Key.CanReadReportAsync(report.Query!, isAdministrator))
                    authorized.Add(report);
            }
        }

        return authorized;
    }

    private bool IsSystemIdentifier(string? reportQuery) =>
        _systemReportProviders.Any(provider => provider.OwnsIdentifier(reportQuery));
}
