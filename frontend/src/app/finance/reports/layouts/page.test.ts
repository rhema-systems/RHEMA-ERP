import { readFileSync } from 'node:fs';
import { join } from 'node:path';
import { describe, expect, it } from 'vitest';
import {
    FINANCIAL_STATEMENT_LAYOUT_PERMISSIONS,
    resolveFinancialStatementLayoutPermissions,
} from './permissions';

const source = readFileSync(join(process.cwd(), 'src/app/finance/reports/layouts/page.tsx'), 'utf8');

describe('financial statement layout workspace authorization contract', () => {
    it.each([
        { name: 'none', granted: [], expected: [false, false, false, false] },
        { name: 'manage only', granted: ['manage'], expected: [false, false, false, false] },
        { name: 'publish only', granted: ['publish'], expected: [false, false, false, false] },
        { name: 'run only', granted: ['run'], expected: [false, false, false, false] },
        { name: 'read only', granted: ['read'], expected: [true, false, false, false] },
        { name: 'read and manage', granted: ['read', 'manage'], expected: [true, true, false, false] },
        { name: 'read and publish', granted: ['read', 'publish'], expected: [true, false, true, false] },
        { name: 'read and run', granted: ['read', 'run'], expected: [true, false, false, true] },
        { name: 'all', granted: ['read', 'manage', 'publish', 'run'], expected: [true, true, true, true] },
    ])('requires Finance.Read before action permission: $name', ({ granted, expected }) => {
        const permissionValues = new Set<string>(granted.map(
            (key) => FINANCIAL_STATEMENT_LAYOUT_PERMISSIONS[key as keyof typeof FINANCIAL_STATEMENT_LAYOUT_PERMISSIONS],
        ));
        const result = resolveFinancialStatementLayoutPermissions((permission) => permissionValues.has(permission));
        expect([result.canRead, result.canManage, result.canPublish, result.canRun]).toEqual(expected);
    });

    it('gates loading and exports behind read while retaining distinct action gates', () => {
        expect(source).toContain('if (!canRead)');
        expect(source).toContain('Action permissions do not grant read access.');
        expect(source).toContain('canRun && !layout.isProtectedStandard');
        expect(source).toContain('canManage && !layout.isProtectedStandard');
        expect(source).toContain("canManage && !layout.isProtectedStandard && selectedVersion?.status === 'Draft'");
    });

    it('keeps protected standards clone-only and classification editing draft-only', () => {
        expect(source).toContain('Protected standard · clone only');
        expect(source).toContain('layout.isProtectedStandard && canManage');
        expect(source).toContain("selectedVersion.status === 'Draft' && row.rowType === 'Account'");
        expect(source).toContain('Add classification mapping');
        expect(source).toContain('Design draft rows');
        expect(source).toContain('setValidation(validationFromError(error))');
    });
});
