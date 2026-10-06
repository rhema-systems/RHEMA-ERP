export interface SettingsAccessSubject {
  href: string;
  roles?: string[];
  permissions?: string[];
  accessMode?: 'all' | 'any';
}

export interface SettingsAccessEvaluator {
  hasAnyRole: (roles: string[]) => boolean;
  hasAnyPermission: (permissions: string[]) => boolean;
}

export interface SettingsAccessNode extends SettingsAccessSubject {
  children?: SettingsAccessNode[];
}

interface SettingsAccessRule {
  matches: (href: string) => boolean;
  roles?: string[];
  permissions?: string[];
  accessMode?: 'all' | 'any';
}

const ADMINISTRATION_ROLES = [
  'admin',
  'SystemAdmin',
  'SuperAdmin',
  'TenantAdmin',
];
const WORKFLOW_ADMINISTRATION_ROLES = [
  ...ADMINISTRATION_ROLES,
  'WorkflowAdmin',
];
const DMS_ADMINISTRATION_ROLES = [
  ...ADMINISTRATION_ROLES,
  'Document Control Officer',
  'Records Officer',
];
const LEGAL_ADMINISTRATION_ROLES = [...ADMINISTRATION_ROLES];
const PROCUREMENT_POLICY_ROLES = ['SuperAdmin', 'TenantAdmin'];

