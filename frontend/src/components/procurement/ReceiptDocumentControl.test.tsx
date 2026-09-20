import React from 'react';
import { act, cleanup, fireEvent, render, screen, within } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { purchasingService, type ProcurementReceiptDocumentOverviewDto } from '@/services/purchasingService';
import { ReceiptDocumentControl } from './ReceiptDocumentControl';

const previewMocks = vi.hoisted(() => ({ download: vi.fn() }));
vi.mock('@/services/api.service', () => ({ apiService: { downloadBlob: previewMocks.download } }));
vi.mock('next/dynamic', () => ({ default: () => (props: { fileData?: Uint8Array; enableAnnotations?: boolean }) =>
  <div role="region" aria-label="PDF content" data-annotations={String(props.enableAnnotations)}>{props.fileData?.length} authorized bytes</div> }));

vi.mock('@/services/purchasingService', () => ({ purchasingService: {
  getReceiptDocumentControl: vi.fn(), ensureReceiptDocuments: vi.fn(),
  reconcileReceiptDocuments: vi.fn(), signReceiptDocument: vi.fn(), issueReceiptDocument: vi.fn(),
} }));

const fixture = (): ProcurementReceiptDocumentOverviewDto => ({
  purchaseOrderReceiptId: 'receipt-1', receiptNumber: 'REC-1', purchaseOrderNumber: 'PO-1',
  supplierName: 'Supplier', receiptStatus: 'PendingInspection', inspectionStatus: 'Draft',
  configurationProfileId: 'profile-1', configurationProfileVersion: 1, configuredDocumentType: 2,
  coexistenceRule: 0, decisionKeys: ['DEC-001', 'DEC-013'], requiredEvidence: ['Waybill'],
  availableEvidence: ['Waybill'], isReconciled: false, allowedActions: ['ensure', 'reconcile'],
  checks: [
    { code: 'SOURCE', label: 'Source purchase order', passed: true, message: 'An approved purchase order is linked.' },
    { code: 'INSPECTION', label: 'Inspection/acceptance', passed: false, message: 'Inspection is Draft.' },
  ],
  documents: [{
    id: 'grn-1', documentKind: 'Grn', documentNumber: 'GRN-1', templateCode: 'TDC-GRN',
    status: 'PendingSignatures', reconciliationStatus: 'Pending', preparedByName: 'John Manager',
    preparedAtUtc: '2026-09-09T12:00:00Z', sourceIntegrityHash: 'retained-integrity-hash',
    centralDocumentRecordId: 'retained-dms-id', requiredSignatures: ['Stores', 'Approving Officer'],
    allowedSignatureRoles: ['Stores'], signatures: [], allowedActions: ['sign', 'issue'], rowVersion: 'AQID',
    actions: [{ id: 'event-1', action: 'Created', fromStatus: '', toStatus: 'PendingSignatures',
      actorName: 'John Manager', occurredAtUtc: '2026-09-09T12:00:00Z',
      reason: 'Generated from DEC-013 configuration.', integrityHash: 'history-hash' }],
  }],
});

beforeEach(() => { vi.resetAllMocks(); vi.mocked(purchasingService.getReceiptDocumentControl).mockResolvedValue(fixture()); });
afterEach(cleanup);

