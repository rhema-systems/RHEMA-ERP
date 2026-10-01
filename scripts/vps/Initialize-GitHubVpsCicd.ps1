[CmdletBinding()]
param(
    [ValidatePattern('^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$')]
    [string]$Repository = 'rhema-systems/RHEMA-ERP',
    [string]$VpsHost = '63.141.230.56',
    [ValidateRange(1, 65535)]
    [int]$SshPort = 2222,
    [ValidatePattern('^[A-Za-z0-9_.-]+$')]
    [string]$SshUser = 'Administrator',
    [uri]$PublicBaseUrl = 'https://63.141.230.56',
    [string]$SshPrivateKeyPath =
        'C:\RhemaERP\secrets\github-actions-vps-deployment',
    [string]$ApiServiceXmlPath = 'C:\RhemaERP\services\api\RhemaERPAPI.xml',
    [ValidatePattern('^\d+\.\d+\.\d+$')]
    [string]$GitHubCliVersion = '2.102.0',
    [ValidatePattern('^[A-Fa-f0-9]{64}$')]
    [string]$GitHubCliSha256 =
        'AE64E556ECC240B200F7EBA60D550E4BB60D78E860E69DD88C449405B86067F4',
    [string]$GitHubCliDirectory = 'C:\RhemaERP\tools\github-cli'
)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

function Assert-True {
    param([bool]$Condition, [string]$Message)
    if (-not $Condition) { throw $Message }
}

