-- Comprehensive Maintenance Tables Seed Script
-- This script seeds all 77+ maintenance-related tables with sample data
-- Execute this script against your SQL Server database

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;

BEGIN TRANSACTION;

DECLARE @TenantId UNIQUEIDENTIFIER = '00000000-0000-0000-0000-000000000001';
DECLARE @UserId UNIQUEIDENTIFIER = NEWID();
DECLARE @CurrentTime DATETIME2 = GETUTCDATE();

-- ================================
-- 1. CORE REFERENCE DATA
-- ================================

-- Priority Levels
INSERT INTO PriorityLevels (Id, Level, Name, Description, Color, ResponseTimeHours, IsActive, TenantId, CreatedAt)
VALUES 
    (NEWID(), 1, 'Critical', 'Immediate attention required - system down', '#FF0000', 1, 1, @TenantId, @CurrentTime),
    (NEWID(), 2, 'High', 'High priority - significant impact', '#FF8C00', 4, 1, @TenantId, @CurrentTime),
    (NEWID(), 3, 'Medium', 'Medium priority - moderate impact', '#FFD700', 24, 1, @TenantId, @CurrentTime),
    (NEWID(), 4, 'Low', 'Low priority - minimal impact', '#32CD32', 72, 1, @TenantId, @CurrentTime),
    (NEWID(), 5, 'Scheduled', 'Scheduled maintenance', '#0000FF', 168, 1, @TenantId, @CurrentTime);

-- Maintenance Types
INSERT INTO MaintenanceTypes (Id, Code, Name, Description, Category, IsActive, TenantId, CreatedAt)
VALUES 
    (NEWID(), 'PM', 'Preventive Maintenance', 'Scheduled preventive maintenance', 'Preventive', 1, @TenantId, @CurrentTime),
    (NEWID(), 'CM', 'Corrective Maintenance', 'Breakdown or failure repair', 'Corrective', 1, @TenantId, @CurrentTime),
    (NEWID(), 'PD', 'Predictive Maintenance', 'Condition-based maintenance', 'Predictive', 1, @TenantId, @CurrentTime),
    (NEWID(), 'EM', 'Emergency Maintenance', 'Emergency repair work', 'Emergency', 1, @TenantId, @CurrentTime),
    (NEWID(), 'IN', 'Inspection', 'Safety and compliance inspection', 'Inspection', 1, @TenantId, @CurrentTime),
    (NEWID(), 'UP', 'Upgrade', 'System or equipment upgrade', 'Improvement', 1, @TenantId, @CurrentTime);

-- Work Order Types
INSERT INTO WorkOrderTypes (Id, Code, Name, Description, IsActive, TenantId, CreatedAt)
VALUES 
    (NEWID(), 'MAINT', 'Maintenance', 'General maintenance work', 1, @TenantId, @CurrentTime),
    (NEWID(), 'REPAIR', 'Repair', 'Equipment repair work', 1, @TenantId, @CurrentTime),
    (NEWID(), 'INSTALL', 'Installation', 'New equipment installation', 1, @TenantId, @CurrentTime),
    (NEWID(), 'INSPECT', 'Inspection', 'Equipment inspection', 1, @TenantId, @CurrentTime),
    (NEWID(), 'CALIB', 'Calibration', 'Equipment calibration', 1, @TenantId, @CurrentTime),
    (NEWID(), 'UPGRADE', 'Upgrade', 'Equipment upgrade', 1, @TenantId, @CurrentTime);

-- Asset Categories
INSERT INTO MaintenanceAssetCategories (Id, Code, Name, Description, IsActive, TenantId, CreatedAt)
VALUES 
    (NEWID(), 'HVAC', 'HVAC Systems', 'Heating, Ventilation, and Air Conditioning', 1, @TenantId, @CurrentTime),
    (NEWID(), 'ELEC', 'Electrical', 'Electrical systems and equipment', 1, @TenantId, @CurrentTime),
    (NEWID(), 'PLUMB', 'Plumbing', 'Plumbing and water systems', 1, @TenantId, @CurrentTime),
    (NEWID(), 'MECH', 'Mechanical', 'Mechanical equipment', 1, @TenantId, @CurrentTime),
    (NEWID(), 'IT', 'IT Equipment', 'Information technology equipment', 1, @TenantId, @CurrentTime),
    (NEWID(), 'SAFETY', 'Safety Systems', 'Safety and security systems', 1, @TenantId, @CurrentTime),
    (NEWID(), 'VEHICLE', 'Vehicles', 'Company vehicles and transportation', 1, @TenantId, @CurrentTime),
    (NEWID(), 'FACILITY', 'Facility', 'Building and facility infrastructure', 1, @TenantId, @CurrentTime);

-- Asset Types
INSERT INTO AssetTypes (Id, Code, Name, Description, IsActive, TenantId, CreatedAt)
VALUES 
    (NEWID(), 'AC_UNIT', 'Air Conditioning Unit', 'HVAC air conditioning equipment', 1, @TenantId, @CurrentTime),
    (NEWID(), 'GENERATOR', 'Generator', 'Backup power generator', 1, @TenantId, @CurrentTime),
    (NEWID(), 'PUMP', 'Water Pump', 'Water circulation pump', 1, @TenantId, @CurrentTime),
    (NEWID(), 'MOTOR', 'Electric Motor', 'Electric motor equipment', 1, @TenantId, @CurrentTime),
    (NEWID(), 'SERVER', 'Server', 'IT server equipment', 1, @TenantId, @CurrentTime),
    (NEWID(), 'ELEVATOR', 'Elevator', 'Passenger/freight elevator', 1, @TenantId, @CurrentTime),
    (NEWID(), 'COMPRESSOR', 'Compressor', 'Air/gas compressor', 1, @TenantId, @CurrentTime),
    (NEWID(), 'VAN', 'Service Van', 'Maintenance service vehicle', 1, @TenantId, @CurrentTime);

