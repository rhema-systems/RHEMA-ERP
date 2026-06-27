using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.DTOs.Maintenance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Maintenance;
using ErpSystem.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SixLabors.ImageSharp.Processing;

namespace ErpSystem.Api.Controllers.Maintenance;

[ApiController]
[Route("api/maintenance/attachments")]
[Authorize]
public class MaintenanceAttachmentsController : ControllerBase
{
    private readonly IMaintenanceAttachmentService _attachmentService;
    private readonly IFileStorageService _storageService;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<MaintenanceAttachmentsController> _logger;
    private readonly IConfiguration _configuration;

    public MaintenanceAttachmentsController(
        IMaintenanceAttachmentService attachmentService,
        IFileStorageService storageService,
        ICurrentUserService currentUserService,
        ILogger<MaintenanceAttachmentsController> logger,
        IConfiguration configuration)
    {
        _attachmentService = attachmentService;
        _storageService = storageService;
        _currentUserService = currentUserService;
        _logger = logger;
        _configuration = configuration;
    }

    /// <summary>
    /// Uploads files for supported maintenance entities (work orders, assets, inspections, and fleet records)
    /// </summary>
    [HttpPost("upload/{entityType}/{entityId:guid}")]
    [RequestSizeLimit(52428800)] // 50MB
    public async Task<ActionResult<IEnumerable<MaintenanceAttachmentDto>>> UploadFiles(
        string entityType,
        Guid entityId,
        [FromForm] List<IFormFile> files,
        [FromForm] string? description = null,
        [FromForm] string? category = null,
        [FromForm] bool isMainImage = false)
    {
        try
        {
            if (!files.Any())
            {
                return BadRequest("No files provided");
            }

            var allowedTypes = Enum.GetNames<AttachmentEntityType>();
            if (!allowedTypes.Contains(entityType, StringComparer.OrdinalIgnoreCase))
            {
                return BadRequest($"Entity type must be one of: {string.Join(", ", allowedTypes)}");
            }

            var maxFileSize = _configuration.GetValue<long>("FileUpload:MaxFileSizeBytes", 52428800); // 50MB
            var allowedExtensions = _configuration.GetSection("FileUpload:AllowedExtensions").Get<string[]>()
                                  ?? new[] { ".jpg", ".jpeg", ".png", ".pdf", ".doc", ".docx", ".xlsx" };

            var uploadedAttachments = new List<MaintenanceAttachmentDto>();

            foreach (var file in files)
            {
                // Validate file size
                if (file.Length > maxFileSize)
                {
                    return BadRequest($"File {file.FileName} exceeds maximum size of {maxFileSize / 1024 / 1024}MB");
                }

                // Validate file extension
                var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
                if (!allowedExtensions.Contains(extension))
                {
                    return BadRequest($"File type {extension} is not allowed");
                }

                // Upload file to storage
                var containerName = GetContainerName(entityType);
                var filePath = $"{entityId}/{Guid.NewGuid()}{extension}";

                using var fileStream = file.OpenReadStream();
                var uploadedPath = await _storageService.UploadFileAsync(fileStream, file.FileName, filePath);

                var uploadResult = new StorageResult
                {
                    Success = !string.IsNullOrEmpty(uploadedPath),
                    Url = uploadedPath,
                    StoragePath = filePath
                };

                if (!uploadResult.Success)
                {
                    return StatusCode(500, $"Failed to upload file {file.FileName}: {uploadResult.ErrorMessage}");
                }

                // Create attachment record
                var createDto = new ErpSystem.Core.DTOs.Maintenance.CreateMaintenanceAttachmentDto
                {
                    FileName = file.FileName,
                    FilePath = uploadResult.Url,
                    ContentType = file.ContentType,
                    FileSizeBytes = file.Length,
                    Description = description,
                    AttachmentType = GetAttachmentType(file.ContentType),
                    EntityType = entityType,
                    EntityId = entityId,
                    IsMainImage = isMainImage && IsImageFile(file.ContentType),
                    Category = category
                };

                // Extract image metadata if it's an image
                if (IsImageFile(file.ContentType))
                {
                    var imageMetadata = await ExtractImageMetadata(file);
                    createDto.ImageWidth = imageMetadata.Width;
                    createDto.ImageHeight = imageMetadata.Height;

                    // Generate thumbnail for images
                    if (imageMetadata.Width > 300 || imageMetadata.Height > 300)
                    {
                        var thumbnailResult = await GenerateThumbnail(file, containerName, uploadResult.StoragePath);
                        if (thumbnailResult.Success)
                        {
                            createDto.ThumbnailPath = thumbnailResult.Url;
                        }
                    }
                }

                var attachment = await _attachmentService.CreateAttachmentAsync(createDto);
                // Map from Core DTO to Controller DTO
                var controllerAttachmentDto = new MaintenanceAttachmentDto
                {
                    Id = attachment.Id,
                    FileName = attachment.FileName,
                    FilePath = attachment.FilePath,
                    ContentType = attachment.ContentType,
                    FileSizeBytes = attachment.FileSizeBytes,
                    Description = attachment.Description,
                    AttachmentType = attachment.AttachmentType,
                    EntityType = attachment.EntityType,
                    EntityId = attachment.EntityId,
                    UploadedDate = attachment.UploadedDate,
                    UploadedByUserName = attachment.UploadedByUserName,
                    IsMainImage = attachment.IsMainImage,
                    ImageWidth = attachment.ImageWidth,
                    ImageHeight = attachment.ImageHeight,
                    ThumbnailPath = attachment.ThumbnailPath,
                    Latitude = attachment.Latitude,
                    Longitude = attachment.Longitude,
                    LocationDescription = attachment.LocationDescription,
                    DocumentVersion = attachment.DocumentVersion
                };
                uploadedAttachments.Add(controllerAttachmentDto);

                _logger.LogInformation("File uploaded successfully: {FileName} for {EntityType} {EntityId}",
                    file.FileName, entityType, entityId);
            }

            return Ok(uploadedAttachments);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading files for {EntityType} {EntityId}", entityType, entityId);
            return StatusCode(500, "An error occurred while uploading files");
        }
    }

