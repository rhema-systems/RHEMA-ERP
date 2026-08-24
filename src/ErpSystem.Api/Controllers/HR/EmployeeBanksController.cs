using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

/// <summary>
/// Manages bank (financial institution) and bank branch reference data.
/// </summary>
[ApiController]
[Authorize(Policy = "InternalOnly")]
public class EmployeeBanksController : ControllerBase
{
    private readonly IEmployeeBankService _bankService;
    private readonly IEmployeeBankBranchService _branchService;
    private readonly ICurrentUserService _currentUserService;

    public EmployeeBanksController(
        IEmployeeBankService bankService,
        IEmployeeBankBranchService branchService,
        ICurrentUserService currentUserService)
    {
        _bankService = bankService;
        _branchService = branchService;
        _currentUserService = currentUserService;
    }

    private bool TryGetTenantId(out Guid tenantId)
    {
        tenantId = _currentUserService.TenantId ?? Guid.Empty;
        return tenantId != Guid.Empty;
    }

    // ════════════════════════════════════════════════════════════════════════
    // BANKS
    // ════════════════════════════════════════════════════════════════════════

    /// <summary>Get all banks for the current tenant.</summary>
    [HttpGet("api/hr/banks")]
    [ProducesResponseType(typeof(IReadOnlyList<EmployeeBankDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<EmployeeBankDto>>> GetAll(CancellationToken cancellationToken)
    {
        if (!TryGetTenantId(out var tenantId))
            return BadRequest(new { message = "Tenant ID is required." });

        var results = await _bankService.GetAllAsync(tenantId, cancellationToken);
        return Ok(results);
    }

    /// <summary>Get active banks only.</summary>
    [HttpGet("api/hr/banks/active")]
    [ProducesResponseType(typeof(IReadOnlyList<EmployeeBankDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<EmployeeBankDto>>> GetActive(CancellationToken cancellationToken)
    {
        if (!TryGetTenantId(out var tenantId))
            return BadRequest(new { message = "Tenant ID is required." });

        var results = await _bankService.GetActiveAsync(tenantId, cancellationToken);
        return Ok(results);
    }

    /// <summary>Get a bank by ID.</summary>
    [HttpGet("api/hr/banks/{id:guid}")]
    [ProducesResponseType(typeof(EmployeeBankDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeeBankDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        if (!TryGetTenantId(out var tenantId))
            return BadRequest(new { message = "Tenant ID is required." });

        try
        {
            var result = await _bankService.GetByIdAsync(tenantId, id, cancellationToken);
            return Ok(result);
        }
        catch (ArgumentException)
        {
            return NotFound(new { message = "Bank not found." });
        }
    }

    /// <summary>Get a bank by short code.</summary>
    [HttpGet("api/hr/banks/code/{code}")]
    [ProducesResponseType(typeof(EmployeeBankDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeeBankDto>> GetByCode(string code, CancellationToken cancellationToken)
    {
        if (!TryGetTenantId(out var tenantId))
            return BadRequest(new { message = "Tenant ID is required." });

        if (string.IsNullOrWhiteSpace(code))
            return BadRequest(new { message = "Code is required." });

        var result = await _bankService.GetByCodeAsync(tenantId, code, cancellationToken);
        return result is null ? NotFound(new { message = "Bank not found." }) : Ok(result);
    }

    /// <summary>Create a new bank.</summary>
    [HttpPost("api/hr/banks")]
    [ProducesResponseType(typeof(EmployeeBankDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<EmployeeBankDto>> Create([FromBody] CreateEmployeeBankDto dto, CancellationToken cancellationToken)
    {
        if (!TryGetTenantId(out var tenantId))
            return BadRequest(new { message = "Tenant ID is required." });

        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            var created = await _bankService.CreateAsync(tenantId, dto, cancellationToken);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>Update an existing bank.</summary>
    [HttpPut("api/hr/banks/{id:guid}")]
    [ProducesResponseType(typeof(EmployeeBankDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<EmployeeBankDto>> Update(Guid id, [FromBody] UpdateEmployeeBankDto dto, CancellationToken cancellationToken)
    {
        if (!TryGetTenantId(out var tenantId))
            return BadRequest(new { message = "Tenant ID is required." });

        if (id != dto.Id)
            return BadRequest(new { message = "ID mismatch." });

        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            var updated = await _bankService.UpdateAsync(tenantId, dto, cancellationToken);
            return Ok(updated);
        }
        catch (ArgumentException)
        {
            return NotFound(new { message = "Bank not found." });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    /// <summary>Activate a bank.</summary>
    [HttpPatch("api/hr/banks/{id:guid}/activate")]
    [ProducesResponseType(typeof(EmployeeBankDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeeBankDto>> Activate(Guid id, CancellationToken cancellationToken)
    {
        if (!TryGetTenantId(out var tenantId))
            return BadRequest(new { message = "Tenant ID is required." });

        try
        {
            var result = await _bankService.ActivateAsync(tenantId, id, cancellationToken);
            return Ok(result);
        }
        catch (ArgumentException)
        {
            return NotFound(new { message = "Bank not found." });
        }
    }

    /// <summary>Deactivate a bank.</summary>
    [HttpPatch("api/hr/banks/{id:guid}/deactivate")]
    [ProducesResponseType(typeof(EmployeeBankDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeeBankDto>> Deactivate(Guid id, CancellationToken cancellationToken)
    {
        if (!TryGetTenantId(out var tenantId))
            return BadRequest(new { message = "Tenant ID is required." });

        try
        {
            var result = await _bankService.DeactivateAsync(tenantId, id, cancellationToken);
            return Ok(result);
        }
        catch (ArgumentException)
        {
            return NotFound(new { message = "Bank not found." });
        }
    }

    /// <summary>Delete a bank (soft delete). Blocked if referenced by employee bank details.</summary>
    [HttpDelete("api/hr/banks/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        if (!TryGetTenantId(out var tenantId))
            return BadRequest(new { message = "Tenant ID is required." });

        try
        {
            await _bankService.DeleteAsync(tenantId, id, cancellationToken);
            return NoContent();
        }
        catch (ArgumentException)
        {
            return NotFound(new { message = "Bank not found." });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    // ════════════════════════════════════════════════════════════════════════
    // BANK BRANCHES  (nested under banks for create/list; standalone for CRUD)
    // ════════════════════════════════════════════════════════════════════════

    /// <summary>Get all branches for a bank.</summary>
    [HttpGet("api/hr/banks/{bankId:guid}/branches")]
    [ProducesResponseType(typeof(IReadOnlyList<EmployeeBankBranchDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<EmployeeBankBranchDto>>> GetBranches(Guid bankId, CancellationToken cancellationToken)
    {
        if (!TryGetTenantId(out var tenantId))
            return BadRequest(new { message = "Tenant ID is required." });

        try
        {
            var results = await _bankService.GetBranchesAsync(tenantId, bankId, cancellationToken);
            return Ok(results);
        }
        catch (ArgumentException)
        {
            return NotFound(new { message = "Bank not found." });
        }
    }

    /// <summary>Get active branches for a bank.</summary>
    [HttpGet("api/hr/banks/{bankId:guid}/branches/active")]
    [ProducesResponseType(typeof(IReadOnlyList<EmployeeBankBranchDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<EmployeeBankBranchDto>>> GetActiveBranches(Guid bankId, CancellationToken cancellationToken)
    {
        if (!TryGetTenantId(out var tenantId))
            return BadRequest(new { message = "Tenant ID is required." });

        var results = await _branchService.GetActiveByBankAsync(tenantId, bankId, cancellationToken);
        return Ok(results);
    }

    /// <summary>Create a branch under a specific bank.</summary>
    [HttpPost("api/hr/banks/{bankId:guid}/branches")]
    [ProducesResponseType(typeof(EmployeeBankBranchDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<EmployeeBankBranchDto>> CreateBranch(Guid bankId, [FromBody] CreateEmployeeBankBranchDto dto, CancellationToken cancellationToken)
    {
        if (!TryGetTenantId(out var tenantId))
            return BadRequest(new { message = "Tenant ID is required." });

        if (bankId != dto.BankId)
            return BadRequest(new { message = "BankId mismatch between route and body." });

        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            var created = await _branchService.CreateAsync(tenantId, dto, cancellationToken);
            return CreatedAtAction(nameof(GetBranchById), new { id = created.Id }, created);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    /// <summary>Get a branch by ID.</summary>
    [HttpGet("api/hr/bank-branches/{id:guid}")]
    [ProducesResponseType(typeof(EmployeeBankBranchDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeeBankBranchDto>> GetBranchById(Guid id, CancellationToken cancellationToken)
    {
        if (!TryGetTenantId(out var tenantId))
            return BadRequest(new { message = "Tenant ID is required." });

        try
        {
            var result = await _branchService.GetByIdAsync(tenantId, id, cancellationToken);
            return Ok(result);
        }
        catch (ArgumentException)
        {
            return NotFound(new { message = "Bank branch not found." });
        }
    }

    /// <summary>Update an existing branch.</summary>
    [HttpPut("api/hr/bank-branches/{id:guid}")]
    [ProducesResponseType(typeof(EmployeeBankBranchDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<EmployeeBankBranchDto>> UpdateBranch(Guid id, [FromBody] UpdateEmployeeBankBranchDto dto, CancellationToken cancellationToken)
    {
        if (!TryGetTenantId(out var tenantId))
            return BadRequest(new { message = "Tenant ID is required." });

        if (id != dto.Id)
            return BadRequest(new { message = "ID mismatch." });

        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            var updated = await _branchService.UpdateAsync(tenantId, dto, cancellationToken);
            return Ok(updated);
        }
        catch (ArgumentException)
        {
            return NotFound(new { message = "Bank branch not found." });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    /// <summary>Activate a branch.</summary>
    [HttpPatch("api/hr/bank-branches/{id:guid}/activate")]
    [ProducesResponseType(typeof(EmployeeBankBranchDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeeBankBranchDto>> ActivateBranch(Guid id, CancellationToken cancellationToken)
    {
        if (!TryGetTenantId(out var tenantId))
            return BadRequest(new { message = "Tenant ID is required." });

        try
        {
            var result = await _branchService.ActivateAsync(tenantId, id, cancellationToken);
            return Ok(result);
        }
        catch (ArgumentException)
        {
            return NotFound(new { message = "Bank branch not found." });
        }
    }

    /// <summary>Deactivate a branch.</summary>
    [HttpPatch("api/hr/bank-branches/{id:guid}/deactivate")]
    [ProducesResponseType(typeof(EmployeeBankBranchDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeeBankBranchDto>> DeactivateBranch(Guid id, CancellationToken cancellationToken)
    {
        if (!TryGetTenantId(out var tenantId))
            return BadRequest(new { message = "Tenant ID is required." });

        try
        {
            var result = await _branchService.DeactivateAsync(tenantId, id, cancellationToken);
            return Ok(result);
        }
        catch (ArgumentException)
        {
            return NotFound(new { message = "Bank branch not found." });
        }
    }

    /// <summary>Delete a branch (soft delete). Blocked if referenced by employee bank details.</summary>
    [HttpDelete("api/hr/bank-branches/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeleteBranch(Guid id, CancellationToken cancellationToken)
    {
        if (!TryGetTenantId(out var tenantId))
            return BadRequest(new { message = "Tenant ID is required." });

        try
        {
            await _branchService.DeleteAsync(tenantId, id, cancellationToken);
            return NoContent();
        }
        catch (ArgumentException)
        {
            return NotFound(new { message = "Bank branch not found." });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }
}
