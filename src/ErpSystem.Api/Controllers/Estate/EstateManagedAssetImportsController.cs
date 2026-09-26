using System.Globalization;
using System.Text.Json;
using ClosedXML.Excel;
using ErpSystem.Core.Entities.Estate;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Controllers.Estate;

[ApiController]
[Route("api/estate/managed-assets/imports")]
[Authorize(Roles = "admin,Admin,SystemAdmin,SuperAdmin,TenantAdmin,Estate Officer,Estate Manager,Property Manager,Land Registry Officer")]
public sealed class EstateManagedAssetImportsController : ControllerBase
{
    private const int MaxRows = 5000;
    private const long MaxFileBytes = 10 * 1024 * 1024;
    private static readonly (string Key, string Label)[] Fields =
    [
        ("assetCode", "Asset code"), ("recordType", "Record type"), ("name", "Name"),
        ("status", "Occupancy status"), ("location", "Location"), ("description", "Description"),
        ("unitType", "Unit type"), ("projectCode", "Project code"), ("blockName", "Block"),
        ("floorLabel", "Floor"), ("propertyFileReference", "Property file reference"),
        ("purpose", "Purpose"), ("zoningClassification", "Zoning classification"),
        ("planningComplianceStatus", "Planning compliance status"), ("gisLayerReference", "GIS layer reference"),
        ("cadastreDescription", "Cadastre description"), ("region", "Region"),
        ("district", "District"), ("town", "Town"), ("areaValue", "Area"),
        ("areaUnit", "Area unit"), ("surveyorName", "Surveyor"), ("surveyDate", "Survey date"),
        ("surveyPlanNumber", "Survey plan number"), ("mapSheetNumber", "Map sheet number"),
        ("beaconCount", "Beacon count"), ("boundaryCoordinates", "Boundary coordinates"),
        ("valuationAmount", "Land bank cost / valuation"), ("currency", "Currency"),
        ("ownerName", "Current owner"), ("ownershipType", "Ownership type"),
        ("interestHeld", "Interest held"), ("identificationType", "Owner ID type"),
        ("identificationNumber", "Owner ID number"), ("contactNumber", "Owner contact"),
        ("ownerAddress", "Owner address"), ("ownershipStartDate", "Ownership start date"),
        ("ownershipPercentage", "Ownership percentage"), ("notes", "Notes")
    ];

