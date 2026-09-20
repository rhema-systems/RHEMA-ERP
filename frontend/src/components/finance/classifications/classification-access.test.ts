import { describe, expect, it, vi } from 'vitest';
import { canConfigureClassifications, MANAGE_CHART_OF_ACCOUNTS_PERMISSION } from './classification-access';

describe('classification administration access', () => {
    it('uses the canonical chart-of-accounts management permission', () => {
        const hasPermission = vi.fn((permission: string) => permission === MANAGE_CHART_OF_ACCOUNTS_PERMISSION);
        expect(canConfigureClassifications(hasPermission)).toBe(true);
        expect(hasPermission).toHaveBeenCalledWith('Finance.ChartOfAccounts.Manage');
    });

    it('keeps mutation controls unavailable to a read-only user', () => {
        expect(canConfigureClassifications(() => false)).toBe(false);
    });
});
