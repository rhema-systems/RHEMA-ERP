import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { describe, expect, it } from 'vitest';

const readSource = (relativePath: string) =>
  readFileSync(resolve(process.cwd(), relativePath), 'utf8');

describe('Supplier Returns Finance quarantine', () => {
  const listSource = readSource('src/app/finance/ap/returns/page.tsx');
  const createSource = readSource('src/app/finance/ap/returns/create/page.tsx');
  const detailSource = readSource('src/app/finance/ap/returns/[id]/page.tsx');

  it('keeps legacy Finance pages read-only until the producer contracts exist', () => {
    expect(listSource).not.toContain('/finance/ap/returns/create');
    expect(createSource).not.toContain('createSupplierReturn');
    expect(detailSource).not.toContain('approveSupplierReturn');
    expect(createSource).toContain('FIN-INT-012');
    expect(createSource).toContain('FIN-INT-013');
    expect(detailSource).toContain(
      'No approve, post, or inventory action is available'
    );
  });

  it('uses tenant Finance Settings for base-currency labels and query isolation', () => {
    expect(listSource).toContain('financeDataService.getFinanceSettings()');
    expect(detailSource).toContain('financeDataService.getFinanceSettings()');
    expect(listSource).toContain("currentTenantCode, 'list'");
    expect(detailSource).toContain("currentTenantCode, 'detail', id");
    expect(listSource).not.toMatch(/formatCurrency\([^\n]+,\s*['\"]GHS['\"]\)/);
    expect(detailSource).not.toMatch(
      /formatCurrency\([^\n]+,\s*['\"]GHS['\"]\)/
    );
  });
});
