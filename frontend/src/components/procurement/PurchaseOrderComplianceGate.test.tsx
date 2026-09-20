import React from 'react';
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { PurchaseOrderComplianceGate } from './PurchaseOrderComplianceGate';
import { purchasingService } from '@/services/purchasingService';

vi.mock('@/services/purchasingService', () => ({ purchasingService: { getPurchaseOrderComplianceReadiness: vi.fn() } }));
afterEach(() => { cleanup(); vi.clearAllMocks(); });

describe('PurchaseOrderComplianceGate disclosure', () => {
  it('evaluates while collapsed, shows the blocker, and retains refresh and detailed checks', async () => {
    const onReadinessChange = vi.fn();
    const readiness = {
      isCompliant: false,
      blockedReasons: ['Upload the signed contract'],
      evaluatedAtUtc: '2026-09-09T00:00:00Z',
      checks: [
        { key: 'supplier', label: 'Supplier eligible', passed: true, required: true, message: 'Supplier evidence retained' },
        { key: 'signature', label: 'Contract signatures', passed: false, required: true, message: 'Signature evidence missing' },
      ],
    };
    vi.mocked(purchasingService.getPurchaseOrderComplianceReadiness).mockResolvedValue(readiness as any);
    render(<PurchaseOrderComplianceGate purchaseOrderId="po-1" status="Approved" onReadinessChange={onReadinessChange} />);
    const trigger = await screen.findByRole('button', { name: /PO compliance gate/ });
    expect(trigger).toHaveAttribute('aria-expanded', 'false');
    expect(onReadinessChange).toHaveBeenCalledWith(readiness);
    expect(screen.getByText('Upload the signed contract')).toBeVisible();
    expect(screen.getByText('Supplier evidence retained')).not.toBeVisible();
    fireEvent.click(trigger);
    expect(screen.getByText('Supplier evidence retained')).toBeVisible();
    fireEvent.click(trigger);
    fireEvent.click(screen.getByRole('button', { name: 'Refresh compliance readiness' }));
    await waitFor(() => expect(purchasingService.getPurchaseOrderComplianceReadiness).toHaveBeenCalledTimes(2));
  });

  it('keeps error recovery visible and never reports ready when loading fails', async () => {
    vi.mocked(purchasingService.getPurchaseOrderComplianceReadiness).mockRejectedValue(new Error('Service unavailable'));
    const onReadinessChange = vi.fn();
    render(<PurchaseOrderComplianceGate purchaseOrderId="po-2" status="Draft" onReadinessChange={onReadinessChange} />);
    expect(await screen.findByText('Compliance readiness unavailable')).toBeVisible();
    expect(screen.getByRole('button', { name: 'Retry' })).toBeVisible();
    expect(onReadinessChange).toHaveBeenCalledWith(null);
    expect(screen.queryByText('Ready to progress')).not.toBeInTheDocument();
  });
});
