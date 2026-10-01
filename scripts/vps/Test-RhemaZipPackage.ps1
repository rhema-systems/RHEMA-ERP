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
$destination = Join-Path $testRoot 'release-fastest.zip'
$storedDestination = Join-Path $testRoot 'release-stored.zip'
$extracted = Join-Path $testRoot 'extracted'
$storedExtracted = Join-Path $testRoot 'stored-extracted'

try {
    [void][IO.Directory]::CreateDirectory((Join-Path $source '.next'))
    [void][IO.Directory]::CreateDirectory((Join-Path $source 'node_modules\sample'))
    [void][IO.Directory]::CreateDirectory((Join-Path $source 'public'))
    [IO.File]::WriteAllText((Join-Path $source '.next\BUILD_ID'), 'build-123')
    [IO.File]::WriteAllText((Join-Path $source 'node_modules\sample\package.json'), '{}')
    [IO.File]::WriteAllText((Join-Path $source 'public\asset.txt'), 'asset')
    [IO.File]::WriteAllText((Join-Path $source 'public\compressible.txt'), ('x' * 1MB))

    New-RhemaZipPackage -Source $source -Destination $destination `
        -CompressionLevel Fastest
    New-RhemaZipPackage -Source $source -Destination $storedDestination `
        -CompressionLevel NoCompression

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

    $tar = Get-Command tar.exe -ErrorAction Stop
    [void][IO.Directory]::CreateDirectory($extracted)
    & $tar.Source -xf $destination -C $extracted
    Assert-Test ($LASTEXITCODE -eq 0) 'Native extraction of the Fastest ZIP failed.'
    [void][IO.Directory]::CreateDirectory($storedExtracted)
    & $tar.Source -xf $storedDestination -C $storedExtracted
    Assert-Test ($LASTEXITCODE -eq 0) 'Native extraction of the stored ZIP failed.'
    Assert-Test ((Get-Content -LiteralPath `
                (Join-Path $extracted '.next\BUILD_ID') -Raw) -eq 'build-123') `
        'Extracted BUILD_ID does not match the source.'
    Assert-Test ((Get-Content -LiteralPath `
                (Join-Path $extracted 'public\asset.txt') -Raw) -eq 'asset') `
        'Extracted public asset does not match the source.'
    Assert-Test ((Get-Content -LiteralPath `
                (Join-Path $storedExtracted '.next\BUILD_ID') -Raw) -eq 'build-123') `
        'Native extraction of the stored ZIP changed the source content.'
    Assert-Test ((Get-Item -LiteralPath $storedDestination).Length -gt `
            (Get-Item -LiteralPath $destination).Length) `
        'NoCompression did not produce a stored ZIP distinct from Fastest.'

    Write-Output 'PASS|Native Fastest and stored ZIP packages preserve release content and extract with tar.exe.'
}
finally {
    if (Test-Path -LiteralPath $testRoot) {
        Remove-Item -LiteralPath $testRoot -Recurse -Force
    }
}
