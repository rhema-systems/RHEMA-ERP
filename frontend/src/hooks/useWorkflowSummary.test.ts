import { act, renderHook, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { useWorkflowSummary } from './useWorkflowSummary';
import { workflowApiService } from '@/services/workflow-api.service';
import type { WorkflowEntitySummaryDto } from '@/types/workflow';

vi.mock('@/services/workflow-api.service', () => ({
  workflowApiService: { getWorkflowEntitySummary: vi.fn() },
}));

const summary = (entityId = 'one'): WorkflowEntitySummaryDto => ({
  entityType: 'PurchaseOrder', entityId, approvalRequired: false, hasActiveInstance: false,
  hasWorkflowHistory: false, canCurrentUserApprove: false, pendingApprovers: [],
});

describe('scoped workflow policy loading', () => {
  beforeEach(() => vi.resetAllMocks());

  it('loads the full summary including active instance and history flags', async () => {
    vi.mocked(workflowApiService.getWorkflowEntitySummary).mockResolvedValue({
      ...summary(), hasActiveInstance: true, hasWorkflowHistory: true,
    });
    const { result } = renderHook(() => useWorkflowSummary({ entityType: 'PurchaseOrder', entityId: 'one' }));
    expect(result.current.visibility.known).toBe(false);
    await waitFor(() => expect(result.current.visibility.active).toBe(true));
    expect(result.current.visibility.direct).toBe(false);
    expect(result.current.summary?.hasWorkflowHistory).toBe(true);
  });

  it('cannot treat a failed lookup as no approval required', async () => {
    vi.mocked(workflowApiService.getWorkflowEntitySummary).mockRejectedValue(new Error('Forbidden'));
    const { result } = renderHook(() => useWorkflowSummary({ entityType: 'PurchaseOrder', entityId: 'one' }));
    await waitFor(() => expect(result.current.error).toBe('Forbidden'));
    expect(result.current.visibility).toMatchObject({ known: false, direct: false, showTab: true });
  });

  it.each([
    ['PurchaseRequisition', 'PURCHASE_REQUISITION'],
    ['InventoryRequisition', 'INVENTORY_REQUISITION'],
    ['StockAdjustment', 'STOCK_ADJUSTMENT'],
  ])('accepts the configured %s alias %s from older APIs', async (entityType, configuredCode) => {
    vi.mocked(workflowApiService.getWorkflowEntitySummary).mockResolvedValue({
      ...summary(), entityType: configuredCode,
    });
    const { result } = renderHook(() => useWorkflowSummary({ entityType, entityId: 'one' }));
    await waitFor(() => expect(result.current.visibility.direct).toBe(true));
    expect(result.current.error).toBeUndefined();
  });

  it('does not accept a different entity even when its record ID matches', async () => {
    vi.mocked(workflowApiService.getWorkflowEntitySummary).mockResolvedValue({
      ...summary(), entityType: 'PURCHASE_REQUISITION',
    });
    const { result } = renderHook(() => useWorkflowSummary({ entityType: 'PurchaseOrder', entityId: 'one' }));
    await waitFor(() => expect(result.current.error).toContain('did not match'));
    expect(result.current.visibility.direct).toBe(false);
  });

  it('does not normalize record ID punctuation when accepting an entity alias', async () => {
    vi.mocked(workflowApiService.getWorkflowEntitySummary).mockResolvedValue({
      ...summary('on-e'), entityType: 'STOCK_ADJUSTMENT',
    });
    const { result } = renderHook(() => useWorkflowSummary({ entityType: 'StockAdjustment', entityId: 'one' }));
    await waitFor(() => expect(result.current.error).toContain('did not match'));
    expect(result.current.visibility.direct).toBe(false);
  });

  it('rejects a summary belonging to another entity', async () => {
    vi.mocked(workflowApiService.getWorkflowEntitySummary).mockResolvedValue(summary('two'));
    const { result } = renderHook(() => useWorkflowSummary({ entityType: 'PurchaseOrder', entityId: 'one' }));
    await waitFor(() => expect(result.current.error).toContain('did not match'));
    expect(result.current.summary).toBeUndefined();
    expect(result.current.visibility.direct).toBe(false);
  });

  it('does not reuse old record policy while a different record is loading', async () => {
    vi.mocked(workflowApiService.getWorkflowEntitySummary).mockResolvedValueOnce(summary());
    let resolveNext!: (value: WorkflowEntitySummaryDto) => void;
    vi.mocked(workflowApiService.getWorkflowEntitySummary).mockImplementationOnce(() => new Promise(resolve => { resolveNext = resolve; }));
    const { result, rerender } = renderHook(({ id }) => useWorkflowSummary({ entityType: 'PurchaseOrder', entityId: id }), {
      initialProps: { id: 'one' },
    });
    await waitFor(() => expect(result.current.visibility.direct).toBe(true));
    rerender({ id: 'two' });
    expect(result.current.summary).toBeUndefined();
    expect(result.current.visibility.direct).toBe(false);
    await act(async () => resolveNext({ ...summary('two'), approvalRequired: true }));
    expect(result.current.visibility.approvalRequired).toBe(true);
  });

  it('invalidates known no-approval while refreshing and after refresh failure', async () => {
    vi.mocked(workflowApiService.getWorkflowEntitySummary).mockResolvedValueOnce(summary()).mockRejectedValueOnce(new Error('Offline'));
    const { result } = renderHook(() => useWorkflowSummary({ entityType: 'PurchaseOrder', entityId: 'one' }));
    await waitFor(() => expect(result.current.visibility.direct).toBe(true));
    await act(async () => { await result.current.refresh(); });
    expect(result.current.visibility.direct).toBe(false);
    expect(result.current.error).toBe('Offline');
  });

  it('uses an externally managed lookup without starting a second request', () => {
    const { result } = renderHook(() => useWorkflowSummary({
      entityType: 'PurchaseOrder', entityId: 'one', loadWorkflowSummary: false,
      workflowSummary: summary(), workflowSummaryLoading: true,
    }));
    expect(result.current.visibility.direct).toBe(false);
    expect(workflowApiService.getWorkflowEntitySummary).not.toHaveBeenCalled();
  });
});
