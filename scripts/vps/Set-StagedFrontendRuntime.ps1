# Operates only on a newly staged package, never the source checkout or live app.
function Set-StagedFrontendRuntime {
    param([Parameter(Mandatory=$true)][string]$FrontendDirectory)
    $packagePath = Join-Path $FrontendDirectory 'package.json'
    $configPath = Join-Path $FrontendDirectory 'next.config.js'
    $manifestPath = Join-Path $FrontendDirectory '.next\required-server-files.json'
    $package = Get-Content -LiteralPath $packagePath -Raw | ConvertFrom-Json
    $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
    $oldDirectory = [string]$manifest.config.distDir
    if ($oldDirectory -cnotin @('.next', '.next-production')) {
        throw 'Staged Next.js output has an unsupported directory; refusing to rewrite it.'
    }
    if ($manifest.config.output -eq 'standalone' -or $manifest.config.output -eq 'export') {
        throw 'The VPS requires a regular Next.js server build.'
    }
    # Public assets already came from the completed prebuild. Runtime does not
    # need the repository-only asset-copy or build/start wrapper scripts.
    $package.scripts.start = 'next start'
    $package.scripts.PSObject.Properties.Remove('prestart')
    $config = [IO.File]::ReadAllText($configPath)
    $config = "process.env.NEXT_DIST_DIR = '.next';`nprocess.env.NEXT_OUTPUT = '';`n" + $config
    $manifest.config.distDir = '.next'
    $manifest.files = @($manifest.files | ForEach-Object {
        if ($_.StartsWith($oldDirectory + '/', [StringComparison]::Ordinal)) {
            '.next/' + $_.Substring($oldDirectory.Length + 1)
        } elseif ($_.StartsWith($oldDirectory + '\', [StringComparison]::Ordinal)) {
            '.next\' + $_.Substring($oldDirectory.Length + 1)
        } else { $_ }
    })
    $encoding = New-Object Text.UTF8Encoding($false)
    [IO.File]::WriteAllText($packagePath, ($package | ConvertTo-Json -Depth 30), $encoding)
    [IO.File]::WriteAllText($configPath, $config, $encoding)
    [IO.File]::WriteAllText($manifestPath, ($manifest | ConvertTo-Json -Depth 100), $encoding)
}
