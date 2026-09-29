# This inventory cannot approve policy or prove a configured workflow's performers.
function Get-RhemaQsUatReadinessSummary {
    param([object[]]$Prerequisites,[bool]$OperationalBaselineReady)
    $missing=@($Prerequisites | Where-Object {
        ($_.Category -eq 'Actor' -and $_.Finding -notlike 'Active;*') -or
        ($_.Category -in @('Contractor partner','Consultant partner') -and $_.Finding -like 'Missing*') -or
        ($_.Category -eq 'QS profile' -and $_.Finding -like 'No published effective*') -or
        ($_.Category -eq 'QS workflow' -and $_.Finding -like 'No active published*') -or
        ($_.Category -eq 'QS decision' -and $_.Finding -like 'Missing decision*') -or
        ($_.Category -eq 'Estate ready land' -and $_.Finding -like 'Count=0;*')
    })
    $review=@($Prerequisites | Where-Object {
        $_.Category -eq 'QS profile' -or
        ($_.Category -eq 'QS decision' -and
            ($_.Finding -notlike '*Status=Approved; Approval=Approved; Evidence=Verified; Currently effective' ))
    } | ForEach-Object {
        [pscustomobject]@{Reference=$_.Reference;Finding=$_.Finding;Route='/administration/project-management/quantity-survey-config'}
    })
    $review+=@(
        [pscustomobject]@{Reference='Workflow bindings and performers';Finding='Verify the selected QS decision routes, independent stage performers and authority limits; an active definition alone is insufficient.';Route='/administration/workflow'},
        [pscustomobject]@{Reference='Rates, evidence and Finance';Finding='Confirm approved rates, required DMS/report templates, supplier accounting mappings, applicable tax and open posting period before the relevant UAT stage.';Route='/administration/project-management/quantity-survey-config'},
        [pscustomobject]@{Reference='Project and contract assignments';Finding='Assign the preparer, reviewer, engineer, approver, contractor and consultant to the fresh UAT project/contract as required.';Route='/development/projects'}
    )
    $land=@($Prerequisites | Where-Object { $_.Category -eq 'Estate ready land' })
    $readyLandCount=$null
    if($land.Count -eq 1 -and $land[0].Finding -match '^Count=(\d+);'){$readyLandCount=[int]$Matches[1]}
    [pscustomobject]@{
        PrerequisiteSetupRequired=(!$OperationalBaselineReady -or $missing.Count -gt 0)
        MissingPrerequisites=$missing
        ReadyLandCount=$readyLandCount
        ConfigurationReviewRequired=$true
        ConfigurationReview=$review
    }
}
