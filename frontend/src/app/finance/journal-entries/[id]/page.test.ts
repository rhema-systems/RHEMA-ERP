import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { describe, expect, it } from 'vitest';

const pageSource = readFileSync(
  resolve(process.cwd(), 'src/app/finance/journal-entries/[id]/page.tsx'),
  'utf8'
);

describe('Journal entry action visibility', () => {
  it('never offers posting while a manual journal is still Draft', () => {
    const draftActions = pageSource.slice(
      pageSource.indexOf('/* Draft Actions:'),
      pageSource.indexOf('/* Pending Approval Actions:')
    );

    expect(draftActions).toContain("entry.postingStatus === 'Draft'");
    expect(draftActions).not.toContain('canPost');
    expect(draftActions).not.toContain('handlePost');
    expect(draftActions).not.toContain('Post Entry');
  });

  it('offers posting only from the Approved action state', () => {
    const approvedActions = pageSource.slice(
      pageSource.indexOf('/* Approved Actions:'),
      pageSource.indexOf('/* Posted Actions:')
    );

    expect(approvedActions).toContain(
      "entry.postingStatus === 'Approved' && canPost"
    );
    expect(approvedActions).toContain('onClick={handlePost}');
    expect(approvedActions).toContain('Post to General Ledger');
  });

  it('labels posted budget evidence as an immutable posting snapshot', () => {
    expect(pageSource).toContain('budgetControl.isPostingSnapshot');
    expect(pageSource).toContain(
      'Amounts below are the immutable budget evidence captured for this posting.'
    );
    expect(pageSource).toContain("'Posted before entry'");
  });
});
