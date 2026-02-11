using ErpSystem.Core.DTOs.Maintenance;
using ErpSystem.Core.Entities.Maintenance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Maintenance;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.Maintenance;

public class MaintenanceAttachmentService : IMaintenanceAttachmentService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly IFileStorageService _storageService;
    private readonly ILogger<MaintenanceAttachmentService> _logger;

    public MaintenanceAttachmentService(
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        IFileStorageService storageService,
        ILogger<MaintenanceAttachmentService> logger)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _storageService = storageService;
        _logger = logger;
    }

    public async Task<MaintenanceAttachmentDto> CreateAttachmentAsync(CreateMaintenanceAttachmentDto createDto)
    {
        var uploadedByEmployeeId = ResolveCurrentEmployeeId();

        var attachmentType = ParseEnum<AttachmentType>(createDto.AttachmentType, nameof(createDto.AttachmentType));
        var entityType = ParseEnum<AttachmentEntityType>(createDto.EntityType, nameof(createDto.EntityType));

        var attachmentRepo = _unitOfWork.Repository<MaintenanceAttachment>();

        var entity = new MaintenanceAttachment
        {
            FileName = createDto.FileName,
            FilePath = createDto.FilePath,
            ContentType = createDto.ContentType,
            FileSizeBytes = createDto.FileSizeBytes,
            Description = createDto.Description,
            AttachmentType = attachmentType,
            EntityType = entityType,
            EntityId = createDto.EntityId,
            UploadedDate = DateTime.UtcNow,
            UploadedByUserId = uploadedByEmployeeId,
            IsMainImage = createDto.IsMainImage,
            ImageWidth = createDto.ImageWidth,
            ImageHeight = createDto.ImageHeight,
            ThumbnailPath = createDto.ThumbnailPath,
            Latitude = createDto.Latitude,
            Longitude = createDto.Longitude,
            LocationDescription = createDto.LocationDescription
        };

        await attachmentRepo.AddAsync(entity);

        // Persist category as a tag to avoid schema changes (Category is present in DTO but not on entity)
        if (!string.IsNullOrWhiteSpace(createDto.Category))
        {
            entity.Tags.Add(new MaintenanceAttachmentTag
            {
                TagName = "Category",
                TagValue = createDto.Category
            });
        }

        await _unitOfWork.SaveChangesAsync();

        // Reload with includes so DTO has tags + uploader name
        var created = await attachmentRepo.GetQueryable()
            .Include(x => x.Tags)
            .Include(x => x.UploadedByUser)
            .FirstOrDefaultAsync(x => x.Id == entity.Id);

        if (created == null)
        {
            throw new InvalidOperationException("Attachment was created but could not be loaded.");
        }

        return ToDto(created);
    }

    public async Task<MaintenanceAttachmentDto> UpdateAttachmentAsync(Guid id, UpdateMaintenanceAttachmentDto updateDto)
    {
        var attachmentRepo = _unitOfWork.Repository<MaintenanceAttachment>();

        var entity = await attachmentRepo.GetQueryable()
            .Include(x => x.Tags)
            .Include(x => x.UploadedByUser)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (entity == null)
        {
            throw new KeyNotFoundException($"Attachment {id} not found.");
        }

        if (updateDto.Description != null)
        {
            entity.Description = updateDto.Description;
        }

        if (updateDto.IsMainImage.HasValue)
        {
            entity.IsMainImage = updateDto.IsMainImage.Value;
        }

        if (updateDto.LocationDescription != null)
        {
            entity.LocationDescription = updateDto.LocationDescription;
        }

        if (updateDto.Tags != null)
        {
            // Replace all non-category tags; keep Category tag if set by upload
            var keepTags = entity.Tags.Where(t => t.TagName.Equals("Category", StringComparison.OrdinalIgnoreCase)).ToList();
            entity.Tags.Clear();
            foreach (var t in keepTags)
            {
                entity.Tags.Add(t);
            }

            foreach (var tag in updateDto.Tags.Where(t => !string.IsNullOrWhiteSpace(t.TagName)))
            {
                entity.Tags.Add(new MaintenanceAttachmentTag
                {
                    TagName = tag.TagName.Trim(),
                    TagValue = string.IsNullOrWhiteSpace(tag.TagValue) ? null : tag.TagValue.Trim()
                });
            }
        }

        await attachmentRepo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();

        return ToDto(entity);
    }

    public async Task DeleteAttachmentAsync(Guid id)
    {
        var attachmentRepo = _unitOfWork.Repository<MaintenanceAttachment>();
        var entity = await attachmentRepo.FirstOrDefaultAsync(x => x.Id == id);
        if (entity == null)
        {
            return;
        }

        await attachmentRepo.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task<MaintenanceAttachmentDto?> GetAttachmentByIdAsync(Guid id)
    {
        var attachmentRepo = _unitOfWork.Repository<MaintenanceAttachment>();

        var entity = await attachmentRepo.GetQueryable()
            .Include(x => x.Tags)
            .Include(x => x.UploadedByUser)
            .FirstOrDefaultAsync(x => x.Id == id);

        return entity == null ? null : ToDto(entity);
    }

    public async Task<IEnumerable<MaintenanceAttachmentDto>> GetAttachmentsByEntityAsync(
        string entityType,
        Guid entityId,
        string? category = null,
        string? attachmentType = null)
    {
        var entityTypeEnum = ParseEnum<AttachmentEntityType>(entityType, nameof(entityType));

        AttachmentType? attachmentTypeEnum = null;
        if (!string.IsNullOrWhiteSpace(attachmentType))
        {
            attachmentTypeEnum = ParseEnum<AttachmentType>(attachmentType, nameof(attachmentType));
        }

        var attachmentRepo = _unitOfWork.Repository<MaintenanceAttachment>();

        var query = attachmentRepo.GetQueryable()
            .Include(x => x.Tags)
            .Include(x => x.UploadedByUser)
            .Where(x => x.EntityType == entityTypeEnum && x.EntityId == entityId);

        if (attachmentTypeEnum.HasValue)
        {
            query = query.Where(x => x.AttachmentType == attachmentTypeEnum.Value);
        }

        if (!string.IsNullOrWhiteSpace(category))
        {
            var categoryValue = category.Trim();
            query = query.Where(x => x.Tags.Any(t => t.TagName == "Category" && t.TagValue == categoryValue));
        }

        var results = await query
            .OrderByDescending(x => x.UploadedDate)
            .ToListAsync();

        return results.Select(ToDto);
    }

    public async Task<IEnumerable<MaintenanceAttachmentDto>> GetAttachmentsByTypeAsync(string attachmentType)
    {
        var attachmentTypeEnum = ParseEnum<AttachmentType>(attachmentType, nameof(attachmentType));

        var attachmentRepo = _unitOfWork.Repository<MaintenanceAttachment>();
        var results = await attachmentRepo.GetQueryable()
            .Include(x => x.Tags)
            .Include(x => x.UploadedByUser)
            .Where(x => x.AttachmentType == attachmentTypeEnum)
            .OrderByDescending(x => x.UploadedDate)
            .ToListAsync();

        return results.Select(ToDto);
    }

    public async Task<IEnumerable<MaintenanceAttachmentDto>> GetRecentAttachmentsAsync(int count = 10)
    {
        var attachmentRepo = _unitOfWork.Repository<MaintenanceAttachment>();
        var results = await attachmentRepo.GetQueryable()
            .Include(x => x.Tags)
            .Include(x => x.UploadedByUser)
            .OrderByDescending(x => x.UploadedDate)
            .Take(count)
            .ToListAsync();

        return results.Select(ToDto);
    }

    public async Task<byte[]> GetAttachmentFileAsync(Guid id)
    {
        var attachment = await GetAttachmentByIdAsync(id);
        if (attachment == null)
        {
            throw new KeyNotFoundException($"Attachment {id} not found.");
        }

        await using var stream = await _storageService.DownloadFileAsync(attachment.FilePath, id);
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms);
        return ms.ToArray();
    }

    public async Task<string> GetAttachmentUrlAsync(Guid id)
    {
        var attachment = await GetAttachmentByIdAsync(id);
        if (attachment == null)
        {
            throw new KeyNotFoundException($"Attachment {id} not found.");
        }

        return await _storageService.GetPublicUrlAsync(attachment.FilePath);
    }

    public async Task<string> GetThumbnailUrlAsync(Guid id)
    {
        var attachment = await GetAttachmentByIdAsync(id);
        if (attachment == null)
        {
            throw new KeyNotFoundException($"Attachment {id} not found.");
        }

        if (string.IsNullOrWhiteSpace(attachment.ThumbnailPath))
        {
            return await _storageService.GetPublicUrlAsync(attachment.FilePath);
        }

        return await _storageService.GetPublicUrlAsync(attachment.ThumbnailPath);
    }

    public async Task<bool> SetMainImageAsync(Guid attachmentId, Guid entityId)
    {
        var attachmentRepo = _unitOfWork.Repository<MaintenanceAttachment>();

        var target = await attachmentRepo.FirstOrDefaultAsync(x => x.Id == attachmentId);
        if (target == null)
        {
            return false;
        }

        var attachments = await attachmentRepo.GetQueryable()
            .Where(x => x.EntityType == target.EntityType && x.EntityId == entityId && x.AttachmentType == AttachmentType.Photo)
            .ToListAsync();

        foreach (var a in attachments)
        {
            a.IsMainImage = a.Id == attachmentId;
        }

        await attachmentRepo.UpdateRangeAsync(attachments);
        await _unitOfWork.SaveChangesAsync();
        return true;
    }

    public async Task<long> GetTotalStorageUsedAsync()
    {
        var attachmentRepo = _unitOfWork.Repository<MaintenanceAttachment>();
        return await attachmentRepo.GetQueryable().SumAsync(x => x.FileSizeBytes);
    }

    public async Task AddAttachmentTagsAsync(Guid attachmentId, List<AttachmentTagDto> tags)
    {
        var attachmentRepo = _unitOfWork.Repository<MaintenanceAttachment>();
        var entity = await attachmentRepo.GetQueryable()
            .Include(x => x.Tags)
            .FirstOrDefaultAsync(x => x.Id == attachmentId);

        if (entity == null)
        {
            throw new KeyNotFoundException($"Attachment {attachmentId} not found.");
        }

        foreach (var tag in tags.Where(t => !string.IsNullOrWhiteSpace(t.TagName)))
        {
            var name = tag.TagName.Trim();
            var value = string.IsNullOrWhiteSpace(tag.TagValue) ? null : tag.TagValue.Trim();

            var exists = entity.Tags.Any(t =>
                t.TagName.Equals(name, StringComparison.OrdinalIgnoreCase) &&
                t.TagValue == value);

            if (exists)
            {
                continue;
            }

            entity.Tags.Add(new MaintenanceAttachmentTag
            {
                TagName = name,
                TagValue = value
            });
        }

        await attachmentRepo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task RemoveTagAsync(Guid attachmentId, string tagName)
    {
        if (string.IsNullOrWhiteSpace(tagName))
        {
            return;
        }

        var tagRepo = _unitOfWork.Repository<MaintenanceAttachmentTag>();
        var name = tagName.Trim();

        var tags = await tagRepo.FindAsync(x => x.AttachmentId == attachmentId && x.TagName == name);
        if (!tags.Any())
        {
            return;
        }

        await tagRepo.DeleteRangeAsync(tags);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task<IEnumerable<MaintenanceAttachmentDto>> GetAttachmentsByTagAsync(string tagName, string? tagValue = null)
    {
        var name = tagName.Trim();

        var tagRepo = _unitOfWork.Repository<MaintenanceAttachmentTag>();
        var query = tagRepo.GetQueryable()
            .Include(x => x.Attachment)
                .ThenInclude(a => a.UploadedByUser)
            .Include(x => x.Attachment)
                .ThenInclude(a => a.Tags)
            .Where(x => x.TagName == name);

        if (!string.IsNullOrWhiteSpace(tagValue))
        {
            query = query.Where(x => x.TagValue == tagValue.Trim());
        }

        var attachments = await query
            .Select(x => x.Attachment)
            .Distinct()
            .OrderByDescending(x => x.UploadedDate)
            .ToListAsync();

        return attachments.Select(ToDto);
    }

    public async Task LogAttachmentAccessAsync(Guid attachmentId, Guid userId, string accessType)
    {
        try
        {
            var accessRepo = _unitOfWork.Repository<MaintenanceAttachmentAccess>();

            var accessEnum = ParseEnumOrDefault<AttachmentAccessType>(accessType, AttachmentAccessType.View);

            var entity = new MaintenanceAttachmentAccess
            {
                AttachmentId = attachmentId,
                AccessedByUserId = userId,
                AccessedDate = DateTime.UtcNow,
                AccessType = accessEnum,
                UserAgent = _currentUserService.UserAgent,
                IpAddress = _currentUserService.IpAddress
            };

            await accessRepo.AddAsync(entity);
            await _unitOfWork.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            // Access logging should never block file operations
            _logger.LogWarning(ex, "Failed to log attachment access for {AttachmentId}", attachmentId);
        }
    }

    public async Task<IEnumerable<AttachmentAccessLogDto>> GetAttachmentAccessLogsAsync(Guid attachmentId)
    {
        var accessRepo = _unitOfWork.Repository<MaintenanceAttachmentAccess>();
        var logs = await accessRepo.GetQueryable()
            .Include(x => x.AccessedByUser)
            .Where(x => x.AttachmentId == attachmentId)
            .OrderByDescending(x => x.AccessedDate)
            .ToListAsync();

        return logs.Select(x => new AttachmentAccessLogDto
        {
            Id = x.Id,
            AttachmentId = x.AttachmentId,
            AccessedByUserId = x.AccessedByUserId,
            AccessedByUserName = x.AccessedByUser == null ? string.Empty : $"{x.AccessedByUser.FirstName} {x.AccessedByUser.LastName}".Trim(),
            AccessedDate = x.AccessedDate,
            AccessType = x.AccessType.ToString(),
            UserAgent = x.UserAgent,
            IpAddress = x.IpAddress
        });
    }

    private Guid ResolveCurrentEmployeeId()
    {
        if (_currentUserService.EmployeeId.HasValue && _currentUserService.EmployeeId.Value != Guid.Empty)
        {
            return _currentUserService.EmployeeId.Value;
        }

        if (Guid.TryParse(_currentUserService.UserId, out var userGuid) && userGuid != Guid.Empty)
        {
            // Fallback for deployments where UserId == EmployeeId
            return userGuid;
        }

        throw new InvalidOperationException("Cannot determine the current employee ID for attachment operations.");
    }

    private static T ParseEnum<T>(string value, string paramName) where T : struct
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value cannot be empty.", paramName);
        }

        if (!Enum.TryParse<T>(value, ignoreCase: true, out var result))
        {
            var allowed = string.Join(", ", Enum.GetNames(typeof(T)));
            throw new ArgumentException($"Invalid value '{value}'. Allowed: {allowed}", paramName);
        }

        return result;
    }

    private static T ParseEnumOrDefault<T>(string value, T defaultValue) where T : struct
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return defaultValue;
        }

        return Enum.TryParse<T>(value, ignoreCase: true, out var result) ? result : defaultValue;
    }

    private static MaintenanceAttachmentDto ToDto(MaintenanceAttachment entity)
    {
        return new MaintenanceAttachmentDto
        {
            Id = entity.Id,
            FileName = entity.FileName,
            FilePath = entity.FilePath,
            ContentType = entity.ContentType,
            FileSizeBytes = entity.FileSizeBytes,
            Description = entity.Description,
            AttachmentType = entity.AttachmentType.ToString(),
            EntityType = entity.EntityType.ToString(),
            EntityId = entity.EntityId,
            UploadedDate = entity.UploadedDate,
            UploadedByUserName = entity.UploadedByUser == null ? string.Empty : $"{entity.UploadedByUser.FirstName} {entity.UploadedByUser.LastName}".Trim(),
            IsMainImage = entity.IsMainImage,
            ImageWidth = entity.ImageWidth,
            ImageHeight = entity.ImageHeight,
            ThumbnailPath = entity.ThumbnailPath,
            Latitude = entity.Latitude,
            Longitude = entity.Longitude,
            LocationDescription = entity.LocationDescription,
            DocumentVersion = entity.DocumentVersion,
            Tags = entity.Tags.Select(t => new AttachmentTagDto
            {
                Id = t.Id,
                TagName = t.TagName,
                TagValue = t.TagValue
            }).ToList()
        };
    }
}

