import React from 'react';
import { cleanup, fireEvent, render, screen } from '@testing-library/react';
import { afterEach, expect, it, vi } from 'vitest';
import { QuantitySurveyPaymentCertificateDialog } from './QuantitySurveyPaymentCertificateDialog';
import { quantitySurveyPaymentCertificateService as service, type QuantitySurveyPaymentCertificate } from '@/services/quantity-survey-payment-certificate.service';
import type { CentralDocumentViewerFile } from '@/components/document-management/CentralDocumentViewerDialog';

const auth = vi.hoisted(() => ({ audit: true }));
vi.mock('@/hooks/use-auth', () => ({ useAuth: () => ({ hasPermission: (permission: string) => permission === 'quantity-survey.workspace.read' || (permission === 'quantity-survey.audit.read' && auth.audit) }) }));
vi.mock('@/services/quantity-survey-payment-certificate.service', () => ({ quantitySurveyPaymentCertificateService: { lookups: vi.fn(), list: vi.fn() } }));
vi.mock('@/components/document-management/CentralDocumentViewerDialog', () => ({ CentralDocumentViewerDialog: ({ file, open }: { file: CentralDocumentViewerFile | null; open: boolean }) => open ? <div role="region" aria-label="Central PDF viewer">{file?.repositoryPath}</div> : null }));
vi.mock('sonner', () => ({ toast: { success: vi.fn(), error: vi.fn() } }));
afterEach(() => { cleanup(); vi.clearAllMocks(); auth.audit = true; });

async function openCertificate(status = 'Approved') {
  const certificate: QuantitySurveyPaymentCertificate = {
    id: 'certificate-id', projectId: 'project-id', projectInterimValuationId: 'valuation-id',
    quantitySurveyValuationWorksheetId: 'worksheet-id', certificateNumber: 'IPC-UAT', title: 'UAT certificate',
    status, approvalStatus: status, issueDate: '2026-09-20', currency: 'GHS',
    certifiedToDateAmount: 10000, previouslyCertifiedAmount: 0, grossCertifiedAmount: 10000,
    retentionHeldAmount: 0, retentionReleasedAmount: 0, advanceRecoveryAmount: 0, materialDeductionAmount: 0,
    materialOnSiteAmount: 0, materialOffSiteAmount: 0, otherDeductionsAmount: 0, taxAmount: 2000,
    netCertifiedAmount: 12000, taxHandling: 'FinanceCalculated', apHandoffStatus: 'Created',
    paymentStatus: 'Draft', financePostingStatus: 'AwaitingFinancePosting', reconciliationStatus: 'Pending',
    documentGenerated: true, rowVersion: 'AAAA',
  };
  vi.mocked(service.list).mockResolvedValue([certificate]);
  vi.mocked(service.lookups).mockResolvedValue({ eligibleValuations: [], eligibleAdvanceRecoveries: [], approvedMaterialReconciliations: [] });
  render(<QuantitySurveyPaymentCertificateDialog projectId="project-id" />);
  fireEvent.click(screen.getByRole('button', { name: 'Payment certificate workspace' }));
  await screen.findByRole('button', { name: /IPC-UAT/ });
}

it('previews the selected approved certificate through the existing protected document endpoint', async () => {
  await openCertificate();
  fireEvent.click(screen.getByRole('button', { name: 'Preview PDF' }));
  expect(screen.getByText('/api/quantity-survey/payment-certificates/certificate-id/document')).toBeInTheDocument();
});

it('does not offer certificate preview without audit access', async () => {
  auth.audit = false;
  await openCertificate();
  expect(screen.queryByRole('button', { name: 'Preview PDF' })).not.toBeInTheDocument();
  expect(screen.queryByRole('button', { name: 'PDF' })).not.toBeInTheDocument();
});

it('does not offer PDF generation or preview for an unapproved certificate', async () => {
  await openCertificate('Draft');
  expect(screen.queryByRole('button', { name: 'Preview PDF' })).not.toBeInTheDocument();
  expect(screen.queryByRole('button', { name: 'PDF' })).not.toBeInTheDocument();
});
