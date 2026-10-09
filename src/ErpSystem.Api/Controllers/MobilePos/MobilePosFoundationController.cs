using ErpSystem.Api.Services.MobilePos;
using ErpSystem.Core.DTOs.MobilePos;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.DTOs.AR;
using ErpSystem.Core.Enums;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.MobilePos;

[ApiController]
[Authorize]
[Route("api/mobile-pos/v1")]
public sealed class MobilePosRuntimeController : ControllerBase
{
    private readonly IMobilePosFoundationService _service;
    private readonly IAuthorizationService _authorization;
    private readonly IMobilePosFinanceReadService _financeReads;
    private readonly IMobilePosCheckoutReadService _checkout;
    private readonly IMobilePosSaleService _sales;
    private readonly IMobilePosSyncService _sync;
    private readonly IMobilePosReceiptService _receipts;
    private readonly IMobilePosTillSessionService _tillSessions;

    public MobilePosRuntimeController(
        IMobilePosFoundationService service,
        IAuthorizationService authorization,
        IMobilePosFinanceReadService financeReads,
        IMobilePosCheckoutReadService checkout,
        IMobilePosSaleService sales,
        IMobilePosSyncService sync,
        IMobilePosReceiptService receipts,
        IMobilePosTillSessionService tillSessions)
    {
        _service = service;
        _authorization = authorization;
        _financeReads = financeReads;
        _checkout = checkout;
        _sales = sales;
        _sync = sync;
        _receipts = receipts;
        _tillSessions = tillSessions;
    }

    [HttpPost("devices/enrollment-requests")]
    [Authorize(Policy = MobilePosPermissions.EnrollDevice)]
    public async Task<ActionResult<MobilePosDeviceDto>> RequestEnrollment(
        [FromBody] MobilePosDeviceEnrollmentRequestDto dto,
        CancellationToken cancellationToken)
        => Ok(await _service.RequestEnrollmentAsync(dto, cancellationToken));

    [HttpGet("bootstrap")]
    [Authorize(Policy = MobilePosPermissions.Access)]
    public async Task<ActionResult<MobilePosBootstrapDto>> Bootstrap(
        [FromQuery] string installationId,
        CancellationToken cancellationToken)
        => Ok(await _service.GetBootstrapAsync(installationId, cancellationToken));

    [HttpPost("heartbeat")]
    [Authorize(Policy = MobilePosPermissions.Access)]
    public async Task<ActionResult<MobilePosDeviceDto>> Heartbeat(
        [FromBody] MobilePosHeartbeatDto dto,
        CancellationToken cancellationToken)
        => Ok(await _service.RecordHeartbeatAsync(dto, cancellationToken));

    [HttpGet("till-sessions/current")]
    [Authorize(Policy = MobilePosPermissions.OperateTill)]
    [Authorize(Policy = FinancePermissions.OperateCashTills)]
    public async Task<ActionResult<CashierTillSessionDto>> GetCurrentTillSession(
        [FromQuery] string installationId,
        CancellationToken cancellationToken)
    {
        var result = await _tillSessions.GetCurrentAsync(installationId, cancellationToken);
        return result == null ? NoContent() : Ok(result);
    }

    [HttpPost("till-sessions/open")]
    [Authorize(Policy = MobilePosPermissions.OperateTill)]
    [Authorize(Policy = FinancePermissions.OperateCashTills)]
    public async Task<ActionResult<CashierTillSessionDto>> OpenTillSession(
        [FromBody] MobilePosOpenTillSessionRequestDto dto,
        CancellationToken cancellationToken)
        => Ok(await _tillSessions.OpenAsync(dto, cancellationToken));

    [HttpGet("till-sessions/{sessionId:guid}/reconciliation")]
    [Authorize(Policy = MobilePosPermissions.OperateTill)]
    [Authorize(Policy = FinancePermissions.OperateCashTills)]
    public async Task<ActionResult<MobilePosTillReconciliationDto>> GetTillReconciliation(
        Guid sessionId,
        [FromQuery] string installationId,
        CancellationToken cancellationToken)
        => Ok(await _tillSessions.GetReconciliationAsync(sessionId, installationId, cancellationToken));

