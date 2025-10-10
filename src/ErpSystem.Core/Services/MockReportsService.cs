using ErpSystem.Core.DTOs.Reports;

namespace ErpSystem.Core.Services
{
    public class MockReportsService : IReportsService
    {
        public async Task<List<ReportDefinitionDto>> GetReportsAsync(Guid tenantId, Guid userId, string? type = null, string? status = null, bool? favoriteOnly = null, bool bypassRoleFiltering = false)
        {
            await Task.Delay(100); // Simulate API delay

            var reports = new List<ReportDefinitionDto>
            {
                new ReportDefinitionDto
                {
                    Id = Guid.NewGuid(),
                    Name = "Monthly Financial Summary",
                    Description = "Comprehensive financial overview including revenue, expenses, and profit margins",
                    Type = "financial",
                    Status = "published",
                    CreatedBy = "John Smith",
                    CreatedAt = DateTime.Now.AddDays(-30),
                    LastRun = DateTime.Now.AddHours(-2),
                    NextRun = DateTime.Now.AddDays(1),
                    IsScheduled = true,
                    IsFavorite = true,
                    Tags = new List<string> { "financial", "monthly", "revenue" }
                },
                new ReportDefinitionDto
                {
                    Id = Guid.NewGuid(),
                    Name = "User Activity Report",
                    Description = "Detailed analysis of user engagement, login patterns, and feature usage",
                    Type = "user",
                    Status = "published",
                    CreatedBy = "Sarah Johnson",
                    CreatedAt = DateTime.Now.AddDays(-20),
                    LastRun = DateTime.Now.AddHours(-5),
                    IsScheduled = false,
                    IsFavorite = false,
                    Tags = new List<string> { "users", "activity", "engagement" }
                },
                new ReportDefinitionDto
                {
                    Id = Guid.NewGuid(),
                    Name = "Tenant Performance Analytics",
                    Description = "Multi-tenant performance metrics and resource utilization analysis",
                    Type = "tenant",
                    Status = "draft",
                    CreatedBy = "Mike Davis",
                    CreatedAt = DateTime.Now.AddDays(-15),
                    IsScheduled = false,
                    IsFavorite = true,
                    Tags = new List<string> { "tenants", "performance", "analytics" }
                }
            };

            // Apply filters
            if (!string.IsNullOrEmpty(type))
                reports = reports.Where(r => r.Type.Equals(type, StringComparison.OrdinalIgnoreCase)).ToList();

            if (!string.IsNullOrEmpty(status))
                reports = reports.Where(r => r.Status.Equals(status, StringComparison.OrdinalIgnoreCase)).ToList();

            if (favoriteOnly == true)
                reports = reports.Where(r => r.IsFavorite).ToList();

            return reports;
        }

        public async Task<ReportDefinitionDto?> GetReportAsync(Guid reportId, Guid tenantId)
        {
            await Task.Delay(50);
            var reports = await GetReportsAsync(tenantId, Guid.NewGuid());
            return reports.FirstOrDefault();
        }

        public async Task<ReportDefinitionDto> CreateReportAsync(CreateReportDto createReportDto, Guid tenantId, Guid userId)
        {
            await Task.Delay(200);
            return new ReportDefinitionDto
            {
                Id = Guid.NewGuid(),
                Name = createReportDto.Name,
                Description = createReportDto.Description,
                Type = createReportDto.Type,
                Status = "draft",
                CreatedBy = "Current User",
                CreatedAt = DateTime.Now,
                IsScheduled = false,
                IsFavorite = false,
                Tags = createReportDto.Tags
            };
        }

        public async Task<ReportDefinitionDto?> UpdateReportAsync(Guid reportId, UpdateReportDto updateReportDto, Guid tenantId, Guid userId, bool isAdminUser = false)
        {
            await Task.Delay(150);
            return await GetReportAsync(reportId, tenantId);
        }

        public async Task<bool> DeleteReportAsync(Guid reportId, Guid tenantId, Guid userId)
        {
            await Task.Delay(100);
            return true;
        }

