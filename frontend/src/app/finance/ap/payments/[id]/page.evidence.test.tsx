import React from 'react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';

const state = vi.hoisted(() => ({
    payment: {} as Record<string, unknown>, control: {} as Record<string, unknown>,
    controlError: null as unknown, permission: vi.fn(), toast: vi.fn(), request: vi.fn(), download: vi.fn(),
    uploadWorkflow: vi.fn(), verify: vi.fn(), refetchControl: vi.fn(), submit: vi.fn(), post: vi.fn(),
}));
vi.mock('next/navigation', () => ({ useParams: () => ({ id: 'payment-1' }), useRouter: () => ({ push: vi.fn() }) }));
vi.mock('@tanstack/react-query', () => ({
    useQuery: (options: { queryKey: string[] }) => ({
        data: options.queryKey[0] === 'vendor-payment' ? state.payment
            : options.queryKey[0] === 'vendor-payment-control' ? state.control
                : options.queryKey[0] === 'finance-settings' ? { minimumReversalReasonLength: 20 } : undefined,
        isLoading: false,
        refetch: options.queryKey[0] === 'vendor-payment-control' ? state.refetchControl : vi.fn(),
        error: options.queryKey[0] === 'vendor-payment-control' ? state.controlError : null,
    }),
}));
vi.mock('@/hooks/use-auth', () => ({ useAuth: () => ({ user: { id: 'operator' }, hasPermission: state.permission }) }));
vi.mock('@/components/ui/use-toast', () => ({ useToast: () => ({ toast: state.toast }) }));
vi.mock('@/services/api.service', () => ({ apiService: { request: state.request, downloadBlob: state.download } }));
vi.mock('@/services/accountsPayableService', () => ({ accountsPayableService: { submitPayment: state.submit, postPayment: state.post } }));
vi.mock('@/services/finance/finance-data.service', () => ({ financeDataService: {} }));
vi.mock('@/services/workflow-api.service', () => ({ workflowApiService: { uploadStepAttachment: state.uploadWorkflow, verifyWorkflowEvidence: state.verify } }));
vi.mock('@/services/document-output.service', () => ({ DOCUMENT_TYPES: {}, documentOutputService: {} }));
vi.mock('@/components/finance/ap/SupplierDebitNoteApplicationsCard', () => ({ SupplierDebitNoteApplicationsCard: () => null }));
vi.mock('@/components/finance/InvoicePaymentSodControl', () => ({ InvoicePaymentSodControl: () => null }));

import Page from './page';

const document = (uploadedById = 'operator', id = 'evidence-1') => ({
    id, attachmentId: id, requirementKey: 'payment-support', fileName: `${id}.pdf`, documentName: 'Payment support',
    verificationStatus: 'Pending', malwareScanStatus: 'Clean', uploadedById, uploadedAt: '2026-09-12', sha256: 'a'.repeat(64),
});

beforeEach(() => {
    vi.clearAllMocks();
    state.permission.mockReturnValue(true);
    state.request.mockResolvedValue({ success: true });
    state.uploadWorkflow.mockResolvedValue({});
    state.verify.mockResolvedValue(undefined);
    state.refetchControl.mockResolvedValue({});
    state.controlError = null;
    state.payment = {
        id: 'payment-1', paymentNumber: 'VP-1', supplierId: 'supplier-1', supplierName: 'Supplier',
        paymentDate: '2026-09-12', createdAt: '2026-09-12', status: 'Draft', approvalRequired: true,
        totalAmount: 100, allocatedAmount: 100, unallocatedAmount: 0, currencyCode: 'GHS', exchangeRate: 1,
        withholdingTaxAmount: 0, withholdingTaxRate: 0, discountTaken: 0, allocations: [],
    };
    state.control = {
        approvalRequired: false, canUploadEvidence: true, canSubmit: false, evidenceRequirementsSatisfied: false,
        evidenceRequirements: [{ requirementKey: 'payment-support', documentName: 'Payment support', documentType: 'PDF',
            minimumDocuments: 1, requireVerification: false, currentDocumentCount: 0, verifiedDocumentCount: 0, isSatisfied: false }],
        evidenceDocuments: [], blockingReasons: ['Attach a payment support document.'],
    };
});
afterEach(() => { cleanup(); vi.restoreAllMocks(); vi.unstubAllGlobals(); });