    [HttpPost("offline-grants")]
    [Authorize(Policy = MobilePosPermissions.UseOffline)]
    [Authorize(Policy = MobilePosPermissions.OperateTill)]
    public async Task<ActionResult<MobilePosOfflineGrantDto>> IssueOfflineGrant(
        [FromBody] MobilePosOfflineGrantRequestDto dto,
        CancellationToken cancellationToken)
    {
        var candidatePermissions = new[]
        {
            MobilePosPermissions.CreateInvoice,
            MobilePosPermissions.PostInvoice,
            MobilePosPermissions.CollectPayment,
            MobilePosPermissions.ApplyDiscount,
            MobilePosPermissions.CreateReturn,
            MobilePosPermissions.CreateReversal,
            MobilePosPermissions.CloseTill,
            FinancePermissions.CreateArInvoices,
            FinancePermissions.ApprovePostArInvoices,
            FinancePermissions.ReceiveCustomerPayments
        };
        var authorizedPermissions = new List<string>();
        foreach (var permission in candidatePermissions)
        {
            if ((await _authorization.AuthorizeAsync(User, permission)).Succeeded)
                authorizedPermissions.Add(permission);
        }

        return Ok(await _service.IssueOfflineGrantAsync(dto, authorizedPermissions, cancellationToken));
    }

    [HttpGet("customers/search")]
    [Authorize(Policy = MobilePosPermissions.ViewCustomer)]
    public async Task<ActionResult<IReadOnlyList<MobilePosCustomerSearchResultDto>>> SearchCustomers(
        [FromQuery] string installationId,
        [FromQuery] string? q,
        [FromQuery] int limit = 20,
        CancellationToken cancellationToken = default)
        => Ok(await _financeReads.SearchCustomersAsync(installationId, q, limit, cancellationToken));

    [HttpGet("customers/changes")]
    [Authorize(Policy = MobilePosPermissions.UseOffline)]
    [Authorize(Policy = MobilePosPermissions.ViewCustomer)]
    public async Task<ActionResult<MobilePosCustomerChangePageDto>> GetCustomerChanges(
        [FromQuery] string installationId,
        [FromQuery] DateTime? sinceUtc,
        [FromQuery] string? cursor,
        [FromQuery] int limit = 250,
        CancellationToken cancellationToken = default)
        => Ok(await _financeReads.GetCustomerChangesAsync(
            installationId, sinceUtc, cursor, limit, cancellationToken));

    [HttpGet("customers/{businessPartnerId:guid}/outstanding-invoices")]
    [Authorize(Policy = MobilePosPermissions.ViewCustomer)]
    public async Task<ActionResult<IReadOnlyList<OutstandingInvoiceDto>>> GetOutstandingInvoices(
        Guid businessPartnerId,
        [FromQuery] Guid businessPartnerRoleId,
        [FromQuery] string installationId,
        CancellationToken cancellationToken)
        => Ok(await _financeReads.GetOutstandingInvoicesAsync(
            installationId, businessPartnerId, businessPartnerRoleId, cancellationToken));

    [HttpGet("catalogue/search")]
    [Authorize(Policy = MobilePosPermissions.OperateTill)]
    [Authorize(Policy = MobilePosPermissions.CreateInvoice)]
    public async Task<ActionResult<IReadOnlyList<MobilePosCatalogueItemDto>>> SearchCatalogue(
        [FromQuery] string installationId,
        [FromQuery(Name = "q")] string? search,
        [FromQuery] int limit = 30,
        CancellationToken cancellationToken = default)
        => Ok(await _checkout.SearchCatalogueAsync(
            installationId, search, limit, cancellationToken));

    [HttpGet("catalogue/changes")]
    [Authorize(Policy = MobilePosPermissions.UseOffline)]
    [Authorize(Policy = MobilePosPermissions.OperateTill)]
    [Authorize(Policy = MobilePosPermissions.CreateInvoice)]
    public async Task<ActionResult<MobilePosCatalogueChangePageDto>> GetCatalogueChanges(
        [FromQuery] string installationId,
        [FromQuery] DateTime? sinceUtc,
        [FromQuery] string? cursor,
        [FromQuery] int limit = 250,
        CancellationToken cancellationToken = default)
        => Ok(await _checkout.GetCatalogueChangesAsync(
            installationId, sinceUtc, cursor, limit, cancellationToken));

    [HttpGet("bank-accounts")]
    [Authorize(Policy = MobilePosPermissions.OperateTill)]
    [Authorize(Policy = MobilePosPermissions.CollectPayment)]
    [Authorize(Policy = FinancePermissions.ReceiveCustomerPayments)]
    public async Task<ActionResult<IReadOnlyList<MobilePosBankAccountOptionDto>>> GetEligibleBankAccounts(
        [FromQuery] string installationId,
        CancellationToken cancellationToken)
        => Ok(await _checkout.GetEligibleBankAccountsAsync(installationId, cancellationToken));

