type EvidenceItem = {
  isRequired: boolean;
  requiresDocument?: boolean;
  comments: string;
  documents: unknown[];
};

/** Comments are optional. The server retains reviewer attribution and checks required documents. */
export function hasAwardVerificationEvidence(item: EvidenceItem): boolean {
  return !item.isRequired || !item.requiresDocument || item.documents.length > 0;
}
