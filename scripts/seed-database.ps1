#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Seeds the ERP System database with initial data

.DESCRIPTION
    This script seeds the database with essential master data using SQL scripts

.EXAMPLE
    .\scripts\seed-database.ps1
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
    Write-Header "ERP SYSTEM DATABASE SEEDER"

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

    Write-Header "SEEDING DATA"

    # Seed Countries
    Write-Step "Seeding countries..."
    $sql = @"
IF NOT EXISTS (SELECT 1 FROM Countries WHERE TenantId = '$tenantId')
BEGIN
    INSERT INTO Countries (Id, Name, Code, Alpha2Code, IsActive, TenantId, CreatedAt, IsDeleted)
    VALUES 
        (NEWID(), 'Ghana', 'GHA', 'GH', 1, '$tenantId', '$now', 0),
        (NEWID(), 'Nigeria', 'NGA', 'NG', 1, '$tenantId', '$now', 0),
        (NEWID(), 'United States', 'USA', 'US', 1, '$tenantId', '$now', 0),
        (NEWID(), 'United Kingdom', 'GBR', 'GB', 1, '$tenantId', '$now', 0);
    PRINT '✓ Seeded 4 countries';
END
ELSE
    PRINT '✓ Countries already exist';
"@
    sqlcmd -S $server -U $userId -P $password -d $database -Q $sql -b
    if ($LASTEXITCODE -ne 0) { throw "Failed to seed countries" }

    # Seed Departments
    Write-Step "Seeding departments..."
    $sql = @"
IF NOT EXISTS (SELECT 1 FROM Departments WHERE TenantId = '$tenantId')
BEGIN
    INSERT INTO Departments (Id, Name, Code, DepartmentType, IsActive, TenantId, CreatedAt, IsDeleted)
    VALUES 
        (NEWID(), 'Human Resources', 'HR', 1, 1, '$tenantId', '$now', 0),
        (NEWID(), 'Finance', 'FIN', 1, 1, '$tenantId', '$now', 0),
        (NEWID(), 'IT', 'IT', 1, 1, '$tenantId', '$now', 0),
        (NEWID(), 'Operations', 'OPS', 0, 1, '$tenantId', '$now', 0),
        (NEWID(), 'Sales', 'SALES', 2, 1, '$tenantId', '$now', 0),
        (NEWID(), 'Procurement', 'PROC', 0, 1, '$tenantId', '$now', 0),
        (NEWID(), 'Maintenance', 'MAINT', 0, 1, '$tenantId', '$now', 0);
    PRINT '✓ Seeded 7 departments';
END
ELSE
    PRINT '✓ Departments already exist';
"@
    sqlcmd -S $server -U $userId -P $password -d $database -Q $sql -b
    if ($LASTEXITCODE -ne 0) { throw "Failed to seed departments" }

    # Seed Partner Categories
    Write-Step "Seeding business partner categories..."
    $sql = @"
IF NOT EXISTS (SELECT 1 FROM PartnerCategories WHERE TenantId = '$tenantId')
BEGIN
    INSERT INTO PartnerCategories (Id, CategoryCode, CategoryName, CategoryType, IsActive, TenantId, CreatedAt, IsDeleted)
    VALUES 
        (NEWID(), 'RM', 'Raw Materials', 'Supplier', 1, '$tenantId', '$now', 0),
        (NEWID(), 'OS', 'Office Supplies', 'Supplier', 1, '$tenantId', '$now', 0),
        (NEWID(), 'IT', 'IT Equipment', 'Supplier', 1, '$tenantId', '$now', 0),
        (NEWID(), 'CONST', 'Construction', 'Contractor', 1, '$tenantId', '$now', 0),
        (NEWID(), 'ELEC', 'Electrical', 'Contractor', 1, '$tenantId', '$now', 0),
        (NEWID(), 'PLUMB', 'Plumbing', 'Contractor', 1, '$tenantId', '$now', 0);
    PRINT '✓ Seeded 6 partner categories';