    [HttpPost("sales/preview")]
    [Authorize(Policy = MobilePosPermissions.OperateTill)]
    [Authorize(Policy = MobilePosPermissions.CreateInvoice)]
    [Authorize(Policy = FinancePermissions.CreateArInvoices)]
    public async Task<ActionResult<MobilePosSalePreviewDto>> PreviewSale(
        [FromBody] MobilePosSalePreviewRequestDto dto,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _checkout.PreviewAsync(dto, cancellationToken));
        }
        catch (MobilePosCommandRejectedException exception)
        {
            return RejectedSale(exception, "Mobile POS sale preview rejected");
        }
    }

    [HttpPost("sales")]
    [Authorize(Policy = MobilePosPermissions.OperateTill)]
    [Authorize(Policy = MobilePosPermissions.CreateInvoice)]
    [Authorize(Policy = MobilePosPermissions.PostInvoice)]
    [Authorize(Policy = MobilePosPermissions.CollectPayment)]
    [Authorize(Policy = FinancePermissions.CreateArInvoices)]
    [Authorize(Policy = FinancePermissions.ApprovePostArInvoices)]
    [Authorize(Policy = FinancePermissions.ReceiveCustomerPayments)]
    public async Task<ActionResult<MobilePosSaleResultDto>> CompleteSale(
        [FromBody] MobilePosCompleteSaleRequestDto dto,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _sales.CompleteAsync(dto, cancellationToken));
        }
        catch (MobilePosMutationConflictException exception)
        {
            return Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Mobile POS request conflict",
                Detail = exception.Message
            });
        }
        catch (MobilePosCommandRejectedException exception)
        {
            return RejectedSale(exception, "Mobile POS sale rejected");
        }
    }

    [HttpPost("sync/push")]
    [Authorize(Policy = MobilePosPermissions.UseOffline)]
    [Authorize(Policy = MobilePosPermissions.OperateTill)]
    public async Task<ActionResult<MobilePosSyncPushResultDto>> PushOfflineCommand(
        [FromBody] MobilePosSyncPushRequestDto dto,
        CancellationToken cancellationToken)
        => Ok(await _sync.PushAsync(dto, cancellationToken));

    [HttpGet("receipts/{id:guid}")]
    [Authorize(Policy = MobilePosPermissions.Access)]
    public async Task<ActionResult<MobilePosReceiptDto>> GetReceipt(
        Guid id,
        [FromQuery] string installationId,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _receipts.GetAsync(id, installationId, cancellationToken));
        }
        catch (MobilePosCommandRejectedException exception)
        {
            return RejectedSale(exception, "Mobile POS receipt unavailable");
        }
    }

    [HttpPost("receipts/{id:guid}/reprint-events")]
    [Authorize(Policy = MobilePosPermissions.Access)]
    [Authorize(Policy = MobilePosPermissions.ReprintReceipt)]
    public async Task<ActionResult<MobilePosReceiptDto>> RecordReceiptReprint(
        Guid id,
        [FromBody] MobilePosReceiptReprintRequestDto dto,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _receipts.RecordReprintAsync(id, dto, cancellationToken));
        }
        catch (MobilePosCommandRejectedException exception)
        {
            return RejectedSale(exception, "Mobile POS receipt reprint rejected");
        }
    }

    private BadRequestObjectResult RejectedSale(
        MobilePosCommandRejectedException exception,
        string title)
    {
        var details = new ProblemDetails
        {
            Status = StatusCodes.Status400BadRequest,
            Title = title,
            Detail = exception.Message
        };
        details.Extensions["code"] = exception.Code;
        details.Extensions["replayed"] = exception.IsReplay;
        return BadRequest(details);
    }
}

[ApiController]
[Authorize]
[Route("api/administration/mobile-pos/v1")]
public sealed class MobilePosAdministrationController : ControllerBase
{
    private readonly IMobilePosFoundationService _service;

    public MobilePosAdministrationController(IMobilePosFoundationService service) => _service = service;

    [HttpGet("references")]
    [Authorize(Policy = MobilePosPermissions.ViewStore)]
    public async Task<ActionResult<MobilePosAdministrationReferencesDto>> GetReferences(
        CancellationToken cancellationToken)
        => Ok(await _service.GetAdministrationReferencesAsync(cancellationToken));

    [HttpGet("stores")]
    [Authorize(Policy = MobilePosPermissions.ViewStore)]
    public async Task<ActionResult<IReadOnlyList<MobilePosStoreDto>>> GetStores(CancellationToken cancellationToken)
        => Ok(await _service.GetStoresAsync(cancellationToken));

    [HttpPost("stores")]
    [Authorize(Policy = MobilePosPermissions.ManageStore)]
    public async Task<ActionResult<MobilePosStoreDto>> CreateStore(
        [FromBody] MobilePosStoreUpsertDto dto,
        CancellationToken cancellationToken)
    {
        var result = await _service.SaveStoreAsync(null, dto, cancellationToken);
        return CreatedAtAction(nameof(GetStores), new { id = result.Id }, result);
    }

