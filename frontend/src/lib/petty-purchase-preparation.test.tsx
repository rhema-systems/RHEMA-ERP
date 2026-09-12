import React from 'react';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import Page from '@/app/procurement/petty-purchases/new/page';

const mocks = vi.hoisted(() => ({ push: vi.fn(), get: vi.fn(), create: vi.fn() }));
vi.mock('next/navigation', () => ({ useRouter: () => ({ push: mocks.push }), useSearchParams: () => new URLSearchParams('fromRequisitionId=pr-1') }));
vi.mock('@/services/purchasingService', () => ({ purchasingService: { getPurchaseRequisitionById: mocks.get } }));
vi.mock('@/services/tenderService', () => ({ tenderService: { createTender: mocks.create } }));
vi.mock('sonner', () => ({ toast: { error: vi.fn() } }));

beforeEach(() => {
  vi.clearAllMocks();
  mocks.get.mockResolvedValue({ id: 'pr-1', requisitionNumber: 'PR-0002', status: 'Approved', currency: 'GHS',
    totalAmount: 750, justification: 'Local UAT only', items: [{ id: 'line', itemDescription: 'Kit', quantity: 1, unitOfMeasure: 'EA' }] });
  mocks.create.mockResolvedValue({ id: 'source-1' });
});

describe('Petty Purchase preparation page', () => {
  it('creates a quotation preparation source with no bidding or evaluation configuration', async () => {
    render(<Page/>);
    fireEvent.click(await screen.findByRole('button', { name: 'Continue to supplier and quotation' }));
    expect(screen.getByRole('dialog')).toBeInTheDocument();
    expect(mocks.create).not.toHaveBeenCalled();
    fireEvent.click(screen.getByRole('button', { name: 'Create preparation draft' }));
    await waitFor(() => expect(mocks.push).toHaveBeenCalledWith('/procurement/tenders/source-1/exception-controls'));
    expect(mocks.create).toHaveBeenCalledTimes(1);
    expect(mocks.create.mock.calls[0][0]).toMatchObject({ sourcePurchaseRequisitionId: 'pr-1', tenderType: 'PettyPurchase', currency: 'GHS', estimatedValue: 750 });
    expect(mocks.create.mock.calls[0][0]).not.toHaveProperty('submissionDeadline');
    expect(mocks.create.mock.calls[0][0]).not.toHaveProperty('evaluationTemplateId');
  });
  it('retains the dialog and input context on a source-policy error', async () => {
    mocks.create.mockRejectedValue(new Error('PETTY_METHOD_MISMATCH: locked method differs'));
    render(<Page/>);
    fireEvent.click(await screen.findByRole('button', { name: 'Continue to supplier and quotation' }));
    fireEvent.click(screen.getByRole('button', { name: 'Create preparation draft' }));
    expect(await screen.findByRole('alert')).toHaveTextContent('PETTY_METHOD_MISMATCH');
    expect(screen.getByRole('dialog')).toBeInTheDocument();
    expect(mocks.push).not.toHaveBeenCalled();
  });
});
