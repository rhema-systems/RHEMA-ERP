import React from 'react';
import {
  cleanup,
  fireEvent,
  render,
  screen,
  waitFor,
  within,
} from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import type { ProcurementTenderDocumentTemplate } from '@/types/procurement-tender-document';
import type { WorkflowEvidenceReviewInstanceDto } from '@/types/workflow';

const api = vi.hoisted(() => ({
  getTemplate: vi.fn(),
  workflowOptions: vi.fn(),
  policyOptions: vi.fn(),
  templateWorkflowInstance: vi.fn(),
  uploadTemplateWorkflowContent: vi.fn(),
  completeTemplateContentStep: vi.fn(),
  submitTemplate: vi.fn(),
  publishTemplate: vi.fn(),
  attachTemplateContent: vi.fn(),
  summary: vi.fn(),
  canManage: true,
}));
vi.mock('@/services/procurement-tender-document.service', () => ({
  procurementTenderDocumentService: api,
}));
vi.mock('@/services/workflow-api.service', () => ({
  workflowApiService: { getWorkflowEntitySummary: api.summary },
}));
vi.mock('@/services/procurement-control-event.service', () => ({
  procurementControlEventService: { search: async () => ({ items: [] }) },
}));
vi.mock('@/hooks/use-auth', () => ({
  useAuth: () => ({
    hasPermission: (permission: string) =>
      permission === 'procurement.tender.administer' && api.canManage,
  }),
}));
vi.mock('./TenderDocumentContentArtifactField', () => ({
  TenderDocumentContentArtifactField: () => null,
}));
vi.mock('./TenderDocumentEvidenceReview', () => ({
  TenderDocumentEvidenceReview: () => null,
  isTemplateWorkflowCompleted: (status: number) => status === 2,
}));
vi.mock('./TenderDocumentWorkflowReview', () => ({
  TenderDocumentWorkflowReview: () => null,
}));
vi.mock('sonner', () => ({ toast: { success: vi.fn(), error: vi.fn() } }));

import { TenderDocumentTemplateEditor } from './TenderDocumentTemplateEditor';
import { toast } from 'sonner';

