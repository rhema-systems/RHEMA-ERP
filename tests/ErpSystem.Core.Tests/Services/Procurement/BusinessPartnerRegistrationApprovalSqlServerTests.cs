using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Events;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.Services.Procurement;
using ErpSystem.Data;
using ErpSystem.Data.Repositories.Procurement;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Procurement;

/// <summary>
/// SQL Server release gate for the complete supplier-approval persistence path.
/// It protects against EF relationship-state regressions that mocked repositories
/// cannot reproduce.
/// </summary>
public sealed class BusinessPartnerRegistrationApprovalSqlServerTests
{
    [SqlServerFact]
    [Trait("Category", "SqlServerIntegration")]
    public async Task ApprovalAndRetry_ShouldPersistExactlyOneSupplierCategoryAndHistoryEntry()
    {
        await using var database = await ApprovalDatabase.CreateAsync();
        await using var context = database.CreateContext(database.TenantId);

        var currentUser = new Mock<ICurrentUserProvider>();
        currentUser.SetupGet(item => item.IsAuthenticated).Returns(true);
        currentUser.SetupGet(item => item.IsExternalUser).Returns(false);
        currentUser.SetupGet(item => item.UserId).Returns(database.ApproverId);
        currentUser.SetupGet(item => item.TenantId).Returns(database.TenantId);
        currentUser.Setup(item => item.HasRole(It.IsAny<string>())).Returns(true);

        var service = new BusinessPartnerRegistrationService(
            new BusinessPartnerRegistrationRepository(
                context,
                NullLogger<BusinessPartnerRegistrationRepository>.Instance),
            new BusinessPartnerRegistrationDocumentRepository(context),
            new BusinessPartnerRegistrationStatusHistoryRepository(context),
            new BusinessPartnerRepository(
                context,
                currentUser.Object,
                NullLogger<BusinessPartnerRepository>.Instance),
            new BusinessPartnerContactRepository(context),
            new BusinessPartnerFinancialRepository(context),
            new BusinessPartnerDocumentRepository(context),
            new BusinessPartnerLicenseRepository(context),
            new UnitOfWork(context),
            currentUser.Object,
            Mock.Of<IAppEventBus>(),
            Mock.Of<IProcurementAccessControlService>(),
            NullLogger<BusinessPartnerRegistrationService>.Instance);

        await service.ApproveRegistrationAsync(
            database.RegistrationId,
            database.ApproverId,
            "SQL approval gate");
        await service.ApproveRegistrationAsync(
            database.RegistrationId,
            database.ApproverId,
            "Repeated request");

        await using var verification = database.CreateContext();
        var registration = await verification.BusinessPartnerRegistrations
            .IgnoreQueryFilters()
            .SingleAsync(item => item.Id == database.RegistrationId);
        var partners = await verification.BusinessPartners
            .IgnoreQueryFilters()
            .Where(item => item.TenantId == database.TenantId)
            .ToListAsync();
        var assignments = await verification.BusinessPartnerCategories
            .Where(item => partners.Select(partner => partner.Id)
                .Contains(item.BusinessPartnerId))
            .ToListAsync();
        var histories = await verification.BusinessPartnerRegistrationStatusHistories
            .IgnoreQueryFilters()
            .Where(item => item.RegistrationId == database.RegistrationId &&
                           item.ToStatus == "Approved")
            .ToListAsync();

        registration.Status.Should().Be("Approved");
        registration.BusinessPartnerId.Should().NotBeNull();
        partners.Should().ContainSingle();
        partners.Single().Id.Should().Be(registration.BusinessPartnerId!.Value);
        assignments.Should().ContainSingle();
        assignments.Single().CategoryId.Should().Be(database.CategoryId);
        assignments.Single().IsPrimary.Should().BeTrue();
        histories.Should().ContainSingle();
    }

