[CmdletBinding()]
param()
$ErrorActionPreference='Stop'
$repositoryRoot=Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$testRoot=Join-Path ([IO.Path]::GetTempPath()) ('rhema-operational-test-'+[guid]::NewGuid().ToString('N'))
[void](New-Item -ItemType Directory -Path $testRoot)
function Assert-Test { param([bool]$Condition,[string]$Message) if(-not $Condition){throw $Message} }
function Read-TestAst { param([string]$Path)
    $tokens=$null;$errors=$null
    $ast=[Management.Automation.Language.Parser]::ParseFile($Path,[ref]$tokens,[ref]$errors)
    Assert-Test ($errors.Count -eq 0) 'Source PowerShell failed to parse.'
    return $ast
}
function Find-TestFunction { param($Ast,[string]$Name)
    $node=$Ast.Find({param($n) $n -is [Management.Automation.Language.FunctionDefinitionAst] -and $n.Name -eq $Name},$true)
    Assert-Test ($null -ne $node) "Required function missing: $Name"
    return $node
}
$remoteAst=Read-TestAst (Join-Path $PSScriptRoot 'Invoke-RhemaVpsRemote.ps1')
$deployAst=Read-TestAst (Join-Path $repositoryRoot 'scripts/Deploy-RhemaVps.ps1')
$freshAst=Read-TestAst (Join-Path $PSScriptRoot 'FreshDatabaseProvisioning.ps1')
$cutoverAst=Read-TestAst (Join-Path $PSScriptRoot 'FreshDatabaseCutover.ps1')
$savedPassword=[Environment]::GetEnvironmentVariable('UatBootstrap__SharedPassword','Process')
$savedUnrelated=[Environment]::GetEnvironmentVariable('RHEMA_TEST_UNRELATED_SECRET','Process')
$processSentinel='fake-process-Uat-credential-!42'
$serviceSentinel='fake-service-Uat-credential-!43'
$databaseSentinel='fake-database-secret-!44'
try {
    # Load selected declarations only, never the executable deployment action.
    Invoke-Expression (Find-TestFunction $remoteAst 'Get-RhemaOperationalPassword').Extent.Text
    function Get-ApiServiceEnvironment { return $script:TestServiceEnvironment }
    $script:TestServiceEnvironment=@{UatBootstrap__SharedPassword=$serviceSentinel}
    [Environment]::SetEnvironmentVariable('UatBootstrap__SharedPassword',$processSentinel,'Process')
    Assert-Test ((Get-RhemaOperationalPassword) -ceq $processSentinel) 'Process credential must take precedence.'
    [Environment]::SetEnvironmentVariable('UatBootstrap__SharedPassword',$null,'Process')
    Assert-Test ((Get-RhemaOperationalPassword) -ceq $serviceSentinel) 'Protected service credential fallback failed.'
    $script:TestServiceEnvironment=@{}
    Assert-Test ([string]::IsNullOrWhiteSpace((Get-RhemaOperationalPassword -Optional))) 'Optional lookup must not fail or manufacture a credential.'
    $rejected=$false
    try { Get-RhemaOperationalPassword | Out-Null } catch {
        $rejected=$_.Exception.Message -like 'Operational account bootstrap password is missing.*'
        Assert-Test (-not $_.Exception.Message.Contains($processSentinel)) 'Missing-password error leaked a credential.'
    }
    Assert-Test $rejected 'Mandatory missing password did not fail safely.'

    # Exercise the actual CLI configuration statements up to Process creation.
    # This constructs ProcessStartInfo only; no executable or SQL is launched.
    $cliNode=Find-TestFunction $freshAst 'Invoke-RhemaFreshApiCli'
    $prefix=@()
    foreach($statement in $cliNode.Body.EndBlock.Statements) {
        if($statement -is [Management.Automation.Language.AssignmentStatementAst] -and
            $statement.Left.Extent.Text -eq '$process'){break}
        $prefix+=$statement.Extent.Text
    }
    $configuration=[scriptblock]::Create($cliNode.Body.ParamBlock.Extent.Text+"`n"+($prefix -join "`n")+"`nreturn `$start")
    [Environment]::SetEnvironmentVariable('UatBootstrap__SharedPassword','must-not-inherit','Process')
    [Environment]::SetEnvironmentVariable('RHEMA_TEST_UNRELATED_SECRET','must-not-inherit-either','Process')
    $fakeConnection="Server=invalid;Database=RhemaERP_Test;User ID=fake;Password=$databaseSentinel"
    $start=& $configuration -ApiExecutable 'unused.exe' -ContentRoot $testRoot -ConnectionString $fakeConnection `
        -Command seed-operational-uat -OperationalUatPassword $processSentinel
    Assert-Test ($start.EnvironmentVariables['UatBootstrap__SharedPassword'] -ceq $processSentinel) 'Credential missing from child protected environment.'
    Assert-Test ($start.EnvironmentVariables['ConnectionStrings__DefaultConnection'] -ceq $fakeConnection) 'Explicit target connection missing.'
    Assert-Test (-not $start.EnvironmentVariables.ContainsKey('RHEMA_TEST_UNRELATED_SECRET')) 'Unrelated process secret inherited by child.'
    Assert-Test (-not $start.Arguments.Contains($processSentinel) -and -not $start.Arguments.Contains($databaseSentinel)) 'Credentials leaked into command-line arguments.'
    Assert-Test ($start.EnvironmentVariables['ASPNETCORE_CONTENTROOT'] -eq $testRoot -and
        $start.EnvironmentVariables['ASPNETCORE_ENVIRONMENT'] -ne 'Development') 'CLI content/config isolation regressed.'
    Assert-Test (-not $start.UseShellExecute -and $start.CreateNoWindow -and $start.RedirectStandardOutput -and $start.RedirectStandardError) 'CLI raw-output isolation regressed.'
    $start.EnvironmentVariables.Clear()
    $withoutPassword=& $configuration -ApiExecutable 'unused.exe' -ContentRoot $testRoot -ConnectionString $fakeConnection -Command apply-migrations
    Assert-Test (-not $withoutPassword.EnvironmentVariables.ContainsKey('UatBootstrap__SharedPassword')) 'Ambient bootstrap password leaked into migration-only child.'
    $withoutPassword.EnvironmentVariables.Clear()

    # Secure prompting must occur after the complete dry-run branch has exited.
    $dryRun=$deployAst.Find({param($n) $n -is [Management.Automation.Language.IfStatementAst] -and
        $n.Clauses[0].Item1.Extent.Text -eq '$DryRun' -and $n.Extent.Text.Contains('FRESH DATABASE PREFLIGHT PASSED')},$true)
    $prompt=$deployAst.Find({param($n) $n -is [Management.Automation.Language.CommandAst] -and $n.GetCommandName() -eq 'Read-Host'},$true)
    Assert-Test ($null -ne $dryRun -and $null -ne $prompt -and $prompt.Extent.StartOffset -gt $dryRun.Extent.EndOffset) 'Password prompt must be outside and after DryRun.'
    Assert-Test ($prompt.Extent.Text -match '-AsSecureString') 'Interactive credential prompt must be secure.'
    $buildCall=$deployAst.Find({param($n) $n -is [Management.Automation.Language.CommandAst] -and $n.GetCommandName() -eq 'New-ReleaseArtifacts'},$true)
    $backupCall=$deployAst.Find({param($n) $n -is [Management.Automation.Language.CommandAst] -and $n.GetCommandName() -eq 'Invoke-RemoteHelper' -and $n.Extent.Text -match "'Backup'"},$true)
    Assert-Test ($prompt.Extent.StartOffset -gt $buildCall.Extent.EndOffset -and $prompt.Extent.StartOffset -gt $backupCall.Extent.EndOffset) 'Secure password prompt must follow artifact creation and verified backups.'
    $remoteCredentialGuard=$deployAst.Find({param($n) $n -is [Management.Automation.Language.CommandAst] -and $n.GetCommandName() -eq 'Assert-True' -and $n.Extent.Text.Contains('Set protected UatBootstrap__SharedPassword')},$true)
    Assert-Test ($null -ne $remoteCredentialGuard -and $remoteCredentialGuard.Extent.StartOffset -lt $buildCall.Extent.StartOffset) 'Missing SSH-side credentials must still fail before artifact builds.'
    $exits=@($dryRun.FindAll({param($n) $n -is [Management.Automation.Language.ExitStatementAst]},$true))
    Assert-Test ($exits.Count -eq 2 -and @($exits | Where-Object {$_.Extent.Text -ne 'exit 0'}).Count -eq 0) 'Both fresh and normal DryRun paths must terminate before prompting.'
    Assert-Test ($deployAst.Extent.Text.Contains('ZeroFreeBSTR') -and
        $deployAst.Extent.Text.Contains("'UatBootstrap__SharedPassword', `$priorOperationalPassword, 'Process'")) 'Secure prompt cleanup/environment restoration is missing.'

    # Actual native helper + harmless child processes: the bootstrap credential
    # must be absent in the child and restored even when that command fails.
    Invoke-Expression (Find-TestFunction $deployAst 'Invoke-NativeChecked').Extent.Text
    $nativeProbe=Join-Path $testRoot 'native-environment-probe.ps1'
    [IO.File]::WriteAllText($nativeProbe,@'
param([int]$RequestedExitCode)
if(-not [string]::IsNullOrEmpty([Environment]::GetEnvironmentVariable('UatBootstrap__SharedPassword','Process'))){exit 99}
exit $RequestedExitCode
'@)
    [Environment]::SetEnvironmentVariable('UatBootstrap__SharedPassword',$processSentinel,'Process')
    $powershell=(Get-Command powershell.exe -ErrorAction Stop).Source
    Invoke-NativeChecked $powershell @('-NoProfile','-ExecutionPolicy','Bypass','-File',$nativeProbe,'-RequestedExitCode','0') 'Test native failure'
    Assert-Test ([Environment]::GetEnvironmentVariable('UatBootstrap__SharedPassword','Process') -ceq $processSentinel) 'Native success did not restore bootstrap credential.'
    $rejected=$false
    try { Invoke-NativeChecked $powershell @('-NoProfile','-ExecutionPolicy','Bypass','-File',$nativeProbe,'-RequestedExitCode','7') 'Test native failure' }
    catch {$rejected=$_.Exception.Message -eq 'Test native failure (exit code 7).'}
    Assert-Test $rejected 'Native failure was not propagated or child inherited bootstrap credential.'
    Assert-Test ([Environment]::GetEnvironmentVariable('UatBootstrap__SharedPassword','Process') -ceq $processSentinel) 'Native failure did not restore bootstrap credential.'

    # Run the real normal-deployment seed wrapper with strictly in-memory stubs.
    Invoke-Expression (Find-TestFunction $remoteAst 'Invoke-OperationalSeed').Extent.Text
    . (Join-Path $PSScriptRoot 'OperationalUatVerification.ps1')
    function Assert-True {param([bool]$Condition,[string]$Message) Assert-Test $Condition $Message}
    function Assert-DeploymentId {}
    function Invoke-RemoteTimedStep {param([string]$Name,[scriptblock]$Operation) & $Operation}
    function Get-DatabaseConnectionString {return $script:FakeConnection}
    function Get-RhemaOperationalSeedSnapshot {param($ConnectionString,$DatabaseName)
        $script:SnapshotCalls++
        Assert-Test ($ConnectionString -eq $script:ExpectedSnapshotConnection -and $DatabaseName -eq $script:ExpectedSnapshotDatabase) 'Verification used an unexpected target connection.'
        $hash=if($script:Drift -and $script:SnapshotCalls -gt 1){'B'*64}else{'A'*64}
        $failures=if($script:MissingOperationalData){@('Actor.Active:storesofficer')}else{@()}
        return [pscustomobject]@{Counts=@{Actors=16};Fingerprint=$hash;Failures=@($failures);Ready=(-not $script:MissingOperationalData)}
    }
    function Invoke-RhemaFreshApiCli {param($ApiExecutable,$ContentRoot,$ConnectionString,$Command,$TimeoutSeconds,[string]$OperationalUatPassword)
        [void]$script:CliCalls.Add([pscustomobject]@{Command=$Command;Password=$OperationalUatPassword;Connection=$ConnectionString})
        return [pscustomobject]@{Command=$Command;ExitCode=0;OutputSha256=('0'*64)}
    }
    $script:FakeConnection=$fakeConnection
    $script:ExpectedSnapshotConnection=$fakeConnection;$script:ExpectedSnapshotDatabase='RhemaERP_Test'
    $script:SnapshotCalls=0;$script:Drift=$false;$script:MissingOperationalData=$false
    $script:CliCalls=New-Object 'System.Collections.Generic.List[object]'
    [Environment]::SetEnvironmentVariable('UatBootstrap__SharedPassword',$processSentinel,'Process')
    $PackagesRoot=$testRoot;$ApiRoot=Join-Path $testRoot 'api';$DeploymentId='test-normal'
    $normalOutput=@(Invoke-OperationalSeed)
    Assert-Test ($script:CliCalls.Count -eq 1 -and $script:CliCalls[0].Command -eq 'seed-operational-uat' -and
        $script:CliCalls[0].Password -ceq $processSentinel -and $script:SnapshotCalls -eq 1) 'Normal deployment did not seed and verify its explicit active database.'
    Assert-Test ($normalOutput -contains 'OPERATIONAL_SEED|PASS') 'Normal operational verification marker missing.'
    $DeploymentId='test-normal-no-secret';$script:CliCalls.Clear();$script:SnapshotCalls=0
    [Environment]::SetEnvironmentVariable('UatBootstrap__SharedPassword',$null,'Process')
    $normalNoSecretOutput=@(Invoke-OperationalSeed)
    Assert-Test ($script:CliCalls.Count -eq 1 -and
        [string]::IsNullOrEmpty($script:CliCalls[0].Password) -and $script:SnapshotCalls -eq 1) `
        'Existing operational accounts should reconcile without manufacturing or requiring a password.'
    Assert-Test ($normalNoSecretOutput -contains 'OPERATIONAL_SEED|PASS') 'Password-free operational reconciliation marker missing.'
    $DeploymentId='test-normal-incomplete';$script:MissingOperationalData=$true;$rejected=$false
    try { Invoke-OperationalSeed | Out-Null } catch {
        $rejected=$_.Exception.Message -like 'Operational UAT readiness failed:*'
    }
    Assert-Test $rejected 'Normal deployment accepted incomplete operational data.'
    Assert-Test (-not (Test-Path -LiteralPath (Join-Path $testRoot 'operational-test-normal-incomplete/verification.json'))) 'Failed readiness must not persist successful verification evidence.'
    $script:MissingOperationalData=$false
    $normalBranch=$deployAst.Find({param($n) $n -is [Management.Automation.Language.IfStatementAst] -and
        $n.Clauses[0].Item1.Extent.Text -eq '-not $FreshDatabaseName' -and $n.Extent.Text.Contains("'SeedOperational'")},$true)
    Assert-Test ($null -ne $normalBranch) 'Normal deploy orchestration must invoke SeedOperational only outside fresh mode.'
    $preflightFunction=Find-TestFunction $remoteAst 'Invoke-Preflight'
    Assert-Test ($preflightFunction.Extent.Text.Contains('Get-RhemaMissingOperationalActorNames') -and
        $preflightFunction.Extent.Text.Contains('UAT_CREDENTIAL|NOT_REQUIRED') -and
        $preflightFunction.Extent.Text.Contains('$FreshDatabaseName')) `
        'Preflight must require a password only for a fresh target or genuinely missing operational actors.'
    $freshCall=$cutoverAst.Find({param($n) $n -is [Management.Automation.Language.CommandAst] -and $n.GetCommandName() -eq 'Invoke-RhemaFreshDatabaseProvisioning'},$true)
    Assert-Test ($freshCall.Extent.Text -match '-OperationalUatPassword\s+\(Get-RhemaOperationalPassword\)') 'Fresh cutover failed to forward protected credential.'

    # Execute actual fresh orchestration with SQL, CLI and snapshots stubbed.
    foreach($name in @('Assert-RhemaFreshDatabaseName','Get-RhemaFreshConnectionBuilder','Invoke-RhemaFreshDatabaseProvisioning')) {
        Invoke-Expression (Find-TestFunction $freshAst $name).Extent.Text
    }
    function Test-RhemaFreshDatabaseTarget {param($SourceConnectionString,$FreshDatabaseName)}
    function Get-RhemaFreshSeedCounts {param($ConnectionString,$DatabaseName)
        return [ordered]@{BusinessPartnerRoles=8;BusinessPartnerApProfileVersions=5;AccountingBooks=3;Suppliers=0;JournalEntries=0}
    }
    function Invoke-RhemaFreshSql {param($ConnectionString,$ExpectedDatabase,$Sql)
        $table=New-Object System.Data.DataTable
        if($Sql -like '*SELECT MigrationId FROM*') {
            [void]$table.Columns.Add('MigrationId',[string]);[void]$table.Rows.Add('20260916132000_DisposableDevelopmentCurrentModelBaseline')
        } elseif($Sql -like '*InvalidForeignKeys*') {
            foreach($column in @('InvalidForeignKeys','InvalidChecks','CanonicalSchema')){[void]$table.Columns.Add($column,[int])}
            [void]$table.Rows.Add(0,0,1)
        }
        return ,$table
    }
    $stage=Join-Path $testRoot 'stage';[void](New-Item -ItemType Directory -Path $stage)
    foreach($name in @('ErpSystem.Api.exe','ErpSystem.Api.dll','ErpSystem.Data.dll','ErpSystem.Api.runtimeconfig.json')){[IO.File]::WriteAllText((Join-Path $stage $name),'test stub')}
    $target='RhemaERP_Test_Operational'
    $builder=New-Object System.Data.SqlClient.SqlConnectionStringBuilder $fakeConnection;$builder['Initial Catalog']=$target
    $script:ExpectedSnapshotConnection=$builder.ConnectionString;$script:ExpectedSnapshotDatabase=$target
    $script:CliCalls.Clear();$script:SnapshotCalls=0
    $result=Invoke-RhemaFreshDatabaseProvisioning $fakeConnection $target $stage @('20260916132000_DisposableDevelopmentCurrentModelBaseline') (Join-Path $testRoot 'fresh-ok') -OperationalUatPassword $processSentinel
    Assert-Test (($script:CliCalls.Command -join ',') -eq 'apply-migrations,seed-deployment-uat,seed-deployment-uat') 'Fresh deploy must run migrations followed by two deployment seeds.'
    Assert-Test ($script:CliCalls[1].Password -ceq $processSentinel -and $script:CliCalls[2].Password -ceq $processSentinel -and
        [string]::IsNullOrEmpty($script:CliCalls[0].Password)) 'Fresh credential forwarding into seed-only calls regressed.'
    Assert-Test ($script:SnapshotCalls -eq 2 -and $result.OperationalSeed.Ready) 'Fresh seed repeat/readiness checks were skipped.'
    $script:SnapshotCalls=0;$script:Drift=$true;$rejected=$false
    try { Invoke-RhemaFreshDatabaseProvisioning $fakeConnection $target $stage @('20260916132000_DisposableDevelopmentCurrentModelBaseline') (Join-Path $testRoot 'fresh-drift') -OperationalUatPassword $processSentinel | Out-Null }
    catch {$rejected=$_.Exception.Message -like 'Fresh provisioning failed at *'}
    Assert-Test $rejected 'Changed operational fingerprint must halt fresh cutover.'

    . (Join-Path $PSScriptRoot 'New-RhemaVpsPreflightHelper.ps1')
    $helper=Join-Path $testRoot 'packaged-helper.ps1'
    New-RhemaVpsPreflightHelper -RepositoryRoot $repositoryRoot -OutputPath $helper | Out-Null
    $helperAst=Read-TestAst $helper
    foreach($name in @('Get-RhemaMissingUserNames','Get-RhemaMissingOperationalActorNames','Get-RhemaOperationalSeedSnapshot','Assert-RhemaOperationalSeedReadiness','Get-RhemaOperationalPassword','Invoke-OperationalSeed')) {
        [void](Find-TestFunction $helperAst $name)
    }
    foreach($file in Get-ChildItem -LiteralPath $testRoot -Recurse -File) {
        $text=[IO.File]::ReadAllText($file.FullName)
        Assert-Test (-not $text.Contains($processSentinel) -and -not $text.Contains($serviceSentinel) -and -not $text.Contains($databaseSentinel)) 'A credential was persisted in generated settings/evidence/helper.'
    }
    Write-Host 'PASS: credential precedence and missing-actor handling; password-free existing-account reconciliation; isolated child environment; native-child stripping/restoration; secure prompt after DryRun/build/backup; normal/fresh seed wiring; repeat-fingerprint rejection; secret-free evidence; packaged helper parsing.'
} finally {
    [Environment]::SetEnvironmentVariable('UatBootstrap__SharedPassword',$savedPassword,'Process')
    [Environment]::SetEnvironmentVariable('RHEMA_TEST_UNRELATED_SECRET',$savedUnrelated,'Process')
    $resolved=[IO.Path]::GetFullPath($testRoot)
    $temp=[IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\')+'\'
    if($resolved.StartsWith($temp,[StringComparison]::OrdinalIgnoreCase) -and (Split-Path -Leaf $resolved) -like 'rhema-operational-test-*') {
        Remove-Item -LiteralPath $resolved -Recurse -Force -ErrorAction SilentlyContinue
    }
}
