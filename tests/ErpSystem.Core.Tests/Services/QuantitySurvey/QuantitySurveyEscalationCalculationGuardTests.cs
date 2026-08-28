using FluentAssertions;
using Xunit;

namespace ErpSystem.Core.Tests.Services.QuantitySurvey;

public sealed class QuantitySurveyEscalationCalculationGuardTests
{
    [Fact]
    public void Migration_is_narrow_discoverable_and_has_database_hard_stops()
    {
        var root = FindRepositoryRoot();
        const string migrationId = "20260809221500_AddQuantitySurveyEscalationCalculationRuns";
        var migration = File.ReadAllText(Path.Combine(root, "src", "ErpSystem.Data", "Migrations", migrationId + ".cs"));
        var preflight = File.ReadAllText(Path.Combine(root, "scripts", "vps", "Invoke-RhemaVpsRemote.ps1"));

        migration.Should().Contain($"[Migration(\"{migrationId}\")]");
        migration.Should().Contain("TR_QsEscalationCalculationRuns_Guard");
        migration.Should().Contain("TR_QsEscalationCalculationLines_Guard");
        migration.Should().Contain("TR_QsEscalationCalculationRevisions_Guard");
        migration.Should().Contain("Escalation calculation source, target, formula, policy, authority and preparer lineage is immutable");
        migration.Should().Contain("approved-index lineage is invalid");
        migration.Should().Contain("revision history is append-only");
        migration.Should().Contain("QS-DEC-006");
        migration.Should().NotContain("EstateManagedAssets");
        migration.Should().NotContain("FinanceSettings");
        preflight.Should().Contain($"GUARD_COVERAGE|{migrationId}");
    }

    [Fact]
    public void Service_reuses_governed_owners_and_applies_final_account_impacts_atomically()
    {
        var root = FindRepositoryRoot();
        var service = File.ReadAllText(Path.Combine(root, "src", "ErpSystem.Api", "Services", "QuantitySurvey", "QuantitySurveyEscalationCalculationService.cs"));

        service.Should().Contain("QuantitySurveyWorkflowBindingRegistry.Escalation");
        service.Should().Contain("IsolationLevel.Serializable");
        service.Should().Contain("entity.PreparedById == UserId");
        service.Should().Contain("ApprovedPendingApplication");
        service.Should().Contain("ImpactApplicationStatus = \"PendingApplication\"");
        service.Should().Contain("ApplyApprovedFinalAccountImpactsAsync");
        service.Should().Contain("db.Database.CurrentTransaction");
        service.Should().Contain("value.Outcome != QuantitySurveyEscalationDisputeOutcome.Accepted");
        service.Should().Contain("run.ImpactApplicationStatus = \"Applied\"");
        service.Should().Contain("QuantitySurveyAuditEventMap.ApplyEscalationToFinalAccount");
        service.Should().Contain("ProjectPaymentCertificates");
        service.Should().Contain("ProjectFinalAccounts");
        service.Should().NotContain("new PaymentCertificate");
        service.Should().NotContain("new ProjectFinalAccount");
    }

    private static string FindRepositoryRoot(
        [System.Runtime.CompilerServices.CallerFilePath] string sourceFile = "")
    {
        for (var directory = new FileInfo(sourceFile).Directory;
             directory is not null;
             directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "ErpSystem.sln")))
                return directory.FullName;
        }

        throw new DirectoryNotFoundException("Repository root containing ErpSystem.sln was not found.");
    }
}
