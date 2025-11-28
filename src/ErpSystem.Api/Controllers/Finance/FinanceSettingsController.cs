using Microsoft.AspNetCore.Mvc;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Interfaces;
using System;
using System.Threading.Tasks;

namespace ErpSystem.Api.Controllers.Finance
{
    [ApiController]
    [Route("api/finance")]
    public class FinanceSettingsController : ControllerBase
    {
        private readonly IFinanceSettingsService _financeSettingsService;
        private readonly ICurrentUserService _currentUserService;

        public FinanceSettingsController(
            IFinanceSettingsService financeSettingsService,
            ICurrentUserService currentUserService)
        {
            _financeSettingsService = financeSettingsService;
            _currentUserService = currentUserService;
        }

        /// <summary>
        /// Get finance settings for current tenant
        /// </summary>
        [HttpGet("settings")]
        public async Task<IActionResult> GetSettings()
        {
            try
            {
                var settings = await _financeSettingsService.GetSettingsAsync();
                return Ok(settings);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Update finance settings
        /// </summary>
        [HttpPut("settings")]
        public async Task<IActionResult> UpdateSettings([FromBody] UpdateFinanceSettingsDto dto)
        {
            try
            {
                var settings = await _financeSettingsService.UpdateSettingsAsync(dto);
                return Ok(settings);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while updating settings", details = ex.Message });
            }
        }

        /// <summary>
        /// Check if COA type can be changed (no accounts exist)
        /// </summary>
        [HttpGet("settings/can-change-coa-type")]
        public async Task<IActionResult> CanChangeCOAType()
        {
            try
            {
                var canChange = await _financeSettingsService.CanChangeCOATypeAsync();
                return Ok(new { canChange });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
    }
}
