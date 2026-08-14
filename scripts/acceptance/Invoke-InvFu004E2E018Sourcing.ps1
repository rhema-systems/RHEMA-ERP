[CmdletBinding()]
param([string]$ApiBaseUrl = 'http://127.0.0.1:5100')

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$apiProject = Join-Path $repoRoot 'src\ErpSystem.Api\ErpSystem.Api.csproj'
$tenantId = [Guid]'00000000-0000-0000-0000-000000000001'
$prId = [Guid]'460760cf-865e-41f0-92da-9380ddae5d1c'
$supplierId = [Guid]'1195675f-2229-4a3c-9484-88e4169afbfe'
$planItemId = [Guid]'b33f0115-4221-4b63-b1e6-49d8faf8bac4'
$inventoryItemId = [Guid]'00000008-0000-0000-0000-000000000003'
$actors = @(
    @{ Id=[Guid]'58cafd8b-42ce-4f67-0dbb-08de862e82ee'; UserName='admin' },
    @{ Id=[Guid]'3307b079-7f92-4fdd-0dc1-08de862e82ee'; UserName='external' },
    @{ Id=[Guid]'e6457120-5070-440b-9143-8b3b02896c82'; UserName='proc-plan-final-approver' },
    @{ Id=[Guid]'45eacad1-fff9-40f4-91fb-a0e72e2fa7f7'; UserName='proc-plan-procurement-approver' }
)

function Add-DbParameter {
    param([System.Data.SqlClient.SqlCommand]$Command,[string]$Name,$Value)
    $Command.Parameters.AddWithValue($Name,$(if($null -eq $Value){[DBNull]::Value}else{$Value}))
}
function Invoke-DbTable {
    param([System.Data.SqlClient.SqlConnection]$Connection,[string]$Sql,[hashtable]$Parameters=@{})
    $command=$Connection.CreateCommand();$command.CommandText=$Sql;$command.CommandTimeout=60
    foreach($entry in $Parameters.GetEnumerator()){$null=Add-DbParameter $command "@$($entry.Key)" $entry.Value}
    $table=[System.Data.DataTable]::new();$adapter=[System.Data.SqlClient.SqlDataAdapter]::new($command);$null=$adapter.Fill($table)
    Write-Output -NoEnumerate $table
}
function Invoke-DbScalar {
    param([System.Data.SqlClient.SqlConnection]$Connection,[string]$Sql,[hashtable]$Parameters=@{})
    $command=$Connection.CreateCommand();$command.CommandText=$Sql
    foreach($entry in $Parameters.GetEnumerator()){$null=Add-DbParameter $command "@$($entry.Key)" $entry.Value}
    $command.ExecuteScalar()
}
function Invoke-DbNonQuery {
    param([System.Data.SqlClient.SqlConnection]$Connection,[string]$Sql,[hashtable]$Parameters=@{})
    $command=$Connection.CreateCommand();$command.CommandText=$Sql
    foreach($entry in $Parameters.GetEnumerator()){$null=Add-DbParameter $command "@$($entry.Key)" $entry.Value}
    $command.ExecuteNonQuery()
}
function Invoke-JsonApi {
    param([string]$Method,[string]$Path,[string]$Token,$Body,[int[]]$ExpectedStatus=@(200))
    $headers=@{'X-Correlation-ID'="INV-FU-004-E2E018-SOURCING-$([Guid]::NewGuid().ToString('N'))"}
    if($Token){$headers.Authorization="Bearer $Token"}
    $parameters=@{Uri="$ApiBaseUrl$Path";Method=$Method;Headers=$headers;SkipHttpErrorCheck=$true;TimeoutSec=120}
    if($null -ne $Body){$parameters.ContentType='application/json';$parameters.Body=$Body|ConvertTo-Json -Depth 30 -Compress}
    $response=Invoke-WebRequest @parameters
    $content=if($response.Content -is [byte[]]){[Text.Encoding]::UTF8.GetString($response.Content)}else{[string]$response.Content}
    if($response.StatusCode -notin $ExpectedStatus){throw "$Method $Path returned $($response.StatusCode): $content"}
    if([string]::IsNullOrWhiteSpace($content)){return $null}
    $content|ConvertFrom-Json
}
function Login-Actor([string]$UserName,[string]$Password){
    $result=Invoke-JsonApi POST '/api/auth/login' '' @{username=$UserName;password=$Password;rememberMe=$false}
    if([string]::IsNullOrWhiteSpace($result.token)){throw "Login did not issue a token for $UserName."}
    [string]$result.token
}
function New-ExternalEvidence([string]$Reference,[string]$Label){
    @{referenceKind=2;reference=$Reference;label=$Label}
}
function Complete-Workflow([Guid]$InstanceId,[string]$ApproverToken,[string]$Comment){
    $pending=Invoke-JsonApi GET '/api/Workflow/approvals/pending' $ApproverToken $null
    $approval=@($pending.data)|Where-Object{$_.stepInstance.workflowInstanceId -eq $InstanceId -or $_.stepInstance.workflowInstance.id -eq $InstanceId}|Select-Object -First 1
    if($approval){
        $null=Invoke-JsonApi POST "/api/Workflow/approvals/$($approval.id)/process" $ApproverToken @{action=0;comments=$Comment}
        return
    }
    $approvalId=Invoke-DbScalar $connection 'SELECT TOP(1) wa.Id FROM WorkflowApprovals wa JOIN WorkflowStepInstances ws ON ws.Id=wa.StepInstanceId WHERE ws.WorkflowInstanceId=@instanceId AND wa.Status=0 AND wa.IsDeleted=0 ORDER BY wa.CreatedAt DESC;' @{instanceId=$InstanceId}
    if($approvalId){
        $null=Invoke-JsonApi POST "/api/Workflow/approvals/$approvalId/process" $ApproverToken @{action=0;comments=$Comment}
        return
    }
    $stepId=Invoke-DbScalar $connection 'SELECT TOP(1) Id FROM WorkflowStepInstances WHERE WorkflowInstanceId=@instanceId AND Status IN (0,1) AND IsDeleted=0 ORDER BY CreatedAt;' @{instanceId=$InstanceId}
    if(-not $stepId){throw "No active shared-workflow step exists for $InstanceId."}
    $processed=Invoke-JsonApi POST "/api/Workflow/steps/$stepId/process" $ApproverToken @{action=0;comments=$Comment}
    if(-not $processed.data.success){throw "Shared workflow did not complete: $($processed.data.message)"}
}