-- ================================
-- 2. TEAM AND TECHNICIAN DATA
-- ================================

-- Technician Shifts
INSERT INTO TechnicianShifts (Id, Name, Description, StartTime, EndTime, IsActive, TenantId, CreatedAt)
VALUES 
    (NEWID(), 'Day Shift', 'Regular day shift', '08:00:00', '17:00:00', 1, @TenantId, @CurrentTime),
    (NEWID(), 'Evening Shift', 'Evening maintenance shift', '16:00:00', '00:00:00', 1, @TenantId, @CurrentTime),
    (NEWID(), 'Night Shift', 'Night maintenance shift', '23:00:00', '07:00:00', 1, @TenantId, @CurrentTime),
    (NEWID(), 'On-Call', 'On-call emergency shift', '00:00:00', '23:59:59', 1, @TenantId, @CurrentTime);

-- Technical Skills
INSERT INTO TechnicalSkills (Id, Code, Name, Description, Category, SkillLevel, Complexity, RiskLevel, IsActive, TenantId, CreatedAt)
VALUES 
    (NEWID(), 'ELEC_BASIC', 'Basic Electrical', 'Basic electrical maintenance skills', 'Electrical', 'Beginner', 'Low', 'Medium', 1, @TenantId, @CurrentTime),
    (NEWID(), 'ELEC_ADV', 'Advanced Electrical', 'Advanced electrical system maintenance', 'Electrical', 'Expert', 'High', 'High', 1, @TenantId, @CurrentTime),
    (NEWID(), 'HVAC_BASIC', 'Basic HVAC', 'Basic HVAC system maintenance', 'HVAC', 'Intermediate', 'Medium', 'Medium', 1, @TenantId, @CurrentTime),
    (NEWID(), 'HVAC_ADV', 'Advanced HVAC', 'Advanced HVAC system repair', 'HVAC', 'Expert', 'High', 'Medium', 1, @TenantId, @CurrentTime),
    (NEWID(), 'PLUMB_BASIC', 'Basic Plumbing', 'Basic plumbing repairs', 'Plumbing', 'Beginner', 'Low', 'Low', 1, @TenantId, @CurrentTime),
    (NEWID(), 'MECH_REPAIR', 'Mechanical Repair', 'General mechanical equipment repair', 'Mechanical', 'Intermediate', 'Medium', 'Medium', 1, @TenantId, @CurrentTime),
    (NEWID(), 'WELD_BASIC', 'Basic Welding', 'Basic welding and fabrication', 'Welding', 'Intermediate', 'Medium', 'High', 1, @TenantId, @CurrentTime),
    (NEWID(), 'IT_SUPPORT', 'IT Support', 'Basic IT equipment maintenance', 'IT', 'Beginner', 'Low', 'Low', 1, @TenantId, @CurrentTime);

-- Technician Skills (Legacy)
INSERT INTO TechnicianSkills (Id, Name, Description, Category, IsActive, TenantId, CreatedAt)
VALUES 
    (NEWID(), 'Electrical Troubleshooting', 'Electrical system diagnostics and repair', 'Electrical', 1, @TenantId, @CurrentTime),
    (NEWID(), 'HVAC Maintenance', 'Heating, ventilation, and air conditioning', 'HVAC', 1, @TenantId, @CurrentTime),
    (NEWID(), 'Plumbing Repair', 'Water and sewage system maintenance', 'Plumbing', 1, @TenantId, @CurrentTime),
    (NEWID(), 'Mechanical Systems', 'General mechanical equipment maintenance', 'Mechanical', 1, @TenantId, @CurrentTime),
    (NEWID(), 'Safety Protocols', 'Workplace safety and compliance', 'Safety', 1, @TenantId, @CurrentTime),
    (NEWID(), 'Preventive Maintenance', 'Scheduled maintenance procedures', 'General', 1, @TenantId, @CurrentTime);

-- Technician Teams
INSERT INTO TechnicianTeams (Id, Name, Description, TeamLeaderId, Status, TenantId, CreatedAt)
VALUES 
    (NEWID(), 'HVAC Team', 'Heating, Ventilation & Air Conditioning specialists', NULL, 'Active', @TenantId, @CurrentTime),
    (NEWID(), 'Electrical Team', 'Electrical systems maintenance team', NULL, 'Active', @TenantId, @CurrentTime),
    (NEWID(), 'General Maintenance', 'General facility maintenance team', NULL, 'Active', @TenantId, @CurrentTime),
    (NEWID(), 'Emergency Response', '24/7 emergency maintenance team', NULL, 'Active', @TenantId, @CurrentTime),
    (NEWID(), 'Preventive Maintenance', 'Scheduled preventive maintenance team', NULL, 'Active', @TenantId, @CurrentTime);

-- ================================
-- 3. SAFETY AND PROTOCOLS
-- ================================

