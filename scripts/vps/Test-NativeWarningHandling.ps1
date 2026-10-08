[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$testRoot = Join-Path ([IO.Path]::GetTempPath()) `
    ('rhema-native-warning-' + [guid]::NewGuid().ToString('N'))

function Assert-Test {
    param([bool]$Condition, [string]$Message)
    if (-not $Condition) { throw $Message }
}

function Read-TestFunction {
    param([string]$Path, [string]$Name)
    $tokens = $null
    $errors = $null
    $ast = [Management.Automation.Language.Parser]::ParseFile(
        $Path, [ref]$tokens, [ref]$errors)
    Assert-Test ($errors.Count -eq 0) "PowerShell source failed to parse: $Path"
    $function = $ast.Find({
            param($candidate)
            $candidate -is [Management.Automation.Language.FunctionDefinitionAst] -and
                $candidate.Name -eq $Name
        }, $true)
    Assert-Test ($null -ne $function) "Required function is missing: $Name"
    return $function.Extent.Text
}

try {
    [void](New-Item -ItemType Directory -Path $testRoot)
    $probe = Join-Path $testRoot 'native-probe.ps1'
    [IO.File]::WriteAllText($probe, @'
param([int]$RequestedExitCode)
[Console]::Error.WriteLine('expected native warning')
exit $RequestedExitCode
'@)
    $powershell = (Get-Command powershell.exe -ErrorAction Stop).Source

    Invoke-Expression (Read-TestFunction `
            (Join-Path $repositoryRoot 'scripts\Deploy-RhemaVps.ps1') `
            'Invoke-NativeChecked')
    [Environment]::SetEnvironmentVariable(
        'UatBootstrap__SharedPassword', 'native-warning-sentinel', 'Process')
    $deployOutput = @(Invoke-NativeChecked $powershell @(
            '-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $probe,
            '-RequestedExitCode', '0') 'Deploy native probe failed')
    Assert-Test (($deployOutput -join "`n").Contains('expected native warning')) `
        'The deployer discarded a successful native stderr warning.'
    Assert-Test ([Environment]::GetEnvironmentVariable(
            'UatBootstrap__SharedPassword', 'Process') -ceq 'native-warning-sentinel') `
        'The deployer did not restore the protected process environment.'

    $deployRejected = $false
    try {
        Invoke-NativeChecked $powershell @(
            '-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $probe,
            '-RequestedExitCode', '7') 'Deploy native probe failed'
    }
    catch {
        $deployRejected = $_.Exception.Message -eq `
            'Deploy native probe failed (exit code 7).'
    }
    Assert-Test $deployRejected 'The deployer accepted a nonzero native exit code.'

    Invoke-Expression (Read-TestFunction `
            (Join-Path $repositoryRoot 'scripts\Build-RhemaRelease.ps1') `
            'Invoke-NativeChecked')
    Invoke-NativeChecked $powershell @(
        '-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $probe,
        '-RequestedExitCode', '0') 'Builder native probe failed'

    $buildRejected = $false
    try {
        Invoke-NativeChecked $powershell @(
            '-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $probe,
            '-RequestedExitCode', '9') 'Builder native probe failed'
    }
    catch {
        $buildRejected = $_.Exception.Message -eq `
            'Builder native probe failed (exit code 9).'
    }
    Assert-Test $buildRejected 'The release builder accepted a nonzero native exit code.'

    Write-Output 'PASS|Windows PowerShell native stderr warnings do not fail successful release commands.'
}
finally {
    [Environment]::SetEnvironmentVariable(
        'UatBootstrap__SharedPassword', $null, 'Process')
    if (Test-Path -LiteralPath $testRoot) {
        Remove-Item -LiteralPath $testRoot -Recurse -Force
    }
}
