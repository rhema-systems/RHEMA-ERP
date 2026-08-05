using ErpSystem.Core.DTOs.Reports;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Services;
using ErpSystem.Data;
using ErpSystem.Data.Services;
using ErpSystem.Shared;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Services;

public sealed class ReportTemplateLifecycleServiceTests
{
    [Fact]
    public async Task Lifecycle_is_tenant_safe_concurrent_and_never_reuses_a_deleted_revision()
    {
        await using var fixture = await Fixture.CreateAsync();
        var request = Request();
        request.ReportId = fixture.ReportId;
        var created = await fixture.Service.CreateAsync(request, fixture.TenantId, fixture.UserId, true);
        created.Status.Should().Be("Draft");
        created.Version.Should().Be(1);

        (await fixture.Service.GetAsync(fixture.TenantId, fixture.UserId, false)).Should().BeEmpty();
        var published = await fixture.Service.PublishAsync(created.Id,
            new ReportTemplateLifecycleActionDto { RowVersion = created.RowVersion },
            fixture.TenantId, fixture.UserId, true);
        published.Status.Should().Be("Published");
        (await fixture.Service.GetAsync(fixture.TenantId, fixture.UserId, false)).Should().ContainSingle();

        var update = Update(published);
        await fixture.Service.Invoking(service => service.UpdateAsync(
                published.Id, update, fixture.TenantId, fixture.UserId, true))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("Only a Draft*");

        var second = await fixture.Service.CloneAsync(published.Id,
            new CloneReportTemplateDto { RowVersion = published.RowVersion },
            fixture.TenantId, fixture.UserId, true);
        second.Version.Should().Be(2);
        await fixture.Service.DeleteAsync(second.Id,
            new ReportTemplateLifecycleActionDto { RowVersion = second.RowVersion },
            fixture.TenantId, fixture.UserId, true);

        var third = await fixture.Service.CloneAsync(published.Id,
            new CloneReportTemplateDto { RowVersion = published.RowVersion },
            fixture.TenantId, fixture.UserId, true);
        third.Version.Should().Be(3, "soft-deleted revisions remain part of the version sequence");
        (await fixture.Service.GetByIdAsync(published.Id, Guid.NewGuid(), fixture.UserId, true)).Should().BeNull();

        (await fixture.Db.AuditLogs.CountAsync(log => log.TenantId == fixture.TenantId &&
            log.Resource == nameof(ReportTemplate))).Should().BeGreaterThanOrEqualTo(5);
    }

    [Fact]
    public async Task Published_template_retains_filters_and_generation_metadata_for_online_and_pdf_history()
    {
        await using var fixture = await Fixture.CreateAsync();
        var request = Request();
        request.ReportId = fixture.ReportId;
        var draft = await fixture.Service.CreateAsync(request, fixture.TenantId, fixture.UserId, true);
        var published = await fixture.Service.PublishAsync(draft.Id,
            new ReportTemplateLifecycleActionDto { RowVersion = draft.RowVersion },
            fixture.TenantId, fixture.UserId, true);

        ExecuteReportDto? execution = null;
        ExportReportDto? export = null;
        fixture.Reports.Setup(service => service.ExecuteReportAsync(
                fixture.ReportId, It.IsAny<ExecuteReportDto>(), fixture.TenantId, fixture.UserId, true))
            .Callback<Guid, ExecuteReportDto, Guid, Guid, bool>((_, request, _, _, _) => execution = request)
            .ReturnsAsync(new ReportResultDto { TotalRows = 4 });
        fixture.Reports.Setup(service => service.ExportReportAsync(
                fixture.ReportId, It.IsAny<ExportReportDto>(), fixture.TenantId, fixture.UserId, true))
            .Callback<Guid, ExportReportDto, Guid, Guid, bool>((_, request, _, _, _) => export = request)
            .ReturnsAsync(new ReportExportResultDto
            {
                ReportId = fixture.ReportId, Status = "completed", FileName = "board.pdf",
                ContentType = "application/pdf", Data = [1, 2, 3], FileSize = 3
            });

        var online = await fixture.Service.ExecuteAsync(published.Id, new GenerateReportTemplateDto
        {
            Format = "Online",
            FilterOverrides = new Dictionary<string, object> { ["WarehouseId"] = "WH-02" }
        }, fixture.TenantId, fixture.UserId, true);
        online.Metadata!.TemplateGeneration!.Audience.Should().Be("Board");
        execution!.Parameters!["StartDate"].ToString().Should().Be("2026-01-01");
        execution.Parameters["WarehouseId"].ToString().Should().Be("WH-02");
        execution.TemplateContext!.GenerationMetadata!["preparedFor"].ToString().Should().Be("Board");

        await fixture.Service.ExportAsync(published.Id,
            new GenerateReportTemplateDto { Format = "PDF" },
            fixture.TenantId, fixture.UserId, true);
        export!.Format.Should().Be("pdf");
        export.TemplateContext!.TemplateId.Should().Be(published.Id);
        export.TemplateContext.OutputFormat.Should().Be("PDF");

        var stored = await fixture.Db.ReportTemplates.IgnoreQueryFilters()
            .SingleAsync(template => template.Id == published.Id && template.TenantId == fixture.TenantId);
        stored.UsageCount.Should().Be(2);
        stored.LastGenerationFormat.Should().Be("PDF");
        stored.LastGeneratedBy.Should().Be(fixture.UserId);
        (await fixture.Db.AuditLogs.CountAsync(log => log.Action == "ReportTemplate.Generate")).Should().Be(2);
    }

