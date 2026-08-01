using System.Globalization;
using System.Text;
using System.Text.Json;
using ErpSystem.Core.DTOs.Inventory;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Inventory;
using ErpSystem.Core.Interfaces.Procurement;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using InventoryItemUnitDto = ErpSystem.Core.DTOs.Inventory.ItemUnitOfMeasureDto;

namespace ErpSystem.Api.Controllers;

[ApiController]
[Route("api/inventory/item-identifiers")]
[Authorize(Policy = "InternalOnly")]
public sealed class InventoryItemIdentifiersController : ControllerBase
{
    private static readonly string[] CsvColumns =
    [
        "ItemCode", "ItemName", "PrimaryBarcode", "AlternateBarcode", "QRCode",
        "UnitCode", "UnitBarcode", "ConversionToBase"
    ];

    private readonly IUnitOfWork _unitOfWork;
    private readonly IInventoryItemIdentifierService _identifiers;
    private readonly ICurrentUserProvider _currentUser;
    private readonly IProcurementMasterDataChangeService? _masterDataChanges;
    private readonly ILogger<InventoryItemIdentifiersController> _logger;

    public InventoryItemIdentifiersController(
        IUnitOfWork unitOfWork,
        IInventoryItemIdentifierService identifiers,
        ICurrentUserProvider currentUser,
        ILogger<InventoryItemIdentifiersController> logger,
        IProcurementMasterDataChangeService? masterDataChanges = null)
    {
        _unitOfWork = unitOfWork;
        _identifiers = identifiers;
        _currentUser = currentUser;
        _logger = logger;
        _masterDataChanges = masterDataChanges;
    }

