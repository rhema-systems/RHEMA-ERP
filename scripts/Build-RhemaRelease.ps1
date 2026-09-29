[CmdletBinding()]
param(
    [ValidateSet('Test')]
    [string]$Environment = 'Test',
    [Parameter(Mandatory = $true)]
    [uri]$PublicBaseUrl,
    [ValidatePattern('^[0-9a-fA-F]{7,40}$')]
    [string]$ExpectedCommit,
    [ValidatePattern('^[0-9a-fA-F]{7,40}$')]
    [string]$ReuseFrontendBuildFromCommit,
    [string]$OutputDirectory,
    [switch]$CleanBuild
)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
$env:GIT_TERMINAL_PROMPT = '0'
$env:GCM_INTERACTIVE = 'Never'
$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repositoryRoot = Split-Path -Parent $scriptRoot
$frontendRoot = Join-Path $repositoryRoot 'frontend'
$releaseRoot = if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    Join-Path $repositoryRoot 'artifacts\vps-releases'
} else { [IO.Path]::GetFullPath($OutputDirectory) }
$workRoot = Join-Path $repositoryRoot 'artifacts\.release-work'
$timings = [System.Collections.Generic.List[object]]::new()
$workDirectory = $null
$license = $null
$previousEnvironment = @{}

function Assert-True {
    param([bool]$Condition, [string]$Message)
    if (-not $Condition) { throw $Message }
}

function Assert-CommandExists {
    param([string]$Name)
    Assert-True ($null -ne (Get-Command $Name -ErrorAction SilentlyContinue)) `
        "Required command is not available: $Name"
}

function Assert-SafeChildPath {
    param([string]$Candidate, [string]$Parent)
    $candidatePath = [IO.Path]::GetFullPath($Candidate)
    $parentPath = [IO.Path]::GetFullPath($Parent).TrimEnd('\') + '\'
    Assert-True ($candidatePath.StartsWith(
            $parentPath, [StringComparison]::OrdinalIgnoreCase)) `
        "Refusing a filesystem operation outside $parentPath"
}

function Invoke-TimedStep {
    param([string]$Name, [scriptblock]$Operation)
    Write-Host "`n==> $Name" -ForegroundColor Cyan
    $started = [DateTime]::UtcNow
    $watch = [Diagnostics.Stopwatch]::StartNew()
    try {
        $result = & $Operation
        $watch.Stop()
        $timings.Add([ordered]@{
            name = $Name
            status = 'Passed'
            startedUtc = $started.ToString('o')
            durationSeconds = [Math]::Round($watch.Elapsed.TotalSeconds, 2)
        })
        Write-Host ("PASS {0} ({1:n1}s)" -f $Name, $watch.Elapsed.TotalSeconds) -ForegroundColor Green
        return $result
    } catch {
        $watch.Stop()
        $timings.Add([ordered]@{
            name = $Name
            status = 'Failed'
            startedUtc = $started.ToString('o')
            durationSeconds = [Math]::Round($watch.Elapsed.TotalSeconds, 2)
            error = $_.Exception.Message
        })
        throw
    }
}

function Invoke-NativeChecked {
    param([string]$Command, [string[]]$Arguments, [string]$FailureMessage)
    & $Command @Arguments
    if ($LASTEXITCODE -ne 0) { throw "$FailureMessage (exit code $LASTEXITCODE)." }
}

function Invoke-RobocopyChecked {
    param([string[]]$Arguments, [string]$FailureMessage)
    & robocopy.exe @Arguments
    if ($LASTEXITCODE -gt 7) { throw "$FailureMessage (robocopy exit code $LASTEXITCODE)." }
}

function Set-ProcessEnvironment {
    param([hashtable]$Values)
    foreach ($key in $Values.Keys) {
        if (-not $previousEnvironment.ContainsKey($key)) {
            $previousEnvironment[$key] = [Environment]::GetEnvironmentVariable($key, 'Process')
        }
        [Environment]::SetEnvironmentVariable($key, [string]$Values[$key], 'Process')
    }
}

