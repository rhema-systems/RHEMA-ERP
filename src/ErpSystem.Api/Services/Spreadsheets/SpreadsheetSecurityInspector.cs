using System.IO.Compression;
using System.Buffers;
using DocumentFormat.OpenXml.Packaging;

namespace ErpSystem.Api.Services.Spreadsheets;

internal static class SpreadsheetSecurityInspector
{
    public static void ValidateXlsxPackage(
        byte[] workbookBytes,
        int maximumEntries = 4_096,
        long maximumEntryBytes = 64L * 1024 * 1024,
        long maximumUncompressedBytes = 128L * 1024 * 1024,
        double maximumCompressionRatio = 500,
        int maximumWorksheets = 64)
    {
        if (workbookBytes.Length < 4 ||
            workbookBytes[0] != 0x50 ||
            workbookBytes[1] != 0x4B ||
            workbookBytes[2] != 0x03 ||
            workbookBytes[3] != 0x04)
        {
            throw new InvalidOperationException(
                "The uploaded file is not a valid Open XML workbook package.");
        }

        try
        {
            using var archiveStream = new MemoryStream(workbookBytes, writable: false);
            using var archive = new ZipArchive(
                archiveStream,
                ZipArchiveMode.Read,
                leaveOpen: false);
            if (archive.Entries.Count == 0 || archive.Entries.Count > maximumEntries)
            {
                throw new InvalidOperationException(
                    $"The workbook package must contain between 1 and {maximumEntries:N0} entries.");
            }

            long totalUncompressedBytes = 0;
            foreach (var entry in archive.Entries)
            {
                if (HasUnsafePackagePath(entry.FullName))
                    throw new InvalidOperationException("The workbook package contains an unsafe entry path.");
                if (entry.Length > maximumEntryBytes)
                {
                    throw new InvalidOperationException(
                        $"Workbook package entry '{entry.Name}' exceeds the {maximumEntryBytes / 1024 / 1024:N0} MB limit.");
                }

                totalUncompressedBytes = checked(totalUncompressedBytes + entry.Length);
                if (totalUncompressedBytes > maximumUncompressedBytes)
                {
                    throw new InvalidOperationException(
                        $"The expanded workbook exceeds the {maximumUncompressedBytes / 1024 / 1024:N0} MB limit.");
                }

                if (entry.Length > 1_048_576)
                {
                    if (entry.CompressedLength == 0 ||
                        entry.Length / (double)entry.CompressedLength > maximumCompressionRatio)
                    {
                        throw new InvalidOperationException(
                            "The workbook package has an unsafe compression ratio.");
                    }
                }

                if (entry.Length == 0)
                    continue;

                var buffer = ArrayPool<byte>.Shared.Rent(64 * 1024);
                try
                {
                    using var contents = entry.Open();
                    long expandedEntryBytes = 0;
                    while (true)
                    {
                        var read = contents.Read(buffer, 0, buffer.Length);
                        if (read == 0)
                            break;

                        expandedEntryBytes = checked(expandedEntryBytes + read);
                        if (expandedEntryBytes > maximumEntryBytes)
                            throw new InvalidOperationException("A workbook package entry exceeds the expanded-size limit.");
                    }

                    if (expandedEntryBytes != entry.Length)
                        throw new InvalidOperationException("A workbook package entry has inconsistent size metadata.");
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(buffer);
                }
            }

            var packageNames = archive.Entries
                .Select(entry => entry.FullName.Replace('\\', '/'))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (!packageNames.Contains("[Content_Types].xml") ||
                !packageNames.Contains("xl/workbook.xml"))
            {
                throw new InvalidOperationException(
                    "The uploaded package is not an .xlsx workbook.");
            }

            using var documentStream = new MemoryStream(workbookBytes, writable: false);
            using var document = SpreadsheetDocument.Open(documentStream, false);
            var worksheetCount = document.WorkbookPart?.WorksheetParts.Count() ?? 0;
            if (worksheetCount == 0 || worksheetCount > maximumWorksheets)
            {
                throw new InvalidOperationException(
                    $"The workbook must contain between 1 and {maximumWorksheets:N0} worksheets.");
            }
        }
        catch (OverflowException)
        {
            throw new InvalidOperationException("The expanded workbook size is invalid.");
        }
        catch (InvalidDataException exception)
        {
            throw new InvalidOperationException(
                "The uploaded file is not a valid Open XML workbook package.",
                exception);
        }
        catch (OpenXmlPackageException exception)
        {
            throw new InvalidOperationException(
                "The uploaded file is not a valid Open XML workbook package.",
                exception);
        }
    }

    public static bool HasExternalRelationships(byte[] workbookBytes)
    {
        using var stream = new MemoryStream(workbookBytes, writable: false);
        using var document = SpreadsheetDocument.Open(stream, false);
        return HasExternalRelationships(document, new HashSet<OpenXmlPart>());
    }

    public static bool HasVbaProject(byte[] workbookBytes)
    {
        using var stream = new MemoryStream(workbookBytes, writable: false);
        using var document = SpreadsheetDocument.Open(stream, false);
        return document.WorkbookPart?.VbaProjectPart != null;
    }

    private static bool HasExternalRelationships(
        OpenXmlPartContainer container,
        ISet<OpenXmlPart> visited)
    {
        if (container.ExternalRelationships.Any())
            return true;

        foreach (var relationship in container.Parts)
        {
            if (visited.Add(relationship.OpenXmlPart) &&
                HasExternalRelationships(relationship.OpenXmlPart, visited))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasUnsafePackagePath(string path)
    {
        var normalized = path.Replace('\\', '/');
        if (normalized.StartsWith("/", StringComparison.Ordinal))
            return true;

        return normalized.Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Any(segment => segment == "..");
    }
}
