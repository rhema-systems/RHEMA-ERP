import type { ProcurementMethodType } from '@/types/procurement-policy';

type TenderPublicationSource = {
  tenderType?: string;
  sourcingCaseId?: string;
  sourcingMethod?: ProcurementMethodType | number;
  usesControlledTenderLifecycle?: boolean;
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
    tender.usesControlledTenderLifecycle ??
    (hasAdvancedSourcingCase &&
      Boolean(method && controlledPublicationMethods.has(method)));
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
      requiresControlledPublication &&
      (method === 'QualityBasedSelection' ||
        method === 'QualityAndCostBasedSelection')
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
      ? 'Publishing records the approved publication controls and opens bid submission until the published deadline. Access and participation follow the configured procurement route and eligibility requirements; invitations are notifications, not an open-tender audience restriction.'
      : 'Publishing opens bid submission until the published deadline. Access and participation follow the configured procurement route and eligibility requirements; invitations are notifications, not an open-tender audience restriction.',
  };
}

export function getTenderPublicationNotificationMessage(invitationCount: number, externalEmailCount = 0) {
  if (invitationCount === 0 && externalEmailCount === 0) return 'No individual invitation notifications are configured.';
  const recipients = [
    invitationCount > 0 ? `${invitationCount} invited supplier${invitationCount === 1 ? '' : 's'}` : '',
    externalEmailCount > 0 ? `${externalEmailCount} additional email recipient${externalEmailCount === 1 ? '' : 's'}` : '',
  ].filter(Boolean).join(' and ');
  return `Individual invitation notifications will be sent to ${recipients}.`;
}
