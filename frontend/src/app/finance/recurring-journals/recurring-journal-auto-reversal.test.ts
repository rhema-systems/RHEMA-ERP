import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { describe, expect, it } from 'vitest';

const detailSource = readFileSync(
  resolve(process.cwd(), 'src/app/finance/recurring-journals/[id]/page.tsx'),
  'utf8'
);
const createSource = readFileSync(
  resolve(process.cwd(), 'src/app/finance/recurring-journals/new/page.tsx'),
  'utf8'
);
const serviceSource = readFileSync(
  resolve(process.cwd(), 'src/services/finance/recurring-journal-data.service.ts'),
  'utf8'
);

describe('Recurring journal automatic reversal controls', () => {
  it('makes the checker authorization and automatic posting consequence explicit', () => {
    expect(createSource).toContain('Automatic reversal');
    expect(createSource).toContain('the checker also authorizes its exact reversing journal');
    expect(detailSource).toContain('Approve + authorize reversal');
    expect(detailSource).toContain('Any changed value requires the ordinary controlled correction workflow.');
  });

  it('shows reversal state, links, attempts, errors and permission-controlled recovery', () => {
    expect(detailSource).toContain('occurrence.reversalDueDate');
    expect(detailSource).toContain('occurrence.reversalStatus');
    expect(detailSource).toContain('occurrence.reversalJournalEntryId');
    expect(detailSource).toContain('occurrence.reversalAttemptCount');
    expect(detailSource).toContain('occurrence.reversalError');
    expect(detailSource).toContain("hasPermission('Finance.JournalEntries.Post')");
    expect(detailSource).toContain("occurrence.reversalStatus === 'Failed'");
    expect(serviceSource).toContain('/retry-reversal');
  });
});
