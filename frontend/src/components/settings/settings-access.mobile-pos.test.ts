import { describe, expect, it } from 'vitest';
import { canAccessSettingsItem, resolveSettingsAccessRule } from './settings-access';

const subject = { href: '/administration/mobile-pos' };

describe('Mobile POS settings access', () => {
  it('is governed only by dynamic Mobile POS permissions', () => {
    expect(resolveSettingsAccessRule(subject)).toEqual({
      permissions: [
        'MobilePOS.Store.View',
        'MobilePOS.Store.Manage',
        'MobilePOS.Device.Approve',
      ],
      roles: undefined,
      accessMode: undefined,
    });
  });

  it('admits any configured Mobile POS permission and denies role name alone', () => {
    expect(canAccessSettingsItem(subject, {
      hasAnyRole: () => false,
      hasAnyPermission: permissions => permissions.includes('MobilePOS.Store.View'),
    })).toBe(true);
    expect(canAccessSettingsItem(subject, {
      hasAnyRole: () => true,
      hasAnyPermission: () => false,
    })).toBe(false);
  });
});
