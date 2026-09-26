<#
.SYNOPSIS
    Answers one question that nothing else can: is the API on a given port serving a given database?

.DESCRIPTION
    Dot-source this and call Test-ErpApiServesDatabase. It reads one employee id straight from the
    named database and asks the running API for that employee. Employee ids are minted on every
    rebuild, so the id exists in exactly one database: a 200 means the API is on that database,
    anything else means it is not.

    !! WHY THIS EXISTS
    On 2026-09-07 the demo rebuild adopted an API that was already listening on port 5000 because it
    answered /health. That API was on the DEVELOPMENT database. Every SQL read in the scenarios went
    to UAT and every API write went to dev: 28 of 34 scenario modules reported "ok", UAT stayed empty,
    and 5,579 demo rows landed in the development database. A health answer says the API is up. It
    says nothing about which database is behind it. This does.

    Used by Invoke-UatDemoScenarios.ps1 before it will touch an API it did not start, and by
    Test-ErpApiDatabase.ps1, which is the check Book 0 tells the presenter to run.
#>

function Test-ErpApiServesDatabase {
    [CmdletBinding()]
    param(
        [int]$Port = 5000,
        [Parameter(Mandatory = $true)][string]$Database,
        [string]$Server = '.',
        [string]$UserId = 'sa',
        [Parameter(Mandatory = $true)][string]$Password,
        [string]$AdminUser = 'admin',
        [string]$AdminPassword = 'Admin123!'
    )

    $result = [pscustomobject]@{ Serves = $false; Reason = ''; EmployeeId = $null }

    # 1. A fingerprint row from the database itself. The oldest live employee is stable across the
    #    life of one database and different in every other one.
    $raw = & sqlcmd -S $Server -d $Database -U $UserId -P $Password -C -I -b -h -1 -W -Q `
        "SET NOCOUNT ON; SELECT TOP 1 CAST(Id AS varchar(36)) FROM Employees WHERE IsDeleted = 0 ORDER BY CreatedAt, Id" 2>$null
    if ($LASTEXITCODE -ne 0 -or -not $raw) {
        $result.Reason = "could not read an employee id from '$Database' on '$Server' (does the database exist?)"
        return $result
    }
    $id = ($raw | Select-Object -First 1).Trim()
    $result.EmployeeId = $id

    # 2. Sign in to the API on that port.
    try {
        $login = Invoke-RestMethod -Method Post -Uri "http://localhost:$Port/api/auth/login" `
            -ContentType 'application/json' -TimeoutSec 20 `
            -Body (@{ username = $AdminUser; password = $AdminPassword } | ConvertTo-Json -Compress)
    }
    catch {
        $result.Reason = "no API answered a login on port $Port ($($_.Exception.Message))"
        return $result
    }
    $token = $login.token
    if (-not $token) { $token = $login.accessToken }
    if (-not $token -and $login.data) { $token = $login.data.token }
    if (-not $token) { $result.Reason = "the API on port $Port refused the $AdminUser login"; return $result }

    # 3. Does that API know the fingerprint employee?
    try {
        $null = Invoke-WebRequest -Uri "http://localhost:$Port/api/hr/Employees/$id" -UseBasicParsing -TimeoutSec 60 `
            -Headers @{ Authorization = "Bearer $token" }
        $result.Serves = $true
        $result.Reason = "employee $id from '$Database' is known to the API on port $Port"
    }
    catch {
        $code = 0
        if ($_.Exception.PSObject.Properties['Response'] -and $_.Exception.Response) { $code = [int]$_.Exception.Response.StatusCode }
        $result.Reason = "GET /api/hr/Employees/$id answered $code; that employee exists in '$Database', so the API on port $Port is serving a DIFFERENT database"
    }
    return $result
}
