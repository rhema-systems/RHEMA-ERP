import { beforeEach, describe, expect, it, vi } from 'vitest';

import { payrollService } from './payrollService';

describe('payroll journal API client', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
    localStorage.clear();
  });

  it('posts the selected payroll run with the supplied notes', async () => {
    const fetchMock = vi.fn().mockResolvedValue(
      new Response(
        JSON.stringify({
          payrollRunId: 'run-1',
          journalEntryId: 'journal-1',
          journalNumber: 'PAY-PAY-2-001',
        }),
        { status: 200, headers: { 'Content-Type': 'application/json' } }
      )
    );
    vi.stubGlobal('fetch', fetchMock);

    await payrollService.postJournal('run-1', 'Posted from payroll desk');

    expect(fetchMock).toHaveBeenCalledWith(
      '/api/hr/payroll/runs/run-1/post-journal',
      expect.objectContaining({
        method: 'POST',
        body: JSON.stringify({ notes: 'Posted from payroll desk' }),
      })
    );
  });

  it('surfaces the ProblemDetails detail and code', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn().mockResolvedValue(
        new Response(
          JSON.stringify({
            detail:
              'Something went wrong while processing your payroll request. Reference ID: payroll-trace.',
            code: 'PAYROLL_UNEXPECTED',
            correlationId: 'payroll-trace',
          }),
          {
            status: 500,
            headers: { 'Content-Type': 'application/problem+json' },
          }
        )
      )
    );

    await expect(payrollService.postJournal('run-1')).rejects.toThrow(
      'Something went wrong while processing your payroll request. Reference ID: payroll-trace. (PAYROLL_UNEXPECTED)'
    );
  });
});
