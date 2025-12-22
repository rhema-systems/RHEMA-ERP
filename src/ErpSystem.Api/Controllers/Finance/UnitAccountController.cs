using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Interfaces.Finance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Finance
{
    /// <summary>
    /// Controller for managing Unit Accounts.
    /// Unit Accounts are hierarchical accounts for tracking non-financial quantities.
    /// </summary>
    [Authorize]
    [ApiController]
    [Route("api/finance/unit-accounts")]
    public class UnitAccountController : ControllerBase
    {
        private readonly IUnitAccountService _unitAccountService;

        public UnitAccountController(IUnitAccountService unitAccountService)
        {
            _unitAccountService = unitAccountService;
        }

        /// <summary>
        /// Retrieves all unit accounts.
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<List<UnitAccountDto>>> GetUnitAccounts()
        {
            try
            {
                var accounts = await _unitAccountService.GetAllAsync();
                return Ok(accounts);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        /// <summary>
        /// Retrieves unit accounts in a hierarchical structure.
        /// </summary>
        [HttpGet("hierarchy")]
        public async Task<ActionResult<List<UnitAccountHierarchyDto>>> GetAccountHierarchy()
        {
            try
            {
                var hierarchy = await _unitAccountService.GetHierarchyAsync();
                return Ok(hierarchy);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        /// <summary>
        /// Retrieves unit accounts filtered by unit type.
        /// </summary>
        [HttpGet("by-type/{unitTypeId:guid}")]
        public async Task<ActionResult<List<UnitAccountDto>>> GetByUnitType(Guid unitTypeId)
        {
            try
            {
                var accounts = await _unitAccountService.GetByUnitTypeAsync(unitTypeId);
                return Ok(accounts);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        /// <summary>
        /// Retrieves only posting accounts (leaf-level accounts that accept journal entries).
        /// </summary>
        [HttpGet("posting")]
        public async Task<ActionResult<List<UnitAccountDto>>> GetPostingAccounts()
        {
            try
            {
                var accounts = await _unitAccountService.GetPostingAccountsAsync();
                return Ok(accounts);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        /// <summary>
        /// Retrieves a specific unit account with detail and balance history.
        /// </summary>
        [HttpGet("{id:guid}")]
        public async Task<ActionResult<UnitAccountDetailDto>> GetUnitAccountById(Guid id)
        {
            try
            {
                var account = await _unitAccountService.GetByIdAsync(id);
                if (account == null)
                    return NotFound($"Unit account with ID {id} not found");

                return Ok(account);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        /// <summary>
        /// Retrieves a unit account by its account number.
        /// </summary>
        [HttpGet("number/{accountNumber}")]
        public async Task<ActionResult<UnitAccountDto>> GetByAccountNumber(string accountNumber)
        {
            try
            {
                var account = await _unitAccountService.GetByAccountNumberAsync(accountNumber);
                if (account == null)
                    return NotFound($"Unit account with number {accountNumber} not found");

                return Ok(account);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        /// <summary>
        /// Creates a new unit account.
        /// </summary>
        [HttpPost]
        public async Task<ActionResult<UnitAccountDto>> CreateUnitAccount([FromBody] CreateUnitAccountDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                var account = await _unitAccountService.CreateAsync(dto);
                return CreatedAtAction(nameof(GetUnitAccountById), new { id = account.Id }, account);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        /// <summary>
        /// Updates an existing unit account.
        /// </summary>
        [HttpPut("{id:guid}")]
        public async Task<ActionResult<UnitAccountDto>> UpdateUnitAccount(Guid id, [FromBody] UpdateUnitAccountDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                var account = await _unitAccountService.UpdateAsync(id, dto);
                return Ok(account);
            }
            catch (ArgumentException ex)
            {
                return NotFound(ex.Message);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        /// <summary>
        /// Deletes a unit account.
        /// </summary>
        [HttpDelete("{id:guid}")]
        public async Task<ActionResult> DeleteUnitAccount(Guid id)
        {
            try
            {
                await _unitAccountService.DeleteAsync(id);
                return NoContent();
            }
            catch (ArgumentException ex)
            {
                return NotFound(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        /// <summary>
        /// Activates a unit account.
        /// </summary>
        [HttpPatch("{id:guid}/activate")]
        public async Task<ActionResult> ActivateUnitAccount(Guid id)
        {
            try
            {
                await _unitAccountService.ActivateAsync(id);
                return NoContent();
            }
            catch (ArgumentException ex)
            {
                return NotFound(ex.Message);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        /// <summary>
        /// Deactivates a unit account.
        /// </summary>
        [HttpPatch("{id:guid}/deactivate")]
        public async Task<ActionResult> DeactivateUnitAccount(Guid id)
        {
            try
            {
                await _unitAccountService.DeactivateAsync(id);
                return NoContent();
            }
            catch (ArgumentException ex)
            {
                return NotFound(ex.Message);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        /// <summary>
        /// Gets the current balance for a unit account.
        /// </summary>
        [HttpGet("{id:guid}/balance")]
        public async Task<ActionResult<decimal>> GetCurrentBalance(Guid id)
        {
            try
            {
                var balance = await _unitAccountService.GetCurrentBalanceAsync(id);
                return Ok(balance);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        /// <summary>
        /// Gets balance history for a unit account.
        /// </summary>
        [HttpGet("{id:guid}/balances")]
        public async Task<ActionResult<List<UnitAccountBalanceDto>>> GetBalances(Guid id, [FromQuery] Guid? fiscalYearId = null)
        {
            try
            {
                var balances = await _unitAccountService.GetBalancesAsync(id, fiscalYearId);
                return Ok(balances);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        /// <summary>
        /// Recalculates balances for a unit account based on posted journal entries.
        /// </summary>
        [HttpPost("{id:guid}/recalculate")]
        public async Task<ActionResult> RecalculateBalances(Guid id)
        {
            try
            {
                await _unitAccountService.RecalculateBalancesAsync(id);
                return NoContent();
            }
            catch (ArgumentException ex)
            {
                return NotFound(ex.Message);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }
    }
}
