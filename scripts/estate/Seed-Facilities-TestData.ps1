param([switch] $Apply)

$ErrorActionPreference = 'Stop'
if (-not $Apply) {
    throw 'This script writes Facilities test fixtures. Run with -Apply in the local test environment.'
}

$repo = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$configPath = Join-Path $repo 'src/ErpSystem.Api/appsettings.Development.json'
$sqlPath = Join-Path $PSScriptRoot 'facilities-test-data.sql'
$config = Get-Content -LiteralPath $configPath -Raw | ConvertFrom-Json
$builder = [System.Data.SqlClient.SqlConnectionStringBuilder]::new($config.ConnectionStrings.DefaultConnection)
if ($builder.InitialCatalog -ne 'RhemaERP') {
    throw "Refusing to seed unexpected database '$($builder.InitialCatalog)'."
}

$connection = [System.Data.SqlClient.SqlConnection]::new($builder.ConnectionString)
try {
    $connection.Open()
    $command = $connection.CreateCommand()
    $command.CommandText = Get-Content -LiteralPath $sqlPath -Raw
    $command.CommandTimeout = 120
    $reader = $command.ExecuteReader()
    try {
        while ($reader.Read()) {
            [pscustomobject]@{
                Fixture = $reader['Fixture']
                Count = $reader['RecordCount']
            }
        }
    }
    finally {
        $reader.Close()
    }
}
finally {
    $connection.Close()
}