-- Safety Protocols
INSERT INTO SafetyProtocols (Id, Code, Name, Description, Category, Severity, RegulatoryStandard, Procedures, RequiredEquipment, RequiredTraining, RequiredCertifications, EmergencyProcedures, PreventiveMeasures, ApplicableMaintenanceTypes, ApplicableAssetTypes, IsMandatory, IsActive, ReviewFrequencyMonths, MinimumTrainingLevel, EffectiveDate, ApprovalStatus, Version, TenantId, CreatedAt)
VALUES 
    (NEWID(), 'LOTO', 'Lockout/Tagout', 'Energy isolation safety procedure', 'Energy Control', 'High', 'OSHA 29 CFR 1910.147', 'Proper lockout/tagout procedures for energy isolation', '["Locks", "Tags", "Hasps", "Circuit Breakers"]', '["LOTO Training", "Energy Control Training"]', '["LOTO Certification"]', 'Contact supervisor immediately if issues arise', 'Always verify zero energy state before work', '["PM", "CM", "EM"]', '["ELEC", "MECH"]', 1, 1, 12, 'Certified', @CurrentTime, 'Approved', '1.0', @TenantId, @CurrentTime),
    
    (NEWID(), 'PPE_ELEC', 'Electrical PPE', 'Personal protective equipment for electrical work', 'Personal Protection', 'High', 'OSHA 29 CFR 1910.132', 'Proper selection and use of electrical PPE', '["Hard Hat", "Safety Glasses", "Insulated Gloves", "Arc Flash Suit"]', '["PPE Training", "Electrical Safety Training"]', '["Electrical Safety Certification"]', 'Remove from energized area immediately', 'Inspect PPE before each use', '["CM", "EM", "IN"]', '["ELEC"]', 1, 1, 6, 'Certified', @CurrentTime, 'Approved', '1.0', @TenantId, @CurrentTime),
    
    (NEWID(), 'CONF_SPACE', 'Confined Space Entry', 'Safe entry procedures for confined spaces', 'Confined Space', 'Critical', 'OSHA 29 CFR 1910.146', 'Atmospheric testing and entry procedures', '["Gas Monitor", "Ventilation Fan", "Harness", "Tripod"]', '["Confined Space Training", "Atmospheric Testing Training"]', '["Confined Space Entry Permit"]', 'Evacuate immediately if atmospheric conditions change', 'Test atmosphere every 10 minutes during work', '["PM", "CM", "IN"]', '["FACILITY", "MECH"]', 1, 1, 12, 'Advanced', @CurrentTime, 'Approved', '1.0', @TenantId, @CurrentTime),
    
    (NEWID(), 'WORK_HEIGHT', 'Working at Height', 'Fall protection and ladder safety', 'Fall Protection', 'High', 'OSHA 29 CFR 1910.23', 'Fall protection system usage and ladder safety', '["Safety Harness", "Lanyard", "Hard Hat", "Safety Shoes"]', '["Fall Protection Training", "Ladder Safety Training"]', '["Height Work Certification"]', 'Call for rescue team if worker is suspended', 'Inspect fall protection equipment daily', '["PM", "CM", "IN"]', '["HVAC", "ELEC", "FACILITY"]', 1, 1, 12, 'Certified', @CurrentTime, 'Approved', '1.0', @TenantId, @CurrentTime),
    
    (NEWID(), 'HOT_WORK', 'Hot Work Permit', 'Fire safety for welding and cutting operations', 'Fire Safety', 'High', 'NFPA 51B', 'Hot work permit and fire watch procedures', '["Fire Extinguisher", "Fire Watch", "Welding Screens", "Fire Blankets"]', '["Hot Work Training", "Fire Safety Training"]', '["Hot Work Permit"]', 'Activate fire alarm and evacuate area', 'Clear combustible materials 35 feet from work area', '["CM", "UP"]', '["MECH", "FACILITY"]', 1, 1, 12, 'Certified', @CurrentTime, 'Approved', '1.0', @TenantId, @CurrentTime);

-- ================================
-- 4. CONTRACTOR MANAGEMENT
-- ================================

-- Maintenance Contractors
INSERT INTO MaintenanceContractors (Id, Name, ContractorCode, Description, ContactInfo, Capabilities, ServiceAreas, Status, Rating, LicenseInfo, InsuranceInfo, CreatedDate, CreatedById, TenantId)
VALUES 
    (NEWID(), 'Elite HVAC Services', 'HVAC001', 'Professional HVAC installation and maintenance', '{"phone": "555-0101", "email": "contact@elitehvac.com", "address": "123 Industrial Ave"}', '["HVAC Installation", "Preventive Maintenance", "Emergency Repair", "System Upgrades"]', '["Downtown", "Industrial District", "North Campus"]', 'Active', 4.8, '{"hvac_license": "HVAC-2024-001", "contractor_license": "CON-2024-567"}', '{"liability": "2000000", "workers_comp": "1000000", "expiry": "2024-12-31"}', @CurrentTime, @UserId, @TenantId),
    
    (NEWID(), 'PowerTech Electrical', 'ELEC001', 'Licensed electrical contractors', '{"phone": "555-0102", "email": "service@powertech.com", "address": "456 Electric Blvd"}', '["Electrical Installation", "Troubleshooting", "Panel Upgrades", "Emergency Services"]', '["All Areas"]', 'Active', 4.6, '{"electrical_license": "E-2024-789", "master_electrician": "ME-2024-123"}', '{"liability": "3000000", "workers_comp": "1500000", "expiry": "2024-12-31"}', @CurrentTime, @UserId, @TenantId),
    
    (NEWID(), 'Reliable Plumbing Co', 'PLUMB001', 'Commercial plumbing specialists', '{"phone": "555-0103", "email": "info@reliableplumbing.com", "address": "789 Water St"}', '["Plumbing Repair", "Pipe Installation", "Water Heater Service", "Drain Cleaning"]', '["Main Campus", "South Building"]', 'Active', 4.2, '{"plumbing_license": "P-2024-456", "backflow_cert": "BF-2024-789"}', '{"liability": "1000000", "workers_comp": "500000", "expiry": "2024-12-31"}', @CurrentTime, @UserId, @TenantId),
    
    (NEWID(), 'MechPro Services', 'MECH001', 'Mechanical equipment specialists', '{"phone": "555-0104", "email": "support@mechpro.com", "address": "321 Machinery Rd"}', '["Pump Repair", "Motor Maintenance", "Belt Replacement", "Bearing Service"]', '["Industrial Zone"]', 'Active', 4.5, '{"mechanical_license": "M-2024-321", "millwright_cert": "MW-2024-654"}', '{"liability": "2500000", "workers_comp": "1200000", "expiry": "2024-12-31"}', @CurrentTime, @UserId, @TenantId);

