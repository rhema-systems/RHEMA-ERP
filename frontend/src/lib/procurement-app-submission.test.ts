import { describe, expect, it } from 'vitest';

import {
  compactProcurementAppSubmissionSearch,
  procurementAppSubmissionActions,
  procurementAppSubmissionStatusTone,
  validateProcurementAppExport,
} from './procurement-app-submission';

describe('procurement APP submission presentation controls', () => {
  it('enforces the external submission lifecycle', () => {
    expect(
      procurementAppSubmissionActions({ status: 'Exported' })
    ).toMatchObject({
      canSubmit: true,
      canAcknowledge: false,
      canReject: false,
      canResubmit: false,
    });
    expect(
      procurementAppSubmissionActions({ status: 'Submitted' })
    ).toMatchObject({
      canSubmit: false,
      canAcknowledge: true,
      canReject: true,
    });
    expect(
      procurementAppSubmissionActions({ status: 'Rejected' }).canResubmit
    ).toBe(true);
    expect(
      procurementAppSubmissionActions({ status: 'Acknowledged' }).isTerminal
    ).toBe(true);
  });

  it('validates the server-generated export format', () => {
    const value = {
      procurementPlanId: 'plan-1',
      exportFormat: 'CSV',
    };
    expect(validateProcurementAppExport(value)).toBeUndefined();
    expect(
      validateProcurementAppExport({ ...value, exportFormat: 'XLSX' })
    ).toContain('CSV');
  });

  it('removes empty filters while retaining paging', () => {
    expect(
      compactProcurementAppSubmissionSearch({
        search: '',
        status: undefined,
        page: 2,
        pageSize: 25,
      })
    ).toEqual({ page: 2, pageSize: 25 });
  });

  it('gives external outcomes distinct visual tones', () => {
    expect(procurementAppSubmissionStatusTone('Acknowledged')).toContain(
      'emerald'
    );
    expect(procurementAppSubmissionStatusTone('Rejected')).toContain('red');
    expect(procurementAppSubmissionStatusTone('Submitted')).toContain('blue');
  });
});
