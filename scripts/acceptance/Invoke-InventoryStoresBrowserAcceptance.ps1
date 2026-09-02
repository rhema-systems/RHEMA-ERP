[CmdletBinding()]
param(
    [string]$SqlServer = 'localhost\SQL2022',
    [string]$DatabaseName = "RhemaInventoryUatAssurance_$((Get-Date).ToString('yyyyMMdd'))",
    [int]$ApiPort = 5100,
    [int]$FrontendPort = 3001
)

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
if ($DatabaseName -notmatch '^RhemaInventoryUatAssurance_[0-9]{8}$') {
    throw 'The disposable Inventory database name must match RhemaInventoryUatAssurance_YYYYMMDD.'
}

$existing = (& sqlcmd -S $SqlServer -E -C -d master -b -h -1 -W -Q `
    "SET NOCOUNT ON; SELECT CASE WHEN DB_ID(N'$DatabaseName') IS NULL THEN 0 ELSE 1 END;").Trim()
if ($LASTEXITCODE -ne 0) { throw 'Could not inspect the disposable Inventory database target.' }
if ($existing -ne '0') {
    throw "Inventory acceptance refused because database '$DatabaseName' already exists."
}

$runDirectory = Join-Path ([IO.Path]::GetTempPath()) "rhema-inventory-browser-$([Guid]::NewGuid().ToString('N'))"
$resolvedTemp = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
$resolvedRunDirectory = [IO.Path]::GetFullPath($runDirectory)
if (-not $resolvedRunDirectory.StartsWith($resolvedTemp, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'The Inventory browser run directory did not resolve beneath the system temporary directory.'
}
New-Item -ItemType Directory -Path $resolvedRunDirectory | Out-Null

$apiProcess = $null
$databaseCreated = $false
$environmentNames = [Collections.Generic.List[string]]::new()

function Set-RunEnvironment([string]$Name, [string]$Value) {
    [Environment]::SetEnvironmentVariable($Name, $Value, 'Process')
    $environmentNames.Add($Name)
}

function New-EphemeralSecret {
    return "$([Guid]::NewGuid().ToString('N'))$([Guid]::NewGuid().ToString('N'))Aa1!"
}

function New-EphemeralBytes([int]$Length) {
    $bytes = New-Object byte[] $Length
    $generator = [Security.Cryptography.RandomNumberGenerator]::Create()
    try { $generator.GetBytes($bytes) } finally { $generator.Dispose() }
    return $bytes
}

function Get-LogTail([string[]]$LogPaths) {
    return (($LogPaths | ForEach-Object {
        if (Test-Path $_) { Get-Content $_ -Tail 50 }
    }) -join [Environment]::NewLine)
}

function Wait-HttpReady(
    [string]$Url,
    [Diagnostics.Process]$Process,
    [string[]]$LogPaths
) {
    for ($attempt = 1; $attempt -le 180; $attempt++) {
        if ($Process.HasExited) {
            throw "Inventory API exited before becoming ready.$([Environment]::NewLine)$(Get-LogTail $LogPaths)"
        }
        try {
            $response = Invoke-WebRequest -UseBasicParsing -Uri $Url -TimeoutSec 3
            if ($response.StatusCode -ge 200 -and $response.StatusCode -lt 500) { return }
        } catch {
            # Poll until the bounded startup window expires.
        }
        Start-Sleep -Seconds 1
    }
    throw 'Inventory API did not become ready within 180 seconds.'
}

try {
    $connection = "Server=$SqlServer;Database=$DatabaseName;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True"
    Set-RunEnvironment 'ConnectionStrings__DefaultConnection' $connection
    Set-RunEnvironment 'Database__Provider' 'SqlServer'
    Set-RunEnvironment 'ASPNETCORE_ENVIRONMENT' 'Testing'
    Set-RunEnvironment 'StartupInitialization__AllowDevelopmentDataSeedingOutsideDevelopment' 'true'
    Set-RunEnvironment 'SkipStartupInitialization' 'true'
    Set-RunEnvironment 'BackgroundServices__Enabled' 'false'
    Set-RunEnvironment 'JwtSettings__SecretKey' (New-EphemeralSecret)
    Set-RunEnvironment 'JwtSettings__Issuer' 'Rhema.Inventory.Browser.Acceptance'
    Set-RunEnvironment 'JwtSettings__Audience' 'Rhema.Inventory.Browser.Client'
    Set-RunEnvironment 'JwtSettings__PortalSecretKey' (New-EphemeralSecret)
    Set-RunEnvironment 'JwtSettings__PortalAudience' 'Rhema.Inventory.Browser.Portal'
    Set-RunEnvironment 'CandidatePortal__PortalUrl' 'https://candidate.test/'
    Set-RunEnvironment 'Security__EncryptionKey' ([Convert]::ToBase64String((New-EphemeralBytes 32)))
    Set-RunEnvironment 'FileVirusScan__ClamAv__Host' '127.0.0.1'
    Set-RunEnvironment 'FileStorage__Local__PrivateBasePath' (Join-Path $resolvedRunDirectory 'secure-file-storage')
    Set-RunEnvironment 'CorsSettings__AllowedOrigins__0' "http://127.0.0.1:$FrontendPort"

    $apiProject = Join-Path $repoRoot 'src\ErpSystem.Api\ErpSystem.Api.csproj'
    $buildLog = Join-Path $resolvedRunDirectory 'build.log'
    & dotnet build $apiProject --no-restore -m:1 -p:UseSharedCompilation=false `
        -p:BuildInParallel=false -p:WarningLevel=0 -p:TdcFastEfBuild=true *> $buildLog
    if ($LASTEXITCODE -ne 0) {
        throw "The Inventory browser API build failed.$([Environment]::NewLine)$(Get-LogTail @($buildLog))"
    }

    $apiDll = Join-Path $repoRoot 'src\ErpSystem.Api\bin\Debug\net8.0\ErpSystem.Api.dll'
    $rebuildLog = Join-Path $resolvedRunDirectory 'rebuild.log'
    & dotnet $apiDll rebuild-db *> $rebuildLog
    if ($LASTEXITCODE -ne 0) {
        throw "The disposable Inventory database rebuild failed.$([Environment]::NewLine)$(Get-LogTail @($rebuildLog))"
    }
    $databaseCreated = $true

    $hrSeedLog = Join-Path $resolvedRunDirectory 'seed-hr.log'
    & dotnet $apiDll seed-hr-all *> $hrSeedLog
    if ($LASTEXITCODE -ne 0) {
        throw "The Inventory HR/location seed failed.$([Environment]::NewLine)$(Get-LogTail @($hrSeedLog))"
    }

    $maintenanceSeedLog = Join-Path $resolvedRunDirectory 'seed-maintenance-e2e.log'
    & dotnet $apiDll seed-maintenance-e2e *> $maintenanceSeedLog
    if ($LASTEXITCODE -ne 0) {
        throw "The Inventory maintenance fixture seed failed.$([Environment]::NewLine)$(Get-LogTail @($maintenanceSeedLog))"
    }

    # The receipt lifecycle deliberately requires an employee-linked actor who is
    # different from the requester. Development identity seed data creates the
    # finance clerk account independently from HR, so bind it to an otherwise
    # unassigned active employee only inside this disposable acceptance database.
    $receiverFixtureSql = @"
SET NOCOUNT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET ARITHABORT ON;
SET NUMERIC_ROUNDABORT OFF;
DECLARE @TenantId uniqueidentifier = '00000000-0000-0000-0000-000000000001';
DECLARE @UserId uniqueidentifier = (
    SELECT TOP (1) Id
    FROM dbo.Users
    WHERE TenantId = @TenantId AND UserName = 'finance.clerk' AND IsActive = 1
);
DECLARE @EmployeeId uniqueidentifier = (
    SELECT TOP (1) e.Id
    FROM dbo.Employees e
    WHERE e.TenantId = @TenantId
      AND e.IsDeleted = 0
      AND e.IsActive = 1
      AND NOT EXISTS (
          SELECT 1 FROM dbo.Users u
          WHERE u.TenantId = e.TenantId AND u.EmployeeId = e.Id
      )
    ORDER BY e.EmployeeNumber, e.Id
);
IF @UserId IS NULL
    THROW 51990, 'The disposable finance clerk acceptance actor is missing.', 1;
IF @EmployeeId IS NULL
BEGIN
    DECLARE @SourceEmployeeId uniqueidentifier = (
        SELECT TOP (1) e.Id
        FROM dbo.Employees e
        WHERE e.TenantId = @TenantId AND e.IsDeleted = 0 AND e.IsActive = 1
        ORDER BY e.EmployeeNumber, e.Id
    );
    IF @SourceEmployeeId IS NULL
        THROW 51991, 'No active employee is available for the disposable receipt actor.', 1;

    SET @EmployeeId = NEWID();
    DECLARE @ColumnList nvarchar(max);
    DECLARE @SelectList nvarchar(max);
    SELECT
        @ColumnList = STRING_AGG(CAST(QUOTENAME(c.name) AS nvarchar(max)), ',')
            WITHIN GROUP (ORDER BY c.column_id),
        @SelectList = STRING_AGG(CAST(
            CASE c.name
                WHEN 'Id' THEN '@NewEmployeeId'
                WHEN 'EmployeeNumber' THEN 'N''INV-E2E-RECEIVER'''
                WHEN 'FirstName' THEN 'N''Inventory'''
                WHEN 'LastName' THEN 'N''Receiver'''
                WHEN 'EmailAddress' THEN 'N''inventory.receiver@acceptance.invalid'''
                WHEN 'HireRecordId' THEN 'NULL'
                WHEN 'CreatedAt' THEN 'SYSUTCDATETIME()'
                WHEN 'UpdatedAt' THEN 'NULL'
                WHEN 'CreatedBy' THEN 'N''INV-E2E'''
                WHEN 'UpdatedBy' THEN 'NULL'
                WHEN 'CreatedById' THEN 'NULL'
                WHEN 'LastModifiedById' THEN 'NULL'
                ELSE 'source.' + QUOTENAME(c.name)
            END AS nvarchar(max)), ',') WITHIN GROUP (ORDER BY c.column_id)
    FROM sys.columns c
    JOIN sys.types t ON t.user_type_id = c.user_type_id
    WHERE c.object_id = OBJECT_ID(N'dbo.Employees')
      AND c.is_identity = 0
      AND c.is_computed = 0
      AND t.name NOT IN ('timestamp', 'rowversion');

    DECLARE @InsertSql nvarchar(max) =
        N'INSERT dbo.Employees (' + @ColumnList + N') SELECT ' + @SelectList +
        N' FROM dbo.Employees source WHERE source.Id = @SourceEmployeeId;';
    EXEC sys.sp_executesql @InsertSql,
        N'@NewEmployeeId uniqueidentifier, @SourceEmployeeId uniqueidentifier',
        @NewEmployeeId = @EmployeeId,
        @SourceEmployeeId = @SourceEmployeeId;
END;
UPDATE dbo.Users SET EmployeeId = @EmployeeId WHERE Id = @UserId;

DECLARE @WarehouseId uniqueidentifier = (
    SELECT TOP (1) Id FROM dbo.Warehouses
    WHERE TenantId = @TenantId AND IsDeleted = 0 AND IsActive = 1
    ORDER BY IsDefault DESC, Code, Id
);
IF @WarehouseId IS NULL
    THROW 51992, 'No active warehouse is available for disposable Inventory acceptance.', 1;

DECLARE @LocationId uniqueidentifier = (
    SELECT TOP (1) Id FROM dbo.WarehouseLocations
    WHERE TenantId = @TenantId AND WarehouseId = @WarehouseId
      AND IsDeleted = 0 AND IsActive = 1
    ORDER BY LocationCode, Id
);
IF @LocationId IS NULL
BEGIN
    SET @LocationId = NEWID();
    INSERT dbo.WarehouseLocations
        (Id,WarehouseId,LocationCode,Name,LocationType,IsActive,IsPickingLocation,IsReceivingLocation,
         IsConsignmentBin,IsQuarantineLocation,IsInspectionLocation,IsInTransitLocation,IsShippingLocation,
         IsStagingLocation,IsReturnLocation,IsDamageLocation,PickSequence,CurrentWeight,CurrentVolume,
         CurrentItemCount,CreatedAt,CreatedBy,IsDeleted,TenantId)
    VALUES
        (@LocationId,@WarehouseId,N'INV-E2E-BIN',N'Inventory acceptance bin',N'Bin',1,1,1,
         0,0,0,0,0,0,0,0,1,0,0,0,SYSUTCDATETIME(),N'INV-E2E',0,@TenantId);
END;

DECLARE @ItemId uniqueidentifier;
DECLARE @AverageCost decimal(18,4);
SELECT TOP (1) @ItemId=Id,@AverageCost=AverageCost
FROM dbo.InventoryItems
WHERE TenantId=@TenantId AND IsDeleted=0 AND Status=1 AND ItemType=1
ORDER BY ItemCode,Id;
IF @ItemId IS NULL
    THROW 51993, 'No active stock item is available for disposable Inventory acceptance.', 1;

IF NOT EXISTS (
    SELECT 1 FROM dbo.WarehouseQuantities
    WHERE TenantId=@TenantId AND InventoryItemId=@ItemId AND WarehouseId=@WarehouseId AND IsDeleted=0
)
BEGIN
    INSERT dbo.WarehouseQuantities
        (Id,InventoryItemId,WarehouseId,CurrentStock,AvailableStock,AllocatedStock,ReorderLevel,
         MaxStock,AverageCost,CreatedAt,CreatedBy,IsDeleted,TenantId)
    VALUES
        (NEWID(),@ItemId,@WarehouseId,25,25,0,5,100,COALESCE(@AverageCost,0),
         SYSUTCDATETIME(),N'INV-E2E',0,@TenantId);
END;

IF NOT EXISTS (
    SELECT 1 FROM dbo.InventoryLocations
    WHERE TenantId=@TenantId AND InventoryItemId=@ItemId AND LocationId=@LocationId AND IsDeleted=0
)
BEGIN
    INSERT dbo.InventoryLocations
        (Id,InventoryItemId,LocationId,Quantity,AllocatedQuantity,AvailableQuantity,AverageCost,
         CountFrequencyDays,CreatedAt,CreatedBy,IsDeleted,TenantId)
    VALUES
        (NEWID(),@ItemId,@LocationId,25,0,25,COALESCE(@AverageCost,0),90,
         SYSUTCDATETIME(),N'INV-E2E',0,@TenantId);
END;

DECLARE @FixedItemId uniqueidentifier;
DECLARE @FixedItemCategoryId uniqueidentifier;
DECLARE @FixedItemAverageCost decimal(18,4);
SELECT TOP (1)
    @FixedItemId=Id,
    @FixedItemCategoryId=CategoryId,
    @FixedItemAverageCost=COALESCE(NULLIF(AverageCost,0),NULLIF(StandardCost,0),NULLIF(SalePrice,0),1)
FROM dbo.InventoryItems
WHERE TenantId=@TenantId AND IsDeleted=0 AND Status=1 AND ItemType=4
ORDER BY ItemCode,Id;
DECLARE @FixedAssetCategoryId uniqueidentifier = (
    SELECT TOP (1) Id FROM dbo.FixedAssetCategories
    WHERE TenantId=@TenantId AND IsDeleted=0
    ORDER BY Code,Id
);
IF @FixedItemId IS NULL OR @FixedAssetCategoryId IS NULL
    THROW 51994, 'Fixed-asset masters are unavailable for disposable Inventory acceptance.', 1;
UPDATE dbo.InventoryItems
SET AverageCost=@FixedItemAverageCost
WHERE Id=@FixedItemId AND AverageCost<=0;

IF NOT EXISTS (
    SELECT 1 FROM dbo.WarehouseQuantities
    WHERE TenantId=@TenantId AND InventoryItemId=@FixedItemId AND WarehouseId=@WarehouseId AND IsDeleted=0
)
BEGIN
    INSERT dbo.WarehouseQuantities
        (Id,InventoryItemId,WarehouseId,CurrentStock,AvailableStock,AllocatedStock,ReorderLevel,
         MaxStock,AverageCost,CreatedAt,CreatedBy,IsDeleted,TenantId)
    VALUES
        (NEWID(),@FixedItemId,@WarehouseId,0,0,0,0,10,COALESCE(@FixedItemAverageCost,0),
         SYSUTCDATETIME(),N'INV-E2E',0,@TenantId);
END;

IF NOT EXISTS (
    SELECT 1 FROM dbo.InventoryIssueAccountingRules
    WHERE TenantId=@TenantId AND InventoryCategoryId=@FixedItemCategoryId
      AND ItemType=4 AND MovementReasonCode=N'ASSET_CUSTODY'
      AND Treatment=2 AND IsDeleted=0 AND IsActive=1
      AND EffectiveFromUtc<=SYSUTCDATETIME()
      AND (EffectiveToUtc IS NULL OR EffectiveToUtc>SYSUTCDATETIME())
)
BEGIN
    INSERT dbo.InventoryIssueAccountingRules
        (Id,InventoryCategoryId,ItemType,MovementReasonCode,Treatment,ExpenseAccountId,
         FixedAssetCategoryId,IsActive,EffectiveFromUtc,EffectiveToUtc,CreatedAt,CreatedBy,IsDeleted,TenantId)
    VALUES
        (NEWID(),@FixedItemCategoryId,4,N'ASSET_CUSTODY',2,NULL,
         @FixedAssetCategoryId,1,DATEADD(day,-1,SYSUTCDATETIME()),NULL,
         SYSUTCDATETIME(),N'INV-E2E',0,@TenantId);
END;
"@
    $receiverFixtureLog = Join-Path $resolvedRunDirectory 'receiver-fixture.log'
    & sqlcmd -S $SqlServer -E -C -d $DatabaseName -b -Q $receiverFixtureSql *> $receiverFixtureLog
    if ($LASTEXITCODE -ne 0) {
        throw "The disposable Inventory receiver fixture failed.$([Environment]::NewLine)$(Get-LogTail @($receiverFixtureLog))"
    }

    $apiLog = Join-Path $resolvedRunDirectory 'api.out.log'
    $apiErrorLog = Join-Path $resolvedRunDirectory 'api.err.log'
    Set-RunEnvironment 'ASPNETCORE_URLS' "http://127.0.0.1:$ApiPort"
    $apiProcess = Start-Process -FilePath 'dotnet' -ArgumentList @($apiDll) `
        -WorkingDirectory $repoRoot -RedirectStandardOutput $apiLog -RedirectStandardError $apiErrorLog `
        -WindowStyle Hidden -PassThru
    Wait-HttpReady "http://127.0.0.1:$ApiPort/health/live" $apiProcess @($apiLog, $apiErrorLog)

    & (Join-Path $PSScriptRoot 'Invoke-InvFu002003.ps1') `
        -ApiBaseUrl "http://127.0.0.1:$ApiPort" `
        -FrontendBaseUrl "http://127.0.0.1:$FrontendPort" `
        -DatabaseConnection $connection
    if ($LASTEXITCODE -ne 0) { throw 'Inventory/Stores authenticated browser acceptance failed.' }

    Write-Output 'INVENTORY_STORES_BROWSER_E2E_PASS'
} finally {
    if ($apiProcess -and -not $apiProcess.HasExited) {
        Stop-Process -Id $apiProcess.Id -Force -ErrorAction SilentlyContinue
    }

    if ($databaseCreated) {
        if ($DatabaseName -notmatch '^RhemaInventoryUatAssurance_[0-9]{8}$') {
            throw 'Refusing to drop a database outside the disposable Inventory prefix.'
        }
        & sqlcmd -S $SqlServer -E -C -d master -b -Q `
            "IF DB_ID(N'$DatabaseName') IS NOT NULL BEGIN ALTER DATABASE [$DatabaseName] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [$DatabaseName]; END;" | Out-Null
        if ($LASTEXITCODE -eq 0) { Write-Output 'INVENTORY_STORES_BROWSER_DATABASE_DROPPED' }
    }

    foreach ($name in $environmentNames | Select-Object -Unique) {
        [Environment]::SetEnvironmentVariable($name, $null, 'Process')
    }
    if (Test-Path -LiteralPath $resolvedRunDirectory) {
        Remove-Item -LiteralPath $resolvedRunDirectory -Recurse -Force
    }
}