function Restore-ProcessEnvironment {
    foreach ($key in $previousEnvironment.Keys) {
        [Environment]::SetEnvironmentVariable($key, $previousEnvironment[$key], 'Process')
    }
}

function Get-ProtectedSyncfusionLicense {
    foreach ($name in @('SYNCFUSION_LICENSE', 'Syncfusion__LicenseKey')) {
        foreach ($scope in @('Process', 'User', 'Machine')) {
            $value = [Environment]::GetEnvironmentVariable($name, $scope)
            if (-not [string]::IsNullOrWhiteSpace($value)) { return $value }
        }
    }
    throw 'Configure SYNCFUSION_LICENSE or Syncfusion__LicenseKey on the controlled build host.'
}

function Clear-NextOutputPreservingCache {
    param([string]$Path, [switch]$RemoveCache)
    Assert-SafeChildPath $Path $frontendRoot
    if (-not (Test-Path -LiteralPath $Path)) {
        New-Item -ItemType Directory -Path $Path -Force | Out-Null
        return
    }
    if ($RemoveCache) {
        Remove-Item -LiteralPath $Path -Recurse -Force
        New-Item -ItemType Directory -Path $Path -Force | Out-Null
        return
    }
    Get-ChildItem -LiteralPath $Path -Force | Where-Object Name -ne 'cache' |
        Remove-Item -Recurse -Force
}

