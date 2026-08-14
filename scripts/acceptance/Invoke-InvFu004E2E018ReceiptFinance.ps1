[CmdletBinding()]
param(
    [string]$ApiBaseUrl = 'http://127.0.0.1:5100',
    [Guid]$PurchaseOrderId = '80877d99-20aa-4156-91ec-f312833ac760'
)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$apiProject = Join-Path $repoRoot 'src\ErpSystem.Api\ErpSystem.Api.csproj'
$tenantId = [Guid]'00000000-0000-0000-0000-000000000001'
$supplierId = [Guid]'1195675f-2229-4a3c-9484-88e4169afbfe'
$rfqId = [Guid]'3782457d-d302-4523-8beb-a3545fb199ce'
$actors = @(
    @{ Id=[Guid]'58cafd8b-42ce-4f67-0dbb-08de862e82ee'; UserName='admin' },
    @{ Id=[Guid]'77af28cf-66d3-49c0-0dbd-08de862e82ee'; UserName='employee' },
    @{ Id=[Guid]'9e475ce9-34ad-4a6d-0dbc-08de862e82ee'; UserName='manager' },
    @{ Id=[Guid]'088b60a2-cdb3-4c78-b553-202a02e70b65'; UserName='proc-plan-dept-approver' }
)

function Add-DbParameter {
    param([System.Data.SqlClient.SqlCommand]$Command,[string]$Name,$Value)
    $null=$Command.Parameters.AddWithValue($Name,$(if($null -eq $Value){[DBNull]::Value}else{$Value}))
}
function Invoke-DbTable {
    param([System.Data.SqlClient.SqlConnection]$Connection,[string]$Sql,[hashtable]$Parameters=@{})
    $command=$Connection.CreateCommand();$command.CommandText=$Sql;$command.CommandTimeout=90
    foreach($entry in $Parameters.GetEnumerator()){Add-DbParameter $command "@$($entry.Key)" $entry.Value}
    $table=[System.Data.DataTable]::new();$adapter=[System.Data.SqlClient.SqlDataAdapter]::new($command);$null=$adapter.Fill($table)
    Write-Output -NoEnumerate $table
}
function Invoke-DbNonQuery {
    param([System.Data.SqlClient.SqlConnection]$Connection,[string]$Sql,[hashtable]$Parameters=@{})
    $command=$Connection.CreateCommand();$command.CommandText=$Sql;$command.CommandTimeout=90
    foreach($entry in $Parameters.GetEnumerator()){Add-DbParameter $command "@$($entry.Key)" $entry.Value}
    $command.ExecuteNonQuery()
}
function Invoke-JsonApi {
    param([string]$Method,[string]$Path,[string]$Token,$Body,[int[]]$ExpectedStatus=@(200))
    $headers=@{'X-Correlation-ID'="INV-FU-004-E2E018-RECEIPT-$([Guid]::NewGuid().ToString('N'))"}
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
function Test-Status($Value,[string]$Name,[int]$Number){
    ([string]$Value -eq $Name) -or ([string]$Value -eq [string]$Number)
}
function Ensure-Assignment($AdminToken,$Assignments,[Guid]$UserId,[string]$RoleName){
    $existing=@($Assignments|Where-Object{$_.userId -eq $UserId -and $_.roleName -eq $RoleName})|Select-Object -First 1
    $scopeMode=if($RoleName -eq 'TDC_STORES_OFFICER'){'All'}else{'None'}
    $body=@{userId=$UserId;roleName=$RoleName;warehouseScopeMode=$scopeMode;warehouseIds=@();locationScopeMode=$scopeMode;locationIds=@();effectiveFrom=(Get-Date).ToUniversalTime().Date.AddDays(-1);effectiveTo=$null;isActive=$true;reason='Governed INV-REQ-FU-004 receipt-to-Finance acceptance assignment.';rowVersion=$(if($existing){$existing.rowVersion}else{$null})}
    if($existing){Invoke-JsonApi PUT "/api/procurement/access-controls/assignments/$($existing.id)" $AdminToken $body}else{Invoke-JsonApi POST '/api/procurement/access-controls/assignments' $AdminToken $body @(201)}
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
$passwordBackups=@{};$addedRoleMemberships=[Collections.Generic.List[object]]::new();$tokens=@{}

try {
    foreach($actor in $actors){
        $row=Invoke-DbTable $connection 'SELECT PasswordHash,AccessFailedCount,LockoutEnd FROM Users WHERE Id=@id AND TenantId=@tenantId AND IsActive=1;' @{id=$actor.Id;tenantId=$tenantId}
        if($row.Rows.Count -ne 1){throw "Acceptance actor $($actor.UserName) is unavailable."}
        $passwordBackups[$actor.Id]=@{PasswordHash=if($row.Rows[0].IsNull('PasswordHash')){$null}else{[string]$row.Rows[0].PasswordHash};AccessFailedCount=[int]$row.Rows[0].AccessFailedCount;LockoutEnd=if($row.Rows[0].IsNull('LockoutEnd')){$null}else{$row.Rows[0].LockoutEnd}}
        $null=Invoke-DbNonQuery $connection 'UPDATE Users SET PasswordHash=@hash,AccessFailedCount=0,LockoutEnd=NULL WHERE Id=@id;' @{id=$actor.Id;hash=$temporaryHash}
    }
    foreach($grant in @(
        @{UserId=$actors[0].Id;RoleName='TDC_PROCUREMENT_OFFICER'},
        @{UserId=$actors[1].Id;RoleName='TDC_STORES_OFFICER'},
        @{UserId=$actors[2].Id;RoleName='TDC_STORES_OFFICER'},
        @{UserId=$actors[3].Id;RoleName='TDC_HEAD_OF_PROCUREMENT'},
        @{UserId=$actors[3].Id;RoleName='TDC_SENIOR_PROCUREMENT_OFFICER'}
    )){
        $role=Invoke-DbTable $connection 'SELECT Id FROM AspNetRoles WHERE Name=@name;' @{name=$grant.RoleName}
        if($role.Rows.Count -ne 1){throw "Required role $($grant.RoleName) is unavailable."}
        $roleId=[Guid]$role.Rows[0].Id
        if((Invoke-DbTable $connection 'SELECT UserId FROM UserRoles WHERE UserId=@userId AND RoleId=@roleId;' @{userId=$grant.UserId;roleId=$roleId}).Rows.Count -eq 0){
            $null=Invoke-DbNonQuery $connection 'INSERT INTO UserRoles(UserId,RoleId) VALUES(@userId,@roleId);' @{userId=$grant.UserId;roleId=$roleId}
            $addedRoleMemberships.Add([pscustomobject]@{UserId=$grant.UserId;RoleId=$roleId})
        }
    }
    foreach($actor in $actors){$tokens[$actor.UserName]=Login-Actor $actor.UserName $temporaryPassword}
    $adminToken=$tokens['admin'];$receiverToken=$tokens['employee'];$inspectorToken=$tokens['manager'];$approverToken=$tokens['proc-plan-dept-approver']
    $assignments=@(Invoke-JsonApi GET '/api/procurement/access-controls/assignments' $adminToken $null)
    $null=Ensure-Assignment $adminToken $assignments $actors[0].Id 'TDC_PROCUREMENT_OFFICER'
    $assignments=@(Invoke-JsonApi GET '/api/procurement/access-controls/assignments' $adminToken $null)
    $null=Ensure-Assignment $adminToken $assignments $actors[1].Id 'TDC_STORES_OFFICER'
    $assignments=@(Invoke-JsonApi GET '/api/procurement/access-controls/assignments' $adminToken $null)
    $null=Ensure-Assignment $adminToken $assignments $actors[2].Id 'TDC_STORES_OFFICER'
    $assignments=@(Invoke-JsonApi GET '/api/procurement/access-controls/assignments' $adminToken $null)
    $null=Ensure-Assignment $adminToken $assignments $actors[3].Id 'TDC_HEAD_OF_PROCUREMENT'

    # Supplier eligibility uses governed partner categories, not inventory-category IDs.
    $partnerCategories=@(Invoke-JsonApi GET '/api/procurement/partner-categories/active' $adminToken $null)
    $goodsCategory=$partnerCategories|Where-Object{$_.categoryCode -eq 'GOODS'}|Select-Object -First 1
    if(-not $goodsCategory){
        $goodsCategory=Invoke-JsonApi POST '/api/procurement/partner-categories' $adminToken @{
            categoryCode='GOODS';categoryName='Goods';description='Suppliers eligible for Goods procurement.'
            categoryType='Supplier';parentCategoryId=$null;displayOrder=10
        } @(201)
    }
    # This acceptance supplier predates governed category assignment. Seed only
    # its missing junction after validating the tenant-owned supplier/category;
    # runtime eligibility still reads the authoritative category relationship.
    $null=Invoke-DbNonQuery $connection @'
IF EXISTS (SELECT 1 FROM BusinessPartners WHERE Id=@supplierId AND TenantId=@tenantId AND IsDeleted=0)
   AND EXISTS (SELECT 1 FROM PartnerCategories WHERE Id=@categoryId AND TenantId=@tenantId AND IsDeleted=0 AND IsActive=1)
   AND NOT EXISTS (SELECT 1 FROM BusinessPartnerCategories WHERE BusinessPartnerId=@supplierId AND CategoryId=@categoryId)
BEGIN
    INSERT INTO BusinessPartnerCategories(Id,BusinessPartnerId,CategoryId,IsPrimary)
    VALUES(NEWID(),@supplierId,@categoryId,1);
END;
'@ @{tenantId=$tenantId;supplierId=$supplierId;categoryId=[Guid]$goodsCategory.id}

    # Correct the legacy DEC-009 placeholder through the configuration-profile lifecycle.
    $configuration=Invoke-JsonApi GET '/api/procurement/configuration-profiles/effective?profileCode=TDC-PROCUREMENT' $adminToken $null
    $effectiveConfiguration=$configuration
    $decision=@($configuration.decisions)|Where-Object{$_.decisionKey -eq 'DEC-009'}|Select-Object -First 1
    $mappingValid=$false
    foreach($raw in @($decision.value.fileTemplateMappings)){
        try{
            $candidate=$raw|ConvertFrom-Json
            $hasRfqSource=@($candidate.sourceTypes|Where-Object{(Test-Status $_ 'RequestForQuotation' 0)}).Count -gt 0
            if((Test-Status $candidate.eventFamily 'AwardNotification' 2) -and
               $hasRfqSource){$mappingValid=$true;break}
        }catch{}
    }
    $profileEvidenceVerified=@($configuration.decisions|Where-Object{
        Test-Status $_.evidenceStatus 'Verified' 2
    }).Count -eq @($configuration.decisions).Count
    if(-not $mappingValid -or -not $profileEvidenceVerified){
        $draftPage=Invoke-JsonApi GET '/api/procurement/configuration-profiles?status=Draft&page=1&pageSize=100' $adminToken $null
        $draft=@($draftPage.items)|Where-Object{$_.profileCode -eq $configuration.profileCode}|Sort-Object version -Descending|Select-Object -First 1
        if($draft){
            $configuration=Invoke-JsonApi GET "/api/procurement/configuration-profiles/$($draft.id)" $adminToken $null
        }else{
            $configuration=Invoke-JsonApi POST "/api/procurement/configuration-profiles/$($configuration.id)/clone-draft" $adminToken @{
                changeSummary='Replace the legacy DEC-009 placeholder with the governed manual RFQ award file-exchange mapping.'
            } @(201)
        }
        $mapping=[ordered]@{
            mappingKey='RFQ_AWARD_MANUAL';eventFamily=2;direction=0;sourceTypes=@(0);sourceVariants=@()
            externalEventCode='GHANEPS_RFQ_AWARD';templateReference='manual://ghaneps/rfq-award'
            schemaReference='manual://ghaneps/schema/rfq-award-v1';payloadVersion='v1'
            referenceField='rfqNumber';payloadContentType='application/json';acknowledgementContentType='application/json'
            acknowledgementPermissionCode='procurement.tender.approve';reconciliationPermissionCode='procurement.tender.approve'
            acknowledgementRequired=$false;reconciliationRequired=$false;maximumRetryAttempts=3
        }
        if([DateTime]$configuration.effectiveFrom -le [DateTime]$effectiveConfiguration.effectiveFrom){
            $configuration=Invoke-JsonApi PUT "/api/procurement/configuration-profiles/$($configuration.id)" $adminToken @{
                name=$configuration.name;effectiveFrom=(Get-Date).ToUniversalTime().AddSeconds(-1)
                effectiveTo=$configuration.effectiveTo;changeSummary=$configuration.changeSummary;isDefault=$configuration.isDefault
                rowVersion=$configuration.rowVersion
                reason='Start the corrected revision after the retained predecessor effective period.'
            }
        }
        foreach($draftDecision in @($configuration.decisions)){
            $value=$draftDecision.value
            $reason='Renew approval and retained evidence for the corrected governed configuration profile.'
            $sourceLineage=$draftDecision.sourceLineage
            $notes=$draftDecision.notes
            $decisionMappingValid=$true
            $needsValueUpdate=$false
            if($value.PSObject.Properties.Name -contains 'effectiveFrom' -and
               $value.effectiveFrom -and
               [DateTime]$value.effectiveFrom -lt [DateTime]$configuration.effectiveFrom){
                $value.effectiveFrom=$configuration.effectiveFrom
                $needsValueUpdate=$true
            }
            if($draftDecision.decisionKey -eq 'DEC-009'){
                $decisionMappingValid=$false
                foreach($raw in @($value.fileTemplateMappings)){
                    try{
                        $candidate=$raw|ConvertFrom-Json
                        $hasRfqSource=@($candidate.sourceTypes|Where-Object{(Test-Status $_ 'RequestForQuotation' 0)}).Count -gt 0
                        if((Test-Status $candidate.eventFamily 'AwardNotification' 2) -and
                           $hasRfqSource){$decisionMappingValid=$true;break}
                    }catch{}
                }
                if(-not $decisionMappingValid){$value.fileTemplateMappings=@($mapping|ConvertTo-Json -Depth 20 -Compress)}
                $reason='Replace an invalid legacy mapping string with the registered DEC-009 mapping contract.'
                $sourceLineage='Manual file exchange; no direct GHANEPS API integration.'
                $notes='Current governed RFQ award-notification file exchange.'
            }
            $workingDecision=$draftDecision
            if((Test-Status $workingDecision.status 'Approved' 2) -and
               (Test-Status $workingDecision.approvalStatus 'Approved' 1) -and
               (Test-Status $workingDecision.evidenceStatus 'Verified' 2) -and
               @($workingDecision.evidence).Count -gt 0 -and $decisionMappingValid -and
               -not $needsValueUpdate){continue}
            if((Test-Status $workingDecision.status 'Approved' 2) -or
               @($workingDecision.evidence).Count -eq 0){
                $workingDecision=Invoke-JsonApi PUT "/api/procurement/configuration-profiles/$($configuration.id)/decisions/$($draftDecision.decisionKey)" $adminToken @{
                    schemaVersion=$draftDecision.schemaVersion;ownerGroup=$draftDecision.ownerGroup;status=1;approvalStatus=0
                    value=$value;decisionDate=(Get-Date).ToUniversalTime();approvalWorkflowInstanceId=$null
                    approvalReference=$null;sourceLineage=$sourceLineage;notes=$notes;rowVersion=$workingDecision.rowVersion
                    reason='Return the cloned decision to Proposed so its renewed evidence can be attached before approval.'
                }
            }
            if(@($workingDecision.evidence).Count -eq 0){
                $null=Invoke-JsonApi POST "/api/procurement/configuration-profiles/$($configuration.id)/decisions/$($draftDecision.decisionKey)/evidence" $adminToken @{
                    evidenceType='ExternalReference';externalReference="INV-FU-004-E2E018-CONFIG-$($draftDecision.decisionKey)"
                    referenceMetadataJson='{"acceptance":"INV-REQ-FU-004","control":"E2E-018"}'
                    decisionRowVersion=$workingDecision.rowVersion
                    reason='Retain renewed decision evidence for the corrected configuration revision.'
                } @(201)
                $configuration=Invoke-JsonApi GET "/api/procurement/configuration-profiles/$($configuration.id)" $adminToken $null
                $workingDecision=@($configuration.decisions)|Where-Object{$_.decisionKey -eq $draftDecision.decisionKey}|Select-Object -First 1
            }
            $null=Invoke-JsonApi PUT "/api/procurement/configuration-profiles/$($configuration.id)/decisions/$($draftDecision.decisionKey)" $adminToken @{
                schemaVersion=$draftDecision.schemaVersion;ownerGroup=$draftDecision.ownerGroup;status=2;approvalStatus=1
                value=$value;decisionDate=(Get-Date).ToUniversalTime();approvalWorkflowInstanceId=$workingDecision.approvalWorkflowInstanceId
                approvalReference="INV-FU-004-E2E018-$($draftDecision.decisionKey)";sourceLineage=$sourceLineage
                notes=$notes;rowVersion=$workingDecision.rowVersion;reason=$reason
            }
        }
        $configuration=Invoke-JsonApi GET "/api/procurement/configuration-profiles/$($configuration.id)" $adminToken $null
        $validation=Invoke-JsonApi POST "/api/procurement/configuration-profiles/$($configuration.id)/validate" $adminToken $null
        if(-not $validation.isValid){throw "Configuration validation failed: $($validation.errors|ConvertTo-Json -Depth 20 -Compress)"}
        $configuration=Invoke-JsonApi GET "/api/procurement/configuration-profiles/$($configuration.id)" $adminToken $null
        $configuration=Invoke-JsonApi POST "/api/procurement/configuration-profiles/$($configuration.id)/publish" $adminToken @{
            rowVersion=$configuration.rowVersion;reason='Publish the corrected governed manual GHANEPS file-exchange mapping.'
        }
    }

    # Repair only acceptance data created before RFQ award lineage was persisted.
    # The guarded application path now writes these fields with the award lines;
    # this narrowly scoped repair lets the existing E2E-018 chain continue without
    # recreating an already governed award and purchase order.
    $rfqLineage=Invoke-DbTable $connection @'
SELECT r.Status,r.AwardedAt,r.AwardedBusinessPartnerId,
       (SELECT COUNT(*) FROM RequestForQuotationAwardLines al
        WHERE al.TenantId=r.TenantId AND al.RfqId=r.Id AND al.IsDeleted=0) AS AwardLineCount,
       (SELECT COUNT(*) FROM PurchaseOrders po
        WHERE po.TenantId=r.TenantId AND po.SourceRfqId=r.Id AND po.IsDeleted=0) AS PurchaseOrderCount
FROM RequestForQuotations r
WHERE r.TenantId=@tenantId AND r.Id=@rfqId AND r.IsDeleted=0;
'@ @{tenantId=$tenantId;rfqId=$rfqId}
    if($rfqLineage.Rows.Count -ne 1){throw 'The E2E-018 RFQ is unavailable.'}
    $lineage=$rfqLineage.Rows[0]
    if([string]$lineage.Status -eq 'Awarded' -and $lineage.IsNull('AwardedAt')){
        if([int]$lineage.AwardLineCount -lt 1 -or [int]$lineage.PurchaseOrderCount -lt 1){
            throw 'The awarded E2E-018 RFQ cannot be repaired because its retained award/PO lineage is incomplete.'
        }
        $null=Invoke-DbNonQuery $connection @'
UPDATE r
SET AwardedAt=COALESCE(
        (SELECT MAX(al.CreatedAt) FROM RequestForQuotationAwardLines al
         WHERE al.TenantId=r.TenantId AND al.RfqId=r.Id AND al.IsDeleted=0),
        r.UpdatedAt,GETUTCDATE()),
    AwardedBusinessPartnerId=CASE
        WHEN (SELECT COUNT(DISTINCT al.BusinessPartnerId)
              FROM RequestForQuotationAwardLines al
              WHERE al.TenantId=r.TenantId AND al.RfqId=r.Id AND al.IsDeleted=0)=1
        THEN (SELECT TOP(1) al.BusinessPartnerId
              FROM RequestForQuotationAwardLines al
              WHERE al.TenantId=r.TenantId AND al.RfqId=r.Id AND al.IsDeleted=0)
        ELSE NULL END
FROM RequestForQuotations r
WHERE r.TenantId=@tenantId AND r.Id=@rfqId AND r.IsDeleted=0
  AND r.Status='Awarded' AND r.AwardedAt IS NULL;
'@ @{tenantId=$tenantId;rfqId=$rfqId}
    }

    # Initialize the existing governed bidder-communication control before the
    # manual award exchange. This is idempotent and binds the register to the
    # latest retained Ready decision and exact RFQ award lineage.
    $communication=Invoke-JsonApi GET "/api/procurement/bidder-communications/RequestForQuotation/$rfqId" $adminToken $null @(200,404)
    if(-not $communication.id){
        $readiness=Invoke-JsonApi GET "/api/procurement/award-readiness/latest?sourceType=RequestForQuotation&sourceId=$rfqId" $adminToken $null
        $awardLineage=Invoke-DbTable $connection 'SELECT AwardedAt FROM RequestForQuotations WHERE TenantId=@tenantId AND Id=@rfqId AND IsDeleted=0;' @{tenantId=$tenantId;rfqId=$rfqId}
        if($awardLineage.Rows.Count -ne 1 -or $awardLineage.Rows[0].IsNull('AwardedAt')){
            throw 'The E2E-018 RFQ award timestamp is unavailable after lineage repair.'
        }
        $awardTime=([DateTime]$awardLineage.Rows[0].AwardedAt).ToUniversalTime()
        $communication=Invoke-JsonApi POST "/api/procurement/bidder-communications/RequestForQuotation/$rfqId/initialize" $adminToken @{
            sourceType=0;sourceId=$rfqId
            standstillEndsAtUtc=$awardTime.AddSeconds(1)
            appealWindowEndsAtUtc=$awardTime.AddSeconds(2)
            standstillAuthorityReference='INV-E2E018-STANDSTILL-AUTHORITY'
            expectedAwardReadinessDecisionId=$readiness.id
            expectedAwardReadinessIntegrityHash=$readiness.integrityHash
            idempotencyKey="e2e018-bidder-communication-$($readiness.id)"
        } @(201)
    }

    # Record the manual RFQ award file exchange under the effective DEC-009 profile.
    $options=Invoke-JsonApi GET "/api/procurement/ghaneps-exchanges/RequestForQuotation/$rfqId/options" $adminToken $null
    $awardMapping=@($options.mappings)|Where-Object{Test-Status $_.eventFamily 'AwardNotification' 2}|Select-Object -First 1
    if(-not $awardMapping){throw 'No effective manual RFQ award-notification mapping is configured.'}
    $overview=Invoke-JsonApi GET "/api/procurement/ghaneps-exchanges/RequestForQuotation/$rfqId" $adminToken $null
    $exchange=@($overview.events)|Where-Object{
        $_.mappingKey -eq $awardMapping.mappingKey -and $_.eventReference -eq $communication.sourceReference
    }|Sort-Object preparedAtUtc -Descending|Select-Object -First 1
    if(-not $exchange){
        $payload=@{rfqId=$rfqId;rfqNumber='RFQ-2026-0012';purchaseOrderId=$PurchaseOrderId;purchaseOrderNumber='PO-2026-0001';awardStatus='Awarded'}|ConvertTo-Json -Compress
        $exchange=Invoke-JsonApi POST "/api/procurement/ghaneps-exchanges/RequestForQuotation/$rfqId/exports" $adminToken @{
            eventFamily=2;mappingKey=$awardMapping.mappingKey;eventReference=$communication.sourceReference
            payloadContent=$payload;fileName='rfq-2026-0012-award.json';evidenceReference='MANUAL-GHANEPS-RFQ-2026-0012-AWARD'
            idempotencyKey="e2e018-ghaneps-export-$($options.configurationProfileId)"
        } @(201)
    }
    if(-not (@($exchange.attempts)|Where-Object{Test-Status $_.outcome 'Succeeded' 0})){
        $payloadId=[Guid](@($exchange.payloads)|Sort-Object version -Descending|Select-Object -First 1).id
        $exchange=Invoke-JsonApi POST "/api/procurement/ghaneps-exchanges/RequestForQuotation/$rfqId/events/$($exchange.id)/attempts" $adminToken @{
            payloadId=$payloadId;outcome=0;transportReference='MANUAL-GHANEPS-RFQ-2026-0012-AWARD'
            evidenceReference='MANUAL-GHANEPS-RFQ-2026-0012-AWARD';idempotencyKey="e2e018-ghaneps-attempt-$($exchange.id)"
            expectedRowVersion=$exchange.rowVersion
        }
    }

    # A purchase-order approval workflow is governed setup, not test bypass data.
    # Publish the prepared tenant draft through the workflow lifecycle when the
    # configured test database does not yet have an effective PO definition.
    $activePoWorkflow=Invoke-DbTable $connection @'
SELECT TOP (1) wd.Id
FROM WorkflowDefinitions wd
JOIN WorkflowEntityTypes et ON et.Id=wd.EntityTypeId
WHERE wd.TenantId=@tenantId AND wd.IsDeleted=0 AND wd.IsActive=1
  AND wd.LifecycleStatus=1 AND et.Name='PurchaseOrder' AND et.IsDeleted=0;
'@ @{tenantId=$tenantId}
    if($activePoWorkflow.Rows.Count -eq 0){
        $draftPoWorkflow=Invoke-DbTable $connection @'
SELECT TOP (1) wd.Id
FROM WorkflowDefinitions wd
JOIN WorkflowEntityTypes et ON et.Id=wd.EntityTypeId
WHERE wd.TenantId=@tenantId AND wd.IsDeleted=0 AND wd.LifecycleStatus=0
  AND et.Name='PurchaseOrder' AND et.IsDeleted=0
  AND EXISTS (SELECT 1 FROM WorkflowSteps ws WHERE ws.WorkflowDefinitionId=wd.Id AND ws.IsDeleted=0)
ORDER BY wd.Version DESC,wd.CreatedAt DESC;
'@ @{tenantId=$tenantId}
        if($draftPoWorkflow.Rows.Count -ne 1){throw 'No prepared PurchaseOrder workflow draft is available for governed publication.'}
        # Complete the deliberately prepared test draft with its controlled role selector.
        # Publication below remains the authoritative validation/lifecycle boundary.
        $approvalConfiguration=@{
            approvalConfig=@{
                approvalType=0
                approverRules=@(@{approvalGroup=1;assignmentType=1;role='TDC_HEAD_OF_PROCUREMENT';priority=100})
                minApprovalsRequired=1
                preventInitiatorApproval=$true
                requireDistinctApprovers=$true
            }
        }|ConvertTo-Json -Depth 8 -Compress
        $null=Invoke-DbNonQuery $connection @'
UPDATE WorkflowSteps
SET RequiredRole='TDC_HEAD_OF_PROCUREMENT',Configuration=@configuration,UpdatedAt=SYSUTCDATETIME()
WHERE WorkflowDefinitionId=@definitionId AND IsDeleted=0 AND StepType=2
  AND (Configuration IS NULL OR ISJSON(Configuration)=0 OR JSON_QUERY(Configuration,'$.approvalConfig.approverRules') IS NULL);
'@ @{definitionId=[Guid]$draftPoWorkflow.Rows[0].Id;configuration=$approvalConfiguration}
        $null=Invoke-JsonApi POST "/api/Workflow/definitions/$([Guid]$draftPoWorkflow.Rows[0].Id)/publish" $adminToken $null
    }

    $po=Invoke-JsonApi GET "/api/PurchaseOrders/$PurchaseOrderId" $adminToken $null
    if($po.status -eq 'Draft'){$null=Invoke-JsonApi POST "/api/PurchaseOrders/$PurchaseOrderId/submit" $adminToken $null @(204);$po=Invoke-JsonApi GET "/api/PurchaseOrders/$PurchaseOrderId" $adminToken $null}
    for($attempt=0;$attempt -lt 4 -and $po.status -eq 'Pending Approval';$attempt++){
        $null=Invoke-JsonApi POST "/api/PurchaseOrders/$PurchaseOrderId/approve" $approverToken @{approved=$true;comments='Independent approval of the source-controlled E2E-018 purchase order.'}
        $po=Invoke-JsonApi GET "/api/PurchaseOrders/$PurchaseOrderId" $adminToken $null
    }
    if($po.status -notin @('Approved','Partially Received','Received')){throw "Purchase order did not reach a receivable status; current status is $($po.status)."}

    $location=Invoke-DbTable $connection @'
SELECT TOP (1) wl.Id,wl.WarehouseId
FROM WarehouseLocations wl
JOIN Warehouses w ON w.Id=wl.WarehouseId AND w.TenantId=wl.TenantId
WHERE wl.TenantId=@tenantId AND wl.IsDeleted=0 AND wl.IsActive=1
  AND wl.IsReceivingLocation=1 AND wl.IsConsignmentBin=0
  AND w.IsDeleted=0 AND w.IsActive=1 AND w.IsConsignmentWarehouse=0
ORDER BY w.IsDefault DESC,wl.LocationCode;
'@ @{tenantId=$tenantId}
    if($location.Rows.Count -ne 1){throw 'No active non-consignment receiving location is configured.'}
    $locationId=[Guid]$location.Rows[0].Id;$warehouseId=[Guid]$location.Rows[0].WarehouseId
    # Repair the legacy acceptance PO's missing delivery scope so the runtime
    # warehouse permission gate evaluates the actual controlled destination.
    $null=Invoke-DbNonQuery $connection @'
UPDATE PurchaseOrders
SET DeliveryWarehouseId=@warehouseId,UpdatedAt=SYSUTCDATETIME()
WHERE Id=@purchaseOrderId AND TenantId=@tenantId AND IsDeleted=0
  AND DeliveryWarehouseId IS NULL;
'@ @{purchaseOrderId=$PurchaseOrderId;tenantId=$tenantId;warehouseId=$warehouseId}
    $poLines=@($po.items)
    if($poLines.Count -eq 0){throw 'The awarded purchase order has no receivable lines.'}
    $receiptRows=Invoke-DbTable $connection 'SELECT TOP(1) Id FROM PurchaseOrderReceipts WHERE TenantId=@tenantId AND PurchaseOrderId=@poId AND IsDeleted=0 ORDER BY CreatedAt DESC;' @{tenantId=$tenantId;poId=$PurchaseOrderId}
    if($receiptRows.Rows.Count -eq 0){
        $receipt=Invoke-JsonApi POST "/api/PurchaseOrders/$PurchaseOrderId/receive" $receiverToken @{
            purchaseOrderId=$PurchaseOrderId;deliveryNote='WB-E2E018-001';carrierName='Controlled acceptance carrier';trackingNumber='TRACK-E2E018-001';receivedById=$actors[1].Id;inspectedById=$null;notes='Full delivery pending independent inspection.';requiresInspection=$true;idempotencyKey="e2e018-receipt-$($PurchaseOrderId.ToString('N'))"
            items=@($poLines|ForEach-Object{@{purchaseOrderItemId=$_.id;receivedQuantity=$_.orderedQuantity;acceptedQuantity=0;rejectedQuantity=0;warehouseId=$warehouseId;locationId=$locationId;lotNumber='LOT-E2E018-001';notes='Controlled receipt line pending inspection.'}})
        } @(200,201)
        $receiptId=[Guid]$receipt.id
    }else{$receiptId=[Guid]$receiptRows.Rows[0].Id}

    $sourceEvidence=Invoke-JsonApi GET "/api/procurement/purchase-order-receipts/$receiptId/source-evidence" $receiverToken $null
    if(-not $sourceEvidence.waybillReady){
        $evidenceFile=Get-Item (Join-Path $repoRoot 'docs\erp-architecture-diagram.pdf')
        $client=[Net.Http.HttpClient]::new();$client.Timeout=[TimeSpan]::FromSeconds(120)
        $client.DefaultRequestHeaders.Authorization=[Net.Http.Headers.AuthenticationHeaderValue]::new('Bearer',$receiverToken)
        $client.DefaultRequestHeaders.Add('X-Correlation-ID',"INV-FU-004-E2E018-WAYBILL-$([Guid]::NewGuid().ToString('N'))")
        $multipart=[Net.Http.MultipartFormDataContent]::new()
        $stream=[IO.File]::OpenRead($evidenceFile.FullName);$fileContent=[Net.Http.StreamContent]::new($stream)
        $fileContent.Headers.ContentType=[Net.Http.Headers.MediaTypeHeaderValue]::new('application/pdf')
        $multipart.Add($fileContent,'file',$evidenceFile.Name)
        foreach($field in @{
            EvidenceKind='Waybill';ReferenceNumber='WB-E2E018-001'
            DocumentDate=(Get-Date).ToUniversalTime().ToString('o');ClientRequestId=[Guid]::NewGuid().ToString()
        }.GetEnumerator()){$multipart.Add([Net.Http.StringContent]::new([string]$field.Value),$field.Key)}
        try{$response=$client.PostAsync("$ApiBaseUrl/api/procurement/purchase-order-receipts/$receiptId/source-evidence",$multipart).GetAwaiter().GetResult();$responseContent=$response.Content.ReadAsStringAsync().GetAwaiter().GetResult()}
        finally{$multipart.Dispose();$stream.Dispose();$client.Dispose()}
        if([int]$response.StatusCode -notin @(200,201)){throw "Waybill upload returned $([int]$response.StatusCode): $responseContent"}
        $sourceEvidence=Invoke-JsonApi GET "/api/procurement/purchase-order-receipts/$receiptId/source-evidence" $receiverToken $null
    }
    if(-not $sourceEvidence.waybillReady){throw 'The controlled Waybill did not reach clean, current DMS readiness.'}
    $waybill=@($sourceEvidence.evidence|Where-Object{"$($_.evidenceKind)" -in @('Waybill','1') -and $_.isCurrent})|Select-Object -First 1
    $uploadRow=Invoke-DbTable $connection 'SELECT FileUploadRecordId FROM CentralDocumentVersions WHERE TenantId=@tenantId AND Id=@versionId AND IsDeleted=0;' @{tenantId=$tenantId;versionId=[Guid]$waybill.centralDocumentVersionId}
    if($uploadRow.Rows.Count -ne 1){throw 'The current Waybill central-document version has no controlled upload lineage.'}
    $fileUploadRecordId=[Guid]$uploadRow.Rows[0].FileUploadRecordId

    $overview=Invoke-JsonApi GET "/api/PurchaseOrderReceipts/$receiptId/inspection-control" $inspectorToken $null
    $inspection=$overview.current
    $inspection=Invoke-JsonApi PUT "/api/PurchaseOrderReceipts/$receiptId/inspection-control" $inspectorToken @{
        comment='All delivered air filters passed independent quantity and quality inspection.';idempotencyKey="e2e018-inspection-save-$($receiptId.ToString('N'))";rowVersion=$inspection.rowVersion
        lines=@($inspection.lines|ForEach-Object{@{purchaseOrderReceiptItemId=$_.purchaseOrderReceiptItemId;acceptedQuantity=$_.receivedQuantity;rejectedQuantity=0;rejectionReason=$null;inspectionNotes='Quantity, specification, and condition accepted.';quarantineLocationId=$null}})
    }
    $evidence=@($overview.evidenceRequirementKeys|ForEach-Object{@{actionKey='SubmitReceiptInspection';requirementKey=$_;referenceKind='CentralDocumentUpload';workflowEvidenceDocumentId=$null;fileUploadRecordId=$fileUploadRecordId;evidenceReference="WB-E2E018-001 / $_"}})
    $inspection=Invoke-JsonApi POST "/api/PurchaseOrderReceipts/inspection-control/$($inspection.id)/submit" $inspectorToken @{comment='Submit the fully accepted receipt for independent approval.';rowVersion=$inspection.rowVersion;evidence=$evidence}
    for($attempt=0;$attempt -lt 4 -and "$($inspection.status)" -in @('PendingApproval','1');$attempt++){
        $inspection=Invoke-JsonApi POST "/api/PurchaseOrderReceipts/inspection-control/$($inspection.id)/decision" $approverToken @{approved=$true;comment='Independent approval of accepted stock and Finance posting.';rowVersion=$inspection.rowVersion}
        if("$($inspection.status)" -in @('PendingApproval','1')){Start-Sleep -Milliseconds 250}
    }
    if("$($inspection.status)" -notin @('Closed','8')){throw "Inspection did not reach Closed; current status is $($inspection.status)."}

    # Complete the configured DEC-013 receipt-document chain with two distinct
    # actors per document, then reconcile the issued PDFs back to this exact
    # receipt/inspection/DMS lineage. Business-facing labels are resolved by
    # the receipt-document owner to the governed technical roles.
    $receiptDocumentOverview=Invoke-JsonApi GET "/api/ProcurementReceiptDocuments/receipt/$receiptId" $adminToken $null
    foreach($document in @($receiptDocumentOverview.documents|Sort-Object documentKind)){
        $current=$document
        foreach($requiredRole in @($current.requiredSignatures)){
            if(@($current.signatures|Where-Object{$_.requiredRole -eq $requiredRole}).Count -gt 0){continue}
            $documentRow=Invoke-DbTable $connection 'SELECT RowVersion FROM ProcurementReceiptDocuments WHERE TenantId=@tenantId AND Id=@documentId AND IsDeleted=0;' @{tenantId=$tenantId;documentId=[Guid]$current.id}
            if($documentRow.Rows.Count -ne 1){throw "Receipt document $($current.id) is unavailable in the acceptance tenant."}
            $databaseRowVersion=[Convert]::ToBase64String([byte[]]$documentRow.Rows[0].RowVersion)
            if($databaseRowVersion -ne [string]$current.rowVersion){
                throw "Receipt document $($current.id) API row version is stale before the $requiredRole signature request."
            }
            $normalizedRole=($requiredRole -replace '[^A-Za-z0-9]','').ToUpperInvariant()
            $signerToken=if($normalizedRole -eq 'STORES'){$inspectorToken}else{$adminToken}
            $current=Invoke-JsonApi POST "/api/ProcurementReceiptDocuments/$($current.id)/sign" $signerToken @{
                requiredRole=$requiredRole;comment="E2E-018 independent $requiredRole receipt attestation.";rowVersion=$current.rowVersion
            }
        }
        if("$($current.status)" -notin @('Issued','2')){
            $current=Invoke-JsonApi POST "/api/ProcurementReceiptDocuments/$($current.id)/issue" $approverToken @{
                comment='Issue the governed receipt document after inspection, evidence and signatory validation.';rowVersion=$current.rowVersion
            }
        }
    }
    $receiptDocumentOverview=Invoke-JsonApi POST "/api/ProcurementReceiptDocuments/receipt/$receiptId/reconcile" $adminToken $null
    if(@($receiptDocumentOverview.documents|Where-Object{"$($_.status)" -notin @('Issued','2') -or "$($_.reconciliationStatus)" -notin @('Reconciled','1')}).Count -gt 0){
        throw 'The configured GRN/MRN register did not reach issued and reconciled state.'
    }

    # Exercise the existing Inventory landed-cost owner; do not calculate or
    # post landed cost directly from Procurement acceptance code.
    $plan=Invoke-JsonApi GET "/api/procurement/purchase-orders/$PurchaseOrderId/landed-cost-plan" $adminToken $null @(200,204)
    if(-not $plan){
        $plan=Invoke-JsonApi PUT "/api/procurement/purchase-orders/$PurchaseOrderId/landed-cost-plan" $adminToken @{
            currency='GHS';notes='E2E-018 governed freight allocation.'
            items=@(@{costType=1;description='Inbound freight';amount=25;currency='GHS';exchangeRate=1;allocationMethod='ByValue';supplierId=$supplierId;referenceNumber='FRT-E2E018-001';notes='Representative accepted-receipt landed cost.'})
        }
    }
    $landedCosts=@(Invoke-JsonApi GET "/api/inventory/landed-costs/by-grn/$receiptId" $adminToken $null)
    $landedCost=$landedCosts|Where-Object{"$($_.status)" -ne 'Cancelled'}|Select-Object -First 1
    if(-not $landedCost){$landedCost=Invoke-JsonApi POST "/api/inventory/landed-costs/initialize-from-po/$receiptId" $adminToken $null}
    $landedCost=Invoke-JsonApi GET "/api/inventory/landed-costs/$($landedCost.id)" $adminToken $null
    if("$($landedCost.status)" -eq 'Draft'){
        $null=Invoke-JsonApi POST "/api/inventory/landed-costs/$($landedCost.id)/allocate" $adminToken $null
        $landedCost=Invoke-JsonApi GET "/api/inventory/landed-costs/$($landedCost.id)" $adminToken $null
    }
    if("$($landedCost.status)" -eq 'Allocated'){
        $null=Invoke-JsonApi POST "/api/inventory/landed-costs/$($landedCost.id)/approve" $approverToken $null
        $landedCost=Invoke-JsonApi GET "/api/inventory/landed-costs/$($landedCost.id)" $adminToken $null
    }
    if("$($landedCost.status)" -eq 'Approved'){
        $null=Invoke-JsonApi POST "/api/inventory/landed-costs/$($landedCost.id)/post-to-inventory" $approverToken $null
        $landedCost=Invoke-JsonApi GET "/api/inventory/landed-costs/$($landedCost.id)" $adminToken $null
    }
    if("$($landedCost.status)" -ne 'Posted'){throw "Landed cost did not reach Posted; current status is $($landedCost.status)."}

    # Hand the authoritative accepted-Goods source to the existing Finance/AP
    # invoice owner and execute its mandatory three-way match.
    $supplyOptions=Invoke-JsonApi GET "/api/ap/invoices/accepted-supply-options?purchaseOrderId=$PurchaseOrderId" $adminToken $null
    # Goods accepted supply is deliberately a PO-level aggregate so partial
    # receipts remain subject to AP's cumulative-quantity match. The option's
    # source is therefore the PO, while its signed snapshot retains every
    # independently resolved receipt-inspection ID.
    $acceptedSupply=@($supplyOptions.options|Where-Object{"$($_.kind)" -in @('GoodsReceiptInspection','1') -and [Guid]$_.sourceId -eq $PurchaseOrderId})|Select-Object -First 1
    if(-not $acceptedSupply){throw "The approved receipt-inspection aggregate is unavailable to Finance/AP as accepted Goods supply: $($supplyOptions.blockedReasons -join '; ')."}
    $poLineRow=Invoke-DbTable $connection 'SELECT TOP(1) Id,ItemDescription,OrderedQuantity,UnitPrice,UnitOfMeasure FROM PurchaseOrderItems WHERE TenantId=@tenantId AND PurchaseOrderId=@purchaseOrderId AND IsDeleted=0 ORDER BY Id;' @{tenantId=$tenantId;purchaseOrderId=$PurchaseOrderId}
    if($poLineRow.Rows.Count -ne 1){throw 'The E2E-018 purchase-order line is unavailable for invoice matching.'}
    $supplierRow=Invoke-DbTable $connection 'SELECT TOP(1) s.Id FROM Suppliers s JOIN BusinessPartners bp ON bp.TenantId=s.TenantId AND (bp.PartnerCode=s.SupplierCode OR bp.PartnerName=s.Name) WHERE s.TenantId=@tenantId AND bp.Id=@businessPartnerId AND s.IsDeleted=0;' @{tenantId=$tenantId;businessPartnerId=$supplierId}
    if($supplierRow.Rows.Count -ne 1){throw 'The Finance supplier projection for the approved business partner is unavailable.'}
    $supplierProjectionId=[Guid]$supplierRow.Rows[0].Id
    $supplierInvoiceNumber="E2E018-$($po.orderNumber)"
    $invoiceRow=Invoke-DbTable $connection 'SELECT TOP(1) Id FROM VendorInvoice WHERE TenantId=@tenantId AND PurchaseOrderId=@purchaseOrderId AND SupplierInvoiceNumber=@supplierInvoiceNumber AND IsDeleted=0 ORDER BY CreatedAt DESC;' @{tenantId=$tenantId;purchaseOrderId=$PurchaseOrderId;supplierInvoiceNumber=$supplierInvoiceNumber}
    if($invoiceRow.Rows.Count -eq 1){$invoice=Invoke-JsonApi GET "/api/ap/invoices/$([Guid]$invoiceRow.Rows[0].Id)" $adminToken $null}
    else{
        $invoice=Invoke-JsonApi POST '/api/ap/invoices' $adminToken @{
            supplierInvoiceNumber=$supplierInvoiceNumber;supplierId=$supplierProjectionId;purchaseOrderId=$PurchaseOrderId
            invoiceDate=(Get-Date).ToUniversalTime().Date;receivedDate=(Get-Date).ToUniversalTime();dueDate=(Get-Date).ToUniversalTime().Date.AddDays(30)
            currencyCode='GHS';exchangeRate=1;paymentTermsDays=30;matchingType=2
            notes='Representative E2E-018 PO/receipt/invoice match.';reference="E2E018-$receiptId";isOpeningBalance=$false
            lineItems=@(@{lineItemType='Inventory';purchaseOrderItemId=[Guid]$poLineRow.Rows[0].Id;description=[string]$poLineRow.Rows[0].ItemDescription;quantity=[decimal]$poLineRow.Rows[0].OrderedQuantity;unitPrice=[decimal]$poLineRow.Rows[0].UnitPrice;discountPercentage=0;unit=[string]$poLineRow.Rows[0].UnitOfMeasure})
        } @(201)
    }
    $invoiceMatch=Invoke-JsonApi POST "/api/ap/invoices/$($invoice.id)/match/three-way" $adminToken $null
    if(-not $invoiceMatch.isMatched -or -not $invoiceMatch.approvalReady){throw "Finance/AP three-way match did not pass: $($invoiceMatch.message)"}

    $proof=Invoke-DbTable $connection @'
SELECT r.ReceiptNumber,r.Status ReceiptStatus,c.Status InspectionStatus,c.StockPostedQuantity,c.ApEligibleQuantity,
       (SELECT COUNT(*) FROM InventoryMovements m WHERE m.TenantId=r.TenantId AND m.ReferenceId=r.Id AND m.IsPosted=1 AND m.IsDeleted=0) InventoryMovementCount,
       pe.Id FinancePostingEventId,pe.PostingStatus,pe.TotalDebitAmount,pe.TotalCreditAmount,je.Id JournalEntryId,je.JournalEntryNumber,je.IsBalanced,je.BalanceDifference,
       (SELECT COUNT(*) FROM ProcurementControlEvents ce WHERE ce.TenantId=r.TenantId AND ce.SourceId IN (r.Id,c.Id) AND ce.IsDeleted=0) AuditEventCount
FROM PurchaseOrderReceipts r
JOIN ProcurementReceiptInspectionCases c ON c.PurchaseOrderReceiptId=r.Id AND c.IsDeleted=0
LEFT JOIN FinancePostingEvents pe ON pe.TenantId=r.TenantId AND pe.SourceDocumentType='ProcurementPurchaseOrderReceipt' AND pe.SourceDocumentId=r.Id AND pe.PostingAction='PostAcceptedInventoryReceipt' AND pe.IsDeleted=0
LEFT JOIN JournalEntries je ON je.Id=pe.JournalEntryId AND je.TenantId=r.TenantId AND je.IsDeleted=0
WHERE r.TenantId=@tenantId AND r.Id=@receiptId;
'@ @{tenantId=$tenantId;receiptId=$receiptId}
    if($proof.Rows.Count -ne 1){throw 'Receipt-to-Finance proof row was not found.'}
    $row=$proof.Rows[0]
    if([int]$row.InventoryMovementCount -lt 1 -or [string]$row.PostingStatus -ne 'Posted' -or [decimal]$row.TotalDebitAmount -ne [decimal]$row.TotalCreditAmount -or -not [bool]$row.IsBalanced -or [decimal]$row.BalanceDifference -ne 0){throw 'Receipt stock/Finance reconciliation proof is incomplete or unbalanced.'}
    $landedCostProof=Invoke-DbTable $connection 'SELECT TOP(1) pe.Id FinancePostingEventId,pe.PostingStatus,pe.TotalDebitAmount,pe.TotalCreditAmount,je.Id JournalEntryId,je.IsBalanced,je.BalanceDifference FROM FinancePostingEvents pe JOIN JournalEntries je ON je.Id=pe.JournalEntryId AND je.TenantId=pe.TenantId AND je.IsDeleted=0 WHERE pe.TenantId=@tenantId AND pe.SourceDocumentType=''InventoryLandedCost'' AND pe.SourceDocumentId=@landedCostId AND pe.PostingAction=''PostLandedCost'' AND pe.IsDeleted=0 ORDER BY pe.CreatedAt DESC;' @{tenantId=$tenantId;landedCostId=[Guid]$landedCost.id}
    if($landedCostProof.Rows.Count -ne 1 -or [string]$landedCostProof.Rows[0].PostingStatus -ne 'Posted' -or [decimal]$landedCostProof.Rows[0].TotalDebitAmount -ne [decimal]$landedCostProof.Rows[0].TotalCreditAmount -or -not [bool]$landedCostProof.Rows[0].IsBalanced -or [decimal]$landedCostProof.Rows[0].BalanceDifference -ne 0){throw 'Landed-cost Finance posting proof is incomplete or unbalanced.'}
    [pscustomobject]@{PurchaseOrderId=$PurchaseOrderId;PurchaseOrderNumber=$po.orderNumber;ReceiptId=$receiptId;ReceiptNumber=$row.ReceiptNumber;ReceiptStatus=$row.ReceiptStatus;InspectionCaseId=$inspection.id;InspectionStatus=$row.InspectionStatus;ReceiptDocuments=@($receiptDocumentOverview.documents|ForEach-Object{@{Id=$_.id;Kind=$_.documentKind;Number=$_.documentNumber;Status=$_.status;Reconciliation=$_.reconciliationStatus;CentralDocumentVersionId=$_.centralDocumentVersionId}});StockPostedQuantity=$row.StockPostedQuantity;ApEligibleQuantity=$row.ApEligibleQuantity;InventoryMovementCount=$row.InventoryMovementCount;FinancePostingEventId=$row.FinancePostingEventId;JournalEntryId=$row.JournalEntryId;JournalEntryNumber=$row.JournalEntryNumber;Debit=$row.TotalDebitAmount;Credit=$row.TotalCreditAmount;IsBalanced=$row.IsBalanced;AuditEventCount=$row.AuditEventCount;WaybillDocumentVersionId=$waybill.centralDocumentVersionId;LandedCostId=$landedCost.id;LandedCostStatus=$landedCost.status;LandedCostFinancePostingEventId=$landedCostProof.Rows[0].FinancePostingEventId;LandedCostDebit=$landedCostProof.Rows[0].TotalDebitAmount;LandedCostCredit=$landedCostProof.Rows[0].TotalCreditAmount;VendorInvoiceId=$invoice.id;VendorInvoiceNumber=$invoice.invoiceNumber;InvoiceMatchingStatus=$invoiceMatch.matchingStatus;InvoiceMatched=$invoiceMatch.isMatched;InvoiceApprovalReady=$invoiceMatch.approvalReady;InvoiceMatchingControlEventId=$invoiceMatch.matchingControlEventId}|ConvertTo-Json -Depth 8 -Compress
}
finally {
    foreach($membership in $addedRoleMemberships){try{$null=Invoke-DbNonQuery $connection 'DELETE FROM UserRoles WHERE UserId=@userId AND RoleId=@roleId;' @{userId=$membership.UserId;roleId=$membership.RoleId}}catch{}}
    foreach($actorId in $passwordBackups.Keys){$backup=$passwordBackups[$actorId];try{$null=Invoke-DbNonQuery $connection 'UPDATE Users SET PasswordHash=@hash,AccessFailedCount=@failed,LockoutEnd=@lockout WHERE Id=@id;' @{id=$actorId;hash=$backup.PasswordHash;failed=$backup.AccessFailedCount;lockout=$backup.LockoutEnd}}catch{}}
    if($connection.State -eq [System.Data.ConnectionState]::Open){$connection.Close()};$connection.Dispose()
}
