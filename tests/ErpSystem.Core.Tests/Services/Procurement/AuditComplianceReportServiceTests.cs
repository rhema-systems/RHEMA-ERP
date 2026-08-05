using ErpSystem.Core.DTOs.Inventory;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.DTOs.Reports;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Inventory;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.Services.Procurement;
using ErpSystem.Data;
using ErpSystem.Data.Repositories;
using ErpSystem.Data.Seeders;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Procurement;

public sealed class AuditComplianceReportServiceTests
{
    [Fact]
    public void CatalogueDefinesTheEightTdc0704ProtectedReports()
    {
        AuditComplianceReportCatalogue.Definitions.Should().HaveCount(8);
        AuditComplianceReportCatalogue.Definitions.Select(item => item.Code).Should().OnlyHaveUniqueItems();
        AuditComplianceReportCatalogue.Definitions.Should().OnlyContain(item =>
            item.Query.StartsWith(AuditComplianceReportCatalogue.QueryPrefix, StringComparison.Ordinal) &&
            item.Columns.Count > 0 &&
            item.Tags.Contains("TDC-0704") &&
            item.Tags.Contains("RPT-004"));
    }

    [Fact]
    public async Task EveryCatalogueReportExecutesThroughExistingAuthoritativeOwners()
    {
        await using var fixture = new Fixture();

        foreach (var definition in AuditComplianceReportCatalogue.Definitions)
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

        fixture.Disposals.Verify(item => item.GetReportSourceAsync(
            null, null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task OverrideRegisterCarriesAnExplicitTenantBoundary()
    {
        await using var fixture = new Fixture();
        fixture.AddControlEvent(fixture.TenantId, "LOCAL-OVERRIDE");
        fixture.AddControlEvent(fixture.ForeignTenantId, "FOREIGN-OVERRIDE");
        await fixture.Context.SaveChangesAsync();

        var result = await fixture.Service.ExecuteAsync(
            AuditComplianceReportCatalogue.QueryPrefix + AuditComplianceReportCatalogue.OverrideCode,
            new ExecuteReportDto { Page = 1, PageSize = 100 },
            isAdministrator: true);

        result.TotalRows.Should().Be(1);
        result.Data.Should().ContainSingle();
        result.Data.Single()["SourceReference"].Should().Be("LOCAL-OVERRIDE");
    }

    [Fact]
    public async Task NonAdministratorRequiresReadAndExportCapabilities()
    {
        await using var fixture = new Fixture(allowCapabilities: false);
        var definition = AuditComplianceReportCatalogue.Definitions[0];

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
                AuditComplianceReportCatalogue.Definitions[0].Query,
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
        var seeder = new AuditComplianceReportSeeder(
            context, NullLogger<AuditComplianceReportSeeder>.Instance);

        (await seeder.SeedAsync()).Should().Be(16);
        (await seeder.SeedAsync()).Should().Be(0);
        (await context.Reports.IgnoreQueryFilters().CountAsync()).Should().Be(16);

        var deleted = await context.Reports.IgnoreQueryFilters()
            .FirstAsync(item => item.TenantId == firstTenantId);
        deleted.IsDeleted = true;
        deleted.DeletedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();

        (await seeder.SeedTenantAsync(firstTenantId)).Should().Be(1);
        deleted.IsDeleted.Should().BeFalse();
        deleted.DeletedAt.Should().BeNull();
        (await context.Reports.IgnoreQueryFilters().CountAsync()).Should().Be(16);
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
            access.Setup(item => item.CheckCapabilityAsync(
                    It.IsAny<ProcurementAccessCapabilityRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((ProcurementAccessCapabilityRequest _, string _, CancellationToken _) =>
                    Decision(allowCapabilities));
            access.Setup(item => item.EnforceCapabilityAsync(
                    It.IsAny<ProcurementAccessCapabilityRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((ProcurementAccessCapabilityRequest _, string _, CancellationToken _) =>
                    Decision(allowCapabilities));

            Disposals = new Mock<IInventoryDisposalReportSource>();
            Disposals.Setup(item => item.GetReportSourceAsync(
                    It.IsAny<InventoryDisposalStatus?>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync([]);
            Service = new AuditComplianceReportService(
                _unitOfWork, currentUser.Object, access.Object, Disposals.Object);
        }

        public Guid TenantId { get; }
        public Guid ForeignTenantId { get; }
        public ApplicationDbContext Context { get; }
        public AuditComplianceReportService Service { get; }
        public Mock<IInventoryDisposalReportSource> Disposals { get; }

        public void AddControlEvent(Guid tenantId, string reference)
        {
            Context.ProcurementControlEvents.Add(new ProcurementControlEvent
            {
                TenantId = tenantId,
                EventKey = $"override:{reference}",
                EventType = "ExceptionalSourcing",
                Action = "ApproveOverride",
                Result = ProcurementControlEventResult.Allowed,
                SourceType = "PurchaseOrder",
                SourceReference = reference,
                ActorUserId = Guid.NewGuid(),
                ActorName = "Test actor",
                CorrelationId = Guid.NewGuid().ToString("N"),
                OccurredAtUtc = DateTime.UtcNow,
                IntegrityHash = new string('a', 64)
            });
        }

        private static ProcurementAccessCapabilityDecisionDto Decision(bool allowed) => new()
        {
            Allowed = allowed,
            Code = allowed ? "ACCESS_ALLOWED" : "ACCESS_PERMISSION_DENIED",
            Message = allowed ? "Allowed" : "Denied"
        };

        public async ValueTask DisposeAsync()
        {
            _unitOfWork.Dispose();
            await Context.DisposeAsync();
        }
    }
}
