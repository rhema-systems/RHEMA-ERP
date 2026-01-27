# Database Seeding Summary

## Overview
Successfully created and executed a database seeding script to populate all master data tables in the ERP system.

## Seeding Script
**Location:** `scripts/seed-database.ps1`

### Features
- ✅ Automatically retrieves connection string from user secrets
- ✅ Color-coded console output for better visibility
- ✅ Idempotent - can be run multiple times without duplicating data
- ✅ Error handling with detailed error messages
- ✅ Transaction-safe SQL operations

### Usage
```powershell
# Run from project root
.\scripts\seed-database.ps1
```

## Seeded Data Summary

### 1. Countries (4 records)
- Ghana (GHA)
- Nigeria (NGA)
- United States (USA)
- United Kingdom (GBR)

### 2. Departments (7 records)
- Human Resources (HR) - Support
- Finance (FIN) - Support
- IT (IT) - Support
- Operations (OPS) - Operations
- Sales (SALES) - Sales
- Procurement (PROC) - Operations
- Maintenance (MAINT) - Operations

### 3. Partner Categories (6 records)
**Suppliers:**
- Raw Materials (RM)
- Office Supplies (OS)
- IT Equipment (IT)

**Contractors:**
- Construction (CONST)
- Electrical (ELEC)
- Plumbing (PLUMB)

### 4. Contractor Specializations (10 records)
- General Construction (GEN-CONST) - Requires License
- Electrical Installation (ELEC-INST) - Requires License
- Plumbing & Sanitation (PLUMB-SAN) - Requires License
- HVAC Systems (HVAC) - Requires License
- Painting & Decoration (PAINT-DEC)
- Carpentry (CARP)
- Roofing (ROOF) - Requires License
- Landscaping (LAND)
- Security Systems (SEC-SYS) - Requires License
- Network Cabling (NET-CAB)

### 5. License Types (7 records)
**Mandatory Licenses:**
- Business Operating License (BUS-LIC) - 12 months validity
- Tax Clearance Certificate (TAX-CLR) - 12 months validity
- Professional Indemnity Insurance (PROF-IND) - 12 months validity
- Safety Certification (SAFE-CERT) - 36 months validity

**Optional Licenses:**
- Electrical Contractor License (ELEC-LIC) - 24 months validity
- Plumbing Contractor License (PLUMB-LIC) - 24 months validity
- General Contractor License (GEN-CONT) - 24 months validity

### 6. Asset Types (6 records)
- **Vehicle (VEH)** - Requires: Location, Operating Hours, Mileage, Licensing, Inspections, Safety Checks
- **Computer (COMP)** - Requires: Location only
- **Machinery (MACH)** - Requires: Location, Operating Hours, Licensing, Inspections, Condition Monitoring, Safety Checks, Lockout/Tagout, Permits
- **Furniture (FURN)** - Requires: Location only (no preventive maintenance)
- **Building (BLDG)** - Requires: Location, Inspections, Hierarchy Support, Safety Checks, Permits
- **Equipment (EQUIP)** - Requires: Location only

### 7. Maintenance Types (5 records)
- **Preventive Maintenance (PM)** - Scheduled, Time-based, Medium priority
- **Corrective Maintenance (CM)** - Scheduled, High priority, 60 min downtime
- **Predictive Maintenance (PDM)** - Condition-based, Medium priority, 30 min downtime, 7 days lead time
- **Emergency Maintenance (EM)** - Emergency, Critical priority, 120 min downtime, Requires approval & shutdown
- **Inspection (INSP)** - Routine, Low priority, Time-based

## Database Verification

All tables successfully seeded:
```
TableName                 RecordCount
------------------------- -----------
Countries                           4
Departments                         7
PartnerCategories                   6
ContractorSpecializations          10
LicenseTypes                        7
AssetTypes                          6
MaintenanceTypes                    5
```

## Technical Details

### Default Tenant ID
All records are seeded with the default tenant ID: `00000000-0000-0000-0000-000000000001`

### Audit Fields
All records include:
- `CreatedAt` - Set to current UTC time
- `IsDeleted` - Set to `false` (0)
- `TenantId` - Set to default tenant ID

### Connection String
Retrieved from user secrets:
- **Server:** LOCALHOST\SQL2017
- **Database:** RhemaERP
- **Authentication:** SQL Server Authentication

## Next Steps

### Additional Seeding Needed
The following tables may need seeding based on business requirements:
- [ ] **Inventory Categories** - Product categorization
- [ ] **Warehouses** - Storage locations
- [ ] **Employee Positions** - Job titles and roles
- [ ] **Currency** - Supported currencies
- [ ] **Tax Rates** - Tax configuration
- [ ] **Payment Terms** - Standard payment terms
- [ ] **Units of Measure** - If implemented as separate entity

### Admin User Seeding
Currently not implemented. To seed admin user, you would need to:
1. Use ASP.NET Core Identity UserManager
2. Create user with proper password hashing
3. Assign to SuperAdmin role

This requires running from within the application context, not via SQL script.

## Maintenance

### Re-running the Script
The script is idempotent and can be safely re-run. It checks for existing data before inserting:
```sql
IF NOT EXISTS (SELECT 1 FROM TableName WHERE TenantId = '$tenantId')
BEGIN
    -- Insert statements
END
```

### Updating Seed Data
To update seed data:
1. Edit `scripts/seed-database.ps1`
2. Modify the SQL INSERT statements
3. Re-run the script (existing data will be preserved)

### Clearing Seed Data
To clear and re-seed:
```sql
-- Delete existing seed data
DELETE FROM Countries WHERE TenantId = '00000000-0000-0000-0000-000000000001';
-- Repeat for other tables
```

Then re-run the seeding script.

---

**Created:** 2025-11-27  
**Status:** ✅ Complete  
**Script Location:** `scripts/seed-database.ps1`

