[CmdletBinding()]
param(
    [string]$ApiBaseUrl = 'http://127.0.0.1:5100',
    [Guid]$RfqId = 'fc933a61-17c2-4562-8900-629a30bca222'
)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$apiProject = Join-Path $repoRoot 'src\ErpSystem.Api\ErpSystem.Api.csproj'
$tenantId = [Guid]'00000000-0000-0000-0000-000000000001'
$supplierId = [Guid]'1195675f-2229-4a3c-9484-88e4169afbfe'
$workflowDefinitionId = [Guid]'3c4d6320-763b-4b17-bf44-a9b087d85bcf'
$actors = @(
    @{ Id=[Guid]'58cafd8b-42ce-4f67-0dbb-08de862e82ee'; UserName='admin' },
    @{ Id=[Guid]'45eacad1-fff9-40f4-91fb-a0e72e2fa7f7'; UserName='proc-plan-procurement-approver' }
)

function Add-DbParameter { param([Data.SqlClient.SqlCommand]$Command,[string]$Name,$Value) $Command.Parameters.AddWithValue($Name,$(if($null -eq $Value){[DBNull]::Value}else{$Value})) }
function Invoke-DbTable { param([Data.SqlClient.SqlConnection]$Connection,[string]$Sql,[hashtable]$Parameters=@{}) $command=$Connection.CreateCommand();$command.CommandText=$Sql;$command.CommandTimeout=60;foreach($entry in $Parameters.GetEnumerator()){$null=Add-DbParameter $command "@$($entry.Key)" $entry.Value};$table=[Data.DataTable]::new();$adapter=[Data.SqlClient.SqlDataAdapter]::new($command);$null=$adapter.Fill($table);Write-Output -NoEnumerate $table }
function Invoke-DbScalar { param([Data.SqlClient.SqlConnection]$Connection,[string]$Sql,[hashtable]$Parameters=@{}) $command=$Connection.CreateCommand();$command.CommandText=$Sql;foreach($entry in $Parameters.GetEnumerator()){$null=Add-DbParameter $command "@$($entry.Key)" $entry.Value};$command.ExecuteScalar() }
function Invoke-DbNonQuery { param([Data.SqlClient.SqlConnection]$Connection,[string]$Sql,[hashtable]$Parameters=@{}) $command=$Connection.CreateCommand();$command.CommandText=$Sql;foreach($entry in $Parameters.GetEnumerator()){$null=Add-DbParameter $command "@$($entry.Key)" $entry.Value};$command.ExecuteNonQuery() }
function Invoke-JsonApi {
    param([string]$Method,[string]$Path,[string]$Token,$Body,[int[]]$ExpectedStatus=@(200))
    $headers=@{'X-Correlation-ID'="INV-FU-004-E2E018-GOV-$([Guid]::NewGuid().ToString('N'))"};if($Token){$headers.Authorization="Bearer $Token"}
    $parameters=@{Uri="$ApiBaseUrl$Path";Method=$Method;Headers=$headers;SkipHttpErrorCheck=$true;TimeoutSec=120}
    if($null -ne $Body){$parameters.ContentType='application/json';$parameters.Body=$Body|ConvertTo-Json -Depth 30 -Compress}
    $response=Invoke-WebRequest @parameters;$content=if($response.Content -is [byte[]]){[Text.Encoding]::UTF8.GetString($response.Content)}else{[string]$response.Content}
    if($response.StatusCode -notin $ExpectedStatus){throw "$Method $Path returned $($response.StatusCode): $content"};if([string]::IsNullOrWhiteSpace($content)){return $null};$content|ConvertFrom-Json
}
function Login-Actor([string]$UserName,[string]$Password){$result=Invoke-JsonApi POST '/api/auth/login' '' @{username=$UserName;password=$Password;rememberMe=$false};if([string]::IsNullOrWhiteSpace($result.token)){throw "Login did not issue a token for $UserName."};[string]$result.token}
function Test-ApiStatus($Value,[string]$Name,[int]$Number){([string]$Value -eq $Name) -or ([string]$Value -eq [string]$Number)}
function Complete-Workflow([Guid]$InstanceId,[string]$ApproverToken,[string]$Comment){
    $pending=Invoke-JsonApi GET '/api/Workflow/approvals/pending' $ApproverToken $null
    $approval=@($pending.data)|Where-Object{$_.stepInstance.workflowInstanceId -eq $InstanceId -or $_.stepInstance.workflowInstance.id -eq $InstanceId}|Select-Object -First 1
    if(-not $approval){
        $approvalId=Invoke-DbScalar $connection 'SELECT TOP(1) wa.Id FROM WorkflowApprovals wa JOIN WorkflowStepInstances ws ON ws.Id=wa.StepInstanceId WHERE ws.WorkflowInstanceId=@instanceId AND wa.Status=0 AND wa.IsDeleted=0 ORDER BY wa.CreatedAt DESC;' @{instanceId=$InstanceId}
        if(-not $approvalId){
            $stepId=Invoke-DbScalar $connection 'SELECT TOP(1) Id FROM WorkflowStepInstances WHERE WorkflowInstanceId=@instanceId AND Status IN (0,1) AND IsDeleted=0 ORDER BY CreatedAt;' @{instanceId=$InstanceId}
            if(-not $stepId){throw "No active shared-workflow step exists for $InstanceId."}
            $processed=Invoke-JsonApi POST "/api/Workflow/steps/$stepId/process" $ApproverToken @{action=0;comments=$Comment}
            if(-not $processed.data.success){throw "Shared workflow did not complete: $($processed.data.message)"}
            return
        }
    } else {$approvalId=$approval.id}
    $null=Invoke-JsonApi POST "/api/Workflow/approvals/$approvalId/process" $ApproverToken @{action=0;comments=$Comment}
}
function New-ExternalEvidence([string]$Reference,[string]$Label){@{referenceKind=2;reference=$Reference;label=$Label}}

