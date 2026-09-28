using System.Globalization;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Core.Enums;
using ErpSystem.Api.Services.Finance;
using ErpSystem.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ErpSystem.Shared;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Controllers.Finance;

/// <summary>
/// Imported bank statements and their lines. Statements feed the bank reconciliation flow
/// (BankReconciliationController), which matches lines against posted cash transactions.
/// </summary>
[ApiController]
[Authorize]
[Route("api/finance/bank-statements")]
public class BankStatementsController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IFinanceAccessScopeService _financeAccessScopeService;

    public BankStatementsController(
        ApplicationDbContext context,
        ICurrentUserService currentUserService,
        IFinanceAccessScopeService financeAccessScopeService)
    {
        _context = context;
        _currentUserService = currentUserService;
        _financeAccessScopeService = financeAccessScopeService;
    }

    private Guid TenantId => _currentUserService.GetRequiredFinanceTenantId();

    [HttpGet]
    [Authorize(Policy = FinancePermissions.ViewFinance)]
    public async Task<ActionResult<IReadOnlyList<BankStatementDto>>> GetStatements([FromQuery] Guid? bankAccountId = null)
    {
        var tenantId = TenantId;
        var query = _context.Set<BankStatement>()
            .AsNoTracking()
            .Where(s => s.TenantId == tenantId && !s.IsDeleted);

        var permittedIds = await _financeAccessScopeService.GetPermittedBankAccountIdsAsync(
            FinanceAccessLevel.Read);
        if (permittedIds != null)
            query = query.Where(s => permittedIds.Contains(s.BankAccountId));

        if (bankAccountId.HasValue)
        {
            if (permittedIds != null && !permittedIds.Contains(bankAccountId.Value))
                return Forbid();
            query = query.Where(s => s.BankAccountId == bankAccountId.Value);
        }

        var statements = await query
            .OrderByDescending(s => s.StatementDate)
            .ToListAsync();

        return Ok(statements.Select(ToDto).ToList());
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = FinancePermissions.ViewFinance)]
    public async Task<ActionResult<BankStatementDto>> GetStatementById(Guid id)
    {
        var tenantId = TenantId;
        var statement = await _context.Set<BankStatement>()
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == id && s.TenantId == tenantId && !s.IsDeleted);

        if (statement != null)
            await _financeAccessScopeService.EnsureBankAccountAccessAsync(
                statement.BankAccountId,
                FinanceAccessLevel.Read);

        return statement == null ? NotFound() : Ok(ToDto(statement));
    }

    [HttpGet("{id:guid}/lines")]
    [Authorize(Policy = FinancePermissions.ViewFinance)]
    public async Task<ActionResult<IReadOnlyList<BankStatementLineDto>>> GetStatementLines(Guid id)
    {
        var tenantId = TenantId;
        var statement = await _context.Set<BankStatement>()
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == id && s.TenantId == tenantId && !s.IsDeleted);
        if (statement == null)
        {
            return NotFound();
        }
        await _financeAccessScopeService.EnsureBankAccountAccessAsync(
            statement.BankAccountId,
            FinanceAccessLevel.Read);

        var lines = await _context.Set<BankStatementLine>()
            .AsNoTracking()
            .Where(l => l.BankStatementId == id && l.TenantId == tenantId && !l.IsDeleted)
            .OrderBy(l => l.TransactionDate)
            .ThenBy(l => l.CreatedAt)
            .ToListAsync();

        return Ok(lines.Select(ToLineDto).ToList());
    }

    /// <summary>
    /// Imports a CSV bank statement for a bank account.
    /// </summary>
    /// <remarks>
    /// Expected CSV columns (header row required, in order):
    /// Date, Description, Reference, Debit, Credit, Balance.
    /// Dates parse as yyyy-MM-dd or dd/MM/yyyy; amounts are invariant-culture decimals.
    /// </remarks>
    [HttpPost("import")]
    [Authorize(Policy = FinancePermissions.PerformBankReconciliation)]
    public async Task<ActionResult<BankStatementDto>> ImportStatement(
        [FromForm] IFormFile file,
        [FromForm] Guid bankAccountId,
        [FromForm] string? statementNumber = null,
        [FromForm] string? notes = null)
    {
        var tenantId = TenantId;
        await _financeAccessScopeService.EnsureBankAccountAccessAsync(
            bankAccountId,
            FinanceAccessLevel.Operate);
        if (file == null || file.Length == 0)
        {
            return BadRequest(new { message = "A statement file is required." });
        }

        var bankAccountExists = await _context.BankAccounts
            .AnyAsync(a => a.Id == bankAccountId && a.TenantId == tenantId && !a.IsDeleted);
        if (!bankAccountExists)
        {
            return BadRequest(new { message = "Bank account not found for the current tenant." });
        }

        List<BankStatementLine> lines;
        try
        {
            lines = await ParseCsvLinesAsync(file, tenantId);
        }
        catch (FormatException ex)
        {
            return BadRequest(new { message = ex.Message });
        }

        if (lines.Count == 0)
        {
            return BadRequest(new { message = "The statement file contains no transaction lines." });
        }

        var firstLine = lines[0];
        var statement = new BankStatement
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            BankAccountId = bankAccountId,
            StatementDate = lines.Max(l => l.TransactionDate),
            StatementNumber = string.IsNullOrWhiteSpace(statementNumber) ? null : statementNumber.Trim(),
            // Opening balance is the first line's balance before its own movement.
            OpeningBalance = firstLine.Balance - firstLine.CreditAmount + firstLine.DebitAmount,
            ClosingBalance = lines[^1].Balance,
            TotalDebits = lines.Sum(l => l.DebitAmount),
            TotalCredits = lines.Sum(l => l.CreditAmount),
            ImportedAt = DateTime.UtcNow,
            ImportedBy = Guid.TryParse(_currentUserService.UserId, out var userId) ? userId : null,
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
            CreatedAt = DateTime.UtcNow,
            CreatedBy = _currentUserService.UserName ?? "system"
        };

        foreach (var line in lines)
        {
            line.BankStatementId = statement.Id;
        }

        _context.Set<BankStatement>().Add(statement);
        _context.Set<BankStatementLine>().AddRange(lines);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetStatementById), new { id = statement.Id }, ToDto(statement));
    }

    private async Task<List<BankStatementLine>> ParseCsvLinesAsync(IFormFile file, Guid tenantId)
    {
        var lines = new List<BankStatementLine>();
        using var reader = new StreamReader(file.OpenReadStream());

        var isFirst = true;
        var lineNumber = 0;
        string? raw;
        while ((raw = await reader.ReadLineAsync()) != null)
        {
            lineNumber++;
            if (string.IsNullOrWhiteSpace(raw))
            {
                continue;
            }

            if (isFirst)
            {
                // Header row is mandatory and skipped.
                isFirst = false;
                continue;
            }

            var fields = SplitCsvLine(raw);
            if (fields.Count < 6)
            {
                throw new FormatException($"Line {lineNumber}: expected 6 columns (Date, Description, Reference, Debit, Credit, Balance).");
            }

            lines.Add(new BankStatementLine
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                TransactionDate = ParseDate(fields[0], lineNumber),
                Description = string.IsNullOrWhiteSpace(fields[1]) ? null : fields[1].Trim(),
                ReferenceNumber = string.IsNullOrWhiteSpace(fields[2]) ? null : fields[2].Trim(),
                DebitAmount = ParseAmount(fields[3], lineNumber, "Debit"),
                CreditAmount = ParseAmount(fields[4], lineNumber, "Credit"),
                Balance = ParseAmount(fields[5], lineNumber, "Balance"),
                IsMatched = false,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = _currentUserService.UserName ?? "system"
            });
        }

        return lines.OrderBy(l => l.TransactionDate).ToList();
    }

    private static List<string> SplitCsvLine(string line)
    {
        var fields = new List<string>();
        var current = new System.Text.StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < line.Length; i++)
        {
            var ch = line[i];
            if (ch == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (ch == ',' && !inQuotes)
            {
                fields.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(ch);
            }
        }

        fields.Add(current.ToString());
        return fields;
    }

    private static DateTime ParseDate(string value, int lineNumber)
    {
        var trimmed = value.Trim();
        if (DateTime.TryParseExact(trimmed, new[] { "yyyy-MM-dd", "dd/MM/yyyy", "yyyy-MM-ddTHH:mm:ss" },
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            return date.Date;
        }

        throw new FormatException($"Line {lineNumber}: '{trimmed}' is not a valid date (expected yyyy-MM-dd or dd/MM/yyyy).");
    }

    private static decimal ParseAmount(string value, int lineNumber, string column)
    {
        var trimmed = value.Trim().Replace(",", string.Empty);
        if (trimmed.Length == 0)
        {
            return 0m;
        }

        if (decimal.TryParse(trimmed, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount))
        {
            return amount;
        }

        throw new FormatException($"Line {lineNumber}: '{value}' is not a valid {column} amount.");
    }

    private static BankStatementDto ToDto(BankStatement statement)
    {
        return new BankStatementDto
        {
            Id = statement.Id,
            BankAccountId = statement.BankAccountId,
            StatementDate = statement.StatementDate,
            StatementNumber = statement.StatementNumber,
            OpeningBalance = statement.OpeningBalance,
            ClosingBalance = statement.ClosingBalance,
            TotalDebits = statement.TotalDebits,
            TotalCredits = statement.TotalCredits,
            ImportedAt = statement.ImportedAt,
            ImportedBy = statement.ImportedBy,
            Notes = statement.Notes
        };
    }

    private static BankStatementLineDto ToLineDto(BankStatementLine line)
    {
        return new BankStatementLineDto
        {
            Id = line.Id,
            BankStatementId = line.BankStatementId,
            TransactionDate = line.TransactionDate,
            ValueDate = line.ValueDate,
            Description = line.Description,
            ReferenceNumber = line.ReferenceNumber,
            DebitAmount = line.DebitAmount,
            CreditAmount = line.CreditAmount,
            Balance = line.Balance,
            IsMatched = line.IsMatched,
            MatchedTransactionId = line.MatchedTransactionId,
            ReconciliationMatchId = line.ReconciliationMatchId
        };
    }
}
