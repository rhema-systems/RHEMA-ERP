[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"
$sqlPath = (Resolve-Path (Join-Path $PSScriptRoot "verify-quantity-survey-e2e.sql")).Path
$connectionString = [Environment]::GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
if ([string]::IsNullOrWhiteSpace($connectionString)) {
    throw "ConnectionStrings__DefaultConnection must explicitly target the disposable QS acceptance database."
}

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
if (-not $server -or -not $database) {
    throw "The configured DefaultConnection does not contain a server and database."
}
if ($database -notmatch '^RhemaQsUatAssurance_[0-9]{8}$') {
    throw "QS E2E verification refused: the database name must match RhemaQsUatAssurance_YYYYMMDD."
}

$machine = $env:COMPUTERNAME
$localServer = $server -in @(".", "localhost", "(local)") -or
    $server.StartsWith(".\", [System.StringComparison]::OrdinalIgnoreCase) -or
    $server.StartsWith("localhost\", [System.StringComparison]::OrdinalIgnoreCase) -or
    $server.StartsWith("(local)\", [System.StringComparison]::OrdinalIgnoreCase) -or
    $server.StartsWith("$machine\", [System.StringComparison]::OrdinalIgnoreCase) -or
    $server.Equals($machine, [System.StringComparison]::OrdinalIgnoreCase)
if (-not $localServer) {
    throw "QS E2E verification refused: the configured SQL server is not local to this development machine."
}

$arguments = @(
    "-S", $server,
    "-d", $database,
    "-C",
    "-b",
    "-V", "16",
    "-W",
    "-s", "|",
    "-w", "65535",
    "-i", $sqlPath
)
try {
    if ($user) {
        $env:SQLCMDPASSWORD = $password
        $arguments += @("-U", $user)
    } else {
        $arguments += "-E"
    }

    Write-Host "Verifying the governed QS E2E postconditions in disposable database '$database'..."
    & sqlcmd @arguments
    if ($LASTEXITCODE -ne 0) {
        throw "QS E2E SQL verification failed with exit code $LASTEXITCODE."
    }
} finally {
    Remove-Item Env:SQLCMDPASSWORD -ErrorAction SilentlyContinue
}
