import React from 'react';
import { act, cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import type { ContractDto } from '@/services/contractService';

const mocks = vi.hoisted(() => ({ getContract: vi.fn(), download: vi.fn(), previewDownload: vi.fn(), deleteDocument: vi.fn(), error: vi.fn(), success: vi.fn(), canManage: false }));
vi.mock('next/navigation', () => ({ useParams: () => ({ id: 'contract-1' }), useRouter: () => ({ push: vi.fn() }), useSearchParams: () => new URLSearchParams() }));
vi.mock('@/hooks/use-auth', () => ({ useAuth: () => ({ hasPermission: (permission: string) => permission === 'procurement.records.read' || (mocks.canManage && permission === 'procurement.contract.manage') }) }));
vi.mock('sonner', () => ({ toast: { error: mocks.error, success: mocks.success } }));
vi.mock('@/services/api.service', () => ({ apiService: { downloadBlob: mocks.previewDownload } }));
vi.mock('next/dynamic', () => ({ default: () => (props: { fileData?: Uint8Array; enableAnnotations?: boolean }) =>
  <div role="region" aria-label="PDF content" data-annotations={String(props.enableAnnotations)}>{props.fileData?.length} authorized bytes</div> }));
vi.mock('@/services/contractService', () => ({ contractService: { getContractById: mocks.getContract, deleteDocument: mocks.deleteDocument } }));
vi.mock('@/services/procurement-document-management.service', () => ({ procurementDocumentManagementService: { download: mocks.download } }));
vi.mock('@/components/procurement/ContractActivationGate', () => ({ ContractActivationGate: () => null }));
vi.mock('@/components/procurement/ContractOperationsDashboard', () => ({ ContractOperationsDashboard: () => null }));
vi.mock('@/components/procurement/WorksCloseoutWorkspace', () => ({ WorksCloseoutWorkspace: () => null }));
vi.mock('@/components/quantity-survey/QuantitySurveyContractCommercialTermsPanel', () => ({ QuantitySurveyContractCommercialTermsPanel: () => null }));
import Page from './page';

let contract: ContractDto;
let opened: { href: string; target: string; rel: string; download: string }[];
const createObjectURL = vi.fn(() => 'blob:protected-contract');
const revokeObjectURL = vi.fn();

beforeEach(() => {
  vi.clearAllMocks();
  vi.stubGlobal('React', React);
  Object.defineProperty(URL, 'createObjectURL', { configurable: true, value: createObjectURL });
  Object.defineProperty(URL, 'revokeObjectURL', { configurable: true, value: revokeObjectURL });
  opened = [];
  vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(function (this: HTMLAnchorElement) {
    opened.push({ href: this.href, target: this.target, rel: this.rel, download: this.download });
  });
  mocks.canManage = false;
  contract = {
    id: 'contract-1', contractNumber: 'CTR-2026-00002', contractTitle: 'Supply contract', contractType: 'Supply',
    status: 'Active', tenderAwardId: 'award-1', tenderId: 'tender-1', tenderNumber: 'T-1', tenderTitle: 'Tender',
    businessPartnerId: 'supplier-1', businessPartnerName: 'Harbourline', contractValue: 52000, currency: 'GHS',
    retentionPercentage: 0, createdAt: '2026-09-13', rowVersion: 'version-1', milestones: [], amendments: [],
    documents: [{ id: 'document-1', contractId: 'contract-1', documentType: 'SignedCopy', fileName: 'Signed contract.pdf',
      filePath: 'https://untrusted.invalid/not-a-download', fileUploadRecordId: 'upload-1',
      centralDocumentRecordId: 'record-1', centralDocumentVersionId: 'version-1', contentType: 'application/pdf',
      fileSize: 100, createdAt: '2026-09-13' }],
    totalPaidAmount: 0, remainingAmount: 52000, completedMilestones: 0, totalMilestones: 0,
  };
  mocks.getContract.mockImplementation(async () => contract);
  mocks.download.mockReset();
  mocks.download.mockResolvedValue(new Blob(['%PDF-protected'], { type: 'application/pdf' }));
  mocks.previewDownload.mockReset();
  mocks.previewDownload.mockResolvedValue({ arrayBuffer: async () => new Uint8Array([37, 80, 68, 70]).buffer });
});

afterEach(() => { cleanup(); vi.restoreAllMocks(); vi.unstubAllGlobals(); });

async function showDocuments() {
  render(<Page />);
  const tab = await screen.findByRole('tab', { name: 'Documents (1)' });
  fireEvent.mouseDown(tab, { button: 0, ctrlKey: false });
  return screen.findByRole('button', { name: `Open ${contract.documents[0].fileName}` });
}

describe('Contract protected document opening', () => {
  it('lets a contract reader open the exact saved DMS version without manage access or public paths', async () => {
    const open = await showDocuments();
    expect(open).toBeEnabled();
    expect(open).not.toHaveTextContent('Open');
    expect(screen.queryByRole('button', { name: 'Delete Signed contract.pdf' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Upload Document' })).not.toBeInTheDocument();
    fireEvent.click(open);
    expect(await screen.findByRole('dialog', { name: 'Signed contract.pdf' })).toBeVisible();
    expect(await screen.findByRole('region', { name: 'PDF content' })).toHaveTextContent('4 authorized bytes');
    expect(mocks.previewDownload).toHaveBeenCalledExactlyOnceWith('/procurement/document-management/records/record-1/versions/version-1/download');
    expect(screen.getByRole('region', { name: 'PDF content' })).toHaveAttribute('data-annotations', 'false');
    expect(opened).toHaveLength(0);
    expect(createObjectURL).not.toHaveBeenCalled();
    expect(mocks.download).not.toHaveBeenCalled();
    expect(mocks.deleteDocument).not.toHaveBeenCalled();
  });

  it.each([
    { detail: 'You cannot read this procurement document.', code: 'PROCUREMENT_DOCUMENT_FORBIDDEN' },
    { detail: 'The current centralized malware result does not permit retrieval.', code: 'PROCUREMENT_DMS_MALWARE_NOT_CLEAN' },
  ])('keeps the document and exposes the protected route error for retry: $code', async problem => {
    contract.documents[0].fileName = 'Signed contract.docx';
    contract.documents[0].contentType = 'application/vnd.openxmlformats-officedocument.wordprocessingml.document';
    mocks.download.mockRejectedValueOnce({ response: { data: problem } });
    const open = await showDocuments();
    fireEvent.click(open);
    expect(await screen.findByRole('alert')).toHaveTextContent(`${problem.detail} (${problem.code})`);
    expect(open).toBeEnabled();
    expect(opened).toHaveLength(0);
    expect(createObjectURL).not.toHaveBeenCalled();
    expect(mocks.deleteDocument).not.toHaveBeenCalled();
    fireEvent.click(open);
    await waitFor(() => expect(opened).toHaveLength(1));
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });

  it('does not fall back to a legacy public path or upload ID without a protected DMS version', async () => {
    contract.documents[0].centralDocumentVersionId = undefined;
    const open = await showDocuments();
    expect(open).toBeDisabled();
    expect(open).toHaveAttribute('title', 'Protected document link unavailable');
    fireEvent.click(open);
    expect(mocks.download).not.toHaveBeenCalled();
    expect(opened).toHaveLength(0);
  });

  it('blocks repeated clicks while the protected file is loading', async () => {
    contract.documents[0].fileName = 'Signed contract.docx';
    contract.documents[0].contentType = 'application/vnd.openxmlformats-officedocument.wordprocessingml.document';
    let finish!: (blob: Blob) => void;
    mocks.download.mockImplementationOnce(() => new Promise<Blob>(resolve => { finish = resolve; }));
    const open = await showDocuments();
    fireEvent.click(open);
    expect(open).toBeDisabled();
    fireEvent.click(open);
    expect(mocks.download).toHaveBeenCalledTimes(1);
    await act(async () => finish(new Blob(['file'], { type: 'application/pdf' })));
    expect(open).toBeEnabled();
    expect(opened).toHaveLength(1);
  });

  it('downloads non-previewable content as an attachment instead of opening executable content', async () => {
    contract.documents[0].fileName = 'Signed contract.docx';
    contract.documents[0].contentType = 'application/vnd.openxmlformats-officedocument.wordprocessingml.document';
    mocks.download.mockResolvedValueOnce(new Blob(['<html>'], { type: 'text/html' }));
    fireEvent.click(await showDocuments());
    await waitFor(() => expect(opened).toHaveLength(1));
    expect(opened[0].download).toBe('Signed contract.docx');
    expect(opened[0].href).toBe('blob:protected-contract');
    expect(opened[0].target).toBe('');
    expect(mocks.success).toHaveBeenCalledWith('Document download started. Check your downloads.');
  });

  it('keeps a failed PDF preview visibly open without a popup or public fallback', async () => {
    mocks.previewDownload.mockRejectedValueOnce(new Error('Protected document access denied. (403)'));
    fireEvent.click(await showDocuments());
    expect(await screen.findByText('Protected document access denied. (403)')).toBeVisible();
    expect(screen.getByRole('dialog', { name: 'Signed contract.pdf' })).toBeVisible();
    expect(opened).toHaveLength(0);
    expect(createObjectURL).not.toHaveBeenCalled();
    expect(mocks.download).not.toHaveBeenCalled();
    expect(mocks.deleteDocument).not.toHaveBeenCalled();
  });
});
