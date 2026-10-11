import { describe, expect, it } from 'vitest';

import { filterNavigationByAccess, navigationItems, type NavItem } from './sidebar';

function findByTitle(items: NavItem[], title: string): NavItem | undefined {
  for (const item of items) {
    if (item.title === title) return item;
    const nested = findByTitle(item.children ?? [], title);
    if (nested) return nested;
  }
  return undefined;
}

describe('Sales Point of Sales navigation', () => {
  const sales = navigationItems.find(item => item.title === 'Sales');
  const pointOfSales = sales ? findByTitle([sales], 'Point of Sales') : undefined;

  it('exposes exactly the three operational pages requested under Sales', () => {
    expect(pointOfSales).toMatchObject({
      href: '/sales/point-of-sales/day-end',
      permissions: ['MobilePOS.Till.Review', 'MobilePOS.Reports.View', 'MobilePOS.Device.Approve'],
    });
    expect(pointOfSales?.children?.map(item => [item.title, item.href])).toEqual([
      ['Day End', '/sales/point-of-sales/day-end'],
      ['Till Report', '/sales/point-of-sales/till-report'],
      ['Devices', '/sales/point-of-sales/devices'],
    ]);
  });

  it.each([
    [['MobilePOS.Till.Review', 'Finance.CashTills.Closures.Review'], 'Day End'],
    [['MobilePOS.Reports.View', 'Finance.CashTills.Closures.Review'], 'Till Report'],
    ['MobilePOS.Device.Approve', 'Devices'],
  ])('shows only the permitted Point of Sales child for %s', (permission, expectedTitle) => {
    const permissions = Array.isArray(permission) ? permission : [permission];
    const filtered = filterNavigationByAccess(
      pointOfSales ? [pointOfSales] : [],
      () => false,
      requested => requested.some(item => permissions.includes(item)),
    );

    expect(filtered).toHaveLength(1);
    expect(filtered[0].children?.map(item => item.title)).toEqual([expectedTitle]);
  });

  it('does not expose Finance governed pages when only the Mobile POS permission is assigned', () => {
    const filtered = filterNavigationByAccess(
      pointOfSales ? [pointOfSales] : [],
      () => false,
      requested => requested.includes('MobilePOS.Till.Review'),
    );

    expect(filtered).toHaveLength(0);
  });
});
