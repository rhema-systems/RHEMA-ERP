using ErpSystem.Core.Enums;
using ErpSystem.Core.Entities.Estate;
using ErpSystem.Core.Entities.Projects;
using ErpSystem.Core.Services.Estate;
using ErpSystem.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.Text.Json;

var root = args.Length > 0 ? args[0] : Directory.GetCurrentDirectory();
var mode = args.Length > 1 ? args[1] : "reset";
var apiConfigPath = Path.Combine(root, "src", "ErpSystem.Api");
var configuration = new ConfigurationBuilder()
    .SetBasePath(apiConfigPath)
    .AddJsonFile("appsettings.json", optional: false)
    .AddJsonFile("appsettings.Development.json", optional: true)
    .Build();

var connectionString = configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("DefaultConnection was not found.");

var options = new DbContextOptionsBuilder<ApplicationDbContext>()
    .UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure())
    .Options;

await using var db = new ApplicationDbContext(options);
var now = DateTime.UtcNow;

if (mode == "schema")
{
    const string migrationId = "20260910120000_AddEstateLandSubDemarcationDisposition";
    await db.Database.ExecuteSqlRawAsync("""
        IF COL_LENGTH('dbo.EstateLandDemarcations', 'ParentDemarcationId') IS NULL
            ALTER TABLE [dbo].[EstateLandDemarcations] ADD [ParentDemarcationId] uniqueidentifier NULL;

        IF COL_LENGTH('dbo.EstateLandDemarcations', 'CostAllocationMethod') IS NULL
            ALTER TABLE [dbo].[EstateLandDemarcations] ADD [CostAllocationMethod] nvarchar(40) NOT NULL
                CONSTRAINT [DF_EstateLandDemarcations_CostAllocationMethod] DEFAULT N'NotSet';

        IF COL_LENGTH('dbo.EstateLandDemarcations', 'AllocatedCost') IS NULL
            ALTER TABLE [dbo].[EstateLandDemarcations] ADD [AllocatedCost] decimal(18,2) NULL;

        IF COL_LENGTH('dbo.EstateLandDemarcations', 'CostPerAcre') IS NULL
            ALTER TABLE [dbo].[EstateLandDemarcations] ADD [CostPerAcre] decimal(18,2) NULL;

        IF COL_LENGTH('dbo.EstateLandDemarcations', 'TargetSalePrice') IS NULL
            ALTER TABLE [dbo].[EstateLandDemarcations] ADD [TargetSalePrice] decimal(18,2) NULL;

        IF COL_LENGTH('dbo.EstateLandDemarcations', 'ParentLandAssetReference') IS NULL
            ALTER TABLE [dbo].[EstateLandDemarcations] ADD [ParentLandAssetReference] nvarchar(120) NULL;

        IF COL_LENGTH('dbo.EstateLandDemarcations', 'ParentFixedAssetReference') IS NULL
            ALTER TABLE [dbo].[EstateLandDemarcations] ADD [ParentFixedAssetReference] nvarchar(120) NULL;

        IF COL_LENGTH('dbo.EstateLandDemarcations', 'ChildFixedAssetReference') IS NULL
            ALTER TABLE [dbo].[EstateLandDemarcations] ADD [ChildFixedAssetReference] nvarchar(120) NULL;

        IF COL_LENGTH('dbo.EstateLandDemarcations', 'FixedAssetPostingStatus') IS NULL
            ALTER TABLE [dbo].[EstateLandDemarcations] ADD [FixedAssetPostingStatus] nvarchar(40) NOT NULL
                CONSTRAINT [DF_EstateLandDemarcations_FixedAssetPostingStatus] DEFAULT N'NotReady';

        IF COL_LENGTH('dbo.EstateLandDemarcations', 'FixedAssetPostedAt') IS NULL
            ALTER TABLE [dbo].[EstateLandDemarcations] ADD [FixedAssetPostedAt] datetime2 NULL;

        IF COL_LENGTH('dbo.EstateLandDemarcations', 'IsReadyForProjectManagement') IS NULL
            ALTER TABLE [dbo].[EstateLandDemarcations] ADD [IsReadyForProjectManagement] bit NOT NULL
                CONSTRAINT [DF_EstateLandDemarcations_IsReadyForProjectManagement] DEFAULT CAST(0 AS bit);

        IF COL_LENGTH('dbo.EstateLandDemarcations', 'IsPublishedToExternalPortal') IS NULL
            ALTER TABLE [dbo].[EstateLandDemarcations] ADD [IsPublishedToExternalPortal] bit NOT NULL
                CONSTRAINT [DF_EstateLandDemarcations_IsPublishedToExternalPortal] DEFAULT CAST(0 AS bit);

        IF COL_LENGTH('dbo.EstateLandDemarcations', 'ExternalListingType') IS NULL
            ALTER TABLE [dbo].[EstateLandDemarcations] ADD [ExternalListingType] nvarchar(40) NOT NULL
                CONSTRAINT [DF_EstateLandDemarcations_ExternalListingType] DEFAULT N'None';

        IF COL_LENGTH('dbo.EstateLandDemarcations', 'ExternalListingStatus') IS NULL
            ALTER TABLE [dbo].[EstateLandDemarcations] ADD [ExternalListingStatus] nvarchar(40) NOT NULL
                CONSTRAINT [DF_EstateLandDemarcations_ExternalListingStatus] DEFAULT N'Draft';

        IF COL_LENGTH('dbo.EstateLandDemarcations', 'ExternalListingPrice') IS NULL
            ALTER TABLE [dbo].[EstateLandDemarcations] ADD [ExternalListingPrice] decimal(18,2) NULL;

        IF COL_LENGTH('dbo.EstateLandDemarcations', 'ExternalSalePrice') IS NULL
            ALTER TABLE [dbo].[EstateLandDemarcations] ADD [ExternalSalePrice] decimal(18,2) NULL;

        IF COL_LENGTH('dbo.EstateLandDemarcations', 'ExternalMonthlyRent') IS NULL
            ALTER TABLE [dbo].[EstateLandDemarcations] ADD [ExternalMonthlyRent] decimal(18,2) NULL;

        IF COL_LENGTH('dbo.EstateLandDemarcations', 'ExternalLeaseTermMonths') IS NULL
            ALTER TABLE [dbo].[EstateLandDemarcations] ADD [ExternalLeaseTermMonths] int NULL;

        IF COL_LENGTH('dbo.EstateLandDemarcations', 'ExternalListingCurrency') IS NULL
            ALTER TABLE [dbo].[EstateLandDemarcations] ADD [ExternalListingCurrency] nvarchar(10) NOT NULL
                CONSTRAINT [DF_EstateLandDemarcations_ExternalListingCurrency] DEFAULT N'GHS';

        IF COL_LENGTH('dbo.EstateLandDemarcations', 'ExternalListingNotes') IS NULL
            ALTER TABLE [dbo].[EstateLandDemarcations] ADD [ExternalListingNotes] nvarchar(2000) NULL;

        IF COL_LENGTH('dbo.EstateLandDemarcations', 'ExternalPublishedAt') IS NULL
            ALTER TABLE [dbo].[EstateLandDemarcations] ADD [ExternalPublishedAt] datetime2 NULL;

        IF NOT EXISTS (
            SELECT 1 FROM sys.indexes
            WHERE [name] = N'IX_EstateLandDemarcations_TenantId_EstateManagedAssetId_ParentDemarcationId'
              AND [object_id] = OBJECT_ID(N'[dbo].[EstateLandDemarcations]'))
            CREATE INDEX [IX_EstateLandDemarcations_TenantId_EstateManagedAssetId_ParentDemarcationId]
                ON [dbo].[EstateLandDemarcations] ([TenantId], [EstateManagedAssetId], [ParentDemarcationId]);

        IF NOT EXISTS (
            SELECT 1 FROM sys.indexes
            WHERE [name] = N'IX_EstateLandDemarcations_TenantId_IsReadyForProjectManagement_IsPublishedToExternalPortal'
              AND [object_id] = OBJECT_ID(N'[dbo].[EstateLandDemarcations]'))
            CREATE INDEX [IX_EstateLandDemarcations_TenantId_IsReadyForProjectManagement_IsPublishedToExternalPortal]
                ON [dbo].[EstateLandDemarcations] ([TenantId], [IsReadyForProjectManagement], [IsPublishedToExternalPortal]);

        IF NOT EXISTS (
            SELECT 1 FROM sys.foreign_keys
            WHERE [name] = N'FK_EstateLandDemarcations_EstateLandDemarcations_ParentDemarcationId')
            ALTER TABLE [dbo].[EstateLandDemarcations]
                ADD CONSTRAINT [FK_EstateLandDemarcations_EstateLandDemarcations_ParentDemarcationId]
                FOREIGN KEY ([ParentDemarcationId])
                REFERENCES [dbo].[EstateLandDemarcations] ([Id])
                ON DELETE NO ACTION;
        """);

    await db.Database.ExecuteSqlRawAsync($"""
        IF OBJECT_ID(N'[dbo].[__EFMigrationsHistory]') IS NOT NULL
           AND NOT EXISTS (SELECT 1 FROM [dbo].[__EFMigrationsHistory] WHERE [MigrationId] = N'{migrationId}')
        BEGIN
            DECLARE @ProductVersion nvarchar(32) =
                COALESCE((SELECT TOP (1) [ProductVersion] FROM [dbo].[__EFMigrationsHistory] ORDER BY [MigrationId] DESC), N'9.0.0');
            INSERT INTO [dbo].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
            VALUES (N'{migrationId}', @ProductVersion);
        END
        """);

    var requiredColumns = new[]
    {
        "ParentDemarcationId",
        "CostAllocationMethod",
        "AllocatedCost",
        "CostPerAcre",
        "TargetSalePrice",
        "ParentLandAssetReference",
        "ParentFixedAssetReference",
        "ChildFixedAssetReference",
        "FixedAssetPostingStatus",
        "FixedAssetPostedAt",
        "IsReadyForProjectManagement",
        "IsPublishedToExternalPortal",
        "ExternalListingType",
        "ExternalListingStatus",
        "ExternalListingPrice",
        "ExternalSalePrice",
        "ExternalMonthlyRent",
        "ExternalLeaseTermMonths",
        "ExternalListingCurrency",
        "ExternalListingNotes",
        "ExternalPublishedAt"
    };
    var presentColumns = await db.Database.SqlQueryRaw<string>("""
        SELECT [name] AS [Value]
        FROM sys.columns
        WHERE [object_id] = OBJECT_ID(N'[dbo].[EstateLandDemarcations]')
        """).ToListAsync();
    var missing = requiredColumns
        .Where(column => !presentColumns.Contains(column, StringComparer.OrdinalIgnoreCase))
        .ToList();

    Console.WriteLine($"Applied {migrationId}.");
    Console.WriteLine(missing.Count == 0
        ? "Confirmed all sub-demarcation columns exist."
        : $"Missing columns: {string.Join(", ", missing)}");
    return;
}

