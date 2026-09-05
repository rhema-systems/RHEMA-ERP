import type { ProcurementMethodType } from '@/types/procurement-policy';

type TenderPublicationSource = {
  tenderType?: string;
  sourcingCaseId?: string;
  sourcingMethod?: ProcurementMethodType | number;
};

const legacyMethodNames: Record<number, ProcurementMethodType> = {
  0: 'RequestForQuotation',
  1: 'NationalCompetitiveTendering',
  2: 'InternationalCompetitiveTendering',
  3: 'RestrictedTendering',
  4: 'SingleSource',
  5: 'PettyPurchase',
  6: 'FrameworkCallOff',
  7: 'QualityBasedSelection',
  8: 'QualityAndCostBasedSelection',
};

const controlledPublicationMethods = new Set<ProcurementMethodType>([
  'NationalCompetitiveTendering',
  'InternationalCompetitiveTendering',
  'QualityBasedSelection',
  'QualityAndCostBasedSelection',
]);

const statutoryAdvertisementMethods = new Set<ProcurementMethodType>([
  'NationalCompetitiveTendering',
  'InternationalCompetitiveTendering',
]);

const exceptionalMethods = new Set<ProcurementMethodType>([
  'RestrictedTendering',
  'SingleSource',
  'PettyPurchase',
]);

function normalizeMethod(
  value?: ProcurementMethodType | number
): ProcurementMethodType | undefined {
  if (typeof value === 'number') return legacyMethodNames[value];
  return value;
}

export function getTenderPublicationPresentation(
  tender: TenderPublicationSource
) {
  const method = normalizeMethod(tender.sourcingMethod);
  const hasAdvancedSourcingCase = Boolean(tender.sourcingCaseId?.trim());
  const requiresControlledPublication =
    hasAdvancedSourcingCase &&
    Boolean(method && controlledPublicationMethods.has(method));
  const usesStatutoryAdvertisement =
    requiresControlledPublication &&
    Boolean(method && statutoryAdvertisementMethods.has(method));
  const usesExceptionalControl =
    hasAdvancedSourcingCase &&
    Boolean(method && exceptionalMethods.has(method));

  return {
    method,
    hasAdvancedSourcingCase,
    requiresControlledPublication,
    usesStatutoryAdvertisement,
    usesExceptionalControl,
    controlledPublicationHeading: usesStatutoryAdvertisement
      ? 'Statutory advertisement and approved tender documents'
      : 'Controlled solicitation and approved documents',
    publicationReferenceLabel: usesStatutoryAdvertisement
      ? 'Advertisement reference'
      : 'Solicitation reference',
    publicationEvidenceLabel: usesStatutoryAdvertisement
      ? 'Advertisement evidence'
      : 'Publication evidence',
    advancedControlLabel:
      method === 'QualityBasedSelection' ||
      method === 'QualityAndCostBasedSelection'
        ? 'QBS / QCBS Controls'
        : requiresControlledPublication
          ? 'NCT / ICT Controls'
          : undefined,
    exceptionalControlLabel:
      method === 'PettyPurchase'
        ? 'Petty Purchase Control'
        : usesExceptionalControl
          ? 'Restricted / Single Source'
          : undefined,
    supplierAccessMessage: requiresControlledPublication
      ? 'Publishing records the approved publication controls, releases the tender to its authorised supplier audience, and opens secure bid submission until the published deadline.'
      : 'Publishing releases this invitation to the selected suppliers through the secure supplier portal and opens bid submission until the published deadline.',
  };
}
