import { describe, expect, it } from 'vitest';

import type { User } from '../types';
import {
  hasAllPermissionsAccess,
  hasAnyPermissionAccess,
  hasAnyRoleAccess,
  hasPermissionAccess,
} from './permissions';

const userWith = (roles: string[], permissions: string[] = []): User =>
  ({
    id: 'user-1',
    username: 'permission-user',
    email: 'permission-user@example.com',
    isActive: true,
    roles,
    permissions,
  }) satisfies User;

describe('permission access', () => {
  it.each(['admin', 'Admin', 'SystemAdmin'])(
    'does not grant every permission to the assignable %s role',
    (role) => {
      const user = userWith([role]);

      expect(hasPermissionAccess(user, 'estate.land.project-readiness')).toBe(
        false
      );
      expect(
        hasAnyPermissionAccess(user, [
          'estate.land.project-readiness',
          'estate.land.manage',
        ])
      ).toBe(false);
      expect(
        hasAllPermissionsAccess(user, [
          'estate.land.project-readiness',
          'estate.land.manage',
        ])
      ).toBe(false);
    }
  );

  it('honors permissions explicitly assigned to a custom admin role', () => {
    const user = userWith(['Admin'], ['estate.land.project-readiness']);

    expect(hasPermissionAccess(user, 'estate.land.project-readiness')).toBe(
      true
    );
  });

  it.each(['SuperAdmin', 'TenantAdmin'])(
    'keeps the protected %s role privileged',
    (role) => {
      expect(
        hasPermissionAccess(userWith([role]), 'estate.land.project-readiness')
      ).toBe(true);
    }
  );

  it('honors the wildcard permission without requiring a privileged role', () => {
    expect(
      hasPermissionAccess(
        userWith(['Estate Officer'], ['*']),
        'estate.land.project-readiness'
      )
    ).toBe(true);
  });

  it('matches route roles without casing differences', () => {
    const user = userWith(['Records Officer']);

    expect(hasAnyRoleAccess(user, ['records officer'])).toBe(true);
    expect(hasAnyRoleAccess(user, ['Head of Legal'])).toBe(false);
  });
});
