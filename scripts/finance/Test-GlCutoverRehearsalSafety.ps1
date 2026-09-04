Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false

$script = Join-Path $PSScriptRoot 'Invoke-GlCutoverRehearsal.ps1'
$cases = @(
    @{ Name='configured development database'; Value='Server=localhost;Database=RhemaERP;Integrated Security=true'; Expected='Refusing database' },
    @{ Name='missing database'; Value='Server=localhost;Integrated Security=true'; Expected='no Database/Initial Catalog' },
    @{ Name='attach file'; Value='Server=localhost;Database=RHEMAERP_GL_REHEARSAL_SAFE;Integrated Security=true;AttachDbFilename=C:\temp\unsafe.mdf'; Expected='AttachDbFilename is forbidden' },
    @{ Name='user instance'; Value='Server=localhost;Database=RHEMAERP_GL_REHEARSAL_SAFE;Integrated Security=true;User Instance=true'; Expected='User Instance connections are forbidden' },
    @{ Name='invalid suffix'; Value='Server=localhost;Database=RHEMAERP_GL_REHEARSAL_bad-name;Integrated Security=true'; Expected='Refusing database' }
)

$prior = [Environment]::GetEnvironmentVariable('RHEMA_GL_REHEARSAL_CONNECTION', 'Process')
try {
    foreach ($case in $cases) {
        [Environment]::SetEnvironmentVariable('RHEMA_GL_REHEARSAL_CONNECTION', $case.Value, 'Process')
        $output = & pwsh -NoProfile -File $script -Mode RehearseEmpty 2>&1 | Out-String
        if ($LASTEXITCODE -eq 0) { throw "Safety case '$($case.Name)' unexpectedly succeeded." }
        if ($output -notmatch [regex]::Escape($case.Expected)) {
            throw "Safety case '$($case.Name)' did not return the expected refusal '$($case.Expected)'."
        }
        Write-Host "PASS: $($case.Name)"
    }
}
finally {
    [Environment]::SetEnvironmentVariable('RHEMA_GL_REHEARSAL_CONNECTION', $prior, 'Process')
}

Write-Host 'All GL cutover rehearsal safety refusals passed.'
