[CmdletBinding()]
param(
    [string]$ApiProject = "src/ErpSystem.Api/ErpSystem.Api.csproj",
    [string]$OutputJsonPath
)

$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$projectPath = (Resolve-Path (Join-Path $repoRoot $ApiProject)).Path
$sqlPath = (Resolve-Path (Join-Path $PSScriptRoot "seed-quantity-survey-e2e.sql")).Path

$connectionString = [Environment]::GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
if ([string]::IsNullOrWhiteSpace($connectionString)) {
    $secretLine = dotnet user-secrets list --project $projectPath |
        Where-Object { $_ -like "ConnectionStrings:DefaultConnection*" } |
        Select-Object -First 1
    if (-not $secretLine) {
        throw "ConnectionStrings:DefaultConnection is not configured in the environment or API user-secrets."
    }

    $connectionString = ($secretLine -split "=", 2)[1].Trim()
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
    Write-Host "Applying the governed QS E2E fixture to local database '$database' on '$server'..."
    $sqlOutput = @(& sqlcmd @arguments 2>&1)
    $sqlExitCode = $LASTEXITCODE
    $sqlOutput | ForEach-Object { Write-Host $_ }
    if ($sqlExitCode -ne 0) { throw "sqlcmd failed with exit code $sqlExitCode." }

    if (-not [string]::IsNullOrWhiteSpace($OutputJsonPath)) {
        $columns = @(
            "Result",
            "TenantId",
            "ProjectId",
            "ProjectCode",
            "ContractId",
            "ContractNumber",
            "ApprovedBoqVersionId",
            "InterimValuationId",
            "ContractorBusinessPartnerId",
            "ConsultantBusinessPartnerId",
            "MakerUserId",
            "ReviewerUserId",
            "WorkflowReviewerUserId",
            "EngineerUserId",
            "FinanceValidatorUserId",
            "IndependentApproverUserId",
            "ContractorPortalUserId",
            "ConsultantPortalUserId",
            "FinanceBankAccountId",
            "CashPaymentMethodId",
            "CrossTenantProjectId",
            "UnassignedSameTenantProjectId",
            "ConfigurationProfileId",
            "ApprovedDecisions",
            "ApprovedBoqLines"
        )
        $readyLine = $sqlOutput |
            ForEach-Object { $_.ToString().Trim() } |
            Where-Object { $_ -like "QS-E2E-READY|*" } |
            Select-Object -Last 1
        if (-not $readyLine) {
            throw "The QS E2E fixture did not emit its structured QS-E2E-READY row."
        }

        $values = @($readyLine -split "\|")
        if ($values.Count -ne $columns.Count) {
            throw "The QS E2E fixture emitted $($values.Count) values; $($columns.Count) were expected."
        }

        $fixture = [ordered]@{}
        for ($index = 0; $index -lt $columns.Count; $index++) {
            $fixture[$columns[$index]] = $values[$index].Trim()
        }

        $fullOutputPath = [System.IO.Path]::GetFullPath($OutputJsonPath)
        $outputDirectory = [System.IO.Path]::GetDirectoryName($fullOutputPath)
        if (-not [string]::IsNullOrWhiteSpace($outputDirectory)) {
            [System.IO.Directory]::CreateDirectory($outputDirectory) | Out-Null
        }
        [System.IO.File]::WriteAllText(
            $fullOutputPath,
            ($fixture | ConvertTo-Json -Depth 3),
            [System.Text.UTF8Encoding]::new($false))
    }
} finally {
    Remove-Item Env:SQLCMDPASSWORD -ErrorAction SilentlyContinue
}
