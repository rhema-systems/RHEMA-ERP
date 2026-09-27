using ErpSystem.Core.DTOs.Sales;
using ErpSystem.Core.Interfaces.Sales;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Sales;

[ApiController, Authorize, Route("api/sales/orders/{orderId:guid}/invoice")]
public sealed class SalesOrderInvoiceController(ISalesOrderInvoiceService service) : ControllerBase
{
    [HttpGet]
    public Task<IActionResult> Get(Guid orderId, CancellationToken ct) => Run(() => service.GetAsync(orderId, ct));
    [HttpPost]
    public Task<IActionResult> Generate(Guid orderId, [FromBody] GenerateSalesOrderInvoiceRequest request, CancellationToken ct) =>
        Run(() => service.GenerateAsync(orderId, request, ct));
    [HttpPost("submit")]
    public Task<IActionResult> Submit(Guid orderId, CancellationToken ct) => Run(() => service.SubmitAsync(orderId, ct));
    [HttpPost("post")]
    public Task<IActionResult> Post(Guid orderId, CancellationToken ct) => Run(() => service.PostAsync(orderId, ct));
    [HttpGet("distribution")]
    public Task<IActionResult> Distribution(Guid orderId, CancellationToken ct) => Run(() => service.GetDistributionAsync(orderId, ct));

    private async Task<IActionResult> Run<T>(Func<Task<T>> action)
    {
        try { return Ok(await action()); }
        catch (UnauthorizedAccessException ex) { return Problem(statusCode: 403, detail: ex.Message, title: "Sales invoice access denied"); }
        catch (KeyNotFoundException ex) { return Problem(statusCode: 404, detail: ex.Message); }
        catch (InvalidOperationException ex) { return Problem(statusCode: 409, detail: ex.Message, title: "Sales invoice validation"); }
    }
}
