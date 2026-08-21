export const apAgingReportQueryKey = (
  tenantCode: string | null,
  asOfDate: string
) =>
  [
    'finance',
    'ap',
    'reports',
    'aging',
    tenantCode ?? 'missing-tenant',
    asOfDate,
  ] as const;

export const apCashRequirementsQueryKey = (
  tenantCode: string | null,
  asOfDate: string
) =>
  [
    'finance',
    'ap',
    'reports',
    'cash-requirements',
    tenantCode ?? 'missing-tenant',
    asOfDate,
  ] as const;