    private static readonly string[] LandRequired =
    [
        "purpose", "zoningClassification", "planningComplianceStatus", "gisLayerReference",
        "cadastreDescription", "region", "district", "town", "areaValue", "areaUnit",
        "surveyorName", "surveyDate", "surveyPlanNumber", "mapSheetNumber", "beaconCount",
        "boundaryCoordinates", "valuationAmount", "ownerName", "ownershipType", "interestHeld",
        "identificationType", "identificationNumber", "contactNumber", "ownerAddress",
        "ownershipStartDate", "ownershipPercentage"
    ];
    private static readonly Dictionary<string, int> MaxLengths = new()
    {
        ["description"] = 2000, ["unitType"] = 120, ["projectCode"] = 80,
        ["blockName"] = 120, ["floorLabel"] = 120, ["propertyFileReference"] = 120,
        ["purpose"] = 200, ["zoningClassification"] = 120, ["planningComplianceStatus"] = 80,
        ["gisLayerReference"] = 160, ["cadastreDescription"] = 240, ["region"] = 120,
        ["district"] = 120, ["town"] = 120, ["areaUnit"] = 40,
        ["surveyorName"] = 160, ["surveyPlanNumber"] = 120, ["mapSheetNumber"] = 120,
        ["boundaryCoordinates"] = 4000, ["currency"] = 10, ["notes"] = 2000
    };

    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public EstateManagedAssetImportsController(ApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    [HttpGet("template")]
    public IActionResult Template([FromQuery] string type = "property")
    {
        var isLand = type.Equals("land", StringComparison.OrdinalIgnoreCase);
        var keys = isLand
            ? new[] { "assetCode", "recordType", "name", "location", "currency", "notes" }.Concat(LandRequired)
            : new[] { "assetCode", "recordType", "name", "status", "location", "unitType", "projectCode",
                "blockName", "floorLabel", "propertyFileReference", "areaValue", "areaUnit", "valuationAmount", "currency", "notes" };
        using var document = new XLWorkbook();
        var worksheet = document.AddWorksheet(isLand ? "Land" : "Properties");
        var headerKeys = keys.Distinct().ToList();
        for (var index = 0; index < headerKeys.Count; index++)
            worksheet.Cell(1, index + 1).Value = headerKeys[index];
        worksheet.Row(1).Style.Font.Bold = true;
        worksheet.SheetView.FreezeRows(1);
        worksheet.Columns().AdjustToContents();
        using var output = new MemoryStream();
        document.SaveAs(output);
        return File(output.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            isLand ? "land-bank-import-template.xlsx" : "property-import-template.xlsx");
    }

    [HttpPost("inspect")]
    [RequestSizeLimit(MaxFileBytes)]
    public async Task<IActionResult> Inspect([FromForm] IFormFile file)
    {
        var workbook = await OpenWorkbookAsync(file);
        if (workbook.Error != null) return BadRequest(new { message = workbook.Error });
        using var document = workbook.Document!;
        var headers = ReadHeaders(document.Worksheet(1));
        if (headers.Count == 0) return BadRequest(new { message = "The first worksheet needs column headers in row 1." });
        if (headers.Count != headers.Distinct(StringComparer.OrdinalIgnoreCase).Count())
            return BadRequest(new { message = "Column headers must be unique." });

        var suggestions = Fields
            .Select(field => (field.Key, Header: headers.FirstOrDefault(header =>
                Normalize(header) == Normalize(field.Key) || Normalize(header) == Normalize(field.Label))))
            .Where(item => item.Header != null)
            .ToDictionary(item => item.Key, item => item.Header!);
        return Ok(new { success = true, headers, fields = Fields.Select(item => new { key = item.Key, label = item.Label }), suggestions });
    }

    [HttpPost("preview")]
    [RequestSizeLimit(MaxFileBytes)]
    public async Task<IActionResult> Preview([FromForm] IFormFile file, [FromForm] string mapping)
    {
        var result = await ValidateAsync(file, mapping);
        return Ok(new { success = result.Errors.Count == 0, rowCount = result.Rows.Count, errors = result.Errors });
    }

    [HttpPost("commit")]
    [RequestSizeLimit(MaxFileBytes)]
    public async Task<IActionResult> Commit([FromForm] IFormFile file, [FromForm] string mapping)
    {
        var result = await ValidateAsync(file, mapping);
        if (result.Errors.Count > 0)
            return BadRequest(new { success = false, message = "Correct the spreadsheet and upload it again.", errors = result.Errors });

        var tenantId = _currentUser.TenantId;
        if (!tenantId.HasValue || tenantId.Value == Guid.Empty) return Forbid();
        await using var transaction = _db.Database.IsRelational()
            ? await _db.Database.BeginTransactionAsync()
            : null;
        var codes = result.Rows.Select(row => row.AssetCode).ToList();
        if (await _db.EstateManagedAssets.AnyAsync(item => item.TenantId == tenantId && !item.IsDeleted && codes.Contains(item.AssetCode)))
            return Conflict(new { success = false, message = "An asset code was imported by another user. Preview the spreadsheet again." });

        _db.EstateManagedAssets.AddRange(result.Rows);
        try
        {
            await _db.SaveChangesAsync();
            if (transaction != null) await transaction.CommitAsync();
        }
        catch (DbUpdateException)
        {
            return Conflict(new { success = false, message = "An asset code already exists. Preview the spreadsheet again." });
        }
        return Ok(new { success = true, importedCount = result.Rows.Count });
    }

    private async Task<(List<EstateManagedAsset> Rows, List<string> Errors)> ValidateAsync(IFormFile file, string mappingJson)
    {
        var errors = new List<string>();
        var rows = new List<EstateManagedAsset>();
        var workbook = await OpenWorkbookAsync(file);
        if (workbook.Error != null) return (rows, [workbook.Error]);
        using var document = workbook.Document!;
        var worksheet = document.Worksheet(1);
        var headers = ReadHeaders(worksheet);
        if (headers.Count != headers.Distinct(StringComparer.OrdinalIgnoreCase).Count())
            return (rows, ["Column headers must be unique."]);
        Dictionary<string, string>? mapping;
        try { mapping = JsonSerializer.Deserialize<Dictionary<string, string>>(mappingJson); }
        catch (JsonException) { return (rows, ["Column mapping is invalid."]); }
        if (mapping == null) return (rows, ["Map the spreadsheet columns first."]);
        if (mapping.Keys.Except(Fields.Select(field => field.Key)).Any()) errors.Add("Column mapping contains an unknown field.");
        if (mapping.Values.Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.OrdinalIgnoreCase).Count()
            != mapping.Values.Count(value => !string.IsNullOrWhiteSpace(value)))
            errors.Add("Each spreadsheet column can map to only one field.");
        if (mapping.Values.Any(value => !string.IsNullOrWhiteSpace(value) && !headers.Contains(value, StringComparer.OrdinalIgnoreCase)))
            errors.Add("A mapped column is missing from the spreadsheet.");
        foreach (var key in new[] { "assetCode", "recordType", "name", "location" })
            if (!mapping.TryGetValue(key, out var value) || string.IsNullOrWhiteSpace(value))
                errors.Add($"Map {Fields.First(field => field.Key == key).Label}.");
        if (errors.Count > 0) return (rows, errors);

        var columns = mapping.Where(item => !string.IsNullOrWhiteSpace(item.Value))
            .ToDictionary(item => item.Key, item => worksheet.Row(1).CellsUsed()
                .First(cell => cell.GetString().Trim().Equals(item.Value, StringComparison.OrdinalIgnoreCase)).Address.ColumnNumber);
        var tenantId = _currentUser.TenantId;
        if (!tenantId.HasValue || tenantId.Value == Guid.Empty) return (rows, ["Select a tenant before importing."]);
        var seenCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenSurveyPlans = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var lastRow = worksheet.LastRowUsed()?.RowNumber() ?? 1;
        if (lastRow - 1 > MaxRows) return (rows, [$"Import no more than {MaxRows} rows at a time."]);
        var existingCodes = await _db.EstateManagedAssets.AsNoTracking()
            .Where(item => item.TenantId == tenantId && !item.IsDeleted)
            .Select(item => item.AssetCode).ToListAsync();
        var existingCodeSet = existingCodes.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var existingSurveyPlans = await _db.EstateManagedAssets.AsNoTracking()
            .Where(item => item.TenantId == tenantId && !item.IsDeleted && item.AssetType == EstateManagedAssetType.Land && item.SurveyPlanNumber != null)
            .Select(item => item.SurveyPlanNumber!).ToListAsync();
        var existingSurveySet = existingSurveyPlans.ToHashSet(StringComparer.OrdinalIgnoreCase);

        for (var rowNumber = 2; rowNumber <= lastRow; rowNumber++)
        {
            var row = worksheet.Row(rowNumber);
            if (row.CellsUsed().All(cell => cell.IsEmpty())) continue;
            string Get(string key) => columns.TryGetValue(key, out var column) && !row.Cell(column).HasFormula
                ? row.Cell(column).GetFormattedString().Trim() : string.Empty;
            var rowErrors = new List<string>();
            foreach (var (key, column) in columns)
                if (row.Cell(column).HasFormula) rowErrors.Add($"{Fields.First(field => field.Key == key).Label} cannot contain a formula");
            var code = Get("assetCode").ToUpperInvariant();
            var name = Get("name");
            var location = Get("location");
            var recordType = Get("recordType");
            var land = recordType.Equals("Land", StringComparison.OrdinalIgnoreCase);
            var facility = recordType.Equals("Facility", StringComparison.OrdinalIgnoreCase);
            var property = new[] { "Property", "Apartment", "Flat", "Store", "Shop", "Office", "Warehouse", "Unit" }
                .Contains(recordType, StringComparer.OrdinalIgnoreCase);
            if (code.Length is 0 or > 80) rowErrors.Add("Asset code is required (maximum 80 characters)");
            else if (!seenCodes.Add(code) || existingCodeSet.Contains(code)) rowErrors.Add("Asset code already exists");
            if (name.Length is 0 or > 240) rowErrors.Add("Name is required (maximum 240 characters)");
            if (location.Length is 0 or > 500) rowErrors.Add("Location is required (maximum 500 characters)");
            if (!land && !facility && !property) rowErrors.Add("Record type must be Land, Property, Facility, Apartment, Store, Office, or another supported unit type");
            if (land)
            {
                foreach (var key in LandRequired)
                    if (string.IsNullOrWhiteSpace(Get(key))) rowErrors.Add($"{Fields.First(field => field.Key == key).Label} is required for land");
            }
            foreach (var (key, maximum) in MaxLengths)
                if (Get(key).Length > maximum) rowErrors.Add($"{Fields.First(field => field.Key == key).Label} exceeds {maximum} characters");
            var status = EstateManagedAssetStatus.LandBank;
            if (!land && (!Enum.TryParse(Get("status"), true, out status) || !Enum.IsDefined(status)))
                rowErrors.Add("Occupancy status must be Available, Occupied, Reserved, Leased, Sold, UnderMaintenance, Blocked, or Retired");
            if (!land && status == EstateManagedAssetStatus.LandBank) rowErrors.Add("LandBank status is only for land");
            var area = ParsePositiveDecimal(Get("areaValue"), "Area", rowErrors, land);
            var areaSquareMeters = area.HasValue ? ConvertAreaToSquareMeters(area.Value, Get("areaUnit")) : null;
            if (area.HasValue && areaSquareMeters == null)
                rowErrors.Add("Area unit must be acres, hectares, square feet, or square metres");
            var valuation = ParsePositiveDecimal(Get("valuationAmount"), "Land bank cost / valuation", rowErrors, land);
            var beaconCount = ParsePositiveInt(Get("beaconCount"), "Beacon count", rowErrors, land);
            if (land && beaconCount is < 3) rowErrors.Add("Beacon count must be at least 3");
            var percentage = ParsePositiveDecimal(Get("ownershipPercentage"), "Ownership percentage", rowErrors, land);
            if (land && percentage is > 100) rowErrors.Add("Ownership percentage cannot exceed 100");
            var surveyDate = ParseDate(Get("surveyDate"), "Survey date", rowErrors, land);
            var ownerStart = ParseDate(Get("ownershipStartDate"), "Ownership start date", rowErrors, land);
            var surveyPlan = Get("surveyPlanNumber");
            if (land && surveyPlan.Length > 0 && (!seenSurveyPlans.Add(surveyPlan) || existingSurveySet.Contains(surveyPlan)))
                rowErrors.Add("Survey plan number already exists");
            if (land && Get("areaUnit").Length == 0) rowErrors.Add("Area unit is required for land");
            if (rowErrors.Count > 0)
            {
                errors.AddRange(rowErrors.Select(error => $"Row {rowNumber}: {error}."));
                continue;
            }

            var owner = land ? new[] { new
            {
                OwnerName = Get("ownerName"), OwnershipType = Get("ownershipType"), InterestHeld = Get("interestHeld"),
                IdentificationType = Get("identificationType"), IdentificationNumber = Get("identificationNumber"),
                ContactNumber = Get("contactNumber"), Address = Get("ownerAddress"),
                OwnershipStartDate = ownerStart, OwnershipEndDate = (DateTime?)null,
                OwnershipPercentage = percentage!.Value, IsCurrentOwner = true
            }} : null;
            var unitType = Get("unitType");
            rows.Add(new EstateManagedAsset
            {
                Id = Guid.NewGuid(), TenantId = tenantId.Value, AssetCode = code, Name = name,
                AssetType = land ? EstateManagedAssetType.Land : facility ? EstateManagedAssetType.Facility : EstateManagedAssetType.Property,
                Status = land ? EstateManagedAssetStatus.LandBank : status, SourceType = EstateManagedAssetSourceType.Imported,
                Location = location, Description = Get("description"), UnitType = unitType.Length > 0 ? unitType : land || facility || recordType == "Property" ? null : recordType,
                ProjectCode = Get("projectCode"), BlockName = Get("blockName"), FloorLabel = Get("floorLabel"),
                PropertyFileReference = Get("propertyFileReference"), Purpose = Get("purpose"),
                ZoningClassification = Get("zoningClassification"), PlanningComplianceStatus = Get("planningComplianceStatus"),
                GisLayerReference = Get("gisLayerReference"), CadastreDescription = Get("cadastreDescription"),
                Region = Get("region"), District = Get("district"), Town = Get("town"), AreaValue = area,
                AreaSquareMeters = areaSquareMeters,
                AreaUnit = Get("areaUnit"), SurveyorName = Get("surveyorName"), SurveyDate = surveyDate,
                SurveyPlanNumber = surveyPlan, MapSheetNumber = Get("mapSheetNumber"), BeaconCount = beaconCount,
                BoundaryCoordinates = Get("boundaryCoordinates"), BoundaryVerified = false,
                ValuationAmount = valuation, TotalCapitalizedCost = land ? valuation : null,
                Currency = string.IsNullOrWhiteSpace(Get("currency")) ? "GHS" : Get("currency").ToUpperInvariant(),
                OwnershipHistoryJson = owner == null ? null : JsonSerializer.Serialize(owner), Notes = Get("notes"),
                IsReadyForProjectManagement = false, IsAvailableForLease = false, IsAvailableForSale = false,
                IsPublishedToExternalPortal = false, ExternalListingType = "None", ExternalListingStatus = "Draft",
                CreatedBy = _currentUser.UserName,
                CreatedById = Guid.TryParse(_currentUser.UserId, out var userId) ? userId : null
            });
        }
        if (rows.Count == 0 && errors.Count == 0) errors.Add("The first worksheet has no data rows.");
        return (rows, errors);
    }

