import { describe, expect, it } from 'vitest';
import {
  getDashboardLoadErrorPresentation,
  getHttpStatus,
  isDashboardAccessError,
} from './dashboard-load-error';

describe('dashboard load error presentation', () => {
  it('recognizes top-level and response authorization statuses', () => {
    expect(getHttpStatus({ status: 403 })).toBe(403);
    expect(getHttpStatus({ response: { status: 401 } })).toBe(401);
    expect(isDashboardAccessError({ status: 403 })).toBe(true);
    expect(isDashboardAccessError(new Error('network'))).toBe(false);
  });

  it('renders a non-retryable permission message for forbidden responses', () => {
    expect(getDashboardLoadErrorPresentation({ status: 403 })).toEqual({
      accessDenied: true,
      message:
        'You do not have permission to view the enterprise dashboard. Ask an administrator for Dashboard access.',
      canRetry: false,
    });
  });

  it('keeps retry available for transient failures', () => {
    expect(getDashboardLoadErrorPresentation(new Error('network'))).toEqual({
      accessDenied: false,
      message: 'The enterprise dashboard could not be loaded. Please retry.',
      canRetry: true,
    });
  });
});