END
ELSE
    PRINT '✓ Partner categories already exist';
"@
    sqlcmd -S $server -U $userId -P $password -d $database -Q $sql -b
    if ($LASTEXITCODE -ne 0) { throw "Failed to seed partner categories" }

    # Seed Contractor Specializations
    Write-Step "Seeding contractor specializations..."
    $sql = @"
IF NOT EXISTS (SELECT 1 FROM ContractorSpecializations WHERE TenantId = '$tenantId')
BEGIN
    INSERT INTO ContractorSpecializations (Id, SpecializationCode, SpecializationName, Description, RequiresLicense, IsActive, TenantId, CreatedAt, IsDeleted)
    VALUES
        (NEWID(), 'GEN-CONST', 'General Construction', 'General building and construction work', 1, 1, '$tenantId', '$now', 0),
        (NEWID(), 'ELEC-INST', 'Electrical Installation', 'Electrical wiring and installation', 1, 1, '$tenantId', '$now', 0),
        (NEWID(), 'PLUMB-SAN', 'Plumbing & Sanitation', 'Plumbing and sanitation systems', 1, 1, '$tenantId', '$now', 0),
        (NEWID(), 'HVAC', 'HVAC Systems', 'Heating, ventilation, and air conditioning', 1, 1, '$tenantId', '$now', 0),
        (NEWID(), 'PAINT-DEC', 'Painting & Decoration', 'Interior and exterior painting', 0, 1, '$tenantId', '$now', 0),
        (NEWID(), 'CARP', 'Carpentry', 'Woodwork and carpentry services', 0, 1, '$tenantId', '$now', 0),
        (NEWID(), 'ROOF', 'Roofing', 'Roof installation and repair', 1, 1, '$tenantId', '$now', 0),
        (NEWID(), 'LAND', 'Landscaping', 'Landscape design and maintenance', 0, 1, '$tenantId', '$now', 0),
        (NEWID(), 'SEC-SYS', 'Security Systems', 'Security system installation', 1, 1, '$tenantId', '$now', 0),
        (NEWID(), 'NET-CAB', 'Network Cabling', 'Data and network cabling', 0, 1, '$tenantId', '$now', 0);
    PRINT '✓ Seeded 10 contractor specializations';
END
ELSE
    PRINT '✓ Contractor specializations already exist';
"@
    sqlcmd -S $server -U $userId -P $password -d $database -Q $sql -b
    if ($LASTEXITCODE -ne 0) { throw "Failed to seed contractor specializations" }

    # Seed License Types
    Write-Step "Seeding license types..."
    $sql = @"
IF NOT EXISTS (SELECT 1 FROM LicenseTypes WHERE TenantId = '$tenantId')
BEGIN
    INSERT INTO LicenseTypes (Id, LicenseCode, LicenseName, Description, ValidityPeriodMonths, IsMandatory, IsActive, TenantId, CreatedAt, IsDeleted)
    VALUES
        (NEWID(), 'BUS-LIC', 'Business Operating License', 'General business operating license', 12, 1, 1, '$tenantId', '$now', 0),
        (NEWID(), 'TAX-CLR', 'Tax Clearance Certificate', 'Tax compliance certificate', 12, 1, 1, '$tenantId', '$now', 0),
        (NEWID(), 'PROF-IND', 'Professional Indemnity Insurance', 'Professional liability insurance', 12, 1, 1, '$tenantId', '$now', 0),
        (NEWID(), 'ELEC-LIC', 'Electrical Contractor License', 'License for electrical work', 24, 0, 1, '$tenantId', '$now', 0),
        (NEWID(), 'PLUMB-LIC', 'Plumbing Contractor License', 'License for plumbing work', 24, 0, 1, '$tenantId', '$now', 0),
        (NEWID(), 'GEN-CONT', 'General Contractor License', 'General construction license', 24, 0, 1, '$tenantId', '$now', 0),
        (NEWID(), 'SAFE-CERT', 'Safety Certification', 'Occupational safety certification', 36, 1, 1, '$tenantId', '$now', 0);
    PRINT '✓ Seeded 7 license types';
