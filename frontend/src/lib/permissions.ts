import type { User } from '../types';

const PRIVILEGED_ROLES = new Set(['superadmin', 'tenantadmin']);

const normalize = (value: string | null | undefined) =>
  value?.trim().toLowerCase() ?? '';

const getNormalizedRoles = (user: User | null | undefined) =>
  new Set((user?.roles ?? []).map((role) => normalize(role)).filter(Boolean));

const getNormalizedPermissions = (user: User | null | undefined) =>
  new Set(
    (user?.permissions ?? [])
      .map((permission) => normalize(permission))
      .filter(Boolean)
  );

const hasPrivilegedRole = (user: User | null | undefined) => {
  const roles = getNormalizedRoles(user);
  return [...PRIVILEGED_ROLES].some((role) => roles.has(role));
};

const hasWildcardPermission = (user: User | null | undefined) =>
  getNormalizedPermissions(user).has('*');

export const hasPermissionAccess = (
  user: User | null | undefined,
  permission: string
) => {
  if (!permission.trim()) {
    return true;
  }

  if (hasPrivilegedRole(user) || hasWildcardPermission(user)) {
    return true;
  }

  return getNormalizedPermissions(user).has(normalize(permission));
};

export const hasAnyPermissionAccess = (
  user: User | null | undefined,
  permissions: string[]
) => {
  if (permissions.length === 0) {
    return true;
  }

  if (hasPrivilegedRole(user) || hasWildcardPermission(user)) {
    return true;
  }

  const normalizedPermissions = getNormalizedPermissions(user);
  return permissions.some((permission) =>
    normalizedPermissions.has(normalize(permission))
  );
};

export const hasAllPermissionsAccess = (
  user: User | null | undefined,
  permissions: string[]
) => {
  if (permissions.length === 0) {
    return true;
  }

  if (hasPrivilegedRole(user) || hasWildcardPermission(user)) {
    return true;
  }

  const normalizedPermissions = getNormalizedPermissions(user);
  return permissions.every((permission) =>
    normalizedPermissions.has(normalize(permission))
  );
};
