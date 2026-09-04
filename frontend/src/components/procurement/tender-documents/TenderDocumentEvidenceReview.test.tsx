import React from 'react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import {
  cleanup,
  fireEvent,
  render,
  screen,
  waitFor,
} from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import type {
  WorkflowEvidenceDocumentDto,
  WorkflowEvidenceReviewInstanceDto,
} from '@/types/workflow';

const api = vi.hoisted(() => ({
  getWorkflowStepEvidence: vi.fn(),
  verifyWorkflowEvidence: vi.fn(),
  downloadStepAttachment: vi.fn(),
}));
vi.mock('@/services/workflow-api.service', () => ({ workflowApiService: api }));
vi.mock('sonner', () => ({ toast: { success: vi.fn(), error: vi.fn() } }));

import {
  TenderDocumentEvidenceReview,
  isTemplateWorkflowCompleted,
} from './TenderDocumentEvidenceReview';

const workflow: WorkflowEvidenceReviewInstanceDto = {
  id: 'exact-template-workflow',
  workflowDefinitionId: 'configured-definition',
  workflowName: 'TDC document review',
  entityType: 'ConfiguredProcurementEntity',
  entityId: 'template-id',
  createdDate: '2026-09-04T00:00:00Z',
  status: 2,
  steps: [
    {
      stepInstanceId: 'exact-upload-step',
      workflowStepId: 'upload-step-definition',
      stepName: 'Upload',
      status: 2,
      evidence: {
        total: 1,
        pending: 1,
        verified: 0,
        rejected: 0,
        legalHold: 0,
      },
    },
  ],
};

const file = (
  overrides: Partial<WorkflowEvidenceDocumentDto> = {}
): WorkflowEvidenceDocumentDto => ({
  id: 'file-1',
  attachmentId: 'attachment-1',
  documentName: 'Tender terms',
  fileName: 'terms.docx',
  filePath: 'controlled/path',
  sha256: 'a'.repeat(64),
  documentOwnerId: 'uploader',
  version: 1,
  isCurrent: true,
  verificationStatus: 0,
  malwareScanStatus: 1,
  isExpired: false,
  isLegalHold: false,
  retainUntil: '2030-01-01',
  canVerify: true,
  ...overrides,
});

function renderReview(
  instance = workflow,
  eligibleContentEvidenceDocumentIds?: string[]
) {
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
  const onUpdated = vi.fn().mockResolvedValue(undefined);
  render(
    <QueryClientProvider client={client}>
      <TenderDocumentEvidenceReview
        workflow={instance}
        onUpdated={onUpdated}
        eligibleContentEvidenceDocumentIds={eligibleContentEvidenceDocumentIds}
      />
    </QueryClientProvider>
  );
  return { onUpdated, client };
}

beforeEach(() => {
  vi.resetAllMocks();
  api.getWorkflowStepEvidence.mockResolvedValue([file()]);
  api.verifyWorkflowEvidence.mockResolvedValue(undefined);
});
afterEach(cleanup);

