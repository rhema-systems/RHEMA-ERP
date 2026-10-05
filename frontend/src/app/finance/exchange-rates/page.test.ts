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

  it('shows governed schedule status and prevents approved evidence from being edited', () => {
    expect(pageSource).toContain('Effective From');
    expect(pageSource).toContain('Effective To');
    expect(pageSource).toContain('rate.approvalStatus');
    expect(pageSource).toContain("rate.approvalStatus !== 'Pending' && rate.approvalStatus !== 'Rejected'");
    expect(pageSource).toContain('Approved accounting evidence is immutable');
    expect(pageSource).toContain('sourceReference');
  });

  it('labels average rates as reporting and valuation evidence', () => {
    expect(pageSource).toContain('Average — reporting/valuation only');
  });

  it('prevents duplicate create and update submissions while showing progress', () => {
    expect(pageSource).toContain('if (isCreatingRate) return');
    expect(pageSource).toContain('disabled={isCreatingRate}');
    expect(pageSource).toContain("isCreatingRate ? 'Creating Rate…' : 'Create Rate'");
    expect(pageSource).toContain('if (!editingRate || isUpdatingRate) return');
    expect(pageSource).toContain('disabled={isUpdatingRate}');
    expect(pageSource).toContain("isUpdatingRate ? 'Updating Rate…' : 'Update Rate'");
  });

  it('controls the edit dialog so Cancel closes it and resets the form', () => {
    expect(pageSource).toContain('open={editingRate?.id === rate.id}');
    expect(pageSource).toContain('if (!open) closeEditDialog()');
    expect(pageSource).toContain('onClick={closeEditDialog}');
    expect(pageSource).toContain('setEditingRate(null)');
    expect(pageSource).toContain('resetForm()');
  });
});
