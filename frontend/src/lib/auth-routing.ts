type RoleUser = {
  roles?: string[] | null;
} | null | undefined;

const EXTERNAL_PORTAL_ROLE = 'externaluser';
const INTERNAL_ADMIN_ROLES = new Set(['superadmin', 'tenantadmin', 'admin', 'administrator']);

const normalizedRoles = (user: RoleUser) =>
  (user?.roles ?? []).map((role) => role.trim().toLowerCase());

export const isExternalPortalUser = (user: RoleUser) => {
  const roles = normalizedRoles(user);
  return (
    roles.includes(EXTERNAL_PORTAL_ROLE) &&
    !roles.some((role) => INTERNAL_ADMIN_ROLES.has(role))
  );
};

/**
 * Area 25 (D2): a user whose ONLY functional role is Employee lands in the self-service
 * portal. Anyone with a further role (Manager, HR, admin tiers…) is a desk user who gets
 * the two-way switcher instead.
 */
export const isEmployeeOnlyUser = (user: RoleUser) => {
  const roles = normalizedRoles(user);
  return roles.length > 0 && roles.every((role) => role === 'employee');
};

/** Desk access = internal and more than the Employee role. Gates the "Back to ERP" switcher. */
export const hasDeskAccess = (user: RoleUser) =>
  !isExternalPortalUser(user) && !isEmployeeOnlyUser(user);

export const isSupportHost = (host?: string | null) => {
  const value =
    host ??
    (typeof window !== 'undefined' ? window.location.hostname : '');

  return value.trim().toLowerCase().startsWith('support.');
};

export const getExternalPortalPath = (host?: string | null) =>
  isSupportHost(host) ? '/' : '/external-portal';

export const getAuthenticatedHomePath = (user: RoleUser, host?: string | null) =>
  isExternalPortalUser(user)
    ? getExternalPortalPath(host)
    : isEmployeeOnlyUser(user)
      ? '/me'
      : '/dashboard';
