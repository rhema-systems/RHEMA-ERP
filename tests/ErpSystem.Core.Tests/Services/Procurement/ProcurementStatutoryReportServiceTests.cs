using System.Reflection;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.DTOs.Reports;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Entities.Projects;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.Services.Procurement;
using ErpSystem.Data;
using ErpSystem.Data.Migrations;
using ErpSystem.Data.Repositories;
using ErpSystem.Data.Seeders;
using ErpSystem.Data.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Procurement;

public sealed class ProcurementStatutoryReportServiceTests
{
    [Fact]
    public void CatalogueDefinesTheElevenProcurementReportsOnTheSharedReportProtocol()
    {
        ProcurementStatutoryReportCatalogue.Definitions.Should().HaveCount(11);
        ProcurementStatutoryReportCatalogue.Definitions.Select(item => item.Code).Should().OnlyHaveUniqueItems();
        ProcurementStatutoryReportCatalogue.Definitions.Should().OnlyContain(item =>
            item.Query.StartsWith(ProcurementStatutoryReportCatalogue.QueryPrefix, StringComparison.Ordinal) &&
            item.Columns.Count > 0 &&
            item.Tags.Contains("TDC-0701") &&
            item.Tags.Contains("RPT-001"));
    }

    [Fact]
    public void OperationalReportMigrationSeedsExactlyFourTenantIdempotentDefinitions()
    {
        var migration = new AddProcurementOperationalReportCatalogue();
        var builder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
        migration.GetType().GetMethod("Up", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(migration, [builder]);
        var sqlOperations = builder.Operations.OfType<SqlOperation>().ToList();
        var sql = string.Join(Environment.NewLine, sqlOperations.Select(item => item.Sql));

        sqlOperations.Should().HaveCount(4);
        foreach (var code in new[]
                 {
                     ProcurementStatutoryReportCatalogue.RequisitionStatusCode,
                     ProcurementStatutoryReportCatalogue.PurchaseOrderRegisterCode,
                     ProcurementStatutoryReportCatalogue.CommitmentRegisterCode,
                     ProcurementStatutoryReportCatalogue.CertificateTrackingCode
                 })
            sql.Should().Contain(ProcurementStatutoryReportCatalogue.QueryPrefix + code);
        sql.Should().Contain("[tenant].[Id]").And.Contain("NOT EXISTS");
        sql.Should().NotContain("CREATE TABLE").And.NotContain("ALTER TABLE");
    }

    [Fact]
    public async Task OperationalRegistersUseTypedOwnersAndExcludeForeignTenantRows()
    {
        await using var fixture = new Fixture();
        AddOperationalRegisterRows(fixture.Context, fixture.TenantId, "LOCAL");
        AddOperationalRegisterRows(fixture.Context, fixture.ForeignTenantId, "FOREIGN");
        await fixture.Context.SaveChangesAsync();

        var cases = new[]
        {
            (ProcurementStatutoryReportCatalogue.RequisitionStatusCode, "RequisitionNumber", "PR-LOCAL"),
            (ProcurementStatutoryReportCatalogue.PurchaseOrderRegisterCode, "OrderNumber", "PO-LOCAL"),
            (ProcurementStatutoryReportCatalogue.CommitmentRegisterCode, "ReservationReference", "COM-LOCAL"),
            (ProcurementStatutoryReportCatalogue.CertificateTrackingCode, "CertificateNumber", "CERT-LOCAL")
        };
        foreach (var (code, key, expected) in cases)
        {
            var result = await fixture.Service.ExecuteAsync(
                ProcurementStatutoryReportCatalogue.QueryPrefix + code,
                new ExecuteReportDto { Page = 1, PageSize = 100 },
                isAdministrator: true);

            result.TotalRows.Should().Be(1, code);
            result.Data.Should().ContainSingle();
            result.Data.Single()[key].Should().Be(expected);
            result.Data.Single().Values.Any(value =>
                    (Convert.ToString(value) ?? string.Empty)
                    .Contains("FOREIGN", StringComparison.OrdinalIgnoreCase))
                .Should().BeFalse();
        }
    }

    [SqlServerFact]
    [Trait("Category", "SqlServerIntegration")]
    public async Task OperationalRegistersTranslateAndExecuteAgainstConfiguredSqlServer()
    {
        var connection = Environment.GetEnvironmentVariable("RHEMA_TEST_SQLSERVER")
            ?? throw new InvalidOperationException("RHEMA_TEST_SQLSERVER is required.");
        var tenantId = Guid.Parse("10000000-0000-0000-0000-000000000004");
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer(connection).Options;
        await using var context = new ApplicationDbContext(options);
        using var unitOfWork = new UnitOfWork(context);
        var currentUser = new Mock<ICurrentUserProvider>();
        currentUser.SetupGet(item => item.TenantId).Returns(tenantId);
        currentUser.SetupGet(item => item.UserId).Returns(Guid.NewGuid());
        currentUser.SetupGet(item => item.IsAuthenticated).Returns(true);
        var access = new Mock<IProcurementAccessControlService>();
        access.Setup(item => item.EnforceCapabilityAsync(
                It.IsAny<ProcurementAccessCapabilityRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcurementAccessCapabilityDecisionDto { Allowed = true, Code = "ACCESS_ALLOWED" });
        var service = new ProcurementStatutoryReportService(unitOfWork, access.Object, currentUser.Object);

        foreach (var code in new[]
                 {
                     ProcurementStatutoryReportCatalogue.RequisitionStatusCode,
                     ProcurementStatutoryReportCatalogue.PurchaseOrderRegisterCode,
                     ProcurementStatutoryReportCatalogue.CommitmentRegisterCode,
                     ProcurementStatutoryReportCatalogue.CertificateTrackingCode
                 })
        {
            var result = await service.ExecuteAsync(
                ProcurementStatutoryReportCatalogue.QueryPrefix + code,
                new ExecuteReportDto { Page = 1, PageSize = 100 },
                isAdministrator: true);
            result.TotalRows.Should().Be(0);
            result.Columns.Should().NotBeEmpty();
        }

        var seededQueries = await context.Reports.IgnoreQueryFilters().AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.Type == ProcurementStatutoryReportCatalogue.ReportType)
            .Select(item => item.Query)
            .ToListAsync();
        seededQueries.Should().Contain(new[]
        {
            ProcurementStatutoryReportCatalogue.QueryPrefix + ProcurementStatutoryReportCatalogue.RequisitionStatusCode,
            ProcurementStatutoryReportCatalogue.QueryPrefix + ProcurementStatutoryReportCatalogue.PurchaseOrderRegisterCode,
            ProcurementStatutoryReportCatalogue.QueryPrefix + ProcurementStatutoryReportCatalogue.CommitmentRegisterCode,
            ProcurementStatutoryReportCatalogue.QueryPrefix + ProcurementStatutoryReportCatalogue.CertificateTrackingCode
        });
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

        (await context.Reports.IgnoreQueryFilters().CountAsync()).Should().Be(22);
        (await context.Reports.IgnoreQueryFilters()
                .CountAsync(item => item.TenantId == firstTenantId && item.ModuleId != null))
            .Should().Be(11);

        var deleted = await context.Reports.IgnoreQueryFilters()
            .FirstAsync(item => item.TenantId == firstTenantId);
        deleted.IsDeleted = true;
        deleted.DeletedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();

        await seeder.SeedTenantAsync(firstTenantId);

        deleted.IsDeleted.Should().BeFalse();
        deleted.DeletedAt.Should().BeNull();
        (await context.Reports.IgnoreQueryFilters().CountAsync()).Should().Be(22);
    }

    private static void AddOperationalRegisterRows(ApplicationDbContext context, Guid tenantId, string suffix)
    {
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(), TenantId = tenantId, UserName = $"requester-{suffix.ToLowerInvariant()}",
            FirstName = suffix, LastName = "Requester", Email = $"{suffix.ToLowerInvariant()}@example.test"
        };
        var requisition = new PurchaseRequisition
        {
            Id = Guid.NewGuid(), TenantId = tenantId, RequisitionNumber = $"PR-{suffix}",
            RequisitionDate = new DateTime(2026, 8, 1), RequestedById = user.Id, RequestedBy = user,
            Status = "Approved", Currency = "GHS", TotalAmount = 100m
        };
        var budget = new ProcurementBudget
        {
            Id = Guid.NewGuid(), TenantId = tenantId, BudgetCode = $"BUD-{suffix}", Title = $"Budget {suffix}",
            DepartmentId = Guid.NewGuid(), FiscalYear = 2026, Currency = "GHS", Status = "Active"
        };
        var partner = new BusinessPartner
        {
            Id = Guid.NewGuid(), TenantId = tenantId, PartnerCode = $"SUP-{suffix}",
            PartnerName = $"Supplier {suffix}", PartnerType = "Supplier", RegistrationStatus = "Approved"
        };
        var purchaseOrder = new PurchaseOrder
        {
            Id = Guid.NewGuid(), TenantId = tenantId, OrderNumber = $"PO-{suffix}",
            BusinessPartnerId = partner.Id, BusinessPartner = partner, OrderDate = new DateTime(2026, 8, 2),
            Status = "Approved", Currency = "GHS", TotalAmount = 100m,
            SourceRequisitionId = requisition.Id, SourceRequisitionNumber = requisition.RequisitionNumber
        };
        var project = new Project
        {
            Id = Guid.NewGuid(), TenantId = tenantId, ProjectCode = $"PROJECT-{suffix}",
            Title = $"Project {suffix}", Status = "Active"
        };

        context.Users.Add(user);
        context.PurchaseRequisitions.Add(requisition);
        context.ProcurementBudgets.Add(budget);
        context.ProcurementBudgetCommitments.Add(new ProcurementBudgetCommitment
        {
            Id = Guid.NewGuid(), TenantId = tenantId, ProcurementBudgetId = budget.Id,
            PurchaseRequisitionId = requisition.Id, ReservationReference = $"COM-{suffix}",
            ReservedAmount = 100m, Currency = "GHS", ReservedAtUtc = new DateTime(2026, 8, 1),
            ReservedById = user.Id, ReservedByName = $"{suffix} Requester", CorrelationId = $"corr-{suffix}"
        });
        context.BusinessPartners.Add(partner);
        context.PurchaseOrders.Add(purchaseOrder);
        context.Projects.Add(project);
        context.ProjectPaymentCertificates.Add(new ProjectPaymentCertificate
        {
            Id = Guid.NewGuid(), TenantId = tenantId, ProjectId = project.Id, Project = project,
            ClientRequestId = Guid.NewGuid(), CertificateNumber = $"CERT-{suffix}", Title = $"Certificate {suffix}",
            Status = ProjectPaymentCertificateStatuses.Approved, ApprovalStatus = "Approved",
            IssueDate = new DateTime(2026, 8, 3), PreparedAt = new DateTime(2026, 8, 3), Currency = "GHS"
        });
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

    private sealed class SqlServerFactAttribute : FactAttribute
    {
        public SqlServerFactAttribute()
        {
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("RHEMA_TEST_SQLSERVER")))
                Skip = "Set RHEMA_TEST_SQLSERVER to run the procurement operational-report SQL translation gate.";
        }
    }
}
