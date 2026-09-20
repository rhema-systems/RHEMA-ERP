$ErrorActionPreference = 'Stop'
$rehearsalRepo = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$rehearsalRoot = Join-Path $rehearsalRepo 'local-artifacts/po-rehearsal-20260909'
if (Test-Path -LiteralPath $rehearsalRoot) { throw 'Rehearsal runtime already exists; refusing to replace it.' }
[void](New-Item -ItemType Directory -Path $rehearsalRoot)
function Copy-RehearsalTree([string]$Source, [string]$Destination, [string[]]$Extra = @()) {
    if (-not (Test-Path -LiteralPath $Source)) { return }
    & robocopy $Source $Destination /E /XJ /R:1 /W:1 /NFL /NDL /NJH /NJS @Extra | Out-Null
    if ($LASTEXITCODE -ge 8) { throw "Copy failed for $Source" }
}
Copy-RehearsalTree (Join-Path $rehearsalRepo 'src/ErpSystem.Api/bin/Debug/net8.0') (Join-Path $rehearsalRoot 'api') @('/XF','appsettings*.json','/XD','logs','uploads')
Copy-RehearsalTree (Join-Path $rehearsalRepo 'frontend/.next/standalone') (Join-Path $rehearsalRoot 'frontend') @('/XF','.env*','/XD','cache')
Copy-RehearsalTree (Join-Path $rehearsalRepo 'frontend/.next/static') (Join-Path $rehearsalRoot 'frontend/.next/static')
Copy-RehearsalTree (Join-Path $rehearsalRepo 'frontend/public') (Join-Path $rehearsalRoot 'frontend/public')
# Copy physical evidence; never point writable rehearsal storage at a UAT junction.
foreach ($rehearsalUploadSource in @('local-artifacts/attendance-retry-api-20260908/uploads','uploads','src/ErpSystem.Api/uploads')) {
    Copy-RehearsalTree (Join-Path $rehearsalRepo $rehearsalUploadSource) (Join-Path $rehearsalRoot 'api/uploads')
}
foreach ($rehearsalPrivateSource in @('secure-file-storage','src/ErpSystem.Api/secure-file-storage')) {
    Copy-RehearsalTree (Join-Path $rehearsalRepo $rehearsalPrivateSource) (Join-Path $rehearsalRoot 'api/secure-file-storage')
}
Copy-RehearsalTree (Join-Path $rehearsalRepo 'src/ErpSystem.Api/wwwroot') (Join-Path $rehearsalRoot 'api/wwwroot') @('/XF','appsettings*.json')
# Mechanical retargeting is restricted to the copied, generated build. Original source/build files are untouched.
$rehearsalChanged = 0
$rehearsalUtf8 = [System.Text.UTF8Encoding]::new($false)
$rehearsalFrontend = (Resolve-Path (Join-Path $rehearsalRoot 'frontend')).Path
$rehearsalBuildFiles = @(& rg --files --hidden (Join-Path $rehearsalFrontend '.next') -g '*.js' -g '*.json' -g '*.html' -g '*.rsc')
$rehearsalBuildFiles += Join-Path $rehearsalFrontend 'server.js'
foreach ($rehearsalBuildFile in $rehearsalBuildFiles) {
    $rehearsalAbsoluteFile = [IO.Path]::GetFullPath($rehearsalBuildFile)
    if (-not $rehearsalAbsoluteFile.StartsWith($rehearsalFrontend + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Build target escaped the rehearsal directory.' }
    $rehearsalText = [IO.File]::ReadAllText($rehearsalAbsoluteFile)
    $rehearsalUpdated = $rehearsalText.Replace('http://localhost:5000','http://127.0.0.1:5002').Replace('http://localhost:3000','http://127.0.0.1:3002').Replace('https://149.102.145.190:8443','http://127.0.0.1:5002')
    if ($rehearsalUpdated -cne $rehearsalText) {
        [IO.File]::WriteAllText($rehearsalAbsoluteFile, $rehearsalUpdated, $rehearsalUtf8)
        $rehearsalChanged++
    }
}
if ($rehearsalChanged -eq 0) { throw 'No build API references were retargeted; inspect the copied build.' }
$rehearsalUnsafe = @(& rg -l --hidden 'http://localhost:5000|https://149\.102\.145\.190:8443' (Join-Path $rehearsalFrontend '.next') -g '*.js' -g '*.json' -g '*.html' -g '*.rsc')
if ($rehearsalUnsafe.Count -gt 0) { throw 'Original API addresses remain in the rehearsal build.' }
[pscustomobject]@{Runtime=$rehearsalRoot;RetargetedGeneratedFiles=$rehearsalChanged;OriginalBuildUntouched=$true;StorageCopied=$true} | ConvertTo-Json -Compress
