using System.Data;
using System.Security.Cryptography;
using System.Text;
using ErpSystem.Data;
using ErpSystem.Data.Migrations;
using ErpSystem.Data.Repositories.Procurement;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Procurement;

/// <summary>
/// Opt-in release gate for the real SQL Server trigger and session-context
/// behavior. Set RHEMA_TEST_SQLSERVER to a connection whose login may create
/// and drop a disposable database.
/// </summary>
public sealed class SupplierApplicantContactCorrectionSqlServerTests
{
    [SqlServerFact]
    [Trait("Category", "SqlServerIntegration")]
    public async Task PreProvisioningCorrection_ShouldUseExactConnectionBoundGate()
    {
        await using var database = await ContactCorrectionDatabase.CreateAsync();

        await using (var context = database.CreateContext())
        await using (var transaction =
                     await context.Database.BeginTransactionAsync(
                         IsolationLevel.Serializable))
        {
            var store = new ProcurementSupplierApplicantContactCorrectionStore(context);
            var correctedContact = "supplier.corrected@example.test";
            var correctedHash = Hash(correctedContact);

            await store.SetVerifiedContactCorrectionContextAsync(
                database.AccessId,
                database.ActorId,
                correctedHash);
            var affected = await context.Database.ExecuteSqlInterpolatedAsync(
                $"""
                 UPDATE dbo.ProcurementSupplierApplicantAccesses
                 SET VerifiedContact = {correctedContact},
                     VerifiedContactMasked = N'su***@example.test',
                     VerifiedContactHashSha256 = {correctedHash},
                     VerifiedAtUtc = DATEADD(second, 1, VerifiedAtUtc)
                 WHERE Id = {database.AccessId};
                 """);
            await store.ClearVerifiedContactCorrectionContextAsync();
            await transaction.CommitAsync();

            affected.Should().Be(1);
        }

        await using (var connection = await database.OpenConnectionAsync())
        {
            var command = connection.CreateCommand();
            command.CommandText =
                "SELECT VerifiedContactHashSha256 FROM dbo.ProcurementSupplierApplicantAccesses WHERE Id=@id;";
            command.Parameters.Add(new SqlParameter("@id", database.AccessId));
            (await command.ExecuteScalarAsync()).Should()
                .Be(Hash("supplier.corrected@example.test"));
        }
    }

    [SqlServerFact]
    [Trait("Category", "SqlServerIntegration")]
    public async Task CorrectionWithoutGovernedContext_ShouldRemainBlocked()
    {
        await using var database = await ContactCorrectionDatabase.CreateAsync();
        await using var context = database.CreateContext();

        var action = async () => await context.Database.ExecuteSqlInterpolatedAsync(
            $"""
             UPDATE dbo.ProcurementSupplierApplicantAccesses
             SET VerifiedContact = N'unguarded@example.test',
                 VerifiedContactMasked = N'un***@example.test',
                 VerifiedContactHashSha256 = {Hash("unguarded@example.test")},
                 VerifiedAtUtc = DATEADD(second, 1, VerifiedAtUtc)
             WHERE Id = {database.AccessId};
             """);

        var exception = await action.Should().ThrowAsync<SqlException>();
        exception.Which.Number.Should().Be(51831);
    }

