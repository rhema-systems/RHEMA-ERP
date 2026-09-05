import React from 'react';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

const mocks = vi.hoisted(() => ({
  router: { replace: vi.fn() },
  getSessionToken: vi.fn(),
  portal: vi.fn(),
  paymentMethods: vi.fn(),
  updateApplication: vi.fn(),
  submit: vi.fn(),
  recordPayment: vi.fn(),
  uploadDocument: vi.fn(),
  deleteDocument: vi.fn(),
  clearSession: vi.fn(),
}));

vi.mock('next/navigation', () => ({
  useRouter: () => mocks.router,
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
    deleteDocument: mocks.deleteDocument,
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
    mocks.updateApplication.mockResolvedValue(portal);
    mocks.submit.mockResolvedValue({ ...portal, status: 'Submitted' });
    mocks.uploadDocument.mockResolvedValue({});
    mocks.deleteDocument.mockResolvedValue(undefined);
  });

  it('keeps the submit action visible above every portal tab', async () => {
    render(<SupplierApplicantPortalPage />);

    await screen.findByText('REG-001');
    expect(screen.getByText('Next: open Application.')).toBeInTheDocument();
    expect(
      screen.getByText(/add the supplier's contact persons and bank accounts/i)
    ).toBeInTheDocument();
    const submitButton = screen.getByRole('button', {
      name: 'Submit for review',
    });
    expect(submitButton).toBeVisible();
    expect(
      screen.getAllByRole('button', { name: 'Submit for review' })
    ).toHaveLength(1);

    for (const tabName of ['Documents', 'Status', 'Payment']) {
      const tab = screen.getByRole('tab', { name: tabName });
      fireEvent.mouseDown(tab, { button: 0, ctrlKey: false });
      fireEvent.click(tab);
      expect(submitButton).toBeVisible();
    }

    fireEvent.click(submitButton);
    await waitFor(() => expect(mocks.submit).toHaveBeenCalledTimes(1));
  });

  it('rehydrates legacy wrapped application data and preserves fields it does not edit', async () => {
    const legacyData = JSON.stringify({
      RegistrationData: JSON.stringify({
        taxNumber: 'TAX-LEGACY-001',
        physicalAddress: '14 Independence Avenue',
        city: 'Accra',
        country: 'Ghana',
        contactPersonName: 'Retained Contact',
        licenses: [{ licenseNumber: 'LIC-001' }],
      }),
    });
    mocks.portal.mockResolvedValue({ ...portal, registrationData: legacyData });
    mocks.updateApplication.mockImplementation(async (request) => ({
      ...portal,
      registrationData: request.registrationData,
    }));

    render(<SupplierApplicantPortalPage />);

    await screen.findByText('REG-001');
    const applicationTab = screen.getByRole('tab', { name: 'Application' });
    fireEvent.mouseDown(applicationTab, { button: 0, ctrlKey: false });
    fireEvent.click(applicationTab);
    expect(
      await screen.findByDisplayValue('TAX-LEGACY-001')
    ).toBeInTheDocument();
    expect(
      screen.getByDisplayValue('14 Independence Avenue')
    ).toBeInTheDocument();
    expect(screen.getByDisplayValue('Accra')).toBeInTheDocument();

    fireEvent.change(screen.getByDisplayValue('TAX-LEGACY-001'), {
      target: { value: 'TAX-UPDATED-002' },
    });
    fireEvent.click(screen.getByRole('button', { name: 'Save application' }));

    await waitFor(() =>
      expect(mocks.updateApplication).toHaveBeenCalledTimes(1)
    );
    const request = mocks.updateApplication.mock.calls[0][0];
    const saved = JSON.parse(request.registrationData);
    expect(saved.taxNumber).toBe('TAX-UPDATED-002');
    expect(saved.contactPersonName).toBe('Retained Contact');
    expect(saved.licenses).toEqual([{ licenseNumber: 'LIC-001' }]);
    expect(saved.RegistrationData).toBeUndefined();
  });

  it('captures and serializes repeatable contacts and bank accounts with one primary each', async () => {
    mocks.updateApplication.mockImplementation(async (request) => ({
      ...portal,
      registrationData: request.registrationData,
    }));

    render(<SupplierApplicantPortalPage />);

    await screen.findByText('REG-001');
    const applicationTab = screen.getByRole('tab', { name: 'Application' });
    fireEvent.mouseDown(applicationTab, { button: 0, ctrlKey: false });
    fireEvent.click(applicationTab);

    expect(
      screen.getByText(
        /add all relevant contact persons and bank accounts below/i
      )
    ).toBeInTheDocument();

    fireEvent.click(screen.getByRole('button', { name: 'Add contact' }));
    fireEvent.click(screen.getByRole('button', { name: 'Add contact' }));
    const contactNames = screen.getAllByLabelText('Full name');
    fireEvent.change(contactNames[0], { target: { value: 'Primary Buyer' } });
    fireEvent.change(contactNames[1], {
      target: { value: 'Accounts Officer' },
    });
    fireEvent.click(screen.getAllByLabelText('Primary contact')[1]);

    fireEvent.click(screen.getByRole('button', { name: 'Add bank account' }));
    fireEvent.click(screen.getByRole('button', { name: 'Add bank account' }));
    const bankNames = screen.getAllByLabelText('Bank name');
    const accountNumbers = screen.getAllByLabelText('Account number');
    fireEvent.change(bankNames[0], { target: { value: 'First Bank' } });
    fireEvent.change(accountNumbers[0], { target: { value: '11110000' } });
    fireEvent.change(bankNames[1], { target: { value: 'Second Bank' } });
    fireEvent.change(accountNumbers[1], { target: { value: '22220000' } });
    fireEvent.click(screen.getAllByLabelText('Primary bank account')[1]);

    fireEvent.click(screen.getByRole('button', { name: 'Save application' }));

    await waitFor(() =>
      expect(mocks.updateApplication).toHaveBeenCalledTimes(1)
    );
    const saved = JSON.parse(
      mocks.updateApplication.mock.calls[0][0].registrationData
    );
    expect(saved.contacts).toHaveLength(2);
    expect(
      saved.contacts.filter(
        (contact: { isPrimary: boolean }) => contact.isPrimary
      )
    ).toHaveLength(1);
    expect(saved.contacts[1]).toMatchObject({
      contactName: 'Accounts Officer',
      isPrimary: true,
    });
    expect(saved.bankAccounts).toHaveLength(2);
    expect(
      saved.bankAccounts.filter(
        (account: { isPrimary: boolean }) => account.isPrimary
      )
    ).toHaveLength(1);
    expect(saved.bankAccounts[1]).toMatchObject({
      bankName: 'Second Bank',
      accountNumber: '22220000',
      isPrimary: true,
    });
    expect(saved.contactPersonName).toBe('Accounts Officer');
    expect(saved.bankAccountNumber).toBe('22220000');
  });

  it('removes repeatable rows and reassigns the primary selection', async () => {
    render(<SupplierApplicantPortalPage />);

    await screen.findByText('REG-001');
    const applicationTab = screen.getByRole('tab', { name: 'Application' });
    fireEvent.mouseDown(applicationTab, { button: 0, ctrlKey: false });
    fireEvent.click(applicationTab);

    fireEvent.click(screen.getByRole('button', { name: 'Add contact' }));
    fireEvent.click(screen.getByRole('button', { name: 'Add contact' }));
    fireEvent.click(screen.getByRole('button', { name: 'Remove contact 1' }));
    expect(screen.getAllByLabelText('Full name')).toHaveLength(1);
    expect(screen.getByLabelText('Primary contact')).toBeChecked();

    fireEvent.click(screen.getByRole('button', { name: 'Add bank account' }));
    fireEvent.click(screen.getByRole('button', { name: 'Add bank account' }));
    fireEvent.click(
      screen.getByRole('button', { name: 'Remove bank account 1' })
    );
    expect(screen.getAllByLabelText('Bank name')).toHaveLength(1);
    expect(screen.getByLabelText('Primary bank account')).toBeChecked();
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
    const uploadButton = screen.getByRole('button', {
      name: 'Upload evidence',
    });
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

  it('lets the applicant confirm and delete an uploaded document while the application is editable', async () => {
    const uploadedDocument = {
      id: 'document-1',
      documentName: 'tax-clearance.pdf',
      documentType: 'TaxClearance',
      fileSize: 2048,
      isVerified: false,
      isRejected: false,
      evidenceRequirementCode: 'SUP-TAX',
    };
    mocks.portal
      .mockResolvedValueOnce({ ...portal, documents: [uploadedDocument] })
      .mockResolvedValueOnce(portal);

    render(<SupplierApplicantPortalPage />);

    await screen.findByText('REG-001');
    const documentsTab = screen.getByRole('tab', { name: 'Documents' });
    fireEvent.mouseDown(documentsTab, { button: 0, ctrlKey: false });
    fireEvent.click(documentsTab);
    fireEvent.click(
      await screen.findByRole('button', {
        name: 'Delete tax-clearance.pdf',
      })
    );

    expect(
      screen.getByRole('heading', { name: 'Delete uploaded document?' })
    ).toBeVisible();
    fireEvent.click(screen.getByRole('button', { name: 'Delete document' }));

    await waitFor(() =>
      expect(mocks.deleteDocument).toHaveBeenCalledWith('document-1')
    );
    await waitFor(() => expect(mocks.portal).toHaveBeenCalledTimes(2));
  });

  it('presents payment first and locks every downstream tab until verification', async () => {
    mocks.portal.mockReset();
    mocks.portal.mockResolvedValue({
      ...portal,
      tokenStatus: 'AwaitingPayment',
      paymentStatus: 'Pending',
      feeMode: 'Paid',
      totalAmount: 100,
      canEdit: false,
      canSubmit: false,
      paymentOnly: true,
    });
    mocks.paymentMethods.mockReset();
    mocks.paymentMethods.mockResolvedValue([
      {
        id: 'method-1',
        code: 'MOMO',
        name: 'Mobile Money',
        requiresReference: true,
        isPostingReady: true,
      },
    ]);

    render(<SupplierApplicantPortalPage />);

    await screen.findByText(/status tracking are unlocked/i);
    const tabs = screen.getAllByRole('tab');
    expect(tabs.map((tab) => tab.textContent)).toEqual([
      'Payment',
      'Application',
      'Documents',
      'Status',
    ]);
    expect(screen.getByRole('tab', { name: 'Payment' })).toHaveAttribute(
      'data-state',
      'active'
    );
    expect(screen.getByRole('tab', { name: 'Application' })).toBeDisabled();
    expect(screen.getByRole('tab', { name: 'Documents' })).toBeDisabled();
    expect(screen.getByRole('tab', { name: 'Status' })).toBeDisabled();
  });

  it('returns to token login after a payment is recorded', async () => {
    mocks.portal.mockReset();
    mocks.portal.mockResolvedValue({
      ...portal,
      tokenStatus: 'AwaitingPayment',
      paymentStatus: 'Pending',
      feeMode: 'Paid',
      totalAmount: 100,
      canEdit: false,
      canSubmit: false,
      paymentOnly: true,
    });
    mocks.paymentMethods.mockResolvedValue([
      {
        id: 'method-1',
        code: 'MOMO',
        name: 'Mobile Money',
        requiresReference: true,
        isPostingReady: true,
      },
    ]);
    mocks.recordPayment.mockResolvedValue({});

    render(<SupplierApplicantPortalPage />);

    const button = await screen.findByRole('button', {
      name: 'Record configured payment',
    });
    await waitFor(() => expect(button).toBeEnabled());
    fireEvent.click(button);

    await waitFor(() => expect(mocks.recordPayment).toHaveBeenCalledTimes(1));
    expect(mocks.clearSession).toHaveBeenCalledTimes(1);
    expect(mocks.router.replace).toHaveBeenCalledWith(
      '/supplier-application?tab=login&payment=pending'
    );
  });
});
