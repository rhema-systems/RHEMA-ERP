import { describe, expect, it } from 'vitest';
import { canDecideInventoryRecord, canPostInventoryRecord } from './inventory-approval-actions';

describe('inventory optional approval action visibility', () => {
  const maker = 'maker';
  it('allows direct Post for a permitted maker without inventing a human approval', () => {
    expect(canPostInventoryRecord({ status: 'ReadyToPost', approvalRequired: false, requestedById: maker }, maker, true)).toBe(true);
  });
  it.each([undefined, true])('keeps maker-checker posting when approvalRequired=%s', approvalRequired => {
    const record = { status: 'Approved', approvalRequired, requestedById: maker };
    expect(canPostInventoryRecord(record, maker, true)).toBe(false);
    expect(canPostInventoryRecord(record, 'poster', true)).toBe(true);
    expect(canPostInventoryRecord({ ...record, status: 'ReadyToPost' }, 'poster', true)).toBe(false);
  });
  it.each(['Draft', 'PendingApproval', 'Approved', 'Posted', 'Reversed'])('does not post a no-approval record in %s', status => {
    expect(canPostInventoryRecord({ status, approvalRequired: false, requestedById: maker }, maker, true)).toBe(false);
  });
  it('still requires an authenticated posting operator with the posting capability', () => {
    const record = { status: 'ReadyToPost', approvalRequired: false, requestedById: maker };
    expect(canPostInventoryRecord(record, maker, false)).toBe(false);
    expect(canPostInventoryRecord(record, undefined, true)).toBe(false);
    expect(canPostInventoryRecord({ ...record, requestedById: undefined }, maker, true)).toBe(false);
  });
  it('only exposes approval decisions for an independent approver on a required pending record', () => {
    const record = { status: 'PendingApproval', requestedById: maker };
    expect(canDecideInventoryRecord(record, 'approver', true)).toBe(true);
    expect(canDecideInventoryRecord(record, 'MAKER', true)).toBe(false);
    expect(canDecideInventoryRecord(record, 'approver', false)).toBe(false);
    expect(canDecideInventoryRecord({ ...record, approvalRequired: false }, 'approver', true)).toBe(false);
  });
});
