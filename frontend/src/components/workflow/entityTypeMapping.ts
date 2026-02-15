import type { WorkflowEntityTypeInfo } from '@/types/workflow';

const normalizeKey = (value?: string | null) =>
  (value || '').replace(/[^a-z0-9]/gi, '').toLowerCase();

const isMatch = (candidate: WorkflowEntityTypeInfo, key: string) => {
  const normalized = normalizeKey(key);
  return (
    normalizeKey(candidate.name) === normalized ||
    normalizeKey(candidate.code) === normalized
  );
};

export const moduleEntityTypeMap: Record<string, string[]> = {
  // Maintenance module includes Fleet (vehicles/trips/inspections/defects) as well as core work management.
  maintenance: ['WorkOrder', 'JobCard', 'Asset', 'FleetTrip', 'FleetVehicle', 'FleetTripInspection', 'FleetDefect'],
  procurement: ['PurchaseOrder', 'PurchaseRequisition', 'Vendor'],
  inventory: ['Inventory', 'Asset'],
  hr: ['Employee'],
  helpdesk: ['EhcTicket'],
  projects: ['Project'],
  sales: ['Customer'],
  quality: ['Quality']
};

const toEntityCode = (name: string) => {
  if (!name.trim()) {
    return 'ENTITY';
  }
  return name
    .replace(/([a-z])([A-Z])/g, '$1_$2')
    .replace(/[^a-zA-Z0-9]+/g, '_')
    .replace(/_+/g, '_')
    .replace(/^_+|_+$/g, '')
    .toUpperCase() || 'ENTITY';
};

export const buildFallbackEntityTypes = (): WorkflowEntityTypeInfo[] => {
  const names = new Set<string>();
  Object.values(moduleEntityTypeMap).forEach((items) => {
    items.forEach((item) => names.add(item));
  });

  return Array.from(names)
    .sort((a, b) => a.localeCompare(b))
    .map((name, index) => ({
      id: name,
      code: toEntityCode(name),
      name,
      description: undefined,
      displayOrder: index,
      icon: undefined,
      colorCode: undefined,
      isActive: true
    }));
};

export const filterEntityTypesByModule = (
  entityTypes: WorkflowEntityTypeInfo[],
  moduleId?: string
) => {
  if (!moduleId || moduleId === 'all' || !moduleEntityTypeMap[moduleId]?.length)
  {
    return { items: entityTypes, fallback: false };
  }

  const keys = moduleEntityTypeMap[moduleId].map(normalizeKey);
  const filtered = entityTypes.filter((entityType) =>
    keys.some((key) => isMatch(entityType, key))
  );

  if (filtered.length === 0)
  {
    return { items: entityTypes, fallback: true };
  }

  return { items: filtered, fallback: false };
};

export const isEntityTypeInList = (
  entityType: string,
  entityTypes: WorkflowEntityTypeInfo[]
) =>
  entityTypes.some((item) =>
    normalizeKey(item.name) === normalizeKey(entityType) ||
    normalizeKey(item.code) === normalizeKey(entityType)
  );