const base = {
  id: 'template',
  templateCode: 'UAT-NCT',
  name: 'NCT document',
  version: 1,
  status: 'PendingApproval',
  documentTypeCode: 'TENDER-DOCUMENT',
  effectiveFromUtc: '2026-09-07T00:00:00Z',
  policySetId: 'policy',
  policySetCode: 'UAT',
  policySetVersion: 8,
  sourceConfigurationProfileId: 'profile',
  workflowDefinitionId: 'definition',
  workflowInstanceId: 'flow',
  rowVersion: 'row-token',
  submittedByName: 'Procurement Officer',
  allowedActions: ['AttachContent'],
  applicableMethods: [],
  blockedReasons: [],
  contentReference: '',
  contentChecksumSha256: '',
} as ProcurementTenderDocumentTemplate;
const baseWorkflow: WorkflowEvidenceReviewInstanceDto = {
  id: 'flow',
  entityId: 'template',
  entityType: 'ConfiguredDocument',
  workflowDefinitionId: 'definition',
  workflowName: 'Configured approval',
  status: 1,
  createdDate: '2026-09-07',
  currentStepInstanceId: 'upload-step',
  steps: [
    {
      stepInstanceId: 'upload-step',
      workflowStepId: 'upload-definition',
      stepName: 'Submitted',
      status: 0,
      evidence: {
        total: 0,
        pending: 0,
        verified: 0,
        rejected: 0,
        legalHold: 0,
      },
    },
  ],
};
let record: ProcurementTenderDocumentTemplate;
let flow: WorkflowEvidenceReviewInstanceDto;
const clients: QueryClient[] = [];
beforeEach(() => {
  vi.clearAllMocks();
  api.canManage = true;
  record = structuredClone(base);
  flow = structuredClone(baseWorkflow);
  api.getTemplate.mockImplementation(async () => record);
  api.templateWorkflowInstance.mockImplementation(async () => flow);
  api.workflowOptions.mockResolvedValue([]);
  api.policyOptions.mockResolvedValue([]);
  api.summary.mockResolvedValue({
    entityId: 'template',
    workflowInstanceId: 'flow',
    currentStepInstanceId: 'review-step',
    pendingApprovers: [{ approverName: 'Assigned Reviewer' }],
  });
});
afterEach(() => {
  cleanup();
  clients.forEach((client) => client.clear());
  clients.length = 0;
});
async function mount() {
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false, gcTime: 0 } },
  });
  clients.push(client);
  render(
    <QueryClientProvider client={client}>
      <TenderDocumentTemplateEditor id="template" />
    </QueryClientProvider>
  );
  await screen.findByRole('heading', { name: 'UAT-NCT · v1' });
}
describe('document preparation visible actions', () => {
  it('starts the same workflow without pretending to send a file or publish', async () => {
    record = {
      ...base,
      status: 'Draft',
      workflowInstanceId: undefined,
      allowedActions: ['Submit'],
    };
    api.submitTemplate.mockImplementation(async () => {
      record = structuredClone(base);
      return record;
    });
    await mount();
    fireEvent.click(
      screen.getByRole('button', { name: 'Start document preparation' })
    );
    const dialog = screen.getByRole('dialog');
    expect(within(dialog).getByText(/No file is needed yet/)).toBeTruthy();
    const start = within(dialog).getByRole('button', {
      name: 'Start preparation',
    });
    expect(start).toBeDisabled();
    fireEvent.change(within(dialog).getAllByRole('textbox')[0], {
      target: { value: 'LOCAL-UAT-preparation-reference' },
    });
    fireEvent.click(start);
    await screen.findByText('Waiting with: Procurement Officer');
    expect(api.submitTemplate).toHaveBeenCalledWith(
      'template',
      expect.objectContaining({
        rowVersion: 'row-token',
        evidence: [
          expect.objectContaining({
            reference: 'LOCAL-UAT-preparation-reference',
          }),
        ],
      })
    );
    expect(api.completeTemplateContentStep).not.toHaveBeenCalled();
    expect(api.publishTemplate).not.toHaveBeenCalled();
    expect(
      screen.queryByRole('button', { name: 'Submit exact workflow' })
    ).toBeNull();
  });
  it('requires an upload, advances the same step once, and shows the real reviewer', async () => {
    api.uploadTemplateWorkflowContent.mockImplementation(async () => {
      flow = {
        ...flow,
        steps: [
          {
            ...flow.steps[0],
            evidence: { ...flow.steps[0].evidence, total: 1 },
          },
        ],
      };
    });
    api.completeTemplateContentStep.mockImplementation(async () => {
      flow = {
        ...flow,
        currentStepInstanceId: 'review-step',
        steps: [
          ...flow.steps,
          {
            ...flow.steps[0],
            stepInstanceId: 'review-step',
            stepName: 'Approval',
          },
        ],
      };
      return { success: true };
    });
    await mount();
    await screen.findByLabelText('Tender document file');
    expect(
      screen.getByRole('button', { name: 'Send for approval' })
    ).toBeDisabled();
    const file = new File(['LOCAL UAT'], 'test-tender.pdf', {
      type: 'application/pdf',
    });
    fireEvent.change(screen.getByLabelText('Tender document file'), {
      target: { files: [file] },
    });
    fireEvent.click(
      screen.getByRole('button', { name: 'Upload controlled content' })
    );
    await waitFor(() =>
      expect(
        screen.getByRole('button', { name: 'Send for approval' })
      ).toBeEnabled()
    );
    expect(api.uploadTemplateWorkflowContent).toHaveBeenCalledWith(
      'upload-step',
      file
    );
    fireEvent.click(screen.getByRole('button', { name: 'Send for approval' }));
    await screen.findByText('Waiting with: Assigned Reviewer');
    expect(api.completeTemplateContentStep).toHaveBeenCalledExactlyOnceWith(
      'upload-step'
    );
    expect(api.submitTemplate).not.toHaveBeenCalled();
    expect(api.publishTemplate).not.toHaveBeenCalled();
  });
  it('keeps the chosen file on upload failure and does not send the request', async () => {
    api.uploadTemplateWorkflowContent.mockRejectedValue(
      new Error('Scan service unavailable')
    );
    await mount();
    await screen.findByLabelText('Tender document file');
    fireEvent.change(screen.getByLabelText('Tender document file'), {
      target: { files: [new File(['test'], 'test.pdf')] },
    });
    fireEvent.click(
      screen.getByRole('button', { name: 'Upload controlled content' })
    );
    await waitFor(() =>
      expect(toast.error).toHaveBeenCalledWith('Scan service unavailable')
    );
    expect(
      screen.getByRole('button', { name: 'Upload controlled content' })
    ).toBeEnabled();
    expect(
      screen.getByRole('button', { name: 'Send for approval' })
    ).toBeDisabled();
    expect(api.completeTemplateContentStep).not.toHaveBeenCalled();
  });
  it('does not advance the UI when sending fails', async () => {
    flow.steps[0].evidence.total = 1;
    api.completeTemplateContentStep.mockResolvedValue({
      success: false,
      message: 'Document review is required',
    });
    await mount();
    await screen.findByLabelText('Tender document file');
    fireEvent.click(screen.getByRole('button', { name: 'Send for approval' }));
    await waitFor(() =>
      expect(toast.error).toHaveBeenCalledWith('Document review is required')
    );
    expect(screen.getByText('Waiting with: Procurement Officer')).toBeTruthy();
    expect(screen.queryByText('Waiting with: Assigned Reviewer')).toBeNull();
  });
  it('does not add preparation actions for an unauthorized user', async () => {
    api.canManage = false;
    await mount();
    expect(
      screen.queryByRole('button', { name: 'Start document preparation' })
    ).toBeNull();
    expect(
      screen.queryByRole('button', { name: 'Send for approval' })
    ).toBeNull();
    expect(screen.queryByLabelText('Tender document file')).toBeNull();
  });
});
