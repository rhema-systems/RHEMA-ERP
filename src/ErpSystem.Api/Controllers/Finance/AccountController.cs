using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Interfaces.Finance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Finance
{
    [Authorize]
    [ApiController]
    [Route("api/finance/accounts")]
    public class AccountController : ControllerBase
    {
        private readonly IAccountService _accountService;

        public AccountController(IAccountService accountService)
        {
            _accountService = accountService;
        }

        #region Multi-Currency Management

        /// <summary>
        /// Add a currency to an account
        /// </summary>
        [HttpPost("{accountId}/currencies")]
        public async Task<ActionResult<CurrencyLinkDto>> AddCurrencyLink(Guid accountId, [FromBody] AddCurrencyLinkDto dto)
        {
            if (accountId != dto.AccountId)
                return BadRequest("Account ID mismatch");

            try
            {
                var result = await _accountService.AddCurrencyLinkAsync(dto);
                return CreatedAtAction(nameof(GetAccountCurrencyLinks), new { accountId }, result);
            }
            catch (ArgumentException ex)
            {
                return NotFound(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        /// <summary>
        /// Remove a currency from an account (with transaction history protection)
        /// </summary>
        [HttpDelete("{accountId}/currencies/{currencyCode}")]
        public async Task<ActionResult<CurrencyLinkRemovalResultDto>> RemoveCurrencyLink(
            Guid accountId, 
            string currencyCode,
            [FromQuery] bool forceRemove = false,
            [FromQuery] string? reason = null)
        {
            try
            {
                var dto = new RemoveCurrencyLinkDto
                {
                    AccountId = accountId,
                    CurrencyCode = currencyCode,
                    ForceRemove = forceRemove,
                    Reason = reason
                };

                var result = await _accountService.RemoveCurrencyLinkAsync(dto);
                return Ok(result);
            }
            catch (ArgumentException ex)
            {
                return NotFound(ex.Message);
            }
        }

        /// <summary>
        /// Inactivate a currency link (soft delete)
        /// </summary>
        [HttpPatch("{accountId}/currencies/{currencyCode}/inactivate")]
        public async Task<ActionResult<CurrencyLinkDto>> InactivateCurrencyLink(Guid accountId, string currencyCode)
        {
            try
            {
                var result = await _accountService.InactivateCurrencyLinkAsync(accountId, currencyCode);
                return Ok(result);
            }
            catch (ArgumentException ex)
            {
                return NotFound(ex.Message);
            }
        }

        /// <summary>
        /// Get all currency links for an account
        /// </summary>
        [HttpGet("{accountId}/currencies")]
        public async Task<ActionResult<List<CurrencyLinkDto>>> GetAccountCurrencyLinks(
            Guid accountId,
            [FromQuery] bool includeInactive = false)
        {
            try
            {
                var result = await _accountService.GetAccountCurrencyLinksAsync(accountId, includeInactive);
                return Ok(result);
            }
            catch (ArgumentException ex)
            {
                return NotFound(ex.Message);
            }
        }

        #endregion
    }
}
