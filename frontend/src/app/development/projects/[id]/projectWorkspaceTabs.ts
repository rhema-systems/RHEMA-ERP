export const PROJECT_WORKSPACE_TABS = [
  'overview',
  'phases',
  'packages',
  'budgeting',
  'plan',
  'design',
  'approvals',
  'commercial',
  'commercial-admin',
  'site-controls',
  'execution',
  'materials',
  'units',
  'variations',
  'handover',
  'defects',
  'analysis',
  'governance',
  'documents',
  'access',
  'history',
] as const;

export type ProjectWorkspaceTab = typeof PROJECT_WORKSPACE_TABS[number];

export const PROJECT_WORKSPACE_TAB_LABELS: Record<ProjectWorkspaceTab, string> = {
  overview: 'Overview',
  phases: 'Phases',
  packages: 'Work Components',
  budgeting: 'Budgeting',
  plan: 'Planning',
  design: 'Design',
  approvals: 'Approvals',
  commercial: 'Commercial',
  'commercial-admin': 'Commercial Admin',
  'site-controls': 'Site',
  execution: 'Execution',
  materials: 'Materials',
  units: 'Units',
  variations: 'Variations',
  handover: 'Handover',
  defects: 'Defects',
  analysis: 'Analysis',
  governance: 'Governance',
  documents: 'Documents',
  access: 'Access',
  history: 'History',
};

type ProjectWorkspaceTabContext = {
  deliveryStructure?: string | null;
  developmentType?: string | null;
};

function normalizeWorkspaceProfileValue(value?: string | null): string {
  return value?.trim().toLowerCase() ?? '';
}

function moveTabsAfter(
  tabs: ProjectWorkspaceTab[],
  tabsToMove: ProjectWorkspaceTab[],
  anchorTab: ProjectWorkspaceTab,
): ProjectWorkspaceTab[] {
  const remaining = tabs.filter((tab) => !tabsToMove.includes(tab));
  const anchorIndex = remaining.indexOf(anchorTab);

  if (anchorIndex === -1) {
    return [...remaining, ...tabsToMove];
  }

  return [
    ...remaining.slice(0, anchorIndex + 1),
    ...tabsToMove,
    ...remaining.slice(anchorIndex + 1),
  ];
}

export function getProjectWorkspaceTabs(context?: ProjectWorkspaceTabContext): ProjectWorkspaceTab[] {
  const deliveryStructure = normalizeWorkspaceProfileValue(context?.deliveryStructure);
  const developmentType = normalizeWorkspaceProfileValue(context?.developmentType);
  let tabs = [...PROJECT_WORKSPACE_TABS];

  const isMultiUnit = deliveryStructure === 'multiunit';
  const isSingleUnit = deliveryStructure === 'singleunit';
  const isWholeDevelopment = !deliveryStructure || deliveryStructure === 'wholedevelopment';
  const isResidential = developmentType === 'residential' || developmentType === 'mixeduse' || developmentType === 'hospitality';
  const isRenovation = developmentType === 'renovation';

  if (isMultiUnit) {
    tabs = moveTabsAfter(tabs, ['units', 'variations', 'handover'], 'commercial-admin');
  } else if (isSingleUnit) {
    tabs = moveTabsAfter(tabs, ['units', 'handover', 'variations'], 'commercial-admin');
  } else if (isWholeDevelopment) {
    tabs = moveTabsAfter(tabs, ['units', 'variations'], 'handover');
  }

  if (isResidential && isMultiUnit) {
    tabs = moveTabsAfter(tabs, ['commercial', 'commercial-admin'], 'budgeting');
  }

  if (isRenovation) {
    tabs = moveTabsAfter(tabs, ['site-controls', 'execution', 'materials', 'variations'], 'plan');
    tabs = moveTabsAfter(tabs, ['commercial', 'commercial-admin'], 'materials');
    tabs = moveTabsAfter(tabs, ['units'], 'handover');
  }

  return tabs;
}
