using System.Text;
using ErpSystem.Core.DTOs.Reports;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Services.Inventory;
using ErpSystem.Core.Services.Procurement;
using ErpSystem.Data;
using ErpSystem.Data.Repositories;
using ErpSystem.Data.Services;
using ErpSystem.Shared;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Reports;

public sealed class E2E020Phase7ReportingAcceptanceTests
{
    [Fact, Trait("Batch", "E2E-020")]
    public void Aggregate_catalogue_covers_every_required_phase_7_report_owner()
    {
        var cases = Cases();

        cases.Should().HaveCount(28);
        cases.Select(item => item.Query).Should().OnlyHaveUniqueItems();
        cases.Should().Contain(item => item.Code == ProcurementStatutoryReportCatalogue.AppVsActualCode);
        cases.Should().Contain(item => item.Code == ProcurementStatutoryReportCatalogue.ContractRegisterCode);
        cases.Should().Contain(item => item.Code == ProcurementStatutoryReportCatalogue.SupplierPerformanceCode);
        cases.Should().Contain(item => item.Code == ProcurementStatutoryReportCatalogue.RequisitionStatusCode);
        cases.Should().Contain(item => item.Code == ProcurementStatutoryReportCatalogue.PurchaseOrderRegisterCode);
        cases.Should().Contain(item => item.Code == ProcurementStatutoryReportCatalogue.CommitmentRegisterCode);
        cases.Should().Contain(item => item.Code == ProcurementStatutoryReportCatalogue.CertificateTrackingCode);
        cases.Should().Contain(item => item.Code == InventoryStatutoryReportCatalogue.BalanceCode);
        cases.Should().Contain(item => item.Code == InventoryStatutoryReportCatalogue.MovementCode);
        cases.Should().Contain(item => item.Code == InventoryStatutoryReportCatalogue.AgeingCode);
        cases.Should().Contain(item => item.Code == InventoryStatutoryReportCatalogue.CountVarianceCode);
        cases.Should().Contain(item => item.Code == AuditComplianceReportCatalogue.MatchingExceptionCode);
        cases.Should().Contain(item => item.Code == AuditComplianceReportCatalogue.PurchaseOrderPaymentCode);
        cases.Should().Contain(item => item.Code == AuditComplianceReportCatalogue.OverrideCode);
        cases.Should().OnlyContain(item => item.Columns.Count > 0 &&
            item.Query.StartsWith("system://tdc/", StringComparison.Ordinal));

        ProcurementStatutoryReportCatalogue.BuildParameters().Should().ContainKeys("startDate", "endDate");
        InventoryStatutoryReportCatalogue.BuildParameters().Should()
            .ContainKeys("startDate", "endDate", "warehouseId", "status");
        AuditComplianceReportCatalogue.BuildParameters().Should()
            .ContainKeys("startDate", "endDate", "warehouseId", "status");
    }

