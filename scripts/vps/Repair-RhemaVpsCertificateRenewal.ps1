[CmdletBinding()]
param(
    [string]$PublicIp = '149.102.145.190',
    [string]$SiteName = 'RhemaERPACME',
    [string]$ApplicationPoolName = 'RhemaERPACMEPool',
    [string]$WebRoot = 'C:\RhemaERP\acme-webroot',
    [string]$RenewalScriptPath = 'C:\RhemaERP\deploy\renew-ip-cert.cmd'
)

$ErrorActionPreference = 'Stop'

function Assert-RhemaPath {
    param([Parameter(Mandatory = $true)][string]$Path)

    $allowedRoot = [System.IO.Path]::GetFullPath('C:\RhemaERP').TrimEnd('\') + '\'
    $resolved = [System.IO.Path]::GetFullPath($Path)
    if (-not $resolved.StartsWith(
            $allowedRoot,
            [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Path is outside the RhemaERP deployment root: $resolved"
    }

    return $resolved
}

$resolvedWebRoot = Assert-RhemaPath $WebRoot
$resolvedRenewalScript = Assert-RhemaPath $RenewalScriptPath
$deployRoot = Assert-RhemaPath 'C:\RhemaERP\deploy'
$backupRoot = Assert-RhemaPath 'C:\RhemaERP\backups\certificate-renewal'
$renewalTemp = Assert-RhemaPath 'C:\RhemaERP\tmp\certificate-renewal'
$challengeRoot = Join-Path $resolvedWebRoot '.well-known\acme-challenge'
$probeScriptSource = Join-Path $PSScriptRoot 'Test-RhemaAcmeChallenge.ps1'
$probeScriptTarget = Join-Path $deployRoot 'Test-RhemaAcmeChallenge.ps1'
$renewalScriptSource = Join-Path $PSScriptRoot 'renew-ip-cert.cmd'
$reloadScriptSource = Join-Path $PSScriptRoot 'reload-ip-cert.cmd'
$reloadScriptTarget = Join-Path $deployRoot 'reload-ip-cert.cmd'

if (-not (Test-Path -LiteralPath $probeScriptSource)) {
    throw "The ACME preflight helper is missing: $probeScriptSource"
}

if (-not (Test-Path -LiteralPath $renewalScriptSource)) {
    throw "The hardened renewal script is missing: $renewalScriptSource"
}

if (-not (Test-Path -LiteralPath $reloadScriptSource)) {
    throw "The certificate reload script is missing: $reloadScriptSource"
}

foreach ($path in @(
        $resolvedWebRoot,
        $challengeRoot,
        $deployRoot,
        $backupRoot,
        $renewalTemp)) {
    [System.IO.Directory]::CreateDirectory($path) | Out-Null
}

$webConfig = @'
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <system.webServer>
    <staticContent>
      <remove fileExtension="." />
      <mimeMap fileExtension="." mimeType="text/plain" />
    </staticContent>
  </system.webServer>
</configuration>
'@
[System.IO.File]::WriteAllText(
    (Join-Path $resolvedWebRoot 'web.config'),
    $webConfig,
    (New-Object System.Text.UTF8Encoding($false)))

Import-Module WebAdministration

if (-not (Test-Path "IIS:\AppPools\$ApplicationPoolName")) {
    New-WebAppPool -Name $ApplicationPoolName | Out-Null
}
Set-ItemProperty `
    "IIS:\AppPools\$ApplicationPoolName" `
    -Name managedRuntimeVersion `
    -Value ''

if (-not (Test-Path "IIS:\Sites\$SiteName")) {
    New-Website `
        -Name $SiteName `
        -PhysicalPath $resolvedWebRoot `
        -Port 80 `
        -IPAddress $PublicIp `
        -HostHeader '' `
        -ApplicationPool $ApplicationPoolName | Out-Null
}
else {
    Set-ItemProperty `
        "IIS:\Sites\$SiteName" `
        -Name physicalPath `
        -Value $resolvedWebRoot
    Set-ItemProperty `
        "IIS:\Sites\$SiteName" `
        -Name applicationPool `
        -Value $ApplicationPoolName

    $binding = Get-WebBinding -Name $SiteName -Protocol 'http' |
        Where-Object {
            $_.bindingInformation -eq "${PublicIp}:80:"
        }
    if (-not $binding) {
        New-WebBinding `
            -Name $SiteName `
            -Protocol 'http' `
            -IPAddress $PublicIp `
            -Port 80 `
            -HostHeader '' | Out-Null
    }
}

Start-WebAppPool `
    -Name $ApplicationPoolName `
    -ErrorAction SilentlyContinue
Start-Website `
    -Name $SiteName `
    -ErrorAction SilentlyContinue

Copy-Item `
    -LiteralPath $probeScriptSource `
    -Destination $probeScriptTarget `
    -Force

& $probeScriptTarget -WebRoot $resolvedWebRoot -PublicIp $PublicIp

$timestamp = [DateTime]::UtcNow.ToString('yyyyMMddHHmmss')
$backupPath = Join-Path $backupRoot "renew-ip-cert-$timestamp.cmd"
if (Test-Path -LiteralPath $resolvedRenewalScript) {
    Copy-Item `
        -LiteralPath $resolvedRenewalScript `
        -Destination $backupPath `
        -ErrorAction Stop
}

