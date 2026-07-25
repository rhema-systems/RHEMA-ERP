using ErpSystem.Core.Interfaces.Finance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Finance
{
    [Authorize]
    [ApiController]
    [Route("api/finance/accounting-books")]
    public class AccountingBooksController : ControllerBase
    {
        private readonly IAccountingBookService _accountingBookService;

        public AccountingBooksController(IAccountingBookService accountingBookService)
        {
            _accountingBookService = accountingBookService;
        }

        [HttpGet]
        public async Task<IActionResult> GetBooks([FromQuery] bool includeInactive = false, CancellationToken cancellationToken = default)
        {
            var books = await _accountingBookService.GetBooksAsync(includeInactive, cancellationToken);
            return Ok(books);
        }
    }
}
