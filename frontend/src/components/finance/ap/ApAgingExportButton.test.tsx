import React from 'react';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { accountsPayableService } from '@/services/accountsPayableService';
import { ApAgingExportButton } from './ApAgingExportButton';

const { toastMock } = vi.hoisted(() => ({ toastMock: vi.fn() }));

vi.mock('@/components/ui/use-toast', () => ({
  useToast: () => ({ toast: toastMock }),
}));

describe('ApAgingExportButton', () => {
  const createObjectUrlMock = vi.fn(() => 'blob:ap-aging');
  const revokeObjectUrlMock = vi.fn();
  let downloadedFileName = '';

  beforeEach(() => {
    vi.clearAllMocks();
    downloadedFileName = '';
    Object.defineProperty(window.URL, 'createObjectURL', {
      configurable: true,
      value: createObjectUrlMock,
    });
    Object.defineProperty(window.URL, 'revokeObjectURL', {
      configurable: true,
      value: revokeObjectUrlMock,
    });
    vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(
      function () {
        downloadedFileName = this.download;
      }
    );
  });

  afterEach(() => vi.restoreAllMocks());

  it('is disabled while the visible report is loading', () => {
    render(<ApAgingExportButton asOfDate="2026-07-31" isReportLoading />);

    expect(
      screen.getByRole('button', { name: 'Export AP aging CSV' })
    ).toBeDisabled();
  });

  it('exports the selected date, downloads a deterministic filename, and cleans up', async () => {
    const csv = new Blob(['Supplier,Total'], { type: 'text/csv' });
    vi.spyOn(
      accountsPayableService,
      'downloadAgingReportCsv'
    ).mockResolvedValueOnce(csv);
    render(
      <ApAgingExportButton asOfDate="2026-07-31" isReportLoading={false} />
    );

    fireEvent.click(
      screen.getByRole('button', { name: 'Export AP aging CSV' })
    );

    await waitFor(() =>
      expect(revokeObjectUrlMock).toHaveBeenCalledWith('blob:ap-aging')
    );
    expect(accountsPayableService.downloadAgingReportCsv).toHaveBeenCalledWith(
      '2026-07-31'
    );
    expect(createObjectUrlMock).toHaveBeenCalledWith(csv);
    expect(downloadedFileName).toBe('ap-aging-2026-07-31.csv');
    expect(toastMock).toHaveBeenCalledWith({
      title: 'AP aging export ready',
      description: 'Downloaded ap-aging-2026-07-31.csv.',
    });
  });

  it('recovers and shows a destructive toast when the export fails', async () => {
    vi.spyOn(
      accountsPayableService,
      'downloadAgingReportCsv'
    ).mockRejectedValueOnce(new Error('Export service unavailable'));
    render(
      <ApAgingExportButton asOfDate="2026-07-31" isReportLoading={false} />
    );

    fireEvent.click(
      screen.getByRole('button', { name: 'Export AP aging CSV' })
    );

    await waitFor(() =>
      expect(toastMock).toHaveBeenCalledWith({
        title: 'AP aging export failed',
        description: 'Export service unavailable',
        variant: 'destructive',
      })
    );
    expect(createObjectUrlMock).not.toHaveBeenCalled();
    expect(
      screen.getByRole('button', { name: 'Export AP aging CSV' })
    ).toBeEnabled();
  });
});