        public async Task<ReportResultDto> ExecuteReportAsync(Guid reportId, ExecuteReportDto executeReportDto, Guid tenantId, Guid userId, bool isAdminUser = false)
        {
            await Task.Delay(2000); // Simulate report execution time

            return new ReportResultDto
            {
                ReportId = reportId,
                ReportName = "Sample Report",
                ExecutedAt = DateTime.Now,
                ExecutionTime = TimeSpan.FromSeconds(1.5),
                TotalRows = 150,
                Columns = new List<ReportColumnDto>
                {
                    new ReportColumnDto { Name = "Date", DataType = "DateTime", DisplayName = "Date", IsVisible = true, Order = 1 },
                    new ReportColumnDto { Name = "Amount", DataType = "Decimal", DisplayName = "Amount", IsVisible = true, Order = 2 },
                    new ReportColumnDto { Name = "Category", DataType = "String", DisplayName = "Category", IsVisible = true, Order = 3 }
                },
                Data = new List<Dictionary<string, object>>
                {
                    new Dictionary<string, object> { ["Date"] = DateTime.Now.AddDays(-1), ["Amount"] = 1500.50, ["Category"] = "Sales" },
                    new Dictionary<string, object> { ["Date"] = DateTime.Now.AddDays(-2), ["Amount"] = 2300.75, ["Category"] = "Marketing" },
                    new Dictionary<string, object> { ["Date"] = DateTime.Now.AddDays(-3), ["Amount"] = 850.25, ["Category"] = "Operations" }
                }
            };
        }

        public async Task<ReportExportResultDto> ExportReportAsync(Guid reportId, ExportReportDto exportReportDto, Guid tenantId, Guid userId, bool isAdminUser = false)
        {
            await Task.Delay(1000);

            // Generate mock file content based on format
            var content = exportReportDto.Format.ToLower() switch
            {
                "csv" => "Date,Amount,Category\n2024-01-01,1500.50,Sales\n2024-01-02,2300.75,Marketing",
                "json" => "[{\"Date\":\"2024-01-01\",\"Amount\":1500.50,\"Category\":\"Sales\"}]",
                _ => "Mock PDF content"
            };

            return new ReportExportResultDto
            {
                Data = System.Text.Encoding.UTF8.GetBytes(content),
                ContentType = exportReportDto.Format.ToLower() switch
                {
                    "csv" => "text/csv",
                    "json" => "application/json",
                    "xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    _ => "application/pdf"
                },
                FileName = $"report-{reportId}.{exportReportDto.Format}",
                FileSize = content.Length
            };
        }

        public async Task<ReportScheduleDto> ScheduleReportAsync(Guid reportId, CreateReportScheduleDto scheduleDto, Guid tenantId, Guid userId)
        {
            await Task.Delay(200);
            return new ReportScheduleDto
            {
                Id = Guid.NewGuid(),
                ReportId = reportId,
                Name = scheduleDto.Name,
                Frequency = scheduleDto.Frequency,
                TimeOfDay = scheduleDto.TimeOfDay,
                StartDate = scheduleDto.StartDate,
                EndDate = scheduleDto.EndDate,
                IsActive = scheduleDto.IsActive,
                Status = "Active",
                CreatedAt = DateTime.Now
            };
        }

        public async Task<ReportScheduleDto?> GetReportScheduleAsync(Guid scheduleId, Guid tenantId)
        {
            await Task.Delay(50);
            return new ReportScheduleDto
            {
                Id = scheduleId,
                ReportId = Guid.NewGuid(),
                Name = "Sample Schedule",
                Frequency = "daily",
                TimeOfDay = TimeOnly.FromDateTime(DateTime.Now),
                StartDate = DateTime.Now,
                IsActive = true,
                Status = "Active",
                CreatedAt = DateTime.Now
            };
        }

        public async Task<bool> DeleteReportScheduleAsync(Guid scheduleId, Guid tenantId, Guid userId)
        {
            await Task.Delay(100);
            return true;
        }

