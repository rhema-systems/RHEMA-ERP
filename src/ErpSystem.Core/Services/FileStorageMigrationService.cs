using System.Diagnostics;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services;

/// <summary>
/// Service for migrating files between different storage providers
/// </summary>
public class FileStorageMigrationService : IFileStorageMigrationService
{
    private readonly ILogger<FileStorageMigrationService> _logger;

    public FileStorageMigrationService(ILogger<FileStorageMigrationService> logger)
    {
        _logger = logger;
    }

    public async Task<MigrationResult> MigrateFilesAsync(
        IFileStorageService sourceStorage,
        IFileStorageService destinationStorage,
        MigrationOptions options)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = new MigrationResult();
        var errors = new List<string>();

        try
        {
            _logger.LogInformation("Starting file migration from {SourceProvider} to {DestinationProvider}",
                sourceStorage.ProviderName, destinationStorage.ProviderName);

            // Get list of files to migrate
            var sourceDirectory = options.SourceDirectoryPath ?? "";
            var files = await sourceStorage.ListFilesAsync(sourceDirectory);

            // Apply include/exclude patterns
            files = ApplyPatterns(files, options.IncludePatterns, options.ExcludePatterns);
            var fileList = files.ToList();

            result.TotalFiles = fileList.Count;
            _logger.LogInformation("Found {TotalFiles} files to migrate", result.TotalFiles);

            // Process files in batches
            var batchSize = options.BatchSize > 0 ? options.BatchSize : 100;
            var batches = fileList.Chunk(batchSize);

            foreach (var batch in batches)
            {
                await ProcessBatch(batch, sourceStorage, destinationStorage, options, result, errors);

                if (!options.ContinueOnError && errors.Count > 0)
                {
                    _logger.LogError("Migration stopped due to errors and ContinueOnError is false");
                    break;
                }
            }

            result.Success = result.FailedFiles == 0;
            result.Errors = errors;

            if (result.Success)
            {
                _logger.LogInformation("Migration completed successfully. {MigratedFiles}/{TotalFiles} files migrated",
                    result.MigratedFiles, result.TotalFiles);
            }
            else
            {
                _logger.LogWarning("Migration completed with errors. {MigratedFiles}/{TotalFiles} files migrated, {FailedFiles} failed",
                    result.MigratedFiles, result.TotalFiles, result.FailedFiles);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Migration failed with exception");
            result.Success = false;
            result.ErrorMessage = ex.Message;
            errors.Add($"Migration exception: {ex.Message}");
        }
        finally
        {
            stopwatch.Stop();
            result.Duration = stopwatch.Elapsed;
            result.Errors = errors;
        }

        return result;
    }

