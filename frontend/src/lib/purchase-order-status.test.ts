import { describe, expect, it } from 'vitest';
import {
  getPurchaseOrderStatusPresentation,
  isPurchaseOrderStatus,
} from './purchase-order-status';

describe('purchase-order status presentation', () => {
  it.each(['PartiallyReceived', 'Partially Received', 'partially_received'])(
    'normalizes %s to the visible partially-received status',
    (status) => {
      const result = getPurchaseOrderStatusPresentation(status);

      expect(result.key).toBe('Partially Received');
      expect(result.label).toBe('Partially Received');
      expect(result.badgeClass).toContain('text-purple-950');
      expect(isPurchaseOrderStatus(status, 'Partially Received')).toBe(true);
    }
  );

  it.each(['PendingApproval', 'Pending Approval', 'pending-approval'])(
    'normalizes %s to pending approval',
    (status) => {
      expect(getPurchaseOrderStatusPresentation(status).key).toBe('Pending Approval');
    }
  );

  it('gives unknown server values a readable label and explicit contrast', () => {
    const result = getPurchaseOrderStatusPresentation('AwaitingDispatch');

    expect(result.label).toBe('Awaiting Dispatch');
    expect(result.badgeClass).toContain('text-slate-950');
  });
});
