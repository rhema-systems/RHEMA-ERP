[CmdletBinding()]
param(
    [string]$PublicIp = '149.102.145.190',
    [int]$PublicPort = 8443,
    [string]$ServiceName = 'RhemaERPHTTPSIPProxy'
)

$ErrorActionPreference = 'Stop'

function Assert-Administrator {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($identity)
    if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
        throw 'Run this repair from an elevated Windows PowerShell session.'
    }
}

function Resolve-Executable {
    param([Parameter(Mandatory = $true)][string]$Name, [string[]]$FallbackPaths = @())
    $command = Get-Command $Name -ErrorAction SilentlyContinue
    if ($null -ne $command) { return $command.Source }
    foreach ($path in $FallbackPaths) {
        if (Test-Path -LiteralPath $path) { return $path }
    }
    throw "Required executable was not found: $Name"
}

function Invoke-Nssm {
    param([Parameter(ValueFromRemainingArguments = $true)][string[]]$Arguments)
    & $script:NssmPath @Arguments | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "NSSM failed with exit code ${LASTEXITCODE}: $($Arguments -join ' ')"
    }
}

function Get-HttpsStatusCode {
    param([Parameter(Mandatory = $true)][string]$Uri)
    $output = & curl.exe -k -sS --max-time 20 -o NUL -w '%{http_code}' $Uri 2>$null
    if ($LASTEXITCODE -ne 0) { return '000' }
    return ([string]$output).Trim()
}

