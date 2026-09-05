import React from 'react';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

const mocks = vi.hoisted(() => ({
  toast: vi.fn(),
  writeText: vi.fn(),
}));

vi.mock('../ui/use-toast', () => ({
  useToast: () => ({ toast: mocks.toast }),
}));

vi.mock('@/hooks/use-auth', () => ({
  useAuth: () => ({ hasPermission: () => true }),
}));

import SystemExceptionLogs from './SystemExceptionLogs';

const logItem = {
  id: 'exception-1',
  level: 'Critical',
  fingerprint: 'fingerprint-1',
  occurrenceCount: 1,
  logger: 'SupplierApplicantAccessController',
  shortMessage: 'Supplier contact correction failed',
  requestMethod: 'POST',
  requestPath: '/api/procurement/contact-correction/confirm',
  username: 'administrator',
  traceId: 'trace-1',
  isResolved: false,
  createdAt: '2026-08-09T15:00:00Z',
  firstOccurredAt: '2026-08-09T15:00:00Z',
  lastOccurredAt: '2026-08-09T15:00:00Z',
};

describe('SystemExceptionLogs detail UX', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    localStorage.setItem('token', 'test-token');
    Object.defineProperty(navigator, 'clipboard', {
      configurable: true,
      value: { writeText: mocks.writeText },
    });
    mocks.writeText.mockResolvedValue(undefined);
    vi.stubGlobal(
      'fetch',
      vi
        .fn()
        .mockResolvedValueOnce({
          ok: true,
          json: async () => ({ items: [logItem], totalCount: 1 }),
        })
        .mockResolvedValueOnce({
          ok: true,
          json: async () => ({
            ...logItem,
            fullMessage:
              'Microsoft.EntityFrameworkCore.DbUpdateException: an-extremely-long-unbroken-value-that-must-remain-inside-the-dialog',
          }),
        })
    );
  });

  it('hides occurrence count and keeps the full message wrappable and copyable', async () => {
    render(<SystemExceptionLogs />);

    await screen.findByText(logItem.shortMessage);
    expect(fetch).toHaveBeenCalledWith(
      expect.stringMatching(/\/api\/admin\/system-exception-logs\?/),
      expect.objectContaining({ headers: expect.any(Object) })
    );
    expect(
      screen.queryByRole('columnheader', { name: 'Count' })
    ).not.toBeInTheDocument();

    fireEvent.click(screen.getByRole('button', { name: 'View' }));
    const fullMessage = await screen.findByText(
      /Microsoft\.EntityFrameworkCore\.DbUpdateException/
    );
    expect(fullMessage).toHaveClass('whitespace-pre-wrap');
    expect(fullMessage).toHaveClass('break-words');
    expect(fullMessage).toHaveClass('[overflow-wrap:anywhere]');

    fireEvent.click(screen.getByRole('button', { name: 'Copy' }));
    await waitFor(() => {
      expect(mocks.writeText).toHaveBeenCalledWith(
        expect.stringContaining('an-extremely-long-unbroken-value')
      );
    });
  });
});
