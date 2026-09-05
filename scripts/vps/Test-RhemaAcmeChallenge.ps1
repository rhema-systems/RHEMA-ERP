[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$WebRoot,

    [string]$PublicIp = '149.102.145.190',

    [ValidateRange(5, 60)]
    [int]$TimeoutSeconds = 20
)

$ErrorActionPreference = 'Stop'

$allowedRoot = [System.IO.Path]::GetFullPath('C:\RhemaERP').TrimEnd('\') + '\'
$resolvedWebRoot = [System.IO.Path]::GetFullPath($WebRoot).TrimEnd('\') + '\'
if (-not $resolvedWebRoot.StartsWith(
        $allowedRoot,
        [System.StringComparison]::OrdinalIgnoreCase)) {
    throw 'The ACME webroot must remain under C:\RhemaERP.'
}

$challengeDirectory = Join-Path $resolvedWebRoot '.well-known\acme-challenge'
[System.IO.Directory]::CreateDirectory($challengeDirectory) | Out-Null

$probeName = 'rhema-acme-probe-' + [Guid]::NewGuid().ToString('N')
$probePath = [System.IO.Path]::GetFullPath(
    (Join-Path $challengeDirectory $probeName))
if (-not $probePath.StartsWith(
        $resolvedWebRoot,
        [System.StringComparison]::OrdinalIgnoreCase)) {
    throw 'The ACME probe path escaped the configured webroot.'
}

$probeBody = 'rhema-acme-ok-' + [Guid]::NewGuid().ToString('N')

try {
    [System.IO.File]::WriteAllText(
        $probePath,
        $probeBody,
        (New-Object System.Text.UTF8Encoding($false)))

    $uri = "http://$PublicIp/.well-known/acme-challenge/$probeName"
    Add-Type -AssemblyName System.Net.Http
    $handler = New-Object System.Net.Http.HttpClientHandler
    $handler.UseProxy = $false
    $client = New-Object System.Net.Http.HttpClient($handler)
    $client.Timeout = [TimeSpan]::FromSeconds($TimeoutSeconds)
    try {
        $response = $client.GetAsync($uri).GetAwaiter().GetResult()
        $statusCode = [int]$response.StatusCode
        $responseBody = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
    }
    finally {
        if ($response) {
            $response.Dispose()
        }
        $client.Dispose()
        $handler.Dispose()
    }

    if ($statusCode -ne 200) {
        throw "ACME challenge preflight returned HTTP $statusCode."
    }

    if ($responseBody.Trim() -ne $probeBody) {
        throw 'ACME challenge preflight returned an unexpected response body.'
    }

    Write-Output 'ACME_CHALLENGE_PREFLIGHT|PASS'
}
finally {
    if (Test-Path -LiteralPath $probePath) {
        Remove-Item `
            -LiteralPath $probePath `
            -Force `
            -ErrorAction SilentlyContinue
    }
}
