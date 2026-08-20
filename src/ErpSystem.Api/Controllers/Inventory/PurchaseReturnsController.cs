using System.Security.Claims;
using ErpSystem.Core.DTOs.Inventory;
using ErpSystem.Core.Interfaces.Inventory;
using ErpSystem.Core.Services.Inventory;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Inventory;

/// <summary>
/// Physical supplier-return lifecycle for accepted and stock-updated GRNs.
/// It deliberately does not create a second Finance/AP debit-note record.
/// </summary>
[ApiController]
[Route("api/inventory/supplier-returns")]
[Authorize(Policy = "InternalOnly")]
public sealed class PurchaseReturnsController : ControllerBase
{
    private readonly IPurchaseReturnService _returns;
    private readonly IGoodsReceiptNoteService _grns;
    private readonly ILogger<PurchaseReturnsController> _logger;

    public PurchaseReturnsController(
        IPurchaseReturnService returns,
        IGoodsReceiptNoteService grns,
        ILogger<PurchaseReturnsController> logger)
    {
        _returns = returns;
        _grns = grns;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<PurchaseReturnDto>>> GetAll([FromQuery] DateTime? fromDate = null, [FromQuery] DateTime? toDate = null)
        => await ExecuteAsync(() => _returns.GetAllAsync(fromDate, toDate));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<PurchaseReturnDetailDto>> Get(Guid id)
        => await ExecuteAsync(async () => await _returns.GetByIdAsync(id) ?? throw new InventorySupplierReturnException(
            "INV_SUPPLIER_RETURN_NOT_FOUND", "The supplier return was not found.", StatusCodes.Status404NotFound));

    [HttpGet("source-grns")]
    public async Task<ActionResult<IEnumerable<GoodsReceiptNoteDetailDto>>> GetSourceGrns()
    {
        try
        {
            var values = await _grns.GetAllAsync();
            var sourceIds = values
                .Where(value => value.Status == ErpSystem.Core.Enums.GRNStatus.StockUpdated)
                .Select(value => value.Id)
                .ToList();
            var sourceDetails = await Task.WhenAll(sourceIds.Select(id => _grns.GetByIdAsync(id)));
            return Ok(sourceDetails.Where(value => value is not null).Select(value => value!));
        }
        catch (Exception ex)
        {
            return ProblemFrom(ex, "INV_SUPPLIER_RETURN_SOURCE_FAILED", "Supplier-return source GRNs could not be loaded.");
        }
    }

    [HttpPost]
    public async Task<ActionResult<PurchaseReturnDto>> Create([FromBody] CreatePurchaseReturnDto dto)
    {
        try
        {
            var value = await _returns.CreateAsync(dto, CurrentUserId());
            return CreatedAtAction(nameof(Get), new { id = value.Id }, value);
        }
        catch (Exception ex)
        {
            return ProblemFrom(ex, "INV_SUPPLIER_RETURN_CREATE_FAILED", "The supplier return could not be created.");
        }
    }

    [HttpPost("{id:guid}/submit")]
    public Task<ActionResult> Submit(Guid id) => MutationAsync(
        () => _returns.SubmitForApprovalAsync(id, CurrentUserId()), "Supplier return submitted for independent approval.");

    [HttpPost("{id:guid}/approve")]
    public Task<ActionResult> Approve(Guid id) => MutationAsync(
        () => _returns.ApproveAsync(id, CurrentUserId()), "Supplier return approved. Stock is unchanged until dispatch.");

    [HttpPost("{id:guid}/reject")]
    public Task<ActionResult> Reject(Guid id, [FromBody] SupplierReturnReasonRequest request) => MutationAsync(
        () => _returns.RejectAsync(id, request?.Reason ?? string.Empty, CurrentUserId()), "Supplier return rejected.");

    [HttpPost("{id:guid}/ship")]
    public Task<ActionResult> Ship(Guid id, [FromBody] SupplierReturnShipmentRequest? request = null) => MutationAsync(
        () => _returns.ShipAsync(id, CurrentUserId(), request?.TrackingNumber), "Supplier return dispatched and stock was reduced.");

    [HttpPost("{id:guid}/credit-note")]
    public Task<ActionResult> RecordCredit(Guid id, [FromBody] SupplierReturnCreditRequest request) => MutationAsync(
        () => _returns.RecordCreditNoteAsync(id, request?.CreditNoteNumber ?? string.Empty, request?.Amount ?? 0m, CurrentUserId()),
        "Supplier credit recorded. Finance/AP should complete any debit-note and ledger processing.");

    [HttpPost("{id:guid}/cancel")]
    public Task<ActionResult> Cancel(Guid id, [FromBody] SupplierReturnReasonRequest request) => MutationAsync(
        () => _returns.CancelAsync(id, request?.Reason ?? string.Empty, CurrentUserId()), "Supplier return cancelled.");

    private async Task<ActionResult<T>> ExecuteAsync<T>(Func<Task<T>> action)
    {
        try { return Ok(await action()); }
        catch (Exception ex) { return ProblemFrom<T>(ex, "INV_SUPPLIER_RETURN_READ_FAILED", "Supplier returns could not be loaded."); }
    }

    private async Task<ActionResult> MutationAsync(Func<Task<bool>> action, string success)
    {
        try
        {
            await action();
            return Ok(new { message = success });
        }
        catch (Exception ex)
        {
            return ProblemFrom(ex, "INV_SUPPLIER_RETURN_MUTATION_FAILED", "The supplier-return action could not be completed.");
        }
    }

    private ActionResult ProblemFrom(Exception exception, string fallbackCode, string fallbackDetail)
    {
        if (exception is InventorySupplierReturnException controlled)
            return StatusCode(controlled.StatusCode, Problem(controlled.Code, controlled.Message, controlled.StatusCode));
        _logger.LogError(exception, "Supplier return request failed. Correlation {CorrelationId}", HttpContext.TraceIdentifier);
        return StatusCode(StatusCodes.Status500InternalServerError, Problem(fallbackCode, fallbackDetail, StatusCodes.Status500InternalServerError));
    }

    private ActionResult<T> ProblemFrom<T>(Exception exception, string fallbackCode, string fallbackDetail)
    {
        return new ActionResult<T>(ProblemFrom(exception, fallbackCode, fallbackDetail));
    }

    private ProblemDetails Problem(string code, string detail, int status) => new()
    {
        Type = $"https://tdc.gov.gh/problems/{code.ToLowerInvariant()}", Title = "Supplier return request failed",
        Status = status, Detail = detail, Instance = HttpContext.Request.Path
    }.Also(problem => problem.Extensions["code"] = code).Also(problem => problem.Extensions["correlationId"] = HttpContext.TraceIdentifier);

    private Guid CurrentUserId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.TryParse(raw, out var id) ? id : throw new InventorySupplierReturnException(
            "INV_SUPPLIER_RETURN_AUTH_REQUIRED", "An authenticated internal user is required.", StatusCodes.Status401Unauthorized);
    }
}

public sealed class SupplierReturnReasonRequest { public string? Reason { get; set; } }
public sealed class SupplierReturnShipmentRequest { public string? TrackingNumber { get; set; } }
public sealed class SupplierReturnCreditRequest { public string? CreditNoteNumber { get; set; } public decimal Amount { get; set; } }

internal static class PurchaseReturnProblemDetailsExtensions
{
    public static T Also<T>(this T value, Action<T> action) { action(value); return value; }
}
