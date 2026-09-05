import { beforeEach, describe, expect, it, vi } from 'vitest';
import { isTenderDocumentContentArtifactApproved } from '@/lib/procurement-tender-document';

const api = vi.hoisted(() => ({ get: vi.fn() }));
vi.mock('./compatibleApiService', () => ({ compatibleApiService: api }));
vi.mock('./api.service', () => ({ apiService: {} }));

import { WorkflowApiService } from './workflow-api.service';

describe('workflow evidence API enum contract', () => {
  const service = new WorkflowApiService();
  beforeEach(() => vi.clearAllMocks());

  it.each([
    ['Verified', 'Clean', 1, 1, true],
    [1, 1, 1, 1, true],
    ['1', '1', 1, 1, true],
    ['Pending', 'Clean', 0, 1, false],
    ['Rejected', 'Clean', 2, 1, false],
    ['Verified', 'Infected', 1, 2, false],
    ['Verified', 'Failed', 1, 3, false],
    ['Verified', 'Pending', 1, 0, false],
    ['Unexpected', 'Clean', -1, 1, false],
    ['Verified', 'Unexpected', 1, -1, false],
    ['', 'Clean', -1, 1, false],
  ])('normalizes %s / %s without accepting unsafe content', async (
    verificationStatus, malwareScanStatus, expectedVerification, expectedScan, approved
  ) => {
    api.get.mockResolvedValue({ success: true, data: [{
      id: 'evidence-1', fileName: 'tender.docx', filePath: 'workflow/tender.docx',
      sha256: 'a'.repeat(64), isCurrent: true, version: 1,
      verificationStatus, malwareScanStatus,
    }] });

    const [evidence] = await service.getWorkflowStepEvidence('step-1');
    expect(api.get).toHaveBeenCalledWith('/workflow/evidence/step/step-1');
    expect(evidence).toMatchObject({
      id: 'evidence-1', sha256: 'a'.repeat(64),
      verificationStatus: expectedVerification, malwareScanStatus: expectedScan,
    });
    expect(isTenderDocumentContentArtifactApproved({
      ...evidence, workflowInstanceId: 'workflow-1', workflowName: 'Template review',
      entityType: 'Template', entityId: 'template-1', stepName: 'Submitted',
    })).toBe(approved);
  });

  it('handles an empty evidence response', async () => {
    api.get.mockResolvedValue({ success: true, data: null });
    await expect(service.getWorkflowStepEvidence('empty-step')).resolves.toEqual([]);
  });

  it('requests the exact workflow on the server rather than searching a recent page', async () => {
    api.get.mockResolvedValue({ success: true, data: [] });
    await service.getWorkflowEvidenceReviewInstances({ workflowInstanceId: 'workflow-exact', pageSize: 100 });
    expect(api.get).toHaveBeenCalledWith('/workflow/evidence/review/instances?workflowInstanceId=workflow-exact&pageSize=100');
  });
});
