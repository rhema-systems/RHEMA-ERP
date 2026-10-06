[CmdletBinding()]
param(
    [ValidateSet('Test')]
    [string]$Environment = 'Test',

    [switch]$DryRun,
    [switch]$PreflightOnly,
    [switch]$DeployOnly,
    [string]$ArtifactDirectory,
    [switch]$ReuseVerifiedArtifacts,
    [ValidatePattern('^[0-9a-fA-F]{7,40}$')]
    [string]$ReuseApiOutputFromCommit,
    [ValidatePattern('^[0-9a-fA-F]{7,40}$')]
    [string]$ReuseFrontendBuildFromCommit,
    [ValidateSet('.next', '.next-production')]
    [string]$ReuseFrontendOutputDirectory = '.next',
    [switch]$SkipBrowserSmoke,
    [switch]$LocalVps,
    [switch]$PrepareOperationalUat,
    [ValidatePattern('^RhemaERP_[A-Za-z0-9_]{1,119}$')]
    [string]$FreshDatabaseName,
    [switch]$AllowDirtyWorktree,
    [switch]$AllowNonRemoteHead,

    [string]$ExpectedCommit,
    [string]$VpsHost = '63.141.230.56',
    [int]$SshPort = 2222,
    [string]$SshUser = 'Administrator',
    [string]$SshKeyPath = (Join-Path $env:USERPROFILE '.ssh\id_rsa'),
    [string]$PublicBaseUrl = 'https://63.141.230.56',
    [int]$ApiReadyTimeoutSeconds = 1800
)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
$env:GIT_TERMINAL_PROMPT = '0'
$env:GCM_INTERACTIVE = 'Never'
$ScriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$RepositoryRoot = Split-Path -Parent $ScriptRoot
$RemoteHelperLocalPath = Join-Path $ScriptRoot 'vps\Invoke-RhemaVpsRemote.ps1'
. (Join-Path $ScriptRoot 'vps\New-RhemaVpsPreflightHelper.ps1')
. (Join-Path $ScriptRoot 'vps\New-RhemaZipPackage.ps1')
. (Join-Path $ScriptRoot 'vps\Set-StagedFrontendRuntime.ps1')
$BrowserSmokePath = Join-Path $ScriptRoot 'vps\Test-RhemaVpsBrowserSmoke.mjs'
$ReleaseRoot = Join-Path $RepositoryRoot 'artifacts\vps-releases'
$RemotePackagesRoot = 'C:\RhemaERP\packages'
$RemoteLogsRoot = 'C:\RhemaERP\logs'
$RunStartedUtc = [DateTime]::UtcNow
$StepResults = [System.Collections.Generic.List[object]]::new()

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
    $candidatePath = [System.IO.Path]::GetFullPath($Candidate)
    $parentPath = [System.IO.Path]::GetFullPath($Parent).TrimEnd('\') + '\'
    Assert-True ($candidatePath.StartsWith(
            $parentPath, [System.StringComparison]::OrdinalIgnoreCase)) `
        "Refusing a filesystem operation outside $parentPath"
}

function Reset-GeneratedDirectory {
    param([string]$Path, [string]$AllowedParent)
    Assert-SafeChildPath $Path $AllowedParent
    if (Test-Path -LiteralPath $Path) {
        Remove-Item -LiteralPath $Path -Recurse -Force
    }
    New-Item -ItemType Directory -Path $Path -Force | Out-Null
}

function Clear-NextOutputPreservingCache {
    param([string]$Path)
    Assert-SafeChildPath $Path (Join-Path $RepositoryRoot 'frontend')
    if (-not (Test-Path -LiteralPath $Path)) {
        New-Item -ItemType Directory -Path $Path -Force | Out-Null
        return
    }
    Get-ChildItem -LiteralPath $Path -Force | Where-Object Name -ne 'cache' |
        Remove-Item -Recurse -Force
}

function Get-DeploymentResourceSnapshot {
    try {
        $operatingSystem = Get-CimInstance Win32_OperatingSystem -ErrorAction Stop
        $processors = @(Get-CimInstance Win32_Processor -ErrorAction Stop)
        $driveId = [IO.Path]::GetPathRoot($RepositoryRoot).TrimEnd('\')
        $drive = Get-CimInstance Win32_LogicalDisk -Filter "DeviceID='$driveId'" -ErrorAction Stop
        $pageFiles = @(Get-CimInstance Win32_PageFileUsage -ErrorAction SilentlyContinue)
        $diskPerformance = @(Get-CimInstance Win32_PerfFormattedData_PerfDisk_PhysicalDisk `
            -ErrorAction SilentlyContinue | Where-Object Name -eq '_Total' | Select-Object -First 1)
        $networkPerformance = @(Get-CimInstance Win32_PerfFormattedData_Tcpip_NetworkInterface `
            -ErrorAction SilentlyContinue)
        return [ordered]@{
            capturedUtc = [DateTime]::UtcNow.ToString('o')
            cpuLoadPercent = [Math]::Round([double](($processors | Measure-Object LoadPercentage -Average).Average), 2)
            availablePhysicalMemoryBytes = [long]$operatingSystem.FreePhysicalMemory * 1KB
            totalPhysicalMemoryBytes = [long]$operatingSystem.TotalVisibleMemorySize * 1KB
            availableVirtualMemoryBytes = [long]$operatingSystem.FreeVirtualMemory * 1KB
            totalVirtualMemoryBytes = [long]$operatingSystem.TotalVirtualMemorySize * 1KB
            pageFileAllocatedBytes = [long](($pageFiles | Measure-Object AllocatedBaseSize -Sum).Sum) * 1MB
            pageFileUsedBytes = [long](($pageFiles | Measure-Object CurrentUsage -Sum).Sum) * 1MB
            diskFreeBytes = [long]$drive.FreeSpace
            diskSizeBytes = [long]$drive.Size
            diskBytesPerSecond = if ($diskPerformance.Count) { [long]$diskPerformance[0].DiskBytesPersec } else { $null }
            averageDiskSecondsPerTransfer = if ($diskPerformance.Count) { [double]$diskPerformance[0].AvgDisksecPerTransfer } else { $null }
            networkBytesPerSecond = [long](($networkPerformance | Measure-Object BytesTotalPersec -Sum).Sum)
        }
    }
    catch {
        return [ordered]@{
            capturedUtc = [DateTime]::UtcNow.ToString('o')
            unavailable = $_.Exception.Message
        }
    }
}

function Write-DeploymentTimingSummary {
    if ($StepResults.Count -eq 0) { return }
    Write-Host "`nDEPLOYMENT TIMING SUMMARY (slowest first)" -ForegroundColor Cyan
    foreach ($step in @($StepResults | Sort-Object durationSeconds -Descending)) {
        $scope = if ($step.scope) { " [$($step.scope)]" } else { '' }
        Write-Host ("{0,10:n1}s  {1,-8}  {2}{3}" -f `
                [double]$step.durationSeconds, $step.status, $step.name, $scope)
    }
}

function Import-RemoteTimings {
    param([string[]]$Output)
    foreach ($line in @($Output | Where-Object { $_ -like 'REMOTE_TIMING_JSON|*' })) {
        try {
            $encoded = $line.Substring('REMOTE_TIMING_JSON|'.Length)
            $json = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($encoded))
            $remote = $json | ConvertFrom-Json
            $StepResults.Add([ordered]@{
                name = [string]$remote.name
                scope = 'Remote VPS'
                status = [string]$remote.status
                startedUtc = [string]$remote.startedUtc
                completedUtc = [string]$remote.completedUtc
                durationSeconds = [double]$remote.durationSeconds
                resourcesBefore = $remote.resourcesBefore
                resourcesAfter = $remote.resourcesAfter
                error = [string]$remote.error
            })
        }
        catch {
            Write-Warning "Could not parse a remote timing record: $($_.Exception.Message)"
        }
    }
}

function Invoke-Step {
    param([string]$Name, [scriptblock]$Operation, [string]$Scope = 'Pipeline')

    Write-Host "`n==> $Name" -ForegroundColor Cyan
    $started = [DateTime]::UtcNow
    $resourcesBefore = Get-DeploymentResourceSnapshot
    $watch = [System.Diagnostics.Stopwatch]::StartNew()
    try {
        $result = & $Operation
        $watch.Stop()
        $completed = [DateTime]::UtcNow
        $StepResults.Add([ordered]@{
            name = $Name
            scope = $Scope
            status = 'Passed'
            startedUtc = $started.ToString('o')
            completedUtc = $completed.ToString('o')
            durationSeconds = [Math]::Round($watch.Elapsed.TotalSeconds, 2)
            resourcesBefore = $resourcesBefore
            resourcesAfter = Get-DeploymentResourceSnapshot
        })
        Write-Host ("PASS {0} ({1:n1}s)" -f $Name, $watch.Elapsed.TotalSeconds) `
            -ForegroundColor Green
        return $result
    }
    catch {
        $watch.Stop()
        $completed = [DateTime]::UtcNow
        $StepResults.Add([ordered]@{
            name = $Name
            scope = $Scope
            status = 'Failed'
            startedUtc = $started.ToString('o')
            completedUtc = $completed.ToString('o')
            durationSeconds = [Math]::Round($watch.Elapsed.TotalSeconds, 2)
            resourcesBefore = $resourcesBefore
            resourcesAfter = Get-DeploymentResourceSnapshot
            error = $_.Exception.Message
        })
        Write-Host ("FAIL {0} ({1:n1}s): {2}" -f `
                $Name, $watch.Elapsed.TotalSeconds, $_.Exception.Message) `
            -ForegroundColor Red
        throw
    }
}

function Invoke-NativeChecked {
    param(
        [string]$Command,
        [string[]]$Arguments,
        [string]$FailureMessage
    )
    # Build/dependency/browser tools do not need the account-bootstrap secret.
    # The dedicated deployment helper still inherits it for seed-only calls.
    $nativePriorOperationalPassword = [Environment]::GetEnvironmentVariable('UatBootstrap__SharedPassword', 'Process')
    try {
        [Environment]::SetEnvironmentVariable('UatBootstrap__SharedPassword', $null, 'Process')
        & $Command @Arguments
        if ($LASTEXITCODE -ne 0) {
            throw "$FailureMessage (exit code $LASTEXITCODE)."
        }
    } finally {
        [Environment]::SetEnvironmentVariable('UatBootstrap__SharedPassword', $nativePriorOperationalPassword, 'Process')
        $nativePriorOperationalPassword = $null
    }
}

function Invoke-RobocopyChecked {
    param(
        [string[]]$Arguments,
        [string]$FailureMessage
    )
    & robocopy.exe @Arguments
    $exitCode = $LASTEXITCODE
    if ($exitCode -gt 7) {
        throw "$FailureMessage (robocopy exit code $exitCode)."
    }
}

function Get-LocalMigrationIds {
    return @(Get-ChildItem `
            (Join-Path $RepositoryRoot 'src\ErpSystem.Data\Migrations') `
            -File -Filter '*.cs' |
        Where-Object {
            $_.Name -match '^\d{14}_.+\.cs$' -and
            $_.Name -notmatch '\.Designer\.cs$'
        } |
        ForEach-Object { [System.IO.Path]::GetFileNameWithoutExtension($_.Name) } |
        Sort-Object)
}

function Get-RemoteMasterCommit {
    $gitReference = @(& git `
            -c 'http.lowSpeedLimit=1' `
            -c 'http.lowSpeedTime=30' `
            ls-remote --exit-code origin refs/heads/master 2>$null)
    if ($LASTEXITCODE -eq 0 -and $gitReference.Count -eq 1) {
        return ([string]$gitReference[0] -split '\s+')[0]
    }

    $githubCli = Get-Command 'gh' -ErrorAction SilentlyContinue
    $originUrl = (& git remote get-url origin).Trim()
    $match = [regex]::Match(
        $originUrl,
        'github\.com[/:](?<repository>[^/\s]+/[^/\s]+?)(?:\.git)?$')
    if ($null -ne $githubCli -and $match.Success) {
        $repository = $match.Groups['repository'].Value
        $githubReference = @(& gh api `
                "repos/$repository/commits/master" --jq '.sha' 2>$null)
        if ($LASTEXITCODE -eq 0 -and $githubReference.Count -eq 1 -and
            [string]$githubReference[0] -match '^[0-9a-f]{40}$') {
            Write-Host 'Verified origin/master through authenticated GitHub CLI.' `
                -ForegroundColor DarkGray
            return [string]$githubReference[0]
        }
    }

    throw ('Could not verify origin/master non-interactively. Configure the Git ' +
        'credential manager or authenticate GitHub CLI with repository access.')
}

function Get-SshArguments {
    return @(
        '-i', $SshKeyPath,
        '-p', [string]$SshPort,
        '-o', 'BatchMode=yes',
        '-o', 'IdentitiesOnly=yes',
        '-o', 'StrictHostKeyChecking=yes',
        '-o', 'ConnectTimeout=15',
        '-o', 'ConnectionAttempts=2',
        '-o', 'ServerAliveInterval=15',
        '-o', 'ServerAliveCountMax=2',
        "${SshUser}@${VpsHost}"
    )
}

function Get-ScpArguments {
    return @(
        '-i', $SshKeyPath,
        '-P', [string]$SshPort,
        '-o', 'BatchMode=yes',
        '-o', 'IdentitiesOnly=yes',
        '-o', 'StrictHostKeyChecking=yes',
        '-o', 'ConnectTimeout=15',
        '-o', 'ConnectionAttempts=2',
        '-o', 'ServerAliveInterval=15',
        '-o', 'ServerAliveCountMax=2'
    )
}

function ConvertTo-SingleQuotedPowerShellLiteral {
    param([string]$Value)
    return "'" + $Value.Replace("'", "''") + "'"
}

function ConvertTo-WindowsProcessArgument {
    param([AllowEmptyString()][string]$Value)

    if ($null -eq $Value -or $Value.Length -eq 0) { return '""' }
    if ($Value -notmatch '[\s"]') { return $Value }

    # ProcessStartInfo.ArgumentList is unavailable in Windows PowerShell 5.1.
    # Quote according to the Windows CommandLineToArgvW rules so paths with
    # spaces, embedded quotes, and trailing backslashes retain their value.
    $builder = [Text.StringBuilder]::new()
    [void]$builder.Append('"')
    $backslashes = 0
    foreach ($character in $Value.ToCharArray()) {
        if ($character -eq '\') {
            $backslashes++
            continue
        }
        if ($character -eq '"') {
            if ($backslashes -gt 0) {
                [void]$builder.Append((('\' * (($backslashes * 2) + 1)) -join ''))
            }
            else {
                [void]$builder.Append('\')
            }
            [void]$builder.Append('"')
        }
        else {
            if ($backslashes -gt 0) {
                [void]$builder.Append((('\' * $backslashes) -join ''))
            }
            [void]$builder.Append($character)
        }
        $backslashes = 0
    }
    if ($backslashes -gt 0) {
        [void]$builder.Append((('\' * ($backslashes * 2)) -join ''))
    }
    [void]$builder.Append('"')
    return $builder.ToString()
}

function Set-WindowsProcessArguments {
    param(
        [System.Diagnostics.ProcessStartInfo]$StartInfo,
        [string[]]$Arguments
    )

    $StartInfo.Arguments = (@($Arguments | ForEach-Object {
        ConvertTo-WindowsProcessArgument ([string]$_)
    }) -join ' ')
}

function Get-SyncfusionLicenseKey {
    foreach ($name in @('SYNCFUSION_LICENSE', 'Syncfusion__LicenseKey')) {
        foreach ($scope in @('Process', 'User', 'Machine')) {
            $configured = [Environment]::GetEnvironmentVariable($name, $scope)
            if (-not [string]::IsNullOrWhiteSpace($configured)) {
                Write-Host "Using the protected Syncfusion license configured for this build host." `
                    -ForegroundColor DarkGray
                return $configured
            }
        }
    }

    if ($LocalVps) {
        # A server-local release can read the protected service value directly,
        # but still keeps it only in memory and never prints it.
        $path = 'C:\RhemaERP\services\api\RhemaERPAPI.xml'
        if (Test-Path -LiteralPath $path) {
            [xml]$xml = Get-Content -LiteralPath $path -Raw
            $value = [string](@($xml.service.env | Where-Object {
                $_.name -in @('Syncfusion__LicenseKey', 'SyncfusionLicenseKey') -and
                -not [string]::IsNullOrWhiteSpace([string]$_.value)
            })[0].value)
        }
        else {
            $nssmPath = 'HKLM:\SYSTEM\CurrentControlSet\Services\RhemaERPAPI\Parameters'
            Assert-True (Test-Path -LiteralPath $nssmPath) `
                "The protected API service configuration is missing: $path or $nssmPath"
            $properties = Get-ItemProperty -LiteralPath $nssmPath
            $entries = @(
                @($properties.AppEnvironment)
                @($properties.AppEnvironmentExtra)
            ) | ForEach-Object { [string]$_ }
            $candidate = @($entries | Where-Object {
                $_ -match '^(Syncfusion__LicenseKey|SyncfusionLicenseKey)=' -and
                $_.Length -gt ($_.IndexOf('=') + 1)
            } | Select-Object -Last 1)[0]
            if ($null -ne $candidate) {
                $value = $candidate.Substring($candidate.IndexOf('=') + 1)
            }
        }
        Assert-True (-not [string]::IsNullOrWhiteSpace($value)) `
            'The protected Syncfusion API license is not configured.'
        Write-Host 'Loaded the protected Syncfusion license without logging its value.' `
            -ForegroundColor DarkGray
        return $value
    }

    # The browser license must be embedded while Next.js is built. Reuse the
    # protected API-service value over SSH when the release host has no local
    # copy. Capture it only in process memory; never echo it or write it to the
    # release manifest, deployment result, or repository.
    $remoteScript = @'
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
$path = 'C:\RhemaERP\services\api\RhemaERPAPI.xml'
[xml]$xml = Get-Content -LiteralPath $path -Raw
$node = @($xml.service.env | Where-Object {
    $_.name -in @('Syncfusion__LicenseKey', 'SyncfusionLicenseKey') -and
    -not [string]::IsNullOrWhiteSpace([string]$_.value)
})[0]
if ($null -eq $node) { throw 'The protected Syncfusion API license is not configured.' }
[Console]::Out.Write([Convert]::ToBase64String(
    [Text.Encoding]::UTF8.GetBytes([string]$node.value)))
'@
    $encoded = [Convert]::ToBase64String(
        [Text.Encoding]::Unicode.GetBytes($remoteScript))
    $startInfo = [System.Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = (Get-Command ssh.exe -ErrorAction Stop).Source
    $startInfo.UseShellExecute = $false
    $startInfo.CreateNoWindow = $true
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    $processArguments = @(Get-SshArguments)
    $processArguments += "powershell.exe -NoLogo -NoProfile -NonInteractive -OutputFormat Text -ExecutionPolicy Bypass -EncodedCommand $encoded"
    Set-WindowsProcessArguments -StartInfo $startInfo -Arguments $processArguments

    $process = [System.Diagnostics.Process]::new()
    $process.StartInfo = $startInfo
    [void]$process.Start()
    $stdoutTask = $process.StandardOutput.ReadToEndAsync()
    $stderrTask = $process.StandardError.ReadToEndAsync()
    $process.WaitForExit()
    $stdout = $stdoutTask.GetAwaiter().GetResult()
    [void]$stderrTask.GetAwaiter().GetResult()
    $exitCode = $process.ExitCode
    $process.Dispose()
    Assert-True ($exitCode -eq 0) `
        'Could not load the protected Syncfusion license from the VPS service configuration.'

    try {
        $license = [Text.Encoding]::UTF8.GetString(
            [Convert]::FromBase64String($stdout.Trim()))
    }
    catch {
        throw 'The protected Syncfusion license returned by the VPS is malformed.'
    }
    Assert-True (-not [string]::IsNullOrWhiteSpace($license)) `
        'The protected Syncfusion license returned by the VPS is empty.'
    Write-Host 'Loaded the protected Syncfusion license without logging its value.' `
        -ForegroundColor DarkGray
    return $license
}

function Invoke-RemoteHelper {
    param(
        [string]$RemoteHelperPath,
        [string]$Action,
        [hashtable]$Parameters = @{}
    )

    if (-not $Parameters.ContainsKey('ExpectedPublicOrigin')) {
        $Parameters['ExpectedPublicOrigin'] = $PublicBaseUrl.TrimEnd('/')
    }

    if ($LocalVps) {
        $helperArguments = @(
            '-NoLogo', '-NoProfile', '-NonInteractive', '-ExecutionPolicy', 'Bypass',
            '-File', $RemoteHelperPath, '-Action', $Action
        )
        foreach ($key in ($Parameters.Keys | Sort-Object)) {
            $value = $Parameters[$key]
            if ($null -eq $value -or [string]::IsNullOrWhiteSpace([string]$value)) {
                continue
            }
            $helperArguments += "-$key"
            $helperArguments += [string]$value
        }
        $liveOutput = [System.Collections.Generic.List[string]]::new()
        & powershell.exe @helperArguments 2>&1 | ForEach-Object {
            $line = [string]$_
            if ($line.StartsWith('FRESH_PROGRESS|')) { Write-Host $line.Substring(15) }
            else { Write-Host $line }
            if (-not [string]::IsNullOrWhiteSpace($line)) { $liveOutput.Add($line) }
        }
        $output = @($liveOutput)
        if ($LASTEXITCODE -ne 0) {
            $summary = ($output | Select-Object -Last 20) -join [Environment]::NewLine
            throw "Local VPS $Action failed.$([Environment]::NewLine)$summary"
        }
        return @($output | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
    }

    $parts = @(
        '&', (ConvertTo-SingleQuotedPowerShellLiteral $RemoteHelperPath),
        '-Action', (ConvertTo-SingleQuotedPowerShellLiteral $Action)
    )
    foreach ($key in ($Parameters.Keys | Sort-Object)) {
        $value = $Parameters[$key]
        if ($null -eq $value -or [string]::IsNullOrWhiteSpace([string]$value)) {
            continue
        }
        $parts += "-$key"
        $parts += ConvertTo-SingleQuotedPowerShellLiteral ([string]$value)
    }
    $remoteScript = $parts -join ' '
    $encoded = [Convert]::ToBase64String(
        [Text.Encoding]::Unicode.GetBytes($remoteScript))
    $sshArguments = Get-SshArguments
    # Capture native stdout/stderr as raw text. PowerShell's native-command
    # adapter attempts to deserialize large remote CLIXML error streams and
    # can itself fail with "unclosed literal string", hiding the real remote
    # result after packages and backups have already succeeded.
    $startInfo = [System.Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = (Get-Command ssh.exe -ErrorAction Stop).Source
    $startInfo.UseShellExecute = $false
    $startInfo.CreateNoWindow = $true
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    $processArguments = @($sshArguments)
    $processArguments += "powershell.exe -NoLogo -NoProfile -NonInteractive -OutputFormat Text -ExecutionPolicy Bypass -EncodedCommand $encoded"
    Set-WindowsProcessArguments -StartInfo $startInfo -Arguments $processArguments

    $process = [System.Diagnostics.Process]::new()
    $process.StartInfo = $startInfo
    [void]$process.Start()
    $stdoutTask = $process.StandardOutput.ReadToEndAsync()
    $stderrTask = $process.StandardError.ReadToEndAsync()
    $process.WaitForExit()
    $stdout = $stdoutTask.GetAwaiter().GetResult()
    $stderr = $stderrTask.GetAwaiter().GetResult()
    $exitCode = $process.ExitCode
    $process.Dispose()

    $output = @(
        @($stdout -split "`r?`n")
        @($stderr -split "`r?`n")
    ) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
    if ($exitCode -ne 0) {
        $summary = (@($output) | Where-Object {
            $_ -notmatch '^#< CLIXML' -and $_ -notmatch '^<Objs '
        } | Select-Object -Last 20) -join [Environment]::NewLine
        throw "Remote $Action failed.$([Environment]::NewLine)$summary"
    }
    return @($output | Where-Object {
        $_ -notmatch '^#< CLIXML' -and $_ -notmatch '^<Objs '
    })
}

function Copy-ToVps {
    param([string[]]$LocalPaths, [string]$RemoteDirectory)
    if ($LocalVps) {
        # The package directory is a disposable staging area. A VPS that was
        # provisioned from only the live service folders may not have it yet.
        New-Item -ItemType Directory -Path $RemoteDirectory -Force | Out-Null
        foreach ($path in $LocalPaths) {
            Copy-Item -LiteralPath $path -Destination $RemoteDirectory -Force
        }
        return
    }
    $scpArguments = Get-ScpArguments
    $destination = "${SshUser}@${VpsHost}:$($RemoteDirectory.Replace('\', '/'))/"
    & scp @scpArguments @LocalPaths $destination
    if ($LASTEXITCODE -ne 0) {
        throw "SCP upload failed with exit code $LASTEXITCODE."
    }
}

function Test-ReleaseManifest {
    param([string]$ManifestPath)
    if (-not (Test-Path -LiteralPath $ManifestPath)) { return $null }
    try {
        $manifest = Get-Content -LiteralPath $ManifestPath -Raw | ConvertFrom-Json
        if ($manifest.commit -ne $script:ArtifactCommit) { return $null }
        if ($DeployOnly -and $manifest.schemaVersion -ne 2) { return $null }
        if ($DeployOnly -and $manifest.environment -ne $Environment) { return $null }
        if ($manifest.publicBaseUrl -ne $PublicBaseUrl) { return $null }
        if ($DeployOnly -and $manifest.nextPublicApiUrl -ne "$($PublicBaseUrl.TrimEnd('/'))/api") {
            return $null
        }
        if ($DeployOnly -and ([string]::IsNullOrWhiteSpace([string]$manifest.releaseId) -or
            [string]$manifest.releaseId -notmatch '^[a-zA-Z0-9-]+$')) { return $null }
        if ([string]::IsNullOrWhiteSpace([string]$manifest.buildId) -or
            [string]::IsNullOrWhiteSpace([string]$manifest.cacheVersion)) {
            return $null
        }
        if ($manifest.syncfusionFrontendLicensed -ne $true) { return $null }
        $apiPath = Join-Path (Split-Path $ManifestPath) $manifest.api.file
        $frontendPath = Join-Path (Split-Path $ManifestPath) $manifest.frontend.file
        if (-not (Test-Path $apiPath) -or -not (Test-Path $frontendPath)) {
            return $null
        }
        if ((Get-FileHash $apiPath -Algorithm SHA256).Hash -ne `
                $manifest.api.sha256) { return $null }
        if ((Get-FileHash $frontendPath -Algorithm SHA256).Hash -ne `
                $manifest.frontend.sha256) { return $null }
        return $manifest
    }
    catch { return $null }
}

function Assert-MigrationDiscovery {
    $migrationRoot = Join-Path $RepositoryRoot 'src\ErpSystem.Data\Migrations'
    $missing = @()
    foreach ($id in (Get-LocalMigrationIds)) {
        $source = Get-Content (Join-Path $migrationRoot "$id.cs") -Raw
        $designerPath = Join-Path $migrationRoot "$id.Designer.cs"
        $designer = if (Test-Path -LiteralPath $designerPath) {
            Get-Content -LiteralPath $designerPath -Raw
        }
        else { '' }
        $migrationAttribute = 'Migration("' + $id + '")'
        if (-not $source.Contains($migrationAttribute) -and
            -not $designer.Contains($migrationAttribute)) {
            $missing += $id
        }
    }
    Assert-True ($missing.Count -eq 0) `
        "Active EF migrations are missing compiled discovery metadata for: $($missing -join ', ')"
}

function Set-TemporaryEnvironment {
    param([hashtable]$Values)
    $previous = @{}
    foreach ($key in $Values.Keys) {
        $previous[$key] = [Environment]::GetEnvironmentVariable($key, 'Process')
        [Environment]::SetEnvironmentVariable($key, [string]$Values[$key], 'Process')
    }
    return $previous
}

function Restore-TemporaryEnvironment {
    param([hashtable]$Values)
    foreach ($key in $Values.Keys) {
        [Environment]::SetEnvironmentVariable($key, $Values[$key], 'Process')
    }
}

function New-ReleaseArtifacts {
    param([string]$ReleaseDirectory)

    Assert-MigrationDiscovery
    $syncfusionLicenseKey = Get-SyncfusionLicenseKey
    $apiOutput = Join-Path $ReleaseDirectory 'api'
    $frontendOutput = Join-Path $ReleaseDirectory 'frontend'
    Reset-GeneratedDirectory $apiOutput $ReleaseDirectory
    Reset-GeneratedDirectory $frontendOutput $ReleaseDirectory

    if (-not [string]::IsNullOrWhiteSpace($ReuseApiOutputFromCommit)) {
        $resolvedSourceCommit = (@(& git rev-parse --verify `
                    "$ReuseApiOutputFromCommit^{commit}" 2>$null) -join '').Trim()
        Assert-True ($LASTEXITCODE -eq 0 -and
            $resolvedSourceCommit -match '^[0-9a-f]{40}$') `
            "The reusable API source commit is invalid: $ReuseApiOutputFromCommit"

        $apiInputPaths = @('src', 'tools/ErpSystem.MigrationModelCompiler')
        foreach ($candidate in @(
                'global.json', 'NuGet.config', 'Directory.Build.props',
                'Directory.Build.targets', 'Directory.Packages.props')) {
            if (Test-Path -LiteralPath (Join-Path $RepositoryRoot $candidate)) {
                $apiInputPaths += $candidate
            }
        }
        & git diff --quiet $resolvedSourceCommit $script:Commit -- @apiInputPaths
        Assert-True ($LASTEXITCODE -eq 0) `
            'API source or build inputs changed; refusing to reuse the earlier publish output.'

        $sourceRelease = Join-Path $ReleaseRoot $resolvedSourceCommit.Substring(0, 8)
        $sourceApiOutput = Join-Path $sourceRelease 'api'
        Assert-SafeChildPath $sourceApiOutput $ReleaseRoot
        Assert-True (Test-Path -LiteralPath `
                (Join-Path $sourceApiOutput 'ErpSystem.Api.exe')) `
            "Reusable API publish output is missing: $sourceApiOutput"
        Invoke-Step -Name 'Reuse prior API publish output' -Scope 'Build host' -Operation {
            Invoke-RobocopyChecked @(
                $sourceApiOutput, $apiOutput, '/E', '/R:2', '/W:2',
                '/NFL', '/NDL', '/NJH', '/NJS', '/NP'
            ) 'Reusing the verified API publish output failed'
        } | Out-Null
        Write-Host "Reused API publish output from $resolvedSourceCommit." `
            -ForegroundColor Green
    }
    else {
        Invoke-Step -Name 'Publish self-contained API' -Scope 'Build host' -Operation {
            Invoke-NativeChecked 'dotnet' @(
                'publish', 'src\ErpSystem.Api\ErpSystem.Api.csproj',
                '-c', 'Release', '-r', 'win-x64', '--self-contained', 'true',
                '-o', $apiOutput,
                '/p:PublishSingleFile=false',
                '-p:UseSharedCompilation=false', '-m:1'
            ) 'API publish failed' | Out-Host
        } | Out-Null
    }

    foreach ($name in @(
            'appsettings.json', 'appsettings.Production.json',
            'appsettings.Development.json', 'appsettings.AntiSpam.json',
            '.env', '.env.production')) {
        $path = Join-Path $apiOutput $name
        if (Test-Path -LiteralPath $path) { Remove-Item -LiteralPath $path -Force }
    }
    Assert-True (Test-Path (Join-Path $apiOutput 'ErpSystem.Api.exe')) `
        'Published API executable is missing.'
    $forbiddenApiFiles = @(Get-ChildItem $apiOutput -File -Force | Where-Object {
        $_.Name -like 'appsettings*.json' -or $_.Name -like '.env*'
    })
    Assert-True ($forbiddenApiFiles.Count -eq 0) `
        'Published API still contains protected configuration.'

    $frontendRoot = Join-Path $RepositoryRoot 'frontend'
    $activeNext = @(Get-CimInstance Win32_Process -ErrorAction SilentlyContinue |
        Where-Object {
            $_.Name -eq 'node.exe' -and
            $_.CommandLine -match 'next(?:\.cmd)?\s+(dev|start)' -and
            $_.CommandLine -like "*$frontendRoot*"
        })
    Assert-True ($activeNext.Count -eq 0) `
        'A local Next.js process is using this frontend. Stop it before release build.'

    $nextOutput = Join-Path $frontendRoot '.next'
    Assert-True ($ReuseFrontendOutputDirectory -eq '.next' -or -not [string]::IsNullOrWhiteSpace($ReuseFrontendBuildFromCommit)) `
        'A different reusable output directory requires -ReuseFrontendBuildFromCommit.'
    if (-not [string]::IsNullOrWhiteSpace($ReuseFrontendBuildFromCommit)) {
        $nextOutput = Join-Path $frontendRoot $ReuseFrontendOutputDirectory
        $resolvedFrontendCommit = (@(& git rev-parse --verify `
                    "$ReuseFrontendBuildFromCommit^{commit}" 2>$null) -join '').Trim()
        Assert-True ($LASTEXITCODE -eq 0 -and
            $resolvedFrontendCommit -match '^[0-9a-f]{40}$') `
            "The reusable frontend source commit is invalid: $ReuseFrontendBuildFromCommit"

        & git diff --quiet $resolvedFrontendCommit $script:Commit -- frontend
        Assert-True ($LASTEXITCODE -eq 0) `
            'Frontend source or build inputs changed; refusing to reuse the earlier build.'
        foreach ($relativePath in @(
                'BUILD_ID', 'required-server-files.json',
                'server\middleware-manifest.json')) {
            Assert-True (Test-Path -LiteralPath (Join-Path $nextOutput $relativePath)) `
                "Reusable frontend build output is incomplete: $relativePath"
        }
        Write-Host "Reused frontend build output from $resolvedFrontendCommit." `
            -ForegroundColor Green
    }
    else {
        Assert-True ([System.IO.Path]::GetFullPath($nextOutput) -eq `
                [System.IO.Path]::GetFullPath((Join-Path $RepositoryRoot 'frontend\.next'))) `
            'Unexpected frontend build-output path.'
        Clear-NextOutputPreservingCache $nextOutput
        $availableMemory = (Get-CimInstance Win32_OperatingSystem).FreePhysicalMemory * 1KB
        $configuredHeapMb = if ($env:NEXT_BUILD_MAX_OLD_SPACE_SIZE_MB) {
            $env:NEXT_BUILD_MAX_OLD_SPACE_SIZE_MB
        } else { '12288' }
        Write-Host "LEGACY_BUILD_MEMORY|HEAP_MB=$configuredHeapMb|AVAILABLE_BYTES=$availableMemory" `
            -ForegroundColor DarkGray
        $previousEnvironment = Set-TemporaryEnvironment @{
            NODE_ENV = 'production'
            NEXT_PUBLIC_API_URL = "$PublicBaseUrl/api"
            API_URL = "$PublicBaseUrl/api"
            NEXTAUTH_URL = $PublicBaseUrl
            NEXT_TELEMETRY_DISABLED = '1'
            NEXT_DIST_DIR = '.next'
            NEXT_OUTPUT = ''
            SYNCFUSION_LICENSE = $syncfusionLicenseKey
        }
        try {
            Push-Location $frontendRoot
            try {
                Invoke-Step -Name 'Restore locked frontend dependencies' -Scope 'Build host' -Operation {
                    Invoke-NativeChecked 'npm.cmd' @(
                        'ci', '--include=dev', '--no-audit', '--no-fund'
                    ) 'Frontend locked-dependency restore failed' | Out-Host
                } | Out-Null
                Invoke-Step -Name 'Activate Syncfusion frontend license' -Scope 'Build host' -Operation {
                    $syncfusionActivator = Join-Path $frontendRoot `
                        'node_modules\.bin\syncfusion-license.cmd'
                    Assert-True (Test-Path -LiteralPath $syncfusionActivator) `
                        'The installed Syncfusion frontend license activator is missing.'
                    Invoke-NativeChecked $syncfusionActivator @('activate') `
                        'Syncfusion frontend license activation failed' | Out-Host
                } | Out-Null
                Invoke-Step -Name 'Build Next.js production application' -Scope 'Build host' -Operation {
                    Invoke-NativeChecked 'npm.cmd' @('run', 'build') `
                        'Frontend build failed' | Out-Host
                } | Out-Null
            }
            finally { Pop-Location }
        }
        finally { Restore-TemporaryEnvironment $previousEnvironment }
    }
    $syncfusionLicenseKey = $null

    $buildId = (Get-Content (Join-Path $nextOutput 'BUILD_ID') -Raw).Trim()
    Assert-True ($buildId -ne 'development') `
        'Frontend build ID is still development.'
    $middleware = Get-Content `
        (Join-Path $nextOutput 'server\middleware-manifest.json') -Raw |
        ConvertFrom-Json
    $middlewareBuildId = $middleware.middleware.'/'.env.__NEXT_BUILD_ID
    Assert-True ($buildId -eq $middlewareBuildId) `
        'Frontend BUILD_ID and middleware build ID differ.'
    Assert-True (Test-Path (Join-Path $nextOutput 'required-server-files.json')) `
        'Regular Next.js server files are missing.'

    Invoke-Step -Name 'Stage frontend runtime and production dependencies' -Scope 'Build host' -Operation {
        Invoke-RobocopyChecked @(
            $nextOutput, (Join-Path $frontendOutput '.next'), '/E', '/R:2', '/W:2',
            '/NFL', '/NDL', '/NJH', '/NJS', '/NP',
            '/XD', (Join-Path $nextOutput 'cache')
        ) 'Frontend .next staging failed'
        Invoke-RobocopyChecked @(
            (Join-Path $frontendRoot 'public'), (Join-Path $frontendOutput 'public'),
            '/E', '/R:2', '/W:2', '/NFL', '/NDL', '/NJH', '/NJS', '/NP'
        ) 'Frontend public staging failed'
        Copy-Item (Join-Path $frontendRoot 'package.json'), `
            (Join-Path $frontendRoot 'package-lock.json'), `
            (Join-Path $frontendRoot 'next.config.js') `
            -Destination $frontendOutput -Force
        Set-StagedFrontendRuntime -FrontendDirectory $frontendOutput
        Push-Location $frontendOutput
        try {
            Invoke-NativeChecked 'npm.cmd' @(
                'ci', '--omit=dev', '--ignore-scripts', '--no-audit', '--no-fund'
            ) 'Frontend production dependency staging failed' | Out-Host
        }
        finally { Pop-Location }
    } | Out-Null
    Assert-True (Test-Path `
            (Join-Path $frontendOutput 'node_modules\next\package.json')) `
        'Frontend production Next.js runtime is missing.'

    # A diagnostic dirty-worktree release can share HEAD with an earlier VPS
    # package. Include the deployment stamp so browsers always receive a new
    # service-worker cache identity for the exact package being applied.
    $cacheVersion = "vps-$($script:ShortCommit)-$($script:DeploymentStamp)"
    $serviceWorkerPath = Join-Path $frontendOutput 'public\sw.js'
    $serviceWorker = Get-Content $serviceWorkerPath -Raw
    $cacheMap = @{
        'CACHE_NAME' = "erp-system-$cacheVersion"
        'STATIC_CACHE_NAME' = "erp-static-$cacheVersion"
        'RUNTIME_CACHE_NAME' = "erp-runtime-$cacheVersion"
        'MOBILE_SHELL_CACHE_NAME' = "erp-mobile-shell-$cacheVersion"
    }
    foreach ($entry in $cacheMap.GetEnumerator()) {
        $pattern = "(?m)^const $([regex]::Escape($entry.Key)) = '[^']+'"
        Assert-True ([regex]::IsMatch($serviceWorker, $pattern)) `
            "Service worker does not declare $($entry.Key)."
        $serviceWorker = [regex]::Replace(
            $serviceWorker, $pattern, "const $($entry.Key) = '$($entry.Value)'", 1)
    }
    [System.IO.File]::WriteAllText(
        $serviceWorkerPath, $serviceWorker,
        (New-Object System.Text.UTF8Encoding($false)))

    $compiledFiles = @(Get-ChildItem `
        (Join-Path $frontendOutput '.next\static'),
        (Join-Path $frontendOutput '.next\server') -File -Recurse -ErrorAction Stop)
    $badUrls = @($compiledFiles | Select-String `
        -Pattern 'localhost:5000|localhost:53484|localhost:7095')
    Assert-True ($badUrls.Count -eq 0) `
        'Compiled frontend contains a development API URL.'

    $apiZipName = "rhema-erp-api-$($script:ShortCommit).zip"
    $frontendZipName = "rhema-erp-frontend-$($script:ShortCommit).zip"
    $apiZip = Join-Path $ReleaseDirectory $apiZipName
    $frontendZip = Join-Path $ReleaseDirectory $frontendZipName
    foreach ($zip in @($apiZip, $frontendZip)) {
        if (Test-Path $zip) { Remove-Item -LiteralPath $zip -Force }
    }
    Invoke-Step -Name 'Package API artifact' -Scope 'Build host' -Operation {
        New-RhemaZipPackage -Source $apiOutput -Destination $apiZip `
            -CompressionLevel Fastest
    } | Out-Null
    Invoke-Step -Name 'Package frontend artifact' -Scope 'Build host' -Operation {
        $frontendCompressionLevel = if ($LocalVps) { 'NoCompression' } else { 'Fastest' }
        New-RhemaZipPackage -Source $frontendOutput -Destination $frontendZip `
            -CompressionLevel $frontendCompressionLevel
    } | Out-Null

    $apiInfo = Get-Item $apiZip
    $frontendInfo = Get-Item $frontendZip
    $artifactHashes = Invoke-Step -Name 'Hash API and frontend artifacts' -Scope 'Build host' -Operation {
        [ordered]@{
            api = (Get-FileHash $apiZip -Algorithm SHA256).Hash
            frontend = (Get-FileHash $frontendZip -Algorithm SHA256).Hash
        }
    }
    $manifest = [ordered]@{
        schemaVersion = 1
        commit = $script:Commit
        shortCommit = $script:ShortCommit
        createdUtc = [DateTime]::UtcNow.ToString('o')
        publicBaseUrl = $PublicBaseUrl
        buildId = $buildId
        cacheVersion = $cacheVersion
        syncfusionFrontendLicensed = $true
        api = [ordered]@{
            file = $apiInfo.Name
            bytes = $apiInfo.Length
            sha256 = $artifactHashes.api
        }
        frontend = [ordered]@{
            file = $frontendInfo.Name
            bytes = $frontendInfo.Length
            sha256 = $artifactHashes.frontend
        }
    }
    [System.IO.File]::WriteAllText(
        (Join-Path $ReleaseDirectory 'release-manifest.json'),
        ($manifest | ConvertTo-Json -Depth 6),
        (New-Object System.Text.UTF8Encoding($false)))
    return $manifest
}

function Compare-MigrationState {
    param([string[]]$RemoteOutput, [bool]$RequireCurrent)

    $remoteIds = @($RemoteOutput | Where-Object { $_ -like 'MIGRATION_ID|*' } |
        ForEach-Object { $_.Substring('MIGRATION_ID|'.Length) })
    $localIds = Get-LocalMigrationIds
    $missing = @($localIds | Where-Object { $_ -notin $remoteIds })
    $extra = @($remoteIds | Where-Object { $_ -notin $localIds })
    $baselineId = '20260916132000_DisposableDevelopmentCurrentModelBaseline'
    $allowedHistoricalExtra = @('20260402003233_InitialCreate')
    $unexpectedExtra = @($extra | Where-Object { $_ -notin $allowedHistoricalExtra })
    Write-Host "Migrations: local=$($localIds.Count), VPS=$($remoteIds.Count), pending=$($missing.Count), historical-extra=$($extra.Count)"

    Assert-True (-not (($missing -contains $baselineId) -and $remoteIds.Count -gt 0)) `
        ('The VPS database has existing migration history but has not adopted ' +
         "$baselineId. The current baseline creates a fresh schema and is not a " +
         'data-preserving upgrade. Stop deployment: retain the current compatible ' +
         'code/database pair, or use an explicitly approved disposable reset or ' +
         'separately reviewed data-preserving cutover plan.')

    $guardCoverage = @($RemoteOutput | Where-Object { $_ -like 'GUARD_COVERAGE|*' } |
        ForEach-Object { $_.Substring('GUARD_COVERAGE|'.Length) })
    $uncoveredGuards = @()
    $migrationRoot = Join-Path $RepositoryRoot 'src\ErpSystem.Data\Migrations'
    foreach ($migrationId in $missing) {
        $source = Get-Content (Join-Path $migrationRoot "$migrationId.cs") -Raw
        if (($source -match 'THROW\s+\d+' -or $source -match '\b\w+Guards?\.Install\s*\(') -and
            $migrationId -notin $guardCoverage) {
            $uncoveredGuards += $migrationId
        }
    }
    Assert-True ($uncoveredGuards.Count -eq 0) `
        (("Pending guarded migrations lack a fail-fast VPS data probe: {0}. " +
          "Extend Invoke-RhemaVpsRemote.ps1 before deployment.") -f `
            ($uncoveredGuards -join ', '))
    Assert-True ($unexpectedExtra.Count -eq 0) `
        ("The VPS contains unknown migrations not present in this repository: {0}" -f `
            ($unexpectedExtra -join ', '))
    if ($RequireCurrent) {
        Assert-True ($missing.Count -eq 0) `
            "The VPS remains behind by $($missing.Count) repository migration(s)."
    }
    return [ordered]@{
        localCount = $localIds.Count
        remoteCount = $remoteIds.Count
        pending = $missing
        historicalExtra = $extra
    }
}

function Invoke-PublicSmoke {
    param(
        [string]$ExpectedCacheVersion,
        [string]$ExpectedEnvironment,
        [string]$ExpectedCommit,
        [switch]$AllowConfigurationDrift
    )

    $base = $PublicBaseUrl.TrimEnd('/')
    foreach ($route in @(
            '/api/tenant', '/api/auth/security-settings', '/api/health/ready',
            '/login', '/supplier-application')) {
        $code = & curl.exe -k -sS --max-time 30 -o NUL -w '%{http_code}' `
            "$base$route"
        Assert-True ($LASTEXITCODE -eq 0 -and $code -eq '200') `
            "Public route failed: $route (HTTP $code)"
        Write-Output "PUBLIC_ROUTE|200|$route"
    }

    if (-not [string]::IsNullOrWhiteSpace($ExpectedEnvironment)) {
        $environmentJson = (& curl.exe -k -sS --max-time 30 `
            "$base/api/public/config/environment") -join "`n"
        Assert-True ($LASTEXITCODE -eq 0) `
            'Could not retrieve the public application environment descriptor.'
        $environmentDescriptor = $environmentJson | ConvertFrom-Json
        Assert-True ($environmentDescriptor.environment -ceq $ExpectedEnvironment) `
            "Public environment descriptor expected $ExpectedEnvironment but received $($environmentDescriptor.environment)."
        Assert-True (-not [bool]$environmentDescriptor.isProduction) `
            'The test VPS must never identify itself as Production.'
        Assert-True ([bool]$environmentDescriptor.configurationValid) `
            'The public environment descriptor reports invalid configuration.'
        if (-not [string]::IsNullOrWhiteSpace($ExpectedCommit)) {
            Assert-True ($environmentDescriptor.buildId -ceq $ExpectedCommit.Substring(0, 7)) `
                'The public build identifier differs from the deployed release commit.'
        }
        Assert-True ($environmentJson -notmatch `
            '(?i)connectionstring|password|secretkey|privatekey|accesstoken') `
            'The public environment descriptor contains a forbidden sensitive field name.'
        Write-Output "PUBLIC_ENVIRONMENT|$($environmentDescriptor.environment)|$($environmentDescriptor.buildId)"
    }

    $serviceWorker = (& curl.exe -k -sS --max-time 30 "$base/sw.js") -join "`n"
    Assert-True ($LASTEXITCODE -eq 0) 'Could not retrieve the public service worker.'
    if (-not [string]::IsNullOrWhiteSpace($ExpectedCacheVersion)) {
        Assert-True ($serviceWorker -match [regex]::Escape($ExpectedCacheVersion)) `
            'Public service worker does not contain the expected release cache version.'
    }

    $allAssets = @()
    foreach ($page in @('/login', '/supplier-application')) {
        $html = (& curl.exe -k -sS --max-time 30 "$base$page") -join "`n"
        Assert-True ($LASTEXITCODE -eq 0) "Could not retrieve public page: $page"
        $allAssets += @([regex]::Matches(
                $html, '(?:src|href)="([^"]+\.(?:js|css))"') |
            ForEach-Object { $_.Groups[1].Value } |
            Where-Object { $_ -like '/_next/static/*' } |
            Select-Object -Unique)
    }
    $allAssets = @($allAssets | Select-Object -Unique)
    $badUrlMatches = 0
    $expectedOriginMatches = 0
    $javascriptCount = 0
    foreach ($asset in $allAssets) {
        $code = & curl.exe -k -sS --max-time 30 -o NUL -w '%{http_code}' `
            "$base$asset"
        Assert-True ($LASTEXITCODE -eq 0 -and $code -eq '200') `
            "Public asset failed: $asset (HTTP $code)"
        if ($asset -like '*.js') {
            $javascript = (& curl.exe -k -sS --max-time 30 "$base$asset") -join "`n"
            Assert-True ($LASTEXITCODE -eq 0) "Could not scan JavaScript asset: $asset"
            $javascriptCount++
            $badUrlMatches += [regex]::Matches(
                $javascript, 'localhost:5000|localhost:53484|localhost:7095').Count
            $expectedOriginMatches += [regex]::Matches(
                $javascript, ([regex]::Escape($base) + '(?:/api)?')).Count
        }
    }
    Assert-True ($badUrlMatches -eq 0) `
        'A public JavaScript asset contains a development API URL.'
    if ($expectedOriginMatches -eq 0) {
        Assert-True $AllowConfigurationDrift `
            'No deployed JavaScript asset contains the expected VPS API origin.'
        Write-Output "CONFIG_DRIFT|FRONTEND_API_ORIGIN|EXPECTED=$base"
    }

    $allowedHeaders = (& curl.exe -k -sS --max-time 30 -D - -o NUL `
        -X OPTIONS -H "Origin: $base" `
        -H 'Access-Control-Request-Method: POST' "$base/api/auth/login") -join "`n"
    $allowedOriginPattern = '(?im)^access-control-allow-origin:\s*' +
        [regex]::Escape($base) + '\s*$'
    if ($allowedHeaders -notmatch $allowedOriginPattern) {
        $actualOriginMatch = [regex]::Match(
            $allowedHeaders, '(?im)^access-control-allow-origin:\s*([^\r\n]+)')
        $actualOrigin = if ($actualOriginMatch.Success) {
            $actualOriginMatch.Groups[1].Value.Trim()
        }
        else { '<missing>' }
        Assert-True $AllowConfigurationDrift `
            "Allowed-origin CORS preflight expected $base but received $actualOrigin."
        Write-Output "CONFIG_DRIFT|CORS|EXPECTED=$base|ACTUAL=$actualOrigin"
    }
    $deniedHeaders = (& curl.exe -k -sS --max-time 30 -D - -o NUL `
        -X OPTIONS -H 'Origin: https://invalid.example' `
        -H 'Access-Control-Request-Method: POST' "$base/api/auth/login") -join "`n"
    Assert-True ($deniedHeaders -notmatch `
        '(?im)^access-control-allow-origin:') `
        'Unapproved-origin CORS preflight returned an allow-origin header.'

    $directClient = [System.Net.Sockets.TcpClient]::new()
    try {
        $directTask = $directClient.ConnectAsync($VpsHost, 5000)
        $directReachable = $directTask.Wait([TimeSpan]::FromSeconds(5)) -and
            $directClient.Connected
    }
    catch {
        $directReachable = $false
    }
    finally {
        $directClient.Dispose()
    }
    Assert-True (-not $directReachable) 'Direct public port 5000 is reachable.'
    Write-Output "PUBLIC_ASSETS|$($allAssets.Count)|JS=$javascriptCount|BAD_URLS=0"
    Write-Output 'PUBLIC_SMOKE|PASS'
}

function Write-RunResult {
    param(
        [string]$Status,
        [string]$DeploymentId,
        [string]$ReleaseDirectory,
        [object]$ReleaseManifest,
        [object]$MigrationState,
        [string]$Failure
    )
    if (-not (Test-Path -LiteralPath $ReleaseDirectory)) {
        New-Item -ItemType Directory -Path $ReleaseDirectory -Force | Out-Null
    }
    $result = [ordered]@{
        schemaVersion = 1
        status = $Status
        dryRun = [bool]$DryRun
        preflightOnly = [bool]$PreflightOnly
        freshDatabase = $FreshDatabaseName
        environment = $Environment
        deploymentId = $DeploymentId
        commit = $script:Commit
        startedUtc = $RunStartedUtc.ToString('o')
        completedUtc = [DateTime]::UtcNow.ToString('o')
        totalDurationSeconds = [Math]::Round(
            ([DateTime]::UtcNow - $RunStartedUtc).TotalSeconds, 2)
        publicBaseUrl = $PublicBaseUrl
        release = $ReleaseManifest
        migrations = $MigrationState
        steps = $StepResults
        slowestSteps = @($StepResults | Sort-Object durationSeconds -Descending)
        failure = $Failure
    }
    $name = if ($PreflightOnly) {
        "preflight-$DeploymentId.json"
    } elseif ($DryRun) {
        "dry-run-$DeploymentId.json"
    } else {
        "deployment-$DeploymentId.json"
    }
    $path = Join-Path $ReleaseDirectory $name
    [System.IO.File]::WriteAllText(
        $path, ($result | ConvertTo-Json -Depth 10),
        (New-Object System.Text.UTF8Encoding($false)))
    return $path
}

Push-Location $RepositoryRoot
$freshApplyAttempted = $false
$applicationApplyCompleted = $false
$priorOperationalPassword = [Environment]::GetEnvironmentVariable('UatBootstrap__SharedPassword', 'Process')
$operationalPasswordPrompted = $false
try {
    Assert-True (-not ($PreflightOnly -and ($DryRun -or $DeployOnly -or
                $ReuseVerifiedArtifacts -or $ReuseApiOutputFromCommit -or
                $ReuseFrontendBuildFromCommit -or $FreshDatabaseName))) `
        'PreflightOnly cannot be combined with deployment, build-reuse, dry-run, or fresh-database options.'
    Assert-True (-not ($DeployOnly -and $DryRun)) `
        'DeployOnly applies a verified artifact and cannot be combined with DryRun.'
    Assert-True (-not ($DeployOnly -and $ReuseVerifiedArtifacts)) `
        'DeployOnly already consumes verified artifacts and cannot use ReuseVerifiedArtifacts.'
    Assert-True (-not ($DeployOnly -and ($ReuseApiOutputFromCommit -or $ReuseFrontendBuildFromCommit))) `
        'DeployOnly cannot use build-output reuse parameters.'
    Assert-True ($DeployOnly -eq (-not [string]::IsNullOrWhiteSpace($ArtifactDirectory))) `
        'Specify both DeployOnly and ArtifactDirectory, or neither.'
    Assert-True ([string]::IsNullOrWhiteSpace($FreshDatabaseName) -or $LocalVps) `
        'Fresh database cutover must run directly on the VPS with -LocalVps.'
    $requiredCommands = @('git', 'curl.exe', 'node')
    if (-not $LocalVps) {
        $requiredCommands += @('ssh', 'scp')
    }
    foreach ($command in $requiredCommands) {
        Assert-CommandExists $command
    }
    if (-not $LocalVps) {
        Assert-True (Test-Path -LiteralPath $SshKeyPath) `
            "SSH key is missing: $SshKeyPath"
    }
    Assert-True (Test-Path -LiteralPath $RemoteHelperLocalPath) `
        'Remote deployment helper is missing.'
    Assert-True (Test-Path -LiteralPath $BrowserSmokePath) `
        'Browser smoke helper is missing.'

    $script:Commit = (& git rev-parse HEAD).Trim()
    $script:ShortCommit = $script:Commit.Substring(0, 8)
    $script:ArtifactCommit = $script:Commit
    if (-not [string]::IsNullOrWhiteSpace($ExpectedCommit)) {
        if ($DeployOnly) {
            Assert-True ($ExpectedCommit -match '^[0-9a-fA-F]{40}$') `
                'DeployOnly requires the exact 40-character artifact commit.'
            $script:ArtifactCommit = $ExpectedCommit.ToLowerInvariant()
        }
        else {
            Assert-True ($script:Commit.StartsWith($ExpectedCommit,
                    [System.StringComparison]::OrdinalIgnoreCase)) `
                "HEAD $($script:Commit) does not match ExpectedCommit $ExpectedCommit."
        }
    }
    $dirty = @(& git status --porcelain)
    if (-not $AllowDirtyWorktree) {
        Assert-True ($dirty.Count -eq 0) `
            'The worktree is dirty. Commit or stash changes before deployment.'
    }
    if (-not $AllowNonRemoteHead) {
        $remoteHead = Get-RemoteMasterCommit
        Assert-True ($script:Commit -eq $remoteHead) `
            "HEAD is $($script:Commit), but origin/master is $remoteHead."
    }

    $script:DeploymentStamp = [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss')
    $releaseManifest = $null
    if ($PreflightOnly) {
        $deploymentId = "$($script:ShortCommit)-$($script:DeploymentStamp)"
        $releaseDirectory = Join-Path $ReleaseRoot "preflights\$deploymentId"
        $runDirectory = $releaseDirectory
        Assert-SafeChildPath $releaseDirectory $ReleaseRoot
        New-Item -ItemType Directory -Path $releaseDirectory -Force | Out-Null
    }
    elseif ($DeployOnly) {
        $releaseDirectory = [IO.Path]::GetFullPath($ArtifactDirectory)
        $releaseManifest = Test-ReleaseManifest (Join-Path $releaseDirectory 'release-manifest.json')
        Assert-True ($null -ne $releaseManifest) `
            'Deploy-only artifact validation failed: commit, environment, public URL, files, or hashes do not match.'
        $deploymentId = "$($releaseManifest.releaseId)-$($script:DeploymentStamp)"
        $runDirectory = Join-Path $ReleaseRoot "deployments\$deploymentId"
        Assert-SafeChildPath $runDirectory $ReleaseRoot
        New-Item -ItemType Directory -Path $runDirectory -Force | Out-Null
    }
    else {
        $deploymentId = "$($script:ShortCommit)-$($script:DeploymentStamp)"
        $releaseDirectory = Join-Path $ReleaseRoot $script:ShortCommit
        $runDirectory = $releaseDirectory
        New-Item -ItemType Directory -Path $releaseDirectory -Force | Out-Null
    }
    $migrationState = $null
    # Embed reviewed read-only probes so the content-addressed remote helper is
    # self-contained. CRLF-normalized source hashes invalidate stale reviews.
    $RemoteHelperLocalPath = New-RhemaVpsPreflightHelper -RepositoryRoot $RepositoryRoot `
        -OutputPath (Join-Path $runDirectory 'Invoke-RhemaVpsRemote.ps1')
    $remoteHelperHash = (Get-FileHash $RemoteHelperLocalPath -Algorithm SHA256).Hash
    $remoteHelperName = "Invoke-RhemaVpsRemote-$($remoteHelperHash.Substring(0,12)).ps1"
    $remoteHelperPath = Join-Path $RemotePackagesRoot $remoteHelperName

    Invoke-Step 'Upload versioned remote helper' {
        Copy-ToVps @($RemoteHelperLocalPath) $RemotePackagesRoot
        $uploadedDefaultName = Join-Path $RemotePackagesRoot `
            ([System.IO.Path]::GetFileName($RemoteHelperLocalPath))
        if ($LocalVps) {
            if ((Get-FileHash -LiteralPath $uploadedDefaultName -Algorithm SHA256).Hash -ne $remoteHelperHash) {
                throw 'Local VPS helper hash mismatch.'
            }
            Move-Item -LiteralPath $uploadedDefaultName -Destination $remoteHelperPath -Force
            Write-Output "REMOTE_HELPER|$remoteHelperPath|$remoteHelperHash"
            return
        }
        $renameScript = @"
`$source = '$uploadedDefaultName'
`$target = '$remoteHelperPath'
if ((Get-FileHash -LiteralPath `$source -Algorithm SHA256).Hash -ne '$remoteHelperHash') { throw 'Remote helper hash mismatch.' }
Move-Item -LiteralPath `$source -Destination `$target -Force
"REMOTE_HELPER|`$target|$remoteHelperHash"
"@
        $encoded = [Convert]::ToBase64String(
            [Text.Encoding]::Unicode.GetBytes($renameScript))
        $sshArguments = Get-SshArguments
        & ssh @sshArguments `
            "powershell.exe -NoLogo -NoProfile -NonInteractive -OutputFormat Text -ExecutionPolicy Bypass -EncodedCommand $encoded"
        if ($LASTEXITCODE -ne 0) { throw 'Remote helper verification failed.' }
    } | Out-Host

    if (-not $DryRun -and -not $PreflightOnly) {
        $pruneOutput = @(Invoke-Step 'Prune obsolete VPS deployment artifacts' {
            Invoke-RemoteHelper $remoteHelperPath 'Prune'
        })
        $pruneOutput | Out-Host
        Import-RemoteTimings $pruneOutput
        Assert-True ($pruneOutput -contains 'PRUNE|PASS') `
            'The VPS retention prune did not report success.'
    }

    $preflight = @(Invoke-Step 'Fail-fast VPS and migration preflight' {
        Invoke-RemoteHelper $remoteHelperPath 'Preflight' @{ FreshDatabaseName = $FreshDatabaseName }
    })
    Import-RemoteTimings $preflight
    if ($FreshDatabaseName) {
        Assert-True ($preflight -contains "FRESH_TARGET_READY|$FreshDatabaseName") 'Fresh database preflight did not confirm an absent target.'
        $migrationState = [ordered]@{ mode = 'FreshDatabase'; target = $FreshDatabaseName; provisioned = $false }
    } else { $migrationState = Compare-MigrationState $preflight $DryRun }

    if ($PreflightOnly) {
        $resultPath = Write-RunResult 'Passed' $deploymentId $releaseDirectory `
            $null $migrationState $null
        Write-Host "`nPREFLIGHT ONLY PASSED: $resultPath" -ForegroundColor Green
        Write-Host 'No release was built, no database was changed, and no service was restarted.'
        exit 0
    }

    if ($DryRun) {
        if ($FreshDatabaseName) {
            $resultPath = Write-RunResult 'Passed' $deploymentId $releaseDirectory $null $migrationState $null
            Write-Host "`nFRESH DATABASE PREFLIGHT PASSED: $resultPath" -ForegroundColor Green
            Write-Host 'No database created or service changed. Migration, seed and release checks run during deployment.'
            exit 0
        }
        $verify = @(Invoke-Step 'Verify deployed services and database' {
            Invoke-RemoteHelper $remoteHelperPath 'Verify'
        })
        Import-RemoteTimings $verify
        $migrationState = Compare-MigrationState $verify $true
        Invoke-Step 'Public API, asset, and CORS smoke' {
            Invoke-PublicSmoke '' '' '' -AllowConfigurationDrift
        } | Out-Host
        if (-not $SkipBrowserSmoke) {
            Invoke-Step 'Headless Chrome browser smoke' {
                Invoke-NativeChecked 'node' @($BrowserSmokePath, $PublicBaseUrl) `
                    'Browser smoke failed'
            } | Out-Host
        }
        $resultPath = Write-RunResult 'Passed' $deploymentId $releaseDirectory `
            $null $migrationState $null
        Write-Host "`nDRY RUN PASSED: $resultPath" -ForegroundColor Green
        exit 0
    }

    if ($PrepareOperationalUat -and $preflight -contains 'UAT_CREDENTIAL|REQUIRED') {
        Assert-True ([bool]$LocalVps) 'Set protected UatBootstrap__SharedPassword on the VPS or run with -LocalVps for the secure account-password prompt.'
    }

    if (-not $DeployOnly) {
        Assert-CommandExists 'dotnet'
        Assert-CommandExists 'npm.cmd'
        Assert-CommandExists 'robocopy.exe'
    }
    $manifestPath = Join-Path $releaseDirectory 'release-manifest.json'
    if ($DeployOnly) {
        Write-Host "Using prebuilt release $($releaseManifest.releaseId); no build or dependency command will run on the VPS." `
            -ForegroundColor Green
        $recordedUtc = [DateTime]::UtcNow.ToString('o')
        $StepResults.Add([ordered]@{
            name = 'Consume prebuilt immutable release artifacts'
            scope = 'Pipeline'
            status = 'Verified'
            startedUtc = $recordedUtc
            completedUtc = $recordedUtc
            durationSeconds = 0
        })
    }
    elseif ($ReuseVerifiedArtifacts) {
        $releaseManifest = Test-ReleaseManifest $manifestPath
    }
    if ($null -eq $releaseManifest) {
        $releaseManifest = Invoke-Step 'Build and package immutable release artifacts' {
            New-ReleaseArtifacts $releaseDirectory
        }
    }
    else {
        Write-Host "Reusing verified artifacts for $($script:Commit)." `
            -ForegroundColor Green
        $recordedUtc = [DateTime]::UtcNow.ToString('o')
        $StepResults.Add([ordered]@{
            name = 'Build and package immutable release artifacts'
            scope = 'Pipeline'
            status = 'Reused'
            startedUtc = $recordedUtc
            completedUtc = $recordedUtc
            durationSeconds = 0
        })
    }

    $apiPackagePath = Join-Path $releaseDirectory $releaseManifest.api.file
    $frontendPackagePath = Join-Path $releaseDirectory $releaseManifest.frontend.file
    Invoke-Step 'Upload and hash release packages' {
        Copy-ToVps @($apiPackagePath, $frontendPackagePath) $RemotePackagesRoot
    } | Out-Host

    $backupOutput = @(Invoke-Step 'Create and verify application and SQL backups' {
        Invoke-RemoteHelper $remoteHelperPath 'Backup' @{
            DeploymentId = $deploymentId
            ExpectedCommit = $script:ArtifactCommit
        }
    })
    Import-RemoteTimings $backupOutput

    if ($PrepareOperationalUat -and $preflight -contains 'UAT_CREDENTIAL|REQUIRED') {
        $secureOperationalPassword = Read-Host 'Initial password for NEW Procurement, Inventory and QS test accounts (existing passwords are preserved)' -AsSecureString
        $passwordPointer = [IntPtr]::Zero
        try {
            $passwordPointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secureOperationalPassword)
            $operationalPassword = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($passwordPointer)
            Assert-True (-not [string]::IsNullOrWhiteSpace($operationalPassword)) 'An initial account password is required.'
            [Environment]::SetEnvironmentVariable('UatBootstrap__SharedPassword', $operationalPassword, 'Process')
            $operationalPasswordPrompted = $true
        } finally {
            if ($passwordPointer -ne [IntPtr]::Zero) { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($passwordPointer) }
            $secureOperationalPassword.Dispose(); $operationalPassword=$null
        }
    }

    $applyOutput = @(Invoke-Step 'Apply API, migrations, and frontend' {
        if ($FreshDatabaseName) { $script:freshApplyAttempted = $true }
        Invoke-RemoteHelper $remoteHelperPath 'Apply' @{
            FreshDatabaseName = $FreshDatabaseName
            DeploymentId = $deploymentId
            ExpectedCommit = $script:ArtifactCommit
            ExpectedBuildId = $releaseManifest.buildId
            ExpectedCacheVersion = $releaseManifest.cacheVersion
            ReleaseId = $(if ($releaseManifest.releaseId) { $releaseManifest.releaseId } else { $deploymentId })
            ApiPackageName = $releaseManifest.api.file
            FrontendPackageName = $releaseManifest.frontend.file
            ApiSha256 = $releaseManifest.api.sha256
            FrontendSha256 = $releaseManifest.frontend.sha256
            ApiReadyTimeoutSeconds = $ApiReadyTimeoutSeconds
        }
    })
    $applyOutput | Out-Host
    Import-RemoteTimings $applyOutput
    $applicationApplyCompleted = $true

    if ($PrepareOperationalUat -and -not $FreshDatabaseName) {
        $seedOutput = @(Invoke-Step 'Seed and verify Procurement, Inventory and QS baseline' {
            Invoke-RemoteHelper $remoteHelperPath 'SeedOperational' @{ DeploymentId=$deploymentId }
        })
        $seedOutput | Out-Host
        Import-RemoteTimings $seedOutput
    }

    $verifyOutput = @(Invoke-Step 'Verify deployed services and database' {
        Invoke-RemoteHelper $remoteHelperPath 'Verify' @{
            ExpectedBuildId = $releaseManifest.buildId
            ExpectedCacheVersion = $releaseManifest.cacheVersion
        }
    })
    Import-RemoteTimings $verifyOutput
    $migrationState = Compare-MigrationState $verifyOutput $true

    Invoke-Step 'Public API, asset, and CORS smoke' {
        Invoke-PublicSmoke $releaseManifest.cacheVersion 'Test' $releaseManifest.commit
    } | Out-Host
    if (-not $SkipBrowserSmoke) {
        Invoke-Step 'Headless Chrome browser smoke' {
            Invoke-NativeChecked 'node' @($BrowserSmokePath, $PublicBaseUrl) `
                'Browser smoke failed'
        } | Out-Host
    }

    $resultPath = Write-RunResult 'Passed' $deploymentId $runDirectory `
        $releaseManifest $migrationState $null
    Invoke-Step 'Publish deployment evidence to VPS' {
        Copy-ToVps @($resultPath) $RemoteLogsRoot
    } | Out-Host

    if ($FreshDatabaseName) {
        $completeFreshOutput = @(Invoke-Step 'Complete verified fresh database cutover' {
            Invoke-RemoteHelper $remoteHelperPath 'CompleteFresh' @{
                DeploymentId = $deploymentId; ExpectedCommit = $script:ArtifactCommit; FreshDatabaseName = $FreshDatabaseName
            }
        })
        $completeFreshOutput | Out-Host
        Import-RemoteTimings $completeFreshOutput
        $freshApplyAttempted = $false
    }

    Write-DeploymentTimingSummary
    Write-Host "`nDEPLOYMENT PASSED: $resultPath" -ForegroundColor Green
}
catch {
    $deploymentFailure = $_
    if ($applicationApplyCompleted -and -not $FreshDatabaseName) {
        try {
            $rollbackOutput = @(Invoke-Step 'Rollback application release after failed verification' {
                Invoke-RemoteHelper $remoteHelperPath 'RollbackRelease' @{
                    DeploymentId = $deploymentId
                }
            })
            $rollbackOutput | Out-Host
            Import-RemoteTimings $rollbackOutput
        } catch {
            Write-Warning "Automatic application rollback failed. The database was not restored. $($_.Exception.Message)"
        }
    }
    if ($freshApplyAttempted) {
        try {
            $rollbackFreshOutput = @(Invoke-Step 'Restore original application and database connection' {
                Invoke-RemoteHelper $remoteHelperPath 'RollbackFresh' @{
                    DeploymentId = $deploymentId; ExpectedCommit = $script:ArtifactCommit; FreshDatabaseName = $FreshDatabaseName
                }
            })
            $rollbackFreshOutput | Out-Host
            Import-RemoteTimings $rollbackFreshOutput
        } catch {
            Write-Warning "Automatic rollback could not complete. Keep both databases and the deployment backup; review the VPS evidence. $($_.Exception.Message)"
        }
    }
    if ($null -ne $script:Commit) {
        $safeReleaseDirectory = if ($null -ne $runDirectory) {
            $runDirectory
        } elseif ($null -ne $releaseDirectory) {
            $releaseDirectory
        } else { Join-Path $ReleaseRoot $script:Commit.Substring(0, 8) }
        $failureId = if ($null -ne $deploymentId) { $deploymentId } else {
            "$($script:Commit.Substring(0,8))-failed-$([DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss'))"
        }
        $failurePath = Write-RunResult 'Failed' $failureId $safeReleaseDirectory `
            $releaseManifest $migrationState $deploymentFailure.Exception.Message
        Write-Host "Deployment evidence: $failurePath" -ForegroundColor Yellow
    }
    throw $deploymentFailure
}
finally {
    if ($null -ne $deploymentFailure) { Write-DeploymentTimingSummary }
    if ($operationalPasswordPrompted) {
        [Environment]::SetEnvironmentVariable('UatBootstrap__SharedPassword', $priorOperationalPassword, 'Process')
    }
    $priorOperationalPassword=$null
    Pop-Location
}
