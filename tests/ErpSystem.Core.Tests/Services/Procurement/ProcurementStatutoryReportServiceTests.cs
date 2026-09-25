using System.Reflection;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.DTOs.Reports;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Entities.Projects;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.Services.Procurement;
using ErpSystem.Data;
using ErpSystem.Data.Migrations;
using ErpSystem.Data.Repositories;
using ErpSystem.Data.Seeders;
using ErpSystem.Data.Services;
using FluentAssertions;
using Microsoft.Data.SqlClient;
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
    public void CatalogueDefinesAllThirteenArchitectureProcurementReportsOnTheSharedReportProtocol()
    {
        ProcurementStatutoryReportCatalogue.Definitions.Should().HaveCount(13);
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

        var commitments = await fixture.Service.ExecuteAsync(
            ProcurementStatutoryReportCatalogue.QueryPrefix + ProcurementStatutoryReportCatalogue.CommitmentRegisterCode,
            new ExecuteReportDto { Page = 1, PageSize = 100 }, isAdministrator: true);
        var commitment = commitments.Data.Single();
        commitment["ReservedAmount"].Should().Be(100m);
        commitment["OutstandingReservedAmount"].Should().Be(40m);
        commitment["FormallyCommittedAmount"].Should().Be(60m);
        commitment["UtilizedAmount"].Should().Be(25m);
    }

    [Fact]
    public async Task ContractRegisterIncludesTheArchitectureRequiredCommercialLifecycleFields()
    {
        await using var fixture = new Fixture();
        var partnerId = Guid.NewGuid();
        var tenderId = Guid.NewGuid();
        var contractId = Guid.NewGuid();
        var purchaseOrderId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var requisitionId = Guid.NewGuid();
        fixture.Context.BusinessPartners.Add(new BusinessPartner
        {
            Id = partnerId, TenantId = fixture.TenantId, PartnerCode = "SUP-ARCH",
            PartnerName = "Architecture Supplier", PartnerType = "Supplier", RegistrationStatus = "Approved"
        });
        fixture.Context.Tenders.Add(new Tender
        {
            Id = tenderId, TenantId = fixture.TenantId, TenderNumber = "TD-ARCH",
            Title = "Architecture Tender", Status = "Awarded", TenderType = "OpenTender",
            SourcePurchaseRequisitionId = requisitionId
        });
        fixture.Context.Projects.Add(new Project
        {
            Id = projectId, TenantId = fixture.TenantId, ProjectCode = "PRJ-ARCH",
            Title = "Architecture Project", Status = "Active"
        });
        fixture.Context.PurchaseRequisitions.Add(new PurchaseRequisition
        {
            Id = requisitionId, TenantId = fixture.TenantId, RequisitionNumber = "PR-ARCH",
            RequisitionDate = new DateTime(2025, 12, 1), RequestedById = Guid.NewGuid(),
            Status = "Approved", Currency = "GHS", ProjectId = projectId
        });
        fixture.Context.Contracts.Add(new Contract
        {
            Id = contractId, TenantId = fixture.TenantId, TenderId = tenderId,
            TenderAwardId = Guid.NewGuid(), BusinessPartnerId = partnerId,
            ContractNumber = "CTR-ARCH", ContractTitle = "Architecture Contract",
            ContractType = "Works", Status = "Active", Currency = "GHS",
            ContractValue = 1000m, RetentionPercentage = 10m,
            StartDate = new DateTime(2026, 1, 1), EndDate = new DateTime(2026, 12, 31),
            SignedDate = new DateTime(2025, 12, 15), ActivatedAt = new DateTime(2026, 1, 2)
        });
        fixture.Context.ContractAmendments.Add(new ContractAmendment
        {
            TenantId = fixture.TenantId, ContractId = contractId, AmendmentNumber = "VAR-001",
            Status = "Approved", ValueChange = 100m
        });
        fixture.Context.PurchaseOrders.Add(new PurchaseOrder
        {
            Id = purchaseOrderId, TenantId = fixture.TenantId, ContractId = contractId,
            OrderNumber = "PO-ARCH", BusinessPartnerId = partnerId, Status = "Approved",
            Currency = "GHS", TotalAmount = 1000m, OrderDate = new DateTime(2026, 1, 3)
        });
        fixture.Context.VendorInvoices.Add(new VendorInvoice
        {
            TenantId = fixture.TenantId, PurchaseOrderId = purchaseOrderId,
            InvoiceNumber = "INV-ARCH", BusinessPartnerId = partnerId, BusinessPartnerCode = "SUP-ARCH",
            SupplierName = "Architecture Supplier",
            InvoiceDate = new DateTime(2026, 2, 1), CurrencyCode = "GHS",
            SubTotal = 600m, TotalAmount = 600m, PaidAmount = 400m, Status = VendorInvoiceStatus.PartiallyPaid
        });
        fixture.Context.ProjectPaymentCertificates.Add(new ProjectPaymentCertificate
        {
            TenantId = fixture.TenantId, ProjectId = projectId, ContractId = contractId,
            ClientRequestId = Guid.NewGuid(), CertificateNumber = "CERT-ARCH",
            Title = "Architecture Certificate", Status = ProjectPaymentCertificateStatuses.Approved,
            IssueDate = new DateTime(2026, 1, 31), PreparedAt = new DateTime(2026, 1, 30),
            Currency = "GHS", NetCertifiedAmount = 550m
        });
        await fixture.Context.SaveChangesAsync();

        var result = await fixture.Service.ExecuteAsync(
            ProcurementStatutoryReportCatalogue.QueryPrefix + ProcurementStatutoryReportCatalogue.ContractRegisterCode,
            new ExecuteReportDto { Page = 1, PageSize = 100 },
            isAdministrator: true);

        result.Data.Should().ContainSingle();
        var row = result.Data.Single();
        row["ProjectCode"].Should().Be("PRJ-ARCH");
        row["ApprovalDate"].Should().Be(new DateTime(2026, 1, 2));
        row["RetentionPercentage"].Should().Be(10m);
        row["ApprovedVariationCount"].Should().Be(1);
        row["ApprovedAmendmentValue"].Should().Be(100m);
        row["CertificateCount"].Should().Be(1);
        row["CertifiedAmount"].Should().Be(550m);
        row["InvoiceCount"].Should().Be(1);
        row["InvoicedAmount"].Should().Be(600m);
        row["PaidAmount"].Should().Be(400m);
        row["Balance"].Should().Be(700m);
    }

    [Fact]
    public async Task ExceptionAndProcurementToPaymentRegistersRetainEndToEndArchitectureLineage()
    {
        await using var fixture = new Fixture();
        var partnerId = Guid.NewGuid();
        var requisitionId = Guid.NewGuid();
        var sourcingCaseId = Guid.NewGuid();
        var tenderId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var receiptId = Guid.NewGuid();
        var invoiceId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();
        var draftPaymentId = Guid.NewGuid();
        fixture.Context.BusinessPartners.Add(new BusinessPartner
        {
            Id = partnerId, TenantId = fixture.TenantId, PartnerCode = "SUP-E2E",
            PartnerName = "End-to-End Supplier", PartnerType = "Supplier", RegistrationStatus = "Approved"
        });
        fixture.Context.PurchaseRequisitions.Add(new PurchaseRequisition
        {
            Id = requisitionId, TenantId = fixture.TenantId, RequisitionNumber = "PR-E2E",
            RequisitionDate = new DateTime(2026, 3, 1), RequestedById = Guid.NewGuid(),
            Status = "Approved", Currency = "GHS"
        });
        fixture.Context.ProcurementSourcingCases.Add(new ProcurementSourcingCase
        {
            Id = sourcingCaseId, TenantId = fixture.TenantId, PurchaseRequisitionId = requisitionId,
            SourcingReleaseId = Guid.NewGuid(), CaseSequence = 1, CaseNumber = "SRC-E2E",
            SourcePlanId = Guid.NewGuid(), SourcePlanItemId = Guid.NewGuid(), Category = ProcurementCategoryClass.Goods,
            RecommendedMethod = ProcurementMethodType.RestrictedTendering,
            SelectedMethod = ProcurementMethodType.RestrictedTendering,
            CurrencyCode = "GHS", PolicyCode = "POL", PolicyVersion = 1,
            MethodRuleCode = "METHOD-EX", ThresholdRuleCode = "THRESHOLD-EX",
            AuthorityRouteReference = "AUTH-EX", Justification = "Urgent statutory supply",
            CreatedByName = "Procurement Officer", SourceControlFingerprint = new string('a', 64),
            CaseFingerprint = new string('b', 64), IntegrityHash = new string('c', 64)
        });
        fixture.Context.Tenders.Add(new Tender
        {
            Id = tenderId, TenantId = fixture.TenantId, TenderNumber = "TD-E2E", Title = "Exceptional supply",
            TenderType = "RestrictedTendering", Status = "Awarded", SourcePurchaseRequisitionId = requisitionId
        });
        fixture.Context.ProcurementExceptionalSourcingControls.Add(new ProcurementExceptionalSourcingControl
        {
            TenantId = fixture.TenantId, TenderId = tenderId, SourcingCaseId = sourcingCaseId,
            MethodRuleId = Guid.NewGuid(), ExceptionRuleId = Guid.NewGuid(), AuthorityRouteId = Guid.NewGuid(),
            Method = ProcurementMethodType.RestrictedTendering, MethodRuleCode = "METHOD-EX",
            ExceptionRuleCode = "EX-001", AuthorityRouteReference = "AUTH-EX",
            Status = ProcurementExceptionalSourcingControlStatus.Filed,
            Justification = "Urgent statutory supply", JustificationEvidenceReference = "DMS-JUST-001",
            SupplierSelectionEvidenceReference = "DMS-SEL-001", PreparedAtUtc = new DateTime(2026, 3, 2),
            PreparedById = Guid.NewGuid(), ManagingDirectorApprovalRequired = true,
            ManagingDirectorApprovalReference = "MD-APP-001", PpaApprovalRequired = false,
            WorkflowDefinitionId = Guid.NewGuid(), ApprovedAtUtc = new DateTime(2026, 3, 3),
            AwardReference = "AWD-001", ContractReference = "CTR-001",
            ExceptionReportReference = "EXR-001", PostAwardFilingReference = "FILE-001",
            FiledAtUtc = new DateTime(2026, 3, 10), IntegrityHash = new string('d', 64)
        });
        fixture.Context.PurchaseOrders.Add(new PurchaseOrder
        {
            Id = orderId, TenantId = fixture.TenantId, OrderNumber = "PO-E2E", BusinessPartnerId = partnerId,
            SourceRequisitionId = requisitionId, SourceRequisitionNumber = "PR-E2E", Status = "Approved",
            Currency = "GHS", TotalAmount = 1000m, OrderDate = new DateTime(2026, 3, 4)
        });
        fixture.Context.PurchaseOrderReceipts.Add(new PurchaseOrderReceipt
        {
            Id = receiptId, TenantId = fixture.TenantId, PurchaseOrderId = orderId,
            ReceiptNumber = "REC-E2E", ReceiptDate = new DateTime(2026, 3, 5), Status = "Accepted"
        });
        fixture.Context.PurchaseOrderReceiptItems.Add(new PurchaseOrderReceiptItem
        {
            TenantId = fixture.TenantId, ReceiptId = receiptId, PurchaseOrderItemId = Guid.NewGuid(),
            ReceivedQuantity = 10m, AcceptedQuantity = 10m
        });
        fixture.Context.VendorInvoices.Add(new VendorInvoice
        {
            Id = invoiceId, TenantId = fixture.TenantId, PurchaseOrderId = orderId,
            InvoiceNumber = "INV-E2E", BusinessPartnerId = partnerId, BusinessPartnerCode = "SUP-E2E",
            SupplierName = "End-to-End Supplier",
            InvoiceDate = new DateTime(2026, 3, 6), CurrencyCode = "GHS", TotalAmount = 950m,
            Status = VendorInvoiceStatus.PartiallyPaid, MatchingType = InvoiceMatchingType.ThreeWay,
            MatchingStatus = InvoiceMatchingStatus.ThreeWayMatched
        });
        fixture.Context.Set<VendorPayment>().Add(new VendorPayment
        {
            Id = paymentId, TenantId = fixture.TenantId, PaymentNumber = "PAY-E2E",
            BusinessPartnerId = partnerId, BusinessPartnerCode = "SUP-E2E", BusinessPartnerName = "End-to-End Supplier",
            PaymentDate = new DateTime(2026, 3, 8), TotalAmount = 600m, AllocatedAmount = 600m,
            CurrencyCode = "GHS", Status = VendorPaymentStatus.Processed
        });
        fixture.Context.Set<VendorPaymentAllocation>().Add(new VendorPaymentAllocation
        {
            TenantId = fixture.TenantId, VendorPaymentId = paymentId, VendorInvoiceId = invoiceId,
            AllocatedAmount = 600m, InvoiceCurrencyCode = "GHS", PaymentCurrencyCode = "GHS",
            AllocationDate = new DateTime(2026, 3, 8)
        });
        fixture.Context.Set<VendorPayment>().Add(new VendorPayment
        {
            Id = draftPaymentId, TenantId = fixture.TenantId, PaymentNumber = "PAY-E2E-DRAFT",
            BusinessPartnerId = partnerId, BusinessPartnerCode = "SUP-E2E", BusinessPartnerName = "End-to-End Supplier",
            PaymentDate = new DateTime(2026, 3, 9), TotalAmount = 100m, AllocatedAmount = 100m,
            CurrencyCode = "GHS", Status = VendorPaymentStatus.Draft
        });
        fixture.Context.Set<VendorPaymentAllocation>().Add(new VendorPaymentAllocation
        {
            TenantId = fixture.TenantId, VendorPaymentId = draftPaymentId, VendorInvoiceId = invoiceId,
            AllocatedAmount = 100m, InvoiceCurrencyCode = "GHS", PaymentCurrencyCode = "GHS",
            AllocationDate = new DateTime(2026, 3, 9)
        });
        await fixture.Context.SaveChangesAsync();

        var exceptions = await fixture.Service.ExecuteAsync(
            ProcurementStatutoryReportCatalogue.QueryPrefix + ProcurementStatutoryReportCatalogue.ExceptionRegisterCode,
            new ExecuteReportDto { Page = 1, PageSize = 100 }, isAdministrator: true);
        exceptions.Data.Should().ContainSingle();
        exceptions.Data.Single()["ExceptionRuleCode"].Should().Be("EX-001");
        exceptions.Data.Single()["ManagingDirectorApprovalReference"].Should().Be("MD-APP-001");
        exceptions.Data.Single()["PostAwardFilingReference"].Should().Be("FILE-001");

        var endToEnd = await fixture.Service.ExecuteAsync(
            ProcurementStatutoryReportCatalogue.QueryPrefix + ProcurementStatutoryReportCatalogue.ProcurementToPaymentCode,
            new ExecuteReportDto { Page = 1, PageSize = 100 }, isAdministrator: true);
        endToEnd.Data.Should().ContainSingle();
        var row = endToEnd.Data.Single();
        row["RequisitionNumber"].Should().Be("PR-E2E");
        row["OrderNumber"].Should().Be("PO-E2E");
        row["AcceptedQuantity"].Should().Be(10m);
        row["MatchedInvoiceCount"].Should().Be(1);
        // A draft allocation is not a payment and must not reduce the reported outstanding amount.
        row["PaidAmount"].Should().Be(600m);
        row["OutstandingAmount"].Should().Be(350m);
    }

    [SqlServerFact]
    [Trait("Category", "SqlServerIntegration")]
    public async Task OperationalRegistersTranslateAndExecuteAgainstDisposableCurrentSchemaSqlServer()
    {
        var baseConnection = Environment.GetEnvironmentVariable("RHEMA_TEST_SQLSERVER")
            ?? throw new InvalidOperationException("RHEMA_TEST_SQLSERVER is required.");
        var databaseName = $"RhemaERP_ProcReport_{Guid.NewGuid():N}";
        var masterBuilder = new SqlConnectionStringBuilder(baseConnection)
        {
            InitialCatalog = "master",
            TrustServerCertificate = true
        };
        var databaseBuilder = new SqlConnectionStringBuilder(baseConnection)
        {
            InitialCatalog = databaseName,
            TrustServerCertificate = true
        };

        await using (var master = new SqlConnection(masterBuilder.ConnectionString))
        {
            await master.OpenAsync();
            await using var create = master.CreateCommand();
            create.CommandText = $"CREATE DATABASE [{databaseName}];";
            await create.ExecuteNonQueryAsync();
        }

        try
        {
            var tenantId = Guid.NewGuid();
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlServer(databaseBuilder.ConnectionString)
                .Options;
            await using var context = new ApplicationDbContext(options);
            await context.Database.EnsureCreatedAsync();
            context.Tenants.Add(new Tenant
            {
                Id = tenantId,
                Name = "Procurement Report SQL Tenant",
                Code = $"PROC-RPT-{tenantId:N}"[..32],
                BaseCurrency = "GHS"
            });
            await context.SaveChangesAsync();

            var seeder = new ProcurementStatutoryReportSeeder(
                context, NullLogger<ProcurementStatutoryReportSeeder>.Instance);
            (await seeder.SeedTenantAsync(tenantId)).Should().Be(13);

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

            var requiredCodes = new[]
            {
                ProcurementStatutoryReportCatalogue.RequisitionStatusCode,
                ProcurementStatutoryReportCatalogue.PurchaseOrderRegisterCode,
                ProcurementStatutoryReportCatalogue.CommitmentRegisterCode,
                ProcurementStatutoryReportCatalogue.CertificateTrackingCode
            };
            foreach (var code in requiredCodes)
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
            seededQueries.Should().Contain(requiredCodes.Select(code =>
                ProcurementStatutoryReportCatalogue.QueryPrefix + code));
        }
        finally
        {
            await using var master = new SqlConnection(masterBuilder.ConnectionString);
            await master.OpenAsync();
            await using var drop = master.CreateCommand();
            drop.CommandText =
                $"IF DB_ID(N'{databaseName}') IS NOT NULL BEGIN ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{databaseName}]; END;";
            await drop.ExecuteNonQueryAsync();
        }
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

        (await context.Reports.IgnoreQueryFilters().CountAsync()).Should().Be(26);
        (await context.Reports.IgnoreQueryFilters()
                .CountAsync(item => item.TenantId == firstTenantId && item.ModuleId != null))
            .Should().Be(13);

        var deleted = await context.Reports.IgnoreQueryFilters()
            .FirstAsync(item => item.TenantId == firstTenantId);
        deleted.IsDeleted = true;
        deleted.DeletedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();

        await seeder.SeedTenantAsync(firstTenantId);

        deleted.IsDeleted.Should().BeFalse();
        deleted.DeletedAt.Should().BeNull();
        (await context.Reports.IgnoreQueryFilters().CountAsync()).Should().Be(26);
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
            FormallyCommittedAmount = 60m, UtilizedAmount = 25m,
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
