import { describe, expect, it } from 'vitest';
import {
    FINANCE_READ_PERMISSION,
    MANAGE_CODING_DIMENSIONS_PERMISSION,
    getDimensionSettingsAccess,
} from './dimension-settings-access';

describe('transaction dimension settings access', () => {
    it('requires Finance.Read before the catalog can be browsed', () => {
        const permissions = new Set([MANAGE_CODING_DIMENSIONS_PERMISSION]);
        expect(getDimensionSettingsAccess(permission => permissions.has(permission))).toEqual({
            canRead: false,
            canManage: false,
        });
    });

    it('treats dimension management as an additional action permission', () => {
        const readOnly = new Set([FINANCE_READ_PERMISSION]);
        expect(getDimensionSettingsAccess(permission => readOnly.has(permission))).toEqual({
            canRead: true,
            canManage: false,
        });

        readOnly.add(MANAGE_CODING_DIMENSIONS_PERMISSION);
        expect(getDimensionSettingsAccess(permission => readOnly.has(permission))).toEqual({
            canRead: true,
            canManage: true,
        });
    });
});
