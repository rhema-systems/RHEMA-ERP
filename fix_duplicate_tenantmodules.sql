-- Check current TenantModules for default tenant
SELECT Id, ModuleName, TenantId, CreatedAt FROM TenantModules 
WHERE TenantId = '00000000-0000-0000-0000-000000000001'
ORDER BY ModuleName, CreatedAt;

-- If there are duplicates, remove the older ones (keep the ones from 2025-10-10)
-- Delete the older entries (from 2025-10-08)
DELETE FROM TenantModules
WHERE TenantId = '00000000-0000-0000-0000-000000000001'
AND CreatedAt < '2025-10-09T00:00:00Z'
AND ModuleName IN ('Finance', 'Sales', 'HR', 'Procurement', 'Marketing', 'Inventory', 'WorkflowEngine');

-- Verify remaining entries
SELECT Id, ModuleName, TenantId, CreatedAt FROM TenantModules 
WHERE TenantId = '00000000-0000-0000-0000-000000000001'
ORDER BY ModuleName, CreatedAt;