const normalizeHref = (href: string) => href.trim().toLowerCase();
const pathOnly = (href: string) =>
  normalizeHref(href).split(/[?#]/, 1)[0].replace(/\/$/, '');

const exactHref = (expected: string) => {
  const normalized = normalizeHref(expected);
  return (href: string) => normalizeHref(href) === normalized;
};

const exactPath = (expected: string) => {
  const normalized = pathOnly(expected);
  return (href: string) => pathOnly(href) === normalized;
};

const pathPrefix = (prefix: string) => {
  const normalized = pathOnly(prefix);
  return (href: string) => {
    const candidate = pathOnly(href);
    return candidate === normalized || candidate.startsWith(`${normalized}/`);
  };
};

const anyAccess = (
  matches: SettingsAccessRule['matches'],
  permissions: string[],
  roles: string[] = ADMINISTRATION_ROLES
): SettingsAccessRule => ({ matches, permissions, roles, accessMode: 'any' });

const roleAccess = (
  matches: SettingsAccessRule['matches'],
  roles: string[] = ADMINISTRATION_ROLES
): SettingsAccessRule => ({ matches, roles });

/**
 * Settings-only access registry. Rules are ordered most-specific first. It keeps
 * the operational sidebar metadata unchanged while giving every Settings leaf
 * a fail-closed role/permission boundary aligned to its existing route owner.
 */
const SETTINGS_ACCESS_RULES: SettingsAccessRule[] = [
  // Finance setup links promoted from the operational navigation tree.
  anyAccess(exactPath('/finance/accounts'), [
    'Finance.Admin',
    'Finance.ChartOfAccounts.Manage',
  ]),
  anyAccess(exactPath('/finance/opening-balances'), [
    'Finance.Admin',
    'Finance.Migration.OpeningBalances.Prepare',
  ]),
  anyAccess(exactPath('/finance/fiscal-years'), [
    'Finance.Admin',
    'Finance.PeriodClose',
    'Finance.PeriodReopen',
  ]),
  anyAccess(exactPath('/finance/fiscal-periods'), [
    'Finance.Admin',
    'Finance.PeriodClose',
    'Finance.PeriodReopen',
  ]),
  anyAccess(exactPath('/finance/cash/accounts'), [
    'Finance.Admin',
    'Finance.BankAccounts.Manage',
  ]),
  anyAccess(exactPath('/finance/unit-accounts'), ['Finance.Admin']),

  // Module-owned setup links promoted from the operational navigation tree.
  anyAccess(exactPath('/procurement/planning/specification-templates'), [
    'procurement.plan.manage',
    'procurement.plan.approve',
  ]),
  anyAccess(exactPath('/sales/journal-templates'), ['settings.update']),
  anyAccess(
    exactHref('/administration/workflow?q=Legal'),
    ['procurement.workflow.configure'],
    WORKFLOW_ADMINISTRATION_ROLES
  ),
  roleAccess(exactPath('/administration/legal'), LEGAL_ADMINISTRATION_ROLES),
  roleAccess(
    exactHref('/administration/document-management/document-templates?q=Legal'),
    DMS_ADMINISTRATION_ROLES
  ),
  roleAccess(
    exactHref('/administration/document-management/metadata-templates?q=Legal'),
    DMS_ADMINISTRATION_ROLES
  ),

  // Notifications are an administration function and never appear to ordinary users.
  anyAccess(pathPrefix('/notifications'), ['settings.update']),

  // Procurement policy and governance controls.
  roleAccess(
    pathPrefix('/administration/procurement/policy-profiles'),
    PROCUREMENT_POLICY_ROLES
  ),
  roleAccess(
    pathPrefix('/administration/procurement/policy-sets'),
    PROCUREMENT_POLICY_ROLES
  ),
  roleAccess(
    pathPrefix('/administration/procurement/compliance-simulator'),
    PROCUREMENT_POLICY_ROLES
  ),
  roleAccess(
    pathPrefix('/administration/procurement/sod-controls'),
    PROCUREMENT_POLICY_ROLES
  ),
  anyAccess(pathPrefix('/administration/procurement/access-controls'), [
    'procurement.access.manage',
  ]),
  anyAccess(
    pathPrefix('/administration/procurement/approval-workflows'),
    ['procurement.workflow.configure'],
    WORKFLOW_ADMINISTRATION_ROLES
  ),

  // Stable supplier classification setup stays on the Settings surface.
  anyAccess(pathPrefix('/administration/procurement/partner-categories'), [
    'procurement.supplier.manage',
  ]),
  anyAccess(
    pathPrefix('/administration/procurement/contractor-specializations'),
    ['procurement.supplier.manage']
  ),
  anyAccess(pathPrefix('/administration/procurement/license-types'), [
    'procurement.supplier.manage',
  ]),

  // Sourcing configuration and tender templates.
  anyAccess(pathPrefix('/administration/procurement/evaluation-criteria'), [
    'procurement.sourcing.manage',
    'procurement.tender.administer',
  ]),
  anyAccess(pathPrefix('/administration/procurement/evaluation-templates'), [
    'procurement.sourcing.manage',
    'procurement.tender.administer',
  ]),
  anyAccess(pathPrefix('/administration/procurement/document-types'), [
    'procurement.sourcing.manage',
    'procurement.tender.administer',
  ]),
  anyAccess(
    pathPrefix('/administration/procurement/award-verification-checklists'),
    ['procurement.sourcing.manage', 'procurement.tender.administer']
  ),
  roleAccess(pathPrefix('/administration/procurement/purchase-order-settings')),
  roleAccess(pathPrefix('/administration/procurement')),

  // Inventory, Finance and the other module administration workspaces.
  anyAccess(pathPrefix('/administration/inventory'), [
    'procurement.inventory.master-data.manage',
  ]),
  anyAccess(pathPrefix('/administration/finance'), ['Finance.Admin']),
  anyAccess(
    pathPrefix('/administration/document-management'),
    ['Finance.Admin'],
    DMS_ADMINISTRATION_ROLES
  ),
  anyAccess(
    pathPrefix('/administration/project-management/quantity-survey-config'),
    ['quantity-survey.configuration.read']
  ),
  anyAccess(
    pathPrefix('/administration/project-management/civil-engineering-config'),
    ['civil-engineering.configuration.read']
  ),
  anyAccess(
    pathPrefix('/administration/project-management/quantity-survey-catalogues'),
    ['quantity-survey.configuration.read']
  ),
  anyAccess(
    pathPrefix(
      '/administration/project-management/quantity-survey-rate-library'
    ),
    ['quantity-survey.workspace.read']
  ),
  anyAccess(pathPrefix('/administration/project-management'), [
    'admin.project-management',
  ]),
  anyAccess(pathPrefix('/administration/maintenance'), ['admin.maintenance']),
  anyAccess(pathPrefix('/administration/fleet-management'), [
    'admin.fleet-management',
  ]),
  roleAccess(pathPrefix('/administration/hr')),
  roleAccess(pathPrefix('/administration/sales')),
  roleAccess(pathPrefix('/administration/marketing')),
  roleAccess(pathPrefix('/administration/estate')),
  roleAccess(
    exactHref('/administration/document-management/document-templates?q=Estate')
  ),
  roleAccess(
    exactHref('/administration/document-management/metadata-templates?q=Estate')
  ),
  anyAccess(
    exactHref('/administration/workflow?q=Estate'),
    ['procurement.workflow.configure'],
    WORKFLOW_ADMINISTRATION_ROLES
  ),
  roleAccess(exactPath('/estate/gis')),
  roleAccess(pathPrefix('/administration/helpdesk')),

  // Shared workflow, identity, tenant, security, audit and communications setup.
  anyAccess(
    exactPath('/administration/workflow'),
    ['procurement.workflow.configure'],
    WORKFLOW_ADMINISTRATION_ROLES
  ),
  anyAccess(exactPath('/administration/security/dashboard'), [
    'settings.read',
    'settings.update',
  ]),
  anyAccess(exactPath('/administration/reports'), ['reports.create']),
  anyAccess(exactPath('/administration/identity-management/users'), [
    'users.read',
    'users.create',
    'users.update',
    'users.delete',
  ]),
  anyAccess(exactPath('/administration/identity-management/roles'), [
    'roles.read',
    'roles.create',
    'roles.update',
    'roles.delete',
  ]),
  anyAccess(exactPath('/administration/user-employee-links'), [
    'users.read',
    'users.update',
  ]),
  anyAccess(
    exactPath('/administration/identity-management/hr-reconciliation'),
    ['settings.read', 'settings.update']
  ),
  anyAccess(exactPath('/administration/tenant-management'), [
    'settings.read',
    'settings.update',
  ]),
  anyAccess(exactPath('/administration/settings/email'), ['settings.update']),
  anyAccess(exactPath('/administration/settings/sms'), ['settings.update']),
  anyAccess(exactPath('/administration/settings/file-uploads'), [
    'settings.update',
  ]),
  anyAccess(exactPath('/administration/settings/field-labels'), [
    'settings.update',
  ]),
  anyAccess(exactPath('/administration/user-tenant-mapping'), ['users.update']),
  anyAccess(exactPath('/administration/identity-management/security-logs'), [
    'audit.read',
  ]),
  anyAccess(exactPath('/administration/audit-logs'), ['audit.read']),
  anyAccess(exactPath('/administration/security/retention'), [
    'settings.update',
  ]),
  anyAccess(exactPath('/data-sources'), ['settings.update']),
  roleAccess(exactPath('/administration/notifications')),
  anyAccess(exactPath('/administration/system-exception-logs'), ['audit.read']),
];

export function resolveSettingsAccessRule(
  subject: SettingsAccessSubject
): Omit<SettingsAccessRule, 'matches'> {
  const registeredRule = SETTINGS_ACCESS_RULES.find((rule) =>
    rule.matches(subject.href)
  );
  if (registeredRule) {
    return {
      roles: registeredRule.roles,
      permissions: registeredRule.permissions,
      accessMode: registeredRule.accessMode,
    };
  }

  if (subject.roles?.length || subject.permissions?.length) {
    return {
      roles: subject.roles,
      permissions: subject.permissions,
      accessMode: subject.accessMode,
    };
  }

  // New Settings leaves fail closed to explicit administrators until their
  // owning team registers a narrower permission above.
  return { roles: ADMINISTRATION_ROLES };
}

export function canAccessSettingsItem(
  subject: SettingsAccessSubject,
  evaluator: SettingsAccessEvaluator
): boolean {
  const rule = resolveSettingsAccessRule(subject);
  const hasRoleAccess = !rule.roles?.length || evaluator.hasAnyRole(rule.roles);
  const hasPermissionAccess =
    !rule.permissions?.length || evaluator.hasAnyPermission(rule.permissions);

  return rule.accessMode === 'any'
    ? hasRoleAccess || hasPermissionAccess
    : hasRoleAccess && hasPermissionAccess;
}

export function hasAnyAccessibleSettings(
  items: SettingsAccessNode[],
  evaluator: SettingsAccessEvaluator
): boolean {
  return items.some((item) =>
    item.children?.length
      ? hasAnyAccessibleSettings(item.children, evaluator)
      : canAccessSettingsItem(item, evaluator)
  );
}
