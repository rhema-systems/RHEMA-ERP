import type { TenderDocumentRequirement } from '@/services/tenderService';

export const isProposalDocumentType = (documentType: string) =>
  documentType === 'TechnicalProposal' || documentType === 'CommercialProposal';

export function getSupportingDocuments<T extends { documentType: string }>(documents?: T[] | null): T[] {
  return (documents ?? []).filter((document) => !isProposalDocumentType(document.documentType));
}

// null means unavailable/invalid, not an empty set of tender requirements.
export function getSupportingDocumentRequirements(
  tender: { requiredDocuments?: string | null } | null,
): TenderDocumentRequirement[] | null {
  try {
    if (!tender) return null;
    const parsed: unknown = tender.requiredDocuments ? JSON.parse(tender.requiredDocuments) : [];
    if (!Array.isArray(parsed) || !parsed.every((item) => item &&
      typeof item.documentType === 'string' && typeof item.documentName === 'string' &&
      typeof item.isRequired === 'boolean')) return null;
    return getSupportingDocuments(parsed);
  } catch {
    return null;
  }
}
