import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { describe, expect, it } from 'vitest';

import { getTenderStageVisibility } from './procurement-tender-header-actions';

const pageSource = readFileSync(
  resolve(process.cwd(), 'src/app/procurement/tenders/[id]/page.tsx'),
  'utf8'
);

// Source-wiring guard complements the visible-browser check: fixing lifecycle
// visibility must not mount the redundant shared header navigation again.
describe('tender detail navigation ownership', () => {
  it('does not mount duplicate stage shortcuts in the tender header', () => {
    expect(pageSource).not.toContain('TenderHeaderControlActions');
    expect(pageSource).toContain(
      'Stage navigation belongs to the process sidebar'
    );
  });

  it('retains the Award tab and shared evaluated-stage visibility', () => {
    expect(pageSource).toContain('getTenderStageVisibility(tender.status)');
    expect(pageSource).toContain('<TabsTrigger value="award">');
    expect(pageSource).toContain('<TenderAward');
    expect(getTenderStageVisibility('Evaluated')).toEqual({
      publishedStage: true,
      awardStage: true,
    });
  });
});
