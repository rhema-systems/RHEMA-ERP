using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FinancePaymentMethod = ErpSystem.Core.Entities.Finance.PaymentMethod;

namespace ErpSystem.Api.Controllers.Finance;

// PaymentMethod inherits BaseEntity (no TenantId), so payment methods are a shared lookup
// across tenants by data model. Tenant-scoping them requires an entity change plus a
// migration/backfill decision tracked outside this controller.
[ApiController]
[Authorize]
[Route("api/finance/payment-methods")]
public class PaymentMethodController : ControllerBase
{
    private static readonly IReadOnlyList<FinancePaymentMethod> DefaultPaymentMethods =
    [
        new()
        {
            Id = Guid.Parse("10000000-0000-0000-0000-000000000001"),
            Code = "CASH",
            Name = "Cash",
            Type = PaymentMethodType.Cash,
            Description = "Cash payment.",
            IsActive = true,
            RequiresBankAccount = false,
            RequiresReference = false
        },
        new()
        {
            Id = Guid.Parse("10000000-0000-0000-0000-000000000002"),
            Code = "CHQ",
            Name = "Cheque",
            Type = PaymentMethodType.Cheque,
            Description = "Cheque payment.",
            IsActive = true,
            RequiresBankAccount = true,
            RequiresReference = true
        },
        new()
        {
            Id = Guid.Parse("10000000-0000-0000-0000-000000000003"),
            Code = "EFT",
            Name = "Electronic Funds Transfer",
            Type = PaymentMethodType.EFT,
            Description = "Electronic bank transfer.",
            IsActive = true,
            RequiresBankAccount = true,
            RequiresReference = true
        },
        new()
        {
            Id = Guid.Parse("10000000-0000-0000-0000-000000000004"),
            Code = "MOMO",
            Name = "Mobile Money",
            Type = PaymentMethodType.MobileMoney,
            Description = "Mobile money payment.",
            IsActive = true,
            RequiresBankAccount = true,
            RequiresReference = true
        },
        new()
        {
            Id = Guid.Parse("10000000-0000-0000-0000-000000000005"),
            Code = "BANK",
            Name = "Bank Transfer",
            Type = PaymentMethodType.BankTransfer,
            Description = "Direct bank transfer.",
            IsActive = true,
            RequiresBankAccount = true,
            RequiresReference = true
        }
    ];

    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public PaymentMethodController(ApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<PaymentMethodDto>>> GetAll([FromQuery] bool? isActive = null)
    {
        await EnsureDefaultPaymentMethodsAsync();

        var query = _context.PaymentMethods
            .AsNoTracking()
            .Where(method => !method.IsDeleted);

        if (isActive.HasValue)
        {
            query = query.Where(method => method.IsActive == isActive.Value);
        }

        var methods = await query
            .OrderBy(method => method.Type)
            .ThenBy(method => method.Name)
            .ToListAsync();

        return Ok(methods.Select(ToDto).ToList());
    }

    [HttpPost]
    public async Task<ActionResult<PaymentMethodDto>> Create([FromBody] CreatePaymentMethodDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
        {
            return BadRequest(new { message = "Payment method name is required." });
        }

        var code = NormalizeCode(dto.Code);
        if (code != null && await CodeExistsAsync(code, excludeId: null))
        {
            return BadRequest(new { message = $"Payment method code '{code}' already exists." });
        }

        var method = new FinancePaymentMethod
        {
            Id = Guid.NewGuid(),
            Name = dto.Name.Trim(),
            Code = code,
            Type = dto.Type,
            Description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description.Trim(),
            IsActive = true,
            RequiresBankAccount = dto.RequiresBankAccount,
            RequiresReference = dto.RequiresReference,
            DefaultGLAccountId = dto.DefaultGLAccountId,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = _currentUserService.UserName ?? "system"
        };

        _context.PaymentMethods.Add(method);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetAll), new { id = method.Id }, ToDto(method));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<PaymentMethodDto>> Update(Guid id, [FromBody] CreatePaymentMethodDto dto)
    {
        var method = await _context.PaymentMethods
            .FirstOrDefaultAsync(item => item.Id == id && !item.IsDeleted);
        if (method == null)
        {
            return NotFound();
        }

        if (string.IsNullOrWhiteSpace(dto.Name))
        {
            return BadRequest(new { message = "Payment method name is required." });
        }

        var code = NormalizeCode(dto.Code);
        if (code != null && await CodeExistsAsync(code, excludeId: id))
        {
            return BadRequest(new { message = $"Payment method code '{code}' already exists." });
        }

        method.Name = dto.Name.Trim();
        method.Code = code;
        method.Type = dto.Type;
        method.Description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description.Trim();
        method.RequiresBankAccount = dto.RequiresBankAccount;
        method.RequiresReference = dto.RequiresReference;
        method.DefaultGLAccountId = dto.DefaultGLAccountId;
        method.UpdatedAt = DateTime.UtcNow;
        method.UpdatedBy = _currentUserService.UserName ?? "system";

        await _context.SaveChangesAsync();
        return Ok(ToDto(method));
    }

    private Task<bool> CodeExistsAsync(string code, Guid? excludeId)
    {
        return _context.PaymentMethods
            .AnyAsync(item => !item.IsDeleted &&
                              item.Code == code &&
                              (excludeId == null || item.Id != excludeId.Value));
    }

    private async Task EnsureDefaultPaymentMethodsAsync()
    {
        // Seed-on-first-read: two concurrent first requests can both observe an empty table.
        // The fixed seed ids make the loser fail on the primary key instead of inserting
        // duplicates, and that conflict is treated as "another request already seeded".
        var hasAnyMethod = await _context.PaymentMethods.AnyAsync(method => !method.IsDeleted);
        if (hasAnyMethod)
        {
            return;
        }

        _context.PaymentMethods.AddRange(DefaultPaymentMethods.Select(method => new FinancePaymentMethod
        {
            Id = method.Id,
            Code = method.Code,
            Name = method.Name,
            Type = method.Type,
            Description = method.Description,
            IsActive = method.IsActive,
            RequiresBankAccount = method.RequiresBankAccount,
            RequiresReference = method.RequiresReference,
            DefaultGLAccountId = method.DefaultGLAccountId,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "system"
        }));

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            _context.ChangeTracker.Clear();
        }
    }

    private static string? NormalizeCode(string? code)
    {
        return string.IsNullOrWhiteSpace(code) ? null : code.Trim().ToUpperInvariant();
    }

    private static PaymentMethodDto ToDto(FinancePaymentMethod method)
    {
        return new PaymentMethodDto
        {
            Id = method.Id,
            Name = method.Name,
            Code = method.Code,
            Type = method.Type,
            Description = method.Description,
            IsActive = method.IsActive,
            RequiresBankAccount = method.RequiresBankAccount,
            RequiresReference = method.RequiresReference,
            DefaultGLAccountId = method.DefaultGLAccountId
        };
    }
}
