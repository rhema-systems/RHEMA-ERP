using System.Data;
using System.Text.Json;
using ErpSystem.Core.DTOs.Inventory;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Inventory;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.Services.Procurement;
using ErpSystem.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Controllers.Inventory;

[ApiController]
[Route("api/inventory/issue-accounting")]
[Authorize(Policy = "InternalOnly")]
[InventoryIssueAccountingExceptionFilter]
public sealed class InventoryIssueAccountingController : ControllerBase
{
    private const string ManagePermission = "procurement.inventory.master-data.manage";
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserProvider _currentUser;
    private readonly IProcurementAccessControlService _access;
    private readonly IProcurementControlEventService _controlEvents;
    private readonly IInventoryIssueFinanceAssetService _posting;

    public InventoryIssueAccountingController(
        ApplicationDbContext db,
        ICurrentUserProvider currentUser,
        IProcurementAccessControlService access,
        IProcurementControlEventService controlEvents,
        IInventoryIssueFinanceAssetService posting)
    {
        _db = db;
        _currentUser = currentUser;
        _access = access;
        _controlEvents = controlEvents;
        _posting = posting;
    }

    [HttpGet("rules")]
    public async Task<ActionResult<IReadOnlyList<InventoryIssueAccountingRuleDto>>> GetRules(
        CancellationToken cancellationToken)
    {
        await RequireAsync(ManagePermission, null, null, "InventoryIssueAccountingRule", "List", cancellationToken);
        var values = await RuleQuery().AsNoTracking()
            .OrderBy(value => value.InventoryCategory.Code)
            .ThenBy(value => value.ItemType)
            .ThenBy(value => value.MovementReasonCode)
            .ToListAsync(cancellationToken);
        return Ok(values.Select(Map).ToList());
    }

    [HttpGet("options")]
    public async Task<ActionResult<InventoryIssueAccountingOptionsDto>> GetOptions(
        [FromQuery] Guid? requisitionId,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<string> applicable = [];
        IReadOnlyDictionary<Guid, IReadOnlyList<string>> applicableByLine =
            new Dictionary<Guid, IReadOnlyList<string>>();
        if (requisitionId.HasValue)
        {
            var requisition = await _db.InventoryRequisitions.AsNoTracking()
                .Include(value => value.Items).ThenInclude(value => value.InventoryItem)
                .SingleOrDefaultAsync(value => value.TenantId == _currentUser.TenantId &&
                    value.Id == requisitionId.Value && !value.IsDeleted,
                    cancellationToken)
                ?? throw new KeyNotFoundException("The inventory requisition was not found in the current tenant.");
            await RequireAsync("procurement.inventory.issue", requisition.WarehouseId, requisition.LocationId,
                "InventoryRequisition", requisition.RequisitionNumber, cancellationToken, requireLocationScope: true);
            var now = DateTime.UtcNow;
            var rules = await _db.InventoryIssueAccountingRules.AsNoTracking()
                .Where(value => value.TenantId == _currentUser.TenantId && value.IsActive && !value.IsDeleted &&
                    value.EffectiveFromUtc <= now && (!value.EffectiveToUtc.HasValue || value.EffectiveToUtc > now))
                .ToListAsync(cancellationToken);
            applicableByLine = requisition.Items.ToDictionary(
                line => line.Id,
                line => (IReadOnlyList<string>)InventoryIssueMovementReasons.Labels.Keys
                    .Where(reason => rules.Any(rule => rule.InventoryCategoryId == line.InventoryItem.CategoryId &&
                        rule.ItemType == line.InventoryItem.ItemType && rule.MovementReasonCode == reason))
                    .OrderBy(value => value)
                    .ToList());
            applicable = applicableByLine.Values.SelectMany(value => value).Distinct().OrderBy(value => value).ToList();
        }
        else
        {
            await RequireAsync(ManagePermission, null, null, "InventoryIssueAccountingRule", "Options", cancellationToken);
        }

        var categories = await _db.InventoryCategories.AsNoTracking()
            .Where(value => value.TenantId == _currentUser.TenantId && value.IsActive && !value.IsDeleted)
            .OrderBy(value => value.Code).ThenBy(value => value.Name)
            .Select(value => new InventoryIssueAccountingOptionDto
            {
                Id = value.Id,
                Code = value.Code,
                Name = value.Name
            }).ToListAsync(cancellationToken);
        var expenseAccounts = await _db.Accounts.AsNoTracking()
            .Where(value => value.TenantId == _currentUser.TenantId && value.AccountType == AccountType.Expense &&
                value.Status == AccountStatus.Active && value.AllowDirectPosting && !value.IsDeleted)
            .OrderBy(value => value.AccountNumber).ThenBy(value => value.AccountName)
            .Select(value => new InventoryIssueAccountingOptionDto
            {
                Id = value.Id,
                Code = value.AccountNumber,
                Name = value.AccountName,
                Type = value.AccountType.ToString()
            }).ToListAsync(cancellationToken);
        var assetCategories = await _db.FixedAssetCategories.AsNoTracking()
            .Where(value => value.TenantId == _currentUser.TenantId && !value.IsDeleted &&
                value.AssetAccount.Status == AccountStatus.Active && value.AssetAccount.AllowDirectPosting &&
                !value.AssetAccount.IsDeleted)
            .OrderBy(value => value.Code).ThenBy(value => value.Name)
            .Select(value => new InventoryIssueAccountingOptionDto
            {
                Id = value.Id,
                Code = value.Code,
                Name = value.Name,
                Type = "FixedAsset"
            }).ToListAsync(cancellationToken);

        return Ok(new InventoryIssueAccountingOptionsDto
        {
            InventoryCategories = categories,
            ExpenseAccounts = expenseAccounts,
            FixedAssetCategories = assetCategories,
            MovementReasons = InventoryIssueMovementReasons.Labels,
            ApplicableMovementReasonCodes = applicable,
            ApplicableMovementReasonCodesByRequisitionItem = applicableByLine
        });
    }

