export const API_CONFIG = {
  BASE_URL: process.env.NEXT_PUBLIC_API_URL || 'http://localhost:5000',
  TIMEOUT: 30000,
  RETRY_ATTEMPTS: 3,
} as const;

export const API_ENDPOINTS = {
  // Authentication
  AUTH: {
    LOGIN: '/api/auth/login',
    LOGOUT: '/api/auth/logout',
    REFRESH: '/api/auth/refresh',
    ME: '/api/auth/me',
  },
  
  // Users
  USERS: {
    LIST: '/api/users',
    BY_ID: (id: string) => `/api/users/${id}`,
    CREATE: '/api/users',
    UPDATE: (id: string) => `/api/users/${id}`,
    DELETE: (id: string) => `/api/users/${id}`,
  },

  // Tenants
  TENANTS: {
    LIST: '/api/tenant',
    BY_ID: (id: string) => `/api/tenant/${id}`,
    BY_CODE: (code: string) => `/api/tenant/by-code/${code}`,
    CREATE: '/api/tenant',
    UPDATE: (id: string) => `/api/tenant/${id}`,
    DELETE: (id: string) => `/api/tenant/${id}`,
    // User-Tenant Mapping
    USERS: (tenantId: string) => `/api/tenant/${tenantId}/users`,
    ADD_USER: (tenantId: string) => `/api/tenant/${tenantId}/users`,
    REMOVE_USER: (tenantId: string, userId: string) => `/api/tenant/${tenantId}/users/${userId}`,
  },

  // Dashboard
  DASHBOARD: {
    STATS: '/api/dashboard/stats',
    HEALTH: '/api/dashboard/health',
  },

  // Health
  HEALTH: {
    CHECK: '/health',
    READY: '/health/ready',
    LIVE: '/health/live',
  },

  // Asset Analytics
  ASSET_ANALYTICS: {
    // General Analytics Data
    DATA: '/api/maintenance/analytics/data',
    
    // OEE Analytics
    ASSET_OEE: (assetId: string) => `/api/maintenance/analytics/assets/${assetId}/oee`,
    FLEET_OEE: '/api/maintenance/analytics/fleet/oee',
    
    // Reliability Analytics
    ASSET_RELIABILITY: (assetId: string) => `/api/maintenance/analytics/assets/${assetId}/reliability`,
    RELIABILITY_RANKINGS: '/api/maintenance/analytics/fleet/reliability/rankings',
    
    // Performance Benchmarking
    BENCHMARKS: '/api/maintenance/analytics/benchmarks',
    COMPARISON: '/api/maintenance/analytics/comparison',
    RANKINGS: '/api/maintenance/analytics/rankings',
    
    // Predictive Analytics
    HEALTH_TRENDS: '/api/maintenance/analytics/health-trends',
    PREDICTIONS: '/api/maintenance/analytics/predictions',
    TRENDS: '/api/maintenance/analytics/trends',
    
    // KPI Dashboard
    DASHBOARD: '/api/maintenance/analytics/dashboard',
    ADVANCED_KPIS: '/api/maintenance/analytics/kpis/advanced',
    
    // Advanced Analytics
    ROOT_CAUSE_ANALYSIS: '/api/maintenance/analytics/root-cause-analysis',
    CRITICALITY_ANALYSIS: '/api/maintenance/analytics/criticality-analysis',
    OPTIMIZATION_RECOMMENDATIONS: '/api/maintenance/analytics/optimization-recommendations',
    
    // Cost Analytics
    TOTAL_COST_OWNERSHIP: (assetId: string) => `/api/maintenance/analytics/assets/${assetId}/total-cost-ownership`,
    COST_EFFICIENCY: (assetId: string) => `/api/maintenance/analytics/assets/${assetId}/cost-efficiency`,
    MAINTENANCE_ROI: (assetId: string) => `/api/maintenance/analytics/assets/${assetId}/maintenance-roi`,
    
    // Energy & Environmental
    ENERGY_PERFORMANCE: (assetId: string) => `/api/maintenance/analytics/assets/${assetId}/energy-performance`,
    ENVIRONMENTAL_IMPACT: (assetId: string) => `/api/maintenance/analytics/assets/${assetId}/environmental-impact`,
  },
} as const;

export const HTTP_STATUS = {
  OK: 200,
  CREATED: 201,
  NO_CONTENT: 204,
  BAD_REQUEST: 400,
  UNAUTHORIZED: 401,
  FORBIDDEN: 403,
  NOT_FOUND: 404,
  CONFLICT: 409,
  UNPROCESSABLE_ENTITY: 422,
  INTERNAL_SERVER_ERROR: 500,
  SERVICE_UNAVAILABLE: 503,
} as const;

