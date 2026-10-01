[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
. (Join-Path $repositoryRoot 'scripts\vps\New-RhemaZipPackage.ps1')

function Assert-Test {
    param([bool]$Condition, [string]$Message)
    if (-not $Condition) { throw $Message }
}

$testRoot = Join-Path ([IO.Path]::GetTempPath()) `
    ('rhema-zip-package-test-' + [guid]::NewGuid().ToString('N'))
$source = Join-Path $testRoot 'source'
$destination = Join-Path $testRoot 'release.zip'
$extracted = Join-Path $testRoot 'extracted'

try {
    [void][IO.Directory]::CreateDirectory((Join-Path $source '.next'))
    [void][IO.Directory]::CreateDirectory((Join-Path $source 'node_modules\sample'))
    [void][IO.Directory]::CreateDirectory((Join-Path $source 'public'))
    [IO.File]::WriteAllText((Join-Path $source '.next\BUILD_ID'), 'build-123')
    [IO.File]::WriteAllText((Join-Path $source 'node_modules\sample\package.json'), '{}')
    [IO.File]::WriteAllText((Join-Path $source 'public\asset.txt'), 'asset')

    New-RhemaZipPackage -Source $source -Destination $destination `
        -CompressionLevel Fastest

    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [IO.Compression.ZipFile]::OpenRead($destination)
    try {
        $names = @($archive.Entries | ForEach-Object {
            $_.FullName.Replace('\', '/') -replace '^\./', ''
        })
        foreach ($expected in @(
                '.next/BUILD_ID',
                'node_modules/sample/package.json',
                'public/asset.txt')) {
            Assert-Test ($expected -in $names) "ZIP is missing $expected."
        }
    }
    finally { $archive.Dispose() }

    Expand-Archive -LiteralPath $destination -DestinationPath $extracted -Force
    Assert-Test ((Get-Content -LiteralPath `
                (Join-Path $extracted '.next\BUILD_ID') -Raw) -eq 'build-123') `
        'Extracted BUILD_ID does not match the source.'
    Assert-Test ((Get-Content -LiteralPath `
                (Join-Path $extracted 'public\asset.txt') -Raw) -eq 'asset') `
        'Extracted public asset does not match the source.'

    Write-Output 'PASS|Fast ZIP packaging creates a standard, validated release archive.'
}
finally {
    if (Test-Path -LiteralPath $testRoot) {
        Remove-Item -LiteralPath $testRoot -Recurse -Force
    }
}
