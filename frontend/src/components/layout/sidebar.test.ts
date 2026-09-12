import { describe, expect, it } from 'vitest';

import { canAccessNavItem, filterNavigationByAccess, sidebarNavigationItems } from './sidebar';

const denyRoles = () => false;
const denyPermissions = () => false;

describe('employee inventory navigation', () => {
  const inventory = (roles: string[], permissions: string[] = []) =>
    filterNavigationByAccess(sidebarNavigationItems,
      allowed => allowed.some(role => roles.includes(role)),
      allowed => allowed.some(permission => permissions.includes(permission)))
      .find(item => item.href === '/inventory');

  it('gives an Employee only the own-requisitions route, including the UAT MD role combination', () => {
    const section = inventory(['Employee', 'TDC_MANAGING_DIRECTOR'], ['procurement.inventory.disposal.approve']);
    expect(section?.children?.map(item => [item.title, item.href])).toEqual([
      ['My requisitions', '/inventory/requisitions'],
    ]);
  });

  it('does not expose the requester menu to an unrelated or supplier role', () => {
    expect(inventory([])).toBeUndefined();
    expect(inventory(['Supplier'])).toBeUndefined();
  });

  it('preserves the full existing menu for permitted Stores staff', () => {
    const section = inventory(['Employee', 'TDC_STORES_OFFICER'], ['procurement.inventory.read']);
    expect(section?.children?.map(item => item.title)).toEqual(['Items & Catalogue', 'Transactions']);
  });
});

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
