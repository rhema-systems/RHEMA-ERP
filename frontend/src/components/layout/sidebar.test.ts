import { describe, expect, it } from 'vitest';

import { canAccessNavItem } from './sidebar';

const denyRoles = () => false;
const denyPermissions = () => false;

describe('canAccessNavItem', () => {
  it('allows an item without access rules', () => {
    expect(canAccessNavItem({}, denyRoles, denyPermissions)).toBe(true);
  });

  it('does not treat a missing role rule as access in any mode', () => {
    expect(
      canAccessNavItem(
        { permissions: ['estate.access'], accessMode: 'any' },
        denyRoles,
        denyPermissions
      )
    ).toBe(false);
  });

  it('allows any mode when either configured rule succeeds', () => {
    expect(
      canAccessNavItem(
        {
          roles: ['Estate Officer'],
          permissions: ['estate.access'],
          accessMode: 'any',
        },
        () => true,
        denyPermissions
      )
    ).toBe(true);
  });

  it('requires every configured rule in all mode', () => {
    expect(
      canAccessNavItem(
        {
          roles: ['Estate Officer'],
          permissions: ['estate.access'],
        },
        () => true,
        denyPermissions
      )
    ).toBe(false);
  });
});
