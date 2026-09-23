[CmdletBinding()]
param(
    [string]$ApiRoot = 'C:\RhemaERP\api',
    [string]$ApiServiceName = 'RhemaERPAPI'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$apiExecutable = Join-Path $ApiRoot 'ErpSystem.Api.exe'
if (-not (Test-Path -LiteralPath $apiExecutable -PathType Leaf)) {
    throw "The deployed API executable is missing: $apiExecutable"
}

$serviceParametersPath = "Registry::HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\$ApiServiceName\Parameters"
if (-not (Test-Path -LiteralPath $serviceParametersPath)) {
    throw "The NSSM service parameters are missing: $serviceParametersPath"
}

$serviceEnvironment = @(
    (Get-ItemProperty -LiteralPath $serviceParametersPath -Name AppEnvironmentExtra -ErrorAction Stop).AppEnvironmentExtra
)
if ($serviceEnvironment.Count -eq 0) {
    throw "$ApiServiceName has no AppEnvironmentExtra configuration to reuse for the seed command."
}

$priorProcessValues = @{}
foreach ($entry in $serviceEnvironment) {
    if ([string]::IsNullOrWhiteSpace([string]$entry)) { continue }
    $separator = ([string]$entry).IndexOf('=')
    if ($separator -lt 1) { continue }
    $name = ([string]$entry).Substring(0, $separator)
    $value = ([string]$entry).Substring($separator + 1)
    if (-not $priorProcessValues.ContainsKey($name)) {
        $priorProcessValues[$name] = [Environment]::GetEnvironmentVariable($name, 'Process')
    }
    [Environment]::SetEnvironmentVariable($name, $value, 'Process')
}

$passwordPointer = [IntPtr]::Zero
try {
    $securePassword = Read-Host 'Enter the existing shared UAT password' -AsSecureString
    $passwordPointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($securePassword)
    $plainPassword = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($passwordPointer)
    $secretName = 'UatBootstrap__SharedPassword'
    if (-not $priorProcessValues.ContainsKey($secretName)) {
        $priorProcessValues[$secretName] = [Environment]::GetEnvironmentVariable($secretName, 'Process')
    }
    [Environment]::SetEnvironmentVariable($secretName, $plainPassword, 'Process')

    Push-Location $ApiRoot
    try {
        & $apiExecutable seed-operational-uat
        if ($LASTEXITCODE -ne 0) {
            throw "Operational UAT baseline seeding failed with exit code $LASTEXITCODE."
        }
    }
    finally {
        Pop-Location
    }
}
finally {
    if ($passwordPointer -ne [IntPtr]::Zero) {
        [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($passwordPointer)
    }
    Remove-Variable plainPassword, securePassword -ErrorAction SilentlyContinue
    foreach ($name in $priorProcessValues.Keys) {
        [Environment]::SetEnvironmentVariable($name, $priorProcessValues[$name], 'Process')
    }
}

Write-Host 'Operational UAT users, roles, Finance masters, Inventory masters, and warehouse scopes are ready.'
