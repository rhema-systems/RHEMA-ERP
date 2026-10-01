[CmdletBinding()]
param()
$ErrorActionPreference='Stop'
$repositoryRoot=Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$testRoot=Join-Path ([IO.Path]::GetTempPath()) ('rhema-qs-preparation-test-'+[guid]::NewGuid().ToString('N'))
$powershell=(Get-Command powershell.exe -ErrorAction Stop).Source
function Assert-QsPreparation {param([bool]$Condition,[string]$Message) if(!$Condition){throw $Message}}
function Invoke-QsTestChild {
 param([string]$File,[string[]]$Arguments,[hashtable]$Environment)
 $start=New-Object Diagnostics.ProcessStartInfo
 $start.FileName=$powershell
 $argsForChild=@('-NoProfile','-NonInteractive','-ExecutionPolicy','Bypass','-File',$File)+$Arguments
 foreach($item in $argsForChild){Assert-QsPreparation (!$item.Contains('"')) 'Unexpected test argument quote.'}
 $start.Arguments=($argsForChild | ForEach-Object {'"'+$_+'"'}) -join ' '
 $start.UseShellExecute=$false;$start.CreateNoWindow=$true;$start.RedirectStandardOutput=$true;$start.RedirectStandardError=$true
 $start.EnvironmentVariables.Remove('UatBootstrap__SharedPassword')
 foreach($name in $Environment.Keys){$start.EnvironmentVariables[$name]=[string]$Environment[$name]}
 $process=New-Object Diagnostics.Process;$process.StartInfo=$start
 try {
  [void]$process.Start();$stdout=$process.StandardOutput.ReadToEndAsync();$stderr=$process.StandardError.ReadToEndAsync()
  if(!$process.WaitForExit(60000)){$process.Kill();throw 'QS preparation test timed out.'}
  [pscustomobject]@{ExitCode=$process.ExitCode;Output=$stdout.Result+"`n"+$stderr.Result}
 }finally{$process.Dispose();$start.EnvironmentVariables.Clear()}
}
try {
 [void][IO.Directory]::CreateDirectory($testRoot)
 . (Join-Path $PSScriptRoot 'FreshDatabaseProvisioning.ps1')
 $probe=Join-Path $testRoot 'EnvironmentProbe.exe'
 Add-Type -OutputAssembly $probe -OutputType ConsoleApplication -ReferencedAssemblies System.Data.dll -TypeDefinition @'
using System;
using System.Data.SqlClient;
public class EnvironmentProbe {
 public static int Main(string[] args) {
  if(args.Length == 0) return 30;
  if(Environment.GetEnvironmentVariable("RHEMA_QS_UNRELATED_SECRET") != null) return 31;
  if(args[0] == "seed-qs-uat") {
   if(Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") != "Test" || Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") != "Test") return 32;
   if(Environment.GetEnvironmentVariable("QsUat__Enabled") != "true") return 33;
   var connection = new SqlConnectionStringBuilder(Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection"));
   var reconcile = connection.InitialCatalog.EndsWith("_Reconciled");
   var autoApprove = connection.InitialCatalog.EndsWith("_AutoApproved") || reconcile;
   if(Environment.GetEnvironmentVariable("QsUat__AutoApprove") != (autoApprove ? "true" : "false")) return 38;
   if(Environment.GetEnvironmentVariable("QsUat__ReconcileUnapprovedDrafts") != (reconcile ? "true" : "false")) return 39;
   if(connection.InitialCatalog != Environment.GetEnvironmentVariable("QsUat__ExpectedDatabase")) return 34;
   if(Environment.GetEnvironmentVariable("UatBootstrap__SharedPassword") != "fake-qs-secret-!42") return 35;
   Console.WriteLine("fake-qs-secret-!42 fake-db-secret-!43");
   if(connection.InitialCatalog.EndsWith("_Failure")) {
     Console.Error.WriteLine("QS_UAT_AUTO_APPROVAL_UNRESOLVED: QS-DEC-008 and QS-DEC-015 require controlled selections.");
     return 17;
   }
  } else {
   if(Environment.GetEnvironmentVariable("QsUat__Enabled") != null || Environment.GetEnvironmentVariable("QsUat__AutoApprove") != null || Environment.GetEnvironmentVariable("QsUat__ReconcileUnapprovedDrafts") != null || Environment.GetEnvironmentVariable("QsUat__ExpectedDatabase") != null || Environment.GetEnvironmentVariable("UatBootstrap__SharedPassword") != null) return 36;
   if(Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") != "FreshDatabaseProvisioning") return 37;
  }
  return 0;
 }
}
'@
 $savedUnrelated=[Environment]::GetEnvironmentVariable('RHEMA_QS_UNRELATED_SECRET','Process')
 $savedPassword=[Environment]::GetEnvironmentVariable('UatBootstrap__SharedPassword','Process')
 try {
  [Environment]::SetEnvironmentVariable('RHEMA_QS_UNRELATED_SECRET','must-not-inherit','Process')
  [Environment]::SetEnvironmentVariable('UatBootstrap__SharedPassword','must-not-inherit','Process')
   foreach($target in @('RhemaERP_VpsTest_GuardTest','RhemaERP_QsUatVerify_GuardTest','RhemaERP_VpsTest_AutoApproved','RhemaERP_VpsTest_Reconciled')) {
    $reconcile=$target.EndsWith('_Reconciled')
    $result=Invoke-RhemaFreshApiCli -ApiExecutable $probe -ContentRoot $testRoot -ConnectionString ('Server=invalid;Database='+$target+';User ID=fake;Password=fake-db-secret-!43') -Command seed-qs-uat -ExpectedQsDatabase $target -OperationalUatPassword 'fake-qs-secret-!42' -TimeoutSeconds 30 -AutoApproveQsUat:($target.EndsWith('_AutoApproved') -or $reconcile) -ReconcileUnapprovedQsDrafts:$reconcile
   $evidence=$result | ConvertTo-Json -Depth 6
   Assert-QsPreparation ($result.ExitCode -eq 0 -and $result.OutputSha256.Length -eq 64) 'Real child did not receive exact QS opt-in environment.'
   Assert-QsPreparation (!$evidence.Contains('fake-qs-secret') -and !$evidence.Contains('fake-db-secret')) 'Raw child credentials leaked into evidence.'
  }
  $result=Invoke-RhemaFreshApiCli -ApiExecutable $probe -ContentRoot $testRoot -ConnectionString 'Server=invalid;Database=RhemaERP_Test' -Command apply-migrations -TimeoutSeconds 30
  Assert-QsPreparation ($result.ExitCode -eq 0) 'Migration child inherited QS opt-in or ambient credential.'
  $failed=$false
  try { Invoke-RhemaFreshApiCli -ApiExecutable $probe -ContentRoot $testRoot -ConnectionString 'Server=invalid;Database=RhemaERP_VpsTest_Failure;User ID=fake;Password=fake-db-secret-!43' -Command seed-qs-uat -ExpectedQsDatabase 'RhemaERP_VpsTest_Failure' -OperationalUatPassword 'fake-qs-secret-!42' -TimeoutSeconds 30 | Out-Null }
  catch {
   $safe=$_.Exception.Data['SafeCliEvidence']
   $errorEvidence=$_.Exception.Message+($safe | ConvertTo-Json -Depth 6)
   $failed=$safe.ExitCode -eq 17 -and $safe.OutputSha256.Length -eq 64 -and
       @($safe.QsDecisionCodes) -join ',' -eq 'QS-DEC-008,QS-DEC-015'
   Assert-QsPreparation (!$errorEvidence.Contains('fake-qs-secret') -and !$errorEvidence.Contains('fake-db-secret')) 'Failed CLI exposed raw child credentials.'
  }
  Assert-QsPreparation $failed 'Failed CLI lost its sanitized child evidence.'
 }finally{
  [Environment]::SetEnvironmentVariable('RHEMA_QS_UNRELATED_SECRET',$savedUnrelated,'Process')
  [Environment]::SetEnvironmentVariable('UatBootstrap__SharedPassword',$savedPassword,'Process')
 }
 foreach($case in @(
  @{Expected='RhemaERP_VpsTest_Expected';Actual='RhemaERP_VpsTest_Other'},
  @{Expected='RhemaERP_Production';Actual='RhemaERP_Production'},
  @{Expected='rhemaERP_VpsTest_WrongCase';Actual='rhemaERP_VpsTest_WrongCase'},
  @{Expected='';Actual='RhemaERP_VpsTest_Expected'}
 )) {
  $rejected=$false
  try {Invoke-RhemaFreshApiCli -ApiExecutable 'must-never-launch.exe' -ContentRoot $testRoot -ConnectionString ('Server=invalid;Database='+$case.Actual) -Command seed-qs-uat -ExpectedQsDatabase $case.Expected | Out-Null}
  catch {$rejected=$_.Exception.Message -eq 'QS preparation requires the exact explicitly selected test database.'}
  Assert-QsPreparation $rejected 'Unsafe QS target was not rejected before process launch.'
 }
 Write-Output 'PASS|Actual CLI child environment: exact test target, opt-in, no inherited secret, raw output suppression, and pre-launch mismatch rejection.'

 $scripts=Join-Path $testRoot 'scripts';$vps=Join-Path $scripts 'vps';[void][IO.Directory]::CreateDirectory($vps)
 Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Initialize-QsUat.ps1') -Destination $vps
 # Only the initializer orchestration is real here. Its CLI dependency is a
 # harmless recorder; the actual CLI process/environment was tested above.
 [IO.File]::WriteAllText((Join-Path $vps 'FreshDatabaseProvisioning.ps1'),@'
function Invoke-RhemaFreshApiCli {
 param($ApiExecutable,$ContentRoot,$ConnectionString,$Command,$ExpectedQsDatabase,$OperationalUatPassword,$TimeoutSeconds,[switch]$AutoApproveQsUat,[switch]$ReconcileUnapprovedQsDrafts)
 if($Command -ne 'seed-qs-uat' -or $ExpectedQsDatabase -ne 'RhemaERP_VpsTest_GuardTest'){throw 'Initializer passed incorrect protected CLI inputs.'}
 if([string]::IsNullOrWhiteSpace($OperationalUatPassword) -and $env:RHEMA_QS_EXISTING_ACTORS -ne '1'){
  $error=New-Object InvalidOperationException 'CLI failed; raw output suppressed.'
  $error.Data['SafeCliEvidence']=[pscustomobject]@{Command=$Command;ExitCode=17;OutputSha256='sanitized-password-required';GuardCodes=@('QS_UAT_PASSWORD_REQUIRED');QsDecisionCodes=@();QsStages=@('QS_UAT_STAGE|ACTORS');MissingServices=@();SqlErrorNumbers=@();ExceptionTypes=@('System.InvalidOperationException')}
  throw $error
 }
 if(-not [string]::IsNullOrWhiteSpace($OperationalUatPassword) -and $OperationalUatPassword -ne 'fake-qs-secret-!42'){throw 'Initializer passed an unexpected protected password.'}
 if($ReconcileUnapprovedQsDrafts -and !$AutoApproveQsUat){throw 'Unsafe reconciliation combination.'}
 if($env:RHEMA_QS_INIT_FAIL -eq '1'){
  $error=New-Object InvalidOperationException 'CLI failed; raw output suppressed.'
  $error.Data['SafeCliEvidence']=[pscustomobject]@{Command=$Command;ExitCode=17;OutputSha256='sanitized-failed-output';GuardCodes=@('QS_UAT_AUTO_APPROVAL_UNRESOLVED');QsDecisionCodes=@('QS-DEC-007');QsStages=@('QS_UAT_STAGE|CONFIGURATION');MissingServices=@();SqlErrorNumbers=@();ExceptionTypes=@('System.InvalidOperationException')}
  throw $error
 }
 $report=@{Database=$ExpectedQsDatabase;Preparation=@{ProfileId=[Guid]::NewGuid().ToString();IndependentConfigurationReviewRequired=(!$AutoApproveQsUat);Unresolved=@();PreparedDecisions=@(1..17 | ForEach-Object {'QS-DEC-'+$_.ToString('000')})}}
 if($env:RHEMA_QS_INIT_FALSE_SUCCESS -eq '1'){$report.Preparation.IndependentConfigurationReviewRequired=$true}
 [IO.File]::WriteAllText((Join-Path $ContentRoot 'qs-uat-preparation.json'),($report | ConvertTo-Json -Depth 5))
 [IO.File]::WriteAllText($env:RHEMA_QS_INIT_LAUNCH,'launched')
 [pscustomobject]@{Command=$Command;ExitCode=0;OutputSha256='not-raw-output'}
}
'@)
 $harness=Join-Path $testRoot 'InitializerHarness.ps1'
 [IO.File]::WriteAllText($harness,@'
param([string]$Initializer,[string]$OutputDirectory,[string]$ExpectedDatabase,[string]$PasswordSource,[switch]$AutoApproveQsUat,[switch]$ReconcileUnapprovedQsDrafts)
$ErrorActionPreference='Stop'
$global:QsPromptCount=0
function Get-ItemProperty {
 param([string]$LiteralPath)
 if($LiteralPath -ne 'HKLM:\SYSTEM\CurrentControlSet\Services\RhemaERPAPI\Parameters'){throw 'Unexpected configuration read.'}
 $entries=@('ConnectionStrings__DefaultConnection=Server=invalid;Database=RhemaERP_VpsTest_GuardTest;User ID=fake;Password=fake-db-secret-!43')
 if($PasswordSource -eq 'Service'){$entries+=@('UatBootstrap__SharedPassword=fake-qs-secret-!42')}
 [pscustomobject]@{AppEnvironment=@();AppEnvironmentExtra=$entries}
}
function Test-Path {
 param([string]$LiteralPath)
 if($LiteralPath -eq 'C:\RhemaERP\services\api\RhemaERPAPI.xml'){return $false}
 Microsoft.PowerShell.Management\Test-Path -LiteralPath $LiteralPath
}
function Read-Host {
 param([string]$Prompt,[switch]$AsSecureString)
 if(!$AsSecureString -or $PasswordSource -notin @('Prompt','PromptAfterBlank')){throw 'Unexpected or insecure password prompt.'}
 $global:QsPromptCount++
 if($PasswordSource -eq 'PromptAfterBlank' -and $global:QsPromptCount -eq 1){return [Security.SecureString]::new()}
 ConvertTo-SecureString 'fake-qs-secret-!42' -AsPlainText -Force
}
& $Initializer -ExpectedDatabase $ExpectedDatabase -OutputDirectory $OutputDirectory -AutoApproveQsUat:$AutoApproveQsUat -ReconcileUnapprovedQsDrafts:$ReconcileUnapprovedQsDrafts
if($PasswordSource -in @('Prompt','PromptAfterBlank')){Write-Output ('PROMPT_COUNT|'+$global:QsPromptCount)}
'@)
  foreach($source in @('Process','Service','ExistingNoPassword','Prompt','PromptAfterBlank','Mismatch','Failure','AutoApproval','AutoReconcile','FalseSuccess')) {
  $outputDirectory=Join-Path $testRoot ('init-'+$source);$launch=Join-Path $testRoot ($source+'-launched.txt')
  $environment=@{RHEMA_QS_INIT_LAUNCH=$launch}
   if($source -in @('Process','Failure','AutoApproval','AutoReconcile','FalseSuccess')){$environment.UatBootstrap__SharedPassword='fake-qs-secret-!42'}
  if($source -eq 'ExistingNoPassword'){$environment.RHEMA_QS_EXISTING_ACTORS='1'}
  if($source -eq 'Failure'){$environment.RHEMA_QS_INIT_FAIL='1'}
  if($source -eq 'FalseSuccess'){$environment.RHEMA_QS_INIT_FALSE_SUCCESS='1'}
  $target=if($source -eq 'Mismatch'){'RhemaERP_VpsTest_Wrong'}else{'RhemaERP_VpsTest_GuardTest'}
  $childArgs=@('-Initializer',(Join-Path $vps 'Initialize-QsUat.ps1'),'-OutputDirectory',$outputDirectory,'-ExpectedDatabase',$target,'-PasswordSource',$source)
   if($source -in @('AutoApproval','AutoReconcile','FalseSuccess')){$childArgs+='-AutoApproveQsUat'}
   if($source -eq 'AutoReconcile'){$childArgs+='-ReconcileUnapprovedQsDrafts'}
  $child=Invoke-QsTestChild -File $harness -Arguments $childArgs -Environment $environment
  Assert-QsPreparation (!$child.Output.Contains('fake-qs-secret') -and !$child.Output.Contains('fake-db-secret')) 'Initializer emitted a credential.'
  if($source -eq 'Mismatch') {
   Assert-QsPreparation ($child.ExitCode -ne 0 -and !(Test-Path -LiteralPath $launch) -and !(Test-Path -LiteralPath $outputDirectory)) 'Initializer launched or created artifacts before target rejection.'
  }elseif($source -eq 'Failure'){
   Assert-QsPreparation ($child.ExitCode -ne 0) 'Failed initializer reported success.'
   $failedReports=@(Get-ChildItem -LiteralPath $outputDirectory -Recurse -Filter 'command-evidence.json')
   Assert-QsPreparation ($failedReports.Count -eq 1) 'Initializer lost sanitized CLI failure evidence.'
   $failedReport=Get-Content -LiteralPath $failedReports[0].FullName -Raw | ConvertFrom-Json
   Assert-QsPreparation ($failedReport.ExitCode -eq 17 -and $failedReport.OutputSha256 -eq 'sanitized-failed-output') 'Initializer failure evidence was not retained accurately.'
   Assert-QsPreparation ($child.Output.Contains('QS_UAT_FAILURE|') -and $child.Output.Contains('QS-DEC-007')) 'Initializer did not print its sanitized actionable failure summary.'
  }elseif($source -eq 'FalseSuccess'){
   Assert-QsPreparation ($child.ExitCode -ne 0 -and !$child.Output.Contains('QS_CONFIGURATION|AUTO_APPROVED_TEST_ONLY')) 'Initializer claimed approval despite an unapproved report.'
  }else{
   Assert-QsPreparation ($child.ExitCode -eq 0 -and (Test-Path -LiteralPath $launch)) ('Initializer credential source failed: '+$source)
   if($source -eq 'Prompt'){Assert-QsPreparation ($child.Output.Contains('PROMPT_COUNT|1')) 'Valid prompted password was requested more than once.'}
   if($source -eq 'PromptAfterBlank'){Assert-QsPreparation ($child.Output.Contains('PROMPT_COUNT|2') -and $child.Output.Contains('password cannot be empty')) 'Blank prompted password was not rejected and requested again.'}
   if($source -eq 'ExistingNoPassword'){Assert-QsPreparation (!$child.Output.Contains('QS_UAT_PASSWORD|REQUIRED_FOR_MISSING_ACTORS')) 'Existing QS actors caused an unnecessary password prompt.'}
   if($source -eq 'AutoApproval'){Assert-QsPreparation ($child.Output.Contains('QS_CONFIGURATION|AUTO_APPROVED_TEST_ONLY')) 'Explicit auto-approval was not verified.'}
   foreach($artifact in Get-ChildItem -LiteralPath $outputDirectory -Recurse -File) {
    $text=Get-Content -LiteralPath $artifact.FullName -Raw
    Assert-QsPreparation (!$text.Contains('fake-qs-secret') -and !$text.Contains('fake-db-secret')) 'Initializer persisted a credential.'
   }
  }
 }
 Write-Output 'PASS|Windows PowerShell initializer: existing actors need no password; missing actors use one secure prompt with blank-entry retry; process/service secret sources, target rejection before launch, and sanitized artifacts.'

 $wrapper=Join-Path $scripts 'Deploy-QsUatVps.ps1'
 Copy-Item -LiteralPath (Join-Path $repositoryRoot 'scripts\Deploy-QsUatVps.ps1') -Destination $wrapper
 [IO.File]::WriteAllText((Join-Path $scripts 'Deploy-RhemaVps.ps1'),@'
param([string]$Environment,[switch]$LocalVps,[string]$ExpectedCommit,[string]$PublicBaseUrl)
Add-Content -LiteralPath $env:RHEMA_QS_PREP_LOG -Value 'Deploy'
exit ([int]$env:RHEMA_QS_PREP_DEPLOY_EXIT)
'@)
 [IO.File]::WriteAllText((Join-Path $vps 'Initialize-QsUat.ps1'),@'
param([string]$ExpectedDatabase,[switch]$AutoApproveQsUat,[switch]$ReconcileUnapprovedQsDrafts)
if($ExpectedDatabase -ne 'RhemaERP_VpsTest_GuardTest'){exit 88}
Add-Content -LiteralPath $env:RHEMA_QS_PREP_LOG -Value 'Prepare'
if($AutoApproveQsUat){Add-Content -LiteralPath $env:RHEMA_QS_PREP_LOG -Value 'AutoApprove'}
if($ReconcileUnapprovedQsDrafts){Add-Content -LiteralPath $env:RHEMA_QS_PREP_LOG -Value 'Reconcile'}
exit ([int]$env:RHEMA_QS_PREP_SEED_EXIT)
'@)
 [IO.File]::WriteAllText((Join-Path $vps 'Get-QsUatReadiness.ps1'),@'
param([string]$ExpectedDatabase,[string]$PublicBaseUrl)
if($ExpectedDatabase -ne 'RhemaERP_VpsTest_GuardTest'){exit 89}
Add-Content -LiteralPath $env:RHEMA_QS_PREP_LOG -Value 'Report'
exit 0
'@)
 foreach($case in @(
  @{Name='deploy-failed';Deploy=4;Seed=0;Exit=1;Steps='Deploy'},
  @{Name='preparation-failed';Deploy=0;Seed=7;Exit=1;Steps='Deploy,Prepare'},
  @{Name='prepared';Deploy=0;Seed=0;Exit=0;Steps='Deploy,Prepare,Report'},
   @{Name='auto-approved';Deploy=0;Seed=0;Exit=0;Steps='Deploy,Prepare,AutoApprove,Report'},
   @{Name='auto-reconciled';Deploy=0;Seed=0;Exit=0;Steps='Deploy,Prepare,AutoApprove,Reconcile,Report'}
 )) {
  $log=Join-Path $testRoot ($case.Name+'.log')
  $wrapperArgs=@('-PrepareQsUat','-ExpectedDatabase','RhemaERP_VpsTest_GuardTest')
   if($case.Name -in @('auto-approved','auto-reconciled')){$wrapperArgs+='-AutoApproveQsUat'}
   if($case.Name -eq 'auto-reconciled'){$wrapperArgs+='-ReconcileUnapprovedQsDrafts'}
  $child=Invoke-QsTestChild -File $wrapper -Arguments $wrapperArgs -Environment @{RHEMA_QS_PREP_LOG=$log;RHEMA_QS_PREP_DEPLOY_EXIT=$case.Deploy;RHEMA_QS_PREP_SEED_EXIT=$case.Seed}
  $steps=(Get-Content -LiteralPath $log) -join ','
  Assert-QsPreparation ($child.ExitCode -eq $case.Exit -and $steps -eq $case.Steps) ('PrepareQsUat branch sequencing failed: '+$case.Name)
  if($case.Exit){Assert-QsPreparation (!$child.Output.Contains('QS_RELEASE_DEPLOYMENT|PASS')) 'Failed preparation falsely reported release success.'}
  if($case.Name -eq 'preparation-failed'){Assert-QsPreparation ($child.Output -match 'preparation (failed|stopped)' -and $child.Output -match 'report was not run' -and $child.Output -notmatch 'QS prerequisite report failed') 'Preparation failure was mislabeled as a report failure.'}
 }
 Write-Output 'PASS|Windows PowerShell PrepareQsUat wrapper: deployment/preparation failure short-circuit and success sequencing.'
 $log=Join-Path $testRoot 'invalid-switch.log'
 $child=Invoke-QsTestChild -File $wrapper -Arguments @('-AutoApproveQsUat') -Environment @{RHEMA_QS_PREP_LOG=$log}
 Assert-QsPreparation ($child.ExitCode -ne 0 -and !(Test-Path -LiteralPath $log)) 'Auto-approval without preparation must stop before deployment.'
 $child=Invoke-QsTestChild -File $wrapper -Arguments @('-PrepareQsUat','-ReconcileUnapprovedQsDrafts') -Environment @{RHEMA_QS_PREP_LOG=$log}
 Assert-QsPreparation ($child.ExitCode -ne 0) 'Draft reconciliation without explicit auto-approval was accepted.'
}finally{
 if(Test-Path -LiteralPath $testRoot){
  $resolved=[IO.Path]::GetFullPath($testRoot)
  $prefix=Join-Path ([IO.Path]::GetFullPath([IO.Path]::GetTempPath())) 'rhema-qs-preparation-test-'
  Assert-QsPreparation ($resolved.StartsWith($prefix,[StringComparison]::OrdinalIgnoreCase)) 'Unsafe temporary cleanup path.'
  Remove-Item -LiteralPath $resolved -Recurse -Force
 }
}
