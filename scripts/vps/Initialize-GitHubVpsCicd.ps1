[CmdletBinding()]
param(
    [ValidatePattern('^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$')]
    [string]$Repository = 'rhema-systems/RHEMA-ERP',
    [string]$VpsHost = '63.141.230.56',
    [ValidateRange(1, 65535)]
    [int]$SshPort = 2222,
    [string]$SshUser = 'Administrator',
    [uri]$PublicBaseUrl = 'https://63.141.230.56',
    [string]$SshPrivateKeyPath = (Join-Path $env:USERPROFILE '.ssh\id_rsa'),
    [string]$ApiServiceXmlPath = 'C:\RhemaERP\services\api\RhemaERPAPI.xml'
)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

function Assert-True {
    param([bool]$Condition, [string]$Message)
    if (-not $Condition) { throw $Message }
}

function Get-ApiServiceEnvironment {
    $values = @{}
    if (Test-Path -LiteralPath $ApiServiceXmlPath) {
        [xml]$service = Get-Content -LiteralPath $ApiServiceXmlPath -Raw
        foreach ($node in @($service.service.env)) {
            $values[[string]$node.name] = [string]$node.value
        }
        return $values
    }

    $nssmPath = 'HKLM:\SYSTEM\CurrentControlSet\Services\RhemaERPAPI\Parameters'
    Assert-True (Test-Path -LiteralPath $nssmPath) `
        "API service configuration was not found at $ApiServiceXmlPath or $nssmPath."
    $properties = Get-ItemProperty -LiteralPath $nssmPath
    foreach ($entry in @($properties.AppEnvironment) + @($properties.AppEnvironmentExtra)) {
        $text = [string]$entry
        $separator = $text.IndexOf('=')
        if ($separator -gt 0) {
            $values[$text.Substring(0, $separator)] = $text.Substring($separator + 1)
        }
    }
    return $values
}

function Set-GitHubSecretFromMemory {
    param([string]$Name, [string]$Value)
    Assert-True (-not [string]::IsNullOrWhiteSpace($Value)) `
        "Protected value for $Name is empty."

    $startInfo = [Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = (Get-Command gh.exe -ErrorAction Stop).Source
    $startInfo.UseShellExecute = $false
    $startInfo.CreateNoWindow = $true
    $startInfo.RedirectStandardInput = $true
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    $startInfo.Arguments = "secret set $Name --repo $Repository"

    $process = [Diagnostics.Process]::new()
    $process.StartInfo = $startInfo
    try {
        [void]$process.Start()
        $process.StandardInput.Write($Value)
        $process.StandardInput.Close()
        $stdoutTask = $process.StandardOutput.ReadToEndAsync()
        $stderrTask = $process.StandardError.ReadToEndAsync()
        $process.WaitForExit()
        [void]$stdoutTask.GetAwaiter().GetResult()
        $stderr = $stderrTask.GetAwaiter().GetResult()
        Assert-True ($process.ExitCode -eq 0) `
            "GitHub rejected secret $Name. $($stderr.Trim())"
    }
    finally {
        $process.Dispose()
    }
}

Assert-True ([Environment]::OSVersion.Platform -eq [PlatformID]::Win32NT) `
    'Run this bootstrap from the Windows test VPS.'
Assert-True ($PublicBaseUrl.IsAbsoluteUri -and $PublicBaseUrl.Scheme -eq 'https' -and
    -not $PublicBaseUrl.UserInfo -and -not $PublicBaseUrl.Query -and
    -not $PublicBaseUrl.Fragment -and $PublicBaseUrl.AbsolutePath -eq '/') `
    'PublicBaseUrl must be an HTTPS origin without a path, query, credentials, or fragment.'
foreach ($command in @('gh.exe', 'ssh-keygen.exe')) {
    Assert-True ($null -ne (Get-Command $command -ErrorAction SilentlyContinue)) `
        "Required command is unavailable: $command"
}
Assert-True (Test-Path -LiteralPath $SshPrivateKeyPath -PathType Leaf) `
    "Deployment SSH private key was not found: $SshPrivateKeyPath"

& gh.exe auth status --hostname github.com | Out-Null
Assert-True ($LASTEXITCODE -eq 0) 'GitHub CLI is not authenticated.'
$resolvedRepository = (& gh.exe repo view $Repository --json nameWithOwner --jq '.nameWithOwner').Trim()
Assert-True ($LASTEXITCODE -eq 0 -and $resolvedRepository -ceq $Repository) `
    "GitHub repository access could not be verified for $Repository."

$serviceEnvironment = Get-ApiServiceEnvironment
$syncfusionLicense = [string]$serviceEnvironment['Syncfusion__LicenseKey']
if ([string]::IsNullOrWhiteSpace($syncfusionLicense)) {
    $syncfusionLicense = [string]$serviceEnvironment['SyncfusionLicenseKey']
}
Assert-True (-not [string]::IsNullOrWhiteSpace($syncfusionLicense)) `
    'The protected Syncfusion license is absent from the API service configuration.'

$hostPublicKeyPath = @(
    'C:\ProgramData\ssh\ssh_host_ed25519_key.pub',
    'C:\ProgramData\ssh\ssh_host_ecdsa_key.pub',
    'C:\ProgramData\ssh\ssh_host_rsa_key.pub'
) | Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } | Select-Object -First 1
Assert-True (-not [string]::IsNullOrWhiteSpace($hostPublicKeyPath)) `
    'No OpenSSH server public host key was found under C:\ProgramData\ssh.'
$publicKeyParts = @((Get-Content -LiteralPath $hostPublicKeyPath -Raw).Trim() -split '\s+')
Assert-True ($publicKeyParts.Count -ge 2 -and
    $publicKeyParts[0] -match '^(ssh-(ed25519|rsa)|ecdsa-sha2-nistp\d+)$' -and
    $publicKeyParts[1] -match '^[A-Za-z0-9+/=]+$') `
    'The OpenSSH server public host key has an unexpected format.'
$knownHosts = "[$VpsHost]:$SshPort $($publicKeyParts[0]) $($publicKeyParts[1])"

$privateKey = Get-Content -LiteralPath $SshPrivateKeyPath -Raw
Assert-True ($privateKey -match '-----BEGIN (OPENSSH|RSA|EC) PRIVATE KEY-----') `
    'The deployment SSH key does not contain a supported private-key header.'
& ssh-keygen.exe -y -P '' -f $SshPrivateKeyPath | Out-Null
Assert-True ($LASTEXITCODE -eq 0) 'The deployment SSH private key is invalid or encrypted.'

try {
    Set-GitHubSecretFromMemory -Name 'SYNCFUSION_LICENSE' -Value $syncfusionLicense
    Set-GitHubSecretFromMemory -Name 'VPS_SSH_PRIVATE_KEY' -Value $privateKey
    Set-GitHubSecretFromMemory -Name 'VPS_SSH_KNOWN_HOSTS' -Value $knownHosts
}
finally {
    $syncfusionLicense = $null
    $privateKey = $null
    $knownHosts = $null
    $serviceEnvironment.Clear()
}

$repositoryVariables = [ordered]@{
    VPS_HOST = $VpsHost
    VPS_SSH_PORT = [string]$SshPort
    VPS_SSH_USER = $SshUser
    VPS_PUBLIC_BASE_URL = $PublicBaseUrl.GetLeftPart([UriPartial]::Authority)
}
foreach ($variable in $repositoryVariables.GetEnumerator()) {
    & gh.exe variable set $variable.Key --body ([string]$variable.Value) --repo $Repository
    Assert-True ($LASTEXITCODE -eq 0) "Could not configure repository variable $($variable.Key)."
}

& gh.exe api --method PUT "repos/$Repository/environments/test-vps" | Out-Null
Assert-True ($LASTEXITCODE -eq 0) 'Could not create or update the test-vps GitHub environment.'
& gh.exe workflow enable ci-cd.yml --repo $Repository
Assert-True ($LASTEXITCODE -eq 0) 'Could not enable the Windows VPS CI/CD workflow.'

$secretNames = @(& gh.exe api "repos/$Repository/actions/secrets" --jq '.secrets[].name')
$variableNames = @(& gh.exe api "repos/$Repository/actions/variables" --jq '.variables[].name')
foreach ($requiredSecret in @('SYNCFUSION_LICENSE', 'VPS_SSH_PRIVATE_KEY',
        'VPS_SSH_KNOWN_HOSTS')) {
    Assert-True ($requiredSecret -in $secretNames) `
        "GitHub did not confirm repository secret $requiredSecret."
}
foreach ($requiredVariable in @('VPS_HOST', 'VPS_SSH_PORT', 'VPS_SSH_USER',
        'VPS_PUBLIC_BASE_URL')) {
    Assert-True ($requiredVariable -in $variableNames) `
        "GitHub did not confirm repository variable $requiredVariable."
}

Write-Output 'GITHUB_VPS_CICD|CONFIGURED'
Write-Output 'No application release was built or deployed.'