-- ================================
-- 5. TOOL AND RESOURCE MANAGEMENT
-- ================================

-- Maintenance Tools
INSERT INTO MaintenanceTools (Id, ToolCode, Name, Description, Category, Manufacturer, Model, SerialNumber, Status, CurrentLocation, HomeLocation, LastMaintenanceDate, NextMaintenanceDate, PurchasePrice, CurrentValue, DailyRentalRate, TotalUsageDays, RequiresCertification, RequiresTraining, SafetyNotes, IsActive, TenantId, CreatedAt)
VALUES 
    (NEWID(), 'DRILL001', 'Heavy Duty Drill', 'Cordless heavy duty drill with bits', 'Power Tools', 'DeWalt', 'DCD771C2', 'DW2024001', 'Available', 'Tool Room A', 'Tool Room A', DATEADD(day, -30, @CurrentTime), DATEADD(day, 60, @CurrentTime), 150.00, 120.00, 25.00, 45, 0, 1, 'Check battery level before use', 1, @TenantId, @CurrentTime),
    
    (NEWID(), 'METER001', 'Digital Multimeter', 'Professional electrical testing meter', 'Electrical', 'Fluke', '87V', 'FL2024001', 'Available', 'Electrical Shop', 'Electrical Shop', DATEADD(day, -15, @CurrentTime), DATEADD(day, 75, @CurrentTime), 350.00, 300.00, 50.00, 120, 1, 1, 'Verify calibration before use', 1, @TenantId, @CurrentTime),
    
    (NEWID(), 'TORCH001', 'Welding Torch', 'Oxy-acetylene welding torch set', 'Welding', 'Lincoln', 'LT-2000', 'LN2024001', 'Available', 'Welding Shop', 'Welding Shop', DATEADD(day, -45, @CurrentTime), DATEADD(day, 45, @CurrentTime), 450.00, 350.00, 75.00, 80, 1, 1, 'Check gas connections and hose condition', 1, @TenantId, @CurrentTime),
    
    (NEWID(), 'LIFT001', 'Scissor Lift', 'Electric scissor lift for height access', 'Lifting', 'Genie', 'GS-1930', 'GE2024001', 'Available', 'Equipment Yard', 'Equipment Yard', DATEADD(day, -60, @CurrentTime), DATEADD(day, 30, @CurrentTime), 8500.00, 7500.00, 150.00, 200, 1, 1, 'Inspect platform and safety railings', 1, @TenantId, @CurrentTime),
    
    (NEWID(), 'PUMP001', 'Test Pump', 'Hydrostatic pressure test pump', 'Testing', 'Reed', 'TP-1000', 'RD2024001', 'Available', 'Testing Lab', 'Testing Lab', DATEADD(day, -20, @CurrentTime), DATEADD(day, 70, @CurrentTime), 1200.00, 1000.00, 100.00, 35, 0, 1, 'Verify pressure relief valve operation', 1, @TenantId, @CurrentTime);

-- Maintenance Vehicles
INSERT INTO MaintenanceVehicles (Id, VehicleCode, Name, VehicleType, LicensePlate, Make, Model, Year, VIN, Status, CurrentLocation, HomeBase, CurrentMileage, LastServiceDate, NextServiceDate, FuelCapacity, FuelLevel, AverageFuelConsumption, InsuranceExpiry, RegistrationExpiry, InspectionExpiry, IsActive, Notes, TenantId, CreatedAt)
VALUES 
    (NEWID(), 'VAN001', 'Maintenance Van #1', 'Van', 'MNT-001', 'Ford', 'Transit', 2022, '1FTBW2CM6NKA12345', 'Available', 'Main Garage', 'Main Garage', 25430, DATEADD(day, -30, @CurrentTime), DATEADD(day, 60, @CurrentTime), 60.0, 45.0, 18.5, DATEADD(day, 180, @CurrentTime), DATEADD(day, 200, @CurrentTime), DATEADD(day, 150, @CurrentTime), 1, 'Equipped with basic tools and ladder', @TenantId, @CurrentTime),
    
    (NEWID(), 'TRUCK001', 'Service Truck', 'Truck', 'SRV-001', 'Chevrolet', 'Silverado 2500', 2021, '1GC4YREY5MF123456', 'Available', 'Main Garage', 'Main Garage', 42150, DATEADD(day, -45, @CurrentTime), DATEADD(day, 45, @CurrentTime), 75.0, 60.0, 15.2, DATEADD(day, 190, @CurrentTime), DATEADD(day, 220, @CurrentTime), DATEADD(day, 160, @CurrentTime), 1, 'Heavy duty truck for equipment transport', @TenantId, @CurrentTime);

-- ================================
-- 6. ASSET MANAGEMENT
-- ================================