    [HttpPut("stores/{id:guid}")]
    [Authorize(Policy = MobilePosPermissions.ManageStore)]
    public async Task<ActionResult<MobilePosStoreDto>> UpdateStore(
        Guid id,
        [FromBody] MobilePosStoreUpsertDto dto,
        CancellationToken cancellationToken)
        => Ok(await _service.SaveStoreAsync(id, dto, cancellationToken));

    [HttpGet("offline-policies")]
    [Authorize(Policy = MobilePosPermissions.ViewStore)]
    public async Task<ActionResult<IReadOnlyList<MobilePosOfflinePolicyDto>>> GetOfflinePolicies(
        CancellationToken cancellationToken)
        => Ok(await _service.GetOfflinePoliciesAsync(cancellationToken));

    [HttpPost("offline-policies")]
    [Authorize(Policy = MobilePosPermissions.ManageStore)]
    public async Task<ActionResult<MobilePosOfflinePolicyDto>> CreateOfflinePolicy(
        [FromBody] MobilePosOfflinePolicyUpsertDto dto,
        CancellationToken cancellationToken)
        => Ok(await _service.SaveOfflinePolicyAsync(null, dto, cancellationToken));

    [HttpPut("offline-policies/{id:guid}")]
    [Authorize(Policy = MobilePosPermissions.ManageStore)]
    public async Task<ActionResult<MobilePosOfflinePolicyDto>> UpdateOfflinePolicy(
        Guid id,
        [FromBody] MobilePosOfflinePolicyUpsertDto dto,
        CancellationToken cancellationToken)
        => Ok(await _service.SaveOfflinePolicyAsync(id, dto, cancellationToken));

    [HttpGet("tills")]
    [Authorize(Policy = MobilePosPermissions.ViewStore)]
    public async Task<ActionResult<IReadOnlyList<MobilePosTillDto>>> GetTills(
        [FromQuery] Guid? storeId,
        CancellationToken cancellationToken)
        => Ok(await _service.GetTillsAsync(storeId, cancellationToken));

    [HttpPost("tills")]
    [Authorize(Policy = MobilePosPermissions.ManageStore)]
    public async Task<ActionResult<MobilePosTillDto>> CreateTill(
        [FromBody] MobilePosTillUpsertDto dto,
        CancellationToken cancellationToken)
        => Ok(await _service.SaveTillAsync(null, dto, cancellationToken));

    [HttpPut("tills/{id:guid}")]
    [Authorize(Policy = MobilePosPermissions.ManageStore)]
    public async Task<ActionResult<MobilePosTillDto>> UpdateTill(
        Guid id,
        [FromBody] MobilePosTillUpsertDto dto,
        CancellationToken cancellationToken)
        => Ok(await _service.SaveTillAsync(id, dto, cancellationToken));

    [HttpPost("user-store-assignments")]
    [Authorize(Policy = MobilePosPermissions.ManageStore)]
    public async Task<IActionResult> SaveUserStoreAssignment(
        [FromBody] MobilePosUserStoreAssignmentUpsertDto dto,
        CancellationToken cancellationToken)
    {
        await _service.SaveUserStoreAssignmentAsync(dto, cancellationToken);
        return NoContent();
    }

    [HttpGet("devices")]
    [Authorize(Policy = MobilePosPermissions.ApproveDevice)]
    public async Task<ActionResult<IReadOnlyList<MobilePosDeviceDto>>> GetDevices(
        [FromQuery] MobilePosDeviceStatus? status,
        CancellationToken cancellationToken)
        => Ok(await _service.GetDevicesAsync(status, cancellationToken));

    [HttpPost("devices/{id:guid}/approve")]
    [Authorize(Policy = MobilePosPermissions.ApproveDevice)]
    public async Task<ActionResult<MobilePosDeviceDto>> ApproveDevice(
        Guid id,
        [FromBody] MobilePosDeviceApprovalDto dto,
        CancellationToken cancellationToken)
        => Ok(await _service.ApproveDeviceAsync(id, dto, cancellationToken));

    [HttpPost("devices/{id:guid}/revoke")]
    [Authorize(Policy = MobilePosPermissions.ApproveDevice)]
    public async Task<ActionResult<MobilePosDeviceDto>> RevokeDevice(
        Guid id,
        [FromBody] MobilePosDeviceStatusChangeDto dto,
        CancellationToken cancellationToken)
        => Ok(await _service.RevokeDeviceAsync(id, dto, cancellationToken));
}
