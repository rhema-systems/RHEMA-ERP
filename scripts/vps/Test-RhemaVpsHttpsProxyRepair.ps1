$ErrorActionPreference = 'Stop'

function Assert-Contains {
    param([string]$Text, [string]$Needle, [string]$Message)
    if (-not $Text.Contains($Needle)) { throw $Message }
}

$proxy = Get-Content (Join-Path $PSScriptRoot 'https-ip-proxy.js') -Raw
$repair = Get-Content (Join-Path $PSScriptRoot 'Repair-RhemaVpsHttpsProxy.ps1') -Raw
$remote = Get-Content (Join-Path $PSScriptRoot 'Invoke-RhemaVpsRemote.ps1') -Raw

Assert-Contains $proxy "path.startsWith('/api/')" 'Proxy must route API paths to the API service.'
Assert-Contains $proxy "path.startsWith('/health/')" 'Proxy must route nested health paths to the API service.'
Assert-Contains $proxy "server.on('upgrade'" 'Proxy must support WebSocket upgrades.'
Assert-Contains $proxy "port: 5000" 'Proxy must target the loopback API port.'
Assert-Contains $proxy "port: 3001" 'Proxy must target the loopback frontend port.'
Assert-Contains $repair 'Repair-RhemaVpsCertificateRenewal.ps1' 'Repair must configure certificate renewal.'
Assert-Contains $repair 'renew-ip-cert.cmd' 'Repair must request a missing certificate.'
Assert-Contains $repair 'Invoke-Nssm set $ServiceName Application' 'Repair must correct an existing NSSM service.'
Assert-Contains $repair 'https://127.0.0.1:$PublicPort/health' 'Repair must verify the local TLS route.'
Assert-Contains $repair 'https://127.0.0.1:$PublicPort/api/tenant' 'Repair must verify HTTPS API routing.'
Assert-Contains $remote 'Repair-RhemaVpsHttpsProxy.ps1' 'Deployment preflight must explain how to restore a missing proxy.'

Write-Output 'Rhema VPS HTTPS proxy repair checks passed.'
