import { beforeEach, describe, expect, it, vi } from 'vitest';
import { emergencyProcurementPlanService } from './procurementPlanningService';

describe('emergency-purchase governance client', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
    localStorage.clear();
    localStorage.setItem('token', 'tenant-user-token');
  });

  it('posts the controlled prepare payload to the dedicated lifecycle route', async () => {
    const fetchMock = vi.fn().mockResolvedValue(new Response(JSON.stringify({ id: 'plan-1', status: 'Prepared' }), {
      status: 200,
      headers: { 'Content-Type': 'application/json' },
    }));
    vi.stubGlobal('fetch', fetchMock);

    await emergencyProcurementPlanService.prepareException('plan-1', {
      purchaseRequisitionId: 'pr-1',
      exceptionRuleId: 'rule-1',
      centralDocumentVersionId: 'dms-1',
      justification: 'Critical emergency and budget exception.',
      rowVersion: 'AQID',
    });

    expect(fetchMock).toHaveBeenCalledWith(
      '/api/procurement/emergencyprocurementplans/plan-1/exception/prepare',
      expect.objectContaining({
        method: 'POST',
        headers: expect.objectContaining({ Authorization: 'Bearer tenant-user-token' }),
        body: JSON.stringify({
          purchaseRequisitionId: 'pr-1',
          exceptionRuleId: 'rule-1',
          centralDocumentVersionId: 'dms-1',
          justification: 'Critical emergency and budget exception.',
          rowVersion: 'AQID',
        }),
      }),
    );
  });

  it('allows rejection without inventing an authority approval reference', async () => {
    const fetchMock = vi.fn().mockResolvedValue(new Response(JSON.stringify({ id: 'plan-1', status: 'Rejected' }), {
      status: 200,
      headers: { 'Content-Type': 'application/json' },
    }));
    vi.stubGlobal('fetch', fetchMock);

    await emergencyProcurementPlanService.decide('plan-1', {
      rowVersion: 'AQID',
      action: 'Reject',
      comments: 'The evidence was not sufficient.',
    });

    const body = JSON.parse(String(fetchMock.mock.calls[0][1]?.body));
    expect(body.action).toBe('Reject');
    expect(body).not.toHaveProperty('approvalReference');
  });

  it('surfaces safe ProblemDetails rather than a generic or stack-trace message', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response(JSON.stringify({
      detail: 'The current user is not independent of the initiator.',
      code: 'SOD_CONFLICT',
    }), { status: 403, headers: { 'Content-Type': 'application/problem+json' } })));

    await expect(emergencyProcurementPlanService.triggerGoverned('plan-1', 'AQID'))
      .rejects.toThrow('The current user is not independent of the initiator. (SOD_CONFLICT)');
  });
});
