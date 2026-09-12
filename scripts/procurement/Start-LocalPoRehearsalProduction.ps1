param([string]$FrontendRuntimePath)

$ErrorActionPreference = 'Stop'
$productionRepo = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$productionPrevious = [System.IO.Path]::GetFullPath((Join-Path $productionRepo 'local-artifacts/rehearsal-production-20260910/frontend'))
$productionUpdated = [System.IO.Path]::GetFullPath((Join-Path $productionRepo 'local-artifacts/rehearsal-requisition-height-20260910/frontend'))
$productionServing = [System.IO.Path]::GetFullPath((Join-Path $productionRepo 'local-artifacts/requisition-issue-20260910/frontend-serving'))
$productionReceiverServing = [System.IO.Path]::GetFullPath((Join-Path $productionRepo 'local-artifacts/requisition-receiver-20260910/frontend-serving'))
$productionBulkServing = [System.IO.Path]::GetFullPath((Join-Path $productionRepo 'local-artifacts/requisition-bulk-20260910/frontend-serving'))
$productionReturnServing = [System.IO.Path]::GetFullPath((Join-Path $productionRepo 'local-artifacts/requisition-return-20260910/frontend-serving'))
$productionQuantityServing = [System.IO.Path]::GetFullPath((Join-Path $productionRepo 'local-artifacts/requisition-return-quantities-20260910/frontend-serving'))
$productionQuantityLayoutServing = [System.IO.Path]::GetFullPath((Join-Path $productionRepo 'local-artifacts/requisition-return-quantities-20260910/frontend-serving-layout'))
$productionReturnColour = [System.IO.Path]::GetFullPath((Join-Path $productionRepo 'local-artifacts/requisition-return-colour-20260910/frontend'))
$productionCountDrafts = [System.IO.Path]::GetFullPath((Join-Path $productionRepo 'local-artifacts/physical-count-drafts-20260911/frontend'))
$productionCountSheet = [System.IO.Path]::GetFullPath((Join-Path $productionRepo 'local-artifacts/physical-count-sheet-20260911/frontend'))
$productionCountUpload = [System.IO.Path]::GetFullPath((Join-Path $productionRepo 'local-artifacts/physical-count-upload-20260911/frontend'))
$productionCurrentSheet = [System.IO.Path]::GetFullPath((Join-Path $productionRepo 'local-artifacts/physical-count-current-20260911/frontend-serving'))
$productionCountReview = [System.IO.Path]::GetFullPath((Join-Path $productionRepo 'local-artifacts/physical-count-review-20260911/frontend'))
$productionCountGridPrevious = [System.IO.Path]::GetFullPath((Join-Path $productionRepo 'local-artifacts/physical-count-grid-20260911/frontend-serving'))
$productionCountSavePrevious = [System.IO.Path]::GetFullPath((Join-Path $productionRepo 'local-artifacts/physical-count-grid-20260911/frontend-saving-snapshot'))
$productionCountFooterPrevious = [System.IO.Path]::GetFullPath((Join-Path $productionRepo 'local-artifacts/physical-count-grid-20260911/frontend-compact-footer-snapshot'))
$productionCountHistoryPrevious = [System.IO.Path]::GetFullPath((Join-Path $productionRepo 'local-artifacts/physical-count-grid-20260911/frontend-history-snapshot'))
$productionCountScopePrevious = [System.IO.Path]::GetFullPath((Join-Path $productionRepo 'local-artifacts/physical-count-grid-20260911/frontend-scope-snapshot'))
$productionCountSubmit = [System.IO.Path]::GetFullPath((Join-Path $productionRepo 'local-artifacts/physical-count-submit-20260912/frontend'))
$productionCountE2E = [System.IO.Path]::GetFullPath((Join-Path $productionRepo 'local-artifacts/physical-count-e2e-20260912/frontend'))
$productionOptionalApproval = [System.IO.Path]::GetFullPath((Join-Path $productionRepo 'local-artifacts/optional-approval-20260912/frontend'))
$productionOptionalApprovalPhase2 = [System.IO.Path]::GetFullPath((Join-Path $productionRepo 'local-artifacts/optional-approval-phase2-20260912/frontend'))
$productionInventoryFinal = [System.IO.Path]::GetFullPath((Join-Path $productionRepo 'local-artifacts/inventory-final-20260912/frontend'))
$productionInventoryLayout = [System.IO.Path]::GetFullPath((Join-Path $productionRepo 'local-artifacts/inventory-final-layout-20260912/frontend'))
if ([string]::IsNullOrWhiteSpace($FrontendRuntimePath) -and (Test-Path -LiteralPath (Join-Path $productionCountE2E 'e2e-runtime-ready.txt'))) {
    $FrontendRuntimePath = $productionCountE2E
}
if ([string]::IsNullOrWhiteSpace($FrontendRuntimePath) -and (Test-Path -LiteralPath (Join-Path $productionCountSubmit 'submit-runtime-ready.txt'))) {
    $FrontendRuntimePath = $productionCountSubmit
}
if ([string]::IsNullOrWhiteSpace($FrontendRuntimePath) -and (Test-Path -LiteralPath (Join-Path $productionCountReview '.next/BUILD_ID'))) {
    $FrontendRuntimePath = $productionCountReview
}
if ([string]::IsNullOrWhiteSpace($FrontendRuntimePath) -and (Test-Path -LiteralPath (Join-Path $productionCurrentSheet '.next/BUILD_ID'))) {
    $FrontendRuntimePath = $productionCurrentSheet
}
if ([string]::IsNullOrWhiteSpace($FrontendRuntimePath)) {
    $FrontendRuntimePath = if (Test-Path -LiteralPath (Join-Path $productionCountUpload '.next/BUILD_ID')) { $productionCountUpload } elseif (Test-Path -LiteralPath (Join-Path $productionCountSheet '.next/BUILD_ID')) { $productionCountSheet } elseif (Test-Path -LiteralPath (Join-Path $productionCountDrafts '.next/BUILD_ID')) { $productionCountDrafts } elseif (Test-Path -LiteralPath (Join-Path $productionReturnColour '.next/BUILD_ID')) { $productionReturnColour } elseif (Test-Path -LiteralPath (Join-Path $productionUpdated '.next/BUILD_ID')) { $productionUpdated } else { $productionPrevious }
}
$productionFrontend = (Resolve-Path -LiteralPath $FrontendRuntimePath).Path
if ($productionFrontend -notin @($productionPrevious, $productionUpdated, $productionServing, $productionReceiverServing, $productionBulkServing, $productionReturnServing, $productionQuantityServing, $productionReturnColour, $productionCountDrafts, $productionCountSheet, $productionCountUpload, $productionCurrentSheet, $productionCountReview, $productionCountGridPrevious, $productionCountSavePrevious, $productionCountFooterPrevious, $productionCountHistoryPrevious, $productionQuantityLayoutServing, $productionCountScopePrevious, $productionCountSubmit, $productionCountE2E, $productionOptionalApproval, $productionOptionalApprovalPhase2, $productionInventoryFinal, $productionInventoryLayout)) {
    throw 'Only a prepared isolated rehearsal frontend may be started by this script.'
}
$productionRoot = Split-Path -Path $productionFrontend -Parent
$productionGuard = Join-Path $productionRepo 'local-artifacts/po-rehearsal-20260909/frontend-guard.cjs'
if (-not (Test-Path -LiteralPath (Join-Path $productionFrontend '.next/BUILD_ID'))) {
    throw 'The rehearsal production build has not finished successfully.'
}
if (-not (Get-NetTCPConnection -LocalAddress '127.0.0.1' -LocalPort 5002 -State Listen -ErrorAction SilentlyContinue)) {
    throw 'Start the isolated rehearsal API on port 5002 first.'
}
$productionResultPath = Join-Path $productionRoot 'frontend-build-result.json'
if (Test-Path -LiteralPath $productionResultPath) {
    $productionResult = Get-Content -LiteralPath $productionResultPath -Raw | ConvertFrom-Json
    if ($productionResult.requiresApiPhase -eq 'TransferAutoComplete') {
        $productionRequiredApi = [IO.Path]::GetFullPath((Join-Path $env:LOCALAPPDATA 'Temp/tdc-inventory-transfer-auto-complete-20260912/api'))
        $productionApiListener = Get-NetTCPConnection -LocalAddress '127.0.0.1' -LocalPort 5002 -State Listen
        $productionApiProcess = Get-CimInstance Win32_Process -Filter "ProcessId=$($productionApiListener.OwningProcess)"
        if (-not $productionApiProcess.ExecutablePath -or [IO.Path]::GetDirectoryName($productionApiProcess.ExecutablePath) -ne $productionRequiredApi) {
            throw 'This frontend requires the verified automatic-completion API; the existing frontend was not stopped.'
        }
        . (Join-Path $PSScriptRoot 'LocalTransferRuntimeReadiness.ps1')
        $null=Get-LocalTransferRuntimeReadiness -ApiRuntimePath $productionRequiredApi -RequireRunning
    }
}
& (Join-Path $PSScriptRoot 'Start-LocalFileScanner.ps1')
$productionListener = Get-NetTCPConnection -LocalAddress '127.0.0.1' -LocalPort 3002 -State Listen -ErrorAction SilentlyContinue
if ($productionListener) {
    $productionOld = Get-CimInstance Win32_Process -Filter "ProcessId=$($productionListener.OwningProcess)"
    if ($productionOld.Name -ne 'node.exe' -or $productionOld.CommandLine -notmatch [regex]::Escape($productionGuard)) {
        throw 'Unexpected process on port 3002; no process was stopped.'
    }
    Stop-Process -Id $productionOld.ProcessId
    Wait-Process -Id $productionOld.ProcessId -Timeout 15 -ErrorAction SilentlyContinue
}
$productionPriorApi = $env:NEXT_PUBLIC_API_URL
$productionPriorNode = $env:NODE_OPTIONS
try {
    $env:NEXT_PUBLIC_API_URL = 'http://127.0.0.1:5002/api'
    $env:NODE_OPTIONS = '--max-old-space-size=8192'
    # This is the same Next.js production command used by npm start, with the
    # existing rehearsal isolation guard loaded before the server starts.
    $productionArguments = @('--require', ('"'+$productionGuard+'"'),
        ('"'+(Join-Path $productionRepo 'frontend/node_modules/next/dist/bin/next')+'"'),
        'start', '--hostname', '127.0.0.1', '--port', '3002')
    $productionProcess = Start-Process -FilePath (Get-Command node).Source -ArgumentList $productionArguments `
        -WorkingDirectory $productionFrontend -WindowStyle Hidden -PassThru `
        -RedirectStandardOutput (Join-Path $productionRoot 'frontend.stdout.log') `
        -RedirectStandardError (Join-Path $productionRoot 'frontend.stderr.log')
    [pscustomobject]@{Url='http://127.0.0.1:3002';Mode='Production (next start)';ProcessId=$productionProcess.Id;UatUntouched=$true} | ConvertTo-Json -Compress
} finally {
    $env:NEXT_PUBLIC_API_URL = $productionPriorApi
    $env:NODE_OPTIONS = $productionPriorNode
}
