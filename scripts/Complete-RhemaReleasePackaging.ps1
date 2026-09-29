[CmdletBinding()]
param(
    [ValidateSet('Test')]
    [string]$Environment = 'Test',
    [Parameter(Mandatory = $true)]
    [uri]$PublicBaseUrl,
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[0-9a-fA-F]{7,40}$')]
    [string]$ExpectedCommit,
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[0-9a-fA-F]{7,40}$')]
    [string]$ArtifactSourceCommit,
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[0-9a-fA-F]{7,40}$')]
    [string]$FrontendBuildCommit,
    [string]$ArtifactRoot
)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repositoryRoot = Split-Path -Parent $scriptRoot
$releaseRoot = if ([string]::IsNullOrWhiteSpace($ArtifactRoot)) {
    Join-Path $repositoryRoot 'artifacts\vps-releases'
} else { [IO.Path]::GetFullPath($ArtifactRoot) }
Add-Type -AssemblyName System.IO.Compression.FileSystem

function Assert-True {
    param([bool]$Condition, [string]$Message)
    if (-not $Condition) { throw $Message }
}

function Resolve-Commit {
    param([string]$Value, [string]$Label)
    $resolved = (& git rev-parse "$Value`^{commit}").Trim()
    Assert-True ($LASTEXITCODE -eq 0 -and $resolved -match '^[0-9a-f]{40}$') `
        "Could not resolve $Label $Value."
    return $resolved
}

function Assert-Ancestor {
    param([string]$Ancestor, [string]$Descendant, [string]$Message)
    & git merge-base --is-ancestor $Ancestor $Descendant
    Assert-True ($LASTEXITCODE -eq 0) $Message
}

function Get-ZipNames {
    param([IO.Compression.ZipArchive]$Archive)
    return @($Archive.Entries | ForEach-Object { $_.FullName.Replace('\', '/') })
}

function Get-ZipEntry {
    param([IO.Compression.ZipArchive]$Archive, [string]$Name)
    $matching = @($Archive.Entries | Where-Object {
        [string]::Equals($_.FullName.Replace('\', '/'), $Name,
            [StringComparison]::OrdinalIgnoreCase)
    })
    Assert-True ($matching.Count -eq 1) "ZIP is missing or duplicates $Name."
    return $matching[0]
}

function Read-ZipText {
    param([IO.Compression.ZipArchive]$Archive, [string]$Name)
    $entry = Get-ZipEntry $Archive $Name
    $stream = $entry.Open()
    $reader = New-Object IO.StreamReader($stream, (New-Object Text.UTF8Encoding($false)), $true)
    try { return $reader.ReadToEnd() }
    finally { $reader.Dispose(); $stream.Dispose() }
}

Push-Location $repositoryRoot
try {
    Assert-True ($PublicBaseUrl.IsAbsoluteUri -and $PublicBaseUrl.Scheme -eq 'https' -and
        -not $PublicBaseUrl.UserInfo -and -not $PublicBaseUrl.Query -and
        -not $PublicBaseUrl.Fragment -and $PublicBaseUrl.AbsolutePath -eq '/') `
        'PublicBaseUrl must be an HTTPS origin without a path, query, credentials, or fragment.'
    $commit = Resolve-Commit $ExpectedCommit 'ExpectedCommit'
    $head = (& git rev-parse HEAD).Trim()
    Assert-True ($head -eq $commit) "HEAD $head does not equal ExpectedCommit $commit."
    Assert-True (@(& git status --porcelain).Count -eq 0) `
        'The packaging recovery checkout must be clean.'

    $artifactCommit = Resolve-Commit $ArtifactSourceCommit 'ArtifactSourceCommit'
    $frontendCommit = Resolve-Commit $FrontendBuildCommit 'FrontendBuildCommit'
    Assert-Ancestor $artifactCommit $commit `
        'The artifact source commit is not an ancestor of the current release commit.'
    Assert-Ancestor $frontendCommit $artifactCommit `
        'The frontend build commit is not an ancestor of the artifact source commit.'

    $applicationInputs = @('src', 'frontend', 'Directory.Build.props',
        'Directory.Build.targets', 'global.json', 'NuGet.config', 'ErpSystem.sln')
    & git diff --quiet "$artifactCommit..$commit" -- @applicationInputs
    Assert-True ($LASTEXITCODE -eq 0) `
        'Application build inputs changed after the compressed artifact was created.'
    & git diff --quiet "$frontendCommit..$artifactCommit" -- frontend
    Assert-True ($LASTEXITCODE -eq 0) `
        'Frontend files changed after the reusable frontend build was created.'

    Assert-True (Test-Path -LiteralPath $releaseRoot) `
        "Release artifact root does not exist: $releaseRoot"
    $prefix = $artifactCommit.Substring(0, 8) + '-'
    $candidate = @(Get-ChildItem -LiteralPath $releaseRoot -Directory | Where-Object {
        $manifestPath = Join-Path $_.FullName 'release-manifest.json'
        $recoverableManifest = -not (Test-Path -LiteralPath $manifestPath)
        if (-not $recoverableManifest) {
            try {
                $priorManifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
                $recoverableManifest = $priorManifest.packagingRecovered -eq $true -and
                    $priorManifest.artifactSourceCommit -eq $artifactCommit
            } catch { $recoverableManifest = $false }
        }
        $_.Name.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase) -and
        $recoverableManifest -and
        (Test-Path -LiteralPath (Join-Path $_.FullName 'api.zip')) -and
        (Test-Path -LiteralPath (Join-Path $_.FullName 'frontend.zip'))
    } | Sort-Object LastWriteTimeUtc -Descending | Select-Object -First 1)
    Assert-True ($candidate.Count -eq 1) `
        "No incomplete compressed release for $artifactCommit was found."

    $releaseDirectory = $candidate[0].FullName
    $releaseId = $candidate[0].Name
    Assert-True ($releaseId -match '^[a-zA-Z0-9-]+$') 'Release ID contains unsupported characters.'
    $apiZip = Join-Path $releaseDirectory 'api.zip'
    $frontendZip = Join-Path $releaseDirectory 'frontend.zip'

    $apiArchive = [IO.Compression.ZipFile]::OpenRead($apiZip)
    try {
        $apiNames = Get-ZipNames $apiArchive
        Assert-True (@($apiNames | Where-Object {
            [string]::Equals($_, 'ErpSystem.Api.exe', [StringComparison]::OrdinalIgnoreCase)
        }).Count -eq 1) 'API ZIP does not contain exactly one root API executable.'
        Assert-True (@($apiNames | Where-Object {
            $_ -match '(^|/)(appsettings[^/]*\.json|\.env[^/]*)$'
        }).Count -eq 0) 'API ZIP contains protected runtime configuration.'
    }
    finally { $apiArchive.Dispose() }

    $frontendArchive = [IO.Compression.ZipFile]::OpenRead($frontendZip)
    try {
        $frontendNames = Get-ZipNames $frontendArchive
        foreach ($expectedEntry in @('.next/BUILD_ID', '.next/required-server-files.json',
                '.next/server/middleware-manifest.json',
                'public/syncfusion/ej2-pdfviewer-lib/pdfium.js',
                'public/syncfusion/ej2-pdfviewer-lib/pdfium.wasm',
                'node_modules/next/package.json', 'package.json', 'package-lock.json',
                'next.config.js', 'public/sw.js')) {
            Assert-True (@($frontendNames | Where-Object { [string]::Equals(
                $_, $expectedEntry, [StringComparison]::OrdinalIgnoreCase) }).Count -eq 1) `
                "Frontend ZIP is missing or duplicates $expectedEntry."
        }
        Assert-True (@($frontendNames | Where-Object {
            $_ -match '(^|/)\.next/cache/'
        }).Count -eq 0) 'Frontend ZIP contains the Next.js build cache.'

        $buildId = (Read-ZipText $frontendArchive '.next/BUILD_ID').Trim()
        Assert-True ($buildId -and $buildId -ne 'development') 'Frontend BUILD_ID is invalid.'
        $middleware = Read-ZipText $frontendArchive '.next/server/middleware-manifest.json' |
            ConvertFrom-Json
        Assert-True ($middleware.middleware.'/'.env.__NEXT_BUILD_ID -eq $buildId) `
            'Frontend BUILD_ID and middleware build ID differ.'
        $nextPackage = Read-ZipText $frontendArchive 'node_modules/next/package.json' |
            ConvertFrom-Json
        $cacheVersion = "vps-$releaseId"
        $serviceWorker = Read-ZipText $frontendArchive 'public/sw.js'
        Assert-True ([regex]::Matches($serviceWorker,
            [regex]::Escape($cacheVersion)).Count -ge 4) `
            'Service worker cache identity does not match the release ID.'

        foreach ($entry in @($frontendArchive.Entries | Where-Object {
            $_.FullName -match '^\.next/(static|server)/' -and
            $_.FullName -match '\.(js|json|html|txt|rsc)$'
        })) {
            $stream = $entry.Open()
            $reader = New-Object IO.StreamReader($stream, (New-Object Text.UTF8Encoding($false)), $true)
            try {
                Assert-True (-not ($reader.ReadToEnd() -match
                    'localhost:5000|localhost:53484|localhost:7095')) `
                    "Compiled frontend entry contains a development API URL: $($entry.FullName)"
            }
            finally { $reader.Dispose(); $stream.Dispose() }
        }
    }
    finally { $frontendArchive.Dispose() }

    $nodeVersion = (& node --version).Trim()
    $npmVersion = (& npm.cmd --version).Trim()
    $operatingSystem = Get-CimInstance Win32_OperatingSystem
    $availableMemoryBytes = [long]$operatingSystem.FreePhysicalMemory * 1KB
    $started = [DateTime]::UtcNow
    $hashes = [ordered]@{
        api = (Get-FileHash $apiZip -Algorithm SHA256).Hash
        frontend = (Get-FileHash $frontendZip -Algorithm SHA256).Hash
    }
    $manifest = [ordered]@{
        schemaVersion = 2
        releaseId = $releaseId
        commit = $commit
        artifactSourceCommit = $artifactCommit
        frontendBuildCommit = $frontendCommit
        packagingRecovered = $true
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
            nodeArchitecture = (& node -p 'process.arch').Trim()
            availablePhysicalMemoryBytes = $availableMemoryBytes
        }
        versions = [ordered]@{
            node = $nodeVersion
            npm = $npmVersion
            next = $nextPackage.version
            configuredNodeHeapMb = 12288
        }
        api = [ordered]@{
            file = 'api.zip'; bytes = (Get-Item $apiZip).Length; sha256 = $hashes.api
        }
        frontend = [ordered]@{
            file = 'frontend.zip'; bytes = (Get-Item $frontendZip).Length;
            sha256 = $hashes.frontend
        }
        timings = @([ordered]@{
            name = 'Recover compressed release packaging'; status = 'Passed';
            startedUtc = $started.ToString('o');
            durationSeconds = [Math]::Round(([DateTime]::UtcNow - $started).TotalSeconds, 2)
        })
        totalBuildSeconds = 0
    }
    $manifestPath = Join-Path $releaseDirectory 'release-manifest.json'
    [IO.File]::WriteAllText($manifestPath, ($manifest | ConvertTo-Json -Depth 10),
        (New-Object Text.UTF8Encoding($false)))
    Write-Host "RELEASE_BUILD_PASSED|$releaseId|$manifestPath" -ForegroundColor Green
}
finally { Pop-Location }
