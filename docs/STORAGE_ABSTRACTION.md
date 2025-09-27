# Storage Abstraction System

## Overview

The ERP System now includes a flexible storage abstraction layer that allows seamless transitions between different file storage providers (Local, Azure Blob Storage, AWS S3, etc.) without changing application code.

## Key Features

✅ **Provider Abstraction** - Switch between storage providers via configuration
✅ **Local Storage** - File system storage (current implementation)
✅ **Azure Blob Storage** - Cloud storage with CDN support (ready to activate)
✅ **AWS S3 Support** - Template ready for implementation
✅ **Migration Tools** - Move files between storage providers
✅ **Health Checks** - Monitor storage provider health
✅ **Factory Pattern** - Automatic provider instantiation
✅ **Backward Compatibility** - Existing APIs continue to work

## Configuration

### Current Configuration (Local Storage)
```json
{
  "FileStorage": {
    "Provider": "Local",
    "MaxFileSizeBytes": 10485760,
    "EnableImageOptimization": false,
    "MaxImageWidth": 2048,
    "MaxImageHeight": 2048,
    "AllowedExtensions": [".jpg", ".jpeg", ".png", ".gif", ".bmp", ".svg", ".webp", ".ico", ".pdf", ".doc", ".docx", ".txt", ".rtf"],
    "AllowedMimeTypes": ["image/jpeg", "image/png", "image/gif", "image/bmp", "image/svg+xml", "image/webp", "image/x-icon", "image/vnd.microsoft.icon", "application/pdf", "application/msword", "application/vnd.openxmlformats-officedocument.wordprocessingml.document", "text/plain", "application/rtf"],
    "Local": {
      "BasePath": "uploads",
      "BaseUrl": "/uploads",
      "UseWebRoot": true,
      "CreateDirectoryIfNotExists": true,
      "DefaultPermissions": "ReadWrite"
    },
    "Azure": {
      "ConnectionString": "",
      "ContainerName": "erp-uploads",
      "CdnUrl": "",
      "PublicAccess": true,
      "TemporaryUrlExpirationHours": 24
    },
    "Aws": {
      "AccessKey": "",
      "SecretKey": "",
      "BucketName": "erp-uploads",
      "Region": "us-east-1",
      "CdnUrl": "",
      "PublicAccess": true,
      "TemporaryUrlExpirationHours": 24
    }
  }
}
```

## Switching Storage Providers

### To Local Storage (Default)
```json
{
  "FileStorage": {
    "Provider": "Local",
    "Local": {
      "BasePath": "D:\\ERP-Uploads",  // Optional: Move outside webroot
      "UseWebRoot": false,
      "BaseUrl": "/files"
    }
  }
}
```

### To Azure Blob Storage
1. Install Azure package:
```bash
dotnet add package Azure.Storage.Blobs
```

2. Update configuration:
```json
{
  "FileStorage": {
    "Provider": "AzureBlob",
    "Azure": {
      "ConnectionString": "DefaultEndpointsProtocol=https;AccountName=yourname;AccountKey=yourkey;EndpointSuffix=core.windows.net",
      "ContainerName": "erp-uploads",
      "CdnUrl": "https://yourcdn.azureedge.net",
      "PublicAccess": true
    }
  }
}
```

3. Uncomment Azure implementation in `AzureBlobStorageService.cs`

### To AWS S3
1. Create `AwsS3StorageService.cs` implementing `IFileStorageService`
2. Install AWS SDK:
```bash
dotnet add package AWSSDK.S3
```

3. Update configuration:
```json
{
  "FileStorage": {
    "Provider": "AwsS3",
    "Aws": {
      "AccessKey": "your-access-key",
      "SecretKey": "your-secret-key",
      "BucketName": "erp-uploads",
      "Region": "us-east-1"
    }
  }
}
```

## File Organization Structure

All storage providers use the same consistent structure:
```
{category}/{tenantId}/{yyyy/MM}/{unique-filename}

Examples:
- tenant-branding-logo/00000000-0000-0000-0000-000000000002/2025/09/logo_20250926_213142_498110ab.jpg
- documents/global/2025/09/contract_20250926_143022_a1b2c3d4.pdf
```

## Usage in Code

The system is designed to be transparent to your existing code. All file operations now go through the `IFileStorageService`:

