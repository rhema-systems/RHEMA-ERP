/**
 * Entity Navigation Router
 * Maps entity types and IDs to corresponding application URLs
 * Enables smart navigation from notification clicks
 * 
 * To add new entities:
 * 1. Add the entity to the ENTITY_CONFIG below
 * 2. The system will automatically handle navigation, icons, and display names
 */

export type EntityType = string;

/**
 * Entity configuration - Add new entities here as they are developed
 */
interface EntityConfig {
  displayName: string;
  iconName: string;
  routePattern: string; // Use {id} as placeholder for entity ID
  module?: string; // Optional module grouping
}

const ENTITY_CONFIG: Record<string, EntityConfig> = {
  // Maintenance Module
  'JobCard': {
    displayName: 'Job Card',
    iconName: 'FileText',
    routePattern: '/maintenance/job-cards?id={id}',
    module: 'maintenance'
  },
  'WorkOrder': {
    displayName: 'Work Order', 
    iconName: 'Wrench',
    routePattern: '/maintenance/work-orders?id={id}',
    module: 'maintenance'
  },
  'Asset': {
    displayName: 'Asset',
    iconName: 'Package',
    routePattern: '/maintenance/assets?id={id}',
    module: 'maintenance'
  },
  'AssetAdmission': {
    displayName: 'Asset Admission',
    iconName: 'LogIn',
    routePattern: '/maintenance/asset-admission?id={id}',
    module: 'maintenance'
  },
  'AssetDischarge': {
    displayName: 'Asset Discharge',
    iconName: 'LogOut',
    routePattern: '/maintenance/asset-discharge?id={id}',
    module: 'maintenance'
  },
  'Quality': {
    displayName: 'Quality Control',
    iconName: 'Shield',
    routePattern: '/maintenance/quality-control?id={id}',
    module: 'maintenance'
  },
  
  // Financial Module (examples for future)
  'Invoice': {
    displayName: 'Invoice',
    iconName: 'FileText',
    routePattern: '/finance/invoices?id={id}',
    module: 'finance'
  },
  'Payment': {
    displayName: 'Payment',
    iconName: 'CreditCard',
    routePattern: '/finance/payments?id={id}',
    module: 'finance'
  },
  'PurchaseOrder': {
    displayName: 'Purchase Order',
    iconName: 'ShoppingCart',
    routePattern: '/procurement/purchase-orders?id={id}',
    module: 'procurement'
  },
  
  // CRM Module (examples for future)
  'Customer': {
    displayName: 'Customer',
    iconName: 'Users',
    routePattern: '/crm/customers?id={id}',
    module: 'crm'
  },
  'Lead': {
    displayName: 'Lead',
    iconName: 'Target',
    routePattern: '/crm/leads?id={id}',
    module: 'crm'
  },
  'Opportunity': {
    displayName: 'Opportunity',
    iconName: 'TrendingUp',
    routePattern: '/crm/opportunities?id={id}',
    module: 'crm'
  },
  
  // Inventory Module (examples for future)
  'Product': {
    displayName: 'Product',
    iconName: 'Box',
    routePattern: '/inventory/products?id={id}',
    module: 'inventory'
  },
  'StockMovement': {
    displayName: 'Stock Movement',
    iconName: 'ArrowRightLeft',
    routePattern: '/inventory/stock-movements?id={id}',
    module: 'inventory'
  },
  
  // HR Module (examples for future)
  'Employee': {
    displayName: 'Employee',
    iconName: 'User',
    routePattern: '/hr/employees?id={id}',
    module: 'hr'
  },
  'LeaveRequest': {
    displayName: 'Leave Request',
    iconName: 'Calendar',
    routePattern: '/hr/leave-requests?id={id}',
    module: 'hr'
  },
  
  // System/General
  'General': {
    displayName: 'System',
    iconName: 'Settings',
    routePattern: '/dashboard',
    module: 'system'
  }
};

/**
 * Build navigation URL for a notification entity
 * @param entityType The type of entity (JobCard, WorkOrder, Customer, Invoice, etc.)
 * @param entityId The ID of the specific entity
 * @returns The URL to navigate to, or null if no mapping exists
 */
export function getEntityNavigationUrl(entityType: EntityType, entityId: string): string | null {
  if (!entityType || !entityId) {
    return null;
  }

  const config = ENTITY_CONFIG[entityType];
  if (!config) {
    console.warn(`No entity configuration found for type: ${entityType}`);
    return null;
  }

  return config.routePattern.replace('{id}', encodeURIComponent(entityId));
}

/**
 * Get display name for entity type
 * @param entityType The entity type
 * @returns Human-readable entity type name
 */
export function getEntityDisplayName(entityType: EntityType): string {
  const config = ENTITY_CONFIG[entityType];
  return config?.displayName || entityType;
}

/**
 * Get icon component name for entity type
 * @param entityType The entity type
 * @returns Lucide icon name
 */
export function getEntityIconName(entityType: EntityType): string {
  const config = ENTITY_CONFIG[entityType];
  return config?.iconName || 'Package';
}

/**
 * Get module/category for entity type
 * @param entityType The entity type
 * @returns Module name or undefined
 */
export function getEntityModule(entityType: EntityType): string | undefined {
  const config = ENTITY_CONFIG[entityType];
  return config?.module;
}

/**
 * Check if entity type is valid and has a navigation route
 * @param entityType The entity type to check
 * @returns True if entity type is navigable
 */
export function isNavigableEntity(entityType: EntityType): boolean {
  const config = ENTITY_CONFIG[entityType];
  return config !== undefined && config.routePattern !== undefined;
}

/**
 * Get all configured entity types
 * @returns Array of all entity type keys
 */
export function getAvailableEntityTypes(): EntityType[] {
  return Object.keys(ENTITY_CONFIG);
}

/**
 * Get all entities for a specific module
 * @param module Module name (e.g., 'maintenance', 'crm', 'finance')
 * @returns Array of entity types in that module
 */
export function getEntitiesByModule(module: string): EntityType[] {
  return Object.entries(ENTITY_CONFIG)
    .filter(([, config]) => config.module === module)
    .map(([key]) => key);
}

/**
 * Register a new entity type dynamically
 * Useful for plugins or dynamic module loading
 * @param entityType The entity type key
 * @param config The entity configuration
 */
export function registerEntity(entityType: EntityType, config: EntityConfig): void {
  ENTITY_CONFIG[entityType] = config;
  console.log(`Entity '${entityType}' registered successfully`);
}

/**
 * Get all configuration for debugging or admin purposes
 * @returns Copy of the entity configuration
 */
export function getEntityConfiguration(): Record<string, EntityConfig> {
  return { ...ENTITY_CONFIG };
}

export default {
  getEntityNavigationUrl,
  getEntityDisplayName,
  getEntityIconName,
  getEntityModule,
  isNavigableEntity,
  getAvailableEntityTypes,
  getEntitiesByModule,
  registerEntity,
  getEntityConfiguration
};
