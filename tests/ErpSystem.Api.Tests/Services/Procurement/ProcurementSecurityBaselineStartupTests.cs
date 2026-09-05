using FluentAssertions;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Procurement;

public sealed class ProcurementSecurityBaselineStartupTests
{
    [Fact]
    [Trait("Category", "Deployment")]
    public void RuntimeStartupReconcilesProcurementSecurityAfterMigrationUnderDatabaseFailurePolicy()
    {
        var root = FindRepositoryRoot();
        var program = File.ReadAllText(Path.Combine(root, "src", "ErpSystem.Api", "Program.cs"));
        var initializationTry = program.IndexOf(
            "await InitializeDatabaseAsync(app, databaseConnectionTimeout, migrationTimeout);",
            StringComparison.Ordinal);
        var reconciliation = program.IndexOf(
            "await ReconcileProcurementSecurityBaselineAsync(app);",
            initializationTry,
            StringComparison.Ordinal);
        var initializationSucceeded = program.IndexOf(
            "databaseInitializationSucceeded = true;",
            reconciliation,
            StringComparison.Ordinal);
        var databaseFailurePolicy = program.IndexOf(
            "if (failFastOnDatabaseInitializationError)",
            initializationSucceeded,
            StringComparison.Ordinal);
        var developmentSeedingGate = program.IndexOf(
            "if (seedDevelopmentData && developmentDataSeedingPermitted && databaseInitializationSucceeded)",
            StringComparison.Ordinal);

        initializationTry.Should().BeGreaterThanOrEqualTo(0);
        reconciliation.Should().BeGreaterThan(initializationTry,
            "the security baseline may only run after successful migrations");
        initializationSucceeded.Should().BeGreaterThan(reconciliation,
            "a reconciliation failure must keep database initialization unsuccessful");
        databaseFailurePolicy.Should().BeGreaterThan(initializationSucceeded,
            "reconciliation failures must follow the database initialization fail-fast policy");
        developmentSeedingGate.Should().BeGreaterThan(reconciliation,
            "the security baseline must not depend on development/demo data seeding");
        program.Should().Contain("GetRequiredService<ProcurementAccessControlSeeder>()");
        program.Should().Contain("ReconcileIdentityAccessBaselineAsync()");
    }

    private static string FindRepositoryRoot(
        [System.Runtime.CompilerServices.CallerFilePath] string sourceFile = "")
    {
        var directory = new DirectoryInfo(Path.GetDirectoryName(sourceFile)!);
        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "src")) &&
                Directory.Exists(Path.Combine(directory.FullName, "tests")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Repository root could not be located.");
    }
}