END
ELSE
    PRINT '✓ License types already exist';
"@
    sqlcmd -S $server -U $userId -P $password -d $database -Q $sql -b
    if ($LASTEXITCODE -ne 0) { throw "Failed to seed license types" }

    # Seed Asset Types
    Write-Step "Seeding asset types..."
    $sql = @"
IF NOT EXISTS (SELECT 1 FROM AssetTypes WHERE TenantId = '$tenantId')
BEGIN
    INSERT INTO AssetTypes (Id, Name, Code, Description, RequiresLocation, RequiresOperatingHours, RequiresMileageTracking, RequiresLicensing, RequiresInspections, SupportsHierarchy, RequiresSpecializedFields, DefaultMaintenanceIntervalDays, RequiresPreventiveMaintenance, RequiresConditionMonitoring, RequiresSafetyChecks, RequiresLockoutTagout, RequiresPermits, DefaultWorkOrderPriority, DefaultEstimatedHours, IsActive, TenantId, CreatedAt, IsDeleted)
    VALUES
        (NEWID(), 'Vehicle', 'VEH', 'Motor vehicles', 1, 1, 1, 1, 1, 0, 1, 90, 1, 0, 1, 0, 0, 3, 2.0, 1, '$tenantId', '$now', 0),
        (NEWID(), 'Computer', 'COMP', 'Desktop and laptop computers', 1, 0, 0, 0, 0, 0, 0, 180, 1, 0, 0, 0, 0, 4, 1.0, 1, '$tenantId', '$now', 0),
        (NEWID(), 'Machinery', 'MACH', 'Industrial machinery', 1, 1, 0, 1, 1, 0, 1, 60, 1, 1, 1, 1, 1, 2, 4.0, 1, '$tenantId', '$now', 0),
        (NEWID(), 'Furniture', 'FURN', 'Office furniture', 1, 0, 0, 0, 0, 0, 0, 365, 0, 0, 0, 0, 0, 5, 0.5, 1, '$tenantId', '$now', 0),
        (NEWID(), 'Building', 'BLDG', 'Buildings and structures', 1, 0, 0, 0, 1, 1, 1, 180, 1, 0, 1, 0, 1, 2, 8.0, 1, '$tenantId', '$now', 0),
        (NEWID(), 'Equipment', 'EQUIP', 'General equipment', 1, 0, 0, 0, 0, 0, 0, 90, 1, 0, 0, 0, 0, 3, 2.0, 1, '$tenantId', '$now', 0);
    PRINT '✓ Seeded 6 asset types';
END
ELSE
    PRINT '✓ Asset types already exist';
"@
    sqlcmd -S $server -U $userId -P $password -d $database -Q $sql -b
    if ($LASTEXITCODE -ne 0) { throw "Failed to seed asset types" }

    # Seed Maintenance Types
    Write-Step "Seeding maintenance types..."
    $sql = @"
