import { beforeEach, describe, expect, it, vi } from 'vitest';

const api = vi.hoisted(() => ({
  get: vi.fn(),
  post: vi.fn(),
  put: vi.fn(),
}));

vi.mock('@/services/api.service', () => ({ apiService: api }));

import { procurementTenderDocumentService as service } from './procurement-tender-document.service';
import type { ProcurementTenderDocumentLifecycleRequest } from '@/types/procurement-tender-document';

describe('procurement tender-document API client', () => {
  beforeEach(() => {
    vi.clearAllMocks();
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
    await service.publishTemplate('template-1', lifecycle);
    await service.rejectTemplate('template-1', lifecycle);
    await service.retireTemplate('template-1', lifecycle);
    await service.deleteDraft('template-1', lifecycle);

    for (const [index, action] of [
      'submit',
      'publish',
      'reject',
      'retire',
      'delete-draft',
    ].entries()) {
      expect(api.post).toHaveBeenNthCalledWith(
        index + 1,
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
