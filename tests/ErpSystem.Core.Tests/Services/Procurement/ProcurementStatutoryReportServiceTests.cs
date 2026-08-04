using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.DTOs.Reports;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.Services.Procurement;
using ErpSystem.Data;
using ErpSystem.Data.Repositories;
using ErpSystem.Data.Seeders;
using ErpSystem.Data.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Procurement;

public sealed class ProcurementStatutoryReportServiceTests
{
    [Fact]
    public void CatalogueDefinesTheSevenTdc0701ReportsOnTheSharedReportProtocol()
    {
        ProcurementStatutoryReportCatalogue.Definitions.Should().HaveCount(7);
        ProcurementStatutoryReportCatalogue.Definitions.Select(item => item.Code).Should().OnlyHaveUniqueItems();
        ProcurementStatutoryReportCatalogue.Definitions.Should().OnlyContain(item =>
            item.Query.StartsWith(ProcurementStatutoryReportCatalogue.QueryPrefix, StringComparison.Ordinal) &&
            item.Columns.Count > 0 &&
            item.Tags.Contains("TDC-0701") &&
            item.Tags.Contains("RPT-001"));
    }

    [Fact]
    public async Task AppVsActualCarriesAnExplicitTenantBoundary()
    {
        await using var fixture = new Fixture();
        fixture.AddPlan(fixture.TenantId, "APP-LOCAL-001");
        fixture.AddPlan(fixture.ForeignTenantId, "APP-FOREIGN-001");
        await fixture.Context.SaveChangesAsync();

        var result = await fixture.Service.ExecuteAsync(
            ProcurementStatutoryReportCatalogue.QueryPrefix + ProcurementStatutoryReportCatalogue.AppVsActualCode,
            new ExecuteReportDto { Page = 1, PageSize = 100 },
            isAdministrator: true);

        result.TotalRows.Should().Be(1);
        result.Data.Should().ContainSingle();
        result.Data.Single()["PlanNumber"].Should().Be("APP-LOCAL-001");
        result.Metadata!.DataSource.Should().Contain("Tenant-scoped");
    }

    [Fact]
    public async Task EveryCatalogueReportExecutesThroughTheTypedProvider()
    {
        await using var fixture = new Fixture();

        foreach (var definition in ProcurementStatutoryReportCatalogue.Definitions)
        {
            var result = await fixture.Service.ExecuteAsync(
                definition.Query,
                new ExecuteReportDto { Page = 1, PageSize = 25 },
                isAdministrator: true);

            result.Columns.Select(item => item.Name).Should().Equal(definition.Columns.Select(item => item.Name));
            result.CurrentPage.Should().Be(1);
            result.PageSize.Should().Be(25);
            result.Metadata!.Query.Should().Be(definition.Query);
        }
    }