$secretRows=dotnet user-secrets list --project $apiProject --json|ConvertFrom-Json
$secretObject=$secretRows|Where-Object{$_ -and $_.PSObject.Properties.Name -contains 'ConnectionStrings:DefaultConnection'}|Select-Object -First 1
if(-not $secretObject){throw 'The configured API database connection was not found in user secrets.'}
Add-Type -AssemblyName System.Data
$connection=[System.Data.SqlClient.SqlConnection]::new([string]$secretObject.'ConnectionStrings:DefaultConnection');$connection.Open()
$identityAssembly=Get-ChildItem 'C:\Program Files\dotnet\shared\Microsoft.AspNetCore.App' -Recurse -Filter Microsoft.Extensions.Identity.Core.dll|Where-Object{$_.Directory.Name -like '8.*'}|Sort-Object{[version]$_.Directory.Name} -Descending|Select-Object -First 1
try{Add-Type -Path $identityAssembly.FullName -ErrorAction Stop}catch [System.Management.Automation.RuntimeException]{}
$hasher=[Microsoft.AspNetCore.Identity.PasswordHasher[object]]::new();$temporaryPassword="Inv!$([Guid]::NewGuid().ToString('N'))aA7";$temporaryHash=$hasher.HashPassword([object]::new(),$temporaryPassword)
$passwordBackups=@{};$addedRoleMemberships=[Collections.Generic.List[object]]::new()
try{
    foreach($actor in $actors){
        $row=Invoke-DbTable $connection 'SELECT PasswordHash,AccessFailedCount,LockoutEnd FROM Users WHERE Id=@id AND TenantId=@tenantId AND IsActive=1;' @{id=$actor.Id;tenantId=$tenantId}
        if($row.Rows.Count -ne 1){throw "Acceptance actor $($actor.UserName) is unavailable."}
        $passwordBackups[$actor.Id]=@{PasswordHash=if($row.Rows[0].IsNull('PasswordHash')){$null}else{[string]$row.Rows[0].PasswordHash};AccessFailedCount=[int]$row.Rows[0].AccessFailedCount;LockoutEnd=if($row.Rows[0].IsNull('LockoutEnd')){$null}else{$row.Rows[0].LockoutEnd}}
        $null=Invoke-DbNonQuery $connection 'UPDATE Users SET PasswordHash=@hash,AccessFailedCount=0,LockoutEnd=NULL WHERE Id=@id;' @{id=$actor.Id;hash=$temporaryHash}
    }
    foreach($grant in @(
        @{UserId=[Guid]'e6457120-5070-440b-9143-8b3b02896c82';RoleName='TDC_EVALUATOR'},
        @{UserId=[Guid]'45eacad1-fff9-40f4-91fb-a0e72e2fa7f7';RoleName='TDC_HEAD_OF_PROCUREMENT'},
        @{UserId=[Guid]'45eacad1-fff9-40f4-91fb-a0e72e2fa7f7';RoleName='TDC_MANAGING_DIRECTOR'}
    )){
        $roleId=Invoke-DbScalar $connection 'SELECT Id FROM AspNetRoles WHERE Name=@name;' @{name=$grant.RoleName}
        if(-not $roleId){throw "Required role $($grant.RoleName) is unavailable."}
        $exists=[int](Invoke-DbScalar $connection 'SELECT COUNT(1) FROM UserRoles WHERE UserId=@userId AND RoleId=@roleId;' @{userId=$grant.UserId;roleId=[Guid]$roleId})
        if($exists -eq 0){$null=Invoke-DbNonQuery $connection 'INSERT INTO UserRoles(UserId,RoleId) VALUES(@userId,@roleId);' @{userId=$grant.UserId;roleId=[Guid]$roleId};$addedRoleMemberships.Add([pscustomobject]@{UserId=$grant.UserId;RoleId=[Guid]$roleId})}
    }
    $tokens=@{};foreach($actor in $actors){$tokens[$actor.UserName]=Login-Actor $actor.UserName $temporaryPassword}
    $adminToken=$tokens['admin'];$supplierToken=$tokens['external']

    $eligibility=Invoke-JsonApi POST "/api/procurement/supplier-validation/rfq/$supplierId" $adminToken @{}
    if(-not $eligibility.isValid){throw "Supplier eligibility blocked: $($eligibility.errors -join '; ')"}

    $effectivePolicy=Invoke-JsonApi GET '/api/procurement/policy-sets/effective?code=TDC-INV-E2E018-ACTIVE' $adminToken $null
    $workflowRow=Invoke-DbTable $connection @'
SELECT TOP (1) wd.Id,wd.LifecycleStatus
FROM WorkflowDefinitions wd
JOIN WorkflowEntityTypes wet ON wet.Id=wd.EntityTypeId
WHERE wd.TenantId=@tenantId AND wd.IsDeleted=0 AND wet.IsDeleted=0
  AND wet.Code='TENDER_EVALUATION' AND wd.LifecycleStatus=1 AND wd.IsActive=1
ORDER BY wd.Version DESC,wd.PublishedAt DESC;
'@ @{tenantId=$tenantId}
    if($workflowRow.Rows.Count -eq 1){$rfqWorkflowId=[Guid]$workflowRow.Rows[0].Id}else{
        $createdWorkflow=Invoke-JsonApi POST '/api/Workflow/definitions' $adminToken @{
            name="TDC Tender Evaluation - Governed $((Get-Date).ToUniversalTime().ToString('yyyyMMddHHmmss'))"
            description='Independent Head of Procurement approval of the committee RFQ evaluation recommendation.'
            entityType='TENDER_EVALUATION';isActive=$true;configuration='{}'
            steps=@(@{name='Independent evaluation approval';description='Head of Procurement decides the committee recommendation.';stepType='Approval';order=1;isRequired=$true;requiredRole='TDC_HEAD_OF_PROCUREMENT';estimatedHours=1;configuration=@{approvalConfig=@{approvalType=0;approverRules=@(@{approvalGroup=1;assignmentType=1;role='TDC_HEAD_OF_PROCUREMENT';priority=100});minApprovalsRequired=1;preventInitiatorApproval=$true;requireDistinctApprovers=$true}}})
            transitions=@()
        } @(201)
        $rfqWorkflowId=[Guid]$createdWorkflow.data.id
    }
    $goodsCategory=@($effectivePolicy.rules|Where-Object{"$($_.kind)" -in @('Category','0') -and "$($_.value.category)" -in @('Goods','0') -and $_.value.isEnabled -and [string]::IsNullOrWhiteSpace([string]$_.value.serviceClass)})
    $goodsRfqMethod=@($effectivePolicy.rules|Where-Object{"$($_.kind)" -in @('Method','1') -and "$($_.value.category)" -in @('Goods','0') -and "$($_.value.method)" -in @('RequestForQuotation','2') -and $_.value.isEnabled})|Select-Object -First 1
    $invalidRfqWorkflow=(-not $goodsRfqMethod) -or ([Guid]$goodsRfqMethod.value.workflowDefinitionId -ne $rfqWorkflowId)
    $unscopedMethodEvidence=@($effectivePolicy.rules|Where-Object{"$($_.kind)" -in @('Evidence','4') -and $_.sourceDecisionKey -in @('DEC-005','DEC-006') -and $null -eq $_.value.method})
    if($goodsCategory.Count -eq 0 -or $unscopedMethodEvidence.Count -gt 0 -or $invalidRfqWorkflow){
        $draftRow=Invoke-DbTable $connection "SELECT TOP (1) Id FROM ProcurementPolicySets WHERE TenantId=@tenantId AND Code='TDC-INV-E2E018-ACTIVE' AND LifecycleStatus=0 AND IsDeleted=0 ORDER BY Version DESC;" @{tenantId=$tenantId}
        $draft=if($draftRow.Rows.Count -eq 1){Invoke-JsonApi GET "/api/procurement/policy-sets/$($draftRow.Rows[0].Id)" $adminToken $null}else{Invoke-JsonApi POST "/api/procurement/policy-sets/$($effectivePolicy.id)/clone-draft" $adminToken @{changeSummary='Add the missing governed Goods category rule exposed by E2E-018 sourcing readiness.'} @(201)}
        $goodsRules=@($draft.rules|Where-Object{"$($_.kind)" -in @('Category','0','Method','1','Threshold','2') -and "$($_.value.category)" -in @('Goods','0') -and $_.value.isEnabled})
        foreach($goodsRule in $goodsRules){
            $value=$goodsRule.value
            if([string]::IsNullOrWhiteSpace([string]$value.serviceClass)){continue}
            $value.serviceClass=$null
            $kind="$($goodsRule.kind)"
            $valueName=@{Category='category';Method='method';Threshold='threshold';'0'='category';'1'='method';'2'='threshold'}[$kind]
            $body=@{kind=$kind;rowVersion=$goodsRule.rowVersion;reason='Make the governed Goods rule applicable when no optional service class is supplied.'}
            $body[$valueName]=$value
            $null=Invoke-JsonApi PUT "/api/procurement/policy-sets/$($draft.id)/rules/$($goodsRule.id)" $adminToken $body
        }
        $draft=Invoke-JsonApi GET "/api/procurement/policy-sets/$($draft.id)" $adminToken $null
        foreach($methodRule in @($draft.rules|Where-Object{"$($_.kind)" -in @('Method','1') -and "$($_.value.category)" -in @('Goods','0') -and "$($_.value.method)" -in @('RequestForQuotation','2') -and $_.value.isEnabled})){
            if([Guid]$methodRule.value.workflowDefinitionId -eq $rfqWorkflowId){continue}
            $methodValue=$methodRule.value
            $methodValue.workflowDefinitionId=$rfqWorkflowId
            $null=Invoke-JsonApi PUT "/api/procurement/policy-sets/$($draft.id)/rules/$($methodRule.id)" $adminToken @{kind='Method';method=$methodValue;rowVersion=$methodRule.rowVersion;reason='Bind RFQ evaluation to the published TENDER_EVALUATION shared workflow.'}
        }
        $draft=Invoke-JsonApi GET "/api/procurement/policy-sets/$($draft.id)" $adminToken $null
        $categoryRule=@($draft.rules|Where-Object{"$($_.kind)" -in @('Category','0') -and "$($_.value.category)" -in @('Goods','0') -and $_.value.isEnabled})|Select-Object -First 1
        if(-not $categoryRule){
            $null=Invoke-JsonApi POST "/api/procurement/policy-sets/$($draft.id)/rules" $adminToken @{
                kind='Category'
                category=@{ruleCode='E2E018-GOODS';name='Goods';category='Goods';description='Controlled Goods category used by the E2E receipt-to-Finance acceptance chain.';requiresSpecification=$true;specificationTemplateCode='GOODS-SPEC';priority=100;isEnabled=$true;effectiveFrom=$draft.effectiveFrom;effectiveTo=$draft.effectiveTo;overrideAction='Add';sourceDecisionKey='DEC-001'}
                reason='Close the effective Goods category configuration gap identified by sourcing readiness.'
            }
        }
        $draft=Invoke-JsonApi GET "/api/procurement/policy-sets/$($draft.id)" $adminToken $null
        foreach($evidenceRule in @($draft.rules|Where-Object{"$($_.kind)" -in @('Evidence','4') -and $_.sourceDecisionKey -in @('DEC-005','DEC-006') -and $null -eq $_.value.method})){
            $evidenceValue=$evidenceRule.value
            $evidenceValue | Add-Member -NotePropertyName method -NotePropertyValue $(if($evidenceRule.sourceDecisionKey -eq 'DEC-005'){'PettyPurchase'}else{'SingleSource'}) -Force
            $null=Invoke-JsonApi PUT "/api/procurement/policy-sets/$($draft.id)/rules/$($evidenceRule.id)" $adminToken @{kind='Evidence';evidence=$evidenceValue;rowVersion=$evidenceRule.rowVersion;reason='Scope method-specific evidence to its governed procurement method.'}
        }
        $validation=Invoke-JsonApi POST "/api/procurement/policy-sets/$($draft.id)/validate" $adminToken $null
        if(-not $validation.isValid){throw "Governed policy validation failed: $($validation.errors|ConvertTo-Json -Depth 12 -Compress)"}
        $draft=Invoke-JsonApi GET "/api/procurement/policy-sets/$($draft.id)" $adminToken $null
        $effectivePolicy=Invoke-JsonApi POST "/api/procurement/policy-sets/$($draft.id)/publish" $adminToken @{rowVersion=$draft.rowVersion;reason='Publish the governed E2E-018 Goods category correction.'}
    }

    $prReadiness=Invoke-JsonApi GET "/api/PurchaseRequisitions/$prId/sourcing-readiness" $adminToken $null
    $lockedPolicySetId=Invoke-DbScalar $connection 'SELECT TOP(1) PolicySetId FROM ProcurementSourcingCases WHERE TenantId=@tenantId AND PurchaseRequisitionId=@prId AND IsDeleted=0 ORDER BY CreatedAt DESC;' @{tenantId=$tenantId;prId=$prId}
    $requiresCurrentPolicyPr=(-not $prReadiness.isReleased) -or (-not $lockedPolicySetId) -or ([Guid]$lockedPolicySetId -ne [Guid]$effectivePolicy.id)
    if($requiresCurrentPolicyPr){
        $currentNote="INV-REQ-FU-004 E2E-018 policy-current-v$($effectivePolicy.version) sourcing chain $((Get-Date).ToUniversalTime().ToString('yyyyMMddHHmmss'))"
        $existingCurrent=Invoke-DbTable $connection @'
SELECT TOP (1) pr.Id
FROM PurchaseRequisitions pr
WHERE pr.TenantId=@tenantId AND pr.IsDeleted=0 AND pr.Notes=@note
  AND NOT EXISTS (
      SELECT 1
      FROM RequestForQuotations rfq
      JOIN ProcurementTenderDocumentRegisters reg ON reg.RequestForQuotationId=rfq.Id AND reg.IsDeleted=0
      WHERE rfq.SourcePurchaseRequisitionId=pr.Id AND rfq.IsDeleted=0
        AND reg.OriginalSubmissionDeadlineUtc<=SYSUTCDATETIME())
ORDER BY pr.CreatedAt DESC;
'@ @{tenantId=$tenantId;note=$currentNote}
        if($existingCurrent.Rows.Count -eq 1){$prId=[Guid]$existingCurrent.Rows[0].Id}else{
            $budgetId=[Guid](Invoke-DbScalar $connection "SELECT TOP (1) Id FROM ProcurementBudgets WHERE TenantId=@tenantId AND IsDeleted=0 AND Title='INV-REQ-FU-004 E2E-018 Air Filter Budget' AND Status IN ('Approved','Active') ORDER BY CreatedAt DESC;" @{tenantId=$tenantId})
            $specificationId=[Guid](Invoke-DbScalar $connection "SELECT TOP (1) Id FROM ProcurementSpecificationTemplates WHERE TenantId=@tenantId AND IsDeleted=0 AND TemplateCode='INV-E2E018-GOODS-SPEC' AND Status=2 ORDER BY Version DESC;" @{tenantId=$tenantId})
            $pr=Invoke-JsonApi POST '/api/PurchaseRequisitions' $adminToken @{
                requiredDate=(Get-Date).ToUniversalTime().Date.AddDays(14);priority='Normal';department='Human Resources';costCenter='HR-STORES'
                justification='Approved replenishment of heavy-duty air filters under the current governed policy.'
                notes=$currentNote;requestedById=$actors[0].Id
                linkage=@{sourcePlanItemId=$planItemId;budgetId=$budgetId;procurementCategory='Goods';costCenter='HR-STORES';requisitionType='StockReplenishment';specificationTemplateId=$specificationId}
                items=@(@{inventoryItemId=$inventoryItemId;itemDescription='Air Filter - Heavy Duty';quantity=10;unitOfMeasure='EA';estimatedUnitPrice=24.50;requiredDate=(Get-Date).ToUniversalTime().Date.AddDays(14);preferredSupplierId=$supplierId;notes='Receive only after Waybill and inspection.';specifications='Must satisfy INV-E2E018-GOODS-SPEC.'})
            } @(201)
            $prId=[Guid]$pr.id
        }
        $pr=Invoke-JsonApi GET "/api/PurchaseRequisitions/$prId" $adminToken $null
        if($pr.status -eq 'Draft'){$null=Invoke-JsonApi POST "/api/PurchaseRequisitions/$prId/submit" $adminToken $null;$pr=Invoke-JsonApi GET "/api/PurchaseRequisitions/$prId" $adminToken $null}
        if($pr.status -in @('Pending Approval','Submitted')){$null=Invoke-JsonApi POST "/api/PurchaseRequisitions/$prId/approve" $tokens['proc-plan-procurement-approver'] @{approved=$true;comments='Independent approval under the current policy and authority route.'}}
        $null=Invoke-JsonApi POST "/api/PurchaseRequisitions/$prId/sourcing-release" $tokens['proc-plan-procurement-approver'] @{reason='Release the current-policy governed Goods requisition for RFQ sourcing.'}
    }

    $caseRow=Invoke-DbTable $connection 'SELECT TOP (1) Id FROM ProcurementSourcingCases WHERE TenantId=@tenantId AND PurchaseRequisitionId=@prId AND IsDeleted=0 ORDER BY CreatedAt DESC;' @{tenantId=$tenantId;prId=$prId}
    if($caseRow.Rows.Count -eq 0){
        $readiness=Invoke-JsonApi GET "/api/procurement/sourcing-cases/readiness/$prId" $adminToken $null
        if(-not $readiness.canCreate){throw "Sourcing-case readiness blocked: $($readiness.decisionCode) - $($readiness.message)"}
        $lineIds=@($readiness.lines|ForEach-Object{[Guid]$_.id})
        if($lineIds.Count -eq 0){throw 'Sourcing-case readiness returned no requisition lines.'}
        $sourcingCase=Invoke-JsonApi POST '/api/procurement/sourcing-cases' $adminToken @{
            requisitionId=$prId
            selectedMethod=$readiness.recommendedMethod
            justification='Controlled E2E-018 sourcing case for the approved immutable procurement-plan release.'
            lots=@(@{lotCode='LOT-01';title='Air filter supply';description='Approved plan and requisition scope.';purchaseRequisitionItemIds=$lineIds})
        } @(201)
        $sourcingCaseId=[Guid]$sourcingCase.id
    }else{$sourcingCaseId=[Guid]$caseRow.Rows[0].Id}

    $rfqRow=Invoke-DbTable $connection 'SELECT TOP (1) Id FROM RequestForQuotations WHERE TenantId=@tenantId AND SourcePurchaseRequisitionId=@prId AND IsDeleted=0 ORDER BY CreatedAt DESC;' @{tenantId=$tenantId;prId=$prId}
    if($rfqRow.Rows.Count -eq 0){$created=Invoke-JsonApi POST "/api/PurchaseRequisitions/$prId/create-rfq" $adminToken $null;$rfqId=[Guid]$created.rfqId}else{$rfqId=[Guid]$rfqRow.Rows[0].Id}
    $rfq=Invoke-JsonApi GET "/api/procurement/rfqs/$rfqId" $adminToken $null
    if($rfq.status -eq 'Draft'){
        # Keep enough time for controlled issuance and supplier submission on
        # slower acceptance hosts; opening still waits for the exact deadline.
        $rfq=Invoke-JsonApi PUT "/api/procurement/rfqs/$rfqId" $adminToken @{title='INV-REQ-FU-004 E2E-018 Air Filter RFQ';description='Governed RFQ retained for receipt-to-Finance acceptance.';submissionDeadline=(Get-Date).ToUniversalTime().AddMinutes(2);supplierIds=@($supplierId);externalRecipientEmails=$null}

        $readiness=Invoke-JsonApi GET "/api/procurement/tender-document-register/readiness?sourceType=RequestForQuotation&sourceId=$rfqId" $adminToken $null
        if(-not $readiness.hasRegister){
            if(-not $readiness.effectiveTemplateVersionId){
                $artifact=Invoke-DbTable $connection 'SELECT TOP(1) Id,FilePath,Sha256 FROM WorkflowEvidenceDocuments WHERE TenantId=@tenantId AND IsDeleted=0 AND IsCurrent=1 AND VerificationStatus=1 AND MalwareScanStatus=1 ORDER BY CreatedAt DESC;' @{tenantId=$tenantId}
                if($artifact.Rows.Count -ne 1){throw 'A verified, malware-clean central workflow evidence document is required to publish the tender template.'}
                $templateRow=Invoke-DbTable $connection 'SELECT TOP(1) Id FROM ProcurementTenderDocumentTemplateVersions WHERE TenantId=@tenantId AND IsDeleted=0 AND PolicySetId=@policySetId AND SourceConfigurationProfileId=@profileId AND Status IN (0,1,2) ORDER BY Version DESC;' @{tenantId=$tenantId;policySetId=[Guid]$effectivePolicy.id;profileId=[Guid]$effectivePolicy.sourceConfigurationProfileId}
                if($templateRow.Rows.Count -eq 1){
                    $template=Invoke-JsonApi GET "/api/procurement/tender-document-templates/$($templateRow.Rows[0].Id)" $adminToken $null
                }else{
                    $templateWorkflow=@(Invoke-JsonApi GET '/api/procurement/tender-document-templates/workflow-options' $adminToken $null)|Select-Object -First 1
                    if(-not $templateWorkflow){
                        $createdWorkflow=Invoke-JsonApi POST '/api/Workflow/definitions' $adminToken @{
                            name="INV-FU-004 Controlled Tender Template Approval $((Get-Date).ToUniversalTime().ToString('yyyyMMddHHmmss'))";description='Independent approval for policy-locked tender-document templates.';entityType='PROCUREMENT_SOURCING';isActive=$true
                            configuration='{}';steps=@(@{name='Independent tender-template approval';description='Head of Procurement independently approves the controlled template.';stepType='Approval';order=1;isRequired=$true;requiredRole='TDC_HEAD_OF_PROCUREMENT';estimatedHours=1;configuration=@{approvalConfig=@{approvalType=0;approverRules=@(@{approvalGroup=1;assignmentType=1;role='TDC_HEAD_OF_PROCUREMENT';priority=100});minApprovalsRequired=1;preventInitiatorApproval=$true;requireDistinctApprovers=$true}}});transitions=@()
                        } @(201)
                        $templateWorkflow=$createdWorkflow.data
                    }
                    $template=Invoke-JsonApi POST '/api/procurement/tender-document-templates' $adminToken @{
                        templateCode="INV-E2E018-RFQ-V$($effectivePolicy.version)";name='E2E-018 controlled RFQ tender document';description='Controlled RFQ issue document locked to the effective E2E-018 policy and configuration profile.'
                        documentTypeCode='RFQ';effectiveFromUtc=(Get-Date).ToUniversalTime().AddMinutes(-1);effectiveToUtc=$null
                        policySetId=[Guid]$effectivePolicy.id;policySetCode=$effectivePolicy.code;policySetVersion=[int]$effectivePolicy.version;sourceConfigurationProfileId=[Guid]$effectivePolicy.sourceConfigurationProfileId
                        contentReference=[string]$artifact.Rows[0].FilePath;contentWorkflowEvidenceDocumentId=[Guid]$artifact.Rows[0].Id;contentFileUploadRecordId=$null;contentChecksumSha256=[string]$artifact.Rows[0].Sha256
                        workflowDefinitionId=[Guid]$templateWorkflow.id;applicableMethods=@('RequestForQuotation');changeSummary='Initial controlled RFQ template for E2E-018 acceptance.'
                    } @(201)
                }
                $templateEvidence=@(New-ExternalEvidence 'INV-E2E018-TENDER-TEMPLATE-APPROVAL' 'Controlled tender-template approval evidence')
                if("$($template.status)" -in @('Draft','0')){
                    $template=Invoke-JsonApi POST "/api/procurement/tender-document-templates/$($template.id)/submit" $adminToken @{rowVersion=$template.rowVersion;comment='Submit policy-locked RFQ tender template for independent approval.';evidence=$templateEvidence}
                }
                if("$($template.status)" -in @('PendingApproval','Pending approval','1')){
                    Complete-Workflow ([Guid]$template.workflowInstanceId) $tokens['proc-plan-procurement-approver'] 'Approve the controlled RFQ tender-document template.'
                    $template=Invoke-JsonApi GET "/api/procurement/tender-document-templates/$($template.id)" $tokens['proc-plan-procurement-approver'] $null
                    $template=Invoke-JsonApi POST "/api/procurement/tender-document-templates/$($template.id)/publish" $tokens['proc-plan-procurement-approver'] @{rowVersion=$template.rowVersion;comment='Publish the independently approved RFQ tender template.';evidence=$templateEvidence}
                }
                $readiness=Invoke-JsonApi GET "/api/procurement/tender-document-register/readiness?sourceType=RequestForQuotation&sourceId=$rfqId" $adminToken $null
            }
            if(-not $readiness.effectiveTemplateVersionId){throw "Tender-document readiness remains blocked: $($readiness.blockedReasons -join '; ')"}
            $register=Invoke-JsonApi POST '/api/procurement/tender-document-register/bind' $adminToken @{
                sourceType='RequestForQuotation';sourceId=$rfqId;templateVersionId=[Guid]$readiness.effectiveTemplateVersionId
                submissionDeadlineUtc=$rfq.submissionDeadline;openingScheduledAtUtc=$null;bidValidityUntilUtc=([DateTime]$rfq.submissionDeadline).ToUniversalTime().AddDays(30)
                feeMode='Free';feeAmount=0;currencyCode='GHS'
            } @(201)
        }
        $register=Invoke-JsonApi GET "/api/procurement/tender-document-register?sourceType=RequestForQuotation&sourceId=$rfqId" $adminToken $null
        if(-not (@($register.issuances)|Where-Object{$_.businessPartnerId -eq $supplierId})){
            $supplier=Invoke-DbTable $connection 'SELECT PartnerName,PrimaryEmail,PrimaryPhone FROM BusinessPartners WHERE Id=@supplierId AND TenantId=@tenantId AND IsDeleted=0;' @{supplierId=$supplierId;tenantId=$tenantId}
            if($supplier.Rows.Count -ne 1){throw 'The selected supplier master record is unavailable.'}
            $register=Invoke-JsonApi POST '/api/procurement/tender-document-register/issue' $adminToken @{
                sourceType='RequestForQuotation';sourceId=$rfqId;businessPartnerId=$supplierId;recipientName=[string]$supplier.Rows[0].PartnerName
                recipientEmail=$(if($supplier.Rows[0].IsNull('PrimaryEmail')){$null}else{[string]$supplier.Rows[0].PrimaryEmail});recipientPhone=$(if($supplier.Rows[0].IsNull('PrimaryPhone')){$null}else{[string]$supplier.Rows[0].PrimaryPhone})
                amountPaid=0;paymentReference=$null;receiptNumber="INV-E2E018-FREE-$($rfqId.ToString('N').Substring(0,12).ToUpperInvariant())";issueChannel='SupplierPortal'
                evidenceReference='INV-E2E018-CONTROLLED-RFQ-ISSUANCE';evidenceWorkflowDocumentId=$null;evidenceFileUploadRecordId=$null;registerRowVersion=$register.rowVersion
            } @(201)
        }
        $dispatchReadiness=Invoke-JsonApi GET "/api/procurement/tender-document-register/readiness?sourceType=RequestForQuotation&sourceId=$rfqId" $adminToken $null
        if(-not $dispatchReadiness.ready){throw "Controlled tender-document dispatch is not ready: $($dispatchReadiness.blockedReasons -join '; ')"}
        $null=Invoke-JsonApi POST "/api/procurement/rfqs/$rfqId/send" $adminToken @{supplierIds=@($supplierId);externalRecipientEmails=$null}
        $rfq=Invoke-JsonApi GET "/api/procurement/rfqs/$rfqId" $adminToken $null
    }
    if($rfq.status -eq 'Sent' -and @($rfq.quotes).Count -eq 0){
        $quote=Invoke-JsonApi POST "/api/procurement/rfqs/$rfqId/quote" $supplierToken @{notes='E2E-018 sealed supplier quotation';items=@($rfq.items|ForEach-Object{@{rfqItemId=$_.id;unitPrice=24.50}})}
    }else{$quote=@($rfq.quotes)|Select-Object -First 1}
    [pscustomobject]@{SupplierId=$supplierId;SupplierEligibility=$eligibility.validationCode;Policy="$($effectivePolicy.code)/v$($effectivePolicy.version)";SourcingCaseId=$sourcingCaseId;RfqId=$rfqId;RfqNumber=$rfq.rfqNumber;RfqStatus=$rfq.status;SubmissionDeadline=$rfq.submissionDeadline;QuoteId=$quote.id;QuoteStatus=$quote.status;QuoteAmount=$quote.totalAmount}|ConvertTo-Json -Compress
}
finally{
    foreach($membership in $addedRoleMemberships){try{$null=Invoke-DbNonQuery $connection 'DELETE FROM UserRoles WHERE UserId=@userId AND RoleId=@roleId;' @{userId=$membership.UserId;roleId=$membership.RoleId}}catch{}}
    foreach($actorId in $passwordBackups.Keys){$backup=$passwordBackups[$actorId];try{$null=Invoke-DbNonQuery $connection 'UPDATE Users SET PasswordHash=@hash,AccessFailedCount=@failed,LockoutEnd=@lockout WHERE Id=@id;' @{id=$actorId;hash=$backup.PasswordHash;failed=$backup.AccessFailedCount;lockout=$backup.LockoutEnd}}catch{}}
    if($connection.State -eq [System.Data.ConnectionState]::Open){$connection.Close()};$connection.Dispose()
}
