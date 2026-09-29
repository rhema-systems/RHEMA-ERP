function Get-RhemaNormalizedSourceHash {
    param([Parameter(Mandatory = $true)][string]$Path)
    $text = [IO.File]::ReadAllText($Path).Replace("`r`n", "`n")
    $sha = [Security.Cryptography.SHA256]::Create()
    try { return ([BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($text)))).Replace('-', '') }
    finally { $sha.Dispose() }
}

function New-RhemaVpsPreflightHelper {
    param(
        [Parameter(Mandatory = $true)][string]$RepositoryRoot,
        [Parameter(Mandatory = $true)][string]$OutputPath
    )
    $probeRoot = Join-Path $RepositoryRoot 'scripts\vps'
    $manifest = Get-Content -Raw -LiteralPath (Join-Path $probeRoot 'CanonicalMigrationPreflight.json') | ConvertFrom-Json
    if ($manifest.schemaVersion -ne 1) { throw 'Unsupported canonical migration preflight manifest.' }
    $seen = @{}
    $probes = @()
    foreach ($probe in $manifest.probes) {
        if ($probe.sqlFile -notmatch '^[A-Za-z]+MigrationPreflight\.sql$') { throw 'Invalid preflight SQL filename.' }
        $ids = @()
        foreach ($migration in $probe.migrations) {
            if ($migration.id -notmatch '^\d{14}_[A-Za-z0-9]+$' -or $seen.ContainsKey($migration.id)) {
                throw 'Invalid or duplicate preflight migration ID.'
            }
            $path = Join-Path $RepositoryRoot "src\ErpSystem.Data\Migrations\$($migration.id).cs"
            if ((Get-RhemaNormalizedSourceHash $path) -ne $migration.sourceSha256) {
                throw "Migration $($migration.id) changed since its preflight review. Review its guard before deployment."
            }
            foreach ($guard in @($migration.guardSources)) {
                if ($null -eq $guard) { continue }
                if ($guard.file -notmatch '^[A-Za-z][A-Za-z0-9]*\.cs$') {
                    throw 'Invalid preflight guard source filename.'
                }
                $guardPath = Join-Path $RepositoryRoot "src\ErpSystem.Data\Migrations\$($guard.file)"
                if ((Get-RhemaNormalizedSourceHash $guardPath) -ne $guard.sourceSha256) {
                    throw "Migration $($migration.id) guard $($guard.file) changed since its preflight review. Review its guard before deployment."
                }
            }
            $seen[$migration.id] = $true
            $ids += $migration.id
        }
        $probes += [ordered]@{ ids = $ids; sql = [IO.File]::ReadAllText((Join-Path $probeRoot $probe.sqlFile)) }
    }
    $json = ConvertTo-Json -Depth 8 -Compress -InputObject @{ schemaVersion = 1; probes = $probes }
    $encoded = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($json))
    $source = [IO.File]::ReadAllText((Join-Path $probeRoot 'Invoke-RhemaVpsRemote.ps1'))
    $marker = '__RHEMA_CANONICAL_PREFLIGHT_BUNDLE__'
    if ([regex]::Matches($source, $marker).Count -ne 1) { throw 'Expected one preflight bundle placeholder.' }
    $source = $source.Replace($marker, $encoded)
    foreach ($library in @('FreshDatabaseProvisioning', 'OperationalUatVerification', 'FreshDatabaseCutover')) {
        $libraryMarker = "__RHEMA_$($library.ToUpperInvariant())_LIBRARY__"
        if ([regex]::Matches($source, $libraryMarker).Count -ne 1) { throw "Expected one $library library placeholder." }
        $source = $source.Replace($libraryMarker, [IO.File]::ReadAllText((Join-Path $probeRoot "$library.ps1")))
    }
    $migrationIds = @(Get-ChildItem (Join-Path $RepositoryRoot 'src\ErpSystem.Data\Migrations') -File -Filter '*.cs' |
        Where-Object { $_.Name -match '^\d{14}_.+\.cs$' -and $_.Name -notmatch '\.Designer\.cs$' } |
        Sort-Object BaseName | Select-Object -ExpandProperty BaseName)
    if ($migrationIds.Count -eq 0) { throw 'Fresh database migration manifest is empty.' }
    $migrationJson = ConvertTo-Json -Compress -InputObject $migrationIds
    $source = $source.Replace('__RHEMA_FRESH_MIGRATION_IDS__', [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($migrationJson)))
    [IO.File]::WriteAllText($OutputPath, $source, (New-Object Text.UTF8Encoding($false)))
    return $OutputPath
}