describe('TenderDocumentEvidenceReview', () => {
  it('refreshes configured template eligibility as well as evidence from the readiness action', async () => {
    const { onUpdated } = renderReview();
    await screen.findByText('terms.docx · v1');
    fireEvent.click(
      screen.getByRole('button', { name: 'Refresh document readiness' })
    );
    await waitFor(() => expect(onUpdated).toHaveBeenCalledTimes(1));
    expect(api.getWorkflowStepEvidence).toHaveBeenCalledTimes(2);
  });
  it('loads only the exact workflow steps and hides superseded files', async () => {
    api.getWorkflowStepEvidence.mockResolvedValue([
      file(),
      file({ id: 'old-file', fileName: 'old.docx', isCurrent: false }),
    ]);
    renderReview();
    expect(await screen.findByText('terms.docx · v1')).toBeInTheDocument();
    expect(api.getWorkflowStepEvidence).toHaveBeenCalledTimes(1);
    expect(api.getWorkflowStepEvidence).toHaveBeenCalledWith(
      'exact-upload-step'
    );
    expect(screen.queryByText('old.docx · v1')).not.toBeInTheDocument();
    expect(screen.getByText('Workflow completed')).toBeInTheDocument();
    expect(
      screen.getByText(/publication also checks the configured policy/)
    ).toBeInTheDocument();
  });

  it.each([false, undefined])(
    'does not infer a reviewer role when canVerify is %s',
    async (canVerify) => {
      api.getWorkflowStepEvidence.mockResolvedValue([
        file({
          canVerify,
          verificationBlockedReason:
            'The uploader cannot verify their own file.',
        }),
      ]);
      renderReview();
      expect(
        await screen.findByText('The uploader cannot verify their own file.')
      ).toBeInTheDocument();
      expect(
        screen.queryByRole('button', { name: 'Verify file' })
      ).not.toBeInTheDocument();
      expect(
        screen.queryByRole('button', { name: 'Reject file' })
      ).not.toBeInTheDocument();
      expect(api.verifyWorkflowEvidence).not.toHaveBeenCalled();
    }
  );

  it('verifies the exact selected file after confirmation and refreshes readiness', async () => {
    const { onUpdated } = renderReview();
    fireEvent.click(await screen.findByRole('button', { name: 'Verify file' }));
    expect(api.verifyWorkflowEvidence).not.toHaveBeenCalled();
    fireEvent.change(screen.getByLabelText('Review notes (optional)'), {
      target: { value: '  Reviewed terms  ' },
    });
    fireEvent.click(
      screen.getByRole('button', { name: 'Confirm verification' })
    );
    await waitFor(() =>
      expect(api.verifyWorkflowEvidence).toHaveBeenCalledWith(
        'file-1',
        true,
        'Reviewed terms'
      )
    );
    await waitFor(() =>
      expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
    );
    expect(api.verifyWorkflowEvidence).toHaveBeenCalledTimes(1);
    expect(onUpdated).toHaveBeenCalledTimes(1);
    expect(api.getWorkflowStepEvidence).toHaveBeenCalledTimes(2);
  });

  it('retains notes and the confirmation dialog on server rejection with detail and code', async () => {
    api.verifyWorkflowEvidence.mockRejectedValueOnce({
      response: {
        data: {
          detail: 'Reviewer assignment changed.',
          code: 'WF_REVIEW_DENIED',
        },
      },
    });
    renderReview();
    fireEvent.click(await screen.findByRole('button', { name: 'Verify file' }));
    fireEvent.change(screen.getByLabelText('Review notes (optional)'), {
      target: { value: 'Keep these review notes' },
    });
    fireEvent.click(
      screen.getByRole('button', { name: 'Confirm verification' })
    );
    expect(await screen.findByRole('alert')).toHaveTextContent(
      'Reviewer assignment changed. (WF_REVIEW_DENIED)'
    );
    expect(screen.getByRole('dialog')).toBeInTheDocument();
    expect(screen.getByLabelText('Review notes (optional)')).toHaveValue(
      'Keep these review notes'
    );
    fireEvent.click(
      screen.getByRole('button', { name: 'Confirm verification' })
    );
    await waitFor(() =>
      expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
    );
    expect(api.verifyWorkflowEvidence).toHaveBeenNthCalledWith(
      2,
      'file-1',
      true,
      'Keep these review notes'
    );
  });

  it('requires a rejection reason and sends an independent rejection decision', async () => {
    renderReview();
    fireEvent.click(await screen.findByRole('button', { name: 'Reject file' }));
    expect(
      screen.getByRole('button', { name: 'Confirm rejection' })
    ).toBeDisabled();
    fireEvent.change(screen.getByLabelText('Rejection reason'), {
      target: { value: 'Wrong terms version' },
    });
    fireEvent.click(screen.getByRole('button', { name: 'Confirm rejection' }));
    await waitFor(() =>
      expect(api.verifyWorkflowEvidence).toHaveBeenCalledWith(
        'file-1',
        false,
        'Wrong terms version'
      )
    );
  });

  it.each([0, 2, 3, -1])(
    'cannot accept or download a file without a clean scan (%s)',
    async (malwareScanStatus) => {
      api.getWorkflowStepEvidence.mockResolvedValue([
        file({ malwareScanStatus }),
      ]);
      renderReview();
      expect(
        await screen.findByRole('button', { name: 'Verify file' })
      ).toBeDisabled();
      expect(
        screen.getByRole('button', { name: 'Download terms.docx' })
      ).toBeDisabled();
      expect(screen.getByRole('button', { name: 'Reject file' })).toBeEnabled();
      expect(api.verifyWorkflowEvidence).not.toHaveBeenCalled();
    }
  );

  it('does not show a new verification action for an already verified file', async () => {
    api.getWorkflowStepEvidence.mockResolvedValue([
      file({ verificationStatus: 1 }),
    ]);
    renderReview();
    expect(
      await screen.findByText('File ready for attachment')
    ).toBeInTheDocument();
    expect(
      screen.queryByRole('button', { name: 'Verify file' })
    ).not.toBeInTheDocument();
  });

  it('surfaces load failures without presenting verification actions', async () => {
    api.getWorkflowStepEvidence.mockRejectedValue(
      new Error('Workflow access unavailable.')
    );
    renderReview();
    expect(await screen.findByRole('alert')).toHaveTextContent(
      'Workflow access unavailable.'
    );
    expect(screen.getByText('File review unavailable')).toBeInTheDocument();
    expect(
      screen.queryByRole('button', { name: 'Verify file' })
    ).not.toBeInTheDocument();
  });
});

describe('configured document review requirements', () => {
  it('does not demand separate review when server eligibility satisfies configured requirements', async () => {
    api.getWorkflowStepEvidence.mockResolvedValue([file({ canVerify: false })]);
    renderReview(workflow, ['file-1']);
    expect(
      await screen.findByText('File ready for attachment')
    ).toBeInTheDocument();
    expect(
      screen.getByText(/Eligible under configured policy/)
    ).toBeInTheDocument();
    expect(
      screen.queryByRole('button', { name: 'Verify file' })
    ).not.toBeInTheDocument();
    expect(screen.queryByText(/must verify the file/)).not.toBeInTheDocument();
  });
  it('does not infer policy eligibility from a completed workflow without an eligible ID', async () => {
    renderReview(workflow, []);
    expect(
      await screen.findByRole('button', { name: 'Verify file' })
    ).toBeInTheDocument();
    expect(
      screen.queryByText('File ready for attachment')
    ).not.toBeInTheDocument();
  });
});

describe('isTemplateWorkflowCompleted', () => {
  it.each([2, '2', 'Completed'])(
    'recognizes the completed status %s',
    (status) => {
      expect(isTemplateWorkflowCompleted(status)).toBe(true);
    }
  );
  it.each([
    undefined,
    0,
    1,
    3,
    4,
    'Cancelled',
    'Failed',
    'InProgress',
    'unknown',
  ])('does not invent completion for %s', (status) => {
    expect(isTemplateWorkflowCompleted(status)).toBe(false);
  });
});
