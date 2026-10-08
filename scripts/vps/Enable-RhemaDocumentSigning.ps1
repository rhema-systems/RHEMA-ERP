[CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'High')]
param(
    [string]$ServiceName = 'RhemaERPAPI',
    [string]$ServiceConfigurationPath = 'C:\RhemaERP\services\api\RhemaERPAPI.xml',
    [string]$CertificateSubject = 'CN=RHEMA ERP Document Signing',
    [ValidateRange(1, 10)]
    [int]$ValidYears = 3,
    [string]$OrganizationName = 'RHEMA ERP',
    [string]$SigningLocation = 'ERP Document Management',
    [switch]$ForceNewCertificate,
    [switch]$NoRestart
)

$ErrorActionPreference = 'Stop'
$certificateStorePath = 'Cert:\LocalMachine\My'
$nssmParametersPath = "HKLM:\SYSTEM\CurrentControlSet\Services\$ServiceName\Parameters"
$backupRoot = 'C:\RhemaERP\backups\document-signing'

function Assert-Administrator {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = [Security.Principal.WindowsPrincipal]::new($identity)
    if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
        throw 'Run this script from an elevated PowerShell session on the Rhema ERP server.'
    }
}

function Get-ServiceIdentity {
    param([Parameter(Mandatory = $true)][string]$Name)

    $escapedName = $Name.Replace("'", "''")
    $service = Get-CimInstance Win32_Service -Filter "Name='$escapedName'"
    if ($null -eq $service) {
        throw "Windows service '$Name' was not found."
    }

    $identity = switch -Regex ([string]$service.StartName) {
        '^(LocalSystem|\.\\LocalSystem)$' { 'NT AUTHORITY\SYSTEM'; break }
        '^NT AUTHORITY\\LocalService$' { 'NT AUTHORITY\LOCAL SERVICE'; break }
        '^NT AUTHORITY\\NetworkService$' { 'NT AUTHORITY\NETWORK SERVICE'; break }
        default { [string]$service.StartName }
    }
    return $identity
}

