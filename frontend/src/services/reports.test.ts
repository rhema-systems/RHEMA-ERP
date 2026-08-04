import { describe, expect, it } from 'vitest';

import { normalizeReportExportFileName } from './reports';

describe('normalizeReportExportFileName', () => {
  it('prefers and decodes the RFC 5987 filename', () => {
    const header = "attachment; filename=Balance_Register.xlsx; filename*=UTF-8''Balance%20Register.xlsx";

    expect(normalizeReportExportFileName(header, 'xlsx')).toBe('Balance Register.xlsx');
  });

  it('stops an unquoted filename at the content-disposition separator', () => {
    const header = "attachment; filename=Movement_Register.csv; filename*=UTF-8''Movement_Register.csv";

    expect(normalizeReportExportFileName(header, 'csv')).toBe('Movement_Register.csv');
  });

  it('removes characters appended after the requested extension', () => {
    expect(normalizeReportExportFileName('attachment; filename="Count_Variance.xlsx_"', 'xlsx'))
      .toBe('Count_Variance.xlsx');
    expect(normalizeReportExportFileName('attachment; filename="Expiry_Register.csv_"', 'csv'))
      .toBe('Expiry_Register.csv');
  });

  it('adds the requested extension when the header omits it', () => {
    expect(normalizeReportExportFileName('attachment; filename="Disposal Register"', 'pdf'))
      .toBe('Disposal Register.pdf');
  });

  it('creates a fallback with the exact requested extension', () => {
    expect(normalizeReportExportFileName(null, 'csv')).toMatch(/^report-\d{4}-\d{2}-\d{2}\.csv$/);
  });
});
