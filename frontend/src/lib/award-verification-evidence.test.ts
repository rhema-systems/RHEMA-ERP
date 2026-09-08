import { describe, expect, it } from 'vitest';
import { hasAwardVerificationEvidence } from './award-verification-evidence';

describe('independent award verification evidence rule', () => {
  it.each([
    [false, 'Reviewed existing records', [], true],
    [false, '', [], true],
    [false, '   ', [], true],
    [true, 'Reviewed existing records', [], false],
    [true, '', [{}], true],
    [false, '', [{}], true],
  ])('enforces document=%s without requiring comments', (requiresDocument, comments, documents, expected) => {
    expect(hasAwardVerificationEvidence({ isRequired: true, requiresDocument: requiresDocument as boolean,
      comments: comments as string, documents: documents as unknown[] })).toBe(expected);
  });
});
