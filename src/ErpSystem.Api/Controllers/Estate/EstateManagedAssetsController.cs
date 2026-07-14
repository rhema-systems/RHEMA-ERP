using ErpSystem.Core.DTOs.Estate;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Estate;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Estate;

[ApiController]
[Route("api/estate/managed-assets")]
[Authorize]
public sealed class EstateManagedAssetsController : ControllerBase
{
    private readonly IEstateManagedAssetService _managedAssetService;
    private readonly IFileStorageService _fileStorageService;

    public EstateManagedAssetsController(IEstateManagedAssetService managedAssetService, IFileStorageService fileStorageService)
    {
        _managedAssetService = managedAssetService;
        _fileStorageService = fileStorageService;
    }

    [HttpGet]
    public async Task<IActionResult> GetManagedAssets(
        [FromQuery] EstateManagedAssetType? assetType = null,
        [FromQuery] EstateManagedAssetStatus? status = null,
        [FromQuery] string? search = null,
        [FromQuery] bool? availableForLease = null,
        [FromQuery] bool? availableForSale = null,
        [FromQuery] int take = 100)
    {
        var assets = await _managedAssetService.GetManagedAssetsAsync(new EstateManagedAssetQuery
        {
            AssetType = assetType,
            Status = status,
            Search = search,
            AvailableForLease = availableForLease,
            AvailableForSale = availableForSale,
            Take = take
        });

        return Ok(new
        {
            success = true,
            data = assets
        });
    }

    [HttpPost("manual-land")]
    [Authorize(Roles = "admin,Admin,SystemAdmin,SuperAdmin,TenantAdmin,Estate Officer,Estate Manager,Land Registry Officer")]
    public async Task<IActionResult> CreateManualLand([FromBody] CreateManualExistingLandDto request)
    {
        try
        {
            var asset = await _managedAssetService.CreateManualExistingLandAsync(request);
            return Ok(new { success = true, data = asset, message = "Existing land added to the land bank." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    [HttpPost("{id:guid}/ready-for-project-management")]
    [Authorize(Roles = "admin,Admin,SystemAdmin,SuperAdmin,TenantAdmin,Estate Officer,Estate Manager,Project Manager")]
    public async Task<IActionResult> MarkReadyForProjectManagement(Guid id)
    {
        try
        {
            var asset = await _managedAssetService.MarkReadyForProjectManagementAsync(id);
            return Ok(new { success = true, data = asset, message = "Land is ready for project management." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    [HttpGet("{id:guid}/documents")]
    public async Task<IActionResult> GetDocuments(Guid id)
        => Ok(new { success = true, data = await _managedAssetService.GetDocumentsAsync(id) });

    [HttpPost("{id:guid}/documents")]
    [Authorize(Roles = "admin,Admin,SystemAdmin,SuperAdmin,TenantAdmin,Estate Officer,Estate Manager,Land Registry Officer,Survey Officer")]
    [RequestSizeLimit(52_428_800)]
    public async Task<IActionResult> UploadDocument(
        Guid id,
        [FromForm] IFormFile file,
        [FromForm] string documentType,
        [FromForm] string? documentName)
    {
        if (file == null || file.Length == 0) return BadRequest(new { success = false, message = "Select a document to upload." });
        var folder = $"estate/managed-assets/{id:N}";
        await using var stream = file.OpenReadStream();
        var filePath = await _fileStorageService.UploadFileAsync(stream, file.FileName, folder);
        var document = await _managedAssetService.RegisterDocumentAsync(id, new RegisterEstateManagedAssetDocumentDto
        {
            FileName = file.FileName,
            FilePath = filePath,
            DocumentType = documentType,
            DocumentName = documentName,
            ContentType = file.ContentType,
            FileSize = file.Length
        });
        return Ok(new { success = true, data = document, message = "Land document uploaded to the file repository." });
    }

    [HttpGet("{id:guid}/documents/{documentId:guid}/download")]
    public async Task<IActionResult> DownloadDocument(Guid id, Guid documentId)
    {
        try
        {
            var stored = await _managedAssetService.GetDocumentAsync(id, documentId);
            var stream = await _fileStorageService.DownloadFileAsync(stored.FilePath, documentId);
            return File(stream, stored.Document.ContentType ?? "application/octet-stream", stored.Document.FileName);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { success = false, message = ex.Message });
        }
    }
}
