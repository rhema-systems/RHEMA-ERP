using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Api.Services.Finance;
using ErpSystem.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Controllers.Finance;

/// <summary>
/// Cheque register: issuance and status lifecycle tracking.
/// </summary>
/// <remarks>
/// Cheque status changes never touch the GL. The financial movement lives on the linked cash
/// transaction, which posts through the finance posting engine; this register tracks the
/// physical instrument (issued, presented, cleared, bounced, cancelled, stale).
/// </remarks>
[ApiController]
[Authorize]
[Route("api/finance/cheques")]
public class ChequesController : ControllerBase
{
    private static readonly IReadOnlyDictionary<ChequeStatus, ChequeStatus[]> LegalTransitions =
        new Dictionary<ChequeStatus, ChequeStatus[]>
        {
            [ChequeStatus.Issued] = new[] { ChequeStatus.Presented, ChequeStatus.Cleared, ChequeStatus.Cancelled, ChequeStatus.Stale },
            [ChequeStatus.Presented] = new[] { ChequeStatus.Cleared, ChequeStatus.Bounced, ChequeStatus.Cancelled },
            [ChequeStatus.Bounced] = new[] { ChequeStatus.Presented, ChequeStatus.Cancelled },
            [ChequeStatus.Stale] = new[] { ChequeStatus.Cancelled },
            [ChequeStatus.Cleared] = Array.Empty<ChequeStatus>(),
            [ChequeStatus.Cancelled] = Array.Empty<ChequeStatus>()
        };

    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public ChequesController(ApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    private Guid TenantId => _currentUserService.GetRequiredFinanceTenantId();

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ChequeDto>>> GetCheques(
        [FromQuery] Guid? bankAccountId = null,
        [FromQuery] ChequeStatus? status = null)
    {
        var tenantId = TenantId;
        var query = _context.Set<Cheque>()
            .AsNoTracking()
            .Include(c => c.BankAccount)
            .Where(c => c.TenantId == tenantId && !c.IsDeleted);

        if (bankAccountId.HasValue)
        {
            query = query.Where(c => c.BankAccountId == bankAccountId.Value);
        }

        if (status.HasValue)
        {
            query = query.Where(c => c.Status == status.Value);
        }

        var cheques = await query
            .OrderByDescending(c => c.IssueDate)
            .ThenBy(c => c.ChequeNumber)
            .ToListAsync();

        return Ok(cheques.Select(ToDto).ToList());
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ChequeDto>> GetChequeById(Guid id)
    {
        var cheque = await FindChequeAsync(id, track: false);
        return cheque == null ? NotFound() : Ok(ToDto(cheque));
    }

    [HttpPost]
    public async Task<ActionResult<ChequeDto>> Create([FromBody] CreateChequeDto dto)
    {
        var tenantId = TenantId;
        if (string.IsNullOrWhiteSpace(dto.ChequeNumber))
        {
            return BadRequest(new { message = "A cheque number is required." });
        }

        if (dto.Amount <= 0)
        {
            return BadRequest(new { message = "The cheque amount must be greater than zero." });
        }

        var bankAccount = await _context.BankAccounts
            .FirstOrDefaultAsync(a => a.Id == dto.BankAccountId && a.TenantId == tenantId && !a.IsDeleted);
        if (bankAccount == null)
        {
            return BadRequest(new { message = "Bank account not found for the current tenant." });
        }

        var chequeNumber = dto.ChequeNumber.Trim();
        var duplicate = await _context.Set<Cheque>()
            .AnyAsync(c => c.TenantId == tenantId
                && c.BankAccountId == dto.BankAccountId
                && c.ChequeNumber == chequeNumber
                && !c.IsDeleted);
        if (duplicate)
        {
            return BadRequest(new { message = $"Cheque '{chequeNumber}' already exists for this bank account." });
        }

        var cheque = new Cheque
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ChequeNumber = chequeNumber,
            BankAccountId = dto.BankAccountId,
            IssueDate = dto.IssueDate == default ? DateTime.UtcNow.Date : dto.IssueDate.Date,
            PayeeName = string.IsNullOrWhiteSpace(dto.PayeeName) ? null : dto.PayeeName.Trim(),
            Amount = dto.Amount,
            Currency = string.IsNullOrWhiteSpace(dto.Currency) ? "GHS" : dto.Currency.Trim().ToUpperInvariant(),
            Status = ChequeStatus.Issued,
            Memo = string.IsNullOrWhiteSpace(dto.Memo) ? null : dto.Memo.Trim(),
            CreatedAt = DateTime.UtcNow,
            CreatedBy = _currentUserService.UserName ?? "system"
        };

        _context.Set<Cheque>().Add(cheque);
        await _context.SaveChangesAsync();

        cheque.BankAccount = bankAccount;
        return CreatedAtAction(nameof(GetChequeById), new { id = cheque.Id }, ToDto(cheque));
    }

    [HttpPut("{id:guid}/status")]
    public async Task<ActionResult<ChequeDto>> UpdateStatus(Guid id, [FromBody] UpdateChequeStatusDto dto)
    {
        var cheque = await FindChequeAsync(id, track: true);
        if (cheque == null)
        {
            return NotFound();
        }

        var error = ApplyStatusTransition(cheque, dto.Status, dto.StatusDate, dto.StatusReason);
        if (error != null)
        {
            return BadRequest(new { message = error });
        }

        await _context.SaveChangesAsync();
        return Ok(ToDto(cheque));
    }

    [HttpPut("{id:guid}/void")]
    public async Task<ActionResult<ChequeDto>> Void(Guid id, [FromBody] VoidChequeDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Reason))
        {
            return BadRequest(new { message = "A reason is required to void a cheque." });
        }

