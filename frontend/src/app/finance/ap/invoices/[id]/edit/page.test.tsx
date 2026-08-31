import React from 'react';
import { render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

vi.mock('next/navigation', () => ({
    useParams: () => ({ id: 'invoice-123' }),
}));

vi.mock('../../create/page', () => ({
    VendorInvoiceFormPage: ({ editInvoiceId }: { editInvoiceId?: string }) => (
        <div>Editing {editInvoiceId}</div>
    ),
}));

import EditVendorInvoicePage from './page';

describe('EditVendorInvoicePage', () => {
    it('passes the dynamic invoice id to the reusable controlled form', () => {
        render(<EditVendorInvoicePage />);

        expect(screen.getByText('Editing invoice-123')).toBeInTheDocument();
    });
});
