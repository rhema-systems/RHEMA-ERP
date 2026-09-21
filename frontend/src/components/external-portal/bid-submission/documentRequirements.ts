import type { TenderDocumentRequirement } from '@/services/tenderService';

export function parseBidDocumentRequirements(value?: string | null): TenderDocumentRequirement[] {
  if (!value?.trim()) return [];
  const error = 'Unable to read this tender’s document requirements. Please contact procurement.';
  let requirements: unknown;
  try { requirements = JSON.parse(value); } catch { throw new Error(error); }
  if (!Array.isArray(requirements) || requirements.some(requirement =>
    !requirement || typeof requirement.documentType !== 'string' || !requirement.documentType.trim() ||
    typeof requirement.documentName !== 'string' || !requirement.documentName.trim() ||
    typeof requirement.isRequired !== 'boolean')) throw new Error(error);
  return requirements;
}
