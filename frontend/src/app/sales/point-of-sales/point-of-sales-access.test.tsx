import React, { type ReactNode } from 'react';
import { cleanup, render, screen } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';

vi.stubGlobal('React', React);

const guardCalls = vi.hoisted(() => [] as Array<{ requiredPermissions?: string[]; accessMode?: 'all' | 'any'; permissionMode?: 'all' | 'any' }>);

vi.mock('@/components/auth/auth-guard', () => ({
  AuthGuard: ({ children, requiredPermissions, accessMode, permissionMode }: { children: ReactNode; requiredPermissions?: string[]; accessMode?: 'all' | 'any'; permissionMode?: 'all' | 'any' }) => {
    guardCalls.push({ requiredPermissions, accessMode, permissionMode });
    return children;
  },
}));

vi.mock('@/components/mobile-pos/MobilePosOperations', () => ({
  MobilePosDayEndWorkspace: () => <div>Day End workspace</div>,
  MobilePosTillReportWorkspace: () => <div>Till Report workspace</div>,
  MobilePosDevicesWorkspace: () => <div>Devices workspace</div>,
}));

import MobilePosDayEndPage from './day-end/page';
import MobilePosDevicesPage from './devices/page';
import MobilePosTillReportPage from './till-report/page';

describe('Point of Sales operational route access', () => {
  afterEach(() => {
    cleanup();
    guardCalls.length = 0;
  });

  it('requires independent Mobile POS and Finance review permissions for Day End', () => {
    render(<MobilePosDayEndPage />);
    expect(screen.getByText('Day End workspace')).toBeTruthy();
    expect(guardCalls).toEqual([{ requiredPermissions: ['MobilePOS.Till.Review', 'Finance.CashTills.Closures.Review'], accessMode: undefined, permissionMode: 'all' }]);
  });

  it('requires report and Finance review permissions for Till Report', () => {
    render(<MobilePosTillReportPage />);
    expect(screen.getByText('Till Report workspace')).toBeTruthy();
    expect(guardCalls).toEqual([{ requiredPermissions: ['MobilePOS.Reports.View', 'Finance.CashTills.Closures.Review'], accessMode: undefined, permissionMode: 'all' }]);
  });

  it('requires the dynamic device approval permission for Devices', () => {
    render(<MobilePosDevicesPage />);
    expect(screen.getByText('Devices workspace')).toBeTruthy();
    expect(guardCalls).toEqual([{ requiredPermissions: ['MobilePOS.Device.Approve'], accessMode: undefined, permissionMode: undefined }]);
  });
});
