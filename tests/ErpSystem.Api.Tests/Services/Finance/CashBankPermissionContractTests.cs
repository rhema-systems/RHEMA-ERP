using ErpSystem.Api.Authorization;
using ErpSystem.Api.Controllers.Finance;
using ErpSystem.Shared;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Finance;

public sealed class CashBankPermissionContractTests
{
    [Fact]
    public void BankReconciliationMatches_ShouldBeReadableWithoutPreparationPermission()
    {
        var policies = FinancePermissionPolicyMap.GetRequiredPolicies(
            "BankReconciliation",
            "GetMatches",
            ["GET"],
            ["{id:guid}/matches"]);

        policies.Should().Equal(FinancePermissions.ViewFinance);
        policies.Should().NotContain(FinancePermissions.PerformBankReconciliation);
    }

    [Fact]
    public void BankReconciliationReturnForCorrection_ShouldRequireApprovalPermission()
    {
        FinancePermissionPolicyMap.GetRequiredPolicies(
                "BankReconciliation",
                "ReturnForCorrection",
                ["POST"],
                ["{id:guid}/return-for-correction"])
            .Should().Equal(FinancePermissions.ApproveBankReconciliation);
    }

    [Theory]
    [InlineData(nameof(CashierTillController.UpdateOpening), "PUT")]
    [InlineData(nameof(CashierTillController.CancelSession), "POST")]
    public void UnusedTillManagement_ShouldRequireTillOperatorPermission(string action, string method)
    {
        FinancePermissionPolicyMap.GetRequiredPolicies(
                "CashierTill",
                action,
                [method],
                [$"sessions/{{id:guid}}/{action}"])
            .Should().Equal(FinancePermissions.OperateCashTills);
    }

    [Theory]
    [InlineData("ApproveReturnedCheque")]
    [InlineData("RejectReturnedCheque")]
    public void ReturnedChequeReview_ShouldNotRequireBankDepositApproval(string action)
    {
        var policies = FinancePermissionPolicyMap.GetRequiredPolicies(
            "BankingSettlement",
            action,
            ["POST"],
            [$"returned-cheques/{{id:guid}}/{action}"]);

        policies.Should().Equal(FinancePermissions.ManageReturnedCheques);
        policies.Should().NotContain(FinancePermissions.ApproveBankDeposits);
    }

    [Fact]
    public void ManualDepositPosting_ShouldRequirePostAfterApprovalPermission()
    {
        var policies = FinancePermissionPolicyMap.GetRequiredPolicies(
            "BankingSettlement",
            "PostDeposit",
            ["POST"],
            ["deposits/{id:guid}/post"]);

        policies.Should().Equal(FinancePermissions.WorkflowPostAfterApproval);
        policies.Should().NotContain(FinancePermissions.SubmitBankDeposits);
    }

    [Theory]
    [InlineData(nameof(BankingSettlementController.PostDeposit), FinancePermissions.WorkflowPostAfterApproval)]
    [InlineData(nameof(BankingSettlementController.ApproveReturnedCheque), FinancePermissions.ManageReturnedCheques)]
    [InlineData(nameof(BankingSettlementController.RejectReturnedCheque), FinancePermissions.ManageReturnedCheques)]
    public void ExplicitControllerPolicy_ShouldAgreeWithConventionPolicy(string action, string expectedPolicy)
    {
        var method = typeof(BankingSettlementController).GetMethod(action);

        method.Should().NotBeNull();
        method!.GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .Select(attribute => attribute.Policy)
            .Should().Contain(expectedPolicy);

        FinancePermissionPolicyMap.GetRequiredPolicies(
                "BankingSettlement",
                action,
                ["POST"],
                [$"contract/{action}"])
            .Should().Equal(expectedPolicy);
    }
}