    /// <summary>
    /// Gets attachments for a specific entity
    /// </summary>
    [HttpGet("{entityType}/{entityId:guid}")]
    public async Task<ActionResult<IEnumerable<MaintenanceAttachmentDto>>> GetAttachments(
        string entityType,
        Guid entityId,
        [FromQuery] string? category = null,
        [FromQuery] string? attachmentType = null)
    {
        try
        {
            var attachments = await _attachmentService.GetAttachmentsByEntityAsync(entityType, entityId, category, attachmentType);
            return Ok(attachments);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving attachments for {EntityType} {EntityId}", entityType, entityId);
            return StatusCode(500, "An error occurred while retrieving attachments");
        }
    }

    /// <summary>
    /// Gets a specific attachment
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<MaintenanceAttachmentDto>> GetAttachment(Guid id)
    {
        try
        {
            var attachment = await _attachmentService.GetAttachmentByIdAsync(id);
            if (attachment == null)
            {
                return NotFound($"Attachment with ID {id} not found");
            }

            return Ok(attachment);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving attachment {AttachmentId}", id);
            return StatusCode(500, "An error occurred while retrieving the attachment");
        }
    }

    /// <summary>
    /// Downloads an attachment file
    /// </summary>
    [HttpGet("{id:guid}/download")]
    public async Task<IActionResult> DownloadAttachment(Guid id)
    {
        try
        {
            var attachment = await _attachmentService.GetAttachmentByIdAsync(id);
            if (attachment == null)
            {
                return NotFound($"Attachment with ID {id} not found");
            }

            // Log access
            if (_currentUserService.EmployeeId is { } downloadEmployeeId && downloadEmployeeId != Guid.Empty)
            {
                await _attachmentService.LogAttachmentAccessAsync(id, downloadEmployeeId, "Download");
            }

            // Get file stream from storage
            var fileStream = await _storageService.DownloadFileAsync(attachment.FilePath, id);
            var downloadResult = new { Success = fileStream != null, FileStream = fileStream };
            if (!downloadResult.Success || downloadResult.FileStream == null)
            {
                return NotFound("File not found in storage");
            }

            return File(downloadResult.FileStream, attachment.ContentType, attachment.FileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading attachment {AttachmentId}", id);
            return StatusCode(500, "An error occurred while downloading the attachment");
        }
    }

    /// <summary>
    /// Gets thumbnail for an image attachment
    /// </summary>
    [HttpGet("{id:guid}/thumbnail")]
    public async Task<IActionResult> GetThumbnail(Guid id)
    {
        try
        {
            var attachment = await _attachmentService.GetAttachmentByIdAsync(id);
            if (attachment == null)
            {
                return NotFound($"Attachment with ID {id} not found");
            }

            if (string.IsNullOrEmpty(attachment.ThumbnailPath))
            {
                return NotFound("Thumbnail not available for this attachment");
            }

            // Log access
            if (_currentUserService.EmployeeId is { } previewEmployeeId && previewEmployeeId != Guid.Empty)
            {
                await _attachmentService.LogAttachmentAccessAsync(id, previewEmployeeId, "Preview");
            }

            var fileStream = await _storageService.DownloadFileAsync(attachment.ThumbnailPath, id);
            var downloadResult = new { Success = fileStream != null, FileStream = fileStream };
            if (!downloadResult.Success || downloadResult.FileStream == null)
            {
                return NotFound("Thumbnail not found in storage");
            }

            return File(downloadResult.FileStream, "image/jpeg");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving thumbnail for attachment {AttachmentId}", id);
            return StatusCode(500, "An error occurred while retrieving the thumbnail");
        }
    }

    /// <summary>
    /// Updates attachment metadata
    /// </summary>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<MaintenanceAttachmentDto>> UpdateAttachment(Guid id, [FromBody] ErpSystem.Core.DTOs.Maintenance.UpdateMaintenanceAttachmentDto updateDto)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var attachment = await _attachmentService.UpdateAttachmentAsync(id, updateDto);
            return Ok(attachment);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating attachment {AttachmentId}", id);
            return StatusCode(500, "An error occurred while updating the attachment");
        }
    }

    /// <summary>
    /// Deletes an attachment
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteAttachment(Guid id)
    {
        try
        {
            var attachment = await _attachmentService.GetAttachmentByIdAsync(id);
            if (attachment == null)
            {
                return NotFound($"Attachment with ID {id} not found");
            }

            // Delete file from storage
            await _storageService.DeleteFileAsync(attachment.FilePath);
            if (!string.IsNullOrEmpty(attachment.ThumbnailPath))
            {
                await _storageService.DeleteFileAsync(attachment.ThumbnailPath);
            }

            // Delete attachment record
            await _attachmentService.DeleteAttachmentAsync(id);

            _logger.LogInformation("Attachment deleted: {AttachmentId} - {FileName}", id, attachment.FileName);
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting attachment {AttachmentId}", id);
            return StatusCode(500, "An error occurred while deleting the attachment");
        }
    }

    /// <summary>
    /// Adds tags to an attachment
    /// </summary>
    [HttpPost("{id:guid}/tags")]
    public async Task<ActionResult> AddAttachmentTags(Guid id, [FromBody] AddAttachmentTagsDto tagsDto)
    {
        try
        {
            await _attachmentService.AddAttachmentTagsAsync(id, tagsDto.Tags);
            return Ok(new { message = "Tags added successfully" });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding tags to attachment {AttachmentId}", id);
            return StatusCode(500, "An error occurred while adding tags");
        }
    }

    /// <summary>
    /// Gets attachment access logs
    /// </summary>
    [HttpGet("{id:guid}/access-logs")]
    public async Task<ActionResult<IEnumerable<AttachmentAccessLogDto>>> GetAccessLogs(Guid id)
    {
        try
        {
            var logs = await _attachmentService.GetAttachmentAccessLogsAsync(id);
            return Ok(logs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving access logs for attachment {AttachmentId}", id);
            return StatusCode(500, "An error occurred while retrieving access logs");
        }
    }

    private static string GetContainerName(string entityType)
    {
        return entityType.ToLowerInvariant() switch
        {
            "workorder" => "maintenance-workorders",
            "asset" => "maintenance-assets",
            "inspection" => "maintenance-inspections",
            "fleetcompliance" => "maintenance-fleet",
            "fleetincident" => "maintenance-fleet",
            "fleettrip" => "maintenance-fleet",
            "fleetfueltransaction" => "maintenance-fleet",
            "fleetcostentry" => "maintenance-fleet",
            _ => "maintenance-general"
        };
    }

    private static string GetAttachmentType(string contentType)
    {
        return contentType switch
        {
            var ct when ct.StartsWith("image/") => "Photo",
            var ct when ct.StartsWith("video/") => "Video",
            var ct when ct.StartsWith("audio/") => "Audio",
            _ => "Document"
        };
    }

    private static bool IsImageFile(string contentType)
    {
        return contentType.StartsWith("image/");
    }

    private static async Task<ImageMetadata> ExtractImageMetadata(IFormFile file)
    {
        try
        {
            using var stream = file.OpenReadStream();
            using var image = await SixLabors.ImageSharp.Image.LoadAsync(stream);

            return new ImageMetadata
            {
                Width = image.Width,
                Height = image.Height
            };
        }
        catch
        {
            return new ImageMetadata { Width = null, Height = null };
        }
    }

    private async Task<StorageResult> GenerateThumbnail(IFormFile file, string containerName, string originalPath)
    {
        try
        {
            using var stream = file.OpenReadStream();
            using var image = await SixLabors.ImageSharp.Image.LoadAsync(stream);

            // Resize to max 300x300 while maintaining aspect ratio
            image.Mutate(x => x.Resize(new SixLabors.ImageSharp.Processing.ResizeOptions
            {
                Size = new SixLabors.ImageSharp.Size(300, 300),
                Mode = SixLabors.ImageSharp.Processing.ResizeMode.Max
            }));

            using var thumbnailStream = new MemoryStream();
            await image.SaveAsync(thumbnailStream, new SixLabors.ImageSharp.Formats.Jpeg.JpegEncoder());
            thumbnailStream.Position = 0;

            var thumbnailPath = originalPath.Replace(Path.GetExtension(originalPath), "_thumb.jpg");

            // Create IFormFile for thumbnail
            var thumbnailFile = new FormFile(thumbnailStream, 0, thumbnailStream.Length, "thumbnail", "thumbnail.jpg")
            {
                Headers = new HeaderDictionary(),
                ContentType = "image/jpeg"
            };

            thumbnailStream.Position = 0;
            var uploadedThumbnailPath = await _storageService.UploadFileAsync(thumbnailStream, "thumbnail.jpg", thumbnailPath);

            return new StorageResult
            {
                Success = !string.IsNullOrEmpty(uploadedThumbnailPath),
                Url = uploadedThumbnailPath,
                StoragePath = thumbnailPath
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to generate thumbnail for file");
            return new StorageResult { Success = false, ErrorMessage = ex.Message };
        }
    }
}

#region Helpers

public class ImageMetadata
{
    public int? Width { get; set; }
    public int? Height { get; set; }
}

public class StorageResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public string? Url { get; set; }
    public string? StoragePath { get; set; }
}

#endregion