describe('payment-bound evidence without approval', () => {
    it.each([false, undefined])('does not offer upload without an explicit bank-operate capability (%s)', (capability) => {
        state.control.canUploadEvidence = capability;
        render(<Page />);
        expect(screen.queryByLabelText('Upload Payment support')).not.toBeInTheDocument();
        expect(state.request).not.toHaveBeenCalled();
    });

    it('uploads the required attachment on a draft without a workflow step, reviewer or payment submission', async () => {
        state.permission.mockImplementation((permission: string) => permission !== 'Finance.Workflow.Approve');
        render(<Page />);
        const file = new File(['invoice'], 'invoice.pdf', { type: 'application/pdf' });
        fireEvent.change(screen.getByLabelText('Upload Payment support'), { target: { files: [file] } });
        await waitFor(() => expect(state.request).toHaveBeenCalledTimes(1));
        const [path, options] = state.request.mock.calls[0];
        expect(path).toBe('/ap/payments/payment-1/evidence');
        expect(options.method).toBe('POST');
        expect(options.body).toBeInstanceOf(FormData);
        expect(options.body.get('file')).toBe(file);
        expect(options.body.get('requirementKey')).toBe('payment-support');
        expect(options.body.get('clientRequestId')).toMatch(/^[0-9a-f-]{36}$/i);
        expect(options.body.has('verifiedById')).toBe(false);
        expect(options.body.has('stepInstanceId')).toBe(false);
        expect(state.uploadWorkflow).not.toHaveBeenCalled();
        expect(state.verify).not.toHaveBeenCalled();
        expect(state.submit).not.toHaveBeenCalled();
        expect(state.post).not.toHaveBeenCalled();
        await waitFor(() => expect(state.refetchControl).toHaveBeenCalled());
        expect(state.toast).toHaveBeenCalledWith(expect.objectContaining({ description: 'invoice.pdf is attached. Verification is not required.' }));
    });

    it('shows attachment counts and Not required for both own and other uploads, without any human review action', () => {
        state.control.evidenceDocuments = [document(), document('another-user', 'evidence-2')];
        render(<Page />);
        expect(screen.getByText('0 of 1 attached · Verification: Not required · PDF')).toBeVisible();
        expect(screen.queryByRole('button', { name: 'Verify evidence' })).not.toBeInTheDocument();
        expect(screen.queryByText('Requires another reviewer')).not.toBeInTheDocument();
        expect(screen.queryByText(/independently verified/)).not.toBeInTheDocument();
        expect(screen.getByText('Attach a payment support document.')).toBeVisible();
        expect(screen.getByRole('button', { name: 'Download evidence-1.pdf' })).toBeVisible();
    });

    it('preserves the active workflow upload and independent verification path', async () => {
        state.payment.status = 'PendingAuthorization';
        state.control.approvalRequired = true;
        state.control.currentStepInstanceId = 'step-1';
        state.control.evidenceDocuments = [document(), document('another-user', 'evidence-2')];
        render(<Page />);
        const file = new File(['proof'], 'proof.pdf', { type: 'application/pdf' });
        fireEvent.change(screen.getByLabelText('Upload Payment support'), { target: { files: [file] } });
        await waitFor(() => expect(state.uploadWorkflow).toHaveBeenCalledWith('step-1', file, 'payment-support', 'Payment support', 'PDF'));
        expect(state.request).not.toHaveBeenCalled();
        fireEvent.click(screen.getByRole('button', { name: 'Verify evidence' }));
        await waitFor(() => expect(state.verify).toHaveBeenCalledWith('evidence-2', true, 'Reviewed against the AP payment control requirement.'));
        expect(screen.getByText('Requires another reviewer')).toBeVisible();
        expect(screen.queryByText(/Verification: Not required/)).not.toBeInTheDocument();
    });

    it('does not expose the direct upload or verification when the policy decision is unknown', () => {
        delete state.control.approvalRequired;
        state.control.evidenceDocuments = [document('another-user')];
        render(<Page />);
        expect(screen.queryByLabelText('Upload Payment support')).not.toBeInTheDocument();
        expect(screen.queryByRole('button', { name: 'Verify evidence' })).not.toBeInTheDocument();
        expect(screen.getByRole('alert')).toHaveTextContent('Payment policy could not be confirmed.');
        fireEvent.click(screen.getByRole('button', { name: 'Retry policy check' }));
        expect(state.refetchControl).toHaveBeenCalledTimes(1);
    });

    it('fails closed on a failed policy refresh even if a previous response opted out', () => {
        state.controlError = new Error('Policy is unavailable.');
        render(<Page />);
        expect(screen.queryByLabelText('Upload Payment support')).not.toBeInTheDocument();
        expect(screen.getByRole('alert')).toHaveTextContent('Policy is unavailable.');
    });

    it('requires the existing payment-processing permission for draft uploads', () => {
        state.permission.mockImplementation((permission: string) => permission !== 'Finance.AP.Payments.Process');
        render(<Page />);
        expect(screen.queryByLabelText('Upload Payment support')).not.toBeInTheDocument();
    });

    it('does not expose draft attachment mutation after completion or on payment-batch records', () => {
        state.payment.status = 'Authorized';
        state.payment.approvalRequired = false;
        const { rerender } = render(<Page />);
        expect(screen.queryByLabelText('Upload Payment support')).not.toBeInTheDocument();
        state.payment.status = 'Draft';
        state.payment.paymentBatchId = 'batch-1';
        rerender(<Page />);
        expect(screen.queryByLabelText('Upload Payment support')).not.toBeInTheDocument();
    });

    it('retains saved documents and retries a failed upload with the same client request ID', async () => {
        state.control.evidenceDocuments = [document()];
        state.request.mockRejectedValueOnce({ response: { detail: 'The scan did not complete.', code: 'FILE_VIRUS_SCAN_INCOMPLETE' } });
        render(<Page />);
        const file = new File(['evidence'], 'retry.pdf', { type: 'application/pdf', lastModified: 123 });
        const input = screen.getByLabelText('Upload Payment support');
        fireEvent.change(input, { target: { files: [file] } });
        await waitFor(() => expect(state.toast).toHaveBeenCalledWith(expect.objectContaining({ description: 'The scan did not complete. (FILE_VIRUS_SCAN_INCOMPLETE)', variant: 'destructive' })));
        expect(screen.getByText('evidence-1.pdf')).toBeVisible();
        await waitFor(() => expect(input).not.toBeDisabled());
        fireEvent.change(input, { target: { files: [file] } });
        await waitFor(() => expect(state.request).toHaveBeenCalledTimes(2));
        expect(state.request.mock.calls[1][1].body.get('clientRequestId')).toBe(state.request.mock.calls[0][1].body.get('clientRequestId'));
        expect(state.verify).not.toHaveBeenCalled();
    });

    it('downloads evidence through the authenticated payment-bound content endpoint', async () => {
        state.control.evidenceDocuments = [document()];
        const blob = new Blob(['evidence'], { type: 'application/pdf' });
        state.download.mockResolvedValue(blob);
        const createObjectURL = vi.fn(() => 'blob:payment-evidence');
        const revokeObjectURL = vi.fn();
        vi.stubGlobal('URL', { createObjectURL, revokeObjectURL });
        const click = vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(() => undefined);
        render(<Page />);
        fireEvent.click(screen.getByRole('button', { name: 'Download evidence-1.pdf' }));
        await waitFor(() => expect(state.download).toHaveBeenCalledWith('/ap/payments/payment-1/evidence/evidence-1/content'));
        await waitFor(() => expect(click).toHaveBeenCalledTimes(1));
        expect(createObjectURL).toHaveBeenCalledWith(blob);
        expect(revokeObjectURL).toHaveBeenCalledWith('blob:payment-evidence');
        expect(state.verify).not.toHaveBeenCalled();
    });
});