    [Fact, Trait("Batch", "E2E-020")]
    public async Task Every_phase_7_report_executes_and_exports_xlsx_and_pdf_with_filters_and_metadata()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var cases = Cases();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"e2e020-{Guid.NewGuid():N}")
            .Options;
        await using var db = new ApplicationDbContext(options, tenantId);
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Code = "E2E020",
            Name = "Phase 7 reporting acceptance",
            Status = TenantStatus.Active,
            CreatedBy = "Tests"
        });
        db.Users.Add(new ApplicationUser
        {
            Id = userId,
            TenantId = tenantId,
            UserName = "report-auditor@tdc.test",
            Email = "report-auditor@tdc.test",
            FirstName = "Report",
            LastName = "Auditor",
            IsActive = true
        });
        var reports = cases.Select(item => new Report
        {
            TenantId = tenantId,
            Name = item.Name,
            Description = $"E2E-020 {item.SourceOwner}",
            Type = "phase7",
            Status = "published",
            Query = item.Query,
            CreatedBy = "System"
        }).ToList();
        db.Reports.AddRange(reports);
        await db.SaveChangesAsync();

        var byQuery = cases.ToDictionary(item => item.Query, StringComparer.OrdinalIgnoreCase);
        var exportExecutions = 0;
        var provider = new Mock<ISystemReportProvider>();
        provider.Setup(item => item.CanHandle(It.IsAny<string?>()))
            .Returns((string? query) => query is not null && byQuery.ContainsKey(query));
        provider.Setup(item => item.OwnsIdentifier(It.IsAny<string?>()))
            .Returns((string? query) => query is not null && query.StartsWith("system://tdc/", StringComparison.Ordinal));
        provider.Setup(item => item.ResolveCode(It.IsAny<string?>()))
            .Returns((string? query) => query is not null && byQuery.TryGetValue(query, out var reportCase)
                ? reportCase.Code
                : null);
        provider.Setup(item => item.CanReadAsync(It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        provider.Setup(item => item.AuthorizeExportAsync(
                It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        provider.Setup(item => item.ExecuteAsync(
                It.IsAny<string>(), It.IsAny<ExecuteReportDto>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string query, ExecuteReportDto request, bool _, CancellationToken _) =>
            {
                if (request.IsExportExecution) exportExecutions++;
                var reportCase = byQuery[query];
                var columns = reportCase.Columns.Select((column, index) => new ReportColumnDto
                {
                    Name = column.Name,
                    DisplayName = column.DisplayName,
                    DataType = column.DataType,
                    Format = column.Format,
                    IsVisible = true,
                    Order = index
                }).ToList();
                var row = columns.ToDictionary(
                    column => column.Name,
                    column => (object)$"{reportCase.Code}:{column.Name}");
                return new ReportResultDto
                {
                    TotalRows = 1,
                    Columns = columns,
                    Data = [row],
                    CurrentPage = request.Page,
                    PageSize = request.PageSize,
                    TotalPages = 1,
                    Metadata = new ReportMetadataDto
                    {
                        Query = query,
                        DataAsOf = new DateTime(2026, 8, 6, 0, 0, 0, DateTimeKind.Utc),
                        DataSource = reportCase.SourceOwner,
                        Parameters = request.Parameters,
                        Statistics = new Dictionary<string, object> { ["sourceRows"] = 1 }
                    }
                };
            });

        using var unitOfWork = new UnitOfWork(db);
        var service = new DatabaseReportsService(
            new ReportRepository(db),
            new ReportScheduleRepository(db),
            new ReportTemplateRepository(db),
            new ReportExecutionRepository(db),
            new UserReportFavoriteRepository(db),
            new ReportExportRepository(db),
            new ReportRoleAssignmentRepository(db),
            NullLogger<DatabaseReportsService>.Instance,
            unitOfWork,
            new ConfigurationBuilder().Build(),
            [provider.Object]);
        var filters = new Dictionary<string, object>
        {
            ["startDate"] = "2026-01-01",
            ["endDate"] = "2026-06-30",
            ["warehouseId"] = Guid.NewGuid(),
            ["status"] = "Approved"
        };
        var templateContext = new ReportTemplateGenerationContextDto
        {
            TemplateId = Guid.NewGuid(),
            TemplateKey = "E2E020-PHASE7",
            TemplateName = "Phase 7 aggregate acceptance",
            Version = 1,
            Audience = "Audit",
            Cadence = "AdHoc",
            OutputFormat = "XLSX/PDF",
            GenerationMetadata = new Dictionary<string, object>
            {
                ["acceptance"] = "E2E-020",
                ["preparedFor"] = "Internal Audit"
            }
        };

        foreach (var (report, reportCase) in reports.Zip(cases))
        {
            var online = await service.ExecuteReportAsync(report.Id, new ExecuteReportDto
            {
                Page = 1,
                PageSize = 50,
                Parameters = new Dictionary<string, object>(filters),
                TemplateContext = templateContext
            }, tenantId, userId, isAdminUser: true);
            online.TotalRows.Should().Be(1);
            online.Data.Should().ContainSingle();
            online.Metadata!.Query.Should().Be(reportCase.Query);
            online.Metadata.DataSource.Should().Be(reportCase.SourceOwner);
            online.Metadata.Parameters.Should().ContainKeys("startDate", "endDate", "warehouseId", "status");
            online.Metadata.TemplateGeneration!.GenerationMetadata!["acceptance"].Should().Be("E2E-020");

            var xlsx = await service.ExportReportAsync(report.Id, new ExportReportDto
            {
                Format = "XLSX",
                Parameters = new Dictionary<string, object>(filters),
                TemplateContext = templateContext
            }, tenantId, userId, isAdminUser: true);
            xlsx.ContentType.Should().Be("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
            xlsx.FileName.Should().EndWith(".xlsx").And.NotEndWith(".xlsx_");
            xlsx.Data.Should().StartWith([0x50, 0x4B], "an XLSX file is an Open XML ZIP package");

            var pdf = await service.ExportReportAsync(report.Id, new ExportReportDto
            {
                Format = "PDF",
                Parameters = new Dictionary<string, object>(filters),
                TemplateContext = templateContext
            }, tenantId, userId, isAdminUser: true);
            pdf.ContentType.Should().Be("application/pdf");
            pdf.FileName.Should().EndWith(".pdf").And.NotEndWith(".pdf_");
            Encoding.ASCII.GetString(pdf.Data, 0, 4).Should().Be("%PDF");
        }

        (await db.ReportExecutions.CountAsync(item => item.TenantId == tenantId)).Should().Be(cases.Count * 3);
        (await db.ReportExports.CountAsync(item => item.TenantId == tenantId)).Should().Be(cases.Count * 2);
        (await db.ReportExecutions.Where(item => item.TenantId == tenantId)
            .AllAsync(item => item.Parameters != null &&
                item.Parameters.Contains("E2E-020") && item.Parameters.Contains("startDate"))).Should().BeTrue();
        (await db.ReportExports.Where(item => item.TenantId == tenantId)
            .AllAsync(item => item.Parameters != null &&
                item.Parameters.Contains("E2E-020") && item.Parameters.Contains("startDate"))).Should().BeTrue();
        provider.Verify(item => item.AuthorizeExportAsync(
            It.IsAny<string>(), true, It.IsAny<CancellationToken>()), Times.Exactly(cases.Count * 2));
        exportExecutions.Should().Be(cases.Count * 2,
            "every XLSX/PDF provider execution must retain its server-only export purpose");
    }

    private static IReadOnlyList<ReportCase> Cases() =>
    [
        .. ProcurementStatutoryReportCatalogue.Definitions.Select(item => new ReportCase(
            item.Code, item.Query, item.Name, item.Columns, "Authoritative procurement, supplier and contract owners")),
        .. InventoryStatutoryReportCatalogue.Definitions.Select(item => new ReportCase(
            item.Code, item.Query, item.Name, item.Columns, "Authoritative inventory, movement, ageing and count owners")),
        .. AuditComplianceReportCatalogue.Definitions.Select(item => new ReportCase(
            item.Code, item.Query, item.Name, item.Columns, "Authoritative procurement, Finance, audit and inventory owners"))
    ];

    private sealed record ReportCase(
        string Code,
        string Query,
        string Name,
        IReadOnlyList<ReportColumnDto> Columns,
        string SourceOwner);
}
