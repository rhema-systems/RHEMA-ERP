using System.Reflection;
using ErpSystem.Api.Authorization;
using ErpSystem.Api.Controllers.Finance;
using ErpSystem.Shared;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Finance;

public sealed class FinanceApprovalAuthorityContractTests
{
    [Theory]
    [InlineData(nameof(RecurringJournalController.Approve), FinancePermissions.WorkflowApprove)]
    [InlineData(nameof(RecurringJournalController.Reject), FinancePermissions.WorkflowReject)]
    [InlineData(nameof(RecurringJournalController.ApproveOccurrence), FinancePermissions.WorkflowApprove)]
    [InlineData(nameof(RecurringJournalController.RejectOccurrence), FinancePermissions.WorkflowReject)]
    public void RecurringJournalDecisionRoutes_UseWorkflowStagePermissions(
        string actionName,
        string expectedPolicy)
    {
        var action = typeof(RecurringJournalController).GetMethod(
            actionName,
            BindingFlags.Instance | BindingFlags.Public);

        action.Should().NotBeNull();
        action!.GetCustomAttributes<AuthorizeAttribute>()
            .Select(attribute => attribute.Policy)
            .Should().ContainSingle().Which.Should().Be(expectedPolicy);
    }

    [Theory]
    [InlineData("Approve", FinancePermissions.WorkflowApprove)]
    [InlineData("ApproveOccurrence", FinancePermissions.WorkflowApprove)]
    [InlineData("Reject", FinancePermissions.WorkflowReject)]
    [InlineData("RejectOccurrence", FinancePermissions.WorkflowReject)]
    public void RecurringJournalConventionPolicy_MatchesExplicitDecisionPolicy(
        string actionName,
        string expectedPolicy)
    {
        FinancePermissionPolicyMap.GetRequiredPolicies(
                "RecurringJournal",
                actionName,
                ["POST"])
            .Should().Equal(expectedPolicy);
    }

    [Fact]
    public void JournalBatchReviewStage_UsesTheBatchReviewerPermission()
    {
        FinancePermissionPolicyMap.GetRequiredPolicies(
                "JournalBatch",
                "ReviewStage",
                ["POST"])
            .Should().Equal(FinancePermissions.ApproveJournalBatches);
    }

    [Theory]
    [InlineData("Approve", FinancePermissions.WorkflowApprove)]
    [InlineData("Reject", FinancePermissions.WorkflowReject)]
    public void GenericWorkbenchDecisionConvention_UsesWorkflowPermissions(
        string actionName,
        string expectedPolicy)
    {
        FinancePermissionPolicyMap.GetRequiredPolicies(
                "FinanceApprovals",
                actionName,
                ["POST"])
            .Should().Equal(expectedPolicy);
    }

    [Fact]
    public void GenericFinanceWorkbenchAllowlist_MatchesTheAuditedEntityMatrix()
    {
        var expected = new[]
        {
            "JournalEntry",
            "DeltaAdjustmentJournal",
            "JournalBatch",
            "RecurringJournalTemplate",
            "RecurringJournalOccurrence",
            "RecurringJournalOccurrenceWaiver",
            "FinancePurchaseOrder",
            "FinancePurchaseOrderReceipt",
            "VendorInvoice",
            "VendorPayment",
            "PaymentBatch",
            "Quote",
            "SalesOrder",
            "Invoice",
            "ReturnOrder",
            "CreditNote",
            "CustomerPayment",
            "Refund",
            "BudgetScenario",
            "BudgetReturn",
            "BudgetRevision",
            "FinanceBudgetOverride",
            "UnitJournalEntry",
            "UnitAccountBudget",
            "AllocationRule",
            "AllocationRunBatch",
            "CashTransaction",
            "BankReconciliation",
            "ExchangeRate",
            "OpeningBalanceBatch",
            "FixedAsset",
            "FixedAssetDepreciationRun",
            "AssetValuation",
            "AssetTransfer",
            "AssetDisposal",
            "AssetVerificationSession",
            "CapitalProject",
            "LeaseContract"
        };
        var field = typeof(FinanceApprovalsController).GetField(
            "FinanceWorkflowEntityKeys",
            BindingFlags.Static | BindingFlags.NonPublic);

        field.Should().NotBeNull();
        var actual = field!.GetValue(null).Should().BeAssignableTo<IEnumerable<string>>().Subject;
        actual.Should().BeEquivalentTo(expected.Select(value => value.ToUpperInvariant()));
    }

    [Theory]
    [InlineData("SupplierReturn")]
    [InlineData("DeliveryNote")]
    [InlineData("AssetDepreciationSchedule")]
    public void QuarantinedOrUnsubmittedEntities_AreNotGenericWorkbenchActions(string entityType)
    {
        FinanceApprovalsController.IsFinanceEntity(entityType).Should().BeFalse();
    }

