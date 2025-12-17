#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Seeds maintenance-related tables in the ERP System database

.DESCRIPTION
    This script seeds maintenance-specific master data tables

.EXAMPLE
    .\scripts\seed-maintenance-tables.ps1
#>

$ErrorActionPreference = "Stop"

# Colors for output
function Write-ColorOutput {
    param([string]$Message, [string]$Color = "White")
    Write-Host $Message -ForegroundColor $Color
}

function Write-Header {
    param([string]$Message)
    Write-Host ""
    Write-ColorOutput "═══════════════════════════════════════════════════════════════" "Cyan"
    Write-ColorOutput "  $Message" "Cyan"
    Write-ColorOutput "═══════════════════════════════════════════════════════════════" "Cyan"
    Write-Host ""
}

function Write-Step {
    param([string]$Message)
    Write-ColorOutput "▶ $Message" "Yellow"
}

function Write-Success {
    param([string]$Message)
    Write-ColorOutput "✓ $Message" "Green"
}

function Write-Error {
    param([string]$Message)
    Write-ColorOutput "✗ $Message" "Red"
}

try {
    Write-Header "MAINTENANCE TABLES SEEDER"

    # Get connection string from user secrets
    Write-Step "Getting connection string..."
    $connectionString = dotnet user-secrets list --project src/ErpSystem.Api/ErpSystem.Api.csproj | Select-String "ConnectionStrings:DefaultConnection" | ForEach-Object { $_.ToString().Split('=', 2)[1].Trim() }
    
    if ([string]::IsNullOrEmpty($connectionString)) {
        Write-Error "Connection string not found in user secrets!"
        exit 1
    }
    
    # Parse connection string
    $server = ($connectionString | Select-String -Pattern "Server=([^;]+)" | ForEach-Object { $_.Matches.Groups[1].Value })
    $database = ($connectionString | Select-String -Pattern "Database=([^;]+)" | ForEach-Object { $_.Matches.Groups[1].Value })
    $userId = ($connectionString | Select-String -Pattern "User Id=([^;]+)" | ForEach-Object { $_.Matches.Groups[1].Value })
    $password = ($connectionString | Select-String -Pattern "Password=([^;]+)" | ForEach-Object { $_.Matches.Groups[1].Value })
    
    Write-Success "Connection: $server / $database"

    # Default tenant ID
    $tenantId = "00000000-0000-0000-0000-000000000001"
    $now = (Get-Date).ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss")

    Write-Header "SEEDING MAINTENANCE DATA"

    # Seed Maintenance Asset Categories
    Write-Step "Seeding maintenance asset categories..."
    $sql = @"
IF NOT EXISTS (SELECT 1 FROM MaintenanceAssetCategories WHERE TenantId = '$tenantId')
BEGIN
    INSERT INTO MaintenanceAssetCategories (Id, Name, Code, Description, MaintenanceScheduleType, AutoGenerateSchedules, MaintenanceType, IsActive, TenantId, CreatedAt, IsDeleted)
    VALUES
        (NEWID(), 'Vehicles', 'VEH', 'Motor vehicles and transportation', 'multi', 1, 'Distance', 1, '$tenantId', '$now', 0),
        (NEWID(), 'Machinery', 'MACH', 'Industrial machinery and equipment', 'multi', 1, 'Usage', 1, '$tenantId', '$now', 0),
        (NEWID(), 'Buildings', 'BLDG', 'Buildings and structures', 'single', 1, 'Time', 1, '$tenantId', '$now', 0),
        (NEWID(), 'IT Equipment', 'IT', 'Computers and IT infrastructure', 'single', 1, 'Time', 1, '$tenantId', '$now', 0),
        (NEWID(), 'HVAC Systems', 'HVAC', 'Heating, ventilation, and air conditioning', 'multi', 1, 'Usage', 1, '$tenantId', '$now', 0),
        (NEWID(), 'Electrical Systems', 'ELEC', 'Electrical systems and equipment', 'single', 1, 'Time', 1, '$tenantId', '$now', 0),
        (NEWID(), 'Plumbing Systems', 'PLUMB', 'Plumbing and water systems', 'single', 1, 'Time', 1, '$tenantId', '$now', 0);
    PRINT '✓ Seeded 7 maintenance asset categories';
END
ELSE
    PRINT '✓ Maintenance asset categories already exist';
"@
    sqlcmd -S $server -U $userId -P $password -d $database -Q $sql -b
    if ($LASTEXITCODE -ne 0) { throw "Failed to seed maintenance asset categories" }

    # Seed Technician Skills
    Write-Step "Seeding technician skills..."
    $sql = @"
IF NOT EXISTS (SELECT 1 FROM TechnicianSkills WHERE TenantId = '$tenantId')
BEGIN
    INSERT INTO TechnicianSkills (Id, Name, Description, Category, IsActive, TenantId, CreatedAt, IsDeleted)
    VALUES
        (NEWID(), 'Electrical Repair', 'Electrical system repair and maintenance', 'Electrical', 1, '$tenantId', '$now', 0),
        (NEWID(), 'Mechanical Repair', 'Mechanical system repair and maintenance', 'Mechanical', 1, '$tenantId', '$now', 0),
        (NEWID(), 'HVAC Maintenance', 'HVAC system maintenance and repair', 'HVAC', 1, '$tenantId', '$now', 0),
        (NEWID(), 'Plumbing', 'Plumbing installation and repair', 'Plumbing', 1, '$tenantId', '$now', 0),
        (NEWID(), 'Welding', 'Metal welding and fabrication', 'Fabrication', 1, '$tenantId', '$now', 0),
        (NEWID(), 'Carpentry', 'Woodworking and carpentry', 'Carpentry', 1, '$tenantId', '$now', 0),
        (NEWID(), 'Painting', 'Painting and finishing', 'Finishing', 1, '$tenantId', '$now', 0),
        (NEWID(), 'Diagnostics', 'Equipment diagnostics and troubleshooting', 'Diagnostics', 1, '$tenantId', '$now', 0);
    PRINT '✓ Seeded 8 technician skills';
END
ELSE
    PRINT '✓ Technician skills already exist';
"@
    sqlcmd -S $server -U $userId -P $password -d $database -Q $sql -b
    if ($LASTEXITCODE -ne 0) { throw "Failed to seed technician skills" }

    # Seed Technician Shifts
    Write-Step "Seeding technician shifts..."
    $sql = @"
IF NOT EXISTS (SELECT 1 FROM TechnicianShifts WHERE TenantId = '$tenantId')
BEGIN
    INSERT INTO TechnicianShifts (Id, Name, StartTime, EndTime, DaysOfWeek, ScheduledHours, BreakMinutes, IsActive, TenantId, CreatedAt, IsDeleted)
    VALUES
        (NEWID(), 'Morning Shift', '06:00:00', '14:00:00', '[1,2,3,4,5]', 8.0, 60, 1, '$tenantId', '$now', 0),
        (NEWID(), 'Afternoon Shift', '14:00:00', '22:00:00', '[1,2,3,4,5]', 8.0, 60, 1, '$tenantId', '$now', 0),
        (NEWID(), 'Night Shift', '22:00:00', '06:00:00', '[1,2,3,4,5]', 8.0, 60, 1, '$tenantId', '$now', 0),
        (NEWID(), 'Day Shift', '08:00:00', '17:00:00', '[1,2,3,4,5]', 8.0, 60, 1, '$tenantId', '$now', 0);
    PRINT '✓ Seeded 4 technician shifts';
END
ELSE
    PRINT '✓ Technician shifts already exist';
"@
    sqlcmd -S $server -U $userId -P $password -d $database -Q $sql -b
    if ($LASTEXITCODE -ne 0) { throw "Failed to seed technician shifts" }

    # Seed Work Order Types (already seeded in main script, but adding here for completeness)
    Write-Step "Seeding work order types..."
    $sql = @"
IF NOT EXISTS (SELECT 1 FROM WorkOrderTypes WHERE TenantId = '$tenantId')
BEGIN
    INSERT INTO WorkOrderTypes (Id, Name, Code, Description, RequiresApproval, DefaultPriority, IsActive, TenantId, CreatedAt, IsDeleted)
    VALUES
        (NEWID(), 'Preventive Maintenance', 'PM', 'Scheduled preventive maintenance work orders', 0, 3, 1, '$tenantId', '$now', 0),
        (NEWID(), 'Corrective Maintenance', 'CM', 'Corrective and repair work orders', 0, 2, 1, '$tenantId', '$now', 0),
        (NEWID(), 'Emergency Repair', 'ER', 'Emergency repair work orders', 1, 1, 1, '$tenantId', '$now', 0),
        (NEWID(), 'Inspection', 'INSP', 'Inspection work orders', 0, 4, 1, '$tenantId', '$now', 0),
        (NEWID(), 'Installation', 'INST', 'New installation work orders', 1, 3, 1, '$tenantId', '$now', 0),
        (NEWID(), 'Modification', 'MOD', 'Modification and upgrade work orders', 1, 3, 1, '$tenantId', '$now', 0),
        (NEWID(), 'Calibration', 'CAL', 'Equipment calibration work orders', 0, 3, 1, '$tenantId', '$now', 0),
        (NEWID(), 'Overhaul', 'OH', 'Major overhaul work orders', 1, 2, 1, '$tenantId', '$now', 0);
    PRINT '✓ Seeded 8 work order types';
END
ELSE
    PRINT '✓ Work order types already exist';
"@
    sqlcmd -S $server -U $userId -P $password -d $database -Q $sql -b
    if ($LASTEXITCODE -ne 0) { throw "Failed to seed work order types" }

    # Seed Priority Levels (already seeded in main script, but adding here for completeness)
    Write-Step "Seeding priority levels..."
    $sql = @"
IF NOT EXISTS (SELECT 1 FROM PriorityLevels WHERE TenantId = '$tenantId')
BEGIN
    INSERT INTO PriorityLevels (Id, Name, Level, Description, Color, ResponseTimeHours, IsActive, TenantId, CreatedAt, IsDeleted)
    VALUES
        (NEWID(), 'Critical', 1, 'Critical priority - immediate attention required', '#FF0000', 1, 1, '$tenantId', '$now', 0),
        (NEWID(), 'High', 2, 'High priority - urgent attention needed', '#FF6600', 4, 1, '$tenantId', '$now', 0),
        (NEWID(), 'Medium', 3, 'Medium priority - normal processing', '#FFCC00', 24, 1, '$tenantId', '$now', 0),
        (NEWID(), 'Low', 4, 'Low priority - can be deferred', '#00CC00', 72, 1, '$tenantId', '$now', 0),
        (NEWID(), 'Deferred', 5, 'Deferred - scheduled for later', '#999999', 168, 1, '$tenantId', '$now', 0);
    PRINT '✓ Seeded 5 priority levels';
END
ELSE
    PRINT '✓ Priority levels already exist';
"@
    sqlcmd -S $server -U $userId -P $password -d $database -Q $sql -b
    if ($LASTEXITCODE -ne 0) { throw "Failed to seed priority levels" }

    # Seed Inspection Templates
    Write-Step "Seeding inspection templates..."
    $sql = @"
IF NOT EXISTS (SELECT 1 FROM InspectionTemplates WHERE TenantId = '$tenantId')
BEGIN
    INSERT INTO InspectionTemplates (Id, Name, Description, Category, InspectionType, ChecklistItems, IsActive, TenantId, CreatedAt, IsDeleted)
    VALUES
        (NEWID(), 'Safety Inspection', 'General safety inspection checklist', 'Safety', 'Safety', '[]', 1, '$tenantId', '$now', 0),
        (NEWID(), 'Quality Inspection', 'Quality control inspection checklist', 'Quality', 'Quality', '[]', 1, '$tenantId', '$now', 0),
        (NEWID(), 'Regulatory Compliance', 'Regulatory compliance inspection', 'Compliance', 'Regulatory', '[]', 1, '$tenantId', '$now', 0),
        (NEWID(), 'Preventive Maintenance Inspection', 'PM inspection checklist', 'Maintenance', 'Maintenance', '[]', 1, '$tenantId', '$now', 0),
        (NEWID(), 'Equipment Inspection', 'Equipment condition inspection', 'Equipment', 'Maintenance', '[]', 1, '$tenantId', '$now', 0);
    PRINT '✓ Seeded 5 inspection templates';
END
ELSE
    PRINT '✓ Inspection templates already exist';
"@
    sqlcmd -S $server -U $userId -P $password -d $database -Q $sql -b
    if ($LASTEXITCODE -ne 0) { throw "Failed to seed inspection templates" }

    # Seed Maintenance Tools
    Write-Step "Seeding maintenance tools..."
    $sql = @"
IF NOT EXISTS (SELECT 1 FROM MaintenanceTools WHERE TenantId = '$tenantId')
BEGIN
    INSERT INTO MaintenanceTools (Id, ToolCode, Name, Description, Category, Status, PurchasePrice, CurrentValue, DailyRentalRate, TotalUsageDays, RequiresCertification, RequiresTraining, IsActive, TenantId, CreatedAt, IsDeleted)
    VALUES
        (NEWID(), 'TOOL-001', 'Multimeter', 'Digital multimeter for electrical testing', 'Electrical', 'Available', 150.00, 150.00, 10.00, 0, 0, 0, 1, '$tenantId', '$now', 0),
        (NEWID(), 'TOOL-002', 'Torque Wrench', 'Precision torque wrench', 'Mechanical', 'Available', 250.00, 250.00, 15.00, 0, 0, 0, 1, '$tenantId', '$now', 0),
        (NEWID(), 'TOOL-003', 'Welding Machine', 'Arc welding machine', 'Specialized', 'Available', 1500.00, 1500.00, 75.00, 0, 1, 1, 1, '$tenantId', '$now', 0),
        (NEWID(), 'TOOL-004', 'Hydraulic Jack', 'Heavy duty hydraulic jack', 'General', 'Available', 500.00, 500.00, 25.00, 0, 0, 1, 1, '$tenantId', '$now', 0),
        (NEWID(), 'TOOL-005', 'Diagnostic Scanner', 'Electronic diagnostic scanner', 'Diagnostic', 'Available', 2000.00, 2000.00, 100.00, 0, 0, 1, 1, '$tenantId', '$now', 0),
        (NEWID(), 'TOOL-006', 'Pressure Gauge', 'Hydraulic pressure gauge', 'Diagnostic', 'Available', 300.00, 300.00, 20.00, 0, 0, 0, 1, '$tenantId', '$now', 0),
        (NEWID(), 'TOOL-007', 'Grease Gun', 'Pneumatic grease gun', 'General', 'Available', 100.00, 100.00, 8.00, 0, 0, 0, 1, '$tenantId', '$now', 0),
        (NEWID(), 'TOOL-008', 'Impact Wrench', 'Air impact wrench', 'General', 'Available', 350.00, 350.00, 20.00, 0, 0, 0, 1, '$tenantId', '$now', 0);
    PRINT '✓ Seeded 8 maintenance tools';
END
ELSE
    PRINT '✓ Maintenance tools already exist';
"@
    sqlcmd -S $server -U $userId -P $password -d $database -Q $sql -b
    if ($LASTEXITCODE -ne 0) { throw "Failed to seed maintenance tools" }

    # Seed Technician Teams
    Write-Step "Seeding technician teams..."
    $sql = @"
IF NOT EXISTS (SELECT 1 FROM TechnicianTeams WHERE TenantId = '$tenantId')
BEGIN
    INSERT INTO TechnicianTeams (Id, Name, Description, Status, TenantId, CreatedAt, IsDeleted)
    VALUES
        (NEWID(), 'Electrical Team', 'Electrical maintenance and repair team', 'Active', '$tenantId', '$now', 0),
        (NEWID(), 'Mechanical Team', 'Mechanical maintenance team', 'Active', '$tenantId', '$now', 0),
        (NEWID(), 'HVAC Team', 'HVAC systems team', 'Active', '$tenantId', '$now', 0),
        (NEWID(), 'Emergency Response Team', 'Emergency repair and response team', 'Active', '$tenantId', '$now', 0),
        (NEWID(), 'Preventive Maintenance Team', 'Scheduled PM team', 'Active', '$tenantId', '$now', 0);
    PRINT '✓ Seeded 5 technician teams';
END
ELSE
    PRINT '✓ Technician teams already exist';
"@
    sqlcmd -S $server -U $userId -P $password -d $database -Q $sql -b
    if ($LASTEXITCODE -ne 0) { throw "Failed to seed technician teams" }

    Write-Host ""
    Write-Success "Maintenance tables seeding completed successfully!"
    Write-Host ""
    Write-ColorOutput "Summary of seeded tables:" "Cyan"
    Write-ColorOutput "  • Maintenance Asset Categories: 7 records" "White"
    Write-ColorOutput "  • Technician Skills: 8 records" "White"
    Write-ColorOutput "  • Technician Shifts: 4 records" "White"
    Write-ColorOutput "  • Work Order Types: 8 records" "White"
    Write-ColorOutput "  • Priority Levels: 5 records" "White"
    Write-ColorOutput "  • Inspection Templates: 5 records" "White"
    Write-ColorOutput "  • Maintenance Tools: 8 records" "White"
    Write-ColorOutput "  • Technician Teams: 5 records" "White"
    Write-Host ""

} catch {
    Write-Host ""
    Write-Error "An error occurred: $_"
    exit 1
}