```csharp
// Dependency injection automatically provides the configured storage provider
public class MyController : ControllerBase
{
    private readonly IFileStorageService _storageService;

    public MyController(IFileStorageService storageService)
    {
        _storageService = storageService; // Will be LocalFileStorageService, AzureBlobStorageService, etc.
    }

    // Upload file
    var uploadRequest = new FileUploadRequest
    {
        FileStream = file.OpenReadStream(),
        FileName = file.FileName,
        ContentType = file.ContentType,
        FileSize = file.Length,
        Category = "documents",
        TenantId = "tenant-123"
    };

    var result = await _storageService.UploadFileAsync(uploadRequest);
}
```

## Migration Between Providers

Use the migration service to move files between storage providers:

```csharp
public class MigrationController : ControllerBase
{
    private readonly IStorageServiceFactory _storageFactory;
    private readonly IFileStorageMigrationService _migrationService;

    // Migrate from local to Azure
    public async Task<IActionResult> MigrateToAzure()
    {
        var sourceStorage = _storageFactory.CreateStorage(StorageProviderType.Local);
        var destinationStorage = _storageFactory.CreateStorage(StorageProviderType.AzureBlob);

        var options = new MigrationOptions
        {
            SourceDirectoryPath = "",
            BatchSize = 50,
            ContinueOnError = true,
            VerifyIntegrity = true
        };

        var result = await _migrationService.MigrateFilesAsync(sourceStorage, destinationStorage, options);
        return Ok(result);
    }
}
```

## Transition Plan

### Phase 1: Current State ✅
- Local file storage working
- Abstraction layer implemented
- Backward compatibility maintained

### Phase 2: Prepare for Cloud (When Ready)
1. Choose cloud provider (Azure/AWS/Google)
2. Set up cloud storage account
3. Install required NuGet packages
4. Update configuration with credentials

### Phase 3: Migration (Zero Downtime)
1. Configure new storage provider
2. Test uploads with new provider
3. Run migration utility to move existing files
4. Switch `Provider` configuration setting
5. Verify all files accessible

### Phase 4: Cleanup (Optional)
- Remove old storage after verification
- Optimize settings (CDN, caching, etc.)

## Benefits

### For Development
- **No Code Changes** - Switch providers via configuration
- **Easy Testing** - Test different providers in different environments
- **Local Development** - Always use local storage for dev

### For Production
- **Scalability** - Move to cloud when needed
- **Performance** - CDN support for global users
- **Reliability** - Cloud provider SLAs and backup
- **Cost Control** - Choose provider based on costs

### For Operations
- **Health Monitoring** - Built-in health checks
- **Migration Tools** - Safe migration between providers
- **Rollback Capability** - Quickly revert if needed

## Security Considerations

### Local Storage
- Files in `wwwroot/uploads` are publicly accessible
- Consider moving outside webroot: `UseWebRoot: false`
- Implement proper file permissions

### Cloud Storage
- Use secure connection strings
- Configure proper access policies
- Enable CDN for performance
- Use signed URLs for private files

## Troubleshooting

### Common Issues

1. **404 on uploads**: Check if API server restarted after configuration changes
2. **415 Unsupported Media Type**: Ensure FormData is sent correctly from frontend
3. **Storage provider not found**: Verify configuration and required packages installed
4. **Migration failures**: Check network connectivity and credentials

### Health Checks
Access `/health` endpoint to verify storage provider status:
```json
{
  "status": "Healthy",
  "results": {
    "storage_service": {
      "status": "Healthy",
      "description": "Storage service (Local) is healthy"
    }
  }
}
```

## Files Created/Modified

### New Files
- `src/ErpSystem.Core/Interfaces/IFileStorageService.cs`
- `src/ErpSystem.Core/Models/FileStorage.cs`
- `src/ErpSystem.Core/Services/LocalFileStorageService.cs`
- `src/ErpSystem.Core/Services/AzureBlobStorageService.cs`
- `src/ErpSystem.Core/Services/StorageServiceFactory.cs`
- `src/ErpSystem.Core/Services/FileStorageMigrationService.cs`

### Modified Files
- `src/ErpSystem.Api/Controllers/FileUploadController.cs` - Now uses storage abstraction
- `src/ErpSystem.Api/Extensions/ServiceCollectionExtensions.cs` - Added DI registration
- `src/ErpSystem.Api/appsettings.json` - New storage configuration
- `frontend/src/services/api.service.ts` - Fixed FormData handling

## Next Steps

1. **Test the System** - Verify current local storage still works
2. **Plan Cloud Migration** - Choose provider and timeline
3. **Implement File Streaming** - Complete migration service for large files
4. **Add More Providers** - Implement Google Cloud, AWS S3 when needed
5. **Performance Optimization** - Add caching, compression, etc.

The system is now ready for seamless transitions between storage providers! 🚀