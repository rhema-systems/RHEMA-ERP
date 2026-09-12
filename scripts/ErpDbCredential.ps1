<#
.SYNOPSIS
    Resolves the SQL Server credential the operational scripts use, without any of them carrying it.

.DESCRIPTION
    Dot-source this from a script that needs to reach the database:

        . (Join-Path $PSScriptRoot 'ErpDbCredential.ps1')
        $cred = Resolve-ErpDbCredential -UserId $UserId -Password $Password -RepoRoot $repoRoot

    !! WHY THIS EXISTS
    The launcher scripts used to carry the sa password as a default parameter value, which put a
    live credential into every diff and into the history of any branch cut from this one. It had
    not reached the remote when that was found (2026-09-04) and it must not.

    Resolution order, first hit wins:
      1. -Password, when the caller passes one explicitly
      2. $env:ERP_DB_PASSWORD
      3. the DefaultConnection string in src/ErpSystem.Api/appsettings.json

    Option 3 is the reason nothing changes at the keyboard: that file is gitignored (.gitignore
    line 194), already holds the credential the API itself uses, and is the single place to change
    a rotated password. Nothing here writes it anywhere, and no caller should echo the result.

    If none of the three resolves, the caller is told to set ERP_DB_PASSWORD rather than being left
    with a connection failure that looks like a server problem.
#>

function Resolve-ErpDbCredential {
    [CmdletBinding()]
    param(
        [string]$UserId,
        [string]$Password,
        [Parameter(Mandatory = $true)][string]$RepoRoot
    )

    $resolvedUser = $UserId
    $resolvedPassword = $Password

    if (-not $resolvedPassword -and $env:ERP_DB_PASSWORD) {
        $resolvedPassword = $env:ERP_DB_PASSWORD
    }
    if (-not $resolvedUser -and $env:ERP_DB_USER) {
        $resolvedUser = $env:ERP_DB_USER
    }

    if (-not $resolvedPassword -or -not $resolvedUser) {
        $settings = Join-Path $RepoRoot 'src\ErpSystem.Api\appsettings.json'
        if (Test-Path $settings) {
            try {
                $connection = (Get-Content $settings -Raw | ConvertFrom-Json).ConnectionStrings.DefaultConnection
                if ($connection) {
                    if (-not $resolvedPassword -and $connection -match 'Password=([^;]+)') {
                        $resolvedPassword = $Matches[1]
                    }
                    if (-not $resolvedUser -and $connection -match 'User Id=([^;]+)') {
                        $resolvedUser = $Matches[1]
                    }
                }
            }
            catch {
                Write-Verbose "Could not read the connection string from $settings : $_"
            }
        }
    }

    if (-not $resolvedUser) { $resolvedUser = 'sa' }

    if (-not $resolvedPassword) {
        throw "No database password found. Set `$env:ERP_DB_PASSWORD, pass -Password, or make sure " +
              "src\ErpSystem.Api\appsettings.json holds a DefaultConnection with a password in it."
    }

    return [pscustomobject]@{
        UserId   = $resolvedUser
        Password = $resolvedPassword
    }
}
