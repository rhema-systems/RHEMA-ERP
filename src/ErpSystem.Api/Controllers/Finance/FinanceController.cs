using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Interfaces;

namespace ErpSystem.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class FinanceController : ControllerBase
    {
        private readonly IGeneralLedgerService _glService;

        public FinanceController(IGeneralLedgerService glService)
        {
            _glService = glService;
        }

        [HttpPost("accounts")]
        public async Task<IActionResult> CreateAccount([FromBody] AccountCreateDto accountDto)
        {
            try
            {
                var account = await _glService.CreateSegmentedAccountAsync(accountDto);
                return CreatedAtAction(nameof(GetAccount), new { id = account.Id }, account);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpGet("accounts/{id}")]
        public async Task<IActionResult> GetAccount(Guid id)
        {
            var account = await _glService.GetAccountByIdAsync(id);
            if (account == null) return NotFound();
            return Ok(account);
        }

        [HttpPost("journal-entries")]
        public async Task<IActionResult> PostJournalEntry([FromBody] CreateJournalEntryDto entryDto)
        {
            try
            {
                var journalEntry = await _glService.PostJournalEntryAsync(entryDto);
                return Ok(journalEntry);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpGet("accounts/{id}/balance")]
        public async Task<IActionResult> GetAccountBalance(Guid id, [FromQuery] string currencyCode = "GHS")
        {
            var balance = await _glService.GetAccountBalanceAsync(id, currencyCode);
            return Ok(balance);
        }

        [HttpPost("revaluation")]
        public async Task<IActionResult> RunRevaluation([FromBody] RevaluationRequestDto requestDto)
        {
            try
            {
                var journalEntry = await _glService.RunCurrencyRevaluationAsync(requestDto);
                return Ok(journalEntry);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpGet("statements/balance-sheet")]
        public async Task<IActionResult> GetBalanceSheet([FromQuery] BalanceSheetRequestDto request)
        {
            try
            {
                var balanceSheet = await _glService.GenerateBalanceSheetAsync(request);
                return Ok(balanceSheet);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpGet("statements/income-statement")]
        public async Task<IActionResult> GetIncomeStatement([FromQuery] IncomeStatementRequestDto request)
        {
            try
            {
                var incomeStatement = await _glService.GenerateIncomeStatementAsync(request);
                return Ok(incomeStatement);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpGet("statements/trial-balance")]
        public async Task<IActionResult> GetTrialBalance([FromQuery] TrialBalanceRequestDto request)
        {
            try
            {
                var trialBalance = await _glService.GenerateTrialBalanceAsync(request);
                return Ok(trialBalance);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpGet("statements/cash-flow")]
        public async Task<IActionResult> GetCashFlowStatement([FromQuery] CashFlowStatementRequestDto request)
        {
            try
            {
                var cashFlowStatement = await _glService.GenerateCashFlowStatementAsync(request);
                return Ok(cashFlowStatement);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("periods/{id}/validate-close")]
        public async Task<IActionResult> ValidatePeriodClose(Guid id)
        {
            try
            {
                var validation = await _glService.ValidatePeriodCloseAsync(id);
                return Ok(validation);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("periods/{id}/close")]
        public async Task<IActionResult> ClosePeriod(Guid id, [FromBody] PeriodCloseRequestDto request)
        {
            try
            {
                request.FiscalPeriodId = id; // Ensure ID from route is used
                var result = await _glService.CloseFiscalPeriodAsync(request);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("periods/{id}/reopen")]
        public async Task<IActionResult> ReopenPeriod(Guid id, [FromBody] PeriodReopenRequestDto request)
        {
            try
            {
                request.FiscalPeriodId = id;
                var result = await _glService.ReopenFiscalPeriodAsync(request);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("periods/{id}/lock")]
        public async Task<IActionResult> LockPeriod(Guid id, [FromBody] PeriodLockRequestDto request)
        {
            try
            {
                await _glService.LockFiscalPeriodAsync(id, request.LockReason);
                return Ok(new { message = "Period locked successfully" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("periods/{id}/unlock")]
        public async Task<IActionResult> UnlockPeriod(Guid id, [FromBody] string unlockReason)
        {
            try
            {
                await _glService.UnlockFiscalPeriodAsync(id, unlockReason);
                return Ok(new { message = "Period unlocked successfully" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("fiscal-years/{id}/close")]
        public async Task<IActionResult> CloseFiscalYear(Guid id, [FromBody] YearEndCloseRequestDto request)
        {
            try
            {
                request.FiscalYearId = id;
                var result = await _glService.CloseFiscalYearAsync(request);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
    }
}
