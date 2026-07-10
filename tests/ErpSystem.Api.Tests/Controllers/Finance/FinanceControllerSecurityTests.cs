using System.Reflection;
using ErpSystem.Api.Authorization;
using ErpSystem.Api.Controllers.Finance;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Shared;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Xunit;
using RootFinanceController = ErpSystem.Api.Controllers.FinanceController;

namespace ErpSystem.Api.Tests.Controllers.Finance;

public sealed class FinanceControllerSecurityTests
{
    public static TheoryData<Type> CashAndBankControllerTypes => new()
    {
        typeof(BankAccountController),
        typeof(CashTransactionController),
        typeof(BankReconciliationController),
        typeof(CashReportsController)
    };

    [Theory]
    [MemberData(nameof(CashAndBankControllerTypes))]
    [Trait("Batch", "FinanceGoLive-1")]
    [Trait("Category", "FinanceSecurity")]
    public void CashAndBankControllers_RequireAuthenticatedUsers(Type controllerType)
    {
        controllerType
            .GetCustomAttributes<AuthorizeAttribute>(inherit: true)
            .Should()
            .NotBeEmpty($"{controllerType.Name} exposes cash, bank, reconciliation, or cash reporting data");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-1-Diagnostic")]
    [Trait("Category", "FinanceSecurity")]
    public void Diagnostic_FinanceMutationActions_ShouldRequireActionLevelPermissionPolicies()
    {
        var mutationActions = new (Type Controller, string Action)[]
        {
            (typeof(JournalEntryController), "CreateJournalEntry"),
            (typeof(JournalEntryController), "PostJournalEntry"),
            (typeof(JournalEntryController), "ReverseJournalEntry"),
            (typeof(VendorPaymentController), "Create"),
            (typeof(PaymentBatchController), "Create"),
            (typeof(PaymentBatchController), "Approve"),
            (typeof(PaymentBatchController), "Process"),
            (typeof(PaymentController), "Create")
        };

        foreach (var (controller, action) in mutationActions)
        {
            var method = controller.GetMethods(BindingFlags.Instance | BindingFlags.Public)
                .SingleOrDefault(m => m.Name == action);

            method.Should().NotBeNull($"{controller.Name}.{action} should exist");
            var policies = GetMappedPolicies(controller, method!);

            policies.Should()
                .NotBeEmpty($"{controller.Name}.{action} should require an action-level Finance policy");
            policies.Should()
                .OnlyContain(policy => FinancePermissions.AllNames.Contains(policy), "Finance policies must be backed by seeded permissions");
        }
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-2")]
    [Trait("Category", "FinanceSecurity")]
    public void FinanceControllerActions_ShouldHaveMappedFinancePermissionPolicies()
    {
        var financeControllers = typeof(RootFinanceController).Assembly
            .GetTypes()
            .Where(type =>
                (type.Namespace == "ErpSystem.Api.Controllers.Finance" || type == typeof(RootFinanceController)) &&
                typeof(ControllerBase).IsAssignableFrom(type) &&
                !type.IsAbstract)
            .ToList();

        financeControllers.Should().NotBeEmpty();

        foreach (var controller in financeControllers)
        {
            var actions = controller
                .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                .Where(method => method.GetCustomAttributes<HttpMethodAttribute>(inherit: true).Any())
                .ToList();

            actions.Should().NotBeEmpty($"{controller.Name} should expose mapped HTTP actions or be removed from Finance controllers");

            foreach (var action in actions)
            {
                var policies = GetMappedPolicies(controller, action);
                policies.Should()
                    .NotBeEmpty($"{controller.Name}.{action.Name} must be covered by the Finance permission convention");
                policies.Should()
                    .OnlyContain(policy => FinancePermissions.AllNames.Contains(policy), $"{controller.Name}.{action.Name} must use seeded Finance permissions");
            }
        }
    }

    [Theory]
    [InlineData(typeof(JournalEntryController), "CreateJournalEntry", FinancePermissions.CreateJournalEntries)]
    [InlineData(typeof(JournalEntryController), "RequestApproval", FinancePermissions.SubmitJournalEntries)]
    [InlineData(typeof(JournalEntryController), "PostJournalEntry", FinancePermissions.PostJournalEntries)]
    [InlineData(typeof(JournalEntryController), "ReverseJournalEntry", FinancePermissions.ReverseJournalEntries)]
    [InlineData(typeof(JournalEntryController), "ApproveJournalEntry", FinancePermissions.ApproveJournalEntries)]
    [InlineData(typeof(VendorInvoiceController), "Post", FinancePermissions.PostApInvoices)]
    [InlineData(typeof(VendorPaymentController), "Create", FinancePermissions.ProcessApPayments)]
    [InlineData(typeof(PaymentBatchController), "Approve", FinancePermissions.ApproveApPayments)]
    [InlineData(typeof(PaymentBatchController), "Process", FinancePermissions.ProcessApPayments)]
    [InlineData(typeof(PaymentController), "Create", FinancePermissions.ReceiveCustomerPayments)]
    [InlineData(typeof(CashTransactionController), "Submit", FinancePermissions.WorkflowSubmit)]
    [InlineData(typeof(CashTransactionController), "Approve", FinancePermissions.WorkflowApprove)]
    [InlineData(typeof(CashTransactionController), "Reject", FinancePermissions.WorkflowReject)]
    [InlineData(typeof(CashTransactionController), "Return", FinancePermissions.WorkflowRequestChanges)]
    [InlineData(typeof(CashTransactionController), "Cancel", FinancePermissions.WorkflowCancel)]
    [InlineData(typeof(CashTransactionController), "Post", FinancePermissions.WorkflowPostAfterApproval)]
    [InlineData(typeof(FinanceApprovalsController), "Approve", FinancePermissions.WorkflowApprove)]
    [InlineData(typeof(FinanceApprovalsController), "Reject", FinancePermissions.WorkflowReject)]
    [InlineData(typeof(RootFinanceController), "GetTrialBalance", FinancePermissions.RunFinanceReports)]
    [InlineData(typeof(FixedAssetsController), "ExportToExcel", FinancePermissions.ExportFinanceReports)]
    [InlineData(typeof(FinanceReportExportsController), "Export", FinancePermissions.ExportFinanceReports)]
    [InlineData(typeof(FinanceReportExportsController), "Print", FinancePermissions.ExportFinanceReports)]
    [Trait("Batch", "FinanceGoLive-2")]
    [Trait("Category", "FinanceSecurity")]
    public void CriticalFinanceActions_ShouldMapToExpectedPermissions(Type controllerType, string actionName, string expectedPermission)
    {
        var method = controllerType.GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .SingleOrDefault(method => method.Name == actionName);

        method.Should().NotBeNull($"{controllerType.Name}.{actionName} should exist");
        GetMappedPolicies(controllerType, method!)
            .Should()
            .Contain(expectedPermission);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-3")]
    [Trait("Category", "TenantIsolation")]
    public void CashAndBankEntities_ShouldDeclareTenantId()
    {
        var entityTypes = new[]
        {
            typeof(BankAccount),
            typeof(CashTransaction),
            typeof(BankStatement),
            typeof(BankStatementLine),
            typeof(BankReconciliation),
            typeof(ReconciliationMatch)
        };

        foreach (var entityType in entityTypes)
        {
            entityType.GetProperty("TenantId")?.PropertyType
                .Should()
                .Be(typeof(Guid), $"{entityType.Name} participates in cash/bank workflows and must be tenant-scoped");
        }
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-3-Diagnostic")]
    [Trait("Category", "TenantIsolation")]
    public void Diagnostic_HighRiskFinanceSource_ShouldNotUseDefaultTenantFallbacksOrFindAsync()
    {
        var root = FindRepositoryRoot();
        var financeSourceFiles = Directory
            .EnumerateFiles(Path.Combine(root, "src", "ErpSystem.Api", "Services", "Finance"), "*.cs", SearchOption.AllDirectories)
            .Concat(Directory.EnumerateFiles(Path.Combine(root, "src", "ErpSystem.Api", "Controllers", "Finance"), "*.cs", SearchOption.TopDirectoryOnly))
            .ToList();

        financeSourceFiles.Should().NotBeEmpty();

        var violations = new List<string>();
        foreach (var path in financeSourceFiles)
        {
            var text = File.ReadAllText(path);
            if (text.Contains("FindAsync(", StringComparison.Ordinal))
                violations.Add($"{Path.GetRelativePath(root, path)} contains direct FindAsync usage");
            if (text.Contains("DefaultTenantId", StringComparison.Ordinal))
                violations.Add($"{Path.GetRelativePath(root, path)} contains DefaultTenantId fallback");
            if (text.Contains("?? DefaultTenantId", StringComparison.Ordinal))
                violations.Add($"{Path.GetRelativePath(root, path)} contains default tenant coalescing");

            var lines = text.Split('\n');
            for (var index = 0; index < lines.Length; index++)
            {
                var line = lines[index];
                if (line.Contains("TenantId", StringComparison.Ordinal) &&
                    line.Contains("?? Guid.Empty", StringComparison.Ordinal))
                {
                    violations.Add($"{Path.GetRelativePath(root, path)}:{index + 1} contains Guid.Empty tenant fallback");
                }
            }
        }

        violations.Should().BeEmpty("Finance tenant isolation must fail closed and avoid ID-only EF lookups");
    }

    private static IReadOnlyList<string> GetMappedPolicies(Type controllerType, MethodInfo action)
    {
        var attributes = action.GetCustomAttributes<HttpMethodAttribute>(inherit: true).ToArray();
        var httpMethods = attributes.SelectMany(attribute => attribute.HttpMethods);
        var routeTemplates = attributes.Select(attribute => attribute.Template);

        return FinancePermissionPolicyMap.GetRequiredPolicies(
            controllerType.Name,
            action.Name,
            httpMethods,
            routeTemplates);
    }

    private static string FindRepositoryRoot([System.Runtime.CompilerServices.CallerFilePath] string sourcePath = "")
    {
        var directory = new DirectoryInfo(Path.GetDirectoryName(sourcePath) ?? AppContext.BaseDirectory);
        while (directory != null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "src")) &&
                File.Exists(Path.Combine(directory.FullName, "ErpSystem.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate repository root from test output directory.");
    }
}
