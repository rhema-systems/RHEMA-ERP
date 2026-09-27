using ErpSystem.Core.Interfaces.Legal;
using ErpSystem.Core.Interfaces;
using ErpSystem.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Controllers.Legal;

[ApiController]
[Route("api/legal/procedures")]
[Authorize]
public sealed class LegalProceduresController : ControllerBase
{
    private readonly ILegalProcedureCatalogService _procedureCatalog;
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public LegalProceduresController(
        ILegalProcedureCatalogService procedureCatalog,
        ApplicationDbContext db,
        ICurrentUserService currentUser)
    {
        _procedureCatalog = procedureCatalog;
        _db = db;
        _currentUser = currentUser;
    }

    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboard(CancellationToken cancellationToken)
    {
        var allowedRoles = new[]
        {
            "admin", "Admin", "SystemAdmin", "SuperAdmin", "TenantAdmin",
            "Legal", "Legal Officer", "Legal Manager", "Head of Legal",
            "Legal Admin Assistant", "Secretary", "Registry", "Legal Registry"
        };
        if (!_currentUser.Roles.Any(role => allowedRoles.Contains(role, StringComparer.OrdinalIgnoreCase)))
        {
            return Forbid();
        }

        var tenantId = _currentUser.TenantId;
        if (!tenantId.HasValue || tenantId == Guid.Empty)
        {
            return Unauthorized(new { success = false, message = "A tenant context is required." });
        }

        var cases = await _db.ProcedureCases
            .AsNoTracking()
            .Include(item => item.Fields.Where(field => !field.IsDeleted))
            .Where(item => item.TenantId == tenantId.Value
                && item.Module == "Legal"
                && !item.IsDeleted)
            .OrderByDescending(item => item.UpdatedAt ?? item.CreatedAt)
            .Take(1000)
            .ToListAsync(cancellationToken);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var inThirtyDays = today.AddDays(30);
        var courtCases = cases.Where(item =>
            item.EntityType == "LegalCourtProcess"
            || item.EntityType == "LegalOtherCourtProcess").ToList();
        string? Value(ErpSystem.Core.Entities.Procedures.ProcedureCase item, string key)
            => item.Fields.FirstOrDefault(field =>
                string.Equals(field.Key, key, StringComparison.OrdinalIgnoreCase))?.Value;
        bool IsOpen(ErpSystem.Core.Entities.Procedures.ProcedureCase item)
            => !string.Equals(item.Status, "Completed", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(item.Status, "Closed", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(item.Status, "Cancelled", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(item.Status, "Canceled", StringComparison.OrdinalIgnoreCase);
        bool IsOutcome(ErpSystem.Core.Entities.Procedures.ProcedureCase item, string outcome)
            => string.Equals(Value(item, "courtOutcome"), outcome, StringComparison.OrdinalIgnoreCase);
        bool DateInRange(string? value, DateOnly start, DateOnly end)
            => DateOnly.TryParse(value, out var date) && date >= start && date <= end;
        bool DateBefore(string? value, DateOnly date)
            => DateOnly.TryParse(value, out var parsed) && parsed < date;

        return Ok(new
        {
            success = true,
            data = new
            {
                totalMatters = cases.Count,
                openMatters = cases.Count(IsOpen),
                completedMatters = cases.Count(item => !IsOpen(item)),
                propertyLinkedMatters = cases.Count(item => !string.IsNullOrWhiteSpace(Value(item, "sourceProcedureCaseId"))),
                activeCourtCases = courtCases.Count(IsOpen),
                courtPending = courtCases.Count(item => IsOpen(item) && (string.IsNullOrWhiteSpace(Value(item, "courtOutcome")) || IsOutcome(item, "Pending"))),
                courtWon = courtCases.Count(item => IsOutcome(item, "Won")),
                courtLost = courtCases.Count(item => IsOutcome(item, "Lost")),
                courtSettled = courtCases.Count(item => IsOutcome(item, "Settled")),
                courtWithdrawn = courtCases.Count(item => IsOutcome(item, "Withdrawn") || IsOutcome(item, "Struck out")),
                hearingsNext30Days = courtCases.Count(item => IsOpen(item) && DateInRange(Value(item, "nextHearingDate"), today, inThirtyDays)),
                overdueResponseDeadlines = courtCases.Count(item => IsOpen(item) && DateBefore(Value(item, "responseDeadline"), today)),
                pendingSignatures = cases.Count(item => IsOpen(item) && !string.Equals(Value(item, "signatureStatus"), "Fully signed", StringComparison.OrdinalIgnoreCase)),
                pendingPayments = cases.Count(item => IsOpen(item) && string.Equals(Value(item, "paymentStatus"), "Pending", StringComparison.OrdinalIgnoreCase)),
                awaitingEstateReturn = cases.Count(item => IsOpen(item) && string.Equals(Value(item, "estateReturnStatus"), "Pending return", StringComparison.OrdinalIgnoreCase)),
                upcomingCourtEvents = courtCases
                    .Where(item => IsOpen(item) && (DateOnly.TryParse(Value(item, "nextHearingDate"), out _) || DateOnly.TryParse(Value(item, "responseDeadline"), out _)))
                    .Select(item => new
                    {
                        item.Id,
                        item.Title,
                        item.ReferenceNumber,
                        CourtName = Value(item, "courtName"),
                        CaseNumber = Value(item, "caseNumber"),
                        ResponseDeadline = Value(item, "responseDeadline"),
                        NextHearingDate = Value(item, "nextHearingDate"),
                        Risk = Value(item, "litigationRisk"),
                        item.CurrentStageName
                    })
                    .OrderBy(item => item.NextHearingDate ?? item.ResponseDeadline)
                    .Take(10)
                    .ToList()
            }
        });
    }

    [HttpGet]
    public IActionResult GetProcedures()
    {
        return Ok(new
        {
            success = true,
            data = _procedureCatalog.GetProcedures()
        });
    }

    [HttpGet("search")]
    public IActionResult SearchProcedures([FromQuery] string? search, [FromQuery] int take = 5)
    {
        var term = search?.Trim() ?? string.Empty;
        var procedures = _procedureCatalog.GetProcedures()
            .Where(item => term.Length >= 2 && term.Length <= 100
                && (item.Title.Contains(term, StringComparison.OrdinalIgnoreCase)
                    || item.EntityType.Contains(term, StringComparison.OrdinalIgnoreCase)))
            .Take(Math.Clamp(take, 1, 10));
        return Ok(new { success = true, data = procedures });
    }

    [HttpGet("{entityType}")]
    public IActionResult GetProcedureWorkspace(string entityType)
    {
        var workspace = _procedureCatalog.GetProcedureWorkspace(entityType);

        if (workspace is null)
        {
            return NotFound(new
            {
                success = false,
                message = $"Legal procedure workspace '{entityType}' was not found."
            });
        }

        return Ok(new
        {
            success = true,
            data = workspace
        });
    }
}
