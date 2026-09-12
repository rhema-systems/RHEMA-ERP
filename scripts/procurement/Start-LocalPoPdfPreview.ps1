param([switch]$StartOnly)
$ErrorActionPreference='Stop'
$poPdfRepo=(Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$poPdfRoot=Join-Path $poPdfRepo 'local-artifacts/po-rehearsal-20260909'
$poPdfPreview=Join-Path $poPdfRoot 'frontend-pdf-preview'
if ($StartOnly -and -not (Test-Path -LiteralPath (Join-Path $poPdfPreview 'src/lib/purchase-order-document.ts'))) { throw 'The prepared PDF preview is missing. Run without -StartOnly for the initial setup.' }
if (-not $StartOnly -and (Test-Path -LiteralPath $poPdfPreview)) { throw 'PDF preview source already exists; use -StartOnly to restart it without replacing files.' }
if (-not (Get-NetTCPConnection -LocalAddress '127.0.0.1' -LocalPort 5002 -State Listen -ErrorAction SilentlyContinue)) { throw 'The isolated rehearsal API must be running first.' }
if (-not $StartOnly) {
[void](New-Item -ItemType Directory -Path $poPdfPreview)
& robocopy (Join-Path $poPdfRepo 'frontend/src') (Join-Path $poPdfPreview 'src') /E /XJ /R:1 /W:1 /NFL /NDL /NJH /NJS | Out-Null
if ($LASTEXITCODE -ge 8) { throw 'Failed to copy frontend source.' }
Get-ChildItem -LiteralPath (Join-Path $poPdfRepo 'frontend') -File | Where-Object { $_.Name -match '^(package(-lock)?\.json|tsconfig\.json|next-env\.d\.ts|next\.config\.[cm]?js|postcss\.config\.[cm]?js|tailwind\.config\.(ts|js))$' } | ForEach-Object { Copy-Item -LiteralPath $_.FullName -Destination $poPdfPreview }
[void](New-Item -ItemType Junction -Path (Join-Path $poPdfPreview 'node_modules') -Target (Join-Path $poPdfRepo 'frontend/node_modules'))
[void](New-Item -ItemType Junction -Path (Join-Path $poPdfPreview 'public') -Target (Join-Path $poPdfRoot 'frontend/public'))
}
# Only the known isolated frontend may be stopped. The original UAT uses port 3000 and is never targeted.
$poPdfListener=Get-NetTCPConnection -LocalAddress '127.0.0.1' -LocalPort 3002 -State Listen -ErrorAction SilentlyContinue
if ($poPdfListener) {
    $poPdfOldProcess=Get-CimInstance Win32_Process -Filter "ProcessId=$($poPdfListener.OwningProcess)"
    if ($poPdfOldProcess.Name -ne 'node.exe' -or $poPdfOldProcess.CommandLine -notmatch [regex]::Escape((Join-Path $poPdfRoot 'frontend-guard.cjs')) -or ($poPdfOldProcess.CommandLine -notmatch 'server\.js' -and $poPdfOldProcess.CommandLine -notmatch 'next[\\/]dist[\\/]server[\\/]lib[\\/]start-server\.js')) { throw 'Unexpected process on rehearsal port 3002; refusing to stop it.' }
    Stop-Process -Id $poPdfOldProcess.ProcessId
    Wait-Process -Id $poPdfOldProcess.ProcessId -Timeout 15 -ErrorAction SilentlyContinue
}
$poPdfPriorApi=$env:NEXT_PUBLIC_API_URL
$poPdfPriorNode=$env:NODE_OPTIONS
try {
    $env:NEXT_PUBLIC_API_URL='http://127.0.0.1:5002/api'
    $env:NODE_OPTIONS='--max-old-space-size=8192'
    $poPdfArguments=@('--require', ('"'+(Join-Path $poPdfRoot 'frontend-guard.cjs')+'"'), ('"'+(Join-Path $poPdfRepo 'frontend/node_modules/next/dist/bin/next')+'"'), 'dev', '--hostname','127.0.0.1','--port','3002')
    $poPdfProcess=Start-Process -FilePath (Get-Command node).Source -ArgumentList $poPdfArguments -WorkingDirectory $poPdfPreview -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $poPdfRoot 'pdf-preview.stdout.log') -RedirectStandardError (Join-Path $poPdfRoot 'pdf-preview.stderr.log')
    [pscustomobject]@{FrontendUrl='http://127.0.0.1:3002';Mode='Isolated development preview';LauncherProcessId=$poPdfProcess.Id;OriginalUatUnchanged=$true} | ConvertTo-Json -Compress
} finally { $env:NEXT_PUBLIC_API_URL=$poPdfPriorApi; $env:NODE_OPTIONS=$poPdfPriorNode }
