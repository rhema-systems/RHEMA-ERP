import { describe, expect, it } from 'vitest';
import {
    FINANCE_READ_PERMISSION,
    RUN_FX_REVALUATION_PERMISSION,
    getFxRevaluationAccess,
} from './access';

function access(...permissions: string[]) {
    const granted = new Set(permissions);
    return getFxRevaluationAccess(permission => granted.has(permission));
}

describe('FX revaluation permission matrix', () => {
    it.each([
        { permissions: [], canRead: false, canRun: false },
        { permissions: [RUN_FX_REVALUATION_PERMISSION], canRead: false, canRun: false },
        { permissions: [FINANCE_READ_PERMISSION], canRead: true, canRun: false },
        { permissions: [FINANCE_READ_PERMISSION, RUN_FX_REVALUATION_PERMISSION], canRead: true, canRun: true },
    ])('maps $permissions to read=$canRead run=$canRun', ({ permissions, canRead, canRun }) => {
        expect(access(...permissions)).toEqual({ canRead, canRun });
    });
});
