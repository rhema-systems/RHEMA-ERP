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
} as const;