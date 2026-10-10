[CmdletBinding()]
param(
    [switch]$SkipInstall,
    [string]$ZcsSdkDirectory,
    [switch]$BuildNativeAndroid,
    [string]$EvidenceDirectory
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$mobileRoot = Join-Path $repoRoot 'apps\mobile'
$frontendRoot = Join-Path $repoRoot 'frontend'
$apiProject = Join-Path $repoRoot 'src\ErpSystem.Api\ErpSystem.Api.csproj'
$testProject = Join-Path $repoRoot 'tests\ErpSystem.Api.Tests\ErpSystem.Api.Tests.csproj'
$dataProject = Join-Path $repoRoot 'src\ErpSystem.Data\ErpSystem.Data.csproj'

if ([string]::IsNullOrWhiteSpace($EvidenceDirectory)) {
    $EvidenceDirectory = Join-Path $repoRoot '.artifacts\mobile-pos\acceptance'
}
$EvidenceDirectory = [IO.Path]::GetFullPath($EvidenceDirectory)
$stamp = [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss')
$runDirectory = Join-Path $EvidenceDirectory $stamp
New-Item -ItemType Directory -Path $runDirectory -Force | Out-Null

$stages = [Collections.Generic.List[object]]::new()
$startedUtc = [DateTime]::UtcNow
$failure = $null

function Resolve-Executable([string[]]$Names) {
    foreach ($name in $Names) {
        $command = Get-Command $name -ErrorAction SilentlyContinue
        if ($command) { return $command.Source }
    }
    throw "Required executable was not found: $($Names -join ', ')."
}

function Resolve-Npm {
    $fromPath = Get-Command npm.cmd -ErrorAction SilentlyContinue
    if (-not $fromPath) { $fromPath = Get-Command npm -ErrorAction SilentlyContinue }
    if ($fromPath) { return $fromPath.Source }

    $userProfile = [Environment]::GetFolderPath('UserProfile')
    $candidates = @(
        (Join-Path $userProfile '.codex\tools\node-portable\npm.cmd'),
        (Join-Path $userProfile 'AppData\Roaming\npm\npm.cmd'),
        (Join-Path $env:ProgramFiles 'nodejs\npm.cmd')
    )
    foreach ($candidate in $candidates) {
        if ($candidate -and (Test-Path -LiteralPath $candidate)) {
            $nodeDirectory = Split-Path -Parent $candidate
            [Environment]::SetEnvironmentVariable('PATH', "$nodeDirectory;$env:PATH", 'Process')
            return $candidate
        }
    }
    throw 'Required npm executable was not found.'
}

function Resolve-RequiredDotnet {
    $requiredVersion = (Get-Content -LiteralPath (Join-Path $repoRoot 'global.json') -Raw | ConvertFrom-Json).sdk.version
    $candidates = [Collections.Generic.List[string]]::new()
    $fromPath = Get-Command dotnet.exe -ErrorAction SilentlyContinue
    if (-not $fromPath) { $fromPath = Get-Command dotnet -ErrorAction SilentlyContinue }
    if ($fromPath) { $candidates.Add($fromPath.Source) }
    $userDotnet = Join-Path ([Environment]::GetFolderPath('UserProfile')) '.dotnet\dotnet.exe'
    if (Test-Path -LiteralPath $userDotnet) { $candidates.Add($userDotnet) }

    foreach ($candidate in @($candidates | Select-Object -Unique)) {
        $versions = @(& $candidate --list-sdks 2>$null)
        if ($versions | Where-Object { $_ -match "^$([regex]::Escape($requiredVersion))\s" }) {
            $dotnetRoot = Split-Path -Parent $candidate
            $userTools = Join-Path ([Environment]::GetFolderPath('UserProfile')) '.dotnet\tools'
            [Environment]::SetEnvironmentVariable('DOTNET_ROOT', $dotnetRoot, 'Process')
            [Environment]::SetEnvironmentVariable('PATH', "$dotnetRoot;$userTools;$env:PATH", 'Process')
            return $candidate
        }
    }
    throw "The repository requires .NET SDK $requiredVersion, but no matching dotnet host was found."
}

function Get-RepoRelativePath([string]$Path) {
    $fullPath = [IO.Path]::GetFullPath($Path)
    $prefix = $repoRoot.TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar
    if ($fullPath.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) {
        return $fullPath.Substring($prefix.Length)
    }
    return $fullPath
}

function Add-SkippedStage([string]$Name, [string]$Reason) {
    $stages.Add([pscustomobject]@{
        Name = $Name
        Status = 'Skipped'
        DurationSeconds = 0
        Detail = $Reason
        Log = $null
    })
}

function Invoke-CheckedCommand(
    [string]$Name,
    [string]$Executable,
    [string[]]$Arguments,
    [string]$WorkingDirectory
) {
    $stageStarted = [DateTime]::UtcNow
    $safeName = ($Name -replace '[^A-Za-z0-9.-]', '-').Trim('-').ToLowerInvariant()
    $logPath = Join-Path $runDirectory "$safeName.log"
    Write-Host "`n==> $Name"
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
        $output | ForEach-Object { $_.ToString() } | Set-Content -LiteralPath $logPath -Encoding utf8
        if ($exitCode -ne 0) {
            $tail = (($output | Select-Object -Last 35 | ForEach-Object { $_.ToString() }) -join [Environment]::NewLine)
            throw "$Name failed with exit code $exitCode.$([Environment]::NewLine)$tail"
        }
        $duration = [Math]::Round(([DateTime]::UtcNow - $stageStarted).TotalSeconds, 1)
        $stages.Add([pscustomobject]@{
            Name = $Name
            Status = 'Passed'
            DurationSeconds = $duration
            Detail = "Exit code $exitCode"
            Log = Get-RepoRelativePath $logPath
        })
        Write-Host "PASS $Name ($duration s)"
    }
    catch {
        $duration = [Math]::Round(([DateTime]::UtcNow - $stageStarted).TotalSeconds, 1)
        $stages.Add([pscustomobject]@{
            Name = $Name
            Status = 'Failed'
            DurationSeconds = $duration
            Detail = $_.Exception.Message
            Log = Get-RepoRelativePath $logPath
        })
        throw
    }
    finally {
        Pop-Location
    }
}

function Invoke-AssertionStage([string]$Name, [scriptblock]$Assertion) {
    $stageStarted = [DateTime]::UtcNow
    Write-Host "`n==> $Name"
    try {
        $detail = & $Assertion
        $duration = [Math]::Round(([DateTime]::UtcNow - $stageStarted).TotalSeconds, 1)
        $stages.Add([pscustomobject]@{
            Name = $Name
            Status = 'Passed'
            DurationSeconds = $duration
            Detail = [string]$detail
            Log = $null
        })
        Write-Host "PASS $Name ($duration s)"
    }
    catch {
        $duration = [Math]::Round(([DateTime]::UtcNow - $stageStarted).TotalSeconds, 1)
        $stages.Add([pscustomobject]@{
            Name = $Name
            Status = 'Failed'
            DurationSeconds = $duration
            Detail = $_.Exception.Message
            Log = $null
        })
        throw
    }
}

try {
    $npm = Resolve-Npm
    $dotnet = Resolve-RequiredDotnet
    $git = Resolve-Executable @('git.exe', 'git')

    Invoke-AssertionStage 'Reject tracked proprietary mobile binaries' {
        $tracked = @(
            @(& $git -C $repoRoot ls-files -- 'apps/mobile') |
                Where-Object { $_ -match '\.(aar|apk|aab|jar|so)$' }
        )
        if ($LASTEXITCODE -ne 0) { throw 'Could not inspect tracked Mobile POS files.' }
        if ($tracked.Count -gt 0) {
            throw "Proprietary or generated mobile binaries are tracked: $($tracked -join ', ')"
        }
        'No tracked AAR, APK, AAB, JAR, or SO files under apps/mobile.'
    }

    if ($SkipInstall) {
        Add-SkippedStage 'Restore locked mobile dependencies' 'Explicitly skipped; existing node_modules is being reused.'
        Add-SkippedStage 'Restore locked HQ frontend dependencies' 'Explicitly skipped; existing node_modules is being reused.'
    }
    else {
        Invoke-CheckedCommand 'Restore locked mobile dependencies' $npm @('ci', '--include=dev', '--no-audit', '--no-fund') $mobileRoot
        Invoke-CheckedCommand 'Restore locked HQ frontend dependencies' $npm @('ci', '--include=dev', '--no-audit', '--no-fund') $frontendRoot
    }

    Invoke-CheckedCommand 'Mobile TypeScript contract' $npm @('run', 'typecheck') $mobileRoot
    Invoke-CheckedCommand 'Mobile unit and adapter tests' $npm @('test', '--', '--reporter=verbose') $mobileRoot
    Invoke-CheckedCommand 'Expo dependency and configuration doctor' $npm @('run', 'doctor') $mobileRoot
    Invoke-CheckedCommand 'Android Hermes export' $npm @('run', 'export:android') $mobileRoot
    Invoke-CheckedCommand 'HQ frontend TypeScript contract' $npm @('run', 'type-check') $frontendRoot

    Invoke-CheckedCommand 'Migration-aware API Release build' $dotnet @(
        'build', $apiProject, '--configuration', 'Release', '--nologo', '-m:1',
        '-p:UseSharedCompilation=false', '-p:BuildInParallel=false'
    ) $repoRoot

    $trxPath = Join-Path $runDirectory 'mobile-pos-api-tests.trx'
    Invoke-CheckedCommand 'Focused Mobile POS API tests' $dotnet @(
        'test', $testProject, '--configuration', 'Release', '--nologo',
        '--filter', 'FullyQualifiedName~MobilePos',
        '--logger', 'trx;LogFileName=mobile-pos-api-tests.trx',
        '--results-directory', $runDirectory
    ) $repoRoot

    Invoke-AssertionStage 'Focused API test result contract' {
        if (-not (Test-Path -LiteralPath $trxPath)) { throw 'The focused Mobile POS TRX file was not produced.' }
        [xml]$trx = Get-Content -LiteralPath $trxPath -Raw
        $counters = $trx.TestRun.ResultSummary.Counters
        $total = [int]$counters.total
        $passed = [int]$counters.passed
        $failed = [int]$counters.failed
        if ($total -lt 1 -or $failed -ne 0 -or $passed -ne $total) {
            throw "Unexpected focused test counters: total=$total passed=$passed failed=$failed."
        }
        "$passed/$total focused API tests passed."
    }

    $migrationSql = Join-Path $runDirectory 'mobile-pos-migrations.sql'
    Invoke-CheckedCommand 'Idempotent Mobile POS migration script' $dotnet @(
        'ef', 'migrations', 'script', '20261008192833_AddPropertySalesOrderDepositLifecycle',
        '--idempotent', '--no-build', '--configuration', 'Release',
        '--context', 'ApplicationDbContext',
        '--project', $dataProject,
        '--startup-project', $apiProject,
        '--output', $migrationSql
    ) $repoRoot

    Invoke-AssertionStage 'Mobile POS migration content contract' {
        if (-not (Test-Path -LiteralPath $migrationSql)) { throw 'The idempotent migration SQL file was not produced.' }
        $sql = Get-Content -LiteralPath $migrationSql -Raw
        $requiredMarkers = @(
            '20261009054618_AddMobilePosFoundation',
            '20261009114817_AddMobilePosTransactionFoundation',
            '20261009235832_AddMobilePosTillCloseControl',
            '20261010003147_LinkMobilePosTillCloseBankDeposit',
            '20261010042645_AddMobilePosCustomerCollections',
            '20261010053732_AddMobilePosOfflineCollections',
            'MobilePosStores',
            'MobilePosSales',
            'MobilePosTillCloseSubmissions',
            'BankDepositBatchId',
            'MobilePosCollections',
            'MobilePosCollectionAllocations',
            'MobilePosCollectionTenders',
            'MobilePosOfflineGrantId',
            'OfflinePolicySnapshotHash'
        )
        $missing = @($requiredMarkers | Where-Object { $sql.IndexOf($_, [StringComparison]::Ordinal) -lt 0 })
        if ($missing.Count -gt 0) { throw "Migration SQL is missing: $($missing -join ', ')" }
        "All $($requiredMarkers.Count) Mobile POS migration markers are present."
    }

    if ([string]::IsNullOrWhiteSpace($ZcsSdkDirectory)) {
        Add-SkippedStage 'Z92S proprietary SDK packaging' 'Portable fallback profile selected; no proprietary SDK artifact may be packaged.'

        if ($BuildNativeAndroid) {
            $priorEnabled = [Environment]::GetEnvironmentVariable('RHEMA_ZCS_ENABLED', 'Process')
            $priorDirectory = [Environment]::GetEnvironmentVariable('RHEMA_ZCS_SDK_DIR', 'Process')
            $priorEnvironment = [Environment]::GetEnvironmentVariable('EXPO_PUBLIC_RHEMA_ENVIRONMENT', 'Process')
            try {
                [Environment]::SetEnvironmentVariable('RHEMA_ZCS_ENABLED', 'false', 'Process')
                [Environment]::SetEnvironmentVariable('RHEMA_ZCS_SDK_DIR', $null, 'Process')
                [Environment]::SetEnvironmentVariable('EXPO_PUBLIC_RHEMA_ENVIRONMENT', 'UAT', 'Process')
                Invoke-CheckedCommand 'Portable fallback clean Android prebuild' $npm @('run', 'prebuild:android') $mobileRoot

                Invoke-AssertionStage 'Portable fallback Android manifest security contract' {
                    [xml]$manifest = Get-Content -LiteralPath (Join-Path $mobileRoot 'android\app\src\main\AndroidManifest.xml')
                    $application = $manifest.manifest.application
                    if ($application.allowBackup -cne 'false') { throw 'UAT Android backup must be disabled.' }
                    if ($application.usesCleartextTraffic -cne 'false') { throw 'UAT Android cleartext traffic must be disabled.' }
                    'Android backup and cleartext traffic are disabled in the portable UAT prebuild.'
                }

                Invoke-AssertionStage 'Portable fallback Android release signing guard contract' {
                    $gradle = Get-Content -LiteralPath (Join-Path $mobileRoot 'android\app\build.gradle') -Raw
                    $requiredMarkers = @(
                        'RHEMA_ANDROID_RELEASE_SIGNING_START',
                        'RHEMA_ANDROID_SIGNING_ENABLED',
                        'RHEMA_ANDROID_KEYSTORE_PATH',
                        'releaseRequested',
                        'signingConfig signingConfigs.rhemaRelease'
                    )
                    $missing = @($requiredMarkers | Where-Object { $gradle.IndexOf($_, [StringComparison]::Ordinal) -lt 0 })
                    if ($missing.Count -gt 0) { throw "Generated Android release signing guard is missing: $($missing -join ', ')" }
                    'Release tasks require externally supplied signing values and override the generated debug signing configuration.'
                }

                Invoke-AssertionStage 'Portable fallback excludes proprietary Z92S artifacts' {
                    $forbidden = @(
                        'android\app\libs\zcs-smartpos-1.8.1.jar',
                        'android\app\src\main\jniLibs\arm64-v8a\libSmartPosJni.so',
                        'android\app\src\main\jniLibs\armeabi-v7a\libSmartPosJni.so'
                    )
                    $present = @($forbidden | Where-Object { Test-Path -LiteralPath (Join-Path $mobileRoot $_) })
                    if ($present.Count -gt 0) { throw "Portable fallback contains proprietary SDK artifacts: $($present -join ', ')" }
                    $gradle = Get-Content -LiteralPath (Join-Path $mobileRoot 'android\app\build.gradle') -Raw
                    if ($gradle.IndexOf("implementation files('libs/zcs-smartpos-1.8.1.jar')", [StringComparison]::Ordinal) -ge 0) {
                        throw 'Portable fallback unexpectedly references the proprietary Z92S JAR.'
                    }
                    'No Z92S JAR, native library, or Gradle dependency is packaged.'
                }

                $java = Get-Command java -ErrorAction SilentlyContinue
                if (-not $java) { throw 'Java is required for -BuildNativeAndroid but was not found.' }
                $gradleWrapper = Join-Path $mobileRoot 'android\gradlew.bat'
                if (-not (Test-Path -LiteralPath $gradleWrapper)) { throw 'The generated Gradle wrapper was not found.' }
                Invoke-CheckedCommand 'Portable fallback native Android debug compile' $gradleWrapper @('app:assembleDebug', '--no-daemon') (Join-Path $mobileRoot 'android')
            }
            finally {
                [Environment]::SetEnvironmentVariable('RHEMA_ZCS_ENABLED', $priorEnabled, 'Process')
                [Environment]::SetEnvironmentVariable('RHEMA_ZCS_SDK_DIR', $priorDirectory, 'Process')
                [Environment]::SetEnvironmentVariable('EXPO_PUBLIC_RHEMA_ENVIRONMENT', $priorEnvironment, 'Process')
            }
        }
        else {
            Add-SkippedStage 'Portable fallback native Android compile' 'Native compile was not requested.'
        }
    }
    else {
        $resolvedSdk = (Resolve-Path -LiteralPath $ZcsSdkDirectory).Path
        $priorEnabled = [Environment]::GetEnvironmentVariable('RHEMA_ZCS_ENABLED', 'Process')
        $priorDirectory = [Environment]::GetEnvironmentVariable('RHEMA_ZCS_SDK_DIR', 'Process')
        $priorEnvironment = [Environment]::GetEnvironmentVariable('EXPO_PUBLIC_RHEMA_ENVIRONMENT', 'Process')
        try {
            [Environment]::SetEnvironmentVariable('RHEMA_ZCS_ENABLED', 'true', 'Process')
            [Environment]::SetEnvironmentVariable('RHEMA_ZCS_SDK_DIR', $resolvedSdk, 'Process')
            [Environment]::SetEnvironmentVariable('EXPO_PUBLIC_RHEMA_ENVIRONMENT', 'PRODUCTION', 'Process')
            Invoke-CheckedCommand 'Z92S audited SDK packaging and clean prebuild' $npm @('run', 'prebuild:android') $mobileRoot

            Invoke-AssertionStage 'Production Android manifest security contract' {
                [xml]$manifest = Get-Content -LiteralPath (Join-Path $mobileRoot 'android\app\src\main\AndroidManifest.xml')
                $application = $manifest.manifest.application
                if ($application.allowBackup -cne 'false') { throw 'Production Android backup must be disabled.' }
                if ($application.usesCleartextTraffic -cne 'false') { throw 'Production Android cleartext traffic must be disabled.' }
                'Android backup and cleartext traffic are disabled in the production prebuild.'
            }

            Invoke-AssertionStage 'Android release signing guard contract' {
                $gradle = Get-Content -LiteralPath (Join-Path $mobileRoot 'android\app\build.gradle') -Raw
                $requiredMarkers = @(
                    'RHEMA_ANDROID_RELEASE_SIGNING_START',
                    'RHEMA_ANDROID_SIGNING_ENABLED',
                    'RHEMA_ANDROID_KEYSTORE_PATH',
                    'releaseRequested',
                    'signingConfig signingConfigs.rhemaRelease'
                )
                $missing = @($requiredMarkers | Where-Object { $gradle.IndexOf($_, [StringComparison]::Ordinal) -lt 0 })
                if ($missing.Count -gt 0) { throw "Generated Android release signing guard is missing: $($missing -join ', ')" }
                'Release tasks require externally supplied signing values and override the generated debug signing configuration.'
            }

            Invoke-AssertionStage 'Z92S packaged artifact hash contract' {
                $expected = [ordered]@{
                    'app\libs\zcs-smartpos-1.8.1.jar' = '3A65BF1A26D59730C014D79BA7B5AA8744BAB6E0B5275A2C055B996F4A7277E9'
                    'app\src\main\jniLibs\arm64-v8a\libSmartPosJni.so' = 'DE5CBF76EAFC3D0FBF1767BD0E8007E6217A2C81A6FDACE02FBD511A1D9FF9BF'
                    'app\src\main\jniLibs\armeabi-v7a\libSmartPosJni.so' = '7D8821DD051F83072023744019F1EDD86AB7CF0B0EAE2D7FD674DF7ED844A090'
                }
                foreach ($entry in $expected.GetEnumerator()) {
                    $file = Join-Path (Join-Path $mobileRoot 'android') $entry.Key
                    if (-not (Test-Path -LiteralPath $file)) { throw "Packaged SDK artifact is missing: $($entry.Key)" }
                    $actual = (Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash.ToUpperInvariant()
                    if ($actual -cne $entry.Value) { throw "Packaged SDK artifact hash mismatch: $($entry.Key)" }
                }
                $gradle = Get-Content -LiteralPath (Join-Path $mobileRoot 'android\app\build.gradle') -Raw
                if ($gradle.IndexOf("implementation files('libs/zcs-smartpos-1.8.1.jar')", [StringComparison]::Ordinal) -lt 0) {
                    throw 'The generated Android app does not reference the audited Z92S JAR.'
                }
                'Three packaged artifacts and the generated Gradle dependency match the audited SDK contract.'
            }

            if ($BuildNativeAndroid) {
                $java = Get-Command java -ErrorAction SilentlyContinue
                if (-not $java) { throw 'Java is required for -BuildNativeAndroid but was not found.' }
                $gradleWrapper = Join-Path $mobileRoot 'android\gradlew.bat'
                if (-not (Test-Path -LiteralPath $gradleWrapper)) { throw 'The generated Gradle wrapper was not found.' }
                Invoke-CheckedCommand 'Z92S native Android debug compile' $gradleWrapper @('app:assembleDebug', '--no-daemon') (Join-Path $mobileRoot 'android')
            }
            else {
                Add-SkippedStage 'Z92S native Android compile' 'SDK packaging was verified; native compile was not requested.'
            }
        }
        finally {
            [Environment]::SetEnvironmentVariable('RHEMA_ZCS_ENABLED', $priorEnabled, 'Process')
            [Environment]::SetEnvironmentVariable('RHEMA_ZCS_SDK_DIR', $priorDirectory, 'Process')
            [Environment]::SetEnvironmentVariable('EXPO_PUBLIC_RHEMA_ENVIRONMENT', $priorEnvironment, 'Process')
        }
    }
}
catch {
    $failure = $_.Exception.Message
}
finally {
    $head = @(& git -C $repoRoot rev-parse HEAD 2>$null)
    $result = [ordered]@{
        Gate = 'acceptance:mobile-pos'
        Passed = [string]::IsNullOrWhiteSpace($failure)
        StartedUtc = $startedUtc.ToString('o')
        FinishedUtc = [DateTime]::UtcNow.ToString('o')
        Repository = $repoRoot
        Commit = if ($head) { [string]$head[0] } else { $null }
        HardwareProfile = if ([string]::IsNullOrWhiteSpace($ZcsSdkDirectory)) { 'PortableFallback' } else { 'Z92S' }
        ZcsSdkPackagingRequested = -not [string]::IsNullOrWhiteSpace($ZcsSdkDirectory)
        NativeAndroidBuildRequested = [bool]$BuildNativeAndroid
        Failure = $failure
        Stages = @($stages)
    }
    $evidencePath = Join-Path $runDirectory 'acceptance-mobile-pos.json'
    $result | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $evidencePath -Encoding utf8
    Write-Host "`nEVIDENCE $evidencePath"
}

if ($failure) {
    Write-Error $failure
    exit 1
}

Write-Host "PASS acceptance:mobile-pos"

