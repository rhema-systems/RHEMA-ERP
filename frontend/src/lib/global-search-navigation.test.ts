import { describe, expect, it } from 'vitest';
import { type NavItem, filterNavigationByAccess, navigationItems } from '@/components/layout/sidebar';
import { buildGlobalSearchNavigation, searchGlobalSearchNavigation } from './global-search-navigation';

const icon = () => null;
const item = (title: string, href: string, options: Partial<NavItem> = {}): NavItem => ({ title, href, icon, ...options });
const allow = () => true;
const deny = () => false;

describe('global navigation search access', () => {
  it('honors denied ancestors, nested gates, and both role/permission access modes', () => {
    const nodes = [
      item('Hidden module', '/hidden', { permissions: ['hidden'], children: [item('Visible-looking child', '/hidden/child')] }),
      item('Finance', '/finance', { roles: ['Accountant'], permissions: ['finance.read'], accessMode: 'any', children: [
        item('Invoices', '/finance/invoices'),
        item('Approval', '/finance/approvals', { roles: ['Approver'], permissions: ['approve'] }),
        item('Settings', '/finance/settings', { navigationSurface: 'settings' }),
      ] }),
    ];
    const entries = buildGlobalSearchNavigation(roles => roles.includes('Accountant'), deny, nodes);
    expect(entries).toEqual([
      { title: 'Invoices', href: '/finance/invoices', module: 'Finance', breadcrumbs: ['Finance'] },
      { title: 'Settings', href: '/finance/settings', module: 'Finance', breadcrumbs: ['Finance'] },
    ]);
  });

  it('retains the real Employee inventory fallback without exposing Stores routes', () => {
    const entries = buildGlobalSearchNavigation(roles => roles.includes('Employee'), deny);
    expect(entries.filter(entry => entry.href.startsWith('/inventory'))).toEqual([
      { title: 'My requisitions', href: '/inventory/requisitions', module: 'Inventory', breadcrumbs: ['Inventory'] },
    ]);
    expect(buildGlobalSearchNavigation(deny, deny).some(entry => entry.href.startsWith('/inventory'))).toBe(false);
  });

  it('only returns leaves visible through the canonical navigation access filter', () => {
    const roles = (values: string[]) => values.includes('Employee');
    const permissions = (values: string[]) => values.includes('Finance.Read');
    const visible = new Set<string>();
    const collect = (nodes: NavItem[]) => nodes.forEach(node => node.children?.length ? collect(node.children) : visible.add(node.href));
    collect(filterNavigationByAccess(navigationItems, roles, permissions));
    const entries = buildGlobalSearchNavigation(roles, permissions);
    expect(entries.length).toBeGreaterThan(10);
    expect(entries.every(entry => visible.has(entry.href))).toBe(true);
    expect(entries.some(entry => entry.href === '/finance/accounts')).toBe(true);
  });

  it('deduplicates equivalent destinations while retaining distinct report filters', () => {
    const entries = buildGlobalSearchNavigation(allow, allow, [
      item('First', '/reports/?tab=aging&module=AP'),
      item('Duplicate', '/reports?module=AP&tab=aging#top'),
      item('Statements', '/reports?module=AP&tab=statements'),
    ]);
    expect(entries.map(entry => entry.title)).toEqual(['First', 'Statements']);
  });

  it('omits groups, actions, placeholders and external links', () => {
    const entries = buildGlobalSearchNavigation(allow, allow, [
      item('Group', '/not-a-real-page', { children: [item('Register', '/invoices')] }),
      ...['new', 'create', 'edit', 'delete', 'approve', 'reject', 'submit', 'post', 'cancel', 'upload', 'import']
        .map(action => item(action, `/invoices/${action}?id=1`)),
      item('Action query', '/invoices?mode=edit'),
      item('Placeholder', '#'),
      item('Dynamic', '/invoices/[id]'),
      item('External', 'https://example.com'),
      item('External shorthand', '//example.com'),
    ]);
    expect(entries.map(entry => entry.href)).toEqual(['/invoices']);
  });
});

describe('global navigation matching', () => {
  const entries = buildGlobalSearchNavigation(allow, allow, [
    item('Finance', '/finance', { children: [item('Invoices', '/finance/invoices'), item('Invoice Adjustments', '/finance/adjustments')] }),
    item('Procurement', '/procurement', { children: [item('Suppliers', '/procurement/suppliers'), item('Supplier Invoices', '/procurement/invoices')] }),
    item('Estate', '/estate', { children: [item('Café Tenants', '/estate/tenants')] }),
  ]);

  it('matches as typing and ranks direct labels before breadcrumb matches', () => {
    expect(searchGlobalSearchNavigation(entries, 'sup').map(entry => entry.title)).toEqual(['Suppliers', 'Supplier Invoices']);
    expect(searchGlobalSearchNavigation(entries, 'invoices').map(entry => entry.title)).toEqual(['Invoices', 'Supplier Invoices']);
    expect(searchGlobalSearchNavigation(entries, 'proc inv').map(entry => entry.title)).toEqual(['Supplier Invoices']);
  });

  it('matches case, spacing and accent differences, requiring every term', () => {
    expect(searchGlobalSearchNavigation(entries, '  CAFE   ten ')).toEqual([entries[4]]);
    expect(searchGlobalSearchNavigation(entries, 'finance suppliers')).toEqual([]);
    expect(searchGlobalSearchNavigation(entries, 'zzzzz')).toEqual([]);
  });

  it('limits results without mutating the index and does not suggest for blank input', () => {
    const snapshot = structuredClone(entries);
    expect(searchGlobalSearchNavigation(entries, 'inv', 1)).toHaveLength(1);
    expect(searchGlobalSearchNavigation(entries, '')).toEqual([]);
    expect(searchGlobalSearchNavigation(entries, '   --- ')).toEqual([]);
    expect(searchGlobalSearchNavigation(entries, 'inv', 0)).toEqual([]);
    expect(entries).toEqual(snapshot);
  });
});