    public async Task<MigrationValidationResult> ValidateMigrationAsync(
        IFileStorageService sourceStorage,
        IFileStorageService destinationStorage,
        string directoryPath)
    {
        var result = new MigrationValidationResult();

        try
        {
            _logger.LogInformation("Starting migration validation between {SourceProvider} and {DestinationProvider}",
                sourceStorage.ProviderName, destinationStorage.ProviderName);

            // Get files from both storages
            var sourceFiles = (await sourceStorage.ListFilesAsync(directoryPath)).ToList();
            var destinationFiles = (await destinationStorage.ListFilesAsync(directoryPath)).ToList();

            result.TotalFiles = sourceFiles.Count;

            // Create dictionaries for quick lookup
            var sourceFileDict = sourceFiles.ToDictionary(f => f.FilePath, f => f);
            var destinationFileDict = destinationFiles.ToDictionary(f => f.FilePath, f => f);

            foreach (var sourceFile in sourceFiles)
            {
                if (!destinationFileDict.TryGetValue(sourceFile.FilePath, out var destinationFile))
                {
                    result.MissingFiles.Add(sourceFile.FilePath);
                    result.InvalidFiles++;
                    continue;
                }

                // Check file sizes
                if (sourceFile.FileSize != destinationFile.FileSize)
                {
                    result.SizeMismatches.Add($"{sourceFile.FilePath}: Source={sourceFile.FileSize}, Destination={destinationFile.FileSize}");
                    result.InvalidFiles++;
                    continue;
                }

                result.ValidFiles++;
            }

            result.IsValid = result.InvalidFiles == 0;

            if (result.IsValid)
            {
                _logger.LogInformation("Migration validation passed. All {TotalFiles} files are valid",
                    result.TotalFiles);
            }
            else
            {
                _logger.LogWarning("Migration validation failed. {ValidFiles}/{TotalFiles} files are valid, {InvalidFiles} invalid",
                    result.ValidFiles, result.TotalFiles, result.InvalidFiles);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Migration validation failed with exception");
            result.IsValid = false;
            result.Errors.Add($"Validation exception: {ex.Message}");
        }

        return result;
    }

    #region Private Methods

    private async Task ProcessBatch(
        IEnumerable<FileInfoResult> batch,
        IFileStorageService sourceStorage,
        IFileStorageService destinationStorage,
        MigrationOptions options,
        MigrationResult result,
        List<string> errors)
    {
        foreach (var fileInfo in batch)
        {
            try
            {
                await MigrateFile(fileInfo, sourceStorage, destinationStorage, options);
                result.MigratedFiles++;

                _logger.LogDebug("Successfully migrated file: {FilePath}", fileInfo.FilePath);
            }
            catch (Exception ex)
            {
                result.FailedFiles++;
                var errorMessage = $"Failed to migrate {fileInfo.FilePath}: {ex.Message}";
                errors.Add(errorMessage);

                _logger.LogError(ex, "Failed to migrate file: {FilePath}", fileInfo.FilePath);

                if (!options.ContinueOnError)
                {
                    break;
                }
            }
        }
    }

    private async Task MigrateFile(
        FileInfoResult fileInfo,
        IFileStorageService sourceStorage,
        IFileStorageService destinationStorage,
        MigrationOptions options)
    {
        var destinationPath = string.IsNullOrEmpty(options.DestinationDirectoryPath)
            ? fileInfo.FilePath
            : Path.Combine(options.DestinationDirectoryPath, Path.GetFileName(fileInfo.FilePath)).Replace('\\', '/');

        // Check if file already exists in destination
        if (await destinationStorage.FileExistsAsync(destinationPath))
        {
            _logger.LogDebug("File already exists in destination, skipping: {FilePath}", destinationPath);
            return;
        }

        // For now, we can't directly stream between storage providers without implementing
        // a temporary buffer or streaming mechanism. This is a simplified implementation.

        // Note: In a real implementation, you'd want to:
        // 1. Stream the file from source to destination without loading it entirely into memory
        // 2. Handle large files efficiently
        // 3. Implement retry logic
        // 4. Add progress reporting

        throw new NotImplementedException(
            "Direct file migration between storage providers is not yet implemented. " +
            "This would require streaming files from source to destination storage. " +
            "Consider implementing a temporary download/upload mechanism for large files.");

        // Placeholder for future implementation:
        /*
        using var sourceStream = await sourceStorage.GetFileStreamAsync(fileInfo.FilePath);
        
        var uploadRequest = new FileUploadRequest
        {
            FileStream = sourceStream,
            FileName = Path.GetFileName(fileInfo.FilePath),
            ContentType = fileInfo.ContentType,
            FileSize = fileInfo.FileSize,
            Category = ExtractCategoryFromPath(fileInfo.FilePath),
            TenantId = ExtractTenantIdFromPath(fileInfo.FilePath),
            OverwriteExisting = true
        };

        var uploadResult = await destinationStorage.UploadFileAsync(uploadRequest);
        
        if (!uploadResult.Success)
        {
            throw new InvalidOperationException($"Upload failed: {uploadResult.ErrorMessage}");
        }

        // Verify the migration if enabled
        if (options.VerifyIntegrity)
        {
            var sourceFileInfo = await sourceStorage.GetFileInfoAsync(fileInfo.FilePath);
            var destFileInfo = await destinationStorage.GetFileInfoAsync(destinationPath);
            
            if (sourceFileInfo?.FileSize != destFileInfo?.FileSize)
            {
                throw new InvalidOperationException("File size mismatch after migration");
            }
        }

        // Delete source file if requested
        if (options.DeleteSourceAfterMigration)
        {
            await sourceStorage.DeleteFileAsync(fileInfo.FilePath);
        }
        */
    }

    private IEnumerable<FileInfoResult> ApplyPatterns(
        IEnumerable<FileInfoResult> files,
        List<string> includePatterns,
        List<string> excludePatterns)
    {
        var filteredFiles = files;

        // Apply include patterns
        if (includePatterns.Count > 0)
        {
            filteredFiles = filteredFiles.Where(f =>
                includePatterns.Any(pattern => IsMatch(f.FileName, pattern)));
        }

        // Apply exclude patterns
        if (excludePatterns.Count > 0)
        {
            filteredFiles = filteredFiles.Where(f =>
                !excludePatterns.Any(pattern => IsMatch(f.FileName, pattern)));
        }

        return filteredFiles;
    }

    private static bool IsMatch(string fileName, string pattern)
    {
        // Simple pattern matching - can be enhanced with proper regex
        if (pattern == "*.*" || pattern == "*")
        {
            return true;
        }

        if (pattern.StartsWith("*."))
        {
            var extension = pattern.Substring(1);
            return fileName.EndsWith(extension, StringComparison.OrdinalIgnoreCase);
        }

        return fileName.Equals(pattern, StringComparison.OrdinalIgnoreCase);
    }

    private static string ExtractCategoryFromPath(string filePath)
    {
        // Extract category from path structure like "category/tenant/date/file.ext"
        var segments = filePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return segments.Length > 0 ? segments[0] : "general";
    }

    private static string? ExtractTenantIdFromPath(string filePath)
    {
        // Extract tenant ID from path structure like "category/tenant/date/file.ext"
        var segments = filePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length > 1 && segments[1] != "global")
        {
            return segments[1];
        }
        return null;
    }

    #endregion
}

/// <summary>
/// Extension methods for migration service registration
/// </summary>
public static class MigrationServiceExtensions
{
    /// <summary>
    /// Add migration services to dependency injection container
    /// </summary>
    public static IServiceCollection AddMigrationServices(this IServiceCollection services)
    {
        services.AddScoped<IFileStorageMigrationService, FileStorageMigrationService>();
        return services;
    }
}
