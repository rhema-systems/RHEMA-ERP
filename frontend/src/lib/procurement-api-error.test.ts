import { describe, expect, it } from 'vitest';

import { throwProcurementResponseError } from './procurement-api-error';

describe('procurement response errors', () => {
  it('surfaces ProblemDetails detail and code', async () => {
    const response = new Response(
      JSON.stringify({
        detail: 'The assigned evaluation committee is not ready.',
        code: 'EVALUATION_COMMITTEE_NOT_READY',
      }),
      { status: 422 }
    );

    await expect(
      throwProcurementResponseError(response, 'Failed to submit evaluation')
    ).rejects.toThrow(
      'The assigned evaluation committee is not ready. (EVALUATION_COMMITTEE_NOT_READY)'
    );
  });

  it('preserves a plain-text server failure', async () => {
    const response = new Response('The award has already been cancelled.', {
      status: 409,
    });

    await expect(
      throwProcurementResponseError(response, 'Failed to cancel award')
    ).rejects.toThrow('The award has already been cancelled.');
  });
});
