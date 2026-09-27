import React from 'react';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { arService } from '@/services/ar-service';
import { ArAgingExportButton } from './ArAgingExportButton';

const { toastMock } = vi.hoisted(() => ({ toastMock: vi.fn() }));

vi.mock('@/components/ui/use-toast', () => ({
    useToast: () => ({ toast: toastMock }),
}));

describe('ArAgingExportButton', () => {
    const createObjectUrlMock = vi.fn(() => 'blob:ar-aging');
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
        vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(function (this: HTMLAnchorElement) {
            downloadedFileName = this.download;
        });
    });

    afterEach(() => vi.restoreAllMocks());

    it('downloads the visible as-of date with a deterministic filename', async () => {
        const csv = new Blob(['Customer,Outstanding'], { type: 'text/csv' });
        vi.spyOn(arService, 'downloadAgingReportCsv').mockResolvedValueOnce(csv);
        render(<ArAgingExportButton asOfDate="2026-07-31" isReportLoading={false} />);

        fireEvent.click(screen.getByRole('button', { name: 'Export AR aging CSV' }));

        await waitFor(() => expect(revokeObjectUrlMock).toHaveBeenCalledWith('blob:ar-aging'));
        expect(arService.downloadAgingReportCsv).toHaveBeenCalledWith('2026-07-31');
        expect(createObjectUrlMock).toHaveBeenCalledWith(csv);
        expect(downloadedFileName).toBe('ar-aging-2026-07-31.csv');
        expect(toastMock).toHaveBeenCalledWith({
            title: 'AR aging export ready',
            description: 'Downloaded ar-aging-2026-07-31.csv.',
        });
    });

    it('reports export failures and restores the button', async () => {
        vi.spyOn(arService, 'downloadAgingReportCsv')
            .mockRejectedValueOnce(new Error('Forbidden'));
        render(<ArAgingExportButton asOfDate="2026-07-31" isReportLoading={false} />);

        fireEvent.click(screen.getByRole('button', { name: 'Export AR aging CSV' }));

        await waitFor(() => expect(toastMock).toHaveBeenCalledWith({
            title: 'AR aging export failed',
            description: 'Forbidden',
            variant: 'destructive',
        }));
        expect(screen.getByRole('button', { name: 'Export AR aging CSV' })).toBeEnabled();
    });
});
