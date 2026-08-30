import { beforeEach, describe, expect, it, vi } from 'vitest';

const api = vi.hoisted(() => ({ get: vi.fn(), post: vi.fn() }));
vi.mock('@/services/api.service', () => ({ apiService: api }));

import { civilEngineeringDevelopmentApprovalFileService } from './civil-engineering-development-approval-file.service';

describe('civilEngineeringDevelopmentApprovalFileService', () => {
  beforeEach(() => vi.clearAllMocks());

  it('uses the tenant-safe Civil development approval-file routes', () => {
    const create = { clientRequestId: 'request', projectId: 'project', estateManagedAssetId: 'property', applicationReference: 'DAP-1', dueDate: '2026-08-21', applicationEvidence: [{ centralDocumentRecordId: 'record', centralDocumentVersionId: 'version' }] };
    const inspection = { clientRequestId: 'inspection', siteInspectedAt: '2026-08-21T10:00:00Z', evidence: [{ centralDocumentRecordId: 'record', centralDocumentVersionId: 'version' }], rowVersion: 'row-version' };
    civilEngineeringDevelopmentApprovalFileService.lookups();
    civilEngineeringDevelopmentApprovalFileService.list();
    civilEngineeringDevelopmentApprovalFileService.create(create);
    civilEngineeringDevelopmentApprovalFileService.recordSiteInspection('file', inspection);
    expect(api.get).toHaveBeenNthCalledWith(1, '/projects/civil-engineering/development-approval-files/lookups');
    expect(api.get).toHaveBeenNthCalledWith(2, '/projects/civil-engineering/development-approval-files');
    expect(api.post).toHaveBeenNthCalledWith(1, '/projects/civil-engineering/development-approval-files', create);
    expect(api.post).toHaveBeenNthCalledWith(2, '/projects/civil-engineering/development-approval-files/file/site-inspection', inspection);
  });
});
