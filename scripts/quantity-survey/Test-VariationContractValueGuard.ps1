param([Parameter(Mandatory)][string]$ConnectionString)
$ErrorActionPreference='Stop'
$source=Get-Content (Join-Path $PSScriptRoot '../../src/ErpSystem.Data/Migrations/20260920154000_AllowGovernedVariationContractValueUpdates.cs') -Raw
$migration=[regex]::Match($source,'(?s)ReconciliationSql = """\s*(.*?)\s*""";').Groups[1].Value
$db=[System.Data.SqlClient.SqlConnection]::new($ConnectionString)
$tx=$null
function Run([string]$sql) {
    $cmd=$db.CreateCommand()
    try { $cmd.Transaction=$tx; $cmd.CommandText=$sql; $cmd.CommandTimeout=60; $cmd.ExecuteScalar() }
    finally { $cmd.Dispose() }
}
try {
    $db.Open()
    if($db.Database -ne 'RhemaERP'){throw 'This fixture requires the preserved RhemaERP QS UAT source.'}
    $baseline=Get-Content (Join-Path $PSScriptRoot '../../src/ErpSystem.Data/Migrations/ArchivedGovernanceBaselineSql.cs') -Raw
    $guard=[regex]::Match($baseline,'(?s)CREATE OR ALTER TRIGGER \[dbo\]\.\[TR_Contracts_QS0520CommercialTerms\].*?(?=\r?\n\s*""")').Value
    if(-not $guard -or -not $migration){throw 'Guard or migration missing.'}
    $db.ChangeDatabase('tempdb')
    $schema='qs_variation_'+[guid]::NewGuid().ToString('N')
    $contract='9c957633-fc63-44f4-b3ed-5f47fbd21016'
    $variation='ade633b5-281f-4bc0-88b1-28e21b934351'
    $actor='905c62e4-b648-4d32-bf87-eb05453f53f3'
    foreach($case in @('valid','direct','unfinished-workflow','wrong-actor','wrong-amount','wrong-tenant','changed-terms','already-applied')) {
        $tx=$db.BeginTransaction()
        try {
            [void](Run "CREATE SCHEMA [$schema]")
            [void](Run "SELECT * INTO [$schema].Contracts FROM RhemaERP.dbo.Contracts WHERE Id='$contract';
                SELECT * INTO [$schema].ProjectVariationOrders FROM RhemaERP.dbo.ProjectVariationOrders WHERE Id='$variation';
                SELECT w.* INTO [$schema].WorkflowInstances FROM RhemaERP.dbo.WorkflowInstances w
                JOIN [$schema].ProjectVariationOrders v ON v.WorkflowInstanceId=w.Id;
                UPDATE [$schema].WorkflowInstances SET Status=2;
                UPDATE [$schema].Contracts SET ContractValue=10000;
                UPDATE [$schema].ProjectVariationOrders SET Status='PendingApproval',DownstreamApplicationStatus='NotApplied',
                    OriginalContractSumSnapshot=10000,EstimatedAmount=1000;")
            $fixtureGuard=$guard.Replace('[dbo].[TR_Contracts_QS0520CommercialTerms]',"[$schema].[TR_Contracts_QS0520CommercialTerms]").Replace('[dbo].[Contracts]',"[$schema].[Contracts]")
            $fixtureGuard=$fixtureGuard.Replace('dbo.','RhemaERP.dbo.')
            $fixtureGuard=$fixtureGuard.Replace('RhemaERP.dbo.ProjectVariationOrders',"$schema.ProjectVariationOrders").Replace('RhemaERP.dbo.WorkflowInstances',"$schema.WorkflowInstances")
            $fixtureGuard=$fixtureGuard.Replace('ALTER TRIGGER','CREATE TRIGGER').Replace('CREATE OR CREATE TRIGGER','CREATE TRIGGER')
            [void](Run $fixtureGuard)
            [void](Run $migration.Replace('dbo.',"$schema."))
            [void](Run $migration.Replace('dbo.',"$schema."))
            [void](Run "EXEC sys.sp_set_session_context N'qs_variation_application_id', '$variation';
                EXEC sys.sp_set_session_context N'qs_variation_application_actor', '$actor';
                DECLARE @hash nvarchar(64)=REPLICATE('a',64); EXEC sys.sp_set_session_context N'qs_variation_application_hash', @hash;")
            $amount=11000; $extra=''
            switch($case) {
                'direct' { [void](Run "EXEC sys.sp_set_session_context N'qs_variation_application_id', NULL") }
                'unfinished-workflow' { [void](Run "UPDATE [$schema].WorkflowInstances SET Status=1") }
                'wrong-actor' { [void](Run "EXEC sys.sp_set_session_context N'qs_variation_application_actor', '00000000-0000-0000-0000-000000000002'") }
                'wrong-amount' { $amount=12000 }
                'wrong-tenant' { [void](Run "UPDATE [$schema].ProjectVariationOrders SET TenantId=NEWID()") }
                'changed-terms' { $extra=",PaymentTerms='Unauthorised terms'" }
                'already-applied' { [void](Run "UPDATE [$schema].ProjectVariationOrders SET DownstreamApplicationStatus='AppliedPendingBoqApproval',ApplicationHash=REPLICATE('b',64),AppliedById='$actor',ApprovedAmount=1000") }
            }
            $denied=$false
            try { [void](Run "UPDATE [$schema].Contracts SET ContractValue=$amount,LastModifiedById='$actor' $extra WHERE Id='$contract'") }
            catch [System.Data.SqlClient.SqlException] { if($_.Exception.Number -notin @(51941,51944)){throw}; $denied=$true }
            if(($case -eq 'valid') -eq $denied){throw "Unexpected guard outcome: $case"}
            Write-Output "PASS: $case"
        }
        finally {
            try {$tx.Rollback()} catch [System.InvalidOperationException] {}
            $tx.Dispose(); $tx=$null
        }
    }
}
finally {$db.Dispose()}