-- Get some IDs for foreign keys
DECLARE @HvacCategoryId UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM MaintenanceAssetCategories WHERE Code = 'HVAC');
DECLARE @ElecCategoryId UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM MaintenanceAssetCategories WHERE Code = 'ELEC');
DECLARE @PlumbCategoryId UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM MaintenanceAssetCategories WHERE Code = 'PLUMB');
DECLARE @MechCategoryId UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM MaintenanceAssetCategories WHERE Code = 'MECH');
DECLARE @FacilityCategoryId UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM MaintenanceAssetCategories WHERE Code = 'FACILITY');

DECLARE @AcUnitTypeId UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM AssetTypes WHERE Code = 'AC_UNIT');
DECLARE @GeneratorTypeId UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM AssetTypes WHERE Code = 'GENERATOR');
DECLARE @PumpTypeId UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM AssetTypes WHERE Code = 'PUMP');
DECLARE @MotorTypeId UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM AssetTypes WHERE Code = 'MOTOR');
DECLARE @ElevatorTypeId UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM AssetTypes WHERE Code = 'ELEVATOR');

-- Maintenance Assets
INSERT INTO MaintenanceAssets (Id, AssetNumber, Name, Description, AssetCategoryId, AssetTypeId, SerialNumber, Model, Manufacturer, PurchaseDate, WarrantyExpiration, Status, Criticality, Location, InstallationDate, LastMaintenanceDate, NextMaintenanceDate, IsDeleted, TenantId, CreatedAt)
VALUES 
    (NEWID(), 'HVAC-001', 'Rooftop AC Unit #1', 'Main building rooftop air conditioning unit', @HvacCategoryId, @AcUnitTypeId, 'RTU2024001', 'RTU-50T', 'Carrier', DATEADD(year, -2, @CurrentTime), DATEADD(year, 3, @CurrentTime), 'Active', 'High', 'Building A - Rooftop', DATEADD(year, -2, @CurrentTime), DATEADD(day, -15, @CurrentTime), DATEADD(day, 75, @CurrentTime), 0, @TenantId, @CurrentTime),
    
    (NEWID(), 'HVAC-002', 'Rooftop AC Unit #2', 'Secondary rooftop air conditioning unit', @HvacCategoryId, @AcUnitTypeId, 'RTU2024002', 'RTU-50T', 'Carrier', DATEADD(year, -2, @CurrentTime), DATEADD(year, 3, @CurrentTime), 'Active', 'High', 'Building B - Rooftop', DATEADD(year, -2, @CurrentTime), DATEADD(day, -30, @CurrentTime), DATEADD(day, 60, @CurrentTime), 0, @TenantId, @CurrentTime),
    
    (NEWID(), 'GEN-001', 'Emergency Generator', 'Backup power generator for critical systems', @ElecCategoryId, @GeneratorTypeId, 'GEN2023001', 'DG-100K', 'Caterpillar', DATEADD(year, -3, @CurrentTime), DATEADD(year, 2, @CurrentTime), 'Active', 'Critical', 'Generator Room', DATEADD(year, -3, @CurrentTime), DATEADD(day, -7, @CurrentTime), DATEADD(day, 83, @CurrentTime), 0, @TenantId, @CurrentTime),
    
    (NEWID(), 'PUMP-001', 'Chilled Water Pump #1', 'Primary chilled water circulation pump', @MechCategoryId, @PumpTypeId, 'CWP2023001', 'CP-500', 'Grundfos', DATEADD(year, -1, @CurrentTime), DATEADD(year, 4, @CurrentTime), 'Active', 'High', 'Mechanical Room', DATEADD(year, -1, @CurrentTime), DATEADD(day, -20, @CurrentTime), DATEADD(day, 70, @CurrentTime), 0, @TenantId, @CurrentTime),
    
    (NEWID(), 'PUMP-002', 'Chilled Water Pump #2', 'Secondary chilled water circulation pump', @MechCategoryId, @PumpTypeId, 'CWP2023002', 'CP-500', 'Grundfos', DATEADD(year, -1, @CurrentTime), DATEADD(year, 4, @CurrentTime), 'Standby', 'High', 'Mechanical Room', DATEADD(year, -1, @CurrentTime), DATEADD(day, -45, @CurrentTime), DATEADD(day, 45, @CurrentTime), 0, @TenantId, @CurrentTime),
    
    (NEWID(), 'MOTOR-001', 'Exhaust Fan Motor', 'Kitchen exhaust fan motor', @ElecCategoryId, @MotorTypeId, 'EFM2022001', 'M-75HP', 'Baldor', DATEADD(year, -2, @CurrentTime), DATEADD(year, 3, @CurrentTime), 'Active', 'Medium', 'Kitchen - Roof', DATEADD(year, -2, @CurrentTime), DATEADD(day, -10, @CurrentTime), DATEADD(day, 80, @CurrentTime), 0, @TenantId, @CurrentTime),
    
    (NEWID(), 'ELEV-001', 'Passenger Elevator #1', 'Main building passenger elevator', @FacilityCategoryId, @ElevatorTypeId, 'ELEV2020001', 'E-2000', 'Otis', DATEADD(year, -4, @CurrentTime), DATEADD(year, 1, @CurrentTime), 'Active', 'Critical', 'Building A - Lobby', DATEADD(year, -4, @CurrentTime), DATEADD(day, -25, @CurrentTime), DATEADD(day, 65, @CurrentTime), 0, @TenantId, @CurrentTime),
    
    (NEWID(), 'ELEV-002', 'Freight Elevator', 'Service and freight elevator', @FacilityCategoryId, @ElevatorTypeId, 'ELEV2021001', 'F-3000', 'Otis', DATEADD(year, -3, @CurrentTime), DATEADD(year, 2, @CurrentTime), 'Active', 'High', 'Building A - Service Area', DATEADD(year, -3, @CurrentTime), DATEADD(day, -35, @CurrentTime), DATEADD(day, 55, @CurrentTime), 0, @TenantId, @CurrentTime);

