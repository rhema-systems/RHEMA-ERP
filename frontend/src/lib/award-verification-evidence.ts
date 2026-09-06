type EvidenceItem = {
  isRequired: boolean;
  requiresDocument?: boolean;
  comments: string;
  documents: unknown[];
};

/** Pre-save UX validation; the server validates retained evidence and reviewer attribution. */
export function hasAwardVerificationEvidence(item: EvidenceItem): boolean {
  if (!item.isRequired) return true;
  return item.requiresDocument
    ? item.documents.length > 0
    : item.documents.length > 0 || item.comments.trim().length > 0;
}
