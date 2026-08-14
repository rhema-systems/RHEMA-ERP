[CmdletBinding()]
param(
    [string]$ApiBaseUrl = 'http://127.0.0.1:5100',
    [Guid]$RfqId = '0147375e-de02-4f83-8fa0-c7177caf1655'
)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$apiProject = Join-Path $repoRoot 'src\ErpSystem.Api\ErpSystem.Api.csproj'
$tenantId = [Guid]'00000000-0000-0000-0000-000000000001'
$committeeTemplateId = [Guid]'da865fff-cd3f-46ef-8698-9d580079f754'
$actors = @(
    @{ Id=[Guid]'58cafd8b-42ce-4f67-0dbb-08de862e82ee'; UserName='admin' },
    @{ Id=[Guid]'470c45cd-6263-4796-0dbe-08de862e82ee'; UserName='helpdesk.agent' },
    @{ Id=[Guid]'9e475ce9-34ad-4a6d-0dbc-08de862e82ee'; UserName='manager' },
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
function Invoke-DbNonQuery {
    param([System.Data.SqlClient.SqlConnection]$Connection,[string]$Sql,[hashtable]$Parameters=@{})
    $command=$Connection.CreateCommand();$command.CommandText=$Sql
    foreach($entry in $Parameters.GetEnumerator()){$null=Add-DbParameter $command "@$($entry.Key)" $entry.Value}
    $command.ExecuteNonQuery()
}
function Invoke-JsonApi {
    param([string]$Method,[string]$Path,[string]$Token,$Body,[int[]]$ExpectedStatus=@(200))
    $headers=@{'X-Correlation-ID'="INV-FU-004-E2E018-AWARD-$([Guid]::NewGuid().ToString('N'))"}
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

$secretRows=dotnet user-secrets list --project $apiProject --json|ConvertFrom-Json
$secretObject=$secretRows|Where-Object{$_ -and $_.PSObject.Properties.Name -contains 'ConnectionStrings:DefaultConnection'}|Select-Object -First 1
if(-not $secretObject){throw 'The configured API database connection was not found in user secrets.'}
Add-Type -AssemblyName System.Data
$connection=[System.Data.SqlClient.SqlConnection]::new([string]$secretObject.'ConnectionStrings:DefaultConnection');$connection.Open()
$identityAssembly=Get-ChildItem 'C:\Program Files\dotnet\shared\Microsoft.AspNetCore.App' -Recurse -Filter Microsoft.Extensions.Identity.Core.dll|Where-Object{$_.Directory.Name -like '8.*'}|Sort-Object{[version]$_.Directory.Name} -Descending|Select-Object -First 1
try{Add-Type -Path $identityAssembly.FullName -ErrorAction Stop}catch [System.Management.Automation.RuntimeException]{}
$hasher=[Microsoft.AspNetCore.Identity.PasswordHasher[object]]::new()
$temporaryPassword="Inv!$([Guid]::NewGuid().ToString('N'))aA7"
$temporaryHash=$hasher.HashPassword([object]::new(),$temporaryPassword)
$passwordBackups=@{};$addedRoleMemberships=[Collections.Generic.List[object]]::new()
$assignmentBackups=@{};$addedCommitteeMemberIds=[Collections.Generic.List[Guid]]::new()
$tokens=@{};$setupChanged=$false
$committeeBackup=$null;$committeeTemporarilyScoped=$false

try {
    foreach($actor in $actors){
        $row=Invoke-DbTable $connection 'SELECT PasswordHash,AccessFailedCount,LockoutEnd FROM Users WHERE Id=@id AND TenantId=@tenantId AND IsActive=1;' @{id=$actor.Id;tenantId=$tenantId}
        if($row.Rows.Count -ne 1){throw "Acceptance actor $($actor.UserName) is unavailable."}
        $passwordBackups[$actor.Id]=@{PasswordHash=if($row.Rows[0].IsNull('PasswordHash')){$null}else{[string]$row.Rows[0].PasswordHash};AccessFailedCount=[int]$row.Rows[0].AccessFailedCount;LockoutEnd=if($row.Rows[0].IsNull('LockoutEnd')){$null}else{$row.Rows[0].LockoutEnd}}
        $null=Invoke-DbNonQuery $connection 'UPDATE Users SET PasswordHash=@hash,AccessFailedCount=0,LockoutEnd=NULL WHERE Id=@id;' @{id=$actor.Id;hash=$temporaryHash}
    }
    foreach($grant in @(
        @{UserId=$actors[0].Id;RoleName='TDC_PROCUREMENT_OFFICER'},
        @{UserId=$actors[1].Id;RoleName='TDC_EVALUATOR'},
        @{UserId=$actors[2].Id;RoleName='TDC_EVALUATOR'},
        @{UserId=$actors[2].Id;RoleName='TDC_PROCUREMENT_OFFICER'},
        @{UserId=$actors[3].Id;RoleName='TDC_EVALUATOR'},
        @{UserId=$actors[4].Id;RoleName='TDC_HEAD_OF_PROCUREMENT'},
        @{UserId=$actors[4].Id;RoleName='TDC_SENIOR_PROCUREMENT_OFFICER'}
    )){
        $role=Invoke-DbTable $connection 'SELECT Id FROM AspNetRoles WHERE Name=@name;' @{name=$grant.RoleName}
        if($role.Rows.Count -ne 1){throw "Required role $($grant.RoleName) is unavailable."}
        $roleId=[Guid]$role.Rows[0].Id
        $exists=(Invoke-DbTable $connection 'SELECT UserId FROM UserRoles WHERE UserId=@userId AND RoleId=@roleId;' @{userId=$grant.UserId;roleId=$roleId}).Rows.Count
        if($exists -eq 0){$null=Invoke-DbNonQuery $connection 'INSERT INTO UserRoles(UserId,RoleId) VALUES(@userId,@roleId);' @{userId=$grant.UserId;roleId=$roleId};$addedRoleMemberships.Add([pscustomobject]@{UserId=$grant.UserId;RoleId=$roleId})}
    }
    foreach($actor in $actors){$tokens[$actor.UserName]=Login-Actor $actor.UserName $temporaryPassword}
    $adminToken=$tokens['admin'];$evaluatorToken=$tokens['helpdesk.agent'];$committeeAdminToken=$tokens['manager'];$approverToken=$tokens['proc-plan-procurement-approver']
    $additionalEvaluatorTokens=@($tokens['manager'],$tokens['proc-plan-final-approver'])

    # The central procurement access service requires an effective governed
    # assignment in addition to the Security role claim for privileged actions.
    $assignments=@(Invoke-JsonApi GET '/api/procurement/access-controls/assignments' $adminToken $null)
    foreach($approverRole in @('TDC_HEAD_OF_PROCUREMENT','TDC_SENIOR_PROCUREMENT_OFFICER')){
        $approverAssignment=@($assignments|Where-Object{$_.userId -eq $actors[4].Id -and $_.roleName -eq $approverRole})|Select-Object -First 1
        $approverAssignmentBody=@{userId=$actors[4].Id;roleName=$approverRole;warehouseScopeMode='None';warehouseIds=@();locationScopeMode='None';locationIds=@();effectiveFrom=(Get-Date).ToUniversalTime().Date.AddDays(-1);effectiveTo=$null;isActive=$true;reason='Governed E2E-018 independent evaluation and award approval assignment.';rowVersion=$(if($approverAssignment){$approverAssignment.rowVersion}else{$null})}
        if($approverAssignment){$null=Invoke-JsonApi PUT "/api/procurement/access-controls/assignments/$($approverAssignment.id)" $adminToken $approverAssignmentBody}else{$null=Invoke-JsonApi POST '/api/procurement/access-controls/assignments' $adminToken $approverAssignmentBody @(201)}
    }

    $rfq=Invoke-JsonApi GET "/api/procurement/rfqs/$RfqId" $adminToken $null
    if($rfq.status -eq 'Sent'){
        $rfqItems=@($rfq.items);$quote=@($rfq.quotes|Where-Object{$_.status -eq 'Submitted'})|Select-Object -First 1
        if(-not $quote){throw 'The RFQ has no submitted quotation.'}

        $assignments=@(Invoke-JsonApi GET '/api/procurement/access-controls/assignments' $adminToken $null)
        foreach($memberActor in @($actors[1],$actors[2])){
            $assignment=@($assignments|Where-Object{$_.userId -eq $memberActor.Id -and $_.roleName -eq 'TDC_EVALUATOR'})|Select-Object -First 1
            if($assignment){$assignmentBackups[$assignment.id]=$assignment}
            $body=@{userId=$memberActor.Id;roleName='TDC_EVALUATOR';warehouseScopeMode='None';warehouseIds=@();locationScopeMode='None';locationIds=@();effectiveFrom=(Get-Date).ToUniversalTime().Date.AddDays(-1);effectiveTo=$null;isActive=$true;reason='Temporary governed E2E-018 evaluation committee acceptance setup.';rowVersion=$(if($assignment){$assignment.rowVersion}else{$null})}
            if($assignment){$active=Invoke-JsonApi PUT "/api/procurement/access-controls/assignments/$($assignment.id)" $adminToken $body}else{$active=Invoke-JsonApi POST '/api/procurement/access-controls/assignments' $adminToken $body @(201)}
            $assignmentBackups[$active.id]=if($assignment){$assignment}else{$null}
        }
        $assignments=@(Invoke-JsonApi GET '/api/procurement/access-controls/assignments' $adminToken $null)
        $administratorAssignment=@($assignments|Where-Object{$_.userId -eq $actors[2].Id -and $_.roleName -eq 'TDC_PROCUREMENT_OFFICER'})|Select-Object -First 1
        $administratorBody=@{userId=$actors[2].Id;roleName='TDC_PROCUREMENT_OFFICER';warehouseScopeMode='None';warehouseIds=@();locationScopeMode='None';locationIds=@();effectiveFrom=(Get-Date).ToUniversalTime().Date.AddDays(-1);effectiveTo=$null;isActive=$true;reason='Temporary governed E2E-018 committee administration setup.';rowVersion=$(if($administratorAssignment){$administratorAssignment.rowVersion}else{$null})}
        if($administratorAssignment){$administratorAssignment=Invoke-JsonApi PUT "/api/procurement/access-controls/assignments/$($administratorAssignment.id)" $adminToken $administratorBody}else{$administratorAssignment=Invoke-JsonApi POST '/api/procurement/access-controls/assignments' $adminToken $administratorBody @(201)}
        $assignments=@(Invoke-JsonApi GET '/api/procurement/access-controls/assignments' $adminToken $null)
        $committees=@(Invoke-JsonApi GET '/api/procurement/access-controls/committees' $adminToken $null)
        $committee=$committees|Where-Object{$_.id -eq $committeeTemplateId}|Select-Object -First 1
        if(-not $committee){throw 'The governed Evaluation Committee template is unavailable.'}
        foreach($memberActor in @($actors[1])){
            $assignment=$assignments|Where-Object{$_.userId -eq $memberActor.Id -and $_.roleName -eq 'TDC_EVALUATOR' -and $_.isActive}|Select-Object -First 1
            if(-not $assignment){throw "Active evaluator assignment missing for $($memberActor.UserName)."}
            if(-not (@($committee.members)|Where-Object{$_.userId -eq $memberActor.Id -and $_.isActive})){
                $created=Invoke-JsonApi POST "/api/procurement/access-controls/committees/$committeeTemplateId/members" $adminToken @{assignmentId=$assignment.id;memberKind=$(if($memberActor.Id -eq $actors[1].Id){'Chair'}elseif($memberActor.Id -eq $actors[3].Id){'Secretary'}else{'VotingMember'});isVoting=$true;effectiveFrom=(Get-Date).ToUniversalTime().Date.AddDays(-1);effectiveTo=$null;reason='Constitute the temporary governed E2E-018 evaluation committee.'}
                $addedCommitteeMemberIds.Add([Guid]$created.id)
            }
            $committee=@(Invoke-JsonApi GET '/api/procurement/access-controls/committees' $adminToken $null)|Where-Object{$_.id -eq $committeeTemplateId}|Select-Object -First 1
        }
        if("$($committee.status)" -notin @('Active','1')){
            $committee=Invoke-JsonApi PUT "/api/procurement/access-controls/committees/$committeeTemplateId" $adminToken @{name=$committee.name;description=$committee.description;requiredQuorum=1;status='Active';effectiveFrom=(Get-Date).ToUniversalTime().Date.AddDays(-1);effectiveTo=$null;reason='Activate the source-scoped committee for governed E2E-018 acceptance.';rowVersion=$committee.rowVersion}
        }
        $setupChanged=$true

        $control=Invoke-JsonApi GET "/api/procurement/evaluation-committees?sourceType=RequestForQuotation&sourceId=$RfqId" $adminToken $null @(200,404)
        if(-not $control.id){
            $control=Invoke-JsonApi POST '/api/procurement/evaluation-committees/bind' $adminToken @{sourceType='RequestForQuotation';sourceId=$RfqId;committeeTemplateId=$committeeTemplateId;purpose='Evaluate the sealed E2E-018 air-filter quotation under the governed RFQ controls.';effectiveFromUtc=(Get-Date).ToUniversalTime().AddMinutes(-1);effectiveToUtc=$null;requiredRoles=@();idempotencyKey="e2e018-bind-$($RfqId.ToString('N'))"} @(201)
        }
        $control=Invoke-JsonApi POST "/api/procurement/evaluation-committees/$($control.id)/activate" $committeeAdminToken @{rowVersion=$control.rowVersion;evidenceReference='INV-E2E018-COMMITTEE-CONSTITUTION';idempotencyKey="e2e018-activate-$($RfqId.ToString('N'))"}
        foreach($appointment in @($control.members)){
            $actor=$actors|Where-Object{$_.Id -eq [Guid]$appointment.userId}|Select-Object -First 1
            if(-not $actor){throw "No acceptance actor is mapped to appointment $($appointment.id)."}
            $memberToken=$tokens[$actor.UserName]
            $accepted=Invoke-JsonApi POST "/api/procurement/evaluation-committees/appointments/$($appointment.id)/response" $memberToken @{accept=$true;rowVersion=$appointment.rowVersion;signatureReference="SIG-E2E018-$($appointment.userId)";evidenceReference='INV-E2E018-APPOINTMENT-ACCEPTANCE';reason='Accept the independent evaluation appointment.';idempotencyKey="e2e018-accept-$($appointment.id)"}
            $null=Invoke-JsonApi POST "/api/procurement/evaluation-committees/appointments/$($appointment.id)/conflict-declarations" $memberToken @{outcome='NoConflict';declaration='I have no actual, potential, or perceived conflict in this RFQ evaluation.';conflictDetails=$null;signatureReference="SIG-COI-$($appointment.userId)";evidenceReference='INV-E2E018-NO-CONFLICT-DECLARATION';workflowEvidenceDocumentId=$null;fileUploadRecordId=$null;validFromUtc=(Get-Date).ToUniversalTime().AddMinutes(-1);validToUtc=(Get-Date).ToUniversalTime().AddDays(30);appointmentRowVersion=$accepted.rowVersion;idempotencyKey="e2e018-coi-$($appointment.id)"} @(201)
        }
        $control=Invoke-JsonApi GET "/api/procurement/evaluation-committees?sourceType=RequestForQuotation&sourceId=$RfqId" $adminToken $null
        $meeting=Invoke-JsonApi POST "/api/procurement/evaluation-committees/$($control.id)/meetings" $committeeAdminToken @{phase='Combined';meetingMode='InPerson';meetingChannel='Procurement evaluation room';scheduledAtUtc=(Get-Date).ToUniversalTime();evidenceReference='INV-E2E018-EVALUATION-AGENDA';remoteMeetingEvidenceReference=$null;committeeRowVersion=$control.rowVersion;idempotencyKey="e2e018-meeting-$($RfqId.ToString('N'))"} @(201)
        if(-not $meeting.quorumMet){
            foreach($appointment in @($control.members)){
                $actor=$actors|Where-Object{$_.Id -eq [Guid]$appointment.userId}|Select-Object -First 1
                $current=Invoke-JsonApi GET "/api/procurement/evaluation-committees?sourceType=RequestForQuotation&sourceId=$RfqId" $tokens[$actor.UserName] $null
                $currentMeeting=@($current.meetings)|Where-Object{$_.id -eq $meeting.id}|Select-Object -First 1
                $currentAppointment=@($current.members)|Where-Object{$_.id -eq $appointment.id}|Select-Object -First 1
                $alreadySigned=@($currentMeeting.attendance)|Where-Object{$_.appointmentId -eq $appointment.id -and $_.isPresent}|Select-Object -First 1
                if(-not $alreadySigned){$null=Invoke-JsonApi POST "/api/procurement/evaluation-committees/meetings/$($meeting.id)/attendance" $tokens[$actor.UserName] @{isPresent=$true;signatureReference="SIG-ATTEND-$($appointment.userId)";evidenceReference='INV-E2E018-SIGNED-ATTENDANCE';meetingRowVersion=$currentMeeting.rowVersion;appointmentRowVersion=$currentAppointment.rowVersion;idempotencyKey="e2e018-attend-$($appointment.id)"} @(201)}
            }
            $control=Invoke-JsonApi GET "/api/procurement/evaluation-committees?sourceType=RequestForQuotation&sourceId=$RfqId" $adminToken $null
            $meeting=@($control.meetings)|Where-Object{$_.id -eq $meeting.id}|Select-Object -First 1
            $null=Invoke-JsonApi POST "/api/procurement/evaluation-committees/meetings/$($meeting.id)/quorum" $committeeAdminToken @{rowVersion=$meeting.rowVersion;evidenceReference='INV-E2E018-QUORUM-CONFIRMATION';remoteMeetingEvidenceReference=$null;idempotencyKey="e2e018-quorum-$($meeting.id)"}
        }

        $eligibility=Invoke-JsonApi GET "/api/procurement/evaluation-committees/scorer-eligibility?sourceType=RequestForQuotation&sourceId=$RfqId&phase=Combined" $evaluatorToken $null
        if(-not $eligibility.allowed){throw "Evaluator eligibility blocked: $($eligibility.blockedReasons -join '; ')"}
        $deadlineUtc=[DateTime]$rfq.submissionDeadline
        $waitSeconds=[Math]::Ceiling(($deadlineUtc.ToUniversalTime()-(Get-Date).ToUniversalTime()).TotalSeconds)+1
        if($waitSeconds -gt 0){Start-Sleep -Seconds ([Math]::Min($waitSeconds,180))}
        $opening=Invoke-JsonApi POST "/api/procurement/rfqs/$RfqId/opening-register" $adminToken @{evidenceReference='INV-E2E018-SEALED-QUOTE-OPENING';participants=@(@{participantUserId=$actors[0].Id;participantName='System Administrator';roleName='Opening officer';isObserver=$false;signatureReference='SIG-E2E018-OPENING-OFFICER'},@{participantUserId=$actors[3].Id;participantName='Independent Observer';roleName='Observer';isObserver=$true;signatureReference='SIG-E2E018-OPENING-OBSERVER'})} @(200)
    }
    $rfq=Invoke-JsonApi GET "/api/procurement/rfqs/$RfqId" $adminToken $null
    $rfqItems=@($rfq.items);$quote=@($rfq.quotes|Where-Object{$_.status -eq 'Submitted'})|Select-Object -First 1
    if($rfq.status -eq 'Evaluation'){
        $controlState=Invoke-JsonApi GET "/api/procurement/rfqs/$RfqId/controls" $evaluatorToken $null
        $evaluation=Invoke-JsonApi PUT "/api/procurement/rfqs/$RfqId/evaluation" $evaluatorToken @{awardMode='WinnerTakesAll';recommendationReason='The only responsive quotation met the controlled technical and commercial requirements.';evidenceReference='INV-E2E018-COMBINED-EVALUATION';lines=@($rfqItems|ForEach-Object{@{rfqItemId=$_.id;quoteId=$quote.id;technicalScore=88;commercialScore=90;totalScore=89;recommendationReason='Responsive and within the approved budget.'}});rowVersion=$controlState.evaluation.rowVersion}
        $evaluation=Invoke-JsonApi POST "/api/procurement/rfqs/$RfqId/evaluation/submit" $evaluatorToken @{rowVersion=$evaluation.rowVersion}
        foreach($additionalEvaluatorToken in $additionalEvaluatorTokens){
            $evaluation=Invoke-JsonApi POST "/api/procurement/rfqs/$RfqId/evaluation/submit" $additionalEvaluatorToken @{rowVersion=$evaluation.rowVersion}
        }
        $evaluation=Invoke-JsonApi POST "/api/procurement/rfqs/$RfqId/evaluation/decision" $approverToken @{action='Approve';comments='Independent approval of the governed evaluation recommendation.';approvalReference='INV-E2E018-EVALUATION-APPROVAL';rowVersion=$evaluation.rowVersion}
        if("$($evaluation.status)" -notin @('Approved','2')){throw "Evaluation did not reach Approved; current status is $($evaluation.status)."}
        $rfq=Invoke-JsonApi GET "/api/procurement/rfqs/$RfqId" $adminToken $null
    }
    if($rfq.status -eq 'Approved'){$award=Invoke-JsonApi POST "/api/procurement/rfqs/$RfqId/award" $approverToken @{}}
    elseif($rfq.status -ne 'Awarded'){throw "RFQ cannot continue to award from status $($rfq.status)."}
    $awardRows=Invoke-DbTable $connection 'SELECT Id AS PurchaseOrderId,OrderNumber,BusinessPartnerId,TotalAmount FROM PurchaseOrders WHERE TenantId=@tenantId AND SourceRfqId=@rfqId AND IsDeleted=0;' @{tenantId=$tenantId;rfqId=$RfqId}
    $award=[pscustomobject]@{purchaseOrders=@($awardRows.Rows|ForEach-Object{[pscustomobject]@{purchaseOrderId=$_.PurchaseOrderId;orderNumber=$_.OrderNumber;businessPartnerId=$_.BusinessPartnerId;totalAmount=$_.TotalAmount}})}
    $finalRfq=Invoke-JsonApi GET "/api/procurement/rfqs/$RfqId" $adminToken $null
    $controlRows=Invoke-DbTable $connection 'SELECT TOP(1) Id FROM ProcurementEvaluationCommitteeControls WHERE TenantId=@tenantId AND SourceId=@rfqId AND IsDeleted=0 ORDER BY Version DESC;' @{tenantId=$tenantId;rfqId=$RfqId}
    $openingRows=Invoke-DbTable $connection 'SELECT TOP(1) Id FROM ProcurementRfqOpeningRegisters WHERE TenantId=@tenantId AND RfqId=@rfqId AND IsDeleted=0 ORDER BY CreatedAt DESC;' @{tenantId=$tenantId;rfqId=$RfqId}
    $evaluationRows=Invoke-DbTable $connection 'SELECT TOP(1) Id,Status FROM ProcurementRfqEvaluations WHERE TenantId=@tenantId AND RfqId=@rfqId AND IsDeleted=0 ORDER BY CreatedAt DESC;' @{tenantId=$tenantId;rfqId=$RfqId}
    [pscustomobject]@{RfqId=$RfqId;RfqNumber=$finalRfq.rfqNumber;RfqStatus=$finalRfq.status;CommitteeControlId=$controlRows.Rows[0].Id;OpeningRegisterId=$openingRows.Rows[0].Id;EvaluationId=$evaluationRows.Rows[0].Id;EvaluationStatus=$evaluationRows.Rows[0].Status;PurchaseOrders=$award.purchaseOrders}|ConvertTo-Json -Depth 10 -Compress
}
finally {
    if($committeeTemporarilyScoped -and $committeeBackup -and $adminToken){
        try{
            $current=@(Invoke-JsonApi GET '/api/procurement/access-controls/committees' $adminToken $null)|Where-Object{$_.id -eq $committeeTemplateId}|Select-Object -First 1
            if($current.status -ne 'Draft'){
                $current=Invoke-JsonApi PUT "/api/procurement/access-controls/committees/$committeeTemplateId" $adminToken @{name=$current.name;description=$current.description;requiredQuorum=1;status='Draft';effectiveFrom=$current.effectiveFrom;effectiveTo=$current.effectiveTo;reason='Restore the reusable evaluation committee after E2E-018 source binding.';rowVersion=$current.rowVersion}
            }
            foreach($member in @($current.members)){
                $backupMember=@($committeeBackup.members|Where-Object{
                    $_.userId -eq $member.userId -and
                    "$($_.memberKind)" -eq "$($member.memberKind)" -and
                    [bool]$_.isVoting -eq [bool]$member.isVoting
                })|Select-Object -First 1
                if(-not $backupMember){
                    $null=Invoke-JsonApi DELETE "/api/procurement/access-controls/committees/$committeeTemplateId/members/$($member.id)" $adminToken @{reason='Remove temporary source-binding membership before restoring the reusable committee.';rowVersion=$member.rowVersion} @(204)
                    $current=@(Invoke-JsonApi GET '/api/procurement/access-controls/committees' $adminToken $null)|Where-Object{$_.id -eq $committeeTemplateId}|Select-Object -First 1
                }
            }
            foreach($member in @($committeeBackup.members)){
                if(-not (@($current.members)|Where-Object{
                    $_.userId -eq $member.userId -and
                    "$($_.memberKind)" -eq "$($member.memberKind)" -and
                    [bool]$_.isVoting -eq [bool]$member.isVoting
                })){
                    $null=Invoke-JsonApi POST "/api/procurement/access-controls/committees/$committeeTemplateId/members" $adminToken @{assignmentId=$member.assignmentId;memberKind=$member.memberKind;isVoting=$member.isVoting;effectiveFrom=$member.effectiveFrom;effectiveTo=$member.effectiveTo;reason='Restore the reusable committee membership after E2E-018 acceptance.'}
                    $current=@(Invoke-JsonApi GET '/api/procurement/access-controls/committees' $adminToken $null)|Where-Object{$_.id -eq $committeeTemplateId}|Select-Object -First 1
                }
            }
            $current=@(Invoke-JsonApi GET '/api/procurement/access-controls/committees' $adminToken $null)|Where-Object{$_.id -eq $committeeTemplateId}|Select-Object -First 1
            $null=Invoke-JsonApi PUT "/api/procurement/access-controls/committees/$committeeTemplateId" $adminToken @{name=$committeeBackup.name;description=$committeeBackup.description;requiredQuorum=$committeeBackup.requiredQuorum;status=$committeeBackup.status;effectiveFrom=$committeeBackup.effectiveFrom;effectiveTo=$committeeBackup.effectiveTo;reason='Complete restoration of the reusable evaluation committee after E2E-018 acceptance.';rowVersion=$current.rowVersion}
        }catch{Write-Warning "Committee template restoration requires attention: $($_.Exception.Message)"}
    }
    # Restore test-only identity state even when the governed acceptance path fails.
    foreach($membership in $addedRoleMemberships){try{$null=Invoke-DbNonQuery $connection 'DELETE FROM UserRoles WHERE UserId=@userId AND RoleId=@roleId;' @{userId=$membership.UserId;roleId=$membership.RoleId}}catch{}}
    foreach($actorId in $passwordBackups.Keys){$backup=$passwordBackups[$actorId];try{$null=Invoke-DbNonQuery $connection 'UPDATE Users SET PasswordHash=@hash,AccessFailedCount=@failed,LockoutEnd=@lockout WHERE Id=@id;' @{id=$actorId;hash=$backup.PasswordHash;failed=$backup.AccessFailedCount;lockout=$backup.LockoutEnd}}catch{}}
    if($connection.State -eq [System.Data.ConnectionState]::Open){$connection.Close()};$connection.Dispose()
}
