[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'

# Load function declarations only: this test must never execute a remote action,
# touch a service, provision a database, or read installed runtime credentials.
$sourcePath = Join-Path $PSScriptRoot 'FreshDatabaseCutover.ps1'
$tokens = $null; $parseErrors = $null
$ast = [Management.Automation.Language.Parser]::ParseFile($sourcePath, [ref]$tokens, [ref]$parseErrors)
if ($parseErrors.Count) { throw 'Fresh cutover library must parse.' }
foreach ($node in $ast.EndBlock.Statements) {
    if ($node -is [Management.Automation.Language.FunctionDefinitionAst]) {
        Invoke-Expression $node.Extent.Text
    }
}

$testRoot = Join-Path ([IO.Path]::GetTempPath()) ('rhema-fresh-cutover-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $testRoot | Out-Null
function Assert-True { param([bool]$Condition, [string]$Message) if (-not $Condition) { throw $Message } }
function Add-TestEvent { param([string]$Event) [void]$script:Events.Add($Event) }
function Write-TestMarker { param([string]$Path,[string]$Value)
    New-Item -ItemType Directory -Path (Split-Path -Parent $Path) -Force | Out-Null
    [IO.File]::WriteAllText($Path,$Value)
}
function Get-TestMarker { param([string]$Path) [IO.File]::ReadAllText($Path) }
function Get-ApiServiceEnvironment {
    if(-not $script:UsesNssmApiConfiguration) {
        $xml=[xml](Get-Content -Raw -LiteralPath $script:ApiServiceXml)
        $script:TestEnvironment=@{}
        foreach($node in $xml.service.env) { $script:TestEnvironment[[string]$node.name]=[string]$node.value }
    }
    return $script:TestEnvironment
}
function Get-DatabaseConnectionString { return (Get-ApiServiceEnvironment)['ConnectionStrings__DefaultConnection'] }
function Get-NssmEnvironmentSnapshot {
    $copy=@{}; foreach($key in $script:TestEnvironment.Keys) { $copy[$key]=$script:TestEnvironment[$key] }
    return [pscustomobject]@{ Values=$copy }
}
function Restore-NssmEnvironmentSnapshot { param($Snapshot)
    Add-TestEvent 'restore-config'
    $script:TestEnvironment=@{}
    foreach($property in $Snapshot.Values.PSObject.Properties) { $script:TestEnvironment[$property.Name]=[string]$property.Value }
    if ($Snapshot.Values -is [System.Collections.IDictionary]) {
        foreach($key in $Snapshot.Values.Keys) { $script:TestEnvironment[$key]=[string]$Snapshot.Values[$key] }
    }
}
function Set-ApiServiceEnvironmentValues { param([hashtable]$Values)
    Add-TestEvent 'set-config'
    foreach($key in $Values.Keys) { $script:TestEnvironment[$key]=[string]$Values[$key] }
    if(-not $script:UsesNssmApiConfiguration) {
        $xml=[xml](Get-Content -Raw -LiteralPath $script:ApiServiceXml)
        foreach($key in $Values.Keys) {
            $node=@($xml.service.env | Where-Object {$_.name -eq $key})[0]
            $node.SetAttribute('value',[string]$Values[$key])
        }
        $xml.Save($script:ApiServiceXml)
    }
}
function Set-TestServerConfiguration { Add-TestEvent 'set-test-config' }
function Stop-ManagedService { param([string]$Name) Add-TestEvent ('stop:'+ $Name); $script:Services[$Name]='Stopped' }
function Stop-Service { param([string]$Name,[switch]$Force,[string]$ErrorAction)
    Stop-ManagedService $Name
}
function Start-Service { param([string]$Name,[string]$ErrorAction)
    Add-TestEvent ('start:'+ $Name)
    if($script:Failure -eq 'frontend-start' -and $Name -eq 'RhemaERPFrontend') {
        $script:Failure=''; throw 'Injected frontend start failure'
    }
    $script:Services[$Name]='Running'
}
function Wait-FrontendReady { Add-TestEvent 'frontend-ready' }
function Wait-ApiReady { param([DateTime]$StartedAt) Add-TestEvent 'api-ready' }
function Start-ApiWithControlledMigrations { param([DateTime]$StartedAt)
    Add-TestEvent 'start-api-controlled'
    if($script:Failure -eq 'api-start') { $script:Failure=''; throw 'Injected API start failure' }
    $script:Services['RhemaERPAPI']='Running'
}
function Invoke-RobocopyChecked { param([string[]]$Arguments)
    $source=$Arguments[0]; $destination=$Arguments[1]
    Add-TestEvent ('copy:'+ (Split-Path -Leaf $source))
    New-Item -ItemType Directory -Path $destination -Force | Out-Null
    foreach($item in Get-ChildItem -LiteralPath $source -Force) {
        Copy-Item -LiteralPath $item.FullName -Destination $destination -Recurse -Force
    }
}

function Assert-DeploymentId {
    Assert-True ($DeploymentId -match '^[a-zA-Z0-9-]+$') 'Invalid test deployment ID.'
}
function Assert-FreshDatabaseServiceIdentity { Add-TestEvent 'check-identity' }
function Get-FreshExpectedMigrationIds { return @('migration-one','migration-two') }
function Get-RhemaOperationalPassword { return 'fixture-only-bootstrap' }
function Invoke-RhemaFreshDatabaseProvisioning {
    param([string]$SourceConnectionString,[string]$FreshDatabaseName,[string]$StagedApiDirectory,
        [string[]]$ExpectedMigrationIds,[string]$WorkDirectory,[string]$OperationalUatPassword)
    Add-TestEvent 'provision'
    Assert-True ($script:Services.RhemaERPAPI -eq 'Running' -and $script:Services.RhemaERPFrontend -eq 'Running') 'Provisioning must precede service interruption.'
    Assert-True ($SourceConnectionString -like '*Initial Catalog=OriginalDb*') 'Provisioning lost original database identity.'
    Assert-True ($ExpectedMigrationIds.Count -eq 2) 'Provisioning lost expected migration IDs.'
    Assert-True ($OperationalUatPassword -eq 'fixture-only-bootstrap') 'Fresh cutover did not request operational account seeding.'
    if($script:Failure -eq 'provision') { throw 'Injected provisioning failure' }
    $builder=New-Object System.Data.SqlClient.SqlConnectionStringBuilder $SourceConnectionString
    $builder['Initial Catalog']=$FreshDatabaseName
    return [pscustomobject]@{ ConnectionString=$builder.ConnectionString }
}
function Move-Item {
    param([string]$LiteralPath,[string]$Path,[string]$Destination,[switch]$Force)
    $source=if($LiteralPath){$LiteralPath}else{$Path}
    if($script:Failure -eq 'frontend-swap' -and $source -eq (Join-Path $script:StageFrontend 'public')) {
        $script:Failure=''; throw 'Injected frontend swap failure'
    }
    Microsoft.PowerShell.Management\Move-Item -LiteralPath $source -Destination $Destination -Force:$Force
}
function Initialize-TestScenario {
    param([string]$Name,[string]$Failure='')
    $script:ScenarioRoot=Join-Path $testRoot $Name
    $script:ApiRoot=Join-Path $script:ScenarioRoot 'api'
    $script:FrontendRoot=Join-Path $script:ScenarioRoot 'frontend'
    $script:PackagesRoot=Join-Path $script:ScenarioRoot 'packages'
    $script:BackupsRoot=Join-Path $script:ScenarioRoot 'backups'
    $script:LogsRoot=Join-Path $script:ScenarioRoot 'logs'
    $script:DeploymentId='fixture-'+$Name
    $script:ExpectedCommit='1111111111111111111111111111111111111111'
    $script:FreshDatabaseName='FreshVerificationDb'
    $script:UsesNssmApiConfiguration=$true
    $script:ApiServiceXml=Join-Path $script:ScenarioRoot 'services\api\RhemaERPAPI.xml'
    $script:Backup=Join-Path $script:BackupsRoot ('deploy-'+$script:DeploymentId)
    $script:Retired=Join-Path $script:PackagesRoot ('retired-'+$script:DeploymentId)
    $script:StageApi=Join-Path $script:PackagesRoot 'stage\api'
    $script:StageFrontend=Join-Path $script:PackagesRoot 'stage\frontend'
    $script:Events=New-Object 'System.Collections.Generic.List[string]'
    $script:Services=@{RhemaERPAPI='Running';RhemaERPFrontend='Running'}
    $script:Failure=$Failure
    $script:TestEnvironment=@{
        'ConnectionStrings__DefaultConnection'='Data Source=FixtureSql;Initial Catalog=OriginalDb;User ID=fixture;Password=not-a-real-secret'
        'Security__EncryptionKey'='fixture-encryption-preserve'
        'Jwt__Key'='fixture-jwt-preserve'
        'UnrelatedSetting'='preserve-me'
    }
    $script:OriginalConnection=$script:TestEnvironment['ConnectionStrings__DefaultConnection']
    foreach($directory in @($ApiRoot,$FrontendRoot,$PackagesRoot,$BackupsRoot,$LogsRoot,$Backup,$Retired,$StageApi,$StageFrontend)) {
        New-Item -ItemType Directory -Path $directory -Force | Out-Null
    }
    Write-TestMarker (Join-Path $ApiRoot 'ErpSystem.Api.exe') 'old-api'
    Write-TestMarker (Join-Path $ApiRoot 'appsettings.Production.json') 'runtime-config-preserved'
    Write-TestMarker (Join-Path $ApiRoot 'secure-file-storage\retained.txt') 'retained-evidence'
    Write-TestMarker (Join-Path $StageApi 'ErpSystem.Api.exe') 'new-api'
    Write-TestMarker (Join-Path $StageApi 'new-library.dll') 'new-library'
    Write-TestMarker (Join-Path $Backup 'api\ErpSystem.Api.exe') 'old-api'
    foreach($directory in @('.next','public','node_modules')) {
        Write-TestMarker (Join-Path $FrontendRoot "$directory\marker.txt") ('old-'+$directory)
        Write-TestMarker (Join-Path $StageFrontend "$directory\marker.txt") ('new-'+$directory)
    }
    foreach($file in @('package.json','package-lock.json','next.config.js')) {
        Write-TestMarker (Join-Path $FrontendRoot $file) ('old-'+$file)
        Write-TestMarker (Join-Path $StageFrontend $file) ('new-'+$file)
    }
    Write-TestMarker (Join-Path $LogsRoot 'current-release.json') '{"commit":"old-release"}'
    Write-TestMarker (Join-Path $Backup 'backup-manifest.json') (@{
        deploymentId=$DeploymentId;commit=$ExpectedCommit;databaseBackupVerified=$true
    } | ConvertTo-Json)
    Write-TestMarker (Join-Path $Backup 'services\api\RhemaERPAPI.nssm-environment.json') ((Get-NssmEnvironmentSnapshot) | ConvertTo-Json -Depth 6)
}
function Assert-TestSecrets {
    Get-ApiServiceEnvironment | Out-Null
    Assert-True ($script:TestEnvironment.Security__EncryptionKey -eq 'fixture-encryption-preserve') 'Encryption key changed.'
    Assert-True ($script:TestEnvironment.Jwt__Key -eq 'fixture-jwt-preserve') 'JWT key changed.'
    Assert-True ($script:TestEnvironment.UnrelatedSetting -eq 'preserve-me') 'Unrelated service setting changed.'
    Assert-True ((Get-TestMarker (Join-Path $ApiRoot 'appsettings.Production.json')) -eq 'runtime-config-preserved') 'Runtime configuration was overwritten.'
    Assert-True ((Get-TestMarker (Join-Path $ApiRoot 'secure-file-storage\retained.txt')) -eq 'retained-evidence') 'DMS evidence changed.'
}
function Assert-TestRestored {
    Assert-True ((Get-DatabaseConnectionString) -eq $script:OriginalConnection) 'Original connection was not restored exactly.'
    Assert-True ((Get-TestMarker (Join-Path $ApiRoot 'ErpSystem.Api.exe')) -eq 'old-api') 'Old API was not restored.'
    Assert-True (-not (Test-Path (Join-Path $ApiRoot 'new-library.dll'))) 'New-only API library survived rollback.'
    foreach($directory in @('.next','public','node_modules')) {
        Assert-True ((Get-TestMarker (Join-Path $FrontendRoot "$directory\marker.txt")) -eq ('old-'+$directory)) "Frontend $directory was not restored."
    }
    foreach($file in @('package.json','package-lock.json','next.config.js')) {
        Assert-True ((Get-TestMarker (Join-Path $FrontendRoot $file)) -eq ('old-'+$file)) "Frontend $file was not restored."
    }
    Assert-True ((Get-TestMarker (Join-Path $LogsRoot 'current-release.json')) -eq '{"commit":"old-release"}') 'Old release marker was not restored.'
    Assert-True ($script:Services.RhemaERPAPI -eq 'Running' -and $script:Services.RhemaERPFrontend -eq 'Running') 'Both restored services must be running.'
    Assert-TestSecrets
}
function Invoke-TestCutover {
    Invoke-FreshDatabaseCutover -StageApi $StageApi -StageFrontend $StageFrontend -Backup $Backup -Retired $Retired | Out-Null
}
function Assert-TestThrows {
    param([scriptblock]$Operation,[string]$Pattern)
    $failed=$false
    try { & $Operation | Out-Null } catch {
        if($_.Exception.Message -notlike $Pattern) { throw }
        $failed=$true
    }
    Assert-True $failed "Expected failure matching $Pattern."
}
try {
    Initialize-TestScenario 'provision-failure' 'provision'
    Assert-TestThrows { Invoke-TestCutover } '*Injected provisioning failure*'
    Assert-True (@($script:Events | Where-Object {$_ -match '^(stop:|start:|set-config|set-test-config)'}).Count -eq 0) 'Provisioning failure interrupted services or changed config.'
    Assert-True (-not (Test-Path (Get-FreshCutoverStatePath))) 'Provisioning failure falsely started cutover.'
    Assert-TestRestored

    foreach($failure in @('api-start','frontend-swap','frontend-start')) {
        Initialize-TestScenario $failure $failure
        Assert-TestThrows { Invoke-TestCutover } '*Injected*failure*'
        Assert-TestRestored
        $state=Get-Content -Raw (Get-FreshCutoverStatePath) | ConvertFrom-Json
        Assert-True ($state.status -eq 'RolledBack') 'Failure did not record completed rollback.'
    }

    Initialize-TestScenario 'winsw-api-start' 'api-start'
    $xml=[xml]'<service><id>RhemaERPAPI</id></service>'
    foreach($key in $script:TestEnvironment.Keys) {
        $node=$xml.CreateElement('env'); $node.SetAttribute('name',$key)
        $node.SetAttribute('value',$script:TestEnvironment[$key]); [void]$xml.service.AppendChild($node)
    }
    Write-TestMarker $ApiServiceXml $xml.OuterXml
    Write-TestMarker (Join-Path $Backup 'services\api\RhemaERPAPI.xml') $xml.OuterXml
    $originalXml=Get-TestMarker $ApiServiceXml
    $script:UsesNssmApiConfiguration=$false
    Assert-TestThrows { Invoke-TestCutover } '*Injected API start failure*'
    Assert-TestRestored
    Assert-True ((Get-TestMarker $ApiServiceXml) -eq $originalXml) 'WinSW configuration was not restored byte-for-byte.'

    Initialize-TestScenario 'late-verification'
    Invoke-TestCutover
    Assert-TestSecrets
    $firstConfig=$script:Events.IndexOf('set-config')
    Assert-True ($script:Events.IndexOf('stop:RhemaERPAPI') -lt $firstConfig -and $script:Events.IndexOf('stop:RhemaERPFrontend') -lt $firstConfig) 'Both services must stop before database connection changes.'
    Assert-True ((Get-DatabaseConnectionString) -like '*Initial Catalog=FreshVerificationDb*') 'Successful cutover did not select fresh database.'
    Assert-True ((Get-TestMarker (Join-Path $ApiRoot 'ErpSystem.Api.exe')) -eq 'new-api') 'Successful cutover retained old API.'
    foreach($directory in @('.next','public','node_modules')) {
        Assert-True ((Get-TestMarker (Join-Path $FrontendRoot "$directory\marker.txt")) -eq ('new-'+$directory)) 'Successful cutover did not swap frontend.'
    }
    $stateText=Get-Content -Raw (Get-FreshCutoverStatePath)
    Assert-True ($stateText -notmatch 'fixture-encryption|fixture-jwt|Password|ConnectionString') 'Cutover state leaked runtime secrets.'
    Write-TestMarker (Join-Path $LogsRoot 'current-release.json') (@{ deploymentId=$DeploymentId;commit=$ExpectedCommit } | ConvertTo-Json)
    Restore-FreshDatabaseCutover | Out-Null
    Assert-TestRestored
    Restore-FreshDatabaseCutover | Out-Null
    Assert-TestRestored

    Initialize-TestScenario 'unrelated-deployment'
    Invoke-TestCutover
    $script:ExpectedCommit='2222222222222222222222222222222222222222'
    $eventCount=$script:Events.Count
    Assert-TestThrows { Restore-FreshDatabaseCutover } '*does not match this deployment*'
    Assert-True ($script:Events.Count -eq $eventCount) 'Unrelated deployment rollback mutated services.'

    Initialize-TestScenario 'unrelated-release-same-database'
    Invoke-TestCutover
    Write-TestMarker (Join-Path $LogsRoot 'current-release.json') '{"deploymentId":"another-release","commit":"3333333333333333333333333333333333333333"}'
    $eventCount=$script:Events.Count
    Assert-TestThrows { Restore-FreshDatabaseCutover } '*Another release changed the VPS*'
    Assert-True ($script:Events.Count -eq $eventCount) 'Unrelated release on same database rollback mutated services.'

    Initialize-TestScenario 'unrelated-database'
    Invoke-TestCutover
    $script:TestEnvironment['ConnectionStrings__DefaultConnection']='Data Source=FixtureSql;Initial Catalog=AnotherDeploymentDb;Integrated Security=True'
    $eventCount=$script:Events.Count
    Assert-TestThrows { Restore-FreshDatabaseCutover } '*unrelated database*'
    Assert-True ($script:Events.Count -eq $eventCount) 'Unrelated database rollback mutated services.'

    Initialize-TestScenario 'committed'
    Invoke-TestCutover
    Complete-FreshDatabaseCutover | Out-Null
    $eventCount=$script:Events.Count
    Assert-TestThrows { Restore-FreshDatabaseCutover } '*completed cutover cannot*'
    Assert-True ($script:Events.Count -eq $eventCount) 'Committed deployment rollback mutated services.'

    # Execute the actual root Apply invocation and outer catch, with external helper
    # calls mocked. This catches scope regressions that helper-only tests cannot see.
    $deployTokens=$null; $deployErrors=$null
    $deployAst=[Management.Automation.Language.Parser]::ParseFile((Join-Path (Split-Path -Parent $PSScriptRoot) 'Deploy-RhemaVps.ps1'),[ref]$deployTokens,[ref]$deployErrors)
    Assert-True ($deployErrors.Count -eq 0) 'Deployment orchestrator must parse.'
    $mainTry=@($deployAst.EndBlock.Statements | Where-Object {$_ -is [Management.Automation.Language.TryStatementAst]})[-1]
    $stepFunction=$deployAst.Find({param($node) $node -is [Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -eq 'Invoke-Step'},$true)
    Invoke-Expression $stepFunction.Extent.Text
    $applyCommand=$mainTry.Body.Find({param($node)
        $node -is [Management.Automation.Language.CommandAst] -and $node.GetCommandName() -eq 'Invoke-Step' -and
        $node.CommandElements.Count -gt 1 -and $node.CommandElements[1].Extent.Text -eq "'Apply API, migrations, and frontend'"
    },$true)
    Assert-True ($null -ne $applyCommand) 'Root Apply invocation was not found.'
    function Invoke-RemoteHelper {
        param([string]$RemoteHelperPath,[string]$Action,[hashtable]$Parameters)
        Add-TestEvent ('remote:'+ $Action)
        if($Action -eq 'RollbackFresh') {
            Assert-True ($Parameters.DeploymentId -eq $DeploymentId -and $Parameters.ExpectedCommit -eq $script:Commit -and $Parameters.FreshDatabaseName -eq $FreshDatabaseName) 'Root rollback lost deployment identity.'
            if($script:MockRollbackFailure) { throw 'Injected rollback infrastructure failure' }
        }
    }
    function Write-RunResult {
        param($Status,$Id,$Directory,$Manifest,$Migrations,$ErrorMessage)
        Assert-True ($Status -eq 'Failed' -and $ErrorMessage -like '*Injected public smoke failure*') 'Root failure evidence lost original error.'
        return 'fixture-result.json'
    }
    $script:Commit=$ExpectedCommit
    $releaseDirectory=$ScenarioRoot
    $remoteHelperPath='fixture-helper.ps1'
    $migrationState=@{}
    $releaseManifest=@{ buildId='fixture';cacheVersion='fixture';api=@{file='api.zip';sha256='fixture'};frontend=@{file='frontend.zip';sha256='fixture'} }
    $ApiReadyTimeoutSeconds=60
    $StepResults=New-Object 'System.Collections.Generic.List[object]'
    $script:freshApplyAttempted=$false
    $script:MockRollbackFailure=$false
    Invoke-Expression $applyCommand.Parent.Extent.Text
    Assert-True $script:freshApplyAttempted 'Apply flag did not escape Invoke-Step function scope.'
    $catchProbe=[scriptblock]::Create("try { throw 'Injected public smoke failure' } " + $mainTry.CatchClauses[0].Extent.Text)
    Assert-TestThrows $catchProbe '*Injected public smoke failure*'
    Assert-True ($script:Events.Contains('remote:RollbackFresh')) 'Post-apply public smoke failure did not request coordinated rollback.'
    $script:Events.Clear(); $script:freshApplyAttempted=$false
    Assert-TestThrows $catchProbe '*Injected public smoke failure*'
    Assert-True (-not $script:Events.Contains('remote:RollbackFresh')) 'Failure before fresh Apply attempted rollback.'
    $script:Events.Clear(); $script:freshApplyAttempted=$true; $script:MockRollbackFailure=$true
    Assert-TestThrows $catchProbe '*Injected public smoke failure*'
    Assert-True ($script:Events.Contains('remote:RollbackFresh')) 'Root did not attempt rollback after late failure.'

    # Exercise the real CLI failed-start catch without starting any executable.
    # An inherited PowerShell variable must never become diagnostic evidence.
    $provisionTokens=$null; $provisionErrors=$null
    $provisionAst=[Management.Automation.Language.Parser]::ParseFile((Join-Path $PSScriptRoot 'FreshDatabaseProvisioning.ps1'),[ref]$provisionTokens,[ref]$provisionErrors)
    Assert-True ($provisionErrors.Count -eq 0) 'Provisioning library must parse.'
    foreach($functionName in @('Assert-RhemaFreshDatabaseName','Get-RhemaFreshConnectionBuilder','Invoke-RhemaFreshApiCli','Invoke-RhemaFreshDatabaseProvisioning')) {
        $functionNode=$provisionAst.Find({param($node)
            $node -is [Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -eq $functionName
        },$true)
        Assert-True ($null -ne $functionNode) "Provisioning function is missing: $functionName"
        Invoke-Expression $functionNode.Extent.Text
    }
    $evidence=[pscustomobject]@{ UnexpectedSecret='caller-only-evidence-marker'; ExitCode=99 }
    $startFailure=$false
    try {
        Invoke-RhemaFreshApiCli -ApiExecutable (Join-Path $testRoot 'nonexistent\missing.exe') -ContentRoot $testRoot `
            -ConnectionString 'Server=fixture;Database=RhemaERP_DiagnosticFixture;Integrated Security=True' `
            -Command 'apply-migrations' -TimeoutSeconds 1 | Out-Null
    } catch {
        Assert-True (-not $_.Exception.Data.Contains('SafeCliEvidence')) 'Failed process start inherited caller diagnostic evidence.'
        Assert-True ($_.Exception.Message -like '*exit code unavailable*' -and $_.Exception.Message -notmatch 'caller-only-evidence-marker|99') 'Failed-start message leaked caller evidence.'
        $startFailure=$true
    }
    Assert-True $startFailure 'Nonexistent executable unexpectedly started.'

    # Run the actual provisioning stage catch with SQL/CLI boundaries mocked.
    # Its durable failure artifact must select allowed metadata rather than
    # serializing arbitrary exception.Data properties or source connections.
    $diagnosticStage=Join-Path $testRoot 'diagnostics\api'
    foreach($file in @('ErpSystem.Api.exe','ErpSystem.Api.dll','ErpSystem.Data.dll','ErpSystem.Api.runtimeconfig.json')) {
        Write-TestMarker (Join-Path $diagnosticStage $file) 'non-executable-test-fixture'
    }
    $diagnosticWork=Join-Path $testRoot 'diagnostics\work'
    $script:DiagnosticSqlCalls=0
    function Test-RhemaFreshDatabaseTarget {
        param([string]$SourceConnectionString,[string]$FreshDatabaseName)
        Assert-True ($FreshDatabaseName -eq 'RhemaERP_DiagnosticFixture') 'Diagnostic fixture selected wrong target.'
    }
    function Invoke-RhemaFreshSql {
        param([string]$ConnectionString,[string]$ExpectedDatabase,[string]$Sql)
        $script:DiagnosticSqlCalls++
        Assert-True ($ExpectedDatabase -eq 'master' -and $Sql.Contains('CREATE DATABASE [RhemaERP_DiagnosticFixture]')) 'Unexpected SQL operation during injected CLI failure.'
        return ,(New-Object System.Data.DataTable)
    }
    function Invoke-RhemaFreshApiCli {
        param([string]$ApiExecutable,[string]$ContentRoot,[string]$ConnectionString,[string]$Command)
        $failure=New-Object InvalidOperationException 'Fresh database CLI apply-migrations failed or timed out (exit code 17).'
        $failure.Data['SafeCliEvidence']=[pscustomobject]@{
            Command=$Command; ExitCode=17; Seconds=2.5; OutputSha256=('A'*64)
            ExceptionTypes=@('System.InvalidOperationException'); SqlErrorNumbers=@('51727'); GuardCodes=@('CANONICAL_FIXTURE')
            UnexpectedSecret='unexpected-diagnostic-secret'; SourceConnectionString=$ConnectionString
        }
        throw $failure
    }
    Assert-TestThrows {
        Invoke-RhemaFreshDatabaseProvisioning `
            -SourceConnectionString 'Server=fixture;Database=OriginalDb;User ID=fixture;Password=source-secret-only' `
            -FreshDatabaseName 'RhemaERP_DiagnosticFixture' -StagedApiDirectory $diagnosticStage `
            -ExpectedMigrationIds @('20260916132000_DisposableDevelopmentCurrentModelBaseline') -WorkDirectory $diagnosticWork
    } '*Fresh provisioning failed at ApplyMigrations*CLI exit code: 17*'
    Assert-True ($script:DiagnosticSqlCalls -eq 1) 'Provisioning continued after injected CLI failure.'
    $failureText=Get-Content -Raw -LiteralPath (Join-Path $diagnosticWork 'failure.json')
    $failureRecord=$failureText | ConvertFrom-Json
    Assert-True ($failureRecord.Stage -eq 'ApplyMigrations' -and $failureRecord.Database -eq 'RhemaERP_DiagnosticFixture') 'Durable diagnostics lost stage or target identity.'
    Assert-True ($failureRecord.Cli.ExitCode -eq 17 -and $failureRecord.Cli.OutputSha256 -eq ('A'*64) -and $failureRecord.Cli.Command -eq 'apply-migrations') 'Durable diagnostics lost allowed CLI metadata.'
    Assert-True ($failureText -notmatch 'UnexpectedSecret|unexpected-diagnostic-secret|SourceConnectionString|source-secret-only|Password|caller-only-evidence-marker') 'Durable diagnostics leaked non-allowlisted evidence or credentials.'
    Write-Host 'PASS: provisioning isolation; partial swap/API/frontend failure rollback; late verification rollback; NSSM/WinSW secret/file preservation; unrelated and committed deployment rejection.'
    Write-Host 'PASS: actual deployment Apply flag scope and outer catch rollback dispatch, identity, original-error preservation and pre-Apply isolation.'
    Write-Host 'PASS: failed-start evidence scope isolation and allowlisted durable provisioning failure diagnostics; no real SQL or CLI executed.'
} finally {
    $resolved=[IO.Path]::GetFullPath($testRoot)
    $parent=[IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\')+'\'
    if($resolved.StartsWith($parent,[StringComparison]::OrdinalIgnoreCase) -and (Split-Path $resolved -Leaf) -match '^rhema-fresh-cutover-[a-f0-9]{32}$') {
        Remove-Item -LiteralPath $resolved -Recurse -Force
    } else { throw 'Unsafe test fixture cleanup path.' }
}