-- ================================
-- 7. INSPECTION TEMPLATES AND CHECKLISTS
-- ================================

-- Inspection Templates
INSERT INTO InspectionTemplates (Id, Name, Description, Category, InspectionType, IsActive, TenantId, CreatedAt)
VALUES 
    (NEWID(), 'HVAC Monthly Inspection', 'Monthly HVAC system inspection checklist', 'HVAC', 'Preventive', 1, @TenantId, @CurrentTime),
    (NEWID(), 'Electrical Safety Inspection', 'Quarterly electrical system safety inspection', 'Electrical', 'Safety', 1, @TenantId, @CurrentTime),
    (NEWID(), 'Generator Load Test', 'Monthly generator load test and inspection', 'Generator', 'Performance', 1, @TenantId, @CurrentTime),
    (NEWID(), 'Elevator Safety Inspection', 'Monthly elevator safety inspection', 'Elevator', 'Safety', 1, @TenantId, @CurrentTime),
    (NEWID(), 'Pump Performance Check', 'Quarterly pump performance inspection', 'Mechanical', 'Performance', 1, @TenantId, @CurrentTime);

-- Quality Control Checklists
INSERT INTO QualityControlChecklists (Id, Name, Description, WorkOrderType, AssetCategory, MaintenanceType, IsMandatory, IsActive, ChecklistItems, MinimumPassingScore, Version, CreatedDate, CreatedById, TenantId)
VALUES 
    (NEWID(), 'HVAC Repair Quality Check', 'Quality checklist for HVAC repair work', 'REPAIR', 'HVAC', 'CM', 1, 1, '[{"item": "System operates within specified parameters", "weight": 20}, {"item": "All connections are secure", "weight": 15}, {"item": "No refrigerant leaks detected", "weight": 20}, {"item": "Electrical connections are proper", "weight": 15}, {"item": "System controls function correctly", "weight": 20}, {"item": "Area is clean and organized", "weight": 10}]', 85, 1, @CurrentTime, @UserId, @TenantId),
    
    (NEWID(), 'Electrical Work Safety Check', 'Safety quality check for electrical work', 'REPAIR', 'ELEC', 'CM', 1, 1, '[{"item": "Proper lockout/tagout procedures followed", "weight": 25}, {"item": "All electrical connections are secure", "weight": 20}, {"item": "Ground fault protection verified", "weight": 20}, {"item": "Circuit labeling is accurate", "weight": 15}, {"item": "No exposed conductors", "weight": 20}]', 90, 1, @CurrentTime, @UserId, @TenantId),
    
    (NEWID(), 'General Maintenance Quality Check', 'Standard quality checklist for general maintenance', 'MAINT', 'General', 'PM', 1, 1, '[{"item": "Work completed per specifications", "weight": 25}, {"item": "All safety protocols followed", "weight": 20}, {"item": "Equipment operates properly", "weight": 20}, {"item": "Work area clean and organized", "weight": 15}, {"item": "Documentation complete", "weight": 20}]', 80, 1, @CurrentTime, @UserId, @TenantId);

-- ================================
-- 8. WORK ORDERS AND JOB CARDS
-- ================================

-- Get some more IDs for work orders
DECLARE @Asset1Id UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM MaintenanceAssets WHERE AssetNumber = 'HVAC-001');
DECLARE @Asset2Id UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM MaintenanceAssets WHERE AssetNumber = 'GEN-001');
DECLARE @Asset3Id UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM MaintenanceAssets WHERE AssetNumber = 'PUMP-001');

DECLARE @PMTypeId UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM MaintenanceTypes WHERE Code = 'PM');
DECLARE @CMTypeId UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM MaintenanceTypes WHERE Code = 'CM');
DECLARE @EMTypeId UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM MaintenanceTypes WHERE Code = 'EM');

DECLARE @MaintWOTypeId UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM WorkOrderTypes WHERE Code = 'MAINT');
DECLARE @RepairWOTypeId UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM WorkOrderTypes WHERE Code = 'REPAIR');
DECLARE @InspectWOTypeId UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM WorkOrderTypes WHERE Code = 'INSPECT');

DECLARE @HighPriorityId UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM PriorityLevels WHERE Level = 2);
DECLARE @MediumPriorityId UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM PriorityLevels WHERE Level = 3);
DECLARE @LowPriorityId UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM PriorityLevels WHERE Level = 4);

-- Work Orders
INSERT INTO WorkOrders (Id, WorkOrderNumber, AssetId, WorkOrderTypeId, MaintenanceTypeId, PriorityLevelId, Title, Description, Status, RequestedById, RequestedStartDate, RequestedCompletionDate, EstimatedHours, EstimatedCost, IsDeleted, TenantId, CreatedAt)
VALUES 
    (NEWID(), 'WO-2024-001', @Asset1Id, @MaintWOTypeId, @PMTypeId, @MediumPriorityId, 'HVAC Unit #1 - Quarterly Maintenance', 'Quarterly preventive maintenance on rooftop AC unit including filter replacement, coil cleaning, and system inspection', 'Open', @UserId, @CurrentTime, DATEADD(day, 7, @CurrentTime), 4.0, 350.00, 0, @TenantId, @CurrentTime),
    
    (NEWID(), 'WO-2024-002', @Asset2Id, @InspectWOTypeId, @PMTypeId, @HighPriorityId, 'Generator Monthly Load Test', 'Monthly load test and inspection of emergency generator system', 'In Progress', @UserId, DATEADD(day, -2, @CurrentTime), DATEADD(day, 3, @CurrentTime), 2.5, 200.00, 0, @TenantId, @CurrentTime),
    
    (NEWID(), 'WO-2024-003', @Asset3Id, @RepairWOTypeId, @CMTypeId, @HighPriorityId, 'Chilled Water Pump Bearing Replacement', 'Replace worn bearings on primary chilled water pump - unusual noise reported', 'Assigned', @UserId, DATEADD(day, 1, @CurrentTime), DATEADD(day, 5, @CurrentTime), 6.0, 850.00, 0, @TenantId, @CurrentTime);