if (mode == "values")
{
    var values = await db.LandAcquisitions
        .Where(acquisition => !acquisition.IsDeleted
            && db.EstateManagedAssets.Any(asset =>
                asset.LandAcquisitionId == acquisition.Id
                && asset.TenantId == acquisition.TenantId
                && !asset.IsDeleted
                && asset.AssetType == EstateManagedAssetType.Land
                && asset.SourceType == EstateManagedAssetSourceType.LandAcquisition
                && asset.Status == EstateManagedAssetStatus.LandBank))
        .Select(acquisition => new
        {
            acquisition.ProjectReference,
            acquisition.WorkspaceDataJson,
            Assets = db.EstateManagedAssets
                .Where(asset => asset.LandAcquisitionId == acquisition.Id
                    && asset.TenantId == acquisition.TenantId
                    && !asset.IsDeleted
                    && asset.AssetType == EstateManagedAssetType.Land
                    && asset.SourceType == EstateManagedAssetSourceType.LandAcquisition
                    && asset.Status == EstateManagedAssetStatus.LandBank)
                .Select(asset => new
                {
                    asset.AssetCode,
                    asset.Name,
                    asset.ValuationAmount,
                    asset.Currency
                })
                .ToList()
        })
        .ToListAsync();

    foreach (var value in values)
    {
        var stage16 = ReadStage(value.WorkspaceDataJson, 16);
        Console.WriteLine(value.ProjectReference);
        Console.WriteLine($"  Asset Creation totalCapitalizedCost: {Text(stage16, "totalCapitalizedCost") ?? "missing"}");
        Console.WriteLine($"  Asset Creation capitalizationValue: {Text(stage16, "capitalizationValue") ?? "missing"}");
        foreach (var asset in value.Assets)
        {
            Console.WriteLine($"  Land Bank {asset.AssetCode}: ValuationAmount {asset.ValuationAmount?.ToString("0.##") ?? "null"} {asset.Currency}");
        }
    }

    return;
}

if (mode == "status")
{
    var rows = await db.LandAcquisitions
        .Where(acquisition => !acquisition.IsDeleted
            && db.EstateManagedAssets.Any(asset =>
                asset.LandAcquisitionId == acquisition.Id
                && asset.TenantId == acquisition.TenantId
                && !asset.IsDeleted
                && asset.AssetType == EstateManagedAssetType.Land
                && asset.SourceType == EstateManagedAssetSourceType.LandAcquisition
                && asset.Status == EstateManagedAssetStatus.LandBank))
        .OrderBy(acquisition => acquisition.ProjectReference)
        .Select(acquisition => new
        {
            acquisition.ProjectReference,
            acquisition.StageOrder,
            acquisition.CurrentStage,
            acquisition.Status
        })
        .ToListAsync();

    Console.WriteLine($"Land Bank acquisitions found: {rows.Count}");
    foreach (var row in rows)
    {
        Console.WriteLine($"{row.ProjectReference} | stage {row.StageOrder} {row.CurrentStage} | {row.Status}");
    }

    return;
}

if (mode == "asset-status")
{
    var assets = await db.EstateManagedAssets
        .Where(asset => !asset.IsDeleted
            && asset.AssetType == EstateManagedAssetType.Land
            && asset.Status == EstateManagedAssetStatus.LandBank)
        .OrderBy(asset => asset.AssetCode)
        .Select(asset => new
        {
            asset.AssetCode,
            asset.Name,
            asset.SourceType,
            asset.LandAcquisitionId,
            AcquisitionReference = db.LandAcquisitions
                .Where(acquisition => acquisition.Id == asset.LandAcquisitionId)
                .Select(acquisition => acquisition.ProjectReference)
                .FirstOrDefault(),
            AcquisitionStage = db.LandAcquisitions
                .Where(acquisition => acquisition.Id == asset.LandAcquisitionId)
                .Select(acquisition => acquisition.CurrentStage.ToString())
                .FirstOrDefault(),
            AcquisitionStatus = db.LandAcquisitions
                .Where(acquisition => acquisition.Id == asset.LandAcquisitionId)
                .Select(acquisition => acquisition.Status.ToString())
                .FirstOrDefault()
        })
        .ToListAsync();

    Console.WriteLine($"Land Bank land assets found: {assets.Count}");
    foreach (var asset in assets)
    {
        Console.WriteLine($"{asset.AssetCode} | {asset.Name} | source {asset.SourceType} | acquisition {asset.AcquisitionReference ?? "none"} | {asset.AcquisitionStage ?? "n/a"} {asset.AcquisitionStatus ?? "n/a"}");
    }

    return;
}

if (mode == "bring-asset")
{
    var selector = args.Length > 2 ? args[2] : string.Empty;
    if (string.IsNullOrWhiteSpace(selector))
    {
        Console.WriteLine("Provide an asset name or asset code.");
        return;
    }

    var matches = await db.EstateManagedAssets
        .Where(item => !item.IsDeleted
            && item.AssetType == EstateManagedAssetType.Land
            && (item.Name == selector
                || item.AssetCode == selector
                || item.Name.Contains(selector)
                || item.AssetCode.Contains(selector)))
        .OrderByDescending(item => item.Name == selector || item.AssetCode == selector)
        .ThenBy(item => item.AssetCode)
        .ToListAsync();

    if (matches.Count == 0)
    {
        Console.WriteLine($"No matching land asset found for '{selector}'.");
        return;
    }

    if (matches.Count > 1)
    {
        Console.WriteLine($"Multiple matching land assets found for '{selector}'. No changes made.");
        foreach (var match in matches)
        {
            Console.WriteLine($"{match.AssetCode} | {match.Name} | {match.Status} | acquisition {match.LandAcquisitionId?.ToString() ?? "none"}");
        }
        return;
    }

    var asset = matches[0];
    var acquisition = asset.LandAcquisitionId.HasValue
        ? await db.LandAcquisitions.FirstOrDefaultAsync(item => item.Id == asset.LandAcquisitionId.Value && !item.IsDeleted)
        : null;

    if (acquisition == null)
    {
        var projectReference = asset.AssetCode;
        var suffix = 2;
        while (await db.LandAcquisitions.AnyAsync(item => item.TenantId == asset.TenantId && item.ProjectReference == projectReference))
        {
            projectReference = $"{asset.AssetCode}-{suffix++}";
        }

        acquisition = new LandAcquisition
        {
            TenantId = asset.TenantId,
            ProjectReference = projectReference,
            IntendedUse = asset.Purpose ?? "Asset Creation testing",
            EstimatedSize = asset.AreaValue ?? asset.AreaSquareMeters ?? 0m,
            Location = asset.Location ?? asset.Name,
            CurrentStage = AcquisitionProcedure.LandAssetCreation,
            StageOrder = (int)AcquisitionProcedure.LandAssetCreation,
            Status = LandAcquisitionStatus.PendingApproval,
            OwnershipType = LandOwnershipType.Unknown,
            Coordinates = asset.BoundaryCoordinates,
            PlanningUploaded = true,
            InternalApproved = true,
            SuitableForDueDiligence = true,
            CreatedAt = now,
            UpdatedAt = now
        };

        db.LandAcquisitions.Add(acquisition);
        asset.LandAcquisitionId = acquisition.Id;

        db.LandAssets.Add(new LandAsset
        {
            TenantId = asset.TenantId,
            LandAcquisition = acquisition,
            AssetCode = asset.AssetCode,
            AssetNumber = asset.AssetCode,
            ParcelIdentifier = asset.Name,
            Location = asset.Location,
            AssetCategory = "Land",
            Size = asset.AreaValue ?? asset.AreaSquareMeters,
            SizeUnit = asset.AreaUnit,
            Status = "Active",
            Purpose = asset.Purpose,
            ZoningClassification = asset.ZoningClassification,
            OwnershipVerification = "Seeded from existing Land Bank asset for development testing",
            CapitalizationValue = asset.ValuationAmount ?? 0m,
            GlAccount = "FA-LAND",
            Custodian = "Fixed Asset Officer",
            Notes = "Seeded from existing Land Bank asset for Asset Creation development testing.",
            CreatedAt = now,
            UpdatedAt = now
        });
    }

    acquisition.StageOrder = (int)AcquisitionProcedure.LandAssetCreation;
    acquisition.CurrentStage = AcquisitionProcedure.LandAssetCreation;
    acquisition.Status = LandAcquisitionStatus.PendingApproval;
    acquisition.UpdatedAt = now;
    asset.Status = EstateManagedAssetStatus.UnderDevelopment;
    asset.UpdatedAt = now;

    var capitalizedValue = asset.ValuationAmount ?? 0m;
    var stage16 = new Dictionary<string, object?>
    {
        ["assetCode"] = asset.AssetCode,
        ["assetNumber"] = asset.AssetCode,
        ["parcelIdentifier"] = asset.Name,
        ["ownerName"] = "Rhema ERP",
        ["assetLocation"] = asset.Location ?? asset.Name,
        ["assetCategory"] = "Land",
        ["size"] = (asset.AreaValue ?? asset.AreaSquareMeters)?.ToString(System.Globalization.CultureInfo.InvariantCulture),
        ["sizeUnit"] = asset.AreaUnit ?? "Acres",
        ["assetStatus"] = "Active",
        ["purpose"] = asset.Purpose ?? "Asset Creation testing",
        ["zoningClassification"] = asset.ZoningClassification,
        ["ownershipVerification"] = "Seeded from existing Land Bank asset for development testing",
        ["capitalizationValue"] = capitalizedValue.ToString(System.Globalization.CultureInfo.InvariantCulture),
        ["ownerConsiderationCost"] = capitalizedValue.ToString(System.Globalization.CultureInfo.InvariantCulture),
        ["externalSurveyorCost"] = "0",
        ["stampDutyCost"] = "0",
        ["otherAcquisitionCost"] = "0",
        ["totalCapitalizedCost"] = capitalizedValue.ToString(System.Globalization.CultureInfo.InvariantCulture),
        ["glAccount"] = "FA-LAND",
        ["custodian"] = "Fixed Asset Officer",
        ["assetNotes"] = "Seeded from existing Land Bank asset for Asset Creation development testing."
    };

    acquisition.WorkspaceDataJson = JsonSerializer.Serialize(new Dictionary<int, Dictionary<string, object?>>
    {
        [(int)AcquisitionProcedure.LandAssetCreation] = stage16
    });

    await db.SaveChangesAsync();

    Console.WriteLine($"Moved asset to Asset Creation Pending Approval: {asset.AssetCode} | {asset.Name}");
    Console.WriteLine($"Acquisition: {acquisition.ProjectReference} | stage {acquisition.StageOrder} {acquisition.CurrentStage} | {acquisition.Status}");
    return;
}

if (mode == "find-asset")
{
    var selector = args.Length > 2 ? args[2] : string.Empty;
    if (string.IsNullOrWhiteSpace(selector))
    {
        Console.WriteLine("Provide search text.");
        return;
    }

    var matches = await db.EstateManagedAssets
        .Where(item => !item.IsDeleted
            && item.AssetType == EstateManagedAssetType.Land
            && (item.Name.Contains(selector)
                || item.AssetCode.Contains(selector)
                || (item.Location != null && item.Location.Contains(selector))))
        .OrderBy(item => item.AssetCode)
        .Select(item => new
        {
            item.AssetCode,
            item.Name,
            item.Location,
            item.Status,
            item.SourceType,
            item.LandAcquisitionId
        })
        .Take(40)
        .ToListAsync();

    Console.WriteLine($"Matches for '{selector}': {matches.Count}");
    foreach (var match in matches)
    {
        Console.WriteLine($"{match.AssetCode} | {match.Name} | {match.Location ?? "no location"} | {match.Status} | source {match.SourceType} | acquisition {match.LandAcquisitionId?.ToString() ?? "none"}");
    }
    return;
}

if (mode == "inspect-cleanup-targets")
{
    var targetCodes = new[] { "LAND-ACQ-UI-EST-001", "LAND-UI-EST-001", "LAND-NEW-PROJECT-00001" };
    var targetNames = new[] { "LAND-ACQ-UI-EST-001", "New Project 00001" };
    var assets = await db.EstateManagedAssets
        .Where(item => !item.IsDeleted
            && item.AssetType == EstateManagedAssetType.Land
            && (targetCodes.Contains(item.AssetCode)
                || targetNames.Contains(item.Name)
                || (item.ProjectCode != null && targetCodes.Contains(item.ProjectCode))))
        .ToListAsync();

    foreach (var asset in assets)
    {
        Console.WriteLine($"ASSET|{asset.Id}|{asset.AssetCode}|{asset.Name}|{asset.Status}|projectReady={asset.IsReadyForProjectManagement}|portal={asset.IsPublishedToExternalPortal}|listing={asset.ExternalListingType}/{asset.ExternalListingStatus}|projectId={asset.ProjectId?.ToString() ?? "none"}");
        var demarcations = await db.EstateLandDemarcations
            .Where(item => item.EstateManagedAssetId == asset.Id)
            .OrderBy(item => item.DemarcationNumber)
            .ToListAsync();
        foreach (var demarcation in demarcations)
        {
            var reference = EstateLandDemarcationReference.Build(asset.AssetCode, demarcation.DemarcationNumber);
            Console.WriteLine($"DEMARCATION|{demarcation.Id}|{reference}|deleted={demarcation.IsDeleted}|projectReady={demarcation.IsReadyForProjectManagement}|portal={demarcation.IsPublishedToExternalPortal}|listing={demarcation.ExternalListingType}/{demarcation.ExternalListingStatus}");
            var profiles = await db.ProjectDevelopmentProfiles
                .Where(item => !item.IsDeleted && item.LandReference != null &&
                    (item.LandReference == asset.AssetCode || item.LandReference == reference))
                .Select(item => new { item.Id, item.ProjectId, item.LandReference })
                .ToListAsync();
            foreach (var profile in profiles)
            {
                Console.WriteLine($"PROJECT_PROFILE|{profile.Id}|{profile.ProjectId}|{profile.LandReference}");
            }
        }
    }
    return;
}

if (mode == "cleanup-targets")
{
    var targetCodes = new[] { "LAND-ACQ-UI-EST-001", "LAND-UI-EST-001", "LAND-NEW-PROJECT-00001" };
    var targetNames = new[] { "LAND-ACQ-UI-EST-001", "New Project 00001" };
    var assets = await db.EstateManagedAssets
        .Where(item => !item.IsDeleted
            && item.AssetType == EstateManagedAssetType.Land
            && (targetCodes.Contains(item.AssetCode)
                || targetNames.Contains(item.Name)
                || (item.ProjectCode != null && targetCodes.Contains(item.ProjectCode))))
        .OrderBy(item => item.AssetCode)
        .ToListAsync();

    if (assets.Count == 0)
    {
        Console.WriteLine("No cleanup target assets found. No changes made.");
        return;
    }

    var assetIds = assets.Select(item => item.Id).ToList();
    var demarcations = await db.EstateLandDemarcations
        .IgnoreQueryFilters()
        .Where(item => assetIds.Contains(item.EstateManagedAssetId))
        .ToListAsync();

    var landReferences = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    foreach (var asset in assets)
    {
        AddLandReference(landReferences, asset.AssetCode);
        AddLandReference(landReferences, asset.Name);
        AddLandReference(landReferences, asset.ProjectCode);
        AddLandReference(landReferences, asset.Id.ToString());

        foreach (var demarcation in demarcations.Where(item => item.EstateManagedAssetId == asset.Id))
        {
            AddLandReference(landReferences, demarcation.Id.ToString());
            AddLandReference(
                landReferences,
                EstateLandDemarcationReference.Build(asset.AssetCode, demarcation.DemarcationNumber));
        }
    }

    var profileIds = await db.ProjectDevelopmentProfiles
        .Where(item => !item.IsDeleted
            && item.LandReference != null
            && landReferences.Contains(item.LandReference))
        .Select(item => item.Id)
        .ToListAsync();

    var hardDeletedProfiles = profileIds.Count == 0
        ? 0
        : await db.ProjectDevelopmentProfiles
            .IgnoreQueryFilters()
            .Where(item => profileIds.Contains(item.Id))
            .ExecuteDeleteAsync();

    var hardDeletedDemarcations = 0;
    var remainingDemarcationIds = demarcations.Select(item => item.Id).ToHashSet();
    while (remainingDemarcationIds.Count > 0)
    {
        var leafIds = demarcations
            .Where(item => remainingDemarcationIds.Contains(item.Id)
                && !demarcations.Any(candidate =>
                    remainingDemarcationIds.Contains(candidate.Id)
                    && candidate.ParentDemarcationId == item.Id))
            .Select(item => item.Id)
            .ToList();

        if (leafIds.Count == 0)
        {
            throw new InvalidOperationException("Could not resolve demarcation delete order.");
        }

        hardDeletedDemarcations += await db.EstateLandDemarcations
            .IgnoreQueryFilters()
            .Where(item => leafIds.Contains(item.Id))
            .ExecuteDeleteAsync();

        foreach (var leafId in leafIds)
        {
            remainingDemarcationIds.Remove(leafId);
        }
    }

    foreach (var asset in assets)
    {
        asset.IsReadyForProjectManagement = false;
        asset.ProjectId = null;
        asset.ProjectTitle = null;
        asset.ProjectUnitId = null;
        asset.ProjectUnitCode = null;
        asset.IsPublishedToExternalPortal = false;
        asset.ExternalListingType = "None";
        asset.ExternalListingStatus = "Draft";
        asset.ExternalListingPrice = null;
        asset.ExternalSalePrice = null;
        asset.ExternalMonthlyRent = null;
        asset.ExternalLeaseTermMonths = null;
        asset.ExternalListingNotes = null;
        asset.ExternalPublishedAt = null;
        asset.UpdatedAt = now;
    }

    await db.SaveChangesAsync();

    Console.WriteLine($"Cleaned assets: {assets.Count}");
    foreach (var asset in assets)
    {
        Console.WriteLine($"ASSET|{asset.AssetCode}|{asset.Name}|status={asset.Status}|projectReady={asset.IsReadyForProjectManagement}|portal={asset.IsPublishedToExternalPortal}|projectId={asset.ProjectId?.ToString() ?? "none"}");
    }
    Console.WriteLine($"Hard-deleted demarcations: {hardDeletedDemarcations}");
    Console.WriteLine($"Hard-deleted project profiles: {hardDeletedProfiles}");

    var remainingActiveDemarcations = await db.EstateLandDemarcations
        .CountAsync(item => assetIds.Contains(item.EstateManagedAssetId) && !item.IsDeleted);
    var remainingProfiles = await db.ProjectDevelopmentProfiles
        .CountAsync(item => !item.IsDeleted
            && item.LandReference != null
            && landReferences.Contains(item.LandReference));

    Console.WriteLine($"Remaining active demarcations: {remainingActiveDemarcations}");
    Console.WriteLine($"Remaining active project profile references: {remainingProfiles}");
    return;
}

if (mode == "remove-test-from-land-bank")
{
    var testAssets = await db.EstateManagedAssets
        .Where(asset => !asset.IsDeleted
            && asset.AssetType == EstateManagedAssetType.Land
            && asset.SourceType == EstateManagedAssetSourceType.LandAcquisition
            && (asset.AssetCode == "LAND-NEW-PROJECT-00001"
                || asset.Name == "New Project 00001"
                || db.LandAcquisitions.Any(acquisition =>
                    acquisition.Id == asset.LandAcquisitionId
                    && acquisition.ProjectReference == "New Project 00001")))
        .ToListAsync();

    foreach (var testAsset in testAssets)
    {
        testAsset.Status = EstateManagedAssetStatus.UnderDevelopment;
        testAsset.UpdatedAt = now;
    }

    await db.SaveChangesAsync();
    Console.WriteLine($"Removed test project assets from Land Bank: {testAssets.Count}");
    foreach (var testAsset in testAssets)
    {
        Console.WriteLine($"{testAsset.AssetCode} | {testAsset.Name} | {testAsset.Status}");
    }
    return;
}

if (mode == "create-demo-workflow")
{
    var asset = await db.EstateManagedAssets
        .Where(item => !item.IsDeleted
            && item.AssetType == EstateManagedAssetType.Land
            && item.Status == EstateManagedAssetStatus.LandBank
            && item.SourceType == EstateManagedAssetSourceType.LandAcquisition
            && (item.LandAcquisitionId == null
                || !db.LandAcquisitions.Any(acquisition => acquisition.Id == item.LandAcquisitionId && !acquisition.IsDeleted)))
        .OrderBy(item => item.AssetCode)
        .FirstOrDefaultAsync();

    if (asset == null)
    {
        Console.WriteLine("No orphan Land Acquisition-sourced Land Bank asset found.");
        return;
    }

    var projectReference = asset.AssetCode;
    var suffix = 2;
    while (await db.LandAcquisitions.AnyAsync(item => item.TenantId == asset.TenantId && item.ProjectReference == projectReference))
    {
        projectReference = $"{asset.AssetCode}-{suffix++}";
    }

    var acquisition = new LandAcquisition
    {
        TenantId = asset.TenantId,
        ProjectReference = projectReference,
        IntendedUse = asset.Purpose ?? "Demo Asset Creation testing",
        EstimatedSize = asset.AreaValue ?? asset.AreaSquareMeters ?? 0m,
        Location = asset.Location ?? asset.Name,
        CurrentStage = AcquisitionProcedure.LandAssetCreation,
        StageOrder = (int)AcquisitionProcedure.LandAssetCreation,
        Status = LandAcquisitionStatus.PendingApproval,
        OwnershipType = LandOwnershipType.Unknown,
        Coordinates = asset.BoundaryCoordinates,
        PlanningUploaded = true,
        InternalApproved = true,
        SuitableForDueDiligence = true,
        CreatedAt = now,
        UpdatedAt = now
    };

    var capitalizedValue = asset.ValuationAmount ?? 0m;
    var areaValue = asset.AreaValue ?? asset.AreaSquareMeters;
    var stage16 = new Dictionary<string, object?>
    {
        ["assetCode"] = asset.AssetCode,
        ["assetNumber"] = asset.AssetCode,
        ["parcelIdentifier"] = asset.Name,
        ["ownerName"] = "Rhema ERP",
        ["assetLocation"] = asset.Location ?? asset.Name,
        ["assetCategory"] = "Land",
        ["size"] = areaValue?.ToString(System.Globalization.CultureInfo.InvariantCulture),
        ["sizeUnit"] = asset.AreaUnit ?? "Acres",
        ["assetStatus"] = "Active",
        ["purpose"] = asset.Purpose ?? "Demo Asset Creation testing",
        ["zoningClassification"] = asset.ZoningClassification,
        ["ownershipVerification"] = "Seeded from existing Land Bank asset for development testing",
        ["capitalizationValue"] = capitalizedValue.ToString(System.Globalization.CultureInfo.InvariantCulture),
        ["ownerConsiderationCost"] = capitalizedValue.ToString(System.Globalization.CultureInfo.InvariantCulture),
        ["externalSurveyorCost"] = "0",
        ["stampDutyCost"] = "0",
        ["otherAcquisitionCost"] = "0",
        ["totalCapitalizedCost"] = capitalizedValue.ToString(System.Globalization.CultureInfo.InvariantCulture),
        ["glAccount"] = "FA-LAND",
        ["custodian"] = "Fixed Asset Officer",
        ["assetNotes"] = "Seeded from existing Land Bank asset for Asset Creation development testing."
    };
    acquisition.WorkspaceDataJson = JsonSerializer.Serialize(new Dictionary<int, Dictionary<string, object?>>
    {
        [(int)AcquisitionProcedure.LandAssetCreation] = stage16
    });

    var landAsset = new LandAsset
    {
        TenantId = asset.TenantId,
        LandAcquisition = acquisition,
        AssetCode = asset.AssetCode,
        AssetNumber = asset.AssetCode,
        ParcelIdentifier = asset.Name,
        Location = asset.Location,
        AssetCategory = "Land",
        Size = areaValue,
        SizeUnit = asset.AreaUnit,
        Status = "Active",
        Purpose = asset.Purpose,
        ZoningClassification = asset.ZoningClassification,
        OwnershipVerification = "Seeded from existing Land Bank asset for development testing",
        CapitalizationValue = capitalizedValue,
        GlAccount = "FA-LAND",
        Custodian = "Fixed Asset Officer",
        Notes = "Seeded from existing Land Bank asset for Asset Creation development testing.",
        CreatedAt = now,
        UpdatedAt = now
    };

    db.LandAcquisitions.Add(acquisition);
    db.LandAssets.Add(landAsset);
    asset.LandAcquisitionId = acquisition.Id;
    asset.UpdatedAt = now;
    await db.SaveChangesAsync();

    Console.WriteLine($"Created Asset Creation workflow: {acquisition.ProjectReference}");
    Console.WriteLine($"Linked Land Bank asset: {asset.AssetCode} | {asset.Name}");
    Console.WriteLine($"Stage: {acquisition.StageOrder} {acquisition.CurrentStage} | {acquisition.Status}");
    return;
}

if (mode == "reset-one")
{
    var target = await db.LandAcquisitions
        .Where(acquisition => !acquisition.IsDeleted
            && !(acquisition.StageOrder == (int)AcquisitionProcedure.LandAssetCreation
                && acquisition.CurrentStage == AcquisitionProcedure.LandAssetCreation
                && acquisition.Status == LandAcquisitionStatus.PendingApproval)
            && db.EstateManagedAssets.Any(asset =>
                asset.LandAcquisitionId == acquisition.Id
                && asset.TenantId == acquisition.TenantId
                && !asset.IsDeleted
                && asset.AssetType == EstateManagedAssetType.Land
                && asset.SourceType == EstateManagedAssetSourceType.LandAcquisition
                && asset.Status == EstateManagedAssetStatus.LandBank))
        .OrderBy(acquisition => acquisition.ProjectReference)
        .Select(acquisition => new
        {
            acquisition.Id,
            acquisition.ProjectReference,
            acquisition.StageOrder,
            acquisition.CurrentStage,
            acquisition.Status
        })
        .FirstOrDefaultAsync();

    if (target == null)
    {
        Console.WriteLine("No additional eligible Land Bank acquisition found to move.");
        return;
    }

    var updatedOne = await db.LandAcquisitions
        .Where(acquisition => acquisition.Id == target.Id)
        .ExecuteUpdateAsync(setters => setters
            .SetProperty(acquisition => acquisition.StageOrder, (int)AcquisitionProcedure.LandAssetCreation)
            .SetProperty(acquisition => acquisition.CurrentStage, AcquisitionProcedure.LandAssetCreation)
            .SetProperty(acquisition => acquisition.Status, LandAcquisitionStatus.PendingApproval)
            .SetProperty(acquisition => acquisition.UpdatedAt, now));

    Console.WriteLine($"Moved one acquisition to Asset Creation: {target.ProjectReference}");
    Console.WriteLine($"Previous state: stage {target.StageOrder} {target.CurrentStage} | {target.Status}");
    Console.WriteLine($"Updated acquisitions: {updatedOne}");
    return;
}

var targets = await db.LandAcquisitions
    .Where(acquisition => !acquisition.IsDeleted
        && db.EstateManagedAssets.Any(asset =>
            asset.LandAcquisitionId == acquisition.Id
            && asset.TenantId == acquisition.TenantId
            && !asset.IsDeleted
            && asset.AssetType == EstateManagedAssetType.Land
            && asset.SourceType == EstateManagedAssetSourceType.LandAcquisition
            && asset.Status == EstateManagedAssetStatus.LandBank))
    .OrderBy(acquisition => acquisition.ProjectReference)
    .Select(acquisition => new
    {
        acquisition.Id,
        acquisition.ProjectReference,
        acquisition.StageOrder,
        acquisition.CurrentStage,
        acquisition.Status
    })
    .ToListAsync();

Console.WriteLine($"Matched active Land Bank acquisitions: {targets.Count}");
foreach (var target in targets.Take(50))
{
    Console.WriteLine($"{target.ProjectReference} | stage {target.StageOrder} {target.CurrentStage} | {target.Status}");
}

var updated = await db.LandAcquisitions
    .Where(acquisition => !acquisition.IsDeleted
        && db.EstateManagedAssets.Any(asset =>
            asset.LandAcquisitionId == acquisition.Id
            && asset.TenantId == acquisition.TenantId
            && !asset.IsDeleted
            && asset.AssetType == EstateManagedAssetType.Land
            && asset.SourceType == EstateManagedAssetSourceType.LandAcquisition
            && asset.Status == EstateManagedAssetStatus.LandBank))
    .ExecuteUpdateAsync(setters => setters
        .SetProperty(acquisition => acquisition.StageOrder, (int)AcquisitionProcedure.LandAssetCreation)
        .SetProperty(acquisition => acquisition.CurrentStage, AcquisitionProcedure.LandAssetCreation)
        .SetProperty(acquisition => acquisition.Status, LandAcquisitionStatus.PendingApproval)
        .SetProperty(acquisition => acquisition.UpdatedAt, now));

var afterCount = await db.LandAcquisitions
    .CountAsync(acquisition => !acquisition.IsDeleted
        && acquisition.StageOrder == (int)AcquisitionProcedure.LandAssetCreation
        && acquisition.CurrentStage == AcquisitionProcedure.LandAssetCreation
        && acquisition.Status == LandAcquisitionStatus.PendingApproval
        && db.EstateManagedAssets.Any(asset =>
            asset.LandAcquisitionId == acquisition.Id
            && asset.TenantId == acquisition.TenantId
            && !asset.IsDeleted
            && asset.AssetType == EstateManagedAssetType.Land
            && asset.SourceType == EstateManagedAssetSourceType.LandAcquisition
            && asset.Status == EstateManagedAssetStatus.LandBank));

Console.WriteLine($"Updated acquisitions: {updated}");
Console.WriteLine($"Confirmed at Asset Creation: {afterCount}");

static Dictionary<string, JsonElement> ReadStage(string? workspaceDataJson, int stage)
{
    if (string.IsNullOrWhiteSpace(workspaceDataJson))
    {
        return new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
    }

    var snapshots = JsonSerializer.Deserialize<Dictionary<int, Dictionary<string, JsonElement>>>(workspaceDataJson);
    return snapshots != null && snapshots.TryGetValue(stage, out var values)
        ? new Dictionary<string, JsonElement>(values, StringComparer.OrdinalIgnoreCase)
        : new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
}

static string? Text(IReadOnlyDictionary<string, JsonElement> values, string key)
{
    if (!values.TryGetValue(key, out var value)
        || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
    {
        return null;
    }

    return value.ValueKind == JsonValueKind.String ? value.GetString() : value.ToString();
}

static void AddLandReference(ISet<string> references, string? value)
{
    if (!string.IsNullOrWhiteSpace(value))
    {
        references.Add(value.Trim());
    }
}
