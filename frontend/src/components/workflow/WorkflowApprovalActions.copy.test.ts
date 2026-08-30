import { describe, expect, it } from 'vitest';
import { shouldUseApprovalSubmitCopy } from './WorkflowApprovalActions';

describe('workflow submission copy', () => {
  it('keeps Finalize for ordinary direct lifecycle actions', () => {
    expect(shouldUseApprovalSubmitCopy(true)).toBe(false);
  });

  it('uses Submit for Approval when the caller explicitly requires approval copy', () => {
    expect(shouldUseApprovalSubmitCopy(true, 'approval')).toBe(true);
  });

  it('uses approval copy when an approval workflow is active', () => {
    expect(shouldUseApprovalSubmitCopy(false)).toBe(true);
  });
});