function Get-PrivateKeyPath {
    param([Parameter(Mandatory = $true)][Security.Cryptography.X509Certificates.X509Certificate2]$Certificate)

    $rsa = [Security.Cryptography.X509Certificates.RSACertificateExtensions]::GetRSAPrivateKey($Certificate)
    if ($null -eq $rsa) {
        throw 'The document-signing certificate does not expose an RSA private key.'
    }

    try {
        if ($rsa -is [Security.Cryptography.RSACng]) {
            return Join-Path $env:ProgramData "Microsoft\Crypto\Keys\$($rsa.Key.UniqueName)"
        }
        if ($rsa -is [Security.Cryptography.RSACryptoServiceProvider]) {
            return Join-Path $env:ProgramData `
                "Microsoft\Crypto\RSA\MachineKeys\$($rsa.CspKeyContainerInfo.UniqueKeyContainerName)"
        }
    }
    finally {
        $rsa.Dispose()
    }

    throw 'The document-signing certificate uses an unsupported private-key provider.'
}

function Grant-PrivateKeyRead {
    param(
        [Parameter(Mandatory = $true)][Security.Cryptography.X509Certificates.X509Certificate2]$Certificate,
        [Parameter(Mandatory = $true)][string]$Identity
    )

    $keyPath = Get-PrivateKeyPath $Certificate
    if (-not (Test-Path -LiteralPath $keyPath)) {
        throw "The certificate private-key file was not found: $keyPath"
    }

    $output = & icacls.exe $keyPath /grant "${Identity}:(R)" 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "Could not grant '$Identity' access to the signing private key. $($output -join ' ')"
    }
}

function Set-XmlEnvironmentValues {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][hashtable]$Values
    )

    [xml]$xml = Get-Content -LiteralPath $Path -Raw
    if ($null -eq $xml.service) {
        throw "The service configuration is not a WinSW service XML file: $Path"
    }

    foreach ($entry in $Values.GetEnumerator()) {
        $node = @($xml.service.env | Where-Object { $_.name -eq $entry.Key })[0]
        if ($null -eq $node) {
            $node = $xml.CreateElement('env')
            $node.SetAttribute('name', [string]$entry.Key)
            [void]$xml.service.AppendChild($node)
        }
        $node.SetAttribute('value', [string]$entry.Value)
    }

    $settings = [Xml.XmlWriterSettings]::new()
    $settings.Indent = $true
    $settings.Encoding = [Text.UTF8Encoding]::new($false)
    $writer = [Xml.XmlWriter]::Create($Path, $settings)
    try {
        $xml.Save($writer)
    }
    finally {
        $writer.Close()
    }
}

function Set-NssmEnvironmentValues {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][hashtable]$Values
    )

    $properties = Get-ItemProperty -LiteralPath $Path
    $environment = [ordered]@{}
    foreach ($raw in @($properties.AppEnvironmentExtra)) {
        $entry = [string]$raw
        $separator = $entry.IndexOf('=')
        if ($separator -gt 0) {
            $environment[$entry.Substring(0, $separator)] = $entry.Substring($separator + 1)
        }
    }
    foreach ($entry in $Values.GetEnumerator()) {
        $environment[[string]$entry.Key] = [string]$entry.Value
    }

    $serialized = [string[]]@($environment.GetEnumerator() | Sort-Object Key | ForEach-Object {
        "{0}={1}" -f $_.Key, $_.Value
    })
    Set-ItemProperty -LiteralPath $Path -Name AppEnvironmentExtra -Value $serialized
}

Assert-Administrator
$serviceIdentity = Get-ServiceIdentity $ServiceName
$now = Get-Date
$certificate = Get-ChildItem $certificateStorePath |
    Where-Object {
        $_.Subject -eq $CertificateSubject -and
        $_.HasPrivateKey -and
        $_.NotAfter.ToUniversalTime() -gt $now.ToUniversalTime().AddDays(90)
    } |
    Sort-Object NotAfter -Descending |
    Select-Object -First 1

if ($ForceNewCertificate -or $null -eq $certificate) {
    if ($PSCmdlet.ShouldProcess($CertificateSubject, 'Create LocalMachine document-signing certificate')) {
        $certificate = New-SelfSignedCertificate `
            -Subject $CertificateSubject `
            -FriendlyName 'RHEMA ERP Organizational Document Signing' `
            -CertStoreLocation $certificateStorePath `
            -Provider 'Microsoft Software Key Storage Provider' `
            -KeyAlgorithm RSA `
            -KeyLength 3072 `
            -HashAlgorithm SHA256 `
            -KeyUsage DigitalSignature `
            -KeyExportPolicy NonExportable `
            -NotAfter $now.AddYears($ValidYears)
    }
}

if ($null -eq $certificate) {
    throw 'A signing certificate was not selected or created.'
}

if ($PSCmdlet.ShouldProcess($serviceIdentity, 'Grant read access to the document-signing private key')) {
    Grant-PrivateKeyRead -Certificate $certificate -Identity $serviceIdentity
}

$settings = @{
    'DocumentSigning__CertificateThumbprint' = $certificate.Thumbprint
    'DocumentSigning__StoreName' = 'My'
    'DocumentSigning__StoreLocation' = 'LocalMachine'
    'DocumentSigning__CreateDevelopmentCertificate' = 'false'
    'DocumentSigning__OrganizationName' = $OrganizationName
    'DocumentSigning__Location' = $SigningLocation
}

if ($PSCmdlet.ShouldProcess($ServiceName, 'Back up and update document-signing service settings')) {
    [IO.Directory]::CreateDirectory($backupRoot) | Out-Null
    $stamp = [DateTime]::UtcNow.ToString('yyyyMMddHHmmss')
    if (Test-Path -LiteralPath $ServiceConfigurationPath) {
        Copy-Item -LiteralPath $ServiceConfigurationPath `
            -Destination (Join-Path $backupRoot "$ServiceName-$stamp.xml")
        Set-XmlEnvironmentValues -Path $ServiceConfigurationPath -Values $settings
    }
    elseif (Test-Path -LiteralPath $nssmParametersPath) {
        $properties = Get-ItemProperty -LiteralPath $nssmParametersPath
        [pscustomobject]@{
            AppEnvironment = @($properties.AppEnvironment)
            AppEnvironmentExtra = @($properties.AppEnvironmentExtra)
        } | ConvertTo-Json -Depth 4 | Set-Content `
            -LiteralPath (Join-Path $backupRoot "$ServiceName-$stamp-nssm.json") `
            -Encoding UTF8
        Set-NssmEnvironmentValues -Path $nssmParametersPath -Values $settings
    }
    else {
        throw "The API service configuration was not found at '$ServiceConfigurationPath' or '$nssmParametersPath'."
    }
}

if (-not $NoRestart -and $PSCmdlet.ShouldProcess($ServiceName, 'Restart API service')) {
    Restart-Service -Name $ServiceName -Force
    $service = Get-Service -Name $ServiceName
    $service.WaitForStatus([ServiceProcess.ServiceControllerStatus]::Running, [TimeSpan]::FromSeconds(45))
}

Write-Output "DOCUMENT_SIGNING_CERTIFICATE|$($certificate.Thumbprint)"
Write-Output "DOCUMENT_SIGNING_STORE|LocalMachine/My"
Write-Output "DOCUMENT_SIGNING_SERVICE_IDENTITY|$serviceIdentity"
Write-Output "DOCUMENT_SIGNING_READY|$ServiceName"
