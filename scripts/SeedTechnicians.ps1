# PowerShell script to seed technician IDs into task templates
Write-Host "Seeding Technician IDs into Task Templates..." -ForegroundColor Cyan

$scriptContent = @"
DECLARE @TechnicianId UNIQUEIDENTIFIER;
DECLARE @TechnicianId2 UNIQUEIDENTIFIER;
DECLARE @RowCount INT;

-- Try to get technicians from Technicians table first
SELECT TOP 1 @TechnicianId = EmployeeId FROM Technicians WHERE IsActive = 1 ORDER BY CreatedAt;
SELECT @TechnicianId2 = EmployeeId FROM Technicians WHERE IsActive = 1 AND EmployeeId != @TechnicianId ORDER BY CreatedAt OFFSET 0 ROWS FETCH NEXT 1 ROWS ONLY;

IF @TechnicianId IS NULL
    SELECT TOP 1 @TechnicianId = Id FROM Employees ORDER BY CreatedAt;

IF @TechnicianId2 IS NULL
    SET @TechnicianId2 = @TechnicianId;

UPDATE MaintenanceTaskTemplates 
SET AssignedTechnicianId = CASE WHEN Sequence % 2 = 1 THEN @TechnicianId ELSE @TechnicianId2 END, UpdatedAt = GETUTCDATE()
WHERE AssignedTechnicianId IS NULL AND @TechnicianId IS NOT NULL;

UPDATE AssetTypeTaskTemplates 
SET AssignedTechnicianId = @TechnicianId, UpdatedAt = GETUTCDATE()
WHERE AssignedTechnicianId IS NULL AND @TechnicianId IS NOT NULL;

UPDATE AssetTaskTemplates 
SET AssignedTechnicianId = @TechnicianId, UpdatedAt = GETUTCDATE()
WHERE AssignedTechnicianId IS NULL AND @TechnicianId IS NOT NULL;

SELECT 'MaintenanceTaskTemplates' AS TableName, COUNT(*) AS Total, SUM(CASE WHEN AssignedTechnicianId IS NOT NULL THEN 1 ELSE 0 END) AS WithTechnician FROM MaintenanceTaskTemplates
UNION ALL
SELECT 'AssetTypeTaskTemplates', COUNT(*), SUM(CASE WHEN AssignedTechnicianId IS NOT NULL THEN 1 ELSE 0 END) FROM AssetTypeTaskTemplates
UNION ALL
SELECT 'AssetTaskTemplates', COUNT(*), SUM(CASE WHEN AssignedTechnicianId IS NOT NULL THEN 1 ELSE 0 END) FROM AssetTaskTemplates;
"@

try {
    # Using .NET SqlClient directly
    $connectionString = "Server=localhost;Database=RHEMA-ERP;Trusted_Connection=true;MultipleActiveResultSets=true;TrustServerCertificate=true;"
    
    $connection = New-Object System.Data.SqlClient.SqlConnection($connectionString)
    $command = $connection.CreateCommand()
    $command.CommandText = $scriptContent
    $command.CommandTimeout = 30
    
    $connection.Open()
    Write-Host "Connected to database" -ForegroundColor Green
    
    $adapter = New-Object System.Data.SqlClient.SqlDataAdapter($command)
    $dataset = New-Object System.Data.DataSet
    [void]$adapter.Fill($dataset)
    
    $connection.Close()
    
    Write-Host "`nResults:" -ForegroundColor Yellow
    $dataset.Tables[0] | Format-Table -AutoSize
    
    Write-Host "`nTechnician IDs seeded successfully!" -ForegroundColor Green
}
catch {
    Write-Host "Error: $_" -ForegroundColor Red
    Write-Host $_.Exception.Message -ForegroundColor Red
}
