[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('UAT', 'PRODUCTION')]
    [string]$Environment,

    [Parameter(Mandatory = $true)]
    [string]$ApiBaseUrl,

    [string]$ZcsSdkDirectory,

    [Parameter(Mandatory = $true)]
    [string]$KeystorePath,

    [Parameter(Mandatory = $true)]
    [string]$AcceptanceEvidencePath,

    [switch]$VendorRedistributionApproved,
    [switch]$PortableFallback,
    [switch]$SkipInstall,
    [string]$OutputDirectory
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$mobileRoot = Join-Path $repoRoot 'apps\mobile'
$appConfigPath = Join-Path $mobileRoot 'app.json'
$acceptancePath = (Resolve-Path -LiteralPath $AcceptanceEvidencePath).Path
$resolvedKeystore = (Resolve-Path -LiteralPath $KeystorePath).Path

$sdkPath = $null
if ($PortableFallback) {
    if (-not [string]::IsNullOrWhiteSpace($ZcsSdkDirectory)) {
        throw 'PortableFallback cannot be combined with ZcsSdkDirectory because the portable build must exclude proprietary vendor artifacts.'
    }
}
else {
    if ([string]::IsNullOrWhiteSpace($ZcsSdkDirectory)) {
        throw 'A Z92S release requires ZcsSdkDirectory. Use -PortableFallback to build without the proprietary printer/scanner SDK.'
    }
    $sdkPath = (Resolve-Path -LiteralPath $ZcsSdkDirectory).Path
    if (-not $VendorRedistributionApproved) {
        throw 'A Z92S release cannot be built until written vendor redistribution approval is recorded. Pass -VendorRedistributionApproved only after that approval exists.'
    }
}
$hardwareProfile = if ($PortableFallback) { 'PortableFallback' } else { 'Z92S' }