export const QUERY_KEYS = {
  // Authentication
  CURRENT_USER: ['auth', 'current-user'],
  
  // Dashboard
  DASHBOARD_STATS: ['dashboard', 'stats'],
  SYSTEM_HEALTH: ['dashboard', 'health'],
  
  // Users
  USERS: ['users'],
  USER_BY_ID: (id: string) => ['users', id],
  
  // Tenants
  TENANTS: ['tenants'],
  TENANT_BY_ID: (id: string) => ['tenants', id],
  
  // Asset Analytics
  ASSET_ANALYTICS: {
    // OEE Analytics
    ASSET_OEE: (assetId: string, startDate?: string, endDate?: string) => [
      'asset-analytics', 'oee', assetId, startDate, endDate
    ],
    FLEET_OEE: (startDate?: string, endDate?: string) => [
      'asset-analytics', 'fleet-oee', startDate, endDate
    ],
    
    // Reliability Analytics
    ASSET_RELIABILITY: (assetId: string, startDate?: string, endDate?: string) => [
      'asset-analytics', 'reliability', assetId, startDate, endDate
    ],
    RELIABILITY_RANKINGS: (startDate?: string, endDate?: string) => [
      'asset-analytics', 'reliability-rankings', startDate, endDate
    ],
    
    // Performance Benchmarking
    BENCHMARKS: (assetId: string, category?: string) => [
      'asset-analytics', 'benchmarks', assetId, category
    ],
    COMPARISON: (assetId: string, industryType: string) => [
      'asset-analytics', 'comparison', assetId, industryType
    ],
    RANKINGS: (metricType: string) => [
      'asset-analytics', 'rankings', metricType
    ],
    
    // Predictive Analytics
    HEALTH_TRENDS: (assetId: string, periodMonths?: number) => [
      'asset-analytics', 'health-trends', assetId, periodMonths
    ],
    PREDICTIONS: (assetId: string, predictionDays?: number) => [
      'asset-analytics', 'predictions', assetId, predictionDays
    ],
    TRENDS: (startDate?: string, endDate?: string, period?: string) => [
      'asset-analytics', 'trends', startDate, endDate, period
    ],
    
    // KPI Dashboard
    DASHBOARD: (assetId?: string, startDate?: string, endDate?: string) => [
      'asset-analytics', 'dashboard', assetId, startDate, endDate
    ],
    ADVANCED_KPIS: (startDate?: string, endDate?: string, category?: string) => [
      'asset-analytics', 'advanced-kpis', startDate, endDate, category
    ],
    
    // Advanced Analytics
    ROOT_CAUSE_ANALYSIS: (assetId: string, incidentDate: string, issueType: string) => [
      'asset-analytics', 'root-cause', assetId, incidentDate, issueType
    ],
    CRITICALITY_ANALYSIS: (assetId: string) => [
      'asset-analytics', 'criticality', assetId
    ],
    OPTIMIZATION_RECOMMENDATIONS: (assetId?: string) => [
      'asset-analytics', 'optimization', assetId
    ],
    
    // Cost Analytics
    TOTAL_COST_OWNERSHIP: (assetId: string, startDate?: string, endDate?: string) => [
      'asset-analytics', 'tco', assetId, startDate, endDate
    ],
    COST_EFFICIENCY: (assetId: string, startDate?: string, endDate?: string) => [
      'asset-analytics', 'cost-efficiency', assetId, startDate, endDate
    ],
    MAINTENANCE_ROI: (assetId: string, startDate?: string, endDate?: string) => [
      'asset-analytics', 'maintenance-roi', assetId, startDate, endDate
    ],
    
    // Energy & Environmental
    ENERGY_PERFORMANCE: (assetId: string, startDate?: string, endDate?: string) => [
      'asset-analytics', 'energy-performance', assetId, startDate, endDate
    ],
    ENVIRONMENTAL_IMPACT: (assetId: string, startDate?: string, endDate?: string) => [
      'asset-analytics', 'environmental-impact', assetId, startDate, endDate
    ],
  },
} as const;

export const ASSET_ANALYTICS_QUERY_KEYS = {
  ASSET_OEE: 'asset-oee',
  FLEET_OEE: 'fleet-oee',
  ASSET_RELIABILITY: 'asset-reliability',
  RELIABILITY_RANKINGS: 'reliability-rankings',
  BENCHMARKS: 'asset-benchmarks',
  COMPARISON: 'asset-comparison',
  RANKINGS: 'asset-rankings',
  HEALTH_TRENDS: 'asset-health-trends',
  PREDICTIONS: 'asset-predictions',
  TRENDS: 'asset-trends',
  DASHBOARD: 'asset-dashboard',
  ADVANCED_KPIS: 'asset-advanced-kpis',
  ROOT_CAUSE_ANALYSIS: 'asset-root-cause-analysis',
  CRITICALITY_ANALYSIS: 'asset-criticality-analysis',
  OPTIMIZATION_RECOMMENDATIONS: 'asset-optimization-recommendations',
  TOTAL_COST_OWNERSHIP: 'asset-total-cost-ownership',
  COST_EFFICIENCY: 'asset-cost-efficiency',
  MAINTENANCE_ROI: 'asset-maintenance-roi',
  ENERGY_PERFORMANCE: 'asset-energy-performance',
  ENVIRONMENTAL_IMPACT: 'asset-environmental-impact',
} as const;
