import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { describe, expect, it } from 'vitest';

const source = readFileSync(resolve(process.cwd(), 'src/app/finance/accounts/[id]/page.tsx'), 'utf8');

describe('account FX revaluation policy governance', () => {
    it('uses distinct override and approval permissions', () => {
        expect(source).toContain("hasPermission('Finance.FX.Policy.Override')");
        expect(source).toContain("hasPermission('Finance.FX.Policy.Approve')");
        expect(source).toContain('disabled={!link.isActive || !canOverrideFxPolicy}');
        expect(source).toContain('canApproveFxPolicy && policy.id');
    });

    it('offers inherit/include/exclude without an account-wide monetary Boolean', () => {
        expect(source).toContain('Inherit classification default');
        expect(source).toContain('Include in closing revaluation');
        expect(source).toContain('Exclude from closing revaluation');
        expect(source).toContain('policy.accountingBookName');
        expect(source).toContain('editingLink?.linkedCurrencyCode');
    });

    it('makes nonstandard inclusion and pending approval conspicuous', () => {
        expect(source).toContain('Non-standard revaluation policy');
        expect(source).toContain('Explicit confirmation required');
        expect(source).toContain('Pending independent approval');
        expect(source).toContain('confirmNonstandardInclusion: confirmNonstandard');
    });
});
