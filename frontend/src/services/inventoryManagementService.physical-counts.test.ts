import axios from 'axios';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { inventoryManagementService } from './inventoryManagementService';

vi.mock('axios');
const mockedAxios = vi.mocked(axios, true);

beforeEach(() => {
  vi.clearAllMocks();
  localStorage.clear();
  localStorage.setItem('authToken', 'tenant-token');
  mockedAxios.get.mockResolvedValue({ data: [] });
  mockedAxios.post.mockResolvedValue({ data: {} });
});

describe('inventoryManagementService controlled physical counts', () => {
  it('searches committee employees using the selected warehouse and exact bin scope', async () => {
    await inventoryManagementService.getPhysicalCountCounterOptions('warehouse-1', ' Ama ', 'bin-1');
    expect(mockedAxios.get).toHaveBeenCalledWith(expect.stringMatching(/\/physical-counts\/counter-options$/), {
      headers: { Authorization: 'Bearer tenant-token', 'Content-Type': 'application/json' },
      params: { warehouseId: 'warehouse-1', search: 'Ama', locationId: 'bin-1' },
    });
  });

  it('refuses employee search without warehouse scope', async () => {
    await expect(inventoryManagementService.getPhysicalCountCounterOptions('')).rejects.toThrow('Select a warehouse');
    expect(mockedAxios.get).not.toHaveBeenCalled();
  });

  it('saves employee identities and concurrency controls without accepting caller actor or email', async () => {
    mockedAxios.put.mockResolvedValueOnce({ status: 204 });
    const request = { employeeIds: ['employee-1', 'employee-2'], rowVersion: 'AQID', idempotencyKey: 'committee-key', comment: 'Rotation' };
    await inventoryManagementService.updatePhysicalCountCounters('count-1', request);
    expect(mockedAxios.put).toHaveBeenCalledWith(expect.stringMatching(/\/physical-counts\/count-1\/counters$/), request, {
      headers: { Authorization: 'Bearer tenant-token', 'Content-Type': 'application/json' },
    });
  });

  it('loads decision options against the saved count, without accepting caller warehouse or location scope', async () => {
    await inventoryManagementService.getPhysicalCountDecisions(false, 'count-1');
    expect(mockedAxios.get).toHaveBeenCalledWith(expect.stringMatching(/\/physical-counts\/decision-options$/), {
      headers: { Authorization: 'Bearer tenant-token', 'Content-Type': 'application/json' }, params: { countId: 'count-1' },
    });
  });

  it('refuses an unscoped decision-options request before sending HTTP', async () => {
    await expect(inventoryManagementService.getPhysicalCountDecisions()).rejects.toThrow('Open a saved physical count');
    expect(mockedAxios.get).not.toHaveBeenCalled();
  });

  it('keeps administrative decision setup separate from count-scoped decision options', async () => {
    await inventoryManagementService.getPhysicalCountDecisions(true);
    expect(mockedAxios.get).toHaveBeenCalledWith(expect.stringMatching(/\/physical-counts\/decision-setup$/), {
      headers: { Authorization: 'Bearer tenant-token', 'Content-Type': 'application/json' },
    });
  });

  it('sends the actual count sheet and all concurrency versions as one multipart request', async () => {
    const file = new File(['workbook'], 'counts.xlsx');
    const lines = [{ id: 'line-1', rowVersion: 'BAUG' }];
    await inventoryManagementService.importPhysicalCountSheet('count-1', file, 'AQID', lines, 'upload-key');
    expect(mockedAxios.post).toHaveBeenCalledOnce();
    const [url, body, options] = mockedAxios.post.mock.calls[0];
    expect(url).toMatch(/\/physical-counts\/count-1\/count-sheet$/);
    expect((body as FormData).get('file')).toBe(file);
    expect((body as FormData).get('rowVersion')).toBe('AQID');
    expect(JSON.parse((body as FormData).get('lineVersions') as string)).toEqual(lines);
    expect((body as FormData).get('idempotencyKey')).toBe('upload-key');
    expect(options?.headers).toEqual({ Authorization: 'Bearer tenant-token' });
  });
  const control = { rowVersion: 'AQID', idempotencyKey: 'count-key', correlationId: 'count:1', comment: 'Controlled stage' };

  it('sends independent recount and the three explicit control stages', async () => {
    await inventoryManagementService.recordPhysicalCountRecount('count-1', {
      ...control, physicalCountItemId: 'line-1', itemRowVersion: 'BAUG', recountedQuantity: 7,
      investigationNotes: 'Independent recount reconciled the bin.',
    });
    const decision = { ...control, decisionCode: 'APPROVE', decisionRevision: 'decision-v1', approved: true };
    await inventoryManagementService.decidePhysicalCountStores('count-1', decision);
    await inventoryManagementService.decidePhysicalCountFinance('count-1', decision);
    await inventoryManagementService.attestPhysicalCountAudit('count-1', decision);

    expect(mockedAxios.post.mock.calls.map(call => call[0])).toEqual([
      expect.stringMatching(/\/count-1\/recount$/), expect.stringMatching(/\/count-1\/stores-decision$/),
      expect.stringMatching(/\/count-1\/finance-decision$/), expect.stringMatching(/\/count-1\/audit-attestation$/),
    ]);
    expect(mockedAxios.post.mock.calls[0][1]).toEqual(expect.objectContaining({ rowVersion: 'AQID', itemRowVersion: 'BAUG', idempotencyKey: 'count-key' }));
    for (const call of mockedAxios.post.mock.calls.slice(1)) expect(call[1]).toEqual(decision);
  });

  it('uses dedicated schedule and controlled Finance-post endpoints', async () => {
    await inventoryManagementService.getCycleCountSchedules();
    await inventoryManagementService.generateDueCycleCounts();
    await inventoryManagementService.postControlledPhysicalCount('count-1', control);

    expect(mockedAxios.get.mock.calls[0][0]).toMatch(/\/physical-counts\/cycle-schedules$/);
    expect(mockedAxios.post.mock.calls[0][0]).toMatch(/\/physical-counts\/cycle-schedules\/generate$/);
    expect(mockedAxios.post.mock.calls[1][0]).toMatch(/\/count-1\/controlled-post$/);
    expect(mockedAxios.post.mock.calls[1][1]).toEqual(expect.objectContaining({ rowVersion: 'AQID', idempotencyKey: 'count-key' }));
  });

  it('loads and uploads current stock-taking evidence through the scoped multipart endpoints', async () => {
    const evidence = [{ centralDocumentVersionId: 'version-1' }];
    mockedAxios.get.mockResolvedValueOnce({ data: evidence });
    mockedAxios.post.mockResolvedValueOnce({ data: evidence[0] });
    const file = new File(['signed count'], 'signed-count.pdf', { type: 'application/pdf' });

    await expect(inventoryManagementService.getPhysicalCountEvidence('count-1')).resolves.toEqual(evidence);
    await expect(inventoryManagementService.uploadPhysicalCountEvidence('count-1', file, ' Signed count sheet '))
      .resolves.toEqual(evidence[0]);

    expect(mockedAxios.get).toHaveBeenCalledWith(
      expect.stringMatching(/\/physical-counts\/count-1\/evidence$/),
      expect.objectContaining({ headers: expect.objectContaining({ Authorization: 'Bearer tenant-token' }) }),
    );
    const [url, body, options] = mockedAxios.post.mock.calls[0];
    expect(url).toMatch(/\/physical-counts\/count-1\/evidence$/);
    expect(body).toBeInstanceOf(FormData);
    expect((body as FormData).get('file')).toBe(file);
    expect((body as FormData).get('title')).toBe('Signed count sheet');
    expect(options?.headers).toEqual({ Authorization: 'Bearer tenant-token' });
  });
});
