import { describe, expect, it } from 'vitest';

import { resolveRecallPrompt } from './WorkflowApprovalActions';

/**
 * These pin the blast radius of the recall reason box rather than its behaviour.
 *
 * WorkflowApprovalActions is shared by many screens with several different owners, so a change to
 * the recall dialog is a change to every one of their modules unless it is opted into.
 * If a later edit makes the reason box the default, the first case here fails.
 */
describe('resolveRecallPrompt', () => {
  it('leaves a screen that asked for nothing on the confirm dialog it has always had', () => {
    expect(resolveRecallPrompt(undefined, false)).toBe('confirm');
  });

  it('gives the reason box to a screen whose owner wired their own recall command', () => {
    expect(resolveRecallPrompt(undefined, true)).toBe('reason');
  });

  it('lets a screen opt in without wiring a recall command', () => {
    expect(resolveRecallPrompt('reason', false)).toBe('reason');
  });

  it('lets a screen opt back out even though it wired a recall command', () => {
    expect(resolveRecallPrompt('confirm', true)).toBe('confirm');
  });
});