    private sealed class SqlServerFactAttribute : FactAttribute
    {
        public SqlServerFactAttribute()
        {
            if (string.IsNullOrWhiteSpace(
                    Environment.GetEnvironmentVariable("RHEMA_TEST_SQLSERVER")))
            {
                Skip = "Set RHEMA_TEST_SQLSERVER to run the disposable supplier-approval SQL gate.";
            }
        }
    }

    private sealed class ApprovalDatabase : IAsyncDisposable
    {
        private readonly string _connectionString;

        public Guid TenantId { get; } = Guid.NewGuid();
        public Guid ApproverId { get; } = Guid.NewGuid();
        public Guid RegistrationId { get; } = Guid.NewGuid();
        public Guid CategoryId { get; } = Guid.NewGuid();

        private ApprovalDatabase(string connectionString)
        {
            _connectionString = connectionString;
        }

        public static async Task<ApprovalDatabase> CreateAsync()
        {
            var baseConnection = Environment.GetEnvironmentVariable(
                    "RHEMA_TEST_SQLSERVER")
                ?? throw new InvalidOperationException(
                    "RHEMA_TEST_SQLSERVER is required.");
            var builder = new SqlConnectionStringBuilder(baseConnection)
            {
                InitialCatalog = $"RhemaERP_SupplierApproval_{Guid.NewGuid():N}",
                TrustServerCertificate = true
            };
            var database = new ApprovalDatabase(builder.ConnectionString);
            await using var context = database.CreateContext();
            try
            {
                await context.Database.EnsureCreatedAsync();
                await database.SeedAsync(context);
                return database;
            }
            catch
            {
                await context.Database.EnsureDeletedAsync();
                throw;
            }
        }

        public ApplicationDbContext CreateContext(Guid? tenantId = null)
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlServer(_connectionString)
                .Options;
            return tenantId.HasValue
                ? new ApplicationDbContext(options, tenantId)
                : new ApplicationDbContext(options);
        }

        private async Task SeedAsync(ApplicationDbContext context)
        {
            context.Tenants.Add(new Tenant
            {
                Id = TenantId,
                Name = "Supplier Approval SQL Gate",
                Code = $"SA-{TenantId:N}"[..12],
                BaseCurrency = "GHS"
            });
            context.Users.Add(new ApplicationUser
            {
                Id = ApproverId,
                TenantId = TenantId,
                FirstName = "Supplier",
                LastName = "Approver",
                UserName = $"supplier-approver-{ApproverId:N}",
                NormalizedUserName = $"SUPPLIER-APPROVER-{ApproverId:N}",
                Email = "supplier.approver@example.test",
                NormalizedEmail = "SUPPLIER.APPROVER@EXAMPLE.TEST",
                IsActive = true
            });
            context.PartnerCategories.Add(new PartnerCategory
            {
                Id = CategoryId,
                TenantId = TenantId,
                CategoryCode = "GOODS",
                CategoryName = "Goods",
                CategoryType = "Supplier",
                IsActive = true
            });
            context.BusinessPartnerRegistrations.Add(new BusinessPartnerRegistration
            {
                Id = RegistrationId,
                TenantId = TenantId,
                RegistrationNumber = $"APP-{RegistrationId:N}"[..20],
                ApplicantName = "SQL Gate Goods Supplier",
                ApplicantEmail = "supplier@example.test",
                PartnerType = "Supplier",
                RegistrationCategory =
                    ProcurementSupplierRegistrationCategory.Goods,
                Status = "Submitted",
                SubmittedDate = DateTime.UtcNow,
                RegistrationDataJson =
                    """
                    {
                      "PartnerType": "Supplier",
                      "CompanyName": "SQL Gate Goods Supplier",
                      "Email": "supplier@example.test"
                    }
                    """
            });
            await context.SaveChangesAsync();
        }

        public async ValueTask DisposeAsync()
        {
            await using var context = CreateContext();
            await context.Database.EnsureDeletedAsync();
        }
    }
}
