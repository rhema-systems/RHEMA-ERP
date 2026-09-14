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

    it('saves rate settings and revaluation treatment as visibly independent operations', () => {
        const rateHandler = source.slice(
            source.indexOf('const handleSaveRatePolicy'),
            source.indexOf('const handleSaveRevaluationPolicy'),
        );
        const revaluationHandler = source.slice(
            source.indexOf('const handleSaveRevaluationPolicy'),
            source.indexOf('const openPolicyDecision'),
        );

        expect(rateHandler).toContain('updateAccountCurrencyLinkRatePolicy');
        expect(rateHandler).not.toContain('saveAccountBookCurrencyPolicy');
        expect(rateHandler).toContain('Rate settings not saved');
        expect(rateHandler).toContain('await loadAccountData(false)');
        expect(revaluationHandler).toContain('saveAccountBookCurrencyPolicy');
        expect(revaluationHandler).not.toContain('updateAccountCurrencyLinkRatePolicy');
        expect(revaluationHandler).toContain('Revaluation treatment not saved');
        expect(revaluationHandler).toContain('await loadAccountData(false)');
        expect(source).toContain('Save revaluation treatment');
        expect(source).toContain('Save rate settings');
    });
});
