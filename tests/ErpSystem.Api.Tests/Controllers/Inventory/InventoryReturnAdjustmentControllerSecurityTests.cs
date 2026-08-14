using System.Reflection;
using System.Security.Claims;
using ErpSystem.Api.Controllers.Inventory;
using ErpSystem.Api.Services.Finance;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.DTOs.Inventory;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Interfaces.Inventory;
using ErpSystem.Core.Services.Inventory;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Controllers.Inventory;

public sealed class InventoryReturnAdjustmentControllerSecurityTests
{
    [Theory]
    [InlineData(typeof(InventoryRequisitionsController))]
    [InlineData(typeof(StockAdjustmentsController))]
    public void Controlled_inventory_controllers_require_internal_tenant_authentication(Type controllerType)
    {
        var authorization = controllerType.GetCustomAttributes<AuthorizeAttribute>(inherit: true).Single();
        authorization.Policy.Should().Be("InternalOnly");
        controllerType.GetCustomAttribute<AllowAnonymousAttribute>(inherit: true).Should().BeNull();
    }

    [Theory]
    [InlineData(nameof(InventoryRequisitionsController.Return), "{id}/return")]
    [InlineData(nameof(InventoryRequisitionsController.DecideReturnVoucher), "return-vouchers/{voucherId:guid}/decision")]
    [InlineData(nameof(InventoryRequisitionsController.PostReturnVoucher), "return-vouchers/{voucherId:guid}/post")]
    [InlineData(nameof(InventoryRequisitionsController.ReverseReturnVoucher), "return-vouchers/{voucherId:guid}/reverse")]
    public void Return_mutations_are_post_only_and_have_no_anonymous_override(string methodName, string template)
    {
        var method = typeof(InventoryRequisitionsController).GetMethod(methodName)!;
        method.GetCustomAttribute<HttpPostAttribute>()!.Template.Should().Be(template);
        method.GetCustomAttribute<AllowAnonymousAttribute>(inherit: true).Should().BeNull();
    }

    [Theory]
    [InlineData(nameof(StockAdjustmentsController.Submit), "{id}/submit")]
    [InlineData(nameof(StockAdjustmentsController.Decide), "{id}/decision")]
    [InlineData(nameof(StockAdjustmentsController.Post), "{id}/post")]
    [InlineData(nameof(StockAdjustmentsController.Reverse), "{id}/reverse")]
    public void Adjustment_lifecycle_mutations_are_post_only_and_have_no_anonymous_override(string methodName, string template)
    {
        var method = typeof(StockAdjustmentsController).GetMethod(methodName)!;
        method.GetCustomAttribute<HttpPostAttribute>()!.Template.Should().Be(template);
        method.GetCustomAttribute<AllowAnonymousAttribute>(inherit: true).Should().BeNull();
    }

    [Fact]
    public void Return_voucher_download_is_a_protected_tenant_route()
    {
        var method = typeof(InventoryRequisitionsController).GetMethod(nameof(InventoryRequisitionsController.DownloadReturnVoucher))!;
        method.GetCustomAttribute<HttpGetAttribute>()!.Template.Should().Be("return-vouchers/{voucherId:guid}/download");
        method.GetCustomAttribute<AllowAnonymousAttribute>(inherit: true).Should().BeNull();
    }

    [Fact]
    public async Task Adjustment_creation_reports_changed_idempotency_payload_as_conflict()
    {
        var userId = Guid.NewGuid();
        var service = new Mock<IStockAdjustmentService>();
        service.Setup(value => value.CreateAsync(It.IsAny<CreateStockAdjustmentDto>(), userId))
            .ThrowsAsync(new StockAdjustmentIdempotencyConflictException("Idempotency payload changed."));
        var controller = new StockAdjustmentsController(service.Object,
            Mock.Of<ILogger<StockAdjustmentsController>>())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        new[] { new Claim(ClaimTypes.NameIdentifier, userId.ToString()) }, "test"))
                }
            }
        };

        var result = await controller.Create(new CreateStockAdjustmentDto());

        result.Result.Should().BeOfType<ConflictObjectResult>()
            .Which.Value.Should().Be("Idempotency payload changed.");
    }

    [Fact]
    public void Fixed_asset_return_reversal_restores_the_governed_line_token_without_changing_other_lines()
    {
        var returnLineId = Guid.NewGuid();
        var assetAccountId = Guid.NewGuid();
        var inventoryAccountId = Guid.NewGuid();
        var token = returnLineId.ToString("N");
        var original = new[]
        {
            new AccountTransaction { LineNumber = 1, AccountId = inventoryAccountId, DebitAmount = 595m },
            new AccountTransaction
            {
                LineNumber = 2, AccountId = assetAccountId, CreditAmount = 595m,
                Notes = $"InventoryReturnVoucherLineId={token}", TransactionTag = $"INV-RETURN-FA-{token}"
            }
        };
        var reversal = new[]
        {
            new FinancePostingLineDto
                { LineNumber = 1, AccountId = inventoryAccountId, CreditAmount = 595m, TransactionTag = "Reversal" },
            new FinancePostingLineDto
                { LineNumber = 2, AccountId = assetAccountId, DebitAmount = 595m, TransactionTag = "Reversal" }
        };

        InventoryIssueFinanceAssetPostingService.PreserveFixedAssetReversalLineage(
            reversal, original, new[] { new KeyValuePair<Guid, decimal>(returnLineId, 595m) }, "Acceptance reversal");

        reversal[0].TransactionTag.Should().Be("Reversal");
        reversal[1].TransactionTag.Should().Be($"INV-RET-FA-R-{token}");
        reversal[1].Notes.Should().Contain($"InventoryReturnVoucherLineId={token}");
    }

    [Fact]
    public void Fixed_asset_return_reversal_rejects_a_plan_without_exact_value_lineage()
    {
        var returnLineId = Guid.NewGuid();
        var original = new[]
        {
            new AccountTransaction
            {
                LineNumber = 2, AccountId = Guid.NewGuid(), CreditAmount = 595m,
                Notes = $"InventoryReturnVoucherLineId={returnLineId:N}"
            }
        };
        var reversal = new[]
        {
            new FinancePostingLineDto
                { LineNumber = 2, AccountId = original[0].AccountId, DebitAmount = 594m, TransactionTag = "Reversal" }
        };

        var action = () => InventoryIssueFinanceAssetPostingService.PreserveFixedAssetReversalLineage(
            reversal, original, new[] { new KeyValuePair<Guid, decimal>(returnLineId, 595m) }, "Acceptance reversal");

        action.Should().Throw<InventoryIssueAccountingControlException>()
            .Which.Code.Should().Be("INV_RETURN_ASSET_REVERSAL_LINEAGE_MISSING");
    }
}
