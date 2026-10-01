function New-RhemaZipPackage {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$Source,
        [Parameter(Mandatory = $true)]
        [string]$Destination,
        [ValidateSet('Optimal', 'Fastest', 'NoCompression')]
        [string]$CompressionLevel = 'Fastest'
    )

    $sourcePath = [IO.Path]::GetFullPath($Source)
    $destinationPath = [IO.Path]::GetFullPath($Destination)
    if (-not (Test-Path -LiteralPath $sourcePath -PathType Container)) {
        throw "ZIP package source directory does not exist: $sourcePath"
    }
    if ($destinationPath.StartsWith(
            $sourcePath.TrimEnd('\') + '\',
            [StringComparison]::OrdinalIgnoreCase)) {
        throw 'ZIP package destination cannot be inside its source directory.'
    }

    $destinationDirectory = Split-Path -Parent $destinationPath
    if (-not (Test-Path -LiteralPath $destinationDirectory)) {
        New-Item -ItemType Directory -Path $destinationDirectory -Force | Out-Null
    }
    if (Test-Path -LiteralPath $destinationPath) {
        Remove-Item -LiteralPath $destinationPath -Force
    }

    # Windows tar uses the native archive implementation and is dramatically
    # faster than Compress-Archive/System.IO.Compression for the frontend's
    # many small files. The managed path remains available on older hosts.
    $nativeTar = Get-Command tar.exe -ErrorAction SilentlyContinue
    $nativeSucceeded = $false
    $packager = 'ManagedZip'
    if ($null -ne $nativeTar) {
        & $nativeTar.Source -a -c -f $destinationPath -C $sourcePath .
        $nativeSucceeded = ($LASTEXITCODE -eq 0)
        if ($nativeSucceeded) { $packager = 'WindowsTar' }
        if (-not $nativeSucceeded -and (Test-Path -LiteralPath $destinationPath)) {
            Remove-Item -LiteralPath $destinationPath -Force
        }
    }

    if (-not $nativeSucceeded) {
        Add-Type -AssemblyName System.IO.Compression.FileSystem
        $level = [Enum]::Parse(
            [IO.Compression.CompressionLevel], $CompressionLevel, $true)
        [IO.Compression.ZipFile]::CreateFromDirectory(
            $sourcePath, $destinationPath, $level, $false)
    }

    $archive = Get-Item -LiteralPath $destinationPath
    if ($archive.Length -le 0) {
        throw "ZIP package is empty: $destinationPath"
    }
    Write-Host ("ZIP_PACKAGER|{0}|{1}|{2}" -f `
            $packager, $archive.Name, $archive.Length)
}
