param([switch] $Apply)

$ErrorActionPreference = 'Stop'
if (-not $Apply) {
    throw 'This repairs only the local RhemaERP test database. Run with -Apply.'
}

$repo = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$config = Get-Content -LiteralPath (Join-Path $repo 'src/ErpSystem.Api/appsettings.Development.json') -Raw | ConvertFrom-Json
$builder = [System.Data.SqlClient.SqlConnectionStringBuilder]::new($config.ConnectionStrings.DefaultConnection)
if ($builder.InitialCatalog -ne 'RhemaERP') {
    throw "Refusing to repair unexpected database '$($builder.InitialCatalog)'."
}

$connection = [System.Data.SqlClient.SqlConnection]::new($builder.ConnectionString)
try {
    $connection.Open()
    $command = $connection.CreateCommand()
    $command.CommandText = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'repair-local-facilities-budget-schema.sql') -Raw
    $command.CommandTimeout = 120
    $reader = $command.ExecuteReader()
    try {
        while ($reader.Read()) {
            [pscustomobject]@{
                Table = $reader['TableName']
                RowsWithBook = $reader['RowsWithBook']
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
