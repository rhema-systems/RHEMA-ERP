using ErpSystem.Core.DTOs.AR;
using ErpSystem.Core.Interfaces.Finance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

/// <summary>
/// HR's narrow read door onto Finance's customers (lane 8, slice 6) — the <c>HrSuppliersController</c>
/// precedent. A consultant client is billed as a Finance customer; this is what the client screen's
/// picker reads, because Sales' own customer routes are Sales-gated. Read-only: HR never creates
/// or edits a customer.
/// </summary>
[ApiController]
[Route("api/hr/customers")]
[Authorize(Policy = "InternalOnly")]
public class HrCustomersController : ControllerBase
{
    private readonly ICustomerService _customers;

    public HrCustomersController(ICustomerService customers) => _customers = customers;

    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<HrCustomerOption>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<HrCustomerOption>>> Search(
        [FromQuery] string? search = null,
        [FromQuery] int take = 50,
        [FromQuery] bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        var capped = take < 1 ? 50 : Math.Min(take, 200);
        var page = await _customers.GetAllAsync(new CustomerQueryDto
        {
            SearchTerm = string.IsNullOrWhiteSpace(search) ? null : search.Trim(),
            IsActive = includeInactive ? null : true,
            PageNumber = 1,
            PageSize = capped,
            SortBy = "CustomerName"
        }, cancellationToken);
        return Ok(page.Items.Select(Project).OrderBy(c => c.Name).ToList());
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(HrCustomerOption), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<HrCustomerOption>> GetById(Guid id, CancellationToken cancellationToken = default)
    {
        var customer = await _customers.GetByIdAsync(id, cancellationToken);
        return customer is null
            ? NotFound(new { message = $"No customer was found with ID '{id}'." })
            : Ok(Project(customer));
    }

    private static HrCustomerOption Project(CustomerDto c) => new(c.Id, c.CustomerCode, c.CustomerName, c.IsActive, c.CurrencyCode);
}

public record HrCustomerOption(Guid Id, string Code, string Name, bool IsActive, string CurrencyCode);
