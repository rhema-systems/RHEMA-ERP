import { beforeEach, describe, expect, it, vi } from 'vitest';

const api = vi.hoisted(() => ({ get: vi.fn(), post: vi.fn() }));
vi.mock('@/services/api.service', () => ({ apiService: api }));

import { civilEngineeringMigrationService } from './civil-engineering-migration.service';

describe('civilEngineeringMigrationService', () => {
  beforeEach(() => vi.clearAllMocks());

  it('uses a project-scoped staging route and never exposes an owner-post endpoint', () => {
    const stage = { clientRequestId: 'stage', sourceType: 'DocumentRegister' as const, sourceRegisterReference: 'CE-ARCH-01', records: [] };
    const reconciliation = { rowVersion: 'row-version', reconciliationDocumentRecordId: 'record', reconciliationDocumentVersionId: 'version', declaration: 'Independent reconciliation confirmed.' };
    civilEngineeringMigrationService.lookups('project');
    civilEngineeringMigrationService.list('project');
    civilEngineeringMigrationService.stage('project', stage);
    civilEngineeringMigrationService.reconcile('project', 'batch', reconciliation);
    civilEngineeringMigrationService.signOff('project', 'batch', { rowVersion: 'next-row-version', declaration: 'Independent sign-off confirmed.' });
    civilEngineeringMigrationService.history('project', 'batch');

    expect(api.get).toHaveBeenNthCalledWith(1, '/projects/project/civil-engineering/migration-batches/lookups');
    expect(api.get).toHaveBeenNthCalledWith(2, '/projects/project/civil-engineering/migration-batches');
    expect(api.get).toHaveBeenNthCalledWith(3, '/projects/project/civil-engineering/migration-batches/batch/history');
    expect(api.post).toHaveBeenCalledWith('/projects/project/civil-engineering/migration-batches', stage);
    expect(api.post).toHaveBeenCalledWith('/projects/project/civil-engineering/migration-batches/batch/reconcile', reconciliation);
    expect(api.post).toHaveBeenCalledWith('/projects/project/civil-engineering/migration-batches/batch/sign-off', { rowVersion: 'next-row-version', declaration: 'Independent sign-off confirmed.' });
    expect(api.post.mock.calls.some(([route]) => String(route).includes('/post'))).toBe(false);
  });
});