    [Fact]
    public void CriticalStartupProvisioning_CoversEveryRecurringWorkflowAndBudgetRevision()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "ErpSystem.Api",
            "Services",
            "DatabaseSeedingService.cs"));

        ExtractMethod(source, "public async Task SeedCriticalFinanceWorkflowDefinitionsAsync()")
            .Should().ContainAll(
                @"""RecurringJournalTemplate""",
                @"""RecurringJournalOccurrence""",
                @"""RecurringJournalOccurrenceWaiver""",
                @"""BudgetRevision""");
        ExtractMethod(source, "private static IReadOnlyList<FinanceWorkflowSeedSpec> GetFinanceWorkflowSeedSpecs()")
            .Should().Contain(@"new(""BudgetRevision""");
    }

    [Fact]
    public void FirstFinanceStageRoles_HaveBothWorkflowDecisionPermissions()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "ErpSystem.Api",
            "Services",
            "DatabaseSeedingService.cs"));

        source.Should().Contain(
            @"new[] { ""Accounts Officer"", ""Senior Accountant"" }",
            "both roles are assigned to the first Finance approval stage");
        ExtractDictionaryEntry(source, @"[""Accounts Officer""]")
            .Should().ContainAll(
                @"""Finance.JournalBatches.Approve""",
                @"""Finance.Workflow.Approve""",
                @"""Finance.Workflow.Reject""");
        ExtractDictionaryEntry(source, @"[""Senior Accountant""]")
            .Should().ContainAll(
                @"""Finance.Workflow.Approve""",
                @"""Finance.Workflow.Reject""");
    }

    [Fact]
    public void RecurringTemplateSubmission_IsAtomicWithWorkflowCreation()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "ErpSystem.Api",
            "Services",
            "Finance",
            "GL",
            "RecurringJournalService.cs"));
        var submit = ExtractMethod(source, "SubmitAsync");
        var transactionHelper = ExtractMethod(
            source,
            "private async Task<T> ExecuteWorkflowSubmissionAtomicallyAsync<T>");

        submit.Should().Contain("ExecuteWorkflowSubmissionAtomicallyAsync");
        submit.Should().Contain("StartApprovalWorkflowAsync");
        submit.Should().Contain("WorkflowInstanceId.HasValue");
        transactionHelper.Should().Contain("CreateExecutionStrategy");
        transactionHelper.Should().Contain("BeginTransactionAsync");
        transactionHelper.Should().Contain("CommitAsync");
        transactionHelper.Should().Contain("RollbackAsync");
    }

    [Fact]
    public void GenericWorkbenchOutcomes_CoverDeltaJournalsAndPaymentBatchRejection()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "ErpSystem.Api",
            "Controllers",
            "Finance",
            "FinanceApprovalsController.cs"));
        var approved = ExtractMethod(source, "private async Task ApplyApprovedOutcomeAsync");
        var rejected = ExtractMethod(source, "private async Task ApplyRejectedOutcomeAsync");

        approved.Should().Contain(@"Normalize(""DeltaAdjustmentJournal"")");
        rejected.Should().Contain(@"Normalize(""DeltaAdjustmentJournal"")");
        rejected.Should().Contain(@"Normalize(""PaymentBatch"")");
        rejected.Should().Contain("PaymentBatchStatus.Cancelled");
        rejected.Should().Contain("AppendReason(item.Notes, reason)");
    }

    private static string ExtractMethod(string source, string methodName)
    {
        var methodIndex = source.IndexOf(methodName, StringComparison.Ordinal);
        methodIndex.Should().BeGreaterThanOrEqualTo(0);
        var bodyStart = source.IndexOf('{', methodIndex);
        bodyStart.Should().BeGreaterThan(methodIndex);
        var depth = 0;
        for (var index = bodyStart; index < source.Length; index++)
        {
            if (source[index] == '{') depth++;
            if (source[index] != '}') continue;
            depth--;
            if (depth == 0)
                return source[bodyStart..(index + 1)];
        }

        throw new InvalidOperationException($"Could not extract {methodName}.");
    }

    private static string ExtractDictionaryEntry(string source, string key)
    {
        var entryIndex = source.IndexOf(key, StringComparison.Ordinal);
        entryIndex.Should().BeGreaterThanOrEqualTo(0);
        var nextEntry = source.IndexOf("                },", entryIndex, StringComparison.Ordinal);
        nextEntry.Should().BeGreaterThan(entryIndex);
        return source[entryIndex..(nextEntry + 18)];
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "src")) &&
                Directory.Exists(Path.Combine(directory.FullName, "tests")))
                return directory.FullName;
            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate repository root from test output directory.");
    }
}
