using System.Reflection;
using ErpSystem.Data.Migrations;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Projects;

/// <summary>
/// Opt-in release proof for the real SQL Server appointment constraints and triggers.
/// RHEMA_TEST_SQLSERVER must point to a SQL login that may create and drop a disposable database.
/// </summary>
public sealed class CivilEngineeringProjectEngineerAssignmentSqlServerTests
{
    [SqlServerFact]
    [Trait("Category", "SqlServerIntegration")]
    public async Task Governed_appointment_lifecycle_is_tenant_safe_unique_and_append_only()
    {
        var baseConnection = Environment.GetEnvironmentVariable("RHEMA_TEST_SQLSERVER")!;
        var databaseName = $"RhemaERP_CivilProjectEngineer_{Guid.NewGuid():N}";
        var masterConnection = new SqlConnectionStringBuilder(baseConnection)
        {
            InitialCatalog = "master",
            TrustServerCertificate = true
        }.ConnectionString;
        var databaseConnection = new SqlConnectionStringBuilder(baseConnection)
        {
            InitialCatalog = databaseName,
            TrustServerCertificate = true
        }.ConnectionString;

        await using var master = new SqlConnection(masterConnection);
        await master.OpenAsync();
        try
        {
            await ExecuteAsync(master, $"CREATE DATABASE [{databaseName}];");
            await using var database = new SqlConnection(databaseConnection);
            await database.OpenAsync();
            await ExecuteAsync(database, MinimalSchemaSql);
            foreach (var operation in Operations())
                await ExecuteAsync(database, operation.Sql);

            var tenant = Guid.NewGuid();
            var project = Guid.NewGuid();
            var policy = Guid.NewGuid();
            var decision = Guid.NewGuid();
            var engineerOne = Guid.NewGuid();
            var engineerTwo = Guid.NewGuid();
            var memberOne = Guid.NewGuid();
            var memberTwo = Guid.NewGuid();
            var assignment = Guid.NewGuid();
            var duplicate = Guid.NewGuid();
            var revision = Guid.NewGuid();

            await ExecuteAsync(database, $$"""
                INSERT dbo.Tenants(Id) VALUES ('{{tenant}}');
                INSERT dbo.Projects(Id,TenantId,IsDeleted) VALUES ('{{project}}','{{tenant}}',0);
                INSERT dbo.Users(Id,TenantId,IsActive) VALUES ('{{engineerOne}}','{{tenant}}',1), ('{{engineerTwo}}','{{tenant}}',1);
                INSERT dbo.ProjectMembers(Id,TenantId,ProjectId,UserId,Role,IsActive,IsDeleted) VALUES
                    ('{{memberOne}}','{{tenant}}','{{project}}','{{engineerOne}}','TDC_PROJECT_ENGINEER',1,0),
                    ('{{memberTwo}}','{{tenant}}','{{project}}','{{engineerTwo}}','TDC_PROJECT_ENGINEER',1,0);
                INSERT dbo.CivilEngineeringConfigurationProfiles(Id,TenantId,IsDeleted,LifecycleStatus) VALUES ('{{policy}}','{{tenant}}',0,1);
                INSERT dbo.CivilEngineeringConfigurationDecisions(Id,TenantId,ProfileId,ConfigurationKey,IsDeleted,Status,ApprovalStatus,EvidenceStatus)
                    VALUES ('{{decision}}','{{tenant}}','{{policy}}','CIV-CFG-005',0,2,1,2);
                """);

            await ExecuteAsync(database, AssignmentInsertSql(assignment, tenant, project, memberOne, engineerOne, policy, decision));

            var duplicateAction = () => ExecuteAsync(database,
                AssignmentInsertSql(duplicate, tenant, project, memberTwo, engineerTwo, policy, decision));
            var duplicateException = await duplicateAction.Should().ThrowAsync<SqlException>();
            duplicateException.Which.Number.Should().BeOneOf(2601, 2627);

            await ExecuteAsync(database,
                $"UPDATE dbo.ProjectCivilProjectEngineerAssignments SET IsActive=0, EffectiveTo='2026-08-21T00:00:00Z' WHERE Id='{assignment}';");
            var reopenAction = () => ExecuteAsync(database,
                $"UPDATE dbo.ProjectCivilProjectEngineerAssignments SET IsActive=1, EffectiveTo=NULL WHERE Id='{assignment}';");
            (await reopenAction.Should().ThrowAsync<SqlException>()).Which.Number.Should().Be(52003);

            await ExecuteAsync(database, $$"""
                INSERT dbo.ProjectCivilProjectEngineerAssignmentRevisions
                    (Id,AssignmentId,Action,ActorUserId,ActorName,CorrelationId,AfterJson,CreatedAt,TenantId,IsDeleted)
                VALUES ('{{revision}}','{{assignment}}','CIVIL_PROJECT_ENGINEER_ENDED','{{engineerOne}}',N'Engineer One',N'civil-sql-proof',N'{"state":"ended"}',SYSUTCDATETIME(),'{{tenant}}',0);
                """);
            var revisionAction = () => ExecuteAsync(database,
                $"UPDATE dbo.ProjectCivilProjectEngineerAssignmentRevisions SET Action=N'changed' WHERE Id='{revision}';");
            (await revisionAction.Should().ThrowAsync<SqlException>()).Which.Number.Should().Be(52005);
        }
        finally
        {
            await ExecuteAsync(master,
                $"IF DB_ID(N'{databaseName}') IS NOT NULL BEGIN ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{databaseName}]; END;");
        }
    }