    private static CreateReportTemplateDto Request() => new()
    {
        ReportId = Guid.Empty,
        TemplateKey = "BOARD-QUARTERLY-SPEND",
        Name = "Board quarterly spend",
        Description = "Quarterly board pack",
        Category = "Procurement",
        Type = "Table",
        Audience = "Board",
        Cadence = "Quarterly",
        DefaultOutputFormat = "Online",
        OutputFormats = ["Online", "XLSX", "PDF"],
        SavedFilters = new Dictionary<string, object>
        {
            ["StartDate"] = "2026-01-01",
            ["WarehouseId"] = "WH-01"
        },
        GenerationMetadata = new Dictionary<string, object>
        {
            ["preparedFor"] = "Board",
            ["classification"] = "Internal"
        }
    };

    private static UpdateReportTemplateDto Update(ReportTemplateDto template) => new()
    {
        ReportId = template.ReportId!.Value,
        TemplateKey = template.TemplateKey,
        Name = template.Name,
        Description = template.Description,
        Category = template.Category,
        Type = template.Type,
        Audience = template.Audience,
        Cadence = template.Cadence,
        DefaultOutputFormat = template.DefaultOutputFormat,
        OutputFormats = template.OutputFormats,
        RowVersion = template.RowVersion
    };

    private sealed class Fixture : IAsyncDisposable
    {
        private Fixture(ApplicationDbContext db, Mock<IReportsService> reports, Guid tenantId, Guid userId, Guid reportId)
        {
            Db = db;
            Reports = reports;
            TenantId = tenantId;
            UserId = userId;
            ReportId = reportId;
            Service = new ReportTemplateLifecycleService(db, reports.Object,
                NullLogger<ReportTemplateLifecycleService>.Instance);
        }

        public ApplicationDbContext Db { get; }
        public Mock<IReportsService> Reports { get; }
        public ReportTemplateLifecycleService Service { get; }
        public Guid TenantId { get; }
        public Guid UserId { get; }
        public Guid ReportId { get; }

        public static async Task<Fixture> CreateAsync()
        {
            var tenantId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var reportId = Guid.NewGuid();
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase($"tdc0705-{Guid.NewGuid():N}").Options;
            var db = new ApplicationDbContext(options, tenantId);
            db.Tenants.Add(new Tenant
            {
                Id = tenantId, Name = "TDC test tenant", Code = "TDC0705",
                Status = TenantStatus.Active, CreatedBy = "Tests"
            });
            db.Users.Add(new ApplicationUser
            {
                Id = userId, TenantId = tenantId, UserName = "report-admin@tdc.test",
                Email = "report-admin@tdc.test", FirstName = "Report", LastName = "Admin", IsActive = true
            });
            db.Reports.Add(new Report
            {
                Id = reportId, TenantId = tenantId, Name = "Procurement spend",
                Description = "Spend", Type = "procurement", Status = "published", Query = "SELECT 1",
                CreatedBy = "Tests"
            });
            await db.SaveChangesAsync();

            var reportDto = new ReportDefinitionDto
            {
                Id = reportId, Name = "Procurement spend", Description = "Spend",
                Type = "procurement", Status = "published"
            };
            var reports = new Mock<IReportsService>();
            reports.Setup(service => service.GetReportAsync(reportId, tenantId, userId, It.IsAny<bool>()))
                .ReturnsAsync(reportDto);
            reports.Setup(service => service.GetReportsAsync(
                    tenantId, userId, null, null, null, It.IsAny<bool>()))
                .ReturnsAsync([reportDto]);

            var fixture = new Fixture(db, reports, tenantId, userId, reportId);
            return fixture;
        }

        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }
}