    [Fact]
    public async Task NonAdministratorRequiresReadAndExportCapabilities()
    {
        await using var fixture = new Fixture(allowCapabilities: false);
        var definition = ProcurementStatutoryReportCatalogue.Definitions[0];

        (await fixture.Service.CanReadAsync(isAdministrator: false)).Should().BeFalse();
        await fixture.Service.Invoking(service => service.ExecuteAsync(
                definition.Query, new ExecuteReportDto(), isAdministrator: false))
            .Should().ThrowAsync<UnauthorizedAccessException>();
        await fixture.Service.Invoking(service => service.AuthorizeExportAsync(
                definition.Query, isAdministrator: false))
            .Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task ExecutionRejectsAnEmptyTenantContext()
    {
        await using var fixture = new Fixture(tenantId: Guid.Empty);

        await fixture.Service.Invoking(service => service.ExecuteAsync(
                ProcurementStatutoryReportCatalogue.Definitions[0].Query,
                new ExecuteReportDto(),
                isAdministrator: true))
            .Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task SeederIsTenantWideIdempotentAndRepairsSoftDeletedDefinitions()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        await using var context = new ApplicationDbContext(options);
        var firstTenantId = Guid.NewGuid();
        var secondTenantId = Guid.NewGuid();
        context.Tenants.AddRange(
            new Tenant { Id = firstTenantId, Code = "ONE", Name = "Tenant One" },
            new Tenant { Id = secondTenantId, Code = "TWO", Name = "Tenant Two" });
        context.TenantModules.AddRange(
            new TenantModule { TenantId = firstTenantId, ModuleName = "Procurement" },
            new TenantModule { TenantId = secondTenantId, ModuleName = "Procurement" });
        await context.SaveChangesAsync();
        var seeder = new ProcurementStatutoryReportSeeder(
            context, NullLogger<ProcurementStatutoryReportSeeder>.Instance);

        await seeder.SeedAsync();
        await seeder.SeedAsync();

        (await context.Reports.IgnoreQueryFilters().CountAsync()).Should().Be(14);
        (await context.Reports.IgnoreQueryFilters()
                .CountAsync(item => item.TenantId == firstTenantId && item.ModuleId != null))
            .Should().Be(7);

        var deleted = await context.Reports.IgnoreQueryFilters()
            .FirstAsync(item => item.TenantId == firstTenantId);
        deleted.IsDeleted = true;
        deleted.DeletedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();

        await seeder.SeedTenantAsync(firstTenantId);

        deleted.IsDeleted.Should().BeFalse();
        deleted.DeletedAt.Should().BeNull();
        (await context.Reports.IgnoreQueryFilters().CountAsync()).Should().Be(14);
    }

    [Fact]
    public async Task SharedReportEngineAuditsExecutionAndExportAndProtectsCatalogueOwnership()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        await using var context = new ApplicationDbContext(options);
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var definition = ProcurementStatutoryReportCatalogue.Definitions[0];
        var report = new Report
        {
            TenantId = tenantId,
            Name = definition.Name,
            Description = definition.Description,
            Type = ProcurementStatutoryReportCatalogue.ReportType,
            Status = "published",
            Query = definition.Query,
            CreatedBy = "System"
        };
        context.Reports.Add(report);
        await context.SaveChangesAsync();

        var provider = new Mock<IProcurementStatutoryReportService>();
        provider.Setup(item => item.CanHandle(definition.Query)).Returns(true);
        provider.Setup(item => item.OwnsIdentifier(definition.Query)).Returns(true);
        provider.Setup(item => item.ResolveCode(definition.Query)).Returns(definition.Code);
        provider.Setup(item => item.ExecuteAsync(
                definition.Query, It.IsAny<ExecuteReportDto>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string _, ExecuteReportDto request, bool _, CancellationToken _) => new ReportResultDto
            {
                TotalRows = 1,
                Columns = [new ReportColumnDto { Name = "PlanNumber", DisplayName = "Plan number", DataType = "String" }],
                Data = [new Dictionary<string, object> { ["PlanNumber"] = "APP-001" }],
                CurrentPage = request.Page,
                PageSize = request.PageSize,
                TotalPages = 1,
                HasNextPage = false,
                Metadata = new ReportMetadataDto { Query = definition.Query, DataAsOf = DateTime.UtcNow }
            });
        provider.Setup(item => item.AuthorizeExportAsync(
                definition.Query, It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        using var unitOfWork = new UnitOfWork(context);
        var service = new DatabaseReportsService(
            new ReportRepository(context),
            new ReportScheduleRepository(context),
            new ReportTemplateRepository(context),
            new ReportExecutionRepository(context),
            new UserReportFavoriteRepository(context),
            new ReportExportRepository(context),
            new ReportRoleAssignmentRepository(context),
            NullLogger<DatabaseReportsService>.Instance,
            unitOfWork,
            new ConfigurationBuilder().Build(),
            [provider.Object]);

        var result = await service.ExecuteReportAsync(
            report.Id, new ExecuteReportDto { Page = 1, PageSize = 100 }, tenantId, userId, isAdminUser: true);
        var export = await service.ExportReportAsync(
            report.Id, new ExportReportDto { Format = "csv" }, tenantId, userId, isAdminUser: true);

        result.ReportId.Should().Be(report.Id);
        result.ReportName.Should().Be(definition.Name);
        export.ContentType.Should().Be("text/csv");
        export.Data.Should().NotBeEmpty();
        (await context.ReportExecutions.CountAsync(item => item.TenantId == tenantId)).Should().Be(2);
        (await context.ReportExports.CountAsync(item => item.TenantId == tenantId)).Should().Be(1);
        report.LastRun.Should().NotBeNull();
        provider.Verify(item => item.AuthorizeExportAsync(
            definition.Query, true, It.IsAny<CancellationToken>()), Times.Once);

        await service.Invoking(item => item.UpdateReportAsync(
                report.Id, new UpdateReportDto { Name = "Forged" }, tenantId, userId, isAdminUser: true))
            .Should().ThrowAsync<InvalidOperationException>();
        await service.Invoking(item => item.DeleteReportAsync(report.Id, tenantId, userId))
            .Should().ThrowAsync<InvalidOperationException>();
        await service.Invoking(item => item.UnpublishReportAsync(report.Id, tenantId, userId))
            .Should().ThrowAsync<InvalidOperationException>();
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly UnitOfWork _unitOfWork;

        public Fixture(bool allowCapabilities = true, Guid? tenantId = null)
        {
            TenantId = tenantId ?? Guid.NewGuid();
            ForeignTenantId = Guid.NewGuid();
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
                .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;
            Context = new ApplicationDbContext(options);
            _unitOfWork = new UnitOfWork(Context);

            var currentUser = new Mock<ICurrentUserProvider>();
            currentUser.SetupGet(item => item.TenantId).Returns(TenantId);
            currentUser.SetupGet(item => item.UserId).Returns(Guid.NewGuid());
            currentUser.SetupGet(item => item.IsAuthenticated).Returns(true);

            var access = new Mock<IProcurementAccessControlService>();
            var decision = new ProcurementAccessCapabilityDecisionDto
            {
                Allowed = allowCapabilities,
                Code = allowCapabilities ? "ACCESS_ALLOWED" : "ACCESS_PERMISSION_DENIED",
                Message = allowCapabilities ? "Allowed" : "Denied"
            };
            access.Setup(item => item.CheckCapabilityAsync(
                    It.IsAny<ProcurementAccessCapabilityRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(decision);
            access.Setup(item => item.EnforceCapabilityAsync(
                    It.IsAny<ProcurementAccessCapabilityRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(decision);
            Service = new ProcurementStatutoryReportService(_unitOfWork, access.Object, currentUser.Object);
        }

        public Guid TenantId { get; }
        public Guid ForeignTenantId { get; }
        public ApplicationDbContext Context { get; }
        public ProcurementStatutoryReportService Service { get; }

        public void AddPlan(Guid tenantId, string planNumber)
        {
            Context.ProcurementPlans.Add(new ProcurementPlan
            {
                TenantId = tenantId,
                PlanNumber = planNumber,
                Title = planNumber,
                DepartmentId = Guid.NewGuid(),
                FiscalYear = 2026,
                PlanStartDate = new DateTime(2026, 1, 1),
                PlanEndDate = new DateTime(2026, 12, 31),
                Status = "Published",
                Currency = "GHS",
                RevisionNumber = 1
            });
        }

        public async ValueTask DisposeAsync()
        {
            _unitOfWork.Dispose();
            await Context.DisposeAsync();
        }
    }
}
