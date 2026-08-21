import { describe, expect, it } from 'vitest';
import { readFileSync } from 'node:fs';
import { join } from 'node:path';
import {
  apAgingReportQueryKey,
  apCashRequirementsQueryKey,
} from './ap-report-query-keys';

describe('AP report query keys', () => {
  it('partitions Aging state by tenant and selected date', () => {
    expect(apAgingReportQueryKey('FINANCE-DEMO', '2025-01-31')).not.toEqual(
      apAgingReportQueryKey('DEFAULT', '2025-01-31')
    );
    expect(apAgingReportQueryKey('FINANCE-DEMO', '2025-01-31')).not.toEqual(
      apAgingReportQueryKey('FINANCE-DEMO', '2026-01-31')
    );
  });

  it('partitions Cash Requirements state by tenant and selected date', () => {
    expect(apCashRequirementsQueryKey('FINANCE-DEMO', '2025-01-31')).toEqual([
      'finance',
      'ap',
      'reports',
      'cash-requirements',
      'FINANCE-DEMO',
      '2025-01-31',
    ]);
  });

  it('uses controlled ISO date inputs and exposes only the supported Aging export', () => {
    const pageSource = readFileSync(
      join(process.cwd(), 'src', 'app', 'finance', 'ap', 'reports', 'page.tsx'),
      'utf8'
    );

    expect(pageSource).toContain('aria-label="AP aging as-of date"');
    expect(pageSource).toContain('aria-label="Cash requirements as-of date"');
    expect(pageSource).toContain('<ApAgingExportButton');
    expect(pageSource).not.toContain('@/components/ui/calendar');
    expect(pageSource).not.toContain('<Button variant="outline" size="icon">');
  });
});