        var cheque = await FindChequeAsync(id, track: true);
        if (cheque == null)
        {
            return NotFound();
        }

        var error = ApplyStatusTransition(cheque, ChequeStatus.Cancelled, null, dto.Reason);
        if (error != null)
        {
            return BadRequest(new { message = error });
        }

        await _context.SaveChangesAsync();
        return Ok(ToDto(cheque));
    }

    private string? ApplyStatusTransition(Cheque cheque, ChequeStatus target, DateTime? statusDate, string? reason)
    {
        if (cheque.Status == target)
        {
            return $"The cheque is already {target}.";
        }

        if (!LegalTransitions.TryGetValue(cheque.Status, out var allowed) || !allowed.Contains(target))
        {
            return $"A {cheque.Status} cheque cannot move to {target}.";
        }

        if ((target == ChequeStatus.Cancelled || target == ChequeStatus.Bounced) && string.IsNullOrWhiteSpace(reason))
        {
            return $"A reason is required to mark a cheque {target}.";
        }

        var effectiveDate = (statusDate ?? DateTime.UtcNow).Date;
        cheque.Status = target;
        cheque.StatusReason = string.IsNullOrWhiteSpace(reason) ? cheque.StatusReason : reason.Trim();

        switch (target)
        {
            case ChequeStatus.Presented:
                cheque.PresentedDate = effectiveDate;
                break;
            case ChequeStatus.Cleared:
                cheque.ClearedDate = effectiveDate;
                cheque.PresentedDate ??= effectiveDate;
                break;
            case ChequeStatus.Cancelled:
                cheque.CancelledDate = effectiveDate;
                break;
        }

        cheque.UpdatedAt = DateTime.UtcNow;
        cheque.UpdatedBy = _currentUserService.UserName ?? "system";
        return null;
    }

    private async Task<Cheque?> FindChequeAsync(Guid id, bool track)
    {
        var tenantId = TenantId;
        var query = _context.Set<Cheque>()
            .Include(c => c.BankAccount)
            .Where(c => c.Id == id && c.TenantId == tenantId && !c.IsDeleted);

        if (!track)
        {
            query = query.AsNoTracking();
        }

        return await query.FirstOrDefaultAsync();
    }

    private static ChequeDto ToDto(Cheque cheque)
    {
        return new ChequeDto
        {
            Id = cheque.Id,
            ChequeNumber = cheque.ChequeNumber,
            BankAccountId = cheque.BankAccountId,
            BankAccountName = cheque.BankAccount?.AccountName ?? string.Empty,
            IssueDate = cheque.IssueDate,
            PayeeName = cheque.PayeeName,
            Amount = cheque.Amount,
            Currency = cheque.Currency,
            Status = cheque.Status,
            PresentedDate = cheque.PresentedDate,
            ClearedDate = cheque.ClearedDate,
            CancelledDate = cheque.CancelledDate,
            Memo = cheque.Memo,
            CashTransactionId = cheque.CashTransactionId,
            StatusReason = cheque.StatusReason,
            CreatedAt = cheque.CreatedAt
        };
    }
}
