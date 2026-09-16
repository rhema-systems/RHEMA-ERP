using ErpSystem.Core.Services.QuantitySurvey;
using FluentAssertions;
using Xunit;

namespace ErpSystem.Core.Tests.Services.QuantitySurvey;

public sealed class QuantitySurveyFinalAccountRulesTests
{
    [Fact]
    public void Calculate_derives_the_final_position_from_governed_sources()
    {
        var result = QuantitySurveyFinalAccountRules.Calculate(
            originalContractValue: 1_000_000m,
            approvedVariationAmount: 120_000m,
            approvedClaimAmount: 30_000m,
            approvedEscalationAmount: 50_000m,
            advanceRecoveryAmount: 80_000m,
            materialDeductionAmount: 10_000m,
            otherDeductionAmount: 5_000m,
            retentionHeldAmount: 60_000m,
            retentionReleasedAmount: 40_000m,
            paidToDateAmount: 900_000m);

        result.GrossFinalAccountValue.Should().Be(1_200_000m);
        result.TotalDeductionAmount.Should().Be(95_000m);
        result.NetFinalAccountValue.Should().Be(1_105_000m);
        result.RetentionOutstandingAmount.Should().Be(20_000m);
        result.FinalPaymentAmount.Should().Be(205_000m);
    }

    [Theory]
    [InlineData(-1, 0, 0)]
    [InlineData(100, 101, 0)]
    [InlineData(100, 0, 101)]
    public void Calculate_rejects_invalid_commercial_positions(
        decimal gross, decimal retentionReleased, decimal deductions)
    {
        Action action = () => QuantitySurveyFinalAccountRules.Calculate(
            gross, 0m, 0m, 0m, deductions, 0m, 0m, 100m,
            retentionReleased, 0m);

        action.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Closure_requires_approved_sources_settlement_and_resolved_work()
    {
        QuantitySurveyFinalAccountRules.ClosureBlockers(false, true, 10m, 25m)
            .Should().ContainInOrder(
                "An approved Published BoQ is required.",
                "Pending variations, valuations, certificates, claims or escalation records must be resolved.",
                "Outstanding retention must be released or formally resolved.",
                "Finance-owned payments do not yet settle the final account.");

        QuantitySurveyFinalAccountRules.ClosureBlockers(true, false, 0m, 0m)
            .Should().BeEmpty();
    }

    [Fact]
    public void Approval_enforces_independent_maker_checker_roles()
    {
        var preparer = Guid.NewGuid();
        var submitter = Guid.NewGuid();

        Action samePreparer = () => QuantitySurveyFinalAccountRules.RequireIndependentApprover(
            preparer, submitter, preparer);
        Action sameSubmitter = () => QuantitySurveyFinalAccountRules.RequireIndependentApprover(
            preparer, submitter, submitter);

        samePreparer.Should().Throw<InvalidOperationException>();
        sameSubmitter.Should().Throw<InvalidOperationException>();
        QuantitySurveyFinalAccountRules.RequireIndependentApprover(
            preparer, submitter, Guid.NewGuid());
    }

    [Fact]
    public void Migration_guards_lifecycle_sources_settlement_and_append_only_history()
    {
        var source = Source("src", "ErpSystem.Data", "LegacyMigrationsArchive",
            "20260810202208_AddQuantitySurveyFinalAccountLifecycle.cs");

        source.Should().Contain("TR_ProjectFinalAccounts_QS0506Guard")
            .And.Contain("TR_ProjectFinalAccountRevisions_QS0506AppendOnly")
            .And.Contain("AFTER INSERT, UPDATE, DELETE")
            .And.Contain("Invalid governed QS final-account lifecycle transition")
            .And.Contain("QS-DEC-012")
            .And.Contain("QS_FINAL_ACCOUNT")
            .And.Contain("i.[ApprovedById] = i.[PreparedById]")
            .And.Contain("i.[ApprovedById] = i.[SubmittedById]")
            .And.Contain("CK_ProjectFinalAccountRevisions_QS0506Request")
            .And.Contain("LEN(ISNULL(i.[ApprovedBoqSnapshotHash], '')) <> 64")
            .And.Contain("i.[PaidToDateAmount] + 0.01 < i.[FinalAccountValue]")
            .And.Contain("QS final-account audit revisions are append-only");
    }

    [Fact]
    public void Service_uses_append_only_revision_keys_for_durable_retry_safety()
    {
        var source = Source("src", "ErpSystem.Api", "Services", "QuantitySurvey",
            "QuantitySurveyFinalAccountService.cs");

        source.Should().Contain("db.ProjectFinalAccountRevisions.AsNoTracking().FirstOrDefaultAsync")
            .And.Contain("value.TenantId == TenantId && value.ClientRequestId == request.ClientRequestId")
            .And.Contain("This client request identifier is already bound to different final-account inputs.")
            .And.Contain("This client request identifier is already bound to a different final-account action.");
    }

    [Fact]
    public void Final_account_refresh_applies_only_governed_accepted_escalation_impacts()
    {
        var finalAccount = Source("src", "ErpSystem.Api", "Services", "QuantitySurvey",
            "QuantitySurveyFinalAccountService.cs");
        var escalation = Source("src", "ErpSystem.Api", "Services", "QuantitySurvey",
            "QuantitySurveyEscalationCalculationService.cs");

        finalAccount.Should().Contain("ApplyApprovedFinalAccountImpactsAsync")
            .And.Contain("value.Status == \"ApprovedPendingApplication\"")
            .And.Contain("value.ImpactApplicationStatus == \"Applied\"");
        escalation.Should().Contain("db.Database.CurrentTransaction")
            .And.Contain("value.Outcome != QuantitySurveyEscalationDisputeOutcome.Accepted")
            .And.Contain("await ValidateStoredAsync(run, cancellationToken)")
            .And.Contain("run.ImpactApplicationStatus = \"Applied\"")
            .And.Contain("QuantitySurveyAuditEventMap.ApplyEscalationToFinalAccount")
            .And.Contain("await SaveAsync(cancellationToken)");
    }

    private static string Source(params string[] path) =>
        File.ReadAllText(Path.Combine(FindRepositoryRoot(), Path.Combine(path)));

    private static string FindRepositoryRoot(
        [System.Runtime.CompilerServices.CallerFilePath] string sourceFile = "")
    {
        foreach (var start in new[] { Environment.CurrentDirectory, Path.GetDirectoryName(sourceFile) })
            for (var directory = string.IsNullOrWhiteSpace(start) ? null : new DirectoryInfo(start);
                 directory is not null;
                 directory = directory.Parent)
                if (File.Exists(Path.Combine(directory.FullName, "ErpSystem.sln"))) return directory.FullName;
        throw new DirectoryNotFoundException("Repository root containing ErpSystem.sln was not found.");
    }
}