    [HttpGet("resolve")]
    public async Task<ActionResult<InventoryIdentifierMatchDto>> Resolve(
        [FromQuery] string identifier,
        CancellationToken cancellationToken)
    {
        try
        {
            var match = await _identifiers.ResolveAsync(GetTenantId(), identifier, cancellationToken);
            return match is null
                ? NotFound(new ProblemDetails { Status = 404, Title = "Identifier not found", Detail = "No current-tenant inventory item uses this identifier." })
                : Ok(match);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ProblemDetails { Status = 400, Title = "Invalid identifier", Detail = ex.Message });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    [HttpGet("items/{inventoryItemId:guid}/units")]
    public async Task<ActionResult<IReadOnlyList<InventoryItemUnitDto>>> GetUnits(
        Guid inventoryItemId,
        CancellationToken cancellationToken)
    {
        if (!TryGetTenantId(out var tenantId)) return Forbid();
        var exists = await Items.AnyAsync(item => item.TenantId == tenantId && item.Id == inventoryItemId && !item.IsDeleted, cancellationToken);
        if (!exists)
        {
            return NotFound();
        }

        var rows = await Units
            .Where(unit => unit.TenantId == tenantId && unit.InventoryItemId == inventoryItemId && !unit.IsDeleted)
            .AsNoTracking()
            .OrderByDescending(unit => unit.IsBaseUnit)
            .ThenBy(unit => unit.UnitOfMeasure.Code)
            .Select(unit => new InventoryItemUnitDto
            {
                Id = unit.Id,
                InventoryItemId = unit.InventoryItemId,
                UnitOfMeasureId = unit.UnitOfMeasureId,
                UnitCode = unit.UnitOfMeasure.Code,
                UnitName = unit.UnitOfMeasure.Name,
                ConversionFactor = unit.ConversionToBase,
                IsBaseUnit = unit.IsBaseUnit,
                IsPurchaseUnit = unit.IsPurchaseUnit,
                IsSalesUnit = unit.IsSalesUnit,
                IsStockingUnit = unit.IsStockingUnit,
                Barcode = unit.Barcode
            })
            .ToListAsync(cancellationToken);
        return Ok(rows);
    }

    [HttpPut("items/{inventoryItemId:guid}")]
    public async Task<ActionResult<InventoryItemDto>> UpdateItemIdentifiers(
        Guid inventoryItemId,
        [FromBody] UpdateInventoryItemIdentifiersDto request,
        CancellationToken cancellationToken)
    {
        try
        {
            var tenantId = GetTenantId();
            var protection = await GuardDirectMutationAsync(inventoryItemId, "InventoryItem.IdentifierUpdate", cancellationToken);
            if (protection is not null) return protection;
            var item = await Items.SingleOrDefaultAsync(
                value => value.TenantId == tenantId && value.Id == inventoryItemId && !value.IsDeleted,
                cancellationToken);
            if (item is null) return NotFound();

            var before = new { item.Barcode, item.AlternateBarcode, item.QRCode };
            item.Barcode = _identifiers.Normalize(request.Barcode);
            item.AlternateBarcode = _identifiers.Normalize(request.AlternateBarcode);
            item.QRCode = _identifiers.Normalize(request.QRCode);
            await _identifiers.ValidateItemIdentifiersAsync(
                tenantId, item.Id, item.Barcode, item.AlternateBarcode, item.QRCode, cancellationToken);

            item.UpdatedAt = DateTime.UtcNow;
            item.LastModifiedById = _currentUser.UserId == Guid.Empty ? null : _currentUser.UserId;
            await QueueAuditAsync("InventoryItemIdentifiers.Updated", item.Id, before, new { item.Barcode, item.AlternateBarcode, item.QRCode });
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return Ok(ToDto(item));
        }
        catch (InventoryIdentifierConflictException ex)
        {
            return IdentifierConflict(ex);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    [HttpPut("items/{inventoryItemId:guid}/units/{unitOfMeasureId:guid}")]
    public async Task<ActionResult<InventoryItemUnitDto>> UpdateUnitIdentifier(
        Guid inventoryItemId,
        Guid unitOfMeasureId,
        [FromBody] UpdateItemUnitIdentifierDto request,
        CancellationToken cancellationToken)
    {
        if (request.UnitOfMeasureId != Guid.Empty && request.UnitOfMeasureId != unitOfMeasureId)
        {
            return BadRequest(new ProblemDetails { Status = 400, Title = "Unit mismatch", Detail = "The route and payload unit identifiers must match." });
        }

        try
        {
            var tenantId = GetTenantId();
            var protection = await GuardDirectMutationAsync(inventoryItemId, "InventoryItem.UnitIdentifierUpdate", cancellationToken);
            if (protection is not null) return protection;
            var item = await Items.AsNoTracking().SingleOrDefaultAsync(
                value => value.TenantId == tenantId && value.Id == inventoryItemId && !value.IsDeleted,
                cancellationToken);
            if (item is null) return NotFound("Inventory item not found.");

            var unitDefinition = await _unitOfWork.Repository<UnitOfMeasure>().GetQueryable(value =>
                    value.TenantId == tenantId && value.Id == unitOfMeasureId && !value.IsDeleted)
                .AsNoTracking()
                .SingleOrDefaultAsync(cancellationToken);
            if (unitDefinition is null) return BadRequest("Unit of measure is not available in the current tenant.");

            var row = await Units.SingleOrDefaultAsync(value =>
                value.TenantId == tenantId &&
                value.InventoryItemId == inventoryItemId &&
                value.UnitOfMeasureId == unitOfMeasureId &&
                !value.IsDeleted, cancellationToken);
            var before = row is null ? null : new { row.Barcode, row.ConversionToBase, row.IsBaseUnit, row.IsPurchaseUnit, row.IsSalesUnit, row.IsStockingUnit };

            var normalized = _identifiers.Normalize(request.Barcode);
            await _identifiers.ValidateUnitIdentifierAsync(tenantId, inventoryItemId, row?.Id, normalized, cancellationToken);

            if (row is null)
            {
                row = new ItemUnitOfMeasure
                {
                    TenantId = tenantId,
                    InventoryItemId = inventoryItemId,
                    UnitOfMeasureId = unitOfMeasureId,
                    CreatedById = _currentUser.UserId == Guid.Empty ? null : _currentUser.UserId
                };
                await _unitOfWork.Repository<ItemUnitOfMeasure>().AddAsync(row);
            }

            row.ConversionToBase = request.ConversionToBase;
            row.IsBaseUnit = request.IsBaseUnit;
            row.IsPurchaseUnit = request.IsPurchaseUnit;
            row.IsSalesUnit = request.IsSalesUnit;
            row.IsStockingUnit = request.IsStockingUnit;
            row.Barcode = normalized;
            row.IsActive = true;
            row.UpdatedAt = DateTime.UtcNow;
            row.LastModifiedById = _currentUser.UserId == Guid.Empty ? null : _currentUser.UserId;

            await QueueAuditAsync("InventoryItemUnitIdentifier.Updated", row.Id, before, new { row.Barcode, row.ConversionToBase, row.UnitOfMeasureId });
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return Ok(new InventoryItemUnitDto
            {
                Id = row.Id,
                InventoryItemId = row.InventoryItemId,
                UnitOfMeasureId = row.UnitOfMeasureId,
                UnitCode = unitDefinition.Code,
                UnitName = unitDefinition.Name,
                ConversionFactor = row.ConversionToBase,
                IsBaseUnit = row.IsBaseUnit,
                IsPurchaseUnit = row.IsPurchaseUnit,
                IsSalesUnit = row.IsSalesUnit,
                IsStockingUnit = row.IsStockingUnit,
                Barcode = row.Barcode
            });
        }
        catch (InventoryIdentifierConflictException ex)
        {
            return IdentifierConflict(ex);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    [HttpGet("export")]
    public async Task<IActionResult> Export(CancellationToken cancellationToken)
    {
        if (!TryGetTenantId(out var tenantId)) return Forbid();
        var items = await Items
            .Where(item => item.TenantId == tenantId && !item.IsDeleted)
            .AsNoTracking()
            .OrderBy(item => item.ItemCode)
            .Select(item => new { item.Id, item.ItemCode, item.Name, item.Barcode, item.AlternateBarcode, item.QRCode })
            .ToListAsync(cancellationToken);
        var units = await Units
            .Where(unit => unit.TenantId == tenantId && !unit.IsDeleted)
            .AsNoTracking()
            .Select(unit => new { unit.InventoryItemId, UnitCode = unit.UnitOfMeasure.Code, unit.Barcode, unit.ConversionToBase })
            .ToListAsync(cancellationToken);
        var byItem = units.ToLookup(unit => unit.InventoryItemId);

        var csv = new StringBuilder().AppendLine(string.Join(',', CsvColumns));
        foreach (var item in items)
        {
            var itemUnits = byItem[item.Id].ToList();
            if (itemUnits.Count == 0)
            {
                AppendCsvRow(csv, item.ItemCode, item.Name, item.Barcode, item.AlternateBarcode, item.QRCode, null, null, null);
                continue;
            }

            foreach (var unit in itemUnits)
            {
                AppendCsvRow(csv, item.ItemCode, item.Name, item.Barcode, item.AlternateBarcode, item.QRCode,
                    unit.UnitCode, unit.Barcode, unit.ConversionToBase.ToString(CultureInfo.InvariantCulture));
            }
        }

        return File(new UTF8Encoding(true).GetBytes(csv.ToString()), "text/csv", $"inventory-item-identifiers-{DateTime.UtcNow:yyyyMMddHHmmss}.csv");
    }

    [HttpPost("import")]
    [RequestSizeLimit(5 * 1024 * 1024)]
    public async Task<ActionResult<InventoryIdentifierImportResultDto>> Import(
        IFormFile file,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest(new ProblemDetails { Status = 400, Title = "CSV file required" });
        }

        try
        {
            var tenantId = GetTenantId();
            var protection = await GuardDirectMutationAsync(null, "InventoryItem.IdentifierImport", cancellationToken);
            if (protection is not null) return protection;
            var parsed = await ParseImportAsync(file, cancellationToken);
            if (parsed.Errors.Count > 0)
            {
                return BadRequest(parsed);
            }

            await ApplyImportAsync(tenantId, parsed, cancellationToken);
            return Ok(parsed);
        }
        catch (InventoryIdentifierConflictException ex)
        {
            return IdentifierConflict(ex);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (InvalidDataException ex)
        {
            return BadRequest(new ProblemDetails { Status = 400, Title = "Invalid identifier import", Detail = ex.Message });
        }
    }

    private IQueryable<InventoryItem> Items => _unitOfWork.Repository<InventoryItem>().GetQueryable();
    private IQueryable<ItemUnitOfMeasure> Units => _unitOfWork.Repository<ItemUnitOfMeasure>().GetQueryable();

    private async Task<ImportState> ParseImportAsync(IFormFile file, CancellationToken cancellationToken)
    {
        var result = new ImportState();
        using var reader = new StreamReader(file.OpenReadStream(), Encoding.UTF8, true, leaveOpen: false);
        var headerLine = await reader.ReadLineAsync(cancellationToken);
        if (headerLine is null) throw new InvalidDataException("The CSV file is empty.");
        var headers = ParseCsvLine(headerLine);
        if (headers.Count != CsvColumns.Length || !headers.SequenceEqual(CsvColumns, StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidDataException($"Expected columns: {string.Join(", ", CsvColumns)}.");
        }

        var lineNumber = 1;
        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            lineNumber++;
            if (string.IsNullOrWhiteSpace(line)) continue;
            result.TotalRows++;
            var values = ParseCsvLine(line);
            if (values.Count != CsvColumns.Length)
            {
                result.Errors.Add($"Line {lineNumber}: expected {CsvColumns.Length} columns but found {values.Count}.");
                continue;
            }

            var itemCode = ImportValue(values[0]);
            if (string.IsNullOrWhiteSpace(itemCode))
            {
                result.Errors.Add($"Line {lineNumber}: ItemCode is required.");
                continue;
            }

            result.Rows.Add(new ImportRow(
                lineNumber,
                itemCode!,
                _identifiers.Normalize(ImportValue(values[2])),
                _identifiers.Normalize(ImportValue(values[3])),
                _identifiers.Normalize(ImportValue(values[4])),
                ImportValue(values[5]),
                _identifiers.Normalize(ImportValue(values[6])),
                ParseConversion(values[7], lineNumber)));
        }

        ValidateBatchDuplicates(result);
        return result;
    }

    private async Task ApplyImportAsync(Guid tenantId, ImportState state, CancellationToken cancellationToken)
    {
        var codes = state.Rows.Select(row => row.ItemCode).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var items = await Items
            .Where(item => item.TenantId == tenantId && !item.IsDeleted && codes.Contains(item.ItemCode))
            .ToListAsync(cancellationToken);
        var byCode = items.ToDictionary(item => item.ItemCode, StringComparer.OrdinalIgnoreCase);

        foreach (var code in codes.Where(code => !byCode.ContainsKey(code)))
        {
            state.Errors.Add($"ItemCode '{code}' does not exist in the current tenant.");
        }
        if (state.Errors.Count > 0) throw new InvalidDataException(string.Join(' ', state.Errors));

        var unitCodes = state.Rows.Where(row => !string.IsNullOrWhiteSpace(row.UnitCode))
            .Select(row => row.UnitCode!).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var unitDefinitions = await _unitOfWork.Repository<UnitOfMeasure>().GetQueryable(unit =>
                unit.TenantId == tenantId && !unit.IsDeleted && unitCodes.Contains(unit.Code))
            .ToListAsync(cancellationToken);
        var unitsByCode = unitDefinitions.ToDictionary(unit => unit.Code, StringComparer.OrdinalIgnoreCase);
        foreach (var code in unitCodes.Where(code => !unitsByCode.ContainsKey(code)))
        {
            state.Errors.Add($"UnitCode '{code}' does not exist in the current tenant.");
        }
        if (state.Errors.Count > 0) throw new InvalidDataException(string.Join(' ', state.Errors));

        await _unitOfWork.ExecuteInStrategyAsync(async () =>
        {
            await _unitOfWork.BeginTransactionAsync(cancellationToken);
            try
            {
                foreach (var group in state.Rows.GroupBy(row => row.ItemCode, StringComparer.OrdinalIgnoreCase))
                {
                    var item = byCode[group.Key];
                    var row = group.First();
                    var before = new { item.Barcode, item.AlternateBarcode, item.QRCode };
                    item.Barcode = row.Barcode;
                    item.AlternateBarcode = row.AlternateBarcode;
                    item.QRCode = row.QRCode;
                    await _identifiers.ValidateItemIdentifiersAsync(tenantId, item.Id, item.Barcode, item.AlternateBarcode, item.QRCode, cancellationToken);
                    item.UpdatedAt = DateTime.UtcNow;
                    item.LastModifiedById = _currentUser.UserId == Guid.Empty ? null : _currentUser.UserId;
                    await QueueAuditAsync("InventoryItemIdentifiers.Imported", item.Id, before, new { item.Barcode, item.AlternateBarcode, item.QRCode });
                    state.UpdatedItems++;

                    foreach (var unitRow in group.Where(value => !string.IsNullOrWhiteSpace(value.UnitCode)))
                    {
                        var unit = unitsByCode[unitRow.UnitCode!];
                        var itemUnit = await Units.SingleOrDefaultAsync(value =>
                            value.TenantId == tenantId && value.InventoryItemId == item.Id && value.UnitOfMeasureId == unit.Id && !value.IsDeleted,
                            cancellationToken);
                        await _identifiers.ValidateUnitIdentifierAsync(tenantId, item.Id, itemUnit?.Id, unitRow.UnitBarcode, cancellationToken);
                        var unitBefore = itemUnit is null ? null : new { itemUnit.Barcode, itemUnit.ConversionToBase };
                        if (itemUnit is null)
                        {
                            itemUnit = new ItemUnitOfMeasure
                            {
                                TenantId = tenantId,
                                InventoryItemId = item.Id,
                                UnitOfMeasureId = unit.Id,
                                CreatedById = _currentUser.UserId == Guid.Empty ? null : _currentUser.UserId
                            };
                            await _unitOfWork.Repository<ItemUnitOfMeasure>().AddAsync(itemUnit);
                        }
                        itemUnit.Barcode = unitRow.UnitBarcode;
                        itemUnit.ConversionToBase = unitRow.ConversionToBase;
                        itemUnit.IsActive = true;
                        itemUnit.UpdatedAt = DateTime.UtcNow;
                        itemUnit.LastModifiedById = _currentUser.UserId == Guid.Empty ? null : _currentUser.UserId;
                        await QueueAuditAsync("InventoryItemUnitIdentifier.Imported", itemUnit.Id, unitBefore, new { itemUnit.Barcode, itemUnit.ConversionToBase, itemUnit.UnitOfMeasureId });
                        state.UpdatedUnits++;
                    }
                }

                await _unitOfWork.SaveChangesAsync(cancellationToken);
                await _unitOfWork.CommitAsync(cancellationToken);
            }
            catch
            {
                await _unitOfWork.RollbackAsync(cancellationToken);
                throw;
            }
        }, cancellationToken);
    }

    private async Task<ObjectResult?> GuardDirectMutationAsync(Guid? itemId, string action, CancellationToken cancellationToken)
    {
        if (_masterDataChanges is null) return null;
        var decision = await _masterDataChanges.CheckDirectMutationAsync(
            [ProcurementMasterDataResourceType.InventoryItem], itemId, action,
            HttpContext.TraceIdentifier, cancellationToken);
        return decision.Allowed ? null : Conflict(new ProblemDetails
        {
            Status = 409,
            Title = "Staged inventory-item change required",
            Detail = decision.Message,
            Instance = HttpContext.Request.Path,
            Extensions = { ["code"] = decision.Code, ["correlationId"] = decision.CorrelationId, ["policyId"] = decision.PolicyId }
        });
    }

    private async Task QueueAuditAsync(string action, Guid resourceId, object? before, object? after)
    {
        await _unitOfWork.Repository<AuditLog>().AddAsync(new AuditLog
        {
            TenantId = GetTenantId(),
            UserId = _currentUser.UserId,
            Username = string.IsNullOrWhiteSpace(_currentUser.Username) ? "Unknown" : _currentUser.Username,
            Action = action,
            Resource = "InventoryItemIdentifier",
            ResourceId = resourceId.ToString(),
            OldValues = before is null ? null : JsonSerializer.Serialize(before),
            NewValues = after is null ? null : JsonSerializer.Serialize(after),
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown",
            UserAgent = Request.Headers.UserAgent.ToString(),
            Timestamp = DateTime.UtcNow
        });
    }

    private Guid GetTenantId()
    {
        if (!TryGetTenantId(out var tenantId))
        {
            throw new UnauthorizedAccessException("A tenant-scoped token is required for inventory identifier access.");
        }
        return tenantId;
    }

    private bool TryGetTenantId(out Guid tenantId)
    {
        var claim = User.FindFirst("tenant_id")?.Value;
        return Guid.TryParse(claim, out tenantId) && tenantId != Guid.Empty;
    }

    private static InventoryItemDto ToDto(InventoryItem item) => new()
    {
        Id = item.Id,
        ItemCode = item.ItemCode,
        Name = item.Name,
        Description = item.Description,
        UnitOfMeasure = item.UnitOfMeasure,
        CategoryId = item.CategoryId,
        ItemType = item.ItemType,
        Status = item.Status,
        IsActive = item.Status == ItemStatus.Active,
        Barcode = item.Barcode,
        AlternateBarcode = item.AlternateBarcode,
        QRCode = item.QRCode
    };

    private ObjectResult IdentifierConflict(InventoryIdentifierConflictException ex) => Conflict(new ProblemDetails
    {
        Status = 409,
        Title = "Duplicate inventory identifier",
        Detail = ex.Message,
        Extensions = { ["code"] = "INVENTORY_IDENTIFIER_DUPLICATE", ["identifier"] = ex.Identifier }
    });

    private static void AppendCsvRow(StringBuilder builder, params string?[] values) =>
        builder.AppendLine(string.Join(',', values.Select(EscapeCsv)));

    private static string EscapeCsv(string? value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        var safe = IsFormulaPrefix(value[0]) ? $"'{value}" : value;
        return $"\"{safe.Replace("\"", "\"\"")}\"";
    }

    private static string? ImportValue(string value)
    {
        var trimmed = value.Trim();
        if (trimmed.Length > 1 && trimmed[0] == '\'' && IsFormulaPrefix(trimmed[1]))
        {
            trimmed = trimmed[1..];
        }
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }

    private static bool IsFormulaPrefix(char value) => value is '=' or '+' or '-' or '@';

    private static decimal ParseConversion(string value, int lineNumber)
    {
        if (string.IsNullOrWhiteSpace(value)) return 1m;
        if (!decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed) || parsed <= 0)
        {
            throw new InvalidDataException($"Line {lineNumber}: ConversionToBase must be a positive decimal.");
        }
        return parsed;
    }

    private static List<string> ParseCsvLine(string line)
    {
        var values = new List<string>();
        var current = new StringBuilder();
        var quoted = false;
        for (var index = 0; index < line.Length; index++)
        {
            var character = line[index];
            if (character == '"')
            {
                if (quoted && index + 1 < line.Length && line[index + 1] == '"')
                {
                    current.Append('"');
                    index++;
                }
                else
                {
                    quoted = !quoted;
                }
            }
            else if (character == ',' && !quoted)
            {
                values.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(character);
            }
        }
        if (quoted) throw new InvalidDataException("The CSV contains an unterminated quoted value.");
        values.Add(current.ToString());
        return values;
    }

    private static void ValidateBatchDuplicates(ImportState state)
    {
        var owners = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var group in state.Rows.GroupBy(row => row.ItemCode, StringComparer.OrdinalIgnoreCase))
        {
            var itemIdentifierSets = group
                .Select(row => (row.Barcode, row.AlternateBarcode, row.QRCode))
                .Distinct()
                .Count();
            if (itemIdentifierSets > 1)
            {
                state.Errors.Add($"Item {group.Key} has inconsistent item identifiers across its CSV rows.");
            }

            foreach (var repeatedUnit in group.Where(row => !string.IsNullOrWhiteSpace(row.UnitCode))
                         .GroupBy(row => row.UnitCode!, StringComparer.OrdinalIgnoreCase)
                         .Where(unitGroup => unitGroup.Count() > 1))
            {
                state.Errors.Add($"Item {group.Key} contains more than one row for unit {repeatedUnit.Key}.");
            }

            var itemOwner = $"item {group.Key}";
            foreach (var identifier in new[] { group.First().Barcode, group.First().AlternateBarcode, group.First().QRCode }.Where(value => value is not null))
            {
                AddOwner(identifier!, itemOwner, group.First().LineNumber, owners, state.Errors);
            }
            foreach (var row in group.Where(value => value.UnitBarcode is not null))
            {
                AddOwner(row.UnitBarcode!, $"item {group.Key} unit {row.UnitCode}", row.LineNumber, owners, state.Errors);
            }
        }
    }

    private static void AddOwner(string identifier, string owner, int line, IDictionary<string, string> owners, ICollection<string> errors)
    {
        if (owners.TryGetValue(identifier, out var existing) && !string.Equals(existing, owner, StringComparison.OrdinalIgnoreCase))
        {
            errors.Add($"Line {line}: identifier '{identifier}' is assigned to both {existing} and {owner}.");
            return;
        }
        owners[identifier] = owner;
    }

    internal sealed record ImportRow(
        int LineNumber,
        string ItemCode,
        string? Barcode,
        string? AlternateBarcode,
        string? QRCode,
        string? UnitCode,
        string? UnitBarcode,
        decimal ConversionToBase);

    private sealed class ImportState : InventoryIdentifierImportResultDto
    {
        internal List<ImportRow> Rows { get; } = [];
    }
}
