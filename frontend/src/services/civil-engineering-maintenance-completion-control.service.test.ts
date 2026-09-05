import { describe, expect, it, vi } from 'vitest';

const get = vi.fn();
const post = vi.fn();
vi.mock('@/services/api.service', () => ({ apiService: { get, post } }));

describe('civilEngineeringMaintenanceCompletionControlService', () => {
  it('uses the governed Civil completion-control resource', async () => {
    const { civilEngineeringMaintenanceCompletionControlService } = await import('./civil-engineering-maintenance-completion-control.service');
    civilEngineeringMaintenanceCompletionControlService.lookups();
    civilEngineeringMaintenanceCompletionControlService.list();
    civilEngineeringMaintenanceCompletionControlService.create({ clientRequestId: 'request', executionLinkId: 'link', completionSummary: 'Completed scope', completionDocumentRecordId: 'record', completionDocumentVersionId: 'version' });
    civilEngineeringMaintenanceCompletionControlService.process('control', { clientRequestId: 'process', rowVersion: 'row', action: 'DirectInspection', note: 'Inspect completed work', centralDocumentRecordId: 'record', centralDocumentVersionId: 'version' });
    expect(get).toHaveBeenNthCalledWith(1, '/projects/civil-engineering/maintenance-completion-controls/lookups');
    expect(get).toHaveBeenNthCalledWith(2, '/projects/civil-engineering/maintenance-completion-controls');
    expect(post).toHaveBeenNthCalledWith(1, '/projects/civil-engineering/maintenance-completion-controls', expect.objectContaining({ executionLinkId: 'link' }));
    expect(post).toHaveBeenNthCalledWith(2, '/projects/civil-engineering/maintenance-completion-controls/control/process', expect.objectContaining({ action: 'DirectInspection' }));
  });
});
