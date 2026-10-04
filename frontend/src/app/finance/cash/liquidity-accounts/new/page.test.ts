import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { describe, expect, it } from 'vitest';

const source = readFileSync(resolve(process.cwd(), 'src/app/finance/cash/liquidity-accounts/new/page.tsx'), 'utf8');

describe('new liquidity account currency field', () => {
    it('limits currencies to the selected GL account and keeps the GL selector first', () => {
        expect(source).toContain("financeDataService.getCurrencies({ isActive: true })");
        expect(source).toContain('financeDataService.getAccountCurrencyLinks(account.id)');
        expect(source).toContain('supportedCurrencyCodes.includes(currency.currencyCode.trim().toUpperCase())');
        expect(source.indexOf('<Label>GL control account</Label>')).toBeLessThan(source.indexOf('<Label>Currency</Label>'));
        expect(source).toContain('{currency.currencyCode} — {currency.currencyName}');
        expect(source).not.toMatch(/<Input[^>]+value=\{form\.currency\}/);
    });

    it('derives the GL account and currency from a selected bank master', () => {
        expect(source).toContain("glAccountId: bank?.glAccountId ?? ''");
        expect(source).toContain("currency: bank?.currency.trim().toUpperCase() ?? ''");
        expect(source).toContain("disabled={form.accountType === 'Bank'}");
    });
});
