import { describe, expect, it } from 'vitest';

import {
  navigationItems,
  sidebarNavigationItems,
  type NavItem,
} from './sidebar';

const civilRoutePermissions = new Map<string, string[]>([
  [
    '/development/civil-engineering/design-inputs',
    ['civil-engineering.design-input.respond'],
  ],
  [
    '/development/civil-engineering/direct-tasks',
    ['civil-engineering.workspace.read'],
  ],
  [
    '/development/civil-engineering/maintenance-intakes',
    ['civil-engineering.maintenance.manage'],
  ],
  [
    '/development/civil-engineering/maintenance-assessments',
    ['civil-engineering.maintenance.manage'],
  ],
  [
    '/development/civil-engineering/maintenance-costing-handoffs',
    ['civil-engineering.maintenance.manage'],
  ],
  [
    '/development/civil-engineering/maintenance-execution-links',
    ['civil-engineering.maintenance.manage'],
  ],
  [
    '/development/civil-engineering/maintenance-completion-controls',
    ['civil-engineering.maintenance.manage'],
  ],
  [
    '/development/civil-engineering/complaint-resolutions',
    ['civil-engineering.workspace.read'],
  ],
  [
    '/development/civil-engineering/development-approval-files',
    ['civil-engineering.permitting.manage'],
  ],
  [
    '/development/civil-engineering/development-approval-handoffs',
    ['civil-engineering.permitting.manage'],
  ],
  [
    '/development/civil-engineering/permitting-engineering-reviews',
    ['civil-engineering.permitting.manage'],
  ],
  [
    '/development/civil-engineering/permitting-hod-decisions',
    ['civil-engineering.permitting.manage'],
  ],
  [
    '/development/civil-engineering/migration-batches',
    ['civil-engineering.migration.manage'],
  ],
]);

function descendants(item: NavItem | undefined): NavItem[] {
  if (!item) return [];
  return (item.children ?? []).flatMap((child) => [
    child,
    ...descendants(child),
  ]);
}

describe('Civil Engineering navigation', () => {
  it('consolidates Civil Engineering under Projects with professional functional groups', () => {
    expect(
      navigationItems.filter((item) =>
        item.href.startsWith('/development/civil-engineering')
      )
    ).toEqual([]);

    const projects = navigationItems.find((item) => item.title === 'Projects');
    const civilEngineering = projects?.children?.find(
      (item) => item.title === 'Civil Engineering'
    );

    expect(civilEngineering?.children?.map((item) => item.title)).toEqual([
      'Design & Delivery',
      'Maintenance',
      'Permitting',
      'Administration',
    ]);
  });

  it('retains every Civil Engineering route with its original permission', () => {
    const projects = sidebarNavigationItems.find(
      (item) => item.title === 'Projects'
    );
    const civilEngineering = projects?.children?.find(
      (item) => item.title === 'Civil Engineering'
    );
    const routeItems = descendants(civilEngineering).filter(
      (item) => !item.children?.length && civilRoutePermissions.has(item.href)
    );

    expect(routeItems).toHaveLength(civilRoutePermissions.size);
    expect(
      new Map(routeItems.map((item) => [item.href, item.permissions]))
    ).toEqual(civilRoutePermissions);
  });

  it('retains project.access on every existing Projects child', () => {
    const projects = navigationItems.find((item) => item.title === 'Projects');
    const existingProjectChildren = projects?.children?.filter(
      (item) => item.title !== 'Civil Engineering'
    );

    expect(existingProjectChildren).not.toHaveLength(0);
    for (const child of existingProjectChildren ?? []) {
      expect(child.permissions).toEqual(['project.access']);
    }
  });
});