function Install-LegoAcmeClient {
    param([Parameter(Mandatory = $true)][string]$DestinationPath)

    $version = '5.3.1'
    $archiveName = "lego_v${version}_windows_amd64.zip"
    $releaseRoot = "https://github.com/go-acme/lego/releases/download/v${version}"
    $checksumsName = "lego_${version}_checksums.txt"
    $expectedChecksumsHash = 'd069acad0ad28bcfc03a9a94ea127ae78c84e6ba5f3387886033abfb1cd88527'
    $downloadRoot = Join-Path $env:TEMP "rhema-lego-$([Guid]::NewGuid().ToString('N'))"
    $archivePath = Join-Path $downloadRoot $archiveName
    $checksumsPath = Join-Path $downloadRoot $checksumsName
    $extractRoot = Join-Path $downloadRoot 'extract'

    [System.IO.Directory]::CreateDirectory($downloadRoot) | Out-Null
    try {
        [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
        Write-Output "ACME_CLIENT|DOWNLOAD|lego v$version"
        Invoke-WebRequest -Uri "$releaseRoot/$checksumsName" `
            -OutFile $checksumsPath -UseBasicParsing
        Invoke-WebRequest -Uri "$releaseRoot/$archiveName" `
            -OutFile $archivePath -UseBasicParsing

        $checksumsHash = (Get-FileHash -LiteralPath $checksumsPath -Algorithm SHA256).Hash.ToLowerInvariant()
        if ($checksumsHash -ne $expectedChecksumsHash) {
            throw 'The downloaded lego checksum manifest does not match the pinned release hash.'
        }

        $escapedArchiveName = [Regex]::Escape($archiveName)
        $checksumLine = Get-Content -LiteralPath $checksumsPath |
            Where-Object { $_ -match "^([0-9a-fA-F]{64})\s+\*?$escapedArchiveName$" } |
            Select-Object -First 1
        if ([string]::IsNullOrWhiteSpace($checksumLine)) {
            throw "The checksum manifest does not contain $archiveName."
        }
        $expectedArchiveHash = ([Regex]::Match(
                $checksumLine,
                '^([0-9a-fA-F]{64})')).Groups[1].Value.ToLowerInvariant()
        $archiveHash = (Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash.ToLowerInvariant()
        if ($archiveHash -ne $expectedArchiveHash) {
            throw "The downloaded $archiveName failed SHA-256 verification."
        }

        Expand-Archive -LiteralPath $archivePath -DestinationPath $extractRoot -Force
        $downloadedExecutable = Get-ChildItem -LiteralPath $extractRoot `
            -Filter 'lego.exe' -File -Recurse | Select-Object -First 1
        if ($null -eq $downloadedExecutable) {
            throw "The verified $archiveName does not contain lego.exe."
        }

        [System.IO.Directory]::CreateDirectory(
            (Split-Path -Parent $DestinationPath)) | Out-Null
        $temporaryDestination = "$DestinationPath.new"
        Copy-Item -LiteralPath $downloadedExecutable.FullName `
            -Destination $temporaryDestination -Force
        Move-Item -LiteralPath $temporaryDestination `
            -Destination $DestinationPath -Force

        $versionOutput = & $DestinationPath --version 2>&1 | Out-String
        if ($LASTEXITCODE -ne 0 -or $versionOutput -notmatch [Regex]::Escape($version)) {
            throw "The installed ACME client did not report the expected version $version."
        }
        Write-Output "ACME_CLIENT|INSTALLED|$($versionOutput.Trim())"
    }
    finally {
        Remove-Item -LiteralPath $downloadRoot -Recurse -Force `
            -ErrorAction SilentlyContinue
    }
}

Assert-Administrator

$rhemaRoot = 'C:\RhemaERP'
$proxyRoot = Join-Path $rhemaRoot 'proxy'
$proxySource = Join-Path $PSScriptRoot 'https-ip-proxy.js'
$proxyTarget = Join-Path $proxyRoot 'https-ip-proxy.js'
$certificatePath = Join-Path $rhemaRoot 'certs\lego-ip\certificates\rhemaerp-ip.crt'
$privateKeyPath = Join-Path $rhemaRoot 'certs\lego-ip\certificates\rhemaerp-ip.key'
$logRoot = Join-Path $rhemaRoot 'logs'
$renewalScript = Join-Path $rhemaRoot 'deploy\renew-ip-cert.cmd'
$renewalLog = Join-Path $logRoot 'renew-ip-cert.log'
$certificateRepair = Join-Path $PSScriptRoot 'Repair-RhemaVpsCertificateRenewal.ps1'
$legoPath = Join-Path $rhemaRoot 'tools\lego\lego.exe'

if (-not (Test-Path -LiteralPath $rhemaRoot -PathType Container)) {
    throw "The Rhema ERP deployment root is missing: $rhemaRoot"
}
foreach ($requiredSource in @($proxySource, $certificateRepair)) {
    if (-not (Test-Path -LiteralPath $requiredSource -PathType Leaf)) {
        throw "Repository repair file is missing: $requiredSource"
    }
}

$nodePath = Resolve-Executable -Name 'node.exe' -FallbackPaths @('C:\Program Files\nodejs\node.exe')
$script:NssmPath = Resolve-Executable -Name 'nssm.exe' -FallbackPaths @(
    'C:\Users\Administrator\AppData\Local\Microsoft\WinGet\Links\nssm.exe',
    'C:\ProgramData\chocolatey\bin\nssm.exe'
)

foreach ($directory in @($proxyRoot, $logRoot)) {
    [System.IO.Directory]::CreateDirectory($directory) | Out-Null
}

$temporaryProxy = "$proxyTarget.new"
Copy-Item -LiteralPath $proxySource -Destination $temporaryProxy -Force
Move-Item -LiteralPath $temporaryProxy -Destination $proxyTarget -Force

$certificateReady = (Test-Path -LiteralPath $certificatePath -PathType Leaf) -and
    (Test-Path -LiteralPath $privateKeyPath -PathType Leaf) -and
    ((Get-Item -LiteralPath $certificatePath).Length -gt 0) -and
    ((Get-Item -LiteralPath $privateKeyPath).Length -gt 0)

if (-not $certificateReady) {
    Write-Output 'CERTIFICATE|MISSING|Configuring ACME and requesting the IP certificate.'
    if (-not (Test-Path -LiteralPath $legoPath -PathType Leaf)) {
        Install-LegoAcmeClient -DestinationPath $legoPath
    }
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $certificateRepair -PublicIp $PublicIp
    if ($LASTEXITCODE -ne 0) { throw "Certificate renewal setup failed with exit code $LASTEXITCODE." }
    if (-not (Test-Path -LiteralPath $renewalScript -PathType Leaf)) {
        throw "Certificate renewal command was not installed: $renewalScript"
    }
    & cmd.exe /d /c $renewalScript
    if ($LASTEXITCODE -ne 0) {
        if (Test-Path -LiteralPath $renewalLog) {
            Write-Output 'The final certificate-renewal log entries follow:'
            Get-Content -LiteralPath $renewalLog -Tail 30
        }
        throw "The IP certificate request failed with exit code $LASTEXITCODE."
    }
}

foreach ($path in @($certificatePath, $privateKeyPath)) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf) -or
        (Get-Item -LiteralPath $path).Length -le 0) {
        throw "The HTTPS credential file is missing or empty: $path"
    }
}

