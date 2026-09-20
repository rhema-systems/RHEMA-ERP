import { describe, expect, it } from 'vitest';
import { shouldUseApprovalSubmitCopy } from './WorkflowApprovalActions';

describe('workflow submission copy', () => {
  it('keeps Finalize for ordinary direct lifecycle actions', () => {
    expect(shouldUseApprovalSubmitCopy(true)).toBe(false);
  });

  it('does not let a legacy copy override imply an approval the server says is not required', () => {
    expect(shouldUseApprovalSubmitCopy(true, 'approval')).toBe(false);
  });

  it('uses approval copy when an approval workflow is active', () => {
    expect(shouldUseApprovalSubmitCopy(false)).toBe(true);
  });
});
