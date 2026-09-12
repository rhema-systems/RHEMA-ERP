import { beforeEach, describe, expect, it, vi } from 'vitest';
import { procurementExceptionalSourcingControlService as service } from '@/services/procurement-exceptional-sourcing-control.service';
import { getExceptionalSourcingActions } from './procurement-exceptional-sourcing-control';

const api = vi.hoisted(() => ({ get: vi.fn(), post: vi.fn() }));
vi.mock('@/services/api.service', () => ({ apiService: api }));
beforeEach(() => vi.clearAllMocks());

describe('exceptional sourcing API enum contract', () => {
  it.each([['PettyPurchase', 5], ['SingleSource', 4], ['RestrictedTendering', 3], [5, 5], ['5', 5]])('normalizes readiness method %s', async (method, expected) => {
    api.get.mockResolvedValue({ method, quotationItems: [{ tenderItemId: 'item' }] });
    expect(await service.readiness('source')).toMatchObject({ method: expected, quotationItems: [{ tenderItemId: 'item' }] });
  });
  it.each(['Approved', 2, '2'])('enables petty recommendation for API status %s', async status => {
    api.get.mockResolvedValue({ method: 'PettyPurchase', status });
    expect(getExceptionalSourcingActions(await service.get('source'))).toMatchObject({ canRecommend: true, canNegotiate: false });
  });
  it('normalizes mutation responses too', async () => {
    api.post.mockResolvedValue({ method: 'PettyPurchase', status: 'PendingApproval', rowVersion: 'updated' });
    expect(await service.submitApproval('source', 'current')).toMatchObject({ method: 5, status: 1, rowVersion: 'updated' });
    expect(api.post).toHaveBeenCalledWith('/procurement/tenders/source/exception-controls/approval/submit', { rowVersion: 'current' });
  });
  it('retains direct completion mode and sends genuine statutory references', async () => {
    api.post.mockResolvedValue({ method: 'SingleSource', status: 'Approved', approvalRequired: false, workflowInstanceId: null });
    expect(await service.submitApproval('source', 'current', { boardApprovalReference: 'BOARD-1', ppaApprovalReference: 'PPA-1' }))
      .toMatchObject({ method: 4, status: 2, approvalRequired: false, workflowInstanceId: null });
    expect(api.post).toHaveBeenCalledWith('/procurement/tenders/source/exception-controls/approval/submit', {
      rowVersion: 'current', boardApprovalReference: 'BOARD-1', ppaApprovalReference: 'PPA-1',
    });
  });
  it('fails closed for unknown methods and statuses', async () => {
    api.get.mockResolvedValue({ method: 'Unknown' });
    await expect(service.readiness('source')).rejects.toThrow('Unrecognized sourcing method');
    api.get.mockResolvedValue({ method: 'PettyPurchase', status: 'FutureStatus' });
    await expect(service.get('source')).rejects.toThrow('Unrecognized sourcing status');
  });
});