function Get-GitHubCliPath {
    $installed = Get-Command gh.exe -ErrorAction SilentlyContinue
    if ($null -ne $installed) {
        return $installed.Source
    }

    $portablePath = Join-Path $GitHubCliDirectory 'gh.exe'
    if (Test-Path -LiteralPath $portablePath -PathType Leaf) {
        return $portablePath
    }

    Write-Output "GitHub CLI is unavailable; installing verified portable v$GitHubCliVersion."
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    $downloadUri =
        "https://github.com/cli/cli/releases/download/v$GitHubCliVersion/gh_${GitHubCliVersion}_windows_amd64.zip"
    $temporaryRoot = Join-Path ([IO.Path]::GetTempPath()) `
        "rhema-gh-$([Guid]::NewGuid().ToString('N'))"
    $archivePath = Join-Path $temporaryRoot 'github-cli.zip'
    $extractPath = Join-Path $temporaryRoot 'extract'
    try {
        [void][IO.Directory]::CreateDirectory($temporaryRoot)
        Invoke-WebRequest -UseBasicParsing -Uri $downloadUri -OutFile $archivePath
        $actualHash = (Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash
        Assert-True ($actualHash -ceq $GitHubCliSha256.ToUpperInvariant()) `
            'The downloaded GitHub CLI archive failed its pinned SHA-256 check.'

        Expand-Archive -LiteralPath $archivePath -DestinationPath $extractPath
        $executables = @(Get-ChildItem -LiteralPath $extractPath -Filter gh.exe `
                -File -Recurse)
        Assert-True ($executables.Count -eq 1) `
            "Expected one gh.exe in the verified archive; found $($executables.Count)."
        $signature = Get-AuthenticodeSignature -FilePath $executables[0].FullName
        Assert-True ($signature.Status -eq [System.Management.Automation.SignatureStatus]::Valid -and
            $signature.SignerCertificate.Subject -match 'O="?GitHub, Inc\."?') `
            'The downloaded GitHub CLI executable does not have a valid GitHub signature.'

        [void][IO.Directory]::CreateDirectory($GitHubCliDirectory)
        Copy-Item -LiteralPath $executables[0].FullName -Destination $portablePath -Force
    }
    finally {
        if (Test-Path -LiteralPath $temporaryRoot) {
            Remove-Item -LiteralPath $temporaryRoot -Recurse -Force
        }
    }

    Assert-True (Test-Path -LiteralPath $portablePath -PathType Leaf) `
        'The portable GitHub CLI installation did not produce gh.exe.'
    return $portablePath
}

function Initialize-WindowsOpenSshServer {
    $principal = [Security.Principal.WindowsPrincipal]::new(
        [Security.Principal.WindowsIdentity]::GetCurrent())
    Assert-True ($principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) `
        'Run this initializer from an elevated Windows PowerShell session.'

    $sshdService = Get-Service -Name sshd -ErrorAction SilentlyContinue
    if ($null -eq $sshdService) {
        Write-Output 'Windows OpenSSH Server is unavailable; installing the Windows capability.'
        $capability = Get-WindowsCapability -Online |
            Where-Object Name -Like 'OpenSSH.Server*' |
            Select-Object -First 1
        Assert-True ($null -ne $capability) `
            'Windows OpenSSH Server capability is unavailable on this VPS.'
        if ($capability.State -ne 'Installed') {
            $capability = Add-WindowsCapability -Online -Name $capability.Name
        }
        Assert-True ($capability.State -eq 'Installed') `
            'Windows could not install the OpenSSH Server capability.'
        $sshdService = Get-Service -Name sshd -ErrorAction SilentlyContinue
    }
    Assert-True ($null -ne $sshdService) `
        'The Windows OpenSSH Server service was not registered after installation.'

    $sshdExecutable = @(
        (Get-Command sshd.exe -ErrorAction SilentlyContinue).Source,
        (Join-Path $env:WINDIR 'System32\OpenSSH\sshd.exe')
    ) | Where-Object {
        -not [string]::IsNullOrWhiteSpace($_) -and
        (Test-Path -LiteralPath $_ -PathType Leaf)
    } | Select-Object -First 1
    Assert-True (-not [string]::IsNullOrWhiteSpace($sshdExecutable)) `
        'sshd.exe was not found after installing Windows OpenSSH Server.'

    $script:SshdConfigPath = 'C:\ProgramData\ssh\sshd_config'
    $serviceDetails = Get-CimInstance Win32_Service -Filter "Name='sshd'" `
        -ErrorAction SilentlyContinue
    if ($null -ne $serviceDetails -and
        $serviceDetails.PathName -match '(?i)(?:^|\s)-f\s+(?:"([^"]+)"|(\S+))') {
        $customConfigPath = if ($matches[1]) { $matches[1] } else { $matches[2] }
        if (-not [string]::IsNullOrWhiteSpace($customConfigPath)) {
            $script:SshdConfigPath = [Environment]::ExpandEnvironmentVariables(
                $customConfigPath)
        }
    }

    [void][IO.Directory]::CreateDirectory((Split-Path -Parent $script:SshdConfigPath))
    if (Test-Path -LiteralPath $script:SshdConfigPath -PathType Leaf) {
        $configuration = Get-Content -LiteralPath $script:SshdConfigPath -Raw
    }
    else {
        $defaultConfigPath = Join-Path $env:WINDIR `
            'System32\OpenSSH\sshd_config_default'
        $configuration = if (Test-Path -LiteralPath $defaultConfigPath -PathType Leaf) {
            Get-Content -LiteralPath $defaultConfigPath -Raw
        }
        else {
            "Subsystem sftp sftp-server.exe`r`n"
        }
    }

    $blockStart = '# BEGIN RHEMA ERP GITHUB ACTIONS SSH'
    $blockEnd = '# END RHEMA ERP GITHUB ACTIONS SSH'
    $managedBlock = @"
$blockStart
Port $SshPort
PubkeyAuthentication yes
Match User $SshUser
    AuthorizedKeysFile __PROGRAMDATA__/ssh/administrators_authorized_keys
    PubkeyAuthentication yes
    PasswordAuthentication no
    KbdInteractiveAuthentication no
    AuthenticationMethods publickey
Match all
$blockEnd
"@
    $managedPattern = '(?ms)^' + [regex]::Escape($blockStart) +
        '.*?^' + [regex]::Escape($blockEnd) + '\s*'
    if ($configuration -match $managedPattern) {
        $configuration = [regex]::Replace(
            $configuration,
            $managedPattern,
            $managedBlock + [Environment]::NewLine,
            1)
    }
    else {
        $configuration = $managedBlock + [Environment]::NewLine + $configuration
    }
    [IO.File]::WriteAllText(
        $script:SshdConfigPath,
        $configuration,
        [Text.UTF8Encoding]::new($false))

    & $sshdExecutable -t -f $script:SshdConfigPath
    Assert-True ($LASTEXITCODE -eq 0) `
        "Windows OpenSSH rejected its managed configuration: $($script:SshdConfigPath)"
    & ssh-keygen.exe -A
    Assert-True ($LASTEXITCODE -eq 0) 'Windows OpenSSH host-key preparation failed.'

    Set-Service -Name sshd -StartupType Automatic
    $sshdService = Get-Service -Name sshd
    if ($sshdService.Status -eq [System.ServiceProcess.ServiceControllerStatus]::Running) {
        Restart-Service -Name sshd -Force
    }
    else {
        Start-Service -Name sshd
    }
    $sshdService = Get-Service -Name sshd
    $sshdService.WaitForStatus(
        [System.ServiceProcess.ServiceControllerStatus]::Running,
        [TimeSpan]::FromSeconds(30))

    $firewallDisplayName = "RhemaERP GitHub Actions SSH $SshPort"
    $firewallRule = Get-NetFirewallRule -DisplayName $firewallDisplayName `
        -ErrorAction SilentlyContinue
    if ($null -eq $firewallRule) {
        New-NetFirewallRule `
            -DisplayName $firewallDisplayName `
            -Direction Inbound `
            -Action Allow `
            -Protocol TCP `
            -LocalPort $SshPort `
            -Profile Any | Out-Null
    }
    else {
        $firewallRule | Set-NetFirewallRule -Enabled True -Action Allow | Out-Null
        $firewallRule | Get-NetFirewallPortFilter |
            Set-NetFirewallPortFilter -Protocol TCP -LocalPort $SshPort | Out-Null
    }

    Write-Output "Windows OpenSSH Server is ready on TCP $SshPort."
}

function Protect-OpenSshFile {
    param(
        [Parameter(Mandatory)]
        [string]$Path,
        [switch]$AdministratorsOnly
    )

    $grants = if ($AdministratorsOnly) {
        @('*S-1-5-18:F', '*S-1-5-32-544:F')
    }
    else {
        $currentUserSid = [Security.Principal.WindowsIdentity]::GetCurrent().User.Value
        @('*S-1-5-18:F', "*$currentUserSid`:F")
    }
    & icacls.exe $Path '/inheritance:r' '/grant:r' @grants | Out-Null
    Assert-True ($LASTEXITCODE -eq 0) "Could not protect OpenSSH file: $Path"
}

function New-DeploymentSshKey {
    param(
        [Parameter(Mandatory)]
        [string]$Path
    )

    Assert-True ($Path -notmatch '["\r\n]') `
        'SshPrivateKeyPath contains unsupported characters.'
    [void][IO.Directory]::CreateDirectory((Split-Path -Parent $Path))

    $startInfo = [Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = (Get-Command ssh-keygen.exe -ErrorAction Stop).Source
    $startInfo.UseShellExecute = $false
    $startInfo.CreateNoWindow = $true
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    $startInfo.Arguments = '-q -t ed25519 -N "" ' +
        '-C "rhema-erp-github-actions" -f "' + $Path + '"'
    $process = [Diagnostics.Process]::new()
    $process.StartInfo = $startInfo
    try {
        [void]$process.Start()
        $stdoutTask = $process.StandardOutput.ReadToEndAsync()
        $stderrTask = $process.StandardError.ReadToEndAsync()
        $process.WaitForExit()
        [void]$stdoutTask.GetAwaiter().GetResult()
        $stderr = $stderrTask.GetAwaiter().GetResult()
        Assert-True ($process.ExitCode -eq 0) `
            "Could not create the GitHub Actions deployment key. $($stderr.Trim())"
    }
    finally {
        $process.Dispose()
    }
}

