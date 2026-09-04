import { beforeEach, describe, expect, it, vi } from 'vitest';

const api = vi.hoisted(() => ({
  get: vi.fn(),
  post: vi.fn(),
  put: vi.fn(),
}));
const workflowApi = vi.hoisted(() => ({
  getWorkflowEvidenceReviewInstances: vi.fn(),
  getWorkflowStepEvidence: vi.fn(),
  uploadStepAttachment: vi.fn(),
  processStep: vi.fn(),
}));

vi.mock('@/services/api.service', () => ({ apiService: api }));
vi.mock('@/services/workflow-api.service', () => ({
  workflowApiService: workflowApi,
}));

import { procurementTenderDocumentService as service } from './procurement-tender-document.service';
import type { ProcurementTenderDocumentLifecycleRequest } from '@/types/procurement-tender-document';

describe('procurement tender-document API client', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('builds current artifact options from the tenant workflow-evidence APIs', async () => {
    workflowApi.getWorkflowEvidenceReviewInstances.mockResolvedValue([
      {
        id: 'instance-1',
        workflowName: 'Document control',
        entityType: 'TenderDocument',
        entityId: 'entity-1',
        steps: [
          {
            stepInstanceId: 'step-1',
            stepName: 'Verify content',
            evidence: { total: 2 },
          },
          {
            stepInstanceId: 'step-empty',
            stepName: 'Empty',
            evidence: { total: 0 },
          },
        ],
      },
    ]);
    workflowApi.getWorkflowStepEvidence.mockResolvedValue([
      {
        id: 'evidence-current',
        documentName: 'Controlled ITB',
        fileName: 'itb.docx',
        filePath: 'workflow-evidence/default/itb.docx',
        sha256: 'a'.repeat(64),
        version: 2,
        isCurrent: true,
        verificationStatus: 1,
        malwareScanStatus: 1,
      },
      {
        id: 'evidence-old',
        fileName: 'itb-v1.docx',
        filePath: 'workflow-evidence/default/itb-v1.docx',
        sha256: 'b'.repeat(64),
        version: 1,
        isCurrent: false,
        verificationStatus: 1,
        malwareScanStatus: 1,
      },
    ]);

    await expect(service.contentArtifactOptions()).resolves.toEqual([
      expect.objectContaining({
        id: 'evidence-current',
        filePath: 'workflow-evidence/default/itb.docx',
        sha256: 'a'.repeat(64),
        workflowInstanceId: 'instance-1',
        workflowName: 'Document control',
        stepName: 'Verify content',
      }),
    ]);
    expect(workflowApi.getWorkflowEvidenceReviewInstances).toHaveBeenCalledWith({
      pageSize: 100,
      workflowInstanceId: undefined,
    });
    expect(workflowApi.getWorkflowStepEvidence).toHaveBeenCalledTimes(1);
    expect(workflowApi.getWorkflowStepEvidence).toHaveBeenCalledWith('step-1');
  });

  it('limits attachable content to the template exact workflow instance', async () => {
    workflowApi.getWorkflowEvidenceReviewInstances.mockResolvedValue([
      {
        id: 'instance-exact',
        workflowName: 'TDC Sourcing Approval',
        entityType: 'Procurement Sourcing',
        entityId: 'template-1',
        steps: [
          {
            stepInstanceId: 'step-exact',
            stepName: 'Approve source document',
            evidence: { total: 1 },
          },
        ],
      },
      {
        id: 'instance-unrelated',
        workflowName: 'Purchase Requisition V2',
        entityType: 'Purchase Requisition',
        entityId: 'pr-1',
        steps: [
          {
            stepInstanceId: 'step-unrelated',
            stepName: 'Task',
            evidence: { total: 1 },
          },
        ],
      },
    ]);
    workflowApi.getWorkflowStepEvidence.mockResolvedValue([]);

    await service.contentArtifactOptions('instance-exact');

    expect(workflowApi.getWorkflowStepEvidence).toHaveBeenCalledTimes(1);
    expect(workflowApi.getWorkflowStepEvidence).toHaveBeenCalledWith(
      'step-exact'
    );
  });

  it('uploads and advances only the exact template workflow step selected by the UI', async () => {
    workflowApi.getWorkflowEvidenceReviewInstances.mockResolvedValue([
      {
        id: 'instance-exact',
        currentStepInstanceId: 'step-submitted',
        workflowName: 'TDC Sourcing Approval',
        entityType: 'Procurement Sourcing',
        entityId: 'template-1',
        steps: [
          {
            stepInstanceId: 'step-submitted',
            stepName: 'Submitted',
            evidence: { total: 0 },
          },
        ],
      },
    ]);
    const file = new File(['controlled tender'], 'nct.pdf', {
      type: 'application/pdf',
    });

    await expect(
      service.templateWorkflowInstance('instance-exact')
    ).resolves.toEqual(
      expect.objectContaining({ currentStepInstanceId: 'step-submitted' })
    );
    await service.uploadTemplateWorkflowContent('step-submitted', file);
    await service.completeTemplateContentStep('step-submitted');

    expect(workflowApi.getWorkflowEvidenceReviewInstances).toHaveBeenCalledWith({
      pageSize: 100,
      workflowInstanceId: 'instance-exact',
    });

    expect(workflowApi.uploadStepAttachment).toHaveBeenCalledWith(
      'step-submitted',
      file,
      undefined,
      'Controlled tender-document content',
      'Tender document'
    );
    expect(workflowApi.processStep).toHaveBeenCalledWith('step-submitted', {
      action: 0,
      comments: 'Controlled tender-document content uploaded for approval.',
    });
  });

  it('uses the dedicated template option and history endpoints', async () => {
    await service.templateSummary();
    await service.workflowOptions();
    await service.policyOptions();
    await service.searchTemplates({ status: 'Published', page: 1, pageSize: 25 });
    await service.getTemplate('template-1');

    expect(api.get).toHaveBeenNthCalledWith(
      1,
      '/procurement/tender-document-templates/summary'
    );
    expect(api.get).toHaveBeenNthCalledWith(
      2,
      '/procurement/tender-document-templates/workflow-options'
    );
    expect(api.get).toHaveBeenNthCalledWith(
      3,
      '/procurement/tender-document-templates/policy-options'
    );
    expect(api.get).toHaveBeenNthCalledWith(
      4,
      '/procurement/tender-document-templates',
      { status: 'Published', page: 1, pageSize: 25 }
    );
    expect(api.get).toHaveBeenNthCalledWith(
      5,
      '/procurement/tender-document-templates/template-1'
    );
  });

  it('posts lifecycle mutations to the exact template revision route', async () => {
    const lifecycle: ProcurementTenderDocumentLifecycleRequest = {
      rowVersion: 'AQID',
      comment: 'Governed action',
      evidence: [
        { referenceKind: 'ExternalReference', reference: 'EVIDENCE-1' },
      ],
    };

    await service.submitTemplate('template-1', lifecycle);
    await service.attachTemplateContent('template-1', {
      contentWorkflowEvidenceDocumentId: 'content-1',
      rowVersion: 'AQID',
    });
    await service.publishTemplate('template-1', lifecycle);
    await service.rejectTemplate('template-1', lifecycle);
    await service.retireTemplate('template-1', lifecycle);
    await service.deleteDraft('template-1', lifecycle);

    expect(api.post).toHaveBeenNthCalledWith(
      1,
      '/procurement/tender-document-templates/template-1/submit',
      lifecycle
    );
    expect(api.post).toHaveBeenNthCalledWith(
      2,
      '/procurement/tender-document-templates/template-1/content',
      {
        contentWorkflowEvidenceDocumentId: 'content-1',
        rowVersion: 'AQID',
      }
    );
    for (const [index, action] of [
      'publish',
      'reject',
      'retire',
      'delete-draft',
    ].entries()) {
      expect(api.post).toHaveBeenNthCalledWith(
        index + 3,
        `/procurement/tender-document-templates/template-1/${action}`,
        lifecycle
      );
    }
  });

  it('uses one tenant-safe register root for readiness and mutations', async () => {
    await service.readiness('Tender', 'tender-1');
    await service.getRegister('Tender', 'tender-1');
    await service.decideChange('change-1', {
      action: 'Approve',
      approvalReference: 'APPROVAL-1',
      rowVersion: 'AQID',
    });
    await service.acknowledge({
      changeRecipientId: 'recipient-1',
      outcome: 'Acknowledged',
      acknowledgementChannel: 'ExternalPortal',
      acknowledgementReference: 'ACK-1',
      evidenceReference: 'EVIDENCE-1',
    });

    expect(api.get).toHaveBeenNthCalledWith(
      1,
      '/procurement/tender-document-register/readiness',
      { sourceType: 'Tender', sourceId: 'tender-1' }
    );
    expect(api.get).toHaveBeenNthCalledWith(
      2,
      '/procurement/tender-document-register',
      { sourceType: 'Tender', sourceId: 'tender-1' }
    );
    expect(api.post).toHaveBeenNthCalledWith(
      1,
      '/procurement/tender-document-register/changes/change-1/decision',
      {
        action: 'Approve',
        approvalReference: 'APPROVAL-1',
        rowVersion: 'AQID',
      }
    );
    expect(api.post).toHaveBeenNthCalledWith(
      2,
      '/procurement/tender-document-register/acknowledgements',
      {
        changeRecipientId: 'recipient-1',
        outcome: 'Acknowledged',
        acknowledgementChannel: 'ExternalPortal',
        acknowledgementReference: 'ACK-1',
        evidenceReference: 'EVIDENCE-1',
      }
    );
  });
});