    private static decimal? ParsePositiveDecimal(string value, string label, List<string> errors, bool required)
    {
        if (value.Length == 0) return null;
        if (decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var number) && number > 0) return number;
        errors.Add($"{label} must be a positive number");
        return null;
    }

    private static int? ParsePositiveInt(string value, string label, List<string> errors, bool required)
    {
        if (value.Length == 0) return null;
        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) && number > 0) return number;
        errors.Add($"{label} must be a positive whole number");
        return null;
    }

    private static DateTime? ParseDate(string value, string label, List<string> errors, bool required)
    {
        if (value.Length == 0) return null;
        if (DateTime.TryParse(value, CultureInfo.GetCultureInfo("en-GB"), DateTimeStyles.None, out var date)
            || DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out date)) return date;
        errors.Add($"{label} must be a valid date");
        return null;
    }

    private static decimal? ConvertAreaToSquareMeters(decimal value, string unit) => unit.Trim().ToLowerInvariant() switch
    {
        "sq ft" or "sqft" or "square feet" => value * 0.09290304m,
        "acres" or "acre" => value * 4046.8564224m,
        "hectares" or "hectare" or "ha" => value * 10000m,
        "sqm" or "sq m" or "square metres" or "square meters" => value,
        _ => null
    };

    private static string Normalize(string value) => new(value.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());

    private static List<string> ReadHeaders(IXLWorksheet worksheet) => worksheet.Row(1).CellsUsed()
        .Select(cell => cell.GetString().Trim()).Where(value => value.Length > 0).ToList();

    private static async Task<(XLWorkbook? Document, string? Error)> OpenWorkbookAsync(IFormFile file)
    {
        if (file == null || file.Length == 0) return (null, "Choose an Excel workbook.");
        if (file.Length > MaxFileBytes) return (null, "The workbook must be 10 MB or smaller.");
        if (!Path.GetExtension(file.FileName).Equals(".xlsx", StringComparison.OrdinalIgnoreCase))
            return (null, "Upload an .xlsx workbook.");
        try
        {
            await using var input = file.OpenReadStream();
            var buffer = new MemoryStream();
            await input.CopyToAsync(buffer);
            buffer.Position = 0;
            var document = new XLWorkbook(buffer);
            return document.Worksheets.Count == 0 ? (null, "The workbook has no worksheets.") : (document, null);
        }
        catch (Exception ex) when (ex is InvalidDataException or ArgumentException or System.IO.FileFormatException)
        {
            return (null, "The Excel workbook could not be opened.");
        }
    }
}
