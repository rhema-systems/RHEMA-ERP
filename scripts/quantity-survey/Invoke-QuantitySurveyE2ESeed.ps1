[CmdletBinding()]
param(
    [string]$ApiProject = "src/ErpSystem.Api/ErpSystem.Api.csproj"
)

$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$projectPath = (Resolve-Path (Join-Path $repoRoot $ApiProject)).Path
$sqlPath = (Resolve-Path (Join-Path $PSScriptRoot "seed-quantity-survey-e2e.sql")).Path

$secretLine = dotnet user-secrets list --project $projectPath |
    Where-Object { $_ -like "ConnectionStrings:DefaultConnection*" } |
    Select-Object -First 1
if (-not $secretLine) { throw "ConnectionStrings:DefaultConnection is not configured in API user-secrets." }

$connectionString = ($secretLine -split "=", 2)[1].Trim()
$parts = @{}
foreach ($part in ($connectionString -split ";")) {
    if ($part -notmatch "=") { continue }
    $pair = $part -split "=", 2
    $parts[$pair[0].Trim().ToLowerInvariant()] = $pair[1].Trim()
}

$server = @($parts["server"], $parts["data source"]) | Where-Object { $_ } | Select-Object -First 1
$database = @($parts["database"], $parts["initial catalog"]) | Where-Object { $_ } | Select-Object -First 1
$user = @($parts["user id"], $parts["uid"]) | Where-Object { $_ } | Select-Object -First 1
$password = @($parts["password"], $parts["pwd"]) | Where-Object { $_ } | Select-Object -First 1
if (-not $server -or -not $database) { throw "The configured DefaultConnection does not contain a server and database." }

$machine = $env:COMPUTERNAME
$localServer = $server -in @(".", "localhost", "(local)") -or
    $server.StartsWith(".\", [System.StringComparison]::OrdinalIgnoreCase) -or
    $server.StartsWith("localhost\", [System.StringComparison]::OrdinalIgnoreCase) -or
    $server.StartsWith("(local)\", [System.StringComparison]::OrdinalIgnoreCase) -or
    $server.StartsWith("$machine\", [System.StringComparison]::OrdinalIgnoreCase) -or
    $server.Equals($machine, [System.StringComparison]::OrdinalIgnoreCase)
if (-not $localServer) {
    throw "QS E2E fixture refused: the configured SQL server is not local to this development machine."
}

$arguments = @("-S", $server, "-d", $database, "-C", "-b", "-V", "16", "-i", $sqlPath)
try {
    if ($user) {
        $env:SQLCMDPASSWORD = $password
        $arguments += @("-U", $user)
    } else {
        $arguments += "-E"
    }
    Write-Host "Applying the governed QS E2E fixture to local database '$database' on '$server'..."
    & sqlcmd @arguments
    if ($LASTEXITCODE -ne 0) { throw "sqlcmd failed with exit code $LASTEXITCODE." }
} finally {
    Remove-Item Env:SQLCMDPASSWORD -ErrorAction SilentlyContinue
}
