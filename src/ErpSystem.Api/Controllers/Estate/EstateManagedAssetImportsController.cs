using System.Globalization;
using System.Data;
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
        ("assetCode", "Asset code"), ("recordType", "Asset type"), ("name", "Name"),
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
    private static readonly Dictionary<string, string>[] LandTemplateSampleRows =
    [
        new()
        {
            ["assetCode"] = "LAND-IMP-001", ["recordType"] = "Land", ["name"] = "Amasaman Residential Plot 1",
            ["location"] = "Amasaman, Ga West, Greater Accra", ["currency"] = "GHS", ["notes"] = "Template sample land record",
            ["purpose"] = "Residential development", ["zoningClassification"] = "Residential", ["planningComplianceStatus"] = "Compliant",
            ["gisLayerReference"] = "GA-WEST-LAND-BANK", ["cadastreDescription"] = "Parcel bounded by access road and drainage reserve",
            ["region"] = "Greater Accra", ["district"] = "Ga West", ["town"] = "Amasaman", ["areaValue"] = "0.75",
            ["areaUnit"] = "acres", ["surveyorName"] = "Kwame Mensah", ["surveyDate"] = "2026-01-15",
            ["surveyPlanNumber"] = "SP/LAND/2026/001", ["mapSheetNumber"] = "MS-GA-001", ["beaconCount"] = "4",
            ["boundaryCoordinates"] = "5.7050,-0.3090;5.7060,-0.3090;5.7060,-0.3080;5.7050,-0.3080",
            ["valuationAmount"] = "180000", ["ownerName"] = "Amasaman Stool Lands", ["ownershipType"] = "Stool",
            ["interestHeld"] = "Allodial interest", ["identificationType"] = "Stool certificate", ["identificationNumber"] = "STL-AMA-001",
            ["contactNumber"] = "0240000001", ["ownerAddress"] = "Amasaman Traditional Council", ["ownershipStartDate"] = "2025-12-01",
            ["ownershipPercentage"] = "100"
        },
        new()
        {
            ["assetCode"] = "LAND-IMP-002", ["recordType"] = "Land", ["name"] = "Prampram Beachfront Parcel",
            ["location"] = "Prampram, Ningo-Prampram, Greater Accra", ["currency"] = "GHS", ["notes"] = "Template sample land record",
            ["purpose"] = "Mixed-use hospitality", ["zoningClassification"] = "Commercial", ["planningComplianceStatus"] = "Compliant",
            ["gisLayerReference"] = "NPR-LAND-BANK", ["cadastreDescription"] = "Beachfront parcel with approved access corridor",
            ["region"] = "Greater Accra", ["district"] = "Ningo-Prampram", ["town"] = "Prampram", ["areaValue"] = "1.20",
            ["areaUnit"] = "acres", ["surveyorName"] = "Akosua Addo", ["surveyDate"] = "2026-01-18",
            ["surveyPlanNumber"] = "SP/LAND/2026/002", ["mapSheetNumber"] = "MS-GA-002", ["beaconCount"] = "5",
            ["boundaryCoordinates"] = "5.7095,0.1070;5.7105,0.1070;5.7105,0.1080;5.7095,0.1080;5.7090,0.1075",
            ["valuationAmount"] = "420000", ["ownerName"] = "Ningo-Prampram Family Lands", ["ownershipType"] = "Family",
            ["interestHeld"] = "Freehold interest", ["identificationType"] = "Family resolution", ["identificationNumber"] = "FAM-NPR-002",
            ["contactNumber"] = "0240000002", ["ownerAddress"] = "Prampram Family House", ["ownershipStartDate"] = "2025-11-10",
            ["ownershipPercentage"] = "100"
        },
        new()
        {
            ["assetCode"] = "LAND-IMP-003", ["recordType"] = "Land", ["name"] = "Oyibi Affordable Housing Parcel",
            ["location"] = "Oyibi, Kpone Katamanso, Greater Accra", ["currency"] = "GHS", ["notes"] = "Template sample land record",
            ["purpose"] = "Affordable housing", ["zoningClassification"] = "Residential", ["planningComplianceStatus"] = "Compliant",
            ["gisLayerReference"] = "KKMA-HOUSING-LAND", ["cadastreDescription"] = "Regular parcel close to main Oyibi road",
            ["region"] = "Greater Accra", ["district"] = "Kpone Katamanso", ["town"] = "Oyibi", ["areaValue"] = "2.50",
            ["areaUnit"] = "acres", ["surveyorName"] = "Esi Boateng", ["surveyDate"] = "2026-01-22",
            ["surveyPlanNumber"] = "SP/LAND/2026/003", ["mapSheetNumber"] = "MS-GA-003", ["beaconCount"] = "6",
            ["boundaryCoordinates"] = "5.8130,-0.1180;5.8140,-0.1180;5.8145,-0.1168;5.8136,-0.1160;5.8128,-0.1165;5.8125,-0.1175",
            ["valuationAmount"] = "650000", ["ownerName"] = "Oyibi Development Family", ["ownershipType"] = "Family",
            ["interestHeld"] = "Leasehold interest", ["identificationType"] = "Indenture", ["identificationNumber"] = "IND-OYB-003",
            ["contactNumber"] = "0240000003", ["ownerAddress"] = "Oyibi Estate Road", ["ownershipStartDate"] = "2025-10-05",
            ["ownershipPercentage"] = "100"
        },
        new()
        {
            ["assetCode"] = "LAND-IMP-004", ["recordType"] = "Land", ["name"] = "Tema Industrial Buffer Land",
            ["location"] = "Tema Community 25, Tema, Greater Accra", ["currency"] = "GHS", ["notes"] = "Template sample land record",
            ["purpose"] = "Industrial expansion", ["zoningClassification"] = "Industrial", ["planningComplianceStatus"] = "Compliant",
            ["gisLayerReference"] = "TMA-IND-LAND", ["cadastreDescription"] = "Industrial buffer parcel near service road",
            ["region"] = "Greater Accra", ["district"] = "Tema Metropolitan", ["town"] = "Tema Community 25", ["areaValue"] = "1.80",
            ["areaUnit"] = "acres", ["surveyorName"] = "Yaw Appiah", ["surveyDate"] = "2026-02-03",
            ["surveyPlanNumber"] = "SP/LAND/2026/004", ["mapSheetNumber"] = "MS-GA-004", ["beaconCount"] = "4",
            ["boundaryCoordinates"] = "5.6800,-0.0100;5.6810,-0.0100;5.6810,-0.0085;5.6800,-0.0085",
            ["valuationAmount"] = "720000", ["ownerName"] = "Tema Industrial Lands", ["ownershipType"] = "State",
            ["interestHeld"] = "Vested interest", ["identificationType"] = "Allocation letter", ["identificationNumber"] = "AL-TMA-004",
            ["contactNumber"] = "0240000004", ["ownerAddress"] = "Tema Development Office", ["ownershipStartDate"] = "2025-09-12",
            ["ownershipPercentage"] = "100"
        },
        new()
        {
            ["assetCode"] = "LAND-IMP-005", ["recordType"] = "Land", ["name"] = "Kasoa Retail Site",
            ["location"] = "Kasoa, Awutu Senya East, Central", ["currency"] = "GHS", ["notes"] = "Template sample land record",
            ["purpose"] = "Retail centre", ["zoningClassification"] = "Commercial", ["planningComplianceStatus"] = "Compliant",
            ["gisLayerReference"] = "ASEMA-COM-LAND", ["cadastreDescription"] = "Corner parcel beside arterial road",
            ["region"] = "Central", ["district"] = "Awutu Senya East", ["town"] = "Kasoa", ["areaValue"] = "0.95",
            ["areaUnit"] = "acres", ["surveyorName"] = "Kofi Agyemang", ["surveyDate"] = "2026-02-08",
            ["surveyPlanNumber"] = "SP/LAND/2026/005", ["mapSheetNumber"] = "MS-CR-001", ["beaconCount"] = "4",
            ["boundaryCoordinates"] = "5.5340,-0.4250;5.5350,-0.4250;5.5350,-0.4240;5.5340,-0.4240",
            ["valuationAmount"] = "510000", ["ownerName"] = "Kasoa East Family", ["ownershipType"] = "Family",
            ["interestHeld"] = "Freehold interest", ["identificationType"] = "Family resolution", ["identificationNumber"] = "FAM-KAS-005",
            ["contactNumber"] = "0240000005", ["ownerAddress"] = "Old Barrier, Kasoa", ["ownershipStartDate"] = "2025-08-25",
            ["ownershipPercentage"] = "100"
        },
        new()
        {
            ["assetCode"] = "LAND-IMP-006", ["recordType"] = "Land", ["name"] = "Kumasi Warehouse Plot",
            ["location"] = "Ejisu, Ashanti", ["currency"] = "GHS", ["notes"] = "Template sample land record",
            ["purpose"] = "Warehouse development", ["zoningClassification"] = "Light industrial", ["planningComplianceStatus"] = "Compliant",
            ["gisLayerReference"] = "EJISU-WH-LAND", ["cadastreDescription"] = "Rectangular parcel near Kumasi-Accra highway",
            ["region"] = "Ashanti", ["district"] = "Ejisu", ["town"] = "Ejisu", ["areaValue"] = "1.40",
            ["areaUnit"] = "acres", ["surveyorName"] = "Ama Serwaa", ["surveyDate"] = "2026-02-14",
            ["surveyPlanNumber"] = "SP/LAND/2026/006", ["mapSheetNumber"] = "MS-AS-001", ["beaconCount"] = "4",
            ["boundaryCoordinates"] = "6.7200,-1.3600;6.7210,-1.3600;6.7210,-1.3588;6.7200,-1.3588",
            ["valuationAmount"] = "390000", ["ownerName"] = "Ejisu Stool Lands", ["ownershipType"] = "Stool",
            ["interestHeld"] = "Allodial interest", ["identificationType"] = "Stool certificate", ["identificationNumber"] = "STL-EJI-006",
            ["contactNumber"] = "0240000006", ["ownerAddress"] = "Ejisu Palace", ["ownershipStartDate"] = "2025-08-01",
            ["ownershipPercentage"] = "100"
        },
        new()
        {
            ["assetCode"] = "LAND-IMP-007", ["recordType"] = "Land", ["name"] = "Takoradi Logistics Yard",
            ["location"] = "Apowa, Effia-Kwesimintsim, Western", ["currency"] = "GHS", ["notes"] = "Template sample land record",
            ["purpose"] = "Logistics yard", ["zoningClassification"] = "Industrial", ["planningComplianceStatus"] = "Compliant",
            ["gisLayerReference"] = "EKMA-LOG-LAND", ["cadastreDescription"] = "Serviced parcel with truck access",
            ["region"] = "Western", ["district"] = "Effia-Kwesimintsim", ["town"] = "Apowa", ["areaValue"] = "2.10",
            ["areaUnit"] = "acres", ["surveyorName"] = "Nana Owusu", ["surveyDate"] = "2026-02-20",
            ["surveyPlanNumber"] = "SP/LAND/2026/007", ["mapSheetNumber"] = "MS-WR-001", ["beaconCount"] = "6",
            ["boundaryCoordinates"] = "4.9400,-1.7900;4.9410,-1.7900;4.9415,-1.7888;4.9406,-1.7880;4.9398,-1.7886;4.9395,-1.7895",
            ["valuationAmount"] = "580000", ["ownerName"] = "Apowa Lands Secretariat", ["ownershipType"] = "Stool",
            ["interestHeld"] = "Leasehold interest", ["identificationType"] = "Lease document", ["identificationNumber"] = "LSE-APW-007",
            ["contactNumber"] = "0240000007", ["ownerAddress"] = "Apowa Chief Palace", ["ownershipStartDate"] = "2025-07-18",
            ["ownershipPercentage"] = "100"
        },
        new()
        {
            ["assetCode"] = "LAND-IMP-008", ["recordType"] = "Land", ["name"] = "Tamale Service Station Land",
            ["location"] = "Sagnarigu, Northern", ["currency"] = "GHS", ["notes"] = "Template sample land record",
            ["purpose"] = "Service station", ["zoningClassification"] = "Commercial", ["planningComplianceStatus"] = "Compliant",
            ["gisLayerReference"] = "SAG-COM-LAND", ["cadastreDescription"] = "Roadside parcel with setback clearance",
            ["region"] = "Northern", ["district"] = "Sagnarigu", ["town"] = "Sagnarigu", ["areaValue"] = "0.65",
            ["areaUnit"] = "acres", ["surveyorName"] = "Issah Mohammed", ["surveyDate"] = "2026-03-02",
            ["surveyPlanNumber"] = "SP/LAND/2026/008", ["mapSheetNumber"] = "MS-NR-001", ["beaconCount"] = "4",
            ["boundaryCoordinates"] = "9.4320,-0.8420;9.4330,-0.8420;9.4330,-0.8410;9.4320,-0.8410",
            ["valuationAmount"] = "210000", ["ownerName"] = "Sagnarigu Family Lands", ["ownershipType"] = "Family",
            ["interestHeld"] = "Leasehold interest", ["identificationType"] = "Indenture", ["identificationNumber"] = "IND-SAG-008",
            ["contactNumber"] = "0240000008", ["ownerAddress"] = "Sagnarigu Township", ["ownershipStartDate"] = "2025-06-30",
            ["ownershipPercentage"] = "100"
        },
        new()
        {
            ["assetCode"] = "LAND-IMP-009", ["recordType"] = "Land", ["name"] = "Ho Staff Housing Land",
            ["location"] = "Ho, Volta", ["currency"] = "GHS", ["notes"] = "Template sample land record",
            ["purpose"] = "Staff housing", ["zoningClassification"] = "Residential", ["planningComplianceStatus"] = "Compliant",
            ["gisLayerReference"] = "HO-RES-LAND", ["cadastreDescription"] = "Gently sloped residential parcel",
            ["region"] = "Volta", ["district"] = "Ho Municipal", ["town"] = "Ho", ["areaValue"] = "1.10",
            ["areaUnit"] = "acres", ["surveyorName"] = "Selorm Dzamesi", ["surveyDate"] = "2026-03-10",
            ["surveyPlanNumber"] = "SP/LAND/2026/009", ["mapSheetNumber"] = "MS-VR-001", ["beaconCount"] = "5",
            ["boundaryCoordinates"] = "6.6120,0.4700;6.6130,0.4700;6.6132,0.4710;6.6123,0.4714;6.6118,0.4708",
            ["valuationAmount"] = "260000", ["ownerName"] = "Ho Municipal Lands", ["ownershipType"] = "State",
            ["interestHeld"] = "Vested interest", ["identificationType"] = "Allocation letter", ["identificationNumber"] = "AL-HO-009",
            ["contactNumber"] = "0240000009", ["ownerAddress"] = "Ho Municipal Assembly", ["ownershipStartDate"] = "2025-06-01",
            ["ownershipPercentage"] = "100"
        },
        new()
        {
            ["assetCode"] = "LAND-IMP-010", ["recordType"] = "Land", ["name"] = "Cape Coast Residential Enclave",
            ["location"] = "Abura, Cape Coast, Central", ["currency"] = "GHS", ["notes"] = "Template sample land record",
            ["purpose"] = "Residential enclave", ["zoningClassification"] = "Residential", ["planningComplianceStatus"] = "Compliant",
            ["gisLayerReference"] = "CCMA-RES-LAND", ["cadastreDescription"] = "Residential parcel near existing utility corridor",
            ["region"] = "Central", ["district"] = "Cape Coast Metropolitan", ["town"] = "Abura", ["areaValue"] = "1.55",
            ["areaUnit"] = "acres", ["surveyorName"] = "Joseph Eshun", ["surveyDate"] = "2026-03-18",
            ["surveyPlanNumber"] = "SP/LAND/2026/010", ["mapSheetNumber"] = "MS-CR-002", ["beaconCount"] = "4",
            ["boundaryCoordinates"] = "5.1200,-1.2500;5.1210,-1.2500;5.1210,-1.2487;5.1200,-1.2487",
            ["valuationAmount"] = "340000", ["ownerName"] = "Abura Family Lands", ["ownershipType"] = "Family",
            ["interestHeld"] = "Freehold interest", ["identificationType"] = "Family resolution", ["identificationNumber"] = "FAM-ABR-010",
            ["contactNumber"] = "0240000010", ["ownerAddress"] = "Abura, Cape Coast", ["ownershipStartDate"] = "2025-05-20",
            ["ownershipPercentage"] = "100"
        }
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
        if (isLand) AddLandTemplateSampleRows(worksheet, headerKeys);
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
        try
        {
            var executionStrategy = _db.Database.CreateExecutionStrategy();
            await executionStrategy.ExecuteAsync(async () =>
            {
                await using var transaction = _db.Database.IsRelational()
                    ? await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable)
                    : null;
                var codes = result.Rows.Select(row => row.AssetCode).ToList();
                if (await _db.EstateManagedAssets.AnyAsync(item => item.TenantId == tenantId && !item.IsDeleted && codes.Contains(item.AssetCode)))
                    throw new ImportConflictException("An asset code was imported by another user. Preview the spreadsheet again.");
                var existingNames = await _db.EstateManagedAssets.AsNoTracking()
                    .Where(item => item.TenantId == tenantId && !item.IsDeleted)
                    .Select(item => item.Name).ToListAsync();
                var existingNameSet = existingNames.Select(NormalizeAssetName)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
                if (result.Rows.Any(row => existingNameSet.Contains(NormalizeAssetName(row.Name))))
                    throw new ImportConflictException("An asset name already exists in the register. Preview the spreadsheet again.");

                _db.EstateManagedAssets.AddRange(result.Rows);
                await _db.SaveChangesAsync();
                if (transaction != null) await transaction.CommitAsync();
            });
        }
        catch (ImportConflictException ex)
        {
            return Conflict(new { success = false, message = ex.Message });
        }
        catch (DbUpdateException)
        {
            return Conflict(new { success = false, message = "An asset code or name already exists. Preview the spreadsheet again." });
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
        var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenSurveyPlans = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var lastRow = worksheet.LastRowUsed()?.RowNumber() ?? 1;
        if (lastRow - 1 > MaxRows) return (rows, [$"Import no more than {MaxRows} rows at a time."]);
        var existingCodes = await _db.EstateManagedAssets.AsNoTracking()
            .Where(item => item.TenantId == tenantId && !item.IsDeleted)
            .Select(item => item.AssetCode).ToListAsync();
        var existingCodeSet = existingCodes.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var existingNames = await _db.EstateManagedAssets.AsNoTracking()
            .Where(item => item.TenantId == tenantId && !item.IsDeleted)
            .Select(item => item.Name).ToListAsync();
        var existingNameSet = existingNames.Select(NormalizeAssetName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
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
            var property = recordType.Equals("Property", StringComparison.OrdinalIgnoreCase);
            if (code.Length is 0 or > 80) rowErrors.Add("Asset code is required (maximum 80 characters)");
            else if (!seenCodes.Add(code) || existingCodeSet.Contains(code)) rowErrors.Add("Asset code already exists");
            if (name.Length is 0 or > 240) rowErrors.Add("Name is required (maximum 240 characters)");
            else if (!seenNames.Add(NormalizeAssetName(name)) || existingNameSet.Contains(NormalizeAssetName(name)))
                rowErrors.Add("Name already exists in this workbook or the asset register");
            if (location.Length is 0 or > 500) rowErrors.Add("Location is required (maximum 500 characters)");
            if (!land && !facility && !property) rowErrors.Add("Asset type must be Land, Property, or Facility; put Apartment, Office, Shop, or Warehouse in Unit type");
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
                Location = location, Description = Get("description"), UnitType = unitType.Length > 0 ? unitType : null,
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

    private static string NormalizeAssetName(string value) =>
        string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    private static void AddLandTemplateSampleRows(IXLWorksheet worksheet, IReadOnlyList<string> headerKeys)
    {
        for (var rowIndex = 0; rowIndex < LandTemplateSampleRows.Length; rowIndex++)
        {
            var sample = LandTemplateSampleRows[rowIndex];
            for (var columnIndex = 0; columnIndex < headerKeys.Count; columnIndex++)
            {
                if (sample.TryGetValue(headerKeys[columnIndex], out var value))
                    worksheet.Cell(rowIndex + 2, columnIndex + 1).Value = value;
            }
        }
    }

    private static List<string> ReadHeaders(IXLWorksheet worksheet) => worksheet.Row(1).CellsUsed()
        .Select(cell => cell.GetString().Trim()).Where(value => value.Length > 0).ToList();

    private sealed class ImportConflictException(string message) : Exception(message);

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
