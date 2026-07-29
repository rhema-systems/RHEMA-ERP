import { describe, expect, it } from 'vitest';

import {
  frameworkActionState,
  frameworkRemainingCeilingNote,
  frameworkStatusTone,
} from './procurement-framework-agreement';
import type { FrameworkAgreement } from '@/types/procurement-framework-agreement';

const agreement = (allowedActions: string[] = []): FrameworkAgreement =>
  ({
    allowedActions,
    ceilingAmount: 1000,
    availableCeiling: 1000,
  }) as FrameworkAgreement;

describe('framework agreement presentation contract', () => {
  it('uses server-supplied allowed actions instead of inferring mutations', () => {
    const actions = frameworkActionState(
      agreement(['edit', 'submit', 'add-document'])
    );

    expect(actions.canEdit).toBe(true);
    expect(actions.canSubmit).toBe(true);
    expect(actions.canApprove).toBe(false);
    expect(actions.canRequestExtension).toBe(false);
  });

  it('distinguishes effective publication from terminal outcomes', () => {
    expect(frameworkStatusTone('Published')).toBe('default');
    expect(frameworkStatusTone('PendingApproval')).toBe('secondary');
    expect(frameworkStatusTone('Terminated')).toBe('destructive');
  });

  it('does not imply call-off spend deductions in the TDC-0401 UI', () => {
    expect(frameworkRemainingCeilingNote(agreement())).toContain(
      'No call-off deductions'
    );
  });
});