function Read-SshPublicKey {
    param(
        [Parameter(Mandatory)]
        [string]$Path
    )

    Assert-True ($Path -notmatch '["\r\n]') `
        'SshPrivateKeyPath contains unsupported characters.'
    $startInfo = [Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = (Get-Command ssh-keygen.exe -ErrorAction Stop).Source
    $startInfo.UseShellExecute = $false
    $startInfo.CreateNoWindow = $true
    $startInfo.RedirectStandardInput = $true
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    $startInfo.Arguments = '-y -f "' + $Path + '"'
    $process = [Diagnostics.Process]::new()
    $process.StartInfo = $startInfo
    try {
        [void]$process.Start()
        $process.StandardInput.Close()
        $stdoutTask = $process.StandardOutput.ReadToEndAsync()
        $stderrTask = $process.StandardError.ReadToEndAsync()
        $process.WaitForExit()
        $stdout = $stdoutTask.GetAwaiter().GetResult().Trim()
        $stderr = $stderrTask.GetAwaiter().GetResult().Trim()
        return [pscustomobject]@{
            ExitCode = $process.ExitCode
            PublicKey = $stdout
            Error = $stderr
        }
    }
    finally {
        $process.Dispose()
    }
}

function Repair-DeploymentSshKey {
    $replacementPath = "$SshPrivateKeyPath.replacement-$([Guid]::NewGuid().ToString('N'))"
    try {
        New-DeploymentSshKey -Path $replacementPath
        $replacement = Read-SshPublicKey -Path $replacementPath
        Assert-True ($replacement.ExitCode -eq 0 -and
            $replacement.PublicKey -match '^(ssh-ed25519|ssh-rsa|ecdsa-sha2-nistp\d+)\s+[A-Za-z0-9+/=]+') `
            "The replacement deployment key failed validation. $($replacement.Error)"
        Copy-Item -LiteralPath $replacementPath -Destination $SshPrivateKeyPath -Force
        Copy-Item -LiteralPath "$replacementPath.pub" `
            -Destination "$SshPrivateKeyPath.pub" -Force
    }
    finally {
        foreach ($temporaryPath in @($replacementPath, "$replacementPath.pub")) {
            if (Test-Path -LiteralPath $temporaryPath -PathType Leaf) {
                Remove-Item -LiteralPath $temporaryPath -Force
            }
        }
    }
}

function Initialize-DeploymentSshIdentity {
    Assert-True ($SshUser -ieq $env:USERNAME) `
        "Run this initializer as the target SSH user '$SshUser'."
    if (-not (Test-Path -LiteralPath $SshPrivateKeyPath -PathType Leaf)) {
        Write-Output 'Creating a dedicated GitHub Actions deployment identity.'
        New-DeploymentSshKey -Path $SshPrivateKeyPath
    }

    Assert-True (Test-Path -LiteralPath $SshPrivateKeyPath -PathType Leaf) `
        "Deployment SSH private key could not be created: $SshPrivateKeyPath"
    Protect-OpenSshFile -Path $SshPrivateKeyPath
    $publicKeyPath = "$SshPrivateKeyPath.pub"
    if (Test-Path -LiteralPath $publicKeyPath -PathType Leaf) {
        Protect-OpenSshFile -Path $publicKeyPath -AdministratorsOnly
    }

    $keyRead = Read-SshPublicKey -Path $SshPrivateKeyPath
    if ($keyRead.ExitCode -ne 0 -or
        $keyRead.PublicKey -notmatch '^(ssh-ed25519|ssh-rsa|ecdsa-sha2-nistp\d+)\s+[A-Za-z0-9+/=]+') {
        Write-Warning 'The dedicated deployment key is unusable; replacing it with a verified unencrypted key.'
        Repair-DeploymentSshKey
        Protect-OpenSshFile -Path $SshPrivateKeyPath
        if (Test-Path -LiteralPath $publicKeyPath -PathType Leaf) {
            Protect-OpenSshFile -Path $publicKeyPath -AdministratorsOnly
        }
        $keyRead = Read-SshPublicKey -Path $SshPrivateKeyPath
    }
    Assert-True ($keyRead.ExitCode -eq 0 -and
        $keyRead.PublicKey -match '^(ssh-ed25519|ssh-rsa|ecdsa-sha2-nistp\d+)\s+[A-Za-z0-9+/=]+') `
        "The deployment SSH private key is invalid or encrypted. $($keyRead.Error)"
    $publicKey = $keyRead.PublicKey

    $sshdConfigPath = $script:SshdConfigPath
    Assert-True (Test-Path -LiteralPath $sshdConfigPath -PathType Leaf) `
        "OpenSSH server configuration was not found: $sshdConfigPath"
    $sshdConfig = Get-Content -LiteralPath $sshdConfigPath -Raw
    $usesAdministratorKeys = $sshdConfig -match
        '(?im)^\s*AuthorizedKeysFile\s+(?:__PROGRAMDATA__|%PROGRAMDATA%)/ssh/administrators_authorized_keys\s*$'
    $authorizedKeysPath = if ($usesAdministratorKeys) {
        'C:\ProgramData\ssh\administrators_authorized_keys'
    }
    else {
        Join-Path $env:USERPROFILE '.ssh\authorized_keys'
    }
    [void][IO.Directory]::CreateDirectory((Split-Path -Parent $authorizedKeysPath))
    $existingLines = if (Test-Path -LiteralPath $authorizedKeysPath -PathType Leaf) {
        @(Get-Content -LiteralPath $authorizedKeysPath)
    }
    else {
        @()
    }
    $publicKeyParts = @($publicKey -split '\s+')
    $keyIdentity = "$($publicKeyParts[0]) $($publicKeyParts[1])"
    $alreadyAuthorized = $existingLines | Where-Object {
        ([string]$_).Trim().StartsWith("$keyIdentity ") -or
        ([string]$_).Trim() -ceq $keyIdentity
    }
    if (-not $alreadyAuthorized) {
        [string[]]$updatedLines = @($existingLines) +
            @("$keyIdentity rhema-erp-github-actions")
        [IO.File]::WriteAllLines(
            $authorizedKeysPath,
            $updatedLines,
            [Text.UTF8Encoding]::new($false))
    }
    Protect-OpenSshFile -Path $authorizedKeysPath `
        -AdministratorsOnly:$usesAdministratorKeys

    Write-Output "GitHub Actions deployment identity is authorized for $SshUser."
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
    $startInfo.FileName = $script:GitHubCliPath
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
$script:GitHubCliPath = Get-GitHubCliPath
Initialize-WindowsOpenSshServer
Assert-True ($null -ne (Get-Command ssh-keygen.exe -ErrorAction SilentlyContinue)) `
    'Required command is unavailable after OpenSSH installation: ssh-keygen.exe'
Assert-True ($null -ne (Get-Command ssh.exe -ErrorAction SilentlyContinue)) `
    'Required command is unavailable after OpenSSH installation: ssh.exe'
Initialize-DeploymentSshIdentity
$sshdService = Get-Service -Name sshd -ErrorAction SilentlyContinue
Assert-True ($null -ne $sshdService -and $sshdService.Status -eq 'Running') `
    'The Windows OpenSSH server service is not running.'

& $script:GitHubCliPath auth status --hostname github.com 2>$null | Out-Null
if ($LASTEXITCODE -ne 0) {
    Write-Warning 'GitHub CLI authentication is required once. Complete the browser/device sign-in.'
    & $script:GitHubCliPath auth login --hostname github.com --git-protocol https --web
}
& $script:GitHubCliPath auth status --hostname github.com | Out-Null
Assert-True ($LASTEXITCODE -eq 0) 'GitHub CLI is not authenticated.'
$repositoryAccess = & $script:GitHubCliPath repo view $Repository `
    --json nameWithOwner,viewerPermission | ConvertFrom-Json
Assert-True ($LASTEXITCODE -eq 0 -and
    $repositoryAccess.nameWithOwner -ceq $Repository) `
    "GitHub repository access could not be verified for $Repository."
Assert-True ($repositoryAccess.viewerPermission -ceq 'ADMIN') `
    'The authenticated GitHub account must be a repository administrator.'

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

$loopbackKnownHostsPath = Join-Path ([IO.Path]::GetTempPath()) `
    "rhema-ci-known-hosts-$([Guid]::NewGuid().ToString('N'))"
try {
    $loopbackKnownHosts =
        "[127.0.0.1]:$SshPort $($publicKeyParts[0]) $($publicKeyParts[1])"
    [IO.File]::WriteAllText(
        $loopbackKnownHostsPath,
        $loopbackKnownHosts + [Environment]::NewLine,
        [Text.UTF8Encoding]::new($false))
    $sshArguments = @(
        '-i', $SshPrivateKeyPath,
        '-p', [string]$SshPort,
        '-o', 'BatchMode=yes',
        '-o', 'IdentitiesOnly=yes',
        '-o', 'StrictHostKeyChecking=yes',
        '-o', "UserKnownHostsFile=$loopbackKnownHostsPath",
        '-o', 'ConnectTimeout=15',
        "$SshUser@127.0.0.1",
        'cmd.exe /d /c exit 0'
    )
    & ssh.exe @sshArguments
    Assert-True ($LASTEXITCODE -eq 0) `
        'The dedicated deployment key could not authenticate to the local OpenSSH service.'
}
finally {
    if (Test-Path -LiteralPath $loopbackKnownHostsPath) {
        Remove-Item -LiteralPath $loopbackKnownHostsPath -Force
    }
}