IF NOT EXISTS (SELECT 1 FROM MaintenanceTypes WHERE TenantId = '$tenantId')
BEGIN
    INSERT INTO MaintenanceTypes (Id, Name, Code, Description, Category, MaintenanceClass, Location, IsConditionBased, IsUsageBased, IsTimeBased, RequiresSafetyPermit, RequiresShutdown, RequiresSpecialTraining, RequiresApproval, ApprovalLevels, EstimatedHours, EstimatedCost, DefaultPriority, Criticality, LeadTimeDays, DowntimeMinutes, RequiresQualityCheck, RequiresDocumentation, RequiresCertification, AverageCompletionHours, AverageCost, IsActive, SortOrder, TenantId, CreatedAt, IsDeleted)
    VALUES
        (NEWID(), 'Preventive Maintenance', 'PM', 'Scheduled preventive maintenance', 'Scheduled', 'Preventive', 'Internal', 0, 0, 1, 0, 0, 0, 0, 1, 2.0, 0, 3, 'Medium', 0, 0, 0, 1, 0, 2.0, 0, 1, 1, '$tenantId', '$now', 0),
        (NEWID(), 'Corrective Maintenance', 'CM', 'Repair and corrective work', 'Scheduled', 'Corrective', 'Internal', 0, 0, 0, 0, 0, 0, 0, 1, 4.0, 0, 2, 'High', 0, 60, 1, 1, 0, 4.0, 0, 1, 2, '$tenantId', '$now', 0),
        (NEWID(), 'Predictive Maintenance', 'PDM', 'Condition-based maintenance', 'Scheduled', 'Predictive', 'Internal', 1, 0, 0, 0, 0, 1, 0, 1, 3.0, 0, 3, 'Medium', 7, 30, 1, 1, 0, 3.0, 0, 1, 3, '$tenantId', '$now', 0),
        (NEWID(), 'Emergency Maintenance', 'EM', 'Emergency repairs', 'Emergency', 'Emergency', 'Internal', 0, 0, 0, 1, 1, 0, 1, 2, 6.0, 0, 1, 'Critical', 0, 120, 1, 1, 0, 6.0, 0, 1, 4, '$tenantId', '$now', 0),
        (NEWID(), 'Inspection', 'INSP', 'Regular inspections', 'Inspection', 'Routine', 'Internal', 0, 0, 1, 0, 0, 0, 0, 1, 1.0, 0, 4, 'Low', 0, 0, 0, 1, 0, 1.0, 0, 1, 5, '$tenantId', '$now', 0);
    PRINT '✓ Seeded 5 maintenance types';
END
ELSE
    PRINT '✓ Maintenance types already exist';
"@
    sqlcmd -S $server -U $userId -P $password -d $database -Q $sql -b
    if ($LASTEXITCODE -ne 0) { throw "Failed to seed maintenance types" }

    # Seed Tenants
    Write-Step "Seeding tenants..."
    $sql = @"
SET QUOTED_IDENTIFIER ON;
IF NOT EXISTS (SELECT 1 FROM Tenants WHERE Id = '$tenantId')
BEGIN
    INSERT INTO Tenants (Id, Name, Code, Status, SubscriptionStartDate, SubscriptionEndDate, CreatedAt, IsDeleted)
    VALUES
        ('$tenantId', 'Default Tenant', 'DEFAULT', 0, '$now', DATEADD(YEAR, 10, '$now'), '$now', 0);
    PRINT '✓ Seeded default tenant';
END
ELSE
    PRINT '✓ Tenant already exists';
"@
    sqlcmd -S $server -U $userId -P $password -d $database -Q $sql -b
    if ($LASTEXITCODE -ne 0) { throw "Failed to seed tenants" }

    # Seed Employee Positions
    Write-Step "Seeding employee positions..."
    $sql = @"
DECLARE @HRDeptId UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM Departments WHERE Code = 'HR' AND TenantId = '$tenantId');
DECLARE @FinDeptId UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM Departments WHERE Code = 'FIN' AND TenantId = '$tenantId');
DECLARE @ITDeptId UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM Departments WHERE Code = 'IT' AND TenantId = '$tenantId');
DECLARE @OpsDeptId UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM Departments WHERE Code = 'OPS' AND TenantId = '$tenantId');

