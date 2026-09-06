import { describe, expect, it } from 'vitest';
import { hasAwardVerificationEvidence } from './award-verification-evidence';

describe('independent award verification evidence rule', () => {
  it.each([
    [false, 'Reviewed existing records', [], true],
    [false, '', [], false],
    [true, 'Reviewed existing records', [], false],
    [true, '', [{}], true],
    [false, '', [{}], true],
  ])('validates document=%s and review notes', (requiresDocument, comments, documents, expected) => {
    expect(hasAwardVerificationEvidence({ isRequired: true, requiresDocument: requiresDocument as boolean,
      comments: comments as string, documents: documents as unknown[] })).toBe(expected);
  });
});
