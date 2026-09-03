using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Api.Services.Finance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Data;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Controllers.Finance;

[ApiController]
[Route("api/finance/migration-readiness/accounting-books")]
[Authorize]
public sealed class AccountingBookMigrationReadinessController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public AccountingBookMigrationReadinessController(ApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    [HttpGet]
    [Authorize(Policy = FinancePermissions.ViewFinance)]
    public async Task<ActionResult<AccountingBookMigrationReadinessDto>> Get(CancellationToken cancellationToken)
    {
        var tenantId = _currentUser.GetRequiredFinanceTenantId();
        var accounts = await _db.Accounts.AsNoTracking()
            .Where(item => item.TenantId == tenantId && !item.IsDeleted)
            .Select(item => new { item.Id, item.AccountCode, item.AccountType })
            .ToListAsync(cancellationToken);
        var mappings = await _db.AccountAccountingBooks.AsNoTracking()
            .Include(item => item.AccountingBook).Include(item => item.AccountClassification)
            .Where(item => item.TenantId == tenantId && !item.IsDeleted && item.IsEnabled)
            .ToListAsync(cancellationToken);
        var blockers = new List<AccountingBookMigrationBlockerDto>();
        foreach (var account in accounts.Where(account => mappings.All(mapping => mapping.AccountId != account.Id)))
            blockers.Add(Blocker("ACCOUNT_WITHOUT_BOOK", "Account has no enabled accounting-book assignment.", account.Id, account.AccountCode));
        foreach (var mapping in mappings)
        {
            var account = accounts.SingleOrDefault(item => item.Id == mapping.AccountId);
            var book = mapping.AccountingBook;
            var classification = mapping.AccountClassification;
            if (account == null || book == null || book.TenantId != tenantId)
            {
                blockers.Add(Blocker("INVALID_TENANT_LINEAGE", "Mapping has invalid tenant-owned account or book lineage.", mapping.AccountId, account?.AccountCode, mapping.AccountingBookId, book?.Code));
                continue;
            }
            if (!book.IsActive || !book.AllowsPosting)
                blockers.Add(Blocker("BOOK_UNAVAILABLE", "Enabled mapping targets an inactive or non-posting book.", account.Id, account.AccountCode, book.Id, book.Code));
            if (classification == null)
                blockers.Add(Blocker("UNCLASSIFIED_MAPPING", "Enabled mapping has no detailed classification.", account.Id, account.AccountCode, book.Id, book.Code));
            else if (classification.TenantId != tenantId || classification.AccountingBookId != book.Id
                || classification.Status != AccountClassificationStatus.Active || !classification.IsPostingClassification
                || classification.CoreAccountType != account.AccountType)
                blockers.Add(Blocker("INVALID_CLASSIFICATION", "Enabled mapping classification is inactive, non-posting or incompatible.", account.Id, account.AccountCode, book.Id, book.Code));
        }
        return Ok(new AccountingBookMigrationReadinessDto
        {
            IsReady = blockers.Count == 0,
            AccountCount = accounts.Count,
            EnabledMappingCount = mappings.Count,
            Blockers = blockers.OrderBy(item => item.AccountCode).ThenBy(item => item.AccountingBookCode).ToList()
        });
    }

    private static AccountingBookMigrationBlockerDto Blocker(string code, string message, Guid? accountId = null,
        string? accountCode = null, Guid? bookId = null, string? bookCode = null) => new()
    {
        Code = code, Message = message, AccountId = accountId, AccountCode = accountCode,
        AccountingBookId = bookId, AccountingBookCode = bookCode
    };
}