IF NOT EXISTS (SELECT 1 FROM EmployeePositions WHERE TenantId = '$tenantId')
BEGIN
    INSERT INTO EmployeePositions (Id, Title, Code, DepartmentId, Level, MinSalary, MaxSalary, RequiresCertification, IsActive, TenantId, CreatedAt, IsDeleted)
    VALUES
        (NEWID(), 'Chief Executive Officer', 'CEO', @OpsDeptId, 10, 150000, 300000, 1, 1, '$tenantId', '$now', 0),
        (NEWID(), 'Chief Financial Officer', 'CFO', @FinDeptId, 10, 120000, 250000, 1, 1, '$tenantId', '$now', 0),
        (NEWID(), 'Chief Technology Officer', 'CTO', @ITDeptId, 10, 120000, 250000, 1, 1, '$tenantId', '$now', 0),
        (NEWID(), 'Manager', 'MGR', @OpsDeptId, 7, 60000, 120000, 0, 1, '$tenantId', '$now', 0),
        (NEWID(), 'Team Lead', 'TL', @OpsDeptId, 5, 45000, 80000, 0, 1, '$tenantId', '$now', 0),
        (NEWID(), 'Senior Officer', 'SR-OFF', @OpsDeptId, 4, 35000, 60000, 0, 1, '$tenantId', '$now', 0),
        (NEWID(), 'Officer', 'OFF', @OpsDeptId, 3, 25000, 45000, 0, 1, '$tenantId', '$now', 0),
        (NEWID(), 'Junior Officer', 'JR-OFF', @OpsDeptId, 1, 18000, 30000, 0, 1, '$tenantId', '$now', 0);
    PRINT '✓ Seeded 8 employee positions';
END
ELSE
    PRINT '✓ Employee positions already exist';
"@
    sqlcmd -S $server -U $userId -P $password -d $database -Q $sql -b
    if ($LASTEXITCODE -ne 0) { throw "Failed to seed employee positions" }

    # Seed Skills
    Write-Step "Seeding skills..."
    $sql = @"
IF NOT EXISTS (SELECT 1 FROM Skills WHERE TenantId = '$tenantId')
BEGIN
    INSERT INTO Skills (Id, Name, Category, Description, RequiresCertification, IsActive, TenantId, CreatedAt, IsDeleted)
    VALUES
        (NEWID(), 'Electrical Wiring', 'Technical', 'Electrical wiring and installation', 1, 1, '$tenantId', '$now', 0),
        (NEWID(), 'Plumbing', 'Technical', 'Plumbing installation and repair', 1, 1, '$tenantId', '$now', 0),
        (NEWID(), 'HVAC Maintenance', 'Technical', 'HVAC system maintenance', 1, 1, '$tenantId', '$now', 0),
        (NEWID(), 'Carpentry', 'Technical', 'Woodworking and carpentry', 0, 1, '$tenantId', '$now', 0),
        (NEWID(), 'Welding', 'Technical', 'Metal welding and fabrication', 1, 1, '$tenantId', '$now', 0),
        (NEWID(), 'Project Management', 'Management', 'Project planning and management', 0, 1, '$tenantId', '$now', 0),
        (NEWID(), 'Quality Control', 'Quality', 'Quality assurance and control', 0, 1, '$tenantId', '$now', 0),
        (NEWID(), 'Safety Management', 'Safety', 'Occupational health and safety', 1, 1, '$tenantId', '$now', 0);
    PRINT '✓ Seeded 8 skills';
END
ELSE
    PRINT '✓ Skills already exist';
"@
    sqlcmd -S $server -U $userId -P $password -d $database -Q $sql -b
    if ($LASTEXITCODE -ne 0) { throw "Failed to seed skills" }

    # Seed Warehouses
    Write-Step "Seeding warehouses..."
    $sql = @"
IF NOT EXISTS (SELECT 1 FROM Warehouses WHERE TenantId = '$tenantId')
BEGIN
    INSERT INTO Warehouses (Id, Name, Code, WarehouseType, Address, City, State, Country, IsActive, TenantId, CreatedAt, IsDeleted)
    VALUES
        (NEWID(), 'Main Warehouse', 'WH-MAIN', 'Central', 'Main Street', 'Accra', 'Greater Accra', 'Ghana', 1, '$tenantId', '$now', 0),
        (NEWID(), 'Secondary Warehouse', 'WH-SEC', 'Regional', 'Industrial Area', 'Tema', 'Greater Accra', 'Ghana', 1, '$tenantId', '$now', 0);
    PRINT '✓ Seeded 2 warehouses';