        public async Task<List<ReportTemplateDto>> GetReportTemplatesAsync(Guid tenantId, string? category = null)
        {
            await Task.Delay(150);

            var templates = new List<ReportTemplateDto>
            {
                new ReportTemplateDto
                {
                    Id = Guid.NewGuid(),
                    Name = "Monthly Revenue Report",
                    Description = "Comprehensive monthly revenue analysis with trends and comparisons",
                    Category = "financial",
                    Type = "dashboard",
                    IsCustom = false,
                    CreatedBy = "System",
                    CreatedAt = DateTime.Now.AddMonths(-2),
                    LastUsed = DateTime.Now.AddDays(-5),
                    UsageCount = 45,
                    Tags = new List<string> { "revenue", "monthly", "financial" }
                },
                new ReportTemplateDto
                {
                    Id = Guid.NewGuid(),
                    Name = "User Activity Summary",
                    Description = "Track user engagement and activity patterns",
                    Category = "users",
                    Type = "chart",
                    ChartType = "line",
                    IsCustom = false,
                    CreatedBy = "System",
                    CreatedAt = DateTime.Now.AddMonths(-3),
                    LastUsed = DateTime.Now.AddDays(-10),
                    UsageCount = 23,
                    Tags = new List<string> { "users", "activity", "engagement" }
                },
                new ReportTemplateDto
                {
                    Id = Guid.NewGuid(),
                    Name = "Sales Performance Dashboard",
                    Description = "Comprehensive sales metrics and performance indicators",
                    Category = "sales",
                    Type = "dashboard",
                    IsCustom = false,
                    CreatedBy = "System",
                    CreatedAt = DateTime.Now.AddMonths(-1),
                    UsageCount = 67,
                    Tags = new List<string> { "sales", "performance", "dashboard" }
                }
            };

            if (!string.IsNullOrEmpty(category))
                templates = templates.Where(t => t.Category.Equals(category, StringComparison.OrdinalIgnoreCase)).ToList();

            return templates;
        }

        public async Task<ReportTemplateDto> CreateReportTemplateAsync(CreateReportTemplateDto createTemplateDto, Guid tenantId, Guid userId)
        {
            await Task.Delay(200);
            return new ReportTemplateDto
            {
                Id = Guid.NewGuid(),
                Name = createTemplateDto.Name,
                Description = createTemplateDto.Description,
                Category = createTemplateDto.Category,
                Type = createTemplateDto.Type,
                IsCustom = true,
                CreatedBy = "Current User",
                CreatedAt = DateTime.Now,
                UsageCount = 0,
                Tags = createTemplateDto.Tags ?? new List<string>()
            };
        }

        public async Task<ReportAnalyticsDto> GetReportAnalyticsAsync(Guid tenantId, string period, string? tenantFilter = null, bool isSuperAdmin = false)
        {
            await Task.Delay(200);

            return new ReportAnalyticsDto
            {
                TotalReports = 24,
                ScheduledReports = 8,
                ReportsRunToday = 12,
                DataSourcesConnected = 5, // This would come from IDataSourceService.GetActiveDataSourcesAsync().Count in real implementation
                TotalExports = 156,
                AvgGenerationTime = 2.3,
                UsageStats = new List<ReportUsageStatsDto>
                {
                    new ReportUsageStatsDto { Period = "Today", ReportsRun = 12, UniqueUsers = 8, AvgExecutionTime = 2.1, Date = DateTime.Today },
                    new ReportUsageStatsDto { Period = "Yesterday", ReportsRun = 18, UniqueUsers = 12, AvgExecutionTime = 2.5, Date = DateTime.Today.AddDays(-1) }
                },
                TopReports = new List<TopReportDto>
                {
                    new TopReportDto { ReportId = Guid.NewGuid(), ReportName = "Monthly Financial Summary", Type = "financial", ExecutionCount = 45, UniqueUsers = 15, LastRun = DateTime.Now.AddHours(-2), AvgRating = 4.8 }
                }
            };
        }

        public async Task<bool> ToggleFavoriteAsync(Guid reportId, Guid tenantId, Guid userId)
        {
            await Task.Delay(100);
            return true; // Return new favorite status
        }

        public async Task<DateTime> PublishReportAsync(Guid reportId, Guid tenantId, Guid userId)
        {
            await Task.Delay(100);
            return DateTime.UtcNow;
        }

        public async Task<DateTime> UnpublishReportAsync(Guid reportId, Guid tenantId, Guid userId)
        {
            await Task.Delay(100);
            return DateTime.UtcNow;
        }

        public async Task<DateTime> AssignReportToModuleAsync(Guid reportId, Guid moduleId, Guid tenantId, Guid userId)
        {
            await Task.Delay(100);
            return DateTime.UtcNow;
        }

        public async Task<DateTime> UnassignReportFromModuleAsync(Guid reportId, Guid tenantId, Guid userId)
        {
            await Task.Delay(100);
            return DateTime.UtcNow;
        }
    }
}
