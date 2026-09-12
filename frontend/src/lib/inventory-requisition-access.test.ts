import { describe, expect, it } from 'vitest';
import { canIssueRequisition } from './inventory-requisition-access';

describe('requisition issue presentation', () => {
  const requisition = { status: 5, requestedById: 'requester', approvedById: 'approver' };
  it('keeps issue unavailable to the requester and approver even if they also hold issue permission', () => {
    expect(canIssueRequisition(requisition, 'REQUESTER', true)).toBe(false);
    expect(canIssueRequisition(requisition, 'approver', true)).toBe(false);
  });
  it('requires a known actor and issue permission', () => {
    expect(canIssueRequisition(requisition, 'manager', false)).toBe(false);
    expect(canIssueRequisition(requisition, undefined, true)).toBe(false);
  });
  it.each([3, 4, 5, 'Approved', 'InProgress', 'PartiallyIssued'])('allows the independent issuer for status %s', status => {
    expect(canIssueRequisition({ ...requisition, status }, 'manager', true)).toBe(true);
  });
  it.each([1, 2, 6, 7, 8, 9, 'Draft', 'Issued'])('does not allow issue for status %s', status => {
    expect(canIssueRequisition({ ...requisition, status }, 'manager', true)).toBe(false);
  });
});