$repoPrefix = $repoRoot.TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar
if ($resolvedKeystore.StartsWith($repoPrefix, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'The Android signing keystore must remain outside the repository.'
}

try { $serverUri = [Uri]$ApiBaseUrl }
catch { throw 'ApiBaseUrl must be a valid absolute HTTPS origin.' }
if (-not $serverUri.IsAbsoluteUri -or $serverUri.Scheme -cne 'https') {
    throw 'UAT and production Mobile POS releases require an HTTPS API origin.'
}
if (-not [string]::IsNullOrWhiteSpace($serverUri.UserInfo) -or
    -not [string]::IsNullOrWhiteSpace($serverUri.Query) -or
    -not [string]::IsNullOrWhiteSpace($serverUri.Fragment) -or
    ($serverUri.AbsolutePath -and $serverUri.AbsolutePath -cne '/')) {
    throw 'ApiBaseUrl must contain only the HTTPS origin, without credentials, path, query, or fragment.'
}
$apiOrigin = $serverUri.GetLeftPart([UriPartial]::Authority)

function Resolve-Executable([string[]]$Names) {
    foreach ($name in $Names) {
        $command = Get-Command $name -ErrorAction SilentlyContinue
        if ($command) { return $command.Source }
    }
    throw "Required executable was not found: $($Names -join ', ')."
}

function Resolve-Npm {
    $command = Get-Command npm.cmd -ErrorAction SilentlyContinue
    if (-not $command) { $command = Get-Command npm -ErrorAction SilentlyContinue }
    if ($command) { return $command.Source }

    $userProfile = [Environment]::GetFolderPath('UserProfile')
    foreach ($candidate in @(
        (Join-Path $userProfile '.codex\tools\node-portable\npm.cmd'),
        (Join-Path $userProfile 'AppData\Roaming\npm\npm.cmd'),
        (Join-Path $env:ProgramFiles 'nodejs\npm.cmd')
    )) {
        if ($candidate -and (Test-Path -LiteralPath $candidate)) {
            $nodeDirectory = Split-Path -Parent $candidate
            [Environment]::SetEnvironmentVariable('PATH', "$nodeDirectory;$env:PATH", 'Process')
            return $candidate
        }
    }
    throw 'Required npm executable was not found.'
}

function Invoke-LoggedCommand(
    [string]$Name,
    [string]$Executable,
    [string[]]$Arguments,
    [string]$WorkingDirectory,
    [string]$LogPath
) {
    Write-Host "`n==> $Name"
    $started = [DateTime]::UtcNow
    Push-Location $WorkingDirectory
    try {
        $previousPreference = $ErrorActionPreference
        $ErrorActionPreference = 'Continue'
        try {
            $output = @(& $Executable @Arguments 2>&1)
            $exitCode = $LASTEXITCODE
        }
        finally {
            $ErrorActionPreference = $previousPreference
        }
        $output | ForEach-Object { $_.ToString() } | Set-Content -LiteralPath $LogPath -Encoding utf8
        if ($exitCode -ne 0) {
            $tail = ($output | Select-Object -Last 40 | ForEach-Object { $_.ToString() }) -join [Environment]::NewLine
            throw "$Name failed with exit code $exitCode.$([Environment]::NewLine)$tail"
        }
        return [pscustomobject]@{
            Name = $Name
            Status = 'Passed'
            DurationSeconds = [Math]::Round(([DateTime]::UtcNow - $started).TotalSeconds, 1)
            Log = Split-Path -Leaf $LogPath
        }
    }
    finally { Pop-Location }
}

$git = Resolve-Executable @('git.exe', 'git')
$npm = Resolve-Npm
$java = Resolve-Executable @('java.exe', 'java')
$jarsigner = Resolve-Executable @('jarsigner.exe', 'jarsigner')

$androidSdkRoot = if ($env:ANDROID_SDK_ROOT) { $env:ANDROID_SDK_ROOT } else { $env:ANDROID_HOME }
if ([string]::IsNullOrWhiteSpace($androidSdkRoot) -or -not (Test-Path -LiteralPath $androidSdkRoot -PathType Container)) {
    throw 'ANDROID_SDK_ROOT or ANDROID_HOME must identify an installed Android SDK.'
}
$apksigner = Get-ChildItem -LiteralPath (Join-Path $androidSdkRoot 'build-tools') -Directory |
    Sort-Object Name -Descending |
    ForEach-Object { Join-Path $_.FullName 'apksigner.bat' } |
    Where-Object { Test-Path -LiteralPath $_ } |
    Select-Object -First 1
if (-not $apksigner) { throw 'Android build-tools does not contain apksigner.bat.' }

$requiredSigningVariables = @(
    'RHEMA_ANDROID_KEYSTORE_PASSWORD',
    'RHEMA_ANDROID_KEY_ALIAS',
    'RHEMA_ANDROID_KEY_PASSWORD'
)
$missingSigningVariables = @($requiredSigningVariables | Where-Object {
    [string]::IsNullOrWhiteSpace([Environment]::GetEnvironmentVariable($_, 'Process'))
})
if ($missingSigningVariables.Count -gt 0) {
    throw "Set the signing values in process environment variables before building: $($missingSigningVariables -join ', ')."
}

$dirty = @(& $git -C $repoRoot status --porcelain --untracked-files=all)
if ($LASTEXITCODE -ne 0) { throw 'Could not inspect the Git worktree.' }
if ($dirty.Count -gt 0) { throw 'The signed release must be built from a clean Git worktree.' }
$commit = (@(& $git -C $repoRoot rev-parse HEAD))[0].Trim().ToLowerInvariant()
if ($LASTEXITCODE -ne 0 -or $commit -notmatch '^[0-9a-f]{40}$') { throw 'Could not resolve the exact release commit.' }

$acceptance = Get-Content -LiteralPath $acceptancePath -Raw | ConvertFrom-Json
if ($acceptance.Gate -cne 'acceptance:mobile-pos' -or -not [bool]$acceptance.Passed) {
    throw 'AcceptanceEvidencePath is not a passing acceptance:mobile-pos result.'
}
if ([string]$acceptance.Commit -cne $commit) {
    throw "Acceptance evidence commit $($acceptance.Commit) does not match release commit $commit."
}
if (-not [bool]$acceptance.NativeAndroidBuildRequested) {
    throw 'Release evidence must include a native Android compile.'
}
if ([string]$acceptance.HardwareProfile -cne $hardwareProfile) {
    throw "Acceptance evidence hardware profile $($acceptance.HardwareProfile) does not match requested release profile $hardwareProfile."
}
if ($PortableFallback -and [bool]$acceptance.ZcsSdkPackagingRequested) {
    throw 'Portable fallback release evidence must prove that Z92S SDK packaging was not requested.'
}
if (-not $PortableFallback -and -not [bool]$acceptance.ZcsSdkPackagingRequested) {
    throw 'Z92S release evidence must include hash-verified Z92S packaging.'
}

$appConfig = Get-Content -LiteralPath $appConfigPath -Raw | ConvertFrom-Json
$versionName = [string]$appConfig.expo.version
$versionCode = [int]$appConfig.expo.android.versionCode
if ([string]::IsNullOrWhiteSpace($versionName) -or $versionCode -lt 1) {
    throw 'app.json must contain a nonempty version and positive Android versionCode.'
}

if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $repoRoot '.artifacts\mobile-pos\releases'
}
$profileSlug = if ($PortableFallback) { 'portable' } else { 'z92s' }
$releaseId = "{0}-{1}-v{2}-{3}-{4}" -f $Environment.ToLowerInvariant(), $profileSlug, $versionName, $versionCode, $commit.Substring(0, 12)
$releaseDirectory = Join-Path ([IO.Path]::GetFullPath($OutputDirectory)) $releaseId
if (Test-Path -LiteralPath $releaseDirectory) { throw "Release directory already exists: $releaseDirectory" }
New-Item -ItemType Directory -Path $releaseDirectory -Force | Out-Null