$existingService = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if ($null -eq $existingService) {
    Invoke-Nssm install $ServiceName $nodePath $proxyTarget
}
else {
    Stop-Service -Name $ServiceName -Force -ErrorAction SilentlyContinue
    $existingService.WaitForStatus('Stopped', [TimeSpan]::FromSeconds(30))
}

Invoke-Nssm set $ServiceName Application $nodePath
Invoke-Nssm set $ServiceName AppParameters $proxyTarget
Invoke-Nssm set $ServiceName AppDirectory $proxyRoot
Invoke-Nssm set $ServiceName AppStdout (Join-Path $logRoot 'https-ip-proxy.stdout.log')
Invoke-Nssm set $ServiceName AppStderr (Join-Path $logRoot 'https-ip-proxy.stderr.log')
Invoke-Nssm set $ServiceName AppRotateFiles 1
Invoke-Nssm set $ServiceName AppRotateOnline 1
Invoke-Nssm set $ServiceName AppExit Default Restart
Invoke-Nssm set $ServiceName Start SERVICE_AUTO_START

$firewallRuleName = 'Rhema ERP HTTPS 8443'
$firewallRule = Get-NetFirewallRule -DisplayName $firewallRuleName -ErrorAction SilentlyContinue
if ($null -eq $firewallRule) {
    New-NetFirewallRule -DisplayName $firewallRuleName -Direction Inbound `
        -Protocol TCP -LocalPort $PublicPort -Action Allow | Out-Null
}
else {
    Enable-NetFirewallRule -DisplayName $firewallRuleName | Out-Null
}

Start-Service -Name $ServiceName
$service = Get-Service -Name $ServiceName
$service.WaitForStatus('Running', [TimeSpan]::FromSeconds(30))

$deadline = [DateTime]::UtcNow.AddSeconds(90)
$localStatus = '000'
do {
    Start-Sleep -Seconds 3
    $localStatus = Get-HttpsStatusCode -Uri "https://127.0.0.1:$PublicPort/health"
} while ($localStatus -ne '200' -and [DateTime]::UtcNow -lt $deadline)

if ($localStatus -ne '200') {
    $stderrPath = Join-Path $logRoot 'https-ip-proxy.stderr.log'
    if (Test-Path -LiteralPath $stderrPath) {
        Write-Output 'The final HTTPS proxy error log entries follow:'
        Get-Content -LiteralPath $stderrPath -Tail 30
    }
    throw "The HTTPS proxy did not return HTTP 200 from its local health route; final status was $localStatus."
}

$loginStatus = Get-HttpsStatusCode -Uri "https://127.0.0.1:$PublicPort/login"
if ($loginStatus -ne '200') { throw "The HTTPS proxy frontend route returned HTTP $loginStatus instead of 200." }
$tenantStatus = Get-HttpsStatusCode -Uri "https://127.0.0.1:$PublicPort/api/tenant"
if ($tenantStatus -ne '200') { throw "The HTTPS proxy API route returned HTTP $tenantStatus instead of 200." }

$publicStatus = Get-HttpsStatusCode -Uri "https://${PublicIp}:$PublicPort/health"
Remove-Item -LiteralPath (Join-Path $logRoot 'renew-ip-cert.restart-required') -Force -ErrorAction SilentlyContinue

Write-Output "SERVICE|$ServiceName|$((Get-Service -Name $ServiceName).Status)"
Write-Output "LOCAL_HTTPS_HEALTH|$localStatus"
Write-Output "LOCAL_HTTPS_LOGIN|$loginStatus"
Write-Output "LOCAL_HTTPS_TENANT|$tenantStatus"
Write-Output "PUBLIC_HTTPS_HEALTH|$publicStatus"
Write-Output 'HTTPS_PROXY_REPAIR|PASS'
if ($publicStatus -ne '200') {
    Write-Warning 'The proxy works locally, but the public-IP health probe did not return HTTP 200. Check the VPS provider firewall or NAT loopback, then run the deployment dry-run from a network that can reach the public IP.'
}