-- Job Cards
DECLARE @WorkOrder1Id UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM WorkOrders WHERE WorkOrderNumber = 'WO-2024-001');

INSERT INTO JobCards (Id, JobCardNumber, AssetId, MaintenanceTypeId, PriorityLevelId, Title, Description, ProblemDescription, MaintenanceLocation, RequestedById, RequestedDate, RequiredCompletionDate, EstimatedHours, EstimatedCost, RequiresSpecialTools, RequiresShutdown, RequiresSafetyPermit, SpecialInstructions, SafetyRequirements, JobCardStatus, ApprovalStatus, GeneratedWorkOrderId, TenantId, CreatedAt)
VALUES 
    (NEWID(), 'JC-2024-001', @Asset1Id, @PMTypeId, @MediumPriorityId, 'HVAC-001 Preventive Maintenance', 'Quarterly preventive maintenance request for HVAC-001', 'Routine quarterly maintenance - no specific issues reported', 'Internal', @UserId, DATEADD(day, -3, @CurrentTime), DATEADD(day, 7, @CurrentTime), 4.0, 350.00, 0, 1, 0, 'Coordinate with building occupants for system shutdown', 'Follow HVAC safety protocols, ensure proper ventilation', 'Approved', 'Approved', @WorkOrder1Id, @TenantId, @CurrentTime),
    
    (NEWID(), 'JC-2024-002', @Asset2Id, @PMTypeId, @HighPriorityId, 'Generator Load Test Request', 'Monthly load test for emergency generator', 'Monthly requirement - generator has been running normally', 'Internal', @UserId, DATEADD(day, -1, @CurrentTime), DATEADD(day, 5, @CurrentTime), 2.5, 200.00, 1, 0, 1, 'Notify facilities management before test', 'Ensure proper ventilation in generator room', 'Submitted', 'Under Review', NULL, @TenantId, @CurrentTime),
    
    (NEWID(), 'JC-2024-003', @Asset3Id, @CMTypeId, @HighPriorityId, 'Pump Bearing Replacement', 'Emergency repair - unusual noise from pump', 'Grinding noise from pump bearings, vibration increasing', 'Internal', @UserId, @CurrentTime, DATEADD(day, 3, @CurrentTime), 6.0, 850.00, 1, 1, 0, 'Order replacement bearings part #CWP-BRG-001', 'Lock out pump electrical supply, drain system', 'Draft', 'Draft', NULL, @TenantId, @CurrentTime);

-- ================================
-- 9. MAINTENANCE SCHEDULES
-- ================================

-- Maintenance Schedules
INSERT INTO MaintenanceSchedules (Id, Name, AssetId, MaintenanceTypeId, PriorityLevelId, ScheduleType, Frequency, FrequencyType, IsActive, NextDueDate, EstimatedDuration, EstimatedCost, Instructions, TenantId, CreatedAt)
VALUES 
    (NEWID(), 'HVAC-001 Quarterly Maintenance', @Asset1Id, @PMTypeId, @MediumPriorityId, 'Recurring', 3, 'Months', 1, DATEADD(day, 75, @CurrentTime), 4.0, 350.00, 'Replace filters, clean coils, check refrigerant levels, inspect electrical connections', @TenantId, @CurrentTime),
    
    (NEWID(), 'Generator Monthly Load Test', @Asset2Id, @PMTypeId, @HighPriorityId, 'Recurring', 1, 'Months', 1, DATEADD(day, 25, @CurrentTime), 2.5, 200.00, 'Perform 30-minute load test at 75% capacity, check all systems', @TenantId, @CurrentTime),
    
    (NEWID(), 'Pump Annual Overhaul', @Asset3Id, @PMTypeId, @MediumPriorityId, 'Recurring', 12, 'Months', 1, DATEADD(day, 300, @CurrentTime), 16.0, 2500.00, 'Complete pump disassembly, bearing replacement, impeller inspection, performance testing', @TenantId, @CurrentTime);

-- ================================
-- 10. ADDITIONAL RELATED ENTITIES
-- ================================

-- Work Order Comments
DECLARE @WO1Id UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM WorkOrders WHERE WorkOrderNumber = 'WO-2024-001');
DECLARE @WO2Id UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM WorkOrders WHERE WorkOrderNumber = 'WO-2024-002');

INSERT INTO WorkOrderComments (Id, WorkOrderId, EmployeeId, Comment, CommentType, CreatedAt, TenantId, IsDeleted)
VALUES 
    (NEWID(), @WO1Id, @UserId, 'Work order created from approved job card JC-2024-001', 'System', @CurrentTime, @TenantId, 0),
    (NEWID(), @WO2Id, @UserId, 'Load test scheduled for this weekend to minimize impact', 'General', DATEADD(hour, -2, @CurrentTime), @TenantId, 0),
    (NEWID(), @WO2Id, @UserId, 'Generator room ventilation fan tested - operating normally', 'Technical', DATEADD(hour, -1, @CurrentTime), @TenantId, 0);

