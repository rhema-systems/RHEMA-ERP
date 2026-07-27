import React from 'react';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

const mocks = vi.hoisted(() => ({
  replace: vi.fn(),
  getSessionToken: vi.fn(),
  portal: vi.fn(),
  paymentMethods: vi.fn(),
  updateApplication: vi.fn(),
  submit: vi.fn(),
  recordPayment: vi.fn(),
  uploadDocument: vi.fn(),
  clearSession: vi.fn(),
}));

vi.mock('next/navigation', () => ({
  useRouter: () => ({ replace: mocks.replace }),
}));

vi.mock('sonner', () => ({
  toast: {
    success: vi.fn(),
    error: vi.fn(),
  },
}));

vi.mock('@/services/procurement-supplier-applicant-access.service', () => ({
  supplierApplicantAccessService: {
    getSessionToken: mocks.getSessionToken,
    portal: mocks.portal,
    paymentMethods: mocks.paymentMethods,
    updateApplication: mocks.updateApplication,
    submit: mocks.submit,
    recordPayment: mocks.recordPayment,
    uploadDocument: mocks.uploadDocument,
    clearSession: mocks.clearSession,
  },
}));

import SupplierApplicantPortalPage from './page';

const portal = {
  registrationId: 'registration-1',
  tokenId: 'token-1',
  registrationNumber: 'REG-001',
  companyName: 'Supplier Ltd',
  email: 'supplier@example.test',
  phone: '+233200000001',
  partnerType: 'Supplier',
  registrationCategory: 'Goods' as const,
  status: 'Draft',
  registrationData: '{}',
  tokenStatus: 'Active',
  paymentStatus: 'Exempt',
  feeMode: 'Free',
  totalAmount: 0,
  currencyCode: 'GHS',
  tokenRowVersion: 'row-version',
  canEdit: true,
  canSubmit: true,
  paymentOnly: false,
  documents: [],
  statusHistory: [],
  evidenceReadiness: {
    registrationId: 'registration-1',
    category: 'Goods' as const,
    packVersionId: 'pack-1',
    packCode: 'SUPPLIER-GOODS',
    packVersion: 1,
    isBound: false,
    isReady: false,
    evaluatedAtUtc: '2026-07-27T20:00:00Z',
    blockingReasons: ['SUP-TAX: required evidence has not been uploaded'],
    requirements: [
      {
        requirementCode: 'SUP-TAX',
        name: 'Tax clearance certificate',
        isMandatory: true,
        isSatisfied: false,
        kind: 'DocumentAndClassification' as const,
        documentType: 'TaxClearance',
        classificationScheme: 'Tax status',
        allowedClassifications: ['Current', 'Conditional'],
        validityMode: 'MinimumRemainingDays' as const,
        minimumRemainingDays: 30,
        maxFileSizeBytes: 5 * 1024 * 1024,
        allowedMimeTypes: ['application/pdf'],
        approvalStepName: 'Compliance review',
        approvalStepOrder: 1,
        issues: ['required evidence has not been uploaded'],
      },
    ],
  },
};

describe('supplier applicant evidence uploads', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mocks.getSessionToken.mockReturnValue('restricted-session');
    mocks.portal.mockResolvedValue(portal);
    mocks.uploadDocument.mockResolvedValue({});
  });

  it('binds the selected evidence requirement and its validation metadata', async () => {
    render(<SupplierApplicantPortalPage />);

    await screen.findByText('REG-001');
    const documentsTab = screen.getByRole('tab', { name: 'Documents' });
    fireEvent.mouseDown(documentsTab, { button: 0, ctrlKey: false });
    fireEvent.click(documentsTab);

    const requirement = await screen.findByLabelText('Evidence requirement');
    await waitFor(() => expect(requirement).toHaveValue('SUP-TAX'));
    expect(screen.getByText('TaxClearance')).toBeInTheDocument();

    fireEvent.change(screen.getByLabelText('Tax status classification'), {
      target: { value: 'Current' },
    });
    fireEvent.change(screen.getByLabelText('Issue date'), {
      target: { value: '2026-07-01' },
    });
    fireEvent.change(screen.getByLabelText('Expiry date'), {
      target: { value: '2027-07-01' },
    });
    const evidence = new File(['clean evidence'], 'tax-clearance.pdf', {
      type: 'application/pdf',
    });
    fireEvent.change(screen.getByLabelText('Evidence file'), {
      target: { files: [evidence] },
    });
    const uploadButton = screen.getByRole('button', { name: 'Upload evidence' });
    await waitFor(() => expect(uploadButton).toBeEnabled());
    fireEvent.click(uploadButton);

    await waitFor(() => expect(mocks.uploadDocument).toHaveBeenCalledTimes(1));
    const form = mocks.uploadDocument.mock.calls[0][0] as FormData;
    expect(form.get('file')).toBe(evidence);
    expect(form.get('documentType')).toBe('TaxClearance');
    expect(form.get('evidenceRequirementCode')).toBe('SUP-TAX');
    expect(form.get('classificationCode')).toBe('Current');
    expect(form.get('issueDate')).toBe('2026-07-01');
    expect(form.get('expiryDate')).toBe('2027-07-01');
  });
});