    [HttpPost("rules")]
    public Task<ActionResult<InventoryIssueAccountingRuleDto>> CreateRule(
        [FromBody] InventoryIssueAccountingRuleRequest request,
        CancellationToken cancellationToken) =>
        SaveRuleAsync(null, request, cancellationToken);

    [HttpPut("rules/{id:guid}")]
    public Task<ActionResult<InventoryIssueAccountingRuleDto>> UpdateRule(
        Guid id,
        [FromBody] InventoryIssueAccountingRuleRequest request,
        CancellationToken cancellationToken) =>
        SaveRuleAsync(id, request, cancellationToken);

    [HttpDelete("rules/{id:guid}")]
    public async Task<IActionResult> DeleteRule(
        Guid id,
        [FromQuery] string rowVersion,
        CancellationToken cancellationToken)
    {
        await RequireAsync(ManagePermission, null, null, "InventoryIssueAccountingRule", id.ToString(), cancellationToken);
        var strategy = _db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync<IActionResult>(async () =>
        {
            await using var transaction = await _db.Database.BeginTransactionAsync(
                IsolationLevel.Serializable, cancellationToken);
            var entity = await RuleQuery().SingleOrDefaultAsync(value => value.Id == id, cancellationToken)
                ?? throw new KeyNotFoundException("The issue-accounting rule was not found in the current tenant.");
            if (await _db.InventoryIssueFinanceLineages.AsNoTracking().AnyAsync(value =>
                    value.TenantId == _currentUser.TenantId && value.InventoryIssueAccountingRuleId == id && !value.IsDeleted,
                    cancellationToken))
                return Conflict(Problem("INV_ISSUE_RULE_IN_USE",
                    "A rule with posted issue lineage cannot be deleted; deactivate it to preserve history.", 409));
            ApplyRowVersion(entity, rowVersion);
            var before = Map(entity);
            entity.IsDeleted = true;
            entity.DeletedAt = DateTime.UtcNow;
            entity.DeletedBy = _currentUser.Username;
            entity.IsActive = false;
            entity.UpdatedAt = DateTime.UtcNow;
            entity.UpdatedBy = _currentUser.Username;
            await AddAuditAsync("Delete", entity.Id, before, new { entity.IsDeleted }, cancellationToken);
            await _db.SaveChangesAsync(cancellationToken);
            await RecordEventAsync(entity, "Delete", before, new { entity.IsDeleted }, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return NoContent();
        });
    }

    [HttpGet("issue-vouchers/{issueVoucherId:guid}/lineage")]
    public async Task<ActionResult<IReadOnlyList<InventoryIssueFinanceLineageDto>>> GetLineage(
        Guid issueVoucherId,
        CancellationToken cancellationToken)
    {
        var voucher = await _db.InventoryIssueVouchers.AsNoTracking()
            .SingleOrDefaultAsync(value => value.TenantId == _currentUser.TenantId &&
                value.Id == issueVoucherId && !value.IsDeleted,
                cancellationToken)
            ?? throw new KeyNotFoundException("The issue voucher was not found in the current tenant.");
        await RequireAsync("procurement.inventory.read", voucher.WarehouseId, voucher.LocationId,
            "InventoryIssueVoucher", voucher.VoucherNumber, cancellationToken, requireLocationScope: true);
        return Ok(await _posting.GetIssueLineageAsync(issueVoucherId, cancellationToken));
    }

    private async Task<ActionResult<InventoryIssueAccountingRuleDto>> SaveRuleAsync(
        Guid? id,
        InventoryIssueAccountingRuleRequest request,
        CancellationToken cancellationToken)
    {
        await RequireAsync(ManagePermission, null, null, "InventoryIssueAccountingRule",
            id?.ToString() ?? "Create", cancellationToken);
        NormalizeAndValidate(request);
        await ValidateOwnersAsync(request, cancellationToken);
        var strategy = _db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync<ActionResult<InventoryIssueAccountingRuleDto>>(async () =>
        {
            await using var transaction = await _db.Database.BeginTransactionAsync(
                IsolationLevel.Serializable, cancellationToken);

            InventoryIssueAccountingRule entity;
            InventoryIssueAccountingRuleDto? before = null;
            if (id.HasValue)
            {
                entity = await RuleQuery().SingleOrDefaultAsync(value => value.Id == id.Value, cancellationToken)
                    ?? throw new KeyNotFoundException("The issue-accounting rule was not found in the current tenant.");
                ApplyRowVersion(entity, request.RowVersion);
                before = Map(entity);
            }
            else
            {
                var duplicate = await RuleQuery().SingleOrDefaultAsync(value =>
                    value.InventoryCategoryId == request.InventoryCategoryId && value.ItemType == request.ItemType &&
                    value.MovementReasonCode == request.MovementReasonCode,
                    cancellationToken);
                if (duplicate != null)
                {
                    if (Equivalent(duplicate, request)) return Ok(Map(duplicate));
                    return Conflict(Problem("INV_ISSUE_RULE_DUPLICATE",
                        "A rule already exists for this category, item type and movement reason.", 409));
                }
                entity = new InventoryIssueAccountingRule
                {
                    TenantId = _currentUser.TenantId,
                    CreatedById = _currentUser.UserId,
                    CreatedBy = _currentUser.Username
                };
                _db.InventoryIssueAccountingRules.Add(entity);
            }

            entity.InventoryCategoryId = request.InventoryCategoryId;
            entity.ItemType = request.ItemType;
            entity.MovementReasonCode = request.MovementReasonCode;
            entity.Treatment = request.Treatment;
            entity.ExpenseAccountId = request.ExpenseAccountId;
            entity.FixedAssetCategoryId = request.FixedAssetCategoryId;
            entity.IsActive = request.IsActive;
            entity.EffectiveFromUtc = request.EffectiveFromUtc;
            entity.EffectiveToUtc = request.EffectiveToUtc;
            entity.UpdatedAt = DateTime.UtcNow;
            entity.UpdatedBy = _currentUser.Username;
            entity.LastModifiedById = _currentUser.UserId;

            var action = id.HasValue ? "Update" : "Create";
            await AddAuditAsync(action, entity.Id, before, request, cancellationToken);
            await _db.SaveChangesAsync(cancellationToken);
            await _db.Entry(entity).Reference(value => value.InventoryCategory).LoadAsync(cancellationToken);
            if (entity.ExpenseAccountId.HasValue)
                await _db.Entry(entity).Reference(value => value.ExpenseAccount).LoadAsync(cancellationToken);
            if (entity.FixedAssetCategoryId.HasValue)
                await _db.Entry(entity).Reference(value => value.FixedAssetCategory).LoadAsync(cancellationToken);
            var after = Map(entity);
            await RecordEventAsync(entity, action, before, after, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return id.HasValue ? Ok(after) : CreatedAtAction(nameof(GetRules), null, after);
        });
    }

    private async Task ValidateOwnersAsync(
        InventoryIssueAccountingRuleRequest request,
        CancellationToken cancellationToken)
    {
        if (!await _db.InventoryCategories.AsNoTracking().AnyAsync(value =>
                value.TenantId == _currentUser.TenantId && value.Id == request.InventoryCategoryId &&
                value.IsActive && !value.IsDeleted,
                cancellationToken))
            throw new InventoryIssueAccountingControlException("INV_ISSUE_CATEGORY_INVALID",
                "Select an active inventory category from this tenant.");
        if (request.Treatment == InventoryIssueAccountingTreatment.Expense)
        {
            if (!request.ExpenseAccountId.HasValue || request.FixedAssetCategoryId.HasValue ||
                !await _db.Accounts.AsNoTracking().AnyAsync(value =>
                    value.TenantId == _currentUser.TenantId && value.Id == request.ExpenseAccountId &&
                    value.AccountType == AccountType.Expense && value.Status == AccountStatus.Active &&
                    value.AllowDirectPosting && !value.IsDeleted,
                    cancellationToken))
                throw new InventoryIssueAccountingControlException("INV_ISSUE_EXPENSE_ACCOUNT_INVALID",
                    "Select an active Finance expense posting account.");
            return;
        }
        if (!request.FixedAssetCategoryId.HasValue || request.ExpenseAccountId.HasValue ||
            !await _db.FixedAssetCategories.AsNoTracking().AnyAsync(value =>
                value.TenantId == _currentUser.TenantId && value.Id == request.FixedAssetCategoryId &&
                !value.IsDeleted && value.AssetAccount.Status == AccountStatus.Active &&
                value.AssetAccount.AllowDirectPosting && !value.AssetAccount.IsDeleted,
                cancellationToken))
            throw new InventoryIssueAccountingControlException("INV_ISSUE_ASSET_CATEGORY_INVALID",
                "Select a Fixed Assets category whose asset account is active for posting.");
    }

    private static void NormalizeAndValidate(InventoryIssueAccountingRuleRequest request)
    {
        request.MovementReasonCode = request.MovementReasonCode.Trim().ToUpperInvariant();
        request.EffectiveFromUtc = request.EffectiveFromUtc == default
            ? DateTime.UtcNow
            : request.EffectiveFromUtc.ToUniversalTime();
        request.EffectiveToUtc = request.EffectiveToUtc?.ToUniversalTime();
        if (!InventoryIssueMovementReasons.Labels.ContainsKey(request.MovementReasonCode))
            throw new InventoryIssueAccountingControlException("INV_ISSUE_MOVEMENT_REASON_INVALID",
                "Select a supported inventory movement reason.");
        if (request.EffectiveToUtc.HasValue && request.EffectiveToUtc <= request.EffectiveFromUtc)
            throw new InventoryIssueAccountingControlException("INV_ISSUE_RULE_EFFECTIVE_RANGE_INVALID",
                "Effective To must be later than Effective From.");
        if (request.ItemType == ItemType.FixedAsset)
        {
            if (request.Treatment != InventoryIssueAccountingTreatment.FixedAsset ||
                request.MovementReasonCode != InventoryIssueMovementReasons.AssetCustody)
                throw new InventoryIssueAccountingControlException("INV_ISSUE_FIXED_ASSET_RULE_INVALID",
                    "Fixed-asset items must use the fixed-asset treatment and asset-custody reason.");
        }
        else if (request.Treatment != InventoryIssueAccountingTreatment.Expense ||
                 request.MovementReasonCode == InventoryIssueMovementReasons.AssetCustody)
            throw new InventoryIssueAccountingControlException("INV_ISSUE_EXPENSE_RULE_INVALID",
                "Non-fixed-asset items must use an expense treatment and consumption reason.");
    }

    private async Task RequireAsync(
        string permission,
        Guid? warehouseId,
        Guid? locationId,
        string sourceType,
        string reference,
        CancellationToken cancellationToken,
        bool requireLocationScope = false)
    {
        var decision = await _access.EnforceCapabilityAsync(new ProcurementAccessCapabilityRequest
        {
            PermissionCode = permission,
            WarehouseId = warehouseId,
            LocationId = locationId,
            RequireLocationScope = requireLocationScope,
            SourceType = sourceType,
            SourceReference = reference
        }, HttpContext.TraceIdentifier, cancellationToken);
        if (!decision.Allowed) throw new ProcurementAccessAuthorizationException(decision.Message);
    }

    private IQueryable<InventoryIssueAccountingRule> RuleQuery() =>
        _db.InventoryIssueAccountingRules
            .Include(value => value.InventoryCategory)
            .Include(value => value.ExpenseAccount)
            .Include(value => value.FixedAssetCategory)
            .Where(value => value.TenantId == _currentUser.TenantId && !value.IsDeleted);

    private async Task AddAuditAsync(
        string action,
        Guid id,
        object? before,
        object? after,
        CancellationToken cancellationToken)
    {
        _db.AuditLogs.Add(new AuditLog
        {
            TenantId = _currentUser.TenantId,
            UserId = _currentUser.UserId,
            Username = _currentUser.Username,
            Action = $"InventoryIssueAccountingRule.{action}",
            Resource = "InventoryIssueAccountingRule",
            ResourceId = id.ToString(),
            OldValues = before == null ? null : JsonSerializer.Serialize(before),
            NewValues = after == null ? null : JsonSerializer.Serialize(after),
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Service",
            UserAgent = Request.Headers.UserAgent.ToString(),
            Timestamp = DateTime.UtcNow
        });
        await Task.CompletedTask;
    }

    private Task RecordEventAsync(
        InventoryIssueAccountingRule entity,
        string action,
        object? before,
        object? after,
        CancellationToken cancellationToken) =>
        _controlEvents.RecordAsync(new ProcurementControlEventWriteRequest
        {
            EventKey = ProcurementControlEventKey.Create("inventory-issue-accounting-rule", entity.Id, action,
                Convert.ToBase64String(entity.RowVersion ?? [])),
            EventType = "InventoryIssueAccountingRule",
            Action = action,
            Result = ProcurementControlEventResult.Allowed,
            RuleCode = "INV-REQ-FU-003",
            RuleId = entity.Id,
            RuleVersion = "1",
            SourceType = "InventoryIssueAccountingRule",
            SourceId = entity.Id,
            SourceReference = $"{entity.InventoryCategoryId:N}:{entity.ItemType}:{entity.MovementReasonCode}",
            Before = before,
            After = after,
            CorrelationId = HttpContext.TraceIdentifier,
            OccurredAtUtc = DateTime.UtcNow
        }, cancellationToken);

    private static void ApplyRowVersion(InventoryIssueAccountingRule entity, string? rowVersion)
    {
        if (string.IsNullOrWhiteSpace(rowVersion))
            throw new InventoryIssueAccountingControlException("INV_ISSUE_RULE_ROW_VERSION_REQUIRED",
                "Refresh the rule before changing it.");
        try { entity.RowVersion = Convert.FromBase64String(rowVersion); }
        catch (FormatException)
        {
            throw new InventoryIssueAccountingControlException("INV_ISSUE_RULE_ROW_VERSION_INVALID",
                "The rule version is invalid. Refresh and retry.");
        }
    }

    private static bool Equivalent(
        InventoryIssueAccountingRule entity,
        InventoryIssueAccountingRuleRequest request) =>
        entity.Treatment == request.Treatment && entity.ExpenseAccountId == request.ExpenseAccountId &&
        entity.FixedAssetCategoryId == request.FixedAssetCategoryId && entity.IsActive == request.IsActive &&
        entity.EffectiveFromUtc == request.EffectiveFromUtc && entity.EffectiveToUtc == request.EffectiveToUtc;

    private static InventoryIssueAccountingRuleDto Map(InventoryIssueAccountingRule value) => new()
    {
        Id = value.Id,
        InventoryCategoryId = value.InventoryCategoryId,
        InventoryCategoryCode = value.InventoryCategory?.Code ?? string.Empty,
        InventoryCategoryName = value.InventoryCategory?.Name ?? string.Empty,
        ItemType = value.ItemType,
        MovementReasonCode = value.MovementReasonCode,
        MovementReasonName = InventoryIssueMovementReasons.Labels.GetValueOrDefault(value.MovementReasonCode,
            value.MovementReasonCode),
        Treatment = value.Treatment,
        ExpenseAccountId = value.ExpenseAccountId,
        ExpenseAccount = value.ExpenseAccount == null
            ? null : $"{value.ExpenseAccount.AccountNumber} - {value.ExpenseAccount.AccountName}",
        FixedAssetCategoryId = value.FixedAssetCategoryId,
        FixedAssetCategory = value.FixedAssetCategory == null
            ? null : $"{value.FixedAssetCategory.Code} - {value.FixedAssetCategory.Name}",
        IsActive = value.IsActive,
        EffectiveFromUtc = value.EffectiveFromUtc,
        EffectiveToUtc = value.EffectiveToUtc,
        RowVersion = Convert.ToBase64String(value.RowVersion ?? [])
    };

    private ProblemDetails Problem(string code, string detail, int status) => new()
    {
        Status = status,
        Title = "Inventory issue accounting request failed",
        Detail = detail,
        Instance = HttpContext.Request.Path,
        Extensions = { ["code"] = code, ["correlationId"] = HttpContext.TraceIdentifier }
    };
}

public sealed class InventoryIssueAccountingExceptionFilterAttribute : ExceptionFilterAttribute
{
    public override void OnException(ExceptionContext context)
    {
        var (status, code, detail) = context.Exception switch
        {
            InventoryIssueAccountingControlException control =>
                (StatusCodes.Status422UnprocessableEntity, control.Code, control.Message),
            ProcurementAccessAuthorizationException access =>
                (StatusCodes.Status403Forbidden, "INV_ISSUE_ACCOUNTING_FORBIDDEN", access.Message),
            KeyNotFoundException missing =>
                (StatusCodes.Status404NotFound, "INV_ISSUE_ACCOUNTING_NOT_FOUND", missing.Message),
            DbUpdateConcurrencyException =>
                (StatusCodes.Status409Conflict, "INV_ISSUE_ACCOUNTING_CONFLICT",
                    "The issue-accounting rule changed. Refresh and retry."),
            DbUpdateException =>
                (StatusCodes.Status409Conflict, "INV_ISSUE_ACCOUNTING_CONFLICT",
                    "The issue-accounting rule conflicts with existing governed data."),
            _ => default
        };
        if (status == 0) return;

        context.HttpContext.Items[
            ErpSystem.Api.Filters.SystemExceptionResultLoggingFilter.HandledExceptionItemKey] = context.Exception;

        context.Result = new ObjectResult(new ProblemDetails
        {
            Status = status,
            Title = "Inventory issue accounting request failed",
            Detail = detail,
            Instance = context.HttpContext.Request.Path,
            Extensions =
            {
                ["code"] = code,
                ["correlationId"] = context.HttpContext.TraceIdentifier
            }
        }) { StatusCode = status };
        context.ExceptionHandled = true;
    }
}
