export const PROJECT_WORKSPACE_TABS = [
  'overview',
  'phases',
  'design',
  'plan',
  'packages',
  'commercial',
  'commercial-admin',
  'approvals',
  'site-controls',
  'units',
  'variations',
  'handover',
  'defects',
  'execution',
  'analysis',
  'materials',
  'governance',
  'access',
  'documents',
  'history',
] as const;

export type ProjectWorkspaceTab = typeof PROJECT_WORKSPACE_TABS[number];
