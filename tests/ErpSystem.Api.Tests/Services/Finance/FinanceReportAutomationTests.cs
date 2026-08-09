using ErpSystem.Api.Authorization;
using ErpSystem.Api.Services.Finance.Reporting;
using ErpSystem.Core.DTOs.Reports;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Models;
using ErpSystem.Core.Services.Finance;
using ErpSystem.Data;
using ErpSystem.Shared;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Finance;

public sealed class FinanceReportAutomationTests
{
    [Fact]
    [Trait("Requirement", "FR-RP-010")]
    public void MonthlySchedule_ClampsMonthEndWithoutSkippingThePack()
    {
        var result = FinanceReportScheduleCalculator.FirstOccurrence(
            "Monthly",
            new DateTime(2027, 1, 31),
            new TimeOnly(7, 30),
            null,
            31,
            new DateTime(2027, 2, 1, 0, 0, 0, DateTimeKind.Utc));

        result.Should().Be(new DateTime(2027, 2, 28, 7, 30, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void WeeklySchedule_AlignsToConfiguredGhanaWeekday()
    {
        var result = FinanceReportScheduleCalculator.FirstOccurrence(
            "Weekly",
            new DateTime(2026, 8, 9),
            new TimeOnly(6, 0),
            (int)DayOfWeek.Monday,
            null,
            new DateTime(2026, 8, 9, 0, 0, 0, DateTimeKind.Utc));

        result.Should().Be(new DateTime(2026, 8, 10, 6, 0, 0, DateTimeKind.Utc));
    }

    [Theory]
    [InlineData("GetWorkspace", FinancePermissions.ViewReportSchedules)]
    [InlineData("DownloadArtifact", FinancePermissions.ViewReportSchedules)]
    [InlineData("Create", FinancePermissions.ManageReportSchedules)]
    [InlineData("Pause", FinancePermissions.ManageReportSchedules)]
    [InlineData("RunNow", FinancePermissions.RunReportSchedules)]
    [InlineData("ProcessDue", FinancePermissions.RunReportSchedules)]
    public void PermissionMap_SeparatesReadManageAndExecutionDuties(string action, string permission)
    {
        FinancePermissionPolicyMap.GetRequiredPolicies("FinanceReportAutomationController", action)
            .Should().ContainSingle().Which.Should().Be(permission);
    }

    [Fact]
    public void Permissions_AreDiscoverableByRoleAdministration()
    {
        FinancePermissions.All.Select(item => item.Name).Should().Contain([
            FinancePermissions.ViewReportSchedules,
            FinancePermissions.ManageReportSchedules,
            FinancePermissions.RunReportSchedules
        ]);
    }

    [Fact]
    [Trait("Requirement", "FR-RP-010")]
    public async Task DueProcessor_PersistsPrivateArtifactAndDoesNotRunSameSlotTwice()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var reportId = Guid.NewGuid();
        var templateId = Guid.NewGuid();
        var scheduleId = Guid.NewGuid();
        var exportId = Guid.NewGuid();
        var due = new DateTime(2026, 8, 10, 7, 0, 0, DateTimeKind.Utc);
        await using var db = CreateContext();
        db.Tenants.Add(new Tenant { Id = tenantId, Name = "Tema Development Corporation", Code = "TDC" });
        db.Users.Add(new ApplicationUser
        {
            Id = userId, TenantId = tenantId, FirstName = "Finance", LastName = "Manager",
            UserName = "finance.manager", Email = "finance.manager@tdc.gov.gh", IsActive = true
        });
        db.Reports.Add(new Report { Id = reportId, TenantId = tenantId, Name = "Trial Balance", Type = "Financial", Status = "published" });
        db.ReportTemplates.Add(new ReportTemplate
        {
            Id = templateId, TenantId = tenantId, ReportId = reportId, TemplateKey = "TB-MONTHLY",
            Name = "Monthly Trial Balance", Category = "Finance", Type = "Tabular", Audience = "Finance",
            Status = "Published", Version = 4, OutputFormats = "[\"PDF\"]"
        });
        db.ReportSchedules.Add(new ReportSchedule
        {
            Id = scheduleId, TenantId = tenantId, ReportId = reportId, ReportTemplateId = templateId,
            Name = "Monthly Trial Balance", Frequency = "Monthly", TimeOfDay = new TimeOnly(7, 0),
            DayOfMonth = 10, StartDate = due.Date, NextExecutionDate = due, ExportFormat = "PDF",
            RecipientUserIds = $"[\"{userId}\"]", RunAsUserId = userId, MaximumRetryAttempts = 3,
            IsActive = true, Status = "Active"
        });
        await db.SaveChangesAsync();

        var templates = new Mock<IReportTemplateLifecycleService>();
        templates.Setup(service => service.ExportAsync(templateId, It.IsAny<GenerateReportTemplateDto>(), tenantId,
                userId, true, It.IsAny<CancellationToken>()))
            .Returns(async () =>
            {
                db.ReportExports.Add(new ReportExport
                {
                    Id = exportId, TenantId = tenantId, ReportId = reportId, UserId = userId,
                    Format = "pdf", FileName = "trial-balance.pdf", FileSize = 3, Status = "completed"
                });
                await db.SaveChangesAsync();
                return new ReportExportResultDto
                {
                    ExportId = exportId, ReportId = reportId, Status = "completed", ExportedAt = due,
                    Data = [1, 2, 3], ContentType = "application/pdf", FileName = "trial-balance.pdf", FileSize = 3
                };
            });
        var storage = new Mock<IFileStorageService>();
        storage.Setup(service => service.UploadFileAsync(It.Is<FileUploadRequest>(request =>
                request.Category == "finance-report-artifacts" && request.TenantId == tenantId.ToString())))
            .ReturnsAsync(new FileStorageResult
            {
                Success = true, FileName = "trial-balance.pdf", OriginalFileName = "trial-balance.pdf",
                FilePath = "private/finance-report-artifacts/trial-balance.pdf", PublicUrl = string.Empty,
                FileSize = 3, ContentType = "application/pdf", Category = "finance-report-artifacts",
                TenantId = tenantId.ToString(), StorageProvider = "test"
            });
        var notifications = new Mock<INotificationService>();
        var processor = new FinanceReportAutomationProcessor(db, templates.Object, storage.Object,
            notifications.Object, NullLogger<FinanceReportAutomationProcessor>.Instance);

        var first = await processor.ProcessDueAsync(due, tenantId);
        var duplicatePass = await processor.ProcessDueAsync(due, tenantId);

        first.SucceededCount.Should().Be(1);
        duplicatePass.DueCount.Should().Be(0);
        var execution = await db.ReportExecutions.SingleAsync();
        execution.Status.Should().Be("Succeeded");
        execution.ReportExportId.Should().Be(exportId);
        (await db.ReportExports.FindAsync(exportId))!.StoragePath.Should().StartWith("private/");
        (await db.ReportExports.FindAsync(exportId))!.Sha256Checksum.Should().HaveLength(64);
        templates.Verify(service => service.ExportAsync(templateId, It.IsAny<GenerateReportTemplateDto>(), tenantId,
            userId, true, It.IsAny<CancellationToken>()), Times.Once);
        notifications.Verify(service => service.CreateInAppNotificationAsync(userId, It.IsAny<string>(), It.IsAny<string>(),
            "FinanceReportReady", It.IsAny<Dictionary<string, object>>(), tenantId), Times.Once);
    }

    private static ApplicationDbContext CreateContext() => new(
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
}
