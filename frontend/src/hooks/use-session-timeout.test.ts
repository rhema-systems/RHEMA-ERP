import { describe, expect, it } from 'vitest';
import { calculateSessionTiming } from './use-session-timeout';

describe('session timeout timestamp calculations', () => {
  const timeoutMs = 30 * 60 * 1000;

  it('keeps a background tab active when another tab records later activity', () => {
    const originalDeadline = 1_000 + timeoutMs;
    const sharedActivity = originalDeadline - 60_000;
    const resumedAt = originalDeadline + 1_000;

    expect(calculateSessionTiming(sharedActivity, resumedAt, timeoutMs)).toMatchObject({
      expired: false,
      showWarning: false,
    });
  });

  it('expires from elapsed wall-clock time after all tabs are idle', () => {
    const lastActivity = 10_000;
    const resumedAfterTimerThrottling = lastActivity + timeoutMs + 5 * 60 * 1000;

    expect(calculateSessionTiming(lastActivity, resumedAfterTimerThrottling, timeoutMs)).toEqual({
      expired: true,
      showWarning: false,
      remainingSeconds: 0,
    });
  });

  it('derives the warning countdown from the actual deadline', () => {
    const lastActivity = 20_000;
    const now = lastActivity + timeoutMs - 90_250;

    expect(calculateSessionTiming(lastActivity, now, timeoutMs)).toEqual({
      expired: false,
      showWarning: true,
      remainingSeconds: 91,
    });
  });
});