function New-ZipPackage {
    param([string]$Source, [string]$Destination)
    if (Test-Path -LiteralPath $Destination) { Remove-Item -LiteralPath $Destination -Force }
    Compress-Archive -Path (Join-Path $Source '*') -DestinationPath $Destination `
        -CompressionLevel Optimal
}

function Get-ZipEntryNames {
    param([string]$Path)
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [IO.Compression.ZipFile]::OpenRead($Path)
    try { return @($archive.Entries | ForEach-Object { $_.FullName.Replace('\', '/') }) }
    finally { $archive.Dispose() }
}

Push-Location $repositoryRoot
$runStarted = [DateTime]::UtcNow
try {
    Assert-True ([Environment]::OSVersion.Platform -eq [PlatformID]::Win32NT) `
        'Release construction requires a Windows x64 build host.'
    Assert-True ($env:PROCESSOR_ARCHITECTURE -in @('AMD64', 'x86_64')) `
        "Release construction requires Windows x64; detected $env:PROCESSOR_ARCHITECTURE."
    foreach ($command in @('git', 'dotnet', 'node', 'npm.cmd', 'robocopy.exe')) {
        Assert-CommandExists $command
    }
    Assert-True ($PublicBaseUrl.IsAbsoluteUri -and $PublicBaseUrl.Scheme -eq 'https' -and
        -not $PublicBaseUrl.UserInfo -and -not $PublicBaseUrl.Query -and
        -not $PublicBaseUrl.Fragment -and $PublicBaseUrl.AbsolutePath -eq '/') `
        'PublicBaseUrl must be an HTTPS origin without a path, query, credentials, or fragment.'

    $commit = (& git rev-parse HEAD).Trim()
    Assert-True ($commit -match '^[0-9a-f]{40}$') 'Could not resolve the exact Git commit.'
    if ($ExpectedCommit) {
        Assert-True ($commit.StartsWith($ExpectedCommit, [StringComparison]::OrdinalIgnoreCase)) `
            "HEAD $commit does not match ExpectedCommit $ExpectedCommit."
    }
    $frontendBuildCommit = $commit
    if ($ReuseFrontendBuildFromCommit) {
        $frontendBuildCommit = (& git rev-parse "$ReuseFrontendBuildFromCommit`^{commit}").Trim()
        Assert-True ($LASTEXITCODE -eq 0 -and $frontendBuildCommit -match '^[0-9a-f]{40}$') `
            "Could not resolve ReuseFrontendBuildFromCommit $ReuseFrontendBuildFromCommit."
        & git merge-base --is-ancestor $frontendBuildCommit $commit
        Assert-True ($LASTEXITCODE -eq 0) `
            "Frontend build commit $frontendBuildCommit is not an ancestor of HEAD $commit."
        & git diff --quiet "$frontendBuildCommit..$commit" -- frontend
        Assert-True ($LASTEXITCODE -eq 0) `
            'Frontend files changed after the requested reusable build commit.'
        Assert-True (-not $CleanBuild) `
            'CleanBuild cannot be combined with ReuseFrontendBuildFromCommit.'
    }
    $dirty = @(& git status --porcelain)
    Assert-True ($dirty.Count -eq 0) `
        'The build worktree is dirty. Use a clean checkout of the exact release commit.'

    $shortCommit = $commit.Substring(0, 8)
    $buildStamp = [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss')
    $releaseId = "$shortCommit-$buildStamp"
    $releaseDirectory = Join-Path $releaseRoot $releaseId
    $workDirectory = Join-Path $workRoot $releaseId
    Assert-SafeChildPath $releaseDirectory $releaseRoot
    Assert-SafeChildPath $workDirectory $workRoot
    foreach ($path in @($releaseDirectory, $workDirectory)) {
        Assert-True (-not (Test-Path -LiteralPath $path)) "Release path already exists: $path"
        New-Item -ItemType Directory -Path $path -Force | Out-Null
    }

    $nodeVersion = (& node --version).Trim()
    $npmVersion = (& npm.cmd --version).Trim()
    $nodeArchitecture = (& node -p 'process.arch').Trim()
    Assert-True ($nodeArchitecture -eq 'x64') "Node.js must be x64; detected $nodeArchitecture."
    $operatingSystem = Get-CimInstance Win32_OperatingSystem
    $availableMemoryBytes = [long]$operatingSystem.FreePhysicalMemory * 1KB
    $heapMb = if ($env:NEXT_BUILD_MAX_OLD_SPACE_SIZE_MB) {
        [int]$env:NEXT_BUILD_MAX_OLD_SPACE_SIZE_MB
    } else { 12288 }
    Write-Host "BUILD_ENVIRONMENT|$Environment|$($PublicBaseUrl.GetLeftPart([UriPartial]::Authority))"
    Write-Host "BUILD_RUNTIME|NODE=$nodeVersion|NPM=$npmVersion|ARCH=$nodeArchitecture"
    Write-Host "BUILD_MEMORY|HEAP_MB=$heapMb|AVAILABLE_BYTES=$availableMemoryBytes"

    $license = Get-ProtectedSyncfusionLicense
    Set-ProcessEnvironment @{
        NODE_ENV = 'production'
        NEXT_PUBLIC_API_URL = "$($PublicBaseUrl.GetLeftPart([UriPartial]::Authority))/api"
        API_URL = "$($PublicBaseUrl.GetLeftPart([UriPartial]::Authority))/api"
        NEXTAUTH_URL = $PublicBaseUrl.GetLeftPart([UriPartial]::Authority)
        NEXT_TELEMETRY_DISABLED = '1'
        NEXT_DIST_DIR = '.next-production'
        NEXT_OUTPUT = ''
        SYNCFUSION_LICENSE = $license
        RHEMA_SKIP_SYNCFUSION_ASSETS = '1'
    }

    Push-Location $frontendRoot
    try {
        Invoke-TimedStep 'npm ci' {
            Invoke-NativeChecked 'npm.cmd' @('ci', '--include=dev', '--no-audit', '--no-fund') `
                'Frontend locked dependency restore failed' | Out-Host
        } | Out-Null
        Invoke-TimedStep 'Syncfusion activation and asset preparation' {
            $activator = Join-Path $frontendRoot 'node_modules\.bin\syncfusion-license.cmd'
            Assert-True (Test-Path -LiteralPath $activator) `
                'The installed Syncfusion frontend license activator is missing.'
            Invoke-NativeChecked $activator @('activate') `
                'Syncfusion frontend license activation failed' | Out-Host
            [Environment]::SetEnvironmentVariable('RHEMA_SKIP_SYNCFUSION_ASSETS', $null, 'Process')
            Invoke-NativeChecked 'npm.cmd' @('run', 'prepare:syncfusion') `
                'Syncfusion PDF Viewer asset preparation failed' | Out-Host
        } | Out-Null
    } finally { Pop-Location }

    $apiOutput = Join-Path $workDirectory 'api'
    New-Item -ItemType Directory -Path $apiOutput | Out-Null
    Invoke-TimedStep 'API publish' {
        Invoke-NativeChecked 'dotnet' @(
            'publish', 'src\ErpSystem.Api\ErpSystem.Api.csproj', '-c', 'Release',
            '-r', 'win-x64', '--self-contained', 'true', '-o', $apiOutput,
            '/p:PublishSingleFile=false', '-p:UseSharedCompilation=false', '-m:1'
        ) 'API publish failed' | Out-Host
    } | Out-Null
    foreach ($name in @('appsettings.json', 'appsettings.Production.json',
            'appsettings.Development.json', 'appsettings.AntiSpam.json', '.env', '.env.production')) {
        $path = Join-Path $apiOutput $name
        if (Test-Path -LiteralPath $path) { Remove-Item -LiteralPath $path -Force }
    }
    Assert-True (Test-Path (Join-Path $apiOutput 'ErpSystem.Api.exe')) `
        'Published API executable is missing.'
    Assert-True (@(Get-ChildItem $apiOutput -File -Force | Where-Object {
        $_.Name -like 'appsettings*.json' -or $_.Name -like '.env*'
    }).Count -eq 0) 'Published API contains protected runtime configuration.'

    $nextOutput = Join-Path $frontendRoot '.next-production'
    if ($ReuseFrontendBuildFromCommit) {
        Invoke-TimedStep 'Reuse existing Next.js production build' {
            Assert-True (Test-Path (Join-Path $nextOutput 'BUILD_ID')) `
                'Reusable frontend BUILD_ID is missing.'
            Assert-True (Test-Path (Join-Path $nextOutput 'required-server-files.json')) `
                'Reusable frontend server files are missing.'
            Assert-True (Test-Path (Join-Path $nextOutput 'server\middleware-manifest.json')) `
                'Reusable frontend middleware manifest is missing.'
        } | Out-Null
    }
    else {
        Clear-NextOutputPreservingCache -Path $nextOutput -RemoveCache:$CleanBuild
        Push-Location $frontendRoot
        try {
            Invoke-TimedStep 'Next.js production build' {
                Invoke-NativeChecked 'npm.cmd' @('run', 'build') 'Frontend build failed' | Out-Host
            } | Out-Null
        } finally { Pop-Location }
    }

    $buildId = (Get-Content (Join-Path $nextOutput 'BUILD_ID') -Raw).Trim()
    Assert-True ($buildId -and $buildId -ne 'development') 'Frontend BUILD_ID is invalid.'
    $middleware = Get-Content (Join-Path $nextOutput 'server\middleware-manifest.json') -Raw |
        ConvertFrom-Json
    Assert-True ($middleware.middleware.'/'.env.__NEXT_BUILD_ID -eq $buildId) `
        'Frontend BUILD_ID and middleware build ID differ.'
    Assert-True (Test-Path (Join-Path $nextOutput 'required-server-files.json')) `
        'Regular Next.js server files are missing.'

    $frontendOutput = Join-Path $workDirectory 'frontend'
    New-Item -ItemType Directory -Path $frontendOutput | Out-Null
    Invoke-TimedStep 'Production dependency preparation' {
        Invoke-RobocopyChecked @(
            (Join-Path $frontendRoot 'node_modules'), (Join-Path $frontendOutput 'node_modules'),
            '/E', '/R:2', '/W:2', '/NFL', '/NDL', '/NJH', '/NJS', '/NP'
        ) 'Frontend dependency staging failed'
        Copy-Item (Join-Path $frontendRoot 'package.json'),
            (Join-Path $frontendRoot 'package-lock.json') -Destination $frontendOutput -Force
        Push-Location $frontendOutput
        try {
            Invoke-NativeChecked 'npm.cmd' @(
                'prune', '--omit=dev', '--ignore-scripts', '--no-audit', '--no-fund'
            ) 'Frontend production dependency pruning failed' | Out-Host
        } finally { Pop-Location }
    } | Out-Null

    Invoke-RobocopyChecked @(
        $nextOutput, (Join-Path $frontendOutput '.next'), '/E', '/R:2', '/W:2',
        '/NFL', '/NDL', '/NJH', '/NJS', '/NP', '/XD', (Join-Path $nextOutput 'cache')
    ) 'Frontend build staging failed'
    Invoke-RobocopyChecked @(
        (Join-Path $frontendRoot 'public'), (Join-Path $frontendOutput 'public'), '/E',
        '/R:2', '/W:2', '/NFL', '/NDL', '/NJH', '/NJS', '/NP'
    ) 'Frontend public staging failed'
    Copy-Item (Join-Path $frontendRoot 'next.config.js') -Destination $frontendOutput -Force
    . (Join-Path $scriptRoot 'vps\Set-StagedFrontendRuntime.ps1')
    Set-StagedFrontendRuntime -FrontendDirectory $frontendOutput

    $runtimeNext = Get-Content (Join-Path $frontendOutput 'node_modules\next\package.json') -Raw |
        ConvertFrom-Json
    $frontendPackage = Get-Content (Join-Path $frontendOutput 'package.json') -Raw |
        ConvertFrom-Json
    Assert-True ($runtimeNext.version -eq $frontendPackage.dependencies.next) `
        'Staged Next.js runtime differs from package.json.'
    foreach ($requiredAsset in @('pdfium.js', 'pdfium.wasm')) {
        Assert-True (Test-Path (Join-Path $frontendOutput "public\syncfusion\ej2-pdfviewer-lib\$requiredAsset")) `
            "Staged Syncfusion asset is missing: $requiredAsset"
    }
    Assert-True (-not (Test-Path (Join-Path $frontendOutput '.next\cache'))) `
        'Runtime frontend artifact contains the Next.js build cache.'

    $cacheVersion = "vps-$releaseId"
    $serviceWorkerPath = Join-Path $frontendOutput 'public\sw.js'
    $serviceWorker = Get-Content $serviceWorkerPath -Raw
    foreach ($entry in @{
            CACHE_NAME = "erp-system-$cacheVersion"
            STATIC_CACHE_NAME = "erp-static-$cacheVersion"
            RUNTIME_CACHE_NAME = "erp-runtime-$cacheVersion"
            MOBILE_SHELL_CACHE_NAME = "erp-mobile-shell-$cacheVersion"
        }.GetEnumerator()) {
        $pattern = "(?m)^const $([regex]::Escape($entry.Key)) = '[^']+'"
        Assert-True ([regex]::IsMatch($serviceWorker, $pattern)) `
            "Service worker does not declare $($entry.Key)."
        $serviceWorker = [regex]::Replace(
            $serviceWorker, $pattern, "const $($entry.Key) = '$($entry.Value)'", 1)
    }
    [IO.File]::WriteAllText($serviceWorkerPath, $serviceWorker,
        (New-Object Text.UTF8Encoding($false)))

    $compiledFiles = @(Get-ChildItem (Join-Path $frontendOutput '.next\static'),
        (Join-Path $frontendOutput '.next\server') -File -Recurse -ErrorAction Stop)
    Assert-True (@($compiledFiles | Select-String -Pattern 'localhost:5000|localhost:53484|localhost:7095').Count -eq 0) `
        'Compiled frontend contains a development API URL.'

    $apiZip = Join-Path $releaseDirectory 'api.zip'
    $frontendZip = Join-Path $releaseDirectory 'frontend.zip'
    Invoke-TimedStep 'Artifact compression' {
        New-ZipPackage -Source $apiOutput -Destination $apiZip
        New-ZipPackage -Source $frontendOutput -Destination $frontendZip
    } | Out-Null
    $apiEntries = Get-ZipEntryNames $apiZip
    $frontendEntries = Get-ZipEntryNames $frontendZip
    Assert-True (@($apiEntries | Where-Object { $_ -match '(^|/)ErpSystem\.Api\.exe$' }).Count -eq 1) `
        'API ZIP does not contain exactly one API executable.'
    Assert-True (@($apiEntries | Where-Object { $_ -match '(^|/)(appsettings[^/]*\.json|\.env[^/]*)$' }).Count -eq 0) `
        'API ZIP contains protected runtime configuration.'
    foreach ($expectedEntry in @('.next/BUILD_ID', '.next/required-server-files.json',
            'public/syncfusion/ej2-pdfviewer-lib/pdfium.js',
            'public/syncfusion/ej2-pdfviewer-lib/pdfium.wasm',
            'node_modules/next/package.json', 'package.json', 'package-lock.json',
            'next.config.js')) {
        Assert-True (@($frontendEntries | Where-Object { [string]::Equals(
            $_, $expectedEntry, [StringComparison]::OrdinalIgnoreCase) }).Count -eq 1) `
            "Frontend ZIP is missing or duplicates $expectedEntry."
    }
    Assert-True (@($frontendEntries | Where-Object { $_ -match '(^|/)\.next/cache/' }).Count -eq 0) `
        'Frontend ZIP contains the Next.js build cache.'
    $hashes = Invoke-TimedStep 'Artifact hashing' {
        [ordered]@{
            api = (Get-FileHash $apiZip -Algorithm SHA256).Hash
            frontend = (Get-FileHash $frontendZip -Algorithm SHA256).Hash
        }
    }

    $manifest = [ordered]@{
        schemaVersion = 2
        releaseId = $releaseId
        commit = $commit
        frontendBuildCommit = $frontendBuildCommit
        shortCommit = $shortCommit
        createdUtc = [DateTime]::UtcNow.ToString('o')
        environment = $Environment
        publicBaseUrl = $PublicBaseUrl.GetLeftPart([UriPartial]::Authority)
        nextPublicApiUrl = "$($PublicBaseUrl.GetLeftPart([UriPartial]::Authority))/api"
        buildId = $buildId
        cacheVersion = $cacheVersion
        syncfusionFrontendLicensed = $true
        buildMachine = [ordered]@{
            operatingSystem = $operatingSystem.Caption
            architecture = $env:PROCESSOR_ARCHITECTURE
            nodeArchitecture = $nodeArchitecture
            availablePhysicalMemoryBytes = $availableMemoryBytes
        }
        versions = [ordered]@{
            node = $nodeVersion
            npm = $npmVersion
            next = $runtimeNext.version
            configuredNodeHeapMb = $heapMb
        }
        api = [ordered]@{
            file = 'api.zip'
            bytes = (Get-Item $apiZip).Length
            sha256 = $hashes.api
        }
        frontend = [ordered]@{
            file = 'frontend.zip'
            bytes = (Get-Item $frontendZip).Length
            sha256 = $hashes.frontend
        }
        timings = @($timings)
        totalBuildSeconds = [Math]::Round(([DateTime]::UtcNow - $runStarted).TotalSeconds, 2)
    }
    $manifestPath = Join-Path $releaseDirectory 'release-manifest.json'
    [IO.File]::WriteAllText($manifestPath, ($manifest | ConvertTo-Json -Depth 10),
        (New-Object Text.UTF8Encoding($false)))
    Write-Host "`nRELEASE_BUILD_PASSED|$releaseId|$manifestPath" -ForegroundColor Green
} finally {
    Restore-ProcessEnvironment
    $license = $null
    if ($workDirectory -and (Test-Path -LiteralPath $workDirectory)) {
        Assert-SafeChildPath $workDirectory $workRoot
        Remove-Item -LiteralPath $workDirectory -Recurse -Force
    }
    Pop-Location
}