$privateKey = Get-Content -LiteralPath $SshPrivateKeyPath -Raw
Assert-True ($privateKey -match '-----BEGIN (OPENSSH|RSA|EC) PRIVATE KEY-----') `
    'The deployment SSH key does not contain a supported private-key header.'
$keyRead = Read-SshPublicKey -Path $SshPrivateKeyPath
Assert-True ($keyRead.ExitCode -eq 0) `
    "The deployment SSH private key became unreadable. $($keyRead.Error)"

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
    & $script:GitHubCliPath variable set $variable.Key `
        --body ([string]$variable.Value) --repo $Repository
    Assert-True ($LASTEXITCODE -eq 0) "Could not configure repository variable $($variable.Key)."
}

& $script:GitHubCliPath api --method PUT "repos/$Repository/environments/test-vps" | Out-Null
Assert-True ($LASTEXITCODE -eq 0) 'Could not create or update the test-vps GitHub environment.'
& $script:GitHubCliPath workflow enable ci-cd.yml --repo $Repository
Assert-True ($LASTEXITCODE -eq 0) 'Could not enable the Windows VPS CI/CD workflow.'

$secretNames = @(& $script:GitHubCliPath api "repos/$Repository/actions/secrets" `
        --jq '.secrets[].name')
$variableNames = @(& $script:GitHubCliPath api "repos/$Repository/actions/variables" `
        --jq '.variables[].name')
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

& $script:GitHubCliPath workflow run ci-cd.yml --repo $Repository --ref master `
    -f build_release=false -f deploy_to_test_vps=false
Assert-True ($LASTEXITCODE -eq 0) `
    'GitHub rejected the contract-only workflow validation. Confirm Actions are enabled for the authenticated account.'

Write-Output 'GITHUB_VPS_CICD|CONFIGURED'
Write-Output 'GITHUB_VPS_CICD|CONTRACT_VALIDATION_QUEUED'
Write-Output 'No application release was built or deployed.'
