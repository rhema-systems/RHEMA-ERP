import React from 'react';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { ReportPdfActions } from './ReportPdfActions';

const toastError = vi.fn();

vi.mock('sonner', () => ({
    toast: { error: (...args: unknown[]) => toastError(...args) },
}));

describe('ReportPdfActions', () => {
    beforeEach(() => toastError.mockReset());

    it('uses distinct download and print callbacks for the same named report', async () => {
        const onDownloadPdf = vi.fn().mockResolvedValue(undefined);
        const onPrint = vi.fn().mockResolvedValue(undefined);
        render(
            <ReportPdfActions
                reportName="AR aging report"
                onDownloadPdf={onDownloadPdf}
                onPrint={onPrint}
            />
        );

        fireEvent.click(screen.getByRole('button', { name: 'Download AR aging report PDF' }));
        await waitFor(() => expect(onDownloadPdf).toHaveBeenCalledTimes(1));

        fireEvent.click(screen.getByRole('button', { name: 'Print AR aging report' }));
        await waitFor(() => expect(onPrint).toHaveBeenCalledTimes(1));
    });

    it('contains failures and restores both actions', async () => {
        render(
            <ReportPdfActions
                reportName="AP cash requirements forecast"
                onDownloadPdf={vi.fn().mockRejectedValue(new Error('network'))}
                onPrint={vi.fn()}
            />
        );

        fireEvent.click(screen.getByRole('button', { name: 'Download AP cash requirements forecast PDF' }));

        await waitFor(() => expect(toastError).toHaveBeenCalledWith(
            'Unable to download AP cash requirements forecast.'
        ));
        expect(screen.getByRole('button', { name: 'Print AP cash requirements forecast' })).toBeEnabled();
    });
});