    private static IReadOnlyList<SqlOperation> Operations()
    {
        var migration = new AddCivilEngineeringProjectEngineerAssignments();
        var builder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
        migration.GetType().GetMethod("Up", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(migration, [builder]);
        return builder.Operations.OfType<SqlOperation>().ToList();
    }

    private static Task ExecuteAsync(SqlConnection connection, string sql)
    {
        var command = connection.CreateCommand();
        command.CommandText = sql;
        return command.ExecuteNonQueryAsync();
    }

    private static string AssignmentInsertSql(Guid id, Guid tenant, Guid project, Guid member, Guid user, Guid policy, Guid decision) => $$"""
        INSERT dbo.ProjectCivilProjectEngineerAssignments
            (Id,ProjectId,ProjectMemberId,AssignedUserId,SourceCivilRole,ProjectRole,Authority,EffectiveFrom,EffectiveTo,IsActive,
             ConfigurationProfileId,ConfigurationDecisionId,PolicyHash,ClientRequestId,RequestHash,CorrelationId,CreatedAt,TenantId,IsDeleted)
        VALUES
            ('{{id}}','{{project}}','{{member}}','{{user}}',N'TDC_CIVIL_ENGINEER',N'TDC_PROJECT_ENGINEER',2,'2026-08-20T00:00:00Z',NULL,1,
             '{{policy}}','{{decision}}',REPLICATE('A',64),NEWID(),REPLICATE('B',64),N'civil-sql-proof',SYSUTCDATETIME(),'{{tenant}}',0);
        """;

    private sealed class SqlServerFactAttribute : FactAttribute
    {
        public SqlServerFactAttribute()
        {
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("RHEMA_TEST_SQLSERVER")))
                Skip = "Set RHEMA_TEST_SQLSERVER to run the disposable Civil Project Engineer trigger gate.";
        }
    }

    private const string MinimalSchemaSql = """
        CREATE TABLE dbo.Tenants (Id uniqueidentifier NOT NULL PRIMARY KEY);
        CREATE TABLE dbo.Projects (Id uniqueidentifier NOT NULL PRIMARY KEY, TenantId uniqueidentifier NOT NULL, IsDeleted bit NOT NULL);
        CREATE TABLE dbo.Users (Id uniqueidentifier NOT NULL PRIMARY KEY, TenantId uniqueidentifier NOT NULL, IsActive bit NOT NULL);
        CREATE TABLE dbo.ProjectMembers
        (
            Id uniqueidentifier NOT NULL PRIMARY KEY, TenantId uniqueidentifier NOT NULL, ProjectId uniqueidentifier NOT NULL,
            UserId uniqueidentifier NOT NULL, Role nvarchar(100) NOT NULL, IsActive bit NOT NULL, IsDeleted bit NOT NULL
        );
        CREATE TABLE dbo.CivilEngineeringConfigurationProfiles
        (
            Id uniqueidentifier NOT NULL PRIMARY KEY, TenantId uniqueidentifier NOT NULL, IsDeleted bit NOT NULL, LifecycleStatus int NOT NULL
        );
        CREATE TABLE dbo.CivilEngineeringConfigurationDecisions
        (
            Id uniqueidentifier NOT NULL PRIMARY KEY, TenantId uniqueidentifier NOT NULL, ProfileId uniqueidentifier NOT NULL,
            ConfigurationKey nvarchar(100) NOT NULL, IsDeleted bit NOT NULL, Status int NOT NULL, ApprovalStatus int NOT NULL, EvidenceStatus int NOT NULL
        );
        CREATE TABLE dbo.ProjectCivilProjectEngineerAssignments
        (
            Id uniqueidentifier NOT NULL PRIMARY KEY, ProjectId uniqueidentifier NOT NULL, ProjectMemberId uniqueidentifier NOT NULL,
            AssignedUserId uniqueidentifier NOT NULL, SourceCivilRole nvarchar(100) NOT NULL, ProjectRole nvarchar(100) NOT NULL,
            Authority int NOT NULL, EffectiveFrom datetime2 NOT NULL, EffectiveTo datetime2 NULL, IsActive bit NOT NULL,
            ConfigurationProfileId uniqueidentifier NOT NULL, ConfigurationDecisionId uniqueidentifier NOT NULL, PolicyHash varchar(64) NOT NULL,
            ClientRequestId uniqueidentifier NOT NULL, RequestHash varchar(64) NOT NULL, Reason nvarchar(2000) NULL,
            CorrelationId nvarchar(100) NOT NULL, CreatedAt datetime2 NOT NULL, TenantId uniqueidentifier NOT NULL, IsDeleted bit NOT NULL
        );
        CREATE UNIQUE INDEX IX_CivilProjectEngineer_ActiveProject ON dbo.ProjectCivilProjectEngineerAssignments(TenantId,ProjectId,IsActive)
            WHERE IsDeleted=0 AND IsActive=1;
        CREATE TABLE dbo.ProjectCivilProjectEngineerAssignmentRevisions
        (
            Id uniqueidentifier NOT NULL PRIMARY KEY, AssignmentId uniqueidentifier NOT NULL, Action nvarchar(100) NOT NULL,
            ActorUserId uniqueidentifier NOT NULL, ActorName nvarchar(300) NOT NULL, ActorRoles nvarchar(500) NULL,
            CorrelationId nvarchar(100) NOT NULL, Reason nvarchar(2000) NULL, BeforeJson nvarchar(max) NULL, AfterJson nvarchar(max) NOT NULL,
            CreatedAt datetime2 NOT NULL, TenantId uniqueidentifier NOT NULL, IsDeleted bit NOT NULL
        );
        """;
}