END
ELSE
    PRINT '✓ Warehouses already exist';
"@
    sqlcmd -S $server -U $userId -P $password -d $database -Q $sql -b
    if ($LASTEXITCODE -ne 0) { throw "Failed to seed warehouses" }

    # Seed Inventory Categories
    Write-Step "Seeding inventory categories..."
    $sql = @"
IF NOT EXISTS (SELECT 1 FROM InventoryCategories WHERE TenantId = '$tenantId')
BEGIN
    INSERT INTO InventoryCategories (Id, Name, Code, Description, DefaultUnitOfMeasure, IsActive, TenantId, CreatedAt, IsDeleted)
    VALUES
        (NEWID(), 'Raw Materials', 'RAW', 'Raw materials for production', 'KG', 1, '$tenantId', '$now', 0),
        (NEWID(), 'Finished Goods', 'FIN', 'Finished products ready for sale', 'PCS', 1, '$tenantId', '$now', 0),
        (NEWID(), 'Office Supplies', 'OFF-SUP', 'Office supplies and stationery', 'PCS', 1, '$tenantId', '$now', 0),
        (NEWID(), 'IT Equipment', 'IT-EQ', 'Computer and IT equipment', 'PCS', 1, '$tenantId', '$now', 0),
        (NEWID(), 'Spare Parts', 'SPARE', 'Maintenance spare parts', 'PCS', 1, '$tenantId', '$now', 0),
        (NEWID(), 'Consumables', 'CONS', 'Consumable items', 'PCS', 1, '$tenantId', '$now', 0),
        (NEWID(), 'Tools & Equipment', 'TOOLS', 'Tools and equipment', 'PCS', 1, '$tenantId', '$now', 0);
    PRINT '✓ Seeded 7 inventory categories';
END
ELSE
    PRINT '✓ Inventory categories already exist';
"@
    sqlcmd -S $server -U $userId -P $password -d $database -Q $sql -b
    if ($LASTEXITCODE -ne 0) { throw "Failed to seed inventory categories" }

    # Seed Shifts
    Write-Step "Seeding shifts..."
    $sql = @"
IF NOT EXISTS (SELECT 1 FROM Shifts WHERE TenantId = '$tenantId')
BEGIN
    INSERT INTO Shifts (Id, Name, Code, StartTime, EndTime, IsActive, TenantId, CreatedAt, IsDeleted)
    VALUES
        (NEWID(), 'Morning Shift', 'MORNING', '06:00:00', '14:00:00', 1, '$tenantId', '$now', 0),
        (NEWID(), 'Afternoon Shift', 'AFTERNOON', '14:00:00', '22:00:00', 1, '$tenantId', '$now', 0),
        (NEWID(), 'Night Shift', 'NIGHT', '22:00:00', '06:00:00', 1, '$tenantId', '$now', 0),
        (NEWID(), 'Day Shift', 'DAY', '08:00:00', '17:00:00', 1, '$tenantId', '$now', 0);
    PRINT '✓ Seeded 4 shifts';
END
ELSE
    PRINT '✓ Shifts already exist';
"@
    sqlcmd -S $server -U $userId -P $password -d $database -Q $sql -b
    if ($LASTEXITCODE -ne 0) { throw "Failed to seed shifts" }

    # Seed Work Order Types
    Write-Step "Seeding work order types..."
    $sql = @"
