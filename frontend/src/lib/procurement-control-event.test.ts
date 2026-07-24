import { describe, expect, it } from 'vitest';

import {
  compactProcurementControlEventSearch,
  formatProcurementControlJson,
  procurementControlEventResultTone,
  procurementControlLineage,
} from './procurement-control-event';

describe('procurement control-event presentation controls', () => {
  it('removes empty filters without dropping paging', () => {
    expect(
      compactProcurementControlEventSearch({
        search: '',
        eventType: undefined,
        page: 2,
        pageSize: 25,
      })
    ).toEqual({ page: 2, pageSize: 25 });
  });

  it('formats valid JSON and preserves non-JSON evidence', () => {
    expect(formatProcurementControlJson('{"allowed":true}')).toContain(
      '\n  "allowed": true\n'
    );
    expect(formatProcurementControlJson('signed digest')).toBe('signed digest');
    expect(formatProcurementControlJson()).toBe('No values recorded.');
  });

  it('makes adverse results visually distinct', () => {
    expect(procurementControlEventResultTone('Allowed')).toContain('emerald');
    expect(procurementControlEventResultTone('Denied')).toContain(
      'destructive'
    );
    expect(procurementControlEventResultTone('ReviewRequired')).toContain(
      'amber'
    );
  });

  it('renders rule and DEC lineage together', () => {
    expect(procurementControlLineage('SOD-01', '3', ['DEC-004'])).toBe(
      'SOD-01 v3 · DEC-004'
    );
  });
});
