export const PROJECT_WORKSPACE_TABS = [
  'overview',
  'plan',
  'packages',
  'commercial',
  'approvals',
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