IF NOT EXISTS (SELECT 1 FROM WorkOrderTypes WHERE TenantId = '$tenantId')
BEGIN
    INSERT INTO WorkOrderTypes (Id, Name, Code, Description, IsActive, TenantId, CreatedAt, IsDeleted)
    VALUES
        (NEWID(), 'Preventive Maintenance', 'PM', 'Scheduled preventive maintenance work orders', 1, '$tenantId', '$now', 0),
        (NEWID(), 'Corrective Maintenance', 'CM', 'Corrective and repair work orders', 1, '$tenantId', '$now', 0),
        (NEWID(), 'Emergency Repair', 'ER', 'Emergency repair work orders', 1, '$tenantId', '$now', 0),
        (NEWID(), 'Inspection', 'INSP', 'Inspection work orders', 1, '$tenantId', '$now', 0),
        (NEWID(), 'Installation', 'INST', 'New installation work orders', 1, '$tenantId', '$now', 0),
        (NEWID(), 'Modification', 'MOD', 'Modification and upgrade work orders', 1, '$tenantId', '$now', 0);
    PRINT '✓ Seeded 6 work order types';
END
ELSE
    PRINT '✓ Work order types already exist';
"@
    sqlcmd -S $server -U $userId -P $password -d $database -Q $sql -b
    if ($LASTEXITCODE -ne 0) { throw "Failed to seed work order types" }

    # Seed Priority Levels
    Write-Step "Seeding priority levels..."
    $sql = @"
IF NOT EXISTS (SELECT 1 FROM PriorityLevels WHERE TenantId = '$tenantId')
BEGIN
    INSERT INTO PriorityLevels (Id, Name, Level, ResponseTimeHours, ResolutionTimeHours, Color, IsActive, TenantId, CreatedAt, IsDeleted)
    VALUES
        (NEWID(), 'Critical', 1, 1, 4, '#FF0000', 1, '$tenantId', '$now', 0),
        (NEWID(), 'High', 2, 4, 24, '#FF6600', 1, '$tenantId', '$now', 0),
        (NEWID(), 'Medium', 3, 24, 72, '#FFCC00', 1, '$tenantId', '$now', 0),
        (NEWID(), 'Low', 4, 72, 168, '#00CC00', 1, '$tenantId', '$now', 0),
        (NEWID(), 'Deferred', 5, 168, 720, '#999999', 1, '$tenantId', '$now', 0);
    PRINT '✓ Seeded 5 priority levels';
END
ELSE
    PRINT '✓ Priority levels already exist';
"@
    sqlcmd -S $server -U $userId -P $password -d $database -Q $sql -b
    if ($LASTEXITCODE -ne 0) { throw "Failed to seed priority levels" }

    # Seed ASP.NET Roles
    Write-Step "Seeding ASP.NET roles..."
    $sql = @"
IF NOT EXISTS (SELECT 1 FROM AspNetRoles)
BEGIN
    INSERT INTO AspNetRoles (Id, Name, NormalizedName, ConcurrencyStamp)
    VALUES
        (NEWID(), 'SuperAdmin', 'SUPERADMIN', NEWID()),
        (NEWID(), 'Admin', 'ADMIN', NEWID()),
        (NEWID(), 'Manager', 'MANAGER', NEWID()),
        (NEWID(), 'Supervisor', 'SUPERVISOR', NEWID()),
        (NEWID(), 'Technician', 'TECHNICIAN', NEWID()),
        (NEWID(), 'User', 'USER', NEWID()),
        (NEWID(), 'Viewer', 'VIEWER', NEWID());
    PRINT '✓ Seeded 7 ASP.NET roles';
END
ELSE
    PRINT '✓ ASP.NET roles already exist';
"@
    sqlcmd -S $server -U $userId -P $password -d $database -Q $sql -b
    if ($LASTEXITCODE -ne 0) { throw "Failed to seed ASP.NET roles" }

    Write-Host ""
    Write-Success "Database seeding completed successfully!"
    Write-Host ""

} catch {
    Write-Host ""
    Write-Error "An error occurred: $_"
    exit 1
}

