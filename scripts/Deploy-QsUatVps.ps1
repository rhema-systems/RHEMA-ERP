[CmdletBinding()]
param(
    [ValidatePattern('^RhemaERP_[A-Za-z0-9_]{1,119}$')]
    [string]$ExpectedDatabase='RhemaERP_VpsTest_20260926_173800',
    [ValidatePattern('^[0-9a-fA-F]{7,40}$')]
    [string]$ExpectedCommit,
    [uri]$PublicBaseUrl='https://63.141.230.56',
    [switch]$PrepareQsUat
)
# Run this file on the VPS release checkout after updating master. The existing
# deployer performs its own preflight, backups, migration and release checks.
$ErrorActionPreference='Stop'
$repositoryRoot=Split-Path -Parent $PSScriptRoot
$deploymentPassed=$false
$preparationRunning=$false
$locationPushed=$false
try {
    if(-not $PublicBaseUrl.IsAbsoluteUri -or $PublicBaseUrl.Scheme -notin @('https','http') -or
        $PublicBaseUrl.UserInfo -or $PublicBaseUrl.Query -or $PublicBaseUrl.Fragment -or $PublicBaseUrl.AbsolutePath -ne '/') {
        throw 'Public URL must be an HTTP(S) origin.'
    }
    $publicOrigin=$PublicBaseUrl.GetLeftPart([UriPartial]::Authority)
    $powershell=(Get-Command powershell.exe -ErrorAction Stop).Source
    Push-Location -LiteralPath $repositoryRoot
    $locationPushed=$true
    $deployArguments=@('-NoProfile','-ExecutionPolicy','Bypass','-File',
        (Join-Path $PSScriptRoot 'Deploy-RhemaVps.ps1'),
        '-Environment','Test','-LocalVps','-PublicBaseUrl',$publicOrigin)
    if($ExpectedCommit){$deployArguments+=@('-ExpectedCommit',$ExpectedCommit)}
    # Keep the child interactive so its existing secure credential prompt works.
    & $powershell @deployArguments
    if($LASTEXITCODE -ne 0){throw 'Deployment command failed.'}
    $deploymentPassed=$true

    if($PrepareQsUat) {
        $preparationRunning=$true
        & $powershell -NoProfile -ExecutionPolicy Bypass `
            -File (Join-Path $PSScriptRoot 'vps\Initialize-QsUat.ps1') -ExpectedDatabase $ExpectedDatabase
        if($LASTEXITCODE -ne 0){throw 'QS test preparation failed; retain its evidence before retrying.'}
        $preparationRunning=$false
    }

    & $powershell -NoProfile -ExecutionPolicy Bypass `
        -File (Join-Path $PSScriptRoot 'vps\Get-QsUatReadiness.ps1') `
        -ExpectedDatabase $ExpectedDatabase -PublicBaseUrl $publicOrigin
    if($LASTEXITCODE -ne 0){throw 'QS prerequisite report command failed.'}

    Write-Output 'QS_RELEASE_DEPLOYMENT|PASS'
    Write-Output 'Deployment completed and QS prerequisite reports generated. Review artifacts\qs-uat before UAT.'
    exit 0
} catch {
    if($preparationRunning) {
        Write-Error 'Deployment passed, but QS test preparation stopped. The readiness report was not run. Keep preparation evidence and resolve the reported blocker before retrying Initialize-QsUat.ps1.' -ErrorAction Continue
    } elseif($deploymentPassed) {
        Write-Error 'Deployment passed, but the QS prerequisite report failed. Inspect the reported target/configuration error and deployment evidence before retrying the report.' -ErrorAction Continue
    } else {
        Write-Error 'Deployment stopped. The QS prerequisite report was not run. Keep the deployment evidence and resolve the reported blocker before retrying.' -ErrorAction Continue
    }
    exit 1
} finally {
    if($locationPushed){Pop-Location}
}
