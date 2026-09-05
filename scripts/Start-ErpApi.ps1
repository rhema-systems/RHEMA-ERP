<#
.SYNOPSIS
    Starts the API against either the development or the UAT / demo database.

.DESCRIPTION
    Switching database is a server-side concern only: the frontend and every dev-harness script
    talk to the API over HTTP, so whichever database this launcher selects is the one they all see.
    Nothing in appsettings.json is edited -- the connection string is overridden by environment
    variable, which standard .NET configuration precedence puts above the file. That matters
    because appsettings.json is shared with the rest of the team and a local edit to it is one
    `git add .` away from switching everyone's database.

.PARAMETER Database
    Dev  -> ErpSystemDB       (the shared development database)
    Uat  -> ErpSystemDB_UAT   (built by ./scripts/New-UatDatabase.ps1)
    Or pass any database name directly.

.PARAMETER Environment
    Staging (default) or Development.

    !! Use Staging when anything will read a status code -- the dev-harness suites, or a demo where
    a refusal should render as a refusal. In Development, UseDeveloperExceptionPage is registered
    AFTER GlobalExceptionHandlingMiddleware, so it sits closer to the endpoint, catches first, and
    turns every handled refusal into a 500 with a stack trace.

.PARAMETER Port
    Defaults to 5000. Pass a different port to run both databases side by side.

.EXAMPLE
    powershell -File ./scripts/Start-ErpApi.ps1 -Database Uat
    powershell -File ./scripts/Start-ErpApi.ps1 -Database Dev -Environment Development
    powershell -File ./scripts/Start-ErpApi.ps1 -Database Uat -Port 5010     # alongside dev on 5000
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$Database,

    [ValidateSet('Staging', 'Development')]
    [string]$Environment = 'Staging',

    [int]$Port = 5000,
    [string]$Server = '.',
    [string]$UserId = 'sa',
    [string]$Password
)

$ErrorActionPreference = 'Stop'

$name = switch ($Database) {
    'Dev' { 'ErpSystemDB' }
    'Uat' { 'ErpSystemDB_UAT' }
    default { $Database }
}

$repoRoot = Split-Path -Parent $PSScriptRoot
$apiDir = Join-Path $repoRoot 'src\ErpSystem.Api'
$dll = Join-Path $apiDir 'bin\Debug\net8.0\ErpSystem.Api.dll'
if (-not (Test-Path $dll)) { throw "Build output not found at $dll. Build the solution first." }

# The credential is never written down here. It comes from -Password, then ERP_DB_PASSWORD, then
# the gitignored appsettings.json the API itself uses. See scripts/ErpDbCredential.ps1.
. (Join-Path $PSScriptRoot 'ErpDbCredential.ps1')
$credential = Resolve-ErpDbCredential -UserId $UserId -Password $Password -RepoRoot $repoRoot
$UserId = $credential.UserId
$Password = $credential.Password


# Refuse to start against a database that does not exist. Without this the API comes up, every
# request 500s, and the cause looks like a code fault rather than a typo in a database name.
$exists = & sqlcmd -S $Server -U $UserId -P $Password -C -I -b -h -1 -W -Q `
    "SET NOCOUNT ON; SELECT COUNT(*) FROM sys.databases WHERE name = '$name'"
if ($LASTEXITCODE -ne 0) { throw "Could not reach SQL Server at '$Server'." }
if ([int]($exists | Select-Object -First 1).Trim() -eq 0) {
    throw "Database '$name' does not exist. Build it with ./scripts/New-UatDatabase.ps1."
}

Push-Location $apiDir
try {
    # Staging has no user-secrets, so the JWT signing key has to be passed in or every login 400s
    # with 'IDX10703: key length is zero'.
    if ($Environment -eq 'Staging' -and -not $env:JwtSettings__SecretKey) {
        $line = (dotnet user-secrets list | Select-String '^JwtSettings:SecretKey = ')
        if (-not $line) { throw "JwtSettings:SecretKey not found in user-secrets; set JwtSettings__SecretKey yourself." }
        $env:JwtSettings__SecretKey = $line.ToString().Substring($line.ToString().IndexOf(' = ') + 3)
    }

    $env:ConnectionStrings__DefaultConnection =
        "Server=$Server;Database=$name;User Id=$UserId;Password=$Password;" +
        "TrustServerCertificate=True;Encrypt=False;MultipleActiveResultSets=True"
    $env:ASPNETCORE_ENVIRONMENT = $Environment
    $env:ASPNETCORE_URLS = "http://localhost:$Port"

    Write-Host ""
    Write-Host "  Database    : $name" -ForegroundColor Cyan
    Write-Host "  Environment : $Environment" -ForegroundColor Cyan
    Write-Host "  Listening   : http://localhost:$Port" -ForegroundColor Cyan
    if ($name -eq 'ErpSystemDB') {
        Write-Host "  !! This is the SHARED DEVELOPMENT database." -ForegroundColor Yellow
    } else {
        Write-Host "  Demo database. The dev-harness suites write fixtures -- do not run them here" -ForegroundColor DarkGray
        Write-Host "  before a demo, or the registers fill with robot names again." -ForegroundColor DarkGray
    }
    Write-Host ""

    & dotnet $dll
}
finally {
    Pop-Location
    $env:ConnectionStrings__DefaultConnection = $null
}
