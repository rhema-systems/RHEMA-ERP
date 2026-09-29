[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$SqlServer)
$ErrorActionPreference='Stop'
$database='RhemaERP_QsUatVerify_MedicalProbe_'+[Guid]::NewGuid().ToString('N')
$builder=New-Object System.Data.SqlClient.SqlConnectionStringBuilder
$builder['Data Source']=$SqlServer;$builder['Initial Catalog']='master';$builder['Integrated Security']=$true;$builder['TrustServerCertificate']=$true
$connection=New-Object System.Data.SqlClient.SqlConnection $builder.ConnectionString
$created=$false
function Invoke-ProbeSql([string]$Sql,[switch]$Rows) {
    $command=$connection.CreateCommand();$command.CommandText=$Sql;$command.CommandTimeout=30
    try {
        if($Rows){$table=New-Object Data.DataTable;$reader=$command.ExecuteReader();try{$table.Load($reader)}finally{$reader.Dispose()};return ,$table}
        [void]$command.ExecuteNonQuery()
    }finally{$command.Dispose()}
}
try {
    $connection.Open()
    Invoke-ProbeSql "CREATE DATABASE [$database]"
    $created=$true;$connection.ChangeDatabase($database)
    Invoke-ProbeSql @'
CREATE TABLE dbo.__EFMigrationsHistory(MigrationId nvarchar(150));
CREATE TABLE dbo.Employees(Id int PRIMARY KEY);
CREATE TABLE dbo.MedicalBoards(Id int PRIMARY KEY);
CREATE TABLE dbo.MedicalBoardCases(BoardId int,EmployeeId int,IsDeleted bit);
CREATE TABLE dbo.MedicalBoardSittingAttendances(SittingId int,MemberId int,IsDeleted bit);
INSERT dbo.Employees VALUES(1),(2);
INSERT dbo.MedicalBoards VALUES(1);
INSERT dbo.MedicalBoardCases VALUES(1,1,0),(1,2,0);
'@
    $probe=Get-Content -LiteralPath (Join-Path $PSScriptRoot 'HrMedicalBoardMigrationPreflight.sql') -Raw
    if((Invoke-ProbeSql $probe -Rows).Rows.Count){throw 'Legitimate multi-case boards were blocked by a rollback-only rule.'}
    Invoke-ProbeSql 'INSERT dbo.MedicalBoardCases VALUES(1,1,0); INSERT dbo.MedicalBoardSittingAttendances VALUES(1,1,0),(1,1,0);'
    $issues=Invoke-ProbeSql $probe -Rows
    if($issues.Rows.Count -ne 2 -or @($issues.Rows | Where-Object {$_.AffectedRows -ne 1}).Count){throw 'Duplicate index risks were not detected accurately.'}
    Invoke-ProbeSql 'DELETE dbo.MedicalBoardCases; DELETE dbo.MedicalBoardSittingAttendances; ALTER TABLE dbo.MedicalBoards ADD EmployeeId int NULL;'
    if((Invoke-ProbeSql $probe -Rows).Rows[0].CheckName -ne 'HrMedicalBoards:LegacyEmployeeMissing'){throw 'Missing legacy employee was not detected.'}
    Invoke-ProbeSql 'UPDATE dbo.MedicalBoards SET EmployeeId=1; INSERT dbo.MedicalBoardCases VALUES(1,2,0);'
    if((Invoke-ProbeSql $probe -Rows).Rows[0].CheckName -ne 'HrMedicalBoards:ConflictingLegacyCase'){throw 'Partial conversion conflict was not detected.'}
    Invoke-ProbeSql 'UPDATE dbo.MedicalBoardCases SET EmployeeId=1;'
    if((Invoke-ProbeSql $probe -Rows).Rows.Count){throw 'Compatible legacy conversion was rejected.'}
    Invoke-ProbeSql "INSERT dbo.__EFMigrationsHistory VALUES(N'20260926143546_AddMedicalBoardCases'); INSERT dbo.MedicalBoardCases VALUES(1,1,0);"
    if((Invoke-ProbeSql $probe -Rows).Rows.Count){throw 'Already applied migration was unnecessarily gated.'}
    Write-Output 'PASS|SQL Server medical-board preflight: forward/rollback distinction, duplicate indexes, legacy references, partial conversion and already-applied handling.'
}finally{
    if($created){
        if($database -cnotmatch '^RhemaERP_QsUatVerify_MedicalProbe_[a-f0-9]{32}$'){throw 'Unsafe test cleanup target.'}
        $connection.ChangeDatabase('master')
        Invoke-ProbeSql "ALTER DATABASE [$database] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [$database];"
    }
    $connection.Dispose()
}
