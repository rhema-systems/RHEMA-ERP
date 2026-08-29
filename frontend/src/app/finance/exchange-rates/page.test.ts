import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { describe, expect, it } from 'vitest';

const pageSource = readFileSync(resolve(process.cwd(), 'src/app/finance/exchange-rates/page.tsx'), 'utf8');

describe('Exchange Rates retained-data boundary', () => {
  it('never substitutes sample rates when the API register is unavailable', () => {
    expect(pageSource).not.toContain('MOCK_EXCHANGE_RATES');
    expect(pageSource).toContain('useState<ExchangeRate[]>([])');
    expect(pageSource).toContain('Exchange-rate register unavailable');
    expect(pageSource).toContain('No sample rates are displayed');
    expect(pageSource).toContain('setRates([])');
  });

  it('blocks mutation controls while retained rate evidence is loading or unavailable', () => {
    expect(pageSource).toContain('disabled={isLoadingRates || Boolean(rateLoadError)}');
    expect(pageSource).toContain('setRateReloadToken((current) => current + 1)');
  });
});