$secretRows=dotnet user-secrets list --project $apiProject --json|ConvertFrom-Json
$secretObject=$secretRows|Where-Object{$_ -and $_.PSObject.Properties.Name -contains 'ConnectionStrings:DefaultConnection'}|Select-Object -First 1
if(-not $secretObject){throw 'The configured API database connection was not found in user secrets.'}
Add-Type -AssemblyName System.Data
$connection=[Data.SqlClient.SqlConnection]::new([string]$secretObject.'ConnectionStrings:DefaultConnection');$connection.Open()
$identityAssembly=Get-ChildItem 'C:\Program Files\dotnet\shared\Microsoft.AspNetCore.App' -Recurse -Filter Microsoft.Extensions.Identity.Core.dll|Where-Object{$_.Directory.Name -like '8.*'}|Sort-Object{[version]$_.Directory.Name} -Descending|Select-Object -First 1
try{Add-Type -Path $identityAssembly.FullName -ErrorAction Stop}catch [Management.Automation.RuntimeException]{}
$hasher=[Microsoft.AspNetCore.Identity.PasswordHasher[object]]::new();$temporaryPassword="Inv!$([Guid]::NewGuid().ToString('N'))aA7";$temporaryHash=$hasher.HashPassword([object]::new(),$temporaryPassword)
$passwordBackups=@{};$addedRoleMemberships=[Collections.Generic.List[object]]::new()
try {
    foreach($actor in $actors){$row=Invoke-DbTable $connection 'SELECT PasswordHash,AccessFailedCount,LockoutEnd FROM Users WHERE Id=@id AND TenantId=@tenantId AND IsActive=1;' @{id=$actor.Id;tenantId=$tenantId};if($row.Rows.Count -ne 1){throw "Acceptance actor $($actor.UserName) is unavailable."};$passwordBackups[$actor.Id]=@{PasswordHash=if($row.Rows[0].IsNull('PasswordHash')){$null}else{[string]$row.Rows[0].PasswordHash};AccessFailedCount=[int]$row.Rows[0].AccessFailedCount;LockoutEnd=if($row.Rows[0].IsNull('LockoutEnd')){$null}else{$row.Rows[0].LockoutEnd}};$null=Invoke-DbNonQuery $connection 'UPDATE Users SET PasswordHash=@hash,AccessFailedCount=0,LockoutEnd=NULL WHERE Id=@id;' @{id=$actor.Id;hash=$temporaryHash}}
    $roleId=Invoke-DbScalar $connection 'SELECT Id FROM AspNetRoles WHERE Name=@name;' @{name='TDC_HEAD_OF_PROCUREMENT'};if(-not $roleId){throw 'Required TDC_HEAD_OF_PROCUREMENT role is unavailable.'}
    if([int](Invoke-DbScalar $connection 'SELECT COUNT(1) FROM UserRoles WHERE UserId=@userId AND RoleId=@roleId;' @{userId=$actors[1].Id;roleId=[Guid]$roleId}) -eq 0){$null=Invoke-DbNonQuery $connection 'INSERT INTO UserRoles(UserId,RoleId) VALUES(@userId,@roleId);' @{userId=$actors[1].Id;roleId=[Guid]$roleId};$addedRoleMemberships.Add([pscustomobject]@{UserId=$actors[1].Id;RoleId=[Guid]$roleId})}
    $adminToken=Login-Actor 'admin' $temporaryPassword;$approverToken=Login-Actor 'proc-plan-procurement-approver' $temporaryPassword

    $due=Invoke-JsonApi GET "/api/procurement/supplier-due-diligence/suppliers/$supplierId/current" $adminToken $null
    if(-not $due.isCurrent){
        $reviewPage=Invoke-JsonApi GET "/api/procurement/supplier-due-diligence?search=CONT-GH-ADOM-BUILD&page=1&pageSize=25" $adminToken $null
        $review=@($reviewPage.items)|Where-Object{$_.businessPartnerId -eq $supplierId -and ((Test-ApiStatus $_.status 'Draft' 0) -or (Test-ApiStatus $_.status 'PendingApproval' 1))}|Select-Object -First 1
        if($review){$review=Invoke-JsonApi GET "/api/procurement/supplier-due-diligence/$($review.id)" $adminToken $null}
        if($review -and (Test-ApiStatus $review.status 'PendingApproval' 1)){
            $workflowStatus=[int](Invoke-DbScalar $connection 'SELECT Status FROM WorkflowInstances WHERE Id=@id AND TenantId=@tenantId AND IsDeleted=0;' @{id=[Guid]$review.workflowInstanceId;tenantId=$tenantId})
            if($workflowStatus -in 3,4){
                $rejectionEvidence=@(New-ExternalEvidence 'E2E018-WORKFLOW-RETRY' 'Cancelled workflow recovery evidence')
                $null=Invoke-JsonApi POST "/api/procurement/supplier-due-diligence/$($review.id)/reject" $approverToken @{rowVersion=$review.rowVersion;comment='Close the cancelled acceptance workflow before a governed retry.';evidence=$rejectionEvidence}
                $review=$null
            }
            elseif($workflowStatus -eq 2 -and $review.policyDecisionId -ne $due.policyDecisionId){
                $supersessionEvidence=@(New-ExternalEvidence 'E2E018-DEC011-SUPERSESSION' 'Completed review superseded by the current DEC-011 policy')
                $null=Invoke-JsonApi POST "/api/procurement/supplier-due-diligence/$($review.id)/supersede-stale" $approverToken @{rowVersion=$review.rowVersion;comment='Close the completed review because its immutable DEC-011 policy was replaced.';evidence=$supersessionEvidence}
                $review=$null
            }
        }
        if(-not $review){
            $hasRetainedReview=[int](Invoke-DbScalar $connection 'SELECT COUNT(1) FROM ProcurementSupplierDueDiligenceReviews WHERE TenantId=@tenantId AND BusinessPartnerId=@supplierId;' @{tenantId=$tenantId;supplierId=$supplierId}) -gt 0
            $review=Invoke-JsonApi POST '/api/procurement/supplier-due-diligence' $adminToken @{businessPartnerId=$supplierId;reviewType=$(if($hasRetainedReview){1}else{0});workflowDefinitionId=$workflowDefinitionId;notes='E2E-018 governed due-diligence cycle.'} @(201)
        }
        $now=(Get-Date).ToUniversalTime();$checks=@();foreach($type in 0..5){$checks+=@{checkType=$type;status=1;sourceName=@('PPA debarment register','GRA tax clearance','Sanctions register','Bank verification','Audited financial review','Reputation references')[$type];sourceReference="E2E018-DD-$type-$($now.ToString('yyyyMMdd'))";checkedAtUtc=$now.AddMinutes(-1);validUntilUtc=$now.AddMonths(12);notes='Acceptance evidence verified against the named authoritative source.';evidence=@(New-ExternalEvidence "E2E018-DD-$type-$($now.ToString('yyyyMMdd'))" 'Retained due-diligence source reference')}}
        if(Test-ApiStatus $review.status 'Draft' 0){$review=Invoke-JsonApi PUT "/api/procurement/supplier-due-diligence/$($review.id)" $adminToken @{rowVersion=$review.rowVersion;notes='All required checks completed clear.';checks=$checks}}
        $lifecycleEvidence=@(New-ExternalEvidence 'E2E018-DUE-DILIGENCE-REVIEW' 'Due-diligence review pack')
        if(Test-ApiStatus $review.status 'Draft' 0){$review=Invoke-JsonApi POST "/api/procurement/supplier-due-diligence/$($review.id)/submit" $adminToken @{rowVersion=$review.rowVersion;comment='Submit clear initial review for independent approval.';evidence=$lifecycleEvidence}}
        Complete-Workflow $review.workflowInstanceId $approverToken 'Shared workflow approval for clear due-diligence review.'
        $review=Invoke-JsonApi GET "/api/procurement/supplier-due-diligence/$($review.id)" $approverToken $null
        $review=Invoke-JsonApi POST "/api/procurement/supplier-due-diligence/$($review.id)/approve" $approverToken @{rowVersion=$review.rowVersion;comment='Independent approval of clear supplier review.';evidence=$lifecycleEvidence}
    }

    $avlCurrent=Invoke-JsonApi GET "/api/procurement/supplier-avl/suppliers/$supplierId/current" $adminToken $null
    if(-not $avlCurrent.isCurrent){
        $effective=(Get-Date).ToUniversalTime().AddMinutes(-1)
        $registerPage=Invoke-JsonApi GET "/api/procurement/supplier-avl?page=1&pageSize=25" $adminToken $null
        $register=@($registerPage.items)|Where-Object{$_.reviewYear -eq $effective.Year -and ((Test-ApiStatus $_.status 'Draft' 0) -or (Test-ApiStatus $_.status 'PendingApproval' 1) -or (Test-ApiStatus $_.status 'Approved' 2))}|Select-Object -First 1
        if($register){$register=Invoke-JsonApi GET "/api/procurement/supplier-avl/$($register.id)" $adminToken $null}
        else {$register=Invoke-JsonApi POST '/api/procurement/supplier-avl' $adminToken @{reviewYear=$effective.Year;effectiveFromUtc=$effective;workflowDefinitionId=$workflowDefinitionId;notes='E2E-018 governed supplier list.'} @(201)}
        if((Test-ApiStatus $register.status 'Draft' 0) -and -not (@($register.entries)|Where-Object{$_.businessPartnerId -eq $supplierId})){$register=Invoke-JsonApi POST "/api/procurement/supplier-avl/$($register.id)/entries" $adminToken @{businessPartnerId=$supplierId;registerRowVersion=$register.rowVersion}}
        $lifecycleEvidence=@(New-ExternalEvidence 'E2E018-AVL-REVIEW' 'AVL eligibility and review record')
        if(Test-ApiStatus $register.status 'Draft' 0){$register=Invoke-JsonApi POST "/api/procurement/supplier-avl/$($register.id)/submit" $adminToken @{rowVersion=$register.rowVersion;comment='Submit current AVL for independent approval.';evidence=$lifecycleEvidence}}
        if(Test-ApiStatus $register.status 'PendingApproval' 1){Complete-Workflow $register.workflowInstanceId $approverToken 'Shared workflow approval for supplier AVL.';$register=Invoke-JsonApi GET "/api/procurement/supplier-avl/$($register.id)" $approverToken $null;$register=Invoke-JsonApi POST "/api/procurement/supplier-avl/$($register.id)/approve" $approverToken @{rowVersion=$register.rowVersion;comment='Independent approval of current supplier AVL.';evidence=$lifecycleEvidence}}
        $register=Invoke-JsonApi POST "/api/procurement/supplier-avl/$($register.id)/publish" $approverToken @{rowVersion=$register.rowVersion;comment='Publish effective AVL for governed sourcing.';evidence=$lifecycleEvidence}
    }
    $eligibility=Invoke-JsonApi POST "/api/procurement/supplier-validation/rfq/$supplierId" $adminToken @{}
    if(-not $eligibility.isValid){throw "Supplier remains ineligible: $($eligibility.errors -join '; ')"}
    $performance=Invoke-JsonApi GET "/api/procurement/supplier-performance-scorecards/suppliers/$supplierId/current" $adminToken $null
    if(-not $performance.current){
        $null=Invoke-JsonApi POST '/api/procurement/supplier-performance-scorecards/calculate' $adminToken @{
            businessPartnerId=$supplierId
            idempotencyKey="e2e018-performance-$($RfqId.ToString('N'))"
            sourceType='RequestForQuotation'
            sourceId=$RfqId
            sourceReference='INV-E2E018-SUPPLIER-PERFORMANCE'
        } @(201)
        $performance=Invoke-JsonApi GET "/api/procurement/supplier-performance-scorecards/suppliers/$supplierId/current" $adminToken $null
    }
    if($performance.awardBlocked){
        $missing=@($performance.scorecard.measures|Where-Object{-not $_.score}|ForEach-Object{"$($_.metric): $($_.missingReason)"}) -join '; '
        throw "Supplier performance remains blocked: $(@($performance.awardBlockReasons) -join '; ') Missing: $missing"
    }
    $risk=Invoke-JsonApi GET "/api/procurement/supplier-risk/suppliers/$supplierId/current" $adminToken $null
    if($risk.awardBlocked -or $risk.assessment.sourceId -ne $RfqId){
        $null=Invoke-JsonApi POST '/api/procurement/supplier-risk/assessments' $adminToken @{
            businessPartnerId=$supplierId
            idempotencyKey="e2e018-risk-v2-$($RfqId.ToString('N'))-$($performance.scorecard.id)"
            sourceType='RequestForQuotation'
            sourceId=$RfqId
            sourceReference='INV-E2E018-SUPPLIER-RISK'
        } @(201)
        $risk=Invoke-JsonApi GET "/api/procurement/supplier-risk/suppliers/$supplierId/current" $adminToken $null
    }
    if(-not $risk.current -or $risk.awardBlocked){
        throw "Supplier risk remains blocked: $(@($risk.awardBlockReasons) -join '; ')"
    }
    [pscustomobject]@{SupplierId=$supplierId;DueDiligence=$eligibility.dueDiligence.code;Avl=$eligibility.avl.code;Eligibility=$eligibility.validationCode;IsValid=$eligibility.isValid;PerformanceScorecardId=$performance.scorecard.id;PerformanceScore=$performance.scorecard.overallScore;RiskAssessmentId=$risk.assessment.id;RiskScore=$risk.assessment.riskScore;AwardBlocked=$risk.awardBlocked}|ConvertTo-Json -Compress
}
finally {
    foreach($membership in $addedRoleMemberships){try{$null=Invoke-DbNonQuery $connection 'DELETE FROM UserRoles WHERE UserId=@userId AND RoleId=@roleId;' @{userId=$membership.UserId;roleId=$membership.RoleId}}catch{}}
    foreach($actorId in $passwordBackups.Keys){$backup=$passwordBackups[$actorId];try{$null=Invoke-DbNonQuery $connection 'UPDATE Users SET PasswordHash=@hash,AccessFailedCount=@failed,LockoutEnd=@lockout WHERE Id=@id;' @{id=$actorId;hash=$backup.PasswordHash;failed=$backup.AccessFailedCount;lockout=$backup.LockoutEnd}}catch{}}
    if($connection.State -eq [Data.ConnectionState]::Open){$connection.Close()};$connection.Dispose()
}
