import { describe, expect, it } from 'vitest';
import {
  buildTenderDocumentLifecycleEvidence,
  buildTenderDocumentLifecycleRequest,
  usesAttachedPublicationEvidence,
} from './tender-document-lifecycle';

const attachedId = '928c2fc0-37b4-4472-b2d8-57c8b8746bd9';
const persisted = {
  rowVersion: 'version-token',
  contentWorkflowEvidenceDocumentId: attachedId,
};

describe('tender document publication evidence reuse', () => {
  it('uses the exact persisted attached workflow document without an invented external reference', () => {
    expect(
      buildTenderDocumentLifecycleRequest(
        'publish',
        persisted,
        '  Publication checked  ',
        ''
      )
    ).toEqual({
      rowVersion: 'version-token',
      comment: 'Publication checked',
      evidence: [
        {
          referenceKind: 'WorkflowEvidenceDocument',
          referenceId: attachedId,
          label: 'Attached tender document used as publication evidence',
          requirementKey: 'SRC-006',
        },
      ],
    });
    expect(usesAttachedPublicationEvidence('publish', attachedId)).toBe(true);
  });

  it('retains an optional external reference alongside controlled publication evidence', () => {
    const request = buildTenderDocumentLifecycleRequest(
      'publish',
      persisted,
      '',
      '  Approval minute 42  '
    );
    expect(request.evidence).toHaveLength(2);
    expect(request.evidence[0]).toMatchObject({
      referenceKind: 'WorkflowEvidenceDocument',
      referenceId: attachedId,
    });
    expect(request.evidence[1]).toMatchObject({
      referenceKind: 'ExternalReference',
      reference: 'Approval minute 42',
    });
    expect(request.comment).toBeUndefined();
  });

  it.each(['submit', 'reject', 'retire'] as const)(
    'does not reuse attached content to bypass the existing %s evidence requirement',
    (action) => {
      expect(usesAttachedPublicationEvidence(action, attachedId)).toBe(false);
      expect(
        buildTenderDocumentLifecycleEvidence(action, attachedId, '')
      ).toEqual([]);
      expect(() =>
        buildTenderDocumentLifecycleRequest(action, persisted, '', '')
      ).toThrow('Shared approval evidence reference is required.');
      expect(
        buildTenderDocumentLifecycleRequest(
          action,
          persisted,
          '',
          'Existing reference'
        ).evidence
      ).toEqual([
        {
          referenceKind: 'ExternalReference',
          reference: 'Existing reference',
          label: `${action} tender-document version`,
          requirementKey: 'SRC-006',
        },
      ]);
    }
  );

  it.each([undefined, '', '   '])(
    'still requires publication evidence when no attached document exists (%s)',
    (contentWorkflowEvidenceDocumentId) => {
      expect(
        usesAttachedPublicationEvidence(
          'publish',
          contentWorkflowEvidenceDocumentId
        )
      ).toBe(false);
      expect(() =>
        buildTenderDocumentLifecycleRequest(
          'publish',
          { rowVersion: 'current', contentWorkflowEvidenceDocumentId },
          '',
          ' '
        )
      ).toThrow('Shared approval evidence reference is required.');
      expect(
        buildTenderDocumentLifecycleEvidence(
          'publish',
          contentWorkflowEvidenceDocumentId,
          'Existing reference'
        )
      ).toEqual([
        {
          referenceKind: 'ExternalReference',
          reference: 'Existing reference',
          label: 'publish tender-document version',
          requirementKey: 'SRC-006',
        },
      ]);
    }
  );

  it('does not enable a dialog action before a lifecycle action is selected', () => {
    expect(
      buildTenderDocumentLifecycleEvidence(
        null,
        attachedId,
        'Existing reference'
      )
    ).toEqual([]);
  });
});
