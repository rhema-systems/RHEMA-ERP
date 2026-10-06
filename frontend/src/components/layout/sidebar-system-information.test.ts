import { describe, expect, it } from 'vitest';

import { settingsNavigationItems, type NavItem } from './sidebar';

const flatten = (items: NavItem[]): NavItem[] =>
  items.flatMap(item => [item, ...flatten(item.children ?? [])]);

describe('System Information navigation', () => {
  it('is available in the Administration settings tree', () => {
    expect(flatten(settingsNavigationItems)).toEqual(expect.arrayContaining([
      expect.objectContaining({
        title: 'System Information',
        href: '/administration/system',
      }),
    ]));
  });
});
