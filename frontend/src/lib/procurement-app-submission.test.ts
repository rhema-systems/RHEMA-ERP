import { describe, expect, it } from 'vitest';

import {
  compactProcurementAppSubmissionSearch,
  procurementAppSubmissionActions,
  procurementAppSubmissionStatusTone,
  readProcurementAppExportFile,
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

  it('validates immutable export package metadata', () => {
    const value = {
      procurementPlanId: 'plan-1',
      exportFileName: 'app.xlsx',
      exportFormat: 'XLSX',
      exportTemplateVersion: 'PPA-v1',
      exportChecksumSha256: 'A'.repeat(64),
    };
    expect(validateProcurementAppExport(value)).toBeUndefined();
    expect(
      validateProcurementAppExport({ ...value, exportChecksumSha256: 'bad' })
    ).toContain('calculated automatically');
  });

  it('derives package metadata and SHA-256 instead of asking a user for technical values', async () => {
    const file = Object.assign(new Blob(['TDC APP export']), {
      name: 'TDC-APP-2026.xlsx',
    });

    await expect(readProcurementAppExportFile(file)).resolves.toEqual({
      exportFileName: 'TDC-APP-2026.xlsx',
      exportFormat: 'XLSX',
      exportChecksumSha256:
        'bf72df96f3b5c4aab148cbc9ea7fecc2bedc076b7c2caa78e18611a6488813de',
    });
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
