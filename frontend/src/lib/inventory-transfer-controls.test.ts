import { describe, expect, it } from 'vitest';

import {
  getInventoryTransferControlCapability,
  getInventoryTransferProblemMessage,
  getInventoryTransferStatusLabel,
} from './inventory-transfer-controls';

const base = {
  status: 'Received',
  hasTransferPermission: true,
  currentUserId: 'independent-user',
  actions: [
    { actionType: 'Submitted', actorUserId: 'maker-user' },
    { actionType: 'Received', actorUserId: 'receiver-user' },
  ],
};

describe('inventory transfer controls', () => {
  it('shows direct readiness without calling it human approval', () => {
    expect(getInventoryTransferStatusLabel('Approved', false)).toBe('Ready to ship');
    expect(getInventoryTransferStatusLabel('Approved', true)).toBe('Approved');
    expect(getInventoryTransferStatusLabel('Approved')).toBe('Approved');
  });

  it('allows the same authorized operator to close a reconciled direct transfer', () => {
    expect(getInventoryTransferControlCapability({ ...base, approvalRequired: false, kind: 'close',
      currentUserId: 'receiver-user', hasOpenDiscrepancy: false })).toEqual({ allowed: true });
    expect(getInventoryTransferControlCapability({ ...base, approvalRequired: false, kind: 'close',
      currentUserId: 'receiver-user', hasTransferPermission: false, hasOpenDiscrepancy: false }).allowed).toBe(false);
  });
  it('allows an independent authorized user to resolve an open received discrepancy', () => {
    expect(getInventoryTransferControlCapability({
      ...base,
      kind: 'resolve',
      hasOpenDiscrepancy: true,
    })).toEqual({ allowed: true });
  });

  it('allows closure only after every discrepancy is resolved', () => {
    expect(getInventoryTransferControlCapability({
      ...base,
      kind: 'close',
      hasOpenDiscrepancy: true,
    })).toEqual({
      allowed: false,
      reason: 'Resolve every open discrepancy before closing the transfer.',
    });

    expect(getInventoryTransferControlCapability({
      ...base,
      kind: 'close',
      hasOpenDiscrepancy: false,
    })).toEqual({ allowed: true });
  });

  it('prevents a receipt participant from resolving or closing the same transfer', () => {
    const capability = getInventoryTransferControlCapability({
      ...base,
      kind: 'resolve',
      currentUserId: 'receiver-user',
      hasOpenDiscrepancy: true,
    });

    expect(capability.allowed).toBe(false);
    expect(capability.reason).toMatch(/independent authorized user/i);
  });

  it('surfaces ProblemDetails detail and code', () => {
    expect(getInventoryTransferProblemMessage({
      response: {
        data: {
          detail: 'The receiver cannot close the same transfer.',
          extensions: { code: 'TRANSFER_INDEPENDENT_CLOSURE_REQUIRED' },
        },
      },
    }, 'Closure failed.')).toBe(
      'The receiver cannot close the same transfer. (TRANSFER_INDEPENDENT_CLOSURE_REQUIRED)'
    );
  });
});
