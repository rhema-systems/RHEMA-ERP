import { beforeEach, describe, expect, it, vi } from 'vitest';
import { contractService } from './contractService';

describe('contract operations client', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
    localStorage.clear();
    localStorage.setItem('token', 'internal-token');
  });

  it('loads the tenant-safe portfolio with operational filters', async () => {
    const fetchMock = vi.fn().mockResolvedValue(
      new Response(
        JSON.stringify({
          generatedAtUtc: '2026-07-30T12:00:00Z',
          totalContracts: 1,
          activeContracts: 1,
          contractsWithPrompts: 1,
          criticalPromptCount: 0,
          currencyTotals: [],
          items: [],
          decisionKeys: ['DEC-001', 'DEC-014'],
        }),
        { status: 200, headers: { 'Content-Type': 'application/json' } }
      )
    );
    vi.stubGlobal('fetch', fetchMock);

    await contractService.getContractOperations(
      'supplier', 'Active', 'High', 25
    );

    expect(fetchMock).toHaveBeenCalledWith(
      '/api/procurement/contract-operations?take=25&search=supplier&status=Active&risk=High',
      expect.objectContaining({
        cache: 'no-store',
        headers: expect.objectContaining({
          Authorization: 'Bearer internal-token',
        }),
      })
    );
  });

  it('loads exact contract operations detail', async () => {
    const fetchMock = vi.fn().mockResolvedValue(
      new Response(
        JSON.stringify({
          summary: { contractId: 'contract-0408' },
          prompts: [],
          decisionKeys: ['DEC-001', 'DEC-014'],
        }),
        { status: 200, headers: { 'Content-Type': 'application/json' } }
      )
    );
    vi.stubGlobal('fetch', fetchMock);

    await contractService.getContractOperationsDetail('contract-0408');

    expect(fetchMock).toHaveBeenCalledWith(
      '/api/procurement/contract-operations/contract-0408',
      expect.objectContaining({
        cache: 'no-store',
        headers: expect.objectContaining({
          Authorization: 'Bearer internal-token',
        }),
      })
    );
  });

  it('publishes prompts without sending a client-controlled amount', async () => {
    const fetchMock = vi.fn().mockResolvedValue(
      new Response(
        JSON.stringify({
          evaluatedPromptCount: 2,
          publishedAlertCount: 2,
          alreadyPublishedCount: 0,
          publishedPromptKeys: ['one', 'two'],
        }),
        { status: 200, headers: { 'Content-Type': 'application/json' } }
      )
    );
    vi.stubGlobal('fetch', fetchMock);

    await contractService.processContractOperationsAlerts('contract-0408');

    expect(fetchMock).toHaveBeenCalledWith(
      '/api/procurement/contract-operations/process-alerts',
      expect.objectContaining({
        method: 'POST',
        headers: expect.objectContaining({
          Authorization: 'Bearer internal-token',
          'X-Correlation-ID': expect.any(String),
        }),
        body: '{"contractId":"contract-0408"}',
      })
    );
    expect(fetchMock.mock.calls[0][1].body).not.toContain('amount');
  });
});
