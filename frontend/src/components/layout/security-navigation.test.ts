import { describe, expect, it } from 'vitest';

import { settingsNavigationItems, type NavItem } from './sidebar';

function flatten(items: NavItem[]): NavItem[] {
  return items.flatMap((item) => [item, ...(item.children ? flatten(item.children) : [])]);
}

describe('consolidated security navigation', () => {
  it('links only Security Management and leaves the legacy URL to redirect', () => {
    const items = flatten(settingsNavigationItems);

    expect(items).toContainEqual(
      expect.objectContaining({
        title: 'Security Management',
        href: '/administration/security/dashboard',
      })
    );
    expect(items.some((item) => item.href === '/administration/settings/security')).toBe(false);
  });
});