    [SqlServerFact]
    [Trait("Category", "SqlServerIntegration")]
    public async Task CorrectionContext_ShouldNotAuthorizeSubjectLineageChange()
    {
        await using var database = await ContactCorrectionDatabase.CreateAsync();
        await using var context = database.CreateContext();
        await using var transaction =
            await context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var store = new ProcurementSupplierApplicantContactCorrectionStore(context);
        var correctedHash = Hash("subject-change@example.test");
        await store.SetVerifiedContactCorrectionContextAsync(
            database.AccessId,
            database.ActorId,
            correctedHash);

        var action = async () => await context.Database.ExecuteSqlInterpolatedAsync(
            $"""
             UPDATE dbo.ProcurementSupplierApplicantAccesses
             SET VerifiedContact = N'subject-change@example.test',
                 VerifiedContactMasked = N'su***@example.test',
                 VerifiedContactHashSha256 = {correctedHash},
                 VerifiedAtUtc = DATEADD(second, 1, VerifiedAtUtc),
                 BusinessPartnerId = {database.BusinessPartnerId}
             WHERE Id = {database.AccessId};
             """);

        var exception = await action.Should().ThrowAsync<SqlException>();
        exception.Which.Number.Should().Be(51831);
    }

    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)))
            .ToLowerInvariant();

    private sealed class SqlServerFactAttribute : FactAttribute
    {
        public SqlServerFactAttribute()
        {
            if (string.IsNullOrWhiteSpace(
                    Environment.GetEnvironmentVariable("RHEMA_TEST_SQLSERVER")))
            {
                Skip = "Set RHEMA_TEST_SQLSERVER to run the disposable supplier-contact trigger gates.";
            }
        }
    }

    private sealed class ContactCorrectionDatabase : IAsyncDisposable
    {
        private readonly string _connectionString;
        private readonly string _databaseName;
        private readonly string _masterConnectionString;

        public Guid TenantId { get; } = Guid.NewGuid();
        public Guid ActorId { get; } = Guid.NewGuid();
        public Guid RegistrationId { get; } = Guid.NewGuid();
        public Guid TokenId { get; } = Guid.NewGuid();
        public Guid AccessId { get; } = Guid.NewGuid();
        public Guid BusinessPartnerId { get; } = Guid.NewGuid();

        private ContactCorrectionDatabase(
            string connectionString,
            string databaseName,
            string masterConnectionString)
        {
            _connectionString = connectionString;
            _databaseName = databaseName;
            _masterConnectionString = masterConnectionString;
        }

        public static async Task<ContactCorrectionDatabase> CreateAsync()
        {
            var baseConnection = Environment.GetEnvironmentVariable(
                    "RHEMA_TEST_SQLSERVER")
                ?? throw new InvalidOperationException(
                    "RHEMA_TEST_SQLSERVER is required.");
            var databaseName = $"RhemaERP_SupplierContact_{Guid.NewGuid():N}";
            var databaseBuilder = new SqlConnectionStringBuilder(baseConnection)
            {
                InitialCatalog = databaseName,
                TrustServerCertificate = true
            };
            var masterBuilder = new SqlConnectionStringBuilder(baseConnection)
            {
                InitialCatalog = "master",
                TrustServerCertificate = true
            };
            var database = new ContactCorrectionDatabase(
                databaseBuilder.ConnectionString,
                databaseName,
                masterBuilder.ConnectionString);
            await database.CreateAndSeedAsync();
            return database;
        }

        public ApplicationDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlServer(_connectionString)
                .Options;
            return new ApplicationDbContext(options);
        }

        public async Task<SqlConnection> OpenConnectionAsync()
        {
            var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();
            return connection;
        }

        private async Task CreateAndSeedAsync()
        {
            await using (var master = new SqlConnection(_masterConnectionString))
            {
                await master.OpenAsync();
                var create = master.CreateCommand();
                create.CommandText = $"CREATE DATABASE [{_databaseName}];";
                await create.ExecuteNonQueryAsync();
            }

            try
            {
                await using var connection = await OpenConnectionAsync();
                var setup = connection.CreateCommand();
                setup.CommandText = MinimalSchemaSql;
                await setup.ExecuteNonQueryAsync();

                var migrationBuilder = new MigrationBuilder(
                    "Microsoft.EntityFrameworkCore.SqlServer");
                new TestMigration().ApplyUp(migrationBuilder);
                var triggerSql = migrationBuilder.Operations
                    .OfType<SqlOperation>()
                    .Single().Sql;
                var trigger = connection.CreateCommand();
                trigger.CommandText = triggerSql;
                await trigger.ExecuteNonQueryAsync();

                var seed = connection.CreateCommand();
                seed.CommandText = SeedSql;
                seed.Parameters.AddRange(
                [
                    new SqlParameter("@tenant", TenantId),
                    new SqlParameter("@actor", ActorId),
                    new SqlParameter("@registration", RegistrationId),
                    new SqlParameter("@token", TokenId),
                    new SqlParameter("@access", AccessId),
                    new SqlParameter("@businessPartner", BusinessPartnerId),
                    new SqlParameter("@hash", Hash("original@example.test"))
                ]);
                await seed.ExecuteNonQueryAsync();
            }
            catch
            {
                await DisposeAsync();
                throw;
            }
        }

        public async ValueTask DisposeAsync()
        {
            await using var master = new SqlConnection(_masterConnectionString);
            await master.OpenAsync();
            var drop = master.CreateCommand();
            drop.CommandText =
                $"""
                 IF DB_ID(N'{_databaseName}') IS NOT NULL
                 BEGIN
                     ALTER DATABASE [{_databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                     DROP DATABASE [{_databaseName}];
                 END;
                 """;
            await drop.ExecuteNonQueryAsync();
        }

        private sealed class TestMigration :
            AllowPreProvisioningSupplierContactCorrection
        {
            public void ApplyUp(MigrationBuilder builder) => Up(builder);
        }

        private const string MinimalSchemaSql =
            """
            CREATE TABLE dbo.Users
            (
                Id uniqueidentifier NOT NULL PRIMARY KEY,
                TenantId uniqueidentifier NOT NULL,
                IsActive bit NOT NULL
            );
            CREATE TABLE dbo.BusinessPartners
            (
                Id uniqueidentifier NOT NULL PRIMARY KEY,
                TenantId uniqueidentifier NOT NULL
            );
            CREATE TABLE dbo.BusinessPartnerRegistrations
            (
                Id uniqueidentifier NOT NULL PRIMARY KEY,
                TenantId uniqueidentifier NOT NULL,
                Status nvarchar(50) NOT NULL,
                BusinessPartnerId uniqueidentifier NULL
            );
            CREATE TABLE dbo.ProcurementSupplierOnboardingTokens
            (
                Id uniqueidentifier NOT NULL PRIMARY KEY,
                TenantId uniqueidentifier NOT NULL,
                RegistrationId uniqueidentifier NOT NULL,
                Status int NOT NULL
            );
            CREATE TABLE dbo.ProcurementSupplierApplicantAccesses
            (
                Id uniqueidentifier NOT NULL PRIMARY KEY,
                TenantId uniqueidentifier NOT NULL,
                RegistrationId uniqueidentifier NOT NULL,
                TokenId uniqueidentifier NOT NULL,
                Status int NOT NULL,
                VerifiedChannel int NOT NULL,
                VerifiedContactHashSha256 nvarchar(64) NOT NULL,
                VerifiedContactMasked nvarchar(320) NOT NULL,
                VerifiedContact nvarchar(320) NOT NULL,
                VerifiedAtUtc datetime2 NOT NULL,
                CreatedAt datetime2 NOT NULL,
                CreatedBy nvarchar(200) NULL,
                CreatedById uniqueidentifier NULL,
                IsDeleted bit NOT NULL,
                ApprovedUserId uniqueidentifier NULL,
                BusinessPartnerId uniqueidentifier NULL,
                TerminalAtUtc datetime2 NULL,
                TerminalOutcome nvarchar(50) NULL,
                NotificationAttemptCount int NOT NULL
            );
            """;

        private const string SeedSql =
            """
            INSERT dbo.Users (Id, TenantId, IsActive)
            VALUES (@actor, @tenant, 1);
            INSERT dbo.BusinessPartners (Id, TenantId)
            VALUES (@businessPartner, @tenant);
            INSERT dbo.BusinessPartnerRegistrations
                (Id, TenantId, Status, BusinessPartnerId)
            VALUES (@registration, @tenant, N'Approved', @businessPartner);
            INSERT dbo.ProcurementSupplierOnboardingTokens
                (Id, TenantId, RegistrationId, Status)
            VALUES (@token, @tenant, @registration, 2);
            INSERT dbo.ProcurementSupplierApplicantAccesses
            (
                Id, TenantId, RegistrationId, TokenId, Status,
                VerifiedChannel, VerifiedContactHashSha256,
                VerifiedContactMasked, VerifiedContact, VerifiedAtUtc,
                CreatedAt, CreatedBy, CreatedById, IsDeleted,
                ApprovedUserId, BusinessPartnerId, TerminalAtUtc,
                TerminalOutcome, NotificationAttemptCount
            )
            VALUES
            (
                @access, @tenant, @registration, @token, 1,
                0, @hash, N'or***@example.test', N'original@example.test',
                SYSUTCDATETIME(), SYSUTCDATETIME(), N'sql-test', @actor, 0,
                NULL, NULL, SYSUTCDATETIME(), N'Approved', 1
            );
            """;
    }
}