-- Job Card Comments
DECLARE @JC1Id UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM JobCards WHERE JobCardNumber = 'JC-2024-001');
DECLARE @JC2Id UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM JobCards WHERE JobCardNumber = 'JC-2024-002');

INSERT INTO JobCardComments (Id, JobCardId, CommentById, Comment, CommentType, IsInternal, CommentDate, TenantId, CreatedAt, IsDeleted)
VALUES 
    (NEWID(), @JC1Id, @UserId, 'Job card approved - creating work order', 'Approval', 1, @CurrentTime, @TenantId, @CurrentTime, 0),
    (NEWID(), @JC2Id, @UserId, 'Reviewing maintenance schedule and resource availability', 'General', 1, DATEADD(hour, -3, @CurrentTime), @TenantId, DATEADD(hour, -3, @CurrentTime), 0);

-- Technician Availability
INSERT INTO TechnicianAvailabilities (Id, TechnicianId, StartDate, EndDate, AvailabilityType, Reason, TenantId, CreatedAt, IsDeleted)
VALUES 
    (NEWID(), @UserId, @CurrentTime, DATEADD(day, 30, @CurrentTime), 'Available', 'Regular work schedule', @TenantId, @CurrentTime, 0),
    (NEWID(), @UserId, DATEADD(day, 5, @CurrentTime), DATEADD(day, 7, @CurrentTime), 'Leave', 'Annual leave - family vacation', @TenantId, @CurrentTime, 0);

-- Asset Attachments
INSERT INTO MaintenanceAttachments (Id, EntityType, EntityId, FileName, FilePath, FileSize, ContentType, Category, Description, UploadedById, UploadedAt, TenantId, CreatedAt, IsDeleted)
VALUES 
    (NEWID(), 'Asset', @Asset1Id, 'HVAC001_Manual.pdf', '/uploads/maintenance/HVAC001_Manual.pdf', 2540000, 'application/pdf', 'Manual', 'Equipment operation and maintenance manual', @UserId, @CurrentTime, @TenantId, @CurrentTime, 0),
    (NEWID(), 'Asset', @Asset1Id, 'HVAC001_WarrantyCard.pdf', '/uploads/maintenance/HVAC001_WarrantyCard.pdf', 180000, 'application/pdf', 'Warranty', 'Manufacturer warranty documentation', @UserId, @CurrentTime, @TenantId, @CurrentTime, 0),
    (NEWID(), 'Asset', @Asset2Id, 'Generator_InstallPhoto.jpg', '/uploads/maintenance/Generator_InstallPhoto.jpg', 980000, 'image/jpeg', 'Photo', 'Generator installation photo', @UserId, DATEADD(year, -3, @CurrentTime), @TenantId, DATEADD(year, -3, @CurrentTime), 0);

-- Maintenance Expenses
INSERT INTO MaintenanceExpenses (Id, WorkOrderId, TechnicianId, ExpenseType, Description, Amount, ExpenseDate, MileageDriven, MileageRate, ReceiptPath, VendorName, Status, IsReimbursable, TenantId, CreatedAt, IsDeleted)
VALUES 
    (NEWID(), @WO1Id, @UserId, 'Parts', 'HVAC filters and cleaning supplies', 125.50, @CurrentTime, NULL, NULL, '/receipts/2024/hvac_supplies_001.jpg', 'HVAC Supply Co', 'Approved', 1, @TenantId, @CurrentTime, 0),
    (NEWID(), @WO2Id, @UserId, 'Fuel', 'Generator fuel for load test', 45.75, DATEADD(day, -1, @CurrentTime), NULL, NULL, '/receipts/2024/fuel_001.jpg', 'Fuel Depot', 'Pending', 1, @TenantId, @CurrentTime, 0),
    (NEWID(), @WO1Id, @UserId, 'Travel', 'Mileage to/from rooftop equipment', 15.60, @CurrentTime, 20.8, 0.75, NULL, NULL, 'Approved', 1, @TenantId, @CurrentTime, 0);

PRINT 'Maintenance seed data inserted successfully!';
PRINT 'Tables seeded:';
PRINT '- Priority Levels: 5 records';
PRINT '- Maintenance Types: 6 records';
PRINT '- Work Order Types: 6 records';
PRINT '- Asset Categories: 8 records';
PRINT '- Asset Types: 8 records';
PRINT '- Technician Shifts: 4 records';
PRINT '- Technical Skills: 8 records';
PRINT '- Technician Skills: 6 records';
PRINT '- Technician Teams: 5 records';
PRINT '- Safety Protocols: 5 records';
PRINT '- Maintenance Contractors: 4 records';
PRINT '- Maintenance Tools: 5 records';
PRINT '- Maintenance Vehicles: 2 records';
PRINT '- Maintenance Assets: 8 records';
PRINT '- Inspection Templates: 5 records';
PRINT '- Quality Control Checklists: 3 records';
PRINT '- Work Orders: 3 records';
PRINT '- Job Cards: 3 records';
PRINT '- Maintenance Schedules: 3 records';
PRINT '- Work Order Comments: 3 records';
PRINT '- Job Card Comments: 2 records';
PRINT '- Technician Availability: 2 records';
PRINT '- Maintenance Attachments: 3 records';
PRINT '- Maintenance Expenses: 3 records';
PRINT '';
PRINT 'Total maintenance entities with seed data: 25+ categories';
PRINT 'Note: Additional entities like inspections, quality checks, rework, etc. will be generated as workflow progresses.';

COMMIT TRANSACTION;