$environmentNames = @(
    'EXPO_PUBLIC_RHEMA_ENVIRONMENT',
    'EXPO_PUBLIC_API_BASE_URL',
    'RHEMA_ZCS_ENABLED',
    'RHEMA_ZCS_SDK_DIR',
    'RHEMA_ANDROID_SIGNING_ENABLED',
    'RHEMA_ANDROID_KEYSTORE_PATH'
)
$previousEnvironment = @{}
foreach ($name in $environmentNames) {
    $previousEnvironment[$name] = [Environment]::GetEnvironmentVariable($name, 'Process')
}

$stages = [Collections.Generic.List[object]]::new()
try {
    [Environment]::SetEnvironmentVariable('EXPO_PUBLIC_RHEMA_ENVIRONMENT', $Environment, 'Process')
    [Environment]::SetEnvironmentVariable('EXPO_PUBLIC_API_BASE_URL', $apiOrigin, 'Process')
    [Environment]::SetEnvironmentVariable('RHEMA_ZCS_ENABLED', $(if ($PortableFallback) { 'false' } else { 'true' }), 'Process')
    [Environment]::SetEnvironmentVariable('RHEMA_ZCS_SDK_DIR', $sdkPath, 'Process')
    [Environment]::SetEnvironmentVariable('RHEMA_ANDROID_SIGNING_ENABLED', 'true', 'Process')
    [Environment]::SetEnvironmentVariable('RHEMA_ANDROID_KEYSTORE_PATH', $resolvedKeystore, 'Process')

    if (-not $SkipInstall) {
        $stages.Add((Invoke-LoggedCommand 'Restore locked mobile dependencies' $npm @('ci', '--no-audit', '--no-fund') $mobileRoot (Join-Path $releaseDirectory 'npm-ci.log')))
    }
    $prebuildName = if ($PortableFallback) { 'Clean Android prebuild without proprietary SDK' } else { 'Clean Android prebuild with audited Z92S SDK' }
    $stages.Add((Invoke-LoggedCommand $prebuildName $npm @('run', 'prebuild:android') $mobileRoot (Join-Path $releaseDirectory 'prebuild.log')))

    if ($PortableFallback) {
        $forbiddenSdkArtifacts = @(
            (Join-Path $mobileRoot 'android\app\libs\zcs-smartpos-1.8.1.jar'),
            (Join-Path $mobileRoot 'android\app\src\main\jniLibs\arm64-v8a\libSmartPosJni.so'),
            (Join-Path $mobileRoot 'android\app\src\main\jniLibs\armeabi-v7a\libSmartPosJni.so')
        )
        $presentSdkArtifacts = @($forbiddenSdkArtifacts | Where-Object { Test-Path -LiteralPath $_ })
        if ($presentSdkArtifacts.Count -gt 0) {
            throw "Portable fallback unexpectedly contains proprietary Z92S artifacts: $($presentSdkArtifacts -join ', ')"
        }
        $generatedGradle = Get-Content -LiteralPath (Join-Path $mobileRoot 'android\app\build.gradle') -Raw
        if ($generatedGradle.IndexOf("implementation files('libs/zcs-smartpos-1.8.1.jar')", [StringComparison]::Ordinal) -ge 0) {
            throw 'Portable fallback unexpectedly references the proprietary Z92S JAR.'
        }
    }

    $gradleWrapper = Join-Path $mobileRoot 'android\gradlew.bat'
    if (-not (Test-Path -LiteralPath $gradleWrapper)) { throw 'The clean prebuild did not produce gradlew.bat.' }
    $stages.Add((Invoke-LoggedCommand 'Signed APK and AAB build' $gradleWrapper @('clean', 'app:assembleRelease', 'app:bundleRelease', '--no-daemon') (Join-Path $mobileRoot 'android') (Join-Path $releaseDirectory 'gradle-release.log')))

    $sourceApk = Join-Path $mobileRoot 'android\app\build\outputs\apk\release\app-release.apk'
    $sourceAab = Join-Path $mobileRoot 'android\app\build\outputs\bundle\release\app-release.aab'
    if (-not (Test-Path -LiteralPath $sourceApk -PathType Leaf)) { throw 'Signed release APK was not produced.' }
    if (-not (Test-Path -LiteralPath $sourceAab -PathType Leaf)) { throw 'Signed release AAB was not produced.' }

    $apkName = "rhema-field-pos-$releaseId.apk"
    $aabName = "rhema-field-pos-$releaseId.aab"
    $apkPath = Join-Path $releaseDirectory $apkName
    $aabPath = Join-Path $releaseDirectory $aabName
    Copy-Item -LiteralPath $sourceApk -Destination $apkPath
    Copy-Item -LiteralPath $sourceAab -Destination $aabPath

    $apkVerifyLog = Join-Path $releaseDirectory 'apksigner-verify.log'
    $stages.Add((Invoke-LoggedCommand 'Verify APK signature and certificates' $apksigner @('verify', '--verbose', '--print-certs', $apkPath) $releaseDirectory $apkVerifyLog))
    $stages.Add((Invoke-LoggedCommand 'Verify AAB JAR signature' $jarsigner @('-verify', '-strict', '-certs', $aabPath) $releaseDirectory (Join-Path $releaseDirectory 'jarsigner-verify.log')))

    $certificateLine = Get-Content -LiteralPath $apkVerifyLog |
        Where-Object { $_ -match 'certificate SHA-256 digest:' } |
        Select-Object -First 1
    if (-not $certificateLine -or $certificateLine -notmatch 'digest:\s*([0-9A-Fa-f:]+)') {
        throw 'Could not read the APK signing certificate SHA-256 digest.'
    }
    $certificateSha256 = $Matches[1].Replace(':', '').ToUpperInvariant()

    $manifest = [ordered]@{
        SchemaVersion = 1
        ReleaseId = $releaseId
        CreatedAtUtc = [DateTime]::UtcNow.ToString('o')
        Environment = $Environment
        HardwareProfile = $hardwareProfile
        ApiOrigin = $apiOrigin
        GitCommit = $commit
        VersionName = $versionName
        VersionCode = $versionCode
        AndroidPackage = [string]$appConfig.expo.android.package
        VendorRedistributionApprovalConfirmedByOperator = [bool](-not $PortableFallback -and $VendorRedistributionApproved)
        FallbackCapabilities = if ($PortableFallback) { @('AndroidSystemPrint', 'RetainedPdfShare', 'CameraBarcode', 'ManualBarcode', 'KeyboardWedge') } else { @() }
        AcceptanceEvidence = [ordered]@{
            Path = $acceptancePath
            Sha256 = (Get-FileHash -LiteralPath $acceptancePath -Algorithm SHA256).Hash.ToUpperInvariant()
        }
        SigningCertificateSha256 = $certificateSha256
        Artifacts = @(
            [ordered]@{ File = $apkName; Sha256 = (Get-FileHash -LiteralPath $apkPath -Algorithm SHA256).Hash.ToUpperInvariant(); Bytes = (Get-Item -LiteralPath $apkPath).Length },
            [ordered]@{ File = $aabName; Sha256 = (Get-FileHash -LiteralPath $aabPath -Algorithm SHA256).Hash.ToUpperInvariant(); Bytes = (Get-Item -LiteralPath $aabPath).Length }
        )
        ZcsSdkArtifacts = if ($PortableFallback) { @() } else { @(
                [ordered]@{ File = 'SmartPos_1.8.1_R231213.jar'; Sha256 = '3A65BF1A26D59730C014D79BA7B5AA8744BAB6E0B5275A2C055B996F4A7277E9' },
                [ordered]@{ File = 'arm64-v8a/libSmartPosJni.so'; Sha256 = 'DE5CBF76EAFC3D0FBF1767BD0E8007E6217A2C81A6FDACE02FBD511A1D9FF9BF' },
                [ordered]@{ File = 'armeabi-v7a/libSmartPosJni.so'; Sha256 = '7D8821DD051F83072023744019F1EDD86AB7CF0B0EAE2D7FD674DF7ED844A090' }
            ) }
        Stages = @($stages)
    }
    $manifestPath = Join-Path $releaseDirectory 'mobile-pos-release-manifest.json'
    $manifest | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $manifestPath -Encoding utf8
    Write-Host "`nPASS signed Mobile POS release: $releaseDirectory"
}
finally {
    foreach ($name in $environmentNames) {
        [Environment]::SetEnvironmentVariable($name, $previousEnvironment[$name], 'Process')
    }
}