describe('ReceiptDocumentControl disclosure', () => {
  it('keeps statuses, blockers and signatures visible while policy codes and audit metadata start hidden', async () => {
    render(<ReceiptDocumentControl receiptId="receipt-1" />);
    const document = await screen.findByTestId('receipt-document-grn');
    expect(screen.getByText('Needs attention')).toBeVisible();
    expect(screen.getAllByText('Inspection is Draft.').some(node => !node.closest('[hidden]'))).toBe(true);
    expect(within(document).getByText('Pending signatures')).toBeVisible();
    expect(within(document).getByText('Stores: pending')).toBeVisible();
    expect(screen.getByText('DEC-013', { exact: true })).not.toBeVisible();
    expect(screen.getByText('retained-integrity-hash')).not.toBeVisible();
    expect(screen.getByText('Record retained-dms-id')).not.toBeVisible();
    expect(screen.getByText('Generated from DEC-013 configuration.')).not.toBeVisible();
    expect(within(document).getByRole('button', { name: 'Sign' })).toBeEnabled();
    expect(within(document).getByRole('button', { name: 'Issue' })).toBeDisabled();
    expect(screen.queryByRole('button', { name: 'Create receipt documents' })).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Reconcile' })).toBeVisible();
  });

  it('reveals technical evidence without changing the form or invoking business actions', async () => {
    render(<ReceiptDocumentControl receiptId="receipt-1" />);
    await screen.findByTestId('receipt-document-grn');
    const comment = screen.getByRole('textbox');
    fireEvent.change(comment, { target: { value: 'Keep this unsaved note' } });
    const audit = screen.getByRole('button', { name: /GRN history & technical details/ });
    fireEvent.click(audit);
    expect(screen.getByText('retained-integrity-hash')).toBeVisible();
    expect(screen.getByText('Generated from DEC-013 configuration.')).toBeVisible();
    fireEvent.click(audit);
    expect(comment).toHaveValue('Keep this unsaved note');
    fireEvent.click(screen.getByRole('button', { name: /Receipt document checks/ }));
    expect(screen.getByText('DEC-013', { exact: true })).toBeVisible();
    expect(purchasingService.signReceiptDocument).not.toHaveBeenCalled();
    expect(purchasingService.issueReceiptDocument).not.toHaveBeenCalled();
    expect(purchasingService.reconcileReceiptDocuments).not.toHaveBeenCalled();
  });

  it('keeps reconciliation exceptions visible even when technical details are closed', async () => {
    const overview = fixture();
    overview.documents[0].reconciliationStatus = 'Exception';
    overview.documents[0].reconciliationMessage = 'The retained source version no longer matches.';
    vi.mocked(purchasingService.getReceiptDocumentControl).mockResolvedValue(overview);
    render(<ReceiptDocumentControl receiptId="receipt-1" />);
    const card = await screen.findByTestId('receipt-document-grn');
    expect(within(card).getByRole('alert')).toHaveTextContent('The retained source version no longer matches.');
  });

  it('preserves the empty-register action and visible load failure recovery', async () => {
    const overview = fixture(); overview.documents = [];
    vi.mocked(purchasingService.getReceiptDocumentControl).mockResolvedValueOnce(overview);
    render(<ReceiptDocumentControl receiptId="receipt-1" />);
    expect(await screen.findByRole('button', { name: 'Create receipt documents' })).toBeEnabled();
    vi.mocked(purchasingService.getReceiptDocumentControl).mockRejectedValueOnce(new Error('Permission denied'));
    fireEvent.click(screen.getByRole('button', { name: 'Refresh document checks' }));
    expect(await screen.findByTestId('receipt-document-error')).toHaveTextContent('Permission denied');
    expect(screen.getByRole('button', { name: 'Retry' })).toBeVisible();
  });

  it.each([1, 'Mrn', 'MRN', 'MaterialReceiptNote'])('hides retained MRN cards and their actions for kind %s', async (kind) => {
    const overview = fixture();
    overview.documents.push({ ...overview.documents[0], id: 'retained-mrn',
      documentKind: kind as typeof overview.documents[0]['documentKind'], documentNumber: 'MRN-ARCHIVE',
      templateCode: 'TDC-MRN', actions: [{ ...overview.documents[0].actions[0], id: 'mrn-event', reason: 'Retained MRN history' }] });
    vi.mocked(purchasingService.getReceiptDocumentControl).mockResolvedValue(overview);
    render(<ReceiptDocumentControl receiptId="receipt-1" />);
    await screen.findByTestId('receipt-document-grn');
    expect(screen.queryByTestId('receipt-document-mrn')).not.toBeInTheDocument();
    expect(screen.queryByText('MRN-ARCHIVE', { exact: false })).not.toBeInTheDocument();
    expect(screen.queryByText('Retained MRN history')).not.toBeInTheDocument();
    expect(screen.getAllByRole('button', { name: 'Sign' })).toHaveLength(1);
    expect(overview.documents).toHaveLength(2);
    expect(purchasingService.ensureReceiptDocuments).not.toHaveBeenCalled();
    expect(purchasingService.issueReceiptDocument).not.toHaveBeenCalled();
    await act(async () => { fireEvent.click(screen.getByRole('button', { name: 'Sign' })); });
    expect(purchasingService.signReceiptDocument).toHaveBeenCalledWith('grn-1', {
      requiredRole: 'Stores', comment: undefined, rowVersion: 'AQID',
    });
  });

  it('reports GRN reconciliation separately from a retained pending MRN', async () => {
    const overview = fixture();
    overview.documents[0].status = 'Issued';
    overview.documents[0].reconciliationStatus = 'Reconciled';
    overview.documents[0].allowedActions = ['download'];
    overview.documents.push({ ...overview.documents[0], id: 'mrn-pending', documentKind: 'Mrn',
      status: 'PendingSignatures', reconciliationStatus: 'Pending', allowedActions: ['sign'] });
    overview.checks = [{ code: 'DOCUMENT_SET', label: 'Configured GRN/MRN set', passed: true,
      message: 'DEC-013 requires Goods Receipt Note (GRN) and Material Receipt Note (MRN).' }];
    overview.isReconciled = false;
    vi.mocked(purchasingService.getReceiptDocumentControl).mockResolvedValue(overview);
    render(<ReceiptDocumentControl receiptId="receipt-1" />);
    await screen.findByTestId('receipt-document-grn');
    fireEvent.click(screen.getByRole('button', { name: /Receipt document checks/ }));
    expect(screen.getByText(/GRN reconciliation: Reconciled/)).toBeVisible();
    expect(screen.getByText('The configured receipt document register is complete.')).toBeVisible();
    expect(screen.queryByText(/Material Receipt Note/)).not.toBeInTheDocument();
    expect(screen.getAllByRole('button', { name: 'Open PDF' })).toHaveLength(1);
    expect(overview.isReconciled).toBe(false);
  });

  it('does not hide a failing server check or bypass GRN issue prerequisites', async () => {
    const overview = fixture();
    overview.checks = [{ code: 'DOCUMENT_SET', label: 'Configured GRN/MRN set', passed: false,
      message: 'The saved receipt document register is incomplete.' }];
    overview.documents[0].requiredSignatures = [];
    vi.mocked(purchasingService.getReceiptDocumentControl).mockResolvedValue(overview);
    render(<ReceiptDocumentControl receiptId="receipt-1" />);
    await screen.findByTestId('receipt-document-grn');
    fireEvent.change(screen.getByRole('textbox'), { target: { value: 'Issue approved GRN' } });
    expect(screen.getByText('Needs attention')).toBeVisible();
    expect(screen.getByRole('button', { name: 'Issue' })).toBeDisabled();
    expect(screen.getAllByText('The saved receipt document register is incomplete.').some(node => !node.closest('[hidden]'))).toBe(true);
  });

  it('explains an MRN-only legacy register without offering a duplicate create action', async () => {
    const overview = fixture();
    overview.documents[0].documentKind = 'Mrn';
    vi.mocked(purchasingService.getReceiptDocumentControl).mockResolvedValue(overview);
    render(<ReceiptDocumentControl receiptId="receipt-1" />);
    expect(await screen.findByTestId('receipt-document-grn-missing')).toHaveTextContent('The saved register does not contain a GRN.');
    expect(screen.queryByTestId('receipt-document-mrn')).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Create receipt documents' })).not.toBeInTheDocument();
    expect(purchasingService.ensureReceiptDocuments).not.toHaveBeenCalled();
  });

  it('opens the issued GRN inside the page from its protected route without popup or stock mutation', async () => {
    const overview = fixture();
    overview.documents[0].status = 'Issued';
    overview.documents[0].allowedActions = ['download'];
    vi.mocked(purchasingService.getReceiptDocumentControl).mockResolvedValue(overview);
    previewMocks.download.mockResolvedValue({ arrayBuffer: async () => new Uint8Array([37, 80, 68, 70]).buffer });
    const openWindow = vi.spyOn(window, 'open').mockImplementation(() => null);
    render(<ReceiptDocumentControl receiptId="receipt-1" />);
    fireEvent.click(await screen.findByRole('button', { name: 'Open PDF' }));
    expect(await screen.findByRole('dialog', { name: 'GRN-1' })).toBeVisible();
    expect(await screen.findByRole('region', { name: 'PDF content' })).toHaveTextContent('4 authorized bytes');
    expect(previewMocks.download).toHaveBeenCalledExactlyOnceWith('/ProcurementReceiptDocuments/grn-1/download');
    expect(screen.getByRole('region', { name: 'PDF content' })).toHaveAttribute('data-annotations', 'false');
    expect(openWindow).not.toHaveBeenCalled();
    expect(purchasingService.issueReceiptDocument).not.toHaveBeenCalled();
    expect(purchasingService.reconcileReceiptDocuments).not.toHaveBeenCalled();
    openWindow.mockRestore();
  });

  it('leaves a failed GRN preview visible with the protected download error', async () => {
    const overview = fixture();
    overview.documents[0].status = 'Issued';
    overview.documents[0].allowedActions = ['download'];
    vi.mocked(purchasingService.getReceiptDocumentControl).mockResolvedValue(overview);
    previewMocks.download.mockRejectedValue(new Error('Issued document is unavailable. (404)'));
    render(<ReceiptDocumentControl receiptId="receipt-1" />);
    fireEvent.click(await screen.findByRole('button', { name: 'Open PDF' }));
    expect(await screen.findByText('Issued document is unavailable. (404)')).toBeVisible();
    expect(screen.getByRole('dialog', { name: 'GRN-1' })).toBeVisible();
    expect(screen.queryByRole('region', { name: 'PDF content' })).not.toBeInTheDocument();
    expect(purchasingService.issueReceiptDocument).not.toHaveBeenCalled();
  });
});
