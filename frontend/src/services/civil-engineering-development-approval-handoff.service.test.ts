import { beforeEach, describe, expect, it, vi } from 'vitest';

const api = vi.hoisted(() => ({ get: vi.fn(), post: vi.fn() }));
vi.mock('@/services/api.service', () => ({ apiService: api }));

import { civilEngineeringDevelopmentApprovalHandoffService } from './civil-engineering-development-approval-handoff.service';

describe('civilEngineeringDevelopmentApprovalHandoffService', () => {
  beforeEach(() => vi.clearAllMocks());

  it('uses the approval-file scoped handoff routes', () => {
    const request = { clientRequestId: 'request', toSection: 'Architecture' as const, recipientRoleId: 'role', recipientUserId: 'user', dueDate: '2026-08-22', evidence: [] };
    civilEngineeringDevelopmentApprovalHandoffService.lookups('file');
    civilEngineeringDevelopmentApprovalHandoffService.list('file');
    civilEngineeringDevelopmentApprovalHandoffService.create('file', request);
    expect(api.get).toHaveBeenNthCalledWith(1, '/projects/civil-engineering/development-approval-files/file/handoffs/lookups');
    expect(api.get).toHaveBeenNthCalledWith(2, '/projects/civil-engineering/development-approval-files/file/handoffs');
    expect(api.post).toHaveBeenCalledWith('/projects/civil-engineering/development-approval-files/file/handoffs', request);
  });
});
