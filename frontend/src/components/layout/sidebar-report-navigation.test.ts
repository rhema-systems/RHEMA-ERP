import { describe, expect, it } from 'vitest';

import { navigationItems, settingsNavigationItems, sidebarNavigationItems, type NavItem } from './sidebar';

function containsHref(items: NavItem[], href: string): boolean {
  return items.some(item => item.href === href || containsHref(item.children ?? [], href));
}

function findByTitle(items: NavItem[], title: string): NavItem | undefined {
  for (const item of items) {
    if (item.title === title) return item;
    const child = findByTitle(item.children ?? [], title);
    if (child) return child;
  }
  return undefined;
}

describe('main Reports navigation', () => {
  it('routes every module submenu to a dedicated landing page', () => {
    const reports = navigationItems.find(item => item.title === 'Reports');

    expect(reports?.children?.map(item => [item.title, item.href])).toEqual([
      ['Financial Reports', '/reports/financial'],
      // These shared destinations were added by other module owners after the
      // original report-navigation gate. Retain the full ordered contract so a
      // future sidebar edit cannot silently remove a module's report landing page.
      ['Report Automation', '/reports/automation'],
      ['Procurement Reports', '/reports/purchasing'],
      ['Inventory Reports', '/reports/inventory'],
      ['Quantity Survey Reports', '/reports/quantity-survey'],
      ['Audit & Compliance Reports', '/reports/audit-compliance'],
      ['Sales Reports', '/reports/sales'],
      ['HR Reports', '/reports/human-resources'],
      ['Estate Reports', '/reports/estate'],
      ['Development Reports', '/reports/development'],
      ['Operations Reports', '/reports/operations'],
      ['Property Management Reports', '/reports?module=property-management'],
      ['Facilities Reports', '/reports?module=facilities'],
      ['Legal Reports', '/reports?module=legal'],
      ['DMS Reports', '/reports?module=dms'],
      ['Planning Reports', '/reports?module=planning'],
    ]);
  });
});

describe('navigation surfaces', () => {
  it('exposes the controlled journal-batch workspace under General Ledger', () => {
    // This assertion guards against a fully implemented Finance workspace becoming
    // reachable only by a memorised URL after future sidebar reorganisations.
    const journalBatches = findByTitle(navigationItems, 'Journal Batches');

    expect(journalBatches).toMatchObject({
      href: '/finance/journal-batches',
      permissions: ['Finance.JournalBatches.View'],
    });
    expect(containsHref(sidebarNavigationItems, '/finance/journal-batches')).toBe(true);
  });

  it('keeps frequent operational records and reports in the sidebar while moving stable setup to settings', () => {
    expect(sidebarNavigationItems.some(item => item.title === 'Administration')).toBe(false);
    expect(sidebarNavigationItems.some(item => item.title === 'Notifications')).toBe(false);
    expect(sidebarNavigationItems.some(item => item.title === 'Reports')).toBe(true);
    expect(sidebarNavigationItems.some(item => item.title === 'Helpdesk')).toBe(true);
    expect(sidebarNavigationItems.some(item => item.title === 'Helpdesk & Complaints')).toBe(false);
    const sidebarTitles = sidebarNavigationItems.map(item => item.title);
    expect(sidebarTitles).toEqual(expect.arrayContaining([
      'Maintenance',
      'Fleet',
      'Projects',
      'Documents',
    ]));
    expect(sidebarTitles).not.toContain('Maintenance Mngt');
    expect(sidebarTitles).not.toContain('Fleet Management');
    expect(sidebarTitles).not.toContain('Project Mngt');
    expect(sidebarTitles).not.toContain('Document Mngt');

    expect(containsHref(sidebarNavigationItems, '/inventory/stock-movements')).toBe(true);
    expect(containsHref(sidebarNavigationItems, '/inventory/items')).toBe(true);
    expect(containsHref(sidebarNavigationItems, '/inventory/warehouse-items')).toBe(true);
    expect(containsHref(sidebarNavigationItems, '/procurement/business-partners')).toBe(true);
    expect(containsHref(sidebarNavigationItems, '/administration/procurement/supplier-risk')).toBe(true);
    expect(containsHref(sidebarNavigationItems, '/maintenance/assets')).toBe(true);
    expect(containsHref(sidebarNavigationItems, '/maintenance/fleet/drivers')).toBe(true);
    expect(containsHref(sidebarNavigationItems, '/finance/exchange-rates')).toBe(true);
    expect(containsHref(sidebarNavigationItems, '/finance/accounts')).toBe(false);
    expect(containsHref(sidebarNavigationItems, '/sales/journal-templates')).toBe(false);

    expect(findByTitle(sidebarNavigationItems, 'Items & Catalogue')?.children?.map(item => item.title)).toEqual([
      'Inventory Items',
      'Warehouse Items',
      'Item Identifiers',
      'Item Suppliers',
      'Price Lists',
    ]);
    expect(sidebarNavigationItems.find(item => item.title === 'Procurement')?.children?.map(item => item.title)).toEqual([
      'Supplier Management',
      'Purchasing',
      'Tendering',
      'Planning',
      // Procurement documents are now an operational record group on master,
      // so the surface contract must distinguish them from stable Settings data.
      'Procurement Documents',
      'Governance & Controls',
    ]);
    expect(findByTitle(sidebarNavigationItems, 'Supplier Management')?.children?.map(item => item.title)).toEqual([
      'Supplier Application Portal',
      'Business Partners',
      'Registrations',
      'Pending Partners',
      'Supplier Onboarding Tokens',
      'Supplier Applicant Access',
      'Supplier Evidence Packs',
      'Supplier Eligibility',
      'Supplier Due Diligence',
      'Approved Vendor List',
      'Supplier Risk & Concentration',
      'Supplier Performance',
      'Supplier Master Changes',
    ]);
    expect(containsHref(settingsNavigationItems, '/inventory/items')).toBe(false);
    expect(containsHref(settingsNavigationItems, '/procurement/business-partners')).toBe(false);
    expect(containsHref(settingsNavigationItems, '/administration/procurement/supplier-risk')).toBe(false);
    expect(containsHref(settingsNavigationItems, '/maintenance/assets')).toBe(false);
    expect(containsHref(settingsNavigationItems, '/maintenance/fleet/drivers')).toBe(false);
    expect(containsHref(settingsNavigationItems, '/finance/exchange-rates')).toBe(false);
    expect(containsHref(settingsNavigationItems, '/administration/inventory/units-of-measure')).toBe(true);
    expect(containsHref(settingsNavigationItems, '/finance/accounts')).toBe(true);
    expect(containsHref(settingsNavigationItems, '/sales/journal-templates')).toBe(true);
    expect(containsHref(settingsNavigationItems, '/notifications#center')).toBe(true);
    expect(containsHref(settingsNavigationItems, '/administration/audit-logs')).toBe(true);
  });
});
