import type {
  ProcurementTenderDocumentEvidenceReference,
  ProcurementTenderDocumentLifecycleRequest,
  ProcurementTenderDocumentTemplate,
} from '@/types/procurement-tender-document';

export type TenderDocumentLifecycleAction =
  | 'submit'
  | 'publish'
  | 'reject'
  | 'retire';

export function usesAttachedPublicationEvidence(
  action: TenderDocumentLifecycleAction | null,
  attachedEvidenceId?: string
) {
  return action === 'publish' && Boolean(attachedEvidenceId?.trim());
}

export function buildTenderDocumentLifecycleEvidence(
  action: TenderDocumentLifecycleAction | null,
  attachedEvidenceId?: string,
  externalReference = ''
): ProcurementTenderDocumentEvidenceReference[] {
  if (!action) return [];
  const evidence: ProcurementTenderDocumentEvidenceReference[] = [];
  const attachedReferenceId = attachedEvidenceId?.trim();
  if (action === 'publish' && attachedReferenceId) {
    evidence.push({
      referenceKind: 'WorkflowEvidenceDocument',
      referenceId: attachedReferenceId,
      label: 'Attached tender document used as publication evidence',
      requirementKey: 'SRC-006',
    });
  }
  if (externalReference.trim()) {
    evidence.push({
      referenceKind: 'ExternalReference',
      reference: externalReference.trim(),
      label: `${action} tender-document version`,
      requirementKey: 'SRC-006',
    });
  }
  return evidence;
}

export function buildTenderDocumentLifecycleRequest(
  action: TenderDocumentLifecycleAction,
  persistedTemplate: Pick<
    ProcurementTenderDocumentTemplate,
    'rowVersion' | 'contentWorkflowEvidenceDocumentId'
  >,
  comment: string,
  externalReference: string
): ProcurementTenderDocumentLifecycleRequest {
  const evidence = buildTenderDocumentLifecycleEvidence(
    action,
    persistedTemplate.contentWorkflowEvidenceDocumentId,
    externalReference
  );
  if (!evidence.length)
    throw new Error('Shared approval evidence reference is required.');
  return {
    rowVersion: persistedTemplate.rowVersion,
    comment: comment.trim() || undefined,
    evidence,
  };
}
