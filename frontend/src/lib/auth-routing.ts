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

export const isSupportHost = (host?: string | null) => {
  const value =
    host ??
    (typeof window !== 'undefined' ? window.location.hostname : '');

  return value.trim().toLowerCase().startsWith('support.');
};

export const getExternalPortalPath = (host?: string | null) =>
  isSupportHost(host) ? '/' : '/external-portal';

export const getAuthenticatedHomePath = (user: RoleUser, host?: string | null) =>
  isExternalPortalUser(user) ? getExternalPortalPath(host) : '/dashboard';