$temporaryPath = $resolvedRenewalScript + '.new'
Copy-Item `
    -LiteralPath $renewalScriptSource `
    -Destination $temporaryPath `
    -Force
Move-Item `
    -LiteralPath $temporaryPath `
    -Destination $resolvedRenewalScript `
    -Force

Copy-Item `
    -LiteralPath $reloadScriptSource `
    -Destination $reloadScriptTarget `
    -Force

$renewalIdentity = 'NT AUTHORITY\NETWORK SERVICE'
foreach ($aclTarget in @(
        'C:\RhemaERP\certs\lego-ip',
        'C:\RhemaERP\acme-webroot',
        'C:\RhemaERP\logs',
        $renewalTemp)) {
    & icacls.exe `
        $aclTarget `
        /grant "${renewalIdentity}:(OI)(CI)(M)" `
        /T `
        /C | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to grant renewal-account access to $aclTarget."
    }
}
& icacls.exe `
    'C:\RhemaERP\tools\lego' `
    /grant "${renewalIdentity}:(OI)(CI)(RX)" `
    /T `
    /C | Out-Null
if ($LASTEXITCODE -ne 0) {
    throw 'Failed to grant renewal-account access to the lego directory.'
}
& icacls.exe `
    $resolvedRenewalScript `
    /grant "${renewalIdentity}:(RX)" `
    /C | Out-Null
if ($LASTEXITCODE -ne 0) {
    throw 'Failed to grant renewal-account access to the renewal script.'
}

$renewalAction = New-ScheduledTaskAction `
    -Execute 'cmd.exe' `
    -Argument '/d /c C:\RhemaERP\deploy\renew-ip-cert.cmd'
$renewalTrigger = New-ScheduledTaskTrigger -Daily -At '02:15'
$renewalSettings = New-ScheduledTaskSettingsSet `
    -ExecutionTimeLimit (New-TimeSpan -Minutes 15) `
    -StartWhenAvailable `
    -MultipleInstances IgnoreNew
$renewalPrincipal = New-ScheduledTaskPrincipal `
    -UserId $renewalIdentity `
    -LogonType ServiceAccount `
    -RunLevel Highest
Get-ScheduledTask `
    -TaskName 'RhemaERP-RenewIPCert' `
    -ErrorAction SilentlyContinue |
    Unregister-ScheduledTask -Confirm:$false
Register-ScheduledTask `
    -TaskName 'RhemaERP-RenewIPCert' `
    -Action $renewalAction `
    -Trigger $renewalTrigger `
    -Settings $renewalSettings `
    -Principal $renewalPrincipal `
    -Description 'Renew the Rhema ERP short-lived IP certificate.' `
    -Force | Out-Null

$legacyRenewalAccountName = 'RhemaCertRenewal'
$legacyRenewalIdentity = `
    "$env:COMPUTERNAME\$legacyRenewalAccountName"
if (Get-LocalUser `
        -Name $legacyRenewalAccountName `
        -ErrorAction SilentlyContinue) {
    foreach ($aclTarget in @(
            'C:\RhemaERP\certs\lego-ip',
            'C:\RhemaERP\acme-webroot',
            'C:\RhemaERP\logs',
            'C:\RhemaERP\tools\lego',
            $renewalTemp)) {
        & icacls.exe `
            $aclTarget `
            /remove $legacyRenewalIdentity `
            /T `
            /C | Out-Null
    }
    & icacls.exe `
        $resolvedRenewalScript `
        /remove $legacyRenewalIdentity `
        /C | Out-Null
    Remove-LocalUser -Name $legacyRenewalAccountName
}

$reloadAction = New-ScheduledTaskAction `
    -Execute 'cmd.exe' `
    -Argument '/d /c C:\RhemaERP\deploy\reload-ip-cert.cmd'
$reloadTrigger = New-ScheduledTaskTrigger -Daily -At '02:35'
$reloadPrincipal = New-ScheduledTaskPrincipal `
    -UserId 'SYSTEM' `
    -LogonType ServiceAccount `
    -RunLevel Highest
$reloadSettings = New-ScheduledTaskSettingsSet `
    -ExecutionTimeLimit (New-TimeSpan -Minutes 5) `
    -StartWhenAvailable
Register-ScheduledTask `
    -TaskName 'RhemaERP-ReloadIPCertificate' `
    -Action $reloadAction `
    -Trigger $reloadTrigger `
    -Principal $reloadPrincipal `
    -Settings $reloadSettings `
    -Description 'Reload the Rhema ERP HTTPS proxy only after a certificate change.' `
    -Force | Out-Null

$siteState = (Get-WebsiteState -Name $SiteName).Value
Write-Output "ACME_SITE|$SiteName|$siteState"
Write-Output "ACME_WEBROOT|$resolvedWebRoot"
Write-Output "RENEWAL_BACKUP|$backupPath"
Write-Output 'RENEWAL_TASK_TIMEOUT|PT15M'
Write-Output "RENEWAL_TASK_ACCOUNT|$renewalIdentity"
Write-Output 'CERTIFICATE_RELOAD_TASK|RhemaERP-ReloadIPCertificate'
Write-Output 'CERTIFICATE_RENEWAL_REPAIR|PASS'
