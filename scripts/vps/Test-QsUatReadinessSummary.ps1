[CmdletBinding()]
param()
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'QsUatReadinessSummary.ps1')
function Assert-QsReport {param([bool]$Condition,[string]$Message) if(!$Condition){throw $Message}}
$missing=@(
 [pscustomobject]@{Category='Actor';Reference='uat.qs.contractor';Finding='Missing'},
 [pscustomobject]@{Category='Actor';Reference='uat.qs.consultant';Finding='No active DEFAULT membership'},
 [pscustomobject]@{Category='Actor';Reference='uat.qs.engineer';Finding='Inactive'},
 [pscustomobject]@{Category='QS profile';Reference='DEFAULT';Finding='No published effective QS configuration profile'},
 [pscustomobject]@{Category='QS decision';Reference='QS-DEC-003';Finding='Profile v1; Status=Proposed; Approval=Pending; Evidence=Missing; Currently effective'},
 [pscustomobject]@{Category='QS workflow';Reference='QS_BOQ';Finding='No active published definition; inspect configured QS decision binding'},
 [pscustomobject]@{Category='Estate ready land';Reference='Available for new project';Finding='Count=0; No selectable demarcated land'}
)
$result=Get-RhemaQsUatReadinessSummary -Prerequisites $missing -OperationalBaselineReady $true
Assert-QsReport ($result.PrerequisiteSetupRequired -and $result.MissingPrerequisites.Count -eq 6) 'Missing actor/land/profile/workflow prerequisites were not reported.'
Assert-QsReport ($result.ReadyLandCount -eq 0) 'Zero ready land was not reported.'
Assert-QsReport ($result.ConfigurationReviewRequired -and @($result.ConfigurationReview | Where-Object Reference -eq 'QS-DEC-003').Count -eq 1) 'Proposed decision incorrectly passed configuration review.'
$configured=@(
 [pscustomobject]@{Category='Actor';Reference='uat.qs.contractor';Finding='Active; permissions and assignments still require verification'},
 [pscustomobject]@{Category='QS profile';Reference='UAT v1';Finding='Published and effective; verify each decision and route below'},
 [pscustomobject]@{Category='QS decision';Reference='QS-DEC-003';Finding='Profile v1; Status=Approved; Approval=Approved; Evidence=Verified; Currently effective'},
 [pscustomobject]@{Category='QS workflow';Reference='QS_BOQ';Finding='UAT BOQ approval'},
 [pscustomobject]@{Category='Estate ready land';Reference='Available for new project';Finding='Count=12; Select the required portion'}
)
$result=Get-RhemaQsUatReadinessSummary -Prerequisites $configured -OperationalBaselineReady $true
Assert-QsReport (!$result.PrerequisiteSetupRequired -and $result.MissingPrerequisites.Count -eq 0 -and $result.ReadyLandCount -eq 12) 'Configured inventory was classified incorrectly.'
Assert-QsReport ($result.ConfigurationReviewRequired -and $result.ConfigurationReview.Count -ge 3) 'Inventory must not claim policy/performer or end-to-end verification.'
$result=Get-RhemaQsUatReadinessSummary -Prerequisites $configured -OperationalBaselineReady $false
Assert-QsReport $result.PrerequisiteSetupRequired 'A failed operational baseline was hidden.'
foreach($path in @('Get-QsUatReadiness.ps1','QsUatReadinessSummary.ps1')) {
 $tokens=$null;$parseErrors=$null
 [void][Management.Automation.Language.Parser]::ParseFile((Join-Path $PSScriptRoot $path),[ref]$tokens,[ref]$parseErrors)
 Assert-QsReport (!$parseErrors.Count) ('PowerShell parse failed: '+$path)
}
Write-Output 'PASS|QS readiness summary: missing prerequisites, ready land, decision review and no false green status.'